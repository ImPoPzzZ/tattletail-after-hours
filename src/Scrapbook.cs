using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KaleidoscopeEndless
{
    public enum StickerRarity { Common, Rare, Legendary }

    public class StickerEntry
    {
        public string Key;         // sprite file name in Stickers/ (no ".png")
        public string Name;        // display name
        public string ColorShopKey; // if set, auto-unlocked the moment this Shop color is bought (never a lootbox drop)
        public StickerRarity Rarity = StickerRarity.Common;

        public static readonly Color CommonColor = new Color(0.75f, 0.75f, 0.75f);
        public static readonly Color RareColor = new Color(0.4f, 0.65f, 1f);
        public static readonly Color LegendaryColor = new Color(1f, 0.75f, 0.2f);

        public Color RarityColor
        {
            get
            {
                switch (Rarity)
                {
                    case StickerRarity.Rare: return RareColor;
                    case StickerRarity.Legendary: return LegendaryColor;
                    default: return CommonColor;
                }
            }
        }
    }

    /// <summary>
    /// "Mama's Scrapbook" — a purely cosmetic sticker collection, separate
    /// from both the shard Shop and Party Shop. Six stickers match a real
    /// Tattletail color and unlock automatically the moment you buy that
    /// color (see Shop.TryBuy -> OnColorPurchased); "Original" starts owned
    /// since it's free from the very beginning. Every other sticker only
    /// comes from the Loot Box (1000 shards, random undiscovered sticker
    /// from that pool) — pure flavor, no gameplay effect either way.
    /// Persisted the same way Shop.txt is (plain "Key = 1" lines, in the
    /// Documents save folder — see SaveFiles).
    /// </summary>
    public static class Scrapbook
    {
        private const string FILE_NAME = "Scrapbook.txt";
        private static string FilePath => SaveFiles.PathFor(FILE_NAME);
        public const int LOOTBOX_PRICE = 1000;

        public static readonly StickerEntry[] Entries =
        {
            // Colors — matched to a real Shop color, auto-unlocked on purchase.
            new StickerEntry { Key = "sb_color_original", Name = "Original",     ColorShopKey = null },
            new StickerEntry { Key = "sb_color_white",    Name = "White",        ColorShopKey = "Color White" },
            new StickerEntry { Key = "sb_color_black",    Name = "Black",        ColorShopKey = "Color Black" },
            new StickerEntry { Key = "sb_color_teal",     Name = "Teal",         ColorShopKey = "Color Teal" },
            new StickerEntry { Key = "sb_color_gold",     Name = "Gold",         ColorShopKey = "Color Gold" },
            new StickerEntry { Key = "sb_color_diamond",  Name = "Diamond",      ColorShopKey = "Color Diamond" },

            // Everything below is Loot Box only. Rarity sets both the pull
            // odds (see RollRarity()) and the color ring shown in the UI.
            new StickerEntry { Key = "sb_color_pink",         Name = "Pink Tattletail",     Rarity = StickerRarity.Rare },
            new StickerEntry { Key = "sb_color_purple",       Name = "Purple Tattletail",   Rarity = StickerRarity.Rare },
            new StickerEntry { Key = "sb_color_orange_a",     Name = "Orange Tattletail",   Rarity = StickerRarity.Rare },
            new StickerEntry { Key = "sb_color_orange_b",     Name = "Orange Tattletail II", Rarity = StickerRarity.Rare },

            new StickerEntry { Key = "sb_mood_neutral",       Name = "Just Vibing" },
            new StickerEntry { Key = "sb_mood_sleepy",        Name = "Sleepy" },
            new StickerEntry { Key = "sb_mood_angry",         Name = "Angry" },
            new StickerEntry { Key = "sb_mood_happy",         Name = "Happy" },
            new StickerEntry { Key = "sb_mood_flashlight",    Name = "Flashlight Buddy" },
            new StickerEntry { Key = "sb_mood_battery",       Name = "Charged Up" },
            new StickerEntry { Key = "sb_mood_partyhat",      Name = "Party Time" },
            new StickerEntry { Key = "sb_mood_bowtie_gift",   Name = "Gift For You" },

            new StickerEntry { Key = "sb_papa_tophat",        Name = "Papa",                    Rarity = StickerRarity.Rare },
            new StickerEntry { Key = "sb_papa_cake_eating",   Name = "Birthday Papa",           Rarity = StickerRarity.Rare },
            new StickerEntry { Key = "sb_papa_cake10",        Name = "The Big 10",              Rarity = StickerRarity.Rare },
            new StickerEntry { Key = "sb_papa_balloons_gift", Name = "Papa's Presents",         Rarity = StickerRarity.Rare },
            new StickerEntry { Key = "sb_papa_bowtie_gift",   Name = "Papa's Gift",             Rarity = StickerRarity.Rare },
            new StickerEntry { Key = "sb_papa_banner",        Name = "Happy 10th Birthday Papa!", Rarity = StickerRarity.Rare },
            new StickerEntry { Key = "sb_papa_table",         Name = "The Party Table",         Rarity = StickerRarity.Rare },
            new StickerEntry { Key = "sb_papa_bowl",          Name = "Papa's Dinner",           Rarity = StickerRarity.Rare },
            new StickerEntry { Key = "sb_papa_dancing",       Name = "Dancing Papa",            Rarity = StickerRarity.Rare },

            new StickerEntry { Key = "sb_prop_batteries",     Name = "Spare Batteries" },
            new StickerEntry { Key = "sb_prop_partyhat",      Name = "Party Hat" },

            new StickerEntry { Key = "sb_horror_mama_silhouette", Name = "???",                  Rarity = StickerRarity.Legendary },
            new StickerEntry { Key = "sb_horror_red_eyes",        Name = "Watching",             Rarity = StickerRarity.Legendary },
            new StickerEntry { Key = "sb_horror_blue_ghost",      Name = "Cold Spot",            Rarity = StickerRarity.Legendary },
            new StickerEntry { Key = "sb_horror_door_eyes",       Name = "Behind the Door",      Rarity = StickerRarity.Legendary },
            new StickerEntry { Key = "sb_horror_run",             Name = "RUN",                  Rarity = StickerRarity.Legendary },
            new StickerEntry { Key = "sb_horror_hallway",         Name = "Down the Hall",        Rarity = StickerRarity.Legendary },
            new StickerEntry { Key = "sb_horror_kaleidoscope_a",  Name = "Kaleidoscope Eyes",    Rarity = StickerRarity.Legendary },
            new StickerEntry { Key = "sb_horror_kaleidoscope_b",  Name = "Kaleidoscope Eyes II", Rarity = StickerRarity.Legendary },
            new StickerEntry { Key = "sb_horror_plush",           Name = "Old Toy",              Rarity = StickerRarity.Legendary },

            new StickerEntry { Key = "sb_pose_sunglasses",      Name = "Cool Tattletail" },
            new StickerEntry { Key = "sb_pose_gift_wrapped",    Name = "Unwrap Me" },
            new StickerEntry { Key = "sb_pose_sparkly",         Name = "Sparkly" },
            new StickerEntry { Key = "sb_pose_sleepy_holding",  Name = "Bedtime" },
            new StickerEntry { Key = "sb_pose_vase",            Name = "Careful With That" },
            new StickerEntry { Key = "sb_pose_flashlight",      Name = "On the Case" },
        };

        private static readonly Dictionary<string, bool> _owned = new Dictionary<string, bool>();
        private static bool _loaded;

        public static bool IsOwned(string key)
        {
            EnsureLoaded();
            bool v;
            return _owned.TryGetValue(key, out v) && v;
        }

        public static int OwnedCount()
        {
            EnsureLoaded();
            int n = 0;
            foreach (var e in Entries) if (IsOwned(e.Key)) n++;
            return n;
        }

        /// <summary>Called from Shop.TryBuy() right after a color purchase succeeds.</summary>
        public static void OnColorPurchased(string shopColorKey)
        {
            EnsureLoaded();
            foreach (var e in Entries)
            {
                if (e.ColorShopKey == shopColorKey && !IsOwned(e.Key))
                {
                    _owned[e.Key] = true;
                    Save();
                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Scrapbook] Unlocked '" + e.Key + "' (matches " + shopColorKey + ").");
                }
            }
        }

        // Common/Rare/Legendary odds — rolled first, then a random undiscovered
        // sticker of that tier is picked. Falls back to whatever tiers still
        // have undiscovered stickers if the rolled tier is already fully collected.
        private const float LEGENDARY_CHANCE = 0.08f;
        private const float RARE_CHANCE = 0.27f;    // Common makes up the remaining 65%

        private static StickerRarity RollRarity()
        {
            float roll = UnityEngine.Random.value;
            if (roll < LEGENDARY_CHANCE) return StickerRarity.Legendary;
            return roll < LEGENDARY_CHANCE + RARE_CHANCE ? StickerRarity.Rare : StickerRarity.Common;
        }

        /// <summary>1000 shards for a random undiscovered non-color sticker, weighted by rarity. Returns the entry won, or null (not enough shards, or every eligible sticker is already owned — shards aren't spent in that case).</summary>
        public static StickerEntry TryOpenLootBox()
        {
            EnsureLoaded();
            if (Shop.Get(Shop.SHARDS_KEY) < LOOTBOX_PRICE) return null;

            var fullPool = new List<StickerEntry>();
            foreach (var e in Entries)
                if (e.ColorShopKey == null && !IsOwned(e.Key) && e.Key != "sb_color_original") fullPool.Add(e);
            if (fullPool.Count == 0) return null;

            StickerRarity rolled = RollRarity();
            var pool = fullPool.FindAll(e => e.Rarity == rolled);
            if (pool.Count == 0) pool = fullPool; // that tier's fully collected — fall back to whatever's left

            Shop.SpendShards(LOOTBOX_PRICE);
            var won = pool[UnityEngine.Random.Range(0, pool.Count)];
            _owned[won.Key] = true;
            Save();
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Scrapbook] Loot Box -> '" + won.Key + "' (" + won.Rarity + ").");
            return won;
        }

        /// <summary>Page/slot index of an entry within Entries, for the "fly to its slot" animation.</summary>
        public static int IndexOf(StickerEntry entry)
        {
            for (int i = 0; i < Entries.Length; i++) if (Entries[i] == entry) return i;
            return -1;
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                if (File.Exists(FilePath))
                {
                    foreach (string line in File.ReadAllLines(FilePath))
                    {
                        int eq = line.LastIndexOf('=');
                        if (eq < 0) continue;
                        string key = line.Substring(0, eq).Trim();
                        int val;
                        if (int.TryParse(line.Substring(eq + 1).Trim(), out val) && val > 0)
                            _owned[key] = true;
                    }
                }
            }
            catch (Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: Scrapbook read failed: " + e.Message);
            }

            // "Original" is free from the start.
            if (!_owned.ContainsKey("sb_color_original")) _owned["sb_color_original"] = true;

            // Catch colors bought before this feature existed — OnColorPurchased()
            // only fires at the moment of a NEW purchase, so an existing save
            // with (say) Color Gold already owned would otherwise show that
            // sticker locked forever.
            bool changed = false;
            foreach (var e in Entries)
            {
                if (e.ColorShopKey != null && Shop.IsOwned(e.ColorShopKey) && !IsOwned(e.Key))
                {
                    _owned[e.Key] = true;
                    changed = true;
                }
            }
            if (changed) Save();
        }

        private static void Save()
        {
            try
            {
                var lines = new List<string>();
                foreach (var e in Entries)
                    if (IsOwned(e.Key)) lines.Add(e.Key + " = 1");
                File.WriteAllLines(FilePath, lines.ToArray());
            }
            catch (Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: Scrapbook write failed: " + e.Message);
            }
        }
    }
}
