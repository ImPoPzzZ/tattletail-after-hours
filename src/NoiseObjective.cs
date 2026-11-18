using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// "Silence the noise" objective: something's making noise at the
    /// washing machine's real basement position — the only spot in this
    /// scene ever confirmed reliable (real name 'LevelSpecific_Washer', same
    /// position WashingMachineObjective.cs already resolves and verifies).
    ///
    /// Used to randomize between three spots (kitchen/garage/washer), but
    /// kitchen and garage were never more than guessed offsets from
    /// unrelated landmarks and the player couldn't reliably find them —
    /// restricted to just the washer per explicit request. No decoys
    /// needed anymore since there's only one possible spot.
    /// </summary>
    public class NoiseObjective : MonoBehaviour
    {
        public static NoiseObjective Instance { get; private set; }

        private const string NOISE_CLIP = "flashlight_danger";

        private Vector3? _spot; // resolved once, reused every time the objective runs
        private bool _resolving;
        private GameObject _activeGO;

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

        /// <summary>Call once, early (same timing as ChargingStation.Setup) — resolves and caches the washer's position.</summary>
        public void ResolveLocations()
        {
            if (_spot != null || _resolving) return;
            _resolving = true;
            StartCoroutine(ResolveLocationsWhenReady());
        }

        private System.Collections.IEnumerator ResolveLocationsWhenReady()
        {
            // Same early-level-load timing problem ChargingStation hit —
            // retry until the scene's actual objects are queryable.
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

            Vector3 basementFallback = new Vector3(2.5f, -2.5f, -0.7f); // offset from confirmed basement spot

            Vector3 washerPos = FindByKeyword(all, "washing") ?? FindByKeyword(all, "washer") ?? FindByKeyword(all, "laundry") ?? basementFallback;
            _spot = washerPos;

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Noise spot 'Basement' resolved to " + washerPos);

            _resolving = false;
        }

        private static Vector3? FindByKeyword(Transform[] all, string keyword)
        {
            foreach (var t in all)
            {
                if (t == null) continue;
                // Skip our own controller/spawned objects (KE_ prefix) — the
                // washing-machine objective hit this same bug class,
                // matching its own "washing" keyword against itself.
                if (t.gameObject.name.StartsWith("KE_")) continue;
                if (t.gameObject.name.ToLowerInvariant().Contains(keyword))
                {
                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Noise spot keyword '" + keyword + "' matched '" + t.gameObject.name + "' at " + t.position);
                    return t.position;
                }
            }
            return null;
        }

        // If the objective starts before the async resolve above has run
        // (e.g. it's the run's very first objective, or a Chaos Round pairs
        // it right at the start), resolve the washer position right now
        // instead of falling back to a guessed spot in the wrong part of
        // the basement.
        private void ResolveNow()
        {
            if (_spot != null) return;
            Transform[] all = FindObjectsOfType<Transform>();
            Vector3 washerPos = FindByKeyword(all, "washing") ?? FindByKeyword(all, "washer") ?? FindByKeyword(all, "laundry") ?? new Vector3(2.5f, -2.5f, -0.7f);
            _spot = washerPos;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Noise spot resolved on demand to " + washerPos);
        }

        public void StartObjective()
        {
            CleanUp();
            ResolveNow();

            Vector3 pos = _spot ?? new Vector3(2.5f, -2.5f, -0.7f);
            if (_spot == null)
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: NoiseObjective started before location resolved — using fallback.");

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Noise objective started at " + pos);
            SpawnMarker(pos);
        }

        private void SpawnMarker(Vector3 pos)
        {
            // No separate visual marker — the real washing machine mesh
            // already marks the spot, same reasoning as ChargingStation /
            // WashingMachineObjective. Generously sized, non-trigger hitbox
            // so exact alignment doesn't matter.
            var go = new GameObject("KE_NoiseMarker");
            go.transform.position = pos;
            _activeGO = go;

            var col = go.AddComponent<BoxCollider>();
            col.size = Vector3.one * 1.6f; // was 2.5f — reached too far
            col.isTrigger = false;

            AudioController.Play(NOISE_CLIP, go.transform);

            // Copy the interaction layer from a confirmed-clickable
            // Interactable rather than guessing — same fix the charging
            // station needed.
            Interactable[] existing = FindObjectsOfType<Interactable>();
            foreach (var it in existing)
            {
                if (it == null) continue;
                go.layer = it.gameObject.layer;
                break;
            }

            var interactable = go.AddComponent<Interactable>();
            interactable.actionText = "Turn Off";
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
        }

        /// <summary>True if the object the player just interacted with was the noise source.</summary>
        public bool IsTheActiveMarker(Interactable interactable)
        {
            return _activeGO != null && interactable != null && interactable.gameObject == _activeGO;
        }

        public void CleanUp()
        {
            AudioController.Stop(NOISE_CLIP);
            if (_activeGO != null) Destroy(_activeGO);
            _activeGO = null;
        }
    }
}
