using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Everyone who talks in the town, as text in the box at the bottom of the screen: the kids when you get back to the
    /// treehouse, Waygetter's announcements, the toys, the bosses. It only watches what the game is doing (where you are,
    /// what the bosses are up to) and says the right line from RcVoiceData, now and then: silence is part of the town.
    /// </summary>
    public class RcVoice : MonoBehaviour
    {
        public static RcVoice I;

        private static readonly string[] Kids = { "JUN", "THE", "MOS" };
        private static readonly string[] Bosses = { "PAT", "DSP", "HAL", "PAP", "MAM" };
        private static readonly string[] ZoneLine = { "PA-ZONE-SUB", "PA-ZONE-MALL", "PA-ZONE-SCH", "PA-ZONE-JNK", "PA-ZONE-FAC" };
        private static readonly string[] NoteTag = { "SUB", "MALL", "SCH", "JNK", "FAC" };
        private static readonly string[] FortLine = { "FORT-SUB", "FORT-MALL", "FORT-SCH", "FORT-JNK" };
        private static readonly Dictionary<string, int> _lastPick = new Dictionary<string, int>();

        private bool _init, _onDeck, _firstSaid, _sirenDone, _healSaid, _jobDone;
        private int _lvlSeen, _bossSeen, _zoneNow = -2;
        private float _t0, _leftAt = -999f, _diedAt = -999f, _idleAt, _paAt, _parkAt, _facAt, _toyAt, _mamaAt, _quietUntil, _facTime, _scanAt, _jobAt;
        private readonly bool[] _zoneSaid = new bool[5], _afterSaid = new bool[5], _fortSaid = new bool[4];
        private readonly Dictionary<string, float> _menuAt = new Dictionary<string, float>();
        private readonly Dictionary<RcEnemy, RcEnemy.St> _was = new Dictionary<RcEnemy, RcEnemy.St>();

        // the boss being fought
        private RcEnemy _boss;
        private int _phase;
        private bool _low;
        private float _attackAt, _prevTele = -1f;

        // ── Saying things ────────────────────────────────────────────────────
        /// <summary>One line by its ID. keep: it's still worth saying if it has to wait its turn.</summary>
        public static void Say(string id, bool keep)
        {
            if (Rc.Hud == null || !RcVoiceData.Has(id)) return;
            RcSay s = RcVoiceData.Line(id);
            Rc.Hud.Sub(s.Who, s.Text, keep);
        }

        /// <summary>A line from a group (JUN-ARR is JUN-ARR-1, JUN-ARR-2...), never the one it said last.</summary>
        public static void SayAny(string group, bool keep)
        {
            List<string> g = RcVoiceData.Group(group);
            if (g.Count == 0) return;
            int i = Random.Range(0, g.Count), last;
            if (g.Count > 1 && _lastPick.TryGetValue(group, out last) && i == last) i = (i + 1) % g.Count;
            _lastPick[group] = i;
            Say(g[i], keep);
        }

        /// <summary>Every line of a group, in order (a boss's first words).</summary>
        private static void SayAll(string group, bool keep)
        {
            List<string> g = RcVoiceData.Group(group);
            for (int i = 0; i < g.Count; i++) Say(g[i], keep);
        }

        private bool Quiet { get { return Rc.Hud != null && !Rc.Hud.SubBusy && Time.time >= _quietUntil; } }

        /// <summary>You handed a job in: one of the kids says something when you're back at it.</summary>
        public void JobDone() { _jobDone = true; _jobAt = Time.time + 0.5f; }

        /// <summary>You used the bench or the stash: now and then, a kid has an opinion.</summary>
        public static void Menu(string what)
        {
            if (I == null || Rc.Hud == null) return;
            float at;
            if (I._menuAt.TryGetValue(what, out at) && Time.time < at) return;
            I._menuAt[what] = Time.time + 150f;
            if (Random.value > 0.4f) return;
            if (what == "BENCH") SayAny(Random.value < 0.5f ? "JUN-BENCH" : "THE-BENCH", false);
            else SayAny("JUN-STASH", false);
        }

        private void Awake() { I = this; }
        private void OnDestroy() { if (I == this) I = null; }

        private void Update()
        {
            if (Rc.Player == null || Rc.Hud == null || Rc.Char == null || Rc.Enemies == null) return;
            if (Rc.Player.Cinematic) return;
            float t = Time.time;
            Vector3 p = Rc.Player.TownPos;
            if (!_init)
            {
                _init = true; _t0 = t;
                _lvlSeen = Rc.Char.Level; _bossSeen = Rc.Char.Bosses;
                _paAt = t + Random.Range(180f, 300f); _parkAt = t + Random.Range(120f, 200f); _facAt = t + 60f; _idleAt = t + 90f;
            }
            bool park = RcTown.InPark(p.x, p.z);
            bool deck = park && p.y > 3f;
            int zone = RcTown.ZoneAt(p.x, p.z);

            if (Rc.Player.Dead) _diedAt = t;
            Treehouse(deck, t);
            Siren(park, deck, t);
            Zones(zone, p, park, t);
            Boss(zone, t);
            if (t >= _scanAt) { _scanAt = t + 0.25f; Toys(p, t); World(p, t); }

            if (_jobDone && t >= _jobAt && !OverlayPanel.AnyOpen)
            {
                _jobDone = false;
                SayAny(Kids[Random.Range(0, 3)] + "-JOB", true);
            }
        }

        // ── The treehouse: the kids ──────────────────────────────────────────
        private void Treehouse(bool deck, float t)
        {
            if (deck && !_onDeck)
            {
                _onDeck = true;
                if (t - _leftAt > 20f) Arrive(t);
            }
            else if (!deck && _onDeck)
            {
                _onDeck = false;
                _leftAt = t;
                if (Random.value < 0.6f) SayAny(Kids[Random.Range(0, 3)] + "-BYE", false);
            }
            if (!deck) return;
            RcPlayer pl = Rc.Player;
            if (!_healSaid && pl.Health < pl.MaxHealth * 0.7f && t - _leftAt > 4f) { _healSaid = true; SayAny("JUN-HEAL", false); }
            if (t >= _idleAt && Quiet)
            {
                _idleAt = t + Random.Range(70f, 130f);
                SayAny(Kids[Random.Range(0, 3)] + "-IDLE", false);
            }
        }

        private void Arrive(float t)
        {
            _healSaid = false;
            _idleAt = t + Random.Range(60f, 100f);
            _quietUntil = t + 25f;
            RcCharacter c = Rc.Char;
            RcPlayer pl = Rc.Player;
            if (c.Quest == 0 && !_firstSaid)
            {
                _firstSaid = true;
                for (int k = 0; k < 3; k++) SayAll(Kids[k] + "-FIRST", true);
                return;
            }
            int a = Random.Range(0, 3), b = (a + 1 + Random.Range(0, 2)) % 3;
            bool wasDown = t - _diedAt < 120f;
            SayAny(Kids[a] + (wasDown ? "-DOWN" : "-ARR"), true);
            for (int z = _bossSeen; z < c.Bosses && z < Bosses.Length; z++)     // (a boss fell since you were last here)
                for (int k = 0; k < 3; k++) Say(Kids[(a + k) % 3] + "-" + Bosses[z] + "-1", true);
            _bossSeen = c.Bosses;
            if (c.Level > _lvlSeen) SayAny(Kids[b] + "-LVL", true);
            _lvlSeen = c.Level;
            if (pl.Health < pl.MaxHealth * 0.4f) SayAny(Kids[Random.Range(0, 3)] + "-HURT", true);
            if (c.ActiveQuest >= 20 && (c.NoticesTold & 32) == 0) { c.NoticesTold |= 32; SayAll("MOS-PLAYER", true); }   // (before the Tape job: Moss has a cassette player)
            for (int z = 0; z < 5; z++)     // (you've read all three of an area's notices: they don't explain, they react)
            {
                if ((c.NoticesTold & (1 << z)) != 0 || !c.ZoneNoticesRead(z)) continue;
                c.NoticesTold |= 1 << z;
                for (int k = 0; k < 3; k++) Say(Kids[(a + k) % 3] + "-NOTE-" + NoteTag[z], true);
            }
        }

        // ── Waygetter ────────────────────────────────────────────────────────
        // The siren, once a night: the kids hear it too, and in the park it doesn't reach.
        private void Siren(bool park, bool deck, float t)
        {
            if (_sirenDone || t - _t0 < 45f) return;
            _sirenDone = true;
            if (!park) { SayAny("PA-SIREN", false); return; }
            Say("PA-PARK-1", false);
            if (deck) SayAny(Kids[Random.Range(0, 3)] + "-SIREN", false);
        }

        private void Zones(int zone, Vector3 p, bool park, float t)
        {
            if (zone != _zoneNow)
            {
                _zoneNow = zone;
                if (zone >= 0 && !_zoneSaid[zone]) { _zoneSaid[zone] = true; Say(ZoneLine[zone], false); }
            }
            bool fac = zone == 4 && RcTown.InBuilding(p.x, p.z);
            if (fac)
            {
                _facTime += Time.deltaTime;
                if (t >= _facAt && Quiet)
                {
                    _facAt = t + Random.Range(100f, 170f);
                    if (_facTime > 300f && Random.value < 0.4f) Say("PA-FAC-4", false); else SayAny("PA-FAC", false);
                }
            }
            else if (!park && t >= _paAt && Quiet && Rc.Enemies.Danger < 2)
            {
                _paAt = t + Random.Range(240f, 420f);
                SayAny("PA-RAND", false);
            }
            if (park && t >= _parkAt && Quiet)
            {
                _parkAt = t + Random.Range(200f, 320f);
                SayAny("PA-PARK", false);
            }
        }

        // ── Forts' signs, and the empty places where a boss was ──────────────
        private void World(Vector3 p, float t)
        {
            for (int f = 0; f < 4; f++)
            {
                if (_fortSaid[f] || !Rc.Char.HasFort(f)) continue;
                float dx = p.x - RcTown.Fort[f, 0], dz = p.z - RcTown.Fort[f, 1];
                if (dx * dx + dz * dz < 64f) { _fortSaid[f] = true; Say(FortLine[f], true); }
            }
            for (int z = 0; z < 5; z++)
            {
                if (_afterSaid[z] || Rc.Char.Bosses <= z || Rc.Enemies.FightOn(z)) continue;
                float dx = p.x - RcTown.Arena[z, 0], dz = p.z - RcTown.Arena[z, 1], r = RcTown.Arena[z, 2] + 8f;
                if (dx * dx + dz * dz < r * r && Mathf.Abs(p.y) < 6f) { _afterSaid[z] = true; Say(Bosses[z] + "-AFTER", true); }
            }
        }

        // ── The toys ─────────────────────────────────────────────────────────
        private void Toys(Vector3 p, float t)
        {
            List<RcEnemy> all = Rc.Enemies.All;
            string line = "";
            for (int i = 0; i < all.Count; i++)
            {
                RcEnemy e = all[i];
                if (!e.Alive || e.IsBoss) continue;
                RcEnemy.St now = e.Def.Kind == RcEnemyKind.JackBox ? (e.Popped ? RcEnemy.St.Chase : RcEnemy.St.Idle) : e.State;
                RcEnemy.St was;
                bool had = _was.TryGetValue(e, out was);
                _was[e] = now;
                float dx = e.Pos.x - p.x, dz = e.Pos.z - p.z;
                if (dx * dx + dz * dz > 18f * 18f || Mathf.Abs(e.Pos.y - p.y) > 4f) continue;
                if (e.Def.Kind == RcEnemyKind.Shadow)
                {
                    if (t >= _mamaAt && (now == RcEnemy.St.Chase || now == RcEnemy.St.Alert)) { _mamaAt = t + Random.Range(12f, 20f); SayAny("TOY-MAM", false); }
                    continue;
                }
                if (line.Length == 0 && now == RcEnemy.St.Chase && (!had || was != RcEnemy.St.Chase) && t >= _toyAt) line = KindGroup(e);
            }
            if (_was.Count > 120)
            {
                var gone = new List<RcEnemy>();
                foreach (var kv in _was) if (!kv.Key.Alive) gone.Add(kv.Key);
                for (int i = 0; i < gone.Count; i++) _was.Remove(gone[i]);
            }
            if (line.Length == 0) return;
            _toyAt = t + 15f;
            if (Random.value < 0.5f) SayAny(Random.value < 0.4f ? "TOY-SEE" : line, false);   // (not every toy: most of them say nothing)
        }

        private static string KindGroup(RcEnemy e)
        {
            switch (e.Def.Kind)
            {
                case RcEnemyKind.JackBox: return "TOY-JCK";
                case RcEnemyKind.RcCar: return "TOY-RCC";
                case RcEnemyKind.Robot: return "TOY-ROB";
                case RcEnemyKind.Fused: return "TOY-FUS";
                default: return e.Def.Color == 6 ? "TOY-DIA" : e.Def.Color == 5 ? "TOY-GLD" : "TOY-STR";
            }
        }

        // ── The bosses ───────────────────────────────────────────────────────
        private void Boss(int zone, float t)
        {
            RcEnemy b = Rc.Enemies.Boss;
            if (b != null && b != _boss)
            {
                _boss = b; _phase = 0; _low = false; _attackAt = t + 5f; _prevTele = -1f;
                SayAll(Bosses[b.Def.Zone] + "-INTRO", false);
                Say("PA-BOSS-START", false);
            }
            else if (b == null && _boss != null)
            {
                if (!_boss.Alive) Defeated(_boss.Def.Zone);
                _boss = null;
            }
            if (_boss == null) return;

            string pre = Bosses[_boss.Def.Zone];
            float tele = Rc.Enemies.TeleOf(_boss);
            if (tele >= 0f && _prevTele < 0f && t >= _attackAt)
            {
                _attackAt = t + 9f;
                string slam = pre + "-SLAM";
                if (Rc.Enemies.IsSlam(_boss) && RcVoiceData.Has(slam)) Say(slam, false); else SayAny(pre + "-ATTACK", false);
            }
            _prevTele = tele;

            bool mama = _boss.Def.Zone == 4;
            float frac = _boss.MaxHp > 0f ? _boss.Hp / _boss.MaxHp : 1f;
            float p1 = mama ? 0.75f : 0.66f, p2 = mama ? 0.5f : 0.4f, p3 = 0.3f, low = mama ? 0.15f : 0.2f;
            int now = frac < p3 && mama ? 3 : frac < p2 ? 2 : frac < p1 ? 1 : 0;
            if (now > _phase)
            {
                _phase = now;
                Say(pre + "-PHASE-" + now, false);
            }
            if (!_low && frac < low) { _low = true; Say(pre + "-LOW", false); Say("PA-BOSS-LOW", false); }
        }

        private void Defeated(int zone)
        {
            string pre = Bosses[zone];
            Say(pre + "-DEFEAT", true);
            if (zone == 4)
            {
                Say("PA-MAMA-OFF", true);
                Say("PA-MAMA-OFF-2", true);
                Say("MAM-AFTER", true);
                _afterSaid[4] = true;
            }
            else SayAny("PA-BOSS-END", true);
        }
    }
}
