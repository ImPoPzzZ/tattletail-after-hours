using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Networking;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Automatic updates. The moment the game starts the mod asks the public releases repo for its signed manifest (latest.txt and
    /// latest.txt.sig). If a newer version is out it downloads only the files that changed (checked by SHA-256) into
    /// BepInEx\AfterHoursUpdate\ and the updater (BepInEx\patchers\AfterHoursUpdater.dll, see tools/updater) swaps the files in on the next
    /// start, before the mod loads (Windows will not let a running DLL be replaced). The TITLE SCREEN does not go on to the Arcade until this is
    /// finished (GateAtTitle): it shows "Checking for updates..." and the download's progress, and after a download restarts the game by itself
    /// (not under Wine/Proton, where the player is asked to restart it).
    ///
    /// Safety: the manifest must carry a valid signature from the owner's key (UpdateKey), every file must match its hash and size, and
    /// a file may only land inside the mod's own folder. Off with the config key Updates/AutoUpdate = false. Never runs in a dev build (DevTools).
    /// </summary>
    public class AutoUpdate : MonoBehaviour
    {
        // The PUBLIC releases repo, branch "public": only what is pushed to that branch is ever offered to players (dev work and early builds never are).
        private const string DEFAULT_URL = "https://raw.githubusercontent.com/ImPoPzzZ/after-hours-releases/public/latest.txt";
        private const float CHECK_TIMEOUT = 15f;      // (the little files: the manifest and its signature)
        private const float DOWNLOAD_TIMEOUT = 180f;  // (per file)

        public static bool Enabled = true;
        public static bool AutoRestart = true;
        public static string ManifestUrl = "";

        /// <summary>True from the start of the game until the check (and any download) is over, whatever the result. The title screen waits on it.</summary>
        public static bool Busy { get; private set; }
        private static float _beat;   // last sign of life of the update coroutine (unscaled time)
        private static string _restartVersion;  // set when a new version was just downloaded

        private class Req { public byte[] Data; public bool Ok; public string Error; }

        private static string _message;
        private static bool _messageShown;

        public static void Begin()
        {
            var go = new GameObject("KE_AutoUpdate");
            DontDestroyOnLoad(go);
            go.AddComponent<AutoUpdate>();
            go.AddComponent<UpdateIndicator>();
            Busy = Enabled && !KaleidoscopeEndlessPlugin.DevTools && !VersionTag.PRIVATE_BUILD;   // (the title screen holds from the very first frame)
        }

        // ── Where things are ─────────────────────────────────────────────────
        private static string PluginDir { get { return Path.GetDirectoryName(typeof(AutoUpdate).Assembly.Location); } }
        private static string BepInExDir { get { return Directory.GetParent(Directory.GetParent(PluginDir).FullName).FullName; } }
        private static string Staging { get { return Path.Combine(BepInExDir, "AfterHoursUpdate"); } }
        private static string UpdaterDll { get { return Path.Combine(Path.Combine(BepInExDir, "patchers"), "AfterHoursUpdater.dll"); } }

        private void Start()
        {
            StartCoroutine(Run());
            StartCoroutine(ShowMessageWhenInArcade());
        }

        private static void Log(string s) { KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Update] " + s); }

        /// <summary>Runs the whole update and always ends with Busy = false (even if something throws).</summary>
        private IEnumerator Run()
        {
            Busy = true;
            _beat = Time.unscaledTime;
            IEnumerator it = RunCore();
            while (true)
            {
                _beat = Time.unscaledTime;
                bool more;
                try { more = it.MoveNext(); }
                catch (Exception e) { Log("The update check failed: " + e.Message); UpdateIndicator.Set("Could not check for updates", 4f); break; }
                if (!more) break;
                yield return it.Current;
            }
            Busy = false;
        }

        private IEnumerator RunCore()
        {
            // (an update that was just applied by the updater at this start)
            try
            {
                string done = Path.Combine(BepInExDir, "afterhours_updated.txt");
                if (File.Exists(done))
                {
                    string v = File.ReadAllText(done).Trim();
                    File.Delete(done);
                    _message = "Tattletail: After Hours was updated to " + v + ".";
                    Log("Updated to " + v + ".");
                    UpdateIndicator.Set("Updated to " + v, 6f);
                }
            }
            catch (Exception e) { Log("Could not read the update note: " + e.Message); }

            if (!Enabled) { Log("Automatic updates are off (Updates/AutoUpdate)."); yield break; }
            if (KaleidoscopeEndlessPlugin.DevTools) { Log("Dev build: no automatic updates."); yield break; }
            if (VersionTag.PRIVATE_BUILD) { Log("Private build " + VersionTag.VERSION + ": no automatic updates (it never goes back to the public version)."); yield break; }
            if (!File.Exists(UpdaterDll)) { Log("The updater is not installed (run the installer once): no automatic updates."); yield break; }

            UpdateIndicator.Set("Checking for updates...", 0f);

            // An update that is already waiting for a restart.
            string pendingVersion = null;
            try { pendingVersion = PendingVersion(); }
            catch (Exception e) { Log("Could not read the staged update: " + e.Message); }
            if (pendingVersion != null)
            {
                if (UpdateCore.CompareVersions(pendingVersion, VersionTag.VERSION) > 0)
                {
                    _message = "Update " + pendingVersion + " is downloaded. Restart the game to install it.";
                    Log("Update " + pendingVersion + " is already waiting for a restart.");
                    UpdateIndicator.Set("Update " + pendingVersion + " is downloaded. Restart the game to install it.", 15f);
                    yield break;
                }
                try { Directory.Delete(Staging, true); } catch { }   // (it is not newer than what runs now: stale)
            }

            string url = string.IsNullOrEmpty(ManifestUrl) ? DEFAULT_URL : ManifestUrl;
            var man = new Req();
            yield return Get(url, man, null, CHECK_TIMEOUT);
            if (!man.Ok) { Log("No update check (" + man.Error + ")."); UpdateIndicator.Set("Could not check for updates", 5f); yield break; }
            var sigReq = new Req();
            yield return Get(url + ".sig", sigReq, null, CHECK_TIMEOUT);
            if (!sigReq.Ok) { Log("No signature (" + sigReq.Error + "): update refused."); UpdateIndicator.Set("Could not check for updates", 5f); yield break; }

            if (!UpdateCore.VerifyOwner(man.Data, Encoding.UTF8.GetString(sigReq.Data)))
            {
                Log("The manifest's signature is not the owner's: update refused (" + UpdateCore.LastError + ").");
                UpdateIndicator.Set("Update refused (not signed)", 6f);
                yield break;
            }
            UpdateCore.Manifest m = UpdateCore.Parse(Encoding.UTF8.GetString(man.Data));
            if (m == null) { Log("The manifest is not valid: update refused."); UpdateIndicator.Set("Update refused", 6f); yield break; }
            if (UpdateCore.CompareVersions(m.Version, VersionTag.VERSION) <= 0) { Log("Up to date (" + VersionTag.VERSION + ")."); UpdateIndicator.Set("Up to date (" + VersionTag.VERSION + ")", 3f); yield break; }

            Log("Version " + m.Version + " is out (this is " + VersionTag.VERSION + "). Checking what changed...");

            // Only what differs from the installed files.
            var todo = new System.Collections.Generic.List<UpdateCore.Entry>();
            foreach (var e in m.Files)
            {
                string local = Path.Combine(PluginDir, e.Path.Replace('/', Path.DirectorySeparatorChar));
                bool same = false;
                try { same = File.Exists(local) && new FileInfo(local).Length == e.Size && UpdateCore.Sha256HexOfFile(local) == e.Sha256; }
                catch (Exception) { }
                if (!same) todo.Add(e);
            }
            if (todo.Count == 0) { Log("Nothing to download."); UpdateIndicator.Set("Up to date (" + VersionTag.VERSION + ")", 3f); yield break; }

            string dir = Path.Combine(Staging, m.Version);
            try { if (Directory.Exists(Staging)) Directory.Delete(Staging, true); Directory.CreateDirectory(dir); }
            catch (Exception e) { Log("Cannot prepare the staging folder: " + e.Message); yield break; }

            for (int i = 0; i < todo.Count; i++)
            {
                var e = todo[i];
                var r = new Req();
                yield return Get(m.Base + e.Asset, r, "Updating to " + m.Version + "  -  file " + (i + 1) + " of " + todo.Count, DOWNLOAD_TIMEOUT, i, todo.Count);
                if (!r.Ok) { Log("Download failed for " + e.Asset + " (" + r.Error + "). Will try again next start."); UpdateIndicator.Set("Update download failed (it will try again next time)", 6f); Cleanup(); yield break; }
                if (r.Data.Length != e.Size || UpdateCore.Sha256Hex(r.Data) != e.Sha256)
                {
                    Log(e.Asset + " does not match its hash: update refused.");
                    UpdateIndicator.Set("Update refused (a file did not match)", 6f);
                    Cleanup();
                    yield break;
                }
                try
                {
                    string target = Path.Combine(dir, e.Path.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.WriteAllBytes(target, r.Data);
                }
                catch (Exception ex) { Log("Cannot save " + e.Asset + ": " + ex.Message); Cleanup(); yield break; }
                Log("Downloaded " + e.Asset + " (" + (i + 1) + "/" + todo.Count + ").");
                r.Data = null;
            }

            // Everything is in and checked: the updater may apply it at the next start.
            try
            {
                File.WriteAllBytes(Path.Combine(Staging, "manifest.txt"), man.Data);
                File.WriteAllText(Path.Combine(Staging, "manifest.sig"), Encoding.UTF8.GetString(sigReq.Data).Trim());
                File.WriteAllText(Path.Combine(Staging, "ready.txt"), m.Version);
            }
            catch (Exception ex) { Log("Cannot finish the staged update: " + ex.Message); Cleanup(); yield break; }

            _message = "Update " + m.Version + " is downloaded. Restart the game to install it.";
            Log("Update " + m.Version + " is ready. It installs on the next start.");
            _restartVersion = m.Version;
            UpdateIndicator.Set("Update " + m.Version + " is downloaded. Restart the game to install it.", 15f);
        }

        /// <summary>
        /// The title screen calls this (as a coroutine) before it loads the Arcade: it holds until the check and any download are over, and after a
        /// download restarts the game so the updater installs it before the title screen comes back. Nothing here can hold the game for ever: every
        /// request has a time limit, and a failed or refused update just lets the game go on.
        /// </summary>
        public static IEnumerator GateAtTitle()
        {
            while (Busy)
            {
                // (the update coroutine died without finishing: no sign of life for 30 s -> let the game go on)
                if (Time.unscaledTime - _beat > 30f) { Log("The update check stopped answering: going on."); Busy = false; break; }
                LoadingCover.Show(null);   // (keeps the cover up past its own 45 s limit; the indicator draws on top of it)
                yield return null;
            }
            string version = _restartVersion;
            if (version == null) yield break;
            _restartVersion = null;   // (the next menu visit does not wait again)

            if (!AutoRestart || UnderWine())
            {
                UpdateIndicator.Set("Update " + version + " is downloaded. Restart the game to install it.", 15f);
                float t = 0f;
                while (t < 4f) { LoadingCover.Show(null); t += Time.unscaledDeltaTime; yield return null; }
                yield break;   // (it cannot restart itself here: the player goes on and restarts when they like)
            }

            UpdateIndicator.Set("Update " + version + " is ready. Restarting the game to install it...", 0f);
            UpdateIndicator.SetProgress(1f);
            float w = 0f;
            while (w < 2.5f) { LoadingCover.Show(null); w += Time.unscaledDeltaTime; yield return null; }
            if (Restart(version)) { while (true) { LoadingCover.Show(null); yield return null; } }   // (the game is closing)
            UpdateIndicator.Set("Update " + version + " is downloaded. Restart the game to install it.", 15f);
        }

        /// <summary>Wine / Proton (Linux, Steam Deck): the game cannot start itself again from inside the prefix.</summary>
        private static bool UnderWine()
        {
            try
            {
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WINEPREFIX"))) return true;
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("STEAM_COMPAT_DATA_PATH"))) return true;
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WINELOADER"))) return true;
                if (Directory.Exists("Z:\\proc") || Directory.Exists("Z:\\usr")) return true;
            }
            catch (Exception) { }
            return false;
        }

        /// <summary>Starts the game again a few seconds from now (Steam copy: through Steam; any other copy: the exe) and quits this one.</summary>
        private static bool Restart(string version)
        {
            try
            {
                string exe = Process.GetCurrentProcess().MainModule.FileName;
                bool steam = exe.ToLowerInvariant().IndexOf("steamapps", StringComparison.Ordinal) >= 0;
                string target = steam ? "steam://rungameid/568090" : exe;
                var psi = new ProcessStartInfo("cmd.exe", "/c ping -n 6 127.0.0.1 >nul & start \"\" \"" + target + "\"");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WorkingDirectory = Path.GetDirectoryName(exe);
                Process.Start(psi);
                Log("Restarting the game (" + (steam ? "through Steam" : "the exe") + ") to install " + version + ".");
                Application.Quit();
                return true;
            }
            catch (Exception e)
            {
                Log("Could not restart the game by itself: " + e.Message);
                return false;
            }
        }

        private static string PendingVersion()
        {
            string ready = Path.Combine(Staging, "ready.txt");
            return File.Exists(ready) ? File.ReadAllText(ready).Trim() : null;
        }

        private static void Cleanup()
        {
            try { if (Directory.Exists(Staging)) Directory.Delete(Staging, true); } catch { }
        }

        private static IEnumerator Get(string url, Req r, string label, float timeout, int fileIndex = 0, int fileCount = 1)
        {
            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                AsyncOperation op = null;
                try { op = www.Send(); }   // (Unity refuses a custom User-Agent: it sends its own)
                catch (Exception ex) { r.Error = ex.Message; }
                if (op == null) yield break;
                float waited = 0f;
                while (!op.isDone && waited < timeout)   // (this Unity has no timeout property)
                {
                    if (label != null)
                    {
                        UpdateIndicator.Set(label + "   " + Mathf.FloorToInt(www.downloadProgress * 100f) + "%", 0f);
                        UpdateIndicator.SetProgress((fileIndex + www.downloadProgress) / Mathf.Max(1, fileCount));
                    }
                    yield return null;
                    waited += Time.unscaledDeltaTime;
                    _beat = Time.unscaledTime;
                }
                if (!op.isDone) { www.Abort(); r.Error = "timed out"; yield break; }
                if (www.isError) { r.Error = www.error; yield break; }
                if (www.responseCode != 200) { r.Error = "HTTP " + www.responseCode; yield break; }
                r.Data = www.downloadHandler.data;
                r.Ok = r.Data != null && r.Data.Length > 0;
                if (!r.Ok) r.Error = "empty answer";
            }
        }

        /// <summary>The note ("Update 3.9 is downloaded...") shows once, on the Arcade's top line, whenever you are next in the Arcade.</summary>
        private IEnumerator ShowMessageWhenInArcade()
        {
            while (true)
            {
                yield return new WaitForSeconds(2f);
                if (_message == null || _messageShown) continue;
                if (HubMode.Active && HubController.Instance != null && HubController.Instance.Built && !OverlayPanel.AnyOpen)
                {
                    HubMode.SetHudText(_message);
                    _messageShown = true;
                    // (a second note may follow later: a download that finishes after the first one was shown)
                    yield return new WaitForSeconds(30f);
                    _message = null;
                    _messageShown = false;
                }
            }
        }
    }
}
