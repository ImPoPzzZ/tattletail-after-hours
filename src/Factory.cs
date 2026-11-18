using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// The Waygetter Facility: the abandoned Waygetter Electronics factory, built like the beach: the
    /// Night 6 level loads (real player, camera, controls), then the factory is built far from the house
    /// and the player moved in by the car. No story (it was scrapped): you just explore it, with the
    /// camcorder. F9 in the Arcade takes you there in dev builds only; players meet the Facility inside Recalled.
    /// </summary>
    public static class FactoryMode
    {
        public static bool Pending;
        public static bool Active;
        /// <summary>Endless in the Waygetter Facility (dev builds): no Mama, the flashlight and camcorder both, the marking tool for item spawn spots.</summary>
        public static bool EndlessPreview;

        /// <summary>The Endless machine's Waygetter Facility map (dev builds): the same place, with Mama off and the marking tool on.</summary>
        public static void LaunchEndlessPreview()
        {
            LoadingCover.Show(null);
            if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            EndlessRunManager.PendingBirthdayMode = false;
            Pending = true;
            EndlessPreview = true; // (the Endless settings the player picked stay: gadget, difficulty, modifiers)
            EndlessRunManager.PendingEndlessRun = true; // SceneMonitor's "the mod owns this level load" gate
            GameManager.PlayLevel(LevelEnum._6_TheParty);
        }

        public static void Launch()
        {
            LoadingCover.Show(null);
            if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            EndlessRunManager.PendingBirthdayMode = false;
            EndlessTitle.PendingLucky = false;
            EndlessTitle.PendingTitle = "Waygetter Facility";
            Gadgets.RunGadget = GadgetType.None;
            Pending = true;
            EndlessRunManager.PendingEndlessRun = true; // SceneMonitor's "the mod owns this level load" gate
            GameManager.PlayLevel(LevelEnum._6_TheParty);
        }

        public static void Reset()
        {
            EndlessPreview = false;
            Pending = false;
            Active = false;
            FactoryController.DestroyInstance();
        }
    }

    public class FactoryController : MonoBehaviour
    {
        public static FactoryController Instance { get; private set; }

        // Far from the house (the beach is at y 300, the Arcade at y 400).
        public static readonly Vector3 Origin = new Vector3(0f, 200f, 0f);

        private Transform _root;
        private bool _built, _decayPending;
        private Camera _retagged;
        private string _oldTag;
        private float _readyTimer;
        public bool Built { get { return _built; } }

        public static void Create()
        {
            if (Instance != null) return;
            var go = new GameObject("KE_FactoryController");
            DontDestroyOnLoad(go);
            go.AddComponent<FactoryController>();
            go.AddComponent<HubMarkTool>(); // E places marks (dev builds only)
            go.AddComponent<FactoryFreeRoam>(); // no story: just explore
        }

        public static void DestroyInstance()
        {
            if (Instance != null) Destroy(Instance.gameObject);
            Instance = null;
        }

        private void Awake() { Instance = this; }

        // ── Mama is out: all the Facility's lights go out, except the glows (batteries, care stations) ──
        private bool _hunting;
        private readonly System.Collections.Generic.List<Light> _lampsOut = new System.Collections.Generic.List<Light>();

        public static void SetHunt(bool hunt) { if (Instance != null) Instance.DoHunt(hunt); }

        private void DoHunt(bool hunt)
        {
            if (hunt == _hunting || _root == null) return;
            _hunting = hunt;
            if (hunt)
            {
                foreach (Light l in _root.GetComponentsInChildren<Light>(false))
                    if (l != null && l.enabled && l.gameObject.name != "Glow") { l.enabled = false; _lampsOut.Add(l); }
                RenderSettings.ambientLight = new Color(0.012f, 0.013f, 0.018f);
            }
            else
            {
                foreach (Light l in _lampsOut) if (l != null) l.enabled = true;
                _lampsOut.Clear();
                RenderSettings.ambientLight = FactoryMode.EndlessPreview ? new Color(0.075f, 0.08f, 0.1f) : new Color(0.018f, 0.02f, 0.028f);
            }
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Factory] Lights " + (hunt ? "out (" + _lampsOut.Count + " lamps)" : "back on") + ".");
        }

        private void OnDestroy()
        {
            if (_retagged != null) _retagged.tag = _oldTag;
            if (_player != null)
            {
                _player.localScale = Vector3.one; // back to kid size
                var cc = _player.GetComponent<CharacterController>();
                if (cc != null && _kidRadius > 0f) { cc.radius = _kidRadius; cc.height = _kidHeight; cc.center = _kidCenter; }
            }
            RestoreNight();
            if (_root != null) Destroy(_root.gameObject);
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!FactoryMode.Active) { Destroy(gameObject); return; } // must never outlive the mode
            var fps = RM.fpsController;
            if (fps == null) return;

            if (!_built)
            {
                if (!GS.acceptInput) { _readyTimer = 0f; return; }
                _readyTimer += Time.deltaTime;
                if (_readyTimer < 0.75f) return;

                _root = new GameObject("KE_Factory").transform;
                _root.position = Origin;
                FactoryLayout.Build(_root);
                SetNight();
                QuietTheHouse();
                _built = true;
                if (!FactoryMode.EndlessPreview) MakeAdult(fps); // (Endless there: you stay kid-sized, so the items on the floor are in reach)
                FasterHere(fps);
                MovePlayerIn(fps);
                Camera pc = PlayerCam;
                if (pc != null && Camera.main == null) { _retagged = pc; _oldTag = pc.tag; pc.tag = "MainCamera"; } // Mama's eyes (PatrolNode) and the jumpscare use Camera.main
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Factory] Built at " + Origin + ", player moved in.");
                LoadingCover.Hide();
                _decayPending = true;
                return;
            }
            if (_decayPending) { _decayPending = false; FactoryLayout.Decay(); } // a frame later: the colliders it aims at are live now

            FactoryLayout.Tick(Time.deltaTime);

            // Safety net: fell through or got past the fence somehow -> back to the car.
            Vector3 local = fps.transform.position - Origin;
            if (local.y < -9f || local.y > 12f || local.x < FactoryLayout.LOT_X0 - 2f || local.x > FactoryLayout.LOT_X1 + 2f || local.z < FactoryLayout.LOT_Z0 - 2f || local.z > FactoryLayout.D + 3f)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Factory] Player left the map (" + local + "), moving them back.");
                MovePlayerIn(fps);
            }
        }

        /// <summary>
        /// A noise the player made (the camcorder's clicks and beeps, and later the ruin underfoot). Mama isn't in the
        /// factory yet; once she is, this is where she hears it (the game's own MamaManager.OnNoise).
        /// </summary>
        public static bool MamaActive;
        public static void MakeNoise(Vector3 at) { MakeNoise(at, false); }
        public static void MakeNoise(Vector3 at, bool big)
        {
            if (RM.mamaManager != null) RM.mamaManager.OnNoise(at, big); // the game's noise: the icon, and Mama's ears
            else if (FactoryMode.EndlessPreview && RM.hud != null) RM.hud.OnNoise(big);
        }

        /// <summary>The player's camera. (Camera.main is null in this level: its camera isn't tagged MainCamera.)</summary>
        public static Camera PlayerCam
        {
            get
            {
                var fps = RM.fpsController;
                Camera c = fps != null ? fps.GetComponentInChildren<Camera>() : null;
                return c != null ? c : Camera.main;
            }
        }

        // ── You move 50% faster in the Facility (walking and running): the building is big ──
        private const float SPEED_MULT = 1.5f;
        private static readonly System.Reflection.FieldInfo WalkField = typeof(FirstPersonController).GetField("m_WalkSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        private static readonly System.Reflection.FieldInfo RunField = typeof(FirstPersonController).GetField("m_RunSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        private static void FasterHere(FirstPersonController fps)
        {
            if ((object)WalkField == null || (object)RunField == null) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Factory] Speed fields not found; normal speed."); return; }
            WalkField.SetValue(fps, (float)WalkField.GetValue(fps) * SPEED_MULT);
            RunField.SetValue(fps, (float)RunField.GetValue(fps) * SPEED_MULT);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Factory] Walking and running speed x" + SPEED_MULT + ".");
        }

        private void MovePlayerIn(FirstPersonController fps)
        {
            // TeleportPlayer lifts by the capsule's unscaled half height; add the rest for the adult scale.
            var cc = fps.GetComponent<CharacterController>();
            // TeleportPlayer lifts by half the capsule's (unscaled) height; lift the rest so the capsule's bottom
            // (center.y - height/2, times the scale) lands on the floor.
            float lift = cc != null ? (cc.height * 0.5f - cc.center.y) * _scale - cc.height * 0.5f : 0f;
            fps.TeleportPlayer(Origin + FactoryLayout.SpawnLocal + Vector3.up * lift, new Vector3(0f, FactoryLayout.SPAWN_YAW, 0f));
        }

        // ── You're a grown-up here: the whole player (capsule, camera, hands) scaled up to adult eye height ──
        private const float ADULT_EYE = 1.62f;
        private float _scale = 1f;
        /// <summary>How much bigger than a kid the player is here (the held Tattletail is scaled by the same).</summary>
        public static float AdultScale { get { return Instance != null ? Instance._scale : 1f; } }
        private Transform _player;
        private float _kidRadius, _kidHeight;
        private Vector3 _kidCenter;

        private void MakeAdult(FirstPersonController fps)
        {
            var cc = fps.GetComponent<CharacterController>();
            Camera cam = PlayerCam;
            if (cc == null || cam == null) return;
            float feet = fps.transform.position.y + (cc.center.y - cc.height * 0.5f) * fps.transform.localScale.y;
            float eye = cam.transform.position.y - feet;
            if (eye < 0.3f) return;
            _scale = Mathf.Clamp(ADULT_EYE / eye, 1f, 1.8f);
            _player = fps.transform;
            _player.localScale = Vector3.one * _scale;
            // Tall, but not wide: scaling made the body as wide as the stairwell door. An adult is ~0.5 m across.
            float kidRadius = cc.radius;
            _kidRadius = kidRadius;
            cc.radius = Mathf.Min(cc.radius, 0.26f / _scale);
            // And no taller than ~1.85 m, feet where they were (every doorway here is 2.3 m).
            _kidHeight = cc.height; _kidCenter = cc.center;
            float maxH = 1.85f / _scale;
            if (cc.height > maxH) { cc.center = new Vector3(cc.center.x, cc.center.y - (cc.height - maxH) * 0.5f, cc.center.z); cc.height = maxH; }
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Factory] Kid eye height " + eye.ToString("F2") + " m, scaled x" + _scale.ToString("F2") + " to an adult's (body " +
                (kidRadius * _scale * 2f).ToString("F2") + " -> " + (cc.radius * _scale * 2f).ToString("F2") + " m wide, " + (cc.height * _scale).ToString("F2") + " m tall).");
        }

        // ── Your Tattletail, from the attic: he rode along and he's in your arms (in your color) ──
        /// <summary>Paints a Tattletail prop in the color the player picked (vanilla recolor, Gold or Diamond).</summary>
        internal static void WearChosenColor(GameObject t)
        {
            if (t == null) return;
            if (EndlessRunManager.PendingRecolor != RecolorType.ORIGINAL && RM.recolorManager != null) RM.recolorManager.RecolorObject(t, EndlessRunManager.PendingRecolor);
            if (EndlessRunManager.PendingGoldColor) GoldRecolor.Apply(t);
            if (EndlessRunManager.PendingDiamondColor) DiamondRecolor.Apply(t);
        }

        internal static void BringTattletail() { BringTattletail(true); }

        /// <summary>Tattletail in your arms (your color). lockNeeds false: his hunger, dirt and battery run, and the care stations matter (Endless).</summary>
        internal static void BringTattletail(bool lockNeeds)
        {
            if (RM.tattletail == null) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Factory] No Tattletail in this level to bring."); return; }
            GS.Tattletail(true, true);          // carried, quietly (he talks once you're inside)
            SceneMonitor.ApplyChosenColor();
            var needs = RM.tattletail.GetNeeds();
            if (needs != null && lockNeeds) needs.LockAllNeeds(); // ponytail: no feeding/brushing/charging yet; the story brings them back
            // Held steady in your hands: the game's springy follow (run in FixedUpdate) made him bob back and
            // forth while you walked at adult size, so he follows your hands directly here.
            RM.tattletail.SetMovementModeDirect();
        }

        // ── Night lighting (restored when leaving) ───────────────────────────
        private bool _nightSet;
        private UnityEngine.Rendering.AmbientMode _savedAmbientMode;
        private Color _savedAmbient;
        private float _savedIntensity;

        private void SetNight()
        {
            _savedAmbientMode = RenderSettings.ambientMode;
            _savedAmbient = RenderSettings.ambientLight;
            _savedIntensity = RenderSettings.ambientIntensity;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = FactoryMode.EndlessPreview ? new Color(0.075f, 0.08f, 0.1f) : new Color(0.018f, 0.02f, 0.028f); // a factory at night, most lamps off (Endless there: a little lighter, you have no flashlight to find the props with)
            _nightSet = true;
        }

        private void RestoreNight()
        {
            if (!_nightSet) return;
            RenderSettings.ambientMode = _savedAmbientMode;
            RenderSettings.ambientLight = _savedAmbient;
            RenderSettings.ambientIntensity = _savedIntensity;
            _nightSet = false;
        }

        private static void QuietTheHouse()
        {
            foreach (var a in FindObjectsOfType<AudioSource>())
            {
                if (a == null || a.clip == null) continue;
                string n = a.clip.name.ToLowerInvariant();
                if (n.Contains("ambience") || n.Contains("music_christmas") || n.Contains("fridge")) a.mute = true;
            }
        }
    }

    /// <summary>A textured material plus how many meters one repeat of its texture covers.</summary>
    public class FMat
    {
        public Material Mat;
        public float Tile; // meters per texture repeat; 0 = stretch once over each face (signs)
        public FMat(Material m, float tile) { Mat = m; Tile = tile; }
    }

    /// <summary>The factory's textures (embedded jpgs, see tools/factory_textures.py) and materials.</summary>
    public static class FTex
    {
        private static readonly Dictionary<string, Texture2D> _tex = new Dictionary<string, Texture2D>();

        public static FMat Get(string key, float tile, Color tint, float gloss, float metal, float bump)
        {
            var m = new Material(Shader.Find("Standard"));
            Texture2D c = Load(key + "_c", false);
            if (c != null) m.mainTexture = c;
            m.color = tint;
            Texture2D n = Load(key + "_n", true);
            if (n != null && m.HasProperty("_BumpMap"))
            {
                m.SetTexture("_BumpMap", n);
                m.SetFloat("_BumpScale", bump);
                m.EnableKeyword("_NORMALMAP");
            }
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            return new FMat(m, tile);
        }

        /// <summary>A plain (untextured) lit material.</summary>
        public static FMat Plain(Color c, float gloss) { return new FMat(Mat.Lit(c, gloss), 1f); }

        /// <summary>Glows by itself (lamp tubes, screens, exit signs).</summary>
        public static FMat Glow(Color c) { return new FMat(Mat.Unlit(c), 1f); }

        /// <summary>Spray paint on a wall: a lit, see-through decal (Standard shader, Fade mode) from graf_&lt;key&gt;.png.</summary>
        public static FMat Spray(string key, out float aspect) { return Fade("graf_" + key, out aspect); }

        /// <summary>A decay decal (tools/factory_decals.py): a stain, crack, cobweb, poster... One shared material per decal.</summary>
        public static FMat Decal(string key, out float aspect)
        {
            FMat m;
            Texture2D t;
            if (_decals.TryGetValue(key, out m) && _tex.TryGetValue("dec_" + key, out t) && t != null) { aspect = t.width / (float)t.height; return m; }
            m = Fade("dec_" + key, out aspect);
            _decals[key] = m;
            return m;
        }

        private static readonly Dictionary<string, FMat> _decals = new Dictionary<string, FMat>();

        private static FMat Fade(string name, out float aspect)
        {
            Texture2D t = Load(name, false, ".png");
            aspect = t != null ? t.width / (float)t.height : 4f;
            var m = new Material(Shader.Find("Standard"));
            if (t != null) { m.mainTexture = t; t.wrapMode = TextureWrapMode.Clamp; }
            m.SetFloat("_Mode", 2f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = 3000;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.12f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            return new FMat(m, 0f);
        }

        private static Texture2D Load(string name, bool normal) { return Load(name, normal, ".jpg"); }

        private static Texture2D Load(string name, bool normal, string ext)
        {
            Texture2D t;
            if (_tex.TryGetValue(name, out t) && t != null) return t;
            byte[] bytes = null;
            using (var s = typeof(FTex).Assembly.GetManifestResourceStream("factory_" + name + ext))
            {
                if (s == null) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Factory] Missing texture " + name); return null; }
                bytes = new byte[s.Length];
                int read = 0;
                while (read < bytes.Length) { int k = s.Read(bytes, read, bytes.Length - read); if (k <= 0) break; read += k; }
            }
            var raw = new Texture2D(2, 2, TextureFormat.RGB24, true);
            raw.LoadImage(bytes);
            if (!normal)
            {
                t = raw;
            }
            else
            {
                // This Unity reads normal maps as (x in alpha, y in green) and wants them linear.
                Color32[] px = raw.GetPixels32();
                for (int i = 0; i < px.Length; i++) { byte x = px[i].r, y = px[i].g; px[i] = new Color32(y, y, y, x); }
                t = new Texture2D(raw.width, raw.height, TextureFormat.RGBA32, true, true);
                t.SetPixels32(px);
                t.Apply(true);
                Object.Destroy(raw);
            }
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 6;
            _tex[name] = t;
            return t;
        }
    }

    /// <summary>Boxes with real UVs: the texture keeps its real-world size on every face, and lines up across neighbors.</summary>
    public static class FGeo
    {
        public static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, FMat m, bool collide)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            var mesh = new Mesh();
            var v = new List<Vector3>(24); var nrm = new List<Vector3>(24); var uv = new List<Vector2>(24); var tan = new List<Vector4>(24); var tri = new List<int>(36);
            Vector3 h = size * 0.5f;
            Face(v, nrm, uv, tan, tri, center, h, Vector3.right, Vector3.forward, m.Tile, size.z, size.y);
            Face(v, nrm, uv, tan, tri, center, h, Vector3.left, Vector3.back, m.Tile, size.z, size.y);
            Face(v, nrm, uv, tan, tri, center, h, Vector3.forward, Vector3.left, m.Tile, size.x, size.y);
            Face(v, nrm, uv, tan, tri, center, h, Vector3.back, Vector3.right, m.Tile, size.x, size.y);
            Face(v, nrm, uv, tan, tri, center, h, Vector3.up, Vector3.right, m.Tile, size.x, size.z);
            Face(v, nrm, uv, tan, tri, center, h, Vector3.down, Vector3.right, m.Tile, size.x, size.z);
            mesh.SetVertices(v); mesh.SetNormals(nrm); mesh.SetUVs(0, uv); mesh.SetTangents(tan); mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m.Mat;
            if (collide) go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        /// <summary>
        /// A six-sided solid from 8 corners (0-3 bottom, 4-7 top, each going -x-z, +x-z, +x+z, -x+z):
        /// wedges, tapered car bodies, slanted windshields. Flat-shaded; the texture is projected per face.
        /// </summary>
        public static GameObject Hex(Transform parent, string name, Vector3[] c, FMat m, bool collide)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mesh = new Mesh();
            var v = new List<Vector3>(24); var nrm = new List<Vector3>(24); var uv = new List<Vector2>(24); var tan = new List<Vector4>(24); var tri = new List<int>(36);
            Vector3 mid = Vector3.zero;
            for (int i = 0; i < 8; i++) mid += c[i];
            mid /= 8f;
            int[,] faces = { { 0, 1, 2, 3 }, { 4, 5, 6, 7 }, { 0, 1, 5, 4 }, { 1, 2, 6, 5 }, { 2, 3, 7, 6 }, { 3, 0, 4, 7 } };
            for (int f = 0; f < 6; f++)
            {
                Vector3 p0 = c[faces[f, 0]], p1 = c[faces[f, 1]], p2 = c[faces[f, 2]], p3 = c[faces[f, 3]];
                Vector3 n = Vector3.Cross(p1 - p0, p2 - p0).normalized;
                if (Vector3.Dot(n, (p0 + p1 + p2 + p3) * 0.25f - mid) < 0f) { Vector3 t = p1; p1 = p3; p3 = t; n = -n; } // face out
                Vector3 u = (p1 - p0).normalized, w = Vector3.Cross(n, u);
                int b0 = v.Count;
                Vector3[] q = { p0, p1, p2, p3 };
                for (int i = 0; i < 4; i++)
                {
                    v.Add(q[i]); nrm.Add(n); tan.Add(new Vector4(u.x, u.y, u.z, 1f));
                    uv.Add(m.Tile > 0f ? new Vector2(Vector3.Dot(q[i], u) / m.Tile, Vector3.Dot(q[i], w) / m.Tile) : new Vector2(i == 1 || i == 2 ? 1f : 0f, i >= 2 ? 1f : 0f));
                }
                // Unity's front face is the side Cross(b - a, c - a) points to, which is n (out) now.
                tri.Add(b0); tri.Add(b0 + 1); tri.Add(b0 + 2);
                tri.Add(b0); tri.Add(b0 + 2); tri.Add(b0 + 3);
            }
            mesh.SetVertices(v); mesh.SetNormals(nrm); mesh.SetUVs(0, uv); mesh.SetTangents(tan); mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m.Mat;
            if (collide) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        // One side of a box: n faces out, u runs along the texture's x (v, its y, is up on walls).
        private static void Face(List<Vector3> v, List<Vector3> nrm, List<Vector2> uv, List<Vector4> tan, List<int> tri,
            Vector3 center, Vector3 h, Vector3 n, Vector3 u, float tile, float su, float sv)
        {
            Vector3 w = Vector3.Cross(u, n); // texture "up": Cross(u, w) = -n keeps the face pointing out
            Vector3 c = Vector3.Scale(n, h);
            float hu = su * 0.5f, hw = sv * 0.5f;
            int b = v.Count;
            for (int i = 0; i < 4; i++)
            {
                float su0 = (i == 2 || i == 3) ? 1f : -1f; // a(-,-) b(-,+) c(+,+) d(+,-)
                float sw0 = (i == 1 || i == 2) ? 1f : -1f;
                Vector3 p = c + u * (su0 * hu) + w * (sw0 * hw);
                v.Add(p);
                nrm.Add(n);
                tan.Add(new Vector4(u.x, u.y, u.z, -1f));
                if (tile <= 0f) uv.Add(new Vector2(su0 * 0.5f + 0.5f, sw0 * 0.5f + 0.5f));
                else
                {
                    Vector3 world = center + p; // place-local, so walls split by doorways line up
                    uv.Add(new Vector2(Vector3.Dot(world, u) / tile, Vector3.Dot(world, w) / tile));
                }
            }
            tri.Add(b); tri.Add(b + 1); tri.Add(b + 2);
            tri.Add(b); tri.Add(b + 2); tri.Add(b + 3);
        }
    }
}

namespace KaleidoscopeEndless
{
    // No "December 24, 1998" title card in the factory: the story's own intro plays instead. Does what
    // the title routine would have done for the level (sound back on, time running, input on).
    [HarmonyLib.HarmonyPatch(typeof(HUD), "PlayTitle")]
    public static class Patch_HUD_PlayTitle_Factory
    {
        static bool Prefix(HUD __instance)
        {
            if (!FactoryMode.Active && !FactoryMode.Pending) return true;
            // The title's "eyes opening" end state: eyelids open, the card hidden.
            if (__instance.eyeTop != null) __instance.eyeTop.localScale = new Vector3(1f, 0f, 1f);
            if (__instance.eyeBottom != null) __instance.eyeBottom.localScale = new Vector3(1f, 0f, 1f);
            if (__instance.titleHolder != null) __instance.titleHolder.SetActive(false);
            AudioController.MuteSound(false);
            Time.timeScale = 1f;
            GS.acceptInput = true;
            Cursor.lockState = CursorLockMode.Locked;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Factory] Skipped the level's title card.");
            return false;
        }
    }
}

namespace KaleidoscopeEndless
{
    /// <summary>Night Shift with the story off: you arrive with your flashlight and Tattletail and explore. The care stations work.</summary>
    public class FactoryFreeRoam : MonoBehaviour
    {
        private bool _done;
        // Our own little overlay instead of the game's quest-text channel (a long two-line text there was the last thing logged before a crash).
        private static void ShowPreviewHint()
        {
            var go = new GameObject("KE_PreviewHint");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 290;
            var tgo = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Shadow));
            tgo.transform.SetParent(go.transform, false);
            var rt = (RectTransform)tgo.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -20f);
            rt.sizeDelta = new Vector2(1100f, 90f);
            var t = tgo.GetComponent<Text>();
            t.font = MainMenuButtonInjector.RealFont != null ? MainMenuButtonInjector.RealFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize = 22;
            t.alignment = TextAnchor.UpperLeft;
            t.raycastTarget = false;
            t.color = new Color(1f, 0.95f, 0.75f);
            t.text = KaleidoscopeEndlessPlugin.DevTools
                ? "ENDLESS - WAYGETTER FACILITY (Mama is off)\nE: place a mark, then a digit for the item.   Z: undo.   Marks go to BepInEx/facility_marks.txt"
                : "ENDLESS - WAYGETTER FACILITY" + System.Environment.NewLine + "Right click: raise the camcorder.   Left click (raised): night vision.   Mama hunts here.";
            Object.Destroy(go, 25f);
        }


        private void Update()
        {
            var fc = FactoryController.Instance;
            if (_done || fc == null || !fc.Built || RM.fpsController == null) return;
            _done = true;
            if (FactoryMode.EndlessPreview && VersusMode.IsMama)
            {
                // Versus in the Facility, the hunter's side: the building stands, the hunter's own screen (VersusMamaController) takes over.
                VersusMode.PendingMamaLoad = false;
                if (FactoryLayout.FlashlightProp != null) FactoryLayout.FlashlightProp.SetActive(false);
                if (RM.flashlight != null) RM.flashlight.SetFlashlight(false);
                if (FactoryLayout.TrunkTattletail != null) FactoryLayout.TrunkTattletail.SetActive(false);
                FacilityMarks.BuildSetDressing();
                FacilityMama.Prepare(); // the hunter stands on the same spots Mama walks in Endless
                VersusMamaController.Create();
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Versus] Facility built: the hunter's side is set up.");
                return;
            }
            if (FactoryMode.EndlessPreview)
            {
                // Endless in the Facility (dev builds): Mama is off and the marking tool is on. Set up exactly like the free roam
                // (the camcorder is how you see; the flashlight stays off here), with a line in the log per step.
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Factory] Endless preview: setting up.");
                if (FactoryLayout.FlashlightProp != null) FactoryLayout.FlashlightProp.SetActive(false);
                if (RM.flashlight != null) RM.flashlight.SetFlashlight(false);
                if (FactoryLayout.TrunkHinge != null) FactoryLayout.TrunkHinge.localRotation = Quaternion.Euler(80f, 0f, 0f);
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Factory] Endless preview: trunk open.");
                if (FactoryLayout.TrunkTattletail != null) FactoryLayout.TrunkTattletail.SetActive(false); // he is in your arms, not in the trunk
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Factory] Endless preview: bringing Tattletail.");
                FactoryController.BringTattletail(false);
                if (RM.tattletail != null && RM.tattletail.tattletail != null && FactoryController.AdultScale > 1.01f) RM.tattletail.tattletail.transform.localScale *= FactoryController.AdultScale; // (the player is scaled up to an adult: he has to be too, or he looks tiny in your arms)
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Factory] Endless preview: Tattletail is with you (x" + FactoryController.AdultScale.ToString("F2") + ").");
                gameObject.AddComponent<FactoryCamcorder>();
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Factory] Endless preview: camcorder on.");
                gameObject.AddComponent<FacilityRunStarter>(); // the real Endless run starts in a few seconds, like the first door does in the house
                ShowPreviewHint();
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Factory] Endless preview: hint shown.");
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Factory] Endless preview: ready.");
                return;
            }
            if (FactoryLayout.FlashlightProp != null) FactoryLayout.FlashlightProp.SetActive(false); // it's in your hand, not the trunk
            if (RM.flashlight != null) RM.flashlight.SetFlashlight(false); // no flashlight: the camcorder is how you see
            // Tattletail isn't carried in here: he waits in the trunk (left open), in the color you picked.
            if (FactoryLayout.TrunkHinge != null) FactoryLayout.TrunkHinge.localRotation = Quaternion.Euler(80f, 0f, 0f);
            if (FactoryLayout.TrunkLid != null) { var c = FactoryLayout.TrunkLid.GetComponent<Collider>(); if (c != null) c.enabled = false; }
            FactoryController.WearChosenColor(FactoryLayout.TrunkTattletail);
            gameObject.AddComponent<FactoryCamcorder>(); // found footage: the camcorder, always recording
            HubMode.SetHudText("");
        }
    }
}
