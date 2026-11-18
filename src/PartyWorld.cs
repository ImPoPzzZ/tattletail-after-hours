using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// The world side of Papa's 10th Birthday!'s party materials: 18 hand-marked
    /// spots, each of which puts a random ingredient on the floor every 30 seconds
    /// — but only if it's empty (an ingredient nobody took just stays). Hold interact to take one into
    /// the inventory (see PartyGame.cs); a taken spot fills again at its next tick.
    /// Also has a debug showroom (off) that lays every model out in rows.
    /// </summary>
    public class PartyMaterialSpawner : MonoBehaviour
    {
        // E-key marks (playerPos) — the floor is found by a raycast below each.
        private static readonly Vector3[] Marks =
        {
            new Vector3(-1.2f, -2.0f, 9.0f),
            new Vector3(1.8f, -1.5f, 8.5f),  new Vector3(2.4f, -1.4f, 8.5f),  new Vector3(9.2f, -2.0f, 6.6f),
            new Vector3(9.2f, -2.0f, 0.6f),  new Vector3(6.4f, -2.0f, -5.0f), new Vector3(-1.7f, -2.0f, -6.4f),
            new Vector3(-3.4f, -1.4f, -6.0f), new Vector3(-6.3f, -2.0f, -3.3f), new Vector3(-5.2f, -2.0f, 2.1f),
            new Vector3(1.3f, -1.0f, 3.1f),  new Vector3(4.2f, -1.0f, 3.2f),  new Vector3(5.3f, 0.7f, 10.1f),
            new Vector3(-0.3f, 0.7f, 12.2f), new Vector3(-4.5f, 0.7f, 10.0f), new Vector3(-9.2f, 0.7f, 12.3f),
        };

        private const float INTERVAL = 30f;

        // TEMP debug: lays out every food and ingredient in front of the player at the start of the mode (set false to remove).
        public static bool DebugShowroom = false;

        private class Spot
        {
            public Vector3 mark;
            public GameObject item;
            public string itemName;
            public float timer;
        }

        private readonly List<Spot> _spots = new List<Spot>();
        private Transform _itemsRoot;

        // Foods dropped by a broken piñata: they lie there until somebody takes them.
        private class Loose { public GameObject go; public string name; }
        private readonly List<Loose> _loose = new List<Loose>();

        /// <summary>Registers an already-built pickup (e.g. a food machine's finished output) with the same take/inventory handling as a spot's own ingredients.</summary>
        public void AddLoose(GameObject go, string name) { _loose.Add(new Loose { go = go, name = name }); }

        private const float PINATA_INTERVAL = 30f;         // every 30 seconds...
        private const float PINATA_CHANCE = 0.10f;         // ...a 10% chance a normal piñata shows up...
        private const float GOLDEN_PINATA_CHANCE = 0.01f;   // ...or a separate 1% chance for a golden one (checked first)
        private const float PINATA_ENTERTAINMENT = 20f;     // a normal piñata fills Entertainment by this much
        private float _pinataTimer = PINATA_INTERVAL;
        private GameObject _pinata;
        private bool _pinataGolden;

        public bool IsPinata(GameObject go) { return _pinata != null && _pinata == go; }
        public bool IsGoldenPinata { get { return _pinata != null && _pinataGolden; } }

        public static PartyMaterialSpawner Instance { get; private set; }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>True if this object is one of the ingredients lying around.</summary>
        public bool IsItem(GameObject go)
        {
            foreach (var s in _spots) if (s.item != null && s.item == go) return true;
            foreach (var l in _loose) if (l.go != null && l.go == go) return true;
            return false;
        }

        public string PeekName(GameObject go)
        {
            foreach (var s in _spots) if (s.item != null && s.item == go) return s.itemName;
            foreach (var l in _loose) if (l.go != null && l.go == go) return l.name;
            return null;
        }

        /// <summary>The ingredient was picked up — remove it; this spot spawns a new one at its next 30s tick.</summary>
        public void Take(GameObject go)
        {
            foreach (var s in _spots)
            {
                if (s.item != null && s.item == go)
                {
                    Destroy(s.item);
                    s.item = null;
                    s.itemName = null;
                    return;
                }
            }
            for (int i = 0; i < _loose.Count; i++)
            {
                if (_loose[i].go != null && _loose[i].go == go)
                {
                    Destroy(go);
                    _loose.RemoveAt(i);
                    return;
                }
            }
        }

        // ── the party piñata ─────────────────────────────────────────────────
        private void SpawnPinata(bool golden)
        {
            Vector3 mark = Marks[Random.Range(0, Marks.Length)];
            Vector3 floor = FloorUnder(mark);
            var pin = PartyFoods.Build(golden ? "Golden Pinata" : "Pinata", floor, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), _itemsRoot);
            if (pin == null) return;

            var col = pin.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.1f, 0f);
            col.size = new Vector3(0.9f, 0.6f, 0.6f);
            col.isTrigger = true;
            PartyGameRunner.MakeInteractable(pin, "Break " + (golden ? "Golden " : "") + "Pi\u00f1ata", 0.8f);

            _pinata = pin;
            _pinataGolden = golden;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Party] A " + (golden ? "GOLDEN " : "") + "pi\u00f1ata appeared at " + floor + ".");
            if (RM.hud != null) RM.hud.SetNewHint(golden ? "A GOLDEN party pi\u00f1ata appeared somewhere in the house!" : "A party pi\u00f1ata appeared somewhere in the house!", 3.5f);
        }

        /// <summary>Breaks the piñata: confetti, and 3-5 ready foods (random finished foods) drop around it. Returns how many.</summary>
        public int BreakPinata()
        {
            if (_pinata == null) return 0;
            bool golden = _pinataGolden;
            Vector3 pos = _pinata.transform.position;
            Vector3 bodyCentre = pos + Vector3.up * 1.1f;
            Destroy(_pinata);
            _pinata = null;
            _pinataGolden = false;
            PartyConfetti.Burst(bodyCentre);

            if (PartyGameRunner.Instance != null)
            {
                PartyGameRunner.Instance.Entertainment = golden ? 100f : Mathf.Min(100f, PartyGameRunner.Instance.Entertainment + PINATA_ENTERTAINMENT);
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Party] " + (golden ? "Golden pi\u00f1ata" : "Pi\u00f1ata") + " broken \u2014 Entertainment " + (golden ? "filled to 100%" : "+" + PINATA_ENTERTAINMENT + "%") + ".");
            }

            int count = Random.Range(3, 6);   // 3 to 5
            for (int i = 0; i < count; i++)
            {
                string food = PartyItems.FoodNames[Random.Range(0, PartyItems.FoodNames.Length)];
                float a = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
                Vector3 floor = FloorUnder(pos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.55f + Vector3.up * 0.5f);
                var go = PartyFoods.BuildHeld(food, floor, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), _itemsRoot);
                if (go == null) continue;

                var col = go.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 0.1f, 0f);
                col.size = new Vector3(0.35f, 0.3f, 0.35f);
                col.isTrigger = true;
                PartyGameRunner.MakeInteractable(go, "Take " + food, 0.4f);

                var lightGO = new GameObject("Glow");
                lightGO.transform.SetParent(go.transform, false);
                lightGO.transform.localPosition = new Vector3(0f, 0.6f, 0f);
                var light = lightGO.AddComponent<Light>();
                light.type = LightType.Point; light.range = 1.6f; light.intensity = 0.7f; light.color = new Color(1f, 0.85f, 0.55f);

                _loose.Add(new Loose { go = go, name = food });
            }
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Party] Pi\u00f1ata broken — " + count + " foods dropped.");
            return count;
        }

        public static void Setup(GameObject host)
        {
            var spawner = host.AddComponent<PartyMaterialSpawner>();
            Instance = spawner;
            spawner.Init(host.transform);
        }

        private void Init(Transform parent)
        {
            var itemsGO = new GameObject("KE_PartyMaterials");
            itemsGO.transform.SetParent(parent, false);
            _itemsRoot = itemsGO.transform;

            foreach (var mark in Marks)
            {
                var spot = new Spot { mark = mark };
                Respawn(spot);                                   // something's there from the start
                spot.timer = Random.Range(5f, INTERVAL);         // staggered, so they don't all refresh at once
                _spots.Add(spot);
            }
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Party] " + _spots.Count + " material spots active — a random ingredient every " + INTERVAL + "s each.");

            if (DebugShowroom) BuildShowroom(parent);
        }

        private void Update()
        {
            var run = EndlessRunManager.Instance;
            if (run == null || !run.RunActive) return;

            _pinataTimer -= Time.deltaTime;
            if (_pinataTimer <= 0f)
            {
                _pinataTimer = PINATA_INTERVAL;
                if (_pinata == null)
                {
                    if (Random.value < GOLDEN_PINATA_CHANCE) SpawnPinata(true);
                    else if (Random.value < PINATA_CHANCE) SpawnPinata(false);
                }
            }

            foreach (var spot in _spots)
            {
                spot.timer -= Time.deltaTime;
                if (spot.timer > 0f) continue;
                spot.timer = INTERVAL;
                if (spot.item == null) Respawn(spot);   // only fills an EMPTY spot — never replaces what's still there
            }
        }

        private void Respawn(Spot spot)
        {
            if (spot.item != null) return;

            // Any ingredient except the one that's already there.
            string pick;
            do { pick = PartyFoods.IngredientNames[Random.Range(0, PartyFoods.IngredientNames.Length)]; }
            while (pick == spot.itemName && PartyFoods.IngredientNames.Length > 1);

            Vector3 floor = FloorUnder(spot.mark);
            var rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var item = PartyFoods.Build(pick, floor, rot, _itemsRoot);
            if (item == null) return;

            // Something you can take: a small trigger box and a hold-to-pick-up prompt.
            var col = item.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.15f, 0f);
            col.size = new Vector3(0.45f, 0.4f, 0.45f);
            col.isTrigger = true;
            PartyGameRunner.MakeInteractable(item, "Take " + pick, 0.4f);

            // The house is dark and these are small — give each a faint warm glow so it can be found.
            var lightGO = new GameObject("Glow");
            lightGO.transform.SetParent(item.transform, false);
            lightGO.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 1.3f;
            light.intensity = 0.6f;
            light.color = new Color(1f, 0.85f, 0.55f);

            spot.item = item;
            spot.itemName = pick;
        }

        // The marks are the player's position, a bit above the floor — find what's actually under it.
        internal static Vector3 FloorUnder(Vector3 pos)
        {
            RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * 0.3f, Vector3.down, out hit, 3f, ~0, QueryTriggerInteraction.Ignore))
                return new Vector3(pos.x, hit.point.y, pos.z);
            return new Vector3(pos.x, pos.y - 0.5f, pos.z);
        }

        // ── temporary showroom ────────────────────────────────────────────────
        private void BuildShowroom(Transform parent)
        {
            if (RM.fpsController == null) return;
            Transform cam = RM.fpsController.transform;
            Vector3 fwd = cam.forward; fwd.y = 0f;
            fwd = fwd.sqrMagnitude < 0.0001f ? Vector3.forward : fwd.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Quaternion facePlayer = Quaternion.LookRotation(-fwd);
            Vector3 origin = cam.position + fwd * 3.5f;

            var room = new GameObject("KE_PartyShowroom");
            room.transform.SetParent(parent, false);

            PlaceRow(room.transform, PartyFoods.FoodNames, origin, right, facePlayer, 1.4f);
            PlaceRow(room.transform, Slice(PartyFoods.IngredientNames, 0, 11), origin + fwd * 2f, right, facePlayer, 0.8f);
            PlaceRow(room.transform, Slice(PartyFoods.IngredientNames, 11, 10), origin + fwd * 3.6f, right, facePlayer, 0.8f);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Party] Showroom laid out in front of the player (temporary).");
        }

        private static string[] Slice(string[] all, int start, int count)
        {
            var list = new List<string>();
            for (int i = start; i < start + count && i < all.Length; i++) list.Add(all[i]);
            return list.ToArray();
        }

        private static void PlaceRow(Transform parent, string[] names, Vector3 center, Vector3 right, Quaternion rot, float spacing)
        {
            for (int i = 0; i < names.Length; i++)
            {
                float offset = (i - (names.Length - 1) * 0.5f) * spacing;
                Vector3 pos = FloorUnder(center + right * offset);
                var model = PartyFoods.Build(names[i], pos, rot, parent);
                if (model == null) continue;

                var labelGO = new GameObject("Label");
                labelGO.transform.SetParent(model.transform, false);
                labelGO.transform.position = pos + Vector3.up * 1.25f;
                var tm = labelGO.AddComponent<TextMesh>();
                tm.text = names[i];
                tm.characterSize = 0.06f;
                tm.fontSize = 48;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = new Color(1f, 0.95f, 0.7f);
                Font f = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (f != null)
                {
                    tm.font = f;
                    labelGO.GetComponent<MeshRenderer>().sharedMaterial = f.material;
                }
                labelGO.AddComponent<PartyLabelBillboard>();
            }
        }
    }

    /// <summary>Turns a showroom label to face the player's camera.</summary>
    public class PartyLabelBillboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }

    /// <summary>A short burst of colored paper pieces (a piñata breaking).</summary>
    public class PartyConfetti : MonoBehaviour
    {
        private class Piece { public Transform t; public Vector3 vel; public Vector3 spin; }
        private readonly System.Collections.Generic.List<Piece> _pieces = new System.Collections.Generic.List<Piece>();
        private float _life = 2.2f;

        public static void Burst(Vector3 pos)
        {
            var go = new GameObject("KE_Confetti");
            go.transform.position = pos;
            var c = go.AddComponent<PartyConfetti>();
            Shader sh = Shader.Find("Unlit/Color");
            Color[] cols = { new Color(0.8f, 0.2f, 0.25f), new Color(0.9f, 0.7f, 0.15f), new Color(0.25f, 0.5f, 0.8f), new Color(0.3f, 0.7f, 0.35f), new Color(0.85f, 0.4f, 0.65f) };
            for (int i = 0; i < 36; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(q.GetComponent<Collider>());
                q.transform.SetParent(go.transform, false);
                q.transform.localScale = new Vector3(0.05f, 0.05f, 0.004f);
                q.transform.localRotation = Random.rotation;
                var m = new Material(sh != null ? sh : Shader.Find("Diffuse"));
                m.color = cols[i % cols.Length];
                q.GetComponent<Renderer>().sharedMaterial = m;
                c._pieces.Add(new Piece { t = q.transform, vel = Random.onUnitSphere * Random.Range(1.2f, 3.2f) + Vector3.up * 1.2f, spin = Random.insideUnitSphere * 400f });
            }
        }

        private void Update()
        {
            _life -= Time.deltaTime;
            foreach (var p in _pieces)
            {
                if (p.t == null) continue;
                p.vel += Vector3.down * 4f * Time.deltaTime;
                p.vel *= (1f - 0.8f * Time.deltaTime);
                p.t.position += p.vel * Time.deltaTime;
                p.t.Rotate(p.spin * Time.deltaTime);
            }
            if (_life <= 0f) Destroy(gameObject);
        }
    }
}
