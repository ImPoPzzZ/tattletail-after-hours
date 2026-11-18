using HarmonyLib;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Papa's 10th Birthday! has one permanent task on the HUD. Anything that tries
    /// to write the task line (the vanilla story, an interaction, a hint reset) gets
    /// this text instead, and clearing it just puts it back — only while that mode's
    /// level is loaded, so Endless and the story are untouched.
    /// </summary>
    public static class BirthdayTask
    {
        // The HUD adds the "TASK:" prefix itself.
        public const string TEXT = "Feed Papa & Keep the Children Happy";

        public static bool Active
        {
            get { return SceneMonitor.IsEndlessActive && EndlessRunManager.PendingBirthdayMode; }
        }
    }

    [HarmonyPatch(typeof(HUD), "SetNewQuest")]
    public static class Patch_HUD_SetNewQuest_BirthdayTask
    {
        static void Prefix(ref string newQuestText)
        {
            if (BirthdayTask.Active) newQuestText = BirthdayTask.TEXT;
        }
    }

    [HarmonyPatch(typeof(HUD), "ClearQuest")]
    public static class Patch_HUD_ClearQuest_BirthdayTask
    {
        static bool Prefix(HUD __instance)
        {
            if (!BirthdayTask.Active) return true;
            __instance.SetNewQuest(BirthdayTask.TEXT); // never let it go blank
            return false;
        }
    }
}
