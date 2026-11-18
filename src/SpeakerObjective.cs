using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// "Silence the Speaker" objective — a single fixed location, same
    /// shape as WashingMachineObjective.cs (not a pick-1-of-N spot like the
    /// tapes). Originally scoped as "Silence the Radio," but the real prop
    /// this scene actually has turned out to be named 'Speaker' (found via
    /// a live E-key mark, not a guess — our own "radio"/"stereo"/"boombox"
    /// keyword search never would have matched it). Find it and turn it
    /// off, while a loud static loop plays the whole time it's active —
    /// the "masking Mama's audio cues" flavor is realized as genuinely
    /// loud ambient noise at that spot rather than actually suppressing
    /// her real audio, same design call made for the original Washing
    /// Machine/Noise objectives.
    ///
    /// Location is now a confirmed real position, not a fallback guess.
    /// No separate visual marker is spawned — same reasoning as
    /// ChargingStation/WashingMachineObjective/NoiseObjective: the real
    /// 'Speaker' prop's own mesh is never touched, so it's still visible
    /// at this exact spot as the landmark.
    ///
    /// No real static/radio audio clip exists anywhere in this game's
    /// scenes (confirmed — this project's usual real-clip search came up
    /// empty every time). Rather than lean on a borrowed placeholder ID,
    /// the static is genuine white noise generated at runtime
    /// (AudioClip.Create() + random samples) and played through a plain
    /// positional AudioSource (spatialBlend=1, distance-attenuated from
    /// the real spot) — never routed through AudioController.Play(),
    /// which defaults to 2D/global and would be audible everywhere
    /// regardless of distance.
    /// </summary>
    public class SpeakerObjective : MonoBehaviour
    {
        public static SpeakerObjective Instance { get; private set; }

        private const string REAL_PROP_NAME = "Speaker"; // exact name, confirmed via a live E-key mark

        // Confirmed real position (E-key mark: playerPos=(4.5, 1.1, -6.2)
        // looking at 'Speaker' at (4.4, 1.1, -6.7)) — used as an immediate
        // fallback even before the live keyword search resolves, since
        // it's a known-good coordinate now, not a guess.
        private static readonly Vector3 KnownSpeakerPosition = new Vector3(4.4f, 1.1f, -6.7f);

        // Real static, generated ourselves — no real "static"/"radio" clip
        // exists in this level, and rather than keep scavenging or leaning
        // on a borrowed placeholder ID, this is genuine white noise built
        // at runtime via AudioClip.Create(): a short buffer filled with
        // random samples, looped. No external asset, no dependency on
        // anything else in this scene ever resolving. Cached once — every
        // call to StartObjective() across the whole run reuses the same
        // generated buffer.
        private static AudioClip _staticNoiseClip;

        private static AudioClip GetOrCreateStaticNoiseClip()
        {
            if (_staticNoiseClip != null) return _staticNoiseClip;

            const int sampleRate = 44100;
            const float durationSeconds = 3f; // long enough that the loop seam is inaudible in pure noise
            int sampleCount = Mathf.RoundToInt(sampleRate * durationSeconds);

            var clip = AudioClip.Create("KE_StaticNoise", sampleCount, 1, sampleRate, false);
            var samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
                samples[i] = UnityEngine.Random.Range(-1f, 1f);
            clip.SetData(samples, 0);

            _staticNoiseClip = clip;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Speaker] Generated " + durationSeconds + "s procedural static noise clip.");
            return _staticNoiseClip;
        }

        private Vector3? _pos;
        private bool _locationResolving;
        private GameObject _speakerGO;
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

        /// <summary>Call once, early (same timing as WashingMachineObjective.ResolveLocation) — resolves and caches the speaker's position.</summary>
        public void ResolveLocation()
        {
            if (_pos != null || _locationResolving) return;
            _locationResolving = true;
            StartCoroutine(ResolveLocationWhenReady());
        }

        private System.Collections.IEnumerator ResolveLocationWhenReady()
        {
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

            // Exact-name match first (we know the real prop's name now),
            // keyword search as backup, the confirmed live-tested
            // coordinate as the last-resort fallback — no more blind
            // guessing at a plausible room.
            Vector3? found = FindByExactName(all, REAL_PROP_NAME) ?? FindByKeyword(all, "speaker");
            _pos = found ?? KnownSpeakerPosition;

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Speaker resolved to " + _pos + (found == null ? " (using confirmed known position — live search didn't match this time)" : ""));

            _locationResolving = false;
        }

        private static Vector3? FindByExactName(Transform[] all, string name)
        {
            foreach (var t in all)
            {
                if (t == null) continue;
                if (t.gameObject.name == name)
                {
                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Speaker exact-name match: '" + t.gameObject.name + "' at " + t.position);
                    return t.position;
                }
            }
            return null;
        }

        private static Vector3? FindByKeyword(Transform[] all, string keyword)
        {
            foreach (var t in all)
            {
                if (t == null) continue;
                if (t.gameObject.name.StartsWith("KE_")) continue;
                if (t.gameObject.name.ToLowerInvariant().Contains(keyword))
                {
                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Speaker keyword '" + keyword + "' matched '" + t.gameObject.name + "' at " + t.position);
                    return t.position;
                }
            }
            return null;
        }


        public void StartObjective()
        {
            CleanUp();

            Vector3 pos = _pos ?? KnownSpeakerPosition;
            FSpot facilitySpot = default(FSpot);
            if (FactoryMode.EndlessPreview) { facilitySpot = FacilityMarks.Pick(FacilityMarks.Speaker); pos = facilitySpot.World; }
            if (_pos == null)
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: SpeakerObjective started before location resolved — using known fallback.");

            _speakerGO = new GameObject("KE_Speaker");
            _speakerGO.transform.position = pos;
            if (FactoryMode.EndlessPreview) { _speakerGO.transform.rotation = Quaternion.Euler(0f, facilitySpot.Yaw, 0f); }

            var col = _speakerGO.AddComponent<BoxCollider>();
            col.size = Vector3.one * 1.0f;
            if (FactoryMode.EndlessPreview) col.center = new Vector3(0f, 0.5f, 0f); // (the pivot is on the floor here)
            col.isTrigger = true;

            Interactable[] existing = FindObjectsOfType<Interactable>();
            if (existing.Length > 0)
            {
                _speakerGO.layer = existing[0].gameObject.layer;
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Speaker using layer " + _speakerGO.layer + " (copied from '" + existing[0].gameObject.name + "')");
            }

            // Positional, not global — a plain AudioSource parented on this
            // GameObject with spatialBlend=1 (fully 3D) attenuates by
            // distance from pos, the same way the washing machine's own
            // running sound works. No AudioController.Play() fallback path
            // here anymore — that route defaults to 2D/global (confirmed
            // spatialBlend=0 in this project's own AUDIO-DIAG logs for
            // flashlight_danger), which is exactly wrong for this.
            _runningAudioSource = _speakerGO.AddComponent<AudioSource>();
            _runningAudioSource.clip = GetOrCreateStaticNoiseClip();
            _runningAudioSource.loop = true;
            _runningAudioSource.volume = 1f;
            _runningAudioSource.spatialBlend = 1f;
            _runningAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            _runningAudioSource.maxDistance = 15f;
            _runningAudioSource.playOnAwake = false;
            _runningAudioSource.Play();
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Speaker playing procedural static (positional, spatialBlend=1) at " + pos);

            var interactable = _speakerGO.AddComponent<Interactable>();
            interactable.actionText = "Turn Off Speaker";
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

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Speaker objective started at " + pos);
        }

        /// <summary>True if the object the player just interacted with was the speaker.</summary>
        public Vector3? ActivePosition { get { return _speakerGO != null ? (Vector3?)_speakerGO.transform.position : null; } }   // (for the Facility's arrow)

        public bool IsTheSpeaker(Interactable interactable)
        {
            return _speakerGO != null && interactable != null && interactable.gameObject == _speakerGO;
        }

        public void CleanUp()
        {
            if (_speakerGO != null) Destroy(_speakerGO); // also stops/destroys _runningAudioSource, which lives on this GameObject
            _speakerGO = null;
            _runningAudioSource = null;
        }
    }
}
