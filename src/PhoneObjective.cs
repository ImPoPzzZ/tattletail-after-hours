using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Spawns the walk-up-and-hold "answer the phone" interactable —
    /// exactly the same shape as NoiseObjective's washer marker: resolve a
    /// real reference position once (here, the real "Landline"
    /// SpookyPhone prop if this scene has one), then spawn our OWN fresh
    /// GameObject at that position rather than modifying the real prop in
    /// place. Modifying the real prop directly was the previous approach
    /// and the actual bug — its own vanilla Interactable/Trigger strip
    /// went through Destroy() (deferred), leaving stale state around, and
    /// its real child hierarchy (unlike a from-scratch GameObject) wasn't
    /// guaranteed to have a collider positioned where hovering would find
    /// it. All actual ring/timer/quest-text/Mama-pause game logic lives in
    /// EndlessRunManager (StartPhoneRing/EndPhoneRing/OnPhoneAnswered),
    /// mirroring StartRestPhase()/EndRestPhase() exactly — this class only
    /// ever owns the spawned marker object.
    /// </summary>
    public class PhoneObjective : MonoBehaviour
    {
        public static PhoneObjective Instance { get; private set; }

        private GameObject _phoneGO;
        private GameObject _realPhoneGO; // resolved once per run, reused every ring — null if this scene has none
        private bool _resolving;

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

        // ── Location resolve (once, early — same timing as every other objective) ──

        /// <summary>Call once per run, early (same timing as the other objectives' resolve calls, before StartRun()) — resolves and caches the real phone prop's position, if this scene has one.</summary>
        public void ResolveLocation()
        {
            if (_realPhoneGO != null || _resolving) return;
            _resolving = true;
            StartCoroutine(ResolveLocationWhenReady());
        }

        private GameObject _facilityLook;   // (Endless in the Facility: the copy of the real phone on the marked spot)

        private System.Collections.IEnumerator ResolveLocationWhenReady()
        {
            Transform[] all = null;
            float waited = 0f;
            const float maxWait = 20f;
            while (waited < maxWait)
            {
                all = Resources.FindObjectsOfTypeAll<Transform>();
                if (all.Length > 0) break;
                yield return new WaitForSeconds(0.25f);
                waited += 0.25f;
            }
            all = all ?? new Transform[0];

            // Most precise: a real SpookyPhone component instance, if this
            // scene happens to include one — confirmed real vanilla class
            // via decompile (the game's own ringing-phone script).
            foreach (SpookyPhone sp in Resources.FindObjectsOfTypeAll<SpookyPhone>())
            {
                if (sp == null || !sp.gameObject.scene.IsValid()) continue;
                _realPhoneGO = sp.gameObject;
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Phone] Real phone resolved via SpookyPhone component: '" + sp.gameObject.name + "' at " + sp.transform.position);
                break;
            }

            // Fallback: name search, same keyword-match trick NoiseObjective
            // uses for the washer — in case this scene's phone model exists
            // without the SpookyPhone script attached (SpookyPhone's own
            // level checks are Night 1/7-specific, so Night 6 — the level
            // Endless always reuses — may only have the bare model).
            if (_realPhoneGO == null)
            {
                foreach (var t in all)
                {
                    if (t == null || !t.gameObject.scene.IsValid()) continue;
                    if (t.gameObject.name.StartsWith("KE_")) continue;
                    if (t.gameObject.name.ToLowerInvariant().Contains("phone"))
                    {
                        _realPhoneGO = t.gameObject;
                        KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Phone] Real phone resolved via name match: '" + t.gameObject.name + "' at " + t.position);
                        break;
                    }
                }
            }

            if (_realPhoneGO == null)
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Phone] No real phone prop found in this scene — will spawn near the player each ring instead.");

            _resolving = false;
        }

        // ── Marker spawn/despawn — called by EndlessRunManager's StartPhoneRing()/EndPhoneRing()/OnPhoneAnswered() ──

        public void SpawnMarker()
        {
            DespawnMarker();

            Vector3 pos;
            if (FactoryMode.EndlessPreview) // Endless in the Waygetter Facility: the marked spot, with a copy of the real phone on it
            {
                FSpot spot = FacilityMarks.Phone[0];
                pos = spot.World;
            }
            else
            if (_realPhoneGO != null)
            {
                pos = _realPhoneGO.transform.position;
            }
            else if (RM.fpsController != null)
            {
                pos = SnapToGround(RM.fpsController.transform.position + RM.fpsController.transform.forward * 2f);
            }
            else
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Phone] Neither a real phone nor fpsController available — cannot place marker.");
                return;
            }

            // No separate visual marker needed when a real phone was
            // resolved — its own mesh already marks the spot, same
            // reasoning as ChargingStation/NoiseObjective. Generously
            // sized, trigger (never solid) hitbox so exact alignment with
            // the real mesh (or exact position in front of the player)
            // doesn't matter.
            var go = new GameObject("KE_Phone");
            go.transform.position = pos;
            _phoneGO = go;

            var col = go.AddComponent<BoxCollider>();
            col.size = Vector3.one * 1.0f; // was 1.5f — reached too far
            col.isTrigger = true;

            // Copy the interaction layer from a confirmed-clickable
            // Interactable rather than guessing.
            Interactable[] existing = FindObjectsOfType<Interactable>();
            foreach (var it in existing)
            {
                if (it == null) continue;
                go.layer = it.gameObject.layer;
                break;
            }

            var interactable = go.AddComponent<Interactable>();
            interactable.actionText = "End Run?";
            interactable.holdTime = 0.8f;
            interactable.highlight = true;
            interactable.setInactiveOnInteract = false;
            interactable.honks = false;
            interactable.newStateOnInteract = Interactable.State.Enabled;
            interactable.currentState = Interactable.State.Enabled;
            interactable.holdAudioName = string.Empty;
            interactable.onInteractEvent = new UnityEngine.Events.UnityEvent();
            interactable.conditions = new InteractableConditions();
            interactable.actions = new InteractableActions { interactActions = new InteractableActions.Actions[0] };

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Phone] Marker placed at " + pos + (_realPhoneGO != null ? " (real phone prop)." : " (near player)."));
        }

        private static Vector3 SnapToGround(Vector3 pos)
        {
            RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out hit, 2f))
                return new Vector3(pos.x, hit.point.y, pos.z);
            return pos;
        }

        /// <summary>True if the object the player just interacted with is the ringing phone marker.</summary>
        public Vector3? ActivePosition { get { return _phoneGO != null ? (Vector3?)_phoneGO.transform.position : null; } }   // (for the Facility's arrow)

        public bool IsThePhone(Interactable interactable)
        {
            return _phoneGO != null && interactable != null && interactable.gameObject == _phoneGO;
        }

        /// <summary>Always our own spawned marker now (never the real prop itself), so always safe to destroy outright.</summary>
        public void DespawnMarker()
        {
            if (_phoneGO != null) Destroy(_phoneGO);
            if (_facilityLook != null) Destroy(_facilityLook);
            _facilityLook = null;
            _phoneGO = null;
        }
    }
}
