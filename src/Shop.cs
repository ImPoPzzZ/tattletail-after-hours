using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KaleidoscopeEndless
{
    public enum ShopCategory { PowerUp, Passive, Color, Beam, Extra, Skin }

    public class ShopItem
    {
        public string Key;          // exact left-hand side written to Shop.txt, e.g. "Power Up Bigger Pockets"
        public string Name;
        public ShopCategory Category;
        public int Price;
        public string Description;
        public bool Consumable;     // true = single run, reverts to 0 once used; false = permanent
        public string Requires;     // Key of an item that must be owned first (Walking Speed II needs I)
        public bool Party;          // Papa's 10th Birthday! shop item — bought with Party Tokens instead of shards
        public bool Versus;         // VERSUS SHOP item — bought with Versus Tokens
    }

    /// <summary>
    /// Everything the player owns, in one plain-text file (Shop.txt, in
    /// Documents\Kaleidoscope Endless\ — see SaveFiles). One "Key = value" line per item:
    ///   Shards = 340
    ///   Power Up Bigger Pockets = 1     (bought; back to 0 once the run uses it)
    ///   Color Gold = 1                  (cosmetics are permanent)
    /// </summary>
    public static class Shop
    {
        private const string FILE_NAME = "Shop.txt";
        private static string FilePath => SaveFiles.PathFor(FILE_NAME);

        public const string SHARDS_KEY = "Shards";
        public const string PARTY_TOKENS_KEY = "Party Tokens";
        public const string VERSUS_TOKENS_KEY = "Versus Tokens";

        /// <summary>The balance key an item is paid for with.</summary>
        public static string CurrencyKey(ShopItem item) { return SHARDS_KEY; }   // one currency: shards, in every shop
        public static string CurrencyName(ShopItem item) { return "shards"; }

        // Priced against earnings of 10 shards per objective + 10 per active
        // run modifier per objective (a 3-modifier run averages ~200 shards
        // per 10 minutes): power ups are under a run's pay, basic colors
        // ~1 run, Gold ~3 runs, Diamond ~6.
        private static readonly ShopItem[] BaseItems =
        {
            new ShopItem { Key = "Power Up Head Start Battery", Name = "Head Start Battery", Category = ShopCategory.PowerUp, Price = 50,  Consumable = true, Description = "Next run: start with a 30 second Strong Battery." },
            new ShopItem { Key = "Power Up Sharper Eyes",       Name = "Sharper Eyes",       Category = ShopCategory.PowerUp, Price = 60,  Consumable = true, Description = "Next run: Power Up coins reveal what they are from farther away." },
            new ShopItem { Key = "Power Up Coin Magnet",        Name = "Coin Magnet",        Category = ShopCategory.PowerUp, Price = 80,  Consumable = true, Description = "Next run: Power Up coins appear twice as often (needs the Power Ups modifier)." },
            new ShopItem { Key = "Power Up Bigger Pockets",     Name = "Bigger Pockets",     Category = ShopCategory.PowerUp, Price = 100, Consumable = true, Description = "Next run: hold up to 4 Totems of Undying instead of 3." },
            new ShopItem { Key = "Power Up Steady Hands",       Name = "Steady Hands",       Category = ShopCategory.PowerUp, Price = 120, Consumable = true, Description = "Next run: Tattletail's needs drain 20% slower." },
            new ShopItem { Key = "Power Up Starting Totem",     Name = "Starting Totem",     Category = ShopCategory.PowerUp, Price = 150, Consumable = true, Description = "Next run: start holding a Totem of Undying." },
            new ShopItem { Key = "Power Up Diamond Flashlight", Name = "Diamond Flashlight", Category = ShopCategory.PowerUp, Price = 250, Consumable = true, Description = "Next run: a diamond flashlight that charges in 2 shakes and holds a much longer charge." },

            new ShopItem { Key = "Passive Walking Speed I",     Name = "Walking Speed I",     Category = ShopCategory.Passive, Price = 250, Description = "Permanent: walk and run 5% faster." },
            new ShopItem { Key = "Passive Walking Speed II",    Name = "Walking Speed II",    Category = ShopCategory.Passive, Price = 500, Requires = "Passive Walking Speed I", Description = "Permanent: walk and run 10% faster in total." },
            new ShopItem { Key = "Passive Flashlight Charge",   Name = "Flashlight Charge",   Category = ShopCategory.Passive, Price = 300, Description = "Permanent: each flashlight shake charges 10% more." },
            new ShopItem { Key = "Passive Golden Flashlight", Name = "Golden Flashlight", Category = ShopCategory.Passive, Price = 800, Description = "Permanent, Endless only: the game's own golden flashlight — its golden glow and longer charge in every run." },
            new ShopItem { Key = "Passive Shard Boost",         Name = "Shard Boost",         Category = ShopCategory.Passive, Price = 400, Description = "Permanent: earn 10% more shards from objectives during a run." },

            new ShopItem { Key = "Color Yellow",  Name = "Yellow",  Category = ShopCategory.Color, Price = 150,  Description = "Tattletail color." },
            new ShopItem { Key = "Color Teal",    Name = "Teal",    Category = ShopCategory.Color, Price = 150,  Description = "Tattletail color." },
            new ShopItem { Key = "Color Black",   Name = "Black",   Category = ShopCategory.Color, Price = 250,  Description = "Tattletail color." },
            new ShopItem { Key = "Color White",   Name = "White",   Category = ShopCategory.Color, Price = 250,  Description = "Tattletail color." },
            new ShopItem { Key = "Color Gold",    Name = "Gold",    Category = ShopCategory.Color, Price = 600,  Description = "Tattletail color." },
            new ShopItem { Key = "Color Diamond", Name = "Diamond", Category = ShopCategory.Color, Price = 1200, Description = "Tattletail color." },

            new ShopItem { Key = "Beam Red",    Name = "Red",    Category = ShopCategory.Beam, Price = 120, Description = "Flashlight beam color." },
            new ShopItem { Key = "Beam Green",  Name = "Green",  Category = ShopCategory.Beam, Price = 120, Description = "Flashlight beam color." },
            new ShopItem { Key = "Beam Blue",   Name = "Blue",   Category = ShopCategory.Beam, Price = 120, Description = "Flashlight beam color." },
            new ShopItem { Key = "Beam Purple", Name = "Purple", Category = ShopCategory.Beam, Price = 180, Description = "Flashlight beam color." },
            new ShopItem { Key = "Beam Orange", Name = "Orange", Category = ShopCategory.Beam, Price = 180, Description = "Flashlight beam color." },
            new ShopItem { Key = "Beam Pink",   Name = "Pink",   Category = ShopCategory.Beam, Price = 180, Description = "Flashlight beam color." },

            new ShopItem { Key = "Extra Kaleidoscope Voice", Name = "Kaleidoscope Voice", Category = ShopCategory.Extra, Price = 300, Description = "Tattletail speaks with the Kaleidoscope voice set." },
            new ShopItem { Key = "Extra Kaleidoscope Mama",  Name = "Kaleidoscope Mama",  Category = ShopCategory.Extra, Price = 400, Description = "Mama appears as her Kaleidoscope self." },
        };

        // Papa's 10th Birthday! has its own shop: the same cosmetics (Tattletail
        // colors, beam colors, voice/Mama skins — no power ups or passives),
        // owned separately ("Party <key> = 1" in Shop.txt) and paid in Party Tokens.
        public static readonly ShopItem[] Items = BuildItems();

        private static ShopItem[] BuildItems()
        {
            var all = new List<ShopItem>(BaseItems);
            foreach (var item in BaseItems)
            {
                if (item.Category != ShopCategory.Color && item.Category != ShopCategory.Beam && item.Category != ShopCategory.Extra) continue;
                all.Add(new ShopItem
                {
                    Key = "Party " + item.Key, Name = item.Name, Category = item.Category, Price = item.Price,
                    Description = item.Description, Consumable = false, Requires = null, Party = true,
                });
            }

            // The VERSUS SHOP: the same Tattletail colors, beams and voice, owned separately
            // ("Versus <key>" in Shop.txt) and paid in Versus Tokens, plus Mama's and Papa's skins.
            foreach (var item in BaseItems)
            {
                if (item.Category != ShopCategory.Color && item.Category != ShopCategory.Beam && item.Key != "Extra Kaleidoscope Voice") continue;
                all.Add(new ShopItem
                {
                    Key = "Versus " + item.Key, Name = item.Name, Category = item.Category, Price = item.Price,
                    Description = item.Description, Consumable = false, Requires = null, Versus = true,
                });
            }
            for (int papa = 0; papa < 2; papa++)
            {
                var skins = VersusHunters.Skins(papa == 1);
                for (int i = 1; i < skins.Length; i++) // skin 0 is the free classic look
                    all.Add(new ShopItem
                    {
                        Key = VersusHunters.Key(papa == 1, i), Name = skins[i].Name + (papa == 1 ? " Papa" : " Mama"),
                        Category = ShopCategory.Skin, Price = skins[i].Price, Description = skins[i].Description, Versus = true,
                    });
            }
            return all.ToArray();
        }

        private static readonly Dictionary<string, int> _values = new Dictionary<string, int>();
        private static bool _loaded;

        /// <summary>Loads (or creates) Shop.txt — called once at plugin startup.</summary>
        public static void Init() { EnsureLoaded(); }

        public static ShopItem Find(string key)
        {
            foreach (var item in Items) if (item.Key == key) return item;
            return null;
        }

        public static int Get(string key)
        {
            EnsureLoaded();
            int v;
            return _values.TryGetValue(key, out v) ? v : 0;
        }

        public static bool IsOwned(string key) { return Get(key) > 0; }

        public static bool RequirementMet(ShopItem item) { return string.IsNullOrEmpty(item.Requires) || IsOwned(item.Requires); }

        private static void Set(string key, int value)
        {
            EnsureLoaded();
            _values[key] = value;
            Save();
        }

        public static bool TryBuy(ShopItem item)
        {
            EnsureLoaded();
            if (item == null || IsOwned(item.Key)) return false;
            if (!RequirementMet(item)) return false;
            string currency = CurrencyKey(item);
            if (Get(currency) < item.Price) return false;
            _values[currency] = Get(currency) - item.Price;
            Daily.Add("spent", item.Price);
            _values[item.Key] = 1;
            Save();
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Shop] Bought '" + item.Key + "' for " + item.Price + ".");
            Scrapbook.OnColorPurchased(item.Key);
            return true;
        }

        /// <summary>Single-run Power Ups only: reads whether it's owned and, if so, puts it back to 0 (used up).</summary>
        public static bool Consume(string key)
        {
            EnsureLoaded();
            if (!IsOwned(key)) return false;
            Set(key, 0);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Shop] '" + key + "' used up for this run (back to 0).");
            return true;
        }

        public static void AddPartyTokens(int amount)
        {
            if (amount <= 0) return;
            AddShards(amount);   // (there are no Party Tokens any more: everything pays shards)
        }

        public static void AddVersusTokens(int amount)
        {
            if (amount <= 0) return;
            AddShards(amount);   // (there are no Versus Tokens any more: everything pays shards)
        }

        /// <summary>Any other saved number (Summer Vacation's day, cash and upgrades: keys starting with "Beach ").</summary>
        public static void SetValue(string key, int value) { Set(key, value); }

        public static void AddShards(int amount)
        {
            if (amount <= 0) return;
            Set(SHARDS_KEY, Get(SHARDS_KEY) + amount);
            if (!Daily.Paying) Daily.Add("shards_today", amount); // (the daily rewards themselves do not count)
        }

        /// <summary>Marks a non-shop unlock (e.g. a gumball gadget) as owned and saves.</summary>
        public static void Unlock(string key) { Set(key, 1); }

        /// <summary>Used by Scrapbook's Loot Box — caller has already checked the balance.</summary>
        public static void SpendShards(int amount)
        {
            if (amount <= 0) return;
            Set(SHARDS_KEY, Mathf.Max(0, Get(SHARDS_KEY) - amount));
            Daily.Add("spent", amount);
        }

        /// <summary>Party Tokens and Versus Tokens are gone: whatever a player still holds of them becomes shards, once.</summary>
        private static void MergeTokensIntoShards()
        {
            int old = Get(PARTY_TOKENS_KEY) + Get(VERSUS_TOKENS_KEY);
            if (old <= 0) return;
            _values[SHARDS_KEY] = Get(SHARDS_KEY) + old;
            _values[PARTY_TOKENS_KEY] = 0;
            _values[VERSUS_TOKENS_KEY] = 0;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Shop] " + old + " old tokens became shards.");
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                // Main file first; if it's missing/empty/unreadable (crash
                // mid-write, hand edit gone wrong) fall back to the last
                // good backup instead of silently starting from zero.
                bool ok = File.Exists(FilePath) && ReadInto(FilePath);
                if (!ok && File.Exists(BackupPath))
                {
                    KaleidoscopeEndlessPlugin.Log.LogWarning("KE: Shop.txt missing or unreadable — restoring from Shop.txt.bak.");
                    ReadInto(BackupPath);
                }

                MigrateOldFiles();
                MergeTokensIntoShards();
                Save();
            }
            catch (Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: Shop read failed: " + e.Message);
            }
        }

        // One-time cleanup: the time-based Gold/Diamond unlocks are gone
        // (those colors are shop items now), and shards used to live in
        // their own Shards.txt before moving into this file.
        private static void MigrateOldFiles()
        {
            string root = BepInEx.Paths.GameRootPath;
            string oldShards = Path.Combine(root, "Shards.txt");
            if (File.Exists(oldShards))
            {
                int old;
                if (int.TryParse(File.ReadAllText(oldShards).Trim(), out old) && old > 0)
                    _values[SHARDS_KEY] = Get(SHARDS_KEY) + old;
                File.Delete(oldShards);
            }
            foreach (string stale in new[] { "Gold.txt", "Diamond.txt" })
            {
                string p = Path.Combine(root, stale);
                if (File.Exists(p)) File.Delete(p);
            }
        }

        private static string BackupPath { get { return FilePath + ".bak"; } }
        private const int FILE_VERSION = 1;

        /// <summary>Parses "Key = value" lines from a file into _values. Returns true if at least one real key was found.</summary>
        private static bool ReadInto(string path)
        {
            bool any = false;
            try
            {
                foreach (string line in File.ReadAllLines(path))
                {
                    int eq = line.LastIndexOf('=');
                    if (eq < 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    int val;
                    if (!int.TryParse(line.Substring(eq + 1).Trim(), out val)) continue;
                    if (key == "Version") continue;
                    if (val < 0) val = 0;
                    _values[key] = val;
                    if (key == SHARDS_KEY || key == PARTY_TOKENS_KEY || Find(key) != null || key.StartsWith("Gadget ")) any = true;
                }
            }
            catch (Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: Shop read of '" + path + "' failed: " + e.Message);
                return false;
            }
            return any;
        }

        private static void Save()
        {
            try
            {
                var lines = new List<string> { "Version = " + FILE_VERSION, SHARDS_KEY + " = " + Get(SHARDS_KEY), PARTY_TOKENS_KEY + " = " + Get(PARTY_TOKENS_KEY) };
                foreach (var item in Items)
                    lines.Add(item.Key + " = " + (IsOwnedRaw(item.Key) ? 1 : 0));
                foreach (var g in Gadgets.All) // gadgets come from the hub's Gumball Machine, not the shop list
                    lines.Add(Gadgets.OwnKey(g) + " = " + (IsOwnedRaw(Gadgets.OwnKey(g)) ? 1 : 0));
                lines.Add(VERSUS_TOKENS_KEY + " = " + Get(VERSUS_TOKENS_KEY)); // was never written: tokens reset every restart
                foreach (var kv in _values)
                    if (kv.Key.StartsWith("Beach ") || kv.Key.StartsWith("Daily ")) lines.Add(kv.Key + " = " + kv.Value);

                // Write to a temp file, then swap it in (keeping the old
                // file as Shop.txt.bak) so an interrupted write can never
                // leave a half-written Shop.txt.
                string tmp = FilePath + ".tmp";
                File.WriteAllLines(tmp, lines.ToArray());
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, BackupPath);
                else File.Move(tmp, FilePath);
            }
            catch (Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: Shop write failed: " + e.Message);
            }
        }

        private static bool IsOwnedRaw(string key)
        {
            int v;
            return _values.TryGetValue(key, out v) && v > 0;
        }
    }

    /// <summary>Permanent one-time-buy passives from the Shop (all owned ones apply).</summary>
    public static class Passives
    {
        public static float WalkSpeedMult
        {
            get { return Shop.IsOwned("Passive Walking Speed II") ? 1.10f : (Shop.IsOwned("Passive Walking Speed I") ? 1.05f : 1f); }
        }
        public static float FlashlightChargeMult { get { return Shop.IsOwned("Passive Flashlight Charge") ? 1.10f : 1f; } }
        public static float ShardMult { get { return Shop.IsOwned("Passive Shard Boost") ? 1.10f : 1f; } }

        private static readonly System.Reflection.FieldInfo WalkSpeedField =
            typeof(FirstPersonController).GetField("m_WalkSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        private static readonly System.Reflection.FieldInfo RunSpeedField =
            typeof(FirstPersonController).GetField("m_RunSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        /// <summary>Called once per run from SceneBootstrap.StartEndless(): walking speed and flashlight charge per shake.</summary>
        public static void ApplyToRun()
        {
            float walk = WalkSpeedMult;
            if (walk > 1f && RM.fpsController != null && (object)WalkSpeedField != null && (object)RunSpeedField != null)
            {
                WalkSpeedField.SetValue(RM.fpsController, (float)WalkSpeedField.GetValue(RM.fpsController) * walk);
                RunSpeedField.SetValue(RM.fpsController, (float)RunSpeedField.GetValue(RM.fpsController) * walk);
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Passive] Walking speed x" + walk);
            }

            float charge = FlashlightChargeMult;
            if (charge > 1f && RM.flashlight != null)
            {
                RM.flashlight.chargePerShake *= charge;
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Passive] Flashlight charge per shake x" + charge);
            }
        }
    }

    /// <summary>Which single-run Power Ups the current run got. Begin() reads what's owned and consumes it (Shop.txt goes back to 0) the moment a run is launched.</summary>
    public static class RunPowerUps
    {
        public static bool StartingTotem, HeadStartBattery, CoinMagnet, BiggerPockets, SharperEyes, SteadyHands, DiamondFlashlight;

        /// <summary>Forgets whatever the previous run got — used by modes that shouldn't have power ups (Papa's 10th Birthday!).</summary>
        public static void Clear()
        {
            StartingTotem = HeadStartBattery = CoinMagnet = BiggerPockets = SharperEyes = SteadyHands = DiamondFlashlight = false;
        }

        public static void Begin()
        {
            StartingTotem     = Shop.Consume("Power Up Starting Totem");
            HeadStartBattery  = Shop.Consume("Power Up Head Start Battery");
            CoinMagnet        = Shop.Consume("Power Up Coin Magnet");
            BiggerPockets     = Shop.Consume("Power Up Bigger Pockets");
            SharperEyes       = Shop.Consume("Power Up Sharper Eyes");
            SteadyHands       = Shop.Consume("Power Up Steady Hands");
            DiamondFlashlight = Shop.Consume("Power Up Diamond Flashlight");
        }

        /// <summary>Called from EndlessRunManager.StartRun() right after PowerUpObjective.CleanUp() — grants the start-of-run effects.</summary>
        public static void ApplyToRun()
        {
            var pu = PowerUpObjective.Instance;
            if (pu != null)
            {
                if (StartingTotem) pu.GrantStartingTotem();
                if (HeadStartBattery) pu.GrantStrongBattery();
            }
            if (SteadyHands) RunModifiers.ApplySteadyHands();
        }
    }
}
