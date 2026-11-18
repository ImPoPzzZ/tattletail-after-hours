using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// The 4 permanent food machines in Papa's 10th Birthday!: hand-marked spots
    /// (Popcorn on the floor; Hot Dog, Nachos and Waffle & Crepe on a counter/
    /// table). Each already visually has its "main" ingredient built into the
    /// model (the popper's already full of popcorn, the grill already has hot
    /// dogs on it, the warmer's already got cheese in it), so each only needs
    /// ONE more ingredient fed in — unlike the hotbar's own 2-ingredient recipes
    /// in PartyItems, which still apply if you'd rather hand-craft these away
    /// from the machines. Feed it, and the finished food appears as a small
    /// take-able pickup (PartyMaterialSpawner's loose-item system), then the
    /// machine resets to empty.
    /// </summary>
    public class PartyMachineController : MonoBehaviour
    {
        public static PartyMachineController Instance { get; private set; }

        // What each machine still needs — the ingredient it doesn't already have baked into its model.
        private static readonly Dictionary<string, string> MachineNeeds = new Dictionary<string, string>
        {
            { "Popcorn", "Butter" },          // the popper's already full of popcorn/corn
            { "Hot Dog", "Buns" },            // the grill's already got hot dogs on it
            { "Nachos", "Nacho Chips" },      // the warmer's already got cheese in it
            { "Waffle & Crepe", "Flour" },    // just cooking hardware — no ingredient baked in either way, picked one
        };

        private class Machine
        {
            public string Food;
            public GameObject GO;
            public Interactable It;
            public string Need;
        }

        private readonly List<Machine> _machines = new List<Machine>();
        private Transform _root;

        public static void Setup(GameObject host)
        {
            var c = host.AddComponent<PartyMachineController>();
            Instance = c;
            c.Build(host.transform);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Build(Transform parent)
        {
            _root = parent;
            // Mark 1 — floor.
            Add("Popcorn", new Vector3(2.4f, 1.1f, 4.8f));
            // Marks 2-4 — on a counter/table (same "find the surface right below the mark" logic as the floor one).
            Add("Hot Dog", new Vector3(6.7f, 2.1f, 1.6f));
            Add("Nachos", new Vector3(6.1f, 2.2f, 6.4f));
            Add("Waffle & Crepe", new Vector3(-3.2f, -1.5f, -3.7f));
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Party] " + _machines.Count + " food machine(s) placed.");
        }

        private void Add(string food, Vector3 mark)
        {
            Vector3 pos = PartyMaterialSpawner.FloorUnder(mark); // same raycast-down — finds the counter/table surface just as well as the floor
            GameObject go = PartyFoods.Build(food, pos, Quaternion.identity, _root);
            if (go == null)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Party] no model for machine '" + food + "' — skipped.");
                return;
            }
            go.name = "KE_PartyMachine_" + food;

            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.3f, 0f);
            col.size = new Vector3(0.7f, 0.9f, 0.6f);

            var m = new Machine { Food = food, GO = go };
            if (!MachineNeeds.TryGetValue(food, out m.Need))
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Party] no ingredient set for machine '" + food + "'.");

            m.It = PartyGameRunner.MakeInteractable(go, PromptFor(m), 0.6f);
            _machines.Add(m);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Party] Machine '" + food + "' placed at " + pos + " (needs " + m.Need + ").");
        }

        private void Update()
        {
            var run = EndlessRunManager.Instance;
            if (run == null || !run.RunActive) return;
            foreach (var m in _machines)
                if (m.It != null) m.It.actionText = PromptFor(m);
        }

        private static string PromptFor(Machine m)
        {
            return m.Need == null ? "Use " + m.Food + " Machine" : "Add " + m.Need;
        }

        public bool IsMachine(GameObject go)
        {
            foreach (var m in _machines) if (m.GO == go) return true;
            return false;
        }

        /// <summary>Tries to feed `holding` into the machine `go`. Returns true if it was consumed (caller removes it from the inventory slot).</summary>
        public bool TryFeed(GameObject go, string holding)
        {
            Machine m = null;
            foreach (var candidate in _machines) if (candidate.GO == go) { m = candidate; break; }
            if (m == null || m.Need == null) return false;

            if (string.IsNullOrEmpty(holding))
            {
                PartyGameRunner.Hint("Press the number of an ingredient first.");
                return false;
            }

            if (holding != m.Need)
            {
                PartyGameRunner.Hint(holding + " doesn't go in the " + m.Food + " machine.");
                return false;
            }

            SpawnReady(m);
            return true;
        }

        private void SpawnReady(Machine m)
        {
            Vector3 pos = m.GO.transform.position + m.GO.transform.forward * 0.35f + Vector3.up * 0.15f;
            GameObject food = PartyFoods.BuildHeld(m.Food, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), _root);
            if (food == null) return;

            var col = food.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.1f, 0f);
            col.size = new Vector3(0.35f, 0.3f, 0.35f);
            col.isTrigger = true;
            PartyGameRunner.MakeInteractable(food, "Take " + m.Food, 0.4f);

            var lightGO = new GameObject("Glow");
            lightGO.transform.SetParent(food.transform, false);
            lightGO.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Point; light.range = 1.6f; light.intensity = 0.7f; light.color = new Color(1f, 0.85f, 0.55f);

            if (PartyMaterialSpawner.Instance != null) PartyMaterialSpawner.Instance.AddLoose(food, m.Food);
            PartyGameRunner.Hint(m.Food + " is ready — take it!");
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Party] Machine finished a " + m.Food + ".");
        }
    }
}
