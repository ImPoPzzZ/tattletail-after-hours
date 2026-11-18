using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Diamond tier, one step up from Gold — the same parts as GoldRecolor.cs
    /// (the fur's base coat + shell material and the ears; everything else left
    /// original), in pale icy blue with the same lit fur recipe as Mama's and
    /// Papa's skins.
    /// </summary>
    public static class DiamondRecolor
    {
        // Pale icy blue, in the same lit, matte recipe as Mama's/Papa's skins
        // (BirthdayDecor.TintMaterial), with a bit more sheen than Gold.
        private static readonly Color DiamondColor = new Color(0.72f, 0.9f, 1f);
        private static readonly Color FurDiamond = new Color(0.72f, 0.98f, 1.3f); // the strands (3.3: real fur again). Extra blue: the cream fur texture turns pale blue gray-green

        public static void Apply(GameObject tattletailRoot)
        {
            if (tattletailRoot == null)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: DiamondRecolor.Apply called with null root.");
                return;
            }

            Renderer[] allRenderers = tattletailRoot.GetComponentsInChildren<Renderer>(true);

            // The real fur, recolored the way the game's own Yellow/Teal do (FurShine), plus sparkles.
            int furCount = FurShine.TintFur(tattletailRoot, FurDiamond);
            FurShine.AddSparkles(tattletailRoot, new Color(0.75f, 0.92f, 1f), Color.white);

            // Ears — same name-matched approach as GoldRecolor (their
            // material is shared with eyes/feet/flesh, which must stay
            // original; Renderer.materials auto-instantiates a per-renderer
            // copy so this can't bleed into those).
            int earsTinted = 0;
            foreach (var r in allRenderers)
            {
                if (r == null) continue;
                if (!r.gameObject.name.ToLowerInvariant().Contains("ear")) continue;

                Material[] mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                    MakeDiamond(mats[i]);
                r.materials = mats;
                earsTinted++;
            }

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Diamond recolor applied (" + furCount + " fur controller(s), " +
                earsTinted + " ear renderer(s)); face/eyes/body-plastic elsewhere left original.");
        }

        private static void MakeDiamond(Material mat)
        {
            if (mat == null) return;

            BirthdayDecor.TintMaterial(mat, DiamondColor, 0.55f, BirthdayDecor.FUR_GLOW); // just a tiny glow
        }
    }
}
