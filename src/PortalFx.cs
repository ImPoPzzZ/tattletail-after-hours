using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// The Kaleidoscope portal (the Endless easter egg): a swirling vortex (two spiral layers turning against each
    /// other), a glowing rainbow rim, sparks spiraling in, a breathing glow. Upright in the local XY plane, facing
    /// local Z. It opens up from nothing when it appears.
    /// </summary>
    public class PortalFx : MonoBehaviour
    {
        private const float R = 1.15f;
        private const int SPARKS = 40;

        private Transform _swirlA, _swirlB, _rim, _body;
        private Material _matA, _matB;
        private Light _light;
        private float _t;
        private readonly List<Transform> _sparks = new List<Transform>();
        private readonly List<float> _ang = new List<float>(), _rad = new List<float>(), _z = new List<float>();
        private System.Random _rnd = new System.Random(3);

        public static PortalFx Build(Transform parent, Light light)
        {
            var go = new GameObject("KE_PortalFx");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<PortalFx>();
            fx._light = light;
            fx.Make();
            return fx;
        }

        private void Make()
        {
            _body = new GameObject("Body").transform;
            _body.SetParent(transform, false);
            Texture2D swirl = SwirlTexture();

            _matA = FxMat(swirl, Color.white, false);
            _swirlA = Part("SwirlA", Disc(R), _matA);
            _matB = FxMat(swirl, new Color(1f, 1f, 1f, 0.55f), false);
            _swirlB = Part("SwirlB", Disc(R * 0.92f), _matB);
            _swirlB.localPosition = new Vector3(0f, 0f, -0.01f);
            _swirlB.localScale = new Vector3(-1f, 1f, 1f); // mirrored: its arms cross the first layer's
            _rim = Part("Rim", Torus(R, 0.075f, 64, 10), FxMat(null, Color.white, true));

            var dot = SparkMat();
            Mesh quad = Quad();
            for (int i = 0; i < SPARKS; i++)
            {
                var s = Part("Spark", quad, dot);
                s.localScale = Vector3.one * (0.05f + (float)_rnd.NextDouble() * 0.07f);
                _sparks.Add(s);
                _ang.Add((float)_rnd.NextDouble() * 360f);
                _rad.Add(0.2f + (float)_rnd.NextDouble() * 1.5f);
                _z.Add(((float)_rnd.NextDouble() - 0.5f) * 0.3f);
            }
            _body.localScale = Vector3.one * 0.01f;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Portal] Built (vortex: " + _matA.shader.name + ", rim/sparks: " + dot.shader.name + ").");
        }

        private Transform Part(string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_body, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            // Opens up (a little overshoot), then breathes.
            float open = _t < 1.4f ? EaseOutBack(Mathf.Clamp01(_t / 1.4f)) : 1f;
            _body.localScale = Vector3.one * Mathf.Max(0.01f, open * (1f + Mathf.Sin(_t * 2.1f) * 0.025f));

            _swirlA.localRotation = Quaternion.Euler(0f, 0f, -_t * 70f);
            _swirlB.localRotation = Quaternion.Euler(0f, 0f, _t * 45f);
            _rim.localRotation = Quaternion.Euler(0f, 0f, _t * 25f); // the rainbow runs around the rim

            // The kaleidoscope drift: the vortex slowly cycles through purples, pinks and blues.
            Color tint = Color.HSVToRGB(Mathf.Repeat(0.02f + Mathf.Sin(_t * 0.35f) * 0.06f, 1f), 0.25f, 1f);
            Tint(_matA, tint);
            Tint(_matB, new Color(tint.b, tint.g, tint.r, 0.55f));

            if (_light != null)
            {
                _light.intensity = (1.6f + Mathf.Sin(_t * 3.1f) * 0.3f + Mathf.PerlinNoise(_t * 6f, 0f) * 0.4f) * open;
                _light.color = Color.HSVToRGB(Mathf.Repeat(0.78f + Mathf.Sin(_t * 0.35f) * 0.06f, 1f), 0.75f, 1f);
            }

            // Sparks: pulled in on a tightening spiral, faster as they near the middle, then back out at the edge.
            Camera cam = Camera.main;
            for (int i = 0; i < _sparks.Count; i++)
            {
                float r = _rad[i];
                r -= Time.deltaTime * (0.35f + 0.9f / (r + 0.4f)) * 0.5f;
                if (r < 0.06f) { r = 1.3f + (float)_rnd.NextDouble() * 0.5f; _z[i] = ((float)_rnd.NextDouble() - 0.5f) * 0.3f; }
                _rad[i] = r;
                _ang[i] += Time.deltaTime * (60f + 140f / (r + 0.25f));
                float a = _ang[i] * Mathf.Deg2Rad;
                _sparks[i].localPosition = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, _z[i] * Mathf.Clamp01(r - 0.3f) - 0.02f);
                if (cam != null) _sparks[i].rotation = cam.transform.rotation;
            }
        }

        private static float EaseOutBack(float x) { const float c = 1.9f; x -= 1f; return 1f + (c + 1f) * x * x * x + c * x * x; }

        // ── Materials ────────────────────────────────────────────────────────
        // The particle shaders: unlit, textured, vertex-colored, both sides, made for effects in the world
        // (Particles/Alpha Blended is in Tattletail's build; Additive, for the glowing bits, if it's there too).
        // Not UI/Default: in the house level it drew nothing out here.
        private static Material FxMat(Texture tex, Color c, bool glow)
        {
            Shader sh = glow ? Shader.Find("Particles/Additive") : null;
            if (sh == null) sh = Shader.Find("Particles/Alpha Blended");
            if (sh == null) return Mat.Unlit(c);
            var m = new Material(sh);
            m.mainTexture = tex != null ? tex : White();
            Tint(m, c);
            return m;
        }

        // (The particle shaders double the tint: 0.5 grey is "as is".)
        private static void Tint(Material m, Color c)
        {
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, c.a * 0.5f));
            else m.color = c;
        }

        private static Texture2D _white;
        private static Texture2D White()
        {
            if (_white != null) return _white;
            _white = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            _white.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            _white.Apply();
            return _white;
        }

        private static Material SparkMat()
        {
            const int N = 32;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r); a *= a;
                    px[y * N + x] = new Color(1f, 0.85f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            var m = FxMat(tex, Color.white, true);
            m.renderQueue = 3001;
            return m;
        }

        /// <summary>The vortex: spiral arms in purple, magenta and blue around a bright core, fading out at the edge.</summary>
        private static Texture2D SwirlTexture()
        {
            const int N = 256;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "KE_PortalSwirl" };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r >= 1f) { px[y * N + x] = new Color(0f, 0f, 0f, 0f); continue; }
                    float th = Mathf.Atan2(dy, dx);
                    float arms = 0.5f + 0.5f * Mathf.Cos(th * 4f + r * 11f);
                    arms = arms * arms;
                    float fine = 0.5f + 0.5f * Mathf.Cos(th * 9f - r * 23f);
                    float hue = Mathf.Repeat(0.66f + r * 0.22f + arms * 0.07f, 1f);   // blue at the middle, purple, then magenta out to the rim
                    float val = Mathf.Lerp(0.18f, 1f, arms * 0.8f + fine * 0.2f) * Mathf.Lerp(0.55f, 1f, r);
                    Color c = Color.HSVToRGB(hue, Mathf.Lerp(0.35f, 0.9f, r), val);
                    float core = Mathf.Exp(-r * r * 14f);                               // the bright heart of it
                    c = Color.Lerp(c, new Color(1f, 0.92f, 1f), core);
                    float alpha = (1f - Mathf.SmoothStep(0.82f, 1f, r)) * Mathf.Lerp(0.8f, 1f, arms);
                    px[y * N + x] = new Color(c.r, c.g, c.b, Mathf.Max(alpha, core));
                }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        // ── Meshes ───────────────────────────────────────────────────────────
        private static Mesh Disc(float radius)
        {
            const int S = 64;
            var v = new Vector3[S + 1]; var uv = new Vector2[S + 1]; var col = new Color[S + 1];
            var tri = new int[S * 3];
            v[0] = Vector3.zero; uv[0] = new Vector2(0.5f, 0.5f); col[0] = Color.white;
            for (int i = 0; i < S; i++)
            {
                float a = i * Mathf.PI * 2f / S;
                v[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
                uv[i + 1] = new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f);
                col[i + 1] = Color.white;
                tri[i * 3] = 0; tri[i * 3 + 1] = 1 + (i + 1) % S; tri[i * 3 + 2] = 1 + i;
            }
            var m = new Mesh { name = "KE_PortalDisc", vertices = v, uv = uv, colors = col, triangles = tri };
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        // A glowing tube around the edge, the kaleidoscope's rainbow running along it (vertex colors).
        private static Mesh Torus(float major, float minor, int segs, int sides)
        {
            var v = new Vector3[(segs + 1) * (sides + 1)];
            var col = new Color[v.Length];
            var uv = new Vector2[v.Length];
            var tri = new int[segs * sides * 6];
            int k = 0;
            for (int i = 0; i <= segs; i++)
            {
                float a = i * Mathf.PI * 2f / segs;
                var center = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                Color hue = Color.HSVToRGB(i / (float)segs, 0.6f, 1f);
                for (int j = 0; j <= sides; j++)
                {
                    float b = j * Mathf.PI * 2f / sides;
                    v[i * (sides + 1) + j] = center * (major + Mathf.Cos(b) * minor) + Vector3.forward * Mathf.Sin(b) * minor;
                    // Brightest facing out of the portal: a hot white-ish core line along the tube.
                    float glow = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(b));
                    col[i * (sides + 1) + j] = Color.Lerp(hue, Color.white, glow * 0.45f);
                    uv[i * (sides + 1) + j] = new Vector2(0.5f, 0.5f);
                }
            }
            for (int i = 0; i < segs; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a0 = i * (sides + 1) + j, a1 = a0 + 1, b0 = a0 + sides + 1, b1 = b0 + 1;
                    tri[k++] = a0; tri[k++] = b0; tri[k++] = a1;
                    tri[k++] = a1; tri[k++] = b0; tri[k++] = b1;
                }
            var m = new Mesh { name = "KE_PortalRim", vertices = v, colors = col, uv = uv, triangles = tri };
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        private static Mesh Quad()
        {
            var m = new Mesh { name = "KE_PortalSpark" };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            m.triangles = new[] { 0, 3, 1, 3, 0, 2 };
            m.RecalculateBounds();
            return m;
        }
    }
}
