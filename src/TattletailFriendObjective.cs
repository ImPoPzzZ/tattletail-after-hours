using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// "Find Tattletail's friend" objective. User marked 6 exact spots
    /// directly in-game (DebugLocationMarker, E key), same workflow already
    /// used for VaseObjective/CandleObjective — full control over placement
    /// instead of being stuck with wherever FriendPlacer_1/2/friendN
    /// happened to sit. Clones the player's own real Tattletail model
    /// (RM.tattletail.tattletail) as the visual template — always present
    /// and active for the whole run, unlike the decorative
    /// 'tattletail3@Friend_N' props, which this same mod deactivates at run
    /// start (see SceneBootstrap's FRIEND-REMOVE pass); a template search
    /// for those could lose that race and silently fall back to a bare
    /// placeholder. GoldRecolor/DiamondRecolor already treat
    /// RM.tattletail.tattletail as a pure visual-only sub-object (no
    /// TattletailController on it — that lives on FPSController), so
    /// cloning it here is the same safe, established pattern, not a new one.
    /// 3.3: cloned from PristineTattletail (the model before the player's color went on) and
    /// given a random color from the pool, like the party Tattletails, instead of your own.
    ///
    /// Same from-scratch Interactable recipe as ChargingStation.cs/
    /// NoiseObjective.cs/WashingMachineObjective.cs: strip any leftover
    /// Interactable/Trigger the clone came with, add our own from scratch.
    /// </summary>
    public class TattletailFriendObjective : MonoBehaviour
    {
        public static TattletailFriendObjective Instance { get; private set; }

        // Exact spots the user marked in-game via DebugLocationMarker (E),
        // not the vanilla FriendPlacer_1/2/friendN positions.
        private static readonly Vector3[] HouseFriendPositions =
        {
            new Vector3(4.6f, 1.1f, 1.6f),
            new Vector3(-8.8f, 0.7f, 10.7f),
            new Vector3(2.6f, -2.0f, 0.8f),
            new Vector3(9.3f, -2.0f, 6.1f),
            new Vector3(-0.9f, -2.0f, -7.2f),
            new Vector3(-5.7f, -2.0f, -1.0f),
        };

        // Endless in the Waygetter Facility uses the owner's marks (FacilityMarks) instead.
        private static Vector3[] FriendPositions { get { return FactoryMode.EndlessPreview ? FacilityMarks.World(FacilityMarks.Friend) : HouseFriendPositions; } }

        private GameObject _spawned;

        /// <summary>Where the friend is (for RoomHints).</summary>
        public List<Vector3> RemainingPositions()
        {
            var list = new List<Vector3>();
            if (_spawned != null) list.Add(_spawned.transform.position);
            return list;
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

        // Marked positions were captured from the player camera, which is
        // close to floor height but not exactly on it — same small gap
        // fixed for vases/candles. Keep both the start offset and search
        // distance small so this can only catch the true nearby floor,
        // never punch through into a different story of the house.
        private static Vector3 SnapToGround(Vector3 pos)
        {
            RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out hit, 2f))
                return new Vector3(pos.x, hit.point.y, pos.z);
            return pos;
        }

        /// <summary>Picks which of the 6 marked spots this run uses (only ever one index, but same array-based shape as VaseObjective/CandleObjective's DecideIndices()).</summary>
        public int[] DecideIndices()
        {
            return new int[] { Random.Range(0, FriendPositions.Length) };
        }

        /// <summary>Spawns the friend clone at the given index into FriendPositions — see DecideIndices().</summary>
        public void StartObjective(int[] indices)
        {
            CleanUp();

            GameObject template = PristineTattletail.Source; // untouched, then a random color below (not the player's)
            if (template == null)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: TattletailFriendObjective.StartObjective — RM.tattletail.tattletail not available, cannot start.");
                return;
            }

            int idx = indices != null && indices.Length > 0 ? indices[0] : 0;
            Vector3 pos = SnapToGround(FriendPositions[idx]);
            _spawned = Instantiate(template, pos, template.transform.rotation);
            _spawned.name = "KE_TattletailFriend";
            PartyTattletailModels.StandLikePartyTattletail(_spawned);
            _spawned.SetActive(true);
            PartyTattletailModels.ApplyRandomColor(_spawned); // any color from the pool: Original, Yellow, Teal, Black, White, Gold, Diamond

            // Strip any leftover Interactable/Trigger the source model came
            // with before adding our own — same reasoning as
            // ChargingStation's from-scratch rebuild. DestroyImmediate, not
            // Destroy — see VaseObjective's identical comment.
            foreach (Interactable existing in _spawned.GetComponentsInChildren<Interactable>(true))
                DestroyImmediate(existing);
            foreach (Trigger trigger in _spawned.GetComponentsInChildren<Trigger>(true))
                DestroyImmediate(trigger);

            // Always replace with our own trigger collider, same fix
            // applied to VaseObjective/CandleObjective — a solid collider
            // here would physically block the player from walking through
            // it (interaction still works via raycast, which hits triggers
            // by default).
            Collider existingCol = _spawned.GetComponent<Collider>();
            if (existingCol != null) Destroy(existingCol);
            var box = _spawned.AddComponent<BoxCollider>();
            box.size = Vector3.one * 1.0f;
            box.isTrigger = true;

            // Copy the interaction layer from a confirmed-clickable
            // Interactable rather than guessing.
            Interactable[] existingInteractables = FindObjectsOfType<Interactable>();
            if (existingInteractables.Length > 0)
            {
                _spawned.layer = existingInteractables[0].gameObject.layer;
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Tattletail's friend using layer " + _spawned.layer + " (copied from '" + existingInteractables[0].gameObject.name + "')");
            }

            var interactable = _spawned.AddComponent<Interactable>();
            interactable.actionText = "Say Hi";
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

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Tattletail's friend spawned at " + pos);
        }

        /// <summary>True if the object the player just interacted with was the spawned friend.</summary>
        public bool IsTheFriend(Interactable interactable)
        {
            return _spawned != null && interactable != null && interactable.gameObject == _spawned;
        }

        public void CleanUp()
        {
            if (_spawned != null) Destroy(_spawned);
            _spawned = null;
        }
    }
}
