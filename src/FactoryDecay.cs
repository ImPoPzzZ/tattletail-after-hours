using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Decades of nobody, part two: decals (water stains, mold, cracks, soot, rust, cobwebs, footprints, leaves,
    /// torn posters, caution tape; tools/factory_decals.py) and clutter (trash, a squatter's camp, tipped
    /// cabinets, dangling cables, hanging ceiling tiles, a collapsed rack, dust drifting in the light).
    /// Wall, floor and ceiling decals are placed by casting rays at the real geometry, so they always sit on
    /// a surface (never in a doorway). Runs one frame after the building exists so the colliders are live.
    /// </summary>
    public static partial class FactoryLayout
    {
        private static readonly string[] WallKeys = { "stain1", "stain2", "stain3", "stain1", "mold", "crack1", "crack2", "soot", "rust", "stain2" };
        private static readonly string[] FloorKeys = { "stain1", "stain3", "stain2", "crack1", "crack2", "soot" };
        private static readonly string[] CeilKeys = { "stain1", "stain2", "stain3", "mold" };

        /// <summary>The decay pass (called by FactoryController the frame after Build).</summary>
        public static void Decay()
        {
            if (_root == null) return;
            // Every room: stains and cracks on the walls, the floor and the ceiling; webs in the top corners.
            Room(0f, 0f, 14f, 20f, 0f, LOW);          // lobby
            Room(6f, 20f, 14f, 27f, 0f, LOW);         // hall
            Room(0f, 27f, 14f, 40f, 0f, LOW);         // break room
            Room(14f, 10f, 42f, 40f, 0f, HIGH);       // assembly floor
            Room(14f, 0f, 30f, 10f, 0f, LOW);         // testing lab
            Room(30f, 0f, 42f, 10f, 0f, LOW);         // the way to R&D
            Room(42f, 0f, 60f, 40f, 0f, HIGH);        // warehouse
            Room(0f, 0f, 14f, 8f, UP, UP_CEIL);       // conference
            Room(0f, 8f, 9f, 20f, UP, UP_CEIL);       // manager
            Room(9f, 8f, 14f, 20f, UP, UP_CEIL);      // PA room
            Room(6f, 20f, 14f, 28f, UP, UP_CEIL);     // upstairs hall
            Room(0f, 28f, 7f, 40f, UP, UP_CEIL);      // foreman
            Room(7f, 28f, 14f, 40f, UP, UP_CEIL);     // records
            Room(0f, 18.6f, 30f, 20.6f, DOWN, -0.2f); // basement corridor
            Room(0f, 8f, 12f, 18.6f, DOWN, -0.2f);    // generator room
            Room(12f, 8f, 24f, 18.6f, DOWN, -0.2f);   // boiler room
            Room(0f, 0f, 24f, 8f, DOWN, -0.2f);       // storage
            Room(30f, 12f, 42f, 24f, PIT, -0.2f);     // the pit

            Posters();
            Trails();
            Tapes();
            Clutter();
            Dust();
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Factory] Decay pass: " + _decalCount + " decals.");
        }

        private static int _decalCount;

        // ── Decals on real surfaces ──────────────────────────────────────────
        private static void Room(float x0, float z0, float x1, float z1, float floorY, float ceilY)
        {
            float area = (x1 - x0) * (z1 - z0);
            float h = ceilY - floorY;
            int walls = Mathf.Clamp((int)(area / 9f * (h > 4f ? 1.6f : 1f)), 3, 90);
            int floors = Mathf.Clamp((int)(area / 16f), 2, 50);
            int ceils = Mathf.Clamp((int)(area / 22f), 1, 30);
            for (int i = 0; i < walls; i++)
            {
                Vector3 p = new Vector3(Rand(x0 + 0.4f, x1 - 0.4f), Rand(floorY + 0.25f, Mathf.Min(ceilY - 0.3f, floorY + 5.5f)), Rand(z0 + 0.4f, z1 - 0.4f));
                Vector3 dir = Axis(_rng.Next(4));
                string key = WallKeys[_rng.Next(WallKeys.Length)];
                float size = key == "rust" ? Rand(0.9f, 1.8f) : key.StartsWith("crack") ? Rand(0.5f, 1.3f) : Rand(0.7f, 1.9f);
                if (key == "mold" || key == "soot") p.y = floorY + Rand(0.3f, 1.0f); // damp and scorch climb up from the floor
                RayDecal(key, p, dir, size, "wall", "paint");
            }
            for (int i = 0; i < floors; i++)
            {
                Vector3 p = new Vector3(Rand(x0 + 0.3f, x1 - 0.3f), floorY + 1.2f, Rand(z0 + 0.3f, z1 - 0.3f));
                RayDecal(FloorKeys[_rng.Next(FloorKeys.Length)], p, Vector3.down, Rand(0.8f, 2.4f), "floor", "ledge");
            }
            for (int i = 0; i < ceils; i++)
            {
                Vector3 p = new Vector3(Rand(x0 + 0.3f, x1 - 0.3f), ceilY - 1.2f, Rand(z0 + 0.3f, z1 - 0.3f));
                RayDecal(CeilKeys[_rng.Next(CeilKeys.Length)], p, Vector3.up, Rand(0.8f, 2.2f), "ceiling", ceilY < 0f ? "floor" : "roof"); // (the basement's ceiling is the ground floor)
            }
            // Cobwebs in the top corners.
            Web(new Vector3(x0 + 0.5f, ceilY - 0.15f, z0 + 0.5f), Vector3.left);
            Web(new Vector3(x1 - 0.5f, ceilY - 0.15f, z0 + 0.5f), Vector3.back);
            Web(new Vector3(x1 - 0.5f, ceilY - 0.15f, z1 - 0.5f), Vector3.right);
            Web(new Vector3(x0 + 0.5f, ceilY - 0.15f, z1 - 0.5f), Vector3.forward);
        }

        private static float Rand(float a, float b) { return a + (float)_rng.NextDouble() * (b - a); }

        private static Vector3 Axis(int i) { return i == 0 ? Vector3.left : i == 1 ? Vector3.right : i == 2 ? Vector3.forward : Vector3.back; }

        private static bool NameHas(Collider c, string a, string b)
        {
            string n = c.gameObject.name.ToLowerInvariant();
            if (n.Contains("invisible")) return false; // the invisible walls behind railings and around the lot: nothing to stick to
            return n.Contains(a) || (b != null && n.Contains(b));
        }

        // Cast from a point inside a room; if it hits the kind of surface named, stick a decal on it.
        private static bool RayDecal(string key, Vector3 local, Vector3 dir, float size, string surface, string surface2)
        {
            RaycastHit hit;
            if (!Physics.Raycast(_root.TransformPoint(local), dir, out hit, 20f, -1, QueryTriggerInteraction.Ignore)) return false;
            if (hit.collider == null || !hit.collider.transform.IsChildOf(_root) || !NameHas(hit.collider, surface, surface2)) return false;
            PlaceDecal(key, _root.InverseTransformPoint(hit.point), hit.normal, size, (float)_rng.NextDouble() * 360f);
            return true;
        }

        private static GameObject PlaceDecal(string key, Vector3 at, Vector3 normal, float height, float roll)
        {
            float aspect;
            FMat m = FTex.Decal(key, out aspect);
            Vector3 up = Mathf.Abs(normal.y) > 0.9f ? new Vector3(Mathf.Cos(roll), 0f, Mathf.Sin(roll)) : Vector3.up;
            var g = FGeo.Box(_root, "Decal " + key, Vector3.zero, new Vector3(height * aspect, height, 0.003f), m, false);
            g.transform.localRotation = Quaternion.LookRotation(-normal, up) * (Mathf.Abs(normal.y) > 0.9f ? Quaternion.identity : Quaternion.Euler(0f, 0f, roll * 0.05f - 9f));
            g.transform.localPosition = at + normal * (0.01f + (_decalCount % 7) * 0.0015f); // a hair off the surface (and off each other)
            _decalCount++;
            return g;
        }

        // A cobweb anchored in a top corner: find the wall, then slide along it to the corner on the web's left.
        private static void Web(Vector3 local, Vector3 dir)
        {
            RaycastHit hit;
            if (!Physics.Raycast(_root.TransformPoint(local), dir, out hit, 3f, -1, QueryTriggerInteraction.Ignore)) return;
            if (!NameHas(hit.collider, "wall", "paint")) return;
            Quaternion rot = Quaternion.LookRotation(-hit.normal, Vector3.up);
            Vector3 left = rot * Vector3.left;
            RaycastHit corner;
            Vector3 start = hit.point + hit.normal * 0.05f;
            if (!Physics.Raycast(start, left, out corner, 3f, -1, QueryTriggerInteraction.Ignore)) return;
            float size = Rand(0.5f, 0.9f);
            Vector3 cornerLocal = _root.InverseTransformPoint(corner.point - hit.normal * 0.05f);
            Vector3 center = cornerLocal - left * (size * 0.5f) + Vector3.down * (size * 0.5f) + Vector3.up * 0.1f;
            float aspect;
            var g = FGeo.Box(_root, "Cobweb", Vector3.zero, new Vector3(size, size, 0.003f), FTex.Decal("web", out aspect), false);
            g.transform.localRotation = rot;
            g.transform.localPosition = center + hit.normal * 0.012f;
            _decalCount++;
        }

        // ── Hand-placed pieces ───────────────────────────────────────────────
        private static void Posters()
        {
            Poster("ad", new Vector3(3f, 1.7f, 10f), Vector3.left, 0.9f);           // lobby, west wall
            Poster("safety", new Vector3(8f, 1.6f, 30f), Vector3.back, 0.8f);       // break room
            Poster("quiet", new Vector3(12.5f, 1.6f, 30f), Vector3.right, 0.8f);    // break room, by the counter... (skips if blocked)
            Poster("care", new Vector3(20f, 2.2f, 30f), Vector3.forward, 1.2f);     // assembly, north wall
            Poster("safety", new Vector3(26f, 1.8f, 20f), Vector3.back, 1.1f);      // assembly, the lab wall
            Poster("quiet", new Vector3(20f, 1.6f, 5f), Vector3.forward, 0.8f);     // testing lab
            Poster("ad", new Vector3(10f, UP + 1.6f, 24f), Vector3.right, 0.8f);    // upstairs hall
            Poster("care", new Vector3(50f, 2f, 3f), Vector3.back, 1.1f);           // warehouse
            Poster("safety", new Vector3(8f, DOWN + 1.6f, 19.6f), Vector3.forward, 0.8f); // basement corridor
        }

        private static void Poster(string key, Vector3 from, Vector3 dir, float height)
        {
            RaycastHit hit;
            if (!Physics.Raycast(_root.TransformPoint(from), dir, out hit, 12f, -1, QueryTriggerInteraction.Ignore)) return;
            if (!NameHas(hit.collider, "wall", "paint")) return;
            PlaceDecal(key, _root.InverseTransformPoint(hit.point), hit.normal, height, 180f).transform.localRotation *= Quaternion.Euler(0f, 0f, Rand(-6f, 6f));
        }

        // Dusty footprints: someone walked from the R&D door, across the floor, out to the lobby. Recently.
        private static void Trails()
        {
            Steps(new Vector3(36f, 0f, 4.2f), new Vector3(36f, 0f, 12f));
            Steps(new Vector3(36f, 0f, 12f), new Vector3(24f, 0f, 14.5f));
            Steps(new Vector3(24f, 0f, 14.5f), new Vector3(15f, 0f, 14f));
            Steps(new Vector3(13f, 0f, 14f), new Vector3(7f, 0f, 9f));
            // Leaves blown in through the front door, the broken window and the roof holes.
            FloorPatch("leaves", 6.5f, 0f, 1.4f, 1.6f); FloorPatch("leaves", 5.2f, 0f, 2.6f, 1.1f);
            FloorPatch("leaves", 11f, 0f, 1f, 1.3f);
            FloorPatch("leaves", 28f, 0f, 24.5f, 2f); FloorPatch("leaves", 30.2f, 0f, 28.5f, 1.4f);
            FloorPatch("leaves", 51.3f, 0f, 30.8f, 1.5f); FloorPatch("leaves", 58.5f, 0f, 18.5f, 1.2f);
        }

        private static void Steps(Vector3 a, Vector3 b)
        {
            Vector3 d = b - a; d.y = 0f;
            float len = d.magnitude;
            for (float t = 0f; t < len; t += 1.9f)
            {
                Vector3 p = a + d.normalized * (t + 0.95f) + Vector3.up * 0.004f;
                float aspect;
                var g = FGeo.Box(_root, "Footprints", Vector3.zero, new Vector3(0.48f, 1.9f, 0.003f), FTex.Decal("steps", out aspect), false);
                g.transform.localRotation = Quaternion.LookRotation(Vector3.down, d.normalized);
                g.transform.localPosition = p;
                _decalCount++;
            }
        }

        private static void FloorPatch(string key, float x, float y, float z, float size)
        {
            PlaceDecal(key, new Vector3(x, y + 0.006f, z), Vector3.up, size, (float)_rng.NextDouble() * 6.28f);
        }

        // Caution tape strung across places nobody should go.
        private static void Tapes()
        {
            Tape(new Vector3(31.4f, 1.1f, 3f), new Vector3(31.4f, 0.9f, 9.9f));        // across the way to R&D
            Tape(new Vector3(31.4f, 0.7f, 3.2f), new Vector3(31.4f, 1.3f, 9.8f));
            Tape(new Vector3(24.8f, 0.9f, 21.2f), new Vector3(32.2f, 1.2f, 21.2f));    // around the rubble on the line
            Tape(new Vector3(24.8f, 1.2f, 32.2f), new Vector3(32.2f, 0.9f, 32.2f));
            Tape(new Vector3(34.6f, 1f, 16.8f), new Vector3(36.6f, 0.8f, 18.4f));      // the reject chute
        }

        private static void Tape(Vector3 a, Vector3 b)
        {
            float aspect;
            FMat baseMat = FTex.Decal("tape", out aspect);
            if (baseMat.Mat.mainTexture != null) baseMat.Mat.mainTexture.wrapMode = TextureWrapMode.Repeat;
            float len = (b - a).magnitude;
            var m = new Material(baseMat.Mat) { mainTextureScale = new Vector2(len / 1.6f, 1f) };
            var g = FGeo.Box(_root, "Caution tape", Vector3.zero, new Vector3(len, 0.075f, 0.004f), new FMat(m, 0f), false);
            g.transform.localPosition = (a + b) * 0.5f;
            g.transform.localRotation = Quaternion.LookRotation(Vector3.Cross(b - a, Vector3.up).normalized, Vector3.up) * Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, new Vector2(b.x - a.x, b.z - a.z).magnitude) * Mathf.Rad2Deg);
        }

        // ── Clutter ──────────────────────────────────────────────────────────
        private static void Clutter()
        {
            // A squatter's camp in the lobby corner: a mattress, a blanket, cans, a burnt-out candle.
            FMat mattress = FTex.Get("fabric", 0.8f, new Color(0.55f, 0.5f, 0.42f), 0.03f, 0f, 1f);
            Box("Mattress", 0.5f, 0f, 16.2f, 2.3f, 0.18f, 19.4f, mattress, true).transform.localRotation = Quaternion.Euler(0f, 4f, 0f);
            Box("Blanket", 0.7f, 0.18f, 17.4f, 2.1f, 0.22f, 19.2f, FTex.Plain(new Color(0.28f, 0.12f, 0.12f), 0.05f), false).transform.localRotation = Quaternion.Euler(0f, -8f, 3f);
            Trash(2.8f, 17.5f, 0f, 1.2f, 10);
            Cyl("Candle stub", 2.6f, 0f, 16.4f, 0.03f, 0.05f, FTex.Plain(new Color(0.85f, 0.82f, 0.7f), 0.1f), false);
            Box("Scorch", 2.45f, 0.003f, 16.25f, 2.75f, 0.004f, 16.55f, _void, false);

            // Trash everywhere people have been.
            Trash(9f, 12f, 0f, 3f, 14); Trash(5f, 34f, 0f, 3f, 12); Trash(20f, 16f, 0f, 4f, 16);
            Trash(38f, 30f, 0f, 3f, 10); Trash(48f, 18f, 0f, 4f, 14); Trash(55f, 34f, 0f, 3f, 10);
            Trash(10f, 24f, UP, 2f, 8); Trash(4f, 33f, UP, 2.5f, 10); Trash(15f, 19.6f, DOWN, 5f, 12); Trash(6f, 12f, DOWN, 3f, 8);

            // Tipped furniture and spilled files upstairs; a cabinet on its side in records.
            TippedCabinet(8.5f, UP, 36f, 90f); TippedCabinet(12f, UP, 30.8f, 20f); TippedCabinet(1.5f, UP, 33f, -60f);
            Papers(10f, 34f, 3f, 30); Lift2(UP, 30); // (Papers are at ground height; Lift2 raises the last N to this floor)
            Papers(4f, 13f, 3f, 20); Lift2(UP, 20);
            Papers(11.5f, 12f, 2f, 14); Lift2(UP, 14);
            Papers(7f, 4f, 4f, 24); Lift2(UP, 24);

            // Ceiling tiles hanging half off their grid, and cables dangling where they fell.
            HangingTile(3.6f, LOW, 7.8f, 50f); HangingTile(9.6f, LOW, 16.2f, -40f); HangingTile(4.8f, LOW, 35.4f, 60f);
            HangingTile(3f, UP_CEIL, 12f, -55f); HangingTile(10.8f, UP_CEIL, 33f, 45f); HangingTile(5.4f, UP_CEIL, 4.2f, 50f);
            Cable(5f, LOW, 11f, 1.6f); Cable(11.5f, LOW, 31f, 1.9f); Cable(9f, LOW, 24.5f, 2.1f);
            Cable(22f, HIGH - 0.7f, 18f, 4.5f); Cable(35f, HIGH - 0.7f, 33f, 5.2f); Cable(47f, HIGH - 0.7f, 10f, 4f); Cable(56f, HIGH - 0.7f, 22f, 5.6f);
            Cable(4f, UP_CEIL, 24.5f, 1.8f); Cable(12f, UP_CEIL, 14f, 2f);
            Cable(10f, -0.2f, 19.6f, 1.6f); Cable(18f, -0.2f, 13f, 1.8f);

            // The warehouse: a rack upright that gave way, and everything that came down with it.
            var beam = Box("Rack upright (fallen)", 45f, 0f, 8.6f, 45.1f, 4.6f, 8.7f, _orange, false);
            beam.transform.localRotation = Quaternion.Euler(0f, 0f, -62f);
            beam.transform.localPosition = new Vector3(46.4f, 1.3f, 8.65f);
            SpilledBoxes(44.6f, 6.4f, 49.5f, 8f, 18);
            Box("Tire", 58f, 0f, 36f, 58.8f, 0.25f, 36.8f, _rubber, true);
            Box("Tire", 58.1f, 0.25f, 36.05f, 58.7f, 0.5f, 36.75f, _rubber, true);
            Box("Tarp", 50.5f, 0.02f, 24f, 53.5f, 0.05f, 26.5f, FTex.Plain(new Color(0.12f, 0.22f, 0.4f), 0.4f), false).transform.localRotation = Quaternion.Euler(3f, 17f, 0f);

            // Tattletail boxes, crushed and scattered on the assembly floor and in the warehouse.
            for (int i = 0; i < 18; i++)
            {
                float x = i < 9 ? Rand(16f, 40f) : Rand(43.5f, 58.5f), z = Rand(14f, 38f);
                var g = FGeo.Box(_root, "Tattletail box", new Vector3(x, 0.14f, z), new Vector3(0.38f, 0.28f, 0.3f), FTex.Plain(new Color(0.4f, 0.16f, 0.5f), 0.3f), true);
                g.transform.localRotation = Quaternion.Euler(_rng.NextDouble() < 0.5 ? 90f : 0f, Rand(0f, 360f), Rand(-8f, 8f));
            }

            // Broken monitors and a knocked-over TV cart.
            Box("Monitor (smashed)", 10f, 0f, 9.5f, 10.45f, 0.4f, 9.9f, _beige, true).transform.localRotation = Quaternion.Euler(0f, 30f, 88f);
            Box("Monitor (smashed)", 11.2f, UP, 18f, 11.65f, UP + 0.4f, 18.4f, _beige, true).transform.localRotation = Quaternion.Euler(-80f, 10f, 0f);
            Shards(10.3f, 9.8f, 0.6f, 12);
        }

        // Things created at ground height by a helper (like Papers) moved up to another floor.
        private static void Lift2(float floorY, int lastN)
        {
            int n = _root.childCount;
            for (int i = Mathf.Max(0, n - lastN); i < n; i++) _root.GetChild(i).localPosition += Vector3.up * floorY;
        }

        private static void Trash(float x, float z, float floorY, float radius, int count)
        {
            FMat can = _metalGrey, bottle = FTex.Plain(new Color(0.18f, 0.32f, 0.16f), 0.9f), paper = _paper, bag = FTex.Plain(new Color(0.06f, 0.06f, 0.07f), 0.6f);
            for (int i = 0; i < count; i++)
            {
                float a = Rand(0f, 6.28f), d = Mathf.Sqrt((float)_rng.NextDouble()) * radius;
                float px = x + Mathf.Cos(a) * d, pz = z + Mathf.Sin(a) * d;
                int kind = _rng.Next(4);
                if (kind == 0)
                {
                    var c = Cyl("Can", px, floorY + 0.035f, pz, 0.033f, 0.11f, can, false);
                    c.transform.localRotation = Quaternion.Euler(90f, Rand(0f, 360f), 0f);
                    c.transform.localPosition = new Vector3(px, floorY + 0.034f, pz);
                }
                else if (kind == 1)
                {
                    var b = Cyl("Bottle", px, floorY, pz, 0.035f, 0.24f, bottle, false);
                    b.transform.localRotation = Quaternion.Euler(90f, Rand(0f, 360f), 0f);
                    b.transform.localPosition = new Vector3(px, floorY + 0.035f, pz);
                }
                else if (kind == 2) Ball("Crumpled paper", new Vector3(px, floorY + 0.04f, pz), new Vector3(0.09f, 0.07f, 0.08f), paper);
                else Ball("Trash bag", new Vector3(px, floorY + 0.16f, pz), new Vector3(0.5f, 0.34f, 0.44f), bag);
            }
        }

        private static void TippedCabinet(float x, float floorY, float z, float yaw)
        {
            var g = FGeo.Box(_root, "Filing cabinet (tipped)", new Vector3(x, floorY + 0.33f, z), new Vector3(1.35f, 0.65f, 0.55f), _metalGrey, true);
            g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var drawer = FGeo.Box(_root, "Drawer (pulled out)", new Vector3(x, floorY + 0.12f, z), new Vector3(0.5f, 0.24f, 0.55f), _metalGrey, false);
            drawer.transform.localRotation = Quaternion.Euler(0f, yaw + 25f, 0f);
            drawer.transform.localPosition += drawer.transform.localRotation * new Vector3(0f, 0f, -0.8f);
        }

        // A drop-ceiling tile hanging by one edge.
        private static void HangingTile(float x, float ceilY, float z, float tilt)
        {
            Box("Ceiling gap", x - 0.3f, ceilY - 0.004f, z - 0.3f, x + 0.3f, ceilY, z + 0.3f, _void, false);
            var g = FGeo.Box(_root, "Ceiling tile (hanging)", Vector3.zero, new Vector3(0.6f, 0.02f, 0.6f), _ceiling, false);
            g.transform.localRotation = Quaternion.Euler(tilt, Rand(0f, 90f), 0f);
            g.transform.localPosition = new Vector3(x, ceilY - 0.25f, z);
        }

        // A cable hanging from the ceiling in a few kinked segments.
        private static void Cable(float x, float topY, float z, float length)
        {
            Vector3 p = new Vector3(x, topY, z);
            int n = 3;
            for (int i = 0; i < n; i++)
            {
                Vector3 q = p + new Vector3(Rand(-0.25f, 0.25f), -length / n, Rand(-0.25f, 0.25f));
                var g = FGeo.Box(_root, "Cable", Vector3.zero, new Vector3(0.025f, 0.025f, (q - p).magnitude), _dark, false);
                g.transform.localRotation = Quaternion.LookRotation(q - p);
                g.transform.localPosition = (p + q) * 0.5f;
                p = q;
            }
        }

        // ── Dust drifting in the light (the roof holes, the window, the headlights) ──
        private static Material _dustMat;

        private static void Dust()
        {
            DustCloud(new Vector3(28.5f, 3.5f, 26.5f), new Vector3(5f, 7f, 5f), 180);   // under the big roof hole
            DustCloud(new Vector3(51.5f, 3.5f, 32f), new Vector3(3f, 7f, 4f), 90);      // warehouse roof hole
            DustCloud(new Vector3(11f, 1.4f, 3f), new Vector3(3f, 1.8f, 4f), 70);       // the boarded window's beams
            DustCloud(new Vector3(6.5f, 1.2f, 1.5f), new Vector3(2.5f, 1.6f, 3f), 60);  // the front door
        }

        private static void DustCloud(Vector3 local, Vector3 size, int count)
        {
            if (_dustMat == null)
            {
                Shader sh = Shader.Find("Particles/Alpha Blended");
                if (sh == null) return;
                var tex = new Texture2D(32, 32, TextureFormat.ARGB32, false);
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        float r = new Vector2(x - 15.5f, y - 15.5f).magnitude / 15.5f;
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - r) * Mathf.Clamp01(1f - r)));
                    }
                tex.Apply();
                _dustMat = new Material(sh) { mainTexture = tex };
                if (_dustMat.HasProperty("_TintColor")) _dustMat.SetColor("_TintColor", new Color(0.35f, 0.36f, 0.38f, 0.12f));
            }
            var go = new GameObject("Dust");
            go.transform.SetParent(_root, false);
            go.transform.localPosition = local;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 14f;
            main.startSpeed = 0.02f;
            main.startSize = 0.008f;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            var em = ps.emission;
            em.rateOverTime = count / 14f;
            var sh2 = ps.shape;
            sh2.shapeType = ParticleSystemShapeType.Box;
            sh2.box = size;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.04f;
            noise.frequency = 0.2f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = _dustMat;
            ps.Clear();
            ps.Play(); // (it started on AddComponent, before any of the settings above)
        }
    }
}
