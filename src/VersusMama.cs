using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Versus, the hunter's side: Mama, or Papa (bought). The hunter can't walk: seen in third
    /// person at one of Mama's real spawn spots (the level's patrol nodes).
    ///  1  MAP: top-down view of the house or basement with a dot on every
    ///     spawn spot; click one to teleport there (15 s cooldown).
    ///  2  KILL FLASHLIGHT: works within 7 m with a clear line of sight (the
    ///     game's own flashlight-off distance); 20 s cooldown.
    ///  Every noise the survivor makes shows as a red ping on her map for a few
    ///  seconds (not while she's away), so she can hunt fairly.
    ///  3  Mama: MAMA'S GAZE — the child (within 5 m, in sight) freezes for 2 s, shaking
    ///     and making noise. Papa: PAPA'S ABSENCE — 30 s where the child can't see him.
    ///  4  KILL METER (passive): each noise the survivor makes within 5 m adds 1 (at most
    ///     every 1.5 s); every 5 s of quiet takes 1 away; at 4
    ///     they're caught.
    ///  A minimap (top right) always shows where the hunter is on their floor, and which way they look.
    /// Uses the game's own camera (so its HUD and effects still work) moved
    /// behind Mama; her task and abilities are drawn on an overlay of our own.
    /// The kill plays in first person: Mama creeps in, lunges, screams, claws
    /// the screen, and the kid falls back.
    /// </summary>
    public class VersusMamaController : MonoBehaviour
    {
        public static VersusMamaController Instance { get; private set; }

        // Camera
        private const float DISTANCE = 2.8f, HEAD_HEIGHT = 1.7f, MOUSE_SPEED = 3f;
        // Abilities
        public const float TELEPORT_COOLDOWN = 15f;
        public const float FLASH_KILL_COOLDOWN = 20f;
        public const float FLASH_KILL_RANGE = 7f;       // PatrolNode.mamaFlashlightOffDist
        public const float NOISE_RANGE = 5f;            // PatrolNode.maxStrikingMamaDist
        public const float NOISE_COOLDOWN = 1.5f;       // at most one meter point per 1.5 s (the game's own Mama: 0.75 s) — steady noise like running takes 6 s to fill it, not 3
        public const float METER_MAX = 4f;
        public const float METER_DECAY_SECONDS = 5f;    // each 5 s of quiet takes a point away
        public const float GAZE_RANGE = 5f, GAZE_STUN = 2f, GAZE_COOLDOWN = 30f;
        public const float ABSENCE_SECONDS = 30f, ABSENCE_COOLDOWN = 90f; // 60 s after it wears off

        private bool _ready, _over, _cinematic;
        private float _settle = 0.5f;
        private GameObject _mama;
        private Camera _cam;
        private float _yaw, _pitch = 12f;

        // Mama sees in the dark: brighter ambient plus a soft light that follows her view.
        private static readonly Color NIGHT_AMBIENT = new Color(0.3f, 0.32f, 0.38f);
        private Light _nightEyes;
        private UnityEngine.Rendering.AmbientMode _oldAmbientMode;
        private Color _oldAmbient;
        private PatrolNode _node;

        private float _teleportCd, _flashCd, _meter, _nextNoiseAt, _lastNoiseAt, _quiet;
        private float _thirdCd, _absentUntil;
        private bool _papa; // hunting as Papa: ability 3 is Papa's Absence instead of Mama's Gaze
        private float _messageTimer;

        // Online: the survivor's game says when Mama may be around (rests, phone break, Music Box, Camera, Totem).
        private bool _present = true;
        private float _pulledUntil;   // Toy Parrot: can't teleport away until then
        private bool _awaitingCatch;  // sent "caught", waiting to hear if a Totem saved them

        // Noise pings on the map.
        private const float PING_SECONDS = 6f;
        private const float PING_MERGE_DIST = 1.5f;
        private const int PING_MAX = 16;
        private class Ping { public Vector3 Pos; public float At; public Image Img; }
        private readonly List<Ping> _pings = new List<Ping>();
        private float _nextHeardHint;

        public bool Ready { get { return _ready && _mama != null; } }
        public bool Present { get { return _present; } }
        public Vector3 BodyPosition { get { return _mama != null ? _mama.transform.position : Vector3.zero; } }
        public float BodyYaw { get { return _yaw; } }

        // UI
        private GameObject _ui, _mapRoot;
        private Font _font;
        private Text _message, _meterText, _mapTitle, _floorButton;
        private Image _mapCdFill, _flashCdFill, _thirdCdFill, _meterFill;
        private Text _mapCdText, _flashCdText, _thirdCdText;
        private RawImage _mapImage, _miniImage;
        private RenderTexture _miniTexture;
        private RectTransform _miniMe, _miniLook;
        private GameObject _miniRoot;
        private int _miniFloor = -1;                    // which floor the minimap shows (VersusMap.FloorOf)
        private Vector3 _mapCenter, _miniCenter;        // the top-down views: center and half-size, for placing dots
        private float _mapSize, _miniSize;
        private readonly List<KeyValuePair<Image, PatrolNode>> _dots = new List<KeyValuePair<Image, PatrolNode>>();
        private int _mapFloor;
        private Camera _mapCam;
        private RenderTexture _mapTexture;
        private static Sprite _dotSprite;

        public static void Create()
        {
            if (Instance != null) return;
            var go = new GameObject("KE_VersusMama");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<VersusMamaController>();
        }

        public static void DestroyInstance()
        {
            if (Instance != null) Destroy(Instance.gameObject);
            Instance = null;
        }

        /// <summary>The survivor made a noise (their footsteps, flashlight shake, Tattletail talking, a loud action).</summary>
        public static void OnSurvivorNoise(Vector3 pos, bool big)
        {
            if (Instance != null) Instance.Noise(pos, big);
        }

        private void OnDestroy()
        {
            MamaController.StopHuntMusic();
            if (_nightEyes != null)
            {
                Destroy(_nightEyes.gameObject);
                RenderSettings.ambientMode = _oldAmbientMode;
                RenderSettings.ambientLight = _oldAmbient;
            }
            if (_mama != null) Destroy(_mama);
            if (_mapCam != null) Destroy(_mapCam.gameObject);
            if (_mapTexture != null) _mapTexture.Release();
            if (_miniTexture != null) _miniTexture.Release();
            if (Instance == this) Instance = null;
        }

        // ── Setup ────────────────────────────────────────────────────────────
        private void Update()
        {
            if (!VersusMode.IsMama) { Destroy(gameObject); return; }
            if (!_ready)
            {
                if (RM.fpsController == null || !GS.acceptInput || !VersusMap.Ready || Camera.main == null) return;
                _settle -= Time.deltaTime;
                if (_settle <= 0f) Setup();
                return;
            }
            if (_over) return;

            _teleportCd = Mathf.Max(0f, _teleportCd - Time.deltaTime);
            _flashCd = Mathf.Max(0f, _flashCd - Time.deltaTime);
            _thirdCd = Mathf.Max(0f, _thirdCd - Time.deltaTime);
            if (_meter > 0f)
            {
                _quiet += Time.deltaTime;
                if (_quiet >= METER_DECAY_SECONDS) { _quiet = 0f; _meter = Mathf.Max(0f, _meter - 1f); }
            }

            if (!_present) { if (_mapRoot != null && _mapRoot.activeSelf) CloseMap(); }
            else if (_mapRoot != null && _mapRoot.activeSelf) UpdateMap();
            else
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) OpenMap();
                else if (Input.GetKeyDown(KeyCode.Alpha2)) FlashKill();
                else if (Input.GetKeyDown(KeyCode.Alpha3)) { if (_papa) Absence(); else Gaze(); }
            }

            UpdatePings();
            UpdateMinimap();
            RefreshBar();
            if (_messageTimer > 0f)
            {
                _messageTimer -= Time.deltaTime;
                if (_messageTimer <= 0f) _message.text = "";
            }
        }

        private void Setup()
        {
            _ready = true;
            var fps = RM.fpsController;
            Vector3 survivorStart = fps.transform.position;

            _node = VersusBots.RandomNode();
            if (_node == null) { KaleidoscopeEndlessPlugin.Log.LogError("KE: [Versus] No Mama spawn spots in this level."); return; }
            _papa = VersusHunters.PlayAsPapa;
            _mama = VersusHunters.BuildBody("KE_VersusMama_Player", VersusHunters.LocalCode);
            MoveTo(_node);

            // The player's own body: switched off and invisible. The game camera (a child of it)
            // is moved behind Mama every frame instead, so the HUD and effects keep working.
            fps.enabled = false;
            var cc = fps.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false; // or its capsule gets in the camera's way
            foreach (var r in fps.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            _cam = Camera.main;
            SetupNightEyes();

            if (VersusMode.OpponentIsBot) VersusBots.PlaceSurvivorBlob(survivorStart);

            BuildUI();
            LockCursor(true);
            if (VersusMode.OpponentIsBot) MamaController.PlayHuntMusic(); // PLAYTEST: hunting from the start
            if (!VersusMode.OpponentIsBot)
            {
                if (VersusNet.MamaPresence == 1) SetPresent(true); // their task started while this level was still loading
                else SetPresent(false, "Waiting for the child to start their first task...");
            }
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Versus] Mama side ready at " + _node.transform.position + ".");
        }

        private void MoveTo(PatrolNode node)
        {
            if (node != null) KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Versus] Hunter moves to spot '" + node.name + "' at " + node.transform.position + " (floor " + VersusMap.FloorOf(node.transform.position) + ", " + VersusMap.Nodes().Count + " spots in this map).");
            _node = node;
            if (_mama == null) return;
            _mama.transform.position = node.transform.position;
            _yaw = node.transform.eulerAngles.y;
            _mama.transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        // ── Camera ───────────────────────────────────────────────────────────
        private void LateUpdate()
        {
            if (!_ready || _mama == null || _cam == null || _cinematic) return;
            bool mapOpen = _mapRoot != null && _mapRoot.activeSelf;
            if (!mapOpen && !_over)
            {
                _yaw += Input.GetAxis("Mouse X") * MOUSE_SPEED;
                _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * MOUSE_SPEED, -30f, 70f);
                _mama.transform.rotation = Quaternion.Euler(0f, _yaw, 0f); // she faces where you look
            }

            // Over-the-shoulder orbit around her head, pulled in if a wall is in the way.
            Vector3 target = _mama.transform.position + Vector3.up * HEAD_HEIGHT;
            Vector3 back = Quaternion.Euler(_pitch, _yaw, 0f) * Vector3.back;
            float dist = DISTANCE;
            RaycastHit hit;
            if (Physics.SphereCast(target, 0.2f, back, out hit, DISTANCE, ~0, QueryTriggerInteraction.Ignore))
                dist = Mathf.Max(0.3f, hit.distance - 0.1f);
            _cam.transform.position = target + back * dist;
            _cam.transform.LookAt(target);
            if (_nightEyes != null)
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = NIGHT_AMBIENT;
            }
        }

        private void SetupNightEyes()
        {
            _oldAmbientMode = RenderSettings.ambientMode;
            _oldAmbient = RenderSettings.ambientLight;
            var go = new GameObject("KE_MamaNightEyes");
            go.transform.SetParent(_cam.transform, false);
            _nightEyes = go.AddComponent<Light>();
            _nightEyes.type = LightType.Point;
            _nightEyes.range = 16f;
            _nightEyes.intensity = 0.9f;
            _nightEyes.color = new Color(0.75f, 0.82f, 1f);
            _nightEyes.shadows = LightShadows.None;
            _nightEyes.renderMode = LightRenderMode.ForcePixel;
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        // ── 2: kill the flashlight ───────────────────────────────────────────
        private void FlashKill()
        {
            if (_flashCd > 0f) { Say((VersusMode.Facility ? "Jam Camcorder" : "Kill Flashlight") + " is recharging (" + Mathf.CeilToInt(_flashCd) + "s)"); return; }
            Vector3? target = SurvivorPosition();
            if (target == null) { Say("Nobody to find..."); return; }

            Vector3 head = _mama.transform.position + Vector3.up * HEAD_HEIGHT;
            Vector3 chest = target.Value + Vector3.up * 0.4f;
            if (Vector3.Distance(head, chest) > FLASH_KILL_RANGE) { Say("Too far away. Get within " + (int)FLASH_KILL_RANGE + " m."); return; }
            if (Physics.Linecast(head, chest, ~0, QueryTriggerInteraction.Ignore)) { Say("You can't see them from here."); return; }

            _flashCd = FLASH_KILL_COOLDOWN;
            AudioController.Play("mama_alert_enter", _mama.transform);
            VersusNet.SendFlashKill();
            Say(VersusMode.Facility ? "You jammed their camcorder!" : "You killed their flashlight!");
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Versus] Mama killed the survivor's flashlight.");
            // Online play: this is where the survivor's flashlight gets killed on their side.
        }

        // ── 3: Mama's Gaze / Papa's Absence ─────────────────────────────────
        private void Gaze()
        {
            if (_thirdCd > 0f) { Say("Mama's Gaze is recharging (" + Mathf.CeilToInt(_thirdCd) + "s)"); return; }
            Vector3? target = SurvivorPosition();
            if (target == null) { Say("Nobody to find..."); return; }
            Vector3 head = _mama.transform.position + Vector3.up * HEAD_HEIGHT;
            Vector3 chest = target.Value + Vector3.up * 0.6f;
            if (Vector3.Distance(head, chest) > GAZE_RANGE + 1f) { Say("Too far away. Get within " + (int)GAZE_RANGE + " m."); return; }
            if (Physics.Linecast(head, chest, ~0, QueryTriggerInteraction.Ignore)) { Say("You can't see them from here."); return; }

            _thirdCd = GAZE_COOLDOWN;
            AudioController.Play("mama_alert_enter", _mama.transform);
            VersusNet.SendGaze(GAZE_STUN);
            var kid = VersusAvatars.VisiblePerson;
            var kidAnim = kid != null ? kid.GetComponent<KidAnimator>() : null;
            if (kidAnim != null) kidAnim.Scare(GAZE_STUN);
            Say("Your gaze froze them in fear!");
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Versus] Mama's Gaze.");
        }

        private void Absence()
        {
            if (Time.time < _absentUntil) { Say("You're already invisible."); return; }
            if (_thirdCd > 0f) { Say("Papa's Absence is recharging (" + Mathf.CeilToInt(_thirdCd) + "s)"); return; }
            _thirdCd = ABSENCE_COOLDOWN;
            _absentUntil = Time.time + ABSENCE_SECONDS;
            VersusNet.SendAbsence(ABSENCE_SECONDS);
            Say("Papa's Absence: the child can't see you for " + (int)ABSENCE_SECONDS + " seconds.", 3f);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Versus] Papa's Absence.");
        }

        // Solo playtest: the bot. Online: the other player's last position in this match.
        private static Vector3? SurvivorPosition()
        {
            if (VersusMode.OpponentIsBot) return VersusBots.SurvivorBotPosition;
            if (VersusNet.RemoteFresh && VersusNet.RemoteCtx == VersusNet.CTX_MATCH) return VersusNet.RemotePos;
            return null;
        }

        // ── 3: kill meter ────────────────────────────────────────────────────
        private void Noise(Vector3 pos, bool big)
        {
            if (!_ready || _over || _mama == null || !_present) return;
            AddPing(pos);
            if (_awaitingCatch || Time.time < _nextNoiseAt) return;
            float dist = Vector3.Distance(pos, _mama.transform.position);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Versus] Survivor noise " + dist.ToString("F1") + " m away" + (dist > NOISE_RANGE ? " — too far to count." : "."));
            if (dist > NOISE_RANGE) return;
            _nextNoiseAt = Time.time + NOISE_COOLDOWN;
            _lastNoiseAt = Time.time;
            _quiet = 0f;
            _meter = Mathf.Min(METER_MAX, _meter + 1f); // every noise counts once, loud or not
            if (_meter >= METER_MAX) Catch();
        }

        private void Catch()
        {
            if (!VersusMode.OpponentIsBot)
            {
                _awaitingCatch = true;
                VersusNet.SendCaught();
                AudioController.Play("mama_alert_enter", _mama.transform);
                Say("Got them...!", 3f);
                return;
            }
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Versus] Kill meter full — the survivor is caught.");
            PlayKill("CAUGHT! The child is yours.");
        }

        private System.Collections.IEnumerator BackToArcade(float delay)
        {
            yield return new WaitForSeconds(delay);
            SceneMonitor.ResetForNewRun();
            HubMode.Launch(null);
        }

        // ── The kill, from Mama's eyes ───────────────────────────────────────
        private const float EYE_HEIGHT = 1.75f;
        private static Sprite _clawSprite;

        /// <summary>The child is caught: first person, a creep, a lunge with the jumpscare scream,
        /// three claw slashes across the screen, the kid falling back, then back to the Arcade.</summary>
        public void PlayKill(string message)
        {
            if (_over || _mama == null) { EndRound(message); return; }
            _over = true;
            StartCoroutine(KillRoutine(message));
        }

        private System.Collections.IEnumerator KillRoutine(string message)
        {
            if (_mapRoot != null && _mapRoot.activeSelf) CloseMap();
            GameObject kid = VersusMode.OpponentIsBot ? VersusBots.SurvivorBot : VersusAvatars.VisiblePerson;
            if (kid != null && !VersusMode.OpponentIsBot) VersusAvatars.HoldPerson(7f);
            Vector3 kidPos;
            if (kid != null) kidPos = kid.transform.position;
            else
            {
                Vector3? known = SurvivorPosition();
                kidPos = known.HasValue ? known.Value : _mama.transform.position + _mama.transform.forward * 2f;
            }
            KidAnimator anim = kid != null ? kid.GetComponent<KidAnimator>() : null;

            _cinematic = true;
            if (_ui != null) _ui.SetActive(false);
            MamaController.StopHuntMusic();

            // Mama faces the kid, the kid faces Mama; the camera goes into her eyes.
            Vector3 flat = kidPos - _mama.transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) flat = _mama.transform.forward;
            Vector3 dir = flat.normalized;
            _mama.transform.rotation = Quaternion.LookRotation(dir);
            if (kid != null) kid.transform.rotation = Quaternion.LookRotation(-dir);
            foreach (var r in _mama.GetComponentsInChildren<Renderer>()) r.enabled = false;

            Vector3 eye = _mama.transform.position + Vector3.up * EYE_HEIGHT + dir * 0.25f;
            var kidLook = kid != null ? kid.GetComponent<KidLook>() : null;
            Vector3 face = kidPos + Vector3.up * ChildModel.FaceHeightOf(kidLook != null ? kidLook.Gender : KidGender.Boy);
            Vector3 hit = face - (face - eye).normalized * 0.4f;
            Vector3 creep = Vector3.Lerp(eye, hit, 0.12f);
            Vector3 over = kidPos - dir * 0.3f + Vector3.up * 1.35f;    // standing over them
            Vector3 lying = kidPos + dir * 0.45f + Vector3.up * 0.12f;  // they fall away from her

            // Overlay: red flash, three claw slashes, the result.
            var fx = new GameObject("KE_MamaKillFX", typeof(RectTransform));
            fx.transform.SetParent(transform, false);
            var canvas = fx.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 960;
            var scaler = fx.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            var flash = new GameObject("Flash", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            flash.transform.SetParent(fx.transform, false);
            Stretch(flash.rectTransform);
            flash.color = new Color(0.6f, 0f, 0.02f, 0f);
            flash.raycastTarget = false;
            var claws = new Image[3];
            Vector2 slash = new Vector2(Mathf.Cos(-32f * Mathf.Deg2Rad), Mathf.Sin(-32f * Mathf.Deg2Rad));
            Vector2 across = new Vector2(-slash.y, slash.x);
            for (int i = 0; i < 3; i++)
            {
                claws[i] = new GameObject("Claw" + i, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                claws[i].transform.SetParent(fx.transform, false);
                claws[i].sprite = ClawSprite();
                claws[i].raycastTarget = false;
                var rt = claws[i].rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(1250f, 80f);
                rt.anchoredPosition = across * ((i - 1) * 110f) - slash * 625f;
                rt.localRotation = Quaternion.Euler(0f, 0f, -32f);
                rt.localScale = new Vector3(0f, 1f, 1f);
            }
            var result = MakeText(fx.transform, "", 48, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.85f));
            Place(result.rectTransform, new Vector2(0.5f, 0.22f), Vector2.zero, new Vector2(1400f, 120f));

            float start = Time.time;
            bool flinched = false, screamed = false;
            while (true)
            {
                float t = Time.time - start;
                Vector3 pos, look = face;
                float shake;
                if (t < 0.7f) { pos = Vector3.Lerp(eye, creep, Smooth(t / 0.7f)); shake = 0.004f; }
                else if (t < 1.0f) { float k = (t - 0.7f) / 0.3f; pos = Vector3.Lerp(creep, hit, k * k * k); shake = 0.004f + 0.03f * k; }
                else
                {
                    float k = Smooth((t - 1.4f) / 1.4f);
                    pos = Vector3.Lerp(hit, over, k);
                    look = Vector3.Lerp(face, lying, k);
                    shake = 0.07f * Mathf.Exp(-(t - 1.0f) * 5f);
                }
                if (!flinched && t >= 0.25f) { flinched = true; if (anim != null) anim.Flinch(); if (kid != null) KidVoice.RemoteAt(KidVoice.Kind.Caught, face); }
                if (!screamed && t >= 0.7f) { screamed = true; PlayScream(); }

                _cam.transform.position = pos + Random.insideUnitSphere * shake;
                _cam.transform.rotation = Quaternion.LookRotation(look - pos);

                float since = t - 1.0f;
                flash.color = new Color(0.6f, 0f, 0.02f, since < 0f ? 0f : 0.7f * Mathf.Exp(-since * 2.5f));
                for (int i = 0; i < 3; i++)
                {
                    float grow = Mathf.Clamp01((since - i * 0.05f) / 0.09f);
                    claws[i].rectTransform.localScale = new Vector3(grow, 1f, 1f);
                    claws[i].color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((t - 1.9f) / 0.8f));
                }
                if (t >= 2.2f && result.text == "") result.text = message;
                if (t >= 5.2f) break;
                yield return null;
            }
            SceneMonitor.ResetForNewRun();
            HubMode.Launch(null);
        }

        private static float Smooth(float k) { k = Mathf.Clamp01(k); return k * k * (3f - 2f * k); }

        // The game's own jumpscare scream (the clip on its JumpscareManager).
        private void PlayScream()
        {
            AudioClip clip = RM.jumpscare != null && RM.jumpscare.source != null ? RM.jumpscare.source.clip : null;
            if (clip == null) { AudioController.Play("mama_alert_enter", _mama.transform); return; }
            var src = gameObject.AddComponent<AudioSource>();
            src.spatialBlend = 0f;
            src.PlayOneShot(clip);
        }

        // A tapered claw streak: thick near the start, dark red edges, a brighter wet core.
        private static Sprite ClawSprite()
        {
            if (_clawSprite != null) return _clawSprite;
            const int W = 256, H = 32;
            var tex = new Texture2D(W, H, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < W; x++)
            {
                float u = x / (W - 1f);
                float half = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 0.6f) * (1f - 0.45f * u);
                for (int y = 0; y < H; y++)
                {
                    float v = Mathf.Abs((y + 0.5f) / H * 2f - 1f);
                    Color c = Color.Lerp(new Color(0.3f, 0f, 0.02f), new Color(0.9f, 0.06f, 0.08f), Mathf.Clamp01(1f - v / Mathf.Max(half, 0.01f)));
                    c.a = Mathf.Clamp01((half - v) * H * 0.5f);
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return _clawSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0f, 0.5f));
        }

        // ── Noise pings ──────────────────────────────────────────────────────
        private void AddPing(Vector3 pos)
        {
            foreach (var p in _pings)
            {
                if (Vector3.Distance(p.Pos, pos) > PING_MERGE_DIST) continue;
                p.Pos = pos; // same spot again: refresh it
                p.At = Time.time;
                return;
            }
            if (_pings.Count >= PING_MAX) RemovePing(0);
            _pings.Add(new Ping { Pos = pos, At = Time.time });

            bool mapOpen = _mapRoot != null && _mapRoot.activeSelf;
            if (!mapOpen && Time.time >= _nextHeardHint)
            {
                _nextHeardHint = Time.time + 4f;
                Say("You heard the child! Press 1 to see where.", 2.5f);
            }
        }

        private void RemovePing(int i)
        {
            if (_pings[i].Img != null) Destroy(_pings[i].Img.gameObject);
            _pings.RemoveAt(i);
        }

        private void UpdatePings()
        {
            for (int i = _pings.Count - 1; i >= 0; i--)
                if (Time.time - _pings[i].At > PING_SECONDS) RemovePing(i);

            if (_mapRoot == null || !_mapRoot.activeSelf || _mapCam == null) return;
            var imgRT = _mapImage.rectTransform;
            foreach (var p in _pings)
            {
                if (p.Img == null)
                {
                    var go = new GameObject("NoisePing", typeof(RectTransform), typeof(Image));
                    go.transform.SetParent(_mapImage.transform, false);
                    p.Img = go.GetComponent<Image>();
                    p.Img.sprite = DotSprite();
                    p.Img.raycastTarget = false;
                }
                bool onFloor = VersusMap.FloorOf(p.Pos) == _mapFloor;
                p.Img.gameObject.SetActive(onFloor);
                if (!onFloor) continue;
                float age = Time.time - p.At;
                Vector2 vp = OnMap(p.Pos, _mapCenter, _mapSize);
                var rt = p.Img.rectTransform;
                rt.anchoredPosition = new Vector2((vp.x - 0.5f) * imgRT.sizeDelta.x, (vp.y - 0.5f) * imgRT.sizeDelta.y);
                float pulse = 34f + Mathf.Sin(age * 8f) * 6f;
                rt.sizeDelta = new Vector2(pulse, pulse);
                p.Img.color = new Color(1f, 0.1f, 0.15f, Mathf.Clamp01(1f - age / PING_SECONDS));
            }
        }

        // ── Online: driven by the survivor's game ────────────────────────────
        /// <summary>Away (rest, phone break, Music Box, Flash Camera, Totem) or back (a new objective: a random spot).</summary>
        public void SetPresent(bool present, string awayText = "The child is safe for now... you're gone until their next task.")
        {
            if (_over) return;
            _present = present;
            if (_mama != null) _mama.SetActive(present);
            if (present)
            {
                MamaController.PlayHuntMusic(); // Mama hears the hunt too
                PatrolNode node = VersusBots.RandomNode();
                if (node != null) MoveTo(node);
                Say("You're back. Hunt them!", 3f);
            }
            else
            {
                MamaController.StopHuntMusic();
                _meter = 0f;
                while (_pings.Count > 0) RemovePing(0);
                Say(awayText, 30f);
            }
        }

        /// <summary>Toy Parrot: pulled to the spawn spot nearest it, and held there for its duration.</summary>
        public void PulledByParrot(Vector3 pos, float seconds)
        {
            if (!_present || !VersusMap.Ready) return;
            int floor = VersusMap.FloorOf(pos);
            PatrolNode best = null;
            float bestDist = float.MaxValue;
            foreach (var n in VersusMap.Nodes())
            {
                if (n == null || VersusMap.FloorOf(n.transform.position) != floor) continue;
                float d = Vector3.Distance(n.transform.position, pos);
                if (d < bestDist) { bestDist = d; best = n; }
            }
            if (best != null) MoveTo(best);
            _pulledUntil = Time.time + seconds;
            Say("A toy parrot is crying... it drags you to it!", 4f);
        }

        public void OnSaved()
        {
            _awaitingCatch = false;
            _meter = 0f;
            Say("A Totem of Undying saved them!", 4f);
        }

        public void EndRound(string message)
        {
            if (_over) return;
            _over = true;
            MamaController.StopHuntMusic();
            Say(message, 6f);
            StartCoroutine(BackToArcade(4f));
        }

        // ── 1: map + teleport ────────────────────────────────────────────────
        private void OpenMap()
        {
            if (_mapRoot == null) BuildMap();
            _mapFloor = _node != null ? VersusMap.FloorOf(_node.transform.position) : 0; // open on the floor she is on
            ShowFloor();
            _mapRoot.SetActive(true);
            LockCursor(false);
        }

        private void CloseMap()
        {
            _mapRoot.SetActive(false);
            LockCursor(true);
        }

        private void UpdateMap()
        {
            _mapTitle.text = _teleportCd > 0f
                ? "TELEPORT RECHARGING (" + Mathf.CeilToInt(_teleportCd) + "s)"
                : "CLICK A SPOT TO TELEPORT";
            foreach (var d in _dots)
                d.Key.color = d.Value == _node ? new Color(1f, 0.25f, 0.35f) : (_teleportCd > 0f ? new Color(0.6f, 0.6f, 0.6f, 0.7f) : Color.white);

            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetMouseButtonDown(1)) { CloseMap(); return; }
            if (!Input.GetMouseButtonDown(0)) return;

            if (Hit(_floorButton.rectTransform)) { _mapFloor = (_mapFloor + 1) % VersusMap.FloorCount; ShowFloor(); return; }
            foreach (var d in _dots)
            {
                if (!d.Key.gameObject.activeSelf || !Hit(d.Key.rectTransform)) continue;
                if (d.Value == _node) return;
                if (Time.time < _pulledUntil) { Say("The parrot's crying won't let you leave (" + Mathf.CeilToInt(_pulledUntil - Time.time) + "s)"); return; }
                if (_teleportCd > 0f) { Say("Teleport is recharging (" + Mathf.CeilToInt(_teleportCd) + "s)"); return; }
                MoveTo(d.Value);
                _teleportCd = TELEPORT_COOLDOWN;
                AudioController.Play("vhs_glitch", _mama.transform);
                CloseMap();
                return;
            }
        }

        private static bool Hit(RectTransform rt) { return RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, null); }

        private void ShowFloor()
        {
            _floorButton.text = "[ SHOW " + VersusMap.FloorName((_mapFloor + 1) % VersusMap.FloorCount) + " ]";
            var nodes = VersusMap.FloorNodes(_mapFloor);
            RenderFloor(nodes, _mapTexture, out _mapCenter, out _mapSize);
            var imgRT = _mapImage.rectTransform;
            for (int i = 0; i < _dots.Count; i++)
            {
                PatrolNode n = _dots[i].Value;
                bool onFloor = nodes.Contains(n);
                _dots[i].Key.gameObject.SetActive(onFloor);
                if (!onFloor) continue;
                Vector2 vp = OnMap(n.transform.position, _mapCenter, _mapSize);
                _dots[i].Key.rectTransform.anchoredPosition = new Vector2((vp.x - 0.5f) * imgRT.sizeDelta.x, (vp.y - 0.5f) * imgRT.sizeDelta.y);
            }
        }

        /// <summary>A top-down picture of one floor: an orthographic camera above the roof whose near clip cuts everything above head height, lit by a temporary light.</summary>
        private void RenderFloor(List<PatrolNode> nodes, RenderTexture target, out Vector3 center, out float size)
        {
            center = Vector3.zero; size = 1f;
            if (nodes.Count == 0) return;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            var ys = new List<float>();
            foreach (var n in nodes)
            {
                Vector3 p = n.transform.position;
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
                ys.Add(p.y);
            }
            ys.Sort();
            float floorY = ys[ys.Count / 2];

            if (_mapCam == null)
            {
                var go = new GameObject("KE_VersusMapCamera");
                _mapCam = go.AddComponent<Camera>();
                _mapCam.enabled = false; // rendered by hand, once per floor switch
                _mapCam.orthographic = true;
                _mapCam.clearFlags = CameraClearFlags.SolidColor;
                _mapCam.backgroundColor = new Color(0.06f, 0.07f, 0.1f);
            }
            const float HEIGHT = 20f;
            center = new Vector3((minX + maxX) / 2f, floorY, (minZ + maxZ) / 2f);
            size = Mathf.Max(maxX - minX, maxZ - minZ) / 2f + 3f;
            _mapCam.targetTexture = target;
            _mapCam.transform.position = center + Vector3.up * HEIGHT;
            _mapCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // straight down, +Z up on the map
            _mapCam.orthographicSize = size;
            _mapCam.nearClipPlane = HEIGHT - 2.2f; // cuts the ceiling and anything above head height
            _mapCam.farClipPlane = HEIGHT + 1f;

            var lightGO = new GameObject("KE_VersusMapLight");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(0.9f, 0.95f, 1f);
            lightGO.transform.rotation = Quaternion.Euler(70f, 30f, 0f);
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            _mapCam.Render();
            RenderSettings.fog = fog;
            Destroy(lightGO);
        }

        /// <summary>A world spot on a top-down view (0..1 across, +Z up).</summary>
        private static Vector2 OnMap(Vector3 p, Vector3 center, float size)
        {
            return new Vector2((p.x - center.x) / (2f * size) + 0.5f, (p.z - center.z) / (2f * size) + 0.5f);
        }

        // ── Minimap: always on, the hunter's own floor, where they are and where they look ──
        private const float MINI = 300f;

        private void BuildMinimap()
        {
            _miniRoot = new GameObject("Minimap", typeof(RectTransform), typeof(RawImage), typeof(Outline));
            _miniRoot.transform.SetParent(_ui.transform, false);
            Place((RectTransform)_miniRoot.transform, new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(MINI, MINI));
            _miniImage = _miniRoot.GetComponent<RawImage>();
            _miniImage.color = new Color(0.85f, 0.9f, 1f);
            _miniImage.raycastTarget = false;
            _miniRoot.GetComponent<Outline>().effectColor = new Color(0.85f, 0.3f, 0.45f, 0.9f);
            _miniTexture = new RenderTexture(512, 512, 16);
            _miniImage.texture = _miniTexture;

            // Which way you look: a short bar from the dot. Then the dot on top.
            var look = new GameObject("Look", typeof(RectTransform), typeof(Image));
            look.transform.SetParent(_miniRoot.transform, false);
            _miniLook = (RectTransform)look.transform;
            _miniLook.anchorMin = _miniLook.anchorMax = new Vector2(0.5f, 0.5f);
            _miniLook.pivot = new Vector2(0.5f, 0f);
            _miniLook.sizeDelta = new Vector2(6f, 26f);
            look.GetComponent<Image>().color = new Color(1f, 0.35f, 0.45f, 0.9f);
            look.GetComponent<Image>().raycastTarget = false;

            var me = new GameObject("You", typeof(RectTransform), typeof(Image));
            me.transform.SetParent(_miniRoot.transform, false);
            _miniMe = (RectTransform)me.transform;
            _miniMe.anchorMin = _miniMe.anchorMax = new Vector2(0.5f, 0.5f);
            _miniMe.sizeDelta = new Vector2(24f, 24f);
            var img = me.GetComponent<Image>();
            img.sprite = DotSprite();
            img.color = new Color(1f, 0.25f, 0.35f);
            img.raycastTarget = false;

            var label = MakeText(_miniRoot.transform, "YOU", 18, TextAnchor.UpperLeft, new Color(1f, 0.85f, 0.9f));
            Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(8f, -6f), new Vector2(120f, 24f));
        }

        private void UpdateMinimap()
        {
            if (_mama == null || !VersusMap.Ready) return;
            if (_miniRoot == null) BuildMinimap();
            bool mapOpen = _mapRoot != null && _mapRoot.activeSelf;
            _miniRoot.SetActive(!mapOpen && !_cinematic);
            if (mapOpen || _cinematic) return;

            Vector3 pos = _mama.transform.position;
            int floor = VersusMap.FloorOf(pos);
            if (_miniFloor != floor) // a new floor: take its picture (once)
            {
                _miniFloor = floor;
                RenderFloor(VersusMap.FloorNodes(floor), _miniTexture, out _miniCenter, out _miniSize);
            }
            Vector2 vp = OnMap(pos, _miniCenter, _miniSize);
            var at = new Vector2((Mathf.Clamp01(vp.x) - 0.5f) * MINI, (Mathf.Clamp01(vp.y) - 0.5f) * MINI);
            _miniMe.anchoredPosition = at;
            _miniLook.anchoredPosition = at;
            _miniLook.localRotation = Quaternion.Euler(0f, 0f, -_yaw); // +Z is up on the map; yaw turns clockwise
        }

        // ── UI ───────────────────────────────────────────────────────────────
        private void Say(string text, float seconds = 2.5f)
        {
            _message.text = text;
            _messageTimer = seconds;
        }

        private void BuildUI()
        {
            _font = MainMenuButtonInjector.RealFont != null ? MainMenuButtonInjector.RealFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
            _ui = new GameObject("KE_VersusMamaUI", typeof(RectTransform));
            _ui.transform.SetParent(transform, false);
            var canvas = _ui.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 950;
            var scaler = _ui.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            Transform root = _ui.transform;

            // Task, top-center.
            var task = MakeText(root, (_papa ? "YOU ARE PAPA" : "YOU ARE MAMA") + (VersusMode.Facility ? "\nHunt the child in the Waygetter Facility: fill the kill meter with their noise, or jam their camcorder." : "\nHunt the child: fill the kill meter with their noise, or kill their flashlight."), 26, TextAnchor.UpperCenter, new Color(1f, 0.8f, 0.85f));
            Place(task.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(1400f, 90f));

            // Ability bar, bottom-center.
            const float SLOT = 86f, GAP = 14f;
            Slot(root, -1.5f * (SLOT + GAP), "1", "MAP", out _mapCdFill, out _mapCdText);
            Slot(root, -0.5f * (SLOT + GAP), "2", VersusMode.Facility ? "JAM\nCAM" : "KILL\nLIGHT", out _flashCdFill, out _flashCdText);
            Slot(root, 0.5f * (SLOT + GAP), "3", _papa ? "PAPA'S\nABSENCE" : "MAMA'S\nGAZE", out _thirdCdFill, out _thirdCdText);
            Text meterKey;
            var meterSlot = Slot(root, 1.5f * (SLOT + GAP), "4", "", out _meterFill, out meterKey);
            _meterFill.color = new Color(0.85f, 0.1f, 0.2f, 0.75f);
            meterKey.text = "";
            _meterText = MakeText(meterSlot, "0/" + (int)METER_MAX, 22, TextAnchor.MiddleCenter, Color.white);
            Stretch(_meterText.rectTransform);

            _message = MakeText(root, "", 26, TextAnchor.LowerCenter, new Color(1f, 0.95f, 0.7f));
            Place(_message.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 26f + SLOT + 16f), new Vector2(1200f, 60f));
        }

        private Transform Slot(Transform root, float x, string key, string label, out Image fill, out Text countdown)
        {
            var slot = new GameObject("Slot" + key, typeof(RectTransform), typeof(Image), typeof(Outline));
            slot.transform.SetParent(root, false);
            Place((RectTransform)slot.transform, new Vector2(0.5f, 0f), new Vector2(x, 26f), new Vector2(86f, 86f));
            ((RectTransform)slot.transform).pivot = new Vector2(0.5f, 0f);
            slot.GetComponent<Image>().color = new Color(0.05f, 0.03f, 0.05f, 0.8f);
            slot.GetComponent<Image>().raycastTarget = false;
            slot.GetComponent<Outline>().effectColor = new Color(0.85f, 0.3f, 0.45f, 0.9f);

            var name = MakeText(slot.transform, label, 17, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.92f));
            Stretch(name.rectTransform);

            var f = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            f.transform.SetParent(slot.transform, false);
            var frt = (RectTransform)f.transform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(1f, 0f); frt.offsetMin = frt.offsetMax = Vector2.zero;
            fill = f.GetComponent<Image>();
            fill.color = new Color(0f, 0f, 0f, 0.72f);
            fill.raycastTarget = false;

            countdown = MakeText(slot.transform, "", 32, TextAnchor.MiddleCenter, Color.white);
            Stretch(countdown.rectTransform);
            countdown.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);

            var num = MakeText(slot.transform, key, 18, TextAnchor.UpperLeft, new Color(0.9f, 0.85f, 0.9f));
            Place(num.rectTransform, new Vector2(0f, 1f), new Vector2(18f, -14f), new Vector2(30f, 24f));
            return slot.transform;
        }

        private void RefreshBar()
        {
            SetCd(_mapCdFill, _mapCdText, _teleportCd, TELEPORT_COOLDOWN);
            SetCd(_flashCdFill, _flashCdText, _flashCd, FLASH_KILL_COOLDOWN);
            SetCd(_thirdCdFill, _thirdCdText, _thirdCd, _papa ? ABSENCE_COOLDOWN : GAZE_COOLDOWN);
            if (Time.time < _absentUntil) _thirdCdText.text = "<color=#9fe3ff>" + Mathf.CeilToInt(_absentUntil - Time.time) + "</color>"; // invisible: time left
            _meterFill.rectTransform.anchorMax = new Vector2(1f, _meter / METER_MAX);
            _meterText.text = Mathf.FloorToInt(_meter) + "/" + (int)METER_MAX;
        }

        private static void SetCd(Image fill, Text text, float cd, float total)
        {
            float frac = cd > 0f ? Mathf.Clamp01(cd / total) : 0f;
            fill.rectTransform.anchorMax = new Vector2(1f, frac);
            fill.enabled = frac > 0f;
            text.text = cd > 0f ? Mathf.CeilToInt(cd).ToString() : "";
        }

        private void BuildMap()
        {
            _mapRoot = new GameObject("Map", typeof(RectTransform), typeof(Image));
            _mapRoot.transform.SetParent(_ui.transform, false);
            var rt = (RectTransform)_mapRoot.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            _mapRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            var imgGO = new GameObject("Picture", typeof(RectTransform), typeof(RawImage), typeof(Outline));
            imgGO.transform.SetParent(_mapRoot.transform, false);
            Place((RectTransform)imgGO.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(820f, 820f));
            _mapImage = imgGO.GetComponent<RawImage>();
            _mapTexture = new RenderTexture(1024, 1024, 16);
            _mapImage.texture = _mapTexture;
            _mapImage.color = new Color(0.85f, 0.9f, 1f); // a cool "sketch" tint
            imgGO.GetComponent<Outline>().effectColor = new Color(0.85f, 0.3f, 0.45f, 0.9f);

            _mapTitle = MakeText(_mapRoot.transform, "", 30, TextAnchor.MiddleCenter, Color.white);
            Place(_mapTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1000f, 50f));
            var hint = MakeText(_mapRoot.transform, "Red = noise the child just made.   1 or right-click to close", 20, TextAnchor.MiddleCenter, new Color(0.75f, 0.75f, 0.8f));
            Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(800f, 30f));
            _floorButton = MakeText(_mapRoot.transform, "", 28, TextAnchor.MiddleCenter, new Color(0.4f, 0.8f, 1f));
            Place(_floorButton.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(400f, 40f));

            foreach (var n in VersusMap.Nodes())
            {
                if (n == null) continue;
                var dot = new GameObject("Spot", typeof(RectTransform), typeof(Image));
                dot.transform.SetParent(imgGO.transform, false);
                var drt = (RectTransform)dot.transform;
                drt.sizeDelta = new Vector2(24f, 24f);
                var img = dot.GetComponent<Image>();
                img.sprite = DotSprite();
                _dots.Add(new KeyValuePair<Image, PatrolNode>(img, n));
            }
        }

        private static Sprite DotSprite()
        {
            if (_dotSprite != null) return _dotSprite;
            const int N = 32;
            var tex = new Texture2D(N, N, TextureFormat.ARGB32, false);
            float r = N / 2f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    float ring = d > r - 5f ? 0.35f : 1f; // darker rim so it reads on light floors
                    tex.SetPixel(x, y, new Color(ring, ring, ring, Mathf.Clamp01(r - d)));
                }
            tex.Apply();
            return _dotSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f));
        }

        private Text MakeText(Transform parent, string text, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = text;
            go.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);
            return t;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
