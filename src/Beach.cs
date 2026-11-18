using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Summer Vacation (in progress): an empty daytime beach — sand, the sea up front (can't be
    /// entered), a real sky with the sun — built the same way as the Arcade: the Night 6 level
    /// loads (real player, camera, controls), then the beach is built far from the house and the
    /// player moved onto it. No Tattletail in hand, no Mama. Layout comes next, from the user's marks.
    /// </summary>
    public static class BeachMode
    {
        /// <summary>Armed by the SUMMER VACATION machine, consumed when the level loads.</summary>
        public static bool Pending;
        /// <summary>True while the player is at the beach.</summary>
        public static bool Active;

        public static void Launch()
        {
            LoadingCover.Show(null);
            if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            EndlessRunManager.PendingBirthdayMode = false;
            EndlessTitle.PendingLucky = false;
            EndlessTitle.PendingTitle = "Summer Vacation";
            Gadgets.RunGadget = GadgetType.None;
            Pending = true;
            EndlessRunManager.PendingEndlessRun = true; // SceneMonitor's "the mod owns this level load" gate
            GameManager.PlayLevel(LevelEnum._6_TheParty);
        }

        public static void Reset()
        {
            Pending = false;
            Active = false;
            BeachController.DestroyInstance();
        }
    }

    public class BeachController : MonoBehaviour
    {
        public static BeachController Instance { get; private set; }

        // Far from the house (the Arcade is at y 400): nothing from the level reaches out here.
        public static readonly Vector3 Origin = new Vector3(0f, 300f, 0f);

        // The beach, in local meters: +z is the sea. You walk on the flat part; dunes rise behind and
        // to the sides; the sand slopes into the water past SHORE.
        private const float HALF_X = 46f, BACK_Z = -44f, FRONT_Z = 26f, CELL = 2f; // a small bay: the dune tops close it in
        public const float WALK_X = 20f, WALK_BACK = -15f;                // a cozy cove: ~40 x 24 m to walk
        public const float SHORE = 8f, SEA_Y = -0.35f, WALL_Z = 9.2f;
        public static readonly Vector3 SpawnLocal = new Vector3(0f, 0f, -8f);

        private Transform _root;
        private bool _built;
        private float _readyTimer;
        private Material _sea, _foam;

        public bool Built { get { return _built; } }
        public Transform Root { get { return _root; } }

        /// <summary>The trailer builds the beach quietly while you're still in the Arcade: no sky change, no moving the player, until ShowDaylight().</summary>
        public static bool Background;
        private bool _daylight;

        public static void Create()
        {
            if (Instance != null) return;
            var go = new GameObject("KE_BeachController");
            DontDestroyOnLoad(go);
            go.AddComponent<BeachController>();
            if (!Background) go.AddComponent<HubMarkTool>(); // marks: everyone sees them; E places them (dev builds only)
            go.AddComponent<BeachShift>();  // the smoothie bar job
        }

        public static void DestroyInstance()
        {
            if (Instance != null) Destroy(Instance.gameObject);
            Instance = null;
        }

        private void Awake() { Instance = this; }

        private void OnDestroy()
        {
            RestoreSky();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!BeachMode.Active) { Destroy(gameObject); return; } // must never outlive the mode
            var fps = RM.fpsController;
            if (fps == null) return;

            if (!_built && Background)
            {
                BuildBeach();
                _built = true;
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Beach] Built in the background (trailer).");
                return;
            }
            if (!_built)
            {
                // Same as the Arcade: wait until the level hands over control, plus a moment.
                if (!GS.acceptInput) { _readyTimer = 0f; return; }
                _readyTimer += Time.deltaTime;
                if (_readyTimer < 0.75f) return;

                BuildBeach();
                SetSky();
                QuietTheHouse();
                _built = true;
                MovePlayerIn(fps);
                if (RM.hud != null) RM.hud.ClearQuest();
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Beach] Built at " + Origin + ", player moved in.");
                LoadingCover.Hide();
                return;
            }

            if (!Background || _daylight)
            {
                HoldDaylightExposure();
                foreach (var hx in _volumetrics) if (hx != null && hx.enabled) hx.enabled = false; // Options can switch it back on
            }

            // The sea moves: waves and foam drift toward the shore.
            float t = Time.time;
            if (_sea != null) _sea.mainTextureOffset = new Vector2(t * 0.004f, -t * 0.012f);
            if (_foam != null) _foam.mainTextureOffset = new Vector2(t * 0.01f, Mathf.Sin(t * 0.5f) * 0.04f);

            if (Background) return; // the player is still in the Arcade

            // Safety net: fell through or wandered off somehow -> back to the spawn.
            Vector3 local = fps.transform.position - Origin;
            if (local.y < -8f || Mathf.Abs(local.x) > WALK_X + 4f || local.z < WALK_BACK - 4f || local.z > WALL_Z + 2f)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Beach] Player left the beach (" + local + ") — moving them back.");
                MovePlayerIn(fps);
            }
        }

        /// <summary>Trailer: switch the world to the beach's sunny sky (undone when the beach goes away).</summary>
        public void ShowDaylight()
        {
            if (_daylight) return;
            _daylight = true;
            SetSky();
        }

        private static void MovePlayerIn(FirstPersonController fps)
        {
            fps.TeleportPlayer(Origin + SpawnLocal + Vector3.up * (Height(SpawnLocal.x, SpawnLocal.z) + 0.1f), new Vector3(0f, 0f, 0f)); // facing the sea
        }

        // ── The ground ───────────────────────────────────────────────────────
        /// <summary>Sand height at a local (x, z): flat to walk on, dunes behind and to the sides, sloping into the sea.</summary>
        public static float Height(float x, float z)
        {
            float h = 0.12f * Mathf.Sin(x * 0.21f + 1.3f) * Mathf.Sin(z * 0.17f) + 0.06f * Mathf.Sin(x * 0.53f + z * 0.41f); // gentle ripples
            float back = Mathf.Clamp01((WALK_BACK + 3f - z) / 16f);        // dunes behind
            float side = Mathf.Clamp01((Mathf.Abs(x) - (WALK_X - 3f)) / 16f); // and to the sides
            float dune = Mathf.Max(back, side);
            h += dune * dune * (7f + 2.5f * Mathf.Sin(x * 0.09f) * Mathf.Sin(z * 0.11f + 0.7f));
            if (z > SHORE) h -= (z - SHORE) * 0.12f;                        // down into the water
            return h;
        }

        private void BuildBeach()
        {
            _root = new GameObject("KE_Beach").transform;
            _root.SetParent(transform, false);
            _root.position = Origin;

            // Sand: one mesh on a 2 m grid, with a collider.
            int nx = Mathf.RoundToInt(HALF_X * 2f / CELL) + 1, nz = Mathf.RoundToInt((FRONT_Z - BACK_Z) / CELL) + 1;
            var verts = new Vector3[nx * nz];
            var uvs = new Vector2[nx * nz];
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    float x = -HALF_X + i * CELL, z = BACK_Z + j * CELL;
                    verts[j * nx + i] = new Vector3(x, Height(x, z), z);
                    uvs[j * nx + i] = new Vector2(x / 4f, z / 4f); // the sand texture repeats every 4 m
                }
            var tris = new int[(nx - 1) * (nz - 1) * 6];
            int k = 0;
            for (int j = 0; j < nz - 1; j++)
                for (int i = 0; i < nx - 1; i++)
                {
                    int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                    tris[k++] = a; tris[k++] = c; tris[k++] = b;
                    tris[k++] = b; tris[k++] = c; tris[k++] = d;
                }
            var mesh = new Mesh { name = "Sand" };
            mesh.vertices = verts; mesh.uv = uvs; mesh.triangles = tris;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var sand = new GameObject("Sand");
            sand.transform.SetParent(_root, false);
            sand.AddComponent<MeshFilter>().sharedMesh = mesh;
            var sandMat = Mat.Lit(Color.white, 0.08f);
            sandMat.mainTexture = SandTexture();
            sand.AddComponent<MeshRenderer>().sharedMaterial = sandMat;
            sand.AddComponent<MeshCollider>().sharedMesh = mesh;

            // The sea: out to the horizon (the haze hides its far edge).
            var sea = GameObject.CreatePrimitive(PrimitiveType.Plane); // 10 x 10 m, facing up
            sea.name = "Sea";
            Destroy(sea.GetComponent<Collider>());
            sea.transform.SetParent(_root, false);
            sea.transform.localPosition = new Vector3(0f, SEA_Y, SHORE + 1500f);
            sea.transform.localScale = new Vector3(400f, 1f, 300f);
            _sea = Mat.Lit(Color.white, 0.9f);
            _sea.mainTexture = WaveTexture();
            _sea.mainTextureScale = new Vector2(600f, 450f); // ~6.7 m per wave tile
            sea.GetComponent<Renderer>().sharedMaterial = _sea;
            sea.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Foam where the water meets the sand.
            var foam = GameObject.CreatePrimitive(PrimitiveType.Quad);
            foam.name = "Foam";
            Destroy(foam.GetComponent<Collider>());
            foam.transform.SetParent(_root, false);
            foam.transform.localPosition = new Vector3(0f, SEA_Y + 0.02f, SHORE + 3.2f);
            foam.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            foam.transform.localScale = new Vector3(HALF_X * 2f, 3.5f, 1f);
            Shader cutout = Shader.Find("Unlit/Transparent Cutout"); // in the game's build (checked)
            _foam = cutout != null ? new Material(cutout) : Mat.Unlit(Color.white);
            _foam.mainTexture = FoamTexture();
            _foam.mainTextureScale = new Vector2(HALF_X / 3f, 1f);
            if (_foam.HasProperty("_Cutoff")) _foam.SetFloat("_Cutoff", 0.5f);
            foam.GetComponent<Renderer>().sharedMaterial = _foam;

            // Invisible walls: the shoreline (the sea can't be entered), the sides and the back.
            Wall("ShoreWall", new Vector3(0f, 2f, WALL_Z + 0.5f), new Vector3(HALF_X * 2f, 8f, 1f));
            Wall("WallLeft", new Vector3(-WALK_X - 0.5f, 3f, (WALK_BACK + WALL_Z) / 2f), new Vector3(1f, 20f, WALL_Z - WALK_BACK + 2f));
            Wall("WallRight", new Vector3(WALK_X + 0.5f, 3f, (WALK_BACK + WALL_Z) / 2f), new Vector3(1f, 20f, WALL_Z - WALK_BACK + 2f));
            Wall("WallBack", new Vector3(0f, 3f, WALK_BACK - 0.5f), new Vector3(WALK_X * 2f + 2f, 20f, 1f));

            BeachLayout.Build(_root); // the bar, the path, tables, towels, sand castles (from the marks)

            // The sea's sound: slow waves rolling in.
            var waves = new GameObject("Waves");
            waves.transform.SetParent(_root, false);
            waves.transform.localPosition = new Vector3(0f, 0f, SHORE + 2f);
            var src = waves.AddComponent<AudioSource>();
            src.clip = WaveSound();
            src.loop = true;
            src.spatialBlend = 1f;   // from the shoreline: soft on the beach, faint inside the bar
            src.minDistance = 8f;
            src.maxDistance = 70f;
            src.volume = 0.3f;
            src.Play();
        }

        private void Wall(string name, Vector3 pos, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = pos;
            go.AddComponent<BoxCollider>().size = size;
        }

        // ── Sky, sun and daylight (all restored when leaving) ────────────────
        private bool _skySet, _savedFog;
        private Material _savedSkybox;
        private UnityEngine.Rendering.AmbientMode _savedAmbientMode;
        private Color _savedAmbient, _savedSky, _savedEquator, _savedGround, _savedFogColor;
        private FogMode _savedFogMode;
        private float _savedFogStart, _savedFogEnd, _savedFar;
        private CameraClearFlags _savedClear;
        private Camera _cam;
        private readonly System.Collections.Generic.List<UnityEngine.PostProcessing.PostProcessingProfile> _profiles = new System.Collections.Generic.List<UnityEngine.PostProcessing.PostProcessingProfile>();
        private readonly System.Collections.Generic.List<float[]> _saved = new System.Collections.Generic.List<float[]>(); // exposure, fog, dof, eye, bloom
        private readonly System.Collections.Generic.List<HxVolumetricCamera> _volumetrics = new System.Collections.Generic.List<HxVolumetricCamera>();
        private readonly System.Collections.Generic.List<bool> _savedVolumetrics = new System.Collections.Generic.List<bool>();

        private void SetSky()
        {
            _savedSkybox = RenderSettings.skybox;
            _savedAmbientMode = RenderSettings.ambientMode;
            _savedAmbient = RenderSettings.ambientLight;
            _savedSky = RenderSettings.ambientSkyColor; _savedEquator = RenderSettings.ambientEquatorColor; _savedGround = RenderSettings.ambientGroundColor;
            _savedFog = RenderSettings.fog; _savedFogColor = RenderSettings.fogColor; _savedFogMode = RenderSettings.fogMode;
            _savedFogStart = RenderSettings.fogStartDistance; _savedFogEnd = RenderSettings.fogEndDistance;

            // The sun: a warm directional light, over the sea ahead and a little to the right.
            var sunGO = new GameObject("Sun");
            sunGO.transform.SetParent(_root, false);
            sunGO.transform.rotation = Quaternion.Euler(38f, 200f, 0f); // shines back toward the beach; the sky draws the sun where it comes from
            var sun = sunGO.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 0.95f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            RenderSettings.sun = sun;

            // The game's own procedural sky shader (in its build — checked): blue sky, sun disk.
            Shader skySh = Shader.Find("Skybox/Procedural");
            if (skySh != null)
            {
                var sky = new Material(skySh);
                if (sky.HasProperty("_SunSize")) sky.SetFloat("_SunSize", 0.05f);
                if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", 0.9f);
                if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", new Color(0.45f, 0.55f, 0.75f));
                if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", new Color(0.35f, 0.5f, 0.6f));
                if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", 1.0f);
                RenderSettings.skybox = sky;
            }

            // Bright daylight from all around (sky blue above, warm sand bounce below).
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.54f, 0.66f);   // toned down: the camera's ACES curve still brightens
            RenderSettings.ambientEquatorColor = new Color(0.56f, 0.55f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.4f, 0.35f, 0.26f);

            // A light sea haze toward the horizon (blends the sea's far edge into the sky).
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.83f, 0.93f);
            RenderSettings.fogStartDistance = 250f;
            RenderSettings.fogEndDistance = 1400f;

            // The camera you see through (Camera.main wasn't it when this runs), plus every post-processing
            // profile and volumetric component in the scene: all the house's dark-tuned effects.
            var fps = RM.fpsController;
            _cam = fps != null ? fps.GetComponentInChildren<Camera>() : null;
            if (_cam == null) _cam = Camera.main;
            if (_cam != null)
            {
                _savedClear = _cam.clearFlags; _savedFar = _cam.farClipPlane;
                _cam.clearFlags = CameraClearFlags.Skybox;
                _cam.farClipPlane = 2500f;
            }
            var ppbs = FindObjectsOfType<UnityEngine.PostProcessing.PostProcessingBehaviour>();
            foreach (var ppb in ppbs)
            {
                if (ppb == null || ppb.profile == null || _profiles.Contains(ppb.profile)) continue;
                var pr = ppb.profile;
                _profiles.Add(pr);
                _saved.Add(new float[] { pr.colorGrading.settings.basic.postExposure, pr.fog.enabled ? 1 : 0, pr.depthOfField.enabled ? 1 : 0, pr.eyeAdaptation.enabled ? 1 : 0, pr.bloom.enabled ? 1 : 0 });
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Beach] Camera effects on '" + ppb.name + "': exposure=" + pr.colorGrading.settings.basic.postExposure +
                    " eyeAdaptation=" + pr.eyeAdaptation.enabled + " bloom=" + pr.bloom.enabled + " (intensity " + pr.bloom.settings.bloom.intensity + ")" +
                    " fog=" + pr.fog.enabled + " tonemapper=" + pr.colorGrading.settings.tonemapping.tonemapper);
                pr.fog.enabled = false;           // the house's dark fog effect
                pr.depthOfField.enabled = false;  // its blur
                pr.eyeAdaptation.enabled = false; // auto-exposure (only ever brightens)
                pr.bloom.enabled = false;         // glow tuned for the dark
            }
            foreach (var hx in FindObjectsOfType<HxVolumetricCamera>())
            {
                if (hx == null) continue;
                _volumetrics.Add(hx);
                _savedVolumetrics.Add(hx.enabled);
                hx.enabled = false; // the volumetric light fog glows with the (now bright) ambient light
            }
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Beach] Daylight: camera '" + (_cam != null ? _cam.name : "none") + "' (Camera.main '" +
                (Camera.main != null ? Camera.main.name : "none") + "'), " + _profiles.Count + " post-processing profile(s), " + _volumetrics.Count + " volumetric camera(s).");
            HoldDaylightExposure();
            try { DynamicGI.UpdateEnvironment(); } catch (System.Exception) { } // reflections from the new sky (the sea's shine)
            _skySet = true;
        }

        // The game's brightness setting is a camera exposure boost of +2.5..+5.5 stops (made for a
        // pitch-dark house): in sunlight that's a white-out. At the beach the exposure is daylight,
        // still nudged by the player's brightness slider (the default = plain daylight); kept every frame
        // because the Options brightness slider writes it back.
        private static float DaylightExposure() { return Mathf.Clamp((GD.brightness - GD.defaultBrightness) * 0.4f, -0.6f, 0.6f) - 0.9f; } // the default setting = a stop under plain (ACES brightens)

        private void HoldDaylightExposure()
        {
            float want = DaylightExposure();
            foreach (var pr in _profiles)
            {
                if (pr == null) continue;
                var cg = pr.colorGrading.settings;
                if (Mathf.Abs(cg.basic.postExposure - want) < 0.001f) continue;
                cg.basic.postExposure = want;
                pr.colorGrading.settings = cg;
            }
        }

        private void RestoreSky()
        {
            if (!_skySet) return;
            _skySet = false;
            for (int i = 0; i < _profiles.Count; i++)
            {
                var pr = _profiles[i];
                if (pr == null) continue;
                float[] v = _saved[i];
                var cg = pr.colorGrading.settings;
                cg.basic.postExposure = v[0]; // the house's own brightness again
                pr.colorGrading.settings = cg;
                pr.fog.enabled = v[1] > 0f; pr.depthOfField.enabled = v[2] > 0f; pr.eyeAdaptation.enabled = v[3] > 0f; pr.bloom.enabled = v[4] > 0f;
            }
            for (int i = 0; i < _volumetrics.Count; i++) if (_volumetrics[i] != null) _volumetrics[i].enabled = _savedVolumetrics[i];
            RenderSettings.skybox = _savedSkybox;
            RenderSettings.ambientMode = _savedAmbientMode;
            RenderSettings.ambientLight = _savedAmbient;
            RenderSettings.ambientSkyColor = _savedSky; RenderSettings.ambientEquatorColor = _savedEquator; RenderSettings.ambientGroundColor = _savedGround;
            RenderSettings.fog = _savedFog; RenderSettings.fogColor = _savedFogColor; RenderSettings.fogMode = _savedFogMode;
            RenderSettings.fogStartDistance = _savedFogStart; RenderSettings.fogEndDistance = _savedFogEnd;
            if (_cam != null) { _cam.clearFlags = _savedClear; _cam.farClipPlane = _savedFar; }
        }

        // The house's night ambience (crickets, the Christmas music) doesn't belong at the beach.
        private static void QuietTheHouse()
        {
            foreach (var a in FindObjectsOfType<AudioSource>())
            {
                if (a == null || a.clip == null) continue;
                string n = a.clip.name.ToLowerInvariant();
                if (n.Contains("ambience") || n.Contains("music_christmas") || n.Contains("fridge")) a.mute = true;
            }
        }

        // ── Generated textures and sound ─────────────────────────────────────
        private static Texture2D SandTexture()
        {
            const int N = 256;
            var tex = new Texture2D(N, N, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat };
            var r = new System.Random(7);
            Color baseC = new Color(0.84f, 0.74f, 0.55f);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N * Mathf.PI * 2f, v = y / (float)N * Mathf.PI * 2f;
                    float ripple = 0.035f * Mathf.Sin(u * 3f + Mathf.Sin(v * 2f) * 1.5f);   // wind ripples (tile seamlessly)
                    float grain = ((float)r.NextDouble() - 0.5f) * 0.09f;                   // grains
                    float speck = r.NextDouble() < 0.012 ? -0.18f : 0f;                     // darker specks
                    float s = 1f + ripple + grain + speck;
                    tex.SetPixel(x, y, new Color(baseC.r * s, baseC.g * s, baseC.b * s));
                }
            tex.Apply();
            return tex;
        }

        private static Texture2D WaveTexture()
        {
            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat };
            Color deep = new Color(0.06f, 0.38f, 0.55f), light = new Color(0.2f, 0.62f, 0.72f);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N * Mathf.PI * 2f, v = y / (float)N * Mathf.PI * 2f;
                    float w = 0.5f + 0.25f * Mathf.Sin(v * 2f + Mathf.Sin(u) * 0.8f) + 0.15f * Mathf.Sin(u * 3f + v) + 0.1f * Mathf.Sin(v * 5f - u * 2f);
                    Color c = Color.Lerp(deep, light, Mathf.Clamp01(w));
                    if (w > 0.86f) c = Color.Lerp(c, Color.white, (w - 0.86f) * 2.5f); // wave crests
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return tex;
        }

        private static Texture2D FoamTexture()
        {
            const int W = 128, H = 32;
            var tex = new Texture2D(W, H, TextureFormat.ARGB32, true) { wrapMode = TextureWrapMode.Repeat };
            var r = new System.Random(3);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W * Mathf.PI * 2f;
                    float edge = 0.5f + 0.25f * Mathf.Sin(u * 3f) + 0.12f * Mathf.Sin(u * 7f + 1f); // the foam line's wavy edge
                    float v = y / (float)H;
                    float band = Mathf.Abs(v - edge) < 0.12f ? 1f : (v < edge ? 0.45f : 0f);   // a bright line, lacy foam behind it
                    float a = band * (0.75f + 0.5f * (float)r.NextDouble());
                    tex.SetPixel(x, y, new Color(0.97f, 0.98f, 1f, a > 0.5f ? 1f : 0f));
                }
            tex.Apply();
            return tex;
        }

        // Waves: brown-ish noise, swelling and fading in slow, uneven sets (a 16 s loop).
        private static AudioClip WaveSound()
        {
            const int SR = 22050, LEN = SR * 16;
            var data = new float[LEN];
            var r = new System.Random(11);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < LEN; i++)
            {
                float t = i / (float)SR;
                float noise = (float)r.NextDouble() * 2f - 1f;
                lp += (noise - lp) * 0.05f;       // soft roar (darker)
                lp2 += (noise - lp2) * 0.25f;     // the wash (much less hiss)
                float swell = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t / 16f * Mathf.PI * 2f * 3f - 1.2f), 2.2f); // three waves per loop
                float wash = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t / 16f * Mathf.PI * 2f * 3f - 2.3f)), 6f);
                data[i] = lp * (0.25f + 0.9f * swell) + lp2 * 0.07f * wash;
            }
            float peak = 1e-4f;
            for (int i = 0; i < LEN; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            for (int i = 0; i < LEN; i++) data[i] = data[i] / peak * 0.8f;
            int fade = SR / 2; // loop seam: crossfade the last half second into the start
            for (int i = 0; i < fade; i++) { float k = i / (float)fade; data[LEN - fade + i] = Mathf.Lerp(data[LEN - fade + i], data[i], k); }
            var clip = AudioClip.Create("KE_Waves", LEN, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
