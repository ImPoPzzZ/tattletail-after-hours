using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// The Waygetter factory's ground floor (step 1: the rooms Night 1 uses). Place-local meters:
    /// x runs west to east 0..60, z south to north 0..40, the floor is y 0.
    ///
    ///   west block (x 0-14):  lobby (z 0-20), hall + stairwell (z 20-27), break room (z 27-40); drop ceiling at 3.2
    ///   assembly floor:       x 14-42, z 10-40, open to the roof at 8
    ///   testing lab / R&amp;D:    x 14-30 / 30-42, z 0-10, drop ceiling
    ///   warehouse:            x 42-60, z 0-40, open to the roof, two loading docks on the east wall
    ///
    /// 2026: the factory has been abandoned for decades and you break in at night. The power is out
    /// (only the R&amp;D keypad still glows, and one tube stutters), the roof has caved in over the line,
    /// and everything is ruined. You arrive in the parking lot (z below 0) by car; the flashlight is in
    /// the trunk.
    /// </summary>
    public static partial class FactoryLayout
    {
        public const float W = 60f, D = 40f;               // the building
        public const float LOW = 3.2f, HIGH = 8f;          // drop ceilings / the roof
        private const float T = 0.25f;                     // wall thickness
        private const float DOOR_TOP = 2.3f;
        private static readonly float[] None = new float[0]; // an empty params call compiles to Array.Empty, which this Mono doesn't have
        public static readonly Vector3 SpawnLocal = new Vector3(10.4f, 0.1f, -13.2f); // beside your car in the parking lot
        public const float SPAWN_YAW = -25f;                                           // looking at the car and the dark factory
        public const float LOT_X0 = -12f, LOT_X1 = 62f, LOT_Z0 = -26f;                // the fenced parking lot

        /// <summary>The flashlight in the car's trunk, the trunk lid's hinge and the lid itself (FactoryController opens and takes).</summary>
        public static GameObject FlashlightProp, TrunkLid, TrunkTattletail;
        public static Transform TrunkHinge;

        /// <summary>Night 1's things to use: the answering machine, the lockers (one opens, with the battery), the accident board and the file behind it.</summary>
        public static GameObject AnsweringMachine, OpenLocker, BatteryProp, AccidentBoard, FileProp;
        public static readonly List<GameObject> Lockers = new List<GameObject>();
        public static Transform LockerHinge;
        public static Vector3 AccidentBoardHome;

        /// <summary>
        /// Set while Recalled builds this as its town's warehouse: the town is the ground around it (so its own
        /// is left out), nobody drove here (no car), the care stations and camcorder batteries stay home, and
        /// the lot's fence is open opposite the town's gate (x TOWN_GATE_X0..X1 on its south side).
        /// </summary>
        public static bool Town;
        public const float TOWN_GATE_X0 = RcTown.FAC_GATE_X0, TOWN_GATE_X1 = RcTown.FAC_GATE_X1;

        private static Transform _root;
        private static FMat _concrete, _block, _blockCream, _paintBand, _lobbyTile, _ceiling, _steel, _roof, _plate, _green, _greenDark, _hazard, _danger;
        private static FMat _darkwood, _wood, _chip, _cardboard, _cork, _fabric;
        private static FMat _rubber, _orange, _yellowPaint, _dark, _metalGrey, _beige, _white, _red, _redPaint, _blue, _tubeOn, _tubeOff, _paper;
        private static FMat _asphalt, _dirt, _water, _void, _shard, _weed, _lineFaded, _carPaint, _carGlass, _headlight, _taillight;

        private class Flicker { public Light L; public Renderer Tube; public float Base; public float Next; public bool Off; public int Burst; }
        private static readonly List<Flicker> _flickers = new List<Flicker>();
        private static System.Random _rng;

        public static void Build(Transform root)
        {
            _root = root;
            _flickers.Clear();
            Lockers.Clear();
            _rng = new System.Random(1998);
            Materials();
            Exterior();
            Shell();
            WestBlock();
            Lobby();
            BreakRoom();
            Assembly();
            SouthRooms();
            Warehouse();
            Ruin();
            Floors();
            if (!Town) { CareStations(); CamBatteryPickups(); }
            Signage();
            Lights();
            Camera cam = FactoryController.PlayerCam;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Factory] Ground floor built. Rendering path: " + (cam != null ? cam.actualRenderingPath.ToString() : "?"));
        }

        private static void Materials()
        {
            _concrete   = FTex.Get("concrete", 3.5f, new Color(0.52f, 0.5f, 0.46f), 0.22f, 0f, 1.2f);
            _block      = FTex.Get("block", 2.4f, new Color(0.56f, 0.54f, 0.49f), 0.08f, 0f, 1.4f);    // decades of grime
            _blockCream = FTex.Get("block", 2.4f, new Color(0.66f, 0.58f, 0.44f), 0.08f, 0f, 1.4f);    // the lobby's old cream paint, yellowed
            _paintBand  = FTex.Get("block", 2.4f, new Color(0.3f, 0.42f, 0.36f), 0.15f, 0f, 1.3f);     // the painted band low on factory walls
            _lobbyTile  = FTex.Get("lobby", 1.6f, new Color(0.58f, 0.56f, 0.52f), 0.3f, 0f, 0.8f);
            _ceiling    = FTex.Get("ceiling", 2.4f, new Color(0.6f, 0.57f, 0.5f), 0.03f, 0f, 1f);
            _steel      = FTex.Get("steel", 2.5f, new Color(0.6f, 0.62f, 0.64f), 0.35f, 0.4f, 1f);
            _roof       = FTex.Get("steel", 3.5f, new Color(0.3f, 0.31f, 0.33f), 0.25f, 0.4f, 1f);
            _plate      = FTex.Get("plate", 1.2f, new Color(0.7f, 0.71f, 0.73f), 0.55f, 0.7f, 1f);
            _green      = FTex.Get("green", 1.5f, Color.white, 0.35f, 0.2f, 1f);
            _greenDark  = FTex.Get("green", 1.5f, new Color(0.55f, 0.6f, 0.58f), 0.3f, 0.3f, 1f);
            _hazard     = FTex.Get("hazard", 1f, Color.white, 0.3f, 0.2f, 1f);
            _danger     = FTex.Get("danger", 0f, Color.white, 0.3f, 0.2f, 0.6f);
            _darkwood   = FTex.Get("darkwood", 1.2f, Color.white, 0.45f, 0f, 1f);
            _wood       = FTex.Get("wood", 1.2f, new Color(0.9f, 0.85f, 0.8f), 0.4f, 0f, 1f);
            _chip       = FTex.Get("chip", 1.2f, Color.white, 0.1f, 0f, 1f);
            _cardboard  = FTex.Get("cardboard", 0.8f, new Color(0.95f, 0.85f, 0.7f), 0.08f, 0f, 1f);
            _cork       = FTex.Get("cork", 0.8f, Color.white, 0.05f, 0f, 1f);
            _fabric     = FTex.Get("fabric", 0.6f, new Color(0.35f, 0.4f, 0.55f), 0.05f, 0f, 1f);
            _rubber     = FTex.Plain(new Color(0.07f, 0.07f, 0.075f), 0.35f);
            _orange     = FTex.Plain(new Color(0.8f, 0.34f, 0.08f), 0.45f);
            _yellowPaint = FTex.Plain(new Color(0.9f, 0.74f, 0.12f), 0.25f);
            _dark       = FTex.Plain(new Color(0.1f, 0.1f, 0.11f), 0.4f);
            _metalGrey  = FTex.Plain(new Color(0.42f, 0.43f, 0.45f), 0.6f);
            _beige      = FTex.Plain(new Color(0.78f, 0.74f, 0.64f), 0.3f);  // 90s computer plastic
            _white      = FTex.Plain(new Color(0.85f, 0.85f, 0.82f), 0.4f);
            _red        = FTex.Glow(new Color(1f, 0.12f, 0.08f));
            _redPaint   = FTex.Plain(new Color(0.62f, 0.07f, 0.06f), 0.5f);
            _blue       = FTex.Plain(new Color(0.12f, 0.25f, 0.55f), 0.5f);
            _tubeOn     = FTex.Glow(new Color(0.92f, 0.97f, 1f));
            _tubeOff    = FTex.Plain(new Color(0.5f, 0.52f, 0.53f), 0.6f);
            _paper      = FTex.Plain(new Color(0.72f, 0.68f, 0.58f), 0.05f);
            _asphalt    = FTex.Get("asphalt", 4f, new Color(0.62f, 0.62f, 0.62f), 0.2f, 0f, 1.2f);
            _dirt       = FTex.Get("dirt", 4f, new Color(0.6f, 0.62f, 0.55f), 0.1f, 0f, 1f);
            _water      = FTex.Plain(new Color(0.03f, 0.035f, 0.04f), 0.97f);
            _void       = FTex.Plain(new Color(0.01f, 0.01f, 0.01f), 0f);
            _shard      = FTex.Plain(new Color(0.3f, 0.36f, 0.38f), 0.98f);
            _weed       = FTex.Plain(new Color(0.16f, 0.22f, 0.1f), 0.1f);
            _lineFaded  = FTex.Plain(new Color(0.55f, 0.5f, 0.3f), 0.1f);
            _carPaint   = FTex.Plain(new Color(0.12f, 0.16f, 0.24f), 0.8f);
            _carGlass   = FTex.Plain(new Color(0.02f, 0.025f, 0.03f), 0.97f);
            _headlight  = FTex.Glow(new Color(1f, 0.95f, 0.8f));
            _taillight  = FTex.Glow(new Color(0.55f, 0.03f, 0.02f));
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private static GameObject Box(string name, float x0, float y0, float z0, float x1, float y1, float z1, FMat m, bool collide)
        {
            return FGeo.Box(_root, name, new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, y1 - y0, z1 - z0), m, collide);
        }

        // An upright cylinder standing on y0 (barrels, pots, bins); rotated for lying-down ones.
        private static GameObject Cyl(string name, float x, float y0, float z, float radius, float height, FMat m, bool collide)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(_root, false);
            go.transform.localPosition = new Vector3(x, y0 + height * 0.5f, z);
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            go.GetComponent<Renderer>().sharedMaterial = m.Mat;
            if (!collide) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        private static GameObject Ball(string name, Vector3 pos, Vector3 size, FMat m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(_root, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m.Mat;
            Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        // Text on a wall. yaw 0 reads from the south (a north wall), 180 from the north, 90 from the west (an east wall), -90 from the east.
        private static void Sign(string text, float x, float y, float z, float yaw, float lineHeight, float width, Color color)
        {
            var t = WorldText.Create(_root, text, color, lineHeight, TextAnchor.MiddleCenter, width);
            t.canvas.transform.localPosition = new Vector3(x, y, z);
            t.canvas.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // A wall along x (at z), from y0 to y1, with openings given as (from, to, top) triples.
        private static void WallX(string name, float z, float x0, float x1, float y0, float y1, FMat m, params float[] gaps)
        {
            float x = x0;
            for (int i = 0; i + 2 < gaps.Length; i += 3)
            {
                float a = gaps[i], b = gaps[i + 1], top = gaps[i + 2];
                if (top <= y0) continue; // this opening doesn't reach this band
                if (a > x) Box(name, x, y0, z - T / 2, a, y1, z + T / 2, m, true);
                if (top < y1) Box(name + " (over door)", a, top, z - T / 2, b, y1, z + T / 2, m, true);
                x = b;
            }
            if (x < x1) Box(name, x, y0, z - T / 2, x1, y1, z + T / 2, m, true);
        }

        // A wall along z (at x).
        private static void WallZ(string name, float x, float z0, float z1, float y0, float y1, FMat m, params float[] gaps)
        {
            float z = z0;
            for (int i = 0; i + 2 < gaps.Length; i += 3)
            {
                float a = gaps[i], b = gaps[i + 1], top = gaps[i + 2];
                if (top <= y0) continue;
                if (a > z) Box(name, x - T / 2, y0, z, x + T / 2, y1, a, m, true);
                if (top < y1) Box(name + " (over door)", x - T / 2, top, a, x + T / 2, y1, b, m, true);
                z = b;
            }
            if (z < z1) Box(name, x - T / 2, y0, z, x + T / 2, y1, z1, m, true);
        }

        // A closed door filling an opening. Its frame sticks out past the wall on both sides and the
        // panel stops just under the lintel, so no two faces share a plane (they flickered before).
        private static void Door(string name, bool alongX, float at, float a, float b, float top, FMat panel)
        {
            const float F = 0.2f; // frame depth each side (the wall is 0.125)
            if (alongX)
            {
                Box(name, a + 0.01f, 0f, at - 0.04f, b - 0.01f, top - 0.02f, at + 0.04f, panel, true);
                Box(name + " frame", a - 0.08f, 0f, at - F, a, top + 0.08f, at + F, _dark, false);
                Box(name + " frame", b, 0f, at - F, b + 0.08f, top + 0.08f, at + F, _dark, false);
                Box(name + " frame", a - 0.08f, top, at - F, b + 0.08f, top + 0.08f, at + F, _dark, false);
                Box(name + " handle", b - 0.2f, 1.0f, at - 0.1f, b - 0.12f, 1.07f, at + 0.1f, _metalGrey, false);
            }
            else
            {
                Box(name, at - 0.04f, 0f, a + 0.01f, at + 0.04f, top - 0.02f, b - 0.01f, panel, true);
                Box(name + " frame", at - F, 0f, a - 0.08f, at + F, top + 0.08f, a, _dark, false);
                Box(name + " frame", at - F, 0f, b, at + F, top + 0.08f, b + 0.08f, _dark, false);
                Box(name + " frame", at - F, top, a - 0.08f, at + F, top + 0.08f, b + 0.08f, _dark, false);
                Box(name + " handle", at - 0.1f, 1.0f, b - 0.2f, at + 0.1f, 1.07f, b - 0.12f, _metalGrey, false);
            }
        }

        // An open door: the frame, and the panel swung 90 degrees on its hinge at 'a' (into the -z / -x side).
        private static void OpenDoor(string name, bool alongX, float at, float a, float b, float top, FMat panel)
        {
            const float F = 0.2f;
            float w = b - a - 0.04f;
            if (alongX)
            {
                Box(name + " frame", a - 0.08f, 0f, at - F, a, top + 0.08f, at + F, _dark, false);
                Box(name + " frame", b, 0f, at - F, b + 0.08f, top + 0.08f, at + F, _dark, false);
                Box(name + " frame", a - 0.08f, top, at - F, b + 0.08f, top + 0.08f, at + F, _dark, false);
                Box(name + " (open)", a + 0.01f, 0f, at - F - w, a + 0.05f, top - 0.02f, at - F, panel, true);
            }
            else
            {
                Box(name + " frame", at - F, 0f, a - 0.08f, at + F, top + 0.08f, a, _dark, false);
                Box(name + " frame", at - F, 0f, b, at + F, top + 0.08f, b + 0.08f, _dark, false);
                Box(name + " frame", at - F, top, a - 0.08f, at + F, top + 0.08f, b + 0.08f, _dark, false);
                Box(name + " (open)", at - F - w, 0f, a + 0.01f, at - F, top - 0.02f, a + 0.05f, panel, true);
            }
        }

        // ── The building ─────────────────────────────────────────────────────
        private static void Shell()
        {
            // Floors: tile in the west block, concrete everywhere else.
            Box("Floor (west block)", 0f, -0.2f, 0f, 2.1f, 0f, D, _lobbyTile, true);
            Box("Floor (west block)", 3.5f, -0.2f, 0f, 14f, 0f, D, _lobbyTile, true);
            Box("Floor (west block)", 2.1f, -0.2f, 0f, 3.5f, 0f, 21.3f, _lobbyTile, true);
            Box("Floor (west block)", 2.1f, -0.2f, 26.5f, 3.5f, 0f, D, _lobbyTile, true);   // (the stairs down go through x 2.1-3.5, z 21.3-26.5)
            Box("Floor (factory)", 14f, -0.2f, 0f, 31.5f, 0f, D, _concrete, true);
            Box("Floor (factory)", 35.2f, -0.2f, 0f, W, 0f, D, _concrete, true);
            Box("Floor (factory)", 31.5f, -0.2f, 0f, 35.2f, 0f, 0.3f, _concrete, true);
            Box("Floor (factory)", 31.5f, -0.2f, 1.7f, 35.2f, 0f, D, _concrete, true);          // (the R&D stairs go through x 31.5-35.2, z 0.3-1.7)

            // Outer walls: painted block up to 3, corrugated steel above in the tall halls.
            WallX("South wall", 0f, 0f, W, 0f, 1.1f, _block, 5.5f, 7.5f, DOOR_TOP);
            WallX("South wall", 0f, 0f, W, 1.1f, 2.3f, _block, 5.5f, 7.5f, DOOR_TOP, 9.5f, 12.5f, 2.3f);
            WallX("South wall", 0f, 0f, W, 2.3f, HIGH, _block, None);
            BrokenFrontDoor();
            BoardedWindow(9.5f, 12.5f);
            WallX("North wall", D, 0f, 14f, 0f, HIGH, _block, None);
            WallX("North wall", D, 14f, W, 0f, 3f, _block, None);
            WallX("North wall (upper)", D, 14f, W, 3f, HIGH, _steel, None);
            WallZ("West wall", 0f, 0f, D, 0f, HIGH, _block, None);
            WallZ("East wall", W, 0f, D, 0f, 3f, _block, 17f, 20.5f, 3.6f, 22f, 25.5f, 3.6f);
            WallZ("East wall (upper)", W, 0f, D, 3f, HIGH, _steel, 17f, 20.5f, 3.6f, 22f, 25.5f, 3.6f);

            // The painted band low on the factory walls (a thin skin, just proud of the wall).
            Box("Paint band", 14.13f, 0f, 10.13f, 14.15f, 1.2f, 12.95f, _paintBand, false);
            Box("Paint band", 14.13f, 0f, 15.05f, 14.15f, 1.2f, 33.45f, _paintBand, false);
            Box("Paint band", 14.13f, 0f, 35.55f, 14.15f, 1.2f, 39.87f, _paintBand, false);
            Box("Paint band", 59.85f, 0f, 20.7f, 59.87f, 1.2f, 21.8f, _paintBand, false);
            Box("Paint band", 14.13f, 0f, 39.85f, 59.87f, 1.2f, 39.87f, _paintBand, false);
            Box("Paint band", 59.85f, 0f, 0.13f, 59.87f, 1.2f, 16.8f, _paintBand, false);
            Box("Paint band", 59.85f, 0f, 25.7f, 59.87f, 1.2f, 39.87f, _paintBand, false);
            Box("Paint band", 42.13f, 0f, 0.13f, 59.87f, 1.2f, 0.15f, _paintBand, false);

            // The roof, and its trusses over the two tall halls.
            // The roof, with two holes where it caved in (over the line, and at the back of the warehouse).
            Box("Roof", 0f, HIGH, 0f, 26f, HIGH + 0.2f, D, _roof, true);
            Box("Roof", 26f, HIGH, 0f, 31f, HIGH + 0.2f, 24f, _roof, true);
            Box("Roof", 26f, HIGH, 29f, 31f, HIGH + 0.2f, D, _roof, true);
            Box("Roof", 31f, HIGH, 0f, 50f, HIGH + 0.2f, D, _roof, true);
            Box("Roof", 50f, HIGH, 0f, 53f, HIGH + 0.2f, 30f, _roof, true);
            Box("Roof", 50f, HIGH, 34f, 53f, HIGH + 0.2f, D, _roof, true);
            Box("Roof", 53f, HIGH, 0f, W, HIGH + 0.2f, D, _roof, true);
            HangingSheet(27f, 24f, 1.8f, 55f);
            HangingSheet(30f, 29f, 1.5f, -60f);
            HangingSheet(51.5f, 30f, 1.2f, 50f);
            for (float x = 17f; x < W; x += 6f)
            {
                float z0 = x < 42f ? 10f : 0f;
                Box("Truss", x - 0.12f, HIGH - 0.7f, z0, x + 0.12f, HIGH, D, _dark, false);
                Box("Truss flange", x - 0.2f, HIGH - 0.74f, z0, x + 0.2f, HIGH - 0.68f, D, _dark, false);
            }
            for (float z = 13f; z < D; z += 9f) Box("Purlin", 14f, HIGH - 0.35f, z - 0.08f, W, HIGH - 0.2f, z + 0.08f, _dark, false);
            // Pipes and a duct running along the ceiling.
            Box("Duct", 14f, 6.6f, 38.6f, 42f, 7.2f, 39.2f, _metalGrey, false);
            Box("Pipe", 14f, 6.9f, 12.2f, 60f, 7.02f, 12.32f, _redPaint, false);
            Box("Pipe", 14f, 6.9f, 12.6f, 60f, 7.02f, 12.72f, _metalGrey, false);
        }

        private static void WestBlock()
        {
            // The wall between the west block and the assembly floor: doors from the lobby and the break room.
            WallZ("Assembly west wall", 14f, 0f, D, 0f, UP, _block, 13f, 15f, DOOR_TOP, 33.5f, 35.5f, DOOR_TOP);
            WallZ("Assembly west wall (upstairs)", 14f, 0f, D, UP, HIGH, _block, 24f, 25.2f, UP + DOOR_TOP); // the door out to the catwalk
            WallX("Lobby north wall", 20f, 0f, 14f, 0f, LOW, _blockCream, 9.5f, 11.5f, DOOR_TOP);
            WallZ("Stairwell wall", 6f, 20f, 27f, 0f, LOW, _block, 22.6f, 23.8f, DOOR_TOP);
            OpenDoor("Stairwell door", false, 6f, 22.6f, 23.8f, DOOR_TOP - 0.1f, _greenDark);
            Sign("STAIRS", 6.21f, 2.45f, 23.2f, -90f, 0.14f, 1f, new Color(0.9f, 0.9f, 0.85f));
            WallX("Break room south wall", 27f, 0f, 14f, 0f, LOW, _block, 9.5f, 11.5f, DOOR_TOP);
            Box("Drop ceiling (west block)", 0f, LOW, 0f, 14f, LOW + 0.08f, 20f, _ceiling, true);
            Box("Drop ceiling (west block)", 6f, LOW, 20f, 14f, LOW + 0.08f, 27f, _ceiling, true);
            Box("Drop ceiling (west block)", 0f, LOW, 27f, 14f, LOW + 0.08f, D, _ceiling, true);   // (the stairwell is open to the top)

            // Hall: a fire extinguisher and a water fountain.
            Box("Fire extinguisher bracket", 13.8f, 1f, 24.3f, 13.87f, 1.1f, 24.5f, _dark, false);
            Cyl("Fire extinguisher", 13.72f, 0.55f, 24.4f, 0.08f, 0.5f, _redPaint, false);
            Box("Water fountain", 6.13f, 0.75f, 25.3f, 6.5f, 1f, 25.8f, _metalGrey, true);
        }

        private static void Lobby()
        {
            // The cream paint is a thin skin on the inside of the outer walls (clear of the door frame).
            Box("Lobby paint (west)", 0.126f, 0f, 0.126f, 0.14f, LOW, 20f, _blockCream, false);
            Box("Lobby paint (south)", 0.126f, 0f, 0.126f, 5.3f, LOW, 0.14f, _blockCream, false);
            Box("Lobby paint (south)", 7.7f, 0f, 0.126f, 9.3f, LOW, 0.14f, _blockCream, false);
            Box("Lobby paint (south)", 9.3f, 0f, 0.126f, 12.7f, 1.1f, 0.14f, _blockCream, false);
            Box("Lobby paint (south)", 9.3f, 2.3f, 0.126f, 12.7f, LOW, 0.14f, _blockCream, false);
            Box("Lobby paint (south)", 12.7f, 0f, 0.126f, 13.874f, LOW, 0.14f, _blockCream, false);
            Box("Lobby paint (east)", 13.86f, 0f, 0.126f, 13.874f, LOW, 12.8f, _blockCream, false);
            Box("Lobby paint (east)", 13.86f, 0f, 15.2f, 13.874f, LOW, 19.87f, _blockCream, false);
            Box("Rug", 5f, 0f, 0.6f, 8f, 0.01f, 2.6f, FTex.Plain(new Color(0.22f, 0.08f, 0.09f), 0.02f), false);

            // Security desk (dark wood), a beige computer, the answering machine and the flashlight.
            Box("Security desk", 9.2f, 0f, 5.6f, 13.2f, 1.05f, 6.4f, _darkwood, true);
            Box("Security desk (side)", 12.4f, 0f, 6.4f, 13.2f, 1.05f, 9f, _darkwood, true);
            Box("Security desk top", 9.1f, 1.05f, 5.5f, 13.3f, 1.1f, 6.5f, _wood, false);
            Box("Security desk top (side)", 12.3f, 1.05f, 6.5f, 13.3f, 1.1f, 9.1f, _wood, false);
            Box("Monitor", 11.7f, 1.1f, 6.05f, 12.15f, 1.48f, 6.45f, _beige, false);
            Box("Monitor back", 11.78f, 1.14f, 6.45f, 12.07f, 1.42f, 6.62f, _beige, false);
            Box("Monitor screen", 11.76f, 1.16f, 6.04f, 12.09f, 1.42f, 6.05f, FTex.Plain(new Color(0.04f, 0.06f, 0.05f), 0.95f), false);
            Box("Keyboard", 11.6f, 1.1f, 5.62f, 12.25f, 1.13f, 5.85f, _beige, false);
            AnsweringMachine = Box("Answering machine", 10.2f, 1.1f, 5.8f, 10.55f, 1.18f, 6.15f, _dark, true);
            Box("Answering machine light", 10.47f, 1.18f, 5.84f, 10.52f, 1.2f, 5.89f, _red, false);
            Box("Paper stack", 9.5f, 1.1f, 5.7f, 9.8f, 1.14f, 6.1f, _paper, false);
            Chair(10.4f, 7.6f, 30f, true);
            Cyl("Trash can", 9.6f, 0f, 7f, 0.16f, 0.4f, _metalGrey, true).transform.localRotation = Quaternion.Euler(0f, 0f, 88f);

            // Time clock and the punch card rack by the door.
            Box("Time clock", 0.14f, 1.2f, 3.6f, 0.44f, 1.65f, 4.0f, _green, false);
            Box("Time clock face", 0.44f, 1.45f, 3.7f, 0.45f, 1.6f, 3.9f, _white, false);
            Box("Punch card rack", 0.14f, 1.1f, 4.3f, 0.24f, 1.8f, 4.9f, _darkwood, false);
            for (int i = 0; i < 6; i++) Box("Punch card", 0.24f, 1.18f + i * 0.1f, 4.4f + (i % 3) * 0.14f, 0.25f, 1.26f + i * 0.1f, 4.5f + (i % 3) * 0.14f, _paper, false);

            // Waiting chairs, plants, a bulletin board, the clock and the signs.
            Chair(0.55f, 9.5f, -90f); Chair(1.4f, 10.6f, -60f, true); Chair(0.55f, 11f, -90f);
            Plant(0.7f, 18.9f);
            Plant(13.2f, 1f);
            Box("Bulletin board", 1.5f, 1.2f, 19.85f, 4.5f, 2.2f, 19.87f, _cork, false);
            for (int i = 0; i < 7; i++)
            {
                float px = 1.7f + (float)_rng.NextDouble() * 2.4f, py = 1.3f + (float)_rng.NextDouble() * 0.6f;
                Box("Note", px, py, 19.84f, px + 0.2f, py + 0.27f, 19.85f, _paper, false);
            }
            Cyl("Wall clock", 13.84f, 2.35f, 7.8f, 0.17f, 0.04f, _white, false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Sign("WAYGET ER  ELECTR NICS", 7f, 2.75f, 19.85f, 0f, 0.28f, 6f, new Color(0.24f, 0.14f, 0.28f));
            Sign("Home of Tattletail!", 7f, 2.4f, 19.85f, 0f, 0.16f, 6f, new Color(0.26f, 0.2f, 0.14f));
            BuildAccidentBoard();
            Box("Exit sign", 6.1f, 2.45f, 0.13f, 6.9f, 2.7f, 0.2f, _dark, false);
            Sign("EXIT", 6.5f, 2.575f, 0.21f, 180f, 0.18f, 0.8f, new Color(0.3f, 0.05f, 0.04f)); // no power

            // The display case: a Tattletail on show, like in a toy store.
            Box("Display pedestal", 6.1f, 0f, 14.6f, 7.9f, 0.85f, 15.4f, _wood, true);
            Box("Display top", 6.1f, 1.75f, 14.6f, 7.9f, 1.8f, 15.4f, _wood, false);
            for (int i = 0; i < 4; i++) Box("Display post", (i % 2 == 0) ? 6.1f : 7.86f, 0.85f, (i < 2) ? 14.6f : 15.36f, (i % 2 == 0) ? 6.14f : 7.9f, 1.75f, (i < 2) ? 14.64f : 15.4f, _metalGrey, false);
            Tattletail(new Vector3(7f, 0.86f, 15f), 180f, 0);
        }

        private static void BreakRoom()
        {
            for (int i = 0; i < 7; i++)
            {
                float x = 0.6f + i * 0.58f;
                if (i == 4) { BuildOpenLocker(x); continue; } // the only one that isn't rusted shut
                Lockers.Add(Box("Locker", x, 0f, 39.35f, x + 0.55f, 1.9f, 39.86f, _green, true));
                Box("Locker vents", x + 0.12f, 1.55f, 39.34f, x + 0.43f, 1.75f, 39.35f, _dark, false);
                Box("Locker handle", x + 0.42f, 0.95f, 39.32f, x + 0.46f, 1.1f, 39.35f, _metalGrey, false);
            }
            Box("Break table", 4.2f, 0.72f, 32.2f, 6.8f, 0.77f, 33.6f, _wood, true);
            for (int i = 0; i < 4; i++) Box("Break table leg", (i % 2 == 0) ? 4.35f : 6.55f, 0f, (i < 2) ? 32.35f : 33.35f, (i % 2 == 0) ? 4.45f : 6.65f, 0.72f, (i < 2) ? 32.45f : 33.45f, _dark, false);
            Chair(4.8f, 31.7f, 180f); Chair(6.6f, 30.9f, 140f, true); Chair(4.8f, 34.1f, 0f); Chair(3.4f, 34.8f, 70f, true);
            Box("Coffee cup", 5.1f, 0.77f, 32.8f, 5.18f, 0.87f, 32.88f, _white, false);

            // Kitchenette on the east wall: cabinets, a counter, a microwave, a coffee maker, the fridge.
            Box("Cabinets", 13.2f, 0f, 28f, 13.87f, 0.88f, 32.6f, _darkwood, true);
            Box("Counter", 13.1f, 0.88f, 27.95f, 13.87f, 0.93f, 32.65f, _white, false);
            Box("Microwave", 13.3f, 0.93f, 28.3f, 13.8f, 1.22f, 28.9f, _white, false);
            Box("Microwave door", 13.29f, 0.97f, 28.35f, 13.3f, 1.18f, 28.75f, _dark, false);
            Box("Coffee maker", 13.45f, 0.93f, 29.6f, 13.8f, 1.3f, 29.9f, _dark, false);
            Box("Fridge", 13.2f, 0f, 36.2f, 13.87f, 1.8f, 36.95f, _white, true);
            Box("Fridge inside", 13.19f, 0.05f, 36.25f, 13.2f, 1.75f, 36.9f, _dark, false);
            Box("Fridge door (hanging open)", 12.45f, 0.02f, 36.96f, 13.17f, 1.8f, 37.02f, _white, false);
            Box("Vending machine", 12.5f, 0f, 38.9f, 13.7f, 1.95f, 39.8f, FTex.Plain(new Color(0.5f, 0.07f, 0.07f), 0.5f), true);
            Box("Vending machine window (smashed)", 12.6f, 0.7f, 38.88f, 13.3f, 1.8f, 38.9f, _void, false);
            Cyl("Trash can", 12.6f, 0f, 33f, 0.18f, 0.5f, _metalGrey, true);

            // The TV cart with the training tape, and a notice board.
            Box("TV cart", 1.2f, 0f, 28.8f, 2.2f, 0.85f, 29.5f, _darkwood, true);
            Box("TV", 1.3f, 0.85f, 28.85f, 2.1f, 1.45f, 29.45f, FTex.Plain(new Color(0.22f, 0.21f, 0.2f), 0.3f), true);
            Box("TV screen", 1.4f, 0.93f, 29.45f, 2.0f, 1.37f, 29.46f, FTex.Plain(new Color(0.05f, 0.07f, 0.08f), 0.9f), false);
            Box("VCR", 1.35f, 0.55f, 28.9f, 2.05f, 0.65f, 29.45f, _dark, false);
            Box("Notice board", 0.13f, 1.2f, 33f, 0.15f, 2f, 35.5f, _cork, false);
            for (int i = 0; i < 5; i++)
            {
                float pz = 33.2f + (float)_rng.NextDouble() * 2f, py = 1.3f + (float)_rng.NextDouble() * 0.45f;
                Box("Note", 0.15f, py, pz, 0.16f, py + 0.27f, pz + 0.2f, _paper, false);
            }

        }

        private static void Chair(float x, float z, float yaw) { Chair(x, z, yaw, false); }

        private static void Chair(float x, float z, float yaw, bool knocked)
        {
            var c = new GameObject("Chair").transform;
            c.SetParent(_root, false);
            c.localPosition = new Vector3(x, (knocked ? 0.25f : 0f) + _floorY, z);
            c.localRotation = Quaternion.Euler(0f, yaw, knocked ? 90f : 0f); // knocked: lying on its side
            Transform keep = _root;
            _root = c; // the chair's parts, in its own space
            Box("Seat", -0.23f, 0.45f, -0.23f, 0.23f, 0.52f, 0.23f, _fabric, true);
            Box("Back", -0.23f, 0.52f, 0.2f, 0.23f, 0.95f, 0.26f, _fabric, false);
            for (int i = 0; i < 4; i++) Box("Leg", (i % 2 == 0) ? -0.21f : 0.18f, 0f, (i < 2) ? -0.21f : 0.18f, (i % 2 == 0) ? -0.18f : 0.21f, 0.45f, (i < 2) ? -0.18f : 0.21f, _metalGrey, false);
            _root = keep;
        }

        private static void Plant(float x, float z) // long dead
        {
            Cyl("Pot", x, 0f, z, 0.22f, 0.45f, FTex.Plain(new Color(0.35f, 0.2f, 0.12f), 0.2f), true);
            Ball("Dead leaves", new Vector3(x + 0.05f, 0.62f, z), new Vector3(0.38f, 0.4f, 0.34f), FTex.Plain(new Color(0.22f, 0.16f, 0.08f), 0.05f));
            Box("Dead leaf", x + 0.3f, 0f, z - 0.2f, x + 0.42f, 0.005f, z - 0.12f, FTex.Plain(new Color(0.25f, 0.17f, 0.08f), 0.05f), false);
        }

        private static GameObject Tattletail(Vector3 local, float yaw, int color) { return Tattletail(local, yaw, color, 0f); }

        private static GameObject Tattletail(Vector3 local, float yaw, int color, float roll)
        {
            string label;
            var t = PartyTattletailModels.BuildColored(_root.TransformPoint(local), _root.rotation * Quaternion.Euler(0f, yaw, roll), _root, color, out label);
            if (t == null) return null;
            foreach (var col in t.GetComponentsInChildren<Collider>()) col.enabled = false;
            return t;
        }

        private static void BuildFlashlight(Vector3 local)
        {
            var holder = new GameObject("Flashlight pickup");
            holder.transform.SetParent(_root, false);
            holder.transform.localPosition = local;
            holder.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
            Transform keep = _root;
            _root = holder.transform;
            var body = Cyl("Flashlight body", 0f, -0.1f, 0f, 0.028f, 0.2f, FTex.Plain(new Color(0.12f, 0.12f, 0.13f), 0.55f), false);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            var head = Cyl("Flashlight head", 0f, -0.03f, 0f, 0.042f, 0.06f, _metalGrey, false);
            head.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            head.transform.localPosition = new Vector3(0f, 0.045f, 0.12f);
            var lens = Cyl("Flashlight lens", 0f, -0.005f, 0f, 0.036f, 0.01f, FTex.Glow(new Color(1f, 0.95f, 0.75f)), false);
            lens.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            lens.transform.localPosition = new Vector3(0f, 0.045f, 0.152f);
            _root = keep;
            var hit = holder.AddComponent<BoxCollider>(); // generous, so it's easy to aim at
            hit.center = new Vector3(0f, 0.05f, 0.03f);
            hit.size = new Vector3(0.3f, 0.2f, 0.45f);
            FlashlightProp = holder;
        }

        private static void Assembly()
        {
            // The conveyor snakes across the floor: in at the north, out at the inspection station.
            Conveyor(17f, 33f, 39f, 33f);
            Conveyor(39f, 33f, 39f, 27f);
            Conveyor(39f, 27f, 17f, 27f);
            Conveyor(17f, 27f, 17f, 21f);
            Conveyor(17f, 21f, 36f, 21f);
            Box("Control panel", 36.4f, 0f, 20.2f, 36.9f, 1.3f, 20.6f, _green, true);
            Box("Control panel buttons", 36.38f, 1.0f, 20.25f, 36.4f, 1.2f, 20.55f, _dark, false);
            Box("Emergency stop", 36.36f, 1.12f, 20.37f, 36.38f, 1.18f, 20.43f, _red, false);

            // Tattletails waiting on the line (tonight's batch).
            Tattletail(new Vector3(33f, 0.95f, 21f), 90f, 0, 90f);   // lying on its side, still on the belt
            Tattletail(new Vector3(28.5f, 0.87f, 21f), 90f, 0, 0f);   // still standing. Watching.
            Tattletail(new Vector3(16.2f, 0.12f, 25.4f), -30f, 0, -85f); // fell off the line

            // Inspection station at the end of the line, and the reject chute beside it.
            Box("Inspection table", 30f, 0.9f, 17f, 33.2f, 0.96f, 18.2f, _plate, true);
            for (int i = 0; i < 4; i++)
                Box("Inspection table leg", (i % 2 == 0) ? 30.1f : 33f, 0f, (i < 2) ? 17.1f : 18f, (i % 2 == 0) ? 30.2f : 33.1f, 0.9f, (i < 2) ? 17.2f : 18.1f, _dark, false);
            Box("Inspection lamp post", 31.5f, 0.96f, 18.05f, 31.58f, 1.9f, 18.13f, _dark, false);
            Box("Inspection lamp", 31.2f, 1.85f, 17.6f, 31.9f, 1.95f, 18.13f, _green, false);
            Box("PASS stamp", 30.3f, 0.96f, 17.3f, 30.45f, 1.06f, 17.42f, FTex.Plain(new Color(0.1f, 0.45f, 0.15f), 0.4f), false);
            Box("FAIL stamp", 30.55f, 0.96f, 17.3f, 30.7f, 1.06f, 17.42f, _redPaint, false);
            Box("Clipboard", 32.2f, 0.96f, 17.25f, 32.5f, 0.975f, 17.65f, _paper, false);
            Chute(35.6f, 17.6f);
            Sign("REJECTS", 35.6f, 1.25f, 18.22f, 180f, 0.12f, 1f, new Color(0.1f, 0.1f, 0.1f));

            // Machines along the north wall: the fur and voice-box lines.
            Box("Molding machine", 20f, 0f, 36.4f, 24f, 2.6f, 39.2f, _green, true);
            Box("Molding machine hopper", 21f, 2.6f, 37f, 23f, 3.6f, 38.6f, _greenDark, true);
            Box("Molding machine panel", 23.2f, 1.1f, 36.38f, 23.8f, 1.7f, 36.4f, _dark, false);
            Box("Molding machine pipe", 21.9f, 3.6f, 37.7f, 22.1f, 6.6f, 37.9f, _metalGrey, false);
            Box("Voice box machine", 30f, 0f, 36.8f, 33f, 2.2f, 39.2f, _green, true);
            Box("Voice box machine panel", 30.4f, 1.0f, 36.78f, 31.2f, 1.6f, 36.8f, _dark, false);
            Box("Voice box machine light", 30.5f, 1.45f, 36.77f, 30.6f, 1.55f, 36.78f, _red, false);

            // Stock, barrels, a cart and a tool chest around the edges.
            Pallet(15.2f, 11.2f, 3);
            Pallet(15.2f, 13.3f, 2);
            Pallet(40.6f, 11.2f, 3);
            Pallet(26f, 11.2f, 1);
            Cyl("Barrel", 40.8f, 0f, 37.5f, 0.3f, 0.9f, _blue, true);
            Cyl("Barrel", 41.4f, 0f, 38.2f, 0.3f, 0.9f, _blue, true);
            Cyl("Barrel", 40.6f, 0f, 38.6f, 0.3f, 0.9f, FTex.Plain(new Color(0.8f, 0.6f, 0.1f), 0.4f), true);
            Box("Tool chest", 35f, 0f, 39f, 36.4f, 1.1f, 39.8f, _redPaint, true);
            Box("Tool chest drawers", 35.05f, 0.2f, 38.98f, 36.35f, 1f, 39f, _dark, false);
            Cart(27.5f, 15.2f);
            Box("Parts bins", 15f, 0f, 30f, 15.8f, 1.4f, 32.4f, _metalGrey, true);
            for (int i = 0; i < 3; i++) Box("Parts bin", 15.8f, 0.2f + i * 0.45f, 30.1f, 15.85f, 0.45f + i * 0.45f, 32.3f, _blue, false);
            Sign("QUALITY IS OUR #1 PRIORITY", 28f, 4.2f, 39.84f, 0f, 0.35f, 10f, new Color(0.9f, 0.85f, 0.7f));
            Box("Banner", 22.5f, 3.9f, 39.86f, 33.5f, 4.5f, 39.87f, FTex.Plain(new Color(0.3f, 0.12f, 0.4f), 0.2f), false);

            // Painted walkway lines around the line.
            Line(15.6f, 19.6f, 40.6f, 19.72f);
            Line(15.6f, 34.4f, 40.6f, 34.52f);
            Line(15.6f, 19.6f, 15.72f, 34.52f);
            Line(40.48f, 19.6f, 40.6f, 34.52f);
        }

        private static void Pallet(float x, float z, int layers)
        {
            Box("Pallet", x - 0.6f, 0f, z - 0.5f, x + 0.6f, 0.14f, z + 0.5f, _chip, true);
            for (int l = 0; l < layers; l++)
                for (int i = 0; i < 2; i++)
                    for (int j = 0; j < 2; j++)
                    {
                        if (l == layers - 1 && _rng.NextDouble() < 0.3) continue;
                        float bx = x - 0.57f + i * 0.58f, bz = z - 0.47f + j * 0.48f, by = 0.14f + l * 0.42f;
                        Box("Box", bx, by, bz, bx + 0.56f, by + 0.42f, bz + 0.46f, _cardboard, true);
                    }
        }

        private static void Cart(float x, float z)
        {
            Box("Cart top", x - 0.5f, 0.75f, z - 0.35f, x + 0.5f, 0.8f, z + 0.35f, _plate, true);
            Box("Cart shelf", x - 0.5f, 0.2f, z - 0.35f, x + 0.5f, 0.24f, z + 0.35f, _plate, false);
            Box("Cart handle", x + 0.46f, 0.8f, z - 0.3f, x + 0.5f, 1.05f, z + 0.3f, _dark, false);
            for (int i = 0; i < 4; i++) Box("Cart wheel", x + ((i % 2 == 0) ? -0.45f : 0.39f), 0f, z + ((i < 2) ? -0.3f : 0.24f), x + ((i % 2 == 0) ? -0.39f : 0.45f), 0.12f, z + ((i < 2) ? -0.24f : 0.3f), _rubber, false);
            Box("Cart legs", x - 0.48f, 0.12f, z - 0.33f, x - 0.45f, 0.75f, z - 0.3f, _dark, false);
            Box("Cart legs", x + 0.45f, 0.12f, z + 0.3f, x + 0.48f, 0.75f, z + 0.33f, _dark, false);
            Box("Box", x - 0.4f, 0.8f, z - 0.25f, x + 0.05f, 1.15f, z + 0.2f, _cardboard, false);
        }

        private static void Conveyor(float x0, float z0, float x1, float z1)
        {
            bool alongX = Mathf.Abs(z1 - z0) < 0.01f;
            float lo, hi;
            if (alongX) { lo = Mathf.Min(x0, x1) - 0.5f; hi = Mathf.Max(x0, x1) + 0.5f; }
            else { lo = Mathf.Min(z0, z1) - 0.5f; hi = Mathf.Max(z0, z1) + 0.5f; }
            float c = alongX ? z0 : x0;
            if (alongX)
            {
                Box("Conveyor frame", lo, 0.7f, c - 0.5f, hi, 0.84f, c + 0.5f, _green, true);
                Box("Conveyor belt", lo, 0.84f, c - 0.4f, hi, 0.87f, c + 0.4f, _rubber, false);
                Box("Conveyor rail", lo, 0.84f, c - 0.5f, hi, 0.95f, c - 0.44f, _plate, false);
                Box("Conveyor rail", lo, 0.84f, c + 0.44f, hi, 0.95f, c + 0.5f, _plate, false);
                for (float x = lo + 0.3f; x < hi; x += 2f)
                {
                    Box("Conveyor leg", x, 0f, c - 0.45f, x + 0.1f, 0.7f, c - 0.35f, _dark, false);
                    Box("Conveyor leg", x, 0f, c + 0.35f, x + 0.1f, 0.7f, c + 0.45f, _dark, false);
                }
            }
            else
            {
                Box("Conveyor frame", c - 0.5f, 0.7f, lo, c + 0.5f, 0.84f, hi, _green, true);
                Box("Conveyor belt", c - 0.4f, 0.84f, lo, c + 0.4f, 0.87f, hi, _rubber, false);
                Box("Conveyor rail", c - 0.5f, 0.84f, lo, c - 0.44f, 0.95f, hi, _plate, false);
                Box("Conveyor rail", c + 0.44f, 0.84f, lo, c + 0.5f, 0.95f, hi, _plate, false);
                for (float z = lo + 0.3f; z < hi; z += 2f)
                {
                    Box("Conveyor leg", c - 0.45f, 0f, z, c - 0.35f, 0.7f, z + 0.1f, _dark, false);
                    Box("Conveyor leg", c + 0.35f, 0f, z, c + 0.45f, 0.7f, z + 0.1f, _dark, false);
                }
            }
        }

        // The reject chute: a hazard-striped hopper with a dark hole going down.
        private static void Chute(float x, float z)
        {
            const float S = 0.6f, H = 1.05f, W2 = 0.06f;
            Box("Reject chute", x - S, 0f, z - S, x + S, H, z - S + W2, _hazard, true);
            Box("Reject chute", x - S, 0f, z + S - W2, x + S, H, z + S, _hazard, true);
            Box("Reject chute", x - S, 0f, z - S, x - S + W2, H, z + S, _hazard, true);
            Box("Reject chute", x + S - W2, 0f, z - S, x + S, H, z + S, _hazard, true);
            Box("Reject chute hole", x - S + W2, 0.01f, z - S + W2, x + S - W2, 0.02f, z + S - W2, FTex.Plain(Color.black, 0f), false);
        }

        private static void Line(float x0, float z0, float x1, float z1)
        {
            Box("Floor line", x0, 0f, z0, x1, 0.006f, z1, _yellowPaint, false);
        }

        private static void SouthRooms()
        {
            // The wall between the assembly floor and the two south rooms: the lab door, a window into
            // the lab, and the way through to the R&D door.
            WallX("Lab north wall", 10f, 14f, 42f, 0f, 1.1f, _block, 19f, 21f, DOOR_TOP, 35f, 37f, DOOR_TOP);
            WallX("Lab north wall", 10f, 14f, 42f, 1.1f, 2.1f, _block, 19f, 21f, DOOR_TOP, 23f, 27f, 2.1f, 35f, 37f, DOOR_TOP);
            WallX("Lab north wall", 10f, 14f, 42f, 2.1f, HIGH, _block, 19f, 21f, DOOR_TOP, 35f, 37f, DOOR_TOP);
            Box("Lab window", 23f, 1.1f, 9.98f, 27f, 2.1f, 10.02f, FTex.Plain(new Color(0.06f, 0.08f, 0.09f), 0.97f), true);
            Box("Lab window sill", 22.9f, 1.05f, 9.8f, 27.1f, 1.1f, 10.2f, _dark, false);
            OpenDoor("Testing lab door", true, 10f, 19f, 21f, DOOR_TOP - 0.1f, _green);
            WallZ("Lab / R&D wall", 30f, 0f, 10f, 0f, LOW, _block, None);
            Box("Drop ceiling (south rooms)", 14f, LOW, 0f, 42f, LOW + 0.08f, 10f, _ceiling, true);

            // R&D: a short corridor, then the heavy door with its keypad and warning sign.
            WallX("R&D wall", 3f, 30f, 42f, 0f, LOW, _block, 35.4f, 36.6f, DOOR_TOP);
            OpenDoor("R&D door", true, 3f, 35.4f, 36.6f, DOOR_TOP - 0.1f, _greenDark); // (open for now: the story will lock it)
            Box("R&D keypad", 36.8f, 1.25f, 3.13f, 37.05f, 1.55f, 3.2f, _dark, false);
            Box("R&D keypad light", 36.9f, 1.47f, 3.2f, 36.95f, 1.52f, 3.21f, _red, false);
            Box("R&D sign", 33.9f, 1.1f, 3.13f, 35f, 2.2f, 3.16f, _danger, false);
            Box("R&D hazard floor", 34.9f, 0f, 3.13f, 37.1f, 0.006f, 4.1f, _hazard, false);
            Sign("RESEARCH & DEVELOPMENT", 36f, 2.75f, 3.14f, 180f, 0.12f, 3f, new Color(0.8f, 0.8f, 0.75f));
            Sign("AUTHORIZED STAFF ONLY", 36f, 2.58f, 3.14f, 180f, 0.09f, 3f, new Color(0.8f, 0.2f, 0.15f));
        }

        private static void Warehouse()
        {
            WallZ("Warehouse west wall", 42f, 0f, D, 0f, HIGH, _block, 23.5f, 27.5f, 4f);
            Box("Roll-up door drum", 41.7f, 4f, 23.3f, 42.35f, 4.5f, 27.7f, _dark, false);
            Sign("SHIPPING & RECEIVING", 42.14f, 4.9f, 25.5f, -90f, 0.25f, 5f, new Color(0.85f, 0.8f, 0.3f));

            // Racks: three rows north of the dock aisle, three south of it.
            float[] rows = { 37.6f, 33.2f, 28.8f, 13.6f, 9.2f, 4.8f };
            foreach (float z in rows) Rack(44.5f, 58.5f, z);

            // The docks: roll-up doors (closed), dock plates, bumpers.
            Dock(17f, 20.5f);
            Dock(22f, 25.5f);
            Sign("DOCK 1", 59.86f, 4f, 18.75f, 90f, 0.3f, 2f, new Color(0.9f, 0.8f, 0.2f));
            Sign("DOCK 2", 59.86f, 4f, 23.75f, 90f, 0.3f, 2f, new Color(0.9f, 0.8f, 0.2f));

            // Crates by the south wall (good for hiding, later), pallets waiting to ship, a forklift.
            Box("Crate", 44.6f, 0f, 0.6f, 46.6f, 2f, 2.6f, _chip, true);
            Box("Crate", 47f, 0f, 0.6f, 48.8f, 1.6f, 2.4f, _chip, true);
            Box("Crate", 44.9f, 2f, 0.9f, 46.3f, 3.2f, 2.3f, _chip, true);
            Box("Crate", 49.2f, 0f, 0.6f, 51f, 1.8f, 2.4f, _chip, true);
            Pallet(50.6f, 20.5f, 2);
            Pallet(52f, 22.5f, 3);
            Pallet(46f, 26f, 1);
            Forklift(54.5f, 19f);
            Box("Pallet jack", 47.5f, 0f, 21f, 48.1f, 0.09f, 22.2f, _redPaint, false);
            Box("Pallet jack handle", 47.72f, 0.09f, 22.15f, 47.88f, 1.1f, 22.25f, _dark, false);
            Cyl("Trash drum", 43f, 0f, 16.5f, 0.3f, 0.9f, _greenDark, true);
        }

        private static void Forklift(float x, float z)
        {
            FMat yellow = FTex.Plain(new Color(0.85f, 0.65f, 0.08f), 0.4f);
            Box("Forklift body", x - 0.6f, 0.25f, z - 1f, x + 0.6f, 1.2f, z + 0.9f, yellow, true);
            Box("Forklift counterweight", x - 0.6f, 0.25f, z + 0.9f, x + 0.6f, 1.3f, z + 1.3f, _dark, true);
            Box("Forklift seat", x - 0.3f, 1.2f, z + 0.1f, x + 0.3f, 1.35f, z + 0.6f, _dark, false);
            Box("Forklift roof", x - 0.6f, 2.15f, z - 0.8f, x + 0.6f, 2.2f, z + 0.9f, _dark, false);
            for (int i = 0; i < 4; i++)
                Box("Forklift post", x + ((i % 2 == 0) ? -0.6f : 0.55f), 1.2f, z + ((i < 2) ? -0.8f : 0.85f), x + ((i % 2 == 0) ? -0.55f : 0.6f), 2.15f, z + ((i < 2) ? -0.75f : 0.9f), _dark, false);
            Box("Forklift mast", x - 0.45f, 0.1f, z - 1.15f, x + 0.45f, 2.6f, z - 1.05f, _dark, false);
            Box("Forklift fork", x - 0.4f, 0.05f, z - 2.2f, x - 0.28f, 0.1f, z - 1.1f, _metalGrey, false);
            Box("Forklift fork", x + 0.28f, 0.05f, z - 2.2f, x + 0.4f, 0.1f, z - 1.1f, _metalGrey, false);
            for (int i = 0; i < 4; i++)
            {
                var w = Cyl("Forklift wheel", 0f, 0f, 0f, 0.25f, 0.2f, _rubber, false);
                w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                w.transform.localPosition = new Vector3(x + ((i % 2 == 0) ? -0.62f : 0.62f), 0.25f, z + ((i < 2) ? -0.6f : 0.8f));
            }
        }

        private static void Rack(float x0, float x1, float z)
        {
            const float DEPTH = 1.1f, TOP = 4.6f;
            float z0 = z - DEPTH / 2, z1 = z + DEPTH / 2;
            for (float x = x0; x <= x1 + 0.01f; x += 2.8f)
            {
                Box("Rack upright", x - 0.05f, 0f, z0, x + 0.05f, TOP, z0 + 0.1f, _orange, true);
                Box("Rack upright", x - 0.05f, 0f, z1 - 0.1f, x + 0.05f, TOP, z1, _orange, true);
            }
            float[] levels = { 0.15f, 1.65f, 3.15f };
            foreach (float y in levels)
            {
                Box("Rack beam", x0, y, z0, x1, y + 0.12f, z0 + 0.08f, _orange, false);
                Box("Rack beam", x0, y, z1 - 0.08f, x1, y + 0.12f, z1, _orange, false);
                Box("Rack deck", x0, y + 0.12f, z0, x1, y + 0.15f, z1, _plate, true);
                for (float x = x0 + 0.15f; x < x1 - 0.5f; x += 0.7f)
                {
                    double r = _rng.NextDouble();
                    if (r < 0.28) continue;
                    float w = 0.5f + (float)_rng.NextDouble() * 0.15f, h = 0.35f + (float)_rng.NextDouble() * 0.5f;
                    float dz = (float)(_rng.NextDouble() * 0.2 - 0.1);
                    Box("Box", x, y + 0.15f, z - 0.4f + dz, x + w, y + 0.15f + h, z + 0.4f + dz, _cardboard, true);
                }
            }
        }

        private static void Dock(float z0, float z1)
        {
            Box("Dock door", W - 0.1f, 0f, z0, W - 0.02f, 3.6f, z1, _steel, true);
            Box("Dock door frame", W - 0.3f, 0f, z0 - 0.15f, W - 0.1f, 3.75f, z0, _hazard, false);
            Box("Dock door frame", W - 0.3f, 0f, z1, W - 0.1f, 3.75f, z1 + 0.15f, _hazard, false);
            Box("Dock plate", W - 2.2f, 0f, z0 + 0.2f, W - 0.1f, 0.02f, z1 - 0.2f, _plate, false);
            Box("Dock hazard", W - 2.5f, 0f, z0 + 0.2f, W - 2.2f, 0.021f, z1 - 0.2f, _hazard, false);
            Box("Dock bumper", W - 0.35f, 0.25f, z0 + 0.3f, W - 0.1f, 0.65f, z0 + 0.7f, _rubber, true);
            Box("Dock bumper", W - 0.35f, 0.25f, z1 - 0.7f, W - 0.1f, 0.65f, z1 - 0.3f, _rubber, true);
        }

        // ── Outside: the parking lot, 2026 ───────────────────────────────────
        public const float CAR_X = 8.5f, CAR_Z = -10.5f;

        private static void Exterior()
        {
            if (!Town)
            {
                Box("Ground", -40f, -0.25f, -60f, 100f, -0.02f, 0f, _dirt, true);
                Box("Ground", -40f, -0.25f, D, 100f, -0.02f, 80f, _dirt, true);
                Box("Ground", -40f, -0.25f, 0f, 0f, -0.02f, D, _dirt, true);
                Box("Ground", W, -0.25f, 0f, 100f, -0.02f, D, _dirt, true);
            }
            Box("Parking lot", LOT_X0, -0.2f, LOT_Z0, LOT_X1, 0f, 0f, _asphalt, true);
            Box("Sidewalk", LOT_X0, 0f, -1.6f, LOT_X1, 0.1f, 0f, _concrete, true);
            for (float x = 1f; x <= 34f; x += 3f) Box("Parking line", x - 0.05f, 0f, -13.5f, x + 0.05f, 0.004f, -7.5f, _lineFaded, false);
            if (!Town) Car(CAR_X, CAR_Z);

            // Weeds through the cracks and along the building.
            for (int i = 0; i < 90; i++)
                Weed(LOT_X0 + (float)_rng.NextDouble() * (LOT_X1 - LOT_X0), LOT_Z0 + (float)_rng.NextDouble() * (-2f - LOT_Z0));
            for (int i = 0; i < 60; i++) Weed((float)_rng.NextDouble() * W, -1.75f - (float)_rng.NextDouble() * 0.5f);

            LampPost(20f, -14f);
            LampPost(44f, -14f);
            Box("Dumpster", 56f, 0f, -4f, 58.2f, 1.3f, -2.8f, _greenDark, true);
            Box("Dumpster lid", 55.95f, 1.3f, -4.05f, 58.25f, 1.36f, -2.75f, _dark, false).transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);

            Cyl("Sign post", 3f, 0f, -4f, 0.04f, 2f, _metalGrey, true);
            Box("Sign board", 2.35f, 1.3f, -4.06f, 3.65f, 2.0f, -4.04f, _white, false);
            Sign("NO TRESPASSING", 3f, 1.78f, -4.065f, 0f, 0.16f, 1.3f, new Color(0.65f, 0.08f, 0.06f));
            Sign("PROPERTY OF WAYGETTER ELECTRONICS", 3f, 1.5f, -4.065f, 0f, 0.07f, 1.3f, new Color(0.15f, 0.15f, 0.15f));
            Box("Notice", 8.2f, 1.2f, -0.14f, 8.9f, 1.9f, -0.126f, _paper, false);
            Sign("CONDEMNED", 8.55f, 1.75f, -0.145f, 0f, 0.1f, 0.7f, new Color(0.6f, 0.06f, 0.05f));
            Sign("UNSAFE - KEEP OUT", 8.55f, 1.55f, -0.145f, 0f, 0.05f, 0.7f, new Color(0.15f, 0.15f, 0.15f));
            Sign("WAYGET ER  ELECTR NICS", 30f, 6.3f, -0.14f, 0f, 1.1f, 40f, new Color(0.22f, 0.14f, 0.26f));

            // The fence, and invisible walls so nobody walks off into the dark.
            if (Town)
            {
                Fence(LOT_X0, LOT_Z0, TOWN_GATE_X0, LOT_Z0);
                Fence(TOWN_GATE_X1, LOT_Z0, LOT_X1, LOT_Z0);
                Blocker(LOT_X0 - 0.3f, LOT_Z0 - 0.3f, TOWN_GATE_X0, LOT_Z0);
                Blocker(TOWN_GATE_X1, LOT_Z0 - 0.3f, LOT_X1 + 0.3f, LOT_Z0);
            }
            else
            {
                Fence(LOT_X0, LOT_Z0, LOT_X1, LOT_Z0);
                Blocker(LOT_X0 - 0.3f, LOT_Z0 - 0.3f, LOT_X1 + 0.3f, LOT_Z0);
            }
            Fence(LOT_X0, LOT_Z0, LOT_X0, 0f);
            Fence(LOT_X1, LOT_Z0, LOT_X1, 0f);
            Blocker(LOT_X0 - 0.3f, LOT_Z0, LOT_X0, 0.3f);
            Blocker(LOT_X1, LOT_Z0, LOT_X1 + 0.3f, 0.3f);
            Blocker(LOT_X0, 0f, 0f, 0.3f);
            Blocker(W, 0f, LOT_X1, 0.3f);
        }

        private static void Weed(float x, float z)
        {
            for (int i = 0; i < 3; i++)
            {
                float h = 0.12f + (float)_rng.NextDouble() * 0.28f;
                var g = Box("Weed", x - 0.015f, 0f, z - 0.01f, x + 0.015f, h, z + 0.01f, _weed, false);
                g.transform.localRotation = Quaternion.Euler((float)_rng.NextDouble() * 30f - 15f, (float)_rng.NextDouble() * 360f, (float)_rng.NextDouble() * 30f - 15f);
                x += 0.05f; z += 0.03f;
            }
        }

        private static void LampPost(float x, float z) // dead for years
        {
            Cyl("Lamp post", x, 0f, z, 0.09f, 6f, _metalGrey, true);
            Box("Lamp arm", x - 0.05f, 5.85f, z, x + 0.05f, 5.95f, z + 1.4f, _metalGrey, false);
            Box("Lamp head", x - 0.25f, 5.7f, z + 1.1f, x + 0.25f, 5.88f, z + 1.7f, _dark, false);
        }

        private static void Fence(float x0, float z0, float x1, float z1)
        {
            float len = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(z1 - z0));
            bool alongX = Mathf.Abs(x1 - x0) > Mathf.Abs(z1 - z0);
            for (float d = 0f; d <= len + 0.01f; d += 2.5f)
                Cyl("Fence post", alongX ? x0 + d : x0, 0f, alongX ? z0 : z0 + d, 0.035f, 2.1f, _metalGrey, false);
            foreach (float y in new[] { 1.0f, 2.05f })
            {
                if (alongX) Box("Fence rail", x0, y, z0 - 0.02f, x1, y + 0.04f, z0 + 0.02f, _metalGrey, false);
                else Box("Fence rail", x0 - 0.02f, y, z0, x0 + 0.02f, y + 0.04f, z1, _metalGrey, false);
            }
        }

        // An invisible wall (full height).
        private static void Blocker(float x0, float z0, float x1, float z1)
        {
            var go = new GameObject("Invisible wall");
            go.transform.SetParent(_root, false);
            go.transform.localPosition = new Vector3((x0 + x1) * 0.5f, 3f, (z0 + z1) * 0.5f);
            go.AddComponent<BoxCollider>().size = new Vector3(Mathf.Max(0.05f, x1 - x0), 6f, Mathf.Max(0.05f, z1 - z0));
        }

        // Your car: facing the factory, engine running, headlights on. The flashlight is in the trunk.
        private static void Car(float x, float z)
        {
            var car = new GameObject("Your car").transform;
            car.SetParent(_root, false);
            car.localPosition = new Vector3(x, 0f, z);
            Transform keep = _root;
            _root = car;
            FGeo.Hex(car, "Car body", new[] {
                new Vector3(-0.9f, 0.3f, -1.2f), new Vector3(0.9f, 0.3f, -1.2f), new Vector3(0.9f, 0.3f, 2.25f), new Vector3(-0.9f, 0.3f, 2.25f),
                new Vector3(-0.88f, 0.85f, -1.2f), new Vector3(0.88f, 0.85f, -1.2f), new Vector3(0.86f, 0.8f, 2.05f), new Vector3(-0.86f, 0.8f, 2.05f) }, _carPaint, true);
            FGeo.Hex(car, "Car cabin", new[] {
                new Vector3(-0.82f, 0.84f, -1.15f), new Vector3(0.82f, 0.84f, -1.15f), new Vector3(0.82f, 0.83f, 0.95f), new Vector3(-0.82f, 0.83f, 0.95f),
                new Vector3(-0.7f, 1.4f, -0.85f), new Vector3(0.7f, 1.4f, -0.85f), new Vector3(0.7f, 1.4f, 0.35f), new Vector3(-0.7f, 1.4f, 0.35f) }, _carGlass, true);
            FGeo.Hex(car, "Car roof", new[] {
                new Vector3(-0.71f, 1.39f, -0.87f), new Vector3(0.71f, 1.39f, -0.87f), new Vector3(0.71f, 1.39f, 0.37f), new Vector3(-0.71f, 1.39f, 0.37f),
                new Vector3(-0.69f, 1.44f, -0.83f), new Vector3(0.69f, 1.44f, -0.83f), new Vector3(0.69f, 1.44f, 0.33f), new Vector3(-0.69f, 1.44f, 0.33f) }, _carPaint, false);

            // The trunk: a tub under the lid.
            Box("Trunk floor", -0.9f, 0.3f, -2.3f, 0.9f, 0.5f, -1.2f, _carPaint, true);
            Box("Trunk side", -0.9f, 0.5f, -2.3f, -0.84f, 0.85f, -1.2f, _carPaint, true);
            Box("Trunk side", 0.84f, 0.5f, -2.3f, 0.9f, 0.85f, -1.2f, _carPaint, true);
            Box("Trunk back", -0.84f, 0.5f, -2.3f, 0.84f, 0.85f, -2.24f, _carPaint, true);
            Box("Trunk carpet", -0.84f, 0.5f, -2.24f, 0.84f, 0.51f, -1.2f, _dark, false);
            BuildFlashlight(new Vector3(0.15f, 0.52f, -1.75f));
            TrunkTattletail = Tattletail(new Vector3(-0.35f, 0.62f, -1.75f), 90f, 0, 80f); // your Tattletail, along for the ride
            Box("Jumper cables", -0.6f, 0.51f, -2.1f, -0.2f, 0.57f, -1.7f, _redPaint, false);
            var hinge = new GameObject("Trunk hinge").transform;
            hinge.SetParent(car, false);
            hinge.localPosition = new Vector3(0f, 0.85f, -1.2f);
            _root = hinge;
            TrunkLid = Box("Trunk lid", -0.88f, 0f, -1.1f, 0.88f, 0.035f, 0f, _carPaint, true);
            _root = car;
            TrunkHinge = hinge;

            // Wheels, bumpers, lights, mirrors, door seams, the plate.
            for (int i = 0; i < 4; i++)
            {
                float wx = (i % 2 == 0) ? -0.84f : 0.84f, wz = (i < 2) ? 1.45f : -1.55f;
                var wheel = Cyl("Wheel", 0f, 0f, 0f, 0.33f, 0.22f, _rubber, false);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheel.transform.localPosition = new Vector3(wx, 0.33f, wz);
                var hub = Cyl("Hubcap", 0f, 0f, 0f, 0.19f, 0.235f, _metalGrey, false);
                hub.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                hub.transform.localPosition = new Vector3(wx, 0.33f, wz);
            }
            Box("Front bumper", -0.92f, 0.26f, 2.18f, 0.92f, 0.46f, 2.34f, _dark, false);
            Box("Rear bumper", -0.92f, 0.28f, -2.38f, 0.92f, 0.48f, -2.24f, _dark, false);
            Box("Headlight", -0.8f, 0.6f, 2.06f, -0.45f, 0.72f, 2.16f, _headlight, false);
            Box("Headlight", 0.45f, 0.6f, 2.06f, 0.8f, 0.72f, 2.16f, _headlight, false);
            Box("Taillight", -0.86f, 0.6f, -2.315f, -0.55f, 0.76f, -2.295f, _taillight, false);
            Box("Taillight", 0.55f, 0.6f, -2.315f, 0.86f, 0.76f, -2.295f, _taillight, false);
            Box("Mirror", -1.02f, 0.9f, 0.85f, -0.9f, 1f, 0.97f, _carPaint, false);
            Box("Mirror", 0.9f, 0.9f, 0.85f, 1.02f, 1f, 0.97f, _carPaint, false);
            foreach (float sz in new[] { -0.15f, 0.95f })
            {
                Box("Door seam", -0.905f, 0.36f, sz, -0.9f, 0.84f, sz + 0.01f, _dark, false);
                Box("Door seam", 0.9f, 0.36f, sz, 0.905f, 0.84f, sz + 0.01f, _dark, false);
            }
            Box("Plate", -0.25f, 0.34f, -2.395f, 0.25f, 0.46f, -2.385f, _white, false);
            Sign("TT 1998", 0f, 0.4f, -2.398f, 0f, 0.08f, 0.5f, new Color(0.1f, 0.12f, 0.35f));
            _root = keep;
        }

        // ── The way in ───────────────────────────────────────────────────────
        private static void BrokenFrontDoor()
        {
            const float a = 5.5f, b = 7.5f, top = DOOR_TOP, F = 0.2f;
            Box("Front door frame", a - 0.08f, 0f, -F, a, top + 0.08f, F, _dark, false);
            Box("Front door frame", b, 0f, -F, b + 0.08f, top + 0.08f, F, _dark, false);
            Box("Front door frame", a - 0.08f, top, -F, b + 0.08f, top + 0.08f, F, _dark, false);
            FMat doorGlass = FTex.Plain(new Color(0.06f, 0.07f, 0.08f), 0.9f);
            Box("Front door (kicked in)", 2.8f, 0.01f, 1.4f, 3.8f, 0.05f, 3.6f, doorGlass, false).transform.localRotation = Quaternion.Euler(0f, 28f, 0f);
            Box("Front door (hanging open)", 7.42f, 0f, 0.22f, 7.48f, top - 0.05f, 1.18f, doorGlass, false);
            // The boards someone nailed up, mostly pried off.
            Plank(a - 0.2f, 1.95f, b + 0.2f, 2.1f, -0.24f, 4f);
            Plank(a - 0.25f, 0.4f, a + 1.1f, 0.54f, -0.24f, -34f);
            Box("Plank (pulled off)", 3.9f, 0.1f, -1.3f, 5.8f, 0.13f, -1.14f, _chip, false).transform.localRotation = Quaternion.Euler(0f, 18f, 0f);
            Box("Plank (pulled off)", 7.8f, 0.1f, -0.9f, 9.5f, 0.13f, -0.75f, _chip, false).transform.localRotation = Quaternion.Euler(0f, -32f, 0f);
            Shards(6.5f, 0.9f, 0.9f, 28);
            Shards(6.5f, -0.7f, 0.7f, 10);
        }

        private static void BoardedWindow(float x0, float x1)
        {
            Box("Window sill", x0 - 0.05f, 1.05f, -0.2f, x1 + 0.05f, 1.1f, 0.2f, _concrete, false);
            Plank(x0 - 0.15f, 1.25f, x1 + 0.15f, 1.42f, 0.2f, 3f);
            Plank(x0 - 0.15f, 1.72f, x1 + 0.15f, 1.88f, 0.2f, -6f);
            Plank(x0 - 0.1f, 2.06f, x0 + 1.6f, 2.21f, 0.2f, 12f);
            var jag = Box("Glass left in the frame", x1 - 0.55f, 1.1f, -0.01f, x1 - 0.05f, 1.55f, 0.01f, _shard, false);
            jag.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
            Blocker(x0, -0.12f, x1, 0.12f);
            Shards((x0 + x1) * 0.5f, 0.7f, 0.9f, 22);
        }

        private static void Plank(float x0, float y0, float x1, float y1, float z, float roll)
        {
            var p = Box("Plank", x0, y0, z - 0.02f, x1, y1, z + 0.02f, _chip, false);
            p.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
        }

        private static void HangingSheet(float x, float z, float len, float tilt)
        {
            var g = Box("Roof sheet (hanging)", x - 0.6f, HIGH - len, z - 0.015f, x + 0.6f, HIGH, z + 0.015f, _roof, false);
            g.transform.localRotation = Quaternion.Euler(tilt, 0f, 0f);
        }

        // ── Decades of nobody: rubble, water, fallen ceilings, paper, graffiti ──
        private static void Ruin()
        {
            // Where the roof came down: rubble and sheet metal across the line, and rain water.
            Rubble(28.5f, 26.5f, 2.6f, 70, true);
            FallenSheet(27.2f, 1.0f, 25.6f, 2.4f, 1.3f, 35f, 20f);
            FallenSheet(29.8f, 0.6f, 27.8f, 2f, 1.2f, -25f, 70f);
            FallenSheet(26.5f, 0.2f, 28.6f, 1.8f, 1.1f, 8f, 140f);
            Puddle(27.6f, 23.9f, 2.6f, 1.4f);
            Rubble(51.5f, 32f, 1.4f, 30, false);
            Puddle(51.2f, 31f, 1.8f, 1.2f);
            Puddle(38f, 12.3f, 1.4f, 0.8f);
            Puddle(47.5f, 16f, 1.2f, 0.9f);

            // Ceiling tiles down in the west block and the south rooms.
            CeilingHole(2.4f, 4.8f); CeilingHole(3f, 4.8f); CeilingHole(2.4f, 5.4f);
            CeilingHole(7.8f, 11.4f); CeilingHole(8.4f, 11.4f); CeilingHole(4.2f, 16.8f);
            CeilingHole(9f, 24f); CeilingHole(7.8f, 30f); CeilingHole(8.4f, 30.6f); CeilingHole(1.8f, 37.2f);
            CeilingHole(21f, 6f); CeilingHole(33.6f, 7.2f);

            // Paper everywhere, punch cards spilled under the time clock, the display case smashed.
            Papers(6.5f, 8f, 3f, 30); Papers(11.5f, 6.8f, 1.2f, 12); Papers(5.5f, 33f, 2.5f, 16); Papers(10f, 23.5f, 2f, 8);
            Papers(31.5f, 16.5f, 2.2f, 14); Papers(50f, 22f, 4f, 16); Papers(36f, 6f, 2f, 8);
            for (int i = 0; i < 14; i++)
            {
                float px = 0.35f + (float)_rng.NextDouble() * 1.1f, pz = 3.7f + (float)_rng.NextDouble() * 1.6f;
                Box("Punch card", px, 0f, pz, px + 0.08f, 0.003f, pz + 0.18f, _paper, false).transform.localRotation = Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f);
            }
            Shards(7f, 15f, 1.3f, 30);

            // Boxes knocked off the racks and pallets.
            SpilledBoxes(47f, 10.5f, 55f, 12f, 14);
            SpilledBoxes(46f, 30.4f, 57f, 31.6f, 10);
            SpilledBoxes(14.8f, 14.6f, 16.6f, 16.2f, 5);
            Rubble(15.2f, 38.8f, 1f, 12, false); // a corner of the wall crumbling

            FallenFixture(25.5f, 31f); // a lamp that came down on one chain

            // Graffiti from explorers who got here first.
            Graffiti("knew", 0.15f, 1.75f, 13.6f, -90f, 0.55f, -4f);
            Graffiti("rip", 12.4f, 1.3f, 19.86f, 0f, 0.35f, 6f);
            Graffiti("still", 6.14f, 1.5f, 21.4f, -90f, 0.4f, -3f);
            Graffiti("sees", 17.6f, 1.9f, 39.84f, 0f, 0.62f, 3f);
            Graffiti("home", 32f, 1.7f, 10.15f, 180f, 0.95f, -5f);
            Graffiti("dont", 32.2f, 1.75f, 3.15f, 180f, 0.42f, 4f);
            Graffiti("kz", 42.14f, 1.7f, 35.4f, -90f, 0.95f, -7f);
            Graffiti("was", 42.14f, 1.5f, 7f, -90f, 0.32f, 5f);
            Graffiti("lies", 59.86f, 1.85f, 31f, 90f, 0.62f, -3f);
            Graffiti("count", 13.85f, 2.6f, 5.9f, 90f, 0.26f, 4f);    // next to the accident board (a hint)
            Graffiti("eye", 38.5f, 1.85f, 3.15f, 180f, 0.6f, 0f);     // by the R&D door

            // Someone has been staying here: a sleeping bag, a lantern, empty cans. (For the story.)
            Box("Sleeping bag", 44.2f, 0f, 35.1f, 46.1f, 0.12f, 35.9f, FTex.Plain(new Color(0.14f, 0.26f, 0.18f), 0.2f), false).transform.localRotation = Quaternion.Euler(0f, 8f, 0f);
            Cyl("Camp lantern", 46.5f, 0f, 35.2f, 0.08f, 0.25f, _greenDark, false);
            Cyl("Can", 46.8f, 0f, 35.7f, 0.04f, 0.11f, _metalGrey, false);
            Cyl("Can", 44f, 0.04f, 36.2f, 0.04f, 0.11f, _metalGrey, false).transform.localRotation = Quaternion.Euler(90f, 0f, 20f);
        }

        private static void Rubble(float x, float z, float radius, int count, bool pile)
        {
            for (int i = 0; i < count; i++)
            {
                float ang = (float)_rng.NextDouble() * Mathf.PI * 2f, d = Mathf.Sqrt((float)_rng.NextDouble()) * radius;
                float px = x + Mathf.Cos(ang) * d, pz = z + Mathf.Sin(ang) * d;
                float s = 0.08f + (float)_rng.NextDouble() * (pile ? 0.5f : 0.25f), h = s * (0.4f + (float)_rng.NextDouble() * 0.5f);
                float heap = pile ? (1f - d / radius) * 0.7f : 0f;
                var g = FGeo.Box(_root, "Rubble", new Vector3(px, heap + h * 0.35f, pz), new Vector3(s, h, s * (0.6f + (float)_rng.NextDouble() * 0.6f)), _rng.NextDouble() < 0.6 ? _concrete : _block, s > 0.4f);
                g.transform.localRotation = Quaternion.Euler((float)_rng.NextDouble() * 50f - 25f, (float)_rng.NextDouble() * 360f, (float)_rng.NextDouble() * 50f - 25f);
            }
        }

        private static void FallenSheet(float x, float y, float z, float w, float len, float tilt, float yaw)
        {
            var g = FGeo.Box(_root, "Roof sheet (fallen)", new Vector3(x, y, z), new Vector3(w, 0.03f, len), _roof, false);
            g.transform.localRotation = Quaternion.Euler(tilt, yaw, 0f);
        }

        private static void Puddle(float x, float z, float w, float d)
        {
            for (int i = 0; i < 3; i++)
            {
                float ox = ((float)_rng.NextDouble() - 0.5f) * w * 0.4f, oz = ((float)_rng.NextDouble() - 0.5f) * d * 0.4f;
                float sw = w * (0.55f + (float)_rng.NextDouble() * 0.4f), sd = d * (0.55f + (float)_rng.NextDouble() * 0.4f);
                var g = FGeo.Box(_root, "Puddle", new Vector3(x + ox, 0.003f + i * 0.0015f, z + oz), new Vector3(sw, 0.002f, sd), _water, false);
                g.transform.localRotation = Quaternion.Euler(0f, (float)_rng.NextDouble() * 90f, 0f);
            }
        }

        // A missing drop-ceiling tile (a black hole) and the tile itself on the floor below.
        private static void CeilingHole(float x, float z)
        {
            Box("Ceiling hole", x, LOW - 0.004f, z, x + 0.6f, LOW, z + 0.6f, _void, false);
            var tile = FGeo.Box(_root, "Fallen ceiling tile", new Vector3(x + 0.3f + ((float)_rng.NextDouble() - 0.5f) * 0.8f, 0.03f, z + 0.3f + ((float)_rng.NextDouble() - 0.5f) * 0.8f), new Vector3(0.6f, 0.02f, 0.6f), _ceiling, false);
            tile.transform.localRotation = Quaternion.Euler((float)_rng.NextDouble() * 16f - 8f, (float)_rng.NextDouble() * 360f, (float)_rng.NextDouble() * 16f - 8f);
            for (int i = 0; i < 4; i++)
            {
                float cx = x + (float)_rng.NextDouble() * 0.9f - 0.15f, cz = z + (float)_rng.NextDouble() * 0.9f - 0.15f;
                FGeo.Box(_root, "Ceiling crumbs", new Vector3(cx, 0.01f, cz), new Vector3(0.08f + (float)_rng.NextDouble() * 0.1f, 0.02f, 0.06f + (float)_rng.NextDouble() * 0.08f), _ceiling, false)
                    .transform.localRotation = Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f);
            }
        }

        private static void Papers(float x, float z, float radius, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float ang = (float)_rng.NextDouble() * Mathf.PI * 2f, d = Mathf.Sqrt((float)_rng.NextDouble()) * radius;
                var g = FGeo.Box(_root, "Paper", new Vector3(x + Mathf.Cos(ang) * d, 0.004f + i * 0.0003f, z + Mathf.Sin(ang) * d), new Vector3(0.21f, 0.002f, 0.28f), _paper, false);
                g.transform.localRotation = Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f);
            }
        }

        private static void Shards(float x, float z, float radius, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float ang = (float)_rng.NextDouble() * Mathf.PI * 2f, d = Mathf.Sqrt((float)_rng.NextDouble()) * radius;
                var g = FGeo.Box(_root, "Glass", new Vector3(x + Mathf.Cos(ang) * d, 0.005f, z + Mathf.Sin(ang) * d), new Vector3(0.02f + (float)_rng.NextDouble() * 0.07f, 0.004f, 0.02f + (float)_rng.NextDouble() * 0.05f), _shard, false);
                g.transform.localRotation = Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f);
            }
        }

        private static void SpilledBoxes(float x0, float z0, float x1, float z1, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float s = 0.4f + (float)_rng.NextDouble() * 0.25f, h = 0.3f + (float)_rng.NextDouble() * 0.3f;
                var g = FGeo.Box(_root, "Box (fallen)", new Vector3(x0 + (float)_rng.NextDouble() * (x1 - x0), h * 0.5f, z0 + (float)_rng.NextDouble() * (z1 - z0)), new Vector3(s, h, s * 0.85f), _cardboard, true);
                g.transform.localRotation = Quaternion.Euler(_rng.NextDouble() < 0.4 ? 90f : 0f, (float)_rng.NextDouble() * 360f, (float)_rng.NextDouble() * 10f - 5f);
            }
        }

        private static void FallenFixture(float x, float z)
        {
            var g = FGeo.Box(_root, "Lamp (fallen)", new Vector3(x, 4.4f, z), new Vector3(1.5f, 0.12f, 0.4f), _dark, false);
            g.transform.localRotation = Quaternion.Euler(8f, 0f, 38f);
            Box("Lamp chain", x + 0.55f, 4.85f, z - 0.01f, x + 0.57f, HIGH - 0.7f, z + 0.01f, _dark, false);
            Box("Lamp chain (snapped)", x - 0.6f, 6.9f, z - 0.01f, x - 0.58f, HIGH - 0.7f, z + 0.01f, _dark, false);
        }

        // ── Signs: every doorway says where it goes, and the lobby has a map ──
        private static void Signage()
        {
            // Over each doorway, on both sides (yaw: 0 reads from the south, 180 from the north, 90 from the west, -90 from the east).
            RoomSign("BREAK ROOM  /  STAIRS", 10.5f, 2.62f, 19.84f, 0f, 2.2f);
            RoomSign("LOBBY  /  EXIT", 10.5f, 2.62f, 20.16f, 180f, 1.8f);
            RoomSign("BREAK ROOM", 10.5f, 2.62f, 26.84f, 0f, 1.6f);
            RoomSign("LOBBY  /  EXIT", 10.5f, 2.62f, 27.16f, 180f, 1.8f);
            RoomSign("ASSEMBLY FLOOR", 13.84f, 2.62f, 14f, 90f, 1.9f);
            RoomSign("LOBBY  /  EXIT", 14.16f, 2.62f, 14f, -90f, 1.8f);
            RoomSign("ASSEMBLY FLOOR", 13.84f, 2.62f, 34.5f, 90f, 1.9f);
            RoomSign("BREAK ROOM", 14.16f, 2.62f, 34.5f, -90f, 1.6f);
            RoomSign("WAREHOUSE", 41.86f, 4.75f, 25.5f, 90f, 2.2f);
            RoomSign("SECURITY", 11.2f, 2.75f, 5.5f, 0f, 1.3f);
            RoomSign("EMPLOYEE LOCKERS", 2.6f, 2.3f, 39.84f, 0f, 2.2f);
            RoomSign("TESTING LAB", 20f, 2.62f, 10.16f, 180f, 1.6f);

            // The facility map by the entrance ("YOU ARE HERE"): tools/factory_map.py.
            Box("Map frame", 0.14f, 1.13f, 6.05f, 0.17f, 2.47f, 8.35f, _dark, false);
            Box("Facility map", 0.17f, 1.18f, 6.1f, 0.18f, 2.42f, 8.3f, FTex.Get("map", 0f, new Color(0.85f, 0.83f, 0.78f), 0.2f, 0f, 0f), false);
        }

        // A dark plate with white letters, like real building signage.
        private static void RoomSign(string text, float x, float y, float z, float yaw, float width)
        {
            var plate = FGeo.Box(_root, "Sign " + text, Vector3.zero, new Vector3(width, 0.24f, 0.02f), FTex.Plain(new Color(0.08f, 0.11f, 0.14f), 0.4f), false);
            plate.transform.localPosition = new Vector3(x, y, z);
            plate.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 front = plate.transform.localRotation * new Vector3(0f, 0f, -0.012f); // just in front of the plate, toward the reader
            Sign(text, x + front.x, y, z + front.z, yaw, 0.15f, width, new Color(0.88f, 0.86f, 0.8f));
        }

        // Spray paint (tools/factory_graffiti.py): a thin see-through decal, 'height' meters tall, on a wall.
        private static void Graffiti(string key, float x, float y, float z, float yaw, float height, float roll)
        {
            float aspect;
            FMat m = FTex.Spray(key, out aspect);
            var g = FGeo.Box(_root, "Graffiti " + key, Vector3.zero, new Vector3(height * aspect, height, 0.004f), m, false);
            g.transform.localPosition = new Vector3(x, y, z);
            g.transform.localRotation = Quaternion.Euler(0f, yaw, roll);
        }

        // The locker that opens: hollow, with a door on a hinge and the battery on its shelf.
        private static void BuildOpenLocker(float x)
        {
            const float Z0 = 39.35f, Z1 = 39.86f, H = 1.9f;
            Box("Locker back", x, 0f, Z1 - 0.06f, x + 0.55f, H, Z1, _green, true);
            Box("Locker side", x, 0f, Z0, x + 0.02f, H, Z1, _green, true);
            Box("Locker side", x + 0.53f, 0f, Z0, x + 0.55f, H, Z1, _green, true);
            Box("Locker top", x, H - 0.02f, Z0, x + 0.55f, H, Z1, _green, false);
            Box("Locker bottom", x, 0f, Z0, x + 0.55f, 0.05f, Z1, _green, true);
            Box("Locker inside", x + 0.02f, 0.05f, Z1 - 0.08f, x + 0.53f, H - 0.02f, Z1 - 0.06f, _dark, false);
            Box("Locker shelf", x + 0.02f, 1.43f, Z0 + 0.02f, x + 0.53f, 1.45f, Z1 - 0.08f, _metalGrey, false);
            Box("Old lunchbox", x + 0.1f, 0.05f, Z0 + 0.1f, x + 0.42f, 0.25f, Z0 + 0.3f, _redPaint, false);

            var hinge = new GameObject("Locker hinge").transform;
            hinge.SetParent(_root, false);
            hinge.localPosition = new Vector3(x, 0f, Z0);
            Transform keep = _root;
            _root = hinge;
            OpenLocker = Box("Locker door", 0f, 0.02f, -0.02f, 0.55f, H - 0.02f, 0f, _green, true);
            Box("Locker vents", 0.12f, 1.55f, -0.021f, 0.43f, 1.75f, -0.02f, _dark, false);
            Box("Locker handle", 0.42f, 0.95f, -0.05f, 0.46f, 1.1f, -0.02f, _metalGrey, false);
            _root = keep;
            LockerHinge = hinge;
            hinge.localRotation = Quaternion.Euler(0f, 12f, 0f); // open a crack: the one that isn't rusted shut
            Lockers.Add(OpenLocker);

            // The battery: a big old lantern battery on the shelf.
            var bat = new GameObject("Battery");
            bat.transform.SetParent(_root, false);
            bat.transform.localPosition = new Vector3(x + 0.27f, 1.45f, 39.58f);
            _root = bat.transform;
            Box("Battery body", -0.055f, 0f, -0.055f, 0.055f, 0.11f, 0.055f, FTex.Plain(new Color(0.85f, 0.45f, 0.08f), 0.4f), false);
            Box("Battery top", -0.055f, 0.11f, -0.055f, 0.055f, 0.12f, 0.055f, _dark, false);
            Box("Battery terminal", -0.03f, 0.12f, -0.005f, -0.02f, 0.14f, 0.005f, _metalGrey, false);
            Box("Battery terminal", 0.02f, 0.12f, -0.005f, 0.03f, 0.14f, 0.005f, _metalGrey, false);
            _root = keep;
            var bc = bat.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.07f, 0f);
            bc.size = new Vector3(0.25f, 0.2f, 0.25f);
            BatteryProp = bat;
        }

        // "DAYS WITHOUT AN ACCIDENT: 14", on standoffs off the wall. Behind it: a hollow in the wall, and the file.
        private static void BuildAccidentBoard()
        {
            var board = new GameObject("Accident board");
            board.transform.SetParent(_root, false);
            AccidentBoardHome = new Vector3(13.79f, 2f, 3.5f);
            board.transform.localPosition = AccidentBoardHome;
            Transform keep = _root;
            _root = board.transform;
            Box("Board", -0.012f, -0.45f, -0.9f, 0.012f, 0.45f, 0.9f, _white, true);
            Sign("DAYS WITHOUT AN ACCIDENT", -0.016f, 0.3f, 0f, 90f, 0.1f, 2.4f, new Color(0.15f, 0.15f, 0.15f));
            Sign("14", -0.016f, -0.1f, 0f, 90f, 0.45f, 1f, new Color(0.62f, 0.05f, 0.05f));
            _root = keep;
            AccidentBoard = board;

            Box("Hollow in the wall", 13.846f, 1.75f, 3.2f, 13.859f, 2.25f, 3.8f, _void, false);
            var file = new GameObject("Mama file");
            file.transform.SetParent(_root, false);
            file.transform.localPosition = new Vector3(13.825f, 2f, 3.5f);
            _root = file.transform;
            Box("Folder", -0.015f, -0.2f, -0.15f, 0.015f, 0.2f, 0.15f, FTex.Plain(new Color(0.78f, 0.64f, 0.38f), 0.15f), true);
            Sign("CONFIDENTIAL", -0.017f, 0.08f, 0f, 90f, 0.035f, 0.28f, new Color(0.6f, 0.08f, 0.06f));
            Sign("P-1", -0.017f, -0.02f, 0f, 90f, 0.06f, 0.28f, new Color(0.15f, 0.12f, 0.1f));
            _root = keep;
            FileProp = file;
        }


        // ── Lights: the power's been out for decades. ─────────────────────────
        private static void Lights()
        {
            Color cool = new Color(0.86f, 0.93f, 1f), warm = new Color(1f, 0.93f, 0.78f), moon = new Color(0.5f, 0.6f, 0.92f);

            // Every fixture is still up there, dead. One tube on the assembly floor still stutters.
            Fixture(10.5f, LOW - 0.02f, 7.5f, false, false, false, warm, 0f, 0f, true);
            Fixture(4f, LOW - 0.02f, 6f, false, false, false, warm, 0f, 0f, true);
            Fixture(4f, LOW - 0.02f, 16.5f, false, false, false, warm, 0f, 0f, true);
            Fixture(10f, LOW - 0.02f, 23.5f, false, false, false, warm, 0f, 0f, true);
            Fixture(4.5f, LOW - 0.02f, 32f, false, false, false, warm, 0f, 0f, true);
            Fixture(10f, LOW - 0.02f, 36f, false, false, false, warm, 0f, 0f, true);
            Fixture(22f, LOW - 0.02f, 5f, false, false, false, cool, 0f, 0f, true);
            Fixture(37f, LOW - 0.02f, 6.5f, false, false, false, cool, 0f, 0f, true);
            float[] ax = { 18.5f, 25.5f, 32.5f, 39.5f }, az = { 15f, 23f, 31f, 38f };
            for (int i = 0; i < ax.Length; i++)
                for (int j = 0; j < az.Length; j++)
                {
                    int k = i * 4 + j;
                    if (k == 6 || k == 3 || k == 5) continue;   // fell, missing, or under the roof hole
                    bool glitch = k == 9;                        // (32.5, 23): how does it still have power?
                    Fixture(ax[i], 5.6f, az[j], glitch, glitch, false, cool, 1.1f, 11f, false);
                }
            float[] wz = { 5f, 12.5f, 20f, 27.5f, 35f };
            for (int j = 0; j < wz.Length; j++)
            {
                Fixture(47.5f, 5.6f, wz[j], false, false, false, cool, 0f, 0f, false);
                if (j != 4) Fixture(55f, 5.6f, wz[j], false, false, false, cool, 0f, 0f, false);
            }

            // Your car's headlights, on the front of the building and in through the broken door.
            if (!Town)
            {
                Spot("Headlight", new Vector3(CAR_X - 0.62f, 0.66f, CAR_Z + 2.2f), new Vector3(CAR_X - 1.8f, 0.2f, CAR_Z + 14f), 48f, 30f, 1.8f, warm, true, true);
                Spot("Headlight", new Vector3(CAR_X + 0.62f, 0.66f, CAR_Z + 2.2f), new Vector3(CAR_X - 0.2f, 0.2f, CAR_Z + 14f), 48f, 30f, 1.6f, warm, false, true);
            }

            // Moonlight: over the lot, and in through the boarded window and the holes in the roof.
            Spot("Moonlight (lot)", new Vector3(30f, 40f, -70f), new Vector3(28f, 0f, -12f), 50f, 120f, 0.5f, moon, true, false);
            Spot("Moonlight (lobby window)", new Vector3(11f, 2.4f, -3f), new Vector3(11.2f, 0.4f, 6.5f), 38f, 15f, 1.3f, moon, true, true);
            Spot("Moonlight (roof hole)", new Vector3(28f, 22f, 23f), new Vector3(28.5f, 0f, 26.8f), 16f, 30f, 1.8f, moon, true, true);
            Spot("Moonlight (warehouse hole)", new Vector3(51f, 20f, 29.5f), new Vector3(51.5f, 0f, 32f), 12f, 26f, 1.4f, moon, true, true);

            // The only thing in the building with power: R&D.
            PointLight("R&D warning light", new Vector3(36f, 2.95f, 3.45f), new Color(1f, 0.15f, 0.1f), 0.9f, 4f);
            Box("R&D warning lamp", 35.85f, 2.92f, 3.13f, 36.15f, 3.07f, 3.25f, _red, false);
        }

        private static void Spot(string name, Vector3 from, Vector3 at, float angle, float range, float intensity, Color c, bool shadows, bool volumetric)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = from;
            go.transform.localRotation = Quaternion.LookRotation(at - from);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.spotAngle = angle;
            l.range = range;
            l.intensity = intensity;
            l.color = c;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            if (volumetric)
            {
                try { go.AddComponent<HxVolumetricLight>(); } // dusty beams (the game's own volumetric lighting)
                catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Factory] No volumetric light: " + e.Message); }
            }
        }

        private static Light PointLight(string name, Vector3 local, Color c, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = local;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range;
            return l;
        }

        private static void Fixture(float x, float y, float z, bool on, bool glitch, bool shadows, Color color, float intensity, float range, bool flush)
        {
            Box("Lamp housing", x - 0.75f, y - 0.1f, z - 0.2f, x + 0.75f, y + 0.02f, z + 0.2f, _dark, false);
            GameObject tube = Box("Lamp tube", x - 0.7f, y - 0.13f, z - 0.13f, x + 0.7f, y - 0.1f, z + 0.13f, on ? _tubeOn : _tubeOff, false);
            if (!flush)
            {
                Box("Lamp chain", x - 0.6f, y + 0.02f, z - 0.01f, x - 0.58f, HIGH - 0.7f, z + 0.01f, _dark, false);
                Box("Lamp chain", x + 0.58f, y + 0.02f, z - 0.01f, x + 0.6f, HIGH - 0.7f, z + 0.01f, _dark, false);
            }
            if (!on) return;
            var l = PointLight("Lamp light", new Vector3(x, y - 0.3f, z), color, intensity, range);
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            if (glitch) _flickers.Add(new Flicker { L = l, Tube = tube.GetComponent<Renderer>(), Base = intensity, Next = (float)_rng.NextDouble() * 2f });
        }

        /// <summary>After you read the file: the one tube that still had power dies for good.</summary>
        public static void KillFlickers()
        {
            foreach (var f in _flickers)
            {
                if (f.L != null) f.L.intensity = 0f;
                if (f.Tube != null) f.Tube.sharedMaterial = _tubeOff.Mat;
            }
            _flickers.Clear();
        }

        /// <summary>Glitching tubes: on for a while, then a stutter of quick blinks, sometimes dead for a few seconds.</summary>
        public static void Tick(float dt)
        {
            foreach (var f in _flickers)
            {
                if (f.L == null) continue;
                f.Next -= dt;
                if (f.Next > 0f) continue;
                if (f.Burst > 0)
                {
                    f.Burst--;
                    f.Off = !f.Off;
                    f.Next = 0.03f + (float)_rng.NextDouble() * 0.09f;
                }
                else if (f.Off)
                {
                    f.Off = false;                                              // back on, for a while
                    f.Next = 0.6f + (float)_rng.NextDouble() * 4f;
                }
                else if (_rng.NextDouble() < 0.7)
                {
                    f.Burst = 3 + _rng.Next(6);                                 // a stutter
                    f.Off = true;
                    f.Next = 0.05f;
                }
                else
                {
                    f.Off = true;                                               // dead for a bit
                    f.Next = 0.8f + (float)_rng.NextDouble() * 2.5f;
                }
                f.L.intensity = f.Off ? 0f : f.Base * (0.85f + (float)_rng.NextDouble() * 0.15f);
                if (f.Tube != null) f.Tube.sharedMaterial = f.Off ? _tubeOff.Mat : _tubeOn.Mat;
            }
        }
    }
}
