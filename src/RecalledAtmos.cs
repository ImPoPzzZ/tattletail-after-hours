using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// What makes the night look like a night: a sky with stars and the moon (where the moonlight comes from), motes and
    /// fireflies drifting through the air round you, embers over the junkyard, and a color grade that follows where you are.
    /// All of it cosmetic, and all of it off with the config's Cosmetics/RecalledGraphics.
    /// </summary>
    public class RcAtmos : MonoBehaviour
    {
        private Transform _sky, _moon;
        private ParticleSystem _motes, _flies, _stars;
        private Camera _cam;
        private UnityEngine.PostProcessing.PostProcessingProfile _profile;
        private bool _graded;
        private float _tempWas, _tintWas, _satWas, _conWas;
        private float _grain0;
        private bool _grainWas;
        private int _zone = -3;

        // What the lighting pass changes (put back when you leave).
        private bool _lit;
        private bool _aoWas, _bloomWas;
        private UnityEngine.PostProcessing.AmbientOcclusionModel.Settings _aoSet;
        private UnityEngine.PostProcessing.BloomModel.Settings _bloomSet;
        private int _pixelLights, _cascades;
        private float _shadowDistance;
        private UnityEngine.Rendering.DefaultReflectionMode _reflMode;
        private Cubemap _reflWas, _refl;
        private float _reflIntensity;
        private float _temp, _tint, _sat = 1f, _con = 1f;

        // The moon's place in the sky: opposite the way its light shines (RecalledController's moon, Euler 48, 205).
        private static readonly Vector3 MoonDir = new Vector3(0.283f, 0.743f, 0.606f);

        private void Start()
        {
            try { Build(); }
            catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Recalled] The night sky didn't build: " + e.Message); }
        }

        private void OnDestroy()
        {
            if (_sky != null) Destroy(_sky.gameObject);
            if (_moon != null) Destroy(_moon.gameObject);
            if (_motes != null) Destroy(_motes.gameObject);
            if (_flies != null) Destroy(_flies.gameObject);
            if (_stars != null) Destroy(_stars.gameObject);
            LightingRestore();
            if (_graded && _profile != null)
            {
                var cg = _profile.colorGrading.settings;
                cg.basic.temperature = _tempWas; cg.basic.tint = _tintWas; cg.basic.saturation = _satWas; cg.basic.contrast = _conWas;
                _profile.colorGrading.settings = cg;
                var gr = _profile.grain.settings;
                gr.intensity = _grain0;
                _profile.grain.settings = gr;
                _profile.grain.enabled = _grainWas;
            }
        }

        // ── Building ─────────────────────────────────────────────────────────
        private void Build()
        {
            _cam = FactoryController.PlayerCam;
            Shader sprite = Shader.Find("Sprites/Default");
            if (sprite != null)
            {
                var sm = new Material(sprite) { mainTexture = BandTexture() };
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(sphere.GetComponent<Collider>());
                sphere.name = "KE_RcSky";
                sphere.transform.localScale = Vector3.one * 560f;
                var sr = sphere.GetComponent<Renderer>();
                sr.sharedMaterial = sm; sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; sr.receiveShadows = false;
                _sky = sphere.transform;

                var mm = new Material(sprite) { mainTexture = MoonTexture() };
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(quad.GetComponent<Collider>());
                quad.name = "KE_RcMoonDisc";
                quad.transform.localScale = Vector3.one * 120f;
                var qr = quad.GetComponent<Renderer>();
                qr.sharedMaterial = mm; qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; qr.receiveShadows = false;
                _moon = quad.transform;
            }
            _stars = Stars(sprite);
            _motes = Drift("KE_RcMotes", 16f, 11f, 0.05f, 0.1f, false);
            _flies = Drift("KE_RcFireflies", 22f, 4f, 0.16f, 0.26f, true);
            GradeSetup();
            try { LightingSetup(); }
            catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Recalled] The lighting pass didn't finish: " + e.Message); }
        }

        // ── The lighting pass ────────────────────────────────────────────────
        // Contact shadows (ambient occlusion), bloom so lamps and glow glow, a night-sky reflection for wet roads, metal and
        // glass, shadows from the moon, more lights per surface, and shafts of light through the fog from the lamps near you.
        private void LightingSetup()
        {
            if (_cam != null)
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Recalled] Camera: " + _cam.renderingPath + ", hdr " + _cam.hdr + ", far " + _cam.farClipPlane + ".");
            if (_profile != null)
            {
                _aoWas = _profile.ambientOcclusion.enabled; _aoSet = _profile.ambientOcclusion.settings;
                var ao = _aoSet;
                ao.intensity = 1.1f; ao.radius = 0.5f;
                _profile.ambientOcclusion.settings = ao;
                _profile.ambientOcclusion.enabled = true;
                _bloomWas = _profile.bloom.enabled; _bloomSet = _profile.bloom.settings;
                var bl = _bloomSet;
                bool hdr = _cam != null && _cam.hdr;
                bl.bloom.intensity = 0.55f; bl.bloom.threshold = hdr ? 1.0f : 0.88f; bl.bloom.softKnee = 0.6f; bl.bloom.radius = 5f; bl.bloom.antiFlicker = true;
                _profile.bloom.settings = bl;
                _profile.bloom.enabled = true;
                if (_cam != null && _cam.renderingPath == RenderingPath.DeferredShading) _profile.screenSpaceReflection.enabled = true;   // (wet roads: only a deferred camera can do it)
            }
            // Something for shiny things to reflect: the night sky, with the moon in it.
            _reflMode = RenderSettings.defaultReflectionMode; _reflWas = RenderSettings.customReflection; _reflIntensity = RenderSettings.reflectionIntensity;
            _refl = NightCube();
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
            RenderSettings.customReflection = _refl;
            RenderSettings.reflectionIntensity = 0.9f;
            // Shadows and lights.
            _pixelLights = QualitySettings.pixelLightCount; _cascades = QualitySettings.shadowCascades; _shadowDistance = QualitySettings.shadowDistance;
            QualitySettings.pixelLightCount = Mathf.Max(_pixelLights, 8);
            QualitySettings.shadowDistance = 70f;
            QualitySettings.shadowCascades = 2;
            var moonGo = GameObject.Find("KE_RcMoon");
            if (moonGo != null) { var ml = moonGo.GetComponent<Light>(); if (ml != null) { ml.shadows = LightShadows.Soft; ml.shadowStrength = 0.65f; } }   // (the moon throws shadows: roofs, trees and fences)
            _lit = true;
        }

        private void LightingRestore()
        {
            if (!_lit) return;
            _lit = false;
            if (_profile != null)
            {
                _profile.ambientOcclusion.settings = _aoSet; _profile.ambientOcclusion.enabled = _aoWas;
                _profile.bloom.settings = _bloomSet; _profile.bloom.enabled = _bloomWas;
            }
            RenderSettings.defaultReflectionMode = _reflMode; RenderSettings.customReflection = _reflWas; RenderSettings.reflectionIntensity = _reflIntensity;
            if (_refl != null) Destroy(_refl);
            QualitySettings.pixelLightCount = _pixelLights; QualitySettings.shadowDistance = _shadowDistance; QualitySettings.shadowCascades = _cascades;
        }

        /// <summary>A small cube map of the night: deep blue overhead, the fog's color at the horizon, black below, the moon as a bright spot.</summary>
        private static Cubemap NightCube()
        {
            const int N = 32;
            var cube = new Cubemap(N, TextureFormat.RGBA32, false);
            Vector3 moon = MoonDir.normalized;
            for (int face = 0; face < 6; face++)
            {
                var px = new Color[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                        Vector3 d;
                        switch ((CubemapFace)face)
                        {
                            case CubemapFace.PositiveX: d = new Vector3(1f, -v, -u); break;
                            case CubemapFace.NegativeX: d = new Vector3(-1f, -v, u); break;
                            case CubemapFace.PositiveY: d = new Vector3(u, 1f, v); break;
                            case CubemapFace.NegativeY: d = new Vector3(u, -1f, -v); break;
                            case CubemapFace.PositiveZ: d = new Vector3(u, -v, 1f); break;
                            default: d = new Vector3(-u, -v, -1f); break;
                        }
                        d.Normalize();
                        float up = Mathf.Clamp01(d.y * 0.5f + 0.5f);
                        Color sky = Color.Lerp(new Color(0.04f, 0.05f, 0.07f), new Color(0.07f, 0.1f, 0.2f), Mathf.Clamp01(d.y * 1.4f));
                        Color c = d.y < 0f ? Color.Lerp(new Color(0.04f, 0.05f, 0.07f), new Color(0.01f, 0.01f, 0.015f), Mathf.Clamp01(-d.y * 3f)) : sky;
                        float m = Mathf.Max(0f, Vector3.Dot(d, moon));
                        c += new Color(0.6f, 0.7f, 1f) * (Mathf.Pow(m, 220f) * 1.6f + Mathf.Pow(m, 12f) * 0.08f);
                        c.a = 1f;
                        px[y * N + x] = c;
                    }
                cube.SetPixels(px, (CubemapFace)face);
            }
            cube.Apply();
            return cube;
        }

        /// <summary>A drifting cloud of specks round the player: dust, spores, fireflies (additive glow).</summary>
        private ParticleSystem Drift(string name, float radius, float rate, float sizeMin, float sizeMax, bool glow)
        {
            Shader sh = Shader.Find(glow ? "Particles/Additive" : "Particles/Alpha Blended");
            if (sh == null) return null;
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.maxParticles = 400;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 11f);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            var em = ps.emission;
            em.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.box = new Vector3(radius * 2f, radius * 0.5f, radius * 2f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = glow ? 0.9f : 0.35f;
            noise.frequency = 0.3f;
            // Fade in and out so nothing pops.
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.material = new Material(sh) { mainTexture = SoftDot() };
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            return ps;
        }

        private static Texture2D SoftDot()
        {
            const int N = 32;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f, d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    byte v = (byte)(a * a * 255f);
                    px[y * N + x] = new Color32(255, 255, 255, v);
                }
            t.SetPixels32(px);
            t.wrapMode = TextureWrapMode.Clamp;
            t.Apply();
            return t;
        }

        /// <summary>Just the pale band across the sky: soft and smooth (it's the stars that are sharp, and they're particles).</summary>
        private static Texture2D BandTexture()
        {
            const int W = 512, H = 256;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W, v = y / (float)H;
                    float line = Mathf.Abs(v - (0.72f + 0.1f * Mathf.Sin(u * 6.283f + 1f)));
                    float band = Mathf.Clamp01(1f - line / 0.16f);
                    float n = Mathf.PerlinNoise(u * 7f, v * 4f) * 0.7f + 0.3f;
                    byte a = v < 0.54f ? (byte)0 : (byte)(band * band * n * 30f);
                    px[y * W + x] = new Color32(150, 165, 215, a);
                }
            t.SetPixels32(px);
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Bilinear;
            t.Apply();
            return t;
        }

        /// <summary>The stars: round, soft specks on a far sphere round the camera (billboards: they stay round at any size).</summary>
        private ParticleSystem Stars(Shader sprite)
        {
            if (sprite == null) return null;
            var go = new GameObject("KE_RcStars");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true; main.playOnAwake = true;
            main.maxParticles = 2400;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSpeed = 0f;
            var em = ps.emission; em.enabled = false;
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.material = new Material(sprite) { mainTexture = SoftDot() };
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            rend.maxParticleSize = 0.05f;
            var r = new System.Random(7);
            for (int i = 0; i < 1900; i++)
            {
                // A direction in the upper sky (a little above the horizon and up), evenly spread.
                double az = r.NextDouble() * 6.2832, y = 0.04 + r.NextDouble() * 0.96, rad = System.Math.Sqrt(1.0 - y * y);
                var dir = new Vector3((float)(System.Math.Cos(az) * rad), (float)y, (float)(System.Math.Sin(az) * rad));
                float b = (float)r.NextDouble(); b = b * b * b;
                float tint = (float)r.NextDouble();
                Color c = tint < 0.2f ? new Color(1f, 0.86f, 0.7f) : tint > 0.8f ? new Color(0.8f, 0.9f, 1f) : Color.white;
                float size = 0.45f + b * 1.1f;
                ps.Emit(dir * 250f, Vector3.zero, size, 1000000f, new Color(c.r, c.g, c.b, 0.25f + b * 0.75f));
            }
            ps.Play();
            return ps;
        }

        /// <summary>The moon: a pale disc with darker seas, and a soft glow round it.</summary>
        private static Texture2D MoonTexture()
        {
            const int N = 256;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f, d = Mathf.Sqrt(dx * dx + dy * dy);
                    float glow = Mathf.Clamp01(1f - d); glow = glow * glow * glow * 0.45f;
                    float disc = Mathf.Clamp01((0.2f - d) / 0.012f);
                    float seas = Mathf.PerlinNoise(x * 0.045f + 3f, y * 0.045f + 7f);
                    float shade = 0.78f + 0.22f * seas;
                    float lit = 0.88f + 0.12f * (dx * -0.4f + dy * 0.4f);   // (a little lighter toward one side)
                    float r = Mathf.Lerp(0.55f, 0.92f, shade) * lit, g = Mathf.Lerp(0.6f, 0.95f, shade) * lit, b = Mathf.Lerp(0.72f, 1f, shade) * lit;
                    float a = Mathf.Clamp01(disc + glow * (1f - disc));
                    float cr = Mathf.Lerp(0.55f, r, disc), cg = Mathf.Lerp(0.65f, g, disc), cb = Mathf.Lerp(0.95f, b, disc);
                    px[y * N + x] = new Color32((byte)(cr * 255f), (byte)(cg * 255f), (byte)(cb * 255f), (byte)(a * 255f));
                }
            t.SetPixels32(px);
            t.wrapMode = TextureWrapMode.Clamp;
            t.Apply();
            return t;
        }

        // ── The color grade ──────────────────────────────────────────────────
        private void GradeSetup()
        {
            var ppb = _cam != null ? _cam.GetComponent<UnityEngine.PostProcessing.PostProcessingBehaviour>() : null;
            if (ppb == null || ppb.profile == null) return;
            _profile = ppb.profile;
            var cg = _profile.colorGrading.settings;
            _tempWas = cg.basic.temperature; _tintWas = cg.basic.tint; _satWas = cg.basic.saturation; _conWas = cg.basic.contrast;
            var gr = _profile.grain.settings;
            _grain0 = gr.intensity; _grainWas = _profile.grain.enabled;
            gr.intensity = 0.14f; gr.size = 0.9f; gr.luminanceContribution = 0.8f; gr.colored = false;
            _profile.grain.settings = gr;
            _profile.grain.enabled = true;                  // (a little film grain: the night isn't clean)
            _temp = _tempWas; _tint = _tintWas; _sat = _satWas; _con = _conWas;
            _graded = true;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Recalled] Grade: temperature " + _tempWas + ", tint " + _tintWas + ", saturation " + _satWas + ", contrast " + _conWas + "; bloom " + (_profile.bloom.enabled ? "on (" + _profile.bloom.settings.bloom.intensity + ")" : "off") + ".");
        }

        // Each place leans a little: temperature, tint, saturation, contrast (street, park, then the five zones).
        private static readonly float[,] Grade =
        {
            { -8f, 3f, 1.05f, 1.06f },     // the streets: cold
            { 2f, 6f, 1.12f, 1.02f },      // the park: a touch of warmth and green
            { -12f, -2f, 1.08f, 1.06f },   // the suburbs: moonlit blue
            { -4f, 14f, 1.1f, 1.08f },     // the mall: sick green
            { 8f, 10f, 1.1f, 1.1f },       // the school: dried blood
            { 14f, 4f, 1.15f, 1.06f },     // the junkyard: rust
            { -14f, -4f, 0.95f, 1.12f },   // the Facility: cold and flat
            { -3f, 2f, 1.08f, 1.08f },     // Egypt, the desert and the roads: moonlit sand
            { 10f, 4f, 1.1f, 1.0f },       // Last Stop: lantern warm
            { 6f, 3f, 1.05f, 1.08f },      // the Plateau Road: dust
            { 12f, 2f, 1.12f, 1.1f },      // the Dig Site: amber
            { -6f, -6f, 1.0f, 1.1f },      // the Test Village: bleached and cold
            { -10f, 4f, 1.0f, 1.12f },     // the Antenna Field: steel blue
            { -12f, 10f, 0.9f, 1.14f },    // the Pyramid Bunker: sick green
        };

        // ── Every frame ──────────────────────────────────────────────────────
        private float _shaftAt;
        private readonly List<Light> _shafted = new List<Light>();

        private void LateUpdate()
        {
            if (Rc.Player == null) return;
            if (_lit && Time.time >= _shaftAt) { _shaftAt = Time.time + 1.2f; Shafts(); }
            Vector3 p = Rc.Player.TownPos;
            Camera cam = _cam != null ? _cam : FactoryController.PlayerCam;
            Vector3 at = cam != null ? cam.transform.position : Rc.W(p.x, p.y, p.z);
            if (_sky != null) _sky.position = at;
            if (_stars != null) _stars.transform.position = at;
            if (_moon != null && cam != null)
            {
                _moon.position = at + MoonDir * 270f;
                _moon.rotation = Quaternion.LookRotation(_moon.position - at, Vector3.up);
            }
            bool park = RcTown.InPark(p.x, p.z);
            int zone = RcTown.ZoneAt(p.x, p.z);
            int place = (park ? 1 : zone >= 0 ? zone + 2 : 0) + (RcWorlds.Egypt ? 7 : 0);
            if (_motes != null) { _motes.transform.position = at + Vector3.up * 2f; SetMote(zone, park); }
            if (_flies != null)
            {
                _flies.transform.position = Rc.W(p.x, p.y + 1.5f, p.z);
                var em = _flies.emission;
                em.rateOverTime = RcWorlds.Egypt ? 0f : park ? 6f : zone == 0 ? 3f : 0f;
            }
            if (_graded && _profile != null)
            {
                float k = 1f - Mathf.Exp(-Time.deltaTime * 0.6f);
                _temp = Mathf.Lerp(_temp, Grade[place, 0], k); _tint = Mathf.Lerp(_tint, Grade[place, 1], k);
                _sat = Mathf.Lerp(_sat, Grade[place, 2], k); _con = Mathf.Lerp(_con, Grade[place, 3], k);
                var cg = _profile.colorGrading.settings;
                cg.basic.temperature = _temp; cg.basic.tint = _tint; cg.basic.saturation = _sat; cg.basic.contrast = _con;
                _profile.colorGrading.settings = cg;
            }
        }

        /// <summary>
        /// Shafts of light through the fog from the lamps nearest you (the game's own volumetric lights: only a few at a time, they cost),
        /// and shadows from the nearest two.
        /// </summary>
        private void Shafts()
        {
            Vector3 me = Rc.W(Rc.Player.TownPos.x, Rc.Player.TownPos.y, Rc.Player.TownPos.z);
            var near = RcLamps.Near(me, 36f, 5);
            for (int i = _shafted.Count - 1; i >= 0; i--)
            {
                Light l = _shafted[i];
                if (l == null) { _shafted.RemoveAt(i); continue; }
                if (near.Contains(l)) continue;
                var hx = l.GetComponent<HxVolumetricLight>();
                if (hx != null) Destroy(hx);
                l.shadows = LightShadows.None;
                _shafted.RemoveAt(i);
            }
            for (int i = 0; i < near.Count; i++)
            {
                Light l = near[i];
                if (!_shafted.Contains(l))
                {
                    try { if (l.GetComponent<HxVolumetricLight>() == null) l.gameObject.AddComponent<HxVolumetricLight>(); }
                    catch (System.Exception) { }
                    _shafted.Add(l);
                }
                l.shadows = i < 2 ? LightShadows.Hard : LightShadows.None;
            }
        }

        // The motes' color and behavior follow the place: dust in the street, spores in the mall, embers in the junkyard.
        private void SetMote(int zone, bool park)
        {
            if (zone == _zone) return;
            _zone = zone;
            var main = _motes.main;
            Color c = new Color(0.7f, 0.78f, 1f, 0.35f);
            float up = 0f;
            if (RcWorlds.Egypt)
            {
                c = new Color(0.95f, 0.82f, 0.55f, 0.35f);   // sand on the wind
                if (zone == 1) c = new Color(1f, 0.7f, 0.3f, 0.4f);
                else if (zone == 2) c = new Color(0.95f, 0.9f, 0.8f, 0.3f);
                else if (zone == 3) c = new Color(0.7f, 0.85f, 1f, 0.3f);
                else if (zone == 4) { c = new Color(0.5f, 1f, 0.6f, 0.4f); up = 0.2f; }
            }
            else if (zone == 1) c = new Color(0.55f, 1f, 0.7f, 0.4f);
            else if (zone == 2) c = new Color(1f, 0.6f, 0.55f, 0.35f);
            else if (zone == 3) { c = new Color(1f, 0.55f, 0.18f, 0.9f); up = 0.35f; }
            else if (zone == 4) c = new Color(0.8f, 0.95f, 1f, 0.3f);
            main.startColor = c;
            main.gravityModifier = -up * 0.2f;
        }
    }
}
