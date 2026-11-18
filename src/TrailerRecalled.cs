using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// The 3.5 trailer (THE RECALLED UPDATE): shot in the real town. F9 in the Arcade (dev builds) goes through the door and
    /// the trailer rolls as soon as the town is up. The director takes the camera and the kid (nobody plays), every toy and
    /// every friend you see is a puppet the script moves (the same ones a friend's game draws: RcRemote kids, mirrored toys),
    /// and the whole town is built at once so no shot stutters while it streams in.
    /// </summary>
    public partial class TrailerDirector
    {
        /// <summary>Roll the Recalled trailer (true) or the Arcade one (false).</summary>
        public const bool RecalledTake = true;
        /// <summary>F9 in the Arcade: the town is loading, and the trailer starts when it's built.</summary>
        public static bool PendingRecalled;

        private const int RC = 3;
        private const bool RECALLED = RecalledTake;
        private const float R_LOGO_T = 46.4f, R_END = 54.5f;

        private const int ST_IDLE = 0, ST_CHASE = 2, ST_WIND = 3, ST_RECOVER = 4, ST_STUN = 6;

        private class Actor { public RcPeer Peer = new RcPeer(); public RcRemote Remote; public bool Up; }
        private readonly Dictionary<string, Actor> _actors = new Dictionary<string, Actor>();
        private readonly Dictionary<int, int> _toys = new Dictionary<int, int>();   // id -> def, the toys put out this frame
        private readonly List<int> _toysLast = new List<int>();
        private readonly List<string> _once = new List<string>();
        private bool _staged, _preCull;
        private readonly List<AudioSource> _muted = new List<AudioSource>();
        private int _anchorShot = -2;
        private float _fog = 1f;

        // The boss shots: where each boss stands, and from which side the camera comes (the way in).
        private static Vector3 ArenaOf(int z) { return new Vector3(RcTown.Arena[z, 0], 0f, RcTown.Arena[z, 1]); }
        private static Vector3 BossDir(int z)
        {
            Vector3 d = new Vector3(RcTown.GateSides[z, 0] - RcTown.Arena[z, 0], 0f, RcTown.GateSides[z, 1] - RcTown.Arena[z, 1]);
            return d.sqrMagnitude < 0.01f ? Vector3.back : d.normalized;
        }
        private readonly Vector3[] _bossDir = new Vector3[5];
        private float BossYaw(int z) { Vector3 d = _bossDir[z].sqrMagnitude > 0.01f ? _bossDir[z] : (z == 4 && _mamaOk ? (_mamaCam - _mamaAt).normalized : BossDir(z)); return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }

        // ── The script ───────────────────────────────────────────────────────
        private void BuildRecalled()
        {
            Color red = new Color(1f, 0.22f, 0.22f), cyan = new Color(0.4f, 0.85f, 1f), orange = new Color(1f, 0.6f, 0.15f), gold = new Color(1f, 0.85f, 0.25f), pink = new Color(1f, 0.35f, 0.85f);
            _cardsA.Add(new Card { T0 = 0.3f, T1 = OPEN_END, Text = "THE TOYS WERE RECALLED.", Size = 76, Scary = true });

            // Main Street at night, from the Arcade's door, rising. Toys wait in the fog.
            _shotsA.Add(new Shot { T0 = OPEN_END, T1 = 9f, Space = RC, P0 = new Vector3(6.6f, 1.5f, -183f), P1 = new Vector3(3.6f, 1.3f, -167f), L0 = new Vector3(0.5f, 1.6f, -150f), L1 = new Vector3(0.2f, 0.7f, -151f), Anchor = new Vector3(0f, 0f, -170f), HasAnchor = true, Flash = true, Fog = 0.5f });
            _capsA.Add(new Caption { T0 = 4.2f, T1 = 8.8f, Text = "RECALLED  ·  NEW IN 3.5", Color = pink });
            _cardsA.Add(new Card { T0 = 9f, T1 = 10.8f, Text = "THEY DIDN'T STAY RECALLED.", Size = 64, Scary = true });

            // They come down the street.
            _shotsA.Add(new Shot { T0 = 10.8f, T1 = 16.2f, Space = RC, P0 = new Vector3(-96.2f, 0.4f, -128f), P1 = new Vector3(-96.9f, 0.4f, -128f), L0 = new Vector3(-112f, 0.5f, -128f), L1 = new Vector3(-108f, 0.45f, -128f), Anchor = new Vector3(-100f, 0f, -128f), HasAnchor = true, Flash = true, Fog = 0.7f });
            _capsA.Add(new Caption { T0 = 11f, T1 = 15.8f, Text = "A TOWN OF RECALLED TOYS  ·  ALONE OR WITH FRIENDS", Color = new Color(0.8f, 0.35f, 1f) });
            _cardsA.Add(new Card { T0 = 16.2f, T1 = 17.6f, Text = "FIGHT BACK.", Hit = true, Size = 130, Scary = true, Color = red });

            // A perfect block, side on; then a roll out of the way.
            _shotsA.Add(new Shot { T0 = 17.6f, T1 = 20.4f, Space = RC, P0 = new Vector3(-102.6f, 0.9f, -131.2f), P1 = new Vector3(-101.9f, 0.85f, -130.4f), L0 = new Vector3(-101.3f, 0.55f, -128f), L1 = new Vector3(-101.3f, 0.55f, -128f), Anchor = new Vector3(-100f, 0f, -128f), HasAnchor = true, Flash = true, Fog = 0.8f });
            _capsA.Add(new Caption { T0 = 17.8f, T1 = 20.3f, Text = "BLOCK  ·  DODGE  ·  COUNTER", Color = cyan });
            _cardsA.Add(new Card { T0 = 18.6f, T1 = 20f, Text = "PERFECT BLOCK.", Hit = true, Size = 112, Color = cyan, Black = false });
            _cuesA.Add(new Cue { T = 18.55f, What = "rc_clang" });
            _shotsA.Add(new Shot { T0 = 20.4f, T1 = 23.6f, Space = RC, P0 = new Vector3(-96.6f, 1.0f, -124.6f), P1 = new Vector3(-96.9f, 0.95f, -125.4f), L0 = new Vector3(-100.6f, 0.45f, -129f), L1 = new Vector3(-100.6f, 0.45f, -129.6f), Anchor = new Vector3(-100f, 0f, -128f), HasAnchor = true, Flash = true, Fog = 0.8f });
            _cardsA.Add(new Card { T0 = 21f, T1 = 22.4f, Text = "DODGE ROLL.", Hit = true, Size = 112, Color = orange, Black = false });
            _cuesA.Add(new Cue { T = 21.3f, What = "rc_whoosh" });

            // The treehouse in the park: the one safe place.
            _shotsA.Add(new Shot { T0 = 23.6f, T1 = 28.8f, Space = RC, P0 = new Vector3(9f, 1.3f, -14f), P1 = new Vector3(5.5f, 5.8f, -12.5f), L0 = new Vector3(0f, 6.5f, 0f), L1 = new Vector3(0f, 8.6f, 0f), Anchor = new Vector3(0f, 0f, -10f), HasAnchor = true, Fog = 0.6f });
            _capsA.Add(new Caption { T0 = 23.9f, T1 = 28.6f, Text = "THE TREEHOUSE  ·  CRAFT GEAR  ·  LEVEL UP", Color = new Color(0.3f, 1f, 0.6f) });

            // Friends: four at a time, two of them new.
            _cardsA.Add(new Card { T0 = 28.8f, T1 = 30.3f, Text = "UP TO FOUR PLAYERS.", Hit = true, Size = 116, Color = cyan });
            _shotsA.Add(new Shot { T0 = 30.3f, T1 = 36f, Space = RC, P0 = new Vector3(0f, 0.8f, -98f), P1 = new Vector3(0f, 0.9f, -97.4f), L0 = new Vector3(0f, 0.85f, -118f), L1 = new Vector3(0f, 0.9f, -118f), Anchor = new Vector3(0f, 0f, -112f), HasAnchor = true, Flash = true, Fog = 0.6f });
            _capsA.Add(new Caption { T0 = 30.6f, T1 = 35.8f, Text = "NEW FREE CHARACTERS  ·  IMPOPZZZ & SA1MONFR", Color = gold });

            // Five bosses, one after the other.
            _cardsA.Add(new Card { T0 = 36f, T1 = 37.4f, Text = "FIVE BOSSES.", Hit = true, Size = 130, Scary = true, Color = red });
            float[] bt = { 37.4f, 38.9f, 40.4f, 41.9f, 43.4f, 45.6f };
            for (int i = 0; i < 5; i++)
            {
                Vector3 a = ArenaOf(i), d = BossDir(i);
                _shotsA.Add(new Shot { T0 = bt[i], T1 = bt[i + 1], Space = RC, Boss = i, Flash = true, Fog = 0.8f, Anchor = a, HasAnchor = true,
                    P0 = a + d * 9f + Vector3.up * 1.5f, P1 = a + d * 8f + Vector3.up * 1.5f, L0 = a + Vector3.up * 2f, L1 = a + Vector3.up * 2f });
                _cardsA.Add(new Card { T0 = bt[i] + 0.15f, T1 = bt[i + 1], Text = RcData.BossNames[i].ToUpper() + ".", Hit = true, Size = 100, Scary = true, Color = i == 4 ? red : Color.white, Black = false });
            }
            _cardsA.Add(new Card { T0 = 45.6f, T1 = R_LOGO_T, Text = "", Size = 10 });
            _cuesA.Add(new Cue { T = R_LOGO_T + 0.4f, What = "logo" });
        }

        private void AwakeRecalled()
        {
            BuildOverlay();
            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false; _music.spatialBlend = 0f;
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false; _sfx.spatialBlend = 0f;
            BuildRecalled();
            _music.clip = TrailerMusic.Recalled();
            _music.volume = 0.85f;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Trailer] Waiting for the town to finish building (3.5, Recalled).");
        }

        private bool _started;
        private float _waited;

        /// <summary>On black until the whole town is built (every zone would hitch the take as it streamed in), then the music and the clock start together.</summary>
        private void WaitForTown()
        {
            HideHud();
            EnsureStage();
            _waited += Time.unscaledDeltaTime;
            _black.color = Color.black;
            _cardText.gameObject.SetActive(false); _subText.gameObject.SetActive(false); _capText.gameObject.SetActive(false); _capBar.gameObject.SetActive(false);
            var rc = RecalledController.Instance;
            if (_waited < 3f || (_waited < 45f && (rc == null || !rc.EverythingBuilt))) return;
            _started = true;
            _t = 0f;
            // Dev: a number in Documents\Tattletail After Hours	railer_start.txt starts the take that many seconds in (to look at one shot).
            try
            {
                string f = System.IO.Path.Combine(System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "Tattletail After Hours"), "trailer_start.txt");
                if (System.IO.File.Exists(f)) _t = float.Parse(System.IO.File.ReadAllText(f).Trim(), System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (System.Exception) { }
            _music.Play();
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Trailer] Rolling (3.5, Recalled): the town took " + _waited.ToString("0.0") + " s to build.");
        }

        private void DestroyRecalled()
        {
            if (_fovSet && _cam != null) _cam.fieldOfView = _fovWas;
            EndStage();
            foreach (var c in _hidden) if (c != null) c.enabled = true;
        }

        // ── The stage ────────────────────────────────────────────────────────
        private void EnsureStage()
        {
            if (_staged || Rc.Player == null || Rc.Enemies == null) return;
            _staged = true;
            Rc.Player.Cinematic = true;
            Rc.Enemies.Stage = true;
            Rc.Enemies.ClearAll();
            RecalledController.HoldZones = true;
            if (Rc.Music != null)
                foreach (var a in Rc.Music.GetComponentsInChildren<AudioSource>(true)) { if (a == null || a.mute) continue; a.mute = true; _muted.Add(a); }
            if (!_preCull) { _preCull = true; Camera.onPreCull += PreCullActors; }
        }

        private void EndStage()
        {
            if (_preCull) { _preCull = false; Camera.onPreCull -= PreCullActors; }
            foreach (var a in _actors.Values) if (a.Remote != null) { a.Remote.Destroy(); a.Remote = null; }
            _actors.Clear();
            foreach (var a in _muted) if (a != null) a.mute = false;
            _muted.Clear();
            RecalledController.HoldZones = false;
            RecalledController.FogScale = 1f;
            if (Rc.Enemies != null) { Rc.Enemies.ClearAll(); Rc.Enemies.Stage = false; }
            if (Rc.Player != null)
            {
                Rc.Player.Cinematic = false;
                Rc.Player.Teleport(new Vector3(RcTown.Arrival[0], 0f, RcTown.Arrival[1]), 0f);
            }
            _staged = false;
        }

        private void PreCullActors(Camera cam)
        {
            if (cam != _cam) return;
            foreach (var a in _actors.Values) if (a.Remote != null && a.Up) a.Remote.Pose();
        }

        private void UpdateRecalled()
        {
            EnsureStage();
            RunCues(_cuesA);
            if (Input.GetKeyDown(KeyCode.Escape) || _t > R_END) { Finish(); return; }
            if (_staged)
            {
                MoveBody();
                foreach (var a in _actors.Values) a.Up = false;
                _toysLast.Clear();
                foreach (var id in _toys.Keys) _toysLast.Add(id);
                _toys.Clear();
                Script(_t);
                // A toy the script has stopped showing is gone at once (unless it was broken: that falls apart on its own).
                foreach (var id in _toysLast)
                    if (!_toys.ContainsKey(id) && !_broken.Contains(id)) Rc.Enemies.Forget(id);
                // A toy that isn't shown any more just goes (the town forgets what it hasn't heard of for a moment).
                foreach (var a in _actors.Values)
                {
                    if (a.Up) { if (a.Remote == null) a.Remote = new RcRemote(a.Peer); a.Remote.Tick(); }
                    else if (a.Remote != null) { a.Remote.Destroy(); a.Remote = null; }
                }
                foreach (var kv in _toys) SendToy(kv.Key);
            }
            DrawOverlay();
        }

        /// <summary>The town streams and its mood follows the player's body: keep it at the shot (a little before the cut, so the fog is right).</summary>
        private void MoveBody()
        {
            int pick = -1;
            for (int i = 0; i < _shotsA.Count; i++)
                if (_shotsA[i].HasAnchor && _t >= _shotsA[i].T0 - 0.8f && _t < _shotsA[i].T1) { pick = i; break; }
            if (pick < 0 || pick == _anchorShot) return;
            _anchorShot = pick;
            Rc.Player.Teleport(_shotsA[pick].Anchor, 0f);
        }

        // ── Puppets ──────────────────────────────────────────────────────────
        private bool Once(string key, float at)
        {
            if (_t < at || _once.Contains(key)) return false;
            _once.Add(key);
            return true;
        }

        private static float U(float t, float a, float b) { return Mathf.Clamp01((t - a) / (b - a)); }
        private static float Ease(float k) { return k * k * (3f - 2f * k); }

        private Actor Kid(string key, string name, int kidCode, int weapon, Vector3 pos, float yaw, int flags)
        {
            Actor a;
            if (!_actors.TryGetValue(key, out a)) { a = new Actor(); a.Peer.Name = name; a.Peer.KidCode = kidCode; a.Peer.Weapon = weapon; _actors[key] = a; }
            a.Up = true;
            a.Peer.Pos = pos; a.Peer.Yaw = yaw; a.Peer.Flags = flags; a.Peer.Hp01 = -1f;
            return a;
        }

        private void Swing(Actor a) { a.Peer.SwingAt = Time.time; }

        private readonly Dictionary<int, Vector3> _toyPos = new Dictionary<int, Vector3>();
        private readonly Dictionary<int, float> _toyYaw = new Dictionary<int, float>(), _toyTele = new Dictionary<int, float>();
        private readonly Dictionary<int, int> _toyState = new Dictionary<int, int>();

        /// <summary>A toy the script wants in the frame (state: one of ST_*; tele: how far through its wind-up, or -1).</summary>
        private void Toy(int id, int def, Vector3 pos, float yaw, int state, float tele)
        {
            _toys[id] = def; _toyPos[id] = pos; _toyYaw[id] = yaw; _toyState[id] = state; _toyTele[id] = tele;
        }

        private void SendToy(int id)
        {
            int st = _toyState[id] | (_toyTele[id] < 0f ? 0 : (Mathf.RoundToInt(_toyTele[id] * 100f) + 1) << 12);
            Rc.Enemies.Mirror(id, _toys[id], _toyPos[id], _toyYaw[id], 1f, st);
        }

        private readonly List<int> _broken = new List<int>();

        private void Break(int id, int def)
        {
            _broken.Add(id);
            if (Rc.Enemies != null) Rc.Enemies.Killed(id, def, _toyPos.ContainsKey(id) ? _toyPos[id] : Vector3.zero, false);
        }

        private const int KID_BOY = 0, KID_GIRL = 100, KID_PLAGUE = 600, KID_FISH = 700;

        private void Script(float t)
        {
            // Main Street: strays standing in the fog, looking down the street at you.
            if (t > 3.1f && t < 10.4f)
            {
                Toy(9001, RcData.E_PURPLE, new Vector3(1.2f, 0f, -158f), 175f, ST_IDLE, -1f);
                Toy(9002, RcData.E_YELLOW, new Vector3(-1.8f, 0f, -155.5f), 185f, ST_IDLE, -1f);
                Toy(9003, RcData.E_PURPLE, new Vector3(2.8f, 0f, -152f), 170f, ST_IDLE, -1f);
                Toy(9004, RcData.E_YELLOW, new Vector3(-0.4f, 0f, -149f), 180f, ST_IDLE, -1f);
                Toy(9005, RcData.E_PURPLE, new Vector3(-3.2f, 0f, -160f), 178f, ST_IDLE, -1f);
            }

            // They come: a pack down the suburb road at the camera, then they wind up.
            if (t > 10.4f && t < 16.4f)
            {
                int[] defs = { RcData.E_PURPLE, RcData.E_YELLOW, RcData.E_TEAL, RcData.E_PURPLE, RcData.E_RCCAR, RcData.E_YELLOW };
                float[] dz = { -1.5f, 1.1f, -0.4f, 1.7f, -1f, 0.3f };
                for (int i = 0; i < defs.Length; i++)
                {
                    float x0 = -116f - i * 3f - (i % 2) * 1.6f, x1 = -100.4f - (i % 3) * 0.8f;
                    float k = Ease(U(t, 10.9f + i * 0.14f, 14.3f));
                    bool there = k >= 1f;
                    bool wind = t > 14.4f + (i % 3) * 0.12f;
                    Toy(9010 + i, defs[i], new Vector3(Mathf.Lerp(x0, x1, k), 0f, -128f + dz[i]), 90f, wind ? ST_WIND : there ? ST_IDLE : ST_CHASE, wind ? U(t, 14.4f + (i % 3) * 0.12f, 15.9f) : -1f);
                }
            }

            // A perfect block: the kid raises the weapon, the toy winds up, and the hit is turned aside.
            if (t > 17.3f && t < 20.5f)
            {
                bool guard = t >= 17.75f && t < 18.8f;
                Actor hero = Kid("hero", "Kid", KID_BOY, 101, new Vector3(-100.2f, 0f, -128f), 270f, guard ? RcNet.F_BLOCK : 0);
                if (Once("hero1", 18.85f)) Swing(hero);
                if (t < 19.05f)
                {
                    bool struck = t >= 18.55f;
                    Toy(9020, RcData.E_TEAL, new Vector3(-102.5f, 0f, -128f), 90f, struck ? ST_STUN : t >= 17.9f ? ST_WIND : ST_IDLE, struck ? -1f : t >= 17.9f ? U(t, 17.9f, 18.55f) : -1f);
                }
                else if (Once("kill1", 19.05f)) Break(9020, RcData.E_TEAL);
                if (Once("perfect", 18.55f))
                {
                    if (hero.Remote != null) hero.Remote.Guarded(true);
                    Rc.Enemies.RingFrom(Rc.W(-101.3f, 0.1f, -128f), 2.6f, new Color(0.5f, 0.9f, 1f), 0.6f, true);
                }
            }

            // A roll out of the way of a swing, and the answer.
            if (t >= 20.4f && t < 23.7f)
            {
                float roll = U(t, 20.95f, 21.45f);
                bool rolling = t >= 20.95f && t < 21.45f;
                Actor hero = Kid("hero", "Kid", KID_BOY, 101, new Vector3(-99.5f, 0f, Mathf.Lerp(-128.3f, -132.1f, Ease(roll))), 270f, rolling ? RcNet.F_ROLL : 0);
                hero.Peer.RollYaw = 180f;
                if (Once("hero2", 21.75f)) Swing(hero);
                if (t < 22f)
                {
                    bool lunge = t >= 21.25f;
                    float x = lunge ? Mathf.Lerp(-103.4f, -101.2f, U(t, 21.25f, 21.4f)) : -103.4f;
                    Toy(9021, RcData.E_YELLOW, new Vector3(x, 0f, -128.3f), 90f, lunge ? ST_RECOVER : t >= 20.55f ? ST_WIND : ST_IDLE, lunge ? -1f : t >= 20.55f ? U(t, 20.55f, 21.25f) : -1f);
                }
                else if (Once("kill2", 22f)) Break(9021, RcData.E_YELLOW);
            }

            // Four friends running up Main Street at the camera, torches on; at the end they raise their weapons.
            if (t > 30f && t < 36.1f)
            {
                float run = Ease(U(t, 30.3f, 34.6f));
                float z = Mathf.Lerp(-117f, -101.2f, run);
                bool moving = t < 34.7f;
                Actor a = Kid("imp", "ImPoPzzZ", KID_PLAGUE, 103, new Vector3(-2.3f, 0f, z - 0.6f), 0f, 0);
                Actor b = Kid("fish", "Sa1monfr", KID_FISH, 104, new Vector3(-0.8f, 0f, z + 0.5f), 0f, 0);
                Actor c = Kid("girl", "Friend", KID_GIRL, 112, new Vector3(0.8f, 0f, z - 0.2f), 0f, 0);
                Actor d = Kid("boy", "Friend", KID_BOY, 101, new Vector3(2.3f, 0f, z + 0.3f), 0f, 0);
                if (!moving && Once("cheer", 34.9f)) { Swing(a); Swing(b); Swing(c); Swing(d); }
            }

            // The bosses: each stands in its arena facing the camera, winding up.
            float[] bt = { 37.4f, 38.9f, 40.4f, 41.9f, 43.4f, 45.6f };
            if (!_mamaPlanned && t > 41f) PlanMama();
            for (int i = 0; i < 5; i++)
                if (t > bt[i] - 0.7f && t < bt[i + 1] + 0.1f)
                    Toy(9100 + i, RcData.E_BOSS0 + i, i == 4 && _mamaOk ? _mamaAt : ArenaOf(i), BossYaw(i), ST_IDLE, Mathf.Lerp(0.1f, 0.95f, U(t, bt[i], bt[i + 1]))); // (a wind-up makes a toy shake: the bar shows without it)
        }

        // ── Mama: a crate stands where she spawns, so she is put on open floor ──
        private bool _mamaPlanned, _mamaOk;
        private Vector3 _mamaAt, _mamaCam;

        /// <summary>
        /// Looks in the Facility's walking map for a place for Mama with open floor around her, and a place for the lens on clear floor
        /// behind her, as far back as it finds (she is meant to be seen in her hall, not up close). Town meters.
        /// </summary>
        private void PlanMama()
        {
            _mamaPlanned = true;
            RcNav nav = RcNav.Instance;
            if (nav == null) return;
            Vector3 a = ArenaOf(4);
            float[] back = { 6.5f, 5.5f, 4.6f, 3.8f };
            int noSpot = 0, crowded = 0, noLens = 0, inTheWay = 0;
            // Tier 0: a free body for her, a free lens, nothing between. Tier 1 drops "nothing between" (shelf edges are thin), tier 2 the free lens.
            for (int tier = 0; tier < 3 && !_mamaOk; tier++)
            {
                float bestScore = -1f;
                for (int ring = 0; ring < 7; ring++)
                    for (int q = 0; q < (ring == 0 ? 1 : 8); q++)
                    {
                        float ang = q * 45f * Mathf.Deg2Rad, r = ring * 2f;
                        float mx, my, mz;
                        if (!nav.Nearest(a.x + Mathf.Sin(ang) * r, 0f, a.z + Mathf.Cos(ang) * r, 1, out mx, out my, out mz)) { noSpot++; continue; }
                        // (the walking map knows the floor, not the crates standing on it: she needs room for a body)
                        Vector3 mw = Rc.W(mx, my, mz);
                        if (Physics.CheckCapsule(mw + Vector3.up * 0.6f, mw + Vector3.up * 1.3f, 0.55f, ~0, QueryTriggerInteraction.Ignore)) { crowded++; continue; }
                        for (int k = 0; k < back.Length; k++)
                            for (int i = 0; i < 24; i++)
                            {
                                float turn = ((i + 1) / 2) * 15f * (i % 2 == 0 ? 1f : -1f);
                                Vector3 c = Quaternion.Euler(0f, turn, 0f) * BossDir(4);
                                float cx = mx + c.x * back[k], cz = mz + c.z * back[k], fx, fy, fz;
                                if (!nav.Nearest(cx, my, cz, 0, out fx, out fy, out fz) || !nav.Clear(mx, my, mz, cx, fy, cz)) continue;
                                Vector3 cw = Rc.W(cx, fy, cz);
                                if (tier < 2 && Physics.CheckSphere(cw + Vector3.up * 0.9f, 0.4f, ~0, QueryTriggerInteraction.Ignore)) { noLens++; continue; }
                                if (tier < 1 && (!ViewClear(mw + Vector3.up * 0.6f, cw + Vector3.up * 0.9f) || !ViewClear(mw + Vector3.up * 1.1f, cw + Vector3.up * 0.9f))) { inTheWay++; continue; }
                                // (farther back is better; nearer to her own spot is better; a side-on view is worth less)
                                float score = back[k] * 10f - ring * 6f - Mathf.Abs(turn) * 0.05f;
                                if (score > bestScore) { bestScore = score; _mamaAt = new Vector3(mx, my, mz); _mamaCam = new Vector3(cx, fy, cz); _mamaOk = true; }
                                break; // (the nearest side that works at this distance)
                            }
                    }
                if (_mamaOk) KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Trailer] Mama: found at tier " + tier + ".");
            }
            KaleidoscopeEndlessPlugin.Log.LogInfo(_mamaOk
                ? "KE: [Trailer] Mama at " + _mamaAt + " (her spawn is " + a + "), lens at " + _mamaCam + ". Turned away: " + noSpot + " no floor, " + crowded + " crowded, " + noLens + " lens blocked, " + inTheWay + " something between."
                : "KE: [Trailer] Mama: no open floor found near her hall (" + noSpot + " no floor, " + crowded + " crowded).");
        }

        // ── The boss shots: framed from the boss's real size ─────────────────
        private void PrepareBoss(Shot s)
        {
            RcEnemy boss = null;
            foreach (var e in Rc.Enemies.All) if (e.Id == 9100 + s.Boss && e.Go != null) { boss = e; break; }
            if (boss == null) return;
            bool any = false;
            var b = new Bounds();
            foreach (var r in boss.Go.GetComponentsInChildren<Renderer>())
            {
                if (r == null || r is ParticleSystemRenderer || InBar(r.transform, boss.Go.transform)) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (!any) return;
            s.Ready = true;
            float height = Mathf.Max(0.8f, b.size.y), width = Mathf.Max(b.size.x, b.size.z);
            float half = _cam != null ? _cam.fieldOfView * 0.5f * Mathf.Deg2Rad : 0.5f, aspect = _cam != null ? Mathf.Max(1f, _cam.aspect) : 1.78f;
            float dist = Mathf.Clamp(Mathf.Max(height * 0.72f / Mathf.Tan(half), width * 0.62f / (Mathf.Tan(half) * aspect)), 1.5f, 26f);
            Vector3 feet = new Vector3(RcTown.Arena[s.Boss, 0], b.min.y - Rc.Origin.y, RcTown.Arena[s.Boss, 1]);
            // The lens stays a little off the floor (a low one sees the props on it, not the toy).
            float camY = feet.y + Mathf.Max(height * 0.2f, 0.7f), lookY = feet.y + height * 0.3f;
            Vector3 d = BossDir(s.Boss);
            if (s.Boss == 4 && _mamaOk)
            {
                // Mama stands where PlanMama put her, in the open, and the lens is where it found a clear floor to put it.
                feet = _mamaAt;
                Vector3 dd = _mamaCam - _mamaAt; dd.y = 0f;
                dist = dd.magnitude; d = dd / dist;
                camY = _mamaCam.y + 0.9f;
                lookY = _mamaAt.y + height * 0.45f;
            }
            _bossDir[s.Boss] = d;
            s.P0 = feet + d * dist; s.P0.y = camY;
            s.P1 = feet + d * (dist * 0.88f); s.P1.y = camY + height * 0.03f;
            s.L0 = s.L1 = new Vector3(feet.x, lookY, feet.z);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Trailer] Boss " + s.Boss + ": " + height.ToString("0.0") + " m tall, camera " + dist.ToString("0.0") + " m away.");
        }

        /// <summary>Nothing of the town between the two points (a little fat, so a lens isn't put against a wall).</summary>
        private static bool ViewClear(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float dist = d.magnitude;
            if (dist < 0.1f) return true;
            RaycastHit[] hits = Physics.SphereCastAll(from, 0.2f, d / dist, dist, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
                if (h.collider != null && h.collider.transform.root.name == "KE_RecalledTown" && h.normal.y < 0.6f) return false; // (the floor isn't in the way)
            return true;
        }

        private static bool InBar(Transform t, Transform root)
        {
            for (; t != null && t != root; t = t.parent) if (t.name == "Windup" || t.name == "Bar") return true;
            return false;
        }

        /// <summary>A camera in the town never looks through a wall: if the town's geometry is between the subject and the lens, the lens moves up to it.</summary>
        private Vector3 KeepOutOfWalls(Vector3 look, Vector3 pos)
        {
            Vector3 d = pos - look;
            float dist = d.magnitude;
            if (dist < 0.5f) return pos;
            d /= dist;
            RaycastHit[] hits = Physics.RaycastAll(look, d, dist, ~0, QueryTriggerInteraction.Ignore);
            float best = dist;
            foreach (var h in hits)
            {
                if (h.collider == null || h.collider.transform.root.name != "KE_RecalledTown") continue;
                if (h.distance < best) best = h.distance;
            }
            return best < dist ? look + d * Mathf.Max(0.6f, best - 0.35f) : pos;
        }

        private void DoRecalledCue(string what)
        {
            switch (what)
            {
                case "rc_clang": _sfx.PlayOneShot(TrailerMusic.Clang(), 1f); break;
                case "rc_whoosh": _sfx.PlayOneShot(TrailerMusic.Boom(), 0.35f); break;
            }
        }
    }
}
