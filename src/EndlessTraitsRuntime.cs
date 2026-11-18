using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// What the chosen traits make happen while a run is going (EndlessTraits has the numbers): your speed, how long holds
    /// take, Tattletail's needs freezing, and the things you see and hear (heartbeat, warnings, pointers, whispers, phantom
    /// Mamas, bad memories, the skip key). One object, made with the other run controllers; it does nothing unless a
    /// profile is running. Every step is guarded so a trait can never take the run down with it.
    /// </summary>
    public class EndlessTraitsRuntime : MonoBehaviour
    {
        public static EndlessTraitsRuntime Instance { get; private set; }

        // ── UI ───────────────────────────────────────────────────────────────
        private Image _pulse, _dark;
        private Text _msg, _pointer, _hint, _tag;
        private float _msgUntil, _pulseA, _darkA;
        private AudioSource _audio;
        private AudioClip _thump;

        // ── State ────────────────────────────────────────────────────────────
        private static readonly FieldInfo WalkField = typeof(FirstPersonController).GetField("m_WalkSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo RunField = typeof(FirstPersonController).GetField("m_RunSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo[] NeedFields =
        {
            typeof(TattletailNeeds).GetField("currentHunger", BindingFlags.NonPublic | BindingFlags.Instance),
            typeof(TattletailNeeds).GetField("currentBattery", BindingFlags.NonPublic | BindingFlags.Instance),
            typeof(TattletailNeeds).GetField("currentDirtiness", BindingFlags.NonPublic | BindingFlags.Instance),
        };

        private float _appliedSpeed = 1f;
        private readonly Dictionary<Interactable, float> _holds = new Dictionary<Interactable, float>();
        private readonly List<Interactable> _dead = new List<Interactable>();
        private float _scanAt;
        private readonly float[] _frozenVals = new float[3];
        private bool _frozen;

        private float _heartAt, _falseUntil, _nextFalse = 30f;
        private Vector3 _lastMama;
        private bool _hadMama;
        private string _flagText = "";
        private float _flagUntil, _pointerUntil, _objectiveClock, _hintUntil;
        private float _nextWhisper = 50f, _whisperEnd, _nextPhantom = 60f;
        private GameObject _phantom, _decoy;
        private float _phantomUntil, _warpUntil, _baseFov;
        private bool _warping;

        private static readonly string[] WhisperLines =
        {
            "...behind you...", "...don't look...", "...she knows where you are...", "...check the corner...", "...it's so quiet...", "...she's counting...",
        };

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            try { BuildUI(); } catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Traits] UI: " + e.Message); }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Every frame ──────────────────────────────────────────────────────
        private void Update()
        {
            var run = EndlessRunManager.Instance;
            if (!EndlessTraits.Running || run == null || !run.RunActive)
            {
                if (_appliedSpeed != 1f || _frozen || _warping || _phantom != null || _decoy != null || _pulseA > 0f || _darkA > 0f) Wind();
                return;
            }

            try
            {
                float dt = Time.deltaTime;
                _objectiveClock += dt;
                UpdateState(dt);
                ApplySpeed();
                Holds();
                FreezeNeeds();
                bool live = !run.InRestPhase && !run.PhoneIsRinging;
                Warnings(dt, live);
                if (live) { Whispers(dt); Phantoms(dt); }
                else if (_phantom != null) { Destroy(_phantom); _phantom = null; }
                Pointers(live);
                if (Input.GetKeyDown(KeyCode.X) && live && EndlessTraits.TrySkip()) Say("Skipped.", 2f);
                Fade(dt);
            }
            catch (System.Exception e)
            {
                if (Time.frameCount % 600 == 0) KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Traits] " + e.Message);
            }
        }

        /// <summary>The run is over (or no profile): put everything back.</summary>
        private void Wind()
        {
            try
            {
                var fps = RM.fpsController;
                if (_appliedSpeed != 1f && fps != null && (object)WalkField != null && (object)RunField != null)
                {
                    float back = 1f / _appliedSpeed;
                    WalkField.SetValue(fps, (float)WalkField.GetValue(fps) * back);
                    RunField.SetValue(fps, (float)RunField.GetValue(fps) * back);
                }
            }
            catch (System.Exception) { }
            _appliedSpeed = 1f;
            _frozen = false;
            EndlessTraits.MemoryHidesWarnings = false;
            if (_warping && Camera.main != null) Camera.main.fieldOfView = _baseFov;
            _warping = false;
            if (_phantom != null) Destroy(_phantom);
            if (_decoy != null) Destroy(_decoy);
            _phantom = _decoy = null;
            _pulseA = _darkA = 0f;
            _holds.Clear();
            if (_pulse != null) _pulse.color = new Color(0f, 0f, 0f, 0f);
            if (_dark != null) _dark.color = new Color(0f, 0f, 0f, 0f);
            if (_msg != null) _msg.text = "";
            if (_pointer != null) _pointer.text = "";
            if (_hint != null) _hint.text = "";
            if (_tag != null) _tag.text = "";
        }

        private void UpdateState(float dt)
        {
            Transform fps = RM.fpsController != null ? RM.fpsController.transform : null;
            GameObject mama = MamaController.Instance != null ? MamaController.Instance.GetActiveMama() : null;
            EndlessTraits.MamaHunting = mama != null;
            EndlessTraits.MamaDist = mama != null && fps != null ? Vector3.Distance(mama.transform.position, fps.position) : 999f;
            var needs = RM.tattletail != null ? RM.tattletail.GetNeeds() : null;
            EndlessTraits.NeedLow = needs != null && (needs.GetHunger() < 0.25f || needs.GetBattery() < 0.25f || needs.GetDirtiness() < 0.25f);
            if (RM.flashlight != null && !RM.flashlight.IsGlowing()) EndlessTraits.FlashlightOffFor += dt; else EndlessTraits.FlashlightOffFor = 0f;
            if (Time.time >= _whisperEnd && (EndlessTraits.WhisperSpeed != 1f || EndlessTraits.WhisperHold != 1f || EndlessTraits.WhisperLoud != 0f))
            {
                EndlessTraits.WhisperSpeed = EndlessTraits.WhisperHold = 1f;
                EndlessTraits.WhisperLoud = 0f;
            }
        }

        private void ApplySpeed()
        {
            var fps = RM.fpsController;
            if (fps == null || (object)WalkField == null || (object)RunField == null) return;
            float target = EndlessTraits.SpeedMult();
            if (Mathf.Abs(target - _appliedSpeed) < 0.001f) return;
            float factor = target / _appliedSpeed;
            WalkField.SetValue(fps, (float)WalkField.GetValue(fps) * factor);
            RunField.SetValue(fps, (float)RunField.GetValue(fps) * factor);
            _appliedSpeed = target;
        }

        // ── Hold times ───────────────────────────────────────────────────────
        private static bool IsObjectiveName(string n)
        {
            return n.StartsWith("KE_WashingMachine") || n.StartsWith("KE_Vase") || n.StartsWith("KE_Candle") || n.StartsWith("KE_TattletailFriend")
                || n.StartsWith("KE_Tape_") || n.StartsWith("KE_Speaker") || n.StartsWith("KE_FuseBox");
        }

        private static bool IsHoldTarget(string n) { return IsObjectiveName(n) || n.StartsWith("KE_ChargingStation"); }

        private void Holds()
        {
            if (Time.time >= _scanAt)
            {
                _scanAt = Time.time + 0.5f;
                foreach (Interactable it in Object.FindObjectsOfType<Interactable>())
                {
                    if (it == null || _holds.ContainsKey(it)) continue;
                    if (it.holdTime > 0f && IsHoldTarget(it.gameObject.name)) _holds[it] = Mathf.Max(it.holdTime, 0.9f);   // (the game raises every hold to at least 0.9 s when the object starts)
                }
            }
            _dead.Clear();
            foreach (KeyValuePair<Interactable, float> kv in _holds)
            {
                if (kv.Key == null) { _dead.Add(kv.Key); continue; }
                kv.Key.holdTime = Mathf.Max(0.12f, kv.Value * EndlessTraits.HoldMult(kv.Key.gameObject.name));
            }
            for (int i = 0; i < _dead.Count; i++) _holds.Remove(_dead[i]);
        }

        /// <summary>The nearest live objective thing to you (never the charging station, the phone or a coin).</summary>
        private bool NearestObjective(out Vector3 at, out float dist)
        {
            at = Vector3.zero; dist = float.MaxValue;
            Transform fps = RM.fpsController != null ? RM.fpsController.transform : null;
            if (fps == null) return false;
            foreach (KeyValuePair<Interactable, float> kv in _holds)
            {
                Interactable it = kv.Key;
                if (it == null || !it.gameObject.activeInHierarchy || !IsObjectiveName(it.gameObject.name)) continue;
                float d = Vector3.Distance(it.transform.position, fps.position);
                if (d < dist) { dist = d; at = it.transform.position; }
            }
            return dist < float.MaxValue;
        }

        // ── Tattletail's needs stop (Persistent Dread) ───────────────────────
        private void FreezeNeeds()
        {
            var needs = RM.tattletail != null ? RM.tattletail.GetNeeds() : null;
            if (needs == null) return;
            if (!EndlessTraits.NeedsFrozen) { _frozen = false; return; }
            for (int i = 0; i < 3; i++)
            {
                if ((object)NeedFields[i] == null) continue;
                float cur = (float)NeedFields[i].GetValue(needs);
                if (!_frozen || cur > _frozenVals[i]) _frozenVals[i] = cur;
                else if (cur < _frozenVals[i]) NeedFields[i].SetValue(needs, _frozenVals[i]);
            }
            _frozen = true;
        }

        // ── Warnings: the heartbeat, the flagged teleport, the pointers ──────
        private void Warnings(float dt, bool live)
        {
            bool hidden = EndlessTraits.MemoryHidesWarnings;
            Transform fps = RM.fpsController != null ? RM.fpsController.transform : null;
            GameObject mama = MamaController.Instance != null ? MamaController.Instance.GetActiveMama() : null;

            if (EndlessTraits.Has(Trait.Paranoid) && !hidden && live)
            {
                float range = EndlessTraits.WarningRange(12f), close = 0f;
                if (EndlessTraits.MamaDist < range) close = 1f - EndlessTraits.MamaDist / range;
                if (Time.time >= _nextFalse && mama == null)
                {
                    _falseUntil = Time.time + 4f;
                    _nextFalse = Time.time + Random.Range(25f, 50f);
                }
                if (Time.time < _falseUntil) close = Mathf.Max(close, 0.55f);
                if (close > 0.02f && Time.time >= _heartAt)
                {
                    _heartAt = Time.time + Mathf.Lerp(1.1f, 0.4f, close);
                    if (_audio != null && _thump != null) _audio.PlayOneShot(_thump, 0.35f + 0.5f * close);
                    _pulseA = Mathf.Max(_pulseA, 0.15f + 0.3f * close);
                }
            }

            if (EndlessTraits.Has(Trait.HypervigilantBehaviour) && !hidden)
            {
                if (mama != null)
                {
                    if (_hadMama && Vector3.Distance(_lastMama, mama.transform.position) > 5f && fps != null)
                    {
                        _flagText = "She moved: " + Direction(mama.transform.position);
                        _flagUntil = Time.time + (EndlessTraits.NothingGetsPastYou ? 6f : 4f);
                        if (_audio != null && _thump != null) _audio.PlayOneShot(_thump, 0.7f);
                        _pulseA = Mathf.Max(_pulseA, 0.35f);
                    }
                    _lastMama = mama.transform.position;
                    _hadMama = true;
                }
                else _hadMama = false;
            }
        }

        private void Pointers(bool live)
        {
            if (_pointer == null) return;
            string s = "";
            bool hidden = EndlessTraits.MemoryHidesWarnings;
            GameObject mama = MamaController.Instance != null ? MamaController.Instance.GetActiveMama() : null;
            if (live && !hidden)
            {
                if (EndlessTraits.Has(Trait.ParanoidFixation) && mama != null && EndlessTraits.MamaDist <= EndlessTraits.WarningRange(15f))
                    s += "Mama: " + Direction(mama.transform.position) + ", " + Mathf.RoundToInt(EndlessTraits.MamaDist) + " m\n";
                if (Time.time < _flagUntil) s += _flagText + "\n";
            }
            if (live)
            {
                Vector3 at; float d;
                bool have = NearestObjective(out at, out d);
                if (have)
                {
                    if (EndlessTraits.Has(Trait.EnvironmentalFixation) && _objectiveClock >= 20f)
                        s += "The spot: " + Direction(at) + ", " + Mathf.RoundToInt(d) + " m\n";
                    else if (Time.time < _pointerUntil)
                        s += "It's over there: " + Direction(at) + "\n";
                    if (EndlessTraits.Has(Trait.CompulsiveObservation) && d <= 6f) s += "Something is here.\n";
                }
            }
            _pointer.text = s;

            if (_tag != null) _tag.text = EndlessTraits.HideHud ? "" : EndlessTraits.TitleNow() + "  -  " + EndlessTraits.ActiveLine();
            if (_hint != null)
            {
                _hint.text = Time.time < _hintUntil && EndlessTraits.SkipsLeft > 0 ? "Press X to skip this objective (once per run)" : "";
            }
        }

        private static string Direction(Vector3 target)
        {
            Transform fps = RM.fpsController != null ? RM.fpsController.transform : null;
            if (fps == null) return "near";
            Vector3 f = fps.forward; f.y = 0f;
            Vector3 d = target - fps.position; d.y = 0f;
            if (f.sqrMagnitude < 0.0001f || d.sqrMagnitude < 0.0001f) return "here";
            float ang = Vector3.Angle(f, d);
            if (Vector3.Cross(f, d).y < 0f) ang = -ang;   // negative: to your left
            float a = Mathf.Abs(ang);
            string side = ang < 0f ? "left" : "right";
            if (a < 30f) return "ahead";
            if (a < 75f) return "ahead-" + side;
            if (a < 105f) return side;
            if (a < 150f) return "behind-" + side;
            return "behind";
        }

        // ── Whispers (Intrusive Thoughts) ────────────────────────────────────
        private void Whispers(float dt)
        {
            if (!EndlessTraits.Has(Trait.IntrusiveThoughts)) return;
            _nextWhisper -= dt;
            if (_nextWhisper > 0f) return;
            _nextWhisper = Random.Range(40f, 70f);
            string line = WhisperLines[Random.Range(0, WhisperLines.Length)];
            if (Random.value < 0.35f)
            {
                _pointerUntil = Time.time + 6f;
                Say(line + "\n(it points somewhere)", 3.5f);
                return;
            }
            _whisperEnd = Time.time + 8f;
            switch (Random.Range(0, 3))
            {
                case 0: EndlessTraits.WhisperSpeed = 0.9f; Say(line + "\n(slower for a while)", 3.5f); break;
                case 1: EndlessTraits.WhisperHold = 1.25f; Say(line + "\n(everything takes longer)", 3.5f); break;
                default: EndlessTraits.WhisperLoud = 0.25f; Say(line + "\n(you feel loud)", 3.5f); break;
            }
        }

        // ── Phantom Mamas and the warping picture (Distorted Perception) ─────
        private void Phantoms(float dt)
        {
            if (!EndlessTraits.Has(Trait.DistortedPerception)) return;
            if (_phantom != null)
            {
                Transform fps = RM.fpsController != null ? RM.fpsController.transform : null;
                if (fps != null)
                {
                    Vector3 look = fps.position - _phantom.transform.position; look.y = 0f;
                    if (look.sqrMagnitude > 0.01f) _phantom.transform.rotation = Quaternion.LookRotation(look);
                    if (Time.time >= _phantomUntil || Vector3.Distance(fps.position, _phantom.transform.position) < 3f) { Destroy(_phantom); _phantom = null; }
                }
            }
            if (_warping)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    if (Time.time >= _warpUntil) { cam.fieldOfView = _baseFov; _warping = false; }
                    else cam.fieldOfView = _baseFov + 7f * Mathf.Sin((Time.time - (_warpUntil - 2.5f)) * 7f);
                }
                else _warping = false;
            }
            _nextPhantom -= dt;
            if (_nextPhantom > 0f) return;
            _nextPhantom = Random.Range(40f, 70f);
            SpawnPhantom();
            if (Camera.main != null && !_warping) { _baseFov = Camera.main.fieldOfView; _warpUntil = Time.time + 2.5f; _warping = true; }
        }

        private void SpawnPhantom()
        {
            Transform fps = RM.fpsController != null ? RM.fpsController.transform : null;
            if (fps == null) return;
            for (int tries = 0; tries < 8; tries++)
            {
                float ang = Random.Range(-60f, 60f);
                Vector3 dir = Quaternion.Euler(0f, ang, 0f) * fps.forward; dir.y = 0f;
                Vector3 pos = fps.position + dir.normalized * Random.Range(7f, 11f);
                if (Physics.CheckSphere(pos + Vector3.up * 1.1f, 0.5f)) continue;
                if (Physics.Linecast(fps.position + Vector3.up * 1.2f, pos + Vector3.up * 1.2f)) continue;   // (in the open, where you'd see her)
                _phantom = Phantom(pos);
                _phantomUntil = Time.time + 2.5f;
                return;
            }
        }

        private static GameObject Phantom(Vector3 pos)
        {
            var root = new GameObject("KE_Phantom");
            root.transform.position = pos;
            Material dark = Mat.Unlit(new Color(0.03f, 0.02f, 0.04f)), eye = Mat.Unlit(new Color(1f, 0.1f, 0.08f));
            Part(root.transform, PrimitiveType.Capsule, new Vector3(0f, 1.0f, 0f), new Vector3(0.9f, 1.0f, 0.6f), dark);
            Part(root.transform, PrimitiveType.Sphere, new Vector3(0f, 2.15f, 0f), new Vector3(0.7f, 0.65f, 0.6f), dark);
            Part(root.transform, PrimitiveType.Sphere, new Vector3(-0.15f, 2.2f, 0.28f), Vector3.one * 0.1f, eye);
            Part(root.transform, PrimitiveType.Sphere, new Vector3(0.15f, 2.2f, 0.28f), Vector3.one * 0.1f, eye);
            return root;
        }

        private static void Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 size, Material m)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
        }

        // ── A new objective ──────────────────────────────────────────────────
        public void OnObjectiveStart(ObjectiveType type, int count)
        {
            try
            {
                _objectiveClock = 0f;
                _hintUntil = Time.time + 6f;
                if (_decoy != null) { Destroy(_decoy); _decoy = null; }

                if (EndlessTraits.Has(Trait.Tormented) && count % 4 == 0) StartCoroutine(BadMemory());

                if (EndlessTraits.Has(Trait.AnomalousAttachment)) Anomaly();

                if (EndlessTraits.Has(Trait.UnstableAttachment))
                {
                    GadgetType g = EndlessTraits.RandomGadget();
                    if (g != GadgetType.None && GadgetController.Instance != null)
                    {
                        GadgetController.Instance.TraitSetGadget(g);
                        Say("Gadget: " + Gadgets.Name(g), 3f);
                    }
                    else if (g != GadgetType.None) Gadgets.RunGadget = g;
                }

                if (EndlessTraits.Has(Trait.UnwarrantedSuspicion) && Random.value < 0.1f) SpawnDecoy();
            }
            catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Traits] objective start: " + e.Message); }
        }

        private IEnumerator BadMemory()
        {
            Say("...", 1f);
            EndlessTraits.MemoryHidesWarnings = true;
            string line = WhisperLines[Random.Range(0, WhisperLines.Length)];
            float t = 0f;
            while (t < 4.5f && EndlessTraits.Running)
            {
                t += Time.deltaTime;
                float target = t < 0.6f ? t / 0.6f : (t > 3.9f ? Mathf.Clamp01((4.5f - t) / 0.6f) : 1f);
                _darkA = 0.6f * target;
                if (t > 0.7f && t < 0.9f && _msg != null) Say(line, 3f);
                yield return null;
            }
            _darkA = 0f;
            EndlessTraits.MemoryHidesWarnings = false;
        }

        private void Anomaly()
        {
            try
            {
                if (RM.recolorManager != null && RM.tattletail != null)
                {
                    RecolorType[] pool = { RecolorType.ORIGINAL, RecolorType.YELLOW, RecolorType.TEAL, RecolorType.BLACK, RecolorType.WHITE };
                    RM.recolorManager.RecolorObject(RM.tattletail.tattletail, pool[Random.Range(0, pool.Length)]);
                }
            }
            catch (System.Exception) { }
            string buff, debuff;
            if (Random.value < 0.5f) { EndlessTraits.AnomalySpeed *= 1.05f; buff = "a little faster"; }
            else { EndlessTraits.AnomalyHold *= 0.85f; buff = "holds a little quicker"; }
            float r = Random.value;
            if (r < 0.34f) { EndlessTraits.AnomalySpeed *= 0.95f; debuff = "but a little slower"; }
            else if (r < 0.67f) { EndlessTraits.AnomalyHold *= 1.15f; debuff = "but holds take longer"; }
            else { EndlessTraits.AnomalyLoud = 0.2f; debuff = "but you are louder"; }
            Say("Tattletail changed: " + buff + ", " + debuff + ".", 4f);
        }

        private void SpawnDecoy()
        {
            Transform fps = RM.fpsController != null ? RM.fpsController.transform : null;
            if (fps == null) return;
            Vector3 pos = fps.position + fps.forward * 3f + Vector3.up * 0.4f;
            if (Physics.CheckSphere(pos, 0.3f)) return;
            _decoy = new GameObject("KE_TraitDecoy");
            _decoy.transform.position = pos;
            Part(_decoy.transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.25f, Mat.Unlit(new Color(1f, 0.85f, 0.4f)));
            var col = _decoy.AddComponent<SphereCollider>();
            col.radius = 0.4f;
            PartyGameRunner.MakeInteractable(_decoy, "Check it", 0.6f);
        }

        /// <summary>The decoy was used: nothing there.</summary>
        public void DecoyUsed()
        {
            Say("Nothing there.", 2.5f);
            if (_decoy != null) { Destroy(_decoy); _decoy = null; }
        }

        // ── UI ───────────────────────────────────────────────────────────────
        /// <summary>A line in the middle-low of the screen for a few seconds.</summary>
        public void Say(string text, float seconds)
        {
            if (_msg == null) return;
            _msg.text = text;
            _msgUntil = Time.time + seconds;
        }

        private void Fade(float dt)
        {
            _pulseA = Mathf.MoveTowards(_pulseA, 0f, dt * 0.9f);
            if (_pulse != null) _pulse.color = new Color(0.75f, 0.02f, 0.02f, _pulseA);
            if (_dark != null) _dark.color = new Color(0f, 0f, 0f, _darkA);
            if (_msg != null && _msg.text.Length > 0 && Time.time >= _msgUntil) _msg.text = "";
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("KE_TraitsCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 880;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            Sprite vignette = MakeVignette();
            _pulse = FullImage(canvasGO, "Pulse", vignette);
            _dark = FullImage(canvasGO, "Dark", null);
            _msg = MakeText(canvasGO, "Msg", new Vector2(0.2f, 0.18f), new Vector2(0.8f, 0.3f), 28, new Color(0.85f, 0.8f, 0.95f, 0.95f), TextAnchor.MiddleCenter);
            _pointer = MakeText(canvasGO, "Pointer", new Vector2(0.3f, 0.74f), new Vector2(0.7f, 0.86f), 24, new Color(1f, 0.85f, 0.55f, 0.95f), TextAnchor.UpperCenter);
            _hint = MakeText(canvasGO, "Hint", new Vector2(0.02f, 0.02f), new Vector2(0.5f, 0.07f), 20, new Color(0.75f, 0.75f, 0.8f, 0.9f), TextAnchor.LowerLeft);
            _tag = MakeText(canvasGO, "Tag", new Vector2(0.02f, 0.075f), new Vector2(0.5f, 0.115f), 18, new Color(0.6f, 0.6f, 0.68f, 0.7f), TextAnchor.LowerLeft);

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 0f;
            _audio.playOnAwake = false;
            _thump = MakeThump();
        }

        private static Image FullImage(GameObject parent, string name, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            if (sprite != null) img.sprite = sprite;
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = false;
            return img;
        }

        private static Sprite MakeVignette()
        {
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((r - 0.45f) / 0.9f);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                }
            tex.SetPixels32(px);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, N, N), new Vector2(0.5f, 0.5f));
        }

        /// <summary>A heartbeat: a low "lub", then a softer "dub".</summary>
        private static AudioClip MakeThump()
        {
            const int RATE = 22050;
            int n = (int)(RATE * 0.42f);
            var data = new float[n];
            for (int beat = 0; beat < 2; beat++)
            {
                int start = (int)(RATE * (beat == 0 ? 0f : 0.17f));
                float amp = beat == 0 ? 1f : 0.65f, f = beat == 0 ? 52f : 64f;
                for (int i = 0; start + i < n && i < (int)(RATE * 0.2f); i++)
                {
                    float t = i / (float)RATE;
                    data[start + i] += amp * Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 22f);
                }
            }
            var clip = AudioClip.Create("KE_Thump", n, 1, RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static Font GetFont()
        {
            Font f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (f != null) return f;
            var existing = Object.FindObjectOfType<Text>();
            return existing != null ? existing.font : null;
        }

        private static Text MakeText(GameObject parent, string name, Vector2 min, Vector2 max, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.font = GetFont();
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.raycastTarget = false;
            return t;
        }
    }
}
