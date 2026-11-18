using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Real, self-contained "find and turn off the washing machine"
    /// objective — same from-scratch Interactable recipe as
    /// ChargingStation.cs and NoiseObjective.cs (proven reliable this
    /// session): a fresh GameObject with its own collider and Interactable,
    /// nothing borrowed from the vanilla washing machine prop or the
    /// vanilla quest system, both of which turned out unreliable here.
    /// No separate visual marker — the real washing machine mesh should
    /// already be visible at this position, same reasoning ChargingStation
    /// used for TattletailPlanningProp.
    /// </summary>
    public class WashingMachineObjective : MonoBehaviour
    {
        public static WashingMachineObjective Instance { get; private set; }

        // Fallback only — used if the real clip below can't be resolved for
        // some reason. The real clip is "dryer_clunk", confirmed via a live
        // AudioSource scene-scan diagnostic (found on a GameObject named
        // "Washing Machine Sound") — it's a plain Unity AudioClip on a raw
        // AudioSource, not an AudioController-registered audio ID at all,
        // which is why AudioController.Play(RUNNING_CLIP) below was always
        // just a placeholder standing in for the real sound.
        private const string RUNNING_CLIP = "flashlight_danger";
        private const string REAL_CLIP_SOURCE_NAME = "Washing Machine Sound";
        private const string REAL_CLIP_NAME = "dryer_clunk";

        private Vector3? _pos;
        private AudioClip _clunkClip;
        private bool _resolving;
        private GameObject _machineGO;
        private AudioSource _runningAudioSource;

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

        // Diagnostic: the "washing"/"washer"/"laundry" keyword search found
        // no real scene object, meaning the actual washing machine mesh has
        // some other internal name (or isn't a separate named object at
        // all) — same "stop guessing, log ground truth" approach that
        // eventually found the real charging station. While this objective
        // is active, log whatever's directly in front of the player once a
        // second: name (if any) and world position, so the next test's log
        // tells us exactly where to place/name-match the real machine
        // instead of relying on another guessed offset.
        private float _diagTimer;
        private void Update()
        {
            if (EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive) return;
            if (EndlessRunManager.Instance.CurrentObjective != ObjectiveType.WashingMachine) return;
            if (RM.fpsController == null) return;

            _diagTimer += Time.deltaTime;
            if (_diagTimer < 1f) return;
            _diagTimer = 0f;

            Transform cam = RM.fpsController.transform;
            RaycastHit hit;
            if (Physics.Raycast(cam.position, cam.forward, out hit, 5f))
            {
                KaleidoscopeEndlessPlugin.Log.LogInfo(
                    "KE: [WM-DIAG] Looking at '" + hit.collider.gameObject.name + "' hitPoint=" + hit.point +
                    " playerPos=" + cam.position);
            }
            else
            {
                KaleidoscopeEndlessPlugin.Log.LogInfo(
                    "KE: [WM-DIAG] No raycast hit within 5m. playerPos=" + cam.position + " forward=" + cam.forward);
            }
        }

        /// <summary>Call once, early (same timing as ChargingStation.Setup / NoiseObjective.ResolveLocations) — resolves and caches the machine's position.</summary>
        public void ResolveLocation()
        {
            if (_pos != null || _resolving) return;
            _resolving = true;
            StartCoroutine(ResolveLocationWhenReady());
        }

        private System.Collections.IEnumerator ResolveLocationWhenReady()
        {
            // Same early-level-load timing problem ChargingStation and
            // NoiseObjective hit — retry until the scene's actual objects
            // are queryable.
            Transform[] all = null;
            float waited = 0f;
            const float maxWait = 20f;
            while (waited < maxWait)
            {
                all = FindObjectsOfType<Transform>();
                if (all.Length > 0) break;
                yield return new WaitForSeconds(0.25f);
                waited += 0.25f;
            }
            all = all ?? new Transform[0];

            // Same basement fallback NoiseObjective uses for this exact
            // prop, offset from the confirmed charging station spot.
            Vector3 fallback = new Vector3(2.5f, -2.5f, -0.7f);

            Vector3? found = FindByKeyword(all, "washing") ?? FindByKeyword(all, "washer") ?? FindByKeyword(all, "laundry");
            _pos = found ?? fallback;

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Washing machine resolved to " + _pos +
                (found == null ? " (fallback — no matching object found)" : ""));

            _resolving = false;
        }

        private bool _clipResolving;

        // Set once by Night2AudioScavenger (main-menu-only, one-shot) if it
        // successfully finds the real clip in Night 2's own scene — this
        // level (Night 6/TheParty) never actually carries the object at
        // all, confirmed by repeated live scans across a shared base scene
        // both nights load, so ResolveClip() checks this first and skips
        // its own doomed in-level search entirely when it's already set.
        private static AudioClip _scavengedClip;
        public static void SetScavengedClip(AudioClip clip) { _scavengedClip = clip; }

        /// <summary>
        /// Call once per level entry, as early as possible (from
        /// SceneMonitor.SetupMonoBehaviours, right when the level loads) —
        /// deliberately NOT tied to ResolveLocation()'s timing. A real test
        /// showed the clip-scan always failing when run at
        /// ResolveLocation()'s usual time (StartEndless(), i.e.
        /// flashlight-pickup time — well into actual gameplay) even though
        /// the exact same scan reliably found the clip ~2s after scene load
        /// via a separate diagnostic. Most likely explanation: the real
        /// "Washing Machine Sound" GameObject only exists briefly early in
        /// the level. The AudioClip ASSET reference itself, once captured,
        /// stays valid regardless of what later happens to the GameObject/
        /// AudioSource that originally carried it.
        /// </summary>
        public void ResolveClip()
        {
            if (_clunkClip != null || _clipResolving) return; // Unity's fake-null correctly detects an already-destroyed cached reference too

            if (_scavengedClip != null)
            {
                _clunkClip = _scavengedClip;
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Washing machine using Night 2-scavenged clip '" + _clunkClip.name + "' — skipping in-level scan.");
                return;
            }

            _clipResolving = true;
            StartCoroutine(ResolveClipWhenReady());
        }

        private System.Collections.IEnumerator ResolveClipWhenReady()
        {
            float waited = 0f;
            const float maxWait = 15f;
            while (_clunkClip == null && waited < maxWait)
            {
                AudioSource[] sources = Resources.FindObjectsOfTypeAll<AudioSource>();
                foreach (var src in sources)
                {
                    if (src == null || !src.gameObject.scene.IsValid() || src.clip == null) continue; // skip prefab/asset-only instances
                    if (src.gameObject.name == REAL_CLIP_SOURCE_NAME || src.clip.name == REAL_CLIP_NAME)
                    {
                        _clunkClip = src.clip;
                        KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Washing machine real clip resolved: '" + src.clip.name + "' (from '" + src.gameObject.name + "').");
                        break;
                    }
                }
                if (_clunkClip != null) break;
                yield return new WaitForSeconds(0.5f);
                waited += 0.5f;
            }
            if (_clunkClip == null)
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: Real '" + REAL_CLIP_NAME + "' clip never resolved after " + maxWait + "s — will fall back to '" + RUNNING_CLIP + "' placeholder.");
            _clipResolving = false;
        }

        private static Vector3? FindByKeyword(Transform[] all, string keyword)
        {
            foreach (var t in all)
            {
                if (t == null) continue;
                // Skip our own controller/spawned objects (KE_ prefix) —
                // "WashingMachineObjective" matching its own "washing"
                // keyword and caching (0,0,0) as the "resolved" position
                // was the actual bug here.
                if (t.gameObject.name.StartsWith("KE_")) continue;
                if (t.gameObject.name.ToLowerInvariant().Contains(keyword))
                {
                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Washing machine keyword '" + keyword + "' matched '" + t.gameObject.name + "' at " + t.position);
                    return t.position;
                }
            }
            return null;
        }

        public void StartObjective()
        {
            CleanUp();

            Vector3 pos = _pos ?? new Vector3(2.5f, -2.5f, -0.7f);
            if (_pos == null)
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: WashingMachineObjective started before location resolved — using fallback.");

            bool facility = FactoryMode.EndlessPreview;   // Endless in the Waygetter Facility: one of the marked spots, and a machine drawn in code (the house has the real model standing there)
            FSpot spot = default(FSpot);
            if (facility) { spot = FacilityMarks.Pick(FacilityMarks.Washer); pos = spot.World; }

            _machineGO = new GameObject("KE_WashingMachine");
            _machineGO.transform.position = pos;
            if (facility) { _machineGO.transform.rotation = Quaternion.Euler(0f, spot.Yaw, 0f); }

            // Generously sized, non-trigger hitbox — exact alignment with
            // the vanilla mesh doesn't matter, same reasoning as
            // ChargingStation's collider.
            var col = _machineGO.AddComponent<BoxCollider>();
            col.size = Vector3.one * 1.6f; // was 2.5f — reached too far
            if (facility) col.center = new Vector3(0f, 0.6f, 0f); // (the pivot is on the floor here, not in the middle of the machine)
            col.isTrigger = false;

            // Copy the interaction layer from a confirmed-clickable
            // Interactable rather than guessing.
            Interactable[] existing = FindObjectsOfType<Interactable>();
            Interactable layerSource = existing.Length > 0 ? existing[0] : null;
            if (layerSource != null)
            {
                _machineGO.layer = layerSource.gameObject.layer;
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Washing machine using layer " + _machineGO.layer + " (copied from '" + layerSource.gameObject.name + "')");
            }
            else
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: No existing Interactable found to copy layer from — washing machine may not be raycast-hittable.");
            }

            // Real running-noise clip, driven by our own fresh AudioSource
            // instead of the real vanilla one (which has volume=0 even in
            // pure vanilla — confirmed by the user, not our bug to fix, but
            // also not something worth depending on). 3D positional so the
            // player can actually home in on it to find the machine, which
            // is the whole point of this objective.
            if (_clunkClip != null)
            {
                _runningAudioSource = _machineGO.AddComponent<AudioSource>();
                _runningAudioSource.clip = _clunkClip;
                _runningAudioSource.loop = true;
                _runningAudioSource.volume = 0.8f;
                _runningAudioSource.spatialBlend = 1f;
                _runningAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
                _runningAudioSource.maxDistance = 15f;
                _runningAudioSource.playOnAwake = false;
                _runningAudioSource.Play();
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Washing machine playing real '" + REAL_CLIP_NAME + "' clip.");
            }
            else
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: Real '" + REAL_CLIP_NAME + "' clip unavailable — falling back to '" + RUNNING_CLIP + "' placeholder.");
                var audioObj = AudioController.Play(RUNNING_CLIP, _machineGO.transform);
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Washing machine fallback AudioController.Play returned " +
                    (audioObj == null ? "NULL" : ("non-null, IsPlaying=" + audioObj.IsPlaying())));
            }

            var interactable = _machineGO.AddComponent<Interactable>();
            interactable.actionText = "Turn Off Washing Machine";
            interactable.holdTime = 1.2f;
            interactable.highlight = true;
            interactable.setInactiveOnInteract = false;
            interactable.honks = false;
            interactable.newStateOnInteract = Interactable.State.Enabled;
            interactable.currentState = Interactable.State.Enabled;
            interactable.holdAudioName = string.Empty;
            interactable.onInteractEvent = new UnityEngine.Events.UnityEvent();
            interactable.conditions = new InteractableConditions();
            interactable.actions = new InteractableActions { interactActions = new InteractableActions.Actions[0] };

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Washing machine objective started at " + pos);
        }

        /// <summary>True if the object the player just interacted with was the washing machine.</summary>
        public Vector3? ActivePosition { get { return _machineGO != null ? (Vector3?)_machineGO.transform.position : null; } }   // (for the Facility's arrow)

        public bool IsTheMachine(Interactable interactable)
        {
            return _machineGO != null && interactable != null && interactable.gameObject == _machineGO;
        }

        public void CleanUp()
        {
            AudioController.Stop(RUNNING_CLIP); // harmless no-op unless the fallback path was used
            if (_machineGO != null) Destroy(_machineGO); // also stops/destroys _runningAudioSource, which lives on this GameObject
            _machineGO = null;
            _runningAudioSource = null;
        }
    }
}
