using HarmonyLib;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// The level title card shown when an Endless run starts. Each run start
    /// first rolls 10% for "Lucky Night" (every shard payout that run is
    /// doubled); a Lucky Night takes priority over everything else below and
    /// keeps whatever skins the player picked. Otherwise it normally reads
    /// "Endless Night", or "Kaleidoscope Night" with both Kaleidoscope skins
    /// active (Voice + Mama), and rolls a 1% flip: a player with both skins
    /// gets "Endless Night" with the normal skins instead, and anyone else
    /// gets a "Kaleidoscope Night" with both Kaleidoscope skins forced on.
    ///
    /// Only ever touches the title while an Endless run is active — the
    /// LevelData asset's original text is restored as soon as the main menu
    /// loads again, so the real story night keeps its own title.
    /// </summary>
    public static class EndlessTitle
    {
        public const string ENDLESS = "Endless Night";
        public const string KALEIDOSCOPE = "Kaleidoscope Night";
        public const string LUCKY = "Lucky Night";
        public const string BIRTHDAY = "Papa's 10th Birthday!"; // the separate free-roam mode
        private const float LUCKY_CHANCE = 0.10f;

        /// <summary>True when this run rolled Lucky Night (read by EndlessRunManager.StartRun).</summary>
        public static bool PendingLucky;
        private const float FLIP_CHANCE = 0.01f;

        /// <summary>Title picked for the run about to start (set by RollNight at launch).</summary>
        public static string PendingTitle = ENDLESS;

        private static LevelData _data;
        private static string _origOne;
        private static string _origTwo;
        private static bool _swapped;

        /// <summary>Call at launch, right after the player's own skin picks are copied into the Pending* fields — may flip them and picks the title.</summary>
        public static void RollNight()
        {
            // Lucky Night has priority: no 1% flip is rolled at all.
            PendingLucky = Random.value < LUCKY_CHANCE;
            if (PendingLucky)
            {
                PendingTitle = LUCKY;
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Title] Lucky Night — shards doubled this run.");
                return;
            }

            bool voice = EndlessRunManager.PendingKaleidoscopeVoice;
            bool mama = EndlessRunManager.PendingKaleidoscopeMama;
            bool both = voice && mama;
            bool flip = Random.value < FLIP_CHANCE;

            bool kaleidoscopeNight = flip ? !both : both;
            if (flip)
            {
                // The flipped night applies the matching skins to BOTH, not just what was picked.
                EndlessRunManager.PendingKaleidoscopeVoice = kaleidoscopeNight;
                EndlessRunManager.PendingKaleidoscopeMama = kaleidoscopeNight;
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Title] 1% flip — this run is '" + (kaleidoscopeNight ? KALEIDOSCOPE : ENDLESS) + "' with " + (kaleidoscopeNight ? "Kaleidoscope" : "normal") + " skins.");
            }
            PendingTitle = kaleidoscopeNight ? KALEIDOSCOPE : ENDLESS;
        }

        /// <summary>Swaps the title text in for this Endless run. Idempotent.</summary>
        public static void Apply()
        {
            if (_swapped) return;
            try
            {
                var data = Utils.GetLevelFlow().GetLevelByEnum(GD.currentLevel);
                if (data == null) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Title] no LevelData for current level — title unchanged."); return; }

                _data = data;
                _origOne = data.introLineOne;
                _origTwo = data.introLineTwo;
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Title] original intro lines: one='" + _origOne + "' two='" + _origTwo + "'");

                bool oneHasTitle = !string.IsNullOrEmpty(_origOne) && _origOne.ToLowerInvariant().Contains("christmas");
                bool twoHasTitle = !string.IsNullOrEmpty(_origTwo) && _origTwo.ToLowerInvariant().Contains("christmas");

                // Replace whichever line carries the night's name; if neither
                // mentions it, fall back to the second line (or the first if
                // there is no second).
                if (oneHasTitle) data.introLineOne = PendingTitle;
                else if (twoHasTitle) data.introLineTwo = PendingTitle;
                else if (!string.IsNullOrEmpty(_origTwo)) data.introLineTwo = PendingTitle;
                else data.introLineOne = PendingTitle;

                _swapped = true;
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Title] set to '" + PendingTitle + "'.");
            }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Title] Apply failed: " + e.Message);
            }
        }

        /// <summary>Puts the original text back — called whenever the main menu loads.</summary>
        public static void Restore()
        {
            if (!_swapped) return;
            try
            {
                if (_data != null)
                {
                    _data.introLineOne = _origOne;
                    _data.introLineTwo = _origTwo;
                }
            }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Title] Restore failed: " + e.Message);
            }
            _swapped = false;
            _data = null;
        }
    }

    // The title card is started by HUD.PlayTitle — swap the text in right
    // before it, only while an Endless run is active.
    [HarmonyPatch(typeof(HUD), "PlayTitle")]
    public static class Patch_HUD_PlayTitle_EndlessTitle
    {
        static void Prefix()
        {
            if (SceneMonitor.IsEndlessActive) EndlessTitle.Apply();
        }
    }
}
