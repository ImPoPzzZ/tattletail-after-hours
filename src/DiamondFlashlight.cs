using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// "Diamond Flashlight" — a single-run Shop power up now. Applied once at level start
    /// (SceneBootstrap.StartEndless()), not a per-frame patch.
    ///
    /// Duration: "60% more than the golden flashlight" — confirmed via
    /// decompile that the real vanilla "golden"/special flashlight
    /// (community name; internally GD.hasSpecialFlashlight) boosts charge
    /// time by a fixed `specialMaxChargeTimeModifier` (1.25, a public
    /// field, no reflection needed). Diamond's ceiling is pinned to
    /// `specialMaxChargeTimeModifier * 1.6`, not just "1.6x the plain
    /// vanilla flashlight" — matters because this mod never actually
    /// enables GD.hasSpecialFlashlight, so the flashlight's own current
    /// modifier would otherwise still be sitting at the plain 1x baseline.
    ///
    /// Color: setting currentColor/spotlight.color only affects the
    /// flashlight's emissive glow parts (glowingRenderers, animated every
    /// frame in FlashlightController.LateUpdate() from currentColor) — it
    /// never touches the actual prop's own plastic body material, which is
    /// why that alone didn't read as "the flashlight itself" changing
    /// color. Recolors the body renderers directly too (same forced-
    /// Unlit/Color technique proven on Tattletail), explicitly skipping
    /// glowingRenderers so their charge-based glow-pulse animation keeps
    /// working instead of freezing on a shader that ignores it.
    /// </summary>
    public static class DiamondFlashlight
    {
        private const float CHARGE_TIME_MULT_OVER_GOLDEN = 1.6f; // "60% more than the golden flashlight"

        // The decompiled class shows C# field INITIALIZERS of
        // maxSpotlightIntensity=0.3/maxGlowAmount=1 — but those are
        // compile-time defaults, not necessarily the real values, since
        // Unity components almost always have their actual runtime
        // numbers overridden by the serialized prefab/scene data.
        // Hardcoding "0.3"/"1" as the base to scale up from was the actual
        // bug behind a real "still dim" report — if the live prefab's real
        // value was already higher than that assumed default, this was
        // silently overwriting it with something LOWER. Fixed by reading
        // and caching whatever the true live value is the first time this
        // runs each level (before ever touching it), then always scaling
        // from that cached baseline — ResetCache() is called once per run
        // (EndlessRunManager.StartRun()) so a fresh level always re-reads
        // real values instead of an earlier run's already-boosted ones.
        //
        // Split into two separate multipliers per direct feedback: the
        // beam (maxSpotlightIntensity, a real dynamic Light) blew out via
        // this game's Bloom post-process specifically during Flashlight
        // Survival — the one objective that's totally dark with nothing
        // else on screen to dilute it, same root cause as the earlier Gold
        // "MY EYES" bloom incident. The glow-on-the-prop material tint
        // (maxGlowAmount) isn't a scene light and doesn't have that
        // problem, so it keeps the fuller boost.
        private const float SPOTLIGHT_BRIGHTNESS_MULT = 1.3f;
        private const float GLOW_BRIGHTNESS_MULT = 1.9f;
        private static float? _baseSpotlightIntensity;
        private static float? _baseGlowAmount;

        public static void ResetCache()
        {
            _baseSpotlightIntensity = null;
            _baseGlowAmount = null;
        }

        // Distinct from DiamondRecolor's (more muted) Tattletail tone —
        // this is a small prop close to the camera, so it needs a bit more
        // saturation to read clearly. Still kept well under max brightness
        // — a near-max Unlit color blew straight through this game's Bloom
        // threshold the one time it was tried (on Gold).
        public static readonly Color DiamondColor = new Color(0.45f, 0.62f, 0.68f);

        private static Shader _unlitShader;
        private static Shader UnlitShader => _unlitShader != null ? _unlitShader : (_unlitShader = Shader.Find("Unlit/Color"));

        private static readonly FieldInfo MaxChargeTimeModifierField =
            typeof(FlashlightController).GetField("currentMaxChargeTimeModifier", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo ChargePerShakeModifierField =
            typeof(FlashlightController).GetField("currentChargePerShakeModifier", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo CurrentColorField =
            typeof(FlashlightController).GetField("currentColor", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void Apply(FlashlightController fc)
        {
            if (fc == null)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: DiamondFlashlight.Apply called with null FlashlightController.");
                return;
            }

            float newMaxChargeTimeModifier = fc.specialMaxChargeTimeModifier * CHARGE_TIME_MULT_OVER_GOLDEN; // 1.25 (golden) * 1.6
            if ((object)MaxChargeTimeModifierField != null)
                MaxChargeTimeModifierField.SetValue(fc, newMaxChargeTimeModifier);
            else
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: DiamondFlashlight — currentMaxChargeTimeModifier field not found via reflection.");

            // "Only take 2 shakes to reach max brightness" — pin
            // chargePerShake (a public field, no reflection needed) to
            // exactly half the new max charge time, and force
            // currentChargePerShakeModifier to a known 1x so it can't be
            // silently doubled by GD.hasSpecialFlashlight on this
            // player's own save (the same class of bug that caused the
            // "turned gold" report) — 2 shakes from empty always lands
            // exactly on the new, taller ceiling.
            float effectiveMaxChargeTime = fc.maxChargeTime * newMaxChargeTimeModifier;
            fc.chargePerShake = effectiveMaxChargeTime / 2f;
            if ((object)ChargePerShakeModifierField != null)
                ChargePerShakeModifierField.SetValue(fc, 1f);

            // Brighter beam + brighter glow-on-the-prop, scaled from the
            // REAL live baseline (cached before we ever touch it — see the
            // class comment above for why a hardcoded assumed default was
            // wrong).
            if (!_baseSpotlightIntensity.HasValue)
            {
                _baseSpotlightIntensity = fc.maxSpotlightIntensity;
                _baseGlowAmount = fc.maxGlowAmount;
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Diamond flashlight — real baseline captured: maxSpotlightIntensity=" +
                    _baseSpotlightIntensity.Value + " maxGlowAmount=" + _baseGlowAmount.Value);
            }
            fc.maxSpotlightIntensity = _baseSpotlightIntensity.Value * SPOTLIGHT_BRIGHTNESS_MULT;
            fc.maxGlowAmount = _baseGlowAmount.Value * GLOW_BRIGHTNESS_MULT;

            if ((object)CurrentColorField != null)
                CurrentColorField.SetValue(fc, DiamondColor);

            // currentColor drives the emissive glow parts every frame
            // (LateUpdate) — but spotlight.color itself is only ever set
            // once, in UpdateSpecialFlashlightStatus(), which nothing here
            // re-triggers, so the beam needs to be tinted directly too.
            if (fc.spotlight != null)
                fc.spotlight.color = DiamondColor;

            // The actual carried prop's body — everything under
            // fc.flashlight EXCEPT the glow parts, which stay on their own
            // shader so FlashlightController's own glow-pulse animation
            // (SetColor("_EmissionColor", ...) every frame) keeps working;
            // Unlit/Color has no such property and would just silently
            // freeze that animation.
            if (fc.flashlight != null)
            {
                var glowSet = new HashSet<Renderer>();
                if (fc.glowingRenderers != null)
                    foreach (var gr in fc.glowingRenderers) if (gr != null) glowSet.Add(gr);

                Renderer[] allRenderers = fc.flashlight.GetComponentsInChildren<Renderer>(true);
                int tinted = 0;
                foreach (var r in allRenderers)
                {
                    if (r == null || glowSet.Contains(r)) continue;
                    Material[] mats = r.materials; // instantiates per-instance copies — can't bleed into other objects sharing the same material asset
                    for (int i = 0; i < mats.Length; i++)
                        MakeDiamond(mats[i]);
                    r.materials = mats;
                    tinted++;
                }
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Diamond flashlight body tinted (" + tinted + " renderer(s), " + glowSet.Count + " glow renderer(s) left untouched).");
            }
            else
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: DiamondFlashlight — fc.flashlight is null, body could not be tinted.");
            }

            // Brightness is driven by `_lightTimer / (maxChargeTime *
            // currentMaxChargeTimeModifier)` (confirmed via decompile) —
            // raising the ceiling above without also topping up the
            // current charge shrinks that fraction, which is exactly what
            // made the flashlight read as "way dimmer" right after this
            // modifier was applied (at level start, before any shake had
            // happened yet). MaxLight() re-fills using whatever ceiling is
            // active right now, so calling it last here keeps this method
            // self-contained regardless of where/how many times it's
            // invoked in a run.
            fc.MaxLight();

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Diamond flashlight applied (charge time x" + CHARGE_TIME_MULT_OVER_GOLDEN + " over golden).");
        }

        private static void MakeDiamond(Material mat)
        {
            if (mat == null) return;
            if (UnlitShader != null) mat.shader = UnlitShader;
            mat.color = DiamondColor;
        }
    }
}
