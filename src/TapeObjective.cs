using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// "Rewind the Tapes" objective — NOT wired into the real rotation yet.
    /// This is the position-verification phase: SpawnAllForTesting() places
    /// all 6 hand-marked spots at once (the real objective will only ever
    /// spawn 1 of 6, same shape as TattletailFriendObjective), each named
    /// KE_Tape_1..KE_Tape_6 so DebugLocationMarker's own E-key raycast log
    /// ("looking at 'KE_Tape_N' at ...") tells us exactly which one is bad
    /// when marked wrong.
    ///
    /// DebugDisableMama should stay true while doing this — same reasoning
    /// as every prior objective's position-marking phase.
    /// </summary>
    public class TapeObjective : MonoBehaviour
    {
        public static TapeObjective Instance { get; private set; }

        // Set true only for the position-verification phase — now false,
        // all 6 spots confirmed and the objective is wired into the real
        // rotation for real single-spawn testing (EndlessRunManager.
        // DebugForceFirstObjective).
        public static bool DebugSpawnAllTapes = false;

        // headphaze_electric_crackle_ticking confirmed INVALID as an
        // AudioController ID (IsValidAudioID=False, Play() returned NULL)
        // — it only ever existed as a raw baked AudioClip on the menu's
        // own "Ambience" AudioSource, same failure class as dryer_clunk.
        // Unlike dryer_clunk, no cross-level scavenge is needed — the
        // Ambience object lives directly in the "menu" scene, which is
        // already loaded every time this mod boots, so TryCaptureMenuAmbienceClip()
        // just grabs its real AudioClip reference directly. REWIND_CLIP
        // stays as a fallback AudioController ID only for the rare case
        // that capture never ran this session.
        public const string REWIND_CLIP = "flashlight_danger";
        private static AudioClip _rewindClip;
        private static bool _rewindClipCaptureAttempted;

        /// <summary>Call once, from the main menu (SceneBootstrap's scene.name=="menu" branch) — same "capture the real asset reference directly, keep it alive across whatever happens later" trick that just worked for dryer_clunk, minus the cross-scene load since this one's already sitting in the loaded scene.</summary>
        public static void TryCaptureMenuAmbienceClip()
        {
            if (_rewindClipCaptureAttempted) return;
            _rewindClipCaptureAttempted = true;

            foreach (var src in Resources.FindObjectsOfTypeAll<AudioSource>())
            {
                if (src == null || src.clip == null) continue;
                if (src.gameObject.name == "Ambience" && src.clip.name == "headphaze_electric_crackle_ticking")
                {
                    _rewindClip = src.clip;
                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Tape] Captured real menu ambience clip '" + _rewindClip.name + "' for tape rewind audio.");
                    return;
                }
            }
            KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Tape] Menu ambience clip not found this session — rewind audio will fall back to '" + REWIND_CLIP + "'.");
        }

        private static readonly string[] TemplateNames = { "vhs", "tape", "cassette" };

        // Exact spots the user marked in-game via DebugLocationMarker (E),
        // pulled directly from a real BepInEx log — same playerPos
        // convention every other objective's marked-spot array uses.
        // Tape 3 replaced with a real fresh mark (was 3 rounds of guessed
        // offsets before this) — same SnapToGround treatment as every
        // other tape now, no more special-casing.
        private static readonly Vector3[] HouseTapePositions =
        {
            new Vector3(3.2f, 1.1f, 6.0f),
            new Vector3(6.5f, -2.0f, -5.3f),
            new Vector3(2.1f, -2.0f, -5.5f),
            new Vector3(-3.3f, -1.5f, -3.7f),
            new Vector3(-5.0f, -2.0f, 2.4f),
            new Vector3(-9.0f, 0.7f, 10.8f),
        };

        private GameObject _template;
        // The real 'vhs_tape' Material — confirmed via TAPE-TEX-DIAG to
        // exist in this level's loaded assets (Standard shader, alongside
        // real vhs_tape_tattletail/vhs_tape_normal textures it almost
        // certainly already references) — applied straight onto the
        // procedural placeholder box instead of a flat color tint.
        private Material _realMaterial;
        private bool _resolving;
        private readonly GameObject[] _testTapes = new GameObject[6];
        private readonly Interactable[] _testInteractables = new Interactable[6];
        private readonly AudioSource[] _testAudioSources = new AudioSource[6];
        private readonly bool[] _wasHeld = new bool[6];
        private readonly float[] _nextNoiseTime = new float[6];
        private const float HOLD_NOISE_INTERVAL = 1f; // re-ping Mama roughly once a second while actively rewinding

        private void Update()
        {
            // Poll each spawned test tape's own hold progress directly —
            // GetHoldProgress() is a plain public method on Interactable
            // (>0 only while actually being held), no Harmony patch or
            // reflection needed. Drives both the audio (only while held,
            // not from spawn) and the real Mama noise pings.
            for (int i = 0; i < _testInteractables.Length; i++)
            {
                var interactable = _testInteractables[i];
                var audioSource = _testAudioSources[i];
                if (interactable == null || audioSource == null) continue;

                bool isHeld = interactable.GetHoldProgress() > 0f;

                if (isHeld && !_wasHeld[i])
                {
                    audioSource.Play();
                    RM.mamaManager?.OnNoise(interactable.transform.position, true);
                    _nextNoiseTime[i] = Time.time + HOLD_NOISE_INTERVAL;
                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Tape-TEST] Rewind started on '" + interactable.gameObject.name + "' — noise pinged.");
                }
                else if (isHeld && Time.time >= _nextNoiseTime[i])
                {
                    RM.mamaManager?.OnNoise(interactable.transform.position, true);
                    _nextNoiseTime[i] = Time.time + HOLD_NOISE_INTERVAL;
                }
                else if (!isHeld && _wasHeld[i])
                {
                    audioSource.Stop();
                }

                _wasHeld[i] = isHeld;
            }
        }

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

        /// <summary>Call once, early — best-effort search for a real VHS-tape-ish prop to clone. Optional: SpawnAllForTesting() falls back to a procedural placeholder if this never resolves.</summary>
        public void ResolveLocations()
        {
            if (_template != null || _resolving) return;
            _resolving = true;
            StartCoroutine(ResolveWhenReady());
        }

        private System.Collections.IEnumerator ResolveWhenReady()
        {
            Transform[] all = null;
            float waited = 0f;
            const float maxWait = 10f; // shorter than other objectives' 20s — this one has a real fallback, not a hard requirement
            while (waited < maxWait)
            {
                all = Resources.FindObjectsOfTypeAll<Transform>();
                if (all.Length > 0) break;
                yield return new WaitForSeconds(0.25f);
                waited += 0.25f;
            }
            all = all ?? new Transform[0];

            foreach (var t in FactoryMode.EndlessPreview ? new Transform[0] : all) // (Endless in the Facility: its "Caution tape" decoration matches "tape": never use that as the VHS tape)
            {
                if (t == null || !t.gameObject.scene.IsValid()) continue;
                if (t.gameObject.name.StartsWith("KE_")) continue;
                if (!t.gameObject.activeInHierarchy) continue;
                string nameLower = t.gameObject.name.ToLowerInvariant();
                foreach (string kw in TemplateNames)
                {
                    if (nameLower.Contains(kw))
                    {
                        _template = t.gameObject;
                        KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Tape] Real template resolved to '" + t.gameObject.name + "' at " + t.position);
                        break;
                    }
                }
                if (_template != null) break;
            }

            if (_template == null)
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Tape] No real vhs/tape/cassette prop found — will use a procedural placeholder.");

            // REWIND_CLIP was only ever confirmed as a raw baked AudioClip
            // on the menu's own Ambience object — never actually confirmed
            // as a real AudioController.Play()-able ID. Same failure class
            // as dryer_clunk (WashingMachineObjective), which has silently
            // failed to resolve twice — check explicitly instead of
            // assuming a third time.
            bool rewindClipValid = AudioController.IsValidAudioID(REWIND_CLIP);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Tape] REWIND_CLIP '" + REWIND_CLIP + "' valid AudioController ID=" + rewindClipValid);

            // Real, already-textured material confirmed via TAPE-TEX-DIAG —
            // exact-name match, not a keyword scan, since we already know
            // the real name from that log.
            foreach (var mat in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (mat == null) continue;
                if (mat.name == "vhs_tape")
                {
                    _realMaterial = mat;
                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Tape] Real 'vhs_tape' material resolved — using it on the placeholder box.");
                    break;
                }
            }
            if (_realMaterial == null)
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Tape] 'vhs_tape' material not found this time — placeholder will fall back to a flat color.");

            _resolving = false;
        }

        // Endless in the Waygetter Facility uses the owner's marks (FacilityMarks) instead.
        public static Vector3[] TapePositions { get { return FactoryMode.EndlessPreview ? FacilityMarks.World(FacilityMarks.Tape) : HouseTapePositions; } }

        private static Vector3 SnapToGround(Vector3 pos, float maxDistance = 2f)
        {
            RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out hit, maxDistance))
                return new Vector3(pos.x, hit.point.y, pos.z);
            // No hit within range — DON'T fall back to the raw, unsnapped
            // input position. That's exactly what made tape 3 vanish: a
            // starting point raised too high moved the true floor outside
            // this search window, the raycast found nothing, and the raw
            // (now way-too-high) input Y got used as-is, floating well
            // above anything walkable. Falling back to a fixed, clearly-
            // wrong basement height instead makes a bad snap obviously
            // broken (visibly buried) rather than invisibly floating.
            return FactoryMode.EndlessPreview ? pos : new Vector3(pos.x, -2.5f, pos.z);   // (the Facility is not at the house's basement height)
        }

        /// <summary>TESTING ONLY — spawns all 6 marked spots at once so they can be walked to and verified, instead of waiting on random chance across many runs.</summary>
        public void SpawnAllForTesting()
        {
            CleanUpTestTapes();
            for (int i = 0; i < TapePositions.Length; i++)
            {
                Vector3 pos = SnapToGround(TapePositions[i]);
                _testTapes[i] = BuildTape(pos, i + 1);
            }
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Tape-TEST] Spawned all 6 test tapes (KE_Tape_1..KE_Tape_6). Look at one and press E — the log will name it.");
        }

        // ── Real objective — spawns exactly 1 of 6, same shape as TattletailFriendObjective ──
        // Reuses the same _testTapes/_testInteractables/_testAudioSources
        // slots and Update() polling as the position-check spawner above —
        // there's nothing test-specific about that machinery, it just
        // tracks up to 6 possible tapes by slot index regardless of
        // whether 1 or all 6 are currently populated.
        private int _activeSlot = -1;

        /// <summary>Where the tape is (for RoomHints).</summary>
        public List<Vector3> RemainingPositions()
        {
            var list = new List<Vector3>();
            if (_activeSlot >= 0 && _testTapes[_activeSlot] != null) list.Add(_testTapes[_activeSlot].transform.position);
            return list;
        }

        /// <summary>Picks which of the 6 marked spots this run uses — only ever one index, but same array-based shape as Vase/Candle's DecideIndices().</summary>
        public int[] DecideIndices()
        {
            return new int[] { Random.Range(0, TapePositions.Length) };
        }

        /// <summary>Spawns the one real tape at the given index into TapePositions — see DecideIndices().</summary>
        public void StartObjective(int[] indices)
        {
            CleanUp();
            int idx = (indices != null && indices.Length > 0) ? indices[0] : 0;
            Vector3 pos = SnapToGround(TapePositions[idx]);
            _testTapes[idx] = BuildTape(pos, idx + 1);
            _activeSlot = idx;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Tape objective started — slot " + idx + ".");
        }

        /// <summary>True if the object the player just interacted with is the currently-active real tape.</summary>
        public bool IsTheTape(Interactable interactable)
        {
            return _activeSlot >= 0 && interactable != null &&
                   _testTapes[_activeSlot] != null && interactable.gameObject == _testTapes[_activeSlot];
        }

        /// <summary>Despawns the currently-active real tape (does not touch SpawnAllForTesting's slots — CleanUpTestTapes() handles those separately).</summary>
        public void CleanUp()
        {
            if (_activeSlot < 0) return;
            if (_testTapes[_activeSlot] != null) Destroy(_testTapes[_activeSlot]); // also destroys its AudioSource/Interactable
            _testTapes[_activeSlot] = null;
            _testInteractables[_activeSlot] = null;
            _testAudioSources[_activeSlot] = null;
            _wasHeld[_activeSlot] = false;
            _activeSlot = -1;
        }

        private GameObject BuildTape(Vector3 pos, int number)
        {
            GameObject go;
            GameObject visual;

            if (_template != null)
            {
                // Real prop — clone directly, same recipe as every other objective.
                go = Instantiate(_template, pos, _template.transform.rotation);
                visual = go;
                foreach (Interactable existing in go.GetComponentsInChildren<Interactable>(true))
                    DestroyImmediate(existing);
                foreach (Trigger trigger in go.GetComponentsInChildren<Trigger>(true))
                    DestroyImmediate(trigger);
            }
            else
            {
                // No real prop found — small procedural placeholder (same
                // "build it ourselves" fallback the vase shards use), split
                // into a full-scale parent (so the collider stays a normal,
                // clickable size) with a tiny tape-shaped visual child,
                // since a real VHS tape is small enough that scaling the
                // collider down to match would make it too fragile to hit.
                //
                // Lying flat, face up — thin axis (0.03) is the vertical
                // (Y) dimension, not a standing-on-edge box. SnapToGround's
                // raycast hit point is the true floor Y, but a box's pivot
                // is its center — placing the pivot AT floor level buries
                // half the box's thickness underground, which is exactly
                // what "too much inside the ground" was: no offset was
                // ever applied. Lifting by half the thickness (0.015) here
                // sits it flush on top of the floor instead.
                const float thickness = 0.03f;
                go = new GameObject();
                go.transform.position = pos + Vector3.up * (thickness / 2f);

                visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.transform.SetParent(go.transform, false);
                visual.transform.localScale = new Vector3(0.19f, thickness, 0.11f); // lying flat: thin dimension vertical
                Destroy(visual.GetComponent<Collider>());
                var rend = visual.GetComponent<Renderer>();
                if (rend != null)
                {
                    if (_realMaterial != null)
                        rend.material = _realMaterial; // real, already-textured VHS tape material — .material (not .sharedMaterial) instantiates a per-renderer copy, same convention used everywhere else in this codebase
                    else
                        rend.material.color = new Color(0.05f, 0.05f, 0.06f); // no real material resolved this run — flat near-black fallback
                }
            }

            go.name = "KE_Tape_" + number;

            Collider existingCol = go.GetComponent<Collider>();
            if (existingCol != null) Destroy(existingCol);
            var col = go.AddComponent<BoxCollider>();
            // 0.6 in every direction was wildly oversized for the tiny
            // procedural placeholder (0.19 x 0.03 x 0.11) — a 0.6m-tall
            // collider on a 0.03m-thick object meant hovering/interacting
            // could trigger well above or below the actual visible tape,
            // which is very likely what "nothing on the table" was really
            // showing. Generous but proportional for the placeholder;
            // 0.6 kept as-is for a real cloned prop, whose real size we
            // don't know in advance (same reasoning every other objective
            // uses for its own collider).
            col.size = (_template == null) ? new Vector3(0.4f, 0.15f, 0.3f) : Vector3.one * 0.6f;
            col.isTrigger = true;

            Interactable[] existingInteractables = FindObjectsOfType<Interactable>();
            if (existingInteractables.Length > 0)
                go.layer = existingInteractables[0].gameObject.layer;

            var interactable = go.AddComponent<Interactable>();
            interactable.actionText = "Rewind Tape";
            interactable.holdTime = 3f;
            interactable.highlight = true;
            interactable.setInactiveOnInteract = false;
            interactable.honks = false;
            interactable.newStateOnInteract = Interactable.State.Enabled;
            interactable.currentState = Interactable.State.Enabled;
            interactable.holdAudioName = string.Empty;
            interactable.onInteractEvent = new UnityEngine.Events.UnityEvent();
            interactable.conditions = new InteractableConditions();
            interactable.actions = new InteractableActions { interactActions = new InteractableActions.Actions[0] };

            // Only plays while actually being held now — GetHoldProgress()
            // (a plain public method on Interactable) polled every frame
            // from this class's own Update(), no Harmony patch needed.
            // Real captured clip takes a raw AudioSource (same recipe
            // WashingMachineObjective uses for dryer_clunk — this isn't an
            // AudioController ID, just a plain asset reference) so Update()
            // can directly Play()/Stop() it; stored into the tracking
            // arrays so Update() can find it by slot. Falls back to the
            // AudioController path only if capture never happened this
            // session — that path isn't hold-gated (AudioController.Play
            // doesn't hand back a controllable instance the way a raw
            // AudioSource does), so it's a degraded experience until the
            // real clip capture is confirmed reliable.
            int idx = number - 1;
            _testInteractables[idx] = interactable;
            if (_rewindClip != null)
            {
                var audioSource = go.AddComponent<AudioSource>();
                audioSource.clip = _rewindClip;
                audioSource.loop = true;
                audioSource.volume = 1f; // was 0.8 — "should play louder"
                audioSource.spatialBlend = 1f;
                audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
                audioSource.maxDistance = 15f;
                audioSource.playOnAwake = false;
                _testAudioSources[idx] = audioSource; // not started here — Update() starts/stops it based on real hold state
            }
            else
            {
                _testAudioSources[idx] = null;
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Tape-TEST] No captured clip yet — KE_Tape_" + number + " has no hold-gated audio this run.");
            }
            _wasHeld[idx] = false;

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Tape-TEST] KE_Tape_" + number + " placed at " + pos);
            return go;
        }

        /// <summary>True if the object the player just interacted with is one of the currently-spawned test tapes.</summary>
        public bool IsATapeTrigger(Interactable interactable)
        {
            if (interactable == null) return false;
            foreach (var t in _testTapes)
                if (t != null && t == interactable.gameObject) return true;
            return false;
        }

        public void CleanUpTestTapes()
        {
            AudioController.Stop(REWIND_CLIP);
            for (int i = 0; i < _testTapes.Length; i++)
            {
                if (_testTapes[i] != null) Destroy(_testTapes[i]); // also destroys its AudioSource/Interactable — no separate Stop() call needed for those
                _testTapes[i] = null;
                _testInteractables[i] = null;
                _testAudioSources[i] = null;
                _wasHeld[i] = false;
            }
        }
    }
}
