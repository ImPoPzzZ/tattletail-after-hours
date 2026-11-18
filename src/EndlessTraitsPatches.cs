using HarmonyLib;
using UnityEngine;

namespace KaleidoscopeEndless
{
    // Each of these does nothing unless a profile is running (EndlessTraits.Running).

    /// <summary>
    /// Noise you make: some of it never reaches Mama (Calm Under Pressure near her, Reality Avoidance), some of it counts as a
    /// big noise (Violent Tendencies, Aggressive Behaviour, Hypervigilant Behaviour, the dark of Fear of Abandonment, a debuff).
    /// Only noises made right at your feet are touched, never an object's (a vase, the phone) or a gadget's.
    /// </summary>
    [HarmonyPatch(typeof(MamaManager), "OnNoise")]
    public static class Patch_MamaManager_OnNoise_Traits
    {
        static bool Prefix(Vector3 noisePosition, ref bool bigNoise)
        {
            if (!EndlessTraits.Running || RM.fpsController == null) return true;
            if ((noisePosition - RM.fpsController.transform.position).sqrMagnitude > 9f) return true;
            float skip = EndlessTraits.NoiseSkipChance();
            if (skip > 0f && Random.value < skip) return false;
            float loud = EndlessTraits.NoiseLoudChance();
            if (loud > 0f && !bigNoise && Random.value < loud) bigNoise = true;
            return true;
        }
    }

    /// <summary>Compulsive Observation: the flashlight drains a little faster (the same extra drain Low Battery Mode adds, scaled).</summary>
    [HarmonyPatch(typeof(FlashlightController), "LateUpdate")]
    public static class Patch_FlashlightController_LateUpdate_Traits
    {
        private static readonly System.Reflection.FieldInfo LightTimerField =
            typeof(FlashlightController).GetField("_lightTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        static void Postfix(FlashlightController __instance)
        {
            if (!EndlessTraits.Running) return;
            float extra = EndlessTraits.ExtraFlashlightDrain();
            if (extra <= 0f || (object)LightTimerField == null) return;
            float current = (float)LightTimerField.GetValue(__instance);
            LightTimerField.SetValue(__instance, Mathf.Max(0f, current - extra * Time.deltaTime));
        }
    }

    /// <summary>Unwarranted Suspicion: the decoy prompt does nothing.</summary>
    [HarmonyPatch(typeof(Interactable), "OnInteractComplete")]
    public static class Patch_Interactable_OnInteractComplete_TraitDecoy
    {
        static void Postfix(Interactable __instance, bool fromAutoComplete)
        {
            if (__instance == null || !EndlessTraits.Running) return;
            if (__instance.gameObject.name == "KE_TraitDecoy") EndlessTraitsRuntime.Instance?.DecoyUsed();
        }
    }
}
