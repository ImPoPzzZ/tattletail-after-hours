using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// "Restart the Fuse Box" objective — a single fixed, hand-picked wall
    /// spot, not a real named prop. The box itself (warning triangle,
    /// "FUSE BOX" plate, status light) is on the wall for every Endless
    /// run; only the hold-to-restart trigger comes and goes with the
    /// objective, and the light blinks red while it's needed. Unlike WashingMachineObjective/
    /// SpeakerObjective (which lean on a real mesh already sitting at
    /// their resolved position), there's nothing real at this wall — same
    /// "no real prop, build our own visible placeholder" recipe the tapes
    /// use, just mounted flat against a wall instead of lying on the
    /// floor. No location/clip resolve coroutines at all: both the
    /// position and the sound are fully fixed/generated, so
    /// StartObjective() can build everything synchronously.
    ///
    /// Hold ~4.5s to restart it, same shape as the Charging Station's
    /// hold. Sound only plays while actively holding (matches the tapes'
    /// established behavior) and pings real Mama noise roughly once a
    /// second for as long as the hold continues (same GetHoldProgress()-
    /// polling mechanism proven on the tapes) — "she knows exactly where
    /// you are the whole time," per the original idea.
    /// </summary>
    public class FuseBoxObjective : MonoBehaviour
    {
        public static FuseBoxObjective Instance { get; private set; }

        // Hand-marked wall spot (E-key mark: playerPos=(5.3, 1.1, 4.2)
        // looking at 'Interior' — a plain wall surface, not a real prop —
        // at (5.6, 1.1, 4.2)). Pulled back half the panel's thickness
        // toward the player so it sits flush ON the wall instead of
        // clipping through it — same ground-offset lesson learned from
        // the tapes, just applied to a wall instead of a floor.
        private static readonly Vector3 HouseFixedPosition = new Vector3(5.56f, 1.1f, 4.2f);
        // Endless in the Waygetter Facility: the marked spot (stand at it, the wall is behind it at x = 0; its face is at x = 0.125).
        private static Vector3 FixedPosition { get { return FactoryMode.EndlessPreview ? FacilityMarks.FuseBox[0].World + new Vector3(-0.33f, 1.1f, 0f) : HouseFixedPosition; } }
        private const float HOLD_TIME = 4.5f;
        private const float HOLD_NOISE_INTERVAL = 1f;

        // Procedural electrical buzz — a low mains-hum-like tone mixed
        // with noise, generated once via AudioClip.Create() same as the
        // speaker's static, just tuned differently so the two objectives
        // don't sound identical.
        private static AudioClip _buzzClip;

        private static AudioClip GetOrCreateBuzzClip()
        {
            if (_buzzClip != null) return _buzzClip;

            const int sampleRate = 44100;
            const float durationSeconds = 2f;
            const float toneHz = 60f; // mains-hum-like low buzz
            int sampleCount = Mathf.RoundToInt(sampleRate * durationSeconds);

            var clip = AudioClip.Create("KE_FuseBuzz", sampleCount, 1, sampleRate, false);
            var samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float tone = Mathf.Sin(2f * Mathf.PI * toneHz * t);
                float noise = UnityEngine.Random.Range(-1f, 1f);
                samples[i] = Mathf.Clamp(tone * 0.6f + noise * 0.4f, -1f, 1f);
            }
            clip.SetData(samples, 0);

            _buzzClip = clip;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [FuseBox] Generated procedural electrical buzz clip.");
            return _buzzClip;
        }

        private GameObject _fuseBoxGO;   // the interaction trigger — only exists while it's the objective
        private GameObject _model;       // the box itself — always on the wall during an Endless run
        private Renderer _statusLight;
        private Light _statusGlow;
        private Interactable _interactable;
        private AudioSource _audioSource;
        private bool _wasHeld;
        private float _nextNoiseTime;

        // The wall is at x = 5.6 and the box faces the player standing at lower x.
        private static readonly Vector3 HouseWallPoint = new Vector3(5.6f, 1.1f, 4.2f);
        private static Vector3 WallPoint { get { return FactoryMode.EndlessPreview ? FacilityMarks.FuseBox[0].World + new Vector3(-0.375f, 1.1f, 0f) : HouseWallPoint; } }
        private static Quaternion Facing { get { return Quaternion.LookRotation(FactoryMode.EndlessPreview ? Vector3.right : Vector3.left); } } // local +Z = out of the wall

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // The box is part of the house for every Endless run, not just its
            // objective. It lives in the level (not DontDestroyOnLoad), so it
            // goes away with the level and is rebuilt on the next run.
            var run = EndlessRunManager.Instance;
            bool endless = run != null && run.RunActive && !run.BirthdayMode;
            if (endless && _model == null) BuildModel();
            UpdateStatusLight();

            // Same GetHoldProgress()-polling trick proven on the tapes —
            // no Harmony patch or reflection needed. Drives both the
            // audio (only while held, not from spawn) and the Mama
            // noise pings.
            if (_interactable == null || _audioSource == null) return;

            bool isHeld = _interactable.GetHoldProgress() > 0f;

            if (isHeld && !_wasHeld)
            {
                _audioSource.Play();
                RM.mamaManager?.OnNoise(_fuseBoxGO.transform.position, true);
                _nextNoiseTime = Time.time + HOLD_NOISE_INTERVAL;
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [FuseBox] Restart started — noise pinged.");
            }
            else if (isHeld && Time.time >= _nextNoiseTime)
            {
                RM.mamaManager?.OnNoise(_fuseBoxGO.transform.position, true);
                _nextNoiseTime = Time.time + HOLD_NOISE_INTERVAL;
            }
            else if (!isHeld && _wasHeld)
            {
                _audioSource.Stop();
            }

            _wasHeld = isHeld;
        }

        // Blinking red while it needs restarting, steady green otherwise.
        private void UpdateStatusLight()
        {
            if (_statusLight == null) return;
            bool broken = _fuseBoxGO != null;
            bool on = !broken || Mathf.Repeat(Time.time, 0.8f) < 0.4f;
            Color c = broken ? new Color(1f, 0.12f, 0.1f) : new Color(0.2f, 1f, 0.35f);
            _statusLight.material.color = on ? c : c * 0.2f;
            if (_statusGlow != null)
            {
                _statusGlow.color = c;
                _statusGlow.intensity = on ? (broken ? 1.2f : 0.35f) : 0f;
            }
        }

        // ── The box ──────────────────────────────────────────────────────────
        private void BuildModel()
        {
            _model = new GameObject("KE_FuseBoxModel");
            _model.transform.position = WallPoint;
            _model.transform.rotation = Facing;
            Transform t = _model.transform;

            Material metal = Mat.Lit(new Color(0.34f, 0.35f, 0.37f), 0.55f);
            Material door = Mat.Lit(new Color(0.42f, 0.43f, 0.45f), 0.5f);
            Material dark = Mat.Lit(new Color(0.08f, 0.08f, 0.09f), 0.6f);

            // Body flush on the wall, door proud of it (a few mm apart so nothing z-fights).
            Part(t, new Vector3(0f, 0f, 0.06f), new Vector3(0.44f, 0.58f, 0.12f), metal);
            Part(t, new Vector3(0f, 0f, 0.126f), new Vector3(0.40f, 0.54f, 0.01f), door);
            Part(t, new Vector3(0.16f, -0.02f, 0.139f), new Vector3(0.025f, 0.11f, 0.02f), dark);        // handle
            Part(t, new Vector3(-0.2f, 0.16f, 0.13f), new Vector3(0.012f, 0.06f, 0.012f), dark);         // hinges
            Part(t, new Vector3(-0.2f, -0.16f, 0.13f), new Vector3(0.012f, 0.06f, 0.012f), dark);
            Part(t, new Vector3(0f, 0.31f, 0.05f), new Vector3(0.05f, 0.05f, 0.1f), dark);               // conduit up the wall
            Part(t, new Vector3(0f, 0.9f, 0.05f), new Vector3(0.035f, 1.2f, 0.035f), dark);

            // Status light in the door's top-right corner.
            var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.transform.SetParent(t, false);
            bulb.transform.localPosition = new Vector3(0.14f, 0.23f, 0.136f);
            bulb.transform.localScale = Vector3.one * 0.035f;
            Object.Destroy(bulb.GetComponent<Collider>());
            _statusLight = bulb.GetComponent<Renderer>();
            _statusLight.material = Mat.Unlit(Color.green);
            var glowGO = new GameObject("StatusGlow");
            glowGO.transform.SetParent(t, false);
            glowGO.transform.localPosition = new Vector3(0.14f, 0.23f, 0.3f);
            _statusGlow = glowGO.AddComponent<Light>();
            _statusGlow.type = LightType.Point;
            _statusGlow.range = 1.5f;
            _statusGlow.shadows = LightShadows.None;
            _statusGlow.renderMode = LightRenderMode.ForcePixel;

            // Warning triangle, "FUSE BOX" plate and hazard stripes on the door.
            Decal(t, WarningSprite(), new Vector3(-0.02f, 0.1f, 0.133f), 0.2f, 0.18f);
            Decal(t, StripeSprite(), new Vector3(0f, -0.22f, 0.133f), 0.38f, 0.06f);
            Part(t, new Vector3(0f, -0.08f, 0.133f), new Vector3(0.3f, 0.075f, 0.004f), Mat.Unlit(new Color(1f, 0.82f, 0.1f)));
            var label = WorldText.Create(t, "FUSE BOX", new Color(0.05f, 0.05f, 0.05f), 0.05f, TextAnchor.MiddleCenter, 0.3f);
            label.fontStyle = FontStyle.Bold;
            label.canvas.transform.localPosition = new Vector3(0f, -0.08f, 0.139f);
            label.canvas.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // world UI reads from behind its forward

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [FuseBox] Box placed on the wall at " + WallPoint + ".");
        }

        private static GameObject Part(Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().material = mat;
            Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>A flat picture on the door, as world-space UI (always draws, depth-tested).</summary>
        private static void Decal(Transform parent, Sprite sprite, Vector3 pos, float width, float height)
        {
            const float SCALE = 0.002f;
            var go = new GameObject("Decal", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            go.transform.localScale = Vector3.one * SCALE;
            go.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)go.transform).sizeDelta = new Vector2(width / SCALE, height / SCALE);

            var img = new GameObject("Image", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            img.transform.SetParent(go.transform, false);
            var rt = (RectTransform)img.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var image = img.GetComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
        }

        private static Sprite _warning, _stripes;

        /// <summary>A yellow warning triangle with a black border and a lightning bolt.</summary>
        private static Sprite WarningSprite()
        {
            if (_warning != null) return _warning;
            const int W = 128, H = 112;
            var tex = new Texture2D(W, H, TextureFormat.ARGB32, false);
            Color yellow = new Color(1f, 0.82f, 0.1f), black = new Color(0.05f, 0.05f, 0.05f);
            Vector2 a = new Vector2(64f, 108f), b = new Vector2(4f, 4f), c = new Vector2(124f, 4f); // apex up
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    if (!InTriangle(p, a, b, c)) { tex.SetPixel(x, y, Color.clear); continue; }
                    float d = Mathf.Min(EdgeDist(p, a, b), Mathf.Min(EdgeDist(p, b, c), EdgeDist(p, c, a)));
                    tex.SetPixel(x, y, d < 9f || InBolt(p) ? black : yellow);
                }
            tex.Apply();
            return _warning = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f));
        }

        // A zig-zag bolt: two slanted bars joined by a short horizontal one.
        private static bool InBolt(Vector2 p)
        {
            bool upper = p.y > 46f && p.y < 86f && Mathf.Abs(p.x - (74f - (p.y - 46f) * 0.45f)) < 6f;
            bool bar = p.y > 42f && p.y < 52f && p.x > 52f && p.x < 80f;
            bool lower = p.y > 18f && p.y <= 46f && Mathf.Abs(p.x - (62f - (p.y - 46f) * 0.45f)) < 6f;
            return upper || bar || lower;
        }

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        private static float Cross(Vector2 p, Vector2 a, Vector2 b) { return (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y); }

        private static float EdgeDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Yellow and black hazard stripes.</summary>
        private static Sprite StripeSprite()
        {
            if (_stripes != null) return _stripes;
            const int W = 128, H = 20;
            var tex = new Texture2D(W, H, TextureFormat.ARGB32, false);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    tex.SetPixel(x, y, Mathf.Repeat(x + y, 20f) < 10f ? new Color(1f, 0.82f, 0.1f) : new Color(0.05f, 0.05f, 0.05f));
            tex.Apply();
            return _stripes = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f));
        }

        // ── The objective ────────────────────────────────────────────────────
        public void StartObjective()
        {
            CleanUp();
            if (_model == null) BuildModel();

            // Just the interaction trigger in front of the (permanent) box.
            _fuseBoxGO = new GameObject("KE_FuseBox");
            _fuseBoxGO.transform.position = FixedPosition;

            var col = _fuseBoxGO.AddComponent<BoxCollider>();
            col.size = new Vector3(0.4f, 0.6f, 0.5f); // generous, independent of the thin visual — same lesson as the tapes' oversized-collider fix
            col.isTrigger = true;

            Interactable[] existing = FindObjectsOfType<Interactable>();
            if (existing.Length > 0)
            {
                _fuseBoxGO.layer = existing[0].gameObject.layer;
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Fuse box using layer " + _fuseBoxGO.layer + " (copied from '" + existing[0].gameObject.name + "')");
            }

            _interactable = _fuseBoxGO.AddComponent<Interactable>();
            _interactable.actionText = "Restart Fuse Box";
            _interactable.holdTime = HOLD_TIME;
            _interactable.highlight = true;
            _interactable.setInactiveOnInteract = false;
            _interactable.honks = false;
            _interactable.newStateOnInteract = Interactable.State.Enabled;
            _interactable.currentState = Interactable.State.Enabled;
            _interactable.holdAudioName = string.Empty;
            _interactable.onInteractEvent = new UnityEngine.Events.UnityEvent();
            _interactable.conditions = new InteractableConditions();
            _interactable.actions = new InteractableActions { interactActions = new InteractableActions.Actions[0] };

            _audioSource = _fuseBoxGO.AddComponent<AudioSource>();
            _audioSource.clip = GetOrCreateBuzzClip();
            _audioSource.loop = true;
            _audioSource.volume = 1f;
            _audioSource.spatialBlend = 1f; // positional, not global
            _audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            _audioSource.maxDistance = 15f;
            _audioSource.playOnAwake = false; // Update() starts/stops it based on real hold state

            _wasHeld = false;

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Fuse box objective started at " + FixedPosition);
        }

        /// <summary>True if the object the player just interacted with was the fuse box.</summary>
        public Vector3? ActivePosition { get { return _fuseBoxGO != null ? (Vector3?)_fuseBoxGO.transform.position : null; } }   // (for the Facility's arrow)

        public bool IsTheFuseBox(Interactable interactable)
        {
            return _fuseBoxGO != null && interactable != null && interactable.gameObject == _fuseBoxGO;
        }

        /// <summary>Ends the objective part (the trigger and its buzz). The box itself stays on the wall.</summary>
        public void CleanUp()
        {
            if (_fuseBoxGO != null) Destroy(_fuseBoxGO); // also destroys its AudioSource/Interactable
            _fuseBoxGO = null;
            _interactable = null;
            _audioSource = null;
            _wasHeld = false;
        }
    }
}
