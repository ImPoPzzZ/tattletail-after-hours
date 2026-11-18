using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Everything on the Summer Vacation beach, placed from the marks the user and their friend
    /// laid down with the E tool ([BEACH-MARK], beach-local meters: +z is the sea, 2026-09-26):
    /// the beach bar (walls, a doorway, floor, thatch roof, the inside wall, kitchen and drinks
    /// counters, the storage room's crates, the EMPLOYEES ONLY sign, Mama working behind the
    /// counter), the gravel path, three umbrella tables with two seated Tattletails each, five
    /// towels with sunbathing Tattletails, and two sand castles with small Tattletails.
    /// Nothing here is interactive yet: the waiter game comes next.
    /// </summary>
    public static class BeachLayout
    {
        private static Transform _root;

        // ── Materials ────────────────────────────────────────────────────────
        private static Material _wood, _darkWood, _plank, _thatch, _counter, _white, _gravel, _castle, _pole;

        private static void MakeMaterials()
        {
            _wood = Mat.Lit(new Color(0.62f, 0.44f, 0.28f), 0.15f);
            _darkWood = Mat.Lit(new Color(0.42f, 0.28f, 0.17f), 0.15f);
            _plank = Mat.Lit(Color.white, 0.12f); _plank.mainTexture = PlankTexture();
            _thatch = Mat.Lit(new Color(0.78f, 0.66f, 0.38f), 0.05f);
            _counter = Mat.Lit(new Color(0.93f, 0.9f, 0.84f), 0.35f);
            _white = Mat.Lit(new Color(0.95f, 0.95f, 0.95f), 0.4f);
            _gravel = Mat.Lit(Color.white, 0.05f); _gravel.mainTexture = GravelTexture();
            _castle = Mat.Lit(new Color(0.78f, 0.66f, 0.46f), 0.05f);
            _pole = Mat.Lit(new Color(0.85f, 0.85f, 0.85f), 0.5f);
        }

        private static float Y(float x, float z) { return BeachController.Height(x, z); }

        public static void Build(Transform root)
        {
            _root = root;
            MakeMaterials();
            try { Bar(); } catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogError("KE: [Beach] Bar: " + e); }
            try { Paths(); } catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogError("KE: [Beach] Paths: " + e); }
            try { Tables(); } catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogError("KE: [Beach] Tables: " + e); }
            try { Towels(); } catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogError("KE: [Beach] Towels: " + e); }
            try { SandCastles(); } catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogError("KE: [Beach] Sand castles: " + e); }
            try { Palms(); } catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogError("KE: [Beach] Palms: " + e); }
        }

        // ── The beach bar ────────────────────────────────────────────────────
        public const float BAR_FLOOR = 0.22f, FLOOR_TOP = BAR_FLOOR + 0.1f, WALL_H = 2.8f, WALL_T = 0.15f;
        /// <summary>Heights above the bar floor: the bar counter's top (low enough to see the customers over it), the kitchen counter's top, a stool seat.</summary>
        public const float BAR_TOP = 0.77f, KITCHEN_TOP = 0.945f, SEAT = 0.57f;
        /// <summary>The customers' stools along the bar, and the kitchen counter's x (along the east wall).</summary>
        public static readonly float[] StoolX = { 15.15f, 15.95f, 16.7f };
        public const float KITCHEN_X = BX1 - 0.4f;
        /// <summary>The prep counter in front of the back shelf (cups, ice, blenders).</summary>
        public const float PREP_Z = -9.95f;
        /// <summary>Windows (sill and top heights above the floor) and the front door customers leave through.</summary>
        public const float SILL = 0.95f, WIN_TOP = 2.15f, EXIT_X0 = 15.75f, EXIT_X1 = 16.95f, EXIT_X = 16.35f;
        private const float WIN_W_Z0 = -5.2f, WIN_W_Z1 = -2.9f, WIN_E_Z0 = -3.4f, WIN_E_Z1 = -1.7f, WIN_N_X0 = 12.6f, WIN_N_X1 = 15.1f;
        private static Material _shutter;
        /// <summary>The topping counter along the west wall, past the door (away from the serving spots).</summary>
        public const float TOPPING_X = BX0 + 0.33f, TOPPING_Z0 = -9.1f, TOPPING_Z1 = -11.05f;
        /// <summary>Mama, and where she stands between her walks to judge your day.</summary>
        public static GameObject Mama;
        public static readonly Vector3 MamaHome = new Vector3(12.65f, 0f, -1.85f);
        public const float MAMA_HOME_YAW = 150f;
        // The outline: a clean rectangle fitted to the marked wall lines (drawn by hand, their ends
        // didn't meet: gaps at the corners). The west wall has the doorway where the path arrives.
        public const float BX0 = 11.9f, BX1 = 17.15f, BZ0 = -12.2f, BZ1 = -0.95f, DOOR_Z0 = -8.9f, DOOR_Z1 = -6.2f;
        public const float COUNTER_Z = -7.85f, SHELF_Z = -10.45f;

        private static void Bar()
        {
            float ft = FLOOR_TOP;
            float cx = (BX0 + BX1) / 2f, cz = (BZ0 + BZ1) / 2f, w = BX1 - BX0, d = BZ1 - BZ0;

            var floor = Box("BarFloor", new Vector3(cx, BAR_FLOOR, cz), new Vector3(w + WALL_T, 0.2f, d + WALL_T), _plank);
            floor.GetComponent<Renderer>().material.mainTextureScale = new Vector2(w / 2f, d / 2f);

            // Walls: straight, meeting at the corners; posts cover every joint. Open windows (a sill, shutters
            // outside, an invisible pane so nobody climbs through) and a front door: the customers' way out.
            _shutter = Mat.Lit(new Color(0.2f, 0.72f, 0.75f), 0.3f);
            WallAlongZ(BX0, BZ0, DOOR_Z0, ft);
            OpenWall(false, BX0, DOOR_Z1, BZ1, ft, -1f, new Gap(WIN_W_Z0, WIN_W_Z1, SILL, WIN_TOP));
            OpenWall(false, BX1, BZ0, BZ1, ft, 1f, new Gap(WIN_E_Z0, WIN_E_Z1, SILL, WIN_TOP));
            WallAlongX(BZ0, BX0, BX1, ft);
            OpenWall(true, BZ1, BX0, BX1, ft, 1f, new Gap(WIN_N_X0, WIN_N_X1, SILL, WIN_TOP), new Gap(EXIT_X0, EXIT_X1, 0f, WALL_H - 0.5f));
            var lintel = Box("DoorBeam", new Vector3(BX0, ft + WALL_H - 0.25f, (DOOR_Z0 + DOOR_Z1) / 2f), new Vector3(WALL_T + 0.02f, 0.5f, DOOR_Z1 - DOOR_Z0), Boards(DOOR_Z1 - DOOR_Z0));
            Object.Destroy(lintel.GetComponent<Collider>());
            float[,] posts = { { BX0, BZ0 }, { BX0, BZ1 }, { BX1, BZ0 }, { BX1, BZ1 }, { BX0, DOOR_Z0 }, { BX0, DOOR_Z1 }, { EXIT_X0, BZ1 } };
            for (int i = 0; i < posts.GetLength(0); i++)
                Box("Post", new Vector3(posts[i, 0], ft + (WALL_H + 0.05f) / 2f, posts[i, 1]), new Vector3(0.26f, WALL_H + 0.05f, 0.26f), _darkWood);
            // A wall plate beam around the top.
            Box("TopBeamW", new Vector3(BX0, ft + WALL_H - 0.06f, cz), new Vector3(0.22f, 0.14f, d + 0.26f), _darkWood);
            Box("TopBeamE", new Vector3(BX1, ft + WALL_H - 0.06f, cz), new Vector3(0.22f, 0.14f, d + 0.26f), _darkWood);
            Box("TopBeamS", new Vector3(cx, ft + WALL_H - 0.06f, BZ0), new Vector3(w + 0.26f, 0.14f, 0.22f), _darkWood);
            Box("TopBeamN", new Vector3(cx, ft + WALL_H - 0.06f, BZ1), new Vector3(w + 0.26f, 0.14f, 0.22f), _darkWood);

            FrontRoom(ft);

            // The bar counter ("bar wall" mark): Mama serves over it; stools on the customers' side.
            BarCounter(BX1 - WALL_T / 2f, COUNTER_Z, 13.75f, COUNTER_Z, ft);
            foreach (float x in StoolX)
                Stool(new Vector3(x, ft, COUNTER_Z + 0.65f));

            // The kitchen counter along the east wall; the back bar ("drinks" mark): shelves full of bottles.
            Counter(KITCHEN_X, COUNTER_Z - 0.4f, KITCHEN_X, SHELF_Z + 0.25f, ft, 0.6f); // the smoothie stations go on it (BeachShift)
            DrinksShelf(BX1 - WALL_T / 2f - 0.02f, SHELF_Z, 13.2f, SHELF_Z, ft);
            Counter(13.4f, PREP_Z, 16.2f, PREP_Z, ft, 0.5f);
            Counter(TOPPING_X, TOPPING_Z0, TOPPING_X, TOPPING_Z1, ft, 0.5f);

            // Storage room: six crates where the items will turn up.
            float[,] spawns = { { 16.31f, -10.94f }, { 16.28f, -11.44f }, { 16.25f, -11.92f }, { 15.60f, -11.78f }, { 15.66f, -11.38f }, { 15.73f, -10.86f } };
            for (int i = 0; i < spawns.GetLength(0); i++)
                Crate(new Vector3(spawns[i, 0], ft, Mathf.Max(spawns[i, 1], BZ0 + 0.3f)));

            GableRoof(ft + WALL_H, cx, cz, w, d);

            // Warm lamps inside (it's shaded under the roof).
            Lamp(new Vector3(14.5f, ft + WALL_H - 0.35f, -4.4f));
            Lamp(new Vector3(15.0f, ft + WALL_H - 0.35f, -9.1f));

            // Signs.
            Sign(new Vector3(12.55f, ft + 2.15f, SHELF_Z), 0f, "EMPLOYEES ONLY", new Color(0.9f, 0.15f, 0.15f), 1.1f, 0.28f);
            Sign(new Vector3(BX0 - 0.12f, ft + WALL_H - 0.35f, (DOOR_Z0 + DOOR_Z1) / 2f), 270f, "BEACH BAR", new Color(1f, 0.85f, 0.15f), 2.2f, 0.5f);
            Sign(new Vector3(cx, ft + WALL_H + 0.5f, BZ1 + 0.12f), 0f, "MAMA'S SMOOTHIES", new Color(0.95f, 0.3f, 0.55f), 3.2f, 0.55f); // on the front gable, facing the sea
            Awning(new Vector3((WIN_N_X0 + WIN_N_X1) / 2f, ft + WIN_TOP + 0.3f, BZ1 + 0.45f), WIN_N_X1 - WIN_N_X0 + 0.5f);
            Awning(new Vector3(EXIT_X, ft + WALL_H - 0.2f, BZ1 + 0.45f), EXIT_X1 - EXIT_X0 + 0.3f);

            // Mama, the boss: in the front room's back corner, where she sees the door, the line, the
            // stools and everything behind the counter. She slowly turns to keep you in sight.
            var mama = VersusBots.BuildMamaBody("KE_BeachMama");
            Mama = mama;
            if (mama != null)
            {
                mama.transform.SetParent(_root, false);
                mama.transform.localPosition = MamaHome + Vector3.up * ft; // clear of the walls and her board
                mama.transform.localRotation = Quaternion.Euler(0f, MAMA_HOME_YAW, 0f);
                mama.transform.localScale *= 1.35f;
                mama.AddComponent<StandOnFloor>().FloorY = _root.position.y + ft;
                mama.AddComponent<WatchPlayer>();
            }
        }

        private struct Gap
        {
            public float A, B, Sill, Top;
            public Gap(float a, float b, float sill, float top) { A = a; B = b; Sill = sill; Top = top; }
        }

        /// <summary>A wall along x (alongX) or z at 'at', from..to, with openings: windows (a sill) or doorways (sill 0). outside: which side is outdoors (+1/-1), for the shutters.</summary>
        private static void OpenWall(bool alongX, float at, float from, float to, float bottom, float outside, params Gap[] gaps)
        {
            float cursor = from;
            foreach (var g in gaps)
            {
                WallPiece(alongX, at, cursor, g.A, bottom, 0f, WALL_H);
                if (g.Sill > 0f) WallPiece(alongX, at, g.A, g.B, bottom, 0f, g.Sill);
                WallPiece(alongX, at, g.A, g.B, bottom, g.Top, WALL_H);
                if (g.Sill > 0f) WindowTrim(alongX, at, g, bottom, outside);
                cursor = g.B;
            }
            WallPiece(alongX, at, cursor, to, bottom, 0f, WALL_H);
        }

        private static void WallPiece(bool alongX, float at, float a, float b, float bottom, float y0, float y1)
        {
            float len = b - a, h = y1 - y0;
            if (len <= 0.01f || h <= 0.01f) return;
            Piece(alongX, at, (a + b) / 2f, bottom + (y0 + y1) / 2f, len, h, WALL_T, Boards(len), true);
        }

        /// <summary>A box lined up with a wall: 'along' is the position along the wall, 'depth' its thickness across it.</summary>
        private static GameObject Piece(bool alongX, float at, float along, float y, float length, float height, float depth, Material m, bool solid)
        {
            var go = Box("WallPart", alongX ? new Vector3(along, y, at) : new Vector3(at, y, along),
                alongX ? new Vector3(length, height, depth) : new Vector3(depth, height, length), m);
            if (!solid) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        private static void WindowTrim(bool alongX, float at, Gap g, float bottom, float outside)
        {
            float w = g.B - g.A, mid = (g.A + g.B) / 2f, h = g.Top - g.Sill, yMid = bottom + (g.Sill + g.Top) / 2f;
            Piece(alongX, at, mid, bottom + g.Sill - 0.025f, w + 0.14f, 0.05f, WALL_T + 0.16f, _darkWood, true);   // sill, sticking out both sides
            Piece(alongX, at, mid, bottom + g.Top + 0.03f, w + 0.14f, 0.06f, WALL_T + 0.06f, _darkWood, false);     // head
            foreach (float e in new[] { g.A, g.B })
                Piece(alongX, at, e, yMid, 0.06f, h, WALL_T + 0.06f, _darkWood, false);                              // jambs
            float off = outside * (WALL_T / 2f + 0.025f);
            Piece(alongX, at + off, g.A - 0.2f, yMid, 0.36f, h, 0.03f, _shutter, false);                            // shutters, open
            Piece(alongX, at + off, g.B + 0.2f, yMid, 0.36f, h, 0.03f, _shutter, false);
            var pane = Piece(alongX, at, mid, yMid, w, h, 0.05f, _darkWood, true);                                  // you can see through, not climb through
            Object.Destroy(pane.GetComponent<Renderer>());
        }

        /// <summary>The customers' room, Freezeria style: a checkered floor and painted lower walls with a trim rail.</summary>
        private static void FrontRoom(float ft)
        {
            float z0 = COUNTER_Z + 0.25f, w = BX1 - BX0, d = BZ1 - z0;
            var floor = Box("CheckerFloor", new Vector3((BX0 + BX1) / 2f, ft + 0.003f, (z0 + BZ1) / 2f), new Vector3(w - WALL_T, 0.006f, d - WALL_T / 2f), Mat.Lit(Color.white, 0.35f));
            Object.Destroy(floor.GetComponent<Collider>());
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            Color teal = new Color(0.35f, 0.78f, 0.78f), cream = new Color(0.98f, 0.96f, 0.9f);
            tex.SetPixels(new[] { teal, cream, cream, teal });
            tex.Apply();
            var fm = floor.GetComponent<Renderer>().sharedMaterial;
            fm.mainTexture = tex;
            fm.mainTextureScale = new Vector2(w / 0.8f, d / 0.8f); // 40 cm tiles

            var paint = Mat.Lit(new Color(0.3f, 0.68f, 0.72f), 0.25f);
            var trim = Mat.Lit(new Color(0.97f, 0.95f, 0.9f), 0.4f);
            float inset = WALL_T / 2f + 0.012f, hgt = SILL - 0.05f;
            Wainscot(false, BX0 + inset, DOOR_Z1 + 0.13f, BZ1 - 0.13f, ft, hgt, paint, trim);
            Wainscot(true, BZ1 - inset, BX0 + 0.13f, EXIT_X0 - 0.13f, ft, hgt, paint, trim);
            Wainscot(false, BX1 - inset, z0, BZ1 - 0.13f, ft, hgt, paint, trim);
        }

        private static void Wainscot(bool alongX, float at, float a, float b, float ft, float h, Material paint, Material trim)
        {
            Piece(alongX, at, (a + b) / 2f, ft + h / 2f, b - a, h, 0.02f, paint, false);
            Piece(alongX, at, (a + b) / 2f, ft + h + 0.02f, b - a, 0.04f, 0.04f, trim, false);
        }

        /// <summary>A striped awning sloping down and out over the front (toward the sea).</summary>
        private static void Awning(Vector3 at, float width)
        {
            var a = Box("Awning", at, new Vector3(width, 0.04f, 0.95f), Mat.Lit(Color.white, 0.3f));
            a.transform.localRotation = Quaternion.Euler(22f, 0f, 0f);
            var m = a.GetComponent<Renderer>().material;
            m.mainTexture = StripeTexture(new Color(0.95f, 0.3f, 0.45f), new Color(0.98f, 0.96f, 0.9f));
            m.mainTextureScale = new Vector2(width / 0.5f, 1f);
            Object.Destroy(a.GetComponent<Collider>());
        }

        private static void WallAlongZ(float x, float z0, float z1, float bottom)
        {
            float len = z1 - z0;
            Box("BarWall", new Vector3(x, bottom + WALL_H / 2f, (z0 + z1) / 2f), new Vector3(WALL_T, WALL_H, len), Boards(len));
        }

        private static void WallAlongX(float z, float x0, float x1, float bottom)
        {
            float len = x1 - x0;
            Box("BarWall", new Vector3((x0 + x1) / 2f, bottom + WALL_H / 2f, z), new Vector3(len, WALL_H, WALL_T), Boards(len));
        }

        /// <summary>Vertical wooden boards, tiled so every board is ~15 cm wide on a wall this long.</summary>
        private static Material Boards(float length)
        {
            var m = Mat.Lit(Color.white, 0.12f);
            if (_boardTex == null) _boardTex = BoardTexture();
            m.mainTexture = _boardTex;
            m.mainTextureScale = new Vector2(length / 0.6f, 1f);
            return m;
        }
        private static Texture2D _boardTex;

        private static void Crate(Vector3 at)
        {
            var c = new GameObject("Crate").transform;
            c.SetParent(_root, false);
            c.localPosition = at;
            Part(c, new Vector3(0f, 0.15f, 0f), new Vector3(0.36f, 0.3f, 0.36f), _wood);
            foreach (float y in new[] { 0.04f, 0.26f })
            {
                Part(c, new Vector3(0f, y, 0.182f), new Vector3(0.38f, 0.05f, 0.01f), _darkWood);
                Part(c, new Vector3(0f, y, -0.182f), new Vector3(0.38f, 0.05f, 0.01f), _darkWood);
                Part(c, new Vector3(0.182f, y, 0f), new Vector3(0.01f, 0.05f, 0.38f), _darkWood);
                Part(c, new Vector3(-0.182f, y, 0f), new Vector3(0.01f, 0.05f, 0.38f), _darkWood);
            }
            var col = c.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.15f, 0f);
            col.size = new Vector3(0.36f, 0.3f, 0.36f);
        }

        /// <summary>A pitched thatch roof (ridge running toward the sea) with overhang, and board gables closing its ends.</summary>
        private static void GableRoof(float top, float cx, float cz, float w, float d)
        {
            const float RISE = 1.25f, OVER = 0.55f;
            float half = w / 2f + OVER;
            float slope = Mathf.Sqrt(half * half + RISE * RISE), ang = Mathf.Atan2(RISE, half) * Mathf.Rad2Deg;
            var thatch = Mat.Lit(Color.white, 0.04f);
            thatch.mainTexture = ThatchTexture();
            thatch.mainTextureScale = new Vector2(slope / 1.2f, (d + 2f * OVER) / 1.2f);
            for (int side = -1; side <= 1; side += 2)
            {
                var slab = Box("RoofSlope", new Vector3(cx + side * half / 2f, top + RISE / 2f, cz), new Vector3(slope + 0.1f, 0.2f, d + 2f * OVER), thatch);
                slab.transform.localRotation = Quaternion.Euler(0f, 0f, -side * ang);
                Object.Destroy(slab.GetComponent<Collider>());
            }
            var ridge = Cyl("RoofRidge", new Vector3(cx, top + RISE + 0.08f, cz), new Vector3(0.22f, (d + 2f * OVER) / 2f, 0.22f), _darkWood);
            ridge.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Object.Destroy(ridge.GetComponent<Collider>());
            foreach (float z in new[] { BZ0, BZ1 }) Gable(new Vector3(cx, top, z), w / 2f, RISE);
        }

        private static void Gable(Vector3 at, float halfWidth, float rise)
        {
            var go = new GameObject("Gable");
            go.transform.SetParent(_root, false);
            go.transform.localPosition = at;
            var m = new Mesh { name = "Gable" };
            m.vertices = new[] { new Vector3(-halfWidth, 0f, 0f), new Vector3(halfWidth, 0f, 0f), new Vector3(0f, rise, 0f) };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(halfWidth * 2f / 0.6f, 0f), new Vector2(halfWidth / 0.6f, 1f) };
            m.triangles = new[] { 0, 2, 1, 0, 1, 2 }; // both sides
            m.RecalculateNormals();
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterial = Boards(1f);
        }

        private static void Lamp(Vector3 at)
        {
            CylPart(_root, at + Vector3.up * 0.2f, new Vector3(0.01f, 0.2f, 0.01f), _darkWood);                        // cord
            var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.transform.SetParent(_root, false);
            bulb.transform.localPosition = at;
            bulb.transform.localScale = Vector3.one * 0.14f;
            bulb.GetComponent<Renderer>().sharedMaterial = Mat.Unlit(new Color(1f, 0.85f, 0.55f));
            Object.Destroy(bulb.GetComponent<Collider>());
            var l = new GameObject("BarLamp").AddComponent<Light>();
            l.transform.SetParent(_root, false);
            l.transform.localPosition = at + Vector3.down * 0.15f;
            l.type = LightType.Point;
            l.range = 7f;
            l.intensity = 0.9f;
            l.color = new Color(1f, 0.82f, 0.55f);
            l.shadows = LightShadows.None;
        }

        /// <summary>A bar counter: low (you see the customers over it), a wooden front and a polished top.</summary>
        private static void BarCounter(float x1, float z1, float x2, float z2, float bottom)
        {
            Vector3 a = new Vector3(x1, 0f, z1), b = new Vector3(x2, 0f, z2), d = b - a;
            Quaternion rot = Quaternion.LookRotation(d);
            Vector3 mid = (a + b) * 0.5f;
            var body = Box("BarCounter", mid + Vector3.up * (bottom + (BAR_TOP - 0.025f) / 2f), new Vector3(0.5f, BAR_TOP - 0.025f, d.magnitude), Boards(d.magnitude));
            body.GetComponent<Renderer>().sharedMaterial.color = new Color(0.62f, 0.45f, 0.3f); // a darker stain than the walls
            body.transform.localRotation = rot;
            var top = Box("BarTop", mid + Vector3.up * (bottom + BAR_TOP - 0.025f), new Vector3(0.62f, 0.05f, d.magnitude + 0.12f), _counter);
            top.transform.localRotation = rot;
            var rail = Box("BarRail", mid + Vector3.up * (bottom + 0.12f) + Vector3.forward * 0.31f, new Vector3(0.05f, 0.05f, d.magnitude), _pole);
            rail.transform.localRotation = rot;
            Object.Destroy(rail.GetComponent<Collider>());
        }

        private static void Stool(Vector3 at)
        {
            var s = new GameObject("BarStool").transform;
            s.SetParent(_root, false);
            s.localPosition = at;
            CylPart(s, new Vector3(0f, (SEAT - 0.03f) / 2f, 0f), new Vector3(0.05f, (SEAT - 0.03f) / 2f, 0.05f), _pole);
            CylPart(s, new Vector3(0f, SEAT - 0.02f, 0f), new Vector3(0.36f, 0.03f, 0.36f), Mat.Lit(new Color(0.85f, 0.2f, 0.25f), 0.4f));
            CylPart(s, new Vector3(0f, 0.01f, 0f), new Vector3(0.3f, 0.01f, 0.3f), _pole);
        }

        /// <summary>The back bar: a tall shelf unit facing the counter, stocked with bottles and glasses.</summary>
        private static void DrinksShelf(float x1, float z1, float x2, float z2, float bottom)
        {
            Vector3 a = new Vector3(x1, 0f, z1), b = new Vector3(x2, 0f, z2), d = b - a;
            float len = d.magnitude;
            var shelf = new GameObject("DrinksShelf").transform;
            shelf.SetParent(_root, false);
            shelf.localPosition = (a + b) * 0.5f + Vector3.up * bottom;
            // Local x runs along the shelf; local +z faces the counter (north, toward the customers).
            Vector3 along = d / len;
            shelf.localRotation = Quaternion.LookRotation(new Vector3(along.z, 0f, -along.x) * (along.x < 0f ? 1f : -1f), Vector3.up);

            const float H = 2.3f, DEPTH = 0.36f;
            Part(shelf, new Vector3(0f, H / 2f, -DEPTH / 2f), new Vector3(len, H, 0.04f), _darkWood); // back panel
            foreach (float sx in new[] { -len / 2f, len / 2f })
                Part(shelf, new Vector3(sx, H / 2f, 0f), new Vector3(0.05f, H, DEPTH), _darkWood); // sides
            var solid = Box("DrinksShelfBody", shelf.localPosition + Vector3.up * (H / 2f), new Vector3(len, H, DEPTH), _darkWood);
            solid.transform.localRotation = shelf.localRotation;
            Object.Destroy(solid.GetComponent<Renderer>()); // just something solid to bump into

            // Four shelves: the bottom one holds glasses, the rest bottles.
            Color[] liquor =
            {
                new Color(0.6f, 0.32f, 0.08f), new Color(0.18f, 0.45f, 0.15f), new Color(0.5f, 0.05f, 0.12f), new Color(0.88f, 0.9f, 0.92f),
                new Color(0.1f, 0.45f, 0.85f), new Color(0.85f, 0.6f, 0.15f), new Color(0.35f, 0.18f, 0.06f), new Color(0.9f, 0.4f, 0.1f),
            };
            var mats = new Material[liquor.Length];
            for (int i = 0; i < liquor.Length; i++) mats[i] = Mat.Lit(liquor[i], 0.85f);
            var cap = Mat.Lit(new Color(0.8f, 0.72f, 0.4f), 0.7f);
            var glass = Mat.Lit(new Color(0.8f, 0.9f, 0.95f), 0.95f);
            var r = new System.Random(4);
            float[] levels = { 0.95f, 1.45f, 1.95f };
            foreach (float y in levels)
            {
                Part(shelf, new Vector3(0f, y - 0.02f, 0f), new Vector3(len - 0.08f, 0.03f, DEPTH), _wood);
                for (float x = -len / 2f + 0.14f; x < len / 2f - 0.1f; x += 0.11f + (float)r.NextDouble() * 0.05f)
                {
                    Material m = mats[r.Next(mats.Length)];
                    float h = 0.2f + (float)r.NextDouble() * 0.12f, w = 0.035f + (float)r.NextDouble() * 0.015f;
                    float z = -0.05f + (float)r.NextDouble() * 0.08f;
                    CylPart(shelf, new Vector3(x, y + h / 2f, z), new Vector3(w * 2f, h / 2f, w * 2f), m);                     // bottle
                    CylPart(shelf, new Vector3(x, y + h + 0.04f, z), new Vector3(w * 0.7f, 0.04f, w * 0.7f), m);               // neck
                    CylPart(shelf, new Vector3(x, y + h + 0.085f, z), new Vector3(w * 0.8f, 0.008f, w * 0.8f), cap);           // cap
                }
            }
            Part(shelf, new Vector3(0f, 0.43f, 0f), new Vector3(len - 0.08f, 0.03f, DEPTH), _wood);
            for (float x = -len / 2f + 0.12f; x < len / 2f - 0.1f; x += 0.12f)
                CylPart(shelf, new Vector3(x, 0.5f, 0.02f), new Vector3(0.07f, 0.055f, 0.07f), glass);                         // glasses
        }

        private static void Counter(float x1, float z1, float x2, float z2, float bottom, float depth)
        {
            Vector3 a = new Vector3(x1, 0f, z1), b = new Vector3(x2, 0f, z2), d = b - a;
            var body = Box("Counter", (a + b) * 0.5f + Vector3.up * (bottom + 0.45f), new Vector3(depth, 0.9f, d.magnitude), _darkWood);
            body.transform.localRotation = Quaternion.LookRotation(d);
            var top = Box("CounterTop", (a + b) * 0.5f + Vector3.up * (bottom + 0.92f), new Vector3(depth + 0.08f, 0.05f, d.magnitude + 0.08f), _counter);
            top.transform.localRotation = Quaternion.LookRotation(d);
        }

        private static void Sign(Vector3 pos, float yaw, string text, Color color, float width, float height)
        {
            var holder = new GameObject("Sign_" + text).transform;
            holder.SetParent(_root, false);
            holder.localPosition = pos;
            holder.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var board = Box("Board", Vector3.zero, new Vector3(width, height, 0.05f), _white);
            board.transform.SetParent(holder, false);
            board.transform.localPosition = Vector3.zero;
            Object.Destroy(board.GetComponent<Collider>());
            var t = WorldText.Create(holder, text, color, height * 0.55f, TextAnchor.MiddleCenter, width);
            t.canvas.transform.localPosition = new Vector3(0f, 0f, 0.035f);
            t.canvas.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // readable from the sign's front (+z)
        }

        // ── The gravel path (from the path edge marks and the "gravel" center line) ──
        private static void Paths()
        {
            Strip("GravelToBar", new Vector2(0.75f, -7.45f), new Vector2(12.1f, -7.6f), 2.1f);
            Strip("GravelToSea", new Vector2(0.55f, -8.4f), new Vector2(1.0f, 5.3f), 1.95f);
        }

        /// <summary>A flat strip that follows the sand, from a to b.</summary>
        private static void Strip(string name, Vector2 a, Vector2 b, float width)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            Vector2 dir = d / len, side = new Vector2(-dir.y, dir.x) * (width / 2f);
            int n = Mathf.Max(2, Mathf.CeilToInt(len / 0.5f) + 1);
            var verts = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            var tris = new List<int>();
            for (int i = 0; i < n; i++)
            {
                Vector2 c = a + d * (i / (float)(n - 1));
                for (int s = 0; s < 2; s++)
                {
                    Vector2 p = c + (s == 0 ? -side : side);
                    verts[i * 2 + s] = new Vector3(p.x, Y(p.x, p.y) + 0.035f, p.y);
                    uvs[i * 2 + s] = new Vector2(s * width / 1.5f, len * i / (float)(n - 1) / 1.5f);
                }
                if (i > 0)
                {
                    int k = (i - 1) * 2;
                    tris.Add(k); tris.Add(k + 2); tris.Add(k + 1);
                    tris.Add(k + 1); tris.Add(k + 2); tris.Add(k + 3);
                }
            }
            var mesh = new Mesh { name = name };
            mesh.vertices = verts; mesh.uv = uvs; mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            if (mesh.normals.Length > 0 && mesh.normals[0].y < 0f) { tris.Reverse(); mesh.triangles = tris.ToArray(); mesh.RecalculateNormals(); } // face up
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = _gravel;
        }

        // ── Umbrella tables: an umbrella, a small table under it, two chairs, a Tattletail on each ──
        private static void Tables()
        {
            // umbrella x, z; the chairs on either side (from the chair marks).
            float[,] sets =
            {
                { -10.40f, -0.47f, -11.87f, -0.59f, -9.06f, -0.77f },
                { -3.12f, -0.64f, -4.11f, -0.87f, -2.16f, -0.89f },
                { 6.69f, -0.60f, 5.71f, -0.86f, 7.68f, -0.88f },
            };
            Color[] stripes = { new Color(0.95f, 0.25f, 0.3f), new Color(0.2f, 0.55f, 0.95f), new Color(1f, 0.75f, 0.15f) };
            for (int i = 0; i < sets.GetLength(0); i++)
            {
                Vector3 u = G(sets[i, 0], sets[i, 1]);
                Umbrella(u, stripes[i], i + 1);
                var top = Cyl("Table" + (i + 1), u + Vector3.up * 0.72f, new Vector3(0.95f, 0.02f, 0.95f), _white);
                Object.Destroy(top.GetComponent<Collider>());
                top.AddComponent<BoxCollider>().size = new Vector3(1f, 30f, 1f); // (squashed cylinder: a box that reaches the ground)
                for (int c = 0; c < 2; c++)
                {
                    Vector3 chair = G(sets[i, 2 + c * 2], sets[i, 3 + c * 2]);
                    Vector3 toTable = u - chair; toTable.y = 0f;
                    Quaternion face = Quaternion.LookRotation(toTable);
                    Chair(chair, face);
                    Tattletail(chair + Vector3.up * 0.47f, face, 1f);
                }
            }
        }

        private static void Umbrella(Vector3 at, Color stripe, int n)
        {
            var pole = Cyl("UmbrellaPole" + n, at + Vector3.up * 1.2f, new Vector3(0.05f, 1.2f, 0.05f), _pole);
            Object.Destroy(pole.GetComponent<Collider>());
            pole.AddComponent<CapsuleCollider>();
            var canopy = new GameObject("Umbrella" + n);
            canopy.transform.SetParent(_root, false);
            canopy.transform.localPosition = at + Vector3.up * 2.25f;
            canopy.AddComponent<MeshFilter>().sharedMesh = CanopyMesh(1.35f, 0.4f, 12);
            canopy.AddComponent<MeshRenderer>().sharedMaterials = new[] { Mat.Lit(stripe, 0.3f), Mat.Lit(new Color(0.97f, 0.97f, 0.95f), 0.3f) };
        }

        /// <summary>An umbrella top: a low cone in alternating stripes (two materials), seen from above and below.</summary>
        private static Mesh CanopyMesh(float radius, float height, int segments)
        {
            var verts = new List<Vector3>();
            var a = new List<int>(); var b = new List<int>();
            for (int i = 0; i < segments; i++)
            {
                float t0 = i / (float)segments * Mathf.PI * 2f, t1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                int k = verts.Count;
                verts.Add(new Vector3(0f, height, 0f));
                verts.Add(new Vector3(Mathf.Cos(t0) * radius, 0f, Mathf.Sin(t0) * radius));
                verts.Add(new Vector3(Mathf.Cos(t1) * radius, 0f, Mathf.Sin(t1) * radius));
                var list = i % 2 == 0 ? a : b;
                list.Add(k); list.Add(k + 2); list.Add(k + 1); // outside
                list.Add(k); list.Add(k + 1); list.Add(k + 2); // underside
            }
            var m = new Mesh { name = "Canopy" };
            m.vertices = verts.ToArray();
            m.subMeshCount = 2;
            m.SetTriangles(a, 0);
            m.SetTriangles(b, 1);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static void Chair(Vector3 at, Quaternion face)
        {
            var chair = new GameObject("Chair").transform;
            chair.SetParent(_root, false);
            chair.localPosition = at;
            chair.localRotation = face;
            Part(chair, new Vector3(0f, 0.44f, 0f), new Vector3(0.46f, 0.05f, 0.46f), _white);          // seat
            Part(chair, new Vector3(0f, 0.72f, -0.21f), new Vector3(0.46f, 0.52f, 0.04f), _white);      // back
            foreach (float x in new[] { -0.2f, 0.2f })
                foreach (float z in new[] { -0.2f, 0.2f })
                    Part(chair, new Vector3(x, 0.21f, z), new Vector3(0.04f, 0.42f, 0.04f), _white);    // legs
        }

        // ── Towels with sunbathers ───────────────────────────────────────────
        private static void Towels()
        {
            // towel x, z, yaw (the user's two and the friend's three).
            float[,] towels = { { 8.24f, 3.20f, 201f }, { 4.88f, 3.54f, 111f }, { -2.24f, 1.91f, 5f }, { -6.65f, 2.38f, 11f }, { -9.47f, 2.84f, 3f } };
            Color[][] colors =
            {
                new[] { new Color(0.95f, 0.3f, 0.35f), Color.white }, new[] { new Color(0.2f, 0.6f, 0.95f), Color.white },
                new[] { new Color(1f, 0.8f, 0.2f), new Color(0.95f, 0.45f, 0.1f) }, new[] { new Color(0.4f, 0.85f, 0.5f), Color.white },
                new[] { new Color(0.75f, 0.4f, 0.95f), new Color(1f, 0.6f, 0.85f) },
            };
            for (int i = 0; i < towels.GetLength(0); i++)
            {
                Vector3 at = G(towels[i, 0], towels[i, 1]);
                Quaternion yaw = Quaternion.Euler(0f, towels[i, 2], 0f);
                var towel = Box("Towel" + (i + 1), at + Vector3.up * 0.015f, new Vector3(0.9f, 0.02f, 1.8f), Mat.Lit(Color.white, 0.05f));
                towel.transform.localRotation = yaw;
                towel.GetComponent<Renderer>().material.mainTexture = StripeTexture(colors[i][0], colors[i][1]);
                Object.Destroy(towel.GetComponent<Collider>());
                // Lying on its back in the sun, head toward the towel's top end.
                Tattletail(at + Vector3.up * 0.12f + yaw * new Vector3(0f, 0f, 0.1f), yaw * Quaternion.Euler(-80f, 0f, 0f), 1f);
            }
        }

        // ── Sand castles with small Tattletails ──────────────────────────────
        private static void SandCastles()
        {
            Castle(G(6.95f, 5.09f), 12f);
            Castle(G(-5.80f, 6.01f), 90f);
            float[,] smalls = { { 6.38f, 5.17f, 6.95f, 5.09f }, { -4.91f, 6.17f, -5.80f, 6.01f }, { -6.80f, 5.82f, -5.80f, 6.01f } };
            for (int i = 0; i < smalls.GetLength(0); i++)
            {
                Vector3 at = G(smalls[i, 0], smalls[i, 1]), castle = G(smalls[i, 2], smalls[i, 3]);
                Vector3 look = castle - at; look.y = 0f;
                Tattletail(at, Quaternion.LookRotation(look), 0.55f);
            }
        }

        private static void Castle(Vector3 at, float yaw)
        {
            var c = new GameObject("SandCastle").transform;
            c.SetParent(_root, false);
            c.localPosition = at;
            c.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Part(c, new Vector3(0f, 0.1f, 0f), new Vector3(0.62f, 0.2f, 0.62f), _castle);
            foreach (float x in new[] { -0.26f, 0.26f })
                foreach (float z in new[] { -0.26f, 0.26f })
                    CylPart(c, new Vector3(x, 0.2f, z), new Vector3(0.14f, 0.2f, 0.14f), _castle);
            CylPart(c, new Vector3(0f, 0.34f, 0f), new Vector3(0.22f, 0.34f, 0.22f), _castle);
            CylPart(c, new Vector3(0f, 0.78f, 0f), new Vector3(0.012f, 0.12f, 0.012f), _darkWood);   // flag pole
            Part(c, new Vector3(0.06f, 0.85f, 0f), new Vector3(0.11f, 0.07f, 0.005f), Mat.Lit(new Color(1f, 0.3f, 0.35f), 0.2f)); // flag
        }

        // ── Palm trees on the dunes behind and beside the cove ───────────────
        private static Material _trunkA, _trunkB, _frondA, _frondB, _coconut;
        private static Mesh _frondMesh;

        private static void Palms()
        {
            _trunkA = Mat.Lit(new Color(0.52f, 0.4f, 0.27f), 0.1f);
            _trunkB = Mat.Lit(new Color(0.4f, 0.3f, 0.2f), 0.1f);
            _frondA = Mat.Lit(new Color(0.22f, 0.55f, 0.2f), 0.25f);
            _frondB = Mat.Lit(new Color(0.3f, 0.62f, 0.18f), 0.25f);
            _coconut = Mat.Lit(new Color(0.36f, 0.25f, 0.12f), 0.2f);
            _frondMesh = FrondMesh(3.2f, 22);

            var r = new System.Random(7);
            var placed = new List<Vector2>();
            // A few right at the edge of the walkable sand (the ones you notice), then the dunes behind and to the sides.
            float[,] spots = { { -17.5f, -13.5f }, { -6.5f, -16f }, { 3.5f, -15.5f }, { 20f, -14f }, { 19.5f, 3.5f }, { -19.5f, 4.5f } };
            for (int i = 0; i < spots.GetLength(0); i++) TryPalm(r, placed, spots[i, 0], spots[i, 1]);
            for (int tries = 0; tries < 400 && placed.Count < 34; tries++)
            {
                float x, z;
                if (r.NextDouble() < 0.65) { x = -36f + (float)r.NextDouble() * 72f; z = -17f - (float)r.NextDouble() * 15f; }      // behind
                else { x = (r.Next(2) == 0 ? -1f : 1f) * (21f + (float)r.NextDouble() * 12f); z = -16f + (float)r.NextDouble() * 22f; } // the sides
                TryPalm(r, placed, x, z);
            }
        }

        private static void TryPalm(System.Random r, List<Vector2> placed, float x, float z)
        {
            if (x > 9.5f && x < 19f && z > -14.5f && z < 1.5f) return; // the beach bar
            var here = new Vector2(x, z);
            if (SegDist(here, new Vector2(-4f, -18f), new Vector2(0.75f, -7.45f)) < 2.5f) return;    // customers coming in from the dunes
            if (SegDist(here, new Vector2(EXIT_X, 1.2f), new Vector2(23f, 1f)) < 2.5f) return;       // and leaving along the beach
            var p = new Vector2(x, z);
            foreach (var q in placed) if ((q - p).sqrMagnitude < 16f) return;
            placed.Add(p);
            Palm(G(x, z) - Vector3.up * 0.1f, (float)r.NextDouble() * 360f, 5.5f + (float)r.NextDouble() * 3.5f, 8f + (float)r.NextDouble() * 14f, r.Next());
        }

        private static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        private static void Palm(Vector3 at, float yaw, float height, float lean, int seed)
        {
            var r = new System.Random(seed);
            var palm = new GameObject("PalmTree").transform;
            palm.SetParent(_root, false);
            palm.localPosition = at;
            palm.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var col = palm.gameObject.AddComponent<CapsuleCollider>();
            col.radius = 0.3f; col.height = 3f; col.center = new Vector3(0f, 1.5f, 0f);

            // The trunk: ringed segments that bend more toward the top.
            const int SEGS = 10;
            float segLen = height / SEGS;
            Vector3 p = Vector3.zero;
            Quaternion dir = Quaternion.identity;
            for (int i = 0; i < SEGS; i++)
            {
                float t = i / (float)(SEGS - 1);
                dir = Quaternion.Euler(lean * Mathf.Pow(t, 1.4f), 0f, 0f);
                Vector3 up = dir * Vector3.up;
                float w = Mathf.Lerp(0.42f, 0.26f, t);
                CylPart(palm, p + up * (segLen * 0.5f), new Vector3(w, segLen * 0.56f, w), i % 2 == 0 ? _trunkA : _trunkB).transform.localRotation = dir;
                p += up * segLen;
            }

            // The crown: fronds all around, coconuts tucked underneath.
            var crown = new GameObject("Crown").transform;
            crown.SetParent(palm, false);
            crown.localPosition = p;
            crown.localRotation = dir;
            SpherePart(crown, Vector3.zero, new Vector3(0.45f, 0.35f, 0.45f), _trunkB);
            int fronds = 8 + r.Next(3);
            for (int i = 0; i < fronds; i++)
            {
                var f = new GameObject("Frond").transform;
                f.SetParent(crown, false);
                f.localRotation = Quaternion.Euler(-15f + (float)r.NextDouble() * 30f, i * 360f / fronds + (float)r.NextDouble() * 20f, 0f);
                f.localScale = Vector3.one * (0.85f + (float)r.NextDouble() * 0.3f);
                f.gameObject.AddComponent<MeshFilter>().sharedMesh = _frondMesh;
                f.gameObject.AddComponent<MeshRenderer>().sharedMaterial = i % 2 == 0 ? _frondA : _frondB;
            }
            int nuts = r.Next(4);
            for (int i = 0; i < nuts; i++)
            {
                float a = i * 2.1f + (float)r.NextDouble();
                SpherePart(crown, new Vector3(Mathf.Cos(a) * 0.22f, -0.25f, Mathf.Sin(a) * 0.22f), Vector3.one * 0.24f, _coconut);
            }
        }

        /// <summary>A palm frond along +z: a spine that rises then droops, with long leaflets down both sides (two-sided).</summary>
        private static Mesh FrondMesh(float length, int leaflets)
        {
            var v = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < leaflets; i++)
            {
                float t0 = (i + 1f) / (leaflets + 2f), t1 = t0 + 0.9f / (leaflets + 2f);
                float reach = length * 0.42f * Mathf.Sin(Mathf.PI * Mathf.Lerp(0.15f, 1f, t0)) + 0.08f;
                Vector3 a = Spine(length, t0), b = Spine(length, t1), ahead = Spine(length, Mathf.Min(1f, t0 + 0.12f));
                foreach (float side in new[] { -1f, 1f })
                    Tri(v, tris, a, b, ahead + new Vector3(side * reach, -reach * 0.35f, 0f));
            }
            var s = new Vector3(0.025f, 0f, 0f);
            for (int i = 0; i < 12; i++)
            {
                Vector3 a = Spine(length, i / 12f), b = Spine(length, (i + 1) / 12f);
                Tri(v, tris, a - s, a + s, b + s);
                Tri(v, tris, a - s, b + s, b - s);
            }
            var m = new Mesh { name = "PalmFrond" };
            m.SetVertices(v);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static Vector3 Spine(float length, float t) { return new Vector3(0f, length * (0.3f * t - 0.7f * t * t), length * t); }

        /// <summary>One triangle seen from both sides (separate vertices so each side gets its own normal).</summary>
        private static void Tri(List<Vector3> v, List<int> tris, Vector3 a, Vector3 b, Vector3 c)
        {
            int k = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(a); v.Add(b); v.Add(c);
            tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
            tris.Add(k + 3); tris.Add(k + 5); tris.Add(k + 4);
        }

        // ── Tattletails (random colors, like the party's) ────────────────────
        private static void Tattletail(Vector3 local, Quaternion rot, float scale)
        {
            var tt = PartyTattletailModels.BuildRandomColored(_root.TransformPoint(local), _root.rotation * rot, _root);
            if (tt == null) return;
            if (scale != 1f) tt.transform.localScale *= scale;
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private static Vector3 G(float x, float z) { return new Vector3(x, Y(x, z), z); } // on the sand

        private static GameObject Box(string name, Vector3 local, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(_root, false);
            go.transform.localPosition = local;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        private static GameObject Cyl(string name, Vector3 local, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(_root, false);
            go.transform.localPosition = local;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        internal static void Part(Transform parent, Vector3 local, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            Object.Destroy(go.GetComponent<Collider>());
        }

        internal static GameObject CylPart(Transform parent, Vector3 local, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        internal static void SpherePart(Transform parent, Vector3 local, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            Object.Destroy(go.GetComponent<Collider>());
        }

        // ── Generated textures ───────────────────────────────────────────────
        private static Texture2D PlankTexture()
        {
            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat };
            var r = new System.Random(5);
            Color baseC = new Color(0.72f, 0.54f, 0.35f);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int plank = x / 32;
                    float shade = 0.9f + 0.1f * ((plank * 37) % 7) / 7f;
                    float grain = 0.05f * Mathf.Sin(y * 0.35f + plank * 2f + Mathf.Sin(x * 0.2f) * 2f);
                    float seam = (x % 32 == 0) ? -0.35f : 0f;
                    float s = shade + grain + seam + ((float)r.NextDouble() - 0.5f) * 0.04f;
                    tex.SetPixel(x, y, new Color(baseC.r * s, baseC.g * s, baseC.b * s));
                }
            tex.Apply();
            return tex;
        }

        private static Texture2D BoardTexture()
        {
            const int W = 128, H = 128;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat };
            var r = new System.Random(8);
            Color baseC = new Color(0.66f, 0.48f, 0.31f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int board = x / 32; // four boards across a tile (0.6 m on the wall: 15 cm boards)
                    float shade = 0.86f + 0.14f * ((board * 53) % 5) / 5f;
                    float grain = 0.06f * Mathf.Sin(x * 0.9f + Mathf.Sin(y * 0.07f + board) * 3f);
                    float seam = (x % 32 < 2) ? -0.4f : 0f;
                    float knot = 0f;
                    float kx = board * 32 + 16, ky = (board * 41) % H;
                    float dist = Mathf.Sqrt((x - kx) * (x - kx) + (y - ky) * (y - ky) * 0.3f);
                    if (dist < 4f) knot = -0.25f * (1f - dist / 4f);
                    float s = shade + grain + seam + knot + ((float)r.NextDouble() - 0.5f) * 0.035f;
                    tex.SetPixel(x, y, new Color(baseC.r * s, baseC.g * s, baseC.b * s));
                }
            tex.Apply();
            return tex;
        }

        private static Texture2D ThatchTexture()
        {
            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat };
            var r = new System.Random(12);
            Color baseC = new Color(0.8f, 0.67f, 0.4f);
            var col = new float[N];
            for (int x = 0; x < N; x++) col[x] = 0.8f + (float)r.NextDouble() * 0.3f; // straw strands run down the slope
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float rows = (y % 21 < 3) ? -0.18f : 0f; // layered bundles
                    float s = col[x] + rows + ((float)r.NextDouble() - 0.5f) * 0.12f;
                    tex.SetPixel(x, y, new Color(baseC.r * s, baseC.g * s, baseC.b * s));
                }
            tex.Apply();
            return tex;
        }

        private static Texture2D GravelTexture()
        {
            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat };
            var r = new System.Random(9);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float v = 0.5f + ((float)r.NextDouble() - 0.5f) * 0.25f;
                    tex.SetPixel(x, y, new Color(v * 1.02f, v * 0.98f, v * 0.92f));
                }
            for (int k = 0; k < 220; k++) // pebbles
            {
                int cx = r.Next(N), cy = r.Next(N), rad = 2 + r.Next(3);
                float v = 0.35f + (float)r.NextDouble() * 0.45f;
                Color c = new Color(v, v * 0.96f, v * 0.9f);
                for (int dy = -rad; dy <= rad; dy++)
                    for (int dx = -rad; dx <= rad; dx++)
                        if (dx * dx + dy * dy <= rad * rad) tex.SetPixel((cx + dx + N) % N, (cy + dy + N) % N, c);
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D StripeTexture(Color a, Color b)
        {
            const int N = 32;
            var tex = new Texture2D(N, N, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                    tex.SetPixel(x, y, (y / 4) % 2 == 0 ? a : b);
            tex.Apply();
            return tex;
        }
    }

    /// <summary>Stands a cloned model on a floor height once it has posed (its real bounds are known a few frames in).</summary>
    /// <summary>Slowly turns (yaw only) to keep the player in sight. Mama at the beach bar.</summary>
    public class WatchPlayer : MonoBehaviour
    {
        private void Update()
        {
            var fps = RM.fpsController;
            if (fps == null) return;
            Vector3 d = fps.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 40f * Time.deltaTime);
        }
    }

    public class StandOnFloor : MonoBehaviour
    {
        public float FloorY;
        private int _frames;

        private void LateUpdate()
        {
            if (++_frames < 3) return;
            bool any = false;
            Bounds b = new Bounds();
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r == null || !r.enabled) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (any) transform.position += Vector3.up * (FloorY - b.min.y);
            Destroy(this);
        }
    }
}
