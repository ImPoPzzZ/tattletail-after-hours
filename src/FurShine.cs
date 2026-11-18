using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Gold and Diamond Tattletail keep their REAL fur: the game's fur shader (Custom/FurShaderV1,
    /// drawn in layers by FurShaderControllerV1) stays, only its strand color (_FurColor) changes —
    /// exactly how the game's own Yellow and Teal recolors work (tattleFur_Recolor1/2: _FurColor
    /// set, _Color white). Plus a few small sparkles drifting off him.
    /// </summary>
    public static class FurShine
    {
        /// <summary>Recolors the fur on every fur controller under root (its renderer and the copy it draws its layers with).</summary>
        public static int TintFur(GameObject root, Color fur)
        {
            var controllers = root.GetComponentsInChildren<FurShaderControllerV1>(true);
            foreach (var c in controllers)
            {
                if (c == null) continue;
                var r = c.GetComponent<Renderer>();
                if (r != null)
                {
                    Material[] mats = r.materials; // per-instance copies: the shared fur asset stays purple for other runs
                    for (int i = 0; i < mats.Length; i++) Tint(mats[i], fur);
                    r.materials = mats;
                }
                // The layers are drawn with this field, not the renderer's material: copy it too (3.0's
                // attempts missed which property is the color and switched shaders instead: flat fur).
                if (c.FurMaterial != null)
                {
                    var copy = new Material(c.FurMaterial);
                    Tint(copy, fur);
                    c.FurMaterial = copy;
                }
            }
            return controllers.Length;
        }

        private static void Tint(Material m, Color fur)
        {
            if (m == null) return;
            if (m.HasProperty("_FurColor"))
            {
                m.SetColor("_FurColor", fur);
                if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
            }
            else BirthdayDecor.TintMaterial(m, fur, 0.35f, BirthdayDecor.FUR_GLOW); // not fur after all: the plain lit recipe
        }

        /// <summary>Small twinkling sparkles around him, moving with him (walking or standing still, they stay put on him).</summary>
        public static void AddSparkles(GameObject root, Color a, Color b)
        {
            var old = root.transform.Find("KE_Sparkles");
            if (old != null) Object.Destroy(old.gameObject);

            Bounds bounds = new Bounds(root.transform.position, Vector3.one * 0.3f);
            bool first = true;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds);
            }

            var go = new GameObject("KE_Sparkles");
            go.transform.SetParent(root.transform, false);
            go.transform.position = bounds.center;
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.loop = true;
            main.prewarm = true; // already sparkling the moment he appears (and in the shop's pictures)
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.06f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.035f);
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI);
            main.gravityModifier = -0.02f; // drift up a touch
            main.simulationSpace = ParticleSystemSimulationSpace.Local; // they ride with him instead of trailing behind
            main.maxParticles = 60;

            var emission = ps.emission;
            emission.rateOverTime = 24f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Max(0.08f, Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.9f);

            var size = ps.sizeOverLifetime; // twinkle: grow, then shrink away
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0f)));

            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.material = SparkleMaterial();
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            ps.Play();
        }

        private static Material _sparkle;

        // A soft four-point star. "Particles/Alpha Blended" is in the game's build (checked: the
        // built-in "Unlit/Texture" isn't, and asking for it stopped the Arcade from loading).
        private static Material SparkleMaterial()
        {
            if (_sparkle != null) return _sparkle;
            Shader sh = Shader.Find("Particles/Alpha Blended");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float star = Mathf.Max(Mathf.Clamp01(1f - Mathf.Abs(dx) * 14f) * Mathf.Clamp01(1f - Mathf.Abs(dy) * 1.1f),
                                           Mathf.Clamp01(1f - Mathf.Abs(dy) * 14f) * Mathf.Clamp01(1f - Mathf.Abs(dx) * 1.1f));
                    float glow = Mathf.Clamp01(1f - r * 2.2f);
                    float alpha = Mathf.Clamp01(Mathf.Max(star, glow * glow) * 1.2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            tex.Apply();
            _sparkle = new Material(sh) { mainTexture = tex };
            if (_sparkle.HasProperty("_TintColor")) _sparkle.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f)); // the particle shader's neutral tint
            return _sparkle;
        }
    }
}
