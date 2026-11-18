using HarmonyLib;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// When Mama can't touch the player (Endless only):
    ///  - for GRACE_SECONDS after she teleports, and
    ///  - the whole time a Toy Parrot is down: she ignores the player's
    ///    noises too, and only goes after the parrot.
    ///
    /// Endless nerf: for GRACE_SECONDS after Mama teleports to a new spot
    /// she can't hurt you. Her arrival can't kill your flashlight (which
    /// counts toward Flashlight Survival's 3 strikes), and she can't catch
    /// you, even if she lands right next to you or in your beam. After
    /// that she's back to normal. Endless only: Papa's party is untouched.
    /// </summary>
    public static class MamaTeleportGrace
    {
        public const float GRACE_SECONDS = 1.5f;
        private static float _until;

        public static void OnTeleported() { _until = Time.time + GRACE_SECONDS; }

        public static bool Active { get { return Time.time < _until && InEndless; } }

        /// <summary>A Toy Parrot is crying: Mama ignores the player completely.</summary>
        public static bool ParrotDown { get { return ToyParrot.Active != null && InEndless; } }

        /// <summary>Mama can't catch the player or kill their flashlight right now.</summary>
        public static bool PlayerProtected { get { return Active || ParrotDown; } }

        /// <summary>While the parrot is down, every noise except the parrot's own is ignored.</summary>
        public static bool IgnoresNoiseAt(Vector3 pos)
        {
            return ParrotDown && Vector3.Distance(pos, ToyParrot.Active.transform.position) > 1f;
        }

        private static bool InEndless
        {
            get
            {
                var run = EndlessRunManager.Instance;
                return run != null && run.RunActive && !run.BirthdayMode;
            }
        }
    }

    /// <summary>
    /// While the parrot is down, the player's own noises don't reach Mama
    /// (the HUD's noise meter still reacts, so the player sees they made one).
    /// </summary>
    [HarmonyPatch(typeof(MamaManager), "OnNoise")]
    public static class Patch_MamaManager_OnNoise_Parrot
    {
        static bool Prefix(Vector3 noisePosition, bool bigNoise)
        {
            if (!MamaTeleportGrace.IgnoresNoiseAt(noisePosition)) return true;
            if (RM.hud != null) RM.hud.OnNoise(bigNoise);
            return false;
        }
    }

    /// <summary>Versus: the survivor's noises feed the Mama player's kill meter.</summary>
    [HarmonyPatch(typeof(MamaManager), "OnNoise")]
    public static class Patch_MamaManager_OnNoise_Versus
    {
        static void Postfix(Vector3 noisePosition, bool bigNoise) { VersusNet.OnLocalNoise(noisePosition, bigNoise); }
    }

    /// <summary>Mama "teleports" by snapping to a new patrol node (Mama.MoveToNode) — start the grace window when the node actually changes.</summary>
    [HarmonyPatch(typeof(Mama), "MoveToNode")]
    public static class Patch_Mama_MoveToNode_Grace
    {
        static void Prefix(Mama __instance, out PatrolNode __state) { __state = __instance.currentPatrolNode; }

        static void Postfix(Mama __instance, PatrolNode __state)
        {
            if (__instance.currentPatrolNode != null && __instance.currentPatrolNode != __state)
                MamaTeleportGrace.OnTeleported();
        }
    }

    /// <summary>
    /// During the grace window (or while the parrot is down) the flashlight doesn't count as "on" her, so
    /// MamaManager.BaseBehavior never calls KillLight for it. (KillLight
    /// itself can't be patched — that breaks the lights-out fade; see
    /// EndlessRunManager's flashlight polling.)
    /// </summary>
    [HarmonyPatch(typeof(Mama), "IsFlashlightOnMama")]
    public static class Patch_Mama_IsFlashlightOnMama_Grace
    {
        static bool Prefix(ref bool __result)
        {
            if (!MamaTeleportGrace.PlayerProtected) return true;
            __result = false;
            return false;
        }
    }
}
