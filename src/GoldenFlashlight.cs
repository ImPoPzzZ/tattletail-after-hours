using System.Reflection;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Shop passive: the game's own golden flashlight, Endless only. Rather
    /// than flipping the save's GD.hasSpecialFlashlight (which could leak
    /// into the story save), this applies exactly what the vanilla golden
    /// flashlight does — its own public special* values — straight onto the
    /// flashlight for the run, the same self-contained approach
    /// DiamondFlashlight uses. Applied before Diamond/beam colors so those
    /// override it.
    /// </summary>
    public static class GoldenFlashlight
    {
        private static readonly FieldInfo MaxChargeTimeModifierField =
            typeof(FlashlightController).GetField("currentMaxChargeTimeModifier", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo ChargePerShakeModifierField =
            typeof(FlashlightController).GetField("currentChargePerShakeModifier", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo CurrentColorField =
            typeof(FlashlightController).GetField("currentColor", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void Apply(FlashlightController fc)
        {
            if (fc == null || !Shop.IsOwned("Passive Golden Flashlight")) return;

            if ((object)MaxChargeTimeModifierField != null)
                MaxChargeTimeModifierField.SetValue(fc, fc.specialMaxChargeTimeModifier);
            if ((object)ChargePerShakeModifierField != null)
                ChargePerShakeModifierField.SetValue(fc, fc.specialChargePerShakeModifier);
            if ((object)CurrentColorField != null)
                CurrentColorField.SetValue(fc, fc.specialColor);
            if (fc.spotlight != null)
                fc.spotlight.color = fc.specialColor;

            fc.MaxLight(); // refill against the new (taller) ceiling
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Golden flashlight applied (Shop passive).");
        }
    }
}
