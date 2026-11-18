using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PostProcessing;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Strength sliders (Options) for the camera's post effects, each 0–100%
    /// where 100% is exactly the game's original look (defaults: Defaults below):
    /// Blur (depth of field), Bloom (halos around lights), Glow (the lens-dirt
    /// haze over the image) and Film (grain + color fringing; at 0% it also
    /// turns off the Kaleidoscope levels' VHS filter). Re-applied twice a
    /// second, since the game resets some of this on level load.
    /// </summary>
    public static class BlurFilter
    {
        public enum Fx { Blur, Bloom, Glow, Film }
        private static readonly string[] Keys = { "KE_FX_BLUR", "KE_FX_BLOOM", "KE_FX_GLOW", "KE_FX_FILM" };
        // The author's tuned mix: no blur, bloom and glow at a quarter, no grain/fringing.
        private static readonly float[] Defaults = { 0f, 0.25f, 0.25f, 0f };

        // An earlier build forced motion blur off once; this undoes that.
        private const string OLD_MOTION_BLUR_FLAG = "KE_BLUR_DEFAULTS_APPLIED";

        private struct Original
        {
            public bool dof, bloom, chroma, grain;
            public float aperture, bloomIntensity, dirtIntensity, chromaIntensity, grainIntensity;
        }

        private static readonly Dictionary<PostProcessingProfile, Original> _originals = new Dictionary<PostProcessingProfile, Original>();
        private static readonly float[] _levels = new float[Keys.Length];
        private static bool _loaded;
        private static float _timer;

        public static float Get(Fx fx)
        {
            Load();
            return _levels[(int)fx];
        }

        public static void Set(Fx fx, float level)
        {
            Load();
            _levels[(int)fx] = Mathf.Clamp01(level);
            PlayerPrefs.SetFloat(Keys[(int)fx], _levels[(int)fx]);
            Apply();
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            for (int i = 0; i < Keys.Length; i++) _levels[i] = Mathf.Clamp01(PlayerPrefs.GetFloat(Keys[i], Defaults[i]));
        }

        public static void UndoOldMotionBlurChange()
        {
            if (!PlayerPrefs.HasKey(OLD_MOTION_BLUR_FLAG)) return;
            PlayerPrefs.DeleteKey(OLD_MOTION_BLUR_FLAG);
            GD.motionBlur = true; // the game's own default
            GD.SaveMotionBlur();
            PlayerPrefs.Save();
        }

        /// <summary>Called every frame from the plugin; re-applies twice a second.</summary>
        public static void Tick()
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = 0.5f;
            Apply();
        }

        public static void Apply()
        {
            Load();
            Camera cam = Camera.main;
            if (cam == null) return;

            float blur = _levels[(int)Fx.Blur], bloom = _levels[(int)Fx.Bloom], glow = _levels[(int)Fx.Glow], film = _levels[(int)Fx.Film];

            if (film <= 0.01f)
            {
                var vhs = cam.GetComponent<postVHSPro>();
                if (vhs != null && vhs.enabled) vhs.enabled = false;
            }

            var ppb = cam.GetComponent<PostProcessingBehaviour>();
            if (ppb == null || ppb.profile == null) return;
            PostProcessingProfile p = ppb.profile;

            Original o;
            if (!_originals.TryGetValue(p, out o))
            {
                o = new Original
                {
                    dof = p.depthOfField.enabled,
                    bloom = p.bloom.enabled,
                    chroma = p.chromaticAberration.enabled,
                    grain = p.grain.enabled,
                    aperture = p.depthOfField.settings.aperture,
                    bloomIntensity = p.bloom.settings.bloom.intensity,
                    dirtIntensity = p.bloom.settings.lensDirt.intensity,
                    chromaIntensity = p.chromaticAberration.settings.intensity,
                    grainIntensity = p.grain.settings.intensity,
                };
                _originals[p] = o;
            }

            // Blur: depth of field gets weaker as the aperture (f-number) goes up.
            p.depthOfField.enabled = o.dof && blur > 0.01f;
            if (p.depthOfField.enabled)
            {
                var d = p.depthOfField.settings;
                d.aperture = Mathf.Min(32f, o.aperture / blur);
                p.depthOfField.settings = d;
            }

            // Bloom and Glow share the bloom effect, scaled separately.
            p.bloom.enabled = o.bloom && (bloom > 0.01f || glow > 0.01f);
            if (p.bloom.enabled)
            {
                var b = p.bloom.settings;
                b.bloom.intensity = o.bloomIntensity * bloom;
                b.lensDirt.intensity = o.dirtIntensity * glow;
                p.bloom.settings = b;
            }

            // Film: grain + color fringing.
            p.chromaticAberration.enabled = o.chroma && film > 0.01f;
            if (p.chromaticAberration.enabled)
            {
                var c = p.chromaticAberration.settings;
                c.intensity = o.chromaIntensity * film;
                p.chromaticAberration.settings = c;
            }
            p.grain.enabled = o.grain && film > 0.01f;
            if (p.grain.enabled)
            {
                var g = p.grain.settings;
                g.intensity = o.grainIntensity * film;
                p.grain.settings = g;
            }
        }
    }
}
