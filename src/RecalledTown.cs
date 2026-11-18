using System.Collections.Generic;

namespace KaleidoscopeEndless
{
    // Recalled's town (World 1), as pure layout: no Unity types. The game builds meshes and colliders from it
    // (Recalled.cs), the headless tests rasterize it to check that everywhere is reachable and draw the map.
    // Town meters: +x east, +z north, y up; the park's oak (the treehouse) is at 0,0. Everything is axis-aligned
    // and on flat ground, which is what lets one simple grid (RcNav) serve pathfinding, sight and the map check.

    public enum RcMat
    {
        None,        // a collider with nothing drawn (fences you can see through, edges)
        Grass, Lawn, Road, Line, Walk, Path, Dirt,
        Bark, Leaf, Wood, Plank, Metal, Rust, Stone, Concrete,
        Brick, House, HouseB, HouseC, Roof, Door, Window, Floor, Tile, Carpet,
        Glow, Neon, RedGlow, BlueGlow, Tape,
        CarA, CarB, CarC, Glass, Shelf, Crate, Fabric, Black,
        Paper, Cardboard, Orange, Pink, White, Teal,   // (props: posters, parcels, cones, toys, trucks)
        Sand, Sandstone, Adobe, Gilt, Lapis, Canvas,   // (Egypt)
        Packed,                                        // (Egypt: a trodden sand track)
    }

    public abstract class RcSink
    {
        public abstract void Box(RcMat m, float x0, float y0, float z0, float x1, float y1, float z1, bool collide);
        /// <summary>A solid box whose top you can stand on: the ground, a floor, a stair step. (Anything else solid only gets in the way.)</summary>
        public abstract void Walk(RcMat m, float x0, float y0, float z0, float x1, float y1, float z1);
        public abstract void Cyl(RcMat m, float x, float y0, float z, float radius, float height, bool collide);
        public abstract void Ball(RcMat m, float x, float y, float z, float rx, float ry, float rz);
        /// <summary>color: 0 warm lamp, 1 neon pink, 2 cold blue, 3 red, 4 sickly green.</summary>
        public abstract void Light(float x, float y, float z, int color, float range, float intensity);
    }

    public struct RcSpawn { public int Zone, Enemy, Count; public float X, Y, Z; public RcSpawn(int zone, int enemy, int count, float x, float y, float z) { Zone = zone; Enemy = enemy; Count = count; X = x; Y = y; Z = z; } }
    public struct RcSpot { public int Zone; public float X, Y, Z; public RcSpot(int zone, float x, float y, float z) { Zone = zone; X = x; Y = y; Z = z; } }

    public static partial class RcTown
    {
        // ── The shape of the town ────────────────────────────────────────────
        public const float X0 = -280f, X1 = 280f, Z0 = -226f, Z1 = 218f;   // the quarantine fence
        public const float PARK = 47f;                                      // the park's half size
        public const int PART_CORE = -1;                                    // parts: the core, then zones 0-4

        /// <summary>Each zone's fenced rectangle: x0, z0, x1, z1.</summary>
        public static readonly float[,] Zone =
        {
            { -217f, -209f, -17f, -87f },   // the suburbs
            { 128f, -142f, 266f, 8f },      // the mall
            { -264f, -61f, -115f, 76f },    // the school
            { 128f, 41f, 266f, 176f },      // the junkyard
            { -99f, 110f, 92f, 195f },      // the warehouse
        };

        /// <summary>Each zone's way in: the gap in its fence (x0, z0, x1, z1). The suburbs' is always open.</summary>
        public static readonly float[,] Gate =
        {
            { -17.5f, -136f, -16.5f, -124f },
            { 127.5f, -6f, 128.5f, 6f },
            { -115.5f, -6f, -114.5f, 6f },
            { 127.5f, 74f, 128.5f, 86f },
            { -6f, 109.5f, 6f, 110.5f },
        };

        /// <summary>Just outside and just inside each gate (x, z, x, z): where the checks walk to, and where a zipline drops you.</summary>
        public static readonly float[,] GateSides =
        {
            { -12f, -130f, -24f, -130f },
            { 122f, 0f, 134f, 0f },
            { -109f, 0f, -121f, 0f },
            { 122f, 80f, 134f, 80f },
            { 0f, 104f, 0f, 112f },
        };

        /// <summary>Boss arenas: x, z, radius.</summary>
        public static float[,] Arena { get { return RcWorlds.Now == RcWorlds.EGYPT ? ArenaEgypt : ArenaTown; } }

        private static readonly float[,] ArenaEgypt =
        {
            { -186f, -180f, 17f },
            { 244.5f, -113f, 13f },
            { -236f, -30f, 20f },
            { 236f, 145f, 17f },
            { 0f, 182f, 11f },     // Mama-2's chamber, at the heart of the pyramid
        };

        private static readonly float[,] ArenaTown =
        {
            { -186f, -180f, 17f },
            { 244.5f, -113f, 13f },
            { -236f, -30f, 20f },
            { 236f, 145f, 17f },
            { FAC_X + 51f, FAC_Z + 21f, 9f }, // (the Facility's warehouse hall)
        };

        /// <summary>Forts (zones 0-3): x, z. The warehouse has none.</summary>
        public static float[,] Fort { get { return _fort; } }
        private static readonly float[,] _fort = { { -102f, -156f }, { 140f, -16f }, { -135f, 30f }, { 118f, 92f } };   // (the same places in both worlds: the kids' forts, and Egypt's outposts)

        public const float STATION_X = 102.5f, STATION_Z = 52f;                 // the Recall Express: the junkyard station, on the railway
        public const float EGYPT_STATION_X = 23f, EGYPT_STATION_Z = -21f;        // the Recall Express at Last Stop
        public static float StationX { get { return RcWorlds.Egypt ? EGYPT_STATION_X : STATION_X; } }
        public static float StationZ { get { return RcWorlds.Egypt ? EGYPT_STATION_Z : STATION_Z; } }
        /// <summary>Where you arrive: outside the Arcade's door, or on the platform at Last Stop.</summary>
        public static float[] Arrival { get { return RcWorlds.Egypt ? ArrivalEgypt : ArrivalTown; } }
        private static readonly float[] ArrivalTown = { 6.2f, -180f };
        private static readonly float[] ArrivalEgypt = { 21f, -23f };
        public static readonly float[] DeckY = { 4.5f, 8.2f, 11.8f };       // the treehouse's three decks
        public const float DECK_HALF0 = 7f, DECK_HALF1 = 5.5f, DECK_HALF2 = 4.5f;

        // The ladder up the oak: where a climber hangs, the hatch it passes through in each deck (x0, z0, x1, z1),
        // and where you stand when you step off (on the ground, and on a deck).
        public const float LADDER_X = 0f, LADDER_Z = -2.3f, LADDER_OFF_GROUND_Z = -3.6f, LADDER_OFF_DECK_Z = -3.9f;
        public static readonly float[] Hatch = { -0.75f, -3.2f, 0.75f, -1.6f };

        // The warehouse is the Waygetter Facility itself (FactoryLayout: the game builds it, not this plan). Its
        // own meters start at its building's south-west corner: the building is 60 x 40, the fenced lot in front
        // of it runs x -12..62, z -26..0, and the lot's fence is opened opposite the zone's gate.
        public const float FAC_X = -30f, FAC_Z = 140f;
        public const float FAC_W = 60f, FAC_D = 40f, FAC_LOT_X0 = -12f, FAC_LOT_X1 = 62f, FAC_LOT_Z0 = -26f;
        public const float FAC_GATE_X0 = 24f, FAC_GATE_X1 = 36f;

        /// <summary>Inside the Facility's building (town meters)?</summary>
        public static bool InBuilding(float x, float z) { return !RcWorlds.Egypt && x > FAC_X && x < FAC_X + FAC_W && z > FAC_Z && z < FAC_Z + FAC_D; }
        /// <summary>On the Facility's ground: its lot or its building. (What can be walked there is measured in the game, from the real thing.)</summary>
        public static bool InFacility(float x, float z) { return !RcWorlds.Egypt && (InBuilding(x, z) || (x > FAC_X + FAC_LOT_X0 && x < FAC_X + FAC_LOT_X1 && z > FAC_Z + FAC_LOT_Z0 && z <= FAC_Z)); }

        /// <summary>Holes in the ground (x0, z0, x1, z1): where stairs go down to a basement, and where the Facility stands on its own floor.</summary>
        public static float[,] Holes { get { return RcWorlds.Now == RcWorlds.EGYPT ? HolesEgypt : HolesTown; } }
        private static readonly float[,] HolesEgypt = new float[0, 4];
        private static readonly float[,] HolesTown =
        {
            { -258f, 34f, -255f, 42f },     // the school's boiler-room stairs
            { FAC_X + FAC_LOT_X0, FAC_Z + FAC_LOT_Z0, FAC_X + FAC_LOT_X1, FAC_Z },
            { FAC_X, FAC_Z, FAC_X + FAC_W, FAC_Z + FAC_D },
        };

        public const float FLOOR = 4.5f;    // from one floor to the next

        private static readonly List<RcSpawn> _spawnsT = new List<RcSpawn>(), _spawnsE = new List<RcSpawn>();
        private static readonly List<RcSpot> _cratesT = new List<RcSpot>(), _cratesE = new List<RcSpot>(), _landT = new List<RcSpot>(), _landE = new List<RcSpot>();
        public static List<RcSpawn> Spawns { get { return RcWorlds.Now == RcWorlds.EGYPT ? _spawnsE : _spawnsT; } }
        public static List<RcSpot> Crates { get { return RcWorlds.Now == RcWorlds.EGYPT ? _cratesE : _cratesT; } }       // scrap piles you can search
        public static List<RcSpot> Landmarks { get { return RcWorlds.Now == RcWorlds.EGYPT ? _landE : _landT; } }       // (for the map picture)

        public static int ZoneAt(float x, float z)
        {
            for (int i = 0; i < 5; i++)
                if (x >= Zone[i, 0] && x <= Zone[i, 2] && z >= Zone[i, 1] && z <= Zone[i, 3]) return i;
            return -1;
        }

        public static bool InPark(float x, float z) { return x > -PARK && x < PARK && z > -PARK && z < PARK; }

        /// <summary>How far a point is from a zone's rectangle (0 inside).</summary>
        public static float ZoneDistance(int zone, float x, float z)
        {
            float dx = x < Zone[zone, 0] ? Zone[zone, 0] - x : x > Zone[zone, 2] ? x - Zone[zone, 2] : 0f;
            float dz = z < Zone[zone, 1] ? Zone[zone, 1] - z : z > Zone[zone, 3] ? z - Zone[zone, 3] : 0f;
            return (float)System.Math.Sqrt(dx * dx + dz * dz);
        }

        // ── Building blocks ──────────────────────────────────────────────────
        private static RcSink S;
        private static uint _seed;

        private static float Rnd() { _seed = _seed * 1664525u + 1013904223u; return ((_seed >> 8) & 0xFFFFFF) / 16777216f; }
        private static int Rnd(int n) { return (int)(Rnd() * n) % n; }

        /// <summary>A box from its footprint (x0,z0)-(x1,z1) and its bottom and top.</summary>
        private static void B(RcMat m, float x0, float z0, float x1, float z1, float y0, float y1, bool collide)
        {
            if (x1 < x0) { float t = x0; x0 = x1; x1 = t; }
            if (z1 < z0) { float t = z0; z0 = z1; z1 = t; }
            if (collide && Furnish(m, x0, z0, x1, z1, y0, y1)) return;
            S.Box(m, x0, y0, z0, x1, y1, z1, collide);
        }

        /// <summary>A box with nothing solid about it (a detail on something bigger). Any two corners.</summary>
        private static void Vis(RcMat m, float x0, float y0, float z0, float x1, float y1, float z1)
        {
            S.Box(m, System.Math.Min(x0, x1), System.Math.Min(y0, y1), System.Math.Min(z0, z1), System.Math.Max(x0, x1), System.Math.Max(y0, y1), System.Math.Max(z0, z1), false);
        }

        // The plan lays furniture down as plain solid boxes. These turn the ones that are shelves, tables and crates
        // (by their material and size) into the real thing. The solid part is the same box, so nothing can be walked
        // through or round differently.
        private static bool Furnish(RcMat m, float x0, float z0, float x1, float z1, float y0, float y1)
        {
            float w = x1 - x0, d = z1 - z0, h = y1 - y0;
            if (m == RcMat.Shelf && h > 1.4f && System.Math.Min(w, d) < 1.6f && System.Math.Max(w, d) > 2f) { ShelfUnit(x0, z0, x1, z1, y0, y1); return true; }
            if (m == RcMat.Wood && h > 0.62f && h < 0.98f && w < 3.6f && d < 3.6f && w > 0.8f && d > 0.8f) { TableOf(x0, z0, x1, z1, y0, y1); return true; }
            if (m == RcMat.Crate && h > 0.5f && h < 3f && w < 4.5f && d < 4.5f) { CrateOf(x0, z0, x1, z1, y0, y1); return true; }
            return false;
        }

        /// <summary>A steel rack: uprights along both long sides, boards every half meter, and boxes, toys and parts on them.</summary>
        private static void ShelfUnit(float x0, float z0, float x1, float z1, float y0, float y1)
        {
            S.Box(RcMat.None, x0, y0, z0, x1, y1, z1, true);
            bool alongX = x1 - x0 >= z1 - z0;
            float len = alongX ? x1 - x0 : z1 - z0, depth = alongX ? z1 - z0 : x1 - x0, h = y1 - y0;
            int posts = (int)(len / 2.4f) + 1;
            for (int i = 0; i < posts; i++)
                for (int side = 0; side < 2; side++)
                {
                    float t = posts == 1 ? 0.5f : i / (float)(posts - 1), along = (alongX ? x0 : z0) + 0.04f + t * (len - 0.08f);
                    float across = side == 0 ? (alongX ? z0 : x0) + 0.04f : (alongX ? z1 : x1) - 0.04f;
                    if (alongX) Vis(RcMat.Metal, along - 0.04f, y0, across - 0.04f, along + 0.04f, y1, across + 0.04f);
                    else Vis(RcMat.Metal, across - 0.04f, y0, along - 0.04f, across + 0.04f, y1, along + 0.04f);
                }
            int levels = System.Math.Max(3, (int)(h / 0.55f));
            RcMat[] stuff = { RcMat.Crate, RcMat.CarA, RcMat.Fabric, RcMat.CarB, RcMat.Plank, RcMat.CarC, RcMat.Crate };
            for (int k = 0; k < levels; k++)
            {
                float by = y0 + 0.1f + k * (h - 0.16f) / (levels - 1);
                Vis(RcMat.Shelf, x0, by, z0, x1, by + 0.04f, z1);
                if (k == levels - 1) continue;
                float room = (h - 0.16f) / (levels - 1) - 0.1f;
                for (float a = 0.25f; a < len - 0.5f; a += 0.7f)
                {
                    float ax = alongX ? x0 + a : x0, az = alongX ? z0 : z0 + a;
                    float v = Vary(ax + k, az, 3 + k);
                    if (v < 0.22f) continue;
                    float iw = 0.3f + Vary(ax, az + k, 9) * 0.32f, id = System.Math.Min(depth - 0.2f, depth * (0.55f + Vary(ax, az, 12) * 0.35f)), ih = System.Math.Min(room, 0.14f + Vary(ax, az, 15) * 0.3f);
                    RcMat what = stuff[(int)(Vary(ax, az + k, 21) * stuff.Length) % stuff.Length];
                    float c = (alongX ? z0 : x0) + depth * 0.5f;
                    if (alongX) Vis(what, ax, by + 0.04f, c - id * 0.5f, ax + iw, by + 0.04f + ih, c + id * 0.5f);
                    else Vis(what, c - id * 0.5f, by + 0.04f, az, c + id * 0.5f, by + 0.04f + ih, az + iw);
                }
            }
        }

        /// <summary>A table or desk: a thick top, four legs, a shelf underneath.</summary>
        private static void TableOf(float x0, float z0, float x1, float z1, float y0, float y1)
        {
            S.Box(RcMat.None, x0, y0, z0, x1, y1, z1, true);
            Vis(RcMat.Wood, x0, y1 - 0.07f, z0, x1, y1, z1);
            Vis(RcMat.Plank, x0 + 0.05f, y1 - 0.2f, z0 + 0.05f, x1 - 0.05f, y1 - 0.07f, z1 - 0.05f); // the apron under the top
            for (int i = 0; i < 4; i++)
            {
                float lx = i % 2 == 0 ? x0 + 0.06f : x1 - 0.13f, lz = i < 2 ? z0 + 0.06f : z1 - 0.13f;
                Vis(RcMat.Wood, lx, y0, lz, lx + 0.07f, y1 - 0.07f, lz + 0.07f);
            }
            Vis(RcMat.Plank, x0 + 0.1f, y0 + 0.18f, z0 + 0.1f, x1 - 0.1f, y0 + 0.21f, z1 - 0.1f);
        }

        /// <summary>A crate: planks banded top and bottom, posts at the corners.</summary>
        private static void CrateOf(float x0, float z0, float x1, float z1, float y0, float y1)
        {
            S.Box(RcMat.Crate, x0, y0, z0, x1, y1, z1, true);
            float band = System.Math.Min(0.14f, (y1 - y0) * 0.2f);
            Vis(RcMat.Plank, x0 - 0.02f, y0, z0 - 0.02f, x1 + 0.02f, y0 + band, z1 + 0.02f);
            Vis(RcMat.Plank, x0 - 0.02f, y1 - band, z0 - 0.02f, x1 + 0.02f, y1, z1 + 0.02f);
            for (int i = 0; i < 4; i++)
            {
                float cx = i % 2 == 0 ? x0 - 0.02f : x1 - 0.1f, cz = i < 2 ? z0 - 0.02f : z1 - 0.1f;
                Vis(RcMat.Plank, cx, y0, cz, cx + 0.12f, y1, cz + 0.12f);
            }
        }

        private const float T = 0.4f; // wall thickness

        /// <summary>A wall running along x at z, from x0 to x1, with gaps (center, width pairs). Above each gap, a lintel.</summary>
        private static void WallX(RcMat m, float z, float x0, float x1, float h, params float[] gaps)
        {
            float at = x0;
            for (int i = 0; i + 1 < gaps.Length; i += 2)
            {
                float g0 = gaps[i] - gaps[i + 1] * 0.5f, g1 = gaps[i] + gaps[i + 1] * 0.5f;
                if (g0 > at) B(m, at, z - T * 0.5f, g0, z + T * 0.5f, 0f, h, true);
                if (h > 2.7f) B(m, g0, z - T * 0.5f, g1, z + T * 0.5f, 2.6f, h, true);
                at = g1;
            }
            if (x1 > at) B(m, at, z - T * 0.5f, x1, z + T * 0.5f, 0f, h, true);
        }

        /// <summary>A wall running along z at x.</summary>
        private static void WallZ(RcMat m, float x, float z0, float z1, float h, params float[] gaps)
        {
            float at = z0;
            for (int i = 0; i + 1 < gaps.Length; i += 2)
            {
                float g0 = gaps[i] - gaps[i + 1] * 0.5f, g1 = gaps[i] + gaps[i + 1] * 0.5f;
                if (g0 > at) B(m, x - T * 0.5f, at, x + T * 0.5f, g0, 0f, h, true);
                if (h > 2.7f) B(m, x - T * 0.5f, g0, x + T * 0.5f, g1, 2.6f, h, true);
                at = g1;
            }
            if (z1 > at) B(m, x - T * 0.5f, at, x + T * 0.5f, z1, 0f, h, true);
        }

        private static readonly float[] NoGaps = new float[0];

        /// <summary>A wall along x from height y0 to y1 (an upper floor's), with gaps (center, width). A lintel tops each gap.</summary>
        private static void WallXY(RcMat m, float z, float x0, float x1, float y0, float y1, params float[] gaps)
        {
            float at = x0;
            for (int i = 0; i + 1 < gaps.Length; i += 2)
            {
                float g0 = gaps[i] - gaps[i + 1] * 0.5f, g1 = gaps[i] + gaps[i + 1] * 0.5f;
                if (g0 > at) B(m, at, z - T * 0.5f, g0, z + T * 0.5f, y0, y1, true);
                if (y1 - y0 > 2.7f) B(m, g0, z - T * 0.5f, g1, z + T * 0.5f, y0 + 2.6f, y1, true);
                at = g1;
            }
            if (x1 > at) B(m, at, z - T * 0.5f, x1, z + T * 0.5f, y0, y1, true);
        }

        private static void WallZY(RcMat m, float x, float z0, float z1, float y0, float y1, params float[] gaps)
        {
            float at = z0;
            for (int i = 0; i + 1 < gaps.Length; i += 2)
            {
                float g0 = gaps[i] - gaps[i + 1] * 0.5f, g1 = gaps[i] + gaps[i + 1] * 0.5f;
                if (g0 > at) B(m, x - T * 0.5f, at, x + T * 0.5f, g0, y0, y1, true);
                if (y1 - y0 > 2.7f) B(m, x - T * 0.5f, g0, x + T * 0.5f, g1, y0 + 2.6f, y1, true);
                at = g1;
            }
            if (z1 > at) B(m, x - T * 0.5f, at, x + T * 0.5f, z1, y0, y1, true);
        }

        /// <summary>
        /// A flight of steps from (x0 or z0 end, at yLow) up to the other end (yHigh): solid underneath, each tread a
        /// box you can stand on. alongX: it runs along x; up: it climbs toward the greater coordinate.
        /// </summary>
        private static void Stairs(RcMat m, float x0, float z0, float x1, float z1, float yLow, float yHigh, bool alongX, bool up)
        {
            int n = (int)System.Math.Ceiling((yHigh - yLow) / 0.18f);
            float run = alongX ? x1 - x0 : z1 - z0;
            for (int i = 0; i < n; i++)
            {
                float top = yLow + (yHigh - yLow) * (i + 1) / n;
                float a = run * i / n, b = run * (i + 1) / n;
                if (!up) { float t = a; a = run - b; b = run - t; }
                if (alongX) S.Walk(m, x0 + a, yLow, z0, x0 + b, top, z1);
                else S.Walk(m, x0, yLow, z0 + a, x1, top, z0 + b);
            }
        }

        /// <summary>A railing: a rail you see and a wall you can't climb, along a line at floor height y.</summary>
        private static void Rail(float x0, float z0, float x1, float z1, float y)
        {
            bool alongX = System.Math.Abs(x1 - x0) > System.Math.Abs(z1 - z0);
            if (alongX)
            {
                if (x1 < x0) { float t = x0; x0 = x1; x1 = t; }
                B(RcMat.None, x0, z0 - 0.08f, x1, z0 + 0.08f, y, y + 2.2f, true);
                B(RcMat.Metal, x0, z0 - 0.03f, x1, z0 + 0.03f, y + 1f, y + 1.06f, false);
                for (float x = x0; x <= x1 + 0.01f; x += 1.5f) B(RcMat.Metal, x - 0.03f, z0 - 0.03f, x + 0.03f, z0 + 0.03f, y, y + 1.03f, false);
            }
            else
            {
                if (z1 < z0) { float t = z0; z0 = z1; z1 = t; }
                B(RcMat.None, x0 - 0.08f, z0, x0 + 0.08f, z1, y, y + 2.2f, true);
                B(RcMat.Metal, x0 - 0.03f, z0, x0 + 0.03f, z1, y + 1f, y + 1.06f, false);
                for (float z = z0; z <= z1 + 0.01f; z += 1.5f) B(RcMat.Metal, x0 - 0.03f, z - 0.03f, x0 + 0.03f, z + 0.03f, y, y + 1.03f, false);
            }
        }

        /// <summary>A number from 0 to 1 that depends only on where something is (so details vary without touching the plan's dice).</summary>
        private static float Vary(float x, float z, int salt)
        {
            double v = System.Math.Sin(x * 12.9898 + z * 78.233 + salt * 37.719) * 43758.5453;
            return (float)(v - System.Math.Floor(v));
        }

        private static void Lamp(float x, float z, float toX, float toZ, bool lit)
        {
            B(RcMat.Stone, x - 0.2f, z - 0.2f, x + 0.2f, z + 0.2f, 0f, 0.45f, false);             // its footing
            S.Cyl(RcMat.Metal, x, 0.45f, z, 0.075f, 4.35f, false);
            S.Cyl(RcMat.Metal, x, 0.45f, z, 0.12f, 0.5f, false);
            float hx = x + toX * 1.1f, hz = z + toZ * 1.1f;
            B(RcMat.Metal, System.Math.Min(x, hx) - 0.05f, System.Math.Min(z, hz) - 0.05f, System.Math.Max(x, hx) + 0.05f, System.Math.Max(z, hz) + 0.05f, 4.7f, 4.8f, false);
            B(RcMat.Metal, hx - 0.34f, hz - 0.22f, hx + 0.34f, hz + 0.22f, 4.68f, 4.78f, false);  // the hood
            B(lit ? RcMat.Glow : RcMat.Black, hx - 0.28f, hz - 0.18f, hx + 0.28f, hz + 0.18f, 4.54f, 4.68f, false);
            if (lit) S.Light(hx, 4.3f, hz, 0, 14f, 1.5f);
            Keep(x - 0.35f, z - 0.35f, x + 0.35f, z + 0.35f);
            if (Vary(x, z, 5) < 0.4f) PPoster(x + 0.08f, 1.45f, z, true);                         // (a recall notice, taped to the pole)
        }

        private static void Tree(float x, float z, float s)
        {
            S.Cyl(RcMat.Bark, x, 0f, z, 0.32f * s, 3.6f * s, true);
            S.Cyl(RcMat.Bark, x, 0f, z, 0.46f * s, 0.35f * s, false);                             // the root flare
            S.Ball(RcMat.Leaf, x, 5.1f * s, z, 2.7f * s, 2.3f * s, 2.7f * s);
            // A crown in clumps, not one ball.
            for (int i = 0; i < 4; i++)
            {
                float a = Vary(x, z, i) * 6.283f, r = (1.3f + Vary(x, z, i + 9) * 0.7f) * s, k = (1.4f + Vary(x, z, i + 17) * 0.6f) * s;
                S.Ball(RcMat.Leaf, x + (float)System.Math.Cos(a) * r, (4.3f + Vary(x, z, i + 23) * 1.7f) * s, z + (float)System.Math.Sin(a) * r, k, k * 0.8f, k);
            }
        }

        /// <summary>Something flat on one face of a block (0 south, 1 north, 2 west, 3 east), standing 'proud' of the wall.</summary>
        private static void OnFace(RcMat m, int face, float x0, float z0, float x1, float z1, float a0, float a1, float y0, float y1, float proud)
        {
            if (face == 0) B(m, a0, z0 - proud, a1, z0, y0, y1, false);
            else if (face == 1) B(m, a0, z1, a1, z1 + proud, y0, y1, false);
            else if (face == 2) B(m, x0 - proud, a0, x0, a1, y0, y1, false);
            else B(m, x1, a0, x1 + proud, a1, y0, y1, false);
        }

        /// <summary>A window on a face: a stone frame, the pane, a cross of glazing bars and a sill. 'a' is its middle along the wall, 'y' its bottom.</summary>
        private static void Pane(int face, float x0, float z0, float x1, float z1, float a, float y, RcMat glass)
        {
            OnFace(RcMat.Stone, face, x0, z0, x1, z1, a - 0.8f, a + 0.8f, y - 0.08f, y + 1.28f, 0.05f);
            OnFace(glass, face, x0, z0, x1, z1, a - 0.7f, a + 0.7f, y, y + 1.2f, 0.07f);
            OnFace(RcMat.Stone, face, x0, z0, x1, z1, a - 0.03f, a + 0.03f, y, y + 1.2f, 0.09f);
            OnFace(RcMat.Stone, face, x0, z0, x1, z1, a - 0.7f, a + 0.7f, y + 0.57f, y + 0.63f, 0.09f);
            OnFace(RcMat.Stone, face, x0, z0, x1, z1, a - 0.9f, a + 0.9f, y - 0.16f, y - 0.08f, 0.16f);
        }

        /// <summary>A house you can't enter: a footing, walls, a stepped roof with a chimney, and on the side that faces the street a door with a step, and windows up and down.</summary>
        private static void Front(float x0, float z0, float x1, float z1, float h, int face, RcMat wall)
        {
            B(wall, x0, z0, x1, z1, 0f, h, true);
            B(RcMat.Stone, x0 - 0.06f, z0 - 0.06f, x1 + 0.06f, z1 + 0.06f, 0f, 0.35f, false);
            // A gabled roof: an eave all round, then steps that close in toward a ridge along the long side.
            B(RcMat.Roof, x0 - 0.45f, z0 - 0.45f, x1 + 0.45f, z1 + 0.45f, h, h + 0.28f, false);
            bool ridgeX = (x1 - x0) >= (z1 - z0);
            float shortHalf = (ridgeX ? z1 - z0 : x1 - x0) * 0.5f;
            for (int k = 0; k < 5; k++)
            {
                float inset = 0.3f + k * (shortHalf - 1.0f) / 5f, yy = h + 0.28f + k * 0.52f;
                if (ridgeX) B(RcMat.Roof, x0 - 0.3f, z0 + inset, x1 + 0.3f, z1 - inset, yy, yy + 0.52f, false);
                else B(RcMat.Roof, x0 + inset, z0 - 0.3f, x1 - inset, z1 + 0.3f, yy, yy + 0.52f, false);
            }
            float topY = h + 0.28f + 5 * 0.52f;
            float chx = Vary(x0, z0, 1) < 0.5f ? x0 + 1.1f : x1 - 1.8f, chz = ridgeX ? (z0 + z1) * 0.5f - 0.35f : z0 + 1.2f;
            if (!ridgeX) chx = (x0 + x1) * 0.5f - 0.35f;
            B(RcMat.Brick, chx, chz, chx + 0.7f, chz + 0.7f, h + 0.28f, topY + 0.9f, false);
            B(RcMat.Stone, chx - 0.06f, chz - 0.06f, chx + 0.76f, chz + 0.76f, topY + 0.9f, topY + 1.0f, false);
            bool lit = Rnd(7) == 0;
            RcMat win = lit ? RcMat.Glow : RcMat.Window;
            bool alongX = face < 2;
            float a0 = alongX ? x0 : z0, a1 = alongX ? x1 : z1, mid = (a0 + a1) * 0.5f;
            // The door: a frame, the door, a step, a little roof over it.
            OnFace(RcMat.Stone, face, x0, z0, x1, z1, mid - 0.64f, mid + 0.64f, 0f, 2.24f, 0.05f);
            OnFace(RcMat.Door, face, x0, z0, x1, z1, mid - 0.5f, mid + 0.5f, 0f, 2.1f, 0.08f);
            OnFace(RcMat.Metal, face, x0, z0, x1, z1, mid + 0.32f, mid + 0.4f, 0.98f, 1.06f, 0.13f);
            OnFace(RcMat.Stone, face, x0, z0, x1, z1, mid - 0.85f, mid + 0.85f, 0f, 0.16f, 0.55f);
            OnFace(RcMat.Roof, face, x0, z0, x1, z1, mid - 0.95f, mid + 0.95f, 2.4f, 2.5f, 0.7f);
            // The doormat, the porch light, the number, recall tape across the door, and what was left on the step.
            OnFace(RcMat.Carpet, face, x0, z0, x1, z1, mid - 0.45f, mid + 0.45f, 0.16f, 0.2f, 0.95f);
            OnFace(RcMat.Glow, face, x0, z0, x1, z1, mid + 0.7f, mid + 0.82f, 2.05f, 2.2f, 0.14f);
            if (Vary(x0, z0, 11) < 0.25f) S.Light(face < 2 ? mid + 0.76f : (face == 2 ? x0 - 0.9f : x1 + 0.9f), 2.3f, face < 2 ? (face == 0 ? z0 - 0.9f : z1 + 0.9f) : mid + 0.76f, 0, 6f, 0.7f);
            OnFace(RcMat.Paper, face, x0, z0, x1, z1, mid - 0.9f, mid - 0.7f, 1.45f, 1.65f, 0.08f);
            if (Vary(x0, z0, 12) < 0.5f)
            {
                OnFace(RcMat.Tape, face, x0, z0, x1, z1, mid - 0.5f, mid + 0.5f, 0.9f, 1.0f, 0.11f);
                OnFace(RcMat.Tape, face, x0, z0, x1, z1, mid - 0.5f, mid + 0.5f, 1.45f, 1.55f, 0.11f);
            }
            if (Vary(x0, z0, 13) < 0.5f)
            {
                float px = face < 2 ? mid + 1.5f : (face == 2 ? x0 - 0.55f : x1 + 0.55f), pz = face < 2 ? (face == 0 ? z0 - 0.55f : z1 + 0.55f) : mid + 1.5f;
                RcMat[] fur = { RcMat.Fabric, RcMat.CarA, RcMat.Teal, RcMat.Pink };
                PParcel(px, pz, fur[(int)(Vary(x0, z0, 14) * 4f) % 4]);
            }
            // Bushes along the foot of the wall, and window boxes.
            for (int b = 0; b < 2; b++)
            {
                float ba = b == 0 ? a0 + 0.9f : a1 - 0.9f;
                if (face == 0) PBush(ba, z0 - 0.75f, 0.8f); else if (face == 1) PBush(ba, z1 + 0.75f, 0.8f); else if (face == 2) PBush(x0 - 0.75f, ba, 0.8f); else PBush(x1 + 0.75f, ba, 0.8f);
            }
            // Windows: two down, two up, with shutters and a flower box under the left one.
            Pane(face, x0, z0, x1, z1, a0 + 1.9f, 1.1f, win);
            Pane(face, x0, z0, x1, z1, a1 - 1.9f, 1.1f, RcMat.Window);
            foreach (float wa in new[] { a0 + 1.9f, a1 - 1.9f })
            {
                OnFace(RcMat.Wood, face, x0, z0, x1, z1, wa - 1.05f, wa - 0.85f, 1.1f, 2.3f, 0.07f);
                OnFace(RcMat.Wood, face, x0, z0, x1, z1, wa + 0.85f, wa + 1.05f, 1.1f, 2.3f, 0.07f);
            }
            OnFace(RcMat.Wood, face, x0, z0, x1, z1, a0 + 1.2f, a0 + 2.6f, 0.78f, 1.0f, 0.4f);
            OnFace(RcMat.Pink, face, x0, z0, x1, z1, a0 + 1.3f, a0 + 2.5f, 1.0f, 1.12f, 0.34f);
            if (h >= 4.6f)
            {
                Pane(face, x0, z0, x1, z1, a0 + 1.9f, 3.2f, RcMat.Window);
                Pane(face, x0, z0, x1, z1, a1 - 1.9f, 3.2f, Vary(x0, z1, 3) < 0.12f ? RcMat.Glow : RcMat.Window);
                Pane(face, x0, z0, x1, z1, mid, 3.2f, RcMat.Window);
            }
        }

        /// <summary>The outer walls, floor and roof of a building you can walk into. doors: side (0 south, 1 north, 2 west, 3 east), center, width.</summary>
        private static void Shell(float x0, float z0, float x1, float z1, float h, RcMat wall, RcMat floor, params float[] doors)
        {
            var g = new List<float>[4];
            for (int i = 0; i < 4; i++) g[i] = new List<float>();
            for (int i = 0; i + 2 < doors.Length; i += 3) { g[(int)doors[i]].Add(doors[i + 1]); g[(int)doors[i]].Add(doors[i + 2]); }
            WallX(wall, z0, x0, x1, h, g[0].ToArray());
            WallX(wall, z1, x0, x1, h, g[1].ToArray());
            WallZ(wall, x0, z0, z1, h, g[2].ToArray());
            WallZ(wall, x1, z0, z1, h, g[3].ToArray());
            B(floor, x0, z0, x1, z1, 0f, 0.05f, false);
            B(RcMat.Roof, x0 - 0.3f, z0 - 0.3f, x1 + 0.3f, z1 + 0.3f, h, h + 0.35f, true);
            B(RcMat.Stone, x0 - 0.36f, z0 - 0.36f, x1 + 0.36f, z1 + 0.36f, h + 0.35f, h + 0.5f, false); // the roof's coping
            // Under the roof: strip lights, dead (nobody's paid the bill), and a skirting board round the walls.
            if (h <= 4.2f && (x1 - x0) * (z1 - z0) >= 60f)
                for (float fx = x0 + 3f; fx < x1 - 1f; fx += 6f)
                    for (float fz = z0 + 3f; fz < z1 - 1f; fz += 6f)
                    {
                        Vis(RcMat.Metal, fx - 0.65f, h - 0.1f, fz - 0.16f, fx + 0.65f, h - 0.03f, fz + 0.16f);
                        Vis(RcMat.Tile, fx - 0.6f, h - 0.13f, fz - 0.12f, fx + 0.6f, h - 0.1f, fz + 0.12f);
                    }
            if (h <= 4.2f)
            {
                float tt = T * 0.5f;
                Vis(RcMat.Stone, x0 + tt, 0.05f, z0 + tt, x1 - tt, 0.2f, z0 + tt + 0.03f);
                Vis(RcMat.Stone, x0 + tt, 0.05f, z1 - tt - 0.03f, x1 - tt, 0.2f, z1 - tt);
                Vis(RcMat.Stone, x0 + tt, 0.05f, z0 + tt, x0 + tt + 0.03f, 0.2f, z1 - tt);
                Vis(RcMat.Stone, x1 - tt - 0.03f, 0.05f, z0 + tt, x1 - tt, 0.2f, z1 - tt);
            }
            // Outside: windows along every wall (dark: nobody's home), clear of the doors, and a frame round each door.
            float t = T * 0.5f;
            for (int face = 0; face < 4; face++)
            {
                bool alongX = face < 2;
                float a0 = alongX ? x0 : z0, a1 = alongX ? x1 : z1;
                int count = (int)((a1 - a0 - 3f) / 5.5f);
                for (int k = 0; k <= count; k++)
                {
                    float a = a0 + 1.5f + (a1 - a0 - 3f) * (count == 0 ? 0.5f : k / (float)count);
                    bool door = false;
                    for (int d = 0; d + 1 < g[face].Count; d += 2) if (System.Math.Abs(a - g[face][d]) < g[face][d + 1] * 0.5f + 1.3f) door = true;
                    if (door) continue;
                    if (h >= 2.9f) Pane(face, x0 - t, z0 - t, x1 + t, z1 + t, a, 1.15f, RcMat.Window);
                    if (h >= 7.5f) Pane(face, x0 - t, z0 - t, x1 + t, z1 + t, a, 5.7f, RcMat.Window);
                }
                for (int d = 0; d + 1 < g[face].Count; d += 2)
                {
                    float c = g[face][d], w = g[face][d + 1] * 0.5f, top = h > 2.7f ? 2.6f : h;
                    OnFace(RcMat.Stone, face, x0 - t, z0 - t, x1 + t, z1 + t, c - w - 0.18f, c - w, 0f, top + 0.18f, 0.07f);
                    OnFace(RcMat.Stone, face, x0 - t, z0 - t, x1 + t, z1 + t, c + w, c + w + 0.18f, 0f, top + 0.18f, 0.07f);
                    if (h > 2.7f) OnFace(RcMat.Stone, face, x0 - t, z0 - t, x1 + t, z1 + t, c - w, c + w, top, top + 0.18f, 0.07f);
                }
            }
        }

        // A car: body, cabin with glass all round, wheels, bumpers, lights.
        private static void Car(float x, float z, bool alongX, RcMat paint)
        {
            float lx = alongX ? 2.2f : 0.95f, lz = alongX ? 0.95f : 2.2f;
            B(paint, x - lx, z - lz, x + lx, z + lz, 0.25f, 0.95f, true);
            B(RcMat.Black, x - lx * 0.9f, z - lz * 0.9f, x + lx * 0.9f, z + lz * 0.9f, 0.12f, 0.25f, false);
            // The cabin sits back of the middle: glass, then a painted roof.
            float cx = alongX ? x - 0.25f : x, cz = alongX ? z : z - 0.25f;
            float gx = alongX ? 1.15f : 0.84f, gz = alongX ? 0.84f : 1.15f;
            B(RcMat.Glass, cx - gx, cz - gz, cx + gx, cz + gz, 0.95f, 1.42f, false);
            B(paint, cx - gx * 0.96f, cz - gz * 0.96f, cx + gx * 0.96f, cz + gz * 0.96f, 1.42f, 1.5f, false);
            for (int sa = -1; sa <= 1; sa += 2)
            {
                // Bumpers and lights at each end (headlights at one, tail lights at the other).
                float end = sa * (alongX ? lx : lz);
                RcMat lamp = sa > 0 ? RcMat.Stone : RcMat.RedGlow;
                if (alongX)
                {
                    B(RcMat.Metal, x + end - 0.06f, z - lz * 0.98f, x + end + 0.06f, z + lz * 0.98f, 0.3f, 0.48f, false);
                    B(lamp, x + end - 0.03f, z - lz * 0.85f, x + end + 0.03f, z - lz * 0.5f, 0.62f, 0.8f, false);
                    B(lamp, x + end - 0.03f, z + lz * 0.5f, x + end + 0.03f, z + lz * 0.85f, 0.62f, 0.8f, false);
                }
                else
                {
                    B(RcMat.Metal, x - lx * 0.98f, z + end - 0.06f, x + lx * 0.98f, z + end + 0.06f, 0.3f, 0.48f, false);
                    B(lamp, x - lx * 0.85f, z + end - 0.03f, x - lx * 0.5f, z + end + 0.03f, 0.62f, 0.8f, false);
                    B(lamp, x + lx * 0.5f, z + end - 0.03f, x + lx * 0.85f, z + end + 0.03f, 0.62f, 0.8f, false);
                }
                // Wheels: two on each side, with hubcaps.
                for (int sb = -1; sb <= 1; sb += 2)
                {
                    float wa = sa * 1.35f, wb = sb * 0.97f;
                    if (alongX)
                    {
                        B(RcMat.Black, x + wa - 0.34f, z + wb - 0.1f, x + wa + 0.34f, z + wb + 0.1f, 0f, 0.68f, false);
                        B(RcMat.Metal, x + wa - 0.14f, z + wb + sb * 0.1f - 0.015f, x + wa + 0.14f, z + wb + sb * 0.1f + 0.015f, 0.2f, 0.48f, false);
                    }
                    else
                    {
                        B(RcMat.Black, x + wb - 0.1f, z + wa - 0.34f, x + wb + 0.1f, z + wa + 0.34f, 0f, 0.68f, false);
                        B(RcMat.Metal, x + wb + sb * 0.1f - 0.015f, z + wa - 0.14f, x + wb + sb * 0.1f + 0.015f, z + wa + 0.14f, 0.2f, 0.48f, false);
                    }
                }
            }
        }

        /// <summary>A board fence along a line (it's solid: you can't see or walk through).</summary>
        private static void BoardFence(float x0, float z0, float x1, float z1, float h, RcMat m)
        {
            bool alongX = System.Math.Abs(x1 - x0) > System.Math.Abs(z1 - z0);
            if (alongX) B(m, x0, z0 - 0.08f, x1, z0 + 0.08f, 0f, h, true);
            else B(m, x0 - 0.08f, z0, x0 + 0.08f, z1, 0f, h, true);
        }

        private static void Pile(int zone, float x, float z) { Pile(zone, x, 0f, z); }

        private static void Pile(int zone, float x, float y, float z)
        {
            // A heap of broken toys and scrap: you can search it. (One eye in it still glows, so you can find it in the dark.)
            B(RcMat.Rust, x - 0.7f, z - 0.6f, x + 0.7f, z + 0.6f, y, y + 0.45f, true);
            B(RcMat.Crate, x - 0.35f, z - 0.3f, x + 0.45f, z + 0.4f, y + 0.45f, y + 0.8f, false);
            B(RcMat.Metal, x - 0.6f, z - 0.2f, x - 0.1f, z + 0.5f, y + 0.45f, y + 0.62f, false);
            B(RcMat.Metal, x - 0.95f, z + 0.45f, x + 0.6f, z + 0.53f, y + 0.02f, y + 0.1f, false);      // a pipe
            B(RcMat.Plank, x + 0.5f, z - 0.75f, x + 0.62f, z + 0.35f, y, y + 0.62f, false);             // a board leaning on it
            B(RcMat.CarC, x + 0.12f, z - 0.62f, x + 0.5f, z - 0.34f, y + 0.45f, y + 0.6f, false);       // a toy's casing
            S.Ball(RcMat.Fabric, x - 0.3f, y + 0.6f, z - 0.32f, 0.26f, 0.22f, 0.24f);                   // what's left of a Tattletail
            S.Ball(RcMat.Fabric, x - 0.2f, y + 0.84f, z - 0.36f, 0.07f, 0.13f, 0.04f);
            S.Ball(RcMat.Fabric, x - 0.4f, y + 0.84f, z - 0.36f, 0.07f, 0.13f, 0.04f);
            B(RcMat.BlueGlow, x - 0.26f, z - 0.57f, x - 0.2f, z - 0.55f, y + 0.62f, y + 0.68f, false);
            S.Cyl(RcMat.Black, x + 0.85f, y, z + 0.1f, 0.3f, 0.2f, false);                              // a tire
            if (_collect) Crates.Add(new RcSpot(zone, x, y, z));
        }

        private static void Spawn(int zone, int enemy, int count, float x, float z) { Spawn(zone, enemy, count, x, 0f, z); }
        private static void Spawn(int zone, int enemy, int count, float x, float y, float z) { if (_collect) Spawns.Add(new RcSpawn(zone, enemy, count, x, y, z)); }

        private static bool _collect;

        static RcTown()
        {
            // Walk the whole plan once with nothing drawn, to collect the spawns and scrap piles.
            _collect = true;
            var none = new RcNullSink();
            for (int world = 0; world < 2; world++)
            {
                RcWorlds.Now = world;
                for (int part = PART_CORE; part < 5; part++) Build(none, part);
            }
            RcWorlds.Now = 0;
            _collect = false;
        }

        /// <summary>Emits one part of the town: PART_CORE (always there) or a zone's insides (0-4).</summary>
        public static void Build(RcSink sink, int part)
        {
            S = new RcRecord(sink);      // (it remembers what's solid, so the dressing finds room)
            _seed = 12345u + (uint)(part + 2) * 7919u;
            switch (part)
            {
                case PART_CORE: Core(); break;
                case 0: Suburbs(); break;
                case 1: Mall(); break;
                case 2: School(); break;
                case 3: Junkyard(); break;
                case 4: Warehouse(); break;
            }
            Dress(part);
            S = null;
        }

        // ── The core: ground, streets, the park and treehouse, the edges of everything ──
        private static void Core()
        {
            // The ground: one slab, in pieces round the holes where stairs go down through it.
            var xs = new List<float> { X0 - 10f, X1 + 10f };
            var zs = new List<float> { Z0 - 10f, Z1 + 10f };
            for (int i = 0; i < Holes.GetLength(0); i++) { xs.Add(Holes[i, 0]); xs.Add(Holes[i, 2]); zs.Add(Holes[i, 1]); zs.Add(Holes[i, 3]); }
            xs.Sort(); zs.Sort();
            for (int i = 0; i + 1 < xs.Count; i++)
                for (int k = 0; k + 1 < zs.Count; k++)
                {
                    if (xs[i + 1] - xs[i] < 0.001f || zs[k + 1] - zs[k] < 0.001f) continue;
                    float mx = (xs[i] + xs[i + 1]) * 0.5f, mz = (zs[k] + zs[k + 1]) * 0.5f;
                    bool hole = false;
                    for (int h = 0; h < Holes.GetLength(0); h++) if (mx > Holes[h, 0] && mx < Holes[h, 2] && mz > Holes[h, 1] && mz < Holes[h, 3]) hole = true;
                    if (!hole) S.Walk(RcMat.Grass, xs[i], -1f, zs[k], xs[i + 1], 0f, zs[k + 1]);
                }

            // The quarantine fence: posts, rails and recall tape. Solid to walk into.
            B(RcMat.None, X0 - 1f, Z0 - 1f, X0, Z1 + 1f, 0f, 6f, true);
            B(RcMat.None, X1, Z0 - 1f, X1 + 1f, Z1 + 1f, 0f, 6f, true);
            B(RcMat.None, X0, Z0 - 1f, X1, Z0, 0f, 6f, true);
            B(RcMat.None, X0, Z1, X1, Z1 + 1f, 0f, 6f, true);
            for (float x = X0; x <= X1; x += 8f)
            {
                B(RcMat.Metal, x - 0.08f, Z0 - 0.08f, x + 0.08f, Z0 + 0.08f, 0f, 3.2f, false);
                B(RcMat.Metal, x - 0.08f, Z1 - 0.08f, x + 0.08f, Z1 + 0.08f, 0f, 3.2f, false);
            }
            for (float z = Z0; z <= Z1; z += 8f)
            {
                B(RcMat.Metal, X0 - 0.08f, z - 0.08f, X0 + 0.08f, z + 0.08f, 0f, 3.2f, false);
                B(RcMat.Metal, X1 - 0.08f, z - 0.08f, X1 + 0.08f, z + 0.08f, 0f, 3.2f, false);
            }
            for (int k = 0; k < 3; k++)
            {
                float y = 0.9f + k * 1f;
                RcMat m = k == 1 ? RcMat.Tape : RcMat.Metal;
                float t = k == 1 ? 0.18f : 0.05f;
                B(m, X0, Z0 - 0.03f, X1, Z0 + 0.03f, y, y + t, false);
                B(m, X0, Z1 - 0.03f, X1, Z1 + 0.03f, y, y + t, false);
                B(m, X0 - 0.03f, Z0, X0 + 0.03f, Z1, y, y + t, false);
                B(m, X1 - 0.03f, Z0, X1 + 0.03f, Z1, y, y + t, false);
            }

            // Streets (drawn only: the ground is what you stand on).
            Street(-4.5f, -215f, 4.5f, -PARK, false);           // Main Street, south
            Street(-4.5f, PARK, 4.5f, 110f, false);             // Main Street, north (to the warehouse gate)
            Street(-115f, -4f, -PARK, 4f, true);                // west, to the school
            Street(PARK, -4f, 128f, 4f, true);                  // east, to the mall
            Street(4.5f, 76f, 128f, 84f, true);                 // north-east, to the junkyard
            Street(-17f, -134f, -4.5f, -126f, true);            // Maple Street's mouth
            B(RcMat.Road, 4.5f, -202f, 200f, -194f, 0f, 0.05f, false);  // the road out of town
            // ...which is closed.
            B(RcMat.Concrete, 58f, -203f, 59.2f, -193f, 0f, 1.1f, true);
            B(RcMat.Tape, 57.9f, -203f, 58f, -193f, 0.5f, 0.75f, false);
            B(RcMat.Concrete, 62f, -203f, 63.2f, -193f, 0f, 1.1f, true);
            B(RcMat.RedGlow, 60.4f, -198.3f, 60.8f, -197.7f, 1.1f, 1.4f, false);
            S.Light(60.6f, 1.6f, -198f, 3, 7f, 1.2f);

            // Street lamps: the lit way.
            for (int i = 0; i < 7; i++) { float z = -205f + i * 24f; Lamp(i % 2 == 0 ? -5.6f : 5.6f, z, i % 2 == 0 ? 1f : -1f, 0f, true); }
            for (int i = 0; i < 3; i++) { float z = 56f + i * 20f; Lamp(i % 2 == 0 ? 5.6f : -5.6f, z, i % 2 == 0 ? -1f : 1f, 0f, true); }
            for (int i = 0; i < 3; i++) { float x = -62f - i * 22f; Lamp(x, i % 2 == 0 ? 5.1f : -5.1f, 0f, i % 2 == 0 ? -1f : 1f, true); }
            for (int i = 0; i < 4; i++) { float x = 60f + i * 20f; Lamp(x, i % 2 == 0 ? -5.1f : 5.1f, 0f, i % 2 == 0 ? 1f : -1f, true); }
            for (int i = 0; i < 4; i++) { float x = 20f + i * 22f; Lamp(x, i % 2 == 0 ? 85.1f : 74.9f, 0f, i % 2 == 0 ? -1f : 1f, i != 3); }

            Park();
            Treehouse();

            // The Arcade: you came out of its door.
            B(RcMat.Brick, 9f, -191f, 49f, -168f, 0f, 6.5f, true);
            B(RcMat.Roof, 8.6f, -191.4f, 49.4f, -167.6f, 6.5f, 7f, false);
            B(RcMat.Door, 8.94f, -180.8f, 9f, -179.2f, 0f, 2.3f, false);
            B(RcMat.Neon, 8.88f, -186f, 9f, -174f, 3.6f, 4.7f, false);
            B(RcMat.Glow, 8.94f, -184f, 9f, -182f, 1f, 2.4f, false);
            B(RcMat.Glow, 8.94f, -178f, 9f, -176f, 1f, 2.4f, false);
            S.Light(6.5f, 3.6f, -180f, 1, 11f, 1.5f);

            // House fronts along Main Street (the east side stops for the Arcade, the west for Maple Street).
            RcMat[] paints = { RcMat.House, RcMat.HouseB, RcMat.HouseC };
            for (float z = -212f; z < -60f; z += 13f)
            {
                if (!(z + 11f > -137f && z < -123f)) Front(-16.4f, z, -7.2f, z + 11f, 5f + Rnd(3) * 0.7f, 3, paints[Rnd(3)]);
                if (!(z + 11f > -204f && z < -167f) && z < -62f) Front(7.2f, z, 16.4f, z + 11f, 5f + Rnd(3) * 0.7f, 2, paints[Rnd(3)]);
            }
            // ...and a few along the north stretch and the side roads.
            for (float z = 50f; z < 70f; z += 13f) { Front(-16.4f, z, -7.2f, z + 11f, 5.4f, 3, paints[Rnd(3)]); Front(7.2f, z, 16.4f, z + 11f, 5f, 2, paints[Rnd(3)]); }
            for (float x = -108f; x < -60f; x += 14f) { Front(x, 6.6f, x + 12f, 15.6f, 5.2f, 0, paints[Rnd(3)]); Front(x, -15.6f, x + 12f, -6.6f, 5f, 1, paints[Rnd(3)]); }
            for (float x = 52f; x < 110f; x += 14f) { Front(x, 6.6f, x + 12f, 15.6f, 5f, 0, paints[Rnd(3)]); Front(x, -15.6f, x + 12f, -6.6f, 5.3f, 1, paints[Rnd(3)]); }
            for (float x = 22f; x < 90f; x += 14f) Front(x, 86.6f, x + 12f, 95.6f, 5f, 0, paints[Rnd(3)]);

            // The rail tracks: down the east side, then off to the east.
            for (int r = -1; r <= 1; r += 2)
            {
                B(RcMat.Metal, 108f + r * 0.75f - 0.05f, 29f, 108f + r * 0.75f + 0.05f, Z1, 0.1f, 0.2f, false);
                B(RcMat.Metal, 126f, 11f + r * 0.75f - 0.05f, X1, 11f + r * 0.75f + 0.05f, 0.1f, 0.2f, false);
            }
            for (float z = 30f; z < Z1; z += 1.6f) B(RcMat.Plank, 106.8f, z, 109.2f, z + 0.3f, 0f, 0.1f, false);
            for (float x = 127f; x < X1; x += 1.6f) B(RcMat.Plank, x, 9.8f, x + 0.3f, 12.2f, 0f, 0.1f, false);
            B(RcMat.Dirt, 106.3f, 29f, 109.7f, Z1, 0f, 0.04f, false);
            B(RcMat.Dirt, 126f, 9.3f, X1, 12.7f, 0f, 0.04f, false);

            Station();

            // The signal box by the rail crossing: the junkyard's fort.
            Shell(115f, 89f, 121f, 95f, 3f, RcMat.Plank, RcMat.Plank, 0f, 118f, 2.6f);
            B(RcMat.Glass, 114.9f, 90.5f, 115f, 93.5f, 1.1f, 2.2f, false);
            B(RcMat.RedGlow, 110.4f, 86.4f, 110.8f, 86.8f, 2.6f, 3f, false);
            B(RcMat.Metal, 110.52f, 86.52f, 110.68f, 86.68f, 0f, 2.6f, false);

            // Every zone's fence, with its one way in. (The insides are built when you get near.)
            RcMat[] fence = { RcMat.Plank, RcMat.Concrete, RcMat.Brick, RcMat.Rust, RcMat.Concrete };
            for (int i = 0; i < 5; i++) ZoneFence(i, fence[i]);

            // Landmarks: one per zone, tall enough to find your way by.
            WaterTower(-207f, -202f);
            MallSign(133f, -24f);
            ClockTower(-153f, 12f);
            Crane(252f, 164f);
            Smokestack(-88f, 117f);

            Trees();
        }

        /// <summary>
        /// The junkyard station: a platform beside the rails with a roof, a departure board, and a freight train waiting
        /// (an engine and two cars). It's where the first world ends.
        /// </summary>
        private static void Station()
        {
            B(RcMat.Concrete, 98.5f, 38f, 106.5f, 66f, 0f, 0.07f, false);                    // the platform (flat: you walk straight onto it)
            for (float z = 40f; z <= 64f; z += 8f) B(RcMat.Metal, 99f, z - 0.12f, 99.25f, z + 0.12f, 0f, 3.2f, true);   // the roof's posts
            B(RcMat.Roof, 98.2f, 39f, 106.8f, 65f, 3.2f, 3.4f, false);
            // The board: dark, with a lit line for each station.
            B(RcMat.Metal, 101.4f, 51.9f, 101.55f, 52.1f, 0f, 2.3f, true);
            B(RcMat.Black, 100.9f, 49.4f, 101.6f, 54.6f, 1.6f, 2.8f, false);
            B(RcMat.BlueGlow, 101.6f, 50f, 101.64f, 54f, 2.45f, 2.55f, false);
            B(RcMat.BlueGlow, 101.6f, 50f, 101.64f, 52.5f, 2.2f, 2.3f, false);
            B(RcMat.RedGlow, 101.6f, 50f, 101.64f, 51f, 1.95f, 2.05f, false);
            B(RcMat.Plank, 99.4f, 56f, 100.6f, 59f, 0.07f, 0.55f, true);                     // a bench
            // The train: an engine and two cars on the rails, with the engine's lamp lit.
            B(RcMat.Black, 106.7f, 40f, 109.3f, 48.5f, 0.35f, 3.4f, true);
            B(RcMat.Metal, 106.5f, 39.8f, 109.5f, 48.7f, 0.2f, 0.4f, false);
            B(RcMat.BlueGlow, 107.4f, 39.7f, 108.6f, 39.8f, 2.4f, 2.8f, false);
            B(RcMat.Rust, 106.6f, 49.5f, 109.4f, 57.5f, 0.35f, 3.1f, true);
            B(RcMat.Plank, 106.4f, 53f, 106.6f, 54.2f, 0.4f, 2.8f, false);                   // the car's open door
            B(RcMat.Rust, 106.6f, 58.5f, 109.4f, 66.5f, 0.35f, 3.1f, true);
            B(RcMat.Plank, 106.4f, 62f, 106.6f, 63.2f, 0.4f, 2.8f, false);
        }

        private static void Street(float x0, float z0, float x1, float z1, bool alongX)
        {
            B(RcMat.Road, x0, z0, x1, z1, 0f, 0.05f, false);
            if (alongX)
            {
                B(RcMat.Walk, x0, z1, x1, z1 + 2.2f, 0f, 0.1f, false);
                B(RcMat.Walk, x0, z0 - 2.2f, x1, z0, 0f, 0.1f, false);
                float mid = (z0 + z1) * 0.5f;
                for (float x = x0 + 2f; x < x1 - 2f; x += 5f) B(RcMat.Line, x, mid - 0.09f, x + 2.2f, mid + 0.09f, 0.05f, 0.08f, false);
            }
            else
            {
                B(RcMat.Walk, x1, z0, x1 + 2.2f, z1, 0f, 0.1f, false);
                B(RcMat.Walk, x0 - 2.2f, z0, x0, z1, 0f, 0.1f, false);
                float mid = (x0 + x1) * 0.5f;
                for (float z = z0 + 2f; z < z1 - 2f; z += 5f) B(RcMat.Line, mid - 0.09f, z, mid + 0.09f, z + 2.2f, 0.05f, 0.08f, false);
            }
        }

        private static void Park()
        {
            float p = PARK;
            B(RcMat.Lawn, -p, -p, p, p, 0f, 0.03f, false);
            B(RcMat.Path, -1.6f, -p, 1.6f, p, 0.03f, 0.06f, false);
            B(RcMat.Path, -p, -1.6f, p, 1.6f, 0.03f, 0.06f, false);
            // A low iron fence, open where the paths meet the streets.
            for (int s = -1; s <= 1; s += 2)
            {
                ParkFence(-p, s * p, -5f, s * p); ParkFence(5f, s * p, p, s * p);
                ParkFence(s * p, -p, s * p, -5f); ParkFence(s * p, 5f, s * p, p);
            }
            // Benches along the paths.
            for (int s = -1; s <= 1; s += 2)
                for (int k = 0; k < 2; k++)
                {
                    float z = k == 0 ? -26f : 24f, x = s * 2.7f;
                    B(RcMat.Wood, x - 0.25f, z - 0.85f, x + 0.25f, z + 0.85f, 0.38f, 0.46f, true);
                    B(RcMat.Wood, x + s * 0.2f, z - 0.85f, x + s * 0.28f, z + 0.85f, 0.46f, 0.95f, false);
                    B(RcMat.Metal, x - 0.22f, z - 0.75f, x + 0.22f, z - 0.68f, 0f, 0.38f, false);
                    B(RcMat.Metal, x - 0.22f, z + 0.68f, x + 0.22f, z + 0.75f, 0f, 0.38f, false);
                }
            // The dry fountain and the bandstand.
            S.Cyl(RcMat.Stone, -26f, 0f, -24f, 3.4f, 0.6f, true);
            S.Cyl(RcMat.Stone, -26f, 0.6f, -24f, 0.4f, 1.9f, false);
            S.Cyl(RcMat.Stone, -26f, 2.4f, -24f, 1.3f, 0.2f, false);
            S.Cyl(RcMat.Wood, 26f, 0f, 24f, 4.2f, 0.5f, true);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 1.0472f, x = 26f + (float)System.Math.Cos(a) * 3.7f, z = 24f + (float)System.Math.Sin(a) * 3.7f;
                B(RcMat.Wood, x - 0.09f, z - 0.09f, x + 0.09f, z + 0.09f, 0.5f, 3.6f, false);
            }
            S.Cyl(RcMat.Roof, 26f, 3.6f, 24f, 4.7f, 0.3f, false);
            B(RcMat.Glow, 25.85f, 23.85f, 26.15f, 24.15f, 3.3f, 3.5f, false);
            S.Light(26f, 3f, 24f, 0, 9f, 1.1f);
            // A few smaller trees.
            float[] tx = { -34f, 32f, -30f, 35f, -38f, 38f, -14f, 16f }, tz = { -34f, -30f, 34f, 12f, 10f, -14f, 36f, -38f };
            for (int i = 0; i < tx.Length; i++) Tree(tx[i], tz[i], 0.9f + (i % 3) * 0.15f);
        }

        private static void ParkFence(float x0, float z0, float x1, float z1)
        {
            bool alongX = System.Math.Abs(x1 - x0) > System.Math.Abs(z1 - z0);
            if (alongX)
            {
                B(RcMat.None, x0, z0 - 0.1f, x1, z0 + 0.1f, 0f, 2.2f, true);
                B(RcMat.Metal, x0, z0 - 0.03f, x1, z0 + 0.03f, 0.82f, 0.88f, false);
                for (float x = x0; x <= x1 + 0.01f; x += 2f) B(RcMat.Metal, x - 0.05f, z0 - 0.05f, x + 0.05f, z0 + 0.05f, 0f, 0.95f, false);
            }
            else
            {
                B(RcMat.None, x0 - 0.1f, z0, x0 + 0.1f, z1, 0f, 2.2f, true);
                B(RcMat.Metal, x0 - 0.03f, z0, x0 + 0.03f, z1, 0.82f, 0.88f, false);
                for (float z = z0; z <= z1 + 0.01f; z += 2f) B(RcMat.Metal, x0 - 0.05f, z - 0.05f, x0 + 0.05f, z + 0.05f, 0f, 0.95f, false);
            }
        }

        // The bench (x -6.4..-3.4, z 3.2..4.4): a heavy top with a vise, tools hung on a pegboard behind, jars of parts, a lamp.
        private static void Bench(float y)
        {
            B(RcMat.None, -6.4f, 3.2f, -3.4f, 4.4f, y, y + 0.95f, true);
            Vis(RcMat.Wood, -6.45f, y + 0.85f, 3.15f, -3.35f, y + 0.95f, 4.45f);                               // the top
            Vis(RcMat.Plank, -6.35f, y + 0.72f, 3.25f, -3.45f, y + 0.85f, 4.35f);
            foreach (float lx in new[] { -6.35f, -3.6f })
                foreach (float lz in new[] { 3.25f, 4.15f }) Vis(RcMat.Wood, lx, y, lz, lx + 0.15f, y + 0.72f, lz + 0.15f);
            Vis(RcMat.Plank, -6.3f, y + 0.22f, 3.3f, -3.5f, y + 0.27f, 4.3f);                                  // the shelf underneath
            Vis(RcMat.Crate, -6.1f, y + 0.27f, 3.4f, -5.4f, y + 0.6f, 4.0f);                                   // a box of scrap on it
            Vis(RcMat.Rust, -4.5f, y + 0.27f, 3.45f, -3.7f, y + 0.5f, 4.1f);
            // On the bench: a vise, an anvil block, a coil of springs, screw jars.
            Vis(RcMat.Metal, -6.3f, y + 0.95f, 3.3f, -5.8f, y + 1.15f, 3.7f);
            Vis(RcMat.Metal, -6.3f, y + 1.15f, 3.3f, -6.22f, y + 1.3f, 3.7f);
            Vis(RcMat.Metal, -6.0f, y + 1.15f, 3.3f, -5.9f, y + 1.27f, 3.7f);
            Rod(-5.95f, y + 1.0f, 3.5f, -5.65f, y + 1.0f, 3.5f, 0.05f, RcMat.Metal);                            // its handle
            Vis(RcMat.Rust, -5.1f, y + 0.95f, 3.5f, -4.4f, y + 1.1f, 4.1f);                                    // anvil
            Vis(RcMat.Rust, -4.95f, y + 1.1f, 3.6f, -4.55f, y + 1.22f, 4.0f);
            for (int i = 0; i < 6; i++) S.Cyl(RcMat.Metal, -4.15f, y + 0.95f + i * 0.045f, 3.75f, 0.09f - i * 0.004f, 0.03f, false); // springs
            for (int i = 0; i < 3; i++)
            {
                S.Cyl(RcMat.Glass, -3.75f, y + 0.95f, 3.5f + i * 0.28f, 0.1f, 0.24f, false);
                S.Cyl(RcMat.Rust, -3.75f, y + 0.95f, 3.5f + i * 0.28f, 0.085f, 0.11f, false);
                Vis(RcMat.Metal, -3.83f, y + 1.19f, 3.42f + i * 0.28f, -3.67f, y + 1.23f, 3.58f + i * 0.28f);
            }
            // The pegboard behind, on two posts, with tools on it.
            Vis(RcMat.Wood, -6.45f, y, 4.5f, -6.3f, y + 2.5f, 4.65f);
            Vis(RcMat.Wood, -3.5f, y, 4.5f, -3.35f, y + 2.5f, 4.65f);
            Vis(RcMat.Plank, -6.45f, y + 1.0f, 4.5f, -3.35f, y + 2.4f, 4.56f);
            Vis(RcMat.Wood, -6.5f, y + 2.4f, 4.46f, -3.3f, y + 2.5f, 4.7f);
            Vis(RcMat.Tape, -6.4f, y + 1.05f, 4.48f, -3.4f, y + 1.09f, 4.5f);                                  // recall tape along its foot
            float ty = y + 1.15f;
            Rod(-6.1f, ty + 0.9f, 4.44f, -6.1f, ty + 0.15f, 4.44f, 0.05f, RcMat.Wood);                          // a hammer
            Vis(RcMat.Metal, -6.22f, ty + 0.9f, 4.4f, -5.98f, ty + 1.05f, 4.48f);
            Rod(-5.6f, ty + 1.0f, 4.44f, -5.6f, ty + 0.1f, 4.44f, 0.045f, RcMat.Metal);                         // a wrench
            Vis(RcMat.Metal, -5.68f, ty + 0.05f, 4.42f, -5.52f, ty + 0.16f, 4.47f);
            Vis(RcMat.Metal, -5.25f, ty + 0.25f, 4.42f, -4.55f, ty + 0.85f, 4.47f);                             // a saw
            Vis(RcMat.Wood, -4.62f, ty + 0.62f, 4.4f, -4.5f, ty + 0.98f, 4.47f);
            Rod(-4.2f, ty + 0.9f, 4.44f, -4.2f, ty + 0.2f, 4.44f, 0.035f, RcMat.Metal);                         // pliers
            Rod(-4.05f, ty + 0.9f, 4.44f, -4.05f, ty + 0.2f, 4.44f, 0.035f, RcMat.Metal);
            Vis(RcMat.RedGlow, -4.25f, ty + 0.1f, 4.4f, -4.0f, ty + 0.3f, 4.47f);
            // A lantern on a hook over it all, and the light it gives.
            Rod(-4.9f, y + 2.5f, 4.1f, -4.9f, y + 2.05f, 4.1f, 0.02f, RcMat.Metal);
            Vis(RcMat.Metal, -5.02f, y + 1.9f, 3.98f, -4.78f, y + 1.95f, 4.22f);
            Vis(RcMat.Glow, -5.0f, y + 1.95f, 4.0f, -4.8f, y + 2.15f, 4.2f);
            Vis(RcMat.Metal, -5.02f, y + 2.15f, 3.98f, -4.78f, y + 2.2f, 4.22f);
            S.Light(-4.9f, y + 1.8f, 3.6f, 0, 6f, 1.1f);
            // A stool to sit on.
            S.Cyl(RcMat.Plank, -5.0f, y + 0.5f, 2.55f, 0.24f, 0.06f, false);
            for (int i = 0; i < 3; i++) { float a = i * 2.094f; Rod(-5.0f + (float)System.Math.Cos(a) * 0.16f, y, 2.55f + (float)System.Math.Sin(a) * 0.16f, -5.0f + (float)System.Math.Cos(a) * 0.2f, y + 0.5f, 2.55f + (float)System.Math.Sin(a) * 0.2f, 0.05f, RcMat.Wood); }
        }

        // The quest board (x 3.2..5.8 against z 6.2, facing the deck): a cork board in a frame with notes pinned to it, a lantern beside it.
        private static void QuestBoard(float y)
        {
            Vis(RcMat.Wood, 3.15f, y, 6.15f, 3.35f, y + 2.5f, 6.4f);                                           // the posts
            Vis(RcMat.Wood, 5.65f, y, 6.15f, 5.85f, y + 2.5f, 6.4f);
            Vis(RcMat.Wood, 3.15f, y + 0.85f, 6.2f, 5.85f, y + 2.4f, 6.35f);                                    // the frame
            Vis(RcMat.Path, 3.3f, y + 0.95f, 6.1f, 5.7f, y + 2.3f, 6.2f);                                       // the cork
            Vis(RcMat.Wood, 3.15f, y + 2.4f, 6.1f, 5.85f, y + 2.5f, 6.4f);                                      // its little roof
            Vis(RcMat.Tape, 3.3f, y + 0.95f, 6.06f, 5.7f, y + 1.02f, 6.1f);
            for (int i = 0; i < 7; i++)
            {
                float nx = 3.4f + (i % 4) * 0.58f + Vary(i, 1f, 1) * 0.12f, ny = y + 1.15f + (i / 4) * 0.5f + Vary(i, 2f, 2) * 0.1f;
                Vis(i % 3 == 0 ? RcMat.Tape : RcMat.Tile, nx, ny, 6.04f, nx + 0.4f, ny + 0.32f, 6.1f);          // a note...
                Vis(RcMat.RedGlow, nx + 0.18f, ny + 0.27f, 6.02f, nx + 0.23f, ny + 0.32f, 6.04f);                // ...and its pin
                Vis(RcMat.Black, nx + 0.05f, ny + 0.18f, 6.03f, nx + 0.33f, ny + 0.2f, 6.04f);                   // a line of writing
                Vis(RcMat.Black, nx + 0.05f, ny + 0.1f, 6.03f, nx + 0.25f, ny + 0.12f, 6.04f);
            }
            Vis(RcMat.Tile, 4.15f, y + 1.7f, 6.03f, 4.95f, y + 2.25f, 6.04f);                                   // a big sheet: the map of the town
            Vis(RcMat.Black, 4.2f, y + 1.85f, 6.025f, 4.9f, y + 1.88f, 6.03f);
            Vis(RcMat.RedGlow, 4.55f, y + 1.97f, 6.02f, 4.62f, y + 2.03f, 6.03f);
            // A lantern on a bracket at its side.
            Vis(RcMat.Metal, 5.85f, y + 1.9f, 6.0f, 6.15f, y + 1.95f, 6.05f);
            Vis(RcMat.Glow, 6.0f, y + 1.65f, 5.95f, 6.15f, y + 1.9f, 6.1f);
            S.Light(6.0f, y + 1.7f, 5.6f, 0, 6f, 1f);
        }

        // The stash (x -4.6..-2.8, z 3.4..4.6): a big iron-bound chest with a padlock, and a couple of boxes on it.
        private static void Chest(float y)
        {
            B(RcMat.None, -4.6f, 3.4f, -2.8f, 4.6f, y, y + 0.95f, true);
            Vis(RcMat.Plank, -4.6f, y, 3.4f, -2.8f, y + 0.62f, 4.6f);                                          // the body
            Vis(RcMat.Wood, -4.66f, y + 0.62f, 3.34f, -2.74f, y + 0.72f, 4.66f);                                // the lid's edge
            Vis(RcMat.Plank, -4.6f, y + 0.72f, 3.4f, -2.8f, y + 0.85f, 4.6f);                                   // ...and its domed top
            Vis(RcMat.Plank, -4.5f, y + 0.85f, 3.5f, -2.9f, y + 0.92f, 4.5f);
            foreach (float bx in new[] { -4.5f, -3.85f, -3.0f })
            {
                Vis(RcMat.Metal, bx, y, 3.36f, bx + 0.12f, y + 0.93f, 4.64f);                                   // straps over the lid and down the front
                Vis(RcMat.Rust, bx - 0.01f, y + 0.3f, 3.34f, bx + 0.13f, y + 0.36f, 3.4f);                       // rivet plates
            }
            foreach (float cx in new[] { -4.62f, -2.9f })
                foreach (float cz in new[] { 3.36f, 4.52f }) Vis(RcMat.Metal, cx, y, cz, cx + 0.12f, y + 0.72f, cz + 0.12f); // corner irons
            Vis(RcMat.Metal, -3.97f, y + 0.5f, 3.3f, -3.63f, y + 0.72f, 3.4f);                                  // the hasp
            Vis(RcMat.Tape, -3.9f, y + 0.4f, 3.24f, -3.7f, y + 0.55f, 3.34f);                                   // and a padlock
            Vis(RcMat.Crate, -4.4f, y + 0.92f, 3.7f, -3.9f, y + 1.2f, 4.2f);                                    // boxes on the lid
            Vis(RcMat.Plank, -4.42f, y + 0.92f, 3.68f, -3.88f, y + 0.98f, 4.22f);
            S.Ball(RcMat.Fabric, -3.4f, y + 1.05f, 4.0f, 0.16f, 0.14f, 0.15f);                                  // a Tattletail, left there
        }

        private static void Rod(float x0, float y0, float z0, float x1, float y1, float z1, float thick, RcMat m)
        {
            // (a rod as a thin box along its longest axis: enough for tool handles and legs)
            float dx = System.Math.Abs(x1 - x0), dy = System.Math.Abs(y1 - y0), dz = System.Math.Abs(z1 - z0), h = thick * 0.5f;
            Vis(m, System.Math.Min(x0, x1) - (dx > 0.001f ? 0f : h), System.Math.Min(y0, y1) - (dy > 0.001f ? 0f : h), System.Math.Min(z0, z1) - (dz > 0.001f ? 0f : h),
                   System.Math.Max(x0, x1) + (dx > 0.001f ? 0f : h), System.Math.Max(y0, y1) + (dy > 0.001f ? 0f : h), System.Math.Max(z0, z1) + (dz > 0.001f ? 0f : h));
        }

        private static void Treehouse()
        {
            if (RcWorlds.Egypt)
            {
                // The watchtower's core: a sandstone obelisk the Resistance built the decks round (Last Stop's).
                S.Cyl(RcMat.Sandstone, 0f, 0f, 0f, 1.7f, 17f, true);
                for (int i = 0; i < 6; i++) { float oh = 2.4f - i * 0.28f, oy = i * 3f; B(RcMat.Sandstone, -oh, -oh, oh, oh, oy, oy + 3f, false); }
                B(RcMat.Gilt, -0.7f, -0.7f, 0.7f, 0.7f, 17f, 19.5f, false);
                B(RcMat.BlueGlow, -0.3f, -0.3f, 0.3f, 0.3f, 19.5f, 20.4f, false);
                S.Light(0f, 20f, 0f, 2, 24f, 1.4f);
            }
            else
            {
            // The oak.
            S.Cyl(RcMat.Bark, 0f, 0f, 0f, 1.7f, 17f, true);
            S.Ball(RcMat.Leaf, 0f, 19f, 0f, 11f, 6.5f, 11f);
            S.Ball(RcMat.Leaf, 6f, 16.5f, 3f, 6f, 4f, 6f);
            S.Ball(RcMat.Leaf, -6f, 17f, -3f, 6.5f, 4f, 6.5f);
            S.Ball(RcMat.Leaf, 1f, 16f, -7f, 5.5f, 3.5f, 5.5f);
            }
            // Three decks around the trunk, each with a railing. You climb between them (the ladders are the game's).
            float[] half = { DECK_HALF0, DECK_HALF1, DECK_HALF2 };
            for (int d = 0; d < 3; d++)
            {
                float y = DeckY[d], h = half[d];
                // The deck: planks round a hatch where the ladder comes through. (What you stand on covers the
                // hatch too: nobody falls down it. Climbing goes through.)
                B(RcMat.None, -h, -h, h, h, y - 0.25f, y, true);
                B(RcMat.Plank, -h, -h, Hatch[0], h, y - 0.25f, y, false);
                B(RcMat.Plank, Hatch[2], -h, h, h, y - 0.25f, y, false);
                B(RcMat.Plank, Hatch[0], -h, Hatch[2], Hatch[1], y - 0.25f, y, false);
                B(RcMat.Plank, Hatch[0], Hatch[3], Hatch[2], h, y - 0.25f, y, false);
                for (int s = -1; s <= 1; s += 2)
                {
                    B(RcMat.None, -h, s * h - 0.1f, h, s * h + 0.1f, y, y + 2.4f, true);
                    B(RcMat.None, s * h - 0.1f, -h, s * h + 0.1f, h, y, y + 2.4f, true);
                    B(RcMat.Wood, -h, s * h - 0.04f, h, s * h + 0.04f, y + 0.95f, y + 1.05f, false);
                    B(RcMat.Wood, s * h - 0.04f, -h, s * h + 0.04f, h, y + 0.95f, y + 1.05f, false);
                    for (float t = -h; t <= h + 0.01f; t += h / 3f)
                    {
                        B(RcMat.Wood, t - 0.05f, s * h - 0.05f, t + 0.05f, s * h + 0.05f, y, y + 1.05f, false);
                        B(RcMat.Wood, s * h - 0.05f, t - 0.05f, s * h + 0.05f, t + 0.05f, y, y + 1.05f, false);
                    }
                }
                // Posts down to the deck below (or the ground), and lanterns at the corners.
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        float px = sx * (h - 0.3f), pz = sz * (h - 0.3f), below = d == 0 ? 0f : DeckY[d - 1];
                        B(RcMat.Wood, px - 0.1f, pz - 0.1f, px + 0.1f, pz + 0.1f, below, y - 0.25f, d == 0);
                        B(RcMat.Glow, px - 0.1f, pz - 0.1f, px + 0.1f, pz + 0.1f, y + 1.05f, y + 1.3f, false);
                    }
                S.Light(h - 1f, y + 1.8f, h - 1f, 0, 10f, 1.2f);
                S.Light(-h + 1f, y + 1.8f, -h + 1f, 0, 10f, 1.2f);
            }
            // The ladder up the trunk: rungs and two rails, from the ground to above the top deck.
            for (int i = 0; i < 30; i++) B(RcMat.Wood, -0.35f, -1.9f, 0.35f, -1.78f, 0.4f + i * 0.42f, 0.46f + i * 0.42f, false);
            B(RcMat.Wood, -0.41f, -1.92f, -0.35f, -1.76f, 0f, DeckY[2] + 1.1f, false);
            B(RcMat.Wood, 0.35f, -1.92f, 0.41f, -1.76f, 0f, DeckY[2] + 1.1f, false);

            // Lower deck: the crafting bench and the quest board.
            float y0 = DeckY[0];
            Bench(y0);
            QuestBoard(y0);
            // Middle deck: the stash.
            float y1 = DeckY[1];
            Chest(y1);
            // Top deck: the ziplines' posts (one toward each fort) and the lookout.
            float y2 = DeckY[2];
            for (int i = 0; i < 4; i++)
            {
                float dx = Fort[i, 0], dz = Fort[i, 1], len = (float)System.Math.Sqrt(dx * dx + dz * dz);
                float px = dx / len * 3.6f, pz = dz / len * 3.6f;
                B(RcMat.Wood, px - 0.12f, pz - 0.12f, px + 0.12f, pz + 0.12f, y2, y2 + 2.6f, false);
                B(RcMat.Tape, px - 0.2f, pz - 0.2f, px + 0.2f, pz + 0.2f, y2 + 2.6f, y2 + 2.8f, false);
            }
            // Lanterns round the foot of the tree.
            for (int i = 0; i < 6; i++)
            {
                float a = i * 1.0472f + 0.5f, x = (float)System.Math.Cos(a) * 9f, z = (float)System.Math.Sin(a) * 9f;
                B(RcMat.Metal, x - 0.04f, z - 0.04f, x + 0.04f, z + 0.04f, 0f, 1.2f, false);
                B(RcMat.Glow, x - 0.12f, z - 0.12f, x + 0.12f, z + 0.12f, 1.2f, 1.5f, false);
                if (i % 2 == 0) S.Light(x, 1.6f, z, 0, 9f, 1.2f);
            }
        }

        /// <summary>A zone's fence: solid all the way round, open only at its gate (the gate itself is the game's).</summary>
        private static void ZoneFence(int i, RcMat m)
        {
            float x0 = Zone[i, 0], z0 = Zone[i, 1], x1 = Zone[i, 2], z1 = Zone[i, 3];
            float gx0 = Gate[i, 0], gz0 = Gate[i, 1], gx1 = Gate[i, 2], gz1 = Gate[i, 3];
            const float h = 3.4f;
            bool gateOnX = gx1 - gx0 > gz1 - gz0; // the gap runs along x: it's in a north or south side
            float gxc = (gx0 + gx1) * 0.5f, gzc = (gz0 + gz1) * 0.5f;
            // south, north
            const float PB = 0.5f; // a gate post's depth: the fence ends where the post begins
            if (gateOnX && System.Math.Abs(gzc - z0) < 1f) { FenceX(m, z0, x0, gx0 - PB, h); FenceX(m, z0, gx1 + PB, x1, h); } else FenceX(m, z0, x0, x1, h);
            if (gateOnX && System.Math.Abs(gzc - z1) < 1f) { FenceX(m, z1, x0, gx0 - PB, h); FenceX(m, z1, gx1 + PB, x1, h); } else FenceX(m, z1, x0, x1, h);
            // west, east
            if (!gateOnX && System.Math.Abs(gxc - x0) < 1f) { FenceZ(m, x0, z0, gz0 - PB, h); FenceZ(m, x0, gz1 + PB, z1, h); } else FenceZ(m, x0, z0, z1, h);
            if (!gateOnX && System.Math.Abs(gxc - x1) < 1f) { FenceZ(m, x1, z0, gz0 - PB, h); FenceZ(m, x1, gz1 + PB, z1, h); } else FenceZ(m, x1, z0, z1, h);
            // Gate posts with a lamp each.
            if (gateOnX)
            {
                B(RcMat.Wood, gx0 - 0.5f, gzc - 0.3f, gx0, gzc + 0.3f, 0f, 4.2f, true);
                B(RcMat.Wood, gx1, gzc - 0.3f, gx1 + 0.5f, gzc + 0.3f, 0f, 4.2f, true);
                B(RcMat.Glow, gx0 - 0.4f, gzc - 0.2f, gx0 - 0.1f, gzc + 0.2f, 4.2f, 4.5f, false);
                S.Light(gxc, 3.6f, gzc - 2f, 0, 10f, 1.3f);
            }
            else
            {
                B(RcMat.Wood, gxc - 0.3f, gz0 - 0.5f, gxc + 0.3f, gz0, 0f, 4.2f, true);
                B(RcMat.Wood, gxc - 0.3f, gz1, gxc + 0.3f, gz1 + 0.5f, 0f, 4.2f, true);
                B(RcMat.Glow, gxc - 0.2f, gz0 - 0.4f, gxc + 0.2f, gz0 - 0.1f, 4.2f, 4.5f, false);
                S.Light(gxc + (gxc < (x0 + x1) * 0.5f ? -2f : 2f), 3.6f, gzc, 0, 10f, 1.3f);
            }
        }

        private static void FenceX(RcMat m, float z, float x0, float x1, float h) { if (x1 - x0 > 0.05f) B(m, x0, z - 0.15f, x1, z + 0.15f, 0f, h, true); }
        private static void FenceZ(RcMat m, float x, float z0, float z1, float h) { if (z1 - z0 > 0.05f) B(m, x - 0.15f, z0, x + 0.15f, z1, 0f, h, true); }

        private static void WaterTower(float x, float z)
        {
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    B(RcMat.Metal, x + sx * 2.2f - 0.15f, z + sz * 2.2f - 0.15f, x + sx * 2.2f + 0.15f, z + sz * 2.2f + 0.15f, 0f, 16f, true);
            S.Cyl(RcMat.Rust, x, 16f, z, 4f, 6f, false);
            S.Cyl(RcMat.Roof, x, 22f, z, 4.4f, 0.8f, false);
            B(RcMat.RedGlow, x - 0.2f, z - 0.2f, x + 0.2f, z + 0.2f, 22.8f, 23.3f, false);
            if (_collect) Landmarks.Add(new RcSpot(0, x, 0f, z));
        }

        private static void MallSign(float x, float z)
        {
            B(RcMat.Metal, x - 0.4f, z - 0.4f, x + 0.4f, z + 0.4f, 0f, 15f, true);
            B(RcMat.Neon, x - 0.5f, z - 3.5f, x + 0.5f, z + 3.5f, 15f, 19f, false);
            B(RcMat.BlueGlow, x - 0.55f, z - 3f, x + 0.55f, z + 3f, 12.5f, 14.2f, false);
            S.Light(x + 2f, 13f, z, 1, 22f, 1.6f);
            if (_collect) Landmarks.Add(new RcSpot(1, x, 0f, z));
        }

        private static void ClockTower(float x, float z)
        {
            B(RcMat.Brick, x - 3f, z - 3f, x + 3f, z + 3f, 0f, 18f, true);
            B(RcMat.Roof, x - 3.5f, z - 3.5f, x + 3.5f, z + 3.5f, 18f, 19f, false);
            B(RcMat.Roof, x - 2.2f, z - 2.2f, x + 2.2f, z + 2.2f, 19f, 21.5f, false);
            B(RcMat.Glow, x + 3f, z - 1.6f, x + 3.06f, z + 1.6f, 13.5f, 16.7f, false);   // the clock's face, toward the park
            B(RcMat.Black, x + 3.06f, z - 0.08f, x + 3.1f, z + 0.08f, 15f, 16.4f, false);
            B(RcMat.Black, x + 3.06f, z - 0.08f, x + 3.1f, z + 0.9f, 15f, 15.16f, false);
            S.Light(x + 5f, 15f, z, 0, 16f, 1.2f);
            if (_collect) Landmarks.Add(new RcSpot(2, x, 0f, z));
        }

        private static void Crane(float x, float z)
        {
            B(RcMat.Rust, x - 1.2f, z - 1.2f, x + 1.2f, z + 1.2f, 0f, 24f, true);
            B(RcMat.Rust, x - 17f, z - 0.7f, x + 5f, z + 0.7f, 24f, 25.6f, false);   // the arm, out over the crusher
            B(RcMat.Metal, x - 15.1f, z - 0.1f, x - 14.9f, z + 0.1f, 12f, 24f, false);
            B(RcMat.Metal, x - 16f, z - 1f, x - 14f, z + 1f, 10.8f, 12f, false);
            B(RcMat.RedGlow, x - 0.25f, z - 0.25f, x + 0.25f, z + 0.25f, 25.6f, 26.2f, false);
            if (_collect) Landmarks.Add(new RcSpot(3, x, 0f, z));
        }

        private static void Smokestack(float x, float z)
        {
            S.Cyl(RcMat.Brick, x, 0f, z, 2.6f, 30f, true);
            S.Cyl(RcMat.Concrete, x, 30f, z, 2.9f, 1.2f, false);
            B(RcMat.RedGlow, x - 0.4f, z - 0.4f, x + 0.4f, z + 0.4f, 31.2f, 32f, false);
            S.Light(x, 33f, z, 3, 30f, 2f);
            if (_collect) Landmarks.Add(new RcSpot(4, x, 0f, z));
        }

        private static void Trees()
        {
            for (float gx = X0 + 8f; gx < X1 - 6f; gx += 13f)
                for (float gz = Z0 + 8f; gz < Z1 - 6f; gz += 13f)
                {
                    float x = gx + (Rnd() - 0.5f) * 9f, z = gz + (Rnd() - 0.5f) * 9f, s = 0.85f + Rnd() * 0.6f;
                    if (Rnd(5) == 0) continue;
                    if (!TreeAllowed(x, z)) continue;
                    Tree(x, z, s);
                }
        }

        private static bool TreeAllowed(float x, float z)
        {
            for (int i = 0; i < 5; i++) if (ZoneDistance(i, x, z) < 5f) return false;
            if (System.Math.Abs(x) < PARK + 5f && System.Math.Abs(z) < PARK + 5f) return false;
            if (System.Math.Abs(x) < 20f && z < -40f) return false;                       // Main Street south and its houses
            if (System.Math.Abs(x) < 20f && z > 40f && z < 115f) return false;            // Main Street north
            if (System.Math.Abs(z) < 19f && x < -40f && x > -120f) return false;          // the west road
            if (System.Math.Abs(z) < 19f && x > 40f && x < 132f) return false;            // the east road
            if (z > 70f && z < 99f && x > 0f && x < 132f) return false;                   // the junkyard road
            if (x > 4f && x < 54f && z > -196f && z < -163f) return false;                // the Arcade
            if (x > 45f && x < 205f && z > -207f && z < -189f) return false;              // the road out
            if (System.Math.Abs(x - 108f) < 5f && z > 25f) return false;                  // the tracks
            if (System.Math.Abs(z - 11f) < 5f && x > 122f) return false;
            if (x < -200f && z < -195f) return false;                                     // the water tower
            if (System.Math.Abs(x + 88f) < 6f && System.Math.Abs(z - 117f) < 6f) return false; // the smokestack
            if (System.Math.Abs(x - 118f) < 7f && System.Math.Abs(z - 92f) < 7f) return false; // the junkyard's fort
            return true;
        }

        // ── Zone 0: the suburbs ──────────────────────────────────────────────
        private static void Suburbs()
        {
            // Streets: Maple (east-west), Oak Lane below it, a lane joining them, the cul-de-sac at Oak's west end.
            Street(-200f, -134f, -17f, -126f, true);
            Street(-168f, -184f, -40f, -176f, true);
            Street(-124f, -176f, -116f, -134f, false);
            B(RcMat.Road, -205f, -198f, -168f, -162f, 0f, 0.05f, false);
            for (int i = 0; i < 8; i++) { float x = -30f - i * 22f; Lamp(x, i % 2 == 0 ? -124.9f : -135.1f, 0f, i % 2 == 0 ? -1f : 1f, i != 5); }
            for (int i = 0; i < 5; i++) { float x = -52f - i * 24f; Lamp(x, i % 2 == 0 ? -185.1f : -174.9f, 0f, i % 2 == 0 ? 1f : -1f, i != 2); }

            RcMat[] paints = { RcMat.House, RcMat.HouseB, RcMat.HouseC };
            // Row A: north of Maple. Front yards on the street, fenced backyards behind (dark, with scrap).
            float[] ax = { -40f, -62f, -84f, -106f, -128f, -150f, -172f, -194f };
            for (int i = 0; i < ax.Length; i++)
            {
                float x = ax[i];
                if (i == 2) HouseA(x); else Front(x - 7f, -118f, x + 7f, -106f, 5.2f + Rnd(3) * 0.6f, 0, paints[Rnd(3)]);
                BoardFence(x - 10.5f, -106f, x - 10.5f, -90f, 1.9f, RcMat.Plank);   // between the backyards
                if (i % 2 == 0) Pile(0, x + 3f, -96f);
                if (i % 3 == 1) Tree(x - 4f, -97f, 0.9f);
            }
            BoardFence(-205f, -90f, -29.5f, -90f, 1.9f, RcMat.Plank);               // the back of the backyards
            BoardFence(-29.5f, -106f, -29.5f, -90f, 1.9f, RcMat.Plank);

            // Row B: south of Maple. One lot is the playground (the fort).
            float[] bx = { -40f, -62f, -84f, -140f, -162f, -186f };
            for (int i = 0; i < bx.Length; i++)
            {
                float x = bx[i];
                if (i == 4) HouseB(x); else Front(x - 7f, -154f, x + 7f, -142f, 5.2f + Rnd(3) * 0.6f, 1, paints[Rnd(3)]);
                if (i % 2 == 1) Pile(0, x - 2f, -162f);
            }
            Playground(-102f, -156f);

            // Row C: south of Oak Lane. The east end is the garage row.
            float[] cx = { -78f, -100f, -122f, -144f };
            for (int i = 0; i < cx.Length; i++)
            {
                float x = cx[i];
                if (i == 2) HouseC(x); else Front(x - 7f, -204f, x + 7f, -192f, 5.2f + Rnd(3) * 0.6f, 1, paints[Rnd(3)]);
            }
            for (int i = 0; i < 4; i++)
            {
                float x = -30f - i * 9f;
                Shell(x - 4f, -200f, x + 4f, -188f, 3.2f, RcMat.Concrete, RcMat.Concrete, 1f, x, 4.4f);
                if (i % 2 == 0) B(RcMat.Crate, x - 3.2f, -199f, x - 1.8f, -197.4f, 0f, 1.1f, true);
                if (i == 1 || i == 3) Spawn(0, RcData.E_JACK, 1, x, -196.5f);
                if (i == 0) Pile(0, x + 2f, -198f);
                if (i == 2) Car(x, -195f, false, RcMat.CarB);
            }

            // Left where they were dropped.
            Car(-58f, -128.3f, true, RcMat.CarA);
            Car(-150f, -131.7f, true, RcMat.CarC);
            Car(-92f, -181.8f, true, RcMat.CarB);
            Tree(-30f, -150f, 1.1f); Tree(-52f, -167f, 1f); Tree(-128f, -163f, 1.1f); Tree(-174f, -150f, 1f); Tree(-66f, -200f, 1f);

            // Strays on the lawns, tougher ones in the backyards and in the houses.
            Spawn(0, RcData.E_PURPLE, 3, -51f, -122f);
            Spawn(0, RcData.E_PURPLE, 3, -95f, -122f);
            Spawn(0, RcData.E_PURPLE, 4, -139f, -122f);
            Spawn(0, RcData.E_PURPLE, 3, -183f, -122f);
            Spawn(0, RcData.E_PURPLE, 3, -73f, -166f);
            Spawn(0, RcData.E_PURPLE, 4, -133f, -188f);
            Spawn(0, RcData.E_YELLOW, 2, -62f, -97f);
            Spawn(0, RcData.E_YELLOW, 2, -128f, -97f);
            Spawn(0, RcData.E_YELLOW, 3, -172f, -97f);
            Spawn(0, RcData.E_YELLOW, 2, -150f, -165f);
            Spawn(0, RcData.E_BOSS0 + 0, 1, Arena[0, 0], Arena[0, 1]);
        }

        // Three houses you can go into: dark, a few rooms, scrap and strays inside.
        /// <summary>What makes a house a home: a rug, a floor lamp in a corner, and pictures on the north wall (at the given x).</summary>
        private static void HomeTouches(float x0, float z0, float x1, float z1, float pic0, float pic1, RcMat rug)
        {
            float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
            Vis(rug, cx - 2f, 0.05f, cz - 1.4f, cx + 2f, 0.075f, cz + 1.4f);
            Vis(RcMat.Tape, cx - 2f, 0.05f, cz - 1.4f, cx - 1.9f, 0.08f, cz + 1.4f);
            Vis(RcMat.Tape, cx + 1.9f, 0.05f, cz - 1.4f, cx + 2f, 0.08f, cz + 1.4f);
            float lx = x1 - 0.7f, lz = z0 + 0.7f;
            Vis(RcMat.Metal, lx - 0.2f, 0.05f, lz - 0.2f, lx + 0.2f, 0.09f, lz + 0.2f);
            S.Cyl(RcMat.Metal, lx, 0.09f, lz, 0.025f, 1.5f, false);
            S.Cyl(RcMat.Glow, lx, 1.5f, lz, 0.22f, 0.3f, false);
            S.Light(lx, 1.5f, lz, 0, 5f, 0.5f);
            float wz = z1 - T * 0.5f;
            foreach (float px in new[] { pic0, pic1 })
            {
                Vis(RcMat.Wood, px - 0.5f, 1.35f, wz - 0.05f, px + 0.5f, 2.1f, wz);
                Vis(px == pic0 ? RcMat.Fabric : RcMat.Lawn, px - 0.42f, 1.42f, wz - 0.07f, px + 0.42f, 2.03f, wz - 0.05f);
            }
        }

        private static void HouseA(float x)
        {
            float x0 = x - 8f, x1 = x + 8f, z0 = -119f, z1 = -105f;
            Shell(x0, z0, x1, z1, 3.4f, RcMat.House, RcMat.Carpet, 0f, x - 3f, 3f, 1f, x + 4f, 3f);
            WallX(RcMat.HouseB, -112f, x0, x1, 3.4f, x + 3f, 3f);
            WallZ(RcMat.HouseB, x - 1f, -112f, z1, 3.4f, -108.5f, 3f);
            B(RcMat.Fabric, x0 + 0.6f, -118.4f, x0 + 3f, -117.2f, 0f, 0.8f, true);     // a couch
            B(RcMat.Wood, x + 4f, -111.4f, x + 7f, -110.2f, 0f, 0.75f, true);         // a table
            Pile(0, x - 5f, -108f);
            Pile(0, x + 6f, -116f);
            S.Light(x - 3f, 2.6f, -116f, 4, 6f, 0.7f);
            HomeTouches(x0, z0, x1, z1, x - 5.8f, x - 2.4f, RcMat.Fabric);
            Spawn(0, RcData.E_YELLOW, 2, x + 2f, -115.5f);
        }

        private static void HouseB(float x)
        {
            float x0 = x - 8f, x1 = x + 8f, z0 = -155f, z1 = -141f;
            Shell(x0, z0, x1, z1, 3.4f, RcMat.HouseC, RcMat.Carpet, 1f, x + 2f, 3f, 0f, x - 4f, 3f);
            WallZ(RcMat.HouseB, x - 1f, z0, z1, 3.4f, -147.5f, 3f);
            B(RcMat.Wood, x - 7f, -154.4f, x - 3.5f, -153f, 0f, 0.9f, true);
            B(RcMat.Fabric, x + 3f, -153.8f, x + 6.5f, -151.8f, 0f, 0.55f, true);      // a bed
            Pile(0, x - 5.5f, -144f);
            Pile(0, x + 5f, -145f);
            HomeTouches(x0, z0, x1, z1, x - 5.8f, x - 3f, RcMat.Carpet);
            Spawn(0, RcData.E_PURPLE, 3, x + 3.5f, -148f);
        }

        private static void HouseC(float x)
        {
            float x0 = x - 8f, x1 = x + 8f, z0 = -205f, z1 = -191f;
            Shell(x0, z0, x1, z1, 3.4f, RcMat.HouseB, RcMat.Carpet, 1f, x - 2f, 3f);
            WallX(RcMat.House, -198f, x0, x1, 3.4f, x - 5f, 3f, x + 4f, 3f);
            B(RcMat.Wood, x - 7.2f, -197.4f, x - 5.8f, -193f, 0f, 1.9f, true);        // a bookcase
            Pile(0, x + 5f, -203f);
            Pile(0, x - 4f, -202.5f);
            S.Light(x + 3f, 2.6f, -195f, 4, 6f, 0.7f);
            HomeTouches(x0, z0, x1, z1, x - 6f, x + 5.6f, RcMat.Carpet);
            Spawn(0, RcData.E_YELLOW, 3, x, -201.5f);
        }

        private static void Playground(float x, float z)
        {
            B(RcMat.Dirt, x - 11f, z - 14f, x + 11f, z + 14f, 0f, 0.04f, false);
            // The climbing frame: the kids' fort.
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    B(RcMat.Wood, x + sx * 1.6f - 0.1f, z + sz * 1.6f - 0.1f, x + sx * 1.6f + 0.1f, z + sz * 1.6f + 0.1f, 0f, 3.2f, true);
            B(RcMat.Plank, x - 1.9f, z - 1.9f, x + 1.9f, z + 1.9f, 1.9f, 2.05f, false);
            B(RcMat.Roof, x - 2.1f, z - 2.1f, x + 2.1f, z + 2.1f, 3.2f, 3.4f, false);
            // Swings, a slide, a sandbox.
            B(RcMat.Metal, x + 5f, z - 6.1f, x + 5.1f, z - 2f, 2.4f, 2.5f, false);
            B(RcMat.Metal, x + 5f, z - 6.1f, x + 5.1f, z - 6f, 0f, 2.5f, false);
            B(RcMat.Metal, x + 5f, z - 2.1f, x + 5.1f, z - 2f, 0f, 2.5f, false);
            B(RcMat.Plank, x + 4.8f, z - 5f, x + 5.3f, z - 4.5f, 0.55f, 0.6f, false);
            B(RcMat.Plank, x + 4.8f, z - 3.6f, x + 5.3f, z - 3.1f, 0.55f, 0.6f, false);
            B(RcMat.Tape, x - 8f, z + 4f, x - 4f, z + 8f, 0f, 0.25f, false);
            B(RcMat.Path, x - 7.8f, z + 4.2f, x - 4.2f, z + 7.8f, 0.2f, 0.27f, false);
            Pile(0, x + 7f, z + 9f);
        }

        // ── Zone 1: the mall ─────────────────────────────────────────────────
        private static int SpotDice(float x, float z, int salt)
        {
            unchecked
            {
                int h = ((int)(x * 10f) * 73856093) ^ ((int)(z * 10f) * 19349663) ^ (salt * 83492791);
                h ^= h >> 13; h *= 1274126177; h ^= h >> 16;
                return h & 0x7fffffff;
            }
        }

        /// <summary>A parked car in the mall's lot, one chance in 'odds' (never on top of a scrap pile).</summary>
        private static void MoreCar(float x, float z, int salt, int odds, RcMat[] paints)
        {
            if (SpotDice(x, z, salt) % odds != 0) return;
            float[,] piles = { { 148f, -36f }, { 181f, -128f }, { 132f, -90f } };
            for (int i = 0; i < piles.GetLength(0); i++)
            {
                float dx = x - piles[i, 0], dz = z - piles[i, 1];
                if (dx * dx + dz * dz < 36f) return;
            }
            Car(x, z, true, paints[SpotDice(x, z, salt + 10) % paints.Length]);
        }

        private static void Mall()
        {
            const float F = FLOOR, top = 9.4f, g = FLOOR - 0.2f; // g: a ground-floor wall's height (to the underside of the floor above)

            // The parking lot.
            B(RcMat.Road, 128.3f, -141.7f, 186f, 7.7f, 0f, 0.05f, false);
            RcMat[] cars = { RcMat.CarA, RcMat.CarB, RcMat.CarC };
            for (float z = -132f; z < -18f; z += 5.5f)
            {
                B(RcMat.Line, 134f, z - 2.78f, 140f, z - 2.7f, 0.05f, 0.08f, false);
                B(RcMat.Line, 172f, z - 2.78f, 178f, z - 2.7f, 0.05f, 0.08f, false);
                if (Rnd(3) != 0) Car(137f, z, true, cars[Rnd(3)]);
                if (Rnd(3) == 0) Car(175f, z, true, cars[Rnd(3)]);
                if (Rnd(4) == 0) Car(155f, z, true, cars[Rnd(3)]);
                // More of them: the lot was too empty. (Their own dice, from where they stand, so the town's other random choices don't shift.)
                MoreCar(146f, z, 1, 2, cars);
                MoreCar(165f, z, 2, 2, cars);
                MoreCar(183f, z, 3, 3, cars);
            }
            for (int i = 0; i < 4; i++) Lamp(156f, -125f + i * 36f, i % 2 == 0 ? 1f : -1f, 0f, i != 2);
            // The security booth: the fort.
            Shell(137f, -19f, 143f, -13f, 2.8f, RcMat.Concrete, RcMat.Tile, 3f, -16f, 2.4f);
            B(RcMat.Glass, 136.9f, -18f, 137f, -14f, 1.1f, 2.1f, false);
            Pile(1, 148f, -36f);
            Pile(1, 181f, -128f);
            Pile(1, 132f, -90f);

            // The mall: two floors of dead shops round an atrium that's open to the roof. You come in on the west side.
            Shell(188f, -138f, 262f, 2f, top, RcMat.Concrete, RcMat.Tile, 2f, -68f, 8f);
            B(RcMat.Neon, 187.7f, -74f, 187.8f, -62f, 4.2f, 5.4f, false); // (on the wall's outside face, not in the same plane as it)
            S.Light(184f, 4f, -68f, 1, 12f, 1.3f);

            // ── Ground floor ──
            WallZY(RcMat.Concrete, 219f, -132f, -72f, 0f, g, -117f, 4f, -87f, 4f);           // west shops' fronts (south of the hall)
            WallZY(RcMat.Concrete, 219f, -64f, -4f, 0f, g, -49f, 4f, -19f, 10f);            // (north of it; the food court is wide open)
            WallZY(RcMat.Concrete, 231f, -94f, -4f, 0f, g, -79f, 4f, -49f, 4f, -19f, 4f);   // east shops' fronts
            WallZ(RcMat.Concrete, 231f, -132f, -94f, top, -113f, 8f);                        // the toy store's front: it's two floors high inside
            WallXY(RcMat.Concrete, -72f, 188f, 219f, 0f, g, NoGaps);                         // the entrance hall's walls
            WallXY(RcMat.Concrete, -64f, 188f, 219f, 0f, g, NoGaps);
            WallXY(RcMat.Concrete, -102f, 188f, 219f, 0f, g, NoGaps);                        // between the west shops
            WallXY(RcMat.Concrete, -34f, 188f, 219f, 0f, g, NoGaps);
            WallX(RcMat.Concrete, -94f, 231f, 262f, top, NoGaps);                            // the toy store's north wall
            WallXY(RcMat.Concrete, -64f, 231f, 254f, 0f, g, NoGaps);                         // between the east shops
            WallXY(RcMat.Concrete, -34f, 231f, 254f, 0f, g, NoGaps);
            WallZY(RcMat.Concrete, 254f, -94f, -4f, 0f, g, -79f, 3f, -49f, 3f, -19f, 3f);    // the service corridor behind them (pitch dark)
            WallXY(RcMat.Concrete, -4f, 188f, 262f, 0f, g, 225f, 8f, 258f, 3f);              // the north end: a hall across the top
            WallX(RcMat.Concrete, -132f, 188f, 262f, top, 225f, 6f);                         // the south end: the loading bay behind

            // The stopped escalators: the way upstairs. One climbs north, one south; each lands on a bridge over the atrium.
            Stairs(RcMat.Metal, 223.5f, -54f, 226.5f, -46f, 0f, F, false, true);
            Stairs(RcMat.Metal, 223.5f, -104f, 226.5f, -96f, 0f, F, false, false);
            S.Cyl(RcMat.Stone, 225f, 0f, -30f, 2f, 0.7f, true);
            S.Cyl(RcMat.Stone, 225f, 0f, -120f, 2f, 0.7f, true);
            S.Light(225f, 8.6f, -68f, 2, 26f, 0.9f);
            S.Light(225f, 8.6f, -20f, 2, 20f, 0.6f);
            S.Light(225f, 8.6f, -118f, 2, 20f, 0.6f);
            // Ground-floor shops: shelves and counters.
            for (int i = 0; i < 2; i++)
            {
                float z = i == 0 ? -125f : -95f;                                           // west, south of the hall
                for (int k = 0; k < 3; k++) B(RcMat.Shelf, 193f + k * 7f, z, 194.2f + k * 7f, z + 14f, 0f, 2f, true);
                Pile(1, 214f, z + 2f);
            }
            for (int k = 0; k < 3; k++) B(RcMat.Shelf, 193f + k * 7f, -60f, 194.2f + k * 7f, -40f, 0f, 2f, true); // west, north of the hall
            Pile(1, 213f, -38f);
            for (int k = 0; k < 4; k++)                                                    // the food court: tables
                for (int j = 0; j < 3; j++) B(RcMat.Wood, 193f + k * 6f, -30f + j * 8f, 195f + k * 6f, -28f + j * 8f, 0f, 0.75f, true);
            B(RcMat.Metal, 189f, -8f, 216f, -6f, 0f, 1.1f, true);                          // its counter
            for (int i = 0; i < 3; i++)                                                    // east shops
            {
                float z = -90f + i * 30f;
                B(RcMat.Shelf, 236f, z, 237.2f, z + 20f, 0f, 2f, true);
                B(RcMat.Shelf, 244f, z + 4f, 245.2f, z + 22f, 0f, 2f, true);
                B(RcMat.Wood, 249f, z + 1f, 252f, z + 2.6f, 0f, 1f, true);
                if (i != 1) Pile(1, 250f, z + 18f);
            }
            Pile(1, 258f, -40f);                                                           // in the service corridor
            Pile(1, 258f, -86f);
            // The toy store: the boss's.
            B(RcMat.Carpet, 231.2f, -131.8f, 261.8f, -94.2f, 0.05f, 0.08f, false);
            B(RcMat.Glass, 231.2f, -117f, 231.3f, -109f, 2.7f, 5f, false);
            for (int i = 0; i < 4; i++) B(RcMat.Shelf, 234f + i * 7f, -131.4f, 238f + i * 7f, -130.2f, 0f, 2.4f, true);
            S.Light(246f, 7f, -113f, 1, 22f, 1f);
            // The loading bay.
            B(RcMat.Crate, 196f, -137.6f, 199f, -135.6f, 0f, 1.6f, true);
            B(RcMat.Crate, 240f, -137.6f, 244f, -135.4f, 0f, 1.6f, true);
            Pile(1, 210f, -135f);

            // ── Upstairs ──
            // The floor: over the west shops, over the east shops, across the north end, and a walkway down each
            // side of the atrium with two bridges between them.
            S.Walk(RcMat.Tile, 188f, g, -132f, 219f, F, 2f);
            S.Walk(RcMat.Tile, 231f, g, -94f, 262f, F, 2f);
            S.Walk(RcMat.Tile, 219f, g, -4f, 231f, F, 2f);
            S.Walk(RcMat.Tile, 219f, g, -132f, 223f, F, -4f);
            S.Walk(RcMat.Tile, 227f, g, -132f, 231f, F, -4f);
            S.Walk(RcMat.Tile, 223f, g, -46f, 227f, F, -40f);
            S.Walk(RcMat.Tile, 223f, g, -110f, 227f, F, -104f);
            // Railings along the drop, open where the escalators arrive.
            float[] gapsAt = { -132f, -110f, -104f, -46f, -40f, -4f };
            for (int i = 0; i + 1 < gapsAt.Length; i += 2) { Rail(223f, gapsAt[i], 223f, gapsAt[i + 1], F); Rail(227f, gapsAt[i], 227f, gapsAt[i + 1], F); }
            Rail(223f, -40f, 227f, -40f, F);
            Rail(223f, -110f, 227f, -110f, F);
            Rail(223f, -4f, 227f, -4f, F);
            // The upstairs shops.
            WallZY(RcMat.Concrete, 219f, -132f, -4f, F, top, -117f, 4f, -87f, 4f, -68f, 3f, -49f, 4f, -19f, 4f);
            WallZY(RcMat.Concrete, 231f, -94f, -4f, F, top, -79f, 4f, -49f, 4f, -19f, 4f);
            WallXY(RcMat.Concrete, -102f, 188f, 219f, F, top, NoGaps);
            WallXY(RcMat.Concrete, -72f, 188f, 219f, F, top, NoGaps);
            WallXY(RcMat.Concrete, -64f, 188f, 219f, F, top, NoGaps);
            WallXY(RcMat.Concrete, -34f, 188f, 219f, F, top, NoGaps);
            WallXY(RcMat.Concrete, -64f, 231f, 262f, F, top, NoGaps);
            WallXY(RcMat.Concrete, -34f, 231f, 262f, F, top, NoGaps);
            WallXY(RcMat.Concrete, -4f, 188f, 219f, F, top, NoGaps);
            WallXY(RcMat.Concrete, -4f, 231f, 262f, F, top, NoGaps);
            for (int i = 0; i < 2; i++)
            {
                float z = i == 0 ? -126f : -96f;
                for (int k = 0; k < 2; k++) B(RcMat.Shelf, 195f + k * 9f, z, 196.2f + k * 9f, z + 16f, F, F + 2f, true);
            }
            for (int k = 0; k < 2; k++) B(RcMat.Shelf, 195f + k * 9f, -58f, 196.2f + k * 9f, -42f, F, F + 2f, true);
            for (int k = 0; k < 3; k++) B(RcMat.Wood, 194f + k * 7f, -24f, 197f + k * 7f, -22.4f, F, F + 0.9f, true);
            for (int i = 0; i < 3; i++)
            {
                float z = -90f + i * 30f;
                B(RcMat.Shelf, 238f, z, 239.2f, z + 20f, F, F + 2f, true);
                B(RcMat.Shelf, 250f, z + 3f, 251.2f, z + 22f, F, F + 2f, true);
            }
            Pile(1, 214f, F, -105f);
            Pile(1, 192f, F, -40f);
            Pile(1, 258f, F, -60f);
            Pile(1, 240f, F, -8f);
            Pile(1, 192f, F, 0f);

            Spawn(1, RcData.E_YELLOW, 3, 150f, -70f);
            Spawn(1, RcData.E_YELLOW, 3, 165f, -120f);
            Spawn(1, RcData.E_RCCAR, 1, 160f, -40f);
            Spawn(1, RcData.E_RCCAR, 1, 162f, -100f);
            Spawn(1, RcData.E_RCCAR, 1, 175f, -70f);       // (seven patrol the lot, and come back quickly)
            Spawn(1, RcData.E_RCCAR, 1, 152f, -75f);
            Spawn(1, RcData.E_RCCAR, 1, 178f, -20f);
            Spawn(1, RcData.E_RCCAR, 1, 170f, -122f);
            Spawn(1, RcData.E_RCCAR, 1, 158f, -55f);
            Spawn(1, RcData.E_TEAL, 3, 204f, -118f);
            Spawn(1, RcData.E_TEAL, 3, 204f, -88f);
            Spawn(1, RcData.E_TEAL, 3, 204f, -48f);
            Spawn(1, RcData.E_TEAL, 4, 204f, -18f);
            Spawn(1, RcData.E_TEAL, 3, 242f, -78f);
            Spawn(1, RcData.E_TEAL, 3, 242f, -48f);
            Spawn(1, RcData.E_TEAL, 3, 242f, -18f);
            Spawn(1, RcData.E_TEAL, 2, 258f, -60f);
            Spawn(1, RcData.E_TEAL, 3, 208f, F, -117f);
            Spawn(1, RcData.E_TEAL, 3, 208f, F, -50f);
            Spawn(1, RcData.E_TEAL, 3, 245f, F, -79f);
            Spawn(1, RcData.E_TEAL, 3, 245f, F, -19f);
            Spawn(1, RcData.E_TEAL, 2, 205f, F, -1f);
            Spawn(1, RcData.E_BOSS0 + 1, 1, Arena[1, 0], Arena[1, 1]);
        }

        // ── Zone 2: the school ───────────────────────────────────────────────
        private static void School()
        {
            const float F = FLOOR, top = 9.4f, g = FLOOR - 0.2f;

            // The yard.
            B(RcMat.Road, -155.8f, -60.7f, -115.3f, 75.7f, 0f, 0.05f, false);
            B(RcMat.CarA, -146f, -46f, -143f, -35f, 0.4f, 3.1f, true);                    // two school buses
            B(RcMat.CarA, -139f, -48f, -136f, -37f, 0.4f, 3.1f, true);
            B(RcMat.Black, -146f, -46f, -143f, -35f, 0f, 0.4f, false);
            B(RcMat.Black, -139f, -48f, -136f, -37f, 0f, 0.4f, false);
            B(RcMat.Metal, -128.1f, -20.1f, -127.9f, -19.9f, 0f, 9f, false);              // the flagpole
            Lamp(-122f, -20f, -1f, 0f, true);
            Lamp(-122f, 40f, -1f, 0f, false);
            // The bus shelter: the fort.
            B(RcMat.Metal, -137.1f, 27.9f, -136.9f, 28.1f, 0f, 2.6f, false);
            B(RcMat.Metal, -133.1f, 27.9f, -132.9f, 28.1f, 0f, 2.6f, false);
            B(RcMat.Glass, -137.2f, 32f, -132.8f, 32.1f, 0f, 2.4f, true);
            B(RcMat.Roof, -137.4f, 27.6f, -132.6f, 32.4f, 2.6f, 2.75f, false);
            B(RcMat.Wood, -136.6f, 31f, -133.4f, 31.7f, 0.4f, 0.5f, false);
            Pile(2, -126f, 60f);
            Pile(2, -148f, -56f);

            Shell(-260f, -56f, -156f, 72f, top, RcMat.Brick, RcMat.Tile, 3f, 0f, 8f);

            // ── Ground floor: a hall from the front door to the back, and one crossing it ──
            WallXY(RcMat.Brick, 4f, -260f, -156f, 0f, g, -244f, 3f, -222f, 3f, -208f, 8f, -181f, 4f);   // north side of the main hall
            WallX(RcMat.Brick, -4f, -260f, -212f, top, -236f, 8f);                                     // south side: the gym (two floors high inside)
            WallXY(RcMat.Brick, -4f, -212f, -156f, 0f, g, -208f, 8f, -196f, 3f, -180f, 3f, -164f, 3f);
            WallZY(RcMat.Brick, -204f, 4f, 72f, 0f, g, 54f, 4f);                            // east side of the cross hall (north)
            WallZY(RcMat.Brick, -212f, 4f, 72f, 0f, g, 50f, 3f);                            // west side (north)
            WallZY(RcMat.Brick, -204f, -56f, -4f, 0f, g, -44f, 3f);                         // east side (south)
            WallZ(RcMat.Brick, -212f, -56f, -4f, top, NoGaps);                              // west side (south): the gym's wall
            // North-east: the cafeteria, then the library.
            WallXY(RcMat.Brick, 38f, -204f, -156f, 0f, g, NoGaps);
            for (int k = 0; k < 4; k++) for (int j = 0; j < 2; j++) B(RcMat.Wood, -198f + k * 10f, 12f + j * 12f, -193f + k * 10f, 14f + j * 12f, 0f, 0.75f, true);
            B(RcMat.Metal, -200f, 34f, -162f, 35.6f, 0f, 1.1f, true);
            for (int k = 0; k < 5; k++) B(RcMat.Shelf, -198f + k * 8f, 44f, -196.8f + k * 8f, 66f, 0f, 2.3f, true);
            Pile(2, -160f, 30f);
            Pile(2, -160f, 68f);
            // South-east: three classrooms on the hall, two rooms behind them.
            WallXY(RcMat.Brick, -30f, -204f, -156f, 0f, g, -168f, 3f);
            WallZY(RcMat.Brick, -172f, -30f, -4f, 0f, g, NoGaps);
            WallZY(RcMat.Brick, -188f, -30f, -4f, 0f, g, NoGaps);
            WallZY(RcMat.Brick, -180f, -56f, -30f, 0f, g, -48f, 3f);
            for (int r = 0; r < 3; r++)
                for (int k = 0; k < 2; k++) for (int j = 0; j < 3; j++)
                    B(RcMat.Wood, -169f - r * 16f + k * 5f, -26f + j * 6f, -167.6f - r * 16f + k * 5f, -24.8f + j * 6f, 0f, 0.7f, true);
            Pile(2, -200f, -52f);
            Pile(2, -160f, -52f);
            // North-west: two classrooms, then the caretaker's room, where the stairs go down to the boiler.
            WallXY(RcMat.Brick, 30f, -260f, -212f, 0f, g, NoGaps);
            WallZY(RcMat.Brick, -236f, 4f, 30f, 0f, g, NoGaps);
            for (int r = 0; r < 2; r++)
                for (int k = 0; k < 2; k++) for (int j = 0; j < 3; j++)
                    B(RcMat.Wood, -232f - r * 24f + k * 6f, 9f + j * 6f, -230.6f - r * 24f + k * 6f, 10.2f + j * 6f, 0f, 0.7f, true);
            Rail(-254.5f, 34f, -254.5f, 42f, 0f);
            Rail(-258.5f, 42f, -254.5f, 42f, 0f);
            Rail(-258.5f, 34f, -258.5f, 42f, 0f);
            B(RcMat.Shelf, -230f, 70f, -216f, 71.2f, 0f, 2.2f, true);
            B(RcMat.Crate, -246f, 60f, -243f, 63f, 0f, 1.4f, true);
            Pile(2, -220f, 62f);
            Pile(2, -248f, 50f);
            // Lockers down the halls.
            for (float x = -250f; x < -160f; x += 6f) if (x < -214f || x > -202f) B(RcMat.Metal, x, 3.4f, x + 4f, 3.78f, 0f, 1.9f, false);
            for (float z = 8f; z < 46f; z += 6f) B(RcMat.Metal, -204.6f, z, -204.22f, z + 4f, 0f, 1.9f, false);
            // The halls have a little emergency light. The rooms have none.
            S.Light(-180f, 3.8f, 0f, 3, 14f, 0.8f);
            S.Light(-232f, 3.8f, 0f, 3, 14f, 0.8f);
            S.Light(-208f, 3.8f, 40f, 3, 12f, 0.6f);
            S.Light(-208f, 3.8f, -34f, 3, 12f, 0.6f);
            // The gym: the boss's. (South-west: the whole corner, open to the roof.)
            B(RcMat.Wood, -259.6f, -55.6f, -212.2f, -4.2f, 0.05f, 0.08f, false);
            B(RcMat.Line, -236.1f, -55f, -235.9f, -5f, 0.09f, 0.11f, false);
            B(RcMat.Plank, -259f, -50f, -257f, -10f, 0f, 1.6f, true);                       // the bleachers
            S.Light(-236f, 8f, -30f, 3, 26f, 0.7f);

            // ── Upstairs: more classrooms, off the same two halls. The stairs are at the north end of the cross hall. ──
            Stairs(RcMat.Concrete, -211.6f, 60f, -208.6f, 68f, 0f, F, false, true);
            S.Walk(RcMat.Tile, -260f, g, -4f, -212f, F, 72f);                               // over the north-west rooms and the west end of the hall
            S.Walk(RcMat.Tile, -204f, g, -56f, -156f, F, 72f);                              // over the whole east side
            S.Walk(RcMat.Tile, -212f, g, -56f, -204f, F, 60f);                              // the cross hall...
            S.Walk(RcMat.Tile, -212f, g, 68f, -204f, F, 72f);                               // ...its landing at the top of the stairs...
            S.Walk(RcMat.Tile, -208.6f, g, 60f, -204f, F, 68f);                             // ...and the strip beside them
            Rail(-208.6f, 60f, -208.6f, 68f, F);
            Rail(-211.8f, 60f, -208.6f, 60f, F);
            WallXY(RcMat.Brick, 4f, -260f, -156f, F, top, -244f, 3f, -222f, 3f, -208f, 8f, -181f, 3f);
            WallXY(RcMat.Brick, -4f, -212f, -156f, F, top, -208f, 8f, -188f, 3f, -168f, 3f);
            WallZY(RcMat.Brick, -204f, 4f, 72f, F, top, 22f, 3f, 54f, 3f);
            WallZY(RcMat.Brick, -212f, 4f, 72f, F, top, 40f, 3f);
            WallZY(RcMat.Brick, -204f, -56f, -4f, F, top, -30f, 3f);
            WallXY(RcMat.Brick, 38f, -204f, -156f, F, top, NoGaps);
            WallZY(RcMat.Brick, -180f, -56f, -4f, F, top, -40f, 3f);
            WallXY(RcMat.Brick, 30f, -260f, -212f, F, top, -236f, 3f);
            for (int r = 0; r < 2; r++)
                for (int k = 0; k < 3; k++) for (int j = 0; j < 3; j++)
                    B(RcMat.Wood, -198f + k * 12f, 10f + r * 34f + j * 7f, -196.6f + k * 12f, 11.2f + r * 34f + j * 7f, F, F + 0.7f, true);
            for (int k = 0; k < 3; k++) for (int j = 0; j < 3; j++) B(RcMat.Wood, -254f + k * 12f, 9f + j * 6f, -252.6f + k * 12f, 10.2f + j * 6f, F, F + 0.7f, true);
            B(RcMat.Shelf, -256f, 66f, -218f, 67.2f, F, F + 2.2f, true);
            B(RcMat.Wood, -200f, -50f, -184f, -48.4f, F, F + 0.8f, true);
            Pile(2, -160f, F, 8f);
            Pile(2, -256f, F, 44f);
            Pile(2, -160f, F, -52f);
            S.Light(-180f, F + 3.6f, 0f, 3, 14f, 0.6f);
            S.Light(-208f, F + 3.6f, 30f, 3, 12f, 0.5f);

            // ── The boiler room: down the stairs, under the north-west corner. No light at all. ──
            Stairs(RcMat.Concrete, -258.5f, 34f, -254.5f, 42f, -F, 0f, false, false);
            S.Walk(RcMat.Concrete, -260f, -F - 0.2f, 30f, -212f, -F, 72f);
            WallXY(RcMat.Concrete, 30f, -260f, -212f, -F, -1f, NoGaps);
            WallXY(RcMat.Concrete, 72f, -260f, -212f, -F, -1f, NoGaps);
            WallZY(RcMat.Concrete, -260f, 30f, 72f, -F, -1f, NoGaps);
            WallZY(RcMat.Concrete, -212f, 30f, 72f, -F, -1f, NoGaps);
            B(RcMat.Concrete, -260f, 30f, -258.5f, 72f, -1.06f, -1f, false);               // its ceiling (the ground above), round the stairwell
            B(RcMat.Concrete, -254.5f, 30f, -212f, 72f, -1.06f, -1f, false);
            B(RcMat.Concrete, -258.5f, 30f, -254.5f, 34f, -1.06f, -1f, false);
            B(RcMat.Concrete, -258.5f, 42f, -254.5f, 72f, -1.06f, -1f, false);
            S.Cyl(RcMat.Rust, -240f, -F, 58f, 3f, 3.2f, true);
            S.Cyl(RcMat.Rust, -226f, -F, 62f, 2f, 3f, true);
            B(RcMat.Rust, -222f, 36f, -216f, 40f, -F, -F + 2.4f, true);
            B(RcMat.Metal, -252f, 69.4f, -216f, 70f, -2.2f, -1.8f, false);                  // pipes
            B(RcMat.Metal, -252f, 66f, -251.4f, 70f, -F, -2f, false);
            Pile(2, -220f, -F, 66f);
            Pile(2, -250f, -F, 66f);
            Pile(2, -232f, -F, 36f);
            S.Light(-236f, -2f, 50f, 3, 9f, 0.35f);

            Spawn(2, RcData.E_BLACK, 3, -184f, 20f);
            Spawn(2, RcData.E_BLACK, 3, -184f, 56f);
            Spawn(2, RcData.E_WHITE, 2, -164f, -16f);
            Spawn(2, RcData.E_BLACK, 2, -180f, -16f);
            Spawn(2, RcData.E_WHITE, 2, -196f, -16f);
            Spawn(2, RcData.E_WHITE, 3, -192f, -44f);
            Spawn(2, RcData.E_BLACK, 3, -168f, -44f);
            Spawn(2, RcData.E_WHITE, 3, -224f, 17f);
            Spawn(2, RcData.E_BLACK, 3, -248f, 17f);
            Spawn(2, RcData.E_BLACK, 2, -236f, 58f);
            Spawn(2, RcData.E_ROBOT, 1, -172f, 0f);
            Spawn(2, RcData.E_ROBOT, 1, -208f, 24f);
            Spawn(2, RcData.E_BLACK, 3, -134f, 50f);
            Spawn(2, RcData.E_WHITE, 3, -180f, F, 21f);
            Spawn(2, RcData.E_BLACK, 3, -180f, F, 56f);
            Spawn(2, RcData.E_WHITE, 3, -192f, F, -30f);
            Spawn(2, RcData.E_BLACK, 2, -168f, F, -22f);
            Spawn(2, RcData.E_WHITE, 3, -238f, F, 17f);
            Spawn(2, RcData.E_BLACK, 3, -238f, F, 52f);
            Spawn(2, RcData.E_WHITE, 4, -236f, -F, 50f);
            Spawn(2, RcData.E_BOSS0 + 2, 1, Arena[2, 0], Arena[2, 1]);
        }

        // ── Zone 3: the junkyard ─────────────────────────────────────────────
        private static void Junkyard()
        {
            B(RcMat.Dirt, 128.3f, 41.3f, 265.7f, 175.7f, 0f, 0.04f, false);
            RcMat[] cars = { RcMat.CarA, RcMat.CarB, RcMat.CarC, RcMat.Rust };
            // The car stacks: rows of crushed cars piled two to four high, with gaps to get through. A maze.
            for (int row = 0; row < 8; row++)
            {
                float z = 50f + row * 15f;
                for (int col = 0; col < 12; col++)
                {
                    float x = 140f + col * 7.2f;
                    if (z > 70f && z < 90f && x < 164f) continue;                          // the way in stays clear
                    if (Dist(x, z, Arena[3, 0], Arena[3, 1]) < Arena[3, 2] + 7f) continue; // the crusher pit
                    if (x > 238f && z > 152f) continue;                                    // the crane's foot
                    if (x < 147f && z > 55f && z < 70f) continue;                          // the office shack
                    if ((col + row * 5) % 4 == 0) continue;                                // gaps, staggered row to row
                    int high = 2 + Rnd(3);
                    for (int k = 0; k < high; k++)
                    {
                        float ox = (Rnd() - 0.5f) * 0.5f;
                        B(cars[Rnd(4)], x - 2.9f + ox, z - 1.15f, x + 2.9f + ox, z + 1.15f, k * 1.05f, k * 1.05f + 1f, k == 0);
                    }
                    B(RcMat.None, x - 3f, z - 1.2f, x + 3f, z + 1.2f, 0f, 5f, true);
                }
            }
            // Scrap hills you can't climb.
            float[] hx = { 150f, 196f, 230f, 258f, 238f }, hz = { 168f, 171f, 100f, 60f, 112f };
            for (int i = 0; i < hx.Length; i++)
            {
                S.Cyl(RcMat.None, hx[i], 0f, hz[i], 4.2f, 5f, true);
                S.Ball(RcMat.Rust, hx[i], 0.6f, hz[i], 5f, 3.2f, 5f);
                S.Ball(RcMat.Metal, hx[i] + 1.4f, 1.6f, hz[i] - 1f, 2.4f, 1.9f, 2.4f);
            }
            // The office shack by the way in, and the signal box over the fence (the fort) is the core's.
            Shell(131f, 58f, 139f, 66f, 3f, RcMat.Rust, RcMat.Plank, 1f, 135f, 3f);
            Pile(3, 133f, 60.5f);
            // The crusher and Papa's throne, in the pit.
            float ax = Arena[3, 0], az = Arena[3, 1];
            B(RcMat.Rust, ax - 7f, az + 19f, ax + 7f, az + 25f, 0f, 5.5f, true);
            B(RcMat.Metal, ax - 5.5f, az + 18.4f, ax + 5.5f, az + 19f, 1.2f, 4.6f, false);
            B(RcMat.Tape, ax - 7.05f, az + 18.95f, ax + 7.05f, az + 19f, 0.2f, 0.6f, false);
            B(RcMat.Rust, ax + 12f, az - 3f, ax + 16f, az + 3f, 0f, 1.2f, true);
            B(RcMat.CarB, ax + 14.4f, az - 2.2f, ax + 16f, az + 2.2f, 1.2f, 3.8f, false);
            B(RcMat.Rust, ax + 12f, az - 3f, ax + 13f, az - 2f, 1.2f, 2.3f, false);
            B(RcMat.Rust, ax + 12f, az + 2f, ax + 13f, az + 3f, 1.2f, 2.3f, false);
            S.Light(ax, 6f, az, 3, 26f, 1.1f);
            S.Light(145f, 4f, 80f, 0, 12f, 1.1f);
            // Scrap piles: more here than anywhere.
            float[] px = { 146f, 160f, 188f, 204f, 232f, 252f, 176f, 146f, 222f, 256f, 200f, 170f };
            float[] pz = { 103f, 58f, 102f, 147f, 72f, 92f, 118f, 133f, 172f, 126f, 58f, 162f };
            for (int i = 0; i < px.Length; i++) Pile(3, px[i], pz[i]);

            Spawn(3, RcData.E_JUNK, 3, 152f, 102f);
            Spawn(3, RcData.E_JUNK, 4, 172f, 72f);
            Spawn(3, RcData.E_JUNK, 3, 182f, 118f);
            Spawn(3, RcData.E_JUNK, 4, 200f, 88f);
            Spawn(3, RcData.E_JUNK, 3, 160f, 148f);
            Spawn(3, RcData.E_JUNK, 4, 226f, 58f);
            Spawn(3, RcData.E_JUNK, 3, 250f, 86f);
            Spawn(3, RcData.E_JUNK, 4, 192f, 163f);
            Spawn(3, RcData.E_GOLD, 1, 246f, 58f);
            Spawn(3, RcData.E_FUSED, 1, 206f, 118f);
            Spawn(3, RcData.E_FUSED, 1, 218f, 164f);
            Spawn(3, RcData.E_BOSS0 + 3, 1, ax, az);
        }

        private static float Dist(float x, float z, float x2, float z2) { return (float)System.Math.Sqrt((x - x2) * (x - x2) + (z - z2) * (z - z2)); }

        // ── Zone 4: the Waygetter warehouse ──────────────────────────────────
        // The building and its lot are the Facility (built by the game from FactoryLayout). This plan adds the
        // strip between the zone's gate and the lot, and says where the toys, the scrap and Mama are inside.
        private static void Warehouse()
        {
            float lx0 = FAC_X + FAC_LOT_X0, lx1 = FAC_X + FAC_LOT_X1, lz0 = FAC_Z + FAC_LOT_Z0;
            B(RcMat.Road, lx0, 110.3f, lx1, lz0, 0f, 0.05f, false);
            // The strip ends at the lot's corners: there's nothing round the back.
            FenceZ(RcMat.Concrete, lx0 - 0.15f, 110f, lz0 + 0.3f, 3f);
            FenceZ(RcMat.Concrete, lx1 + 0.15f, 110f, lz0 + 0.3f, 3f);
            B(RcMat.Tape, FAC_X + FAC_GATE_X0 - 0.2f, lz0 - 0.1f, FAC_X + FAC_GATE_X0, lz0 + 0.1f, 0f, 2.2f, false);
            B(RcMat.Tape, FAC_X + FAC_GATE_X1, lz0 - 0.1f, FAC_X + FAC_GATE_X1 + 0.2f, lz0 + 0.1f, 0f, 2.2f, false);
            Lamp(-14f, 112f, 1f, 0f, false);
            Lamp(14f, 112f, -1f, 0f, true);

            // The lot.
            Fac(RcData.E_WARE, 2, 46f, 0f, -14f);
            Fac(RcData.E_WARE, 2, -4f, 0f, -12f);
            FacPile(58f, 0f, -22f); FacPile(-8f, 0f, -5f);
            // Ground floor. Mama keeps to the east half (the dock aisle in the warehouse and the edge of the assembly
            // floor): the packs are in the west half, well back from her, so a fight with one isn't a fight beside her.
            Fac(RcData.E_WARE, 2, 7f, 0f, 10f);             // the lobby
            Fac(RcData.E_WARE, 2, 7f, 0f, 33f);             // the break room
            Fac(RcData.E_WARE, 3, 20f, 0f, 16f);            // the assembly floor, west side
            Fac(RcData.E_WARE, 3, 22f, 0f, 32f);
            Fac(RcData.E_WARE, 2, 20f, 0f, 6.5f);           // the testing lab
            Fac(RcData.E_FUSED, 1, 31f, 0f, 7f);            // in front of the R&D door
            Fac(RcData.E_SHADOW, 1, 52f, 0f, 22f);          // Mama walks the warehouse end
            FacPile(1.5f, 0f, 18.5f); FacPile(1.5f, 0f, 38.5f); FacPile(40.5f, 0f, 35.5f); FacPile(17.5f, 0f, 8.8f); FacPile(25f, 0f, 38.5f);
            // Upstairs: the offices.
            Fac(RcData.E_WARE, 2, 3f, FactoryUp, 17f);
            Fac(RcData.E_WARE, 2, 10.5f, FactoryUp, 24f);
            FacPile(1.2f, FactoryUp, 1.2f); FacPile(13f, FactoryUp, 31f);
            // The basement: storage, the boiler room, and R&D (where the diamond one is).
            Fac(RcData.E_WARE, 3, 12f, FactoryDown, 4f);
            Fac(RcData.E_WARE, 2, 20f, FactoryDown, 10f);
            Fac(RcData.E_DIAMOND, 1, 30f, FactoryDown, 6f);
            FacPile(22.8f, FactoryDown, 4f); FacPile(1.2f, FactoryDown, 9.5f); FacPile(40.5f, FactoryDown, 2f);
            // The warehouse hall: hers.
            Spawn(4, RcData.E_BOSS0 + 4, 1, Arena[4, 0], 0f, Arena[4, 1]);
        }

        private const float FactoryUp = 4f, FactoryDown = -4f; // (FactoryLayout.UP and DOWN: this file has no Unity in it)

        private static void Fac(int enemy, int count, float x, float y, float z) { Spawn(4, enemy, count, FAC_X + x, y, FAC_Z + z); }
        private static void FacPile(float x, float y, float z) { Pile(4, FAC_X + x, y, FAC_Z + z); }
    }

    /// <summary>A sink that draws nothing (the plan is walked once with it, to collect spawns and scrap piles).</summary>
    public class RcNullSink : RcSink
    {
        public override void Box(RcMat m, float x0, float y0, float z0, float x1, float y1, float z1, bool collide) { }
        public override void Walk(RcMat m, float x0, float y0, float z0, float x1, float y1, float z1) { }
        public override void Cyl(RcMat m, float x, float y0, float z, float radius, float height, bool collide) { }
        public override void Ball(RcMat m, float x, float y, float z, float rx, float ry, float rz) { }
        public override void Light(float x, float y, float z, int color, float range, float intensity) { }
    }

    /// <summary>
    /// Where a body can stand in the town, as spots on a one-meter grid: the ground everywhere it isn't blocked,
    /// plus every floor slab and stair step above or below it (each its own spot, at its height). Spots in
    /// neighbouring cells are joined when a body could step from one to the other (no more than STEP up or down),
    /// which is what makes stairs work. Enemies path over the spots (A*), see along them, and the tests flood
    /// them to prove every place can be reached.
    ///
    /// A spot's id: a ground spot is its cell's index; the others follow after all the cells.
    /// </summary>
    public class RcNav : RcSink
    {
        public const float OX = RcTown.X0 - 4f, OZ = RcTown.Z0 - 4f;
        public readonly int W = (int)(RcTown.X1 - RcTown.X0) + 8, H = (int)(RcTown.Z1 - RcTown.Z0) + 8;
        public const float STEP = 0.78f;         // the most a body steps up or down between cells (stairs climb 0.56 a meter: up to four 0.18 steps in one cell)
        private const float AGENT = 0.15f;       // solid things block every cell they come this close to
        private const float HEAD = 1.0f, FOOT = 0.15f; // what has to be clear above a spot

        /// <summary>Ground level: is there ground here at all (not a hole), and is something solid standing on it?</summary>
        public readonly bool[] Ground, Blocked;

        // Spots that aren't the ground.
        private readonly List<int> _xCell = new List<int>();
        private readonly List<float> _xY = new List<float>();
        private readonly List<int> _xNext = new List<int>();
        private readonly int[] _head;

        private struct Solid { public float X0, Y0, Z0, X1, Y1, Z1; public bool Walk; }
        private readonly List<Solid> _solids = new List<Solid>();
        private int _partFirstSolid, _partFirstSpot;

        private static RcNav _instance;
        private static int _instanceWorld = -1;
        private static bool _fresh;
        /// <summary>The whole world's spots (built from every part of the plan, once for the world you're in).</summary>
        public static RcNav Instance
        {
            get
            {
                if (_instance == null || _instanceWorld != RcWorlds.Now)
                {
                    var n = new RcNav();
                    for (int part = RcTown.PART_CORE; part < 5; part++) { n.BeginPart(); RcTown.Build(n, part); n.EndPart(); }
                    _instance = n;
                    _instanceWorld = RcWorlds.Now;
                    _fresh = true;
                }
                return _instance;
            }
        }

        /// <summary>True once after the grid was rebuilt (anything added to it since the last build, like the Facility's floors, is gone).</summary>
        public static bool TakeFresh() { bool f = _fresh; _fresh = false; return f; }

        public RcNav()
        {
            Ground = new bool[W * H]; Blocked = new bool[W * H];
            _head = new int[W * H];
            for (int i = 0; i < _head.Length; i++) _head[i] = -1;
        }

        public int Cells { get { return W * H; } }
        public int SpotCount { get { return W * H + _xCell.Count; } }
        public int CellX(float x) { int c = (int)System.Math.Floor(x - OX); return c < 0 ? 0 : c >= W ? W - 1 : c; }
        public int CellZ(float z) { int c = (int)System.Math.Floor(z - OZ); return c < 0 ? 0 : c >= H ? H - 1 : c; }
        public float CenterX(int cx) { return OX + cx + 0.5f; }
        public float CenterZ(int cz) { return OZ + cz + 0.5f; }
        /// <summary>Can you stand on the ground in this cell?</summary>
        public bool Free(int cx, int cz) { return cx >= 0 && cz >= 0 && cx < W && cz < H && Ground[cz * W + cx] && !Blocked[cz * W + cx]; }
        public bool FreeAt(float x, float z) { return Free(CellX(x), CellZ(z)); }

        public int SpotCell(int id) { return id < Cells ? id : _xCell[id - Cells]; }
        public float SpotY(int id) { return id < Cells ? 0f : _xY[id - Cells]; }
        public float SpotX(int id) { return CenterX(SpotCell(id) % W); }
        public float SpotZ(int id) { return CenterZ(SpotCell(id) / W); }

        // ── Taking in the town's shapes ──────────────────────────────────────
        public void BeginPart() { _partFirstSolid = _solids.Count; _partFirstSpot = _xCell.Count; }

        /// <summary>After a part's shapes are in: spots that turned out to be inside something solid are dropped.</summary>
        public void EndPart()
        {
            for (int i = _partFirstSpot; i < _xCell.Count; i++)
            {
                float y = _xY[i];
                if (float.IsNaN(y)) continue;
                int cell = _xCell[i];
                float px = CenterX(cell % W), pz = CenterZ(cell / W);
                for (int k = _partFirstSolid; k < _solids.Count; k++)
                {
                    Solid s = _solids[k];
                    if (s.Y0 >= y + HEAD || s.Y1 <= y + FOOT) continue;       // not in the way of a body standing here
                    float g = s.Walk ? 0f : AGENT + 0.35f;                      // (walls keep a body's width away; a step is only in the way if you're inside it)
                    if (px <= s.X0 - g || px >= s.X1 + g || pz <= s.Z0 - g || pz >= s.Z1 + g) continue;
                    _xY[i] = float.NaN;
                    break;
                }
            }
        }

        private void AddSolid(float x0, float y0, float z0, float x1, float y1, float z1, bool walk)
        {
            _solids.Add(new Solid { X0 = x0, Y0 = y0, Z0 = z0, X1 = x1, Y1 = y1, Z1 = z1, Walk = walk });
            if (y0 > HEAD || y1 < FOOT) return; // floors, roofs and things overhead don't block a body on the ground
            // Every ground cell the grown box overlaps. (A fence on the line between two cells blocks both. Steps
            // aren't grown: the cell at the foot of a flight has to stay open, or nothing could get onto it.)
            float grow = walk ? 0f : AGENT;
            int cx0 = (int)System.Math.Floor(x0 - grow - OX), cx1 = (int)System.Math.Ceiling(x1 + grow - OX) - 1;
            int cz0 = (int)System.Math.Floor(z0 - grow - OZ), cz1 = (int)System.Math.Ceiling(z1 + grow - OZ) - 1;
            for (int cz = cz0 < 0 ? 0 : cz0; cz <= cz1 && cz < H; cz++)
                for (int cx = cx0 < 0 ? 0 : cx0; cx <= cx1 && cx < W; cx++)
                    Blocked[cz * W + cx] = true;
        }

        public override void Box(RcMat m, float x0, float y0, float z0, float x1, float y1, float z1, bool collide)
        {
            if (collide) AddSolid(x0, y0, z0, x1, y1, z1, false);
        }

        public override void Walk(RcMat m, float x0, float y0, float z0, float x1, float y1, float z1)
        {
            bool ground = y1 > -0.12f && y1 < 0.12f;
            if (!ground) AddSolid(x0, y0, z0, x1, y1, z1, true);
            // A spot in every cell whose middle is on this top.
            for (int cz = CellZ(z0); cz <= CellZ(z1); cz++)
                for (int cx = CellX(x0); cx <= CellX(x1); cx++)
                {
                    float px = CenterX(cx), pz = CenterZ(cz);
                    if (px <= x0 || px >= x1 || pz <= z0 || pz >= z1) continue;
                    if (ground) Ground[cz * W + cx] = true;
                    else AddSpot(cz * W + cx, y1);
                }
        }

        /// <summary>A spot that isn't the ground (a floor, a step), unless there's already one at that height in the cell.</summary>
        public void AddSpot(int cell, float y)
        {
            for (int i = _head[cell]; i >= 0; i = _xNext[i]) if (!float.IsNaN(_xY[i]) && System.Math.Abs(_xY[i] - y) < 0.1f) return;
            _xCell.Add(cell); _xY.Add(y); _xNext.Add(_head[cell]);
            _head[cell] = _xCell.Count - 1;
        }

        public override void Cyl(RcMat m, float x, float y0, float z, float radius, float height, bool collide)
        {
            if (!collide) return;
            _solids.Add(new Solid { X0 = x - radius * 0.8f, Y0 = y0, Z0 = z - radius * 0.8f, X1 = x + radius * 0.8f, Y1 = y0 + height, Z1 = z + radius * 0.8f });
            if (y0 > HEAD || y0 + height < FOOT) return;
            float r = radius + AGENT;
            for (int cz = CellZ(z - r - 1f); cz <= CellZ(z + r + 1f); cz++)
                for (int cx = CellX(x - r - 1f); cx <= CellX(x + r + 1f); cx++)
                {
                    // The nearest point of the cell to the cylinder's middle.
                    float nx = x < OX + cx ? OX + cx : x > OX + cx + 1f ? OX + cx + 1f : x, nz = z < OZ + cz ? OZ + cz : z > OZ + cz + 1f ? OZ + cz + 1f : z;
                    float dx = nx - x, dz = nz - z;
                    if (dx * dx + dz * dz < r * r) Blocked[cz * W + cx] = true;
                }
        }

        public override void Ball(RcMat m, float x, float y, float z, float rx, float ry, float rz) { }
        public override void Light(float x, float y, float z, int color, float range, float intensity) { }

        // ── Finding spots ────────────────────────────────────────────────────
        /// <summary>The spot in a cell nearest a height, if it's within 'tol' of it (-1: none).</summary>
        public int SpotIn(int cx, int cz, float y, float tol)
        {
            if (cx < 0 || cz < 0 || cx >= W || cz >= H) return -1;
            int cell = cz * W + cx, best = -1;
            float bestD = tol;
            if (Ground[cell] && !Blocked[cell]) { float d = y < 0f ? -y : y; if (d <= bestD) { bestD = d; best = cell; } }
            for (int i = _head[cell]; i >= 0; i = _xNext[i])
            {
                float sy = _xY[i];
                if (float.IsNaN(sy)) continue;
                float d = sy > y ? sy - y : y - sy;
                if (d <= bestD) { bestD = d; best = Cells + i; }
            }
            return best;
        }

        public int SpotAt(float x, float y, float z, float tol) { return SpotIn(CellX(x), CellZ(z), y, tol); }
        public bool FreeAt(float x, float y, float z) { return SpotAt(x, y, z, 1.1f) >= 0; }

        /// <summary>The nearest spot to a point, on about the same level: its place in fx, fy, fz. False if there's none within 'reach' cells.</summary>
        public bool Nearest(float x, float y, float z, int reach, out float fx, out float fy, out float fz)
        {
            int cx = CellX(x), cz = CellZ(z);
            fx = x; fy = y; fz = z;
            int s = SpotIn(cx, cz, y, 1.3f);
            if (s >= 0) { fy = SpotY(s); return true; }
            for (int r = 1; r <= reach; r++)
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (System.Math.Abs(dx) != r && System.Math.Abs(dz) != r) continue;
                        s = SpotIn(cx + dx, cz + dz, y, 1.3f);
                        if (s >= 0) { fx = CenterX(cx + dx); fz = CenterZ(cz + dz); fy = SpotY(s); return true; }
                    }
            return false;
        }

        /// <summary>The ground-level version (the tests use it).</summary>
        public bool Nearest(float x, float z, int reach, out float fx, out float fz)
        {
            float fy;
            return Nearest(x, 0f, z, reach, out fx, out fy, out fz);
        }

        /// <summary>Can one point see another: is there floor all the way along the straight line between them?</summary>
        public bool Clear(float x0, float y0, float z0, float x1, float y1, float z1)
        {
            float dx = x1 - x0, dy = y1 - y0, dz = z1 - z0, len = (float)System.Math.Sqrt(dx * dx + dz * dz);
            int steps = (int)(len / 0.45f) + 1;
            for (int i = 1; i < steps; i++)
            {
                float t = i / (float)steps;
                if (SpotAt(x0 + dx * t, y0 + dy * t, z0 + dz * t, 0.75f) < 0) return false;
            }
            return true;
        }

        public bool Clear(float x0, float z0, float x1, float z1) { return Clear(x0, 0f, z0, x1, 0f, z1); }

        // ── A* ───────────────────────────────────────────────────────────────
        private float[] _g = new float[0];
        private int[] _from = new int[0], _stamp = new int[0];
        private int _run;
        private readonly List<int> _heap = new List<int>();
        private readonly List<float> _heapF = new List<float>();
        private readonly int[] _nb = new int[24];
        private readonly float[] _nbCost = new float[24];

        private void Push(int node, float f)
        {
            _heap.Add(node); _heapF.Add(f);
            int i = _heap.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (_heapF[p] <= _heapF[i]) break;
                int tn = _heap[p]; _heap[p] = _heap[i]; _heap[i] = tn;
                float tf = _heapF[p]; _heapF[p] = _heapF[i]; _heapF[i] = tf;
                i = p;
            }
        }

        private int Pop()
        {
            int top = _heap[0], last = _heap.Count - 1;
            _heap[0] = _heap[last]; _heapF[0] = _heapF[last];
            _heap.RemoveAt(last); _heapF.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, s = i;
                if (l < _heap.Count && _heapF[l] < _heapF[s]) s = l;
                if (r < _heap.Count && _heapF[r] < _heapF[s]) s = r;
                if (s == i) break;
                int tn = _heap[s]; _heap[s] = _heap[i]; _heap[i] = tn;
                float tf = _heapF[s]; _heapF[s] = _heapF[i]; _heapF[i] = tf;
                i = s;
            }
            return top;
        }

        private static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 }, DZ = { 0, 0, 1, -1, 1, -1, 1, -1 };

        /// <summary>The spots you can step to from a spot, into _nb (and what each step costs): returns how many.</summary>
        private int Neighbours(int id)
        {
            int cell = SpotCell(id), cx = cell % W, cz = cell / W, n = 0;
            float y = SpotY(id);
            for (int d = 0; d < 8; d++)
            {
                int nx = cx + DX[d], nz = cz + DZ[d];
                if (nx < 0 || nz < 0 || nx >= W || nz >= H) continue;
                if (d >= 4 && (SpotIn(cx + DX[d], cz, y, STEP) < 0 || SpotIn(cx, cz + DZ[d], y, STEP) < 0)) continue; // no cutting corners
                int s = SpotIn(nx, nz, y, STEP);
                if (s < 0) continue;
                float dy = SpotY(s) - y;
                _nb[n] = s;
                _nbCost[n] = (d >= 4 ? 1.4142f : 1f) + (dy < 0f ? -dy : dy);
                n++;
            }
            return n;
        }

        /// <summary>The spots you can step to from a spot: how many (then Step(i) and StepCost(i) give each one).</summary>
        public int Steps(int id) { return Neighbours(id); }
        public int Step(int i) { return _nb[i]; }
        public float StepCost(int i) { return _nbCost[i]; }

        private void Room()
        {
            if (_g.Length >= SpotCount) return;
            _g = new float[SpotCount + 1024]; _from = new int[_g.Length]; _stamp = new int[_g.Length];
            _run = 0;
        }

        /// <summary>
        /// A path from one point to another as (x, y, z) triples in 'path', straightened where the way is clear. False
        /// if there's none within maxNodes spots searched (enemies pass a few thousand: they only chase nearby).
        /// </summary>
        public bool FindPath(float x0, float y0, float z0, float x1, float y1, float z1, int maxNodes, List<float> path)
        {
            path.Clear();
            float sx, sy, sz, tx, ty, tz;
            if (!Nearest(x0, y0, z0, 4, out sx, out sy, out sz) || !Nearest(x1, y1, z1, 4, out tx, out ty, out tz)) return false;
            int start = SpotAt(sx, sy, sz, 0.2f), goal = SpotAt(tx, ty, tz, 0.2f);
            if (start < 0 || goal < 0) return false;
            if (start == goal) { path.Add(x1); path.Add(ty); path.Add(z1); return true; }
            Room();
            _run++;
            _heap.Clear(); _heapF.Clear();
            _g[start] = 0f; _from[start] = -1; _stamp[start] = _run;
            Push(start, 0f);
            float gx = SpotX(goal), gy = SpotY(goal), gz = SpotZ(goal);
            int searched = 0;
            bool found = false;
            while (_heap.Count > 0 && searched++ < maxNodes)
            {
                int cur = Pop();
                if (cur == goal) { found = true; break; }
                int n = Neighbours(cur);
                for (int i = 0; i < n; i++)
                {
                    int s = _nb[i];
                    float g = _g[cur] + _nbCost[i];
                    if (_stamp[s] == _run && g >= _g[s]) continue;
                    _stamp[s] = _run; _g[s] = g; _from[s] = cur;
                    float hx = System.Math.Abs(SpotX(s) - gx), hz = System.Math.Abs(SpotZ(s) - gz), hy = System.Math.Abs(SpotY(s) - gy);
                    Push(s, g + (hx > hz ? hx + 0.4142f * hz : hz + 0.4142f * hx) + hy);
                }
            }
            if (!found) return false;
            // Walk back, then drop every point you can see past.
            var spots = new List<int>();
            for (int c = goal; c != -1; c = _from[c]) spots.Add(c);
            spots.Reverse();
            float ax = sx, ay = sy, az = sz;
            int at = 1;
            while (at < spots.Count)
            {
                int far = at;
                for (int j = spots.Count - 1; j > at; j--)
                    if (System.Math.Abs(SpotY(spots[j]) - ay) < 0.05f && Clear(ax, ay, az, SpotX(spots[j]), SpotY(spots[j]), SpotZ(spots[j]))) { far = j; break; } // (only along the flat: stairs are walked step by step)
                ax = SpotX(spots[far]); ay = SpotY(spots[far]); az = SpotZ(spots[far]);
                path.Add(ax); path.Add(ay); path.Add(az);
                at = far + 1;
            }
            if (path.Count >= 3 && SpotAt(x1, ty, z1, 0.2f) >= 0) { path[path.Count - 3] = x1; path[path.Count - 1] = z1; }
            return true;
        }

        /// <summary>The ground-level version, as (x, z) pairs (the tests use it).</summary>
        public bool FindPath(float x0, float z0, float x1, float z1, int maxNodes, List<float> path)
        {
            var p3 = new List<float>();
            bool ok = FindPath(x0, 0f, z0, x1, 0f, z1, maxNodes, p3);
            path.Clear();
            for (int i = 0; i + 2 < p3.Count; i += 3) { path.Add(p3[i]); path.Add(p3[i + 2]); }
            return ok;
        }

        /// <summary>Every spot you can walk to from a point (for the tests and the log): true where reachable, indexed by spot id.</summary>
        public bool[] Flood(float x, float y, float z)
        {
            var seen = new bool[SpotCount];
            float sx, sy, sz;
            if (!Nearest(x, y, z, 4, out sx, out sy, out sz)) return seen;
            int s0 = SpotAt(sx, sy, sz, 0.2f);
            if (s0 < 0) return seen;
            var open = new List<int>(); // (a plain list used as a stack: nothing from outside mscorlib)
            seen[s0] = true; open.Add(s0);
            while (open.Count > 0)
            {
                int cur = open[open.Count - 1];
                open.RemoveAt(open.Count - 1);
                int n = Neighbours(cur);
                for (int i = 0; i < n; i++)
                {
                    if (seen[_nb[i]]) continue;
                    seen[_nb[i]] = true; open.Add(_nb[i]);
                }
            }
            return seen;
        }

        public bool[] Flood(float x, float z) { return Flood(x, 0f, z); }

        /// <summary>Was a point reachable in a flood (the spot at it, or the nearest within 'slack' cells)?</summary>
        public bool Reached(bool[] seen, float x, float y, float z, int slack)
        {
            float fx, fy, fz;
            if (!Nearest(x, y, z, slack, out fx, out fy, out fz)) return false;
            int s = SpotAt(fx, fy, fz, 0.2f);
            return s >= 0 && s < seen.Length && seen[s];
        }
    }

    /// <summary>
    /// How far every spot in the town is, on foot, from one place (the job you're on). With that, the way to walk
    /// from anywhere is just downhill, so the quest arrow can always point along the real route: round fences,
    /// through the one gate, to the stairs. It's worked out from the place outward, a few thousand spots a frame
    /// (a whole town takes about a second), and again whenever the place changes.
    /// </summary>
    public class RcField
    {
        private readonly RcNav _nav;
        private float[] _d = new float[0];
        private readonly List<int> _heap = new List<int>();
        private readonly List<float> _heapF = new List<float>();
        private int _goal = -1;

        public float X, Y, Z;               // the place
        public bool Started { get { return _goal >= 0; } }
        public bool Done { get { return _goal >= 0 && _heap.Count == 0; } }

        public RcField(RcNav nav) { _nav = nav; }

        private void Push(int node, float f)
        {
            _heap.Add(node); _heapF.Add(f);
            int i = _heap.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (_heapF[p] <= _heapF[i]) break;
                int tn = _heap[p]; _heap[p] = _heap[i]; _heap[i] = tn;
                float tf = _heapF[p]; _heapF[p] = _heapF[i]; _heapF[i] = tf;
                i = p;
            }
        }

        private int Pop(out float f)
        {
            int top = _heap[0], last = _heap.Count - 1;
            f = _heapF[0];
            _heap[0] = _heap[last]; _heapF[0] = _heapF[last];
            _heap.RemoveAt(last); _heapF.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, s = i;
                if (l < _heap.Count && _heapF[l] < _heapF[s]) s = l;
                if (r < _heap.Count && _heapF[r] < _heapF[s]) s = r;
                if (s == i) break;
                int tn = _heap[s]; _heap[s] = _heap[i]; _heap[i] = tn;
                float tf = _heapF[s]; _heapF[s] = _heapF[i]; _heapF[i] = tf;
                i = s;
            }
            return top;
        }

        /// <summary>Start over from a new place. False if there's nowhere to stand near it.</summary>
        public bool Begin(float x, float y, float z)
        {
            X = x; Y = y; Z = z;
            _heap.Clear(); _heapF.Clear();
            _goal = -1;
            float fx, fy, fz;
            if (!_nav.Nearest(x, y, z, 5, out fx, out fy, out fz)) return false;
            int goal = _nav.SpotAt(fx, fy, fz, 0.2f);
            if (goal < 0) return false;
            if (_d.Length < _nav.SpotCount) _d = new float[_nav.SpotCount + 4096];
            for (int i = 0; i < _d.Length; i++) _d[i] = float.MaxValue;
            _goal = goal;
            _d[goal] = 0f;
            Push(goal, 0f);
            return true;
        }

        /// <summary>Carries on working outward, up to 'budget' spots. (Call every frame until Done.)</summary>
        public void Work(int budget)
        {
            while (budget-- > 0 && _heap.Count > 0)
            {
                float f;
                int cur = Pop(out f);
                if (f > _d[cur]) continue; // (an older, longer way to it)
                int n = _nav.Steps(cur);
                for (int i = 0; i < n; i++)
                {
                    int s = _nav.Step(i);
                    if (s >= _d.Length) continue; // (a spot added since this began)
                    float d = f + _nav.StepCost(i);
                    if (d >= _d[s]) continue;
                    _d[s] = d;
                    Push(s, d);
                }
            }
        }

        /// <summary>
        /// From where you stand: the point 'ahead' steps along the way to the place, and how far the whole walk is.
        /// False if it isn't known yet (still being worked out) or you can't get there from here.
        /// </summary>
        public bool Toward(float x, float y, float z, int ahead, out float fx, out float fy, out float fz, out float walk)
        {
            fx = x; fy = y; fz = z; walk = 0f;
            if (_goal < 0) return false;
            float sx, sy, sz;
            if (!_nav.Nearest(x, y, z, 4, out sx, out sy, out sz)) return false;
            int cur = _nav.SpotAt(sx, sy, sz, 0.2f);
            if (cur < 0 || cur >= _d.Length || _d[cur] == float.MaxValue) return false;
            walk = _d[cur];
            for (int k = 0; k < ahead && cur != _goal; k++)
            {
                int n = _nav.Steps(cur), best = -1;
                float bestD = _d[cur];
                for (int i = 0; i < n; i++)
                {
                    int s = _nav.Step(i);
                    if (s < _d.Length && _d[s] < bestD) { bestD = _d[s]; best = s; }
                }
                if (best < 0) break;
                cur = best;
            }
            if (cur == _goal) { fx = X; fy = _nav.SpotY(cur); fz = Z; }
            else { fx = _nav.SpotX(cur); fy = _nav.SpotY(cur); fz = _nav.SpotZ(cur); }
            return true;
        }
    }
}
