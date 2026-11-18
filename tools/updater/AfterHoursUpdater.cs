using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KaleidoscopeEndless;
using Mono.Cecil;

namespace AfterHoursUpdater
{
    /// <summary>
    /// BepInEx preloader patcher (BepInEx\patchers\AfterHoursUpdater.dll). BepInEx runs it before any plugin is loaded, so the mod's own files are
    /// not locked: if the mod staged an update (BepInEx\AfterHoursUpdate with a ready.txt) it is checked again and copied over the mod's folder.
    ///
    /// Checks, again, because this is the part that writes files: the manifest's signature must be the owner's, every staged file must match its
    /// hash and size, and a file may only be written inside BepInEx\plugins\KaleidoscopeEndless (UpdateCore.IsSafePath). Anything wrong: nothing is
    /// touched. A failed copy is retried at the next start (three tries), then the staged update is dropped.
    /// Old-Mono safe: plain File/Directory calls only. Writes BepInEx\afterhours_update.log.
    /// </summary>
    public static class Patcher
    {
        // BepInEx needs these two to treat this DLL as a patcher.
        public static IEnumerable<string> TargetDLLs { get { return new[] { "Assembly-CSharp.dll" }; } }
        public static void Patch(AssemblyDefinition assembly) { ApplyOnce(); }

        // (BepInEx 5.4 also calls Initialize before any patching: the earliest moment.)
        public static void Initialize() { ApplyOnce(); }

        private static bool _done;

        private static string BepInExDir
        {
            get { return Directory.GetParent(Path.GetDirectoryName(typeof(Patcher).Assembly.Location)).FullName; }   // ...\BepInEx\patchers -> ...\BepInEx
        }

        private static void Log(string s)
        {
            try { File.AppendAllText(Path.Combine(BepInExDir, "afterhours_update.log"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + s + "\n"); } catch { }
        }

        private static void ApplyOnce()
        {
            if (_done) return;
            _done = true;
            try { Apply(); }
            catch (Exception e) { Log("The updater failed: " + e.Message); }
        }

        private static void Apply()
        {
            string bep = BepInExDir;
            string staging = Path.Combine(bep, "AfterHoursUpdate");
            string ready = Path.Combine(staging, "ready.txt");
            if (!File.Exists(ready)) return;

            string version = File.ReadAllText(ready).Trim();
            string pluginDir = Path.Combine(Path.Combine(bep, "plugins"), "KaleidoscopeEndless");
            string attemptsFile = Path.Combine(staging, "attempts.txt");

            int attempts = 0;
            try { if (File.Exists(attemptsFile)) int.TryParse(File.ReadAllText(attemptsFile).Trim(), out attempts); } catch { }
            if (attempts >= 3) { Log("Update " + version + " failed three times: dropped."); Drop(staging); return; }
            try { File.WriteAllText(attemptsFile, (attempts + 1).ToString()); } catch { }

            // 1. The manifest is the owner's, and says this version.
            byte[] manifestBytes = File.ReadAllBytes(Path.Combine(staging, "manifest.txt"));
            string sig = File.ReadAllText(Path.Combine(staging, "manifest.sig"));
            if (!UpdateCore.VerifyOwner(manifestBytes, sig)) { Log("The staged manifest is not signed by the owner: dropped."); Drop(staging); return; }
            UpdateCore.Manifest m = UpdateCore.Parse(Encoding.UTF8.GetString(manifestBytes));
            if (m == null || m.Version != version) { Log("The staged manifest is not valid: dropped."); Drop(staging); return; }

            // 2. Every staged file is what the manifest says. (Files the mod did not need to download are not in staging: they are unchanged.)
            string stagedDir = Path.Combine(staging, m.Version);
            var toCopy = new List<UpdateCore.Entry>();
            foreach (var e in m.Files)
            {
                string src = Path.Combine(stagedDir, e.Path.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(src)) continue;
                if (new FileInfo(src).Length != e.Size || UpdateCore.Sha256HexOfFile(src) != e.Sha256) { Log(e.Path + " is damaged: dropped."); Drop(staging); return; }
                toCopy.Add(e);
            }
            if (toCopy.Count == 0) { Log("Nothing staged for " + m.Version + "."); Drop(staging); return; }

            // 3. Copy each file in: to a temporary name next to it, then swap.
            string root = Path.GetFullPath(pluginDir);
            foreach (var e in toCopy)
            {
                string src = Path.Combine(stagedDir, e.Path.Replace('/', Path.DirectorySeparatorChar));
                string dest = Path.GetFullPath(Path.Combine(pluginDir, e.Path.Replace('/', Path.DirectorySeparatorChar)));
                if (!dest.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) { Log(e.Path + " is outside the mod's folder: dropped."); Drop(staging); return; }
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                string tmp = dest + ".new";
                File.Copy(src, tmp, true);
                if (UpdateCore.Sha256HexOfFile(tmp) != e.Sha256) { Log(e.Path + " changed while copying: will retry."); File.Delete(tmp); return; }
                if (File.Exists(dest)) File.Delete(dest);
                File.Move(tmp, dest);
                Log("Updated " + e.Path + ".");
            }

            File.WriteAllText(Path.Combine(bep, "afterhours_updated.txt"), m.Version);   // (the mod tells the player once it is running)
            Drop(staging);
            Log("Updated to " + m.Version + ".");
        }

        private static void Drop(string staging)
        {
            try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { }
        }
    }
}
