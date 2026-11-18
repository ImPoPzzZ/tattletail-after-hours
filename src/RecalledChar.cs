using System.Collections.Generic;
using System.IO;

namespace KaleidoscopeEndless
{
    /// <summary>One slot: an item and how many (materials stack; gear is always 1).</summary>
    public class RcStack
    {
        public int Id, Count;
        public RcStack(int id, int count) { Id = id; Count = count; }
    }

    /// <summary>
    /// A Recalled character: experience, what you carry, wear and keep, and where you are in the story. Pure state
    /// and rules (no Unity), so the headless tests can play with it. RecalledSave turns it into the protected file.
    /// </summary>
    public class RcCharacter
    {
        public const int SLOT_WEAPON1 = 0, SLOT_WEAPON2 = 1, SLOT_ARMOR = 2, SLOT_TRINKET1 = 3, SLOT_TRINKET2 = 4, EQUIP_SLOTS = 5;
        public const float BASE_HEALTH = 100f;

        public int Xp;
        public int BossesBeaten;                 // in order across both worlds: 1 = Patches, ... 5 = Mama, 6 = the Sphinx Unit, ... 10 = Mama-2
        /// <summary>How many of this world's five bosses you've beaten (0 to 5).</summary>
        public int Bosses { get { int b = BossesBeaten - RcWorlds.BOSSES_PER_WORLD * RcWorlds.Now; return b < 0 ? 0 : b > RcWorlds.BOSSES_PER_WORLD ? RcWorlds.BOSSES_PER_WORLD : b; } }
        public int BackpackUpgrades;
        public int Forts;                        // bit per zone: its fort is built (the town's zones are bits 0 to 3; Egypt's outposts are bits 4 to 7)
        public bool HasFort(int zone) { return (Forts & (1 << (zone + 4 * RcWorlds.Now))) != 0; }
        public int Quest, QuestProgress;         // YOUR job (index into RcData.Quests) and how far along it is: this is what's saved

        // While you play with a friend you work on THEIR job (the town host's), so the two of you can do it together. Your own job and its
        // progress aren't touched: leave the town and they're right where you left them. (Not saved.)
        public int SessionQuest = -1, SessionProgress;
        public bool SessionHanded;               // handed this one in already
        /// <summary>Working on a friend's job (not your own).</summary>
        public bool InSession { get { return SessionQuest >= 0 && SessionQuest != Quest; } }
        /// <summary>The job you're on right now: the friend's while you're in their town, your own otherwise.</summary>
        public int ActiveQuest { get { return InSession ? SessionQuest : Quest; } }
        public int ActiveProgress { get { return InSession ? SessionProgress : QuestProgress; } }
        public void SetSession(int quest) { if (quest == SessionQuest) return; SessionQuest = quest; SessionProgress = 0; SessionHanded = false; }
        public void ClearSession() { SessionQuest = -1; SessionProgress = 0; SessionHanded = false; }
        public bool QuestTold;                   // the quest giver has told you about it
        public int Notices;                      // bit per Recall Notice you've read (RcNotices)
        public int NoticesTold;                  // bit per zone: the kids have said what they think of its three; bit 5: Moss has told you about his cassette player
        public int Deaths, Kills;
        public float PlaySeconds;
        public readonly int[] Equip = new int[EQUIP_SLOTS];
        public readonly List<RcStack> Backpack = new List<RcStack>();
        public readonly List<RcStack> Stash = new List<RcStack>();

        public int Level { get { return RecalledRules.LevelFor(Xp); } }
        public int BackpackSlots { get { return RecalledRules.BackpackSlots(BackpackUpgrades); } }

        /// <summary>Experience into the current level, and what the level needs (0 at the cap).</summary>
        public void LevelProgress(out int into, out int need)
        {
            int xp = Xp, level = 1;
            while (level < RecalledRules.LEVEL_CAP && xp >= RecalledRules.XpToNext(level)) { xp -= RecalledRules.XpToNext(level); level++; }
            into = xp; need = RecalledRules.XpToNext(level);
            if (RcWorlds.Now == RcWorlds.TOWN && level >= RecalledRules.TOWN_CAP) need = 0;   // (the town has nothing more to give: Egypt does)
        }

        /// <summary>Adds experience; true if that was a level up.</summary>
        public bool AddXp(int amount)
        {
            int before = Level;
            int cap = RecalledRules.XpFor(RcWorlds.Now == RcWorlds.TOWN ? RecalledRules.TOWN_CAP : RecalledRules.LEVEL_CAP);
            if (Xp >= cap) return false;   // (already past what this world gives)
            Xp = System.Math.Min(cap, Xp + System.Math.Max(0, amount));
            return Level > before;
        }

        // ── Stats: all from gear ─────────────────────────────────────────────
        private float Sum(int which)
        {
            float t = 0f;
            for (int i = 0; i < EQUIP_SLOTS; i++)
            {
                RcItem it = RcData.Item(Equip[i]);
                if (it == null) continue;
                t += which == 0 ? it.Health : which == 1 ? it.PowerPct : which == 2 ? it.SpeedPct : which == 3 ? it.CooldownPct : which == 4 ? it.HealingPct : it.Armor;
            }
            return t;
        }

        public float MaxHealth { get { return BASE_HEALTH + (Level - 1) * RecalledRules.HEALTH_PER_LEVEL + Sum(0); } }
        public float PowerMult { get { return 1f + Sum(1) / 100f; } }
        public float SpeedMult { get { return 1f + Sum(2) / 100f; } }
        public float CooldownMult { get { return 1f / (1f + Sum(3) / 100f); } }
        public float HealingMult { get { return 1f + Sum(4) / 100f; } }
        /// <summary>Armor points from what you're wearing: each one is 1% less damage taken (never more than ARMOR_CAP).</summary>
        public int Armor { get { int a = (int)Sum(5); return a > ARMOR_CAP ? ARMOR_CAP : a; } }
        public const int ARMOR_CAP = 75;
        /// <summary>What's left of a hit once your armor has had its share (1 = all of it).</summary>
        public float DamageTaken { get { return 1f - Armor / 100f; } }

        public RcItem Weapon(int slot) { return RcData.Item(Equip[slot == 1 ? SLOT_WEAPON2 : SLOT_WEAPON1]); }

        // ── Inventory ────────────────────────────────────────────────────────
        public static int Count(List<RcStack> bag, int id)
        {
            int n = 0;
            for (int i = 0; i < bag.Count; i++) if (bag[i].Id == id) n += bag[i].Count;
            return n;
        }

        public int Carried(int id) { return Count(Backpack, id); }

        /// <summary>What the bench can use: your backpack and your stash together.</summary>
        public int Have(int id) { return Count(Backpack, id) + Count(Stash, id); }

        /// <summary>Puts items into a bag of 'slots' slots; returns how many didn't fit.</summary>
        public static int AddTo(List<RcStack> bag, int slots, int id, int count)
        {
            RcItem it = RcData.Item(id);
            if (it == null || count <= 0) return 0;
            if (!it.IsGear)
            {
                for (int i = 0; i < bag.Count && count > 0; i++)
                {
                    if (bag[i].Id != id || bag[i].Count >= RecalledRules.MATERIAL_STACK) continue;
                    int room = RecalledRules.MATERIAL_STACK - bag[i].Count, put = count < room ? count : room;
                    bag[i].Count += put; count -= put;
                }
            }
            while (count > 0 && bag.Count < slots)
            {
                int put = it.IsGear ? 1 : (count < RecalledRules.MATERIAL_STACK ? count : RecalledRules.MATERIAL_STACK);
                bag.Add(new RcStack(id, put));
                count -= put;
            }
            return count;
        }

        /// <summary>Into the backpack; returns how many didn't fit.</summary>
        public int Add(int id, int count) { return AddTo(Backpack, BackpackSlots, id, count); }

        /// <summary>Takes items out of a bag (all or nothing).</summary>
        public static bool RemoveFrom(List<RcStack> bag, int id, int count)
        {
            if (Count(bag, id) < count) return false;
            for (int i = bag.Count - 1; i >= 0 && count > 0; i--)
            {
                if (bag[i].Id != id) continue;
                int take = bag[i].Count < count ? bag[i].Count : count;
                bag[i].Count -= take; count -= take;
                if (bag[i].Count <= 0) bag.RemoveAt(i);
            }
            return true;
        }

        public bool Remove(int id, int count) { return RemoveFrom(Backpack, id, count); }

        /// <summary>Moves one slot between the backpack and the stash (as much of it as fits).</summary>
        public bool Move(bool toStash, int index)
        {
            List<RcStack> from = toStash ? Backpack : Stash, to = toStash ? Stash : Backpack;
            if (index < 0 || index >= from.Count) return false;
            RcStack s = from[index];
            int left = AddTo(to, toStash ? RecalledRules.STASH_SLOTS : BackpackSlots, s.Id, s.Count);
            if (left == s.Count) return false;
            if (left == 0) from.RemoveAt(index); else s.Count = left;
            return true;
        }

        // ── Gear ─────────────────────────────────────────────────────────────
        /// <summary>The equipment slot an item goes in (-1: not gear). Weapons and trinkets take the first free one of their two.</summary>
        public int SlotFor(RcItem it)
        {
            if (it == null) return -1;
            switch (it.Kind)
            {
                case RcKind.Melee: case RcKind.Ranged: case RcKind.Gadget: return Equip[SLOT_WEAPON1] == 0 ? SLOT_WEAPON1 : Equip[SLOT_WEAPON2] == 0 ? SLOT_WEAPON2 : SLOT_WEAPON1;
                case RcKind.Armor: return SLOT_ARMOR;
                case RcKind.Trinket: return Equip[SLOT_TRINKET1] == 0 ? SLOT_TRINKET1 : Equip[SLOT_TRINKET2] == 0 ? SLOT_TRINKET2 : SLOT_TRINKET1;
                default: return -1;
            }
        }

        /// <summary>Why this backpack item can't be equipped (null: it can).</summary>
        public string EquipBlock(int backpackIndex)
        {
            if (backpackIndex < 0 || backpackIndex >= Backpack.Count) return "Nothing there.";
            RcItem it = RcData.Item(Backpack[backpackIndex].Id);
            if (it != null && it.Kind == RcKind.Mount) return "Press V to ride it. It stays in your backpack.";
            if (it == null || !it.IsGear) return "That's a material: use it at the crafting bench.";
            if (!RecalledRules.CanEquip(Level, it.Level)) return "Needs level " + it.Level + ".";
            return null;
        }

        /// <summary>Equips a backpack item into 'slot' (-1: pick one); what was there goes back in the backpack.</summary>
        public bool EquipFromBackpack(int backpackIndex, int slot)
        {
            if (EquipBlock(backpackIndex) != null) return false;
            RcItem it = RcData.Item(Backpack[backpackIndex].Id);
            if (slot < 0) slot = SlotFor(it);
            bool weaponSlot = slot == SLOT_WEAPON1 || slot == SLOT_WEAPON2, trinketSlot = slot == SLOT_TRINKET1 || slot == SLOT_TRINKET2;
            bool isWeapon = it.Kind == RcKind.Melee || it.Kind == RcKind.Ranged || it.Kind == RcKind.Gadget;
            if ((isWeapon && !weaponSlot) || (it.Kind == RcKind.Trinket && !trinketSlot) || (it.Kind == RcKind.Armor && slot != SLOT_ARMOR)) return false;
            int old = Equip[slot];
            Backpack.RemoveAt(backpackIndex);
            Equip[slot] = it.Id;
            if (old != 0) Backpack.Add(new RcStack(old, 1)); // (the slot we just freed)
            return true;
        }

        public bool Unequip(int slot)
        {
            if (slot < 0 || slot >= EQUIP_SLOTS || Equip[slot] == 0 || Backpack.Count >= BackpackSlots) return false;
            Backpack.Add(new RcStack(Equip[slot], 1));
            Equip[slot] = 0;
            return true;
        }

        // ── Crafting ─────────────────────────────────────────────────────────
        /// <summary>Why a recipe can't be made right now (null: it can).</summary>
        public string CraftBlock(RcRecipe r)
        {
            if (r.Zone > Bosses) return "Beat " + RcData.BossNames[r.Zone - 1] + " first.";
            if (r.Makes == 0)
            {
                if (BackpackUpgrades >= RecalledRules.BACKPACK_UPGRADES) return "Your backpack can't get any bigger.";
                if (r.Needs[0].Id != RcData.THREAD + BackpackUpgrades) return BackpackUpgrades > r.Needs[0].Id - RcData.THREAD ? "Already made." : "Make the smaller upgrade first.";
            }
            for (int i = 0; i < r.Needs.Length; i++)
                if (Have(r.Needs[i].Id) < r.Needs[i].Count) return "Not enough " + RcData.Item(r.Needs[i].Id).Name + " (backpack and stash together).";
            if (r.Makes != 0 && Backpack.Count >= BackpackSlots)
            {
                // (crafting frees material slots only if a stack runs out: don't count on it)
                return "Your backpack is full.";
            }
            return null;
        }

        public bool Craft(RcRecipe r)
        {
            if (CraftBlock(r) != null) return false;
            for (int i = 0; i < r.Needs.Length; i++)
            {
                // The backpack's first; the stash makes up the rest.
                int fromBag = System.Math.Min(r.Needs[i].Count, Carried(r.Needs[i].Id));
                Remove(r.Needs[i].Id, fromBag);
                if (fromBag < r.Needs[i].Count) RemoveFrom(Stash, r.Needs[i].Id, r.Needs[i].Count - fromBag);
            }
            if (r.Makes == 0) BackpackUpgrades++;
            else Add(r.Makes, 1);
            return true;
        }

        /// <summary>
        /// The kids build a zone's fort the moment you've handed in their first job there (no going out to it). Builds
        /// any that are due, and returns which, as a bit per zone (0: none).
        /// </summary>
        public int AutoForts()
        {
            int built = 0;
            for (int z = 0; z < 4; z++)
                if (!HasFort(z) && Quest > RcData.FirstJob(z) && RcData.Quests[RcData.FirstJob(z)].World == RcWorlds.Now) { Forts |= 1 << (z + 4 * RcWorlds.Now); built |= 1 << z; }
            return built;
        }

        // ── Quests ───────────────────────────────────────────────────────────
        public RcQuest CurrentQuest { get { int i = ActiveQuest; return i >= 0 && i < RcData.Quests.Count && (RcWorlds.EgyptOpen || i < RcWorlds.TOWN_JOBS) ? RcData.Quests[i] : null; } }

        public bool QuestComplete
        {
            get
            {
                RcQuest q = CurrentQuest;
                if (q == null) return false;
                if (q.World != RcWorlds.Now) return false;                               // (a job in the other world: you hand it in there)
                if (InSession && (SessionHanded || SessionQuest < Quest)) return false; // (handed in already, or a job you've done: you only help)
                if (q.Kind == RcQuestKind.Bring) return Carried(q.Target) + Count(Stash, q.Target) >= q.Count;
                return ActiveProgress >= q.Count;
            }
        }

        private void Bump(RcQuestKind kind, int zone, int target)
        {
            RcQuest q = CurrentQuest;
            if (q == null || q.Kind != kind || ActiveProgress >= q.Count) return;
            if (q.World != RcWorlds.Now) return;   // (a job in the other world isn't done here)
            if (kind == RcQuestKind.Kill && (q.Zone != zone)) return;
            if ((kind == RcQuestKind.Elite || kind == RcQuestKind.Craft) && q.Target != target) return;
            if ((kind == RcQuestKind.Boss || kind == RcQuestKind.Reach) && q.Zone != zone) return;
            if (InSession) SessionProgress++; else QuestProgress++;
        }

        public void OnKill(RcEnemyDef e)
        {
            Kills++;
            if (e.Kind == RcEnemyKind.Boss)
            {
                int beaten = e.World * RcWorlds.BOSSES_PER_WORLD + e.Zone + 1;
                if (BossesBeaten < beaten) BossesBeaten = beaten;
                Bump(RcQuestKind.Boss, e.Zone, e.Id);
                return;
            }
            Bump(RcQuestKind.Kill, e.Zone, e.Id);
            Bump(RcQuestKind.Elite, e.Zone, e.Id);
        }

        public void OnCrafted(int itemId) { Bump(RcQuestKind.Craft, 0, itemId); }
        public void OnReached(int zone) { Bump(RcQuestKind.Reach, zone, 0); }
        public void OnTalked() { Bump(RcQuestKind.Talk, 0, 0); }
        public void OnScene() { Bump(RcQuestKind.Scene, 0, 0); }         // (the argument at the treehouse is over)
        public void OnBoarded() { Bump(RcQuestKind.Station, 4, 0); }     // (you stood at the Recall Express)

        // ── Recall Notices ───────────────────────────────────────────────────
        public bool HasNotice(int i) { return i >= 0 && i < RcNotices.COUNT && (Notices & (1 << i)) != 0; }
        public void ReadNotice(int i) { if (i >= 0 && i < RcNotices.COUNT) Notices |= 1 << i; }
        public int NoticeCount { get { int n = 0; for (int i = 0; i < RcNotices.COUNT; i++) if (HasNotice(i)) n++; return n; } }
        /// <summary>All three of a zone's notices read.</summary>
        public bool ZoneNoticesRead(int zone) { return HasNotice(zone * 3) && HasNotice(zone * 3 + 1) && HasNotice(zone * 3 + 2); }

        /// <summary>Mama's Tape is yours to carry from the moment she breaks until Moss has played it. Put it back if it went missing.</summary>
        public bool EnsureTape()
        {
            RcQuest q = CurrentQuest;
            if (q == null || q.Kind != RcQuestKind.Bring || q.Target != RcData.TAPE || Have(q.Target) > 0) return false;
            return Add(q.Target, 1) == 0;
        }

        /// <summary>
        /// Hands the current quest in: takes what it asked you to bring, gives its reward, moves to the next one.
        /// Returns the experience it paid (-1: not complete, or the reward doesn't fit in your backpack).
        /// </summary>
        public int TurnIn()
        {
            RcQuest q = CurrentQuest;
            if (q == null || !QuestComplete) return -1;
            if (Backpack.Count + q.Reward.Length > BackpackSlots) return -1; // room for the reward (a slot each, at worst)
            if (q.Kind == RcQuestKind.Bring && !q.Keep)
            {
                int fromBag = System.Math.Min(q.Count, Carried(q.Target));
                Remove(q.Target, fromBag);
                if (fromBag < q.Count) RemoveFrom(Stash, q.Target, q.Count - fromBag);
            }
            for (int i = 0; i < q.Reward.Length; i++) Add(q.Reward[i].Id, q.Reward[i].Count);
            int xp = q.Kind == RcQuestKind.Boss ? 0 : q.Xp;
            if (InSession) { SessionHanded = true; return xp; } // (a friend's job: you're paid for it, and your own job stays where it was)
            Quest++;
            QuestProgress = 0;
            QuestTold = false;
            return xp;
        }

        // ── The file ─────────────────────────────────────────────────────────
        private const int VERSION = 2;   // 2: the Recall Notices

        public void Write(BinaryWriter w)
        {
            w.Write(VERSION);
            w.Write(Xp); w.Write(BossesBeaten); w.Write(BackpackUpgrades); w.Write(Forts);
            w.Write(Quest); w.Write(QuestProgress); w.Write(QuestTold);
            w.Write(Deaths); w.Write(Kills); w.Write(PlaySeconds);
            for (int i = 0; i < EQUIP_SLOTS; i++) w.Write(Equip[i]);
            WriteBag(w, Backpack);
            WriteBag(w, Stash);
            w.Write(Notices); w.Write(NoticesTold);
        }

        private static void WriteBag(BinaryWriter w, List<RcStack> bag)
        {
            w.Write(bag.Count);
            for (int i = 0; i < bag.Count; i++) { w.Write(bag[i].Id); w.Write(bag[i].Count); }
        }

        /// <summary>Reads a character; null if the data isn't one.</summary>
        public static RcCharacter Read(BinaryReader r)
        {
            try
            {
                int version = r.ReadInt32();
                if (version != 1 && version != 2) return null;
                var c = new RcCharacter();
                c.Xp = r.ReadInt32(); c.BossesBeaten = r.ReadInt32(); c.BackpackUpgrades = r.ReadInt32(); c.Forts = r.ReadInt32();
                c.Quest = r.ReadInt32(); c.QuestProgress = r.ReadInt32(); c.QuestTold = r.ReadBoolean();
                c.Deaths = r.ReadInt32(); c.Kills = r.ReadInt32(); c.PlaySeconds = r.ReadSingle();
                for (int i = 0; i < EQUIP_SLOTS; i++) c.Equip[i] = r.ReadInt32();
                if (!ReadBag(r, c.Backpack, 64) || !ReadBag(r, c.Stash, 128)) return null;
                if (version >= 2) { c.Notices = r.ReadInt32(); c.NoticesTold = r.ReadInt32(); }
                return c;
            }
            catch (System.Exception) { return null; }
        }

        private static bool ReadBag(BinaryReader r, List<RcStack> bag, int max)
        {
            int n = r.ReadInt32();
            if (n < 0 || n > max) return false;
            for (int i = 0; i < n; i++) bag.Add(new RcStack(r.ReadInt32(), r.ReadInt32()));
            return true;
        }

        /// <summary>
        /// Is this a character the game could have produced? (The save's signature stops casual editing; this stops
        /// saves that are valid but impossible.) Null if fine, else what's wrong.
        /// </summary>
        public string Problem()
        {
            if (Xp < 0 || BossesBeaten < 0 || BossesBeaten > 2 * RcWorlds.BOSSES_PER_WORLD) return "experience or bosses out of range";
            if (BackpackUpgrades < 0 || BackpackUpgrades > RecalledRules.BACKPACK_UPGRADES || BackpackUpgrades > BossesBeaten) return "backpack upgrades without the bosses";
            if (Quest < 0 || Quest > RcData.Quests.Count || QuestProgress < 0) return "quest out of range";
            if ((Notices >> RcNotices.COUNT) != 0 || (NoticesTold >> 6) != 0 || Notices < 0 || NoticesTold < 0) return "notices out of range";
            if (Backpack.Count > BackpackSlots || Stash.Count > RecalledRules.STASH_SLOTS) return "too many items";
            int level = Level;
            // A zone's gate needs its level and the boss before it: levels and bosses have to agree, roughly.
            for (int z = 1; z <= 4; z++)
                if (BossesBeaten > z && level < RecalledRules.GateLevelIn(0, z)) return "bosses beaten below their zone's level";
            for (int z = 0; z <= 4; z++)
                if (BossesBeaten > RcWorlds.BOSSES_PER_WORLD + z && level < RecalledRules.GateLevelIn(1, z)) return "Egypt's bosses beaten below their zone's level";
            for (int i = 0; i < EQUIP_SLOTS; i++)
            {
                if (Equip[i] == 0) continue;
                RcItem it = RcData.Item(Equip[i]);
                if (it == null || !it.IsGear) return "equipped something that isn't gear";
                if (it.Level > level) return "equipped gear above your level";
                bool weapon = it.Kind == RcKind.Melee || it.Kind == RcKind.Ranged || it.Kind == RcKind.Gadget;
                if ((i <= SLOT_WEAPON2) != weapon || (i == SLOT_ARMOR) != (it.Kind == RcKind.Armor)) return "gear in the wrong slot";
            }
            string p = BagProblem(Backpack);
            return p != null ? p : BagProblem(Stash);
        }

        private static string BagProblem(List<RcStack> bag)
        {
            for (int i = 0; i < bag.Count; i++)
            {
                RcItem it = RcData.Item(bag[i].Id);
                if (it == null) return "an item that doesn't exist";
                if (bag[i].Count < 1 || bag[i].Count > (it.IsGear ? 1 : RecalledRules.MATERIAL_STACK)) return "a stack that's too big";
            }
            return null;
        }
    }
}
