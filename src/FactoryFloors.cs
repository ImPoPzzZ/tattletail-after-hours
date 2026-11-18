using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// The rest of the Waygetter factory: the stairs, the upstairs offices and catwalk (floor y 4),
    /// the basement (floor y -4: generator, boiler room, storage, the reject pit, R&amp;D) and the
    /// testing lab's insides. Same place-local meters as FactoryLayout.
    ///
    ///   upstairs (over the west block, x 0-14):  conference (z 0-8), manager (x 0-9, z 8-20), PA room (x 9-14, z 8-20),
    ///                                            hall (x 6-14, z 20-28) + stair landing, foreman (x 0-7, z 28-40), records (x 7-14, z 28-40);
    ///                                            catwalk along the assembly floor's west and north walls
    ///   basement:  stairs (x 0-6, z 18.6-27), corridor (x 6-30, z 18.6-20.6), generator (x 0-12, z 8-18.6),
    ///              boiler (x 12-24, z 8-18.6), storage (x 0-24, z 0-8), reject pit (x 30-42, z 12-24, pit floor y -6),
    ///              R&amp;D lab (x 24-42, z 0-12) with the prototype chamber (x 36-42, z 5-12)
    /// </summary>
    public static partial class FactoryLayout
    {
        public const float UP = 4f, UP_CEIL = 7.2f, DOWN = -4f, PIT = -6f;

        private static void Floors()
        {
            Stairs();
            Upstairs();
            Catwalk();
            Basement();
            RejectPit();
            RnD();
            TestingLab();
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        // A straight flight of steps: from 'start' (on the floor where it begins) along an axis direction,
        // 'run' meters long, climbing 'rise' (negative goes down). Walkable by an invisible ramp under the noses.
        private static void Flight(string name, Vector3 start, Vector3 dir, float width, float run, float rise, FMat m)
        {
            int n = Mathf.Max(2, Mathf.RoundToInt(run / 0.26f));
            Vector3 side = new Vector3(dir.z, 0f, -dir.x);
            for (int i = 0; i < n; i++)
            {
                float t0 = run * i / n, t1 = run * (i + 1) / n;
                float top = start.y + rise * (i + 1) / n; // each tread one step higher (or lower, going down)
                Vector3 a = start + dir * t0 - side * (width * 0.5f), b = start + dir * t1 + side * (width * 0.5f);
                float bottom = top - 0.22f;
                Box(name + " step", Mathf.Min(a.x, b.x), bottom, Mathf.Min(a.z, b.z), Mathf.Max(a.x, b.x), top, Mathf.Max(a.z, b.z), m, false);
            }
            // The ramp you actually walk on.
            Vector3 end = start + dir * run + Vector3.up * rise;
            var ramp = new GameObject(name + " (ramp)");
            ramp.transform.SetParent(_root, false);
            ramp.transform.localRotation = Quaternion.LookRotation(end - start);
            Vector3 up = ramp.transform.localRotation * Vector3.up;
            ramp.transform.localPosition = (start + end) * 0.5f - up * 0.05f;
            ramp.AddComponent<BoxCollider>().size = new Vector3(width, 0.1f, (end - start).magnitude + 0.1f);
        }

        // A railing with an invisible wall behind it (so nobody falls or climbs over).
        private static void Rail(float x0, float z0, float x1, float z1, float y) { Rail(x0, z0, x1, z1, y, 2.4f); }

        private static void Rail(float x0, float z0, float x1, float z1, float y, float block)
        {
            bool alongX = Mathf.Abs(x1 - x0) > Mathf.Abs(z1 - z0);
            float len = alongX ? Mathf.Abs(x1 - x0) : Mathf.Abs(z1 - z0);
            for (float d = 0f; d <= len + 0.01f; d += 1.2f)
            {
                float px = alongX ? Mathf.Min(x0, x1) + d : x0, pz = alongX ? z0 : Mathf.Min(z0, z1) + d;
                Box("Rail post", px - 0.025f, y, pz - 0.025f, px + 0.025f, y + 1.05f, pz + 0.025f, _yellowPaint, false);
            }
            if (alongX)
            {
                Box("Rail", Mathf.Min(x0, x1), y + 1.0f, z0 - 0.03f, Mathf.Max(x0, x1), y + 1.06f, z0 + 0.03f, _yellowPaint, false);
                Box("Rail", Mathf.Min(x0, x1), y + 0.5f, z0 - 0.02f, Mathf.Max(x0, x1), y + 0.54f, z0 + 0.02f, _yellowPaint, false);
                BlockerAt(Mathf.Min(x0, x1), y, z0 - 0.05f, Mathf.Max(x0, x1), y + block, z0 + 0.05f);
            }
            else
            {
                Box("Rail", x0 - 0.03f, y + 1.0f, Mathf.Min(z0, z1), x0 + 0.03f, y + 1.06f, Mathf.Max(z0, z1), _yellowPaint, false);
                Box("Rail", x0 - 0.02f, y + 0.5f, Mathf.Min(z0, z1), x0 + 0.02f, y + 0.54f, Mathf.Max(z0, z1), _yellowPaint, false);
                BlockerAt(x0 - 0.05f, y, Mathf.Min(z0, z1), x0 + 0.05f, y + block, Mathf.Max(z0, z1));
            }
        }

        private static void BlockerAt(float x0, float y0, float z0, float x1, float y1, float z1)
        {
            var go = new GameObject("Invisible wall");
            go.transform.SetParent(_root, false);
            go.transform.localPosition = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
            go.AddComponent<BoxCollider>().size = new Vector3(Mathf.Max(0.05f, x1 - x0), y1 - y0, Mathf.Max(0.05f, z1 - z0));
        }

        private static void Desk(float x, float z, float yaw, FMat top)
        {
            var d = new GameObject("Desk").transform;
            d.SetParent(_root, false);
            d.localPosition = new Vector3(x, _floorY, z);
            d.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Transform keep = _root;
            _root = d;
            Box("Desk top", -0.75f, 0.72f, -0.4f, 0.75f, 0.77f, 0.4f, top, true);
            Box("Desk drawers", 0.3f, 0f, -0.38f, 0.72f, 0.72f, 0.38f, _darkwood, true);
            Box("Desk leg", -0.72f, 0f, -0.38f, -0.66f, 0.72f, 0.38f, _darkwood, false);
            _root = keep;
        }

        private static float _floorY; // the floor level props are being placed on

        // ── Stairs (ground floor x 0-6, z 20-27): one flight up along the west wall, one down beside it ──
        private static void Stairs()
        {
            // Both flights start from the strip along the south wall (z 20-21.3), reached from the door, and run north.
            Flight("Stairs up", new Vector3(1.0f, 0f, 21.2f), Vector3.forward, 1.4f, 5.3f, UP, _concrete);
            Flight("Stairs down", new Vector3(2.8f, 0f, 21.3f), Vector3.forward, 1.4f, 5.2f, DOWN, _concrete);
            Rail(2.1f, 21.3f, 2.1f, 26.5f, 0f, 4.6f);   // between the two flights (tall: the up flight climbs beside it)
            Rail(3.5f, 21.3f, 3.5f, 26.5f, 0f, 2.4f);   // along the hole going down
            Rail(2.1f, 26.5f, 3.5f, 26.5f, 0f, 2.4f);   // its far end
            RoomSign("UP: OFFICES", 1f, 2.3f, 20.16f, 180f, 1.3f);
            RoomSign("DOWN: BASEMENT", 2.8f, 2.6f, 20.16f, 180f, 1.5f);
        }

        // ── Upstairs: the offices over the west block, y 4 to 7.2 ─────────────
        private static void Upstairs()
        {
            FMat carpet = FTex.Get("fabric", 1.2f, new Color(0.34f, 0.3f, 0.26f), 0.03f, 0f, 0.8f);
            // Floor slab (3.6-4.0) with the stairwell left open (x 0-6, z 20-26.5).
            Box("Upstairs floor", 0f, UP - 0.4f, 0f, 14f, UP, 20f, carpet, true);
            Box("Upstairs floor", 6f, UP - 0.4f, 20f, 14f, UP, 26.5f, _lobbyTile, true);
            Box("Upstairs floor", 0f, UP - 0.4f, 26.5f, 14f, UP, 28f, _lobbyTile, true);
            Box("Upstairs floor", 0f, UP - 0.4f, 28f, 14f, UP, D, carpet, true);
            Box("Upstairs ceiling", 0f, UP_CEIL, 0f, 14f, UP_CEIL + 0.08f, D, _ceiling, true);

            // The stairwell's own walls, full height (the void in the middle is open to the ground floor).
            WallX("Stairwell wall (upper)", 20f, 0f, 14f, LOW, UP, _block, None);
            WallX("Upstairs south wall", 20f, 0f, 14f, UP, UP_CEIL, _block, 7.2f, 8.4f, UP + DOOR_TOP, 11f, 12.2f, UP + DOOR_TOP);
            WallZ("Stairwell wall (upper)", 6f, 20f, 26.5f, LOW, UP_CEIL, _block, None);
            Rail(1.7f, 26.5f, 6f, 26.5f, UP);  // the landing's edge over the stairwell

            // Hall north wall with the foreman's and records doors; the wall between those two offices.
            WallX("Upstairs north wall", 28f, 0f, 14f, UP, UP_CEIL, _block, 2.4f, 3.6f, UP + DOOR_TOP, 9.6f, 10.8f, UP + DOOR_TOP);
            WallZ("Office wall", 7f, 28f, D, UP, UP_CEIL, _block, None);
            // South offices: manager (x 0-9) and PA room (x 9-14), and the conference room behind them (z 0-8).
            WallZ("Office wall", 9f, 8f, 20f, UP, UP_CEIL, _block, None);
            WallX("Conference wall", 8f, 0f, 14f, UP, UP_CEIL, _block, 11.4f, 12.6f, UP + DOOR_TOP);

            // Door plates.
            RoomSign("FOREMAN", 3f, UP + 2.6f, 27.84f, 0f, 1.2f);
            RoomSign("RECORDS", 10.2f, UP + 2.6f, 27.84f, 0f, 1.2f);
            RoomSign("MANAGER", 7.8f, UP + 2.6f, 20.16f, 180f, 1.2f);
            RoomSign("PA / SECURITY", 11.6f, UP + 2.6f, 20.16f, 180f, 1.6f);
            RoomSign("CONFERENCE", 12f, UP + 2.6f, 8.16f, 180f, 1.4f);
            RoomSign("CATWALK", 13.84f, UP + 2.6f, 24.6f, 90f, 1.2f);

            _floorY = UP;
            // Foreman's office (x 0-7, z 28-40): his desk, a cot, a filing cabinet.
            Desk(3.5f, 36.5f, 0f, _wood);
            Chair(3.5f, 37.4f, 0f);
            Lift(Box("Cot", 0.3f, 0f, 29f, 1.1f, 0.45f, 31.2f, FTex.Plain(new Color(0.3f, 0.34f, 0.26f), 0.1f), true));
            Lift(Box("Filing cabinet", 6.2f, 0f, 38.6f, 6.85f, 1.35f, 39.8f, _metalGrey, true));
            // Records (x 7-14, z 28-40): rows of cabinets and boxes of paper.
            for (int i = 0; i < 4; i++)
            {
                Lift(Box("Records cabinet", 7.6f + i * 1.5f, 0f, 39.2f, 8.8f + i * 1.5f, 1.4f, 39.85f, _metalGrey, true));
                Lift(Box("Records cabinet", 7.6f + i * 1.5f, 0f, 33f, 8.8f + i * 1.5f, 1.4f, 33.65f, _metalGrey, true));
            }
            for (int i = 0; i < 6; i++) Lift(Box("Archive box", 8f + i * 0.9f, 0f, 30f + (i % 2) * 0.4f, 8.55f + i * 0.9f, 0.35f, 30.45f + (i % 2) * 0.4f, _cardboard, true));
            // Manager (x 0-9, z 8-20): the big desk, a safe, a couch.
            Desk(4.5f, 13f, 0f, _darkwood);
            Chair(4.5f, 13.9f, 0f);
            Lift(Box("Safe", 0.3f, 0f, 18.8f, 1.1f, 0.9f, 19.7f, FTex.Plain(new Color(0.18f, 0.2f, 0.18f), 0.5f), true));
            Lift(Box("Couch", 6.8f, 0f, 9f, 8.6f, 0.45f, 11.8f, FTex.Plain(new Color(0.35f, 0.14f, 0.1f), 0.2f), true));
            // PA / security (x 9-14, z 8-20): the PA console and dead monitors.
            Lift(Box("PA console", 9.4f, 0f, 16f, 13.6f, 0.9f, 17f, _beige, true));
            for (int i = 0; i < 3; i++) Lift(Box("Monitor", 9.8f + i * 1.3f, 0.9f, 16.3f, 10.6f + i * 1.3f, 1.5f, 16.9f, _beige, false));
            Lift(Box("Microphone", 11.4f, 0.9f, 15.8f, 11.5f, 1.25f, 15.9f, _dark, false));
            // Conference (z 0-8): a long table.
            Lift(Box("Conference table", 2f, 0.72f, 3f, 12f, 0.78f, 5f, _darkwood, true));
            for (int i = 0; i < 5; i++) { Chair(3f + i * 2f, 2.4f, 180f); Chair(3f + i * 2f, 5.6f, 0f); }
            _floorY = 0f;
        }

        // Things built with Box at floor height 0 go up to wherever _floorY is.
        private static void Lift(GameObject go) { go.transform.localPosition += Vector3.up * _floorY; }

        // ── The catwalk: along the assembly floor's west and north walls, at y 4 ─
        private static void Catwalk()
        {
            Box("Catwalk", 14.13f, UP - 0.08f, 10.2f, 15.6f, UP, 39.87f, _plate, true);
            Box("Catwalk", 14.13f, UP - 0.08f, 38.4f, 41.87f, UP, 39.87f, _plate, true);
            Rail(15.6f, 10.2f, 15.6f, 38.4f, UP);
            Rail(15.6f, 38.4f, 41.87f, 38.4f, UP);
            Rail(14.13f, 10.2f, 15.6f, 10.2f, UP);
            Rail(41.87f, 38.4f, 41.87f, 39.87f, UP);
            for (float z = 12f; z < 38f; z += 4f) Box("Catwalk hanger", 15.5f, UP, z - 0.02f, 15.54f, HIGH - 0.7f, z + 0.02f, _dark, false);
        }

        // ── The basement: y -4 to -0.2 ─────────────────────────────────────
        private static void Basement()
        {
            FMat bconc = FTex.Get("concrete", 3.5f, new Color(0.4f, 0.39f, 0.36f), 0.3f, 0f, 1.3f);
            FMat bwall = FTex.Get("block", 2.4f, new Color(0.42f, 0.41f, 0.38f), 0.08f, 0f, 1.4f);
            const float C = -0.2f; // ceiling: the underside of the ground floor

            // Floors.
            Box("Basement floor", 0f, DOWN - 0.2f, 0f, 30f, DOWN, 20.6f, bconc, true);
            Box("Basement floor", 0f, DOWN - 0.2f, 20.6f, 6f, DOWN, 27f, bconc, true);
            Box("Basement floor", 24f, DOWN - 0.2f, 0f, 42f, DOWN, 12f, bconc, true);

            // Outer walls.
            WallZ("Basement wall", 0f, 0f, 27f, DOWN, C, bwall, None);
            WallX("Basement wall", 0f, 0f, 42f, DOWN, C, bwall, None);
            WallX("Basement wall", 27f, 0f, 6f, DOWN, C, bwall, None);
            WallZ("Basement wall", 6f, 20.6f, 27f, DOWN, C, bwall, None);
            WallX("Basement wall", 20.6f, 6f, 30f, DOWN, C, bwall, None);
            WallZ("Basement wall", 42f, 0f, 24f, DOWN, C, bwall, None);
            WallX("Basement wall", 24f, 30f, 42f, DOWN, C, bwall, None);

            // Inner walls: corridor (z 18.6-20.6), generator / boiler / storage, the pit and R&D.
            WallX("Corridor wall", 18.6f, 0f, 30f, DOWN, C, bwall, 3f, 4.2f, DOWN + DOOR_TOP, 8f, 9.4f, DOWN + DOOR_TOP, 16f, 17.4f, DOWN + DOOR_TOP);
            WallZ("Generator / boiler wall", 12f, 8f, 18.6f, DOWN, C, bwall, None);
            WallX("Storage wall", 8f, 0f, 24f, DOWN, C, bwall, 3f, 4.2f, DOWN + DOOR_TOP);
            WallZ("Storage / R&D wall", 24f, 0f, 18.6f, DOWN, C, bwall, None);
            WallZ("Pit wall", 30f, 12f, 24f, DOWN, C, bwall, 18.8f, 20.4f, DOWN + DOOR_TOP);
            WallZ("Corridor end", 30f, 20.6f, 24f, DOWN, C, bwall, None);
            WallX("R&D north wall", 12f, 24f, 42f, DOWN, C, bwall, None);
            Box("Corridor filler", 24f, DOWN, 12f, 30f, C, 18.6f, bwall, true); // solid rock between the boiler room and the pit

            // Signs.
            RoomSign("GENERATOR", 8.7f, DOWN + 2.6f, 18.76f, 0f, 1.4f);
            RoomSign("BOILER ROOM", 16.7f, DOWN + 2.6f, 18.76f, 0f, 1.5f);
            RoomSign("STORAGE", 3.6f, DOWN + 2.6f, 8.16f, 180f, 1.2f);
            RoomSign("REJECTS", 29.84f, DOWN + 2.6f, 19.6f, 90f, 1.2f);

            // Pipes along the corridor ceiling, a few lamps (dead).
            Box("Pipe", 6f, C - 0.35f, 19f, 30f, C - 0.23f, 19.12f, _redPaint, false);
            Box("Pipe", 6f, C - 0.35f, 20.1f, 30f, C - 0.23f, 20.22f, _metalGrey, false);
            for (float x = 9f; x < 30f; x += 6f) Box("Cage lamp", x - 0.12f, C - 0.25f, 19.5f, x + 0.12f, C - 0.05f, 19.7f, _dark, false);

            _floorY = DOWN;
            // Generator room (x 0-12, z 8-18.6).
            Lift(Box("Generator", 2f, 0f, 11f, 7f, 1.9f, 14.2f, FTex.Get("green", 1.5f, new Color(0.6f, 0.65f, 0.55f), 0.35f, 0.3f, 1f), true));
            Lift(Box("Generator exhaust", 3f, 1.9f, 12f, 3.4f, 3.7f, 12.4f, _dark, false));
            Lift(Box("Fuel drum", 9f, 0f, 9f, 9.7f, 0.9f, 9.7f, _redPaint, true));
            Lift(Box("Breaker panel", 11.8f, 1.1f, 15f, 11.87f, 2.2f, 16.4f, _metalGrey, false));
            Lift(Box("Breaker lever", 11.7f, 1.5f, 15.6f, 11.8f, 1.8f, 15.7f, _redPaint, false));
            // Boiler room (x 12-24, z 8-18.6).
            var boiler = Cyl("Boiler", 18f, DOWN, 12.5f, 1.4f, 3.4f, _metalGrey, true);
            boiler.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            boiler.transform.localPosition = new Vector3(18f, DOWN + 1.5f, 13.5f);
            Lift(Box("Boiler pipe", 17.8f, 1.5f, 15.6f, 18.2f, 3.8f, 16f, _redPaint, false));
            // Storage (x 0-24, z 0-8): shelves of parts.
            for (int i = 0; i < 4; i++)
            {
                float x = 2f + i * 5.5f;
                Lift(Box("Shelf", x, 0f, 1f, x + 3.5f, 2.2f, 1.6f, _orange, true));
                Lift(Box("Shelf", x, 0f, 5.6f, x + 3.5f, 2.2f, 6.2f, _orange, true));
                for (int j = 0; j < 4; j++) Lift(Box("Parts crate", x + 0.1f + j * 0.85f, 2.2f, 1.05f, x + 0.8f + j * 0.85f, 2.6f, 1.55f, _cardboard, false));
            }
            _floorY = 0f;
        }

        // ── The reject pit (x 30-42, z 12-24): a ledge along the west wall, and the pit ──
        private static void RejectPit()
        {
            FMat bconc = FTex.Get("concrete", 3.5f, new Color(0.34f, 0.33f, 0.3f), 0.3f, 0f, 1.3f);
            Box("Pit ledge", 30f, DOWN - 0.2f, 12f, 32f, DOWN, 24f, bconc, true);
            Box("Pit floor", 32f, PIT - 0.2f, 12f, 42f, PIT, 24f, bconc, true);
            Box("Pit edge", 32f, PIT, 12f, 32.2f, DOWN, 24f, bconc, true);
            WallZ("Pit wall", 42f, 12f, 24f, PIT, DOWN, bconc, None);
            WallX("Pit wall", 24f, 32f, 42f, PIT, DOWN, bconc, None);
            WallX("Pit wall", 12f, 32f, 42f, PIT, DOWN, bconc, None);
            Rail(32.1f, 12f, 32.1f, 24f, DOWN);
            // The chute from the inspection station comes down through the ceiling.
            Box("Chute", 35f, -2.6f, 17f, 36.2f, -0.2f, 18.2f, _hazard, false);
            // Everything that ever failed inspection.
            for (int i = 0; i < 26; i++)
            {
                float x = 33f + (float)_rng.NextDouble() * 8.5f, z = 13f + (float)_rng.NextDouble() * 10f;
                float heap = Mathf.Max(0f, 1.2f - new Vector2(x - 35.6f, z - 17.6f).magnitude * 0.25f);
                Tattletail(new Vector3(x, PIT + heap, z), (float)_rng.NextDouble() * 360f, -1, (float)_rng.NextDouble() * 180f - 90f);
            }
            for (int i = 0; i < 20; i++)
            {
                float x = 33f + (float)_rng.NextDouble() * 8.5f, z = 13f + (float)_rng.NextDouble() * 10f;
                FGeo.Box(_root, "Broken box", new Vector3(x, PIT + 0.2f, z), new Vector3(0.5f, 0.35f, 0.4f), _cardboard, false)
                    .transform.localRotation = Quaternion.Euler((float)_rng.NextDouble() * 60f, (float)_rng.NextDouble() * 360f, 0f);
            }
        }

        // ── R&D: stairs down behind the R&D door, the lab, the prototype chamber ──
        private static void RnD()
        {
            FMat lab = FTex.Get("lobby", 1.6f, new Color(0.7f, 0.7f, 0.72f), 0.5f, 0f, 0.6f);
            Flight("R&D stairs", new Vector3(35.2f, 0f, 1f), Vector3.left, 1.4f, 6f, DOWN, _plate);
            Rail(31.5f, 1.75f, 35.2f, 1.75f, 0f);
            Rail(31.5f, 0.25f, 31.5f, 1.75f, 0f);
            Box("R&D lab floor", 24f, DOWN, 0f, 42f, DOWN + 0.01f, 12f, lab, false);
            Box("R&D lab ceiling", 24f, -0.45f, 2.2f, 42f, -0.4f, 12f, _ceiling, false);

            _floorY = DOWN;
            // Workbenches with early designs: sketches, a half-built Tattletail, spare eyes.
            Lift(Box("Workbench", 25f, 0.9f, 4f, 28.5f, 0.96f, 5.2f, _plate, true));
            Lift(Box("Workbench", 25f, 0.9f, 8f, 28.5f, 0.96f, 9.2f, _plate, true));
            Lift(Box("Workbench legs", 25.1f, 0f, 4.1f, 28.4f, 0.9f, 5.1f, _dark, false));
            Lift(Box("Workbench legs", 25.1f, 0f, 8.1f, 28.4f, 0.9f, 9.1f, _dark, false));
            for (int i = 0; i < 6; i++) Lift(Box("Sketch", 25.2f + i * 0.5f, 0.96f, 4.3f + (i % 2) * 0.3f, 25.6f + i * 0.5f, 0.965f, 4.6f + (i % 2) * 0.3f, _paper, false));
            Tattletail(new Vector3(26.5f, DOWN + 0.96f, 8.6f), 30f, 0, 0f);
            Lift(Box("Blackboard", 24.14f, 1.2f, 3f, 24.18f, 2.4f, 9f, FTex.Plain(new Color(0.1f, 0.13f, 0.11f), 0.2f), false));
            Sign("P-1  PARENT UNIT", 24.2f, DOWN + 2.1f, 6f, -90f, 0.2f, 4f, new Color(0.85f, 0.85f, 0.8f));
            Sign("\"SHE KEEPS THEM GOOD\"", 24.2f, DOWN + 1.7f, 6f, -90f, 0.13f, 4f, new Color(0.8f, 0.8f, 0.75f));

            // The prototype chamber (x 36-42, z 5-12): a glass wall, a terminal, something under a sheet.
            WallZ("Chamber wall", 36f, 5f, 12f, DOWN, DOWN + 1f, _metalGrey, None);
            Box("Chamber glass", 35.97f, DOWN + 1f, 5f, 36.03f, -0.4f, 12f, _shard, true);
            WallX("Chamber wall", 5f, 36f, 42f, DOWN, -0.4f, _metalGrey, 38f, 39.2f, DOWN + DOOR_TOP);
            Lift(Box("Terminal", 34.4f, 0f, 9.5f, 35.4f, 1.1f, 10.3f, _beige, true));
            Lift(Box("Terminal screen", 34.5f, 1.1f, 9.6f, 35.3f, 1.6f, 9.62f, FTex.Glow(new Color(0.2f, 0.75f, 0.35f)), false));
            // Under the sheet: tall, with a head.
            Sheet(new Vector3(39.5f, DOWN, 9f));
            Lift(Box("Cables", 37f, 0f, 8.95f, 39f, 0.05f, 9.05f, _dark, false));
            _floorY = 0f;

            // R&D has power: a red light over the lab and the terminal's glow.
            PointLight("R&D lab light", new Vector3(30f, DOWN + 3f, 6f), new Color(1f, 0.18f, 0.12f), 0.9f, 9f);
            PointLight("Terminal glow", new Vector3(34.9f, DOWN + 1.5f, 9.3f), new Color(0.25f, 0.9f, 0.4f), 0.6f, 2.5f);
            PointLight("Chamber light", new Vector3(39.5f, DOWN + 3.3f, 8.5f), new Color(0.9f, 0.9f, 1f), 0.7f, 5f);
        }

        // A dust sheet draped over something tall with a big round head (ears poking up under the cloth),
        // pooling on the floor in folds. One mesh: rings of vertices from the top down, wrinkled.
        private static void Sheet(Vector3 at)
        {
            const int SEG = 40, RINGS = 34;
            const float TOP = 2.55f;
            var verts = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var tris = new System.Collections.Generic.List<int>();
            var rnd = new System.Random(66);
            float[] phase = new float[6];
            for (int k = 0; k < phase.Length; k++) phase[k] = (float)rnd.NextDouble() * 6.28f;
            for (int r = 0; r <= RINGS; r++)
            {
                float t = r / (float)RINGS;                 // 0 at the top, 1 on the floor
                float y = TOP * (1f - t);
                // The silhouette: a round head, a narrow neck, broad shoulders, then the cloth falls and spreads.
                float radius;
                if (t < 0.18f) radius = 0.42f * Mathf.Sin(Mathf.Clamp01(t / 0.18f) * Mathf.PI * 0.5f + 0.15f);
                else if (t < 0.26f) radius = Mathf.Lerp(0.42f, 0.36f, (t - 0.18f) / 0.08f);
                else if (t < 0.36f) radius = Mathf.Lerp(0.36f, 0.62f, (t - 0.26f) / 0.1f);
                else radius = Mathf.Lerp(0.62f, 0.95f, (t - 0.36f) / 0.64f);
                if (r == RINGS) radius += 0.12f; // the hem rests on the floor, a little wider
                for (int s = 0; s <= SEG; s++)
                {
                    float a = s / (float)SEG * Mathf.PI * 2f;
                    // Folds grow toward the hem; two bumps up top are the ears.
                    float fold = Mathf.Sin(a * 7f + phase[0]) * 0.05f + Mathf.Sin(a * 3f + phase[1]) * 0.07f + Mathf.Sin(a * 11f + phase[2]) * 0.025f;
                    float rr = radius * (1f + fold * Mathf.Clamp01((t - 0.3f) / 0.4f));
                    float ear = t < 0.12f ? Mathf.Max(0f, Mathf.Cos(a - 0.9f)) * Mathf.Max(0f, Mathf.Cos(a - 0.9f)) + Mathf.Max(0f, Mathf.Cos(a - 2.25f)) * Mathf.Max(0f, Mathf.Cos(a - 2.25f)) : 0f;
                    float yy = y + ear * 0.22f * (1f - t / 0.12f) + (t > 0.9f ? Mathf.Sin(a * 5f + phase[3]) * 0.03f : 0f);
                    verts.Add(at + new Vector3(Mathf.Cos(a) * rr, Mathf.Max(0.01f, yy) + (at.y - at.y), Mathf.Sin(a) * rr));
                    uvs.Add(new Vector2(s / (float)SEG * 3f, t * 3f));
                }
            }
            for (int r = 0; r < RINGS; r++)
                for (int s = 0; s < SEG; s++)
                {
                    int i0 = r * (SEG + 1) + s, i1 = i0 + 1, i2 = i0 + SEG + 1, i3 = i2 + 1;
                    tris.Add(i0); tris.Add(i1); tris.Add(i2);
                    tris.Add(i1); tris.Add(i3); tris.Add(i2);
                }
            // The very top: close it.
            int apex = verts.Count;
            verts.Add(at + Vector3.up * (TOP + 0.02f)); uvs.Add(new Vector2(0.5f, 0f));
            for (int s = 0; s < SEG; s++) { tris.Add(apex); tris.Add(s + 1); tris.Add(s); }
            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("Dust sheet");
            go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = FTex.Get("fabric", 0f, new Color(0.78f, 0.75f, 0.68f), 0.04f, 0f, 1.2f).Mat;
            var col = go.AddComponent<CapsuleCollider>();
            col.center = at + Vector3.up * 1.2f; col.radius = 0.55f; col.height = 2.5f;
        }

        // ── The testing lab (x 14-30, z 0-10): benches, the voice booth ─────
        private static void TestingLab()
        {
            Box("Voice booth", 26f, 0f, 2f, 26.1f, 2.4f, 6f, _metalGrey, true);
            Box("Voice booth", 26f, 0f, 2f, 29.8f, 2.4f, 2.1f, _metalGrey, true);
            Box("Voice booth window", 26f, 1f, 6f, 29.8f, 2.4f, 6.05f, _shard, true);
            Box("Voice booth", 26f, 0f, 6f, 28.8f, 1f, 6.1f, _metalGrey, true);
            Box("Voice booth foam", 29.6f, 0.2f, 2.2f, 29.8f, 2.3f, 5.9f, FTex.Plain(new Color(0.12f, 0.12f, 0.13f), 0.02f), false);
            Box("Voice booth stool", 27.8f, 0f, 3.8f, 28.2f, 0.6f, 4.2f, _dark, true);
            Tattletail(new Vector3(28.6f, 0.95f, 4f), -90f, 0, 0f);
            Box("Booth table", 28.2f, 0.9f, 3.4f, 29.4f, 0.95f, 4.6f, _plate, true);
            for (int i = 0; i < 3; i++)
            {
                float x = 15.5f + i * 3.2f;
                Box("Test bench", x, 0.9f, 3f, x + 2.4f, 0.96f, 4.2f, _plate, true);
                Box("Test bench legs", x + 0.1f, 0f, 3.1f, x + 2.3f, 0.9f, 4.1f, _dark, false);
                Box("Tester", x + 0.3f, 0.96f, 3.3f, x + 0.9f, 1.3f, 3.9f, _beige, false);
            }
            Tattletail(new Vector3(17.5f, 0.97f, 3.6f), 200f, 0, 0f);
            Papers(20f, 6f, 2f, 12);
            Box("Filing cabinet", 14.3f, 0f, 8.6f, 15f, 1.35f, 9.8f, _metalGrey, true);
        }
    }
}
