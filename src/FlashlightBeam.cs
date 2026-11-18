using System.Reflection;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Shop cosmetic: flashlight beam colors. Same two levers DiamondFlashlight
    /// already proved out — the private currentColor field (drives the
    /// emissive glow parts every frame) plus spotlight.color (set only once
    /// by the game itself, so it needs tinting directly). The prop's body is
    /// left alone.
    /// </summary>
    public static class FlashlightBeam
    {
        public struct BeamColor
        {
            public string name; public Color color;
            public BeamColor(string n, Color c) { name = n; color = c; }
        }

        public static readonly BeamColor[] Colors =
        {
            new BeamColor("Red",    new Color(1.00f, 0.20f, 0.20f)),
            new BeamColor("Green",  new Color(0.30f, 1.00f, 0.35f)),
            new BeamColor("Blue",   new Color(0.30f, 0.50f, 1.00f)),
            new BeamColor("Purple", new Color(0.70f, 0.30f, 1.00f)),
            new BeamColor("Orange", new Color(1.00f, 0.60f, 0.15f)),
            new BeamColor("Pink",   new Color(1.00f, 0.45f, 0.75f)),
        };

        private static readonly FieldInfo CurrentColorField =
            typeof(FlashlightController).GetField("currentColor", BindingFlags.NonPublic | BindingFlags.Instance);

        public static bool TryGetColor(string name, out Color color)
        {
            foreach (var c in Colors)
                if (c.name == name) { color = c.color; return true; }
            color = Color.white;
            return false;
        }

        /// <summary>Applies the beam color picked on the menu (EndlessRunManager.PendingBeamColor, null = default). Call after DiamondFlashlight.Apply so it wins on the shared color.</summary>
        public static void ApplyPending(FlashlightController fc)
        {
            string name = EndlessRunManager.PendingBeamColor;
            if (fc == null || string.IsNullOrEmpty(name)) return;

            Color color;
            if (!TryGetColor(name, out color)) return;

            if ((object)CurrentColorField != null)
                CurrentColorField.SetValue(fc, color);
            if (fc.spotlight != null)
                fc.spotlight.color = color;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Flashlight beam color applied: " + name);
        }
    }
}
