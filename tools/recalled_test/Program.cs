using System;
using System.Collections.Generic;
using System.IO;
using KaleidoscopeEndless;

// Headless checks for Recalled: the data, the character, and the town (is everywhere reachable? do gates seal?).
// Also draws the real geometry top-down to map.ppm (and one crop per zone).

class MapSink : RcSink
{
    public struct Prim { public RcMat M; public float X0, Y0, Z0, X1, Y1, Z1; public bool Round; }
    public readonly List<Prim> Prims = new List<Prim>();
    public readonly List<float[]> Lights = new List<float[]>();
    public int Boxes, Colliders;
    public override void Box(RcMat m, float x0, float y0, float z0, float x1, float y1, float z1, bool collide)
    {
        Boxes++; if (collide) Colliders++;
        if (x1 <= x0 || y1 <= y0 || z1 <= z0) Console.WriteLine("  ! degenerate box " + m + " " + x0 + "," + z0 + " .. " + x1 + "," + z1 + " y " + y0 + ".." + y1);
        if (m != RcMat.None) Prims.Add(new Prim { M = m, X0 = x0, Y0 = y0, Z0 = z0, X1 = x1, Y1 = y1, Z1 = z1 });
    }
    public override void Walk(RcMat m, float x0, float y0, float z0, float x1, float y1, float z1) { Box(m, x0, y0, z0, x1, y1, z1, true); }
    public override void Cyl(RcMat m, float x, float y0, float z, float radius, float height, bool collide)
    {
        Boxes++; if (collide) Colliders++;
        if (m != RcMat.None) Prims.Add(new Prim { M = m, X0 = x - radius, Y0 = y0, Z0 = z - radius, X1 = x + radius, Y1 = y0 + height, Z1 = z + radius, Round = true });
    }
    public override void Ball(RcMat m, float x, float y, float z, float rx, float ry, float rz)
    {
        Boxes++;
        Prims.Add(new Prim { M = m, X0 = x - rx, Y0 = y - ry, Z0 = z - rz, X1 = x + rx, Y1 = y + ry, Z1 = z + rz, Round = true });
    }
    public override void Light(float x, float y, float z, int color, float range, float intensity) { Lights.Add(new[] { x, z, color, range }); }
}

static class Program
{
    static int fails;
    static void Check(bool ok, string what) { if (!ok) { fails++; Console.WriteLine("  FAIL: " + what); } }

    static void Tall()
    {
        int touch = RcTown.Spawns.Count;
        RcWorlds.Now = 1;
        var nv = RcNav.Instance;
        foreach (float[] c in new[] { new[] { -165f, -153f }, new[] { -170f, -150f }, new[] { -176f, -153f }, new[] { -160f, -158f }, new[] { -153.5f, -153f } })
            Console.WriteLine("  probe " + c[0] + "," + c[1] + " free=" + nv.Free((int)(c[0] - RcNav.OX), (int)(c[1] - RcNav.OZ)));
        var sink = new TallSink();
        for (int part = RcTown.PART_CORE; part < 5; part++) { Console.WriteLine("part " + part); RcTown.Build(sink, part); }
    }

    static void Main(string[] args)
    {
        if (args.Length > 3 && args[0] == "give") { Give(args); return; } // give <folder> <steam id> <item ids...>
        if (args.Length > 2 && args[0] == "forge") { Forge(args[1], args[2]); return; } // forge <saves folder> <steam id>: level 15, every boss beaten, best gear, on "Take the Tape"
        if (args.Length > 1 && args[0] == "dump") { Dump(args[1]); return; } // dump <file>: the game's data as markdown
        if (args.Length > 0 && args[0] == "tall") { Tall(); return; } // tall: Egypt's big drawn-only shapes (things you could walk through)
        Data();
        Character();
        SaveFile();
        Together();
        Town(args.Length > 0 ? args[0] : ".");
        Console.WriteLine(fails == 0 ? "ALL OK" : fails + " FAILURE(S)");
    }

    // Rewrites a real save for dev testing (the folder's files are backed up first).
    static void Forge(string dir, string steamId)
    {
        string keep = Path.Combine(dir, "forge_backup"); Directory.CreateDirectory(keep);
        foreach (string f in Directory.GetFiles(dir, "Recalled*")) { string to = Path.Combine(keep, Path.GetFileName(f) + ".copy"); if (!File.Exists(to)) File.Copy(f, to); }
        SaveFiles.Dir = dir;
        Steamworks.SteamUser.Id = ulong.Parse(steamId);
        string note;
        RcCharacter c = RecalledSave.Load(out note);
        Console.WriteLine("loaded: " + (c == null ? "NO" : "level " + c.Level + ", job " + (c.Quest + 1)) + (note != null ? " (" + note + ")" : ""));
        if (c == null) return;
        c.Xp = 10500; c.BossesBeaten = 5; c.BackpackUpgrades = 4; c.Forts = 15;
        c.Quest = 21; c.QuestProgress = 0; c.QuestTold = false;
        int[] gear = { 104, 114, 134, 142, 154 };            // Sledgehammer, Bolt Thrower, Vault Plate, Lucky Marble III, Teddy Patch III
        for (int i = 0; i < 5; i++) c.Equip[i] = gear[i];
        int[] spare = { 120, 121, 122, 123, 124, 145, 148, 151 }; // every gadget, and the other level-13 trinkets
        foreach (int id in spare) if (c.Carried(id) + RcCharacter.Count(c.Stash, id) == 0) c.Add(id, 1);
        c.Add(RcData.TAPE, 1);
        Console.WriteLine("problem: " + (c.Problem() ?? "none") + ", level " + c.Level);
        RecalledSave.Save(c);
        var back = RecalledSave.Load(out note);
        Console.WriteLine("saved and read back: level " + back.Level + ", job " + (back.Quest + 1) + ", bosses " + back.BossesBeaten + ", weapon " + RcData.Item(back.Equip[0]).Name + ", armor " + RcData.Item(back.Equip[2]).Name);
    }

    static void Give(string[] args)
    {
        SaveFiles.Dir = args[1];
        Steamworks.SteamUser.Id = ulong.Parse(args[2]);
        string note;
        RcCharacter c = RecalledSave.Load(out note);
        if (c == null) { Console.WriteLine("couldn't load the save"); return; }
        for (int i = 3; i < args.Length; i++) { int id = int.Parse(args[i]); int left = c.Add(id, 1); Console.WriteLine((left == 0 ? "gave " : "no room for ") + RcData.Item(id).Name); }
        RecalledSave.Save(c);
        Console.WriteLine("saved: level " + c.Level + ", job " + (c.Quest + 1));
    }

    static void Data()
    {
        Console.WriteLine("== data");
        Console.WriteLine("  " + RcData.Items.Count + " items, " + RcData.Recipes.Count + " recipes, " + RcData.Quests.Count + " quests");
        foreach (var r in RcData.Recipes)
        {
            Check(r.Makes == 0 || RcData.Item(r.Makes) != null, "recipe makes unknown item " + r.Makes);
            foreach (var n in r.Needs) Check(RcData.Item(n.Id) != null && n.Count > 0, "recipe needs unknown item " + n.Id);
        }
        foreach (var e in RcData.Enemies)
            foreach (var d in e.Drops) Check(RcData.Item(d.Id) != null && d.Min >= 1 && d.Max >= d.Min && d.Chance > 0f, "bad drop on " + e.Name);
        Check(RcData.Quests.Count == RcWorlds.TOWN_JOBS, "the town's 25 jobs (" + RcData.Quests.Count + ")");
        for (int world = 0; world < 1; world++)
        {
            RcWorlds.Now = world;
            Console.WriteLine("  -- " + RcWorlds.Name(world));
            int z = 0, jobs = 0;
            foreach (var q in RcData.Quests)
            {
                if (q.World != world) continue;
                jobs++;
                Check(q.Zone >= z, "quests out of zone order: " + q.Title);
                z = q.Zone;
                foreach (var rw in q.Reward) Check(RcData.Item(rw.Id) != null, "quest reward unknown item " + rw.Id + " in " + q.Title);
                if (q.Kind == RcQuestKind.Kill || q.Kind == RcQuestKind.Elite) Check(q.Target == 0 || RcData.Enemy(q.Target) != null, "quest target enemy " + q.Target);
                if (q.Kind == RcQuestKind.Bring || q.Kind == RcQuestKind.Craft) Check(RcData.Item(q.Target) != null, "quest target item " + q.Target);
                if (q.Kind == RcQuestKind.Elite) Check(RcData.Enemy(q.Target).Zone == q.Zone && RcData.Enemy(q.Target).World == world, "elite quest enemy in another zone: " + q.Title);
            }
            // Every craftable item's materials must drop somewhere reachable by then (its zone or earlier), or come from a quest.
            var dropsByZone = new HashSet<int>[5];
            for (int i = 0; i < 5; i++) dropsByZone[i] = new HashSet<int>();
            foreach (var e in RcData.Enemies) if (e.World == world) foreach (var d in e.Drops) for (int i = e.Zone; i < 5; i++) dropsByZone[i].Add(d.Id);
            foreach (var r in RcData.Recipes)
            {
                if (r.World != world) continue;
                foreach (var n in r.Needs)
                    Check(dropsByZone[Math.Min(4, r.Zone)].Contains(n.Id), "recipe for " + (r.Makes == 0 ? "backpack" : RcData.Item(r.Makes).Name) + " (bench " + r.Zone + ") needs " + RcData.Item(n.Id).Name + ", which doesn't drop by then");
            }
            // Experience: quests + bosses per zone against what the zone's levels need.
            int total = 0, last = world == 0 ? 15 : 25;
            for (int zone = 0; zone < 5; zone++)
            {
                int quests = 0;
                foreach (var q in RcData.Quests) if (q.World == world && q.Zone == zone) quests += q.Xp;
                int from = RecalledRules.GateLevel((RecalledZone)zone), to = zone == 4 ? last : RecalledRules.GateLevel((RecalledZone)(zone + 1)), need = 0;
                for (int l = from; l < to; l++) need += RecalledRules.XpToNext(l);
                int boss = RecalledRules.BossXp(to - 1);
                Console.WriteLine("  zone " + zone + ": levels " + from + "-" + to + " need " + need + " xp; quests give " + quests + ", boss " + boss + " (" + (100 * (quests + boss) / need) + "%)");
                Check(quests + boss >= need * 0.6f && quests + boss <= need * 1.05f, "world " + world + " zone " + zone + " quest xp out of range");
                total += need;
            }
            Check(total == (world == 0 ? 10500 : 19500), "total xp for the world should be " + (world == 0 ? 10500 : 19500) + ", is " + total);
        }
        RcWorlds.Now = 0;
    }

    static void Character()
    {
        Console.WriteLine("== character");
        var c = new RcCharacter();
        Check(c.Level == 1 && c.MaxHealth == 100f && c.BackpackSlots == 20, "fresh character");
        Check(c.Problem() == null, "fresh character valid: " + c.Problem());
        // Stacking.
        Check(c.Add(RcData.SPRING, 120) == 0, "add 120 springs");
        Check(c.Backpack.Count == 3 && c.Carried(RcData.SPRING) == 120, "120 springs = 3 stacks");
        Check(c.Remove(RcData.SPRING, 70) && c.Carried(RcData.SPRING) == 50, "remove 70");
        Check(!c.Remove(RcData.SPRING, 51), "can't remove more than carried");
        // Full backpack.
        int left = 0;
        for (int i = 0; i < 25; i++) left += c.Add(RcData.BAT, 1);
        Check(c.Backpack.Count == 20 && left == 6, "backpack fills at 20 (left " + left + ", count " + c.Backpack.Count + ")");
        // Play the quest chain through, the way the game would.
        c = new RcCharacter();
        int guard = 0;
        while (c.CurrentQuest != null && guard++ < 200)
        {
            RcQuest q = c.CurrentQuest;
            RcWorlds.Now = q.World;   // (the train takes you from one world to the other)
            switch (q.Kind)
            {
                case RcQuestKind.Talk: c.OnTalked(); break;
                case RcQuestKind.Kill:
                    for (int i = 0; i < q.Count; i++) { var e = FirstStray(q.Zone); c.OnKill(e); c.AddXp(e.Xp); }
                    break;
                case RcQuestKind.Elite:
                    for (int i = 0; i < q.Count; i++) { var e = RcData.Enemy(q.Target); c.OnKill(e); c.AddXp(e.Xp); }
                    break;
                case RcQuestKind.Bring: c.Add(q.Target, q.Count); break;
                case RcQuestKind.Craft:
                    foreach (var r in RcData.Recipes)
                        if (r.Makes == q.Target)
                        {
                            foreach (var n in r.Needs) c.Add(n.Id, n.Count);
                            Check(c.CraftBlock(r) == null, "can craft " + RcData.Item(r.Makes).Name + ": " + c.CraftBlock(r));
                            Check(c.Craft(r), "crafted");
                            c.OnCrafted(r.Makes);
                        }
                    break;
                case RcQuestKind.Reach: c.OnReached(q.Zone); break;
                case RcQuestKind.Scene: c.OnScene(); break;
                case RcQuestKind.Station: c.OnBoarded(); break;
                case RcQuestKind.Boss:
                    Check(RecalledRules.CanEnter((RecalledZone)q.Zone, c.Level, c.Bosses), "level " + c.Level + " can enter zone " + q.Zone + " for its boss (bosses " + c.Bosses + ")");
                    var boss = RcData.Enemy(RcData.BossId(q.World, q.Zone));
                    c.OnKill(boss);
                    c.AddXp(RecalledRules.BossXp(c.Level));
                    break;
            }
            Check(c.QuestComplete, "quest complete: " + q.Title);
            // Keep the backpack from filling with rewards (the player would stash them).
            while (c.Backpack.Count > 8) c.Move(true, 0);
            int xp = c.TurnIn();
            c.AutoForts(); // (what the game does right after a hand-in)
            Check(xp >= 0, "turn in " + q.Title);
            if (xp > 0) c.AddXp(xp);
            if (q.Kind == RcQuestKind.Boss) Console.WriteLine("  after " + q.Title + ": level " + c.Level + ", bosses " + c.BossesBeaten);
        }
        Check(c.CurrentQuest == null, "the chain ends");
        Check(c.Forts == 255, "all eight forts and outposts were built by handing in the first jobs (" + c.Forts + ")");
        RcWorlds.Now = 0;
        var fortTest = new RcCharacter();
        fortTest.Quest = RcData.FirstJob(1);
        Check(fortTest.AutoForts() == 1 && (fortTest.Forts & 2) == 0, "before the mall's first job is handed in: only the suburbs' fort is due");
        fortTest.Quest = RcData.FirstJob(1) + 1;
        Check(fortTest.AutoForts() == 2 && fortTest.AutoForts() == 0, "handing it in builds the mall's fort, once");
        Check(c.BossesBeaten == 10, "all ten bosses");
        Check(c.Level >= 23, "reaches at least 23 from quests, quest kills and bosses alone (is " + c.Level + ")");
        RcWorlds.Now = 1;
        Check(c.Bosses == 5, "all five of Egypt's");
        RcWorlds.Now = 0;
        // Equip.
        c.Add(100, 1);
        int idx = c.Backpack.Count - 1;
        Check(c.EquipBlock(idx) == null && c.EquipFromBackpack(idx, -1), "equip a bat");
        Check(c.Equip[RcCharacter.SLOT_WEAPON1] == 100 || c.Equip[RcCharacter.SLOT_WEAPON2] == 100, "bat is in a weapon slot");
        c.Add(134, 1);
        Check(c.EquipFromBackpack(c.Backpack.Count - 1, -1) && c.MaxHealth == 100f + (c.Level - 1) * RecalledRules.HEALTH_PER_LEVEL + 165f + TrinketHealth(c), "vault plate and level give health (" + c.MaxHealth + ")");
        // The bench uses the stash.
        var bench = new RcCharacter();
        bench.BossesBeaten = 1; bench.Xp = 600;
        RcRecipe hockey = null;
        foreach (var r in RcData.Recipes) if (r.Makes == 101) hockey = r;
        Check(bench.CraftBlock(hockey) != null, "no materials: can't make it");
        RcCharacter.AddTo(bench.Stash, RecalledRules.STASH_SLOTS, RcData.BATTERY, 6);
        RcCharacter.AddTo(bench.Stash, RecalledRules.STASH_SLOTS, RcData.SCREW, 1);
        bench.Add(RcData.SCREW, 2);
        Check(bench.CraftBlock(hockey) == null && bench.Craft(hockey), "a Hockey Stick from 6 batteries in the stash and screws split between bag and stash");
        Check(bench.Have(RcData.BATTERY) == 0 && bench.Have(RcData.SCREW) == 0 && bench.Carried(101) == 1, "it took the backpack's screws first, then the stash's");
        Check(new RcCharacter().MaxHealth == 100f, "a new character has 100 health");
        Check(c.Armor == 50 && System.Math.Abs(c.DamageTaken - 0.5f) < 0.001f, "vault plate gives 50 armor: half damage (" + c.Armor + ")");
        // Save round trip.
        var ms = new MemoryStream();
        c.Write(new BinaryWriter(ms));
        var c2 = RcCharacter.Read(new BinaryReader(new MemoryStream(ms.ToArray())));
        Check(c2 != null && c2.Xp == c.Xp && c2.Backpack.Count == c.Backpack.Count && c2.Stash.Count == c.Stash.Count && c2.Equip[2] == c.Equip[2] && c2.Quest == c.Quest, "save round trip");
        Check(c2.Problem() == null, "round-tripped character valid: " + c2.Problem());
        // Impossible characters are caught.
        var bad = new RcCharacter(); bad.Equip[0] = 104;
        Check(bad.Problem() != null, "level 1 with a sledgehammer is caught");
        bad = new RcCharacter(); bad.BossesBeaten = 4;
        Check(bad.Problem() != null, "level 1 with four bosses is caught");
        bad = new RcCharacter(); bad.Backpack.Add(new RcStack(RcData.SPRING, 9999));
        Check(bad.Problem() != null, "a 9999 stack is caught");
        bad = new RcCharacter(); bad.Backpack.Add(new RcStack(77777, 1));
        Check(bad.Problem() != null, "an item that doesn't exist is caught");
        bad = new RcCharacter(); bad.Equip[RcCharacter.SLOT_ARMOR] = 100;
        Check(bad.Problem() != null, "a bat in the armor slot is caught");
        Check(RcCharacter.Read(new BinaryReader(new MemoryStream(new byte[] { 1, 2, 3 }))) == null, "garbage isn't a character");
    }

    static void SaveFile()
    {
        Console.WriteLine("== save file");
        string dir = Path.Combine(Path.GetTempPath(), "rctest_save_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        SaveFiles.Dir = dir;
        string note;
        var fresh = RecalledSave.Load(out note);
        Check(fresh != null && fresh.Xp == 0 && note == null, "no file: a new character, no warning");

        var c = new RcCharacter();
        c.AddXp(700); c.BossesBeaten = 1; c.Add(RcData.SPRING, 33); c.Add(101, 1); c.Equip[0] = 100; c.Quest = 5; c.Forts = 1;
        Check(c.Problem() == null, "test character is valid: " + c.Problem());
        RecalledSave.Save(c);
        string file = Path.Combine(dir, "Recalled.sav");
        Check(File.Exists(file), "the save file exists");
        byte[] raw = File.ReadAllBytes(file);
        Console.WriteLine("  file is " + raw.Length + " bytes");
        string asText = System.Text.Encoding.ASCII.GetString(raw);
        Check(!asText.Contains("Spring") && !asText.Contains("Xp"), "it isn't readable text");
        var back = RecalledSave.Load(out note);
        Check(back != null && back.Xp == c.Xp && back.Carried(RcData.SPRING) == 33 && back.Equip[0] == 100 && back.Quest == 5 && back.Forts == 1 && note == null, "save then load gives the same character");

        // The Recall Notices you've read, and what the kids said about them, are saved too.
        c.ReadNotice(0); c.ReadNotice(14); c.NoticesTold = 1;
        RecalledSave.Save(c);
        var withNotices = RecalledSave.Load(out note);
        Check(withNotices != null && withNotices.HasNotice(0) && withNotices.HasNotice(14) && !withNotices.HasNotice(1) && withNotices.NoticeCount == 2 && withNotices.NoticesTold == 1, "notices are saved and loaded");
        Check(!withNotices.ZoneNoticesRead(0), "one of three is not a whole zone");
        c.ReadNotice(1); c.ReadNotice(2);
        Check(c.ZoneNoticesRead(0), "all three of a zone");
        c.Notices = 1 << 20;
        Check(c.Problem() != null, "a notice that doesn't exist is refused");
        c.Notices = 0; c.NoticesTold = 0;
        // The Tape: handing in "Take the Tape" leaves it with you, "The Call" takes it, and a lost one comes back.
        var tape = new RcCharacter();
        tape.Quest = 21;
        Check(tape.CurrentQuest.Title == "Take the Tape" && tape.CurrentQuest.Keep, "job 22 is Take the Tape, and it lets you keep it");
        Check(tape.EnsureTape() && tape.Have(RcData.TAPE) == 1 && !tape.EnsureTape(), "a missing Tape is put back, once");
        Check(tape.QuestComplete && tape.TurnIn() >= 0 && tape.Have(RcData.TAPE) == 1, "handing in Take the Tape keeps the Tape");
        Check(tape.CurrentQuest.Title == "The Call" && tape.QuestComplete && tape.TurnIn() >= 0 && tape.Have(RcData.TAPE) == 0, "handing in The Call gives it to Moss");
        Check(tape.CurrentQuest.Kind == RcQuestKind.Scene && !tape.QuestComplete, "then the argument, which is only over when it's over");
        tape.OnScene();
        Check(tape.QuestComplete && tape.TurnIn() >= 0 && tape.CurrentQuest.Kind == RcQuestKind.Station && !tape.QuestComplete, "then the train");
        tape.OnBoarded();
        Check(tape.QuestComplete && tape.TurnIn() >= 0 && tape.CurrentQuest != null && tape.CurrentQuest.World == 1 && tape.CurrentQuest.Title == "The Siege", "boarding ends the town: next is Egypt's first job");

        // Saving twice gives different bytes (a fresh IV), and still loads.
        RecalledSave.Save(c);
        byte[] raw2 = File.ReadAllBytes(file);
        bool same = raw.Length == raw2.Length;
        if (same) for (int i = 0; i < raw.Length; i++) if (raw[i] != raw2[i]) { same = false; break; }
        Check(!same, "two saves of the same character differ (fresh IV)");
        Check(File.Exists(Path.Combine(dir, "Recalled.sav.bak")), "the previous save became the backup");

        // Every single-byte change is caught.
        int caught = 0, tried = 0;
        for (int i = 0; i < raw2.Length; i += 3)
        {
            byte[] t = (byte[])raw2.Clone();
            t[i] ^= 0x01;
            tried++;
            if (RecalledSave.Open(t) == null) caught++;
        }
        Check(caught == tried, "tampering is caught (" + caught + " of " + tried + " single-byte changes)");
        Console.WriteLine("  tampering: " + caught + " of " + tried + " single-byte changes caught");
        Check(RecalledSave.Open(new byte[10]) == null && RecalledSave.Open(null) == null, "garbage doesn't open");

        // Someone else's save doesn't open on your account.
        Steamworks.SteamUser.Id = 76561190000000002UL;
        Check(RecalledSave.Open(raw2) == null, "another account's save doesn't open");
        Steamworks.SteamUser.Id = 76561190000000001UL;
        Check(RecalledSave.Open(raw2) != null, "yours still does");

        // A damaged save falls back to the backup, and says so.
        byte[] bad = (byte[])raw2.Clone(); bad[bad.Length / 2] ^= 0xFF;
        File.WriteAllBytes(file, bad);
        var fromBackup = RecalledSave.Load(out note);
        Check(fromBackup != null && fromBackup.Xp == c.Xp && note != null, "a damaged save loads the backup, with a note: " + note);

        // A save that's signed correctly but impossible (e.g. made by a modified game) is still refused.
        var cheat = new RcCharacter(); cheat.Equip[0] = 104; // a level 13 sledgehammer at level 1
        RecalledSave.Save(cheat);
        File.Delete(Path.Combine(dir, "Recalled.sav.bak"));
        var refused = RecalledSave.Load(out note);
        Check(refused != null && refused.Equip[0] != 104 && note != null, "an impossible character is refused (its slot is not the cheated weapon): " + note);

        // Without Steam (the itch.io version) it still saves and loads, tied to the PC.
        Steamworks.SteamUser.Fail = true;
        RecalledSave.Save(c);
        var noSteam = RecalledSave.Load(out note);
        Check(noSteam != null && noSteam.Xp == c.Xp, "saves and loads without Steam");
        Steamworks.SteamUser.Fail = false;

        // Steam is shut down before the game's last save on the way out: that save must still be signed for the same
        // account (it wasn't, once: it was signed for the PC, and the next start threw it away for the backup).
        RecalledSave.Save(c);
        Steamworks.SteamUser.Fail = true;
        RecalledSave.Save(c);   // the quit-time save, Steam gone
        Steamworks.SteamUser.Fail = false;
        var afterQuit = RecalledSave.Load(out note);
        Check(afterQuit != null && afterQuit.Xp == c.Xp && note == null, "the save made with Steam already gone still loads next time, with no warning (" + note + ")");

        // ...and a save the old version signed for the PC at that moment is recovered, and signed again for the account.
        Steamworks.SteamUser.Id = 76561190000000001UL;
        c.AddXp(50);
        byte[] plain;
        using (var ms = new MemoryStream()) { using (var w = new BinaryWriter(ms)) c.Write(w); plain = ms.ToArray(); }
        File.WriteAllBytes(file, SealForPc(plain));
        bool primary;
        Check(RecalledSave.Open(File.ReadAllBytes(file), out primary) != null && !primary, "a save signed for the PC opens (as the PC's)");
        var recovered = RecalledSave.Load(out note);
        Check(recovered != null && recovered.Xp == c.Xp && note == null, "it's recovered, not replaced by the backup");
        RecalledSave.Open(File.ReadAllBytes(file), out primary);
        Check(primary, "and it was signed again for the account");

        // A new character saved over yours (a load that failed once) doesn't take your progress: the best one ever saved comes back.
        RecalledSave.Save(new RcCharacter());
        var lostThenBack = RecalledSave.Load(out note);
        Check(lostThenBack != null && lostThenBack.Xp == c.Xp && note != null, "a lost save is recovered from the best one: " + note);
        Directory.Delete(dir, true);
    }


    // The game's own numbers, as markdown (for the master document).
    static string ZoneOf(int world, int zone) { int was = RcWorlds.Now; RcWorlds.Now = world; string n = RcData.ZoneNames[zone]; RcWorlds.Now = was; return n; }

    static void Dump(string path)
    {
        var w = new System.Text.StringBuilder();
        string[] kinds = { "Material", "Melee", "Ranged", "Gadget", "Armor", "Trinket", "Mount" };
        w.AppendLine("### Levels and experience");
        w.AppendLine("| Level | Experience to the next | Total to reach it | Health bonus |");
        w.AppendLine("|---|---|---|---|");
        int total = 0;
        for (int lv = 1; lv <= RecalledRules.LEVEL_CAP; lv++)
        {
            w.AppendLine("| " + lv + " | " + (lv < RecalledRules.LEVEL_CAP ? RecalledRules.XpToNext(lv).ToString() : "(cap)") + " | " + total + " | +" + (lv - 1) * (int)RecalledRules.HEALTH_PER_LEVEL + " |");
            total += RecalledRules.XpToNext(lv);
        }
        w.AppendLine();
        w.AppendLine("### Zones and their gates");
        w.AppendLine("| Zone | Gate opens at level | Boss | Boss health | Boss damage |");
        w.AppendLine("|---|---|---|---|---|");
        for (int wd = 0; wd < 1; wd++)
        for (int z = 0; z < 5; z++)
        {
            RcWorlds.Now = wd;
            RcEnemyDef boss = RcData.Enemy(RcData.BossId(wd, z));
            w.AppendLine("| " + RcData.ZoneNames[z] + " (" + RcWorlds.Name(wd) + ") | " + RecalledRules.GateLevelIn(wd, z) + (z > 0 ? " and " + RcData.BossNames[z - 1] + " beaten" : " (always open)") + " | " + RcData.BossNames[z] + " | " + boss.Hp + " | " + boss.Damage + " |");
        }
        RcWorlds.Now = 0;
        w.AppendLine();
        w.AppendLine("### Toys (enemies)");
        w.AppendLine("| Toy | Kind | Zone | Health | Damage | Speed | Experience |");
        w.AppendLine("|---|---|---|---|---|---|---|");
        foreach (RcEnemyDef e in RcData.Enemies) w.AppendLine("| " + e.Name + " | " + e.Kind + " | " + ZoneOf(e.World, e.Zone) + " | " + e.Hp + " | " + e.Damage + " | " + e.Speed + " | " + (e.Kind == RcEnemyKind.Boss ? "a third of a level" : e.Xp.ToString()) + " |");
        w.AppendLine();
        w.AppendLine("Health is for one player; it scales with the group (x1, x1.6, x2.2, x2.8 for 1 to 4 players). Damage never scales. Every toy's damage is multiplied by 0.65 on top (tuned in playtests).");
        w.AppendLine();
        w.AppendLine("### Gear");
        w.AppendLine("| Item | Kind | Level | Power | Rate | Range | Noise | Extra |");
        w.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach (RcItem it in RcData.Items)
        {
            if (it.Kind == RcKind.Material) continue;
            string extra = it.Desc ?? "";
            if (it.Armor > 0) extra += " Armor " + it.Armor + (it.Health > 0 ? ", +" + it.Health + " health" : "") + ".";
            else if (it.Health > 0) extra += " +" + it.Health + " health.";
            w.AppendLine("| " + it.Name + " | " + kinds[(int)it.Kind] + " | " + it.Level + " | " + it.Power + " | " + it.Rate + " | " + it.Range + " | " + it.Noise + " | " + extra.Trim() + " |");
        }
        w.AppendLine();
        w.AppendLine("### Materials");
        foreach (RcItem it in RcData.Items) if (it.Kind == RcKind.Material) w.AppendLine("- **" + it.Name + "**: " + it.Desc);
        w.AppendLine();
        w.AppendLine("### Crafting recipes (at the treehouse bench; it uses your backpack and your stash together)");
        w.AppendLine("| Makes | Needs | Bench tier (bosses beaten) |");
        w.AppendLine("|---|---|---|");
        foreach (RcRecipe r in RcData.Recipes)
        {
            string needs = "";
            for (int i = 0; i < r.Needs.Length; i++) needs += (i > 0 ? ", " : "") + r.Needs[i].Count + " " + RcData.Item(r.Needs[i].Id).Name;
            w.AppendLine("| " + (r.Makes == 0 ? "A bigger backpack (+" + RecalledRules.BACKPACK_STEP + " slots)" : RcData.Item(r.Makes).Name) + " | " + needs + " | " + r.Zone + " |");
        }
        w.AppendLine();
        w.AppendLine("### The jobs (the quest chain)");
        for (int i = 0; i < RcData.Quests.Count; i++)
        {
            RcQuest q = RcData.Quests[i];
            string reward = q.Kind == RcQuestKind.Boss ? "what it drops" : q.Xp + " experience";
            for (int k = 0; k < q.Reward.Length; k++) reward += ", " + q.Reward[k].Count + " " + RcData.Item(q.Reward[k].Id).Name;
            w.AppendLine("#### " + (i + 1) + ". " + q.Title + "  (" + q.Kind + ", " + ZoneOf(q.World, q.Zone) + (q.Count > 1 ? ", " + q.Count : "") + ")");
            w.AppendLine("- Board: \"" + q.Text + "\"");
            w.AppendLine("- Done: \"" + q.Done + "\"");
            w.AppendLine("- Pays: " + reward);
            if (q.Steps != null) { w.AppendLine("- How:"); for (int k = 0; k < q.Steps.Length; k++) w.AppendLine("  " + (k + 1) + ". " + q.Steps[k]); }
            w.AppendLine();
        }
        System.IO.File.WriteAllText(path, w.ToString(), new System.Text.UTF8Encoding(false));
        Console.WriteLine("wrote " + path);
    }

    // Playing with a friend: you work on THEIR job, and your own job is never touched.
    static void Together()
    {
        var me = new RcCharacter(); me.Xp = 700; me.Quest = 2; me.QuestProgress = 1;
        me.SetSession(5);   // the host is on job 5 (Shoplifters: break 12 toys in the mall)
        Check(me.InSession && me.ActiveQuest == 5 && me.ActiveProgress == 0 && me.Quest == 2 && me.QuestProgress == 1, "in a friend's town you're on their job, with your own left alone");
        RcEnemyDef teal = RcData.Enemy(RcData.E_TEAL);
        for (int i = 0; i < 12; i++) me.OnKill(teal);
        Check(me.ActiveProgress == 12 && me.QuestProgress == 1 && me.QuestComplete, "kills count toward their job, not yours");
        int xp = me.TurnIn();
        Check(xp >= 0 && me.Quest == 2 && me.QuestProgress == 1 && !me.QuestComplete, "you hand it in for your own pay and your job stays where it was (xp " + xp + ")");
        me.ClearSession();
        Check(!me.InSession && me.ActiveQuest == 2 && me.ActiveProgress == 1, "leave, and your own job is right where you left it");
        // A job you've done already: you help, nothing to hand in.
        var ahead = new RcCharacter(); ahead.Xp = 2000; ahead.Quest = 7;
        ahead.SetSession(3);
        Check(ahead.InSession && !ahead.QuestComplete, "a friend on a job you've done: you help, no hand-in");
        // The same job as yours is just yours.
        var same = new RcCharacter(); same.Quest = 4; same.SetSession(4);
        Check(!same.InSession && same.ActiveQuest == 4, "the same job as yours isn't a separate one");
    }

    // Signs bytes for this PC, as the first version did when Steam had already gone.
    static byte[] SealForPc(byte[] plain)
    {
        using (var sha = System.Security.Cryptography.SHA256.Create())
        {
            byte[] baseKey = System.Text.Encoding.UTF8.GetBytes(RecalledSecret.Value + "|pc:" + UnityEngine.SystemInfo.deviceUniqueIdentifier);
            byte[] one = new byte[baseKey.Length + 1], two = new byte[baseKey.Length + 1];
            Buffer.BlockCopy(baseKey, 0, one, 0, baseKey.Length); one[baseKey.Length] = 1;
            Buffer.BlockCopy(baseKey, 0, two, 0, baseKey.Length); two[baseKey.Length] = 2;
            byte[] aesKey = sha.ComputeHash(one), macKey = sha.ComputeHash(two);
            using (var aes = System.Security.Cryptography.Aes.Create())
            {
                aes.Key = aesKey; aes.Mode = System.Security.Cryptography.CipherMode.CBC; aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;
                aes.GenerateIV();
                byte[] cipher;
                using (var enc = aes.CreateEncryptor()) cipher = enc.TransformFinalBlock(plain, 0, plain.Length);
                var body = new byte[4 + 16 + cipher.Length];
                body[0] = (byte)'R'; body[1] = (byte)'C'; body[2] = (byte)'L'; body[3] = (byte)'1';
                Buffer.BlockCopy(aes.IV, 0, body, 4, 16);
                Buffer.BlockCopy(cipher, 0, body, 20, cipher.Length);
                using (var h = new System.Security.Cryptography.HMACSHA256(macKey))
                {
                    byte[] mac = h.ComputeHash(body);
                    var all = new byte[body.Length + 32];
                    Buffer.BlockCopy(body, 0, all, 0, body.Length); Buffer.BlockCopy(mac, 0, all, body.Length, 32);
                    return all;
                }
            }
        }
    }

    static float TrinketHealth(RcCharacter c)
    {
        float t = 0f;
        for (int i = 3; i < 5; i++) { var it = RcData.Item(c.Equip[i]); if (it != null) t += it.Health; }
        return t;
    }

    static RcEnemyDef FirstStray(int zone)
    {
        foreach (var e in RcData.Enemies) if (e.World == RcWorlds.Now && e.Zone == zone && e.Kind == RcEnemyKind.Stray) return e;
        return null;
    }

    static void Town(string outDir)
    {
        for (int world = 0; world < 1; world++)
        {
            string dir = world == 0 ? outDir : Path.Combine(outDir, "egypt");
            Directory.CreateDirectory(dir);
            TownWorld(dir, world);
        }
        RcWorlds.Now = 0;
    }

    static void TownWorld(string outDir, int world)
    {
        RcWorlds.Now = world;
        bool egypt = world == 1;
        Console.WriteLine("== " + RcWorlds.Name(world).ToLower());
        var nav = RcNav.Instance;
        int free = 0;
        for (int cz = 0; cz < nav.H; cz++) for (int cx = 0; cx < nav.W; cx++) if (nav.Free(cx, cz)) free++;
        Console.WriteLine("  grid " + nav.W + " x " + nav.H + ", " + free + " free ground cells, " + (nav.SpotCount - nav.Cells) + " spots on floors and stairs; " + RcTown.Spawns.Count + " spawns, " + RcTown.Crates.Count + " scrap piles");

        var map = new MapSink();
        for (int part = RcTown.PART_CORE; part < 5; part++)
        {
            int before = map.Boxes, lb = map.Lights.Count, cb = map.Colliders;
            RcTown.Build(map, part);
            Console.WriteLine("  part " + part + ": " + (map.Boxes - before) + " shapes (" + (map.Colliders - cb) + " colliders), " + (map.Lights.Count - lb) + " lights");
        }

        // 1. With every gate open, everything must be reachable from where you arrive.
        bool[] seen = nav.Flood(RcTown.Arrival[0], RcTown.Arrival[1]);
        if (Environment.GetEnvironmentVariable("RC_DEBUG") != null)
            foreach (float[] col in new[] { new[] { 224.5f, -57.5f, -40.5f, 0f }, new[] { 225.5f, -110.5f, -94.5f, 0f }, new[] { -209.5f, 57.5f, 70.5f, 0f }, new[] { -256.5f, 31.5f, 44.5f, -4.5f } })
                for (float z = col[1]; z <= col[2]; z += 1f)
                {
                    string line = "  dbg " + col[0] + "," + z + ":";
                    for (float y = col[3] - 0.3f; y < col[3] + 5f; y += 0.15f)
                    {
                        int s = nav.SpotAt(col[0], y, z, 0.08f);
                        if (s >= 0) line += " " + nav.SpotY(s).ToString("0.00") + (seen[s] ? "+" : "-");
                    }
                    Console.WriteLine(line);
                }
        Check(nav.FreeAt(RcTown.Arrival[0], RcTown.Arrival[1]), "the arrival spot is free");
        Reach(nav, seen, 0f, 0f, -3.5f, 1, "the treehouse ladder's foot");
        for (int z = 0; z < 5; z++)
        {
            Reach(nav, seen, RcTown.GateSides[z, 0], 0f, RcTown.GateSides[z, 1], 0, "outside gate " + z);
            Reach(nav, seen, RcTown.GateSides[z, 2], 0f, RcTown.GateSides[z, 3], 0, "inside gate " + z);
            if (z == 4 && !egypt) continue; // the warehouse is the Waygetter Facility: its insides are measured in the game, not here
            Reach(nav, seen, RcTown.Arena[z, 0], 0f, RcTown.Arena[z, 1], 2, "arena " + z);
            // An arena needs room to fight in.
            int open = 0, cells = 0;
            for (float x = RcTown.Arena[z, 0] - RcTown.Arena[z, 2]; x <= RcTown.Arena[z, 0] + RcTown.Arena[z, 2]; x += 1f)
                for (float zz = RcTown.Arena[z, 1] - RcTown.Arena[z, 2]; zz <= RcTown.Arena[z, 1] + RcTown.Arena[z, 2]; zz += 1f)
                {
                    float dx = x - RcTown.Arena[z, 0], dz = zz - RcTown.Arena[z, 1];
                    if (dx * dx + dz * dz > RcTown.Arena[z, 2] * RcTown.Arena[z, 2]) continue;
                    cells++; if (nav.FreeAt(x, zz) && seen[nav.CellZ(zz) * nav.W + nav.CellX(x)]) open++;
                }
            Console.WriteLine("  arena " + z + ": " + (100 * open / cells) + "% open");
            Check(open > cells * (egypt && z == 4 ? 0.4f : 0.55f), "arena " + z + " is mostly open floor");
        }
        for (int f = 0; f < 4; f++) Reach(nav, seen, RcTown.Fort[f, 0], 0f, RcTown.Fort[f, 1], 1, "fort " + f);
        Reach(nav, seen, RcTown.StationX, 0f, RcTown.StationZ, 1, "the Recall Express platform");
        int[] perZone = new int[5], enemiesPerZone = new int[5];
        foreach (var s in RcTown.Spawns)
        {
            // (what's inside the Facility is checked in the game, against the real building: see the log)
            if (!RcTown.InFacility(s.X, s.Z)) Reach(nav, seen, s.X, s.Y, s.Z, 1, "spawn of " + RcData.Enemy(s.Enemy).Name + " at " + s.X + "," + s.Y + "," + s.Z);
            Check(RcTown.ZoneAt(s.X, s.Z) == s.Zone, "spawn at " + s.X + "," + s.Z + " is inside zone " + s.Zone);
            Check(RcData.Enemy(s.Enemy).Zone == s.Zone || RcData.Enemy(s.Enemy).Kind != RcEnemyKind.Stray || s.Zone >= RcData.Enemy(s.Enemy).Zone, "spawn enemy zone");
            perZone[s.Zone]++; enemiesPerZone[s.Zone] += s.Count;
        }
        int[] cratesPerZone = new int[5];
        foreach (var c in RcTown.Crates)
        {
            // A pile is solid: you stand next to it.
            bool near = RcTown.InFacility(c.X, c.Z) || nav.Reached(seen, c.X, c.Y, c.Z, 2);
            Check(near, "scrap pile at " + c.X + "," + c.Y + "," + c.Z + " can be reached");
            Check(RcTown.ZoneAt(c.X, c.Z) == c.Zone, "scrap pile at " + c.X + "," + c.Z + " is inside zone " + c.Zone);
            cratesPerZone[c.Zone]++;
        }
        for (int z = 0; z < 5; z++) Console.WriteLine("  zone " + z + ": " + perZone[z] + " spawn points, " + enemiesPerZone[z] + " enemies, " + cratesPerZone[z] + " scrap piles");
        // Quest elites must exist where their quests send you.
        foreach (var q in RcData.Quests)
            if (q.Kind == RcQuestKind.Elite && q.World == world)
            {
                int n = 0;
                foreach (var s in RcTown.Spawns) if (s.Enemy == q.Target && s.Zone == q.Zone) n += s.Count;
                Check(n >= q.Count, "quest '" + q.Title + "' needs " + q.Count + " of " + RcData.Enemy(q.Target).Name + ", the zone spawns " + n);
            }

        // 2. With a zone's gate shut, nothing inside it can be reached: the fence has no holes.
        for (int z = 1; z < 5; z++)
        {
            var saved = (bool[])nav.Blocked.Clone();
            for (float x = RcTown.Gate[z, 0] - 1f; x <= RcTown.Gate[z, 2] + 1f; x += 0.5f)
                for (float zz = RcTown.Gate[z, 1] - 1f; zz <= RcTown.Gate[z, 3] + 1f; zz += 0.5f)
                    nav.Blocked[nav.CellZ(zz) * nav.W + nav.CellX(x)] = true;
            bool[] shut = nav.Flood(RcTown.Arrival[0], RcTown.Arrival[1]);
            Check(!shut[nav.CellZ(RcTown.GateSides[z, 3]) * nav.W + nav.CellX(RcTown.GateSides[z, 2])], "zone " + z + " is sealed when its gate is shut");
            int leaked = 0;
            for (int cz = 0; cz < nav.H; cz++)
                for (int cx = 0; cx < nav.W; cx++)
                    if (shut[cz * nav.W + cx] && RcTown.ZoneAt(nav.CenterX(cx), nav.CenterZ(cz)) == z && RcTown.ZoneDistance(z, nav.CenterX(cx), nav.CenterZ(cz)) == 0f
                        && nav.CenterX(cx) > RcTown.Zone[z, 0] + 1f && nav.CenterX(cx) < RcTown.Zone[z, 2] - 1f && nav.CenterZ(cz) > RcTown.Zone[z, 1] + 1f && nav.CenterZ(cz) < RcTown.Zone[z, 3] - 1f) leaked++;
            Check(leaked == 0, "zone " + z + " leaks " + leaked + " cells with its gate shut");
            System.Array.Copy(saved, nav.Blocked, saved.Length);
        }
        // Zones don't overlap each other, the park or the streets' ends.
        for (int a = 0; a < 5; a++)
            for (int b = a + 1; b < 5; b++)
                Check(RcTown.Zone[a, 2] < RcTown.Zone[b, 0] || RcTown.Zone[b, 2] < RcTown.Zone[a, 0] || RcTown.Zone[a, 3] < RcTown.Zone[b, 1] || RcTown.Zone[b, 3] < RcTown.Zone[a, 1], "zones " + a + " and " + b + " overlap");

        // 3. Paths: from the arrival to the treehouse and every arena.
        var path = new List<float>();
        Check(nav.FindPath(RcTown.Arrival[0], RcTown.Arrival[1], 0f, -3.5f, 400000, path), "path arrival -> treehouse");
        Console.WriteLine("  arrival -> treehouse: " + Len(path, RcTown.Arrival[0], RcTown.Arrival[1]).ToString("0") + " m, " + path.Count / 2 + " points");
        for (int z = 0; z < 5; z++)
        {
            if (z == 4 && !egypt) continue;
            bool ok = nav.FindPath(0f, -3.5f, RcTown.Arena[z, 0], RcTown.Arena[z, 1], 400000, path);
            Check(ok, "path treehouse -> arena " + z);
            if (ok) Console.WriteLine("  treehouse -> " + RcData.BossNames[z] + ": " + Len(path, 0f, -3.5f).ToString("0") + " m");
        }
        // The quest arrow: from anywhere, walking where it points gets you to the job. (From every spawn: to a boss, to an upstairs, to a basement, to the treehouse.)
        var clock = System.Diagnostics.Stopwatch.StartNew();
        float[][] goals = egypt ? new[] { new[] { RcTown.Arena[3, 0], 0f, RcTown.Arena[3, 1] }, new[] { RcTown.Arena[4, 0], 0f, RcTown.Arena[4, 1] }, new[] { 0f, 0f, -3.6f } }
            : new[] { new[] { RcTown.Arena[3, 0], 0f, RcTown.Arena[3, 1] }, new[] { 245f, 4.5f, -79f }, new[] { -236f, -4.5f, 50f }, new[] { 0f, 0f, -3.6f } };
        foreach (float[] goal in goals)
        {
            var field = new RcField(nav);
            Check(field.Begin(goal[0], goal[1], goal[2]), "the arrow's goal " + goal[0] + "," + goal[1] + "," + goal[2] + " is somewhere");
            int frames = 0;
            while (!field.Done) { field.Work(5000); frames++; }
            int lost = 0, walked = 0;
            foreach (var from in RcTown.Spawns)
            {
                if (RcTown.InFacility(from.X, from.Z)) continue;
                float x = from.X, y = from.Y, z = from.Z, walk = 0f;
                bool there = false;
                for (int hop = 0; hop < 400 && !there; hop++)
                {
                    float fx, fy, fz;
                    if (!field.Toward(x, y, z, 9, out fx, out fy, out fz, out walk)) break;
                    if (walk < 1.5f) there = true;
                    x = fx; y = fy; z = fz;
                }
                walked++;
                if (!there) lost++;
            }
            Console.WriteLine("  arrow to " + goal[0] + "," + goal[1] + "," + goal[2] + ": ready in " + frames + " frames; followed from " + walked + " places, " + lost + " lost");
            Check(lost == 0, "following the arrow to " + goal[0] + "," + goal[1] + "," + goal[2] + " always arrives");
        }
        Console.WriteLine("  (" + clock.ElapsedMilliseconds + " ms for all four)");

        // A short chase, the way an enemy would ask (a small search budget).
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int found = 0;
        if (!egypt)
        {
            for (int i = 0; i < 200; i++) if (nav.FindPath(-139f, -122f, -95f - (i % 20), -122f - (i % 7) * 4f, 3000, path)) found++;
            Console.WriteLine("  200 short paths (3000-cell budget): " + found + " found, " + sw.ElapsedMilliseconds + " ms");
            Check(found >= 190, "short chases find a path");
        }

        // 4. Every job points somewhere you can walk to (from outside its zone and from inside), and says how it's done.
        for (int i = 0; i < RcData.Quests.Count; i++)
        {
            RcQuest q = RcData.Quests[i];
            if (q.World != world) continue;
            Check(q.Steps != null && q.Steps.Length >= 3, "quest '" + q.Title + "' has its steps written out");
            var who = new RcCharacter { Quest = i };
            for (int pass = 0; pass < 2; pass++)
            {
                float px = pass == 0 ? RcTown.Arrival[0] : RcTown.GateSides[q.Zone, 2], pz = pass == 0 ? RcTown.Arrival[1] : RcTown.GateSides[q.Zone, 3];
                float x, y, z; string name;
                Check(RcGuide.Target(who, px, pz, out x, out y, out z, out name), "quest '" + q.Title + "' has a place to go");
                if (!egypt && q.Zone == 4 && q.Kind != RcQuestKind.Reach) continue; // (the warehouse's insides are the Facility's)
                Check(nav.Reached(seen, x, y, z, 3), "quest '" + q.Title + "' points at " + name + " (" + x + "," + y + "," + z + "), which can be reached");
                if (pass == 0 && i % 4 == 0) Console.WriteLine("  '" + q.Title + "' from the Arcade: " + RcGuide.Where(who, px, 0f, pz));
            }
        }

        Draw(map, nav, seen, outDir);
    }

    static float Len(List<float> path, float x, float z)
    {
        float t = 0f;
        for (int i = 0; i + 1 < path.Count; i += 2) { t += (float)Math.Sqrt((path[i] - x) * (path[i] - x) + (path[i + 1] - z) * (path[i + 1] - z)); x = path[i]; z = path[i + 1]; }
        return t;
    }

    static void Reach(RcNav nav, bool[] seen, float x, float y, float z, int slack, string what)
    {
        float fx, fy, fz;
        if (!nav.Nearest(x, y, z, slack, out fx, out fy, out fz)) { Check(false, what + " (" + x + "," + y + "," + z + ") has nowhere to stand"); return; }
        Check(nav.Reached(seen, x, y, z, slack), what + " (" + x + "," + y + "," + z + ") can be reached on foot");
    }

    static readonly Dictionary<RcMat, int> Colors = new Dictionary<RcMat, int>
    {
        { RcMat.Grass, 0x24402a }, { RcMat.Lawn, 0x2f5a35 }, { RcMat.Road, 0x3a3a40 }, { RcMat.Line, 0xb8a860 }, { RcMat.Walk, 0x6a6a70 }, { RcMat.Path, 0x8a7a60 }, { RcMat.Dirt, 0x5a4a38 },
        { RcMat.Bark, 0x3a2615 }, { RcMat.Leaf, 0x1f4a24 }, { RcMat.Wood, 0x8a6038 }, { RcMat.Plank, 0x9a7a4a }, { RcMat.Metal, 0x70747c }, { RcMat.Rust, 0x7a4528 }, { RcMat.Stone, 0x8a8a90 }, { RcMat.Concrete, 0x9a9a9a },
        { RcMat.Brick, 0x8a4538 }, { RcMat.House, 0x6a6a78 }, { RcMat.HouseB, 0x5a6a5a }, { RcMat.HouseC, 0x7a6a58 }, { RcMat.Roof, 0x2a2426 }, { RcMat.Door, 0x4a3020 }, { RcMat.Window, 0x203040 }, { RcMat.Floor, 0x807868 }, { RcMat.Tile, 0xb0b0a8 }, { RcMat.Carpet, 0x6a3a3a },
        { RcMat.Glow, 0xffd070 }, { RcMat.Neon, 0xff4aa0 }, { RcMat.RedGlow, 0xff3020 }, { RcMat.BlueGlow, 0x50a0ff }, { RcMat.Tape, 0xe0a000 },
        { RcMat.CarA, 0xc0a020 }, { RcMat.CarB, 0x3060a0 }, { RcMat.CarC, 0xa03030 }, { RcMat.Glass, 0x88aabb }, { RcMat.Shelf, 0x556070 }, { RcMat.Crate, 0xa08050 }, { RcMat.Fabric, 0x705080 }, { RcMat.Black, 0x101014 },
        { RcMat.Paper, 0xd0c8a8 }, { RcMat.Cardboard, 0x805a38 }, { RcMat.Orange, 0xe05010 }, { RcMat.Pink, 0xd0708a }, { RcMat.White, 0xc0c4cc }, { RcMat.Teal, 0x208080 },
        { RcMat.Sand, 0xb89a60 }, { RcMat.Sandstone, 0xcfae78 }, { RcMat.Adobe, 0xa87850 }, { RcMat.Gilt, 0xe8c030 }, { RcMat.Lapis, 0x1838a0 }, { RcMat.Canvas, 0xd8c8a0 },
    };

    // Top-down: what you'd see from above with the roofs lifted off (anything above 2.7 m is skipped unless it's a
    // landmark-sized thing), so interiors show.
    static void Draw(MapSink map, RcNav nav, bool[] seen, string outDir)
    {
        DrawRegion(map, nav, seen, Path.Combine(outDir, "map.ppm"), RcTown.X0 - 4f, RcTown.Z0 - 4f, RcTown.X1 + 4f, RcTown.Z1 + 4f, 3f, 0f);
        string[] names = RcWorlds.Egypt ? new[] { "plateau", "dig", "village", "antennas", "pyramid" } : new[] { "suburbs", "mall", "school", "junkyard", "warehouse" };
        for (int z = 0; z < 5; z++)
            DrawRegion(map, nav, seen, Path.Combine(outDir, "zone_" + names[z] + ".ppm"), RcTown.Zone[z, 0] - 6f, RcTown.Zone[z, 1] - 6f, RcTown.Zone[z, 2] + 6f, RcTown.Zone[z, 3] + 6f, 8f, 0f);
        if (!RcWorlds.Egypt)
        {
            DrawRegion(map, nav, seen, Path.Combine(outDir, "zone_mall_upstairs.ppm"), RcTown.Zone[1, 0] - 6f, RcTown.Zone[1, 1] - 6f, RcTown.Zone[1, 2] + 6f, RcTown.Zone[1, 3] + 6f, 8f, RcTown.FLOOR);
            DrawRegion(map, nav, seen, Path.Combine(outDir, "zone_school_upstairs.ppm"), RcTown.Zone[2, 0] - 6f, RcTown.Zone[2, 1] - 6f, RcTown.Zone[2, 2] + 6f, RcTown.Zone[2, 3] + 6f, 8f, RcTown.FLOOR);
            DrawRegion(map, nav, seen, Path.Combine(outDir, "zone_school_basement.ppm"), RcTown.Zone[2, 0] - 6f, RcTown.Zone[2, 1] - 6f, RcTown.Zone[2, 2] + 6f, RcTown.Zone[2, 3] + 6f, 8f, -RcTown.FLOOR);
        }
        DrawRegion(map, nav, seen, Path.Combine(outDir, "zone_park.ppm"), -75f, -75f, 75f, 75f, 8f, 0f);
        if (!RcWorlds.Egypt) DrawRegion(map, nav, seen, Path.Combine(outDir, "zone_arrival.ppm"), -40f, -226f, 110f, -110f, 8f, 0f);
    }

    // level: the floor height to draw (0 the ground, 4.5 upstairs, -4.5 a basement): what stands on that floor, from above.
    static void DrawRegion(MapSink map, RcNav nav, bool[] seen, string file, float x0, float z0, float x1, float z1, float scale, float level)
    {
        int w = (int)((x1 - x0) * scale), h = (int)((z1 - z0) * scale);
        var rgb = new byte[w * h * 3];
        var top = new float[w * h];
        for (int i = 0; i < top.Length; i++) top[i] = -100f;
        foreach (var p in map.Prims)
        {
            if (p.X1 < x0 || p.X0 > x1 || p.Z1 < z0 || p.Z0 > z1) continue;
            // Lift the roofs off: skip roofs and leaves, and ceilings-high slabs.
            if (p.M == RcMat.Roof || p.M == RcMat.Leaf) continue;
            if (p.Y0 >= level + 2.55f) continue;   // lintels, the floor above, anything overhead
            if (p.Y1 < level - 0.45f) continue;    // anything wholly under this floor
            if (level > 1f && p.Y1 < level - 0.05f && p.Y0 <= 0.01f && p.M != RcMat.None && (p.X1 - p.X0) * (p.Z1 - p.Z0) > 30000f) continue; // (not the ground, seen from upstairs)
            int col;
            if (!Colors.TryGetValue(p.M, out col)) col = 0xff00ff;
            int px0 = (int)Math.Floor((p.X0 - x0) * scale), px1 = (int)Math.Ceiling((p.X1 - x0) * scale), pz0 = (int)Math.Floor((p.Z0 - z0) * scale), pz1 = (int)Math.Ceiling((p.Z1 - z0) * scale);
            if (px1 == px0) px1++; if (pz1 == pz0) pz1++;
            float cx = (p.X0 + p.X1) * 0.5f, cz = (p.Z0 + p.Z1) * 0.5f, rx = (p.X1 - p.X0) * 0.5f, rz = (p.Z1 - p.Z0) * 0.5f;
            for (int pz = Math.Max(0, pz0); pz < Math.Min(h, pz1); pz++)
                for (int px = Math.Max(0, px0); px < Math.Min(w, px1); px++)
                {
                    if (p.Round)
                    {
                        float wx = x0 + (px + 0.5f) / scale - cx, wz = z0 + (pz + 0.5f) / scale - cz;
                        if (wx * wx / (rx * rx) + wz * wz / (rz * rz) > 1f) continue;
                    }
                    int i = (h - 1 - pz) * w + px;
                    if (p.Y1 < top[i]) continue;
                    top[i] = p.Y1;
                    rgb[i * 3] = (byte)(col >> 16); rgb[i * 3 + 1] = (byte)(col >> 8); rgb[i * 3 + 2] = (byte)col;
                }
        }
        // Lights as soft warm discs; unreachable free ground tinted red (it should only be inside solid things).
        foreach (var l in map.Lights) Disc(rgb, w, h, (l[0] - x0) * scale, (l[1] - z0) * scale, Math.Max(2f, scale * 0.6f), l[2] == 3 ? 0xff5040 : l[2] == 1 ? 0xff60b0 : l[2] == 2 ? 0x80b0ff : l[2] == 4 ? 0x90ff90 : 0xffe090);
        for (int pz = 0; pz < h; pz++)
            for (int px = 0; px < w; px++)
            {
                float wx = x0 + (px + 0.5f) / scale, wz = z0 + (pz + 0.5f) / scale;
                int spot = nav.SpotAt(wx, level, wz, level == 0f ? 0.1f : 2.2f);
                int i = (h - 1 - pz) * w + px;
                if (spot >= 0 && !seen[spot]) { rgb[i * 3] = 255; rgb[i * 3 + 1] = (byte)(rgb[i * 3 + 1] / 3); rgb[i * 3 + 2] = (byte)(rgb[i * 3 + 2] / 3); }
                else if (level != 0f && spot < 0) { rgb[i * 3] = (byte)(rgb[i * 3] / 4); rgb[i * 3 + 1] = (byte)(rgb[i * 3 + 1] / 4); rgb[i * 3 + 2] = (byte)(rgb[i * 3 + 2] / 4); } // no floor here on this level
            }
        // Markers: spawns (red dots, bosses big), scrap piles (yellow), forts (cyan), gates (white), arenas (ring).
        foreach (var s in RcTown.Spawns)
        {
            if (Math.Abs(s.Y - level) > 2f) continue;
            var e = RcData.Enemy(s.Enemy);
            Disc(rgb, w, h, (s.X - x0) * scale, (s.Z - z0) * scale, e.Kind == RcEnemyKind.Boss ? scale * 2.2f : e.Elite ? scale * 1.2f : scale * 0.8f, e.Kind == RcEnemyKind.Boss ? 0xff0000 : e.Elite ? 0xff8000 : 0xff4060);
        }
        foreach (var c in RcTown.Crates) if (Math.Abs(c.Y - level) < 2f) Disc(rgb, w, h, (c.X - x0) * scale, (c.Z - z0) * scale, scale * 0.6f, 0xffff00);
        if (level == 0f) for (int f = 0; f < 4; f++) Disc(rgb, w, h, (RcTown.Fort[f, 0] - x0) * scale, (RcTown.Fort[f, 1] - z0) * scale, scale * 1.1f, 0x00ffff);
        for (int z = 0; z < 5 && level == 0f; z++)
        {
            Ring(rgb, w, h, (RcTown.Arena[z, 0] - x0) * scale, (RcTown.Arena[z, 1] - z0) * scale, RcTown.Arena[z, 2] * scale, 0xff2020);
            for (float t = 0f; t <= 1f; t += 0.02f)
                Disc(rgb, w, h, (RcTown.Gate[z, 0] + (RcTown.Gate[z, 2] - RcTown.Gate[z, 0]) * t - x0) * scale, (RcTown.Gate[z, 1] + (RcTown.Gate[z, 3] - RcTown.Gate[z, 1]) * t - z0) * scale, Math.Max(1.5f, scale * 0.35f), 0xffffff);
        }
        Disc(rgb, w, h, (RcTown.Arrival[0] - x0) * scale, (RcTown.Arrival[1] - z0) * scale, scale * 1.2f, 0x00ff00);
        using (var f = new FileStream(file, FileMode.Create))
        {
            byte[] head = System.Text.Encoding.ASCII.GetBytes("P6\n" + w + " " + h + "\n255\n");
            f.Write(head, 0, head.Length);
            f.Write(rgb, 0, rgb.Length);
        }
    }

    static void Disc(byte[] rgb, int w, int h, float cx, float cz, float r, int col)
    {
        for (int pz = (int)(cz - r); pz <= (int)(cz + r); pz++)
            for (int px = (int)(cx - r); px <= (int)(cx + r); px++)
            {
                if (px < 0 || pz < 0 || px >= w || pz >= h) continue;
                if ((px - cx) * (px - cx) + (pz - cz) * (pz - cz) > r * r) continue;
                int i = (h - 1 - pz) * w + px;
                rgb[i * 3] = (byte)(col >> 16); rgb[i * 3 + 1] = (byte)(col >> 8); rgb[i * 3 + 2] = (byte)col;
            }
    }

    static void Ring(byte[] rgb, int w, int h, float cx, float cz, float r, int col)
    {
        for (float a = 0f; a < 6.2832f; a += 0.5f / r)
        {
            int px = (int)(cx + Math.Cos(a) * r), pz = (int)(cz + Math.Sin(a) * r);
            if (px < 0 || pz < 0 || px >= w || pz >= h) continue;
            int i = (h - 1 - pz) * w + px;
            rgb[i * 3] = (byte)(col >> 16); rgb[i * 3 + 1] = (byte)(col >> 8); rgb[i * 3 + 2] = (byte)col;
        }
    }
}

class TallSink : RcSink
{
    bool _tr;
    public override void Box(RcMat m, float x0, float y0, float z0, float x1, float y1, float z1, bool collide)
    {
        if (collide && x0 <= -165f && x1 >= -165f && z0 <= -153f && z1 >= -153f) Console.WriteLine("  SOLID at sphinx: " + m + " y " + y0 + ".." + y1 + " x " + x0 + ".." + x1);
        if (!collide && y1 >= 2.4f && (x1 - x0 > 5f || z1 - z0 > 5f) && x1 > -215f && x0 < -100f && z1 > -215f && z0 < -95f) Console.WriteLine("  " + m + " x " + x0 + ".." + x1 + " z " + z0 + ".." + z1 + " y " + y0 + ".." + y1);
    }
    public override void Walk(RcMat m, float x0, float y0, float z0, float x1, float y1, float z1) { }
    public override void Cyl(RcMat m, float x, float y0, float z, float radius, float height, bool collide) { }
    public override void Ball(RcMat m, float x, float y, float z, float rx, float ry, float rz) { if (y + ry >= 5f) Console.WriteLine("  ball " + m + " at " + x + "," + y + "," + z + " r " + rx + "," + ry + "," + rz); }
    public override void Light(float x, float y, float z, int color, float range, float intensity) { }
}
