using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>A hand-marked spot in the Facility: building-local x and z, the floor height (0 ground, 4 upstairs, -4 basement; a few sit on a table) and the way it faces.</summary>
    public struct FSpot
    {
        public float X, Z, H, Yaw;
        public FSpot(float x, float z, float h, float yaw) { X = x; Z = z; H = h; Yaw = yaw; }
        public Vector3 World { get { return FactoryController.Origin + new Vector3(X, H, Z); } }
    }

    /// <summary>
    /// Endless in the Waygetter Facility: the real Endless (EndlessRunManager and every objective class, exactly as in the house)
    /// with two differences: the spawn spots come from the owner's marks (docs/facility_marks.txt, below), and Mama is off.
    /// </summary>
    public static class FacilityMarks
    {
        public static bool On { get { return FactoryMode.EndlessPreview; } }

        public static readonly FSpot[] Vase =
        {
            new FSpot(4.65f, 24.40f, -3.99f, 31f), new FSpot(13.47f, 19.35f, 0.01f, 238f), new FSpot(40.61f, 1.82f, 0.01f, 233f),
            new FSpot(37.47f, 9.00f, -3.99f, 165f), new FSpot(3.74f, 35.05f, 4.01f, 176f), new FSpot(12.82f, 21.33f, 4.01f, 312f),
        };
        public static readonly FSpot[] Candle =
        {
            new FSpot(8.08f, 19.16f, 0.01f, 116f), new FSpot(6.42f, 33.19f, 0.78f, 29f), new FSpot(30.24f, 29.86f, 0.01f, 275f),
            new FSpot(31.36f, 23.34f, 0.01f, 332f), new FSpot(30.84f, 5.83f, -3.99f, 184f),
        };
        public static readonly FSpot[] Friend =
        {
            new FSpot(36.15f, 6.56f, 0.01f, 304f), new FSpot(27.39f, 8.33f, 0.01f, 90f), new FSpot(5.75f, 9.01f, 0.01f, 48f),
        };
        public static readonly FSpot[] Tape =
        {
            new FSpot(30.86f, 17.60f, 0.97f, 271f), new FSpot(39.51f, 11.19f, 0.01f, 320f), new FSpot(48.39f, 22.13f, 0.01f, 94f),
            new FSpot(44.52f, 31.55f, 0.01f, 208f), new FSpot(57.81f, 25.81f, 0.01f, 249f),
        };
        public static readonly FSpot[] Washer =
        {
            new FSpot(41.15f, 18.55f, 0.01f, 298f), new FSpot(31.26f, 4.45f, 0.01f, 41f), new FSpot(6.62f, 38.96f, 0.01f, 144f),
        };
        public static readonly FSpot[] Speaker =
        {
            new FSpot(17.70f, 38.75f, 0.01f, 254f), new FSpot(20.41f, 5.54f, 0.01f, 118f), new FSpot(13.39f, 10.68f, 0.01f, 319f),
        };
        /// <summary>Stand in front of the wall: the box goes on the wall behind (x = 0), facing the way you faced.</summary>
        public static readonly FSpot[] FuseBox = { new FSpot(0.50f, 17.73f, 0.19f, 86f) };
        public static readonly FSpot[] Coin =
        {
            new FSpot(17.38f, 33.82f, 0.01f, 282f), new FSpot(23.13f, 29.00f, 0.01f, 317f), new FSpot(25.32f, 22.31f, 0.01f, 338f),
            new FSpot(25.47f, 16.22f, 0.01f, 7f), new FSpot(23.52f, 7.61f, 0.01f, 271f), new FSpot(2.90f, 27.21f, 4.01f, 257f),
            new FSpot(2.57f, 31.68f, 4.01f, 149f), new FSpot(14.79f, 26.77f, 4.01f, 194f), new FSpot(14.89f, 11.83f, 4.01f, 3f),
        };
        public static readonly FSpot[] Phone = { new FSpot(8.41f, 0.10f, 1.12f, 353f) };

        /// <summary>
        /// Where Mama can stand in the Facility: every spot the owner marked (they are all places a player can reach), put on its floor
        /// (ground, upstairs 4, basement -4) and facing the way the owner faced, with spots closer than 2 m to one already taken left out.
        /// Marks named 'Mama' will go here too when they exist.
        /// </summary>
        public static System.Collections.Generic.List<FSpot> MamaSpots()
        {
            var all = new System.Collections.Generic.List<FSpot>();
            FSpot[][] sets = { Vase, Candle, Friend, Tape, Washer, Speaker, Coin, Phone, FuseBox, CareSpots };
            foreach (FSpot[] set in sets)
                foreach (FSpot s in set)
                {
                    float floor = s.H >= 3f ? 4f : s.H <= -2f ? -4f : 0f;
                    var f = new FSpot(s.X, s.Z, floor + 0.01f, s.Yaw);
                    bool near = false;
                    foreach (FSpot t in all)
                        if (Mathf.Abs(t.H - f.H) < 1f && (t.X - f.X) * (t.X - f.X) + (t.Z - f.Z) * (t.Z - f.Z) < 4f) { near = true; break; }
                    if (!near) all.Add(f);
                }
            return all;
        }

        /// <summary>The care stations' spots (fridges, brushes, chargers), for Mama's spots.</summary>
        private static readonly FSpot[] CareSpots =
        {
            new FSpot(10.09f, 38.92f, 0.01f, 223f), new FSpot(33.11f, 29.53f, 0.01f, 275f), new FSpot(47.72f, 22.91f, 0.01f, 280f), new FSpot(0.58f, 39.13f, 4.01f, 156f),
            new FSpot(33.54f, 23.31f, 0.01f, 260f), new FSpot(6.73f, 13.06f, 0.01f, 70f), new FSpot(3.48f, 36.54f, 4.01f, 43f),
            new FSpot(30.89f, 8.76f, -3.99f, 178f), new FSpot(8.21f, 24.88f, 4.01f, 142f),
        };

        /// <summary>The world positions of a set of spots (what the house objectives keep their marked positions as).</summary>
        public static Vector3[] World(FSpot[] spots)
        {
            var list = new Vector3[spots.Length];
            for (int i = 0; i < spots.Length; i++) list[i] = spots[i].World;
            return list;
        }

        public static FSpot Pick(FSpot[] spots) { return spots[Random.Range(0, spots.Length)]; }

        /// <summary>A dim light over a prop that has to be found (the Facility is dark; lit models are black without one).</summary>
        public static void AddFinderLight(Transform parent)
        {
            var go = new GameObject("KE_FinderLight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 1.4f, 0.3f);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.range = 4f; l.intensity = 1.1f; l.color = new Color(0.85f, 0.92f, 1f); l.shadows = LightShadows.None;
        }

        /// <summary>
        /// Every washing machine, every speaker and the phone stand at their marked spots for the whole run, like the house's real props do
        /// (the objective only turns one of them on). Drawn in code, with a solid box so you cannot walk through them.
        /// </summary>
        public static void BuildSetDressing()
        {
            var root = new GameObject("KE_FacilitySetDressing").transform;
            for (int i = 0; i < Washer.Length; i++) Place(root, "Washing machine", Washer[i], 0);
            for (int i = 0; i < Speaker.Length; i++) Place(root, "Speaker", Speaker[i], 1);
            for (int i = 0; i < Phone.Length; i++) Place(root, "Phone", Phone[i], 2);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Facility] Set dressing placed: " + Washer.Length + " washing machines, " + Speaker.Length + " speakers, " + Phone.Length + " phone.");
        }

        // (a solid box: you cannot walk through them; the objective's own use box is a separate trigger)
        private static void Solid(GameObject go, Vector3 center, Vector3 size)
        {
            var b = go.AddComponent<BoxCollider>();
            b.center = center; b.size = size;
        }

        private static void Place(Transform root, string name, FSpot s, int kind)
        {
            var go = new GameObject("KE_" + name);
            go.transform.SetParent(root, false);
            go.transform.position = s.World;
            go.transform.rotation = Quaternion.Euler(0f, s.Yaw, 0f);
            if (kind == 0) { BuildWasherLook(go.transform); Solid(go, new Vector3(0f, 0.45f, 0f), new Vector3(0.7f, 0.9f, 0.7f)); }
            else if (kind == 1) { BuildSpeakerLook(go.transform); Solid(go, new Vector3(0f, 0.35f, 0f), new Vector3(0.36f, 0.7f, 0.3f)); }
            else BuildPhoneLook(go.transform);
        }

        /// <summary>
        /// A washing machine drawn in code (lit like the rest of the Facility, no glow): it does not
        /// depend on any light, and does not depend on copying a model from the house (the copies did not show up in the Facility).
        /// </summary>
        public static void BuildWasherLook(Transform parent)
        {
            Material white = Mat.Lit(new Color(0.82f, 0.84f, 0.86f), 0.5f), dark = Mat.Lit(new Color(0.1f, 0.12f, 0.16f), 0.8f), grey = Mat.Lit(new Color(0.5f, 0.52f, 0.56f), 0.6f);
            Part(parent, PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(0.7f, 0.9f, 0.7f), white);
            var door = Part(parent, PrimitiveType.Cylinder, new Vector3(0f, 0.45f, 0.36f), new Vector3(0.46f, 0.03f, 0.46f), dark);
            door.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part(parent, PrimitiveType.Cube, new Vector3(0f, 0.82f, 0.355f), new Vector3(0.6f, 0.1f, 0.03f), grey);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Facility] Washing machine drawn at " + parent.position + " (" + parent.GetComponentsInChildren<Renderer>().Length + " renderers).");
        }

        /// <summary>A speaker drawn the same way (a box with two cones).</summary>
        public static void BuildSpeakerLook(Transform parent)
        {
            Material body = Mat.Lit(new Color(0.24f, 0.24f, 0.28f), 0.4f), cone = Mat.Lit(new Color(0.5f, 0.5f, 0.56f), 0.3f);
            Part(parent, PrimitiveType.Cube, new Vector3(0f, 0.35f, 0f), new Vector3(0.36f, 0.7f, 0.3f), body);
            var w = Part(parent, PrimitiveType.Cylinder, new Vector3(0f, 0.2f, 0.16f), new Vector3(0.24f, 0.02f, 0.24f), cone); w.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var t = Part(parent, PrimitiveType.Cylinder, new Vector3(0f, 0.52f, 0.16f), new Vector3(0.12f, 0.02f, 0.12f), cone); t.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Facility] Speaker drawn at " + parent.position + " (" + parent.GetComponentsInChildren<Renderer>().Length + " renderers).");
        }

        /// <summary>A phone drawn the same way (a base and a handset).</summary>
        public static void BuildPhoneLook(Transform parent)
        {
            Material body = Mat.Lit(new Color(0.2f, 0.2f, 0.24f), 0.5f), hand = Mat.Lit(new Color(0.4f, 0.4f, 0.46f), 0.5f);
            Part(parent, PrimitiveType.Cube, new Vector3(0f, 0.05f, 0f), new Vector3(0.26f, 0.1f, 0.22f), body);
            Part(parent, PrimitiveType.Cube, new Vector3(0f, 0.12f, 0f), new Vector3(0.26f, 0.04f, 0.08f), hand);
        }


        private static GameObject Part(Transform parent, PrimitiveType t, Vector3 pos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(t);
            var c = go.GetComponent<Collider>();
            if (c != null) Object.Destroy(c); // (the objective's own trigger is the only collider)
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }
    }

    /// <summary>
    /// Starts the real Endless run in the Facility once Tattletail and the flashlight exist (like the first door does in the house).
    /// </summary>
    public class FacilityRunStarter : MonoBehaviour
    {
        private float _wait;
        private bool _started;

        private void Update()
        {
            if (_started) return;
            _wait += Time.deltaTime;
            bool ready = RM.tattletail != null && RM.flashlight != null && RM.hud != null && RM.fpsController != null;
            if (!ready && _wait < 20f) return;
            if (_wait < 3f) return; // a few seconds to look around first
            _started = true;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Factory] Endless preview: starting the real Endless run.");
            FactoryLayout.MakeStationsInteractive();
            FacilityMarks.BuildSetDressing();
            FacilityMama.Prepare();
            gameObject.AddComponent<FacilityMamaWatch>();
            gameObject.AddComponent<FacilityArrow>(); // the arrow to the nearest objective
            SceneMonitor.StartFacilityRun();
            Destroy(this);
        }
    }
}

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Mama in the Facility: the game's own Mama AI on the Facility's own patrol nodes (60 dark corners and doorways all over the building,
    /// feet on the floor), the way the Night Shift story ran her before it was shelved (the nodes and the way she was set up come from that code).
    /// </summary>
    public static class FacilityMama
    {
        public static PatrolNodeSet Nodes;
        private const float UP = 4f, DOWN = -4f;
        private static GameObject _setGo;

        private static void Node(float x, float y, float z, float yaw)
        {
            var go = new GameObject("Mama node");
            go.transform.SetParent(_setGo.transform, false);
            go.transform.position = FactoryController.Origin + new Vector3(x, y + 0.02f, z);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<PatrolNode>();
        }

        /// <summary>Builds the Facility's patrol node set (once per run).</summary>
        public static void Prepare()
        {
            if (_setGo != null) Object.Destroy(_setGo);
            _setGo = new GameObject("Mama patrol nodes");
            Nodes = _setGo.AddComponent<PatrolNodeSet>();
            Nodes.patrolNodes = new System.Collections.Generic.List<PatrolNode>();
            // Ground floor.
            Node(1.2f, 0f, 1.5f, 45f); Node(12.8f, 0f, 18.5f, 225f); Node(1f, 0f, 12f, 90f);                   // lobby
            Node(12.5f, 0f, 21f, 200f); Node(4.5f, 0f, 21f, 90f);                                             // hall, stairwell
            Node(1f, 0f, 30f, 60f); Node(12.8f, 0f, 38f, 225f);                                                // break room
            Node(15.5f, 0f, 17f, 90f); Node(15.5f, 0f, 37f, 135f); Node(40.5f, 0f, 36.5f, 225f);              // assembly corners
            Node(26f, 0f, 16f, 0f); Node(40.5f, 0f, 16.5f, 270f); Node(28f, 0f, 35f, 180f); Node(21f, 0f, 30f, 90f);
            Node(15f, 0f, 1f, 45f); Node(29f, 0f, 8.5f, 225f);                                                 // testing lab
            Node(31f, 0f, 8.5f, 135f); Node(41f, 0f, 5f, 270f);                                                // the way to R&D
            Node(43.5f, 0f, 3f, 45f); Node(43.5f, 0f, 38.5f, 135f); Node(58.8f, 0f, 38.5f, 225f);             // warehouse
            Node(58.8f, 0f, 2f, 315f); Node(51f, 0f, 11.4f, 90f); Node(51f, 0f, 31f, 90f); Node(46f, 0f, 16f, 60f);
            Node(10f, 0f, -6f, 180f); Node(30f, 0f, -6f, 180f);                                                // out front, in the dark
            // Upstairs.
            Node(12.5f, UP, 21f, 200f); Node(7f, UP, 27f, 180f);                                              // hall
            Node(1f, UP, 39f, 135f); Node(6f, UP, 29f, 315f);                                                 // foreman
            Node(13f, UP, 39f, 225f); Node(8f, UP, 29f, 45f);                                                  // records
            Node(1f, UP, 9f, 45f); Node(8.3f, UP, 19f, 225f);                                                  // manager
            Node(13f, UP, 9f, 315f);                                                                           // PA room
            Node(1f, UP, 1f, 45f); Node(13f, UP, 7f, 225f);                                                    // conference
            Node(15f, UP, 30f, 90f); Node(40f, UP, 39f, 180f);                                                 // the catwalk
            // Basement.
            Node(5f, DOWN, 25f, 180f); Node(20f, DOWN, 19.6f, 270f); Node(29f, DOWN, 19.6f, 270f);           // stairs, corridor
            Node(1f, DOWN, 9f, 45f); Node(11f, DOWN, 17.5f, 225f);                                             // generator room
            Node(13f, DOWN, 9f, 45f); Node(23f, DOWN, 17.5f, 225f);                                            // boiler room
            Node(1f, DOWN, 1f, 45f); Node(23f, DOWN, 7f, 225f);                                                // storage
            Node(31f, DOWN, 13f, 0f); Node(31f, DOWN, 23f, 180f);                                              // the pit's ledge
            Node(25f, DOWN, 11f, 135f); Node(41f, DOWN, 1f, 315f); Node(39.5f, DOWN, 9f, 180f);               // R&D (one is the chamber)
            Nodes.RegisterNodes();
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [FacilityMama] " + Nodes.patrolNodes.Count + " patrol nodes built.");
        }

        private static readonly System.Reflection.FieldInfo NodeSetField = typeof(MamaManager).GetField("currentPatrolNodeSet", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        private static readonly System.Reflection.FieldInfo FreezeField = typeof(MamaManager).GetField("freezeStateSetting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        /// <summary>Called between InitializeNewMama and ActivateMama: she walks the Facility's nodes (not the house's).</summary>
        public static void UseNodes(MamaManager mm)
        {
            if (mm == null || Nodes == null) return;
            if ((object)NodeSetField != null) NodeSetField.SetValue(mm, Nodes);
            Nodes.RegisterNodes();
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [FacilityMama] Mama walks " + Nodes.patrolNodes.Count + " Facility nodes.");
        }

        /// <summary>Before she is set up: nothing frozen.</summary>
        public static void Unfreeze(MamaManager mm)
        {
            if (mm != null && (object)FreezeField != null) FreezeField.SetValue(mm, false);
        }
    }
}

namespace KaleidoscopeEndless
{
    /// <summary>Logs where Mama is every few seconds while she is out in the Facility (so a Mama nobody sees can be found).</summary>
    public class FacilityMamaWatch : MonoBehaviour
    {
        private float _next;
        private void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + 4f;
            if (!FactoryMode.EndlessPreview || MamaController.Instance == null) return;
            GameObject m = MamaController.Instance.GetActiveMama();
            if (m == null || RM.fpsController == null) return;
            Vector3 p = m.transform.position;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [FacilityMama] Mama at " + p + ", " + Vector3.Distance(p, RM.fpsController.transform.position).ToString("F1") + " m from you.");
        }
    }
}
