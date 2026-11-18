using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Who you are, in the Arcade (Sa1monfr's idea): your consciousness is trapped in there. The very first time a
    /// player arrives they wake up as a blank wooden mannequin (ArcadeAwakening), and stay one (in the mirror, and to
    /// their Versus friend) until they look in the mirror and "Remember who you are": that opens OUTFITS, and closing
    /// it gives them their look back for good. Once per player, ever (saved in PlayerPrefs).
    /// </summary>
    public static class Identity
    {
        // ("_v2": the wooden one; testers who saw the black one get it once more.)
        private const string PREF_AWAKE = "KE_Awakened_v2", PREF_REMEMBERED = "KE_Remembered_v2";

        /// <summary>The awakening has played for this player (once, ever).</summary>
        public static bool Awakened { get { return PlayerPrefs.GetInt(PREF_AWAKE, 0) == 1; } }

        /// <summary>Woke up but hasn't used the mirror yet: still a blank mannequin.</summary>
        public static bool Forgotten { get { return Awakened && PlayerPrefs.GetInt(PREF_REMEMBERED, 0) == 0; } }

        /// <summary>The first arrival: who you were is gone.</summary>
        public static void Erase()
        {
            PlayerPrefs.SetInt(PREF_AWAKE, 1);
            PlayerPrefs.SetInt(PREF_REMEMBERED, 0);
            PlayerPrefs.Save();
            HubMirror.ShowPrompt();
        }

        /// <summary>OUTFITS was closed: you're you again, for good.</summary>
        public static void Remember()
        {
            if (!Forgotten) return;
            PlayerPrefs.SetInt(PREF_REMEMBERED, 1);
            PlayerPrefs.Save();
            HubMirror.HidePrompt();
            HubMode.SetHudText("...right. That's me.");
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Identity] Remembered.");
        }

        private static Material _mannequin;
        private static Texture2D _grain;
        private static readonly Dictionary<Mesh, Mesh> _woodMeshes = new Dictionary<Mesh, Mesh>();

        /// <summary>A built kid, turned into the blank mannequin: varnished light wood all over, no face.</summary>
        public static void PaintMannequin(GameObject kid)
        {
            if (_mannequin == null)
            {
                _mannequin = Mat.Lit(Color.white, 0.35f);
                _mannequin.mainTexture = Grain();
            }
            PaintMannequin(kid, _mannequin);
        }

        /// <summary>Every renderer on the kid gets `mat` (a wood-grain material), on a copy of its mesh with UVs for the grain.</summary>
        public static void PaintMannequin(GameObject kid, Material mat)
        {
            if (kid == null) return;
            foreach (var smr in kid.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.sharedMesh != null) smr.sharedMesh = WoodMesh(smr.sharedMesh);
            foreach (var r in kid.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        // The kid meshes have no UVs: a copy mapped from the rest pose, so the grain runs up the body and limbs.
        private static Mesh WoodMesh(Mesh src)
        {
            Mesh m;
            if (_woodMeshes.TryGetValue(src, out m)) return m;
            m = Object.Instantiate(src);
            m.name = src.name + "_Wood";
            Vector3[] v = src.vertices;
            var uv = new Vector2[v.Length];
            for (int i = 0; i < v.Length; i++) uv[i] = new Vector2((v[i].x + v[i].z * 0.6f) * 2.2f, v[i].y * 0.45f);
            m.uv = uv;
            _woodMeshes[src] = m;
            return m;
        }

        /// <summary>Light beech: soft wavy grain lines and a little fleck, tiling.</summary>
        public static Texture2D Grain()
        {
            if (_grain != null) return _grain;
            const int N = 256;
            _grain = new Texture2D(N, N, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat, name = "KE_WoodGrain" };
            var px = new Color[N * N];
            var light = new Color(0.86f, 0.68f, 0.47f);
            var dark = new Color(0.64f, 0.45f, 0.28f);
            var rnd = new System.Random(11);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N, w = y / (float)N;
                    // Tiling wobble: sines of whole periods, so the edges meet.
                    float wob = Mathf.Sin(w * Mathf.PI * 2f * 2f + Mathf.Sin(u * Mathf.PI * 2f) * 1.5f) * 0.035f
                              + Mathf.Sin(w * Mathf.PI * 2f * 5f + u * Mathf.PI * 2f * 3f) * 0.012f;
                    float ring = Mathf.Sin((u + wob) * Mathf.PI * 2f * 9f) * 0.5f + 0.5f;
                    ring = Mathf.Pow(ring, 5f); // thin darker lines on a pale ground
                    float fine = Mathf.Sin((u + wob * 1.7f) * Mathf.PI * 2f * 37f) * 0.5f + 0.5f;
                    float k = Mathf.Clamp01(ring * 0.75f + fine * 0.12f + ((float)rnd.NextDouble() - 0.5f) * 0.06f);
                    px[y * N + x] = Color.Lerp(light, dark, k);
                }
            _grain.SetPixels(px);
            _grain.Apply(true);
            return _grain;
        }
    }

    /// <summary>
    /// The awakening: black, a glitch; you look down as a wooden mannequin body forms out of drifting wood chips,
    /// your hands come up in front of you and turn over, you look around the Arcade, and it's yours.
    /// </summary>
    public class ArcadeAwakening : MonoBehaviour
    {
        public static ArcadeAwakening Instance { get; private set; }
        public static bool Running { get { return Instance != null && !Instance._done; } }

        private const float END = 9.4f;

        public static void Begin()
        {
            if (Instance != null) return;
            new GameObject("KE_Awakening").AddComponent<ArcadeAwakening>();
        }

        private FirstPersonController _fps;
        private Camera _cam;
        private Vector3 _camLocalPos;
        private Quaternion _camLocalRot;
        private float _near;
        private bool _ready, _done;
        private float _t, _doneAt;
        private Vector3 _eye, _feet;
        private float _yaw;

        private GameObject _body;
        private Material _skin;
        private Transform _upperR, _upperL, _lowerR, _lowerL, _handR, _handL;
        private readonly List<Transform> _specks = new List<Transform>();
        private readonly List<Vector3> _from = new List<Vector3>(), _to = new List<Vector3>();
        private readonly List<float> _delay = new List<float>();

        private GameObject _ui;
        private Image _black;
        private AudioSource _sfx;
        private bool _glitch1, _glitch2, _thump;
        private bool _sawTitle; // the game's title card ("The Arcade") came up over the start

        private void Awake() { Instance = this; }

        private void Start()
        {
            _fps = RM.fpsController;
            if (_fps == null) { Destroy(gameObject); return; }
            _cam = _fps.GetComponentInChildren<Camera>();
            if (_cam == null) { Destroy(gameObject); return; }
            Identity.Erase();
            _fps.enabled = false;
            _camLocalPos = _cam.transform.localPosition;
            _camLocalRot = _cam.transform.localRotation;
            _near = _cam.nearClipPlane;
            _cam.nearClipPlane = Mathf.Min(_near, 0.01f); // (the game's HUD, title card included, is drawn 2 cm in front of this camera: stay under it)

            _ui = new GameObject("KE_AwakeningUI");
            var canvas = _ui.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var bgo = new GameObject("Black", typeof(RectTransform), typeof(Image));
            bgo.transform.SetParent(_ui.transform, false);
            var rt = (RectTransform)bgo.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            _black = bgo.GetComponent<Image>();
            _black.color = Color.black;
            _black.raycastTarget = false;
            LoadingCover.Hide();

            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.spatialBlend = 0f; _sfx.playOnAwake = false;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Identity] Waking up.");
        }

        // Once the teleport has landed: where your eyes and feet are, and the body that forms there.
        private void Setup()
        {
            _ready = true;
            _eye = _cam.transform.parent != null ? _cam.transform.parent.TransformPoint(_camLocalPos) : _cam.transform.position;
            _feet = WorldRays.FeetOf(_fps.transform.position);
            _yaw = _fps.transform.eulerAngles.y;

            _body = ChildModel.Build("KE_AwakeningBody", KidGender.Boy, 0);
            var anim = _body.GetComponent<KidAnimator>();
            if (anim != null) Destroy(anim); // (we pose it)
            float eyeHeight = Mathf.Max(0.5f, _eye.y - _feet.y);
            _body.transform.localScale = Vector3.one * (eyeHeight / 0.93f); // its eyes where yours are
            _body.transform.position = _feet;
            _body.transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            _skin = FadeWood();
            Identity.PaintMannequin(_body, _skin);
            foreach (var tr in _body.GetComponentsInChildren<Transform>(true))
            {
                switch (tr.name)
                {
                    case "head": tr.localScale = Vector3.one * 0.001f; break; // (you're looking out of it)
                    case "upperarmR": _upperR = tr; break;
                    case "upperarmL": _upperL = tr; break;
                    case "lowerarmR": _lowerR = tr; break;
                    case "lowerarmL": _lowerL = tr; break;
                    case "handR": _handR = tr; break;
                    case "handL": _handL = tr; break;
                }
            }

            // The specks: they drift in from all around and settle into the body.
            var smr = _body.GetComponentInChildren<SkinnedMeshRenderer>();
            var baked = new Mesh();
            if (smr != null) smr.BakeMesh(baked);
            Vector3[] verts = baked.vertices;
            Material speck = Mat.Lit(new Color(0.8f, 0.62f, 0.42f), 0.2f); // wood chips
            var rnd = new System.Random(7);
            for (int i = 0; i < 110; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(go.GetComponent<Collider>());
                go.GetComponent<Renderer>().sharedMaterial = speck;
                go.transform.localScale = Vector3.one * (0.012f + (float)rnd.NextDouble() * 0.018f);
                Vector3 target = verts.Length > 0 && smr != null ? smr.transform.TransformPoint(verts[rnd.Next(verts.Length)]) : _feet + Vector3.up * 0.5f;
                Vector3 dir = new Vector3((float)rnd.NextDouble() * 2f - 1f, (float)rnd.NextDouble() * 1.2f - 0.3f, (float)rnd.NextDouble() * 2f - 1f).normalized;
                _from.Add(target + dir * (0.8f + (float)rnd.NextDouble() * 1.1f));
                _to.Add(target);
                _delay.Add((float)rnd.NextDouble() * 0.9f);
                go.transform.position = _from[i];
                _specks.Add(go.transform);
            }
        }

        private static Material FadeWood()
        {
            var m = new Material(Shader.Find("Standard")) { color = new Color(1f, 1f, 1f, 0f), mainTexture = Identity.Grain() };
            m.SetFloat("_Mode", 2f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = 3000;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.35f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            return m;
        }

        private static bool TitleUp() { return RM.hud != null && RM.hud.titleHolder != null && RM.hud.titleHolder.activeSelf; }

        private static float S(float a, float b, float t) { return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t)); }

        private void Update()
        {
            if (_done)
            {
                if (Time.time > _doneAt) { HubMode.SetHudText("Find a mirror."); Destroy(gameObject); }
                return;
            }
            if (_fps == null || _cam == null) return;

            // The game's title card plays over the start: keep the black off it (it's black itself), then its
            // eyes open straight onto the body forming, no second fade from black.
            if (TitleUp())
            {
                _sawTitle = true;
                _ui.SetActive(false);
                _glitch1 = _glitch2 = true;
                if (Time.timeScale == 0f) return;
            }
            if (_sawTitle && _t < 1.2f) _t = 1.2f;
            _t += Time.deltaTime;
            if (!_ready) { if (_t > 0.1f) Setup(); return; }

            // Sound: two glitches in the dark, the specks rushing in, a low thump when the body's whole.
            if (!_glitch1 && _t > 0.1f) { _glitch1 = true; Sfx("vhs_glitch"); }
            if (!_glitch2 && _t > 1.0f) { _glitch2 = true; Sfx("vhs_glitch"); _sfx.PlayOneShot(Whoosh(), 0.7f); }
            if (!_thump && _t > 3.9f) { _thump = true; _sfx.PlayOneShot(TrailerMusic.Boom(), 0.45f); }

            // Out of the black, with a few flickers.
            float black = _t < 1.0f ? 1f : 1f - S(1.0f, 1.7f, _t);
            if (_t > 1.0f && _t < 1.9f && Mathf.PerlinNoise(_t * 25f, 0f) > 0.62f) black = Mathf.Max(black, 0.85f);
            _black.color = new Color(0f, 0f, 0f, _sawTitle ? 0f : black);

            // The body fills in as the specks arrive.
            float form = S(1.6f, 3.9f, _t);
            _skin.color = new Color(1f, 1f, 1f, form);
            for (int i = 0; i < _specks.Count; i++)
            {
                float k = Mathf.Clamp01((_t - 1.2f - _delay[i]) / 1.6f);
                k = k * k * (3f - 2f * k);
                Vector3 swirl = Quaternion.Euler(0f, (1f - k) * 220f, 0f) * (_from[i] - _to[i]);
                _specks[i].position = _to[i] + swirl * (1f - k);
                _specks[i].Rotate(90f * Time.deltaTime, 140f * Time.deltaTime, 0f);
                _specks[i].localScale = Vector3.one * Mathf.Lerp(0.02f, 0f, Mathf.InverseLerp(0.85f, 1f, k));
            }

            if (_t >= END) Finish();
        }

        private void LateUpdate()
        {
            if (!_ready || _done || _cam == null) return;
            // The arms come up in front of you, the hands turn over (twice), then drop.
            float arms = S(4.0f, 5.2f, _t) * (1f - S(7.0f, 7.8f, _t));
            float twist = Mathf.Sin((_t - 5.2f) * 2.2f) * 65f * S(5.2f, 5.6f, _t);
            Pose(_upperR, Quaternion.Euler(0f, 0f, -21f), Quaternion.Euler(-78f, -28f, -10f), arms);
            Pose(_upperL, Quaternion.Euler(0f, 0f, 21f), Quaternion.Euler(-78f, 28f, 10f), arms);
            Pose(_lowerR, Quaternion.Euler(-10f, 0f, 0f), Quaternion.Euler(-55f, twist, 0f), arms);
            Pose(_lowerL, Quaternion.Euler(-10f, 0f, 0f), Quaternion.Euler(-55f, -twist, 0f), arms);
            Pose(_handR, Quaternion.identity, Quaternion.Euler(-20f, 0f, 0f), arms);
            Pose(_handL, Quaternion.identity, Quaternion.Euler(-20f, 0f, 0f), arms);

            // Where you look: down at the forming body, up at your hands, around the room, then ahead.
            float pitch = 72f;
            pitch = Mathf.Lerp(pitch, 38f, S(4.0f, 5.2f, _t));
            pitch = Mathf.Lerp(pitch, 4f, S(7.0f, 7.6f, _t));
            pitch = Mathf.Lerp(pitch, 0f, S(8.4f, 9.2f, _t));
            pitch += Mathf.Sin(_t * 1.3f) * 2f * (1f - S(8.4f, 9.2f, _t));
            float yaw = Mathf.Lerp(0f, -55f, S(7.0f, 7.6f, _t));
            yaw = Mathf.Lerp(yaw, 50f, S(7.6f, 8.4f, _t));
            yaw = Mathf.Lerp(yaw, 0f, S(8.4f, 9.2f, _t));
            yaw += Mathf.Sin((_t - 5.2f) * 1.7f) * 4f * S(5.2f, 5.6f, _t) * (1f - S(7.0f, 7.4f, _t));
            Quaternion look = Quaternion.Euler(pitch, _yaw + yaw, 0f);
            Vector3 flat = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            float scale = _body != null ? _body.transform.localScale.x : 1f;
            _cam.transform.position = _eye + flat * 0.13f * scale * (1f - S(8.4f, 9.2f, _t)); // at the face's front (its eyes), easing back to yours at the end
            _cam.transform.rotation = look;
        }

        private static void Pose(Transform bone, Quaternion rest, Quaternion up, float k)
        {
            if (bone != null) bone.localRotation = Quaternion.Slerp(rest, up, k);
        }

        private void Finish()
        {
            _done = true;
            _doneAt = Time.time + 4f;
            if (_body != null) Destroy(_body);
            foreach (var s in _specks) if (s != null) Destroy(s.gameObject);
            if (_ui != null) Destroy(_ui);
            if (_cam != null) { _cam.transform.localPosition = _camLocalPos; _cam.transform.localRotation = _camLocalRot; _cam.nearClipPlane = _near; }
            if (_fps != null) _fps.enabled = true;
            HubMode.SetHudText("...where am I?");
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Identity] Awake.");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (!_done)
            {
                if (_body != null) Destroy(_body);
                foreach (var s in _specks) if (s != null) Destroy(s.gameObject);
                if (_ui != null) Destroy(_ui);
                if (_cam != null) { _cam.transform.localPosition = _camLocalPos; _cam.transform.localRotation = _camLocalRot; _cam.nearClipPlane = _near; }
                if (_fps != null) _fps.enabled = true;
            }
        }

        private void Sfx(string id)
        {
            try { if (_cam != null && AudioController.IsValidAudioID(id)) AudioController.Play(id, _cam.transform); }
            catch (System.Exception) { }
        }

        private static AudioClip _whoosh;
        // Dust rushing in: filtered noise swelling up, then gone.
        private static AudioClip Whoosh()
        {
            if (_whoosh != null) return _whoosh;
            const int SR = 22050;
            int n = (int)(2.9f * SR);
            var d = new float[n];
            var r = new System.Random(5);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR, env = Mathf.Sin(Mathf.Clamp01(t / 2.9f) * Mathf.PI) * Mathf.Clamp01(t / 2.9f);
                lp += (((float)r.NextDouble() * 2f - 1f) - lp) * Mathf.Lerp(0.02f, 0.25f, t / 2.9f);
                d[i] = lp * env * 1.4f;
            }
            _whoosh = AudioClip.Create("KE_Whoosh", n, 1, SR, false);
            _whoosh.SetData(d, 0);
            return _whoosh;
        }
    }
}
