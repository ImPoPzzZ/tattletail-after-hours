using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// "Gold" isn't a real vanilla recolor — decompiling RecolorType (the
    /// game's own enum) confirmed it only has STORED_COLOR/YELLOW/TEAL/
    /// ORIGINAL/EDUCATIONAL/BLACK/WHITE, and RecolorManager only ever swaps
    /// in pre-made Material assets wired up per-enum-value in the scene —
    /// there's no gold material to swap to and no vanilla enum value to add
    /// one under. Self-contained instead: tint fur + ears directly.
    ///
    /// Ground truth confirmed via the `[GOLD-DIAG]` renderer dump: there's
    /// exactly one fur-covered renderer ('body', shader Custom/FurShaderV1)
    /// and two ear renderers ('earLeft'/'earRight', Standard shader,
    /// sharing a "tattletailPlastic" material with the eyes/feet/flesh —
    /// never touched here, since Renderer.materials auto-instantiates a
    /// per-renderer copy the moment it's touched).
    ///
    /// Fighting the fur's own custom shader property-by-property once led to forcing the fur
    /// onto Standard (a flat, shiny coat with no strands). 3.3 found the actual color: the
    /// shader's _FurColor, the same property the game's own Yellow/Teal recolors change
    /// (tattleFur_Recolor1/2), so the fur keeps its real strands now (FurShine.cs). The
    /// layers draw with FurShaderControllerV1.FurMaterial, which FurShine copies and tints too.
    /// </summary>
    public static class GoldRecolor
    {
        // The same gold as Golden Mama / Golden Papa (VersusHunters), in the same
        // lit, matte recipe as their fur (BirthdayDecor.TintMaterial). 3.2 remake:
        // 3.0's flat Unlit gold looked outdated, and a shiny metal version with its
        // own reflection probe was tried and dropped (the probe also turned the
        // room around Tattletail white).
        internal static readonly Color GoldColor = new Color(0.95f, 0.72f, 0.22f);
        private static readonly Color FurGold = new Color(1.2f, 0.88f, 0.3f); // the strands (3.3: real fur again, not a flat coat). Above 1: the fur texture is cream and darkens it

        public static void Apply(GameObject tattletailRoot)
        {
            if (tattletailRoot == null)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: GoldRecolor.Apply called with null root.");
                return;
            }

            // Full inventory first, unconditionally — every renderer under
            // Tattletail, its shader(s), and whether it's under a
            // FurShaderControllerV1 at all. Kept as an ongoing diagnostic.
            Renderer[] allRenderers = tattletailRoot.GetComponentsInChildren<Renderer>(true);
            foreach (var r in allRenderers)
            {
                if (r == null) continue;
                bool hasFur = r.GetComponent<FurShaderControllerV1>() != null;
                var matDesc = new List<string>();
                foreach (var m in r.sharedMaterials)
                    matDesc.Add(m == null ? "null" : (m.name + "(" + (m.shader != null ? m.shader.name : "no-shader") + ")"));
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [GOLD-DIAG] renderer='" + r.gameObject.name + "' active=" + r.gameObject.activeInHierarchy +
                    " enabled=" + r.enabled + " hasFurController=" + hasFur + " materials=[" + string.Join(", ", matDesc.ToArray()) + "]");
            }

            // Fur (body) — base coat + shell material, both forced onto
            // Standard now, same as the ears.
            // The real fur, recolored the way the game's own Yellow/Teal do (FurShine), plus sparkles.
            int furCount = FurShine.TintFur(tattletailRoot, FurGold);
            FurShine.AddSparkles(tattletailRoot, new Color(1f, 0.82f, 0.35f), new Color(1f, 0.97f, 0.75f));

            // Ears — separate parts, purple in the original design, not
            // covered by the fur controller at all. Name-matched rather
            // than material-matched since their material
            // ("tattletailPlastic") is shared with eyes/feet/flesh, which
            // must stay untouched.
            int earsTinted = 0;
            foreach (var r in allRenderers)
            {
                if (r == null) continue;
                if (!r.gameObject.name.ToLowerInvariant().Contains("ear")) continue;

                Material[] mats = r.materials; // per-instance copies — the eyes'/feet's own copies of the same shared material asset are untouched
                for (int i = 0; i < mats.Length; i++)
                    MakeGold(mats[i]);
                r.materials = mats;
                earsTinted++;
            }

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Gold recolor applied (" + furCount + " fur controller(s), " +
                earsTinted + " ear renderer(s)); face/eyes/body-plastic elsewhere left original.");
        }

        /// <summary>
        /// History: metallic Standard rendered dark mud in this unevenly lit house, heavy
        /// emission a glowing blob, and 3.0's flat Unlit gold looked outdated; a 3.2 try with
        /// real metal and its own reflection probe whitened the room around Tattletail. What
        /// stuck is the plain lit fur Mama's and Papa's skins use: a matte-ish gold that reacts
        /// to the house's light like everything else.
        /// </summary>
        private static void MakeGold(Material mat)
        {
            if (mat == null) return;

            BirthdayDecor.TintMaterial(mat, GoldColor, 0.35f, BirthdayDecor.FUR_GLOW); // a little sheen and just a tiny glow
        }
    }
}
