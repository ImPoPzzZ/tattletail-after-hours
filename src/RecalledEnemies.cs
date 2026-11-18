using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// One toy. Not a MonoBehaviour: RcEnemies ticks them all (cheaper, and their order is fixed).
    /// Positions are in town meters; they walk RcNav's spots (the ground, floors and stairs), so they need no colliders.
    /// In a town with friends only the host's toys think. Everyone else's are mirrors: they go where the host says.
    /// </summary>
    public class RcEnemy
    {
        public enum St { Idle, Alert, Chase, Windup, Recover, Return, Stun, Special, Dead }

        public int Id;                     // the host's number for it (what hits and snapshots name)
        public bool IsMirror;              // a copy of the host's toy
        public Vector3 NetPos;             // (a mirror: where the host last said it was)
        public float NetYaw, HeardAt;
        public ulong Target;               // who it's after: a player's Steam id, 0 for whoever's game this is
        public RcEnemyDef Def;
        public GameObject Go;
        public Transform Body;
        public Vector3 Pos, Home;          // town meters
        public float Yaw, Hp, MaxHp, Radius, Height;
        public St State = St.Idle;
        public float StateT, SeeT, LostT, RepathAt, AttackAt, ThinkAt, HopT, FlashT, SpecialAt, ChatterAt;
        public Vector3 Goal;
        public readonly List<float> Path = new List<float>();
        public int PathAt;
        public object Spawner;
        public bool Noticed, Popped, Enraged, ChargeHit;
        public int Special, Summons, Volley;
        public Vector3 ChargeDir;
        public Transform Bar, BarFill, Pop;
        public Transform Tele, TeleFill;   // the bar that fills as it winds up an attack
        public Renderer TeleFillRenderer;
        public float TeleShown = -1f, TeleNet = -1f;
        public Light Eye;
        public bool IsBoss { get { return Def.Kind == RcEnemyKind.Boss; } }
        public bool Alive { get { return State != St.Dead; } }
        public bool Fighting { get { return State == St.Chase || State == St.Windup || State == St.Recover || State == St.Special; } }
        public Vector3 World { get { return Rc.W(Pos.x, Pos.y, Pos.z); } }
    }

    /// <summary>
    /// Every toy in the town: where they appear (RcTown.Spawns, zone by zone), how they find a player (sight along the
    /// grid, and the noise players make), how they move (A* on RcNav), how they hit, and what they leave behind.
    /// Also the shots, and the rings on the ground that warn of a slam.
    ///
    /// With friends in the town the host runs all of this for everybody: a toy goes for whichever player it can
    /// see (and stays on them unless someone's much nearer, or hits it), zones wake when any player is near, and
    /// health is scaled to how many are playing. The others see the host's toys, swing at those, and send the hits.
    /// </summary>
    public class RcEnemies : MonoBehaviour
    {
        private class Spawner
        {
            public RcSpawn Def;
            public readonly List<RcEnemy> Alive = new List<RcEnemy>();
            public float ReadyAt;
            public bool Done;      // a boss, beaten tonight
        }

        /// <summary>A player, as the toys see them.</summary>
        private class Who
        {
            public ulong Id;       // 0: this game's own player
            public Vector3 Pos;    // town meters
            public bool Hidden, Light, There;
            public RcPeer Peer;
        }

        private struct ShotFx { public Vector3 Pos, Dir; public float Speed, Dmg, Left; public bool Mine, Ghost; public GameObject Go; }
        private class RingFx { public GameObject Go; public float Born, Life, Radius; public bool Grow; }

        public readonly List<RcEnemy> All = new List<RcEnemy>();
        /// <summary>The trailer is on: no toys are spawned and only the ones it puts there are about (see ClearAll).</summary>
        public bool Stage;
        private readonly Dictionary<int, RcEnemy> _mirrors = new Dictionary<int, RcEnemy>();
        private readonly List<Spawner>[] _spawners = new List<Spawner>[5];
        private readonly bool[] _zoneUp = new bool[5];
        private static readonly int[] Cap = { 12, 16, 16, 20, 14 }; // toys alive at once in a zone: fur is expensive to draw
        private readonly List<ShotFx> _shots = new List<ShotFx>();
        private readonly List<RingFx> _rings = new List<RingFx>();
        private readonly Who[] _who = { new Who(), new Who(), new Who(), new Who() };
        private int _players = 1;
        private RcNav _nav;
        private float _spawnAt, _snapAt;
        private int _repaths, _nextId;

        /// <summary>0 quiet, 1 they've noticed you, 2 a fight. (The music follows it.)</summary>
        public int Danger { get; private set; }
        /// <summary>The boss you're fighting, if any (for the bar at the top).</summary>
        public RcEnemy Boss { get; private set; }

        private Vector3 _decoy;
        private float _decoyUntil;
        private GameObject _decoyGo;

        private static bool Host { get { return RcNet.IsHost; } }

        /// <summary>Every toy hits this much as hard as its numbers say (they were too much for a kid).</summary>
        private const float DAMAGE_SCALE = 0.65f;

        /// <summary>How long a toy winds up before it hits: the bar over its head fills over this long. Long enough to roll out of, or raise a guard for.</summary>
        private static float Wind(RcEnemy e)
        {
            RcEnemyKind k = e.Def.Kind;
            return k == RcEnemyKind.Robot ? 1.0f : k == RcEnemyKind.Fused ? 1.0f : e.IsBoss ? 0.85f : k == RcEnemyKind.RcCar ? 0.5f : 0.65f;
        }

        /// <summary>0..1 how far along the toy is in winding up something that will hurt (-1: it isn't).</summary>
        public float TeleOf(RcEnemy e)
        {
            if (e.IsMirror) return e.TeleShown;
            switch (e.State)
            {
                case RcEnemy.St.Windup: return Mathf.Clamp01(e.StateT / Wind(e));
                case RcEnemy.St.Special:
                    if (e.Special == SP_SLAM) return Mathf.Clamp01(e.StateT / SLAM_WIND);
                    if (e.Special == SP_CHARGE) return e.StateT < CHARGE_WIND ? e.StateT / CHARGE_WIND : -1f;
                    if (e.Special == SP_VOLLEY) return e.Volley == 0 ? Mathf.Clamp01(e.StateT / VOLLEY_WIND) : -1f;
                    return -1f;
            }
            if (e.Def.Kind == RcEnemyKind.JackBox && e.Popped)
            {
                float rem = e.AttackAt - Time.time;
                if (rem > 0f && rem < 0.8f) return 1f - rem / 0.8f;
            }
            return -1f;
        }

        /// <summary>Is this boss winding up its ground slam? (Only the host knows; a friend's game says no.)</summary>
        public bool IsSlam(RcEnemy e) { return !e.IsMirror && e.State == RcEnemy.St.Special && e.Special == SP_SLAM; }

        /// <summary>A perfect block near this spot (world): whoever was swinging is thrown off balance. Anyone can say so; the host does it.</summary>
        public void Parried(Vector3 world)
        {
            if (Host) StaggerNear(Rc.T(world)); else RcNet.SendParry(world);
        }

        public void StaggerNear(Vector3 town)
        {
            for (int i = 0; i < All.Count; i++)
            {
                RcEnemy e = All[i];
                if (!e.Alive || e.IsMirror || Mathf.Abs(e.Pos.y - town.y) > LEVEL) continue;
                Vector3 d = e.Pos - town; d.y = 0f;
                if (d.magnitude > 4.5f + e.Radius) continue;
                if (e.State != RcEnemy.St.Windup && e.State != RcEnemy.St.Special && e.State != RcEnemy.St.Chase && e.State != RcEnemy.St.Recover) continue;
                if (e.Def.Kind == RcEnemyKind.Shadow) continue;
                if (e.IsBoss)
                {
                    // A boss can't be stunned, but it loses the attack and the next one comes later.
                    e.State = RcEnemy.St.Recover; e.StateT = 0f; e.Special = 0;
                    e.AttackAt = Mathf.Max(e.AttackAt, Time.time + 1.4f); e.SpecialAt = Mathf.Max(e.SpecialAt, Time.time + 2.4f);
                }
                else if (e.Def.Kind != RcEnemyKind.JackBox) { e.State = RcEnemy.St.Stun; e.StateT = 3f; }  // (stun counts down at twice speed: 1.5 s)
                else e.AttackAt = Time.time + 1.8f;
            }
        }

        private const float SLAM_WIND = 1.3f, CHARGE_WIND = 1.0f, VOLLEY_WIND = 0.8f;

        private void Awake()
        {
            _nav = RcNav.Instance;
            for (int z = 0; z < 5; z++)
            {
                _spawners[z] = new List<Spawner>();
                for (int i = 0; i < RcTown.Spawns.Count; i++)
                    if (RcTown.Spawns[i].Zone == z) _spawners[z].Add(new Spawner { Def = RcTown.Spawns[i] });
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < All.Count; i++) if (All[i].Go != null) Destroy(All[i].Go);
            for (int i = 0; i < _shots.Count; i++) if (_shots[i].Go != null) Destroy(_shots[i].Go);
            for (int i = 0; i < _rings.Count; i++) if (_rings[i].Go != null) Destroy(_rings[i].Go);
            if (_decoyGo != null) Destroy(_decoyGo);
        }

        /// <summary>The town changed hands (you joined someone, or you're the host now): every toy goes, and whoever runs them starts over.</summary>
        public void Regroup()
        {
            for (int i = 0; i < All.Count; i++) if (All[i].Go != null) Destroy(All[i].Go);
            All.Clear();
            _mirrors.Clear();
            for (int z = 0; z < 5; z++)
            {
                _zoneUp[z] = false;
                foreach (var s in _spawners[z]) { s.Alive.Clear(); s.ReadyAt = Time.time + 3f; }
            }
        }

        // ── The players ──────────────────────────────────────────────────────
        private void Gather()
        {
            Vector3 p = Rc.Player.TownPos;
            Who me = _who[0];
            me.Id = 0; me.Peer = null; me.There = true; me.Pos = p; me.Light = Rc.Player.LightOn;
            me.Hidden = Rc.Player.Dead || Rc.Player.Waiting || Rc.Player.Cinematic || RcTown.InPark(p.x, p.z);
            _players = 1;
            for (int i = 0; i < RcNet.Peers.Count && _players < _who.Length; i++)
            {
                RcPeer peer = RcNet.Peers[i];
                if (!peer.Live) continue;
                Who w = _who[_players++];
                w.Id = peer.Id.m_SteamID; w.Peer = peer; w.There = true; w.Pos = peer.Pos; w.Light = peer.Light;
                w.Hidden = peer.Down || peer.Waiting || RcTown.InPark(peer.Pos.x, peer.Pos.z); // nobody's seen in the park, or when they're down
            }
        }

        private Who Find(ulong id) { for (int i = 0; i < _players; i++) if (_who[i].Id == id) return _who[i]; return null; }

        private Who Nearest(Vector3 to)
        {
            Who best = _who[0];
            float d = float.MaxValue;
            for (int i = 0; i < _players; i++) { float k = Flat(to, _who[i].Pos) + (_who[i].Hidden ? 500f : 0f); if (k < d) { d = k; best = _who[i]; } }
            return best;
        }

        private static float Plain(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return Mathf.Sqrt(dx * dx + dz * dz); }

        /// <summary>A toy's blow lands on a player: yours is taken here, a friend's is sent to their game (where their block and their dodge are).</summary>
        private void Strike(Who w, float dmg, Vector3 fromWorld, bool blockable)
        {
            dmg *= DAMAGE_SCALE;
            if (w.Peer != null) { RcNet.SendStruck(w.Peer, dmg, fromWorld, blockable); return; }
            if (blockable) Rc.Player.Damage(dmg, fromWorld); else Rc.Player.Damage(dmg);
        }

        // ── Zones ────────────────────────────────────────────────────────────
        private void ZoneDown(int zone)
        {
            _zoneUp[zone] = false;
            for (int i = All.Count - 1; i >= 0; i--)
            {
                if (All[i].Def.Zone != zone && RcTown.ZoneAt(All[i].Home.x, All[i].Home.z) != zone) continue;
                if (All[i].Go != null) Destroy(All[i].Go);
                All.RemoveAt(i);
            }
            foreach (var s in _spawners[zone]) s.Alive.Clear();
        }

        private int AliveIn(int zone)
        {
            int n = 0;
            foreach (var s in _spawners[zone]) n += s.Alive.Count;
            return n;
        }

        private void TickSpawners()
        {
            for (int zone = 0; zone < 5; zone++)
            {
                // A zone's toys are about while any player is near it. (The Facility's need its floors measured first.)
                float near = float.MaxValue;
                for (int i = 0; i < _players; i++) near = Mathf.Min(near, RcTown.ZoneDistance(zone, _who[i].Pos.x, _who[i].Pos.z));
                if (!_zoneUp[zone] && near < 75f && (zone != 4 || RecalledController.FacilityReady)) _zoneUp[zone] = true;
                else if (_zoneUp[zone] && near > 150f) ZoneDown(zone);
                if (!_zoneUp[zone]) continue;

                int alive = AliveIn(zone);
                foreach (var s in _spawners[zone])
                {
                    for (int i = s.Alive.Count - 1; i >= 0; i--) if (!s.Alive[i].Alive) s.Alive.RemoveAt(i);
                    // Once someone's in her hall, the Mama that hunted them through the building is the one in front of them.
                    if (s.Alive.Count > 0 && s.Alive[0].Def.Kind == RcEnemyKind.Shadow && FightOn(zone))
                    {
                        if (s.Alive[0].Go != null) Destroy(s.Alive[0].Go);
                        All.Remove(s.Alive[0]);
                        s.Alive.Clear();
                        s.ReadyAt = Time.time + 20f;
                    }
                    if (s.Alive.Count > 0 || (s.Done && !AnyoneNeedsBoss(zone)) || Time.time < s.ReadyAt) continue;
                    RcEnemyDef def = RcData.Enemy(s.Def.Enemy);
                    float d = float.MaxValue;
                    for (int i = 0; i < _players; i++) d = Mathf.Min(d, Plain(_who[i].Pos, new Vector3(s.Def.X, 0f, s.Def.Z)));
                    if (def.Kind == RcEnemyKind.Boss)
                    {
                        if (d > RcTown.Arena[zone, 2] + 14f) continue;
                    }
                    else if (def.Kind == RcEnemyKind.Shadow)
                    {
                        if (!AnyoneNeedsBoss(4) || FightOn(zone)) continue; // she's in her hall, or gone
                    }
                    else
                    {
                        if (d > 62f || d < 13f) continue;      // near enough to matter, not so near they pop into view
                        if (alive + s.Def.Count > Cap[zone]) continue;
                    }
                    for (int i = 0; i < s.Def.Count; i++)
                    {
                        RcEnemy e = Spawn(def, s.Def.X + (i == 0 ? 0f : Random.Range(-2.4f, 2.4f)), s.Def.Y, s.Def.Z + (i == 0 ? 0f : Random.Range(-2.4f, 2.4f)));
                        if (e == null) continue;
                        e.Spawner = s;
                        s.Alive.Add(e);
                        alive++;
                    }
                }
            }
        }

        /// <summary>
        /// Does anyone in this town still have this zone's boss to beat? Everyone is on their own job: a boss one of you
        /// has beaten is still standing for whoever hasn't, and comes back for them.
        /// </summary>
        private bool AnyoneNeedsBoss(int zone)
        {
            if (Rc.Char.Bosses <= zone) return true;
            for (int i = 0; i < RcNet.Peers.Count; i++) if (RcNet.Peers[i].Live && RcNet.Peers[i].Bosses <= zone) return true;
            return false;
        }

        /// <summary>Is a zone's boss in a fight right now? (Anyone who wasn't there when it started waits outside.)</summary>
        public bool FightOn(int zone)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].IsBoss && All[i].Alive && All[i].Def.Zone == zone && All[i].State != RcEnemy.St.Idle && All[i].State != RcEnemy.St.Return) return true;
            return false;
        }

        /// <summary>One mirrored toy gone at once, no falling apart (for the trailer's stage).</summary>
        public void Forget(int id)
        {
            RcEnemy e;
            if (!_mirrors.TryGetValue(id, out e)) return;
            _mirrors.Remove(id);
            e.State = RcEnemy.St.Dead;
            if (e.Go != null) Destroy(e.Go);
            All.Remove(e);
        }

        /// <summary>Every toy gone (for the trailer's stage).</summary>
        public void ClearAll()
        {
            for (int i = 0; i < All.Count; i++) { if (All[i].Go != null) Destroy(All[i].Go); All[i].State = RcEnemy.St.Dead; } // (dead, so the spawners forget them)
            All.Clear();
            _mirrors.Clear();
            for (int z = 0; z < 5; z++) _zoneUp[z] = false;
        }

        public RcEnemy Spawn(RcEnemyDef def, float x, float y, float z)
        {
            float fx, fy, fz;
            if (!_nav.Nearest(x, y, z, 5, out fx, out fy, out fz)) return null;
            var e = new RcEnemy { Def = def, Id = ++_nextId, Pos = new Vector3(fx, fy, fz), Home = new Vector3(fx, fy, fz), Yaw = Random.Range(0f, 360f) };
            e.MaxHp = e.Hp = def.Hp * RecalledRules.EnemyHealthScale(_players); // (tougher for a bigger group)
            Look(e);
            e.ThinkAt = Time.time + Random.Range(0f, 0.3f);
            e.ChatterAt = Time.time + Random.Range(2f, 9f);
            e.SpecialAt = Time.time + 4f;
            All.Add(e);
            return e;
        }

        private static void Look(RcEnemy e)
        {
            try { RcLooks.Build(e); }
            catch (System.Exception ex)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Recalled] Couldn't build " + e.Def.Name + ": " + ex.Message);
                if (e.Go == null) { e.Go = new GameObject("KE_RcEnemy"); RcLooks.Fallback(e); }
            }
            e.Go.transform.position = e.World;
        }

        // ── Mirrors: the host's toys, in a friend's game ─────────────────────
        /// <summary>A line of a snapshot: make the toy if it's new, and note where the host says it is.</summary>
        public void Mirror(int id, int defId, Vector3 pos, float yaw, float hp01, int state)
        {
            RcEnemy e;
            if (!_mirrors.TryGetValue(id, out e) || e.Go == null)
            {
                RcEnemyDef def = RcData.Enemy(defId);
                if (def == null) return;
                e = new RcEnemy { Def = def, Id = id, IsMirror = true, Pos = pos, Home = pos, Yaw = yaw, MaxHp = def.Hp };
                e.Hp = e.MaxHp;
                e.ChatterAt = Time.time + Random.Range(2f, 9f);
                Look(e);
                All.Add(e);
                _mirrors[id] = e;
            }
            if (!e.Alive) return;
            e.NetPos = pos; e.NetYaw = yaw; e.HeardAt = Time.time;
            float hp = hp01 * e.MaxHp;
            if (hp < e.Hp || hp > e.Hp + e.MaxHp * 0.15f) e.Hp = hp; // (your own hits show at once; it only goes back up if it mended)
            e.TeleNet = (state >> 12) == 0 ? -1f : ((state >> 12) - 1) / 100f;
            e.State = (RcEnemy.St)(state & 255);
            if (e.State == RcEnemy.St.Dead) e.State = RcEnemy.St.Idle; // (its end comes by its own message)
            e.Popped = (state & 256) != 0;
            e.Enraged = (state & 512) != 0;
        }

        private void TickMirror(RcEnemy e, Vector3 me)
        {
            // (the host says how far along its wind-up a toy is; between snapshots it's eased along)
            if (e.TeleNet < 0f) e.TeleShown = -1f;
            else e.TeleShown = e.TeleShown < 0f ? e.TeleNet : Mathf.MoveTowards(e.TeleShown, e.TeleNet, Time.deltaTime * 3f);
            if (Time.time - e.HeardAt > 1.6f) { e.State = RcEnemy.St.Dead; e.StateT = 2f; return; } // out of the host's sight of us: it just goes
            Vector3 to = e.NetPos - e.Pos;
            float d = to.magnitude;
            if (d > 8f) e.Pos = e.NetPos;
            else if (d > 0.01f)
            {
                e.Pos = Vector3.MoveTowards(e.Pos, e.NetPos, Mathf.Max(d * 8f, e.Def.Speed) * Time.deltaTime);
                if (d > 0.08f) e.HopT += Time.deltaTime * Mathf.Max(0.5f, e.Def.Speed / 3f);
            }
            e.Yaw = Mathf.MoveTowardsAngle(e.Yaw, e.NetYaw, 540f * Time.deltaTime);
            if (e.State == RcEnemy.St.Idle && e.Def.Kind == RcEnemyKind.Stray && Time.time >= e.ChatterAt)
            {
                e.ChatterAt = Time.time + Random.Range(5f, 14f);
                if (Flat(e.Pos, me) < 30f) Rc.Music.Chatter(e.World, e.Def.Color);
            }
        }

        /// <summary>The host says a toy broke.</summary>
        public void Killed(int id, int defId, Vector3 pos, bool reward)
        {
            RcEnemy e;
            if (_mirrors.TryGetValue(id, out e) && e.Alive) { e.State = RcEnemy.St.Dead; e.StateT = 0f; }
            _mirrors.Remove(id);
            RcEnemyDef def = RcData.Enemy(defId);
            if (reward && def != null) Reward(def, pos);
        }

        // ── What players do to them ──────────────────────────────────────────
        /// <summary>A melee swing: everything in an arc in front of you. Returns how many it hit.</summary>
        public int HitArc(Vector3 world, float yaw, float range, float halfAngle, float dmg)
        {
            Vector3 p = Rc.T(world), fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            int n = 0;
            for (int i = 0; i < All.Count; i++)
            {
                RcEnemy e = All[i];
                if (!e.Alive || Mathf.Abs(e.Pos.y - p.y) > LEVEL) continue;
                Vector3 d = e.Pos - p; d.y = 0f;
                float dist = d.magnitude;
                if (dist > range + e.Radius) continue;
                if (dist > e.Radius + 0.4f && Vector3.Angle(fwd, d) > halfAngle) continue;
                Hurt(e, dmg, 0f);
                n++;
            }
            return n;
        }

        /// <summary>A shock: everything within a radius, stunned for a moment.</summary>
        public void Blast(Vector3 world, float radius, float dmg, float stun)
        {
            Vector3 p = Rc.T(world);
            for (int i = 0; i < All.Count; i++)
            {
                RcEnemy e = All[i];
                if (!e.Alive || Mathf.Abs(e.Pos.y - p.y) > LEVEL) continue;
                Vector3 d = e.Pos - p; d.y = 0f;
                if (d.magnitude > radius + e.Radius) continue;
                Hurt(e, dmg, stun);
            }
        }

        public void SetDecoy(Vector3 world, float seconds)
        {
            if (!Host) RcNet.SendDecoy(world, seconds); // (the host's toys are the ones it has to fool)
            _decoy = Rc.T(world);
            _decoyUntil = Time.time + seconds;
            if (_decoyGo != null) Destroy(_decoyGo);
            _decoyGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(_decoyGo.GetComponent<Collider>());
            _decoyGo.name = "KE_RcDecoy";
            _decoyGo.transform.position = world + Vector3.up * 0.2f;
            _decoyGo.transform.localScale = new Vector3(0.3f, 0.4f, 0.3f);
            _decoyGo.GetComponent<Renderer>().sharedMaterial = Mat.Unlit(new Color(1f, 0.4f, 0.8f));
            if (Host) Noise(world, 18f);
        }

        /// <summary>A sound: toys within 'radius' come to look (if they aren't already after someone).</summary>
        public void Noise(Vector3 world, float radius)
        {
            if (!Host) { RcNet.SendNoise(world, radius); return; }
            Vector3 p = Rc.T(world);
            if (RcTown.InPark(p.x, p.z)) return; // nothing comes into the park
            for (int i = 0; i < All.Count; i++)
            {
                RcEnemy e = All[i];
                if (!e.Alive || e.IsBoss || e.Def.Kind == RcEnemyKind.JackBox) continue;
                if (e.State != RcEnemy.St.Idle && e.State != RcEnemy.St.Alert && e.State != RcEnemy.St.Return) continue;
                Vector3 d = e.Pos - p; d.y = 0f;
                // (Mama hears a long way, but not the whole building: a scuffle in the lobby isn't her business.)
                if (d.magnitude > (e.Def.Kind == RcEnemyKind.Shadow ? Mathf.Min(radius, 20f) : radius)) continue;
                e.State = RcEnemy.St.Alert; e.StateT = 0f; e.Goal = p; e.RepathAt = 0f;
            }
        }

        /// <summary>You hit a toy. In a friend's town it's a mirror: the number shows at once and the hit goes to the host.</summary>
        private void Hurt(RcEnemy e, float dmg, float stun)
        {
            if (e.Def.Kind == RcEnemyKind.Shadow)
            {
                Rc.Hud.Number(e.World + Vector3.up * e.Height, "can't be hurt", new Color(0.8f, 0.8f, 0.85f));
                if (e.IsMirror) RcNet.SendHit(e.Id, 0.01f, 0f); else Notice(e, 0);
                return;
            }
            Rc.Hud.Number(e.World + Vector3.up * (e.Height + 0.2f), Mathf.RoundToInt(dmg).ToString(), e.IsBoss ? new Color(1f, 0.85f, 0.3f) : Color.white);
            e.FlashT = 0.12f;
            if (e.IsMirror)
            {
                e.Hp = Mathf.Max(1f, e.Hp - dmg);
                RcNet.SendHit(e.Id, dmg, stun);
                return;
            }
            Apply(e, dmg, stun, 0);
        }

        /// <summary>A friend's hit, arriving at the host.</summary>
        public void HitFrom(RcPeer peer, int id, float dmg, float stun)
        {
            for (int i = 0; i < All.Count; i++)
            {
                RcEnemy e = All[i];
                if (e.Id != id || e.IsMirror || !e.Alive) continue;
                if (e.Def.Kind == RcEnemyKind.Shadow) { Notice(e, peer.Id.m_SteamID); return; }
                e.FlashT = 0.12f;
                Apply(e, dmg, Mathf.Min(stun, 3f), peer.Id.m_SteamID);
                return;
            }
        }

        private void Apply(RcEnemy e, float dmg, float stun, ulong by)
        {
            e.Hp -= dmg;
            if (e.Hp <= 0f) { Kill(e); return; }
            Notice(e, by);
            if (stun > 0f && !e.IsBoss && e.Def.Kind != RcEnemyKind.JackBox) { e.State = RcEnemy.St.Stun; e.StateT = stun; }
            // A boss calls for help as it breaks.
            if (e.IsBoss)
            {
                int want = e.Hp < e.MaxHp * 0.33f ? 2 : e.Hp < e.MaxHp * 0.66f ? 1 : 0;
                if (e.Summons < want) { e.Summons = want; Summon(e); }
                if (!e.Enraged && e.Hp < e.MaxHp * 0.3f) { e.Enraged = true; RcNet.Say(e.Def.Name + " is furious."); }
            }
        }

        private void Summon(RcEnemy boss)
        {
            int[] adds = new[] { RcData.E_PURPLE, RcData.E_TEAL, RcData.E_BLACK, RcData.E_JUNK, RcData.E_WARE };
            RcEnemyDef def = RcData.Enemy(adds[boss.Def.Zone]);
            for (int i = 0; i < 3; i++)
            {
                float a = Random.Range(0f, 6.283f);
                RcEnemy e = Spawn(def, boss.Pos.x + Mathf.Cos(a) * 4f, boss.Pos.y, boss.Pos.z + Mathf.Sin(a) * 4f);
                if (e != null) Notice(e, boss.Target);
            }
            RcNet.Say(boss.Def.Name + " calls for help.");
        }

        private void Notice(RcEnemy e, ulong by)
        {
            if (Find(by) != null) e.Target = by; // it turns on whoever hurt it
            if (e.Def.Kind == RcEnemyKind.JackBox) { e.Noticed = true; return; }
            if (e.State == RcEnemy.St.Idle || e.State == RcEnemy.St.Alert || e.State == RcEnemy.St.Return) { e.State = RcEnemy.St.Chase; e.RepathAt = 0f; }
            if (e.Noticed) return;
            e.Noticed = true;
            e.LostT = 0f;
            // The rest of its pack comes too.
            var sp = e.Spawner as Spawner;
            if (sp != null) foreach (RcEnemy o in sp.Alive) if (o != e && o.Alive && !o.Noticed) Notice(o, by);
        }

        private void Kill(RcEnemy e)
        {
            e.State = RcEnemy.St.Dead;
            e.StateT = 0f;
            RcEnemyDef def = e.Def;
            var sp = e.Spawner as Spawner;
            RcNet.SendKill(e, true);
            Reward(def, e.Pos);
            if (e.IsBoss)
            {
                if (sp != null) { sp.Done = true; sp.ReadyAt = Time.time + 15f; } // (back in a moment if somebody here still has it to beat)
                // Its helpers fall with it.
                for (int i = 0; i < All.Count; i++)
                    if (All[i].Alive && All[i].Spawner == null && All[i].Def.Zone == def.Zone) { All[i].State = RcEnemy.St.Dead; All[i].StateT = 0f; RcNet.SendKill(All[i], false); }
            }
            else if (sp != null && sp.Alive.Count <= 1) sp.ReadyAt = Time.time + (def.Kind == RcEnemyKind.RcCar ? 40f : 150f); // the pack's gone: more come, in time (the RC cars soon)
        }

        /// <summary>
        /// A toy broke near you: its experience, your own roll of what it leaves, and the job it counts toward.
        /// Everyone near enough gets all three (a boss: everyone in its zone), whoever struck the last blow.
        /// </summary>
        private void Reward(RcEnemyDef def, Vector3 at)
        {
            Vector3 me = Rc.Player.TownPos;
            bool boss = def.Kind == RcEnemyKind.Boss;
            if (boss ? RcTown.ZoneAt(me.x, me.z) != def.Zone && Plain(me, at) > 60f : Plain(me, at) > 45f) return;
            for (int i = 0; i < def.Drops.Length; i++)
                if (Random.value < def.Drops[i].Chance) Rc.Player.Give(def.Drops[i].Id, Random.Range(def.Drops[i].Min, def.Drops[i].Max + 1));
            Rc.Char.OnKill(def);
            Daily.Add("toys", 1);
            if (boss)
            {
                Rc.Player.Give(RcData.BossGear(def.Zone, Random.value), 1);
                if (def.World == 0 && def.Zone == 4 && Rc.Char.ActiveQuest <= 21 && Rc.Char.Have(RcData.TAPE) == 0)
                {
                    Rc.Player.Give(RcData.TAPE, 1);
                    Rc.Hud.Feed("Mama's Tape was in her back. A small light on it blinked once.");
                }
                Rc.Player.AddXp(RecalledRules.BossXp(Rc.Char.Level));
                Shards.Add(100); Rc.Hud.Feed("+100 shards");   // (everything pays shards)
                Daily.Add("boss", 1);
                Rc.Hud.Toast(def.Name.ToUpper() + " IS BROKEN", def.Zone < 4 ? RcData.ZoneNames[def.Zone + 1] + " is open to you." : def.World == 1 ? "The broadcast stops." : "It's over. For tonight.");
                Rc.Music.Jingle(RcMusic.J_BOSS);
                Rc.Save();
            }
            else
            {
                Rc.Player.AddXp(def.Xp);
                Rc.Music.Jingle(RcMusic.J_BREAK);
            }
        }

        // ── Shots and rings ──────────────────────────────────────────────────
        private static Material _shotMine, _shotTheirs, _ringMat;

        /// <summary>A shot: yours (it hurts toys), or a toy's (it hurts you). Everyone in the town sees it.</summary>
        public void Fire(Vector3 from, Vector3 dir, float speed, float dmg, float range, bool mine, int tier)
        {
            if (!mine) dmg *= DAMAGE_SCALE;
            Shot(from, dir, speed, dmg, range, mine, tier, false);
            if (mine || Host) RcNet.SendFire(from, dir, speed, dmg, range, mine);
        }

        /// <summary>'ghost': a friend's shot, drawn but harmless here (their game decides what it hits).</summary>
        public void Shot(Vector3 from, Vector3 dir, float speed, float dmg, float range, bool mine, int tier, bool ghost)
        {
            if (_shotMine == null) { _shotMine = Mat.Unlit(new Color(1f, 0.9f, 0.5f)); _shotTheirs = Mat.Unlit(new Color(1f, 0.25f, 0.2f)); }
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.name = "KE_RcShot";
            go.transform.position = from;
            go.transform.localScale = Vector3.one * (mine || ghost ? 0.09f + tier * 0.015f : 0.2f);
            go.GetComponent<Renderer>().sharedMaterial = mine || ghost ? _shotMine : _shotTheirs;
            _shots.Add(new ShotFx { Pos = from, Dir = dir, Speed = speed, Dmg = dmg, Left = range, Mine = mine, Ghost = ghost, Go = go });
            if (!mine && !ghost && Plain(Rc.T(from), Rc.Player.TownPos) < 40f) Rc.Music.Jingle(RcMusic.J_SHOT);
        }

        /// <summary>A disc on the ground that grows out: an effect everyone sees.</summary>
        public void Ring(Vector3 world, float radius, Color c, float life) { AddRing(world, radius, c, life, true); }

        private void AddRing(Vector3 world, float radius, Color c, float life, bool grow)
        {
            RingFrom(world, radius, c, life, grow);
            RcNet.SendRing(world, radius, c, life, grow);
        }

        /// <summary>A ring, here only (one another game told us about, or our own).</summary>
        public void RingFrom(Vector3 world, float radius, Color c, float life, bool grow)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(go.GetComponent<Collider>());
            go.name = "KE_RcRing";
            go.transform.position = new Vector3(world.x, world.y + 0.05f, world.z);
            Shader sh = Shader.Find("Particles/Alpha Blended");
            Material m;
            if (sh != null) { m = new Material(sh); m.SetColor("_TintColor", new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, 0.22f)); }
            else m = Mat.Unlit(c);
            go.GetComponent<Renderer>().sharedMaterial = m;
            go.transform.localScale = new Vector3(grow ? 0.2f : radius * 2f, 0.01f, grow ? 0.2f : radius * 2f);
            _rings.Add(new RingFx { Go = go, Born = Time.time, Life = life, Radius = radius, Grow = grow });
        }

        private void TickShots()
        {
            Vector3 chest = Rc.Player.WorldPos + Vector3.up * 0.7f;
            for (int i = _shots.Count - 1; i >= 0; i--)
            {
                ShotFx s = _shots[i];
                float step = s.Speed * Time.deltaTime;
                Vector3 next = s.Pos + s.Dir * step;
                bool gone = false;
                RaycastHit hit;
                if (WorldRays.Raycast(s.Pos, s.Dir, step + 0.05f, out hit)) gone = true;
                if (!gone && (s.Mine || s.Ghost))
                {
                    for (int k = 0; k < All.Count; k++)
                    {
                        RcEnemy e = All[k];
                        if (!e.Alive) continue;
                        Vector3 c = e.World + Vector3.up * (e.Height * 0.5f);
                        if (SegDist(s.Pos, next, c) > Mathf.Max(e.Radius, e.Height * 0.45f) + 0.15f) continue;
                        if (s.Mine) Hurt(e, s.Dmg, 0f);
                        gone = true;
                        break;
                    }
                }
                else if (!gone && SegDist(s.Pos, next, chest) < 0.5f)
                {
                    Rc.Player.Damage(s.Dmg, s.Pos - s.Dir);
                    gone = true;
                }
                s.Pos = next; s.Left -= step;
                if (gone || s.Left <= 0f) { Destroy(s.Go); _shots.RemoveAt(i); continue; }
                s.Go.transform.position = next;
                _shots[i] = s;
            }
            for (int i = _rings.Count - 1; i >= 0; i--)
            {
                RingFx r = _rings[i];
                float k = (Time.time - r.Born) / r.Life;
                if (k >= 1f) { Destroy(r.Go); _rings.RemoveAt(i); continue; }
                if (r.Grow) { float d = Mathf.Lerp(0.2f, r.Radius * 2f, Mathf.Sqrt(k)); r.Go.transform.localScale = new Vector3(d, 0.01f, d); }
            }
        }

        private static float SegDist(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        // ── Every frame ──────────────────────────────────────────────────────
        private void Update()
        {
            if (Rc.Player == null || Rc.Char == null || !Rc.World.Built) return;
            Gather();
            Vector3 me = _who[0].Pos;
            bool host = Host;
            if (host && !Stage && Time.time >= _spawnAt) { _spawnAt = Time.time + 0.5f; TickSpawners(); }
            if (_decoyGo != null && Time.time > _decoyUntil) { Destroy(_decoyGo); _decoyGo = null; }

            _repaths = 3; // (paths per frame, shared out)
            Camera cam = FactoryController.PlayerCam;
            int danger = 0;
            RcEnemy boss = null;
            for (int i = All.Count - 1; i >= 0; i--)
            {
                RcEnemy e = All[i];
                if (e.Go == null) { All.RemoveAt(i); continue; }
                if (e.State == RcEnemy.St.Dead)
                {
                    // It falls apart: tips over and sinks.
                    e.StateT += Time.deltaTime;
                    e.Go.transform.rotation = Quaternion.Euler(Mathf.Min(90f, e.StateT * 260f), e.Yaw, 0f);
                    e.Go.transform.localScale = Vector3.one * Mathf.Max(0.01f, 1f - Mathf.Max(0f, e.StateT - 0.5f) * 2f);
                    if (e.StateT > 1f) { Destroy(e.Go); All.RemoveAt(i); if (e.IsMirror) _mirrors.Remove(e.Id); }
                    continue;
                }
                if (e.IsMirror) TickMirror(e, me); else Think(e);
                if (!e.Alive) continue;
                Show(e, cam);
                float d = Flat(e.Pos, me);
                if (e.Fighting)
                {
                    if (d < 22f || (e.IsBoss && d < RcTown.Arena[e.Def.Zone, 2] + 30f)) danger = 2;
                    else if (danger < 1 && d < 60f) danger = 1;
                    if (e.IsBoss && d < RcTown.Arena[e.Def.Zone, 2] + 30f && (boss == null || d < Flat(boss.Pos, me))) boss = e;
                }
                else if (e.State == RcEnemy.St.Alert && d < 30f && danger < 1) danger = 1;
            }
            Boss = boss;
            Danger = danger;
            TickShots();
            if (host && RcNet.Active && Time.time >= _snapAt) { _snapAt = Time.time + 0.12f; Snapshots(); }
        }

        // The toys near each friend, nearest first (as many as a packet holds).
        private readonly List<RcEnemy> _snap = new List<RcEnemy>();
        private readonly List<float> _snapD = new List<float>();

        private void Snapshots()
        {
            for (int w = 1; w < _players; w++)
            {
                Who who = _who[w];
                _snap.Clear(); _snapD.Clear();
                for (int i = 0; i < All.Count; i++)
                {
                    RcEnemy e = All[i];
                    if (!e.Alive || e.IsMirror) continue;
                    float d = Plain(e.Pos, who.Pos);
                    if (d > 85f) continue;
                    int at = _snap.Count;
                    while (at > 0 && _snapD[at - 1] > d) at--;
                    if (at >= RcNet.SNAP_MAX) continue;
                    _snap.Insert(at, e); _snapD.Insert(at, d);
                    if (_snap.Count > RcNet.SNAP_MAX) { _snap.RemoveAt(RcNet.SNAP_MAX); _snapD.RemoveAt(RcNet.SNAP_MAX); }
                }
                RcNet.SendSnapshot(who.Peer, _snap);
            }
        }

        private const float LEVEL = 2f; // further apart than this in height: on different floors

        /// <summary>How far apart two points are along the ground. On different floors they're much further: nothing reaches through a ceiling.</summary>
        private static float Flat(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z, dy = Mathf.Abs(a.y - b.y);
            return Mathf.Sqrt(dx * dx + dz * dz) + (dy > LEVEL ? dy * 4f : 0f);
        }

        private void Show(RcEnemy e, Camera cam)
        {
            Transform t = e.Go.transform;
            t.position = e.World;
            t.rotation = Quaternion.Euler(0f, e.Yaw, 0f);
            // Toys hop when they move, and shake as they wind up.
            if (e.Body != null)
            {
                float hop = 0f;
                if (e.State == RcEnemy.St.Windup) hop = Mathf.Sin(Time.time * 60f) * 0.02f;
                else if (e.HopT > 0f && e.Def.Kind != RcEnemyKind.RcCar && e.Def.Kind != RcEnemyKind.Robot && e.Def.Kind != RcEnemyKind.Shadow) hop = Mathf.Abs(Mathf.Sin(e.HopT * 9f)) * 0.13f * Mathf.Min(1.6f, e.Def.Scale);
                e.Body.localPosition = new Vector3(0f, hop, 0f);
                e.Body.localRotation = e.State == RcEnemy.St.Stun ? Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 30f) * 8f) : Quaternion.identity;
            }
            if (e.Pop != null)
            {
                float k = Mathf.MoveTowards(e.Pop.localScale.y, e.Popped ? 1f : 0.02f, Time.deltaTime * 6f);
                e.Pop.localScale = new Vector3(1f, k, 1f);
            }
            // The health bar: only once it's been hurt, and it faces you.
            if (e.Bar != null)
            {
                bool show = e.Hp < e.MaxHp && !e.IsBoss;
                if (e.Bar.gameObject.activeSelf != show) e.Bar.gameObject.SetActive(show);
                if (show && cam != null)
                {
                    e.Bar.rotation = cam.transform.rotation;
                    float k = Mathf.Clamp01(e.Hp / e.MaxHp);
                    e.BarFill.localScale = new Vector3(k, 1f, 1f);
                    e.BarFill.localPosition = new Vector3(-(1f - k) * 0.5f, 0f, -0.01f);
                }
            }
            // The wind-up bar: over its head, filling as it gets ready to hit, red near the end.
            if (e.Tele != null)
            {
                float fill = TeleOf(e);
                bool on = fill >= 0f && e.Alive;
                if (e.Tele.gameObject.activeSelf != on) e.Tele.gameObject.SetActive(on);
                if (on && cam != null)
                {
                    e.Tele.rotation = cam.transform.rotation;
                    e.TeleFill.localScale = new Vector3(fill, 0.7f, 1f);
                    e.TeleFill.localPosition = new Vector3(-(1f - fill) * 0.5f, 0f, -0.01f);
                    e.TeleFillRenderer.sharedMaterial = fill > 0.72f ? RcLooks.TeleHot : RcLooks.TeleWarm;
                }
            }
            if (e.FlashT > 0f) e.FlashT -= Time.deltaTime;
        }

        // ── How a toy thinks (the host's only) ───────────────────────────────
        private void Think(RcEnemy e)
        {
            float dt = Time.deltaTime;
            e.StateT += dt;
            RcEnemyDef def = e.Def;
            bool decoy = _decoyGo != null && !e.IsBoss && def.Kind != RcEnemyKind.Shadow;

            // Who can it see? (Looked for a few times a second, not every frame.)
            if (Time.time >= e.ThinkAt)
            {
                e.ThinkAt = Time.time + 0.22f;
                Who seen = null;
                float best = float.MaxValue;
                for (int i = 0; i < _players; i++)
                {
                    Who w = _who[i];
                    if (w.Hidden) continue;
                    float d = Flat(e.Pos, w.Pos);
                    bool can = e.IsBoss
                        ? Flat(new Vector3(RcTown.Arena[def.Zone, 0], e.Home.y, RcTown.Arena[def.Zone, 1]), w.Pos) < RcTown.Arena[def.Zone, 2] + 9f
                        : d < def.Sight * (w.Light ? 1.5f : 1f) && _nav.Clear(e.Pos.x, e.Pos.y, e.Pos.z, w.Pos.x, w.Pos.y, w.Pos.z);
                    if (!can) continue;
                    if (w.Id == e.Target) d *= 0.6f; // it stays on whoever it's after unless someone else is much nearer
                    if (d < best) { best = d; seen = w; }
                }
                if (seen != null) e.Target = seen.Id;
                bool sees = seen != null || (decoy && Flat(e.Pos, _decoy) < 25f);
                e.SeeT = sees ? 1f : 0f;
                if (sees) e.LostT = 0f;
            }
            if (e.SeeT <= 0f) e.LostT += dt;
            bool canSee = e.SeeT > 0f;

            Who who = Find(e.Target);
            if (who == null) { who = Nearest(e.Pos); e.Target = who.Id; }
            Vector3 p = who.Pos;
            bool hidden = who.Hidden;
            Vector3 target = decoy ? _decoy : p;
            float dist = Flat(e.Pos, target);
            float speed = def.Speed * (e.Enraged ? 1.25f : 1f);

            if (def.Kind == RcEnemyKind.JackBox) { JackBox(e); return; }

            float leash = e.IsBoss ? RcTown.Arena[def.Zone, 2] + 14f : def.Kind == RcEnemyKind.Shadow ? 90f : 48f;
            float reach = e.Radius + (def.Kind == RcEnemyKind.Fused || e.IsBoss ? 1.5f : 0.9f);

            switch (e.State)
            {
                case RcEnemy.St.Idle:
                    if (canSee) { Notice(e, e.Target); break; }
                    // Wander near home; chatter while nothing's wrong.
                    if (Time.time >= e.RepathAt && e.PathAt * 3 >= e.Path.Count)
                    {
                        e.RepathAt = Time.time + Random.Range(3f, 8f);
                        float wander = def.Kind == RcEnemyKind.Shadow ? 16f : def.Kind == RcEnemyKind.RcCar || def.Kind == RcEnemyKind.Robot ? 24f : e.IsBoss ? 0f : 6f;
                        if (wander > 0f) PathTo(e, new Vector3(e.Home.x + Random.Range(-wander, wander), e.Home.y, e.Home.z + Random.Range(-wander, wander)));
                    }
                    Walk(e, speed * (def.Kind == RcEnemyKind.Shadow ? 0.8f : 0.4f), dt);
                    if (Time.time >= e.ChatterAt && def.Kind == RcEnemyKind.Stray)
                    {
                        e.ChatterAt = Time.time + Random.Range(5f, 14f);
                        if (Flat(e.Pos, _who[0].Pos) < 30f) Rc.Music.Chatter(e.World, def.Color);
                    }
                    break;

                case RcEnemy.St.Alert:
                    if (canSee) { Notice(e, e.Target); break; }
                    if (Time.time >= e.RepathAt) { e.RepathAt = Time.time + 1.5f; PathTo(e, e.Goal); }
                    Walk(e, speed * 0.7f, dt);
                    if (e.StateT > 9f || (e.PathAt * 3 >= e.Path.Count && e.StateT > 3f)) { e.State = RcEnemy.St.Return; e.RepathAt = 0f; }
                    break;

                case RcEnemy.St.Chase:
                    if ((e.LostT > 5.5f && !e.IsBoss) || Flat(e.Pos, e.Home) > leash || (hidden && !decoy) || (e.IsBoss && !canSee))
                    {
                        e.State = RcEnemy.St.Return; e.RepathAt = 0f; e.Noticed = false;
                        break;
                    }
                    if (e.IsBoss && Time.time >= e.SpecialAt && BossSpecial(e, dist, p)) break;
                    if (def.Kind == RcEnemyKind.Robot && canSee && dist < 17f && dist > 3f)
                    {
                        // It stops and takes aim.
                        Face(e, target, dt);
                        if (Time.time >= e.AttackAt) { e.State = RcEnemy.St.Windup; e.StateT = 0f; }
                        break;
                    }
                    if (dist < reach && Time.time >= e.AttackAt) { e.State = RcEnemy.St.Windup; e.StateT = 0f; break; }
                    if (Time.time >= e.RepathAt) { e.RepathAt = Time.time + (def.Kind == RcEnemyKind.RcCar ? 0.3f : 0.45f); PathTo(e, target); }
                    if (dist > reach * 0.8f) Walk(e, speed, dt); else Face(e, target, dt);
                    break;

                case RcEnemy.St.Windup:
                    Face(e, target, dt);
                    float wind = Wind(e);
                    if (e.StateT < wind) break;
                    if (def.Kind == RcEnemyKind.Robot)
                    {
                        Vector3 from = e.World + Vector3.up * (e.Height * 0.8f), at = Rc.W(target.x, target.y + (decoy ? 0.3f : 0.7f), target.z);
                        Fire(from, (at - from).normalized, 17f, def.Damage, 30f, false, 0);
                        e.AttackAt = Time.time + 2.7f;
                    }
                    else
                    {
                        if (!decoy && Flat(e.Pos, p) < reach + 0.7f && !hidden) Strike(who, def.Damage, e.World, true);
                        e.AttackAt = Time.time + (def.Kind == RcEnemyKind.RcCar ? 2.2f : 1.6f);
                    }
                    e.State = RcEnemy.St.Recover; e.StateT = 0f;
                    break;

                case RcEnemy.St.Recover:
                    // An RC car bounces off and comes round again.
                    if (def.Kind == RcEnemyKind.RcCar) { Vector3 away = e.Pos - target; away.y = 0f; TryMove(e, e.Pos + away.normalized * speed * 0.5f * dt); }
                    if (e.StateT > (def.Kind == RcEnemyKind.Fused ? 1.1f : 0.8f)) { e.State = RcEnemy.St.Chase; e.RepathAt = 0f; }
                    break;

                case RcEnemy.St.Stun:
                    e.StateT -= dt * 2f; // (StateT was set to the stun's length; it counts down)
                    if (e.StateT <= 0f) { e.State = RcEnemy.St.Chase; e.StateT = 0f; e.RepathAt = 0f; e.Noticed = true; }
                    break;

                case RcEnemy.St.Special:
                    TickSpecial(e, p, dt);
                    break;

                case RcEnemy.St.Return:
                    if (canSee && Flat(e.Pos, e.Home) < leash * 0.7f) { Notice(e, e.Target); break; }
                    e.Hp = Mathf.Min(e.MaxHp, e.Hp + e.MaxHp * 0.25f * dt); // it mends on the way home
                    if (Time.time >= e.RepathAt) { e.RepathAt = Time.time + 2f; PathTo(e, e.Home); }
                    Walk(e, speed, dt);
                    if (Flat(e.Pos, e.Home) < 1.2f) { e.State = RcEnemy.St.Idle; e.RepathAt = Time.time + 2f; e.Summons = 0; e.Enraged = false; }
                    break;
            }
        }

        // It sits there looking like a box. Get close and it isn't.
        private void JackBox(RcEnemy e)
        {
            // Whoever's nearest that it could see sets it off.
            float d = float.MaxValue;
            Vector3 p = e.Pos + Vector3.forward;
            for (int i = 0; i < _players; i++)
            {
                if (_who[i].Hidden) continue;
                float k = Flat(e.Pos, _who[i].Pos);
                if (k < d) { d = k; p = _who[i].Pos; }
            }
            if (!e.Popped)
            {
                if (d < 3.4f || e.Noticed) { e.Popped = true; e.AttackAt = Time.time + 0.95f; if (d < 40f) Rc.Music.Jingle(RcMusic.J_POP); }
                return;
            }
            if (d > 16f && e.Hp >= e.MaxHp) { e.Popped = false; e.Noticed = false; e.State = RcEnemy.St.Idle; return; }
            e.Yaw = Mathf.MoveTowardsAngle(e.Yaw, Mathf.Atan2(p.x - e.Pos.x, p.z - e.Pos.z) * Mathf.Rad2Deg, 400f * Time.deltaTime);
            e.State = RcEnemy.St.Chase; // (for the music: it's a fight)
            if (Time.time >= e.AttackAt)
            {
                e.AttackAt = Time.time + 2.8f;
                if (d < 4.6f)
                {
                    AddRing(e.World, 3.6f, new Color(1f, 0.3f, 0.2f), 0.3f, true);
                    for (int i = 0; i < _players; i++)
                        if (!_who[i].Hidden && Flat(e.Pos, _who[i].Pos) < 3.8f) Strike(_who[i], e.Def.Damage, e.World, true);
                }
            }
        }

        // ── Bosses ───────────────────────────────────────────────────────────
        private const int SP_SLAM = 1, SP_CHARGE = 2, SP_VOLLEY = 3;

        private bool BossSpecial(RcEnemy e, float dist, Vector3 p)
        {
            int zone = e.Def.Zone;
            float slamR = zone == 1 ? 6.5f : zone >= 3 ? 5.5f : 4.6f;
            int pick = 0;
            float roll = Random.value;
            if (dist < slamR + 1.5f) pick = SP_SLAM;
            else if (zone >= 3 && dist > 6f && dist < 24f && roll < 0.6f) pick = SP_CHARGE;
            else if ((zone == 2 || zone == 4) && dist > 5f) pick = SP_VOLLEY;
            else if (zone == 1 && dist < 12f) pick = SP_SLAM;
            if (pick == 0) { e.SpecialAt = Time.time + 1.2f; return false; }
            e.Special = pick;
            e.State = RcEnemy.St.Special;
            e.StateT = 0f;
            e.ChargeHit = false;
            e.Volley = 0;
            e.SpecialAt = Time.time + (e.Enraged ? 5.5f : 8f);
            if (pick == SP_SLAM) { AddRing(e.World, slamR, new Color(1f, 0.15f, 0.1f), SLAM_WIND + 0.05f, false); e.Goal = new Vector3(slamR, 0f, 0f); }
            if (pick == SP_CHARGE) { Vector3 to = p - e.Pos; to.y = 0f; e.ChargeDir = to.normalized; }
            return true;
        }

        private void TickSpecial(RcEnemy e, Vector3 p, float dt)
        {
            RcEnemyDef def = e.Def;
            switch (e.Special)
            {
                case SP_SLAM:
                    if (e.StateT < SLAM_WIND) break; // the ring on the ground is the warning, and so is the bar
                    // (a slam comes up through the floor: it can't be blocked. Everyone in the ring gets it.)
                    for (int i = 0; i < _players; i++)
                        if (!_who[i].Hidden && Flat(e.Pos, _who[i].Pos) < e.Goal.x) Strike(_who[i], def.Damage * 1.6f, e.World, false);
                    AddRing(e.World, e.Goal.x, new Color(1f, 0.6f, 0.2f), 0.35f, true);
                    if (Flat(e.Pos, _who[0].Pos) < 40f) { Rc.Music.Jingle(RcMusic.J_SLAM); Rc.Hud.Shake(0.35f); }
                    EndSpecial(e);
                    break;
                case SP_CHARGE:
                    if (e.StateT < CHARGE_WIND) { Vector3 to = p - e.Pos; to.y = 0f; e.ChargeDir = to.normalized; e.Yaw = Mathf.Atan2(e.ChargeDir.x, e.ChargeDir.z) * Mathf.Rad2Deg; break; }
                    e.HopT += dt * 2f;
                    if (!TryMove(e, e.Pos + e.ChargeDir * def.Speed * 3.4f * dt) || e.StateT > 1.9f) { EndSpecial(e); break; }
                    if (!e.ChargeHit)
                        for (int i = 0; i < _players; i++)
                            if (!_who[i].Hidden && Flat(e.Pos, _who[i].Pos) < e.Radius + 1.1f) { e.ChargeHit = true; Strike(_who[i], def.Damage * 1.3f, e.World, true); }
                    break;
                case SP_VOLLEY:
                    Face(e, p, dt);
                    int shots = def.Zone == 4 ? 7 : 4;
                    if (e.StateT > VOLLEY_WIND + e.Volley * 0.35f)
                    {
                        Vector3 from = e.World + Vector3.up * (e.Height * 0.7f), at = Rc.W(p.x, p.y + 0.7f, p.z);
                        Vector3 dir = (at - from).normalized;
                        dir = Quaternion.Euler(0f, (e.Volley - shots * 0.5f) * 5f, 0f) * dir;
                        Fire(from, dir, 19f, def.Damage * 0.6f, 40f, false, 0);
                        e.Volley++;
                        if (e.Volley >= shots) EndSpecial(e);
                    }
                    break;
                default: EndSpecial(e); break;
            }
        }

        private static void EndSpecial(RcEnemy e) { e.State = RcEnemy.St.Recover; e.StateT = 0f; e.Special = 0; }

        // ── Moving ───────────────────────────────────────────────────────────
        private void PathTo(RcEnemy e, Vector3 to)
        {
            if (_repaths <= 0) { e.RepathAt = Time.time + 0.05f; return; } // try again next frame
            _repaths--;
            if (RcTown.InPark(to.x, to.z)) { e.Path.Clear(); e.PathAt = 0; return; } // nothing goes into the park
            if (!_nav.FindPath(e.Pos.x, e.Pos.y, e.Pos.z, to.x, to.y, to.z, e.IsBoss || e.Def.Kind == RcEnemyKind.Shadow ? 9000 : 3500, e.Path)) e.Path.Clear();
            e.PathAt = 0;
        }

        // The path is (x, y, z) points; a toy walks toward the next one across the floor, and its height follows
        // whatever it's standing on (so it goes up and down stairs).
        private void Walk(RcEnemy e, float speed, float dt)
        {
            if (e.PathAt * 3 >= e.Path.Count || speed <= 0f) return;
            Vector3 to = new Vector3(e.Path[e.PathAt * 3], e.Pos.y, e.Path[e.PathAt * 3 + 2]);
            Vector3 d = to - e.Pos;
            float len = d.magnitude;
            if (len < 0.25f) { e.PathAt++; return; }
            d /= len;
            // Keep a little apart from the rest of the pack.
            Vector3 push = Vector3.zero;
            for (int i = 0; i < All.Count; i++)
            {
                RcEnemy o = All[i];
                if (o == e || !o.Alive) continue;
                if (Mathf.Abs(o.Pos.y - e.Pos.y) > LEVEL) continue;
                Vector3 off = e.Pos - o.Pos; off.y = 0f;
                float od = off.magnitude, min = e.Radius + o.Radius + 0.15f;
                if (od < min && od > 0.001f) push += off / od * (min - od);
            }
            e.Yaw = Mathf.MoveTowardsAngle(e.Yaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 540f * dt);
            if (!TryMove(e, e.Pos + d * Mathf.Min(len, speed * dt) + push * 2f * dt)) { TryMove(e, e.Pos + d * Mathf.Min(len, speed * dt)); }
            e.HopT += dt * Mathf.Max(0.5f, speed / 3f);
        }

        private bool TryMove(RcEnemy e, Vector3 to)
        {
            int spot = _nav.SpotAt(to.x, e.Pos.y, to.z, 0.9f);
            if (spot < 0) return false;
            if (RcTown.InPark(to.x, to.z)) return false;
            to.y = Mathf.MoveTowards(e.Pos.y, _nav.SpotY(spot), Time.deltaTime * 7f);
            e.Pos = to;
            return true;
        }

        private static void Face(RcEnemy e, Vector3 at, float dt)
        {
            e.Yaw = Mathf.MoveTowardsAngle(e.Yaw, Mathf.Atan2(at.x - e.Pos.x, at.z - e.Pos.z) * Mathf.Rad2Deg, 600f * dt);
        }
    }

    /// <summary>What the toys look like: the real Tattletail, cloned and recolored, and everything else built from simple shapes.</summary>
    public static partial class RcLooks
    {
        private static Material _barBack, _barFill, _teleBack, _teleEdge;
        public static Material TeleWarm, TeleHot;

        public static void Build(RcEnemy e)
        {
            RcEnemyDef def = e.Def;
            e.Go = new GameObject("KE_Rc_" + def.Name);
            var body = new GameObject("Body").transform;
            body.SetParent(e.Go.transform, false);
            e.Body = body;
            switch (def.Kind)
            {
                case RcEnemyKind.Stray:
                    Toy(body, def.Color, def.Scale, Vector3.zero);
                    e.Radius = 0.35f * def.Scale; e.Height = 0.62f * def.Scale;
                    break;
                case RcEnemyKind.JackBox: JackBox(e, body); break;
                case RcEnemyKind.RcCar: RcCar(e, body); break;
                case RcEnemyKind.Robot: Robot(e, body, 1f); break;
                case RcEnemyKind.Fused:
                    Robot(e, body, 1.15f);
                    Toy(body, 3, 1.1f, new Vector3(0f, 2.6f, 0f)); // (riding on its head)
                    Toy(body, 0, 0.9f, new Vector3(0.55f, 0.9f, 0.1f));
                    e.Radius = 0.75f; e.Height = 2.5f;
                    break;
                case RcEnemyKind.Shadow: Hunter(e, body, 1000, 1f); break;
                case RcEnemyKind.Boss:
                    switch (def.Zone)
                    {
                        case 0:
                            // Patches: one huge Tattletail, with others stitched onto it.
                            Toy(body, 0, 3f, Vector3.zero);
                            Toy(body, 1, 1.5f, new Vector3(0.75f, 0.5f, -0.1f));
                            Toy(body, 2, 1.4f, new Vector3(-0.75f, 0.6f, -0.1f));
                            Toy(body, 4, 1.2f, new Vector3(0f, 1.75f, -0.15f));
                            e.Radius = 1.1f; e.Height = 2.3f;
                            break;
                        case 1:
                            Toy(body, 2, 5f, Vector3.zero);
                            e.Radius = 1.6f; e.Height = 3.1f;
                            break;
                        case 2:
                            Robot(e, body, 1.9f);
                            var spot = new GameObject("Searchlight");
                            spot.transform.SetParent(body, false);
                            spot.transform.localPosition = new Vector3(0f, 3f, 0.4f);
                            spot.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
                            var l = spot.AddComponent<Light>();
                            l.type = LightType.Spot; l.spotAngle = 40f; l.range = 26f; l.intensity = 2.2f; l.color = new Color(1f, 0.9f, 0.8f);
                            e.Radius = 1.2f; e.Height = 3.4f;
                            break;
                        case 3: Hunter(e, body, 1100, 1.15f); break;
                        default: Hunter(e, body, 1000, 1.3f); break;
                    }
                    break;
            }
            if (def.Kind == RcEnemyKind.Shadow || (def.Kind == RcEnemyKind.Boss && def.Zone >= 3)) Measure(e); // Mama and Papa: the real models, whose height isn't a number we can guess
            Bar(e);
        }

        private static readonly System.Collections.Generic.HashSet<int> _measured = new System.Collections.Generic.HashSet<int>();

        /// <summary>Sets a toy's height from what's actually drawn (the top of its model), so the bars and numbers sit just over its head.</summary>
        private static void Measure(RcEnemy e)
        {
            float top = 0f;
            Vector3 origin = e.Go.transform.position;
            Renderer[] all = e.Go.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] is ParticleSystemRenderer || all[i] is LineRenderer || all[i] is TrailRenderer) continue;
                top = Mathf.Max(top, all[i].bounds.max.y - origin.y);
            }
            if (_measured.Add(e.Def.Id)) KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Recalled] " + e.Def.Name + " is " + top.ToString("0.00") + " m tall (was taken as " + e.Height.ToString("0.00") + ").");
            if (top > 0.8f && top < 3.5f) e.Height = top;
        }

        /// <summary>One Tattletail: the real model if this level has one, else a fuzzy stand-in.</summary>
        private static void Toy(Transform parent, int color, float scale, Vector3 at)
        {
            GameObject clone = null;
            try { string label; clone = PartyTattletailModels.BuildColored(parent.position, Quaternion.identity, parent, color >= 7 ? 0 : color, out label); }
            catch (System.Exception) { clone = null; }
            if (clone != null)
            {
                clone.transform.localPosition = at;
                clone.transform.localRotation = Quaternion.identity;
                clone.transform.localScale *= scale;
                return;
            }
            Color[] fur = { new Color(0.45f, 0.2f, 0.6f), new Color(0.85f, 0.75f, 0.2f), new Color(0.15f, 0.6f, 0.6f), new Color(0.08f, 0.08f, 0.1f), new Color(0.85f, 0.85f, 0.85f), new Color(0.9f, 0.7f, 0.2f), new Color(0.6f, 0.85f, 0.95f),
                new Color(0.7f, 0.5f, 0.24f), new Color(0.85f, 0.82f, 0.7f), new Color(0.14f, 0.12f, 0.12f), new Color(0.12f, 0.6f, 0.56f), new Color(0.85f, 0.78f, 0.6f), new Color(0.9f, 0.7f, 0.2f), new Color(0.08f, 0.07f, 0.09f) };
            Material m = Mat.Lit(fur[Mathf.Clamp(color, 0, fur.Length - 1)], 0.1f);
            float s = 0.55f * scale;
            Prim(parent, PrimitiveType.Sphere, at + new Vector3(0f, s * 0.5f, 0f), new Vector3(s * 0.95f, s, s * 0.9f), m);
            Prim(parent, PrimitiveType.Sphere, at + new Vector3(s * 0.32f, s * 1.0f, 0f), new Vector3(s * 0.25f, s * 0.45f, s * 0.12f), m);
            Prim(parent, PrimitiveType.Sphere, at + new Vector3(-s * 0.32f, s * 1.0f, 0f), new Vector3(s * 0.25f, s * 0.45f, s * 0.12f), m);
            Material eye = Mat.Unlit(Color.white);
            Prim(parent, PrimitiveType.Sphere, at + new Vector3(s * 0.18f, s * 0.62f, s * 0.4f), Vector3.one * s * 0.2f, eye);
            Prim(parent, PrimitiveType.Sphere, at + new Vector3(-s * 0.18f, s * 0.62f, s * 0.4f), Vector3.one * s * 0.2f, eye);
        }

        public static void Fallback(RcEnemy e)
        {
            var body = new GameObject("Body").transform;
            body.SetParent(e.Go.transform, false);
            e.Body = body;
            Prim(body, PrimitiveType.Sphere, new Vector3(0f, 0.4f, 0f), Vector3.one * 0.7f, Mat.Lit(new Color(0.45f, 0.2f, 0.6f), 0.1f));
            e.Radius = 0.4f; e.Height = 0.8f;
            Bar(e);
        }

        // ── The toys that aren't Tattletails: built from rounded parts, in painted tin and plastic ──
        private static readonly System.Collections.Generic.Dictionary<string, Material> _paints = new System.Collections.Generic.Dictionary<string, Material>();

        /// <summary>A shared material: plain paint, scratched tin (a texture from the Facility, tinted), or something that glows.</summary>
        private static Material Paint(string key, Color c, float gloss, int kind)
        {
            Material m;
            if (_paints.TryGetValue(key, out m) && m != null) return m;
            if (kind == 2) m = Mat.Unlit(c);
            else if (kind == 1)
            {
                try { m = FTex.Get("plate", 1f, c, gloss, 0.75f, 0.8f).Mat; }
                catch (System.Exception) { m = Mat.Lit(c, gloss); }
            }
            else m = Mat.Lit(c, gloss);
            _paints[key] = m;
            return m;
        }

        private static GameObject Turned(GameObject go, float x, float y, float z) { go.transform.localRotation = Quaternion.Euler(x, y, z); return go; }

        /// <summary>A rod between two points (a cylinder: legs, arms, antennas, axles).</summary>
        private static GameObject Rod(Transform parent, Vector3 from, Vector3 to, float thick, Material m)
        {
            var go = Prim(parent, PrimitiveType.Cylinder, (from + to) * 0.5f, new Vector3(thick, (to - from).magnitude * 0.5f, thick), m);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (to - from).normalized);
            return go;
        }

        private static void JackBox(RcEnemy e, Transform body)
        {
            Material red = Paint("jack_red", new Color(0.62f, 0.12f, 0.13f), 0.45f, 0), gold = Paint("gold", new Color(0.9f, 0.72f, 0.2f), 0.7f, 1);
            Material blue = Paint("jack_blue", new Color(0.13f, 0.25f, 0.6f), 0.45f, 0), cream = Paint("cream", new Color(0.93f, 0.88f, 0.78f), 0.3f, 0);
            Material dark = Paint("dark", new Color(0.05f, 0.05f, 0.06f), 0.3f, 0), glow = Paint("glow_red", new Color(1f, 0.12f, 0.08f), 0f, 2);
            Material purple = Paint("jack_purple", new Color(0.42f, 0.16f, 0.55f), 0.3f, 0), green = Paint("jack_green", new Color(0.16f, 0.5f, 0.24f), 0.3f, 0);
            Material tin = Paint("tin", new Color(0.62f, 0.64f, 0.68f), 0.6f, 1);

            // The box: painted panels with a diamond on each, gold trim on every edge, a crank on the side.
            Prim(body, PrimitiveType.Cube, new Vector3(0f, 0.4f, 0f), new Vector3(0.8f, 0.8f, 0.8f), red);
            for (int side = 0; side < 4; side++)
            {
                var face = new GameObject("Side").transform;
                face.SetParent(body, false);
                face.localPosition = new Vector3(0f, 0.4f, 0f);
                face.localRotation = Quaternion.Euler(0f, side * 90f, 0f);
                Prim(face, PrimitiveType.Cube, new Vector3(0f, 0f, 0.401f), new Vector3(0.62f, 0.62f, 0.01f), side % 2 == 0 ? blue : red);
                Turned(Prim(face, PrimitiveType.Cube, new Vector3(0f, 0f, 0.405f), new Vector3(0.3f, 0.3f, 0.012f), side % 2 == 0 ? gold : blue), 0f, 0f, 45f);
                Prim(face, PrimitiveType.Cube, new Vector3(0.4f, 0f, 0.4f), new Vector3(0.07f, 0.84f, 0.07f), gold);           // a corner
                Prim(face, PrimitiveType.Cube, new Vector3(0f, 0.4f, 0.4f), new Vector3(0.8f, 0.06f, 0.07f), gold);            // the top edge
                Prim(face, PrimitiveType.Cube, new Vector3(0f, -0.385f, 0.4f), new Vector3(0.8f, 0.05f, 0.07f), gold);         // the bottom edge
            }
            Prim(body, PrimitiveType.Cube, new Vector3(0f, 0.815f, 0f), new Vector3(0.84f, 0.035f, 0.84f), gold);              // the lid
            Rod(body, new Vector3(0.4f, 0.45f, 0f), new Vector3(0.56f, 0.45f, 0f), 0.06f, tin);
            Rod(body, new Vector3(0.55f, 0.45f, 0f), new Vector3(0.55f, 0.6f, 0.1f), 0.04f, tin);
            Prim(body, PrimitiveType.Sphere, new Vector3(0.58f, 0.62f, 0.11f), Vector3.one * 0.1f, red);

            // What comes out: a spring, a ruff, a grinning jester's head. Squashed flat until it pops.
            var pop = new GameObject("Pop").transform;
            pop.SetParent(body, false);
            pop.localPosition = new Vector3(0f, 0.85f, 0f);
            pop.localScale = new Vector3(1f, 0.02f, 1f);
            for (int i = 0; i < 7; i++)
                Turned(Prim(pop, PrimitiveType.Cylinder, new Vector3((i % 2 == 0 ? 0.03f : -0.03f), 0.08f + i * 0.1f, 0f), new Vector3(0.3f - i * 0.012f, 0.018f, 0.3f - i * 0.012f), tin), (i % 2 == 0 ? 9f : -9f), 0f, 0f);
            Prim(pop, PrimitiveType.Cylinder, new Vector3(0f, 0.8f, 0f), new Vector3(0.52f, 0.03f, 0.52f), cream);              // the ruff
            Prim(pop, PrimitiveType.Cylinder, new Vector3(0f, 0.85f, 0f), new Vector3(0.4f, 0.03f, 0.4f), purple);
            Prim(pop, PrimitiveType.Sphere, new Vector3(0f, 1.12f, 0f), new Vector3(0.56f, 0.6f, 0.54f), cream);               // the head
            Prim(pop, PrimitiveType.Sphere, new Vector3(0f, 1.1f, 0.28f), Vector3.one * 0.11f, red);                           // nose
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Prim(pop, PrimitiveType.Sphere, new Vector3(sx * 0.12f, 1.2f, 0.22f), new Vector3(0.13f, 0.16f, 0.08f), dark);  // eye sockets
                Prim(pop, PrimitiveType.Sphere, new Vector3(sx * 0.12f, 1.2f, 0.255f), Vector3.one * 0.055f, glow);
                Prim(pop, PrimitiveType.Sphere, new Vector3(sx * 0.2f, 1.04f, 0.2f), new Vector3(0.12f, 0.1f, 0.06f), red);     // cheeks
                Turned(Prim(pop, PrimitiveType.Cube, new Vector3(sx * 0.12f, 1.31f, 0.24f), new Vector3(0.14f, 0.025f, 0.02f), dark), 0f, 0f, sx * -22f); // brows
            }
            Prim(pop, PrimitiveType.Cube, new Vector3(0f, 0.98f, 0.235f), new Vector3(0.3f, 0.07f, 0.04f), dark);               // the grin
            for (int i = -2; i <= 2; i++) Prim(pop, PrimitiveType.Cube, new Vector3(i * 0.055f, 0.995f, 0.258f), new Vector3(0.04f, 0.035f, 0.01f), cream);
            // A jester's hat: three horns with a bell on each.
            Prim(pop, PrimitiveType.Sphere, new Vector3(0f, 1.36f, -0.02f), new Vector3(0.5f, 0.22f, 0.5f), purple);
            for (int i = -1; i <= 1; i++)
            {
                Vector3 from = new Vector3(i * 0.12f, 1.4f, -0.02f), to = new Vector3(i * 0.36f, i == 0 ? 1.78f : 1.62f, -0.06f);
                Rod(pop, from, to, 0.12f, i == 0 ? green : purple);
                Prim(pop, PrimitiveType.Sphere, to, Vector3.one * 0.09f, gold);
            }
            e.Pop = pop;
            e.Radius = 0.6f; e.Height = 1.3f;
        }

        private static void RcCar(RcEnemy e, Transform body)
        {
            Material paint = Paint("car_red", new Color(0.75f, 0.1f, 0.1f), 0.8f, 0), dark = Paint("dark", new Color(0.05f, 0.05f, 0.06f), 0.3f, 0);
            Material tire = Paint("tire", new Color(0.03f, 0.03f, 0.035f), 0.15f, 0), chrome = Paint("tin", new Color(0.62f, 0.64f, 0.68f), 0.6f, 1);
            Material yellow = Paint("car_yellow", new Color(0.95f, 0.78f, 0.12f), 0.6f, 0), glass = Paint("car_glass", new Color(0.06f, 0.09f, 0.13f), 0.95f, 0);
            Material lamp = Paint("glow_warm", new Color(1f, 0.92f, 0.65f), 0f, 2), tail = Paint("glow_red", new Color(1f, 0.12f, 0.08f), 0f, 2);

            Prim(body, PrimitiveType.Cube, new Vector3(0f, 0.2f, 0f), new Vector3(0.56f, 0.1f, 1.16f), dark);                   // the chassis
            Prim(body, PrimitiveType.Cube, new Vector3(0f, 0.32f, -0.05f), new Vector3(0.62f, 0.17f, 1.0f), paint);             // the body
            Turned(Prim(body, PrimitiveType.Cube, new Vector3(0f, 0.34f, 0.43f), new Vector3(0.6f, 0.13f, 0.36f), paint), 12f, 0f, 0f);   // a hood that slopes down
            Prim(body, PrimitiveType.Cube, new Vector3(0f, 0.415f, 0.2f), new Vector3(0.16f, 0.012f, 0.7f), yellow);            // a racing stripe
            Prim(body, PrimitiveType.Sphere, new Vector3(0f, 0.47f, -0.12f), new Vector3(0.5f, 0.3f, 0.56f), glass);            // the cabin: a bubble
            Prim(body, PrimitiveType.Cube, new Vector3(0f, 0.29f, 0.6f), new Vector3(0.64f, 0.07f, 0.06f), chrome);             // bumpers
            Prim(body, PrimitiveType.Cube, new Vector3(0f, 0.29f, -0.57f), new Vector3(0.64f, 0.07f, 0.06f), chrome);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Prim(body, PrimitiveType.Sphere, new Vector3(sx * 0.2f, 0.36f, 0.585f), new Vector3(0.12f, 0.09f, 0.05f), lamp);
                Prim(body, PrimitiveType.Cube, new Vector3(sx * 0.22f, 0.36f, -0.56f), new Vector3(0.12f, 0.06f, 0.02f), tail);
                Rod(body, new Vector3(sx * 0.24f, 0.4f, -0.48f), new Vector3(sx * 0.24f, 0.62f, -0.54f), 0.03f, dark);          // the spoiler's struts
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    // Big soft tires with yellow hubs.
                    Vector3 hub = new Vector3(sx * 0.36f, 0.19f, sz * 0.37f);
                    Rod(body, hub - Vector3.right * 0.085f, hub + Vector3.right * 0.085f, 0.38f, tire);
                    Rod(body, hub + Vector3.right * sx * 0.06f, hub + Vector3.right * sx * 0.095f, 0.18f, yellow);
                }
            }
            Prim(body, PrimitiveType.Cube, new Vector3(0f, 0.63f, -0.55f), new Vector3(0.66f, 0.025f, 0.2f), paint);            // the spoiler
            Rod(body, new Vector3(0.2f, 0.42f, -0.35f), new Vector3(0.23f, 1.0f, -0.42f), 0.015f, chrome);                       // the aerial
            Prim(body, PrimitiveType.Sphere, new Vector3(0.23f, 1.0f, -0.42f), Vector3.one * 0.05f, tail);
            e.Radius = 0.6f; e.Height = 0.6f;
        }

        // A wind-up tin robot (the Hall Robot; bigger, the Fused Toy's frame and the Hall Monitor).
        private static void Robot(RcEnemy e, Transform parent, float s)
        {
            var body = new GameObject("Robot").transform;
            body.SetParent(parent, false);
            body.localScale = Vector3.one * s;
            Material tin = Paint("tin", new Color(0.62f, 0.64f, 0.68f), 0.6f, 1), blue = Paint("robot_blue", new Color(0.3f, 0.42f, 0.6f), 0.55f, 1);
            Material dark = Paint("dark", new Color(0.05f, 0.05f, 0.06f), 0.3f, 0), red = Paint("glow_red", new Color(1f, 0.12f, 0.08f), 0f, 2);
            Material amber = Paint("glow_amber", new Color(1f, 0.7f, 0.15f), 0f, 2), green = Paint("glow_green", new Color(0.3f, 1f, 0.4f), 0f, 2);
            Material brass = Paint("gold", new Color(0.9f, 0.72f, 0.2f), 0.7f, 1);

            for (int sx = -1; sx <= 1; sx += 2)
            {
                Prim(body, PrimitiveType.Cube, new Vector3(sx * 0.2f, 0.07f, 0.05f), new Vector3(0.26f, 0.14f, 0.4f), blue);                  // feet
                Rod(body, new Vector3(sx * 0.2f, 0.12f, 0f), new Vector3(sx * 0.2f, 0.8f, 0f), 0.17f, tin);                                  // legs
                Prim(body, PrimitiveType.Cylinder, new Vector3(sx * 0.2f, 0.46f, 0f), new Vector3(0.2f, 0.035f, 0.2f), dark);                // knees
                Prim(body, PrimitiveType.Sphere, new Vector3(sx * 0.5f, 1.5f, 0f), Vector3.one * 0.24f, blue);                               // shoulders
                Rod(body, new Vector3(sx * 0.52f, 1.46f, 0f), new Vector3(sx * 0.55f, 1.08f, 0.04f), 0.14f, tin);                            // arms
                Prim(body, PrimitiveType.Sphere, new Vector3(sx * 0.55f, 1.06f, 0.04f), Vector3.one * 0.16f, dark);                          // elbows
                Rod(body, new Vector3(sx * 0.55f, 1.06f, 0.04f), new Vector3(sx * 0.56f, 0.74f, 0.18f), 0.13f, tin);
                Prim(body, PrimitiveType.Cube, new Vector3(sx * 0.6f, 0.66f, 0.22f), new Vector3(0.04f, 0.16f, 0.1f), blue);                 // claws
                Prim(body, PrimitiveType.Cube, new Vector3(sx * 0.52f, 0.66f, 0.22f), new Vector3(0.04f, 0.16f, 0.1f), blue);
                Prim(body, PrimitiveType.Sphere, new Vector3(sx * 0.11f, 1.9f, 0.205f), Vector3.one * 0.11f, red);                           // eyes
                Prim(body, PrimitiveType.Cylinder, new Vector3(sx * 0.11f, 1.9f, 0.2f), new Vector3(0.15f, 0.012f, 0.15f), dark).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Rod(body, new Vector3(sx * 0.23f, 1.88f, 0f), new Vector3(sx * 0.31f, 1.88f, 0f), 0.14f, blue);                              // ears
                Rod(body, new Vector3(sx * 0.3f, 1.88f, 0f), new Vector3(sx * 0.36f, 2.08f, 0f), 0.025f, dark);
                Prim(body, PrimitiveType.Sphere, new Vector3(sx * 0.36f, 2.08f, 0f), Vector3.one * 0.06f, amber);
            }
            Prim(body, PrimitiveType.Cube, new Vector3(0f, 0.85f, 0f), new Vector3(0.62f, 0.14f, 0.36f), blue);                              // hips
            Prim(body, PrimitiveType.Cube, new Vector3(0f, 1.27f, 0f), new Vector3(0.8f, 0.7f, 0.46f), tin);                                 // chest
            Prim(body, PrimitiveType.Cube, new Vector3(0f, 1.3f, 0.232f), new Vector3(0.54f, 0.44f, 0.012f), dark);                          // its panel
            Prim(body, PrimitiveType.Cylinder, new Vector3(-0.14f, 1.38f, 0.24f), new Vector3(0.17f, 0.012f, 0.17f), brass).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // dials
            Prim(body, PrimitiveType.Cylinder, new Vector3(0.12f, 1.38f, 0.24f), new Vector3(0.17f, 0.012f, 0.17f), brass).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Turned(Prim(body, PrimitiveType.Cube, new Vector3(-0.14f, 1.4f, 0.255f), new Vector3(0.012f, 0.08f, 0.006f), dark), 0f, 0f, 30f);
            Turned(Prim(body, PrimitiveType.Cube, new Vector3(0.12f, 1.4f, 0.255f), new Vector3(0.012f, 0.08f, 0.006f), dark), 0f, 0f, -50f);
            Prim(body, PrimitiveType.Cube, new Vector3(-0.16f, 1.16f, 0.242f), new Vector3(0.07f, 0.07f, 0.012f), red);                      // lights
            Prim(body, PrimitiveType.Cube, new Vector3(0f, 1.16f, 0.242f), new Vector3(0.07f, 0.07f, 0.012f), amber);
            Prim(body, PrimitiveType.Cube, new Vector3(0.16f, 1.16f, 0.242f), new Vector3(0.07f, 0.07f, 0.012f), green);
            for (int i = 0; i < 4; i++) Prim(body, PrimitiveType.Sphere, new Vector3((i % 2 == 0 ? -0.36f : 0.36f), (i < 2 ? 1.58f : 0.96f), 0.232f), Vector3.one * 0.045f, brass); // rivets
            Rod(body, new Vector3(0f, 1.62f, 0f), new Vector3(0f, 1.7f, 0f), 0.2f, dark);                                                   // neck
            Prim(body, PrimitiveType.Cube, new Vector3(0f, 1.88f, 0f), new Vector3(0.46f, 0.38f, 0.42f), tin);                               // head
            Prim(body, PrimitiveType.Cube, new Vector3(0f, 1.75f, 0.212f), new Vector3(0.28f, 0.08f, 0.012f), dark);                         // a grille for a mouth
            for (int i = -2; i <= 2; i++) Prim(body, PrimitiveType.Cube, new Vector3(i * 0.05f, 1.75f, 0.22f), new Vector3(0.014f, 0.08f, 0.006f), tin);
            Rod(body, new Vector3(0f, 2.07f, 0f), new Vector3(0f, 2.24f, 0f), 0.03f, dark);                                                  // aerial
            Prim(body, PrimitiveType.Sphere, new Vector3(0f, 2.26f, 0f), Vector3.one * 0.08f, red);
            // The key in its back.
            Rod(body, new Vector3(0f, 1.3f, -0.23f), new Vector3(0f, 1.3f, -0.4f), 0.05f, brass);
            Prim(body, PrimitiveType.Sphere, new Vector3(-0.09f, 1.3f, -0.42f), new Vector3(0.17f, 0.2f, 0.03f), brass);
            Prim(body, PrimitiveType.Sphere, new Vector3(0.09f, 1.3f, -0.42f), new Vector3(0.17f, 0.2f, 0.03f), brass);
            e.Radius = 0.55f * s; e.Height = 2.1f * s;
        }

        // Mama, or Papa: the real model if the level has it.
        private static void Hunter(RcEnemy e, Transform body, int code, float scale)
        {
            GameObject m = null;
            try { m = VersusHunters.BuildBody("KE_RcHunter", code); }
            catch (System.Exception) { m = null; }
            if (m != null)
            {
                m.transform.SetParent(body, false);
                m.transform.localPosition = Vector3.zero;
                m.transform.localRotation = Quaternion.identity;
                m.transform.localScale *= scale;
            }
            else
            {
                Material fur = Mat.Lit(code >= 1100 ? new Color(0.8f, 0.8f, 0.78f) : new Color(0.3f, 0.12f, 0.4f), 0.1f);
                Prim(body, PrimitiveType.Capsule, new Vector3(0f, 1.2f, 0f) * scale, new Vector3(1.3f, 1.2f, 1.2f) * scale, fur);
                Material eye = Mat.Unlit(new Color(1f, 0.15f, 0.1f));
                Prim(body, PrimitiveType.Sphere, new Vector3(0.28f, 1.9f, 0.5f) * scale, Vector3.one * 0.22f * scale, eye);
                Prim(body, PrimitiveType.Sphere, new Vector3(-0.28f, 1.9f, 0.5f) * scale, Vector3.one * 0.22f * scale, eye);
            }
            e.Radius = 0.9f * scale; e.Height = 2.4f * scale;
        }

        private static GameObject Prim(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        private static void Bar(RcEnemy e)
        {
            if (_barBack == null) { _barBack = Mat.Unlit(new Color(0.08f, 0.02f, 0.02f)); _barFill = Mat.Unlit(new Color(0.9f, 0.15f, 0.12f)); }
            var bar = new GameObject("Bar").transform;
            bar.SetParent(e.Go.transform, false);
            bar.localPosition = new Vector3(0f, e.Height + 0.35f, 0f);
            float w = Mathf.Clamp(e.Radius * 2.2f, 0.7f, 1.6f);
            bar.localScale = new Vector3(w, 0.09f, 0.02f);
            Prim(bar, PrimitiveType.Cube, Vector3.zero, Vector3.one, _barBack);
            e.BarFill = Prim(bar, PrimitiveType.Cube, new Vector3(0f, 0f, -0.01f), Vector3.one, _barFill).transform;
            bar.gameObject.SetActive(false);
            e.Bar = bar;

            // The wind-up bar: wider, higher up, with a bright edge so it reads at a distance.
            if (_teleBack == null)
            {
                _teleBack = Mat.Unlit(new Color(0.06f, 0.02f, 0.02f)); _teleEdge = Mat.Unlit(new Color(1f, 0.93f, 0.7f));
                TeleWarm = Mat.Unlit(new Color(1f, 0.7f, 0.1f)); TeleHot = Mat.Unlit(new Color(1f, 0.12f, 0.08f));
            }
            var tele = new GameObject("Windup").transform;
            tele.SetParent(e.Go.transform, false);
            tele.localPosition = new Vector3(0f, e.Height + 0.65f, 0f);
            float tw = Mathf.Clamp(e.Radius * 2.6f, 1.1f, 2.6f);
            tele.localScale = new Vector3(tw, 0.17f, 0.02f);
            Prim(tele, PrimitiveType.Cube, new Vector3(0f, 0f, 0.012f), new Vector3(1.05f, 1.35f, 1f), _teleEdge);
            Prim(tele, PrimitiveType.Cube, Vector3.zero, Vector3.one, _teleBack);
            GameObject fill = Prim(tele, PrimitiveType.Cube, new Vector3(0f, 0f, -0.01f), new Vector3(1f, 0.7f, 1f), TeleWarm);
            e.TeleFill = fill.transform;
            e.TeleFillRenderer = fill.GetComponent<Renderer>();
            tele.gameObject.SetActive(false);
            e.Tele = tele;
        }
    }
}
