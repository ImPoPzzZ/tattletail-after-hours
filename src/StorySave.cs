using HarmonyLib;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Keeps the STORY's save point safe from the mod's own modes. The hub,
    /// Endless and Papa's 10th Birthday! all load Night 6, and the game's
    /// GameManager.PlayLevel() saves whatever level it loads as "where you
    /// left off" — so without this, Continue would always drop you into
    /// Night 6. Mod-mode level loads skip that save, and the menu's Continue
    /// always gets the real story level back.
    /// </summary>
    public static class StorySave
    {
        public static LevelEnum ContinueLevel = LevelEnum.None;
        public static bool Known;

        /// <summary>The Kaleidoscope DLC's levels (the game's "Memories").</summary>
        public static bool IsMemory(LevelEnum level)
        {
            return level.ToString().StartsWith("_Memory_");
        }

        /// <summary>True while one of the mod's own modes is launching or running.</summary>
        public static bool ModOwnsLevel
        {
            get { return EndlessRunManager.PendingEndlessRun || SceneMonitor.IsEndlessActive || HubMode.Active || HubMode.Pending; }
        }
    }

    [HarmonyPatch(typeof(GD), "SaveLevel")]
    public static class Patch_GD_SaveLevel_StoryOnly
    {
        static bool Prefix()
        {
            if (StorySave.ModOwnsLevel) return false; // don't let a mod mode become your story save point
            if (StorySave.IsMemory(GD.currentLevel)) return false; // the Kaleidoscope DLC isn't the story either
            StorySave.ContinueLevel = GD.currentLevel;
            StorySave.Known = true;
            return true;
        }
    }

    [HarmonyPatch(typeof(MainMenu), "Start")]
    public static class Patch_MainMenu_Start_StorySave
    {
        static void Prefix()
        {
            if (!StorySave.Known)
            {
                // First menu of this session: whatever the game loaded from the save is the story point.
                StorySave.ContinueLevel = GD.currentLevel;
                StorySave.Known = true;
            }
            else
            {
                // Coming back from a mod mode: undo its in-memory Night 6 so Continue is right.
                GD.currentLevel = StorySave.ContinueLevel;
            }
        }
    }
}
