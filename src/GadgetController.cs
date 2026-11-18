using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Runs the gadget picked in the menu (Gadgets.RunGadget) during an
    /// Endless run: a one-slot bar in the style of Papa's 10th Birthday!'s
    /// hotbar, the 1 key to use it, and a visible 60s cooldown (the slot
    /// darkens and drains while a countdown ticks). Hidden in the party mode.
    /// </summary>
    public class GadgetController : MonoBehaviour
    {
        public static GadgetController Instance { get; private set; }

        private const float SLOT = 76f;
        private const float PLUNGE_RANGE = 14f;
        private const float YOYO_RANGE = 7f;              // half the plunger's reach
        private const float YOYO_SPEED = 13f;
        private const float PLUNGE_MIN_DISTANCE = 1.2f;
        private const float PLUNGE_SPEED = 16f;           // m/s while reeling in
        private const float FLOOR_NORMAL_MIN_Y = 0.7f;    // "facing mostly up" = floor; walls/furniture sides don't catch
        private const float DROWSY_SPEED_MULT = 0.75f;
        private const float MUFFLE_CUTOFF_HZ = 1100f;

        private static readonly FieldInfo WalkSpeedField =
            typeof(FirstPersonController).GetField("m_WalkSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo RunSpeedField =
            typeof(FirstPersonController).GetField("m_RunSpeed", BindingFlags.NonPublic | BindingFlags.Instance);

        private GameObject _canvasGO;
        private Image _slotBg;
        private Image _cooldownFill;
        private Image _readyFlash;
        private Text _nameText;
        private Text _countdownText;
        private Text _usesText;           // uses left, for gadgets with more than one
        private Text _messageText;
        private float _messageTimer;
        private float _flashTimer;

        private AudioSource _sfx;
        private AudioSource _lullabySource;

        private float _cooldown;
        private GameObject _parrotPreview;   // Toy Parrot: shown while choosing where to put it
        private Outline _slotOutline;
        private float _cooldownTotal = 1f; // this cooldown's full length (Spare Battery's changes per run)
        private Image _screenFlash;        // Flash Camera's white-out
        private float _screenFlashTimer;
        private int _uses;                // left before it has to recharge
        private bool _wasActive;
        private bool _busy;               // a plunge reel is in progress
        private Coroutine _musicBoxRoutine;
        private Coroutine _plungeRoutine;
        private GameObject _rope, _plungerHead;
        private float _ropeWidth = 0.02f;
        private float _appliedSpeedMult = 1f;
        private AudioLowPassFilter _muffle;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.spatialBlend = 0f;
            _sfx.playOnAwake = false;
            _lullabySource = gameObject.AddComponent<AudioSource>();
            _lullabySource.spatialBlend = 0f;
            _lullabySource.playOnAwake = false;
            _lullabySource.loop = true;

            BuildUI();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            var run = EndlessRunManager.Instance;
            bool active = run != null && run.RunActive && !run.BirthdayMode && Gadgets.RunGadget != GadgetType.None;

            if (active && !_wasActive) BeginRun();
            else if (!active && _wasActive) CleanUp();
            _wasActive = active;

            if (_canvasGO.activeSelf != active) _canvasGO.SetActive(active);
            if (!active) return;

            if (_cooldown > 0f)
            {
                _cooldown -= Time.deltaTime;
                if (_cooldown <= 0f)
                {
                    _cooldown = 0f;
                    _uses = Gadgets.Uses(Gadgets.RunGadget);
                    _flashTimer = 0.6f;
                    ShowMessage(Gadgets.Name(Gadgets.RunGadget) + " is ready!" + (_uses > 1 ? " (" + _uses + " uses)" : ""), 2.5f);
                }
            }

            if (_parrotPreview != null) UpdateParrotPlacement();
            else if (Time.timeScale > 0f && Input.GetKeyDown(KeyCode.Alpha1)) TryUse();

            RefreshSlot();

            if (_messageTimer > 0f)
            {
                _messageTimer -= Time.deltaTime;
                if (_messageTimer <= 0f) _messageText.text = "";
            }
        }

        /// <summary>3.7 (Unstable Attachment): a different gadget for this objective, ready at once.</summary>
        public void TraitSetGadget(GadgetType g)
        {
            Gadgets.RunGadget = g;
            _busy = false;
            _cooldown = 0f;
            _uses = Gadgets.Uses(g);
            if (_nameText != null) _nameText.text = Gadgets.ShortName(g);
        }

        private void BeginRun()
        {
            _cooldown = 0f;
            _uses = Gadgets.Uses(Gadgets.RunGadget);
            _nameText.text = Gadgets.ShortName(Gadgets.RunGadget);
            ShowMessage("Gadget: " + Gadgets.Name(Gadgets.RunGadget) + "  (press 1 to use)", 5f);
        }

        // ── Use ──────────────────────────────────────────────────────────────
        private void TryUse()
        {
            if (_busy) return;
            if (_uses <= 0)
            {
                ShowMessage("Recharging... " + Mathf.CeilToInt(_cooldown) + "s", 1.5f);
                return;
            }

            bool used = false;
            switch (Gadgets.RunGadget)
            {
                case GadgetType.PlungeOLaunch: used = TryPlunge(); break;
                case GadgetType.MusicBox: used = StartMusicBox(); break;
                case GadgetType.FortuneCookie: used = UseFortuneCookie(); break;
                case GadgetType.YoYo: used = TryYoYo(); break;
                case GadgetType.FlashCamera: used = UseFlashCamera(); break;
                case GadgetType.SpareBattery: used = UseSpareBattery(); break;
                case GadgetType.ToyParrot: StartParrotPlacement(); break; // used once it's set down
            }
            if (used) ConsumeUse();
        }

        private void ConsumeUse()
        {
            _uses--;
            if (_cooldown <= 0f) _cooldown = _cooldownTotal = CooldownFor(Gadgets.RunGadget); // starts on the first use, refills every use
        }

        // ── Toy Parrot: choose a spot on the floor, then it cries for Mama ────
        private const float PARROT_REACH = 4f;

        private void StartParrotPlacement()
        {
            _parrotPreview = ToyParrot.BuildModel("KE_ParrotPreview");
            ShowMessage("Aim at the floor: 1 to set the parrot down, right-click to cancel.", 30f);
        }

        private void UpdateParrotPlacement()
        {
            Camera cam = Camera.main;
            if (cam == null) { CancelParrotPlacement(); return; }

            // Where it would go: the floor you're looking at, or the floor just ahead.
            Vector3 spot;
            bool ok = FloorSpot(cam, out spot);
            _parrotPreview.SetActive(ok);
            if (ok)
            {
                _parrotPreview.transform.position = spot;
                Vector3 face = cam.transform.position - spot; face.y = 0f; // it faces you
                if (face.sqrMagnitude > 0.001f) _parrotPreview.transform.rotation = Quaternion.LookRotation(face);
                // A gentle bob so it reads as "not placed yet".
                _parrotPreview.transform.position += Vector3.up * (0.03f + Mathf.Sin(Time.time * 6f) * 0.02f);
            }

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) { CancelParrotPlacement(); ShowMessage("Parrot put away.", 1.5f); return; }
            if (Time.timeScale > 0f && Input.GetKeyDown(KeyCode.Alpha1))
            {
                if (!ok) { ShowMessage("The parrot needs FLOOR to stand on!", 2f); return; }
                float yaw = _parrotPreview.transform.eulerAngles.y;
                CancelParrotPlacement();
                ToyParrot.Place(spot, yaw);
                ShowMessage("The parrot starts wailing... Mama heard that.", 3f);
                ConsumeUse();
            }
        }

        private static bool FloorSpot(Camera cam, out Vector3 spot)
        {
            spot = Vector3.zero;
            RaycastHit hit;
            // (WorldRays: your own body isn't floor.)
            if (WorldRays.Raycast(cam.transform.position, cam.transform.forward, PARROT_REACH * EndlessTraits.GadgetReachMult(), out hit) && hit.normal.y >= FLOOR_NORMAL_MIN_Y)
            {
                spot = hit.point;
                return true;
            }
            // Looking straight ahead: drop it on the floor about a meter and a half in front.
            Vector3 flat = cam.transform.forward; flat.y = 0f;
            if (flat.sqrMagnitude < 0.001f) return false;
            Vector3 ahead = cam.transform.position + flat.normalized * 1.5f;
            if (WorldRays.Linecast(cam.transform.position, ahead)) return false; // a wall in the way
            if (WorldRays.Raycast(ahead, Vector3.down, 3f, out hit) && hit.normal.y >= FLOOR_NORMAL_MIN_Y)
            {
                spot = hit.point;
                return true;
            }
            return false;
        }

        private void CancelParrotPlacement()
        {
            if (_parrotPreview != null) Destroy(_parrotPreview);
            _parrotPreview = null;
            _messageTimer = 0f;
            if (_messageText != null) _messageText.text = "";
        }

        private static float CooldownFor(GadgetType g)
        {
            if (g == GadgetType.SpareBattery) return Gadgets.BATTERY_COOLDOWN_CHARGES * SecondsToEmpty(BatteryDrainField, Gadgets.BATTERY_FULL_CHARGE_SECONDS) * EndlessTraits.GadgetCooldownMult();
            if (g == GadgetType.FortuneCookie) return Gadgets.FORTUNE_COOLDOWN_HUNGERS * SecondsToEmpty(HungerDrainField, Gadgets.HUNGER_FULL_SECONDS) * EndlessTraits.GadgetCooldownMult();
            return Gadgets.Cooldown(g) * EndlessTraits.GadgetCooldownMult();
        }

        private static readonly FieldInfo BatteryDrainField =
            typeof(TattletailNeeds).GetField("batteryDecreasePerSecond", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo HungerDrainField =
            typeof(TattletailNeeds).GetField("hungerDecreasePerSecond", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>How long one of Tattletail's needs takes to go from full to empty right now (run modifiers and Steady Hands change the drain).</summary>
        private static float SecondsToEmpty(FieldInfo drainField, float fallback)
        {
            try
            {
                var needs = RM.tattletail != null ? RM.tattletail.GetNeeds() : null;
                if (needs != null && (object)drainField != null)
                {
                    float perSecond = (float)drainField.GetValue(needs);
                    if (perSecond > 0f) return 1f / perSecond;
                }
            }
            catch { }
            return fallback;
        }

        // ── Flash Camera: banish Mama if she's close, in front and in sight ──
        private bool UseFlashCamera()
        {
            _sfx.PlayOneShot(GadgetAudio.Shutter, 1f);
            _screenFlashTimer = 0.7f;

            GameObject mama = MamaController.Instance != null ? MamaController.Instance.GetActiveMama() : null;
            if (mama == null || !InFlashRange(mama))
            {
                ShowMessage("*FLASH*  ...nothing there. Wasted.", 2.5f);
                return true; // still used: the cooldown is the gamble
            }

            var run = EndlessRunManager.Instance;
            MamaController.Instance.RelocateAway(run != null ? run.CurrentMamaDifficulty : 1f, Gadgets.CAMERA_RETURN_DELAY);
            ShowMessage("*FLASH*  Mama vanishes into the dark!", 3f);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Gadget] Flash Camera banished Mama.");
            return true;
        }

        private static bool InFlashRange(GameObject mama)
        {
            Camera cam = Camera.main;
            if (cam == null) return false;
            Vector3 eye = cam.transform.position;
            Vector3 target = mama.transform.position + Vector3.up * 1.2f; // her chest, not her feet
            Vector3 to = target - eye;
            if (to.magnitude > Gadgets.CAMERA_RANGE * EndlessTraits.GadgetReachMult()) return false;
            if (Vector3.Angle(cam.transform.forward, to) > 50f) return false; // roughly on screen

            // Not through a wall: anything hit on the way must be Mama herself (or the player).
            foreach (var hit in Physics.RaycastAll(eye, to.normalized, to.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                Transform h = hit.transform;
                if (h.IsChildOf(mama.transform)) continue;
                if (RM.fpsController != null && h.IsChildOf(RM.fpsController.transform)) continue;
                if (RM.tattletail != null && h.IsChildOf(RM.tattletail.transform)) continue; // the one in your arms
                return false;
            }
            return true;
        }

        // ── Spare Battery: a full Tattletail charge anywhere ─────────────────
        private bool UseSpareBattery()
        {
            var needs = RM.tattletail != null ? RM.tattletail.GetNeeds() : null;
            if (needs == null) return false;
            needs.Charge();
            _sfx.PlayOneShot(GadgetAudio.Zap, 1f);
            int minutes = Mathf.FloorToInt(CooldownFor(GadgetType.SpareBattery) / 60f), seconds = Mathf.RoundToInt(CooldownFor(GadgetType.SpareBattery) % 60f);
            ShowMessage("Tattletail is fully charged! (next battery in " + minutes + ":" + seconds.ToString("D2") + ")", 3f);
            return true;
        }

        // ── Plunge-O-Launch ──────────────────────────────────────────────────
        private bool TryPlunge()
        {
            Camera cam = Camera.main;
            var fps = RM.fpsController;
            CharacterController cc = fps != null ? fps.GetComponent<CharacterController>() : null;
            if (cam == null || cc == null) return false;

            RaycastHit hit;
            if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, PLUNGE_RANGE * EndlessTraits.GadgetReachMult(), ~0, QueryTriggerInteraction.Ignore))
            {
                ShowMessage("Nothing in reach — the rope won't go that far.", 2f);
                return false;
            }
            if (hit.normal.y < FLOOR_NORMAL_MIN_Y)
            {
                ShowMessage("The plunger only sticks to the FLOOR!", 2f);
                return false;
            }
            if (Vector3.Distance(fps.transform.position, hit.point) < PLUNGE_MIN_DISTANCE)
            {
                ShowMessage("Too close to bother.", 1.5f);
                return false;
            }

            _sfx.PlayOneShot(GadgetAudio.Squelch, 1f);
            RM.mamaManager?.OnNoise(hit.point, true); // the SQUELCH is loud — Mama hears it
            _plungeRoutine = StartCoroutine(PlungeRoutine(fps.transform, cc, hit, PLUNGE_SPEED, false));
            return true;
        }

        // ── Yo-Yo: a shorter, silent Plunge-O-Launch with 2 uses ──────────────
        private bool TryYoYo()
        {
            Camera cam = Camera.main;
            var fps = RM.fpsController;
            CharacterController cc = fps != null ? fps.GetComponent<CharacterController>() : null;
            if (cam == null || cc == null) return false;

            RaycastHit hit;
            if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, YOYO_RANGE * EndlessTraits.GadgetReachMult(), ~0, QueryTriggerInteraction.Ignore))
            {
                ShowMessage("Too far: the yo-yo string is short.", 2f);
                return false;
            }
            if (hit.normal.y < FLOOR_NORMAL_MIN_Y)
            {
                ShowMessage("The yo-yo only catches the FLOOR!", 2f);
                return false;
            }
            if (Vector3.Distance(fps.transform.position, hit.point) < PLUNGE_MIN_DISTANCE)
            {
                ShowMessage("Too close to bother.", 1.5f);
                return false;
            }

            _sfx.PlayOneShot(GadgetAudio.Whirr, 0.6f); // soft, and no noise for Mama
            _plungeRoutine = StartCoroutine(PlungeRoutine(fps.transform, cc, hit, YOYO_SPEED, true));
            return true;
        }

        private IEnumerator PlungeRoutine(Transform player, CharacterController cc, RaycastHit hit, float speed, bool yoyo)
        {
            _busy = true;
            SpawnPlungerVisuals(hit, yoyo);

            // Where the player's transform ends up with the capsule's bottom resting on the hit point.
            float bottomOffset = cc.center.y - cc.height * 0.5f;
            Vector3 dest = hit.point + Vector3.up * (cc.skinWidth + 0.05f - bottomOffset);

            float elapsed = 0f;
            int stuckFrames = 0;
            while (elapsed < 1.6f)
            {
                Vector3 to = dest - player.position;
                float dist = to.magnitude;
                if (dist < 0.3f) break;

                float step = Mathf.Min(speed * Time.deltaTime, dist);
                Vector3 before = player.position;
                // Real collision-aware movement, not a teleport: a table or
                // wall in the way stops you against it instead of letting
                // you clip through.
                cc.Move(to / dist * step);
                float moved = (player.position - before).magnitude;
                if (moved < step * 0.25f) { if (++stuckFrames >= 5) break; }
                else stuckFrames = 0;

                UpdateRope(player.position + Vector3.up * 0.9f, hit.point);
                elapsed += Time.deltaTime;
                yield return null;
            }

            DestroyPlungerVisuals();
            _busy = false;
            _plungeRoutine = null;
        }

        private void SpawnPlungerVisuals(RaycastHit hit, bool yoyo)
        {
            Shader unlit = Shader.Find("Unlit/Color");

            _plungerHead = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _plungerHead.name = "KE_PlungerHead";
            Destroy(_plungerHead.GetComponent<Collider>());
            _plungerHead.transform.position = hit.point + hit.normal * 0.03f;
            _plungerHead.transform.up = hit.normal;
            _plungerHead.transform.localScale = yoyo ? new Vector3(0.12f, 0.025f, 0.12f) : new Vector3(0.22f, 0.03f, 0.22f);
            var headMat = new Material(unlit) { color = yoyo ? Gadgets.GumballColor(GadgetType.YoYo) : new Color(0.75f, 0.1f, 0.12f) };
            _plungerHead.GetComponent<Renderer>().material = headMat;

            _rope = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _rope.name = "KE_PlungerRope";
            Destroy(_rope.GetComponent<Collider>());
            var ropeMat = new Material(unlit) { color = yoyo ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.9f, 0.85f, 0.7f) };
            _ropeWidth = yoyo ? 0.008f : 0.02f; // a thin string vs the plunger's rope
            _rope.GetComponent<Renderer>().material = ropeMat;
        }

        private void UpdateRope(Vector3 from, Vector3 to)
        {
            if (_rope == null) return;
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 0.01f) { _rope.SetActive(false); return; }
            _rope.SetActive(true);
            _rope.transform.position = from + d * 0.5f;
            _rope.transform.rotation = Quaternion.LookRotation(d);
            _rope.transform.localScale = new Vector3(_ropeWidth, _ropeWidth, len);
        }

        private void DestroyPlungerVisuals()
        {
            if (_rope != null) Destroy(_rope);
            if (_plungerHead != null) Destroy(_plungerHead);
            _rope = _plungerHead = null;
        }

        // ── Music Box ────────────────────────────────────────────────────────
        private bool StartMusicBox()
        {
            if (_musicBoxRoutine != null) StopCoroutine(_musicBoxRoutine);
            _musicBoxRoutine = StartCoroutine(MusicBoxRoutine());
            return true;
        }

        private IEnumerator MusicBoxRoutine()
        {
            // Blocks every Mama (re)activation path until the pause is over
            // — including a new objective starting mid-lullaby.
            MamaController.SuppressedUntil = Time.time + Gadgets.MUSIC_BOX_PAUSE;
            MamaController.Instance?.DeactivateMama();

            _lullabySource.clip = GadgetAudio.Lullaby;
            _lullabySource.volume = 0.7f;
            _lullabySource.Play();
            SetDrowsy(true);
            ShowMessage("The lullaby plays... Mama drifts away. (" + (int)Gadgets.MUSIC_BOX_PAUSE + "s)", 4f);

            yield return new WaitForSeconds(Gadgets.MUSIC_BOX_PAUSE);

            MamaController.SuppressedUntil = 0f;
            EndlessRunManager.Instance?.ResumeMamaAfterMissedCall(); // same "retry her activation now" path a missed phone call uses; no-op during rest
            _lullabySource.volume = 0.18f; // it's stuck in your head now
            SetMuffled(true);
            ShowMessage("The music stopped... but it's still stuck in your head.", 4f);

            yield return new WaitForSeconds(Gadgets.MUSIC_BOX_LINGER);

            _lullabySource.Stop();
            SetMuffled(false);
            SetDrowsy(false);
            ShowMessage("The tune finally fades.", 2.5f);
            _musicBoxRoutine = null;
        }

        private void SetDrowsy(bool on)
        {
            var fps = RM.fpsController;
            if (fps == null || (object)WalkSpeedField == null || (object)RunSpeedField == null) { _appliedSpeedMult = 1f; return; }

            float target = on ? DROWSY_SPEED_MULT : 1f;
            if (Mathf.Approximately(target, _appliedSpeedMult)) return;
            float factor = target / _appliedSpeedMult;
            WalkSpeedField.SetValue(fps, (float)WalkSpeedField.GetValue(fps) * factor);
            RunSpeedField.SetValue(fps, (float)RunSpeedField.GetValue(fps) * factor);
            _appliedSpeedMult = target;
        }

        private void SetMuffled(bool on)
        {
            if (on)
            {
                var listener = FindObjectOfType<AudioListener>();
                if (listener == null) return;
                _muffle = listener.GetComponent<AudioLowPassFilter>();
                if (_muffle == null) _muffle = listener.gameObject.AddComponent<AudioLowPassFilter>();
                _muffle.cutoffFrequency = MUFFLE_CUTOFF_HZ;
                _muffle.enabled = true;
            }
            else if (_muffle != null)
            {
                Destroy(_muffle);
                _muffle = null;
            }
        }

        // ── Fortune Cookie ───────────────────────────────────────────────────
        private bool UseFortuneCookie()
        {
            var needs = RM.tattletail != null ? RM.tattletail.GetNeeds() : null;
            if (needs != null) needs.Feed();
            _sfx.PlayOneShot(GadgetAudio.Crack, 1f);

            string text;
            if (PowerUpObjective.Instance != null && Random.value < Gadgets.FORTUNE_LOOT_CHANCE)
            {
                PowerUpObjective.Instance.SpawnGiftCoin();
                text = "\"" + Gadgets.LootFortunes[Random.Range(0, Gadgets.LootFortunes.Length)] + "\"\n(A gift coin appeared somewhere in the house!)";
            }
            else
            {
                text = "\"" + Gadgets.LoreFortunes[Random.Range(0, Gadgets.LoreFortunes.Length)] + "\"";
            }
            ShowMessage("Tattletail munches the cookie. Your fortune:\n" + text, 8f);
            return true;
        }

        // ── Cleanup ──────────────────────────────────────────────────────────
        public void CleanUp()
        {
            if (_plungeRoutine != null) { StopCoroutine(_plungeRoutine); _plungeRoutine = null; }
            if (_musicBoxRoutine != null) { StopCoroutine(_musicBoxRoutine); _musicBoxRoutine = null; }
            DestroyPlungerVisuals();
            _busy = false;
            if (_lullabySource != null) _lullabySource.Stop();
            SetMuffled(false);
            SetDrowsy(false);
            MamaController.SuppressedUntil = 0f;
            _cooldown = 0f;
            _uses = 0;
            if (_parrotPreview != null) Destroy(_parrotPreview);
            _parrotPreview = null;
            if (ToyParrot.Active != null) ToyParrot.Active.Pop();
            _screenFlashTimer = 0f;
            if (_screenFlash != null) _screenFlash.color = Color.clear;
            _messageTimer = 0f;
            if (_messageText != null) _messageText.text = "";
        }

        // ── HUD ──────────────────────────────────────────────────────────────
        private void ShowMessage(string text, float seconds)
        {
            _messageText.text = text;
            _messageTimer = seconds;
        }

        private void RefreshSlot()
        {
            float total = _cooldownTotal;
            float frac = total > 0f ? Mathf.Clamp01(_cooldown / total) : 0f;
            bool empty = _uses <= 0;

            // The dark overlay drains from the top down as the cooldown runs out
            // (lighter while there's still a use left, so it reads as "recharging", not "locked").
            var frt = _cooldownFill.rectTransform;
            frt.anchorMax = new Vector2(1f, frac);
            _cooldownFill.enabled = frac > 0f;
            _cooldownFill.color = new Color(0f, 0f, 0f, empty ? 0.72f : 0.35f);
            _countdownText.text = empty && _cooldown > 0f ? Mathf.CeilToInt(_cooldown).ToString() : "";
            _nameText.color = empty ? new Color(1f, 0.95f, 0.75f, 0.35f) : new Color(1f, 0.95f, 0.75f, 1f);
            _usesText.text = Gadgets.Uses(Gadgets.RunGadget) > 1 ? _uses.ToString() : "";
            _slotOutline.effectColor = Gadgets.IsDlc(Gadgets.RunGadget) ? Gadgets.Rainbow(Time.unscaledTime * 0.35f) : new Color(0.7f, 0.7f, 0.78f, 0.9f); // DLC gadgets glow rainbow
            _usesText.color = empty ? new Color(1f, 0.4f, 0.4f) : Color.white;

            if (_screenFlashTimer > 0f)
            {
                _screenFlashTimer -= Time.deltaTime;
                _screenFlash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(_screenFlashTimer / 0.7f) * 0.95f);
            }
            else if (_screenFlash.color.a != 0f) _screenFlash.color = Color.clear;

            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                _readyFlash.color = new Color(0.3f, 1f, 0.4f, Mathf.Clamp01(_flashTimer / 0.6f) * 0.5f);
            }
            else if (_readyFlash.color.a != 0f) _readyFlash.color = Color.clear;
        }

        private void BuildUI()
        {
            Font font = MainMenuButtonInjector.RealFont != null ? MainMenuButtonInjector.RealFont : Resources.GetBuiltinResource<Font>("Arial.ttf");

            _canvasGO = new GameObject("KE_GadgetCanvas");
            _canvasGO.transform.SetParent(transform, false);
            var canvas = _canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 940;
            var scaler = _canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // Flash Camera's white-out, full screen, behind the slot.
            var whiteout = new GameObject("ScreenFlash", typeof(RectTransform), typeof(Image));
            whiteout.transform.SetParent(_canvasGO.transform, false);
            Stretch((RectTransform)whiteout.transform);
            _screenFlash = whiteout.GetComponent<Image>();
            _screenFlash.color = Color.clear;
            _screenFlash.raycastTarget = false;

            // One slot, same look as the party hotbar's boxes.
            var slot = new GameObject("GadgetSlot", typeof(RectTransform), typeof(Image), typeof(Outline));
            slot.transform.SetParent(_canvasGO.transform, false);
            var srt = (RectTransform)slot.transform;
            srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0.5f, 0f);
            srt.anchoredPosition = new Vector2(0f, 26f);
            srt.sizeDelta = new Vector2(SLOT, SLOT);
            _slotBg = slot.GetComponent<Image>();
            _slotBg.color = new Color(0.04f, 0.04f, 0.06f, 0.78f);
            _slotBg.raycastTarget = false;
            var outline = slot.GetComponent<Outline>();
            _slotOutline = outline;
            outline.effectColor = new Color(0.7f, 0.7f, 0.78f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            _nameText = MakeText(slot.transform, "Name", 15, TextAnchor.MiddleCenter, new Color(1f, 0.95f, 0.75f), font);
            Stretch(_nameText.rectTransform);

            var fill = new GameObject("CooldownFill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(slot.transform, false);
            var frt = (RectTransform)fill.transform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(1f, 0f);
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            _cooldownFill = fill.GetComponent<Image>();
            _cooldownFill.color = new Color(0f, 0f, 0f, 0.72f);
            _cooldownFill.raycastTarget = false;

            var flash = new GameObject("ReadyFlash", typeof(RectTransform), typeof(Image));
            flash.transform.SetParent(slot.transform, false);
            Stretch((RectTransform)flash.transform);
            _readyFlash = flash.GetComponent<Image>();
            _readyFlash.color = Color.clear;
            _readyFlash.raycastTarget = false;

            _countdownText = MakeText(slot.transform, "Countdown", 34, TextAnchor.MiddleCenter, Color.white, font);
            Stretch(_countdownText.rectTransform);
            _countdownText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);

            var num = MakeText(slot.transform, "Number", 20, TextAnchor.UpperLeft, new Color(0.85f, 0.85f, 0.9f, 0.9f), font);
            num.text = "1";
            var nrt = num.rectTransform;
            nrt.anchorMin = nrt.anchorMax = nrt.pivot = new Vector2(0f, 1f);
            nrt.anchoredPosition = new Vector2(6f, -4f);
            nrt.sizeDelta = new Vector2(30f, 24f);

            // Uses left (Yo-Yo: 2, then 1), bottom-right of the slot.
            _usesText = MakeText(slot.transform, "Uses", 24, TextAnchor.LowerRight, Color.white, font);
            var urt = _usesText.rectTransform;
            urt.anchorMin = urt.anchorMax = urt.pivot = new Vector2(1f, 0f);
            urt.anchoredPosition = new Vector2(-6f, 3f);
            urt.sizeDelta = new Vector2(30f, 28f);
            _usesText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);

            _messageText = MakeText(_canvasGO.transform, "GadgetMessage", 26, TextAnchor.LowerCenter, new Color(1f, 0.95f, 0.7f), font);
            _messageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            var mrt = _messageText.rectTransform;
            mrt.anchorMin = mrt.anchorMax = mrt.pivot = new Vector2(0.5f, 0f);
            mrt.anchoredPosition = new Vector2(0f, 26f + SLOT + 12f);
            mrt.sizeDelta = new Vector2(1000f, 120f);
            _messageText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);

            _canvasGO.SetActive(false);
        }

        private static Text MakeText(Transform parent, string name, int size, TextAnchor anchor, Color color, Font font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
