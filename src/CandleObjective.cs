using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// "Light the candles" objective. Candle is a real, dedicated vanilla
    /// component (confirmed via decompile: SetCandle(bool) plays a flame
    /// ParticleSystem + restores emissive glow) — used here purely as a
    /// clone template, not for its own position. User marked 6 exact spots
    /// directly in-game (DebugLocationMarker, E key) rather than using
    /// wherever the vanilla candle props happened to sit. Pick 3 of the 6
    /// marked spots at random each time, clone the real candle prop at
    /// each (real mesh + flame effect, same trick as TattletailFriendObjective
    /// — clone a confirmed-real instance instead of building one from
    /// scratch), strip any leftover Interactable/Trigger the clone came
    /// with, add our own from-scratch Interactable. Lit candles stay lit as
    /// more are found; the clones (all of them, lit or not) are destroyed
    /// once the objective completes — "despawn when the objective is met."
    /// </summary>
    public class CandleObjective : MonoBehaviour
    {
        public static CandleObjective Instance { get; private set; }

        private const string LIGHT_CANDLES_LINE = "tt_light_the_candles";

        // Exact spots the user marked in-game via DebugLocationMarker (E),
        // not vanilla candle positions.
        private static readonly Vector3[] HouseCandlePositions =
        {
            new Vector3(10.4f, -2.0f, 6.4f),
            new Vector3(5.9f, -2.0f, -5.2f),
            new Vector3(-2.7f, -2.0f, -6.4f),
            new Vector3(-5.6f, -2.0f, 1.8f),
            new Vector3(1.6f, -2.0f, 8.0f),
            new Vector3(6.2f, 1.1f, -4.4f),
        };

        // Endless in the Waygetter Facility uses the owner's marks (FacilityMarks) instead.
        private static Vector3[] CandlePositions { get { return FactoryMode.EndlessPreview ? FacilityMarks.World(FacilityMarks.Candle) : HouseCandlePositions; } }

        private GameObject _template;
        private bool _resolving;

        // Slot index = ordinal position in the indices array StartObjective()
        // was given (0..N-1).
        private class CandleSlot { public GameObject GO; public Candle Candle; public int SlotIndex; public bool Lit; }
        private readonly List<CandleSlot> _slots = new List<CandleSlot>();

        public int LitCount { get; private set; }

        /// <summary>Where the unlit candles are (for RoomHints).</summary>
        public List<Vector3> RemainingPositions()
        {
            var list = new List<Vector3>();
            foreach (var s in _slots) if (!s.Lit && s.GO != null) list.Add(s.GO.transform.position);
            return list;
        }
        public int TargetCount { get; private set; }

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

        /// <summary>Call once, early (same timing as the other objectives' resolve calls) — resolves and caches a real Candle to use as a clone template.</summary>
        public void ResolveLocations()
        {
            if (_template != null || _resolving) return;
            _resolving = true;
            StartCoroutine(ResolveWhenReady());
        }

        private System.Collections.IEnumerator ResolveWhenReady()
        {
            Candle[] all = null;
            float waited = 0f;
            const float maxWait = 20f;
            while (waited < maxWait)
            {
                all = Resources.FindObjectsOfTypeAll<Candle>();
                if (all.Length > 0) break;
                yield return new WaitForSeconds(0.25f);
                waited += 0.25f;
            }
            all = all ?? new Candle[0];

            foreach (Candle c in all)
            {
                if (c == null || !c.gameObject.scene.IsValid()) continue;
                _template = c.gameObject;
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Candle clone template resolved to '" + c.gameObject.name + "'.");
                break;
            }
            if (_template == null)
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: No real Candle component found in this scene — objective cannot spawn clones.");

            _resolving = false;
        }

        /// <summary>Picks which 3-of-6 marked spots this run uses. Always returned in ascending index order so spawn order is deterministic.</summary>
        public int[] DecideIndices()
        {
            var indices = new List<int>();
            for (int i = 0; i < CandlePositions.Length; i++) indices.Add(i);
            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int tmp = indices[i]; indices[i] = indices[j]; indices[j] = tmp;
            }
            var chosen = indices.GetRange(0, Mathf.Min(3, CandlePositions.Length));
            chosen.Sort();
            return chosen.ToArray();
        }

        /// <summary>Spawns a candle clone at each given index into CandlePositions — see DecideIndices().</summary>
        public void StartObjective(int[] indices)
        {
            CleanUp();

            if (_template == null)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: CandleObjective.StartObjective — no template resolved, cannot start.");
                return;
            }

            TargetCount = indices.Length;
            LitCount = 0;

            for (int slot = 0; slot < indices.Length; slot++)
            {
                Candle candle;
                GameObject go = SpawnCandle(SnapToGround(CandlePositions[indices[slot]]), out candle);
                _slots.Add(new CandleSlot { GO = go, Candle = candle, SlotIndex = slot, Lit = false });
            }

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Candle objective started — " + TargetCount + " candles to light.");
        }

        // Marked positions were captured from the player camera, which
        // turned out to already be close to floor height (basement marks
        // came in at y=-2.0 vs. real basement candles at y=-2.4/-2.5 — only
        // ~0.4-0.5 off), not up at head height like first assumed. The
        // first version of this cast 3 units up then 10 down — in a
        // multi-level house that's more than enough to punch through the
        // floor slab above and snap to the *upper* level instead, which is
        // exactly what put the candles out of reach ("hidden in a wall").
        // Keep both the start offset and search distance small so this can
        // only catch the true nearby floor, never a different story.
        private static Vector3 SnapToGround(Vector3 pos)
        {
            RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out hit, 2f))
                return new Vector3(pos.x, hit.point.y, pos.z);
            return pos;
        }

        private GameObject SpawnCandle(Vector3 pos, out Candle candleComp)
        {
            GameObject clone = Instantiate(_template, pos, _template.transform.rotation);
            clone.name = "KE_Candle";
            clone.SetActive(true);

            // Candle.SetCandle() reads a `candleMat` field that Candle only
            // assigns in its own Start() (candleMat = candleRenderer.
            // materials[0]) — calling SetCandle() ourselves right here,
            // synchronously after Instantiate, hits that field before
            // Start() has ever run and NREs. Just set `startOn` instead;
            // Candle.Start() calls SetCandle(startOn) itself once candleMat
            // is ready, so this still guarantees an unlit start.
            candleComp = clone.GetComponent<Candle>();
            if (candleComp != null)
                candleComp.startOn = false;

            // Strip any leftover Interactable/Trigger the source model came
            // with before adding our own — same reasoning as
            // TattletailFriendObjective's clone. DestroyImmediate, not
            // Destroy — see VaseObjective's identical comment.
            foreach (Interactable existing in clone.GetComponentsInChildren<Interactable>(true))
                DestroyImmediate(existing);
            foreach (Trigger trigger in clone.GetComponentsInChildren<Trigger>(true))
                DestroyImmediate(trigger);

            // Always replace with our own trigger collider, same as
            // VaseObjective — the source Candle prop may or may not already
            // have one, but if it does it's a solid collider that would
            // physically block the player from walking through it.
            Collider existingCol = clone.GetComponent<Collider>();
            if (existingCol != null) Destroy(existingCol);
            var col = clone.AddComponent<BoxCollider>();
            col.size = Vector3.one * 0.6f; // smaller than vases' — candles are thin, don't need as generous a hitbox
            col.isTrigger = true;

            // Copy the interaction layer from a confirmed-clickable
            // Interactable rather than guessing.
            Interactable[] existingInteractables = FindObjectsOfType<Interactable>();
            if (existingInteractables.Length > 0)
                clone.layer = existingInteractables[0].gameObject.layer;

            var interactable = clone.AddComponent<Interactable>();
            interactable.actionText = "Light";
            interactable.holdTime = 0.6f;
            interactable.highlight = true;
            interactable.setInactiveOnInteract = false;
            interactable.honks = false;
            interactable.newStateOnInteract = Interactable.State.Enabled;
            interactable.currentState = Interactable.State.Enabled;
            interactable.holdAudioName = string.Empty;
            interactable.onInteractEvent = new UnityEngine.Events.UnityEvent();
            interactable.conditions = new InteractableConditions();
            interactable.actions = new InteractableActions { interactActions = new InteractableActions.Actions[0] };

            return clone;
        }

        public bool IsACandleTrigger(Interactable interactable)
        {
            return interactable != null && _slots.Exists(s => s.GO == interactable.gameObject && !s.Lit);
        }

        /// <summary>Looks up which slot a spawned clone belongs to. -1 if not found.</summary>
        public int GetSlotIndex(GameObject go)
        {
            var slot = _slots.Find(s => s.GO == go);
            return slot != null ? slot.SlotIndex : -1;
        }

        /// <summary>Host-authoritative apply (solo also calls this via LightCandle below). No-ops on a slot already lit, so a duplicate ObjectiveInteractRequest can't double-count.</summary>
        public bool LightCandleAtSlot(int slotIndex)
        {
            var slot = _slots.Find(s => s.SlotIndex == slotIndex);
            if (slot == null || slot.Lit) return LitCount >= TargetCount;

            if (slot.Candle != null) slot.Candle.SetCandle(true); // stays lit as more are found
            slot.Lit = true;

            // Remove the ability to interact with it once lit — a repeat
            // interact is already no-op'd by slot.Lit above, but the
            // Interactable itself was still hoverable/clickable (showing
            // "[Light]" forever) with nothing visibly different about an
            // already-lit candle.
            if (slot.GO != null)
            {
                var interactable = slot.GO.GetComponent<Interactable>();
                if (interactable != null) Destroy(interactable);
            }

            LitCount++;
            Daily.Add("candles", 1);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Candle lit " + LitCount + "/" + TargetCount);
            return LitCount >= TargetCount;
        }

        public void CleanUp()
        {
            foreach (var slot in _slots)
                if (slot.GO != null) Destroy(slot.GO);
            _slots.Clear();
        }
    }
}
