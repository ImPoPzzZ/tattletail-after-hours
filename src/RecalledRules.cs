using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>Recalled's zones, in the order you play them (docs/RECALLED_DESIGN.md).</summary>
    public enum RecalledZone { Suburbs = 0, Mall = 1, School = 2, Junkyard = 3, Warehouse = 4 }

    /// <summary>
    /// Recalled's rules as plain numbers (no scene, no saves, nothing calls it yet): levels and experience, how
    /// enemies scale with the group, the backpack and stash, item level requirements and the zone gates. Every
    /// number is a starting point to tune in playtests (design doc, section 9b).
    /// </summary>
    public static class RecalledRules
    {
        // ── Levels and experience ────────────────────────────────────────────
        public const int LEVEL_CAP = 25;       // both worlds together
        public const int TOWN_CAP = 15;        // the town stops giving experience here (Egypt takes you the rest of the way)

        /// <summary>Experience needed to go from this level to the next (0 at the cap).</summary>
        public static int XpToNext(int level)
        {
            if (level < 1) level = 1;
            return level >= LEVEL_CAP ? 0 : 100 * level; // level 2 comes fast; 10,500 in total to reach 15, 30,000 to reach 25
        }

        /// <summary>The level a character with this much total experience is.</summary>
        public static int LevelFor(int totalXp)
        {
            int level = 1;
            while (level < LEVEL_CAP && totalXp >= XpToNext(level)) { totalXp -= XpToNext(level); level++; }
            return level;
        }

        /// <summary>A boss gives about a third of a level.</summary>
        public static int BossXp(int level) { return Mathf.Max(30, XpToNext(Mathf.Clamp(level, 1, LEVEL_CAP - 1)) / 3); }

        // ── Scaling with the group (1 to 4 players) ──────────────────────────
        public const int MAX_PLAYERS = 4;
        private static readonly float[] HealthScale = { 1f, 1.6f, 2.2f, 2.8f };

        /// <summary>Enemy and boss health for this many players. Damage never scales; experience isn't split.</summary>
        public static float EnemyHealthScale(int players) { return HealthScale[Mathf.Clamp(players, 1, MAX_PLAYERS) - 1]; }

        /// <summary>Extra enemies in a pack for this many players.</summary>
        public static int PackBonus(int players) { return Mathf.Clamp(players, 1, MAX_PLAYERS) - 1; }

        /// <summary>Bosses add something (helpers, a two-place attack) at three or more players.</summary>
        public static bool BossHasExtras(int players) { return players >= 3; }

        // ── Backpack and stash ───────────────────────────────────────────────
        public const float HEALTH_PER_LEVEL = 30f;   // every level past the first makes you this much tougher
        public const int BACKPACK_START = 20, BACKPACK_STEP = 5, BACKPACK_UPGRADES = 4; // one per zone boss before the warehouse
        public const int STASH_SLOTS = 100;
        public const int MATERIAL_STACK = 50;

        public static int BackpackSlots(int upgrades) { return BACKPACK_START + BACKPACK_STEP * Mathf.Clamp(upgrades, 0, BACKPACK_UPGRADES); }

        // ── Gear ─────────────────────────────────────────────────────────────
        /// <summary>Every item needs a level to equip (it can still be traded and carried below that).</summary>
        public static bool CanEquip(int playerLevel, int itemLevel) { return playerLevel >= itemLevel; }

        // ── Zones ────────────────────────────────────────────────────────────
        private static readonly int[] ZoneLevel = { 1, 4, 7, 10, 13 };
        private static readonly int[] ZoneLevelEgypt = { 15, 17, 19, 21, 23 };

        /// <summary>The level a zone's gate opens at, in the world you're in.</summary>
        public static int GateLevel(RecalledZone zone) { return GateLevelIn(RcWorlds.Now, (int)zone); }

        /// <summary>The level a zone's gate opens at, in a given world.</summary>
        public static int GateLevelIn(int world, int zone) { return world == RcWorlds.EGYPT ? ZoneLevelEgypt[zone] : ZoneLevel[zone]; }

        /// <summary>The experience that makes level 'level' (the town stops here: nothing in it gives more).</summary>
        public static int XpFor(int level)
        {
            int t = 0;
            for (int l = 1; l < level && l < LEVEL_CAP; l++) t += 100 * l;
            return t;
        }

        /// <summary>A gate opens at the zone's level, once the boss before it is beaten (bossesBeaten: how many, in order).</summary>
        public static bool CanEnter(RecalledZone zone, int playerLevel, int bossesBeaten)
        {
            return playerLevel >= GateLevel(zone) && bossesBeaten >= (int)zone;
        }
    }
}
