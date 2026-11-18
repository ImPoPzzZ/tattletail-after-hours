using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using System.Reflection;

namespace KaleidoscopeEndless
{
    [BepInPlugin("com.kaleidoscope.endless", "Tattletail: After Hours", "4.0.0")] // keep in step with VersionTag.VERSION. The id (and the DLL's name) stay from the old name "Kaleidoscope Endless": changing them would leave players with two copies of the mod after updating.
    public class KaleidoscopeEndlessPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static KaleidoscopeEndlessPlugin Instance;

        /// <summary>
        /// The E-key location markers (arcade: HubMarkTool, Endless:
        /// DebugLocationMarker). Off unless turned on in
        /// BepInEx\config\com.kaleidoscope.endless.cfg — the release zip
        /// doesn't ship that file, so players never get them.
        /// </summary>
        internal static bool DevTools;
        internal static bool RecalledGraphics = true;

        private void Awake()
        {
            Instance = this;
            Log = base.Logger;
            DevTools = Config.Bind("Developer", "DevTools", false, "E-key location markers in the arcade and in Endless runs. For mod development only.").Value;
            AutoUpdate.Enabled = Config.Bind("Updates", "AutoUpdate", true, "Check for a newer version at start-up and download it (it installs the next time you start the game). Set to false to turn it off.").Value;
            AutoUpdate.AutoRestart = Config.Bind("Updates", "AutoRestart", true, "After an update was downloaded, restart the game by itself so it installs before you play (Windows only). Set to false to restart it yourself.").Value;
            AutoUpdate.ManifestUrl = Config.Bind("Updates", "ManifestUrl", "", "Leave empty. (For testing: another place to read the update list from.)").Value;
            RecalledGraphics = Config.Bind("Cosmetics", "RecalledGraphics", true, "Recalled's night sky, drifting motes and fireflies, and color grade. Turn off if it costs you frames.").Value;

            try { Assembly.GetExecutingAssembly().GetTypes(); }
            catch (ReflectionTypeLoadException ex)
            {
                foreach (var le in ex.LoaderExceptions)
                    if (le != null) Log.LogError("LoaderException: " + le.Message);
            }

            // Was unprotected until now — a single bad patch (an unrelated
            // jump-disable attempt, since reverted) threw during PatchAll()
            // and silently killed this entire Awake(), including
            // SceneMonitor.Init() below, with no obvious link between
            // symptom (no main menu button) and cause. Never leave this
            // uncaught again.
            try
            {
                var harmony = new Harmony("com.kaleidoscope.endless");
                harmony.PatchAll();
            }
            catch (System.Exception e)
            {
                Log.LogError("KE: harmony.PatchAll() failed: " + e.Message + "\n" + e.StackTrace);
            }

            SceneMonitor.Init();
            try { AutoUpdate.Begin(); }
            catch (System.Exception e) { Log.LogError("KE: AutoUpdate.Begin failed: " + e.Message); }

            // Saves live in Documents\Tattletail After Hours\ — must run before
            // anything reads a save, so old ones get moved over first.
            try { SaveFiles.Init(); }
            catch (System.Exception e) { Log.LogError("KE: SaveFiles.Init failed: " + e.Message); }

            OptionsMenu.ApplySavedVolume();
            BlurFilter.UndoOldMotionBlurChange();

            // Creates Shop.txt on the very first launch (and migrates the
            // old Shards.txt/Gold.txt/Diamond.txt files) instead of waiting
            // for someone to open the shop.
            try { Shop.Init(); }
            catch (System.Exception e) { Log.LogError("KE: Shop.Init failed: " + e.Message); }

            // Persistent for the whole game process — unlike EndlessRunManager/
            // MamaController (recreated per Endless run from SceneBootstrap),
            // the lobby needs to exist starting at the main menu, before any
            // run begins.
            try
            {
                new GameObject("KE_MpLobby").AddComponent<MpLobby>();
            }
            catch (System.Exception e)
            {
                Log.LogError("KE: MpLobby bootstrap failed: " + e.Message + "\n" + e.StackTrace);
            }

            string stamp = "?";
            try { stamp = System.IO.File.GetLastWriteTime(Assembly.GetExecutingAssembly().Location).ToString("yyyy-MM-dd HH:mm:ss"); } catch { }
            Log.LogInfo("Tattletail: After Hours loaded. [build " + stamp + "]");
        }

        private int _updateErrorCount;

        private void Update()
        {
            try
            {
                // The old main menu (Endless/Papa's/HUB buttons and their
                // submenus) is no longer built — the arcade hub replaces it
                // (see Patch_MainMenu_Start_GoToHub).
                EndlessRunManager.Instance?.Tick();
                BlurFilter.Tick();
                VersusNet.Tick();
                VersusNet.TickMamaGlance(); // the survivor's flashlight flickers while they look at Mama (the game's own Mama does that; the Versus one is a body)
                KidVoice.Tick(); // the kid's reactions to the AI Mama (Endless)
                SceneMonitor.CheckScene();
            }
            catch (System.Exception e)
            {
                // Log every crash, but cap the spam so a per-frame exception
                // doesn't flood the log file into gigabytes.
                _updateErrorCount++;
                if (_updateErrorCount <= 20 || _updateErrorCount % 200 == 0)
                {
                    Log.LogError("KE: Update() crashed (#" + _updateErrorCount + "): " +
                        e.Message + "\n" + e.StackTrace);
                }
            }
        }
    }
}
