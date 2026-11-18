using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// "Break 3 vases" objective. User marked 6 exact spots directly in-game
    /// (DebugLocationMarker, E key) rather than using the 6 real vanilla
    /// vase positions — full control over placement instead of being stuck
    /// with wherever those props happened to sit. Resolves one real vase
    /// object purely as a clone template (not for its own position), clones
    /// it at 3-of-6 random marked spots per objective start (same pattern
    /// as CandleObjective/TattletailFriendObjective — clone a confirmed-
    /// real instance instead of building one from scratch), scaled up and
    /// tinted green per earlier request (real vases were reported too small
    /// to notice). Breaking one spawns a shard-burst effect, plays the real
    /// "shatter" audio item, and makes real noise via RM.mamaManager.OnNoise
    /// (genuinely risky, not just cosmetic) — then destroys the clone.
    /// </summary>
    public class VaseObjective : MonoBehaviour
    {
        public static VaseObjective Instance { get; private set; }

        private static readonly string[] TemplateNames =
        {
            "Vase1", "Vase2", "Vase3", "Vase3 (3)", "LevelSpecific_Vase4", "Vase5",
        };

        // Exact spots the user marked in-game via DebugLocationMarker (E),
        // not vanilla vase positions.
        private static readonly Vector3[] HouseVasePositions =
        {
            new Vector3(4.8f, 1.1f, -1.4f),
            new Vector3(2.2f, -2.0f, 1.6f),
            new Vector3(8.7f, -2.0f, 6.1f),
            new Vector3(2.8f, -2.0f, -6.2f),
            new Vector3(-5.1f, -2.0f, -3.2f),
            new Vector3(0.8f, 1.1f, -7.6f),
        };

        // Endless in the Waygetter Facility uses the owner's marks (FacilityMarks) instead.
        private static Vector3[] VasePositions { get { return FactoryMode.EndlessPreview ? FacilityMarks.World(FacilityMarks.Vase) : HouseVasePositions; } }

        private GameObject _template;
        private bool _resolving;

        // Slot index = ordinal position in the indices array StartObjective()
        // was given (0..N-1).
        private class VaseSlot { public GameObject GO; public int SlotIndex; public bool Broken; }
        private readonly List<VaseSlot> _slots = new List<VaseSlot>();

        public int BrokenCount { get; private set; }

        /// <summary>Where the unbroken vases are (for RoomHints).</summary>
        public List<Vector3> RemainingPositions()
        {
            var list = new List<Vector3>();
            foreach (var s in _slots) if (!s.Broken && s.GO != null) list.Add(s.GO.transform.position);
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

        /// <summary>Call once, early (same timing as the other objectives' resolve calls) — resolves and caches a real vase to use as a clone template.</summary>
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
            const float maxWait = 20f;
            while (waited < maxWait)
            {
                all = Resources.FindObjectsOfTypeAll<Transform>();
                if (all.Length > 0) break;
                yield return new WaitForSeconds(0.25f);
                waited += 0.25f;
            }
            all = all ?? new Transform[0];

            foreach (string name in TemplateNames)
            {
                Transform t = FindExact(all, name);
                if (t != null)
                {
                    _template = t.gameObject;
                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Vase clone template resolved to '" + name + "'.");
                    break;
                }
            }
            if (_template == null)
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: No real vase found in scene — objective cannot spawn clones.");

            _resolving = false;
        }

        private static Transform FindExact(Transform[] all, string name)
        {
            foreach (Transform t in all)
            {
                if (t == null || !t.gameObject.scene.IsValid()) continue;
                if (t.gameObject.name == name) return t;
            }
            return null;
        }

        /// <summary>Picks which 3-of-6 marked spots this run uses. Always returned in ascending index order so spawn order is deterministic.</summary>
        public int[] DecideIndices()
        {
            var indices = new List<int>();
            for (int i = 0; i < VasePositions.Length; i++) indices.Add(i);
            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int tmp = indices[i]; indices[i] = indices[j]; indices[j] = tmp;
            }
            var chosen = indices.GetRange(0, Mathf.Min(3, VasePositions.Length));
            chosen.Sort();
            return chosen.ToArray();
        }

        /// <summary>Spawns a vase clone at each given index into VasePositions — see DecideIndices().</summary>
        public void StartObjective(int[] indices)
        {
            CleanUp();

            if (_template == null)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: VaseObjective.StartObjective — no template resolved, cannot start.");
                return;
            }

            TargetCount = indices.Length;
            BrokenCount = 0;

            for (int slot = 0; slot < indices.Length; slot++)
            {
                GameObject go = SpawnVase(SnapToGround(VasePositions[indices[slot]]));
                _slots.Add(new VaseSlot { GO = go, SlotIndex = slot, Broken = false });
            }

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Vase objective started — " + TargetCount + " vases to break.");
        }

        // Marked positions were captured from the player camera, which
        // turned out to already be close to floor height (basement marks
        // came in at y=-2.0 vs. real basement props at y~-2.4/-2.5 — only
        // ~0.4-0.5 off), not up at head height like first assumed. The
        // first version of this cast 3 units up then 10 down — in a
        // multi-level house that's more than enough to punch through the
        // floor slab above and snap to the *upper* level instead, which is
        // exactly what put the vases out of reach ("hidden in a wall").
        // Keep both the start offset and search distance small so this can
        // only catch the true nearby floor, never a different story.
        private static Vector3 SnapToGround(Vector3 pos)
        {
            RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out hit, 2f))
                return new Vector3(pos.x, hit.point.y, pos.z);
            return pos;
        }

        private GameObject SpawnVase(Vector3 pos)
        {
            GameObject clone = Instantiate(_template, pos, _template.transform.rotation);
            clone.name = "KE_Vase";
            clone.SetActive(true);
            ApplyBigGreenSkin(clone);

            // Strip any leftover Interactable/Trigger the source model came
            // with before adding our own. DestroyImmediate, not Destroy —
            // Destroy() defers to end of frame, leaving the old and new
            // Interactable coexisting for the rest of it (confirmed as a
            // real bug for PhoneObjective's real-prop reuse; harmless here
            // in practice since these clone decorative templates that
            // rarely carry a real one, but no reason to leave the same
            // latent risk in place).
            foreach (Interactable existing in clone.GetComponentsInChildren<Interactable>(true))
                DestroyImmediate(existing);
            foreach (Trigger trigger in clone.GetComponentsInChildren<Trigger>(true))
                DestroyImmediate(trigger);

            Collider existingCol = clone.GetComponent<Collider>();
            if (existingCol != null) Destroy(existingCol);
            var col = clone.AddComponent<BoxCollider>();
            col.size = Vector3.one * 1.0f; // was 1.2f (1.8f blocked movement) — reached too far, tightened a bit
            col.isTrigger = true; // was a solid collider, physically blocking the player from walking through it

            // The box used to sit on the model's pivot (usually its base, so
            // half the box was under the floor and it felt offset from the
            // vase). Center it on the vase's visible body instead.
            Bounds bodyBounds;
            bool haveBounds = false;
            bodyBounds = new Bounds(clone.transform.position, Vector3.zero);
            foreach (Renderer r in clone.GetComponentsInChildren<Renderer>(false))
            {
                if (r == null || !r.enabled) continue;
                if (!haveBounds) { bodyBounds = r.bounds; haveBounds = true; }
                else bodyBounds.Encapsulate(r.bounds);
            }
            if (haveBounds)
            {
                // Sized to sit INSIDE the vase (70% of its bounds), not
                // stick out past it: physics raycasts don't hit a collider
                // the ray starts inside of, so a box bigger than the vase
                // meant standing close put the camera inside it and the
                // vase couldn't be clicked at all.
                col.center = clone.transform.InverseTransformPoint(bodyBounds.center);
                Vector3 s = clone.transform.lossyScale;
                Vector3 local = new Vector3(
                    bodyBounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(s.x)),
                    bodyBounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(s.y)),
                    bodyBounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(s.z))) * 0.7f;
                col.size = new Vector3(Mathf.Max(0.05f, local.x), Mathf.Max(0.05f, local.y), Mathf.Max(0.05f, local.z));
            }

            // Copy the interaction layer from a confirmed-clickable
            // Interactable rather than guessing.
            Interactable[] existingInteractables = FindObjectsOfType<Interactable>();
            if (existingInteractables.Length > 0)
                clone.layer = existingInteractables[0].gameObject.layer;

            var interactable = clone.AddComponent<Interactable>();
            interactable.actionText = "Break";
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

        private static void ApplyBigGreenSkin(GameObject vaseGO)
        {
            vaseGO.transform.localScale *= 1.6f; // was 2.5f — too huge, dialed back per request
            foreach (Renderer r in vaseGO.GetComponentsInChildren<Renderer>(true))
            {
                r.material.color = new Color(0.1f, 0.6f, 0.15f);
            }
        }

        public bool IsAVaseTrigger(Interactable interactable)
        {
            return interactable != null && _slots.Exists(s => s.GO == interactable.gameObject && !s.Broken);
        }

        /// <summary>Looks up which slot a spawned clone belongs to. -1 if not found.</summary>
        public int GetSlotIndex(GameObject go)
        {
            var slot = _slots.Find(s => s.GO == go);
            return slot != null ? slot.SlotIndex : -1;
        }

        /// <summary>
        /// Host-authoritative apply — solo also calls this (via BreakVase
        /// below) since solo is inherently its own single authority. Mutates
        /// BrokenCount and destroys the clone. No-ops (but still reports the
        /// current allDone state) if the slot's already broken, so a
        /// duplicate ObjectiveInteractRequest for the same slot — e.g. both
        /// players clicking the same vase in the same frame — can never
        /// double-count.
        /// </summary>
        public bool BreakVaseAtSlot(int slotIndex)
        {
            var slot = _slots.Find(s => s.SlotIndex == slotIndex);
            if (slot == null || slot.Broken) return BrokenCount >= TargetCount;
            if (slot.GO == null) { slot.Broken = true; return BrokenCount >= TargetCount; }

            Vector3 pos = slot.GO.transform.position;

            SpawnShatterEffect(pos);
            // "shatter" confirmed as a real, existing audio item via
            // decompile (used once in vanilla QuestFinishSeance.cs) — fits
            // perfectly here, no invented clip name.
            AudioController.Play("shatter", pos);
            // A vase breaking is genuinely loud — let Mama actually hear it
            // (RM.mamaManager.OnNoise, same call the flashlight shake uses),
            // so breaking one is real risk, not just a free objective step.
            if (!EndlessTraits.VaseBreakIsSilent()) RM.mamaManager?.OnNoise(pos, true);

            Destroy(slot.GO);
            slot.GO = null;
            slot.Broken = true;

            BrokenCount++;
            Daily.Add("vases", 1);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Vase broken " + BrokenCount + "/" + TargetCount);
            return BrokenCount >= TargetCount;
        }

        // No pre-made shatter/debris art to reuse, so this builds a quick
        // procedural burst instead of just instantly hiding the vase —
        // small cubes flying outward under gravity, no collider (keeps them
        // from getting stuck in world geometry), auto-cleaned after 3s.
        private static void SpawnShatterEffect(Vector3 pos)
        {
            const int shardCount = 7;
            for (int i = 0; i < shardCount; i++)
            {
                GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = "KE_VaseShard";
                shard.transform.position = pos;
                shard.transform.localScale = Vector3.one * Random.Range(0.08f, 0.16f);

                Collider col = shard.GetComponent<Collider>();
                if (col != null) Destroy(col);

                Rigidbody rb = shard.AddComponent<Rigidbody>();
                Vector3 dir = Random.onUnitSphere;
                if (dir.y < 0f) dir.y = -dir.y; // bias outward/upward, not into the floor
                rb.AddForce(dir * Random.Range(1.5f, 3.5f), ForceMode.VelocityChange);
                rb.AddTorque(Random.insideUnitSphere * 5f, ForceMode.VelocityChange);

                Destroy(shard, 3f);
            }
        }

        public void CleanUp()
        {
            foreach (var slot in _slots)
                if (slot.GO != null) Destroy(slot.GO);
            _slots.Clear();
        }
    }
}
