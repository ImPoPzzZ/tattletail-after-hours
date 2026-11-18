using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    public delegate void RcAction();
    public delegate string RcText();
    public delegate bool RcCheck();

    /// <summary>
    /// Recalled (docs/RECALLED_DESIGN.md): the co-op RPG in the town the recalled toys took over. This is the solo
    /// game for World 1. Like the beach and the factory, the Night 6 level loads (real player, controls), then the
    /// town is built far from the house and you're put outside the Arcade's door, in third person.
    /// You get there through the door in the Arcade (behind where you arrive).
    /// </summary>
    public static class RecalledMode
    {
        public static bool Pending;
        public static bool Active;
        /// <summary>The next session starts on the Recall Express's platform (coming back by train) instead of at the world's own arrival.</summary>
        public static bool ArriveAtStation;
        /// <summary>The world the door takes you to: the one you were last in.</summary>
        public static int Dest;

        /// <summary>The Recall Express: from the town to Egypt, or back. Saves, ends this session and starts the other world's.</summary>
        public static void Travel(int world)
        {
            if (world == RcWorlds.EGYPT && !RcWorlds.EgyptOpen) world = RcWorlds.TOWN;   // (Last Stop is under development)
            if (Rc.Char != null) RecalledSave.Save(Rc.Char);
            ArriveAtStation = world == RcWorlds.TOWN;
            Reset();
            SceneMonitor.ResetForNewRun();
            Launch(world);
        }

        public static void Launch() { Launch(RcWorlds.TOWN); }

        public static void Launch(int world)
        {
            if (world == RcWorlds.EGYPT && !RcWorlds.EgyptOpen) world = RcWorlds.TOWN;   // (Last Stop is under development)
            RcWorlds.Now = world;
            Dest = world;
            RcLobby.PublishWorld(world);   // (a party's lobby says where it is, for whoever joins)
            LoadingCover.Show(null);
            if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            EndlessRunManager.PendingBirthdayMode = false;
            EndlessTitle.PendingLucky = false;
            EndlessTitle.PendingTitle = "Recalled";
            Gadgets.RunGadget = GadgetType.None;
            Pending = true;
            EndlessRunManager.PendingEndlessRun = true; // SceneMonitor's "the mod owns this level load" gate
            // The level load freezes the game: let the loading screen be drawn a few times first, or you see the Arcade frozen instead.
            if (KaleidoscopeEndlessPlugin.Instance != null) KaleidoscopeEndlessPlugin.Instance.StartCoroutine(LoadSoon());
            else GameManager.PlayLevel(LevelEnum._6_TheParty);
        }

        private static System.Collections.IEnumerator LoadSoon()
        {
            for (int i = 0; i < 4; i++) yield return new WaitForEndOfFrame();
            GameManager.PlayLevel(LevelEnum._6_TheParty);
        }

        public static void Reset()
        {
            Pending = false;
            Active = false;
            RcWorlds.Now = RcWorlds.TOWN;   // (outside Recalled you're nowhere: the next launch says where)
            RecalledController.DestroyInstance();
        }
    }

    /// <summary>Shortcuts to the pieces of a Recalled session.</summary>
    public static class Rc
    {
        public static RcCharacter Char;
        public static RcPlayer Player;
        public static RecalledController World;
        public static RcHud Hud;
        public static RcEnemies Enemies;
        public static RcMusic Music;

        // Far from the house (the factory is at y 200, the beach 300, the Arcade 400).
        public static readonly Vector3 Origin = new Vector3(0f, 600f, 0f);

        /// <summary>Town meters -> world.</summary>
        public static Vector3 W(float x, float y, float z) { return new Vector3(Origin.x + x, Origin.y + y, Origin.z + z); }
        /// <summary>World -> town meters.</summary>
        public static Vector3 T(Vector3 world) { return world - Origin; }

        public static void Save() { if (Char != null) RecalledSave.Save(Char); }
    }

    /// <summary>Something you can use by walking up to it and pressing E.</summary>
    public class RcUse
    {
        public Vector3 Pos;          // town meters
        public float Radius = 1.8f;
        public RcText Text;
        public RcAction Act;
        public RcCheck On;           // null: always
        public int Zone = RcTown.PART_CORE;
    }

    public class RecalledController : MonoBehaviour
    {
        public static RecalledController Instance { get; private set; }

        /// <summary>The trailer wants the whole town built at once and kept that way.</summary>
        public static bool HoldZones;
        /// <summary>The trailer scales the fog (1: the town's own).</summary>
        public static float FogScale = 1f;

        private Transform _root;
        private bool _built;
        private float _readyTimer, _streamAt, _saveAt, _playClock;
        public bool Built { get { return _built; } }
        /// <summary>Every zone is up and the Facility is measured: nothing more will hitch (the trailer waits for this).</summary>
        public bool EverythingBuilt { get { for (int z = 0; z < 5; z++) if (_zoneRoot[z] == null) return false; return _facilityMeasured || RcWorlds.Egypt; } }

        private readonly Transform[] _zoneRoot = new Transform[5];
        private readonly GameObject[] _gate = new GameObject[5];
        public readonly List<RcUse> Uses = new List<RcUse>();
        private readonly float[] _crateReadyAt = new float[256];
        private int _zoneNow = -2;
        private readonly bool[] _fightSeen = new bool[5], _inFight = new bool[5];
        private float _fightToldAt;

        public static void Create()
        {
            if (Instance != null) return;
            var go = new GameObject("KE_RecalledController");
            DontDestroyOnLoad(go);
            go.AddComponent<RecalledController>();
        }

        public static void DestroyInstance()
        {
            if (Instance != null) Destroy(Instance.gameObject);
            Instance = null;
        }

        private void Awake() { Instance = this; Rc.World = this; }

        private void OnDestroy()
        {
            HoldZones = false; FogScale = 1f;
            if (_built && Rc.Char != null) { Rc.Char.PlaySeconds += _playClock; Rc.Save(); }
            RcNet.Clear();
            if (!RecalledMode.Pending) RcLobby.Leave(); // (back inside: out of the town's lobby too)
            RestoreAtmosphere();
            if (_root != null) Destroy(_root.gameObject);
            if (Instance == this) Instance = null;
            if (Rc.World == this) { Rc.World = null; Rc.Player = null; Rc.Hud = null; Rc.Enemies = null; Rc.Music = null; Rc.Char = null; }
        }

        private void Update()
        {
            if (!RecalledMode.Active) { Destroy(gameObject); return; } // must never outlive the mode
            var fps = RM.fpsController;
            if (fps == null) return;

            if (!_built)
            {
                LoadingCover.Keep(); // (the town takes a few seconds to build: the loading screen stays up until it's there)
                if (!GS.acceptInput) { _readyTimer = 0f; return; }
                _readyTimer += Time.deltaTime;
                if (_readyTimer < 0.75f) return;
                try { Begin(fps); }
                catch (System.Exception e)
                {
                    KaleidoscopeEndlessPlugin.Log.LogError("KE: [Recalled] Couldn't start: " + e);
                    LoadingCover.Hide();
                    HubMode.Launch(null); // back to the Arcade rather than a broken town
                }
                return;
            }

            _playClock += Time.deltaTime;
            if (RcWorlds.Egypt && !_probed && _playClock > 4f) { _probed = true; ProbeSolids(); }
            RcNet.Tick(); // friends: where they are, what the host's toys are doing
            Vector3 p = Rc.Player != null ? Rc.Player.TownPos : Rc.T(fps.transform.position);

            // The town is built a zone at a time: a zone's insides appear as you get near and go when you're far.
            if (Time.time >= _streamAt)
            {
                _streamAt = Time.time + 0.4f;
                for (int z = 0; z < 5; z++)
                {
                    float d = RcTown.ZoneDistance(z, p.x, p.z);
                    // (the host runs the Facility's toys for whoever goes there, so it needs the building's floors measured even from across town)
                    if (z == 4 && !_facilityMeasured && !RcWorlds.Egypt && RcNet.IsHost) d = Mathf.Min(d, RcNet.NearestPeerToZone(4));
                    if ((d < 75f || HoldZones) && _zoneRoot[z] == null) { BuildZone(z); break; } // (one a tick: each is a hitch)
                    if (d > 150f && !HoldZones && _zoneRoot[z] != null) DestroyZone(z);
                }
                for (int z = 1; z < 5; z++)
                    if (_gate[z] != null) _gate[z].SetActive(!RecalledRules.CanEnter((RecalledZone)z, Rc.Char.Level, Rc.Char.Bosses));
            }

            // Which zone you're in: its name as you walk in, and the warehouse's "get inside" quest.
            int zone = RcTown.ZoneAt(p.x, p.z);
            if (zone != _zoneNow)
            {
                _zoneNow = zone;
                if (zone >= 0) { Rc.Hud.Toast(RcData.ZoneNames[zone], "Levels " + RecalledRules.GateLevel((RecalledZone)zone) + " to " + (zone == 4 ? (RcWorlds.Egypt ? 25 : 15) : RecalledRules.GateLevel((RecalledZone)(zone + 1)))); }
                else if (RcTown.InPark(p.x, p.z)) Rc.Hud.Toast(RcWorlds.Egypt ? "Last Stop" : "The Park", "Safe");
                if (Rc.Music != null) Rc.Music.SetPlace(zone, RcTown.InPark(p.x, p.z));
            }
            if (zone == 4 && (RcTown.InBuilding(p.x, p.z) && p.z > RcTown.FAC_Z + 0.6f)) Rc.Char.OnReached(4);

            // The Facility: it rots the frame after it's built, and what can be walked in it is measured the frame after that.
            if (_facility == 1) { _facility = 2; try { FactoryLayout.Decay(); } catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Recalled] The Facility's decay pass failed: " + e.Message); } }
            else if (_facility == 2) { _facility = 3; MeasureFacility(); }
            if (_facility > 0) FactoryLayout.Tick(Time.deltaTime);

            // A boss fight belongs to whoever was there when it began. Anyone who turns up later waits outside until it's over.
            bool waiting = false;
            for (int z = 0; z < 5 && Rc.Enemies != null; z++)
            {
                if (!Rc.Enemies.FightOn(z)) { _fightSeen[z] = false; continue; }
                float ax = RcTown.Arena[z, 0], az = RcTown.Arena[z, 1], r = RcTown.Arena[z, 2];
                float dx = p.x - ax, dz = p.z - az, d = Mathf.Sqrt(dx * dx + dz * dz);
                if (!_fightSeen[z]) { _fightSeen[z] = true; _inFight[z] = d < r + 16f; }
                if (_inFight[z] || d > r + 22f) continue;
                waiting = true;
                if (d < r + 6f && d > 0.1f)
                {
                    Rc.Player.MoveTo(new Vector3(ax + dx / d * (r + 7f), p.y, az + dz / d * (r + 7f)));
                    if (Time.time >= _fightToldAt) { _fightToldAt = Time.time + 4f; Rc.Hud.Toast("A FIGHT IS ON", "Wait here: you can go in when it's over."); }
                }
            }
            if (Rc.Player != null) Rc.Player.Waiting = waiting;

            if (Rc.Player != null) UpdateMood(p, zone);

            // Up in the treehouse you're safe, and you mend.
            if (RcTown.InPark(p.x, p.z) && p.y > 3f && Rc.Player != null) Rc.Player.Mend(Time.deltaTime);

            if (Time.time >= _saveAt) { _saveAt = Time.time + 60f; Rc.Char.PlaySeconds += _playClock; _playClock = 0f; Rc.Save(); }

            // Safety net: fell off or got past the edge somehow -> back outside the Arcade.
            if (p.y < -8f || p.x < RcTown.X0 - 3f || p.x > RcTown.X1 + 3f || p.z < RcTown.Z0 - 3f || p.z > RcTown.Z1 + 3f)
                Rc.Player.Teleport(new Vector3(RcTown.Arrival[0], 0f, RcTown.Arrival[1]), 0f);
        }

        private void Begin(FirstPersonController fps)
        {
            PristineTattletail.Capture(); // the toys are cloned from the untouched Tattletail, not yours
            string note;
            Rc.Char = RecalledSave.Load(out note);
            if (RcWorlds.Egypt && Rc.Char.Quest < RcWorlds.TOWN_JOBS && !RcLobby.Exists) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Recalled] Egypt before the train: going to the town instead."); RcWorlds.Now = RcWorlds.TOWN; }

            _root = new GameObject("KE_RecalledTown").transform;
            _root.position = Rc.Origin;
            var sink = new RcMeshSink();
            RcTown.Build(sink, RcTown.PART_CORE);
            sink.Finish(_root, "Core");
            var nav = RcNav.Instance; // (builds the walkability grid once for the world)
            if (RcNav.TakeFresh()) _facilityMeasured = false;
            SetAtmosphere();

            Rc.Hud = gameObject.AddComponent<RcHud>();
            Rc.Enemies = gameObject.AddComponent<RcEnemies>();
            Rc.Music = gameObject.AddComponent<RcMusic>();
            if (!RcWorlds.Egypt) gameObject.AddComponent<RcVoice>();   // (Egypt's voices come once their lines are approved)
            if (KaleidoscopeEndlessPlugin.RecalledGraphics) gameObject.AddComponent<RcAtmos>();
            Rc.Player = gameObject.AddComponent<RcPlayer>();
            Rc.Player.Begin(fps);

            BuildGates();
            BuildTreehouseUses();
            BuildFortUses();
            BuildStation();
            BuildKids();
            BuildDueForts(false); // (forts for jobs handed in before they built themselves)

            _built = true;
            if (TrailerDirector.PendingRecalled) { TrailerDirector.PendingRecalled = false; TrailerDirector.Begin(); } // (dev builds: F9 in the Arcade rolls the Recalled trailer once the town is up)
            if (RecalledMode.ArriveAtStation && !RcWorlds.Egypt) { RecalledMode.ArriveAtStation = false; Rc.Player.Teleport(new Vector3(103.5f, 0f, 46f), 270f); } // (back by train: on the platform, facing west)
            else Rc.Player.Teleport(new Vector3(RcTown.Arrival[0], 0f, RcTown.Arrival[1]), 0f); // facing north, up Main Street
            RecalledMode.ArriveAtStation = false;
            if (RM.flashlight != null) RM.flashlight.SetFlashlight(false);
            if (RM.hud != null) RM.hud.ClearQuest();
            HubMode.SetHudText("");
            _saveAt = Time.time + 60f;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Recalled] Town built at " + Rc.Origin + ". Level " + Rc.Char.Level + ", quest " + Rc.Char.Quest + ", bosses " + Rc.Char.BossesBeaten + ".");
            LoadingCover.Hide();
            Rc.Hud.Toast(RcWorlds.Egypt ? "EGYPT" : "RECALLED", RcWorlds.Egypt ? "Last Stop. The tower, at the middle of the camp." : Rc.Char.Quest == 0 && Rc.Char.QuestProgress == 0 ? "The treehouse is up Main Street, in the park." : "Level " + Rc.Char.Level);
            if (note != null) Rc.Hud.Feed(note);
            Rc.Hud.Feed("J: what to do next.   M: the map.   H: the controls.   P: friends.");
            if (RcLobby.Status.Length > 0) Rc.Hud.Feed(RcLobby.Status);
        }

        // ── Zones ────────────────────────────────────────────────────────────
        private void BuildZone(int z)
        {
            var root = new GameObject("Zone" + z).transform;
            root.SetParent(_root, false);
            var sink = new RcMeshSink();
            RcTown.Build(sink, z);
            sink.Finish(root, RcData.ZoneNames[z]);
            _zoneRoot[z] = root;
            // The scrap piles you can search.
            for (int i = 0; i < RcTown.Crates.Count; i++)
            {
                if (RcTown.Crates[i].Zone != z) continue;
                int index = i;
                RcSpot c = RcTown.Crates[i];
                Uses.Add(new RcUse
                {
                    Zone = z, Pos = new Vector3(c.X, c.Y, c.Z), Radius = 2.1f,
                    // (a searched pile says when there'll be more: it used to just stop answering, which looked like it was gone for good)
                    Text = () => Time.time >= _crateReadyAt[index] ? "Search the scrap" : "Searched. The toys leave more in " + Clock(_crateReadyAt[index] - Time.time),
                    Act = () => { if (Time.time >= _crateReadyAt[index]) SearchCrate(index); },
                });
            }
            AddNotices(z, root);
            if (z == 4 && !RcWorlds.Egypt) BuildFacility(root); // (its toys come once its floors have been measured)
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Recalled] Built " + RcData.ZoneNames[z] + ".");
        }

        private bool _probed;

        /// <summary>Is what the plan says is solid really solid in the game? (A line in the log for a few places: the Sphinx, a wall of Last Stop, the gas station.)</summary>
        private void ProbeSolids()
        {
            float[][] pts = { new[] { -165f, 4f, -153f, 0f }, new[] { -165f, 0.6f, -153f, 1f }, new[] { -20f, 2f, -44f, 2f }, new[] { -141f, 2f, -118f, 3f }, new[] { -150f, 3f, -150f, 4f }, new[] { -170f, 5f, -147.5f, 5f } };
            string[] what = { "Sphinx body", "Sphinx plinth", "Last Stop's north wall", "gas station wall", "Sphinx body, east", "Sphinx body, north face" };
            for (int i = 0; i < pts.Length; i++)
            {
                Collider[] hit = Physics.OverlapSphere(Rc.W(pts[i][0], pts[i][1], pts[i][2]), 0.3f);
                string names = "";
                for (int k = 0; k < hit.Length && k < 3; k++) names += (names.Length > 0 ? ", " : "") + hit[k].gameObject.name + "/" + hit[k].GetType().Name + (hit[k].isTrigger ? " (trigger)" : "") + " layer " + hit[k].gameObject.layer;
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Recalled] Solid check, " + what[i] + ": " + hit.Length + " collider(s)" + (names.Length > 0 ? ": " + names : "") + ".");
            }
        }

        private static Material _paper, _stamp;

        /// <summary>
        /// Waygetter's notices: three to a zone, each a sheet of paper lying on top of a scrap pile (spread through the
        /// zone's piles). Press E to read one: until you have, it's what the pile's prompt says; after, the pile is a pile again.
        /// </summary>
        private void AddNotices(int z, Transform root)
        {
            if (RcWorlds.Egypt) return;   // (Waygetter's notices are the town's; Egypt's own documents come later)
            var piles = new List<int>();
            for (int i = 0; i < RcTown.Crates.Count; i++) if (RcTown.Crates[i].Zone == z) piles.Add(i);
            if (piles.Count == 0) return;
            if (_paper == null) { _paper = Mat.Unlit(new Color(0.95f, 0.92f, 0.78f)); _stamp = Mat.Unlit(new Color(0.75f, 0.12f, 0.1f)); }
            for (int k = 0; k < 3; k++)
            {
                int n = z * 3 + k;
                RcSpot c = RcTown.Crates[piles[Mathf.Min(piles.Count - 1, piles.Count * (2 * k + 1) / 6)]];
                GameObject sheet = null;
                if (!Rc.Char.HasNotice(n))
                {
                    sheet = Mat.Box(root, "Notice", new Vector3(c.X + 0.05f, c.Y + 0.81f, c.Z + 0.05f), new Vector3(0.36f, 0.012f, 0.46f), _paper);
                    sheet.transform.localRotation = Quaternion.Euler(0f, 17f * (k + 1), 0f);
                    sheet.GetComponent<Collider>().enabled = false;
                    GameObject mark = Mat.Box(sheet.transform, "Stamp", new Vector3(0.18f, 0.7f, -0.2f), new Vector3(0.28f, 0.8f, 0.2f), _stamp);
                    mark.GetComponent<Collider>().enabled = false;
                }
                GameObject paper = sheet;
                Uses.Insert(0, new RcUse   // (first in the list: when it and the pile are equally close, the notice is what E does)
                {
                    Zone = z, Pos = new Vector3(c.X, c.Y, c.Z), Radius = 2.1f,
                    Text = () => "Read the notice",
                    On = () => !Rc.Char.HasNotice(n),
                    Act = () =>
                    {
                        Rc.Char.ReadNotice(n);
                        if (paper != null) paper.SetActive(false);
                        Rc.Music.Jingle(RcMusic.J_PICKUP);
                        Rc.Hud.Feed("Recall notice " + Rc.Char.NoticeCount + " of " + RcNotices.COUNT + ": " + RcNotices.All[n].Title);
                        Rc.Save();
                        RcNoticeMenu.Open(n);
                    },
                });
            }
        }

        /// <summary>The junkyard station: its departure board.</summary>
        private void BuildStation()
        {
            Uses.Add(new RcUse
            {
                Pos = new Vector3(RcTown.StationX, 0f, RcTown.StationZ), Radius = 4.2f,
                Text = () => "The Recall Express: the departure board",
                Act = () => RcBoardMenu.Open(),
            });
        }

        // ── The warehouse is the Waygetter Facility ──────────────────────────
        private int _facility;                 // 0 not there, 1 built, 2 decayed, 3 measured and live
        private static bool _facilityMeasured; // (its walkable spots go into the town's shared grid once)
        /// <summary>The Facility's floors are in the grid: toys can be run there.</summary>
        public static bool FacilityReady { get { return _facilityMeasured || RcWorlds.Egypt; } }

        private void BuildFacility(Transform zoneRoot)
        {
            var f = new GameObject("KE_RcFacility").transform;
            f.SetParent(zoneRoot, false);
            f.localPosition = new Vector3(RcTown.FAC_X, 0f, RcTown.FAC_Z);
            FactoryLayout.Town = true;
            try { FactoryLayout.Build(f); }
            finally { FactoryLayout.Town = false; }
            _facility = 1;
        }

        private static bool FacilityFloor(Collider c)
        {
            string n = c.name;
            return n.IndexOf("loor") >= 0 || n == "Parking lot" || n == "Sidewalk" || n == "Catwalk" || n == "Pit ledge" || n.EndsWith("(ramp)");
        }

        /// <summary>
        /// The Facility wasn't drawn from the town's plan, so where a toy can stand in it is found by looking: a ray
        /// straight down through every square meter of it, a spot on every floor, catwalk and stair it meets that
        /// has room for a body above it. Then a check that everything the plan puts in there can be walked to.
        /// </summary>
        private void MeasureFacility()
        {
            RcNav nav = RcNav.Instance;
            if (!_facilityMeasured)
            {
                _facilityMeasured = true;
                int added = 0;
                float x0 = RcTown.FAC_X + RcTown.FAC_LOT_X0, x1 = RcTown.FAC_X + RcTown.FAC_LOT_X1, z0 = RcTown.FAC_Z + RcTown.FAC_LOT_Z0, z1 = RcTown.FAC_Z + RcTown.FAC_D;
                for (float z = z0 + 0.5f; z < z1; z += 1f)
                    for (float x = x0 + 0.5f; x < x1; x += 1f)
                    {
                        if (!RcTown.InFacility(x, z)) continue;
                        RaycastHit[] hits = Physics.RaycastAll(Rc.W(x, 9.5f, z), Vector3.down, 17f, ~0, QueryTriggerInteraction.Ignore);
                        for (int i = 0; i < hits.Length; i++)
                        {
                            if (hits[i].normal.y < 0.6f || !FacilityFloor(hits[i].collider)) continue;
                            Vector3 at = hits[i].point;
                            if (Physics.CheckSphere(at + Vector3.up * 0.5f, 0.3f, ~0, QueryTriggerInteraction.Ignore) || Physics.CheckSphere(at + Vector3.up * 1.1f, 0.3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                            nav.AddSpot(nav.CellZ(z) * nav.W + nav.CellX(x), at.y - Rc.Origin.y);
                            added++;
                        }
                    }
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Recalled] The Facility: " + added + " places to stand.");
            }
            bool[] seen = nav.Flood(RcTown.GateSides[4, 2], 0f, RcTown.GateSides[4, 3]);
            int spawns = 0, spawnsOk = 0, piles = 0, pilesOk = 0;
            foreach (RcSpawn s in RcTown.Spawns)
            {
                if (s.Zone != 4) continue;
                spawns++;
                if (nav.Reached(seen, s.X, s.Y, s.Z, 5)) spawnsOk++;
                else KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Recalled] The Facility: nothing can walk to the " + RcData.Enemy(s.Enemy).Name + " at " + (s.X - RcTown.FAC_X) + ", " + s.Y + ", " + (s.Z - RcTown.FAC_Z) + ".");
            }
            foreach (RcSpot c in RcTown.Crates)
            {
                if (c.Zone != 4) continue;
                piles++;
                if (nav.Reached(seen, c.X, c.Y, c.Z, 3)) pilesOk++;
                else KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Recalled] The Facility: nothing can walk to the scrap at " + (c.X - RcTown.FAC_X) + ", " + c.Y + ", " + (c.Z - RcTown.FAC_Z) + ".");
            }
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Recalled] The Facility: " + spawnsOk + " of " + spawns + " packs and " + pilesOk + " of " + piles + " scrap piles can be walked to from the gate.");
        }

        private void DestroyZone(int z)
        {
            if (z == 4) _facility = 0;
            for (int i = Uses.Count - 1; i >= 0; i--) if (Uses[i].Zone == z) Uses.RemoveAt(i);
            Destroy(_zoneRoot[z].gameObject);
            _zoneRoot[z] = null;
        }

        private static string Clock(float seconds)
        {
            int s = Mathf.Max(1, Mathf.CeilToInt(seconds));
            return s / 60 + ":" + (s % 60).ToString("00");
        }

        private const float SCRAP_REFILL = 180f; // seconds until a searched pile has scrap again

        private void SearchCrate(int index)
        {
            _crateReadyAt[index] = Time.time + SCRAP_REFILL; // the toys leave more, in time
            int zone = RcTown.Crates[index].Zone, rolls = Random.Range(2, 5);
            Rc.Player.Noise(5f);
            for (int i = 0; i < rolls; i++) Rc.Player.Give(RcData.ScrapLoot(zone, Random.value, Random.value), Random.Range(1, 3));
            Rc.Player.Give(RcData.BATTERY, RcData.ScrapBatteries(zone, Random.value));
            Rc.Music.Jingle(RcMusic.J_PICKUP);
        }

        // ── Gates ────────────────────────────────────────────────────────────
        private void BuildGates()
        {
            Material plank = Mat.Lit(new Color(0.45f, 0.34f, 0.22f), 0.1f), tape = Mat.Lit(new Color(0.85f, 0.62f, 0.05f), 0.2f);
            for (int z = 1; z < 5; z++)
            {
                float x0 = RcTown.Gate[z, 0], z0 = RcTown.Gate[z, 1], x1 = RcTown.Gate[z, 2], z1 = RcTown.Gate[z, 3];
                var go = new GameObject("Gate" + z);
                go.transform.SetParent(_root, false);
                go.transform.localPosition = new Vector3((x0 + x1) * 0.5f, 0f, (z0 + z1) * 0.5f);
                bool alongX = x1 - x0 > z1 - z0;
                float len = alongX ? x1 - x0 : z1 - z0;
                var col = go.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 2.5f, 0f);
                col.size = alongX ? new Vector3(len, 5f, 0.6f) : new Vector3(0.6f, 5f, len);
                // A barricade the kids built: planks and recall tape.
                for (int k = 0; k < 4; k++)
                {
                    float y = 0.5f + k * 0.75f;
                    Mat.Box(go.transform, "Plank", new Vector3(0f, y, 0f), alongX ? new Vector3(len, 0.32f, 0.12f) : new Vector3(0.12f, 0.32f, len), k == 2 ? tape : plank).GetComponent<Collider>().enabled = false;
                }
                for (int k = -2; k <= 2; k++)
                    Mat.Box(go.transform, "Post", alongX ? new Vector3(k * len * 0.24f, 1.6f, 0.1f) : new Vector3(0.1f, 1.6f, k * len * 0.24f), new Vector3(0.18f, 3.2f, 0.18f), plank).GetComponent<Collider>().enabled = false;
                _gate[z] = go;
                int zone = z;
                Uses.Add(new RcUse
                {
                    Pos = new Vector3(RcTown.GateSides[z, 0], 0f, RcTown.GateSides[z, 1]), Radius = 6f,
                    Text = () => RcData.ZoneNames[zone] + ": level " + RecalledRules.GateLevel((RecalledZone)zone) + ", and " + RcData.BossNames[zone - 1] + " beaten",
                    On = () => _gate[zone] != null && _gate[zone].activeSelf,
                    Act = () => Rc.Hud.Feed((RcWorlds.Egypt ? "The Resistance's barricade" : "The kids' barricade") + " won't open for you yet. You're level " + Rc.Char.Level + "."),
                });
            }
        }

        // ── The treehouse: ladders, the bench, the quest board, the stash, the ziplines ──
        private void BuildTreehouseUses()
        {
            float[] y = { 0f, RcTown.DeckY[0], RcTown.DeckY[1], RcTown.DeckY[2] };
            // The ladder: you grab it at the ground or at any deck's hatch, then climb it yourself (RcPlayer).
            for (int level = 0; level < 4; level++)
                Uses.Add(new RcUse { Pos = new Vector3(RcTown.LADDER_X, y[level], -3.2f), Radius = 1.7f, Text = () => "Grab the ladder", On = () => !Rc.Player.Climbing, Act = () => Rc.Player.GrabLadder() });
            Uses.Add(new RcUse { Pos = new Vector3(-4.9f, y[1], 2.4f), Radius = 2.2f, Text = () => "Crafting bench", Act = () => RcCraftMenu.Open() });
            Uses.Add(new RcUse { Pos = new Vector3(4.5f, y[1], 5.2f), Radius = 2.4f, Text = () => Rc.Char.QuestComplete ? "Quest board: hand it in" : "Quest board", Act = () => RcQuestMenu.Open() });
            Uses.Add(new RcUse { Pos = new Vector3(-3.7f, y[2], 2.6f), Radius = 2.2f, Text = () => "Your stash", Act = () => RcStashMenu.Open() });
            for (int f = 0; f < 4; f++)
            {
                int fort = f;
                float dx = RcTown.Fort[f, 0], dz = RcTown.Fort[f, 1], len = Mathf.Sqrt(dx * dx + dz * dz);
                Uses.Add(new RcUse
                {
                    Pos = new Vector3(dx / len * 3.2f, y[3], dz / len * 3.2f), Radius = 1.3f,
                    Text = () => Rc.Char.HasFort(fort) ? "Zipline to " + RcData.ZoneNames[fort].ToLower() : RcData.ZoneNames[fort] + ": no fort there yet",
                    Act = () =>
                    {
                        if (!Rc.Char.HasFort(fort)) { Rc.Hud.Feed("Build the fort in " + RcData.ZoneNames[fort].ToLower() + " first: the line needs somewhere to end."); return; }
                        Zip(new Vector3(RcTown.Fort[fort, 0], 0f, RcTown.Fort[fort, 1]));
                    },
                });
            }
        }

        private void BuildFortUses()
        {
            for (int f = 0; f < 4; f++)
            {
                int fort = f;
                Uses.Add(new RcUse
                {
                    Pos = new Vector3(RcTown.Fort[f, 0], 0f, RcTown.Fort[f, 1]), Radius = 3f,
                    Text = () => Rc.Char.HasFort(fort) ? (RcWorlds.Egypt ? "Zipline to Last Stop" : "Zipline to the treehouse") : (RcWorlds.Egypt ? "The Resistance's outpost (they'll build it when you hand in their first job here)" : "The kids' fort (they'll build it when you hand in their first job here)"),
                    Act = () =>
                    {
                        if (!Rc.Char.HasFort(fort)) { Rc.Hud.Feed("Finish the kids' first job here (\"" + RcData.Quests[RcData.FirstJob(fort)].Title + "\") and hand it in: they build the fort themselves."); return; }
                        Zip(new Vector3(2.6f, RcTown.DeckY[2], 2.6f));
                    },
                });
            }
        }

        /// <summary>
        /// Builds any fort that's due (see RcCharacter.AutoForts) and says so. Called when a job is handed in, and once
        /// on the way out for jobs handed in before forts built themselves.
        /// </summary>
        public void BuildDueForts(bool loud)
        {
            int built = Rc.Char.AutoForts();
            if (built == 0) return;
            for (int z = 0; z < 4; z++)
            {
                if ((built & (1 << z)) == 0) continue;
                if (loud) Rc.Hud.Toast(RcWorlds.Egypt ? "OUTPOST BUILT" : "FORT BUILT", RcData.ZoneNames[z] + ": a zipline runs to " + (RcWorlds.Egypt ? "Last Stop" : "the treehouse") + " now.");
                Rc.Hud.Feed(RcWorlds.Egypt ? "The Resistance built an outpost in " + RcData.ZoneNames[z].ToLower() + ". Use the zipline from it, or from the watchtower's top deck." : "The kids built a fort in " + RcData.ZoneNames[z].ToLower() + ". Use the zipline from its fort, or from the treehouse's top deck.");
            }
            if (loud) Rc.Music.Jingle(RcMusic.J_QUEST);
            Rc.Save();
        }

        private void Zip(Vector3 to)
        {
            Rc.Player.Teleport(to, Rc.Player.Yaw);
            Rc.Music.Jingle(RcMusic.J_ZIP);
        }

        // The kids who are left: they stand on the lower deck by the quest board.
        private void BuildKids()
        {
            float y = RcTown.DeckY[0];
            if (RcWorlds.Egypt)
            {
                // Pip (a yellow Tattletail), by the quest board, and two of the Resistance beside the bench.
                try
                {
                    string label;
                    GameObject pip = PartyTattletailModels.BuildColored(_root.TransformPoint(new Vector3(3.2f, y, 4.2f)), _root.rotation * Quaternion.Euler(0f, 215f, 0f), _root, 1, out label);
                    if (pip != null) pip.name = "KE_RcPip";
                    Kid("KE_RcAlly1", KidGender.Girl, 2, new Vector3(-2.6f, y, 4.9f), 160f);
                    Kid("KE_RcAlly2", KidGender.Boy, 1, new Vector3(5.8f, y, 2.6f), 250f);
                }
                catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Recalled] No one at Last Stop: " + e.Message); }
                return;
            }
            try
            {
                Kid("KE_RcKid1", KidGender.Girl, 1, new Vector3(5.6f, y, 3.6f), 250f);
                Kid("KE_RcKid2", KidGender.Boy, 2, new Vector3(2.6f, y, 5.3f), 170f);
                Kid("KE_RcKid3", KidGender.Boy, 0, new Vector3(-2.2f, y, 4.9f), 200f);
            }
            catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Recalled] No kids in the treehouse: " + e.Message); }
        }

        private void Kid(string name, KidGender gender, int skin, Vector3 at, float yaw)
        {
            GameObject kid = ChildModel.Build(name, gender, skin);
            kid.transform.SetParent(_root, false);
            kid.transform.localPosition = at;
            kid.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>The nearest thing you can use from a spot (town meters), or null.</summary>
        public RcUse NearestUse(Vector3 at)
        {
            RcUse best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < Uses.Count; i++)
            {
                RcUse u = Uses[i];
                if (Mathf.Abs(u.Pos.y - at.y) > 1.6f) continue;
                float dx = u.Pos.x - at.x, dz = u.Pos.z - at.z, d = dx * dx + dz * dz;
                if (d > u.Radius * u.Radius || d >= bestD) continue;
                if (u.On != null && !u.On()) continue;
                best = u; bestD = d;
            }
            return best;
        }

        // ── Night ────────────────────────────────────────────────────────────
        // ── How each place feels: how thick the fog is, what color the dark is, how much the moon gives ──
        // The fog is the game's own (exponential: clear right round you, thickening fast, solid a few dozen meters
        // out), so it lies on everything the same way, the ground, the walls and the sky.
        private struct Mood
        {
            public float Density, Moon;
            public Color Fog, Ambient;
            public Mood(float density, Color fog, Color ambient, float moon) { Density = density; Fog = fog; Ambient = ambient; Moon = moon; }
        }

        private static readonly Mood MoodStreet = new Mood(0.036f, new Color(0.050f, 0.060f, 0.090f), new Color(0.050f, 0.058f, 0.095f), 0.20f);
        private static readonly Mood MoodPark = new Mood(0.024f, new Color(0.070f, 0.082f, 0.115f), new Color(0.085f, 0.095f, 0.140f), 0.30f);   // the treehouse's park is the one place that feels safe
        private static readonly Mood[] MoodZone =
        {
            new Mood(0.040f, new Color(0.050f, 0.062f, 0.100f), new Color(0.048f, 0.055f, 0.092f), 0.17f),   // the suburbs: cold blue
            new Mood(0.044f, new Color(0.045f, 0.095f, 0.078f), new Color(0.040f, 0.065f, 0.060f), 0.12f),   // the mall: sick green
            new Mood(0.048f, new Color(0.110f, 0.042f, 0.048f), new Color(0.070f, 0.040f, 0.045f), 0.12f),   // the school: dried blood
            new Mood(0.042f, new Color(0.105f, 0.070f, 0.042f), new Color(0.068f, 0.052f, 0.040f), 0.15f),   // the junkyard: rust
            new Mood(0.058f, new Color(0.042f, 0.078f, 0.084f), new Color(0.036f, 0.056f, 0.062f), 0.08f),   // the Facility: cold and close
        };

        private static readonly Mood MoodStreetE = new Mood(0.026f, new Color(0.09f, 0.10f, 0.15f), new Color(0.09f, 0.09f, 0.13f), 0.30f);     // the desert at night: clearer, wide, moonlit
        private static readonly Mood MoodParkE = new Mood(0.020f, new Color(0.12f, 0.09f, 0.07f), new Color(0.12f, 0.10f, 0.09f), 0.32f);      // Last Stop: lanterns, warm, safe
        private static readonly Mood[] MoodZoneE =
        {
            new Mood(0.034f, new Color(0.14f, 0.10f, 0.07f), new Color(0.10f, 0.085f, 0.07f), 0.26f),   // the Plateau Road: dust in the air
            new Mood(0.036f, new Color(0.11f, 0.09f, 0.07f), new Color(0.08f, 0.07f, 0.06f), 0.20f),    // the Dig Site: deep trenches
            new Mood(0.034f, new Color(0.08f, 0.10f, 0.12f), new Color(0.07f, 0.08f, 0.10f), 0.24f),    // the Test Village: teal shadow
            new Mood(0.032f, new Color(0.07f, 0.07f, 0.12f), new Color(0.06f, 0.06f, 0.10f), 0.22f),    // the Antenna Field: static blue
            new Mood(0.050f, new Color(0.05f, 0.09f, 0.09f), new Color(0.04f, 0.07f, 0.07f), 0.10f),    // the Pyramid Bunker: close and cold
        };

        private Mood _mood;
        private float _eerieAt;

        private void ApplyMood()
        {
            RenderSettings.ambientLight = _mood.Ambient;
            RenderSettings.fogColor = _mood.Fog;
            RenderSettings.fogDensity = _mood.Density;
            if (_moon != null) { var l = _moon.GetComponent<Light>(); if (l != null) l.intensity = _mood.Moon; }
        }

        /// <summary>The fog and dark drift to where you are; the fog thickens when toys are on you, and breathes a little.</summary>
        private void UpdateMood(Vector3 p, int zone)
        {
            bool park = RcTown.InPark(p.x, p.z);
            Mood t = RcWorlds.Egypt ? (park ? MoodParkE : zone >= 0 ? MoodZoneE[zone] : MoodStreetE) : (park ? MoodPark : zone >= 0 ? MoodZone[zone] : MoodStreet);
            int danger = Rc.Enemies != null ? Rc.Enemies.Danger : 0;
            float thicker = danger >= 2 ? 1.4f : danger == 1 ? 1.15f : 1f;
            float k = 1f - Mathf.Exp(-Time.deltaTime * 0.55f);
            _mood.Density = Mathf.Lerp(_mood.Density, t.Density * thicker, k);
            _mood.Moon = Mathf.Lerp(_mood.Moon, t.Moon, k);
            _mood.Fog = Color.Lerp(_mood.Fog, t.Fog, k);
            _mood.Ambient = Color.Lerp(_mood.Ambient, t.Ambient, k);
            ApplyMood();
            RenderSettings.fogDensity = _mood.Density * FogScale * (1f + 0.07f * Mathf.Sin(Time.time * 0.31f) + 0.03f * Mathf.Sin(Time.time * 0.83f)); // breathes

            // Now and then, something far off in the dark.
            if (Time.time >= _eerieAt)
            {
                _eerieAt = Time.time + Random.Range(14f, 32f);
                if (!park && p.y < 3f && Rc.Music != null && danger == 0)
                {
                    float a = Random.Range(0f, 6.283f), d = Random.Range(24f, 44f);
                    Rc.Music.Chatter(Rc.W(p.x + Mathf.Cos(a) * d, p.y, p.z + Mathf.Sin(a) * d), Random.Range(0, 5));
                }
            }
        }

        private bool _atmosphere, _savedFog, _savedPpFog, _savedDof;
        private Color _savedFogColor, _savedAmbient;
        private FogMode _savedFogMode;
        private float _savedFogStart, _savedFogEnd, _savedFogDensity;
        private UnityEngine.Rendering.AmbientMode _savedAmbientMode;
        private UnityEngine.PostProcessing.PostProcessingProfile _profile;
        private GameObject _moon;

        private void SetAtmosphere()
        {
            _savedAmbientMode = RenderSettings.ambientMode;
            _savedAmbient = RenderSettings.ambientLight;
            _savedFog = RenderSettings.fog; _savedFogColor = RenderSettings.fogColor; _savedFogMode = RenderSettings.fogMode;
            _savedFogStart = RenderSettings.fogStartDistance; _savedFogEnd = RenderSettings.fogEndDistance; _savedFogDensity = RenderSettings.fogDensity;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            // Night fog, close and cold: you see a few houses ahead and the rest is the dark. Each place has its own
            // (see UpdateMood); this is where it starts.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            _mood = RcWorlds.Egypt ? MoodStreetE : MoodStreet;
            ApplyMood();
            var cam = FactoryController.PlayerCam;
            var ppb = cam != null ? cam.GetComponent<UnityEngine.PostProcessing.PostProcessingBehaviour>() : null;
            if (ppb != null && ppb.profile != null)
            {
                _profile = ppb.profile;
                _savedPpFog = _profile.fog.enabled;
                _savedDof = _profile.depthOfField.enabled;
                _profile.fog.enabled = true;
                _profile.depthOfField.enabled = false; // (third person: it would blur your own kid)
            }
            // The moon: just enough to read shapes off the street.
            _moon = new GameObject("KE_RcMoon");
            _moon.transform.SetParent(_root, false);
            _moon.transform.rotation = Quaternion.Euler(48f, 205f, 0f);
            var ml = _moon.AddComponent<Light>();
            ml.type = LightType.Directional;
            ml.color = new Color(0.55f, 0.65f, 0.95f);
            ml.intensity = _mood.Moon;
            ml.shadows = LightShadows.None;
            _atmosphere = true;
        }

        private void RestoreAtmosphere()
        {
            if (!_atmosphere) return;
            _atmosphere = false;
            RenderSettings.ambientMode = _savedAmbientMode;
            RenderSettings.ambientLight = _savedAmbient;
            RenderSettings.fog = _savedFog; RenderSettings.fogColor = _savedFogColor; RenderSettings.fogMode = _savedFogMode;
            RenderSettings.fogStartDistance = _savedFogStart; RenderSettings.fogEndDistance = _savedFogEnd; RenderSettings.fogDensity = _savedFogDensity;
            if (_profile != null) { _profile.fog.enabled = _savedPpFog; _profile.depthOfField.enabled = _savedDof; }
        }
    }

    /// <summary>
    /// Turns the town's layout into what Unity draws and collides with: one mesh per material (so a whole zone is a
    /// handful of draw calls), every solid box as a BoxCollider on one object, and the lights.
    /// Surfaces are textured the way the Facility's are (its brick, asphalt, concrete, wood and steel, tinted for
    /// the town): every face gets texture coordinates from where it is in the world, so a texture keeps its
    /// real size and runs on unbroken from one box to the next.
    /// </summary>
    /// <summary>The town's bigger lamps, so the lighting can find the ones near you.</summary>
    public static class RcLamps
    {
        private static readonly List<Light> _all = new List<Light>();

        public static void Add(Light l) { _all.Add(l); }

        /// <summary>The nearest 'count' lamps within 'range' of a world point (dead lamps and destroyed zones skipped).</summary>
        public static List<Light> Near(Vector3 at, float range, int count)
        {
            var found = new List<Light>();
            var dist = new List<float>();
            for (int i = _all.Count - 1; i >= 0; i--)
            {
                Light l = _all[i];
                if (l == null) { _all.RemoveAt(i); continue; }
                if (!l.enabled || l.intensity < 0.2f) continue;
                float d = (l.transform.position - at).sqrMagnitude;
                if (d > range * range) continue;
                int k = 0;
                while (k < dist.Count && dist[k] < d) k++;
                found.Insert(k, l); dist.Insert(k, d);
                if (found.Count > count) { found.RemoveAt(found.Count - 1); dist.RemoveAt(dist.Count - 1); }
            }
            return found;
        }
    }

    public class RcMeshSink : RcSink
    {
        private class Buf
        {
            public readonly List<Vector3> V = new List<Vector3>(), N = new List<Vector3>();
            public readonly List<Vector2> U = new List<Vector2>();
            public readonly List<Vector4> Tan = new List<Vector4>();
            public readonly List<int> T = new List<int>();
            public readonly List<Mesh> Done = new List<Mesh>();
            public float Tile = 1f;
        }

        private struct Col { public Vector3 C, S; public bool Capsule; public float R, H; }
        private struct Lamp { public Vector3 P; public int Color; public float Range, Intensity; }

        private readonly Dictionary<RcMat, Buf> _bufs = new Dictionary<RcMat, Buf>();
        private readonly List<Col> _cols = new List<Col>();
        private readonly List<Lamp> _lamps = new List<Lamp>();

        private Buf For(RcMat m)
        {
            Buf b;
            if (!_bufs.TryGetValue(m, out b)) { b = new Buf(); b.Tile = Look(m).Tile; _bufs[m] = b; }
            if (b.V.Count > 58000) Flush(b);
            return b;
        }

        private static void Flush(Buf b)
        {
            if (b.V.Count == 0) return;
            var mesh = new Mesh { name = "KE_RcTown" };
            mesh.SetVertices(b.V);
            mesh.SetNormals(b.N);
            mesh.SetUVs(0, b.U);
            mesh.SetTangents(b.Tan);
            mesh.SetTriangles(b.T, 0);
            mesh.RecalculateBounds();
            b.Done.Add(mesh);
            b.V.Clear(); b.N.Clear(); b.U.Clear(); b.Tan.Clear(); b.T.Clear();
        }

        private static void Vert(Buf b, Vector3 p, Vector3 n, Vector3 u, Vector3 w)
        {
            b.V.Add(p); b.N.Add(n);
            float t = b.Tile > 0.001f ? b.Tile : 1f;
            b.U.Add(new Vector2(Vector3.Dot(p, u) / t, Vector3.Dot(p, w) / t));
            b.Tan.Add(new Vector4(u.x, u.y, u.z, Vector3.Dot(Vector3.Cross(n, u), w) < 0f ? -1f : 1f));
        }

        // A flat face (its corners going round so that it faces out). The texture lies flat on floors and roofs and
        // stands upright on walls.
        private static void Quad(Buf b, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            Vector3 n = Vector3.Cross(p1 - p0, p2 - p0).normalized;
            Vector3 u, w;
            if (Mathf.Abs(n.y) > 0.9f) { u = Vector3.right; w = Vector3.forward; }
            else { u = Vector3.Cross(Vector3.up, n).normalized; w = Vector3.up; }
            int i = b.V.Count;
            Vert(b, p0, n, u, w); Vert(b, p1, n, u, w); Vert(b, p2, n, u, w); Vert(b, p3, n, u, w);
            b.T.Add(i); b.T.Add(i + 1); b.T.Add(i + 2); b.T.Add(i); b.T.Add(i + 2); b.T.Add(i + 3);
        }

        public override void Box(RcMat m, float x0, float y0, float z0, float x1, float y1, float z1, bool collide)
        {
            if (collide) _cols.Add(new Col { C = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f), S = new Vector3(x1 - x0, y1 - y0, z1 - z0) });
            if (m == RcMat.None) return;
            Buf b = For(m);
            Vector3 a = new Vector3(x0, y0, z0), c = new Vector3(x1, y0, z0), d = new Vector3(x1, y0, z1), e = new Vector3(x0, y0, z1);
            Vector3 f = new Vector3(x0, y1, z0), g = new Vector3(x1, y1, z0), h = new Vector3(x1, y1, z1), k = new Vector3(x0, y1, z1);
            Quad(b, f, k, h, g);                 // top
            if (y0 > 0.001f || y0 < -0.001f) Quad(b, a, c, d, e); // bottom (not for things standing on the ground)
            Quad(b, a, f, g, c);                 // south
            Quad(b, d, h, k, e);                 // north
            Quad(b, e, k, f, a);                 // west
            Quad(b, c, g, h, d);                 // east
        }

        public override void Walk(RcMat m, float x0, float y0, float z0, float x1, float y1, float z1) { Box(m, x0, y0, z0, x1, y1, z1, true); }

        public override void Cyl(RcMat m, float x, float y0, float z, float radius, float height, bool collide)
        {
            if (collide)
            {
                if (height >= radius * 2f) _cols.Add(new Col { C = new Vector3(x, y0 + height * 0.5f, z), Capsule = true, R = radius, H = height });
                else _cols.Add(new Col { C = new Vector3(x, y0 + height * 0.5f, z), S = new Vector3(radius * 1.75f, height, radius * 1.75f) });
            }
            if (m == RcMat.None) return;
            Buf b = For(m);
            const int N = 16;
            float t = b.Tile > 0.001f ? b.Tile : 1f;
            // The side: smooth all the way round, the texture wrapped about it.
            int start = b.V.Count;
            for (int i = 0; i <= N; i++)
            {
                float a = i * Mathf.PI * 2f / N, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                var n = new Vector3(cs, 0f, sn);
                var tan = new Vector4(-sn, 0f, cs, -1f);
                for (int k = 0; k < 2; k++)
                {
                    b.V.Add(new Vector3(x + cs * radius, y0 + k * height, z + sn * radius));
                    b.N.Add(n);
                    b.U.Add(new Vector2(a * radius / t, (y0 + k * height) / t));
                    b.Tan.Add(tan);
                }
            }
            for (int i = 0; i < N; i++)
            {
                int i0 = start + i * 2, i1 = i0 + 2;
                b.T.Add(i0); b.T.Add(i0 + 1); b.T.Add(i1 + 1); b.T.Add(i0); b.T.Add(i1 + 1); b.T.Add(i1);
            }
            // The top cap (its own vertices, so the edge stays crisp).
            int cap = b.V.Count;
            Vert(b, new Vector3(x, y0 + height, z), Vector3.up, Vector3.right, Vector3.forward);
            for (int i = 0; i < N; i++) { float a = i * Mathf.PI * 2f / N; Vert(b, new Vector3(x + Mathf.Cos(a) * radius, y0 + height, z + Mathf.Sin(a) * radius), Vector3.up, Vector3.right, Vector3.forward); }
            for (int i = 0; i < N; i++) { b.T.Add(cap); b.T.Add(cap + 1 + (i + 1) % N); b.T.Add(cap + 1 + i); }
        }

        public override void Ball(RcMat m, float x, float y, float z, float rx, float ry, float rz)
        {
            if (m == RcMat.None) return;
            Buf b = For(m);
            const int LON = 14, LAT = 8;
            float t = b.Tile > 0.001f ? b.Tile : 1f;
            int start = b.V.Count;
            for (int j = 0; j <= LAT; j++)
            {
                float v = j * Mathf.PI / LAT - Mathf.PI * 0.5f, cy = Mathf.Sin(v), cr = Mathf.Cos(v);
                for (int i = 0; i <= LON; i++)
                {
                    float a = i * Mathf.PI * 2f / LON, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                    b.V.Add(new Vector3(x + cs * cr * rx, y + cy * ry, z + sn * cr * rz));
                    b.N.Add(new Vector3(cs * cr / rx, cy / ry, sn * cr / rz).normalized);
                    // (sand is mapped from above, like the ground it lies on: a sphere's own map streaks at the poles)
                    b.U.Add(m == RcMat.Sand ? new Vector2((x + cs * cr * rx) / t, (z + sn * cr * rz) / t) : new Vector2(a * (rx + rz) * 0.5f / t, v * ry / t));
                    b.Tan.Add(m == RcMat.Sand ? new Vector4(1f, 0f, 0f, -1f) : new Vector4(-sn, 0f, cs, -1f));
                }
            }
            for (int j = 0; j < LAT; j++)
                for (int i = 0; i < LON; i++)
                {
                    int p0 = start + j * (LON + 1) + i, p1 = p0 + 1, p2 = p0 + LON + 1, p3 = p2 + 1;
                    b.T.Add(p0); b.T.Add(p2); b.T.Add(p3); b.T.Add(p0); b.T.Add(p3); b.T.Add(p1);
                }
        }

        public override void Light(float x, float y, float z, int color, float range, float intensity)
        {
            _lamps.Add(new Lamp { P = new Vector3(x, y, z), Color = color, Range = range, Intensity = intensity });
        }

        private static readonly Color[] LightColors =
        {
            new Color(1f, 0.82f, 0.5f), new Color(1f, 0.3f, 0.6f), new Color(0.5f, 0.65f, 1f), new Color(1f, 0.2f, 0.15f), new Color(0.6f, 1f, 0.6f),
        };

        /// <summary>Makes the objects under 'root' (whose origin is the town's).</summary>
        public void Finish(Transform root, string name)
        {
            int meshes = 0;
            foreach (var pair in _bufs)
            {
                Flush(pair.Value);
                Material mat = MaterialFor(pair.Key);
                for (int i = 0; i < pair.Value.Done.Count; i++)
                {
                    var go = new GameObject(pair.Key.ToString());
                    go.transform.SetParent(root, false);
                    go.AddComponent<MeshFilter>().sharedMesh = pair.Value.Done[i];
                    go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                    meshes++;
                }
            }
            var cols = new GameObject("Colliders");
            cols.transform.SetParent(root, false);
            for (int i = 0; i < _cols.Count; i++)
            {
                if (_cols[i].Capsule)
                {
                    var cc = cols.AddComponent<CapsuleCollider>();
                    cc.center = _cols[i].C; cc.radius = _cols[i].R; cc.height = _cols[i].H; cc.direction = 1;
                }
                else
                {
                    var bc = cols.AddComponent<BoxCollider>();
                    bc.center = _cols[i].C; bc.size = _cols[i].S;
                }
            }
            for (int i = 0; i < _lamps.Count; i++)
            {
                var go = new GameObject("Light");
                go.transform.SetParent(root, false);
                go.transform.localPosition = _lamps[i].P;
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = LightColors[_lamps[i].Color];
                l.range = _lamps[i].Range;
                l.intensity = _lamps[i].Intensity;
                l.shadows = LightShadows.None;
                if (_lamps[i].Range >= 9f) RcLamps.Add(l);
                // Some of the warm lamps are failing (chosen by where they are, so the same ones every time).
                if (_lamps[i].Color == 0 && _lamps[i].Range >= 9f)
                {
                    double v = System.Math.Sin(_lamps[i].P.x * 12.9898 + _lamps[i].P.z * 78.233) * 43758.5453;
                    if (v - System.Math.Floor(v) < 0.4) go.AddComponent<RcFlicker>();
                }
            }
            if (KaleidoscopeEndlessPlugin.RecalledGraphics) { try { Halos(root); } catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Recalled] No lamp glow: " + e.Message); } }
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Recalled] " + name + ": " + meshes + " meshes, " + _cols.Count + " colliders, " + _lamps.Count + " lights.");
        }

        private static Material _haloMat;

        /// <summary>A soft glow round every lamp (one particle each, in one system): lamps look lit in the fog, not just bright where they land.</summary>
        private void Halos(Transform root)
        {
            if (_lamps.Count == 0) return;
            if (_haloMat == null)
            {
                Shader sh = Shader.Find("Particles/Additive");
                if (sh == null) return;
                const int N = 64;
                var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f, d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - d);
                        float v = a * a * a * 0.8f + (d < 0.12f ? (1f - d / 0.12f) * 0.5f : 0f);   // a soft falloff with a hot core
                        byte c = (byte)(Mathf.Clamp01(v) * 255f);
                        px[y * N + x] = new Color32(c, c, c, c);
                    }
                t.SetPixels32(px);
                t.wrapMode = TextureWrapMode.Clamp;
                t.Apply();
                _haloMat = new Material(sh) { mainTexture = t };
            }
            var go = new GameObject("Halos");
            go.transform.SetParent(root, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true; main.playOnAwake = true;
            main.maxParticles = _lamps.Count + 8;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSpeed = 0f;
            var em = ps.emission; em.enabled = false;
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.material = _haloMat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            for (int i = 0; i < _lamps.Count; i++)
            {
                Lamp l = _lamps[i];
                if (l.Range < 5f) continue;                       // (a floor lamp's glow is its light)
                Color c = LightColors[l.Color];
                float size = Mathf.Clamp(l.Range * 0.34f, 1.6f, 6f);
                ps.Emit(l.P, Vector3.zero, size, 1000000f, new Color32((byte)(c.r * 130f), (byte)(c.g * 130f), (byte)(c.b * 130f), 255));
            }
            ps.Play();
        }

        // ── What everything is made of ───────────────────────────────────────
        private static readonly Dictionary<RcMat, FMat> _looks = new Dictionary<RcMat, FMat>();

        private static FMat Tex(string key, float tile, float r, float g, float b, float gloss, float metal, float bump) { return FTex.Get(key, tile, new Color(r, g, b), gloss, metal, bump); }

        /// <summary>A material and how many meters one repeat of its texture covers.</summary>
        private static FMat Look(RcMat m)
        {
            FMat f;
            if (_looks.TryGetValue(m, out f) && f != null && f.Mat != null) return f;
            try
            {
                switch (m)
                {
                    // The ground.
                    case RcMat.Grass: f = Tex("dirt", 5f, 0.30f, 0.46f, 0.26f, 0.05f, 0f, 1f); break;
                    case RcMat.Lawn: f = Tex("dirt", 3f, 0.36f, 0.62f, 0.30f, 0.05f, 0f, 0.8f); break;
                    case RcMat.Road: f = Tex("asphalt", 4f, 0.42f, 0.42f, 0.44f, 0.22f, 0f, 1.2f); break;
                    case RcMat.Walk: f = Tex("concrete", 3f, 0.62f, 0.62f, 0.62f, 0.15f, 0f, 1.2f); break;
                    case RcMat.Path: f = Tex("dirt", 3f, 0.80f, 0.70f, 0.52f, 0.05f, 0f, 1f); break;
                    case RcMat.Dirt: f = Tex("dirt", 4f, 0.55f, 0.45f, 0.36f, 0.05f, 0f, 1.2f); break;
                    // Wood, metal, stone.
                    case RcMat.Bark: f = Tex("darkwood", 1.4f, 0.62f, 0.50f, 0.42f, 0.08f, 0f, 1.6f); break;
                    case RcMat.Leaf: f = Tex("fabric", 1.8f, 0.24f, 0.50f, 0.22f, 0.03f, 0f, 1.5f); break;
                    case RcMat.Wood: f = Tex("wood", 1.4f, 0.85f, 0.72f, 0.60f, 0.25f, 0f, 1f); break;
                    case RcMat.Plank: f = Tex("wood", 1.8f, 1f, 0.90f, 0.74f, 0.15f, 0f, 1.2f); break;
                    case RcMat.Door: f = Tex("darkwood", 1.2f, 0.80f, 0.62f, 0.52f, 0.35f, 0f, 1f); break;
                    case RcMat.Crate: f = Tex("chip", 1.2f, 0.95f, 0.85f, 0.70f, 0.1f, 0f, 1f); break;
                    case RcMat.Metal: f = Tex("steel", 2.5f, 0.55f, 0.57f, 0.62f, 0.45f, 0.5f, 1f); break;
                    case RcMat.Shelf: f = Tex("plate", 1.2f, 0.45f, 0.52f, 0.62f, 0.4f, 0.5f, 1f); break;
                    case RcMat.Rust: f = Tex("steel", 2.5f, 0.78f, 0.42f, 0.26f, 0.15f, 0.2f, 1.4f); break;
                    case RcMat.Roof: f = Tex("steel", 3f, 0.24f, 0.21f, 0.22f, 0.2f, 0.2f, 1.4f); break;
                    case RcMat.Stone: f = Tex("concrete", 2.5f, 0.80f, 0.80f, 0.82f, 0.2f, 0f, 1.3f); break;
                    case RcMat.Concrete: f = Tex("concrete", 3.5f, 0.66f, 0.65f, 0.64f, 0.12f, 0f, 1.3f); break;
                    // Walls.
                    case RcMat.Brick: f = Tex("block", 2.2f, 0.78f, 0.40f, 0.31f, 0.1f, 0f, 1.6f); break;
                    case RcMat.House: f = Tex("block", 2.6f, 0.58f, 0.60f, 0.70f, 0.08f, 0f, 1.2f); break;
                    case RcMat.HouseB: f = Tex("block", 2.6f, 0.54f, 0.66f, 0.56f, 0.08f, 0f, 1.2f); break;
                    case RcMat.HouseC: f = Tex("block", 2.6f, 0.74f, 0.62f, 0.50f, 0.08f, 0f, 1.2f); break;
                    // Floors.
                    case RcMat.Floor: f = Tex("concrete", 3.5f, 0.58f, 0.55f, 0.50f, 0.2f, 0f, 1f); break;
                    case RcMat.Tile: f = Tex("lobby", 1.6f, 0.78f, 0.78f, 0.74f, 0.4f, 0f, 0.8f); break;
                    case RcMat.Carpet: f = Tex("fabric", 1.2f, 0.62f, 0.24f, 0.24f, 0.02f, 0f, 1f); break;
                    case RcMat.Fabric: f = Tex("fabric", 0.8f, 0.55f, 0.38f, 0.66f, 0.02f, 0f, 1f); break;
                    case RcMat.Tape: f = Tex("hazard", 1f, 0.95f, 0.85f, 0.55f, 0.2f, 0f, 0.6f); break;
                    case RcMat.Sandstone: f = Tex("block", 2.4f, 0.80f, 0.66f, 0.44f, 0.08f, 0f, 1.5f); break;
                    case RcMat.Adobe: f = Tex("concrete", 3f, 0.68f, 0.50f, 0.34f, 0.05f, 0f, 1.5f); break;
                    case RcMat.Canvas: f = Tex("fabric", 1.5f, 0.84f, 0.78f, 0.62f, 0.03f, 0f, 1f); break;
                }
            }
            catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Recalled] No texture for " + m + ": " + e.Message); f = null; }
            if (f == null)
            {
                // Paint, glass and things that glow: plain.
                switch (m)
                {
                    case RcMat.Line: f = FTex.Plain(new Color(0.75f, 0.68f, 0.38f), 0.1f); break;
                    case RcMat.Window: f = FTex.Plain(new Color(0.05f, 0.07f, 0.11f), 0.92f); break;
                    case RcMat.Glass: f = FTex.Plain(new Color(0.10f, 0.15f, 0.19f), 0.95f); break;
                    case RcMat.Glow: f = FTex.Glow(new Color(1f, 0.85f, 0.5f)); break;
                    case RcMat.Neon: f = FTex.Glow(new Color(1f, 0.25f, 0.6f)); break;
                    case RcMat.RedGlow: f = FTex.Glow(new Color(1f, 0.15f, 0.1f)); break;
                    case RcMat.BlueGlow: f = FTex.Glow(new Color(0.3f, 0.6f, 1f)); break;
                    case RcMat.CarA: f = FTex.Plain(new Color(0.72f, 0.58f, 0.10f), 0.75f); break;
                    case RcMat.CarB: f = FTex.Plain(new Color(0.16f, 0.30f, 0.56f), 0.75f); break;
                    case RcMat.CarC: f = FTex.Plain(new Color(0.58f, 0.13f, 0.12f), 0.75f); break;
                    case RcMat.Black: f = FTex.Plain(new Color(0.03f, 0.03f, 0.035f), 0.35f); break;
                    case RcMat.Paper: f = FTex.Plain(new Color(0.80f, 0.77f, 0.66f), 0.05f); break;
                    case RcMat.Cardboard: f = FTex.Plain(new Color(0.50f, 0.36f, 0.22f), 0.05f); break;
                    case RcMat.Orange: f = FTex.Plain(new Color(0.85f, 0.32f, 0.05f), 0.3f); break;
                    case RcMat.Pink: f = FTex.Plain(new Color(0.80f, 0.40f, 0.52f), 0.2f); break;
                    case RcMat.White: f = FTex.Plain(new Color(0.72f, 0.74f, 0.78f), 0.3f); break;
                    case RcMat.Teal: f = FTex.Plain(new Color(0.12f, 0.50f, 0.50f), 0.3f); break;
                    case RcMat.Gilt: f = FTex.Plain(new Color(0.88f, 0.68f, 0.18f), 0.8f); break;
                    case RcMat.Lapis: f = FTex.Plain(new Color(0.08f, 0.22f, 0.62f), 0.6f); break;
                    default: f = FTex.Plain(FlatColor(m), 0.1f); break; // (a texture that wouldn't load)
                }
            }
            _looks[m] = f;
            return f;
        }

        public static Material MaterialFor(RcMat m) { return Look(m).Mat; }

        /// <summary>A material's color with no texture: what the map (M) paints with.</summary>
        public static Color FlatColor(RcMat m)
        {
            switch (m)
            {
                case RcMat.Grass: return new Color(0.06f, 0.11f, 0.07f);
                case RcMat.Lawn: return new Color(0.08f, 0.15f, 0.09f);
                case RcMat.Road: return new Color(0.07f, 0.07f, 0.08f);
                case RcMat.Line: return new Color(0.55f, 0.5f, 0.3f);
                case RcMat.Walk: return new Color(0.2f, 0.2f, 0.21f);
                case RcMat.Path: return new Color(0.3f, 0.27f, 0.22f);
                case RcMat.Dirt: return new Color(0.17f, 0.13f, 0.1f);
                case RcMat.Bark: return new Color(0.2f, 0.13f, 0.08f);
                case RcMat.Leaf: return new Color(0.07f, 0.17f, 0.08f);
                case RcMat.Wood: return new Color(0.42f, 0.28f, 0.17f);
                case RcMat.Plank: return new Color(0.45f, 0.34f, 0.22f);
                case RcMat.Metal: return new Color(0.22f, 0.23f, 0.26f);
                case RcMat.Rust: return new Color(0.36f, 0.2f, 0.12f);
                case RcMat.Stone: return new Color(0.35f, 0.35f, 0.37f);
                case RcMat.Concrete: return new Color(0.4f, 0.4f, 0.41f);
                case RcMat.Brick: return new Color(0.36f, 0.17f, 0.13f);
                case RcMat.House: return new Color(0.27f, 0.27f, 0.31f);
                case RcMat.HouseB: return new Color(0.24f, 0.29f, 0.25f);
                case RcMat.HouseC: return new Color(0.32f, 0.27f, 0.22f);
                case RcMat.Roof: return new Color(0.09f, 0.07f, 0.07f);
                case RcMat.Door: return new Color(0.2f, 0.12f, 0.08f);
                case RcMat.Window: return new Color(0.04f, 0.06f, 0.09f);
                case RcMat.Floor: return new Color(0.33f, 0.31f, 0.27f);
                case RcMat.Tile: return new Color(0.5f, 0.5f, 0.47f);
                case RcMat.Carpet: return new Color(0.3f, 0.13f, 0.13f);
                case RcMat.Glow: return new Color(1f, 0.85f, 0.5f);
                case RcMat.Neon: return new Color(1f, 0.25f, 0.6f);
                case RcMat.RedGlow: return new Color(1f, 0.15f, 0.1f);
                case RcMat.BlueGlow: return new Color(0.3f, 0.6f, 1f);
                case RcMat.Tape: return new Color(0.85f, 0.62f, 0.05f);
                case RcMat.CarA: return new Color(0.6f, 0.5f, 0.1f);
                case RcMat.CarB: return new Color(0.15f, 0.28f, 0.5f);
                case RcMat.CarC: return new Color(0.5f, 0.13f, 0.12f);
                case RcMat.Glass: return new Color(0.1f, 0.14f, 0.17f);
                case RcMat.Shelf: return new Color(0.2f, 0.24f, 0.29f);
                case RcMat.Crate: return new Color(0.45f, 0.33f, 0.2f);
                case RcMat.Fabric: return new Color(0.3f, 0.2f, 0.36f);
                case RcMat.Paper: return new Color(0.4f, 0.38f, 0.32f);
                case RcMat.Cardboard: return new Color(0.3f, 0.22f, 0.13f);
                case RcMat.Orange: return new Color(0.5f, 0.2f, 0.04f);
                case RcMat.Pink: return new Color(0.45f, 0.22f, 0.3f);
                case RcMat.White: return new Color(0.4f, 0.41f, 0.44f);
                case RcMat.Teal: return new Color(0.07f, 0.3f, 0.3f);
                case RcMat.Sand: return new Color(0.2f, 0.16f, 0.1f);
                case RcMat.Packed: return new Color(0.17f, 0.13f, 0.08f);
                case RcMat.Sandstone: return new Color(0.3f, 0.24f, 0.15f);
                case RcMat.Adobe: return new Color(0.26f, 0.18f, 0.11f);
                case RcMat.Canvas: return new Color(0.34f, 0.31f, 0.24f);
                case RcMat.Gilt: return new Color(0.45f, 0.34f, 0.08f);
                case RcMat.Lapis: return new Color(0.04f, 0.1f, 0.3f);
                default: return new Color(0.02f, 0.02f, 0.025f);
            }
        }
    }
}
