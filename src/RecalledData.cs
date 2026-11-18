using System.Collections.Generic;

namespace KaleidoscopeEndless
{
    // Recalled's game data: items, recipes, enemies, loot and the quest chain (docs/RECALLED_DESIGN.md).
    // Pure data and lookups: no Unity types, so it runs in the headless tests too. Item and enemy ids are
    // saved in players' characters: never renumber them, only append.

    public enum RcKind { Material, Melee, Ranged, Gadget, Armor, Trinket, Mount }
    public enum RcAbility { None, Shock, Repair, Decoy, Shield }

    public class RcItem
    {
        public int Id, Level, Zone;          // Level: needed to equip. Zone: which zone's tier it belongs to (0-4)
        public string Name, Desc;
        public RcKind Kind;
        public float Power;                  // damage per hit; for gadgets, the ability's strength
        public float Rate;                   // hits per second; for gadgets, the cooldown in seconds
        public float Range;                  // reach (melee), projectile range (ranged), radius (gadgets)
        public float Noise;                  // how far toys hear it, in meters
        public RcAbility Ability;
        public int Health;                   // + max health
        public int Armor;                    // armor points: each one is 1% less damage taken
        public float PowerPct, SpeedPct, CooldownPct, HealingPct; // + percent
        public bool IsGear { get { return Kind != RcKind.Material && Kind != RcKind.Mount; } }   // (a mount isn't worn: it stays in your backpack and V rides it)
    }

    public class RcCost { public int Id, Count; public RcCost(int id, int count) { Id = id; Count = count; } }

    public class RcRecipe
    {
        public int Makes;                    // item id (0 = a backpack upgrade)
        public RcCost[] Needs;
        public int Zone;                     // the bench tier: how many bosses you must have beaten
        public int World;                    // whose bench makes it: 0 the treehouse's, 1 Last Stop's
    }

    public enum RcEnemyKind { Stray, JackBox, RcCar, Robot, Fused, Shadow, Boss }

    public class RcDrop { public int Id, Min, Max; public float Chance; public RcDrop(int id, float chance, int min, int max) { Id = id; Chance = chance; Min = min; Max = max; } }

    public class RcEnemyDef
    {
        public int Id, Zone, Color;          // Color: 0 purple, 1 yellow, 2 teal, 3 black, 4 white, 5 gold, 6 diamond; Egypt: 7 sand, 8 bandage, 9 jackal, 10 faience, 11 bunker, 12 pharaoh, 13 anubis
        public int World;                    // 0 the town, 1 Egypt (Zone is within it)
        public string Name;
        public RcEnemyKind Kind;
        public float Hp, Damage, Speed, Sight;
        public int Xp;
        public float Scale = 1f;
        public RcDrop[] Drops;
        public bool Elite { get { return Kind != RcEnemyKind.Stray; } }
    }

    public enum RcQuestKind { Talk, Kill, Bring, Elite, Boss, Craft, Reach, Scene, Station }

    public class RcQuest
    {
        public string Title, Text, Done;     // Done: what the quest giver says when you hand it in
        public int World;                    // 0 the town, 1 Egypt (Zone is within it)
        public RcQuestKind Kind;
        public int Zone, Target, Count;      // Target: an enemy id (Kill/Elite, 0 = any toy in the zone), an item id (Bring/Craft)
        public int Xp;
        public RcCost[] Reward;
        public string[] Steps;               // how to do it, step by step (the objective screen: J)
        public bool Keep;                    // a Bring job that doesn't take what you bring (you hand it in and keep it)

        public RcQuest How(params string[] steps) { Steps = steps; return this; }
        public RcQuest Keeping() { Keep = true; return this; }
    }

    public static partial class RcData
    {
        // ── Item ids ─────────────────────────────────────────────────────────
        public const int SPRING = 1, PLASTIC = 2, FUR = 3, SCREW = 4, BATTERY = 5, CIRCUIT = 6, VOICE_BOX = 7, EYE = 8, GOLD_FUR = 9, DIAMOND_EYE = 10;
        public const int MOUNT0 = 160, MOUNT_SPEED = 55;                                              // the three mounts (160 to 162), and what they add
        public const int TAPE = 30;                                                                   // Mama's Tape (a quest item)
        public const int THREAD = 20, DISPLAY_GLASS = 21, MONITOR_LENS = 22, MONOCLE = 23, MAMAS_EYE = 24; // one per boss
        public const int BAT = 100, SLINGSHOT = 110, SHOCK_BATTERY = 120, PAN_LID = 130;

        private static readonly string[] ZoneNamesTown = { "The Suburbs", "The Mall", "The School", "The Junkyard", "The Waygetter Warehouse" };
        private static readonly string[] BossNamesTown = { "Patches", "The Display Model", "The Hall Monitor", "Papa", "Mama" };
        private static int _worldNow;   // (while the data is built: which world the jobs and toys being defined belong to)

        /// <summary>This world's zone names, in order.</summary>
        public static string[] ZoneNames { get { return ZoneNamesTown; } }
        /// <summary>This world's boss names, in order.</summary>
        public static string[] BossNames { get { return BossNamesTown; } }
        public static string[] ZoneNamesOf(int world) { return ZoneNamesTown; }
        public static string[] BossNamesOf(int world) { return BossNamesTown; }

        private static readonly Dictionary<int, RcItem> _items = new Dictionary<int, RcItem>();
        private static readonly List<RcItem> _itemList = new List<RcItem>();
        private static readonly Dictionary<int, RcEnemyDef> _enemies = new Dictionary<int, RcEnemyDef>();
        public static readonly List<RcRecipe> Recipes = new List<RcRecipe>();
        public static readonly List<RcQuest> Quests = new List<RcQuest>();

        public static RcItem Item(int id) { RcItem it; return _items.TryGetValue(id, out it) ? it : null; }
        public static List<RcItem> Items { get { return _itemList; } }
        public static RcEnemyDef Enemy(int id) { RcEnemyDef e; return _enemies.TryGetValue(id, out e) ? e : null; }
        public static IEnumerable<RcEnemyDef> Enemies { get { return _enemies.Values; } }

        // ── Enemy ids ────────────────────────────────────────────────────────
        public const int E_PURPLE = 1, E_YELLOW = 2, E_TEAL = 3, E_BLACK = 4, E_WHITE = 5, E_JUNK = 6, E_WARE = 7, E_GOLD = 8, E_DIAMOND = 9;
        public const int E_JACK = 20, E_RCCAR = 21, E_ROBOT = 22, E_FUSED = 23, E_SHADOW = 24;
        public const int E_BOSS0 = 40; // + zone: Patches, the Display Model, the Hall Monitor, Papa, Mama
        /// <summary>The boss of a zone (the town: E_BOSS0 + zone).</summary>
        public static int BossId(int world, int zone) { return E_BOSS0 + zone; }

        static RcData()
        {
            // Materials: what toys are made of.
            Mat(SPRING, "Spring", "A coiled spring from inside a toy.");
            Mat(PLASTIC, "Plastic", "Cracked toy casing.");
            Mat(FUR, "Fur", "A handful of Tattletail fur.");
            Mat(SCREW, "Screws", "Tiny screws. They get everywhere.");
            Mat(BATTERY, "Battery", "Still has some charge.");
            Mat(CIRCUIT, "Circuit Board", "The brains of a toy.");
            Mat(VOICE_BOX, "Voice Box", "It still whispers if you shake it.");
            Mat(EYE, "Glass Eye", "It follows you around the room.");
            Mat(GOLD_FUR, "Gold Fur", "From a golden Tattletail. Rare.");
            Mat(DIAMOND_EYE, "Diamond Eye", "From a diamond Tattletail. Very rare.");
            Mat(THREAD, "Patches' Thread", "The thread that held Patches together.");
            Mat(DISPLAY_GLASS, "Display Glass", "A shard of the shop window.");
            Mat(MONITOR_LENS, "Monitor Lens", "The Hall Monitor's searchlight lens.");
            Mat(MONOCLE, "Papa's Monocle", "Cracked. He won't be needing it.");
            Mat(MAMAS_EYE, "Mama's Eye", "It's over. For tonight.");
            Mat(TAPE, "Mama's Tape", "A cassette from Mama's back. It's warm, and a small light on it blinked once.");

            // Mounts: toys you ride. All three are +55% speed; they differ in what they look like and when you can make them. V rides.
            Add(new RcItem { Id = MOUNT0, Name = "Toy Wagon", Kind = RcKind.Mount, Zone = 0, Level = 1, SpeedPct = MOUNT_SPEED, Desc = "A red wagon with the handle bent over. Hop in." });
            Add(new RcItem { Id = MOUNT0 + 1, Name = "Ride-On RC Car", Kind = RcKind.Mount, Zone = 1, Level = 1, SpeedPct = MOUNT_SPEED, Desc = "A kid-sized RC car with the lights still working." });
            Add(new RcItem { Id = MOUNT0 + 2, Name = "Rocking Horse", Kind = RcKind.Mount, Zone = 3, Level = 1, SpeedPct = MOUNT_SPEED, Desc = "It rocks so hard it goes. Don't look at its eyes." });

            // Melee: quiet, up close. (id, name, zone, damage, hits/s, reach, noise)
            string[] melee = { "Baseball Bat", "Hockey Stick", "Pipe Wrench", "Crowbar", "Sledgehammer" };
            float[] meleeDmg = { 14f, 27f, 44f, 66f, 94f };
            for (int z = 0; z < 5; z++)
                Add(new RcItem { Id = 100 + z, Name = melee[z], Kind = RcKind.Melee, Zone = z, Level = RecalledRules.GateLevelIn(0, z), Power = meleeDmg[z], Rate = z == 4 ? 1.1f : 1.7f, Range = 2.3f, Noise = 7f,
                    Desc = z == 4 ? "Slow, heavy, and it ends arguments." : "Quiet, and it works." });

            // Ranged: loud, from a distance.
            string[] ranged = { "Slingshot", "Dart Blaster", "Nail Gun", "Bottle-Rocket Launcher", "Bolt Thrower" };
            float[] rangedDmg = { 11f, 21f, 34f, 52f, 74f };
            float[] rangedNoise = { 10f, 14f, 26f, 40f, 30f };
            for (int z = 0; z < 5; z++)
                Add(new RcItem { Id = 110 + z, Name = ranged[z], Kind = RcKind.Ranged, Zone = z, Level = RecalledRules.GateLevelIn(0, z), Power = rangedDmg[z], Rate = z == 2 ? 2.4f : 1.3f, Range = 45f, Noise = rangedNoise[z],
                    Desc = z == 2 ? "Fast and very loud. Every toy nearby will hear it." : "Keeps them at a distance. They'll hear it." });

            // Gadgets: abilities on a cooldown (Rate = seconds).
            Add(new RcItem { Id = 120, Name = "Shock Battery", Kind = RcKind.Gadget, Zone = 0, Level = 1, Ability = RcAbility.Shock, Power = 32f, Rate = 8f, Range = 5f, Noise = 16f, Desc = "A rigged battery: shocks every toy around you." });
            Add(new RcItem { Id = 121, Name = "Repair Kit", Kind = RcKind.Gadget, Zone = 1, Level = 4, Ability = RcAbility.Repair, Power = 45f, Rate = 18f, Range = 6f, Noise = 4f, Desc = "Patches you up: heals 45% of your health." });
            Add(new RcItem { Id = 122, Name = "Decoy Toy", Kind = RcKind.Gadget, Zone = 2, Level = 7, Ability = RcAbility.Decoy, Power = 8f, Rate = 20f, Range = 18f, Noise = 22f, Desc = "A chattering toy that draws them away for 8 seconds." });
            Add(new RcItem { Id = 123, Name = "Lid Shield", Kind = RcKind.Gadget, Zone = 3, Level = 10, Ability = RcAbility.Shield, Power = 160f, Rate = 22f, Range = 0f, Noise = 6f, Desc = "A humming shield that soaks up 160 damage for 6 seconds." });
            Add(new RcItem { Id = 124, Name = "Overcharger", Kind = RcKind.Gadget, Zone = 4, Level = 13, Ability = RcAbility.Shock, Power = 150f, Rate = 10f, Range = 7f, Noise = 30f, Desc = "Every battery you own, at once." });

            // Armor: armor points (each one is 1% less damage from everything), and some health.
            string[] armor = { "Pan Lid", "Trash-Can Lid", "Street Sign", "Car Door", "Vault Plate" };
            int[] armorPts = { 10, 20, 30, 40, 50 }, armorHp = { 20, 45, 80, 120, 165 };
            for (int z = 0; z < 5; z++)
                Add(new RcItem { Id = 130 + z, Name = armor[z], Kind = RcKind.Armor, Zone = z, Level = RecalledRules.GateLevelIn(0, z), Armor = armorPts[z], Health = armorHp[z],
                    Desc = "Strapped to your arm. " + armorPts[z] + " armor: you take " + armorPts[z] + "% less damage. +" + armorHp[z] + " health." });

            // Trinkets: they push your build one way. Three tiers each (zones 0, 2, 4).
            for (int t = 0; t < 3; t++)
            {
                int zone = t * 2, lvl = RecalledRules.GateLevelIn(0, zone);
                string tier = t == 0 ? "" : t == 1 ? " II" : " III";
                float k = 1f + t;
                Add(new RcItem { Id = 140 + t, Name = "Lucky Marble" + tier, Kind = RcKind.Trinket, Zone = zone, Level = lvl, PowerPct = 10f * k, Desc = "+" + 10f * k + "% damage." });
                Add(new RcItem { Id = 143 + t, Name = "Sneakers" + tier, Kind = RcKind.Trinket, Zone = zone, Level = lvl, SpeedPct = 6f * k, Desc = "+" + 6f * k + "% movement speed." });
                Add(new RcItem { Id = 146 + t, Name = "Wind-Up Key" + tier, Kind = RcKind.Trinket, Zone = zone, Level = lvl, CooldownPct = 10f * k, Desc = "Gadgets recharge " + 10f * k + "% faster." });
                Add(new RcItem { Id = 149 + t, Name = "Band-Aid Box" + tier, Kind = RcKind.Trinket, Zone = zone, Level = lvl, HealingPct = 20f * k, Desc = "+" + 20f * k + "% healing." });
                Add(new RcItem { Id = 152 + t, Name = "Teddy Patch" + tier, Kind = RcKind.Trinket, Zone = zone, Level = lvl, Health = 25 * (int)k, Desc = "+" + 25 * (int)k + " health." });
            }

            BuildRecipes();
            BuildEnemies();
            BuildQuests();
        }

        private static void Mat(int id, string name, string desc) { Add(new RcItem { Id = id, Name = name, Desc = desc, Kind = RcKind.Material }); }
        private static void Add(RcItem it) { _items[it.Id] = it; _itemList.Add(it); }

        // Each zone's two common materials, and its rarer one.
        private static readonly int[] ZoneMatA = { SPRING, BATTERY, CIRCUIT, SCREW, VOICE_BOX };
        private static readonly int[] ZoneMatB = { PLASTIC, PLASTIC, EYE, BATTERY, CIRCUIT };
        private static readonly int[] ZoneMatC = { FUR, CIRCUIT, VOICE_BOX, GOLD_FUR, DIAMOND_EYE };

        private static void BuildRecipes()
        {
            // Mounts first (so they're on the bench's first page): the wagon from the start, the RC car after Patches, the horse after the Hall Monitor.
            Recipe(MOUNT0, 0, new RcCost(PLASTIC, 6), new RcCost(SPRING, 4), new RcCost(SCREW, 4));
            Recipe(MOUNT0 + 1, 1, new RcCost(CIRCUIT, 4), new RcCost(BATTERY, 6), new RcCost(PLASTIC, 8));
            Recipe(MOUNT0 + 2, 3, new RcCost(FUR, 12), new RcCost(SPRING, 10), new RcCost(SCREW, 8), new RcCost(GOLD_FUR, 2));
            for (int z = 0; z < 5; z++)
            {
                int a = ZoneMatA[z], b = ZoneMatB[z], c = ZoneMatC[z], n = 4 + z * 2;
                Recipe(100 + z, z, new RcCost(a, n), new RcCost(SCREW, 2 + z));
                Recipe(110 + z, z, new RcCost(a, n), new RcCost(b, n - 1));
                Recipe(120 + z, z, new RcCost(BATTERY, 2 + z * 2), new RcCost(c, z >= 3 ? 1 : 2 + z));
                Recipe(130 + z, z, new RcCost(b, n + 2), new RcCost(SCREW, 3 + z));
            }
            for (int t = 0; t < 3; t++)
            {
                int zone = t * 2, c = ZoneMatC[zone], n = t == 2 ? 1 : 3 + t * 2;
                Recipe(140 + t, zone, new RcCost(PLASTIC, 4 + t * 4), new RcCost(c, n));
                Recipe(143 + t, zone, new RcCost(FUR, 4 + t * 4), new RcCost(SPRING, 4 + t * 4));
                Recipe(146 + t, zone, new RcCost(SPRING, 6 + t * 4), new RcCost(SCREW, 4 + t * 3));
                Recipe(149 + t, zone, new RcCost(FUR, 6 + t * 4), new RcCost(c, n));
                Recipe(152 + t, zone, new RcCost(FUR, 8 + t * 6));
            }
            // Backpack upgrades: one per boss part, in order.
            for (int u = 0; u < RecalledRules.BACKPACK_UPGRADES; u++)
                Recipes.Add(new RcRecipe { Makes = 0, Zone = u + 1, Needs = new[] { new RcCost(THREAD + u, 1), new RcCost(FUR, 6 + u * 4), new RcCost(SCREW, 4 + u * 3) } });
        }

        private static void Recipe(int makes, int zone, params RcCost[] needs) { Recipes.Add(new RcRecipe { Makes = makes, Zone = zone, Needs = needs, World = 0 }); }

        private static RcEnemyDef E(int id, string name, RcEnemyKind kind, int zone, int color, float hp, float dmg, float speed, int xp, float scale, params RcDrop[] drops)
        {
            var e = new RcEnemyDef { Id = id, World = _worldNow, Name = name, Kind = kind, Zone = zone, Color = color, Hp = hp, Damage = dmg, Speed = speed, Xp = xp, Scale = scale, Drops = drops, Sight = kind == RcEnemyKind.Boss ? 30f : 11f };
            _enemies[id] = e;
            return e;
        }

        private static RcDrop D(int id, float chance, int min, int max) { return new RcDrop(id, chance, min, max); }

        private static void BuildEnemies()
        {
            E(E_PURPLE, "Stray", RcEnemyKind.Stray, 0, 0, 36f, 8f, 3.3f, 12, 1f, D(SPRING, 0.7f, 1, 2), D(PLASTIC, 0.6f, 1, 2), D(FUR, 0.5f, 1, 1), D(SCREW, 0.4f, 1, 2), D(BATTERY, 0.3f, 1, 1));
            E(E_YELLOW, "Yellow Stray", RcEnemyKind.Stray, 0, 1, 62f, 11f, 3.6f, 19, 1.05f, D(SPRING, 0.8f, 1, 3), D(FUR, 0.7f, 1, 2), D(SCREW, 0.5f, 1, 2), D(BATTERY, 0.6f, 1, 2));
            E(E_TEAL, "Teal Stray", RcEnemyKind.Stray, 1, 2, 115f, 16f, 3.8f, 26, 1.1f, D(BATTERY, 1f, 1, 3), D(PLASTIC, 0.7f, 1, 3), D(CIRCUIT, 0.35f, 1, 1), D(FUR, 0.4f, 1, 2));
            E(E_BLACK, "Black Stray", RcEnemyKind.Stray, 2, 3, 195f, 24f, 4.0f, 40, 1.1f, D(CIRCUIT, 0.7f, 1, 2), D(EYE, 0.6f, 1, 2), D(VOICE_BOX, 0.3f, 1, 1), D(SCREW, 0.5f, 1, 3));
            E(E_WHITE, "White Stray", RcEnemyKind.Stray, 2, 4, 235f, 27f, 4.0f, 44, 1.15f, D(CIRCUIT, 0.7f, 1, 2), D(EYE, 0.7f, 1, 2), D(VOICE_BOX, 0.4f, 1, 1), D(FUR, 0.5f, 1, 3));
            E(E_JUNK, "Scrapyard Stray", RcEnemyKind.Stray, 3, 3, 340f, 34f, 4.2f, 45, 1.2f, D(SCREW, 0.9f, 2, 4), D(BATTERY, 0.7f, 1, 3), D(SPRING, 0.6f, 2, 4), D(CIRCUIT, 0.4f, 1, 2));
            E(E_WARE, "Warehouse Stray", RcEnemyKind.Stray, 4, 4, 480f, 44f, 4.3f, 70, 1.25f, D(VOICE_BOX, 0.7f, 1, 2), D(CIRCUIT, 0.7f, 1, 3), D(EYE, 0.5f, 1, 2), D(FUR, 0.6f, 2, 4));
            E(E_GOLD, "Gold Tattletail", RcEnemyKind.Stray, 3, 5, 950f, 40f, 4.4f, 180, 1.45f, D(GOLD_FUR, 1f, 1, 2), D(SCREW, 1f, 3, 6), D(BATTERY, 1f, 2, 4));
            E(E_DIAMOND, "Diamond Tattletail", RcEnemyKind.Stray, 4, 6, 1450f, 52f, 4.5f, 300, 1.5f, D(DIAMOND_EYE, 1f, 1, 2), D(VOICE_BOX, 1f, 2, 4), D(CIRCUIT, 1f, 2, 5));

            E(E_JACK, "Jack-in-the-Box", RcEnemyKind.JackBox, 0, 0, 170f, 22f, 0f, 60, 1f, D(SPRING, 1f, 3, 5), D(PLASTIC, 1f, 2, 4), D(SCREW, 0.8f, 2, 3), D(BATTERY, 1f, 2, 3));
            E(E_RCCAR, "RC Car", RcEnemyKind.RcCar, 1, 0, 270f, 26f, 7.5f, 80, 1f, D(BATTERY, 1f, 4, 6), D(CIRCUIT, 0.8f, 1, 2), D(PLASTIC, 1f, 2, 4));
            E(E_ROBOT, "Hall Robot", RcEnemyKind.Robot, 2, 0, 490f, 30f, 2.6f, 120, 1f, D(CIRCUIT, 1f, 2, 4), D(EYE, 1f, 1, 3), D(SCREW, 1f, 3, 5), D(VOICE_BOX, 0.5f, 1, 2));
            E(E_FUSED, "Fused Toy", RcEnemyKind.Fused, 3, 0, 920f, 45f, 3.6f, 150, 1f, D(SCREW, 1f, 4, 8), D(BATTERY, 1f, 3, 5), D(SPRING, 1f, 3, 6), D(GOLD_FUR, 0.2f, 1, 1));
            E(E_SHADOW, "Mama", RcEnemyKind.Shadow, 4, 0, 1f, 70f, 3.4f, 0, 1f, new RcDrop[0]); // she can't be hurt yet: you hide (an explicit empty array: no Array.Empty on old Mono)

            E(E_BOSS0 + 0, BossNames[0], RcEnemyKind.Boss, 0, 0, 900f, 22f, 3.6f, 0, 3f, D(THREAD, 1f, 1, 1), D(FUR, 1f, 6, 10), D(SPRING, 1f, 4, 8));
            E(E_BOSS0 + 1, BossNames[1], RcEnemyKind.Boss, 1, 2, 2400f, 34f, 3.4f, 0, 5f, D(DISPLAY_GLASS, 1f, 1, 1), D(BATTERY, 1f, 5, 9), D(CIRCUIT, 1f, 3, 6));
            E(E_BOSS0 + 2, BossNames[2], RcEnemyKind.Boss, 2, 0, 3400f, 46f, 3.2f, 0, 1f, D(MONITOR_LENS, 1f, 1, 1), D(CIRCUIT, 1f, 5, 9), D(EYE, 1f, 4, 8));
            E(E_BOSS0 + 3, BossNames[3], RcEnemyKind.Boss, 3, 0, 5400f, 60f, 3.8f, 0, 1f, D(MONOCLE, 1f, 1, 1), D(GOLD_FUR, 1f, 2, 3), D(SCREW, 1f, 8, 14));
            E(E_BOSS0 + 4, BossNames[4], RcEnemyKind.Boss, 4, 0, 8000f, 80f, 4.0f, 0, 1f, D(MAMAS_EYE, 1f, 1, 1), D(DIAMOND_EYE, 1f, 2, 3), D(VOICE_BOX, 1f, 6, 10));
        }

        private static RcQuest Q(RcQuestKind kind, int zone, int target, int count, int xp, string title, string text, string done, params RcCost[] reward)
        {
            var q = new RcQuest { World = _worldNow, Kind = kind, Zone = zone, Target = target, Count = count, Xp = xp, Title = title, Text = text, Done = done, Reward = reward };
            Quests.Add(q);
            return q;
        }

        // The main chain: four quests a zone, the last one its boss. Quest experience is most of what a zone
        // needs (design: a zone's quests get you most of the way; no grinding to move on).
        private static void BuildQuests()
        {
            Q(RcQuestKind.Talk, 0, 0, 1, 40, "The Treehouse", "Find the treehouse in the park, up Main Street, and talk to the kids.", "You made it. Most don't. Take this, you'll need it out there.", new RcCost(BAT, 1), new RcCost(PAN_LID, 1))
                .How(
                    "You start outside the Arcade, on Main Street. Walk north (the way you're facing when you arrive) along the lit road until you reach the park.",
                    "The big oak in the middle of the park is the treehouse. Nothing follows you into the park.",
                    "Go to the ladder on the trunk and press E to grab it. Hold W to climb; you step off on the first deck.",
                    "Walk up to the quest board (the plank sign beside the kids, in the far right corner of the deck) and press E.",
                    "Press HAND IT IN. They give you a Baseball Bat and a Pan Lid.",
                    "Press TAB to open your backpack, click the bat, press EQUIP. Do the same for the lid.");
            Q(RcQuestKind.Kill, 0, 0, 8, 110, "Strays", "The suburbs are full of strays. Break 8 toys there.", "That's eight fewer on Maple Street. They hear everything, so stay off the grass when you can.", new RcCost(SPRING, 4), new RcCost(PLASTIC, 4))
                .How(
                    "Grab the ladder, hold S to climb down, and leave the park by the south path, back down Main Street.",
                    "Maple Street is the side street on the right (the west side), a little before you get back to the Arcade. The gap in the plank fence is the way into the suburbs.",
                    "Break 8 toys. Any toy in the suburbs counts.",
                    "Left click swings your bat. Keep it held after a swing to wind up a heavy swing, then let go: it hits up to twice as hard.",
                    "Every toy winds up before it hits, and an orange bar fills over its head while it does. Roll out of the way with ALT (or C): you're untouchable for most of the roll.",
                    "Or hold right click to block with your weapon: a blue bubble of light comes up, and a hit from the front does a quarter of its damage. Raise it just before the hit lands (the bubble gleams brighter for that moment) for a perfect block: no damage at all, the toy is knocked off balance, and your next blow counters for more.",
                    "Strays come in packs of 2 to 4, and running (SHIFT) is loud. Walk up to one pack at a time.",
                    "When it says 8 / 8, go back to the treehouse and hand it in at the quest board.");
            Q(RcQuestKind.Craft, 0, SLINGSHOT, 1, 110, "Make Something", "Bring parts back to the bench and craft a Slingshot.", "Loud, isn't it? Everything in the street heard that.", new RcCost(SCREW, 4))
                .How(
                    "A Slingshot costs 4 Springs and 3 Plastic. The last job paid you 4 of each, so you should already have them.",
                    "If you're short: strays drop both, and the scrap piles in the suburbs (heaps of junk: press E to search one) give both. A searched pile has more after 3 minutes; it tells you how long.",
                    "In the treehouse, first deck: the crafting bench is on the left side. Press E at it. It uses the materials in your stash too.",
                    "Click Slingshot in the list.",
                    "Press TAB, click the Slingshot, press EQUIP. It goes in your second weapon slot: press 2 (or Q) to take it out.",
                    "To fire it: hold left click to draw it back, aim with the dot in the middle of the screen, let go. A full draw hits hardest; a quick tap does nothing.",
                    "Hand the job in at the quest board.");
            Q(RcQuestKind.Elite, 0, E_JACK, 1, 130, "The Garages", "Something in the garages on Oak Lane jumps out at people. Break it.", "A jack-in-the-box. Figures. Keep the spring.", new RcCost(SHOCK_BATTERY, 1))
                .How(
                    "Go into the suburbs and follow Maple Street west to the lane that runs south (left), about halfway along. It leads down to Oak Lane.",
                    "On Oak Lane turn east (left) and walk to its end. The row of four concrete garages is on the south side.",
                    "Two of the garages have a red box with a gold crank sitting in them. That is the Jack-in-the-Box. It pops when you come within about 3 meters.",
                    "It can't move. Every 2 seconds it bursts and hurts everything within 4 meters: a red ring shows where.",
                    "Easiest way: stand outside the garage, more than 5 meters off, and shoot it with the Slingshot. Or run in, swing twice, and back out before the burst.",
                    "Break one, then hand the job in at the treehouse. The reward is a Shock Battery (a gadget: equip it in a weapon slot, click to set it off).");
            Q(RcQuestKind.Boss, 0, 0, 1, 0, "Patches", "A huge thing made of other Tattletails sits in the cul-de-sac under the water tower. It has to go.", "You broke Patches. The mall's barricade is open to you now.", new RcCost(140, 1))
                .How(
                    "You need to be level 4 or close to it: Patches has 900 health and hits for 14. Make a Pan Lid and better gear at the bench first if you haven't.",
                    "Go down Oak Lane to its west end. The cul-de-sac under the water tower (the tall tank on legs you can see from anywhere in the suburbs) is where it sits.",
                    "Its health bar appears at the top of the screen when the fight starts. Leave the cul-de-sac and it gives up and heals.",
                    "When a red ring appears on the ground around it, get out of the ring: a second later it slams, and a slam can't be blocked.",
                    "At two thirds and one third health it calls 3 strays. Break them first, or block with right click while you deal with them.",
                    "When it breaks it drops Patches' Thread (for a bigger backpack, at the bench) and a piece of gear. The mall's barricade opens.",
                    "Hand the job in at the treehouse.");

            Q(RcQuestKind.Kill, 1, 0, 12, 250, "Shoplifters", "Clear out the mall. Break 12 toys there.", "The mall used to be where we got batteries. Now we can again.", new RcCost(BATTERY, 6))
                .How(
                    "The mall is east of the park. Leave the park by the east path and follow the road to its end: the barricade there opens once Patches is broken and you're level 4.",
                    "Break 12 toys anywhere in the mall: the parking lot, the shops, or upstairs.",
                    "The way into the building is the door under the pink sign, on the far side of the parking lot. Inside, two stopped escalators in the middle go up to the second floor.",
                    "Teal strays have 115 health and hit for 10. The shops are dark: press F if your flashlight is off, but toys see you from further when it's on.",
                    "Hand the job in at the treehouse. If you built the fort in the suburbs or here, its zipline gets you back fast.");
            Q(RcQuestKind.Bring, 1, BATTERY, 10, 270, "Charge", "Bring 10 batteries to the treehouse. The lanterns are running low.", "Good. The lights stay on another night.", new RcCost(121, 1))
                .How(
                    "Bring 10 Batteries to the treehouse. The last job paid you 6.",
                    "Every teal stray drops 1 to 3, an RC car 4 to 6, and every scrap pile in town has 2 or 3 (1 or 2 in the suburbs).",
                    "Batteries in your backpack are dropped if you fall. Batteries in your stash (second deck of the treehouse) are safe, and they count.",
                    "When backpack plus stash is 10, press E at the quest board and hand it in. The reward is a Repair Kit: a gadget that heals you.");
            Q(RcQuestKind.Elite, 1, E_RCCAR, 2, 290, "Hit and Run", "Two RC cars patrol the mall's parking lot. Break both.", "They won't be running anyone down now.", new RcCost(CIRCUIT, 4), new RcCost(143, 1))
                .How(
                    "Two RC cars circle the mall's parking lot: one near the way in, one at the south end.",
                    "They're fast (you can't outrun one) and they ram you, bounce off, and come round again. Each has 270 health.",
                    "Stand with a parked car or a lamp post behind you, hold right click to block the ram, then swing while it bounces away.",
                    "Or shoot them as they come in: they drive straight at you.",
                    "Break both, then hand the job in at the treehouse.");
            Q(RcQuestKind.Boss, 1, 0, 1, 0, "The Display Model", "The giant Tattletail from the toy store window woke up. Put it back to sleep.", "Three meters of fur and glass, and you broke it. The school is next.", new RcCost(146, 1))
                .How(
                    "You need to be about level 7. The Display Model has 2,400 health and hits for 22.",
                    "Go into the mall building and straight through the entrance hall to the middle. The toy store is the big shop in the far right (south-east) corner of the ground floor, with the red carpet.",
                    "Its slam reaches 6.5 meters: when the red ring appears, run straight out of it. A slam can't be blocked.",
                    "It's slow to turn. Stay behind it, swing, and back off when the ring shows.",
                    "It calls teal strays at two thirds and one third health. A Shock Battery stuns all of them at once.",
                    "It drops Display Glass and a piece of gear. The school's barricade opens. Hand the job in at the treehouse.");

            Q(RcQuestKind.Kill, 2, 0, 14, 400, "Detention", "The school is dark and they hide in it. Break 14 toys there.", "Nobody's hiding in the lockers tonight.", new RcCost(EYE, 5), new RcCost(CIRCUIT, 5))
                .How(
                    "The school is west of the park. Leave by the west path and follow the road to its end.",
                    "Break 14 toys in the school: the yard, the ground floor, the classrooms upstairs, or the boiler room.",
                    "The front door is across the yard. A hall runs from it to the back of the building, and a second hall crosses it halfway.",
                    "Stairs up: at the north end of the cross hall (turn right at the crossing). Stairs down to the boiler room: in the caretaker's room, through the door on the left just before those stairs.",
                    "The rooms have no light at all. Black strays hit for 16, white ones for 18.",
                    "Hand the job in at the treehouse.");
            Q(RcQuestKind.Bring, 2, VOICE_BOX, 6, 420, "Voices", "Bring 6 voice boxes. We want to know what they're saying to each other.", "They all say the same thing. 'Mama's coming.'", new RcCost(122, 1))
                .How(
                    "Bring 6 Voice Boxes to the treehouse.",
                    "In the school, black strays drop one about a third of the time, white strays a little more often, and a Hall Robot half the time.",
                    "The school's scrap piles sometimes have one. The boiler room has three piles and a pack of four white strays.",
                    "Like the batteries: what's in your stash counts, and is safe if you fall.",
                    "Hand them in at the quest board. The reward is a Decoy Toy.");
            Q(RcQuestKind.Elite, 2, E_ROBOT, 2, 460, "Hall Pass", "Two robots walk the school's halls. Break them, or learn their route and slip past. No, break them.", "That's the halls clear.", new RcCost(149, 1))
                .How(
                    "Two Hall Robots walk the school: one in the main hall (the one from the front door), one in the cross hall.",
                    "A robot stops when it sees you within 17 meters, aims for a moment, and fires a red shot. 490 health each.",
                    "Its shots can be blocked: hold right click while facing it. Or step behind a corner or a row of lockers: the shot stops at walls.",
                    "Close the distance between shots (one every 2 seconds) and swing. Up close it still shoots, point blank: keep blocking between swings.",
                    "A Decoy Toy makes it shoot the decoy instead of you for 8 seconds.",
                    "Break both, then hand the job in at the treehouse.");
            Q(RcQuestKind.Boss, 2, 0, 1, 0, "The Hall Monitor", "Something tall with a searchlight is in the gym.", "The junkyard's open. Papa's in there. Be careful.", new RcCost(141, 1))
                .How(
                    "You need to be about level 10. The Hall Monitor has 3,400 health and hits for 30.",
                    "From the school's front door, walk down the main hall past the crossing. The gym's wide doorway is on the left (south) side.",
                    "It carries a searchlight: you can see where it's looking.",
                    "From a distance it fires a spread of 4 shots. Block them facing it, or get behind the bleachers along the far wall.",
                    "Up close it slams (red ring, 4.6 meters): get out of the ring.",
                    "It calls black strays at two thirds and one third health, and gets faster below a third.",
                    "It drops the Monitor Lens and a piece of gear. The junkyard's barricade opens. Hand the job in at the treehouse.");

            Q(RcQuestKind.Kill, 3, 0, 16, 560, "Scrap", "The junkyard has the best scrap in town and the worst toys. Break 16.", "You're getting good at this.", new RcCost(SCREW, 10), new RcCost(BATTERY, 6))
                .How(
                    "The junkyard is north-east. Leave the park by the north path, go up Main Street a short way, and take the road on the right to its end.",
                    "Break 16 toys there. The yard is a maze of stacked cars: the gaps between stacks are staggered from row to row.",
                    "Scrapyard strays have 340 health and hit for 22, in packs of 3 and 4.",
                    "There are more scrap piles here than anywhere: screws, batteries, and now and then gold fur.",
                    "Hand the job in at the treehouse.");
            Q(RcQuestKind.Elite, 3, E_GOLD, 1, 600, "Gold", "A golden Tattletail was seen in the car stacks. Its fur is worth the trouble.", "Real gold fur. We can make something of this.", new RcCost(GOLD_FUR, 2))
                .How(
                    "One golden Tattletail lives in the junkyard's far corner: from the way in, all the way across the yard and to the right (the south-east corner).",
                    "It's bigger than a stray, has 950 health, and hits for 26. It's alone.",
                    "It always drops Gold Fur. If someone else broke it, it comes back after a couple of minutes.",
                    "Hand the job in at the treehouse.");
            Q(RcQuestKind.Elite, 3, E_FUSED, 2, 640, "Welded", "Toys welded together guard the way to the crusher. Break two.", "Whoever welded them is further in.", new RcCost(123, 1))
                .How(
                    "Two Fused Toys (a tin robot with Tattletails welded to it) stand in the junkyard: one in the middle of the yard, one at the north end near the crane.",
                    "920 health each, 45 damage, and a long reach. They wind up slowly: block or step back when one rears.",
                    "They don't shoot. Keep your distance with a ranged weapon, or block each hit and swing in between.",
                    "Break both, then hand the job in at the treehouse. The reward is a Lid Shield.");
            Q(RcQuestKind.Boss, 3, 0, 1, 0, "Papa", "Papa sits on a throne of scrap by the crusher. He's the last one before her.", "Papa's gone. The warehouse gate is open. She knows you're coming.", new RcCost(147, 1), new RcCost(150, 1))
                .How(
                    "You need to be about level 13. Papa has 5,400 health and hits for 39.",
                    "He's in the crusher pit: the open ground in the north-east of the junkyard, beside the crane.",
                    "He charges: he turns to face you for a moment, then runs in a straight line. Step sideways (or dodge with ALT) as he starts; blocking only takes the edge off.",
                    "His slam reaches 5.5 meters and can't be blocked. Watch for the red ring.",
                    "He calls scrapyard strays at two thirds and one third health. Use a Lid Shield or a Shock Battery when they arrive.",
                    "He drops his Monocle and a piece of gear. The warehouse gate opens. Hand the job in at the treehouse.");

            Q(RcQuestKind.Reach, 4, 0, 1, 500, "The Warehouse", "Go through the last gate, at the top of Main Street. If you hear her, hide.", "You got inside. None of us ever did.", new RcCost(VOICE_BOX, 6))
                .How(
                    "The Waygetter warehouse is at the top of Main Street: leave the park by the north path and keep going north to the last barricade.",
                    "Past the barricade is the Facility's parking lot. Cross it to the building: the way in is the broken front door, toward the left end of the front wall.",
                    "The power has been out for years. Press F if your flashlight is off.",
                    "Mama walks the building. She can't be hurt out here. If you see her, break her line of sight and keep moving: she gives up after a few seconds without seeing you.",
                    "Getting inside is the whole job. Hand it in at the treehouse.");
            Q(RcQuestKind.Kill, 4, 0, 14, 600, "The Recall Line", "Break 14 of the toys that guard the warehouse.", "That's the way to her chamber cleared.", new RcCost(124, 1))
                .How(
                    "Break 14 toys in the Facility. Warehouse strays have 480 health and hit for 29.",
                    "They're in the parking lot, the lobby (straight through the front door), the break room behind it, the assembly floor (through the door in the lobby's right-hand wall), the offices upstairs and the basement.",
                    "Stairs up and down are in the stairwell off the hall behind the lobby, marked STAIRS.",
                    "Fight near a doorway you can back through: if Mama turns up mid-fight, leave.",
                    "Hand the job in at the treehouse. The reward is an Overcharger.");
            Q(RcQuestKind.Elite, 4, E_DIAMOND, 1, 600, "Diamond", "A diamond Tattletail is in there. Its eye will make the best gear we've ever had.", "It's beautiful. It's also still looking at me.", new RcCost(DIAMOND_EYE, 2), new RcCost(142, 1))
                .How(
                    "One diamond Tattletail is in the R&D lab, in the Facility's basement.",
                    "The way down is not the main stairwell. From the assembly floor, go through the doorway in its south wall on the right-hand (east) side, then the heavy door with the red keypad light. The stairs are behind it.",
                    "A Fused Toy stands in front of that door.",
                    "1,450 health, 52 damage. It always drops a Diamond Eye.",
                    "Hand the job in at the treehouse.");
            Q(RcQuestKind.Boss, 4, 0, 1, 0, "Mama", "She's in her chamber at the far end of the warehouse. End it.", "She's gone. The whole building went quiet.", new RcCost(154, 1))
                .How(
                    "Be level 15 with the best gear the bench will make. Mama has 8,000 health and hits for 52.",
                    "She's in the warehouse hall: the tall room full of racks at the east end of the building, through the big roll-up doorway at the far side of the assembly floor.",
                    "Once you're in there she stops hunting and fights. She fires 7 shots in a fan, charges in a straight line, and slams (5.5 meters).",
                    "Block the shots, sidestep the charge, run from the ring.",
                    "She calls warehouse strays twice. Save a gadget for each time.",
                    "When she breaks, hand the job in at the treehouse.");

            // The end of the first world: the Tape, the call from Egypt, and the train.
            Q(RcQuestKind.Bring, 4, TAPE, 1, 300, "Take the Tape", "Take the Tape out of Mama's back and bring it to the treehouse. It's what keeps them going.", "Is it still playing?", new RcCost(BATTERY, 6)).Keeping()
                .How(
                    "Mama's Tape is in her back, in a little deck. When she breaks, it comes out and goes in your backpack.",
                    "A small light on it blinks once. Remember that.",
                    "If you've already beaten her, or you lose it, the quest board gives you another.",
                    "Go to the treehouse and hand the job in. You keep the Tape: Moss wants it next.");
            Q(RcQuestKind.Bring, 4, TAPE, 1, 0, "The Call", "Bring the Tape to Moss. He's got a cassette player. We want to hear what's on it.", "Somebody was calling. Somebody's in trouble.", new RcCost[0])
                .How(
                    "You're carrying the Tape from the last job. Go to the quest board in the treehouse (first deck).",
                    "Press HAND IT IN: Moss takes it to his cassette player and plays it low, so nothing outside hears it.",
                    "Listen to the whole tape. Click or press SPACE to move through what they say.");
            Q(RcQuestKind.Scene, 4, 0, 1, 0, "What Do We Do?", "Talk to the kids. Nobody can agree. Moss noticed something.", "Okay. So we're doing this.", new RcCost[0])
                .How(
                    "Open the quest board and listen: the kids are talking over each other.",
                    "When they stop, you get one thing to say. Press it.",
                    "Hear them out, then hand the job in.");
            Q(RcQuestKind.Station, 4, 0, 1, 0, "The Recall Express", "Gather your team and board the Recall Express, at the junkyard station. Find Pip.", "...", new RcCost[0])
                .How(
                    "The station is east of the park, on the railway just south of the junkyard road, before you reach the junkyard fence. A freight train is waiting there.",
                    "Gather your team first: the train fits four, and the kids say to bring everyone you trust.",
                    "Press E at the departure board and press BOARD THE TRAIN.");
        }

        /// <summary>A zone's first job for the kids (its first "break N toys" quest): finishing it is what gets a fort built there.</summary>
        public static int FirstJob(int zone)
        {
            for (int i = 0; i < Quests.Count; i++) if (Quests[i].World == RcWorlds.Now && Quests[i].Zone == zone && Quests[i].Kind == RcQuestKind.Kill) return i;
            return 0;
        }

        /// <summary>What a searched scrap pile gives: mostly its zone's two common materials, sometimes its rarer one, sometimes something from an earlier zone (rolls 0..1).</summary>
        public static int ScrapLoot(int zone, float roll, float roll2)
        {
            if (zone > 0 && roll2 < 0.25f) zone = (int)(roll2 * 4f * zone); // an older zone's scrap
            bool rare = ZoneMatC[zone] == GOLD_FUR || ZoneMatC[zone] == DIAMOND_EYE;
            if (roll < (rare ? 0.04f : 0.15f)) return ZoneMatC[zone];
            return roll < 0.6f ? ZoneMatA[zone] : ZoneMatB[zone];
        }

        /// <summary>
        /// Batteries in a searched scrap pile, on top of everything else it gives (roll 0..1). Every weapon tier,
        /// every gadget and a quest want them, so every pile in town has some: 1 or 2 in the suburbs, 2 or 3 beyond.
        /// </summary>
        public static int ScrapBatteries(int zone, float roll) { return RcWorlds.Egypt ? 0 : (zone == 0 ? 1 : 2) + (roll < 0.5f ? 1 : 0); }

        /// <summary>The gear a boss can drop: its zone's tier (one item, picked by roll 0..1).</summary>
        public static int BossGear(int zone, float roll)
        {
            int[] pool = RcWorlds.Egypt ? new[] { 200 + zone, 210 + zone, 220 + zone, 230 + zone } : new[] { 100 + zone, 110 + zone, 120 + zone, 130 + zone };
            int i = (int)(roll * pool.Length);
            return pool[i < 0 ? 0 : i >= pool.Length ? pool.Length - 1 : i];
        }
    }
}
