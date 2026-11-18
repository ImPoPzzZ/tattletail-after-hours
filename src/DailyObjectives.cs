using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Daily objectives: three goals on the Arcade's wall (one easy, one medium, one hard) that change every day (at local midnight; the
    /// same three all day). Each pays its own reward in shards when it is done, and then disappears from the wall until tomorrow.
    /// The games report what happens (Daily.Add / Daily.Best); progress is for today only and is kept in Shop.txt ("Daily ..." keys).
    /// </summary>
    public static class Daily
    {
        public class Def
        {
            public string Id, Text, Counter;
            public int Tier, Target;
            public bool Best;   // the best single value today counts (a run, a score); otherwise things add up over the day
        }

        private static Def D(string id, int tier, string text, string counter, int target, bool best)
        {
            return new Def { Id = id, Tier = tier, Text = text, Counter = counter, Target = target, Best = best };
        }

        public static readonly Def[] Pool =
        {
            // easy
            D("e_obj3", 0, "Complete 3 objectives in Endless", "endless_obj", 3, false),
            D("e_vase4", 0, "Break 4 vases in Endless", "vases", 4, false),
            D("e_candle4", 0, "Light 4 candles in Endless", "candles", 4, false),
            D("e_serve8", 0, "Serve 8 customers in Summer Vacation", "served", 8, false),
            D("e_party2", 0, "Survive 2 minutes at Papa's party", "party_minutes", 2, false),
            D("e_toys10", 0, "Defeat 10 toys in Recalled", "toys", 10, false),
            D("e_spend50", 0, "Spend 50 shards in the shops", "spent", 50, false),
            // medium
            D("m_obj6", 1, "Complete 6 objectives in Endless", "endless_obj", 6, false),
            D("m_run4", 1, "Survive 4 minutes in one Endless run", "run_time", 240, true),
            D("m_exfil", 1, "Escape Endless by answering the phone", "exfil", 1, false),
            D("m_candle8", 1, "Light 8 candles in Endless", "candles", 8, false),
            D("m_day1", 1, "Play 1 day of Summer Vacation", "beach_days", 1, false),
            D("m_toys25", 1, "Defeat 25 toys in Recalled", "toys", 25, false),
            D("m_party4", 1, "Survive 4 minutes at Papa's party", "party_minutes", 4, false),
            // hard
            D("h_run10", 2, "Complete 10 objectives in one Endless run", "run_obj", 10, true),
            D("h_run8", 2, "Survive 8 minutes in one Endless run", "run_time", 480, true),
            D("h_hard5", 2, "Complete 5 objectives on Hard or Impossible", "hard_obj", 5, false),
            D("h_day3", 2, "Play 3 days of Summer Vacation", "beach_days", 3, false),
            D("h_toys60", 2, "Defeat 60 toys in Recalled", "toys", 60, false),
            D("h_exfil2", 2, "Escape Endless twice", "exfil", 2, false),
            D("h_shards150", 2, "Earn 150 shards today", "shards_today", 150, false),
        };

        public static readonly int[] Rewards = { 25, 60, 120 };
        public static readonly string[] TierNames = { "EASY", "MEDIUM", "HARD" };

        private const string DATE_KEY = "Daily Date", DONE_KEY = "Daily Done ", COUNT_KEY = "Daily C ";
        private static bool _paying;
        private static string _pending;

        /// <summary>True while a daily reward is being paid (so it does not count as "shards earned today").</summary>
        public static bool Paying { get { return _paying; } }

        private static int Today
        {
            get { System.DateTime n = System.DateTime.Now; return n.Year * 10000 + n.Month * 100 + n.Day; }
        }

        /// <summary>A new day: yesterday's progress is wiped.</summary>
        private static void EnsureToday()
        {
            int d = Today;
            if (Shop.Get(DATE_KEY) == d) return;
            var seen = new List<string>();
            foreach (Def def in Pool)
                if (!seen.Contains(def.Counter)) { seen.Add(def.Counter); Shop.SetValue(COUNT_KEY + def.Counter, 0); }
            for (int i = 0; i < 3; i++) { Shop.SetValue(DONE_KEY + i, 0); Shop.SetValue(PICK_KEY + i, 0); }
            Shop.SetValue(REROLL_KEY, 0);
            Shop.SetValue(DATE_KEY, d);
        }

        private const string PICK_KEY = "Daily Pick ", REROLL_KEY = "Daily Reroll";

        /// <summary>The pool index of today's easy, medium and hard objective (kept for the day; a reroll changes the ones not done yet).</summary>
        public static int[] Picks
        {
            get
            {
                EnsureToday();
                var picks = new int[3];
                for (int tier = 0; tier < 3; tier++)
                {
                    int stored = Shop.Get(PICK_KEY + tier) - 1;   // (stored as index + 1; 0 = not picked yet)
                    if (stored < 0 || stored >= Pool.Length || Pool[stored].Tier != tier)
                    {
                        stored = PickFor(tier, Today * 31 + tier * 7 + 3, -1);   // (the same pick all day, whoever asks)
                        Shop.SetValue(PICK_KEY + tier, stored + 1);
                    }
                    picks[tier] = stored;
                }
                return picks;
            }
        }

        /// <summary>A pool index of this tier, not the one to avoid, and not already satisfied by today's progress (a reroll never hands out a free one).</summary>
        private static int PickFor(int tier, int seed, int avoid)
        {
            var options = new List<int>();
            for (int i = 0; i < Pool.Length; i++)
                if (Pool[i].Tier == tier && i != avoid && Shop.Get(COUNT_KEY + Pool[i].Counter) < Pool[i].Target) options.Add(i);
            if (options.Count == 0)
                for (int i = 0; i < Pool.Length; i++) if (Pool[i].Tier == tier && i != avoid) options.Add(i);
            var rng = new System.Random(seed);
            return options[rng.Next(options.Count)];
        }

        /// <summary>Has today's reroll been used? (One a day.)</summary>
        public static bool RerollUsed { get { EnsureToday(); return Shop.Get(REROLL_KEY) > 0; } }

        /// <summary>Reroll: every objective not done yet is swapped for another of its tier. Once a day. Returns a line to show.</summary>
        public static string Reroll()
        {
            EnsureToday();
            if (AllDone) return "All three are done. New ones tomorrow.";
            if (RerollUsed) return "You already rerolled today.";
            int[] current = Picks;
            int seed = Today * 17 + 5;
            for (int tier = 0; tier < 3; tier++)
            {
                if (IsDone(tier)) continue;
                Shop.SetValue(PICK_KEY + tier, PickFor(tier, seed + tier * 101, current[tier]) + 1);
            }
            Shop.SetValue(REROLL_KEY, 1);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Daily] Rerolled.");
            return "Quests rerolled. (One reroll a day.)";
        }

        public static Def Slot(int tier) { return Pool[Picks[tier]]; }
        public static bool IsDone(int tier) { EnsureToday(); return Shop.Get(DONE_KEY + tier) > 0; }
        public static int Progress(Def d) { EnsureToday(); return Shop.Get(COUNT_KEY + d.Counter); }
        public static bool AllDone { get { return IsDone(0) && IsDone(1) && IsDone(2); } }

        /// <summary>Something happened that adds up over the day (a vase broken, a customer served).</summary>
        public static void Add(string counter, int amount)
        {
            if (amount <= 0) return;
            EnsureToday();
            Shop.SetValue(COUNT_KEY + counter, Shop.Get(COUNT_KEY + counter) + amount);
            Check();
        }

        /// <summary>A value where the best of the day counts (the longest run, the top score, the furthest day).</summary>
        public static void Best(string counter, int value)
        {
            EnsureToday();
            if (value <= Shop.Get(COUNT_KEY + counter)) return;
            Shop.SetValue(COUNT_KEY + counter, value);
            Check();
        }

        private static void Check()
        {
            for (int tier = 0; tier < 3; tier++)
            {
                if (IsDone(tier)) continue;
                Def d = Slot(tier);
                if (Progress(d) < d.Target) continue;
                Shop.SetValue(DONE_KEY + tier, 1);
                _paying = true;
                try { Shop.AddShards(Rewards[tier]); } finally { _paying = false; }
                string msg = "DAILY OBJECTIVE DONE (" + TierNames[tier] + "): " + d.Text + "   +" + Rewards[tier] + " shards";
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Daily] " + msg);
                if (HubMode.Active && HubController.Instance != null && HubController.Instance.Built) HubMode.SetHudText(msg);
                else _pending = _pending == null ? msg : _pending + "\n" + msg;
            }
        }

        /// <summary>What was finished away from the Arcade, to show when you are back (null if nothing).</summary>
        public static string TakePending() { string p = _pending; _pending = null; return p; }

        /// <summary>The wall's text: only the objectives that are not done yet (a finished one is gone for the day).</summary>
        public static string BoardText()
        {
            EnsureToday();
            if (AllDone) return "All three are done.\nCome back tomorrow for new ones.";
            var sb = new System.Text.StringBuilder();
            for (int tier = 0; tier < 3; tier++)
            {
                if (IsDone(tier)) continue;
                Def d = Slot(tier);
                int p = Mathf.Min(Progress(d), d.Target);
                if (sb.Length > 0) sb.Append("\n\n");
                sb.Append(TierNames[tier]).Append("   +").Append(Rewards[tier]).Append(" shards\n");
                sb.Append(d.Text).Append("   (").Append(p.ToString("N0")).Append(" / ").Append(d.Target.ToString("N0")).Append(")");
            }
            return sb.ToString();
        }
    }
}
