using System.Collections.Generic;
using Steamworks;
using UnityEngine;

namespace KaleidoscopeEndless
{
    public enum RcPlay { Solo, Friends, Online }

    /// <summary>
    /// Recalled's own Steam lobby: up to four players in one town. Separate from the two-player lobby Versus and
    /// Air Hockey use (MpLobby hands this class every Steam lobby event that belongs to it; a player is only
    /// ever in one of the two).
    ///   Friends: a friends-only lobby you invite people to (the party screen, P, in the town).
    ///   Online:  looks for a public town on the same version, nearest your own progress, with room; if there's
    ///            none it opens one and others drop in.
    /// Whoever owns the lobby runs the toys. When they leave, Steam passes the lobby to someone else and that
    /// player takes over.
    /// </summary>
    public static class RcLobby
    {
        public const int SIZE = 4;
        private const string KEY = "ke_rc", KEY_NET = "ke_net", KEY_ZONE = "ke_zone", KEY_WORLD = "ke_world";

        public static CSteamID Id = CSteamID.Nil;
        public static bool Exists { get { return Id.m_SteamID != 0; } }
        public static bool Creating { get; private set; }
        public static bool Searching { get; private set; }
        public static bool Public { get; private set; }
        /// <summary>What the door and the party screen say is going on.</summary>
        public static string Status = "";

        private static CSteamID _joining = CSteamID.Nil;
        private static bool _inviteWhenCreated, _launchWhenArcade, _createPublic;
        private static Callback<LobbyMatchList_t> _listCallback;
        private static float _zoneAt;

        public static bool IsHost { get { return !Exists || SteamMatchmaking.GetLobbyOwner(Id) == SteamUser.GetSteamID(); } }
        public static int Members { get { return Exists ? SteamMatchmaking.GetNumLobbyMembers(Id) : 1; } }

        public static bool IsMember(CSteamID who)
        {
            if (!Exists) return false;
            int n = SteamMatchmaking.GetNumLobbyMembers(Id);
            for (int i = 0; i < n; i++) if (SteamMatchmaking.GetLobbyMemberByIndex(Id, i) == who) return true;
            return false;
        }

        /// <summary>Everyone else in the town's lobby, by name (wherever they are: outside already, or on the way).</summary>
        public static string[] OtherNames()
        {
            var names = new List<string>();
            if (Exists)
            {
                CSteamID me = SteamUser.GetSteamID();
                int n = SteamMatchmaking.GetNumLobbyMembers(Id);
                for (int i = 0; i < n; i++)
                {
                    CSteamID m = SteamMatchmaking.GetLobbyMemberByIndex(Id, i);
                    if (m != me) names.Add(SteamFriends.GetFriendPersonaName(m));
                }
            }
            return names.ToArray();
        }

        /// <summary>Is this lobby Recalled's (so MpLobby leaves it alone)?</summary>
        public static bool Claims(CSteamID lobby)
        {
            if (lobby.m_SteamID == 0) return false;
            return lobby == Id || lobby == _joining || SteamMatchmaking.GetLobbyData(lobby, KEY) == "1";
        }

        private static void OneLobbyOnly()
        {
            Leave();
            if (MpLobby.Instance != null && MpLobby.Instance.LobbyExists) MpLobby.Instance.LeaveLobby();
        }

        /// <summary>A friends-only town. 'invite' opens Steam's invite window as soon as it exists.</summary>
        public static void OpenForFriends(bool invite)
        {
            if (Exists) { if (invite) Invite(); return; }
            if (Creating) return;
            OneLobbyOnly();
            Creating = true; Public = false; _inviteWhenCreated = invite;
            Status = "Opening your town to friends...";
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, SIZE);
        }

        public static void Invite() { if (Exists) SteamFriends.ActivateGameOverlayInviteDialog(Id); }

        /// <summary>Find a public town to drop into; open one if there's none.</summary>
        public static void GoOnline(int myZone)
        {
            if (Creating || Searching) return;
            OneLobbyOnly();
            if (_listCallback == null) _listCallback = Callback<LobbyMatchList_t>.Create(OnList);
            Searching = true;
            Status = "Looking for a town with room...";
            SteamMatchmaking.AddRequestLobbyListStringFilter(KEY, "1", ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(KEY_NET, VersionTag.NET_VERSION, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);
            SteamMatchmaking.AddRequestLobbyListNearValueFilter(KEY_ZONE, myZone); // nearest your own progress first
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.RequestLobbyList();
        }

        private static void OnList(LobbyMatchList_t list)
        {
            if (!Searching) return; // (somebody else's search)
            Searching = false;
            if (list.m_nLobbiesMatching > 0)
            {
                _joining = SteamMatchmaking.GetLobbyByIndex(0);
                Status = "Found one. Joining...";
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Recalled] " + list.m_nLobbiesMatching + " public town(s); joining " + _joining + ".");
                SteamMatchmaking.JoinLobby(_joining);
                return;
            }
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Recalled] No public town with room: opening one.");
            Creating = true; Public = true; _inviteWhenCreated = false; _createPublic = true;
            Status = "Nobody's out there yet. Your town is open: others can drop in.";
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, SIZE);
        }

        // ── Steam's lobby events (from MpLobby) ──────────────────────────────
        public static void OnCreated(LobbyCreated_t cb)
        {
            Creating = false;
            if (cb.m_eResult != EResult.k_EResultOK)
            {
                Status = "Steam couldn't open a lobby (" + cb.m_eResult + "). You're on your own.";
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Recalled] Lobby creation failed: " + cb.m_eResult);
                return;
            }
            Id = new CSteamID(cb.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(Id, KEY, "1");
            SteamMatchmaking.SetLobbyData(Id, KEY_NET, VersionTag.NET_VERSION);
            PublishZone();
            if (!_createPublic) Status = "Your town is open to friends.";
            _createPublic = false;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Recalled] Lobby " + Id + " open (" + (Public ? "public" : "friends") + ").");
            if (_inviteWhenCreated) { _inviteWhenCreated = false; Invite(); }
        }

        public static void OnEntered(LobbyEnter_t cb)
        {
            var lobby = new CSteamID(cb.m_ulSteamIDLobby);
            _joining = CSteamID.Nil;
            if ((EChatRoomEnterResponse)cb.m_EChatRoomEnterResponse != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                Status = "Couldn't join that town. You're on your own.";
                Say(Status);
                return;
            }
            if (lobby == Id) return; // (our own, just created)
            if (SteamMatchmaking.GetLobbyData(lobby, KEY_NET) != VersionTag.NET_VERSION)
            {
                SteamMatchmaking.LeaveLobby(lobby);
                Status = "That town is on another version of After Hours. Both need the same one.";
                Say(Status);
                return;
            }
            if (MpLobby.Instance != null && MpLobby.Instance.LobbyExists) MpLobby.Instance.LeaveLobby();
            Id = lobby;
            Public = false;
            string owner = SteamFriends.GetFriendPersonaName(SteamMatchmaking.GetLobbyOwner(Id));
            Status = "You're in " + owner + "'s town.";
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Recalled] Joined lobby " + Id + " (" + owner + "'s).");
            Say(Status);
            // Already out there, but the host is in the other world: follow.
            if (RecalledMode.Active && HostWorld != RcWorlds.Now) { RecalledMode.Travel(HostWorld); return; }
            // Not out there yet: go. (From the menu the Arcade loads first; from a run, straight out.)
            if (RecalledMode.Active || RecalledMode.Pending) return;
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "menu" || HubMode.Pending) { _launchWhenArcade = true; return; }
            RcDoorMenu.Go();
        }

        private static void Say(string line) { if (Rc.Hud != null) Rc.Hud.Feed(line); else if (HubMode.Active) HubMode.SetHudText(line); }

        public static void OnChat(LobbyChatUpdate_t cb) { RcNet.MembersChanged(); }

        /// <summary>Every frame, wherever you are (MpLobby).</summary>
        public static void Tick()
        {
            if (_launchWhenArcade && HubMode.Active && HubController.Instance != null && HubController.Instance.Built)
            {
                _launchWhenArcade = false;
                if (Exists) RcDoorMenu.Go();
            }
            if (Exists && IsHost && Time.unscaledTime >= _zoneAt) { _zoneAt = Time.unscaledTime + 20f; PublishZone(); }
        }

        private static void PublishZone()
        {
            if (!Exists || !IsHost) return;
            SteamMatchmaking.SetLobbyData(Id, KEY_ZONE, (Rc.Char != null ? Rc.Char.Bosses : 0).ToString());
            SteamMatchmaking.SetLobbyData(Id, KEY_WORLD, RecalledMode.Dest.ToString());
        }

        /// <summary>The party's host is in this world now.</summary>
        public static void PublishWorld(int world) { if (Exists && IsHost) SteamMatchmaking.SetLobbyData(Id, KEY_WORLD, world.ToString()); }

        /// <summary>Where the host is (the town, if the lobby doesn't say).</summary>
        public static int HostWorld
        {
            get
            {
                int w;
                return Exists && int.TryParse(SteamMatchmaking.GetLobbyData(Id, KEY_WORLD), out w) && w >= 0 && w <= RcWorlds.EGYPT ? w : RcWorlds.TOWN;
            }
        }

        public static void Leave()
        {
            Creating = false; Searching = false; _inviteWhenCreated = false; _launchWhenArcade = false; _createPublic = false;
            if (!Exists) return;
            SteamMatchmaking.LeaveLobby(Id);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Recalled] Left lobby " + Id + ".");
            Id = CSteamID.Nil;
            Status = "";
            RcNet.MembersChanged();
        }
    }

    /// <summary>Another player in your town, as your game knows them.</summary>
    public class RcPeer
    {
        public CSteamID Id;
        public string Name = "";
        public Vector3 Pos;                 // town meters (their feet)
        public float Yaw, Charge, Hp01 = 1f, ClimbY, RollYaw, HeardAt = -100f, SwingAt = -10f, CastAt = -10f, HelloAt;
        public int Flags, Weapon, Swing = -1, Cast = -1, Level = 1, KidCode, Quest = -1, Bosses, World;   // Bosses: how many they've beaten; World: the town (0) or Egypt (1)

        /// <summary>The job they're on (their own: everyone has one of their own), or "" if not known yet.</summary>
        public string Mission { get { return Quest >= 0 && Quest < RcData.Quests.Count ? RcData.Quests[Quest].Title : Quest >= RcData.Quests.Count ? "Every job done" : ""; } }
        public string Spec = "";
        public bool Hello, Refused;
        public RcRemote Avatar;

        /// <summary>In your world, and heard from lately.</summary>
        public bool Live { get { return Hello && !Refused && World == RcWorlds.Now && Time.time - HeardAt < 3f; } }
        public bool Down { get { return (Flags & RcNet.F_DOWN) != 0; } }
        public bool Waiting { get { return (Flags & RcNet.F_WAIT) != 0; } }
        public bool Light { get { return (Flags & RcNet.F_LIGHT) != 0; } }
    }

    /// <summary>
    /// What the players in a town tell each other (Steam P2P, on a channel of its own):
    ///  - where they are and what they're doing, about 12 times a second (so you see them walk, swing, block, climb);
    ///  - who they are: their kid, their level, what they're wearing (the host turns away a character that can't be real);
    ///  - the toys: only the host runs them. It sends everyone the toys near them about 8 times a second; the others
    ///    send the hits they land, the noise they make and the decoys they throw, and are told when a toy hits them
    ///    and when one breaks (everyone near it gets its experience, its loot roll and its quest credit);
    ///  - shots and warning rings, so everyone sees them;
    ///  - pings, emotes, gifts (trading) and helping a downed friend up.
    /// </summary>
    public static class RcNet
    {
        private const int PACKET = 100, CHANNEL = 1;
        private const int OP_POSE = 1, OP_HELLO = 2, OP_REFUSE = 3, OP_SNAP = 4, OP_HIT = 5, OP_KILL = 6, OP_STRUCK = 7, OP_FIRE = 8, OP_RING = 9,
                          OP_NOISE = 10, OP_DECOY = 11, OP_EMOTE = 12, OP_PING = 13, OP_GIFT = 14, OP_REVIVE = 15, OP_TEXT = 16, OP_PARRY = 17, OP_TRAVEL = 18;
        public const int F_DOWN = 1, F_BLOCK = 2, F_LIGHT = 4, F_CLIMB = 8, F_HEAVY = 16, F_WAIT = 32, F_ROLL = 64, F_MOUNT = 128;   // (a mount: F_MOUNT, and bits 8 to 10 say which, plus one)
        public const int SNAP_MAX = 28;     // toys in one snapshot (an unreliable packet holds 1200 bytes)

        public static readonly List<RcPeer> Peers = new List<RcPeer>();
        public static readonly string[] Emotes = { "Over here!", "Help!", "Thanks!", "Wait." };

        private static bool _membersDirty = true, _wasHost = true, _preCull;
        private static float _poseAt, _helloAt, _membersAt;
        private static string _helloKey = "";

        /// <summary>In a town with a lobby behind it (there may still be nobody else in it).</summary>
        public static bool Active { get { return RcLobby.Exists && RecalledMode.Active && Rc.World != null && Rc.World.Built; } }
        public static bool IsHost { get { return RcLobby.IsHost; } }

        public static int LivePeers { get { int n = 0; for (int i = 0; i < Peers.Count; i++) if (Peers[i].Live) n++; return n; } }

        /// <summary>Is there anyone who could help you up?</summary>
        public static bool FriendStanding { get { for (int i = 0; i < Peers.Count; i++) if (Peers[i].Live && !Peers[i].Down) return true; return false; } }

        public static void MembersChanged() { _membersDirty = true; }

        public static RcPeer Peer(CSteamID id) { for (int i = 0; i < Peers.Count; i++) if (Peers[i].Id == id) return Peers[i]; return null; }
        public static RcPeer Peer(ulong id) { for (int i = 0; i < Peers.Count; i++) if (Peers[i].Id.m_SteamID == id) return Peers[i]; return null; }

        /// <summary>How near the nearest other player is to a zone (the host keeps a zone's toys going for them).</summary>
        public static float NearestPeerToZone(int zone)
        {
            float best = float.MaxValue;
            for (int i = 0; i < Peers.Count; i++) if (Peers[i].Live) best = Mathf.Min(best, RcTown.ZoneDistance(zone, Peers[i].Pos.x, Peers[i].Pos.z));
            return best;
        }

        // ── Every frame, in the town (RecalledController) ────────────────────
        /// <summary>Playing with friends, everyone works on the same job: the host's. Alone (or hosting) you're on your own.</summary>
        private static void SyncJob()
        {
            if (Rc.Char == null) return;
            if (!RcLobby.Exists || IsHost) { Rc.Char.ClearSession(); return; }
            for (int i = 0; i < Peers.Count; i++)
                if (Peers[i].Hello && !Peers[i].Refused && FromHost(Peers[i]))
                {
                    int q = Peers[i].Quest;
                    if (q >= 0 && q < RcData.Quests.Count) Rc.Char.SetSession(q); else Rc.Char.ClearSession(); // (a host with every job done has none left to share)
                    return;
                }
        }

        public static void Tick()
        {
            SyncJob();
            if (!RcLobby.Exists)
            {
                if (Peers.Count > 0) Clear();
                if (!_wasHost) { _wasHost = true; if (Rc.Enemies != null) Rc.Enemies.Regroup(); }
                try { Drain(); }
                catch (System.InvalidOperationException) { } // (no Steam, as in the itch.io copy of the game: alone in the town is all there is)
                return;
            }
            if (_membersDirty || Time.time >= _membersAt) { _membersDirty = false; _membersAt = Time.time + 2f; SyncMembers(); }
            Read();

            // The toys are run by whoever owns the lobby. If that just changed hands, they regroup.
            bool host = IsHost;
            if (host != _wasHost)
            {
                _wasHost = host;
                if (Rc.Enemies != null) Rc.Enemies.Regroup();
                if (host && Peers.Count > 0) Rc.Hud.Feed("The town is yours to run now: the toys regroup.");
            }

            if (Time.time >= _poseAt) { _poseAt = Time.time + 0.08f; SendPose(); }
            // Who you are: when it changes, and every few seconds for anyone who missed it.
            string key = KidSkins.Code + "|" + Rc.Char.Level + "|" + Rc.Char.Bosses + "|" + Rc.Char.ActiveQuest + "|" + Rc.Char.Equip[0] + "|" + Rc.Char.Equip[1] + "|" + Rc.Char.Equip[2];
            if (key != _helloKey || Time.time >= _helloAt) { _helloKey = key; _helloAt = Time.time + 4f; SendHello(); }

            for (int i = 0; i < Peers.Count; i++)
            {
                RcPeer p = Peers[i];
                if (p.Live) { if (p.Avatar == null) p.Avatar = new RcRemote(p); p.Avatar.Tick(); }
                else if (p.Avatar != null) { p.Avatar.Destroy(); p.Avatar = null; }
            }
            if (!_preCull) { _preCull = true; Camera.onPreCull += PreCull; }
        }

        private static void PreCull(Camera cam)
        {
            if (cam != FactoryController.PlayerCam) return;
            for (int i = 0; i < Peers.Count; i++) if (Peers[i].Avatar != null) Peers[i].Avatar.Pose();
        }

        /// <summary>Leaving the town.</summary>
        public static void Clear()
        {
            for (int i = 0; i < Peers.Count; i++) if (Peers[i].Avatar != null) Peers[i].Avatar.Destroy();
            Peers.Clear();
            if (_preCull) { _preCull = false; Camera.onPreCull -= PreCull; }
            _membersDirty = true;
        }

        private static void SyncMembers()
        {
            CSteamID me = SteamUser.GetSteamID();
            int n = SteamMatchmaking.GetNumLobbyMembers(RcLobby.Id);
            var here = new List<CSteamID>();
            for (int i = 0; i < n; i++)
            {
                CSteamID m = SteamMatchmaking.GetLobbyMemberByIndex(RcLobby.Id, i);
                if (m == me) continue;
                here.Add(m);
                if (Peer(m) != null) continue;
                var p = new RcPeer { Id = m, Name = SteamFriends.GetFriendPersonaName(m) };
                Peers.Add(p);
                if (Rc.Hud != null) Rc.Hud.Feed(p.Name + " is coming out.");
                _helloAt = 0f; // tell them who we are
            }
            for (int i = Peers.Count - 1; i >= 0; i--)
            {
                if (here.Contains(Peers[i].Id)) continue;
                if (Rc.Hud != null) Rc.Hud.Feed(Peers[i].Name + " went back inside.");
                if (Peers[i].Avatar != null) Peers[i].Avatar.Destroy();
                SteamNetworking.CloseP2PSessionWithUser(Peers[i].Id);
                Peers.RemoveAt(i);
            }
        }

        // ── Sending ──────────────────────────────────────────────────────────
        private static MpPacket New(int op) { var p = new MpPacket(PACKET); p.Write(op); return p; }

        private static void To(CSteamID who, MpPacket p, bool reliable)
        {
            if (who.m_SteamID == 0) return;
            byte[] data = p.ToArray();
            SteamNetworking.SendP2PPacket(who, data, (uint)data.Length, reliable ? EP2PSend.k_EP2PSendReliable : EP2PSend.k_EP2PSendUnreliable, CHANNEL);
        }

        private static void ToAll(MpPacket p, bool reliable) { for (int i = 0; i < Peers.Count; i++) if (!Peers[i].Refused) To(Peers[i].Id, p, reliable); }

        private static void ToHost(MpPacket p, bool reliable) { if (RcLobby.Exists) To(SteamMatchmaking.GetLobbyOwner(RcLobby.Id), p, reliable); }

        private static void Write(MpPacket p, Vector3 v) { p.Write(v.x); p.Write(v.y); p.Write(v.z); }
        private static Vector3 ReadV(MpPacket p) { return new Vector3(p.ReadFloat(), p.ReadFloat(), p.ReadFloat()); }

        private static void SendPose()
        {
            if (Peers.Count == 0 || Rc.Player == null) return;
            RcPlayer me = Rc.Player;
            int flags = (me.Dead ? F_DOWN : 0) | (me.Blocking ? F_BLOCK : 0) | (me.LightOn ? F_LIGHT : 0) | (me.Climbing ? F_CLIMB : 0) | (me.SwingHeavy ? F_HEAVY : 0) | (me.Waiting ? F_WAIT : 0) | (me.Rolling ? F_ROLL : 0) | (me.Mounted ? F_MOUNT | ((me.MountType + 1) << 8) : 0);
            using (var p = New(OP_POSE))
            {
                Write(p, me.TownPos);
                p.Write(me.KidYaw);
                p.Write(flags);
                p.Write(me.Weapon != null ? me.Weapon.Id : 0);
                p.Write(me.Charge);
                p.Write(me.SwingCount);
                p.Write(me.CastCount);
                p.Write(Mathf.Clamp01(me.Health / me.MaxHealth));
                p.Write(me.ClimbHeight);
                p.Write(me.RollYaw);
                ToAll(p, false);
            }
        }

        private static void SendHello()
        {
            if (Peers.Count == 0) return;
            using (var p = New(OP_HELLO))
            {
                p.Write(Rc.Char.Level);
                p.Write(Rc.Char.Bosses);
                p.Write(Rc.Char.ActiveQuest);
                for (int i = 0; i < RcCharacter.EQUIP_SLOTS; i++) p.Write(Rc.Char.Equip[i]);
                p.Write(KidSkins.Code);
                p.Write(KidSkins.Gender == KidGender.Custom && !Identity.Forgotten ? CharacterSpec.LocalString : "");
                p.Write(RcWorlds.Now);
                ToAll(p, true);
            }
        }

        /// <summary>The host's toys near each player: id, kind, where, which way, health, what it's doing.</summary>
        public static void SendSnapshot(RcPeer to, List<RcEnemy> toys)
        {
            using (var p = New(OP_SNAP))
            {
                p.Write(toys.Count);
                for (int i = 0; i < toys.Count; i++)
                {
                    RcEnemy e = toys[i];
                    p.Write(e.Id); p.Write(e.Def.Id);
                    Write(p, e.Pos);
                    p.Write(e.Yaw);
                    p.Write(Mathf.Clamp01(e.Hp / e.MaxHp));
                    float tele = Rc.Enemies.TeleOf(e);
                    p.Write((int)e.State | (e.Popped ? 256 : 0) | (e.Enraged ? 512 : 0) | ((tele < 0f ? 0 : 1 + Mathf.RoundToInt(tele * 100f)) << 12));
                }
                To(to.Id, p, false);
            }
        }

        public static void SendHit(int toy, float dmg, float stun)
        {
            using (var p = New(OP_HIT)) { p.Write(toy); p.Write(dmg); p.Write(stun); ToHost(p, true); }
        }

        public static void SendKill(RcEnemy e, bool reward)
        {
            if (Peers.Count == 0) return;
            using (var p = New(OP_KILL)) { p.Write(e.Id); p.Write(e.Def.Id); Write(p, e.Pos); p.Write(reward); ToAll(p, true); }
        }

        public static void SendStruck(RcPeer who, float dmg, Vector3 fromWorld, bool blockable)
        {
            using (var p = New(OP_STRUCK)) { p.Write(dmg); Write(p, Rc.T(fromWorld)); p.Write(blockable); To(who.Id, p, true); }
        }

        public static void SendFire(Vector3 fromWorld, Vector3 dir, float speed, float dmg, float range, bool player)
        {
            if (Peers.Count == 0) return;
            using (var p = New(OP_FIRE)) { Write(p, Rc.T(fromWorld)); Write(p, dir); p.Write(speed); p.Write(dmg); p.Write(range); p.Write(player); ToAll(p, true); }
        }

        public static void SendRing(Vector3 world, float radius, Color c, float life, bool grow)
        {
            if (Peers.Count == 0) return;
            using (var p = New(OP_RING)) { Write(p, Rc.T(world)); p.Write(radius); p.Write(c.r); p.Write(c.g); p.Write(c.b); p.Write(life); p.Write(grow); ToAll(p, true); }
        }

        /// <summary>You blocked perfectly near this spot: tell the host, whose toy it is.</summary>
        public static void SendParry(Vector3 world) { using (var p = New(OP_PARRY)) { Write(p, Rc.T(world)); ToHost(p, true); } }

        public static void SendNoise(Vector3 world, float radius) { using (var p = New(OP_NOISE)) { Write(p, Rc.T(world)); p.Write(radius); ToHost(p, true); } }
        public static void SendDecoy(Vector3 world, float seconds) { using (var p = New(OP_DECOY)) { Write(p, Rc.T(world)); p.Write(seconds); ToHost(p, true); } }

        public static void Emote(int index)
        {
            if (Rc.Hud != null) Rc.Hud.Feed("You: " + Emotes[index]);
            using (var p = New(OP_EMOTE)) { p.Write(index); ToAll(p, true); }
        }

        public static void Ping(Vector3 world)
        {
            if (Rc.Hud != null) Rc.Hud.Ping(world, "you");
            using (var p = New(OP_PING)) { Write(p, Rc.T(world)); ToAll(p, true); }
        }

        /// <summary>Hands items to another player. (They're already out of your backpack.)</summary>
        public static void Gift(RcPeer who, int item, int count, bool bounced)
        {
            using (var p = New(OP_GIFT)) { p.Write(item); p.Write(count); p.Write(bounced); To(who.Id, p, true); }
        }

        public static void Revive(RcPeer who) { using (var p = New(OP_REVIVE)) To(who.Id, p, true); }

        /// <summary>A line for everyone's feed (what a boss is up to).</summary>
        /// <summary>The host rides the Recall Express: the whole party goes with them.</summary>
        public static void SendTravel(int world)
        {
            if (Peers.Count == 0 || !IsHost) return;
            using (var p = New(OP_TRAVEL)) { p.Write(world); ToAll(p, true); }
        }

        public static void Say(string line)
        {
            if (Rc.Hud != null) Rc.Hud.Feed(line);
            if (Peers.Count == 0) return;
            using (var p = New(OP_TEXT)) { p.Write(line); ToAll(p, true); }
        }

        // ── Receiving ────────────────────────────────────────────────────────
        private static void Drain()
        {
            uint size;
            while (SteamNetworking.IsP2PPacketAvailable(out size, CHANNEL))
            {
                byte[] data = new byte[size];
                uint read; CSteamID from;
                if (!SteamNetworking.ReadP2PPacket(data, size, out read, out from, CHANNEL)) break;
            }
        }

        private static void Read()
        {
            uint size;
            while (SteamNetworking.IsP2PPacketAvailable(out size, CHANNEL))
            {
                byte[] data = new byte[size];
                uint read; CSteamID from;
                if (!SteamNetworking.ReadP2PPacket(data, size, out read, out from, CHANNEL)) break;
                RcPeer peer = Peer(from);
                if (peer == null || read < 8) continue; // (not in this town)
                try
                {
                    using (var p = new MpPacket(data))
                    {
                        if (p.ReadInt() != PACKET) continue;
                        Handle(peer, p.ReadInt(), p);
                    }
                }
                catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Recalled] Bad packet from " + peer.Name + ": " + e.Message); }
            }
        }

        private static bool FromHost(RcPeer peer) { return RcLobby.Exists && SteamMatchmaking.GetLobbyOwner(RcLobby.Id) == peer.Id; }

        private static void Handle(RcPeer peer, int op, MpPacket p)
        {
            if (peer.Refused && op != OP_HELLO) return;
            switch (op)
            {
                case OP_POSE:
                {
                    peer.Pos = ReadV(p);
                    peer.Yaw = p.ReadFloat();
                    peer.Flags = p.ReadInt();
                    peer.Weapon = p.ReadInt();
                    peer.Charge = p.ReadFloat();
                    int swing = p.ReadInt(), cast = p.ReadInt();
                    if (peer.Swing >= 0 && swing != peer.Swing) peer.SwingAt = Time.time;
                    if (peer.Cast >= 0 && cast != peer.Cast) peer.CastAt = Time.time;
                    peer.Swing = swing; peer.Cast = cast;
                    peer.Hp01 = p.ReadFloat();
                    peer.ClimbY = p.ReadFloat();
                    peer.RollYaw = p.ReadFloat();
                    peer.HeardAt = Time.time;
                    break;
                }
                case OP_HELLO:
                {
                    int level = p.ReadInt(), bosses = p.ReadInt();
                    int quest = p.ReadInt();
                    peer.Quest = quest >= 0 && quest <= RcData.Quests.Count ? quest : -1;
                    peer.Bosses = bosses;
                    var equip = new int[RcCharacter.EQUIP_SLOTS];
                    for (int i = 0; i < equip.Length; i++) equip[i] = p.ReadInt();
                    peer.KidCode = p.ReadInt();
                    peer.Spec = p.ReadString();
                    peer.World = p.ReadInt();
                    peer.Level = level;
                    bool first = !peer.Hello;
                    peer.Hello = true;
                    // The host looks a newcomer over: a character that couldn't have been earned isn't let in.
                    string wrong = Problem(level, bosses, equip);
                    if (IsHost && wrong != null && !peer.Refused)
                    {
                        peer.Refused = true;
                        KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Recalled] Turned " + peer.Name + " away: " + wrong + ".");
                        Rc.Hud.Feed(peer.Name + " was turned away (" + wrong + ").");
                        using (var r = New(OP_REFUSE)) { r.Write(wrong); To(peer.Id, r, true); }
                    }
                    else if (first && wrong == null) Rc.Hud.Feed(peer.Name + " is in town (level " + level + ").");
                    break;
                }
                case OP_REFUSE:
                    if (!FromHost(peer)) break;
                    Rc.Hud.Toast("TURNED AWAY", "This town's host didn't accept your character: " + p.ReadString() + ".");
                    RcLobby.Leave();
                    break;
                case OP_SNAP:
                {
                    if (IsHost || !FromHost(peer) || Rc.Enemies == null) break;
                    int n = p.ReadInt();
                    for (int i = 0; i < n && i < SNAP_MAX; i++)
                    {
                        int id = p.ReadInt(), def = p.ReadInt();
                        Vector3 pos = ReadV(p);
                        float yaw = p.ReadFloat(), hp = p.ReadFloat();
                        int state = p.ReadInt();
                        Rc.Enemies.Mirror(id, def, pos, yaw, hp, state);
                    }
                    break;
                }
                case OP_HIT:
                {
                    int id = p.ReadInt(); float dmg = p.ReadFloat(), stun = p.ReadFloat();
                    if (IsHost && Rc.Enemies != null && dmg > 0f && dmg < 5000f) Rc.Enemies.HitFrom(peer, id, dmg, stun);
                    break;
                }
                case OP_KILL:
                {
                    int id = p.ReadInt(), def = p.ReadInt(); Vector3 pos = ReadV(p); bool reward = p.ReadBool();
                    if (!IsHost && FromHost(peer) && Rc.Enemies != null) Rc.Enemies.Killed(id, def, pos, reward);
                    break;
                }
                case OP_STRUCK:
                {
                    float dmg = p.ReadFloat(); Vector3 from = ReadV(p); bool blockable = p.ReadBool();
                    if (!FromHost(peer) || Rc.Player == null) break;
                    if (blockable) Rc.Player.Damage(dmg, Rc.W(from.x, from.y, from.z)); else Rc.Player.Damage(dmg);
                    break;
                }
                case OP_FIRE:
                {
                    Vector3 from = ReadV(p), dir = ReadV(p); float speed = p.ReadFloat(), dmg = p.ReadFloat(), range = p.ReadFloat(); bool player = p.ReadBool();
                    if (Rc.Enemies == null || (!player && !FromHost(peer))) break;
                    Rc.Enemies.Shot(Rc.W(from.x, from.y, from.z), dir.normalized, speed, dmg, range, false, 0, player); // (a friend's shot is only for show: their game decides what it hits)
                    break;
                }
                case OP_RING:
                {
                    Vector3 at = ReadV(p); float radius = p.ReadFloat(); var c = new Color(p.ReadFloat(), p.ReadFloat(), p.ReadFloat()); float life = p.ReadFloat(); bool grow = p.ReadBool();
                    if (Rc.Enemies != null) Rc.Enemies.RingFrom(Rc.W(at.x, at.y, at.z), radius, c, life, grow);
                    break;
                }
                case OP_PARRY:
                {
                    Vector3 at = ReadV(p);
                    if (IsHost && Rc.Enemies != null) Rc.Enemies.StaggerNear(at);
                    break;
                }
                case OP_NOISE:
                {
                    Vector3 at = ReadV(p); float radius = p.ReadFloat();
                    if (IsHost && Rc.Enemies != null) Rc.Enemies.Noise(Rc.W(at.x, at.y, at.z), Mathf.Min(radius, 60f));
                    break;
                }
                case OP_DECOY:
                {
                    Vector3 at = ReadV(p); float seconds = p.ReadFloat();
                    if (IsHost && Rc.Enemies != null) Rc.Enemies.SetDecoy(Rc.W(at.x, at.y, at.z), Mathf.Min(seconds, 12f));
                    break;
                }
                case OP_EMOTE:
                {
                    int i = p.ReadInt();
                    if (i < 0 || i >= Emotes.Length) break;
                    Rc.Hud.Feed(peer.Name + ": " + Emotes[i]);
                    Rc.Hud.Ping(Rc.W(peer.Pos.x, peer.Pos.y + 1.5f, peer.Pos.z), Emotes[i]);
                    break;
                }
                case OP_PING:
                {
                    Vector3 at = ReadV(p);
                    Rc.Hud.Ping(Rc.W(at.x, at.y, at.z), peer.Name);
                    Rc.Music.Jingle(RcMusic.J_PICKUP);
                    break;
                }
                case OP_GIFT:
                {
                    int item = p.ReadInt(), count = p.ReadInt(); bool bounced = p.ReadBool();
                    RcItem it = RcData.Item(item);
                    if (it == null || count <= 0 || count > RecalledRules.MATERIAL_STACK) break;
                    int left = Rc.Char.Add(item, count);
                    if (bounced) { Rc.Hud.Feed(peer.Name + " has no room: " + count + " " + it.Name + " came back."); break; }
                    if (count - left > 0) { Rc.Hud.Feed(peer.Name + " gave you " + (count - left) + " " + it.Name + "."); Rc.Music.Jingle(RcMusic.J_PICKUP); }
                    if (left > 0) Gift(peer, item, left, true); // what doesn't fit goes back
                    Rc.Save();
                    break;
                }
                case OP_REVIVE:
                    if (Rc.Player != null && Rc.Player.Dead && peer.Live) { Rc.Player.GetUp(); Rc.Hud.Feed(peer.Name + " helped you up."); }
                    break;
                case OP_TEXT:
                    if (FromHost(peer)) Rc.Hud.Feed(p.ReadString());
                    break;
                case OP_TRAVEL:
                {
                    int world = p.ReadInt();
                    if (FromHost(peer) && world >= 0 && world <= RcWorlds.EGYPT && world != RcWorlds.Now && RecalledMode.Active) RecalledMode.Travel(world);
                    break;
                }
            }
        }

        /// <summary>What's wrong with a character somebody says they have (null: nothing). The rules every real one obeys.</summary>
        public static string Problem(int level, int bosses, int[] equip)
        {
            if (level < 1 || level > RecalledRules.LEVEL_CAP) return "level " + level;
            if (bosses < 0 || bosses > 5) return "bosses beaten";
            for (int i = 0; i < equip.Length; i++)
            {
                if (equip[i] == 0) continue;
                RcItem it = RcData.Item(equip[i]);
                if (it == null || !it.IsGear) return "gear that doesn't exist";
                if (!RecalledRules.CanEquip(level, it.Level)) return it.Name + " at level " + level;
            }
            return null;
        }
    }

    /// <summary>Another player's kid in your town: where their game says they are, holding what they hold, their name over their head.</summary>
    public class RcRemote
    {
        private readonly RcPeer _peer;
        private GameObject _kid;
        private string _key;
        private RcKidRig _rig;
        private UnityEngine.UI.Text _name;
        private Vector3 _pos;
        private float _yaw;
        private bool _placed, _down, _rolling, _blocking;
        private Light _torch;
        private RcBubble _bubble;
        private KidAnimator _anim;
        private float _rollAt, _blockAt;
        private RcMountView _mount;
        private int _mountType = -1;

        public RcRemote(RcPeer peer) { _peer = peer; }

        public void Tick()
        {
            string key = _peer.KidCode + "|" + _peer.Spec;
            if (_kid == null || key != _key)
            {
                if (_kid != null) Object.Destroy(_kid);
                _key = key;
                KidGender gender = KidSkins.GenderOf(_peer.KidCode);
                try
                {
                    _kid = gender == KidGender.Custom && _peer.Spec.Length > 0
                        ? CharacterKit.Build("KE_RcFriend", CharacterSpec.FromString(_peer.Spec), KidSkins.SkinOf(_peer.KidCode))
                        : ChildModel.Build("KE_RcFriend", gender == KidGender.Custom ? KidGender.Boy : gender, KidSkins.SkinOf(_peer.KidCode));
                }
                catch (System.Exception e)
                {
                    KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Recalled] Couldn't build " + _peer.Name + "'s kid: " + e.Message);
                    _kid = ChildModel.Build("KE_RcFriend", KidGender.Boy, 0);
                }
                _rig = new RcKidRig(_kid);
                _anim = _kid.GetComponent<KidAnimator>();
                var label = WorldText.Create(_kid.transform, "", Color.white, 0.12f, TextAnchor.MiddleCenter, 2.4f);
                label.canvas.transform.localPosition = new Vector3(0f, ChildModel.HeightOf(gender == KidGender.Custom ? KidGender.Boy : gender) + 0.18f, 0f);
                label.canvas.gameObject.AddComponent<PartyLabelBillboard>();
                _name = label;
                // Their flashlight: you see where a friend is looking.
                var t = new GameObject("Torch");
                t.transform.SetParent(_kid.transform, false);
                t.transform.localPosition = new Vector3(0f, 0.8f, 0.25f);
                _torch = t.AddComponent<Light>();
                _torch.type = LightType.Spot; _torch.spotAngle = 50f; _torch.range = 18f; _torch.intensity = 1.2f; _torch.color = new Color(1f, 0.95f, 0.82f);
                _torch.shadows = LightShadows.None;
                _placed = false; _down = false;
            }
            Vector3 want = Rc.W(_peer.Pos.x, _peer.Pos.y, _peer.Pos.z);
            if (!_placed || (want - _pos).sqrMagnitude > 64f) { _pos = want; _yaw = _peer.Yaw; _placed = true; } // (a zipline, or the first we've heard)
            _pos = Vector3.Lerp(_pos, want, Mathf.Clamp01(Time.deltaTime * 14f));
            _yaw = Mathf.LerpAngle(_yaw, _peer.Yaw, Mathf.Clamp01(Time.deltaTime * 14f));
            _kid.transform.position = _pos;
            _kid.transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            _torch.enabled = _peer.Light && !_peer.Down;

            // Their mount, if they're riding one.
            int mt = (_peer.Flags & RcNet.F_MOUNT) != 0 && !_peer.Down ? ((_peer.Flags >> 8) & 7) - 1 : -1;
            if (mt != _mountType)
            {
                if (_mount != null) { _mount.Destroy(); _mount = null; }
                _mountType = mt;
                if (mt >= 0 && mt < RcMountView.TYPES) _mount = new RcMountView(mt);
            }
            if (_mount != null)
            {
                _mount.Place(_pos, _yaw);
                _kid.transform.position = _pos + Vector3.up * (RcMountView.Seat[_mount.Type] - 0.2f);
            }

            // Their guard, and their roll.
            bool blk = (_peer.Flags & RcNet.F_BLOCK) != 0 && !_peer.Down;
            if (blk && !_blocking) _blockAt = Time.time;
            _blocking = blk;
            if (_bubble == null) _bubble = new RcBubble();
            _bubble.Tick(blk, _pos, blk && Time.time - _blockAt < RcPlayer.PARRY_WINDOW);
            bool roll = (_peer.Flags & RcNet.F_ROLL) != 0 && !_peer.Down;
            if (roll && !_rolling) _rollAt = Time.time;
            _rolling = roll;
            float tuck = _mount != null ? 0.55f : 0f;
            if (roll) tuck = RcRoll.Apply(_kid.transform, _pos, Quaternion.Euler(0f, _peer.RollYaw, 0f) * Vector3.forward, (Time.time - _rollAt) / RcRoll.TIME);
            if (_anim != null) _anim.Tuck = tuck;
            string text = _peer.Hp01 < 0f ? _peer.Name : _peer.Name + (_peer.Down ? "  (down)" : "  " + Mathf.RoundToInt(_peer.Hp01 * 100f) + "%"); // (the trailer's kids: just the name)
            if (_name.text != text) { _name.text = text; _name.color = _peer.Down ? new Color(1f, 0.35f, 0.3f) : Color.white; }
            if (_peer.Down != _down)
            {
                _down = _peer.Down;
                var anim = _kid.GetComponent<KidAnimator>();
                if (anim != null) { if (_down) anim.Flinch(); else anim.ResetFlinch(); }
            }
            _rig.Hold(_peer.Down || _mount != null ? null : RcData.Item(_peer.Weapon));
        }

        /// <summary>Something landed on their guard (the trailer).</summary>
        public void Guarded(bool perfect) { if (_bubble != null) _bubble.Hit(perfect); }

        /// <summary>Their arms (just before the camera draws, over the walk cycle).</summary>
        public void Pose()
        {
            if (_rig == null || _kid == null || _peer.Down || _rolling || _mount != null) return;
            _rig.Pose(RcData.Item(_peer.Weapon), _peer.Charge, Time.time - _peer.SwingAt, Time.time - _peer.CastAt, (_peer.Flags & RcNet.F_HEAVY) != 0,
                (_peer.Flags & RcNet.F_BLOCK) != 0, 10f, (_peer.Flags & RcNet.F_CLIMB) != 0, _peer.ClimbY);
        }

        public void Destroy()
        {
            if (_rig != null) _rig.Destroy();
            if (_bubble != null) _bubble.Destroy();
            _bubble = null;
            if (_mount != null) _mount.Destroy();
            _mount = null;
            if (_kid != null) Object.Destroy(_kid);
            _kid = null;
        }
    }

    /// <summary>
    /// A kid's arms and what's in their hands, for you and for the friends you see: the weapon's shape in the fist,
    /// and the poses (at the ready, swinging, winding up, drawing a slingshot, aiming, blocking, casting, climbing).
    /// Angles are (pitch, yaw, roll) from an arm hanging straight down: pitch -90 points it forward, -180 up; yaw
    /// swings it across the body; an elbow only pitches.
    /// </summary>
    public class RcKidRig
    {
        private readonly Transform _upperR, _lowerR, _upperL, _lowerL, _handR, _handL;
        private readonly Vector3 _gripR, _gripL;   // where the fist is, in each hand bone's own space
        private GameObject _weapon, _band;
        private int _weaponFor = -1;
        private float _charge; // (eased, so a draw that's let go doesn't snap)

        public RcKidRig(GameObject kid)
        {
            foreach (var tr in kid.GetComponentsInChildren<Transform>(true))
            {
                switch (tr.name)
                {
                    case "upperarmR": _upperR = tr; break;
                    case "lowerarmR": _lowerR = tr; break;
                    case "upperarmL": _upperL = tr; break;
                    case "lowerarmL": _lowerL = tr; break;
                    case "handR": _handR = tr; break;
                    case "handL": _handL = tr; break;
                }
            }
            if (_handR == null) KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Recalled] This kid has no 'handR' bone: weapons can't be held.");
            _gripR = Fist(kid, _handR);
            _gripL = Fist(kid, _handL);
        }

        /// <summary>
        /// Where a hand's fist is, found from the model itself: the middle of every vertex that belongs to that hand's
        /// bone, in the bone's own space. A weapon parented there (and kept there every frame) is in the palm, whatever
        /// size or shape the kid is. (Falls back to a guess if the model won't say.)
        /// </summary>
        private static Vector3 Fist(GameObject kid, Transform hand)
        {
            Vector3 guess = new Vector3(0f, -0.035f, 0.01f);
            if (hand == null) return guess;
            try
            {
                Vector3 sum = Vector3.zero;
                int count = 0;
                SkinnedMeshRenderer[] skins = kid.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                for (int s = 0; s < skins.Length; s++)
                {
                    Mesh mesh = skins[s].sharedMesh;
                    if (mesh == null) continue;
                    Transform[] bones = skins[s].bones;
                    int bi = -1;
                    for (int i = 0; i < bones.Length; i++) if (bones[i] == hand) { bi = i; break; }
                    if (bi < 0) continue;
                    Vector3[] verts = mesh.vertices;
                    BoneWeight[] weights = mesh.boneWeights;
                    Matrix4x4[] bind = mesh.bindposes;
                    if (weights.Length != verts.Length || bi >= bind.Length) continue;
                    for (int v = 0; v < verts.Length; v++)
                    {
                        BoneWeight b = weights[v];
                        float w = (b.boneIndex0 == bi ? b.weight0 : 0f) + (b.boneIndex1 == bi ? b.weight1 : 0f) + (b.boneIndex2 == bi ? b.weight2 : 0f) + (b.boneIndex3 == bi ? b.weight3 : 0f);
                        if (w < 0.7f) continue;
                        sum += bind[bi].MultiplyPoint3x4(verts[v]);
                        count++;
                    }
                }
                if (count > 8)
                {
                    Vector3 c = sum / count;
                    if (c.magnitude < 0.3f) return c;
                }
            }
            catch (System.Exception) { }
            return guess;
        }

        public void Destroy()
        {
            if (_weapon != null) Object.Destroy(_weapon);
            if (_band != null) Object.Destroy(_band);
        }

        public void Forget() { _weaponFor = -1; }

        private static bool IsSlingshot(RcItem w) { return w != null && w.Kind == RcKind.Ranged && w.Zone == 0; }

        // A hand bone's own "down" runs on from the forearm and its "forward" is where the knuckles face, so a bat
        // (built pointing down, then turned to point forward) stands up out of the fist when the forearm is level.
        public void Hold(RcItem w)
        {
            int id = w != null ? w.Id : 0;
            if (id == _weaponFor) return;
            _weaponFor = id;
            if (_weapon != null) Object.Destroy(_weapon);
            _weapon = null;
            Transform hand = IsSlingshot(w) && _handL != null ? _handL : _handR; // (a slingshot's fork is held out in the other hand)
            if (w == null || hand == null) return;
            _weapon = new GameObject("KE_RcWeapon");
            _weapon.transform.SetParent(hand, false);
            _weapon.transform.localPosition = hand == _handL ? _gripL : _gripR; // in the palm
            Transform t = _weapon.transform;
            // Melee weapons and the slingshot are built pointing "down" and then turned, so they stand up out of the
            // fist; blasters and gadgets are built with their length running on from the forearm.
            if (w.Kind == RcKind.Melee || IsSlingshot(w)) t.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            switch (w.Id)
            {
                case 100: Bat(t); break;
                case 101: HockeyStick(t); break;
                case 102: Wrench(t); break;
                case 103: Crowbar(t); break;
                case 104: Sledgehammer(t); break;
                case 110: Slingshot(t); break;
                case 111: DartBlaster(t); break;
                case 112: NailGun(t); break;
                case 113: RocketLauncher(t); break;
                case 114: BoltThrower(t); break;
                case 120: Battery(t, false); break;
                case 121: RepairKit(t); break;
                case 122: DecoyToy(t); break;
                case 123: LidShield(t); break;
                case 124: Battery(t, true); break;
                default:
                    if (w.Kind == RcKind.Melee) Bat(t); else if (w.Kind == RcKind.Ranged) DartBlaster(t); else RepairKit(t);
                    break;
            }
        }

        // ── What each thing looks like ───────────────────────────────────────
        private static readonly Dictionary<string, Material> _paints = new Dictionary<string, Material>();

        /// <summary>A shared material: plain paint (kind 0), scratched metal or grained wood from the Facility's textures (1: "plate", 3: "wood"), or a glow (2).</summary>
        private static Material Paint(string key, float r, float g, float b, float gloss, int kind)
        {
            Material m;
            if (_paints.TryGetValue(key, out m) && m != null) return m;
            var c = new Color(r, g, b);
            if (kind == 2) m = Mat.Unlit(c);
            else if (kind == 0) m = Mat.Lit(c, gloss);
            else
            {
                try { m = FTex.Get(kind == 3 ? "wood" : "plate", 1f, c, gloss, kind == 3 ? 0f : 0.75f, 0.8f).Mat; }
                catch (System.Exception) { m = Mat.Lit(c, gloss); }
            }
            _paints[key] = m;
            return m;
        }

        private static Material Wood { get { return Paint("wood", 0.85f, 0.66f, 0.45f, 0.3f, 3); } }
        private static Material Steel { get { return Paint("steel", 0.6f, 0.62f, 0.66f, 0.6f, 1); } }
        private static Material Dark { get { return Paint("dark", 0.06f, 0.06f, 0.07f, 0.3f, 0); } }
        private static Material Tape { get { return Paint("tape", 0.9f, 0.9f, 0.86f, 0.1f, 0); } }

        private static GameObject Shape(Transform parent, PrimitiveType type, Vector3 pos, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        private static void Part(Transform parent, Vector3 pos, Vector3 size, Material m) { Shape(parent, PrimitiveType.Cube, pos, size, m); }

        /// <summary>A round bar between two points.</summary>
        private static void Rod(Transform parent, Vector3 from, Vector3 to, float thick, Material m)
        {
            var go = Shape(parent, PrimitiveType.Cylinder, (from + to) * 0.5f, new Vector3(thick, (to - from).magnitude * 0.5f, thick), m);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (to - from).normalized);
        }

        private static Vector3 Y(float y) { return new Vector3(0f, y, 0f); }

        private static void Bat(Transform t)
        {
            Shape(t, PrimitiveType.Sphere, Y(0.06f), new Vector3(0.055f, 0.03f, 0.055f), Wood);      // the knob
            Rod(t, Y(0.06f), Y(-0.2f), 0.034f, Wood);                                                 // the handle...
            Rod(t, Y(0.03f), Y(-0.13f), 0.04f, Dark);                                                 // ...wrapped in grip tape
            Rod(t, Y(-0.2f), Y(-0.38f), 0.05f, Wood);                                                 // widening
            Rod(t, Y(-0.38f), Y(-0.66f), 0.075f, Wood);                                               // the barrel
            Shape(t, PrimitiveType.Sphere, Y(-0.66f), new Vector3(0.075f, 0.04f, 0.075f), Wood);
        }

        private static void HockeyStick(Transform t)
        {
            Part(t, Y(-0.3f), new Vector3(0.03f, 0.74f, 0.022f), Wood);                               // the shaft
            Part(t, Y(0.06f), new Vector3(0.036f, 0.05f, 0.028f), Dark);                              // the butt end
            Part(t, Y(-0.05f), new Vector3(0.033f, 0.12f, 0.025f), Tape);
            var blade = Shape(t, PrimitiveType.Cube, new Vector3(0f, -0.69f, 0.1f), new Vector3(0.02f, 0.075f, 0.26f), Dark);
            blade.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);                           // the blade, angled off the shaft
            Shape(t, PrimitiveType.Cube, new Vector3(0f, -0.7f, 0.14f), new Vector3(0.022f, 0.078f, 0.12f), Tape).transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
        }

        private static void Wrench(Transform t)
        {
            Material red = Paint("wrench_red", 0.7f, 0.12f, 0.1f, 0.5f, 0);
            Rod(t, Y(0.07f), Y(-0.46f), 0.04f, red);                                                  // the handle
            Shape(t, PrimitiveType.Sphere, Y(0.07f), new Vector3(0.05f, 0.03f, 0.05f), red);
            Part(t, new Vector3(0f, -0.54f, 0.02f), new Vector3(0.04f, 0.18f, 0.07f), Steel);         // the head
            Part(t, new Vector3(0f, -0.62f, 0.075f), new Vector3(0.042f, 0.035f, 0.1f), Steel);       // the fixed jaw
            Part(t, new Vector3(0f, -0.5f, 0.085f), new Vector3(0.042f, 0.035f, 0.08f), Steel);       // the jaw that moves
            Rod(t, new Vector3(0f, -0.53f, -0.02f), new Vector3(0f, -0.53f, -0.06f), 0.06f, Dark);    // the wheel that moves it
        }

        private static void Crowbar(Transform t)
        {
            Material blue = Paint("crowbar", 0.16f, 0.2f, 0.3f, 0.55f, 1);
            Rod(t, Y(0.05f), Y(-0.6f), 0.032f, blue);
            Rod(t, Y(0.05f), new Vector3(0f, 0.1f, 0.03f), 0.032f, blue);                             // the flat end, a little bent
            Part(t, new Vector3(0f, 0.12f, 0.04f), new Vector3(0.04f, 0.05f, 0.012f), Steel);
            Rod(t, Y(-0.6f), new Vector3(0f, -0.67f, 0.05f), 0.032f, blue);                           // the hook
            Rod(t, new Vector3(0f, -0.67f, 0.05f), new Vector3(0f, -0.63f, 0.12f), 0.03f, blue);
            Part(t, new Vector3(0f, -0.615f, 0.135f), new Vector3(0.042f, 0.012f, 0.05f), Steel);     // its claw
        }

        private static void Sledgehammer(Transform t)
        {
            Rod(t, Y(0.09f), Y(-0.6f), 0.04f, Wood);
            Rod(t, Y(0.06f), Y(-0.1f), 0.046f, Dark);
            Part(t, Y(-0.63f), new Vector3(0.13f, 0.14f, 0.28f), Steel);                              // the head
            Part(t, new Vector3(0f, -0.63f, 0.145f), new Vector3(0.14f, 0.15f, 0.02f), Dark);         // its faces
            Part(t, new Vector3(0f, -0.63f, -0.145f), new Vector3(0.14f, 0.15f, 0.02f), Dark);
            Part(t, Y(-0.55f), new Vector3(0.06f, 0.025f, 0.06f), Steel);                             // the collar
        }

        private static void Slingshot(Transform t)
        {
            Rod(t, Y(0.06f), Y(-0.1f), 0.036f, Wood);                                                 // the handle
            Rod(t, Y(0.04f), Y(-0.06f), 0.042f, Dark);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Rod(t, Y(-0.1f), new Vector3(sx * 0.065f, -0.22f, 0f), 0.03f, Wood);                  // the fork
                Rod(t, new Vector3(sx * 0.065f, -0.19f, 0f), new Vector3(sx * 0.065f, -0.225f, 0f), 0.036f, Paint("band", 0.5f, 0.12f, 0.1f, 0.1f, 0));
            }
        }

        // The blasters: the grip is in the fist, the barrel runs on from the forearm (-y), "up" is +z.
        private static void Grip(Transform t, Material m)
        {
            var g = Shape(t, PrimitiveType.Cube, new Vector3(0f, 0f, -0.01f), new Vector3(0.036f, 0.05f, 0.11f), m);
            g.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
        }

        private static void DartBlaster(Transform t)
        {
            Material orange = Paint("blaster_orange", 0.95f, 0.45f, 0.08f, 0.5f, 0), blue = Paint("blaster_blue", 0.12f, 0.35f, 0.8f, 0.5f, 0);
            Grip(t, Dark);
            Part(t, new Vector3(0f, -0.09f, 0.06f), new Vector3(0.05f, 0.24f, 0.075f), blue);         // the body
            Part(t, new Vector3(0f, -0.06f, 0.105f), new Vector3(0.03f, 0.14f, 0.02f), orange);       // the slide on top
            Rod(t, new Vector3(0f, -0.2f, 0.065f), new Vector3(0f, -0.36f, 0.065f), 0.04f, orange);   // the barrel
            Rod(t, new Vector3(0f, -0.35f, 0.065f), new Vector3(0f, -0.375f, 0.065f), 0.05f, blue);
            Shape(t, PrimitiveType.Sphere, new Vector3(0f, -0.385f, 0.065f), Vector3.one * 0.03f, Paint("dart", 0.9f, 0.1f, 0.1f, 0.4f, 0)); // the dart's tip
            Part(t, new Vector3(0f, -0.06f, 0.015f), new Vector3(0.012f, 0.05f, 0.03f), orange);      // the trigger
        }

        private static void NailGun(Transform t)
        {
            Material yellow = Paint("nailgun", 0.92f, 0.72f, 0.1f, 0.45f, 0);
            Grip(t, Dark);
            Part(t, new Vector3(0f, -0.1f, 0.08f), new Vector3(0.075f, 0.24f, 0.12f), yellow);        // the body
            Rod(t, new Vector3(0f, 0.02f, 0.09f), new Vector3(0f, 0.06f, 0.09f), 0.07f, Dark);        // the cap at the back
            Rod(t, new Vector3(0f, -0.22f, 0.06f), new Vector3(0f, -0.34f, 0.06f), 0.035f, Steel);    // the nose
            Part(t, new Vector3(0f, -0.345f, 0.06f), new Vector3(0.05f, 0.012f, 0.05f), Dark);
            var mag = Shape(t, PrimitiveType.Cube, new Vector3(0f, -0.17f, -0.03f), new Vector3(0.04f, 0.2f, 0.06f), Steel); // the magazine, slanting back to the grip
            mag.transform.localRotation = Quaternion.Euler(-24f, 0f, 0f);
            Part(t, new Vector3(0f, -0.1f, 0.145f), new Vector3(0.05f, 0.12f, 0.012f), Dark);
        }

        private static void RocketLauncher(Transform t)
        {
            Material green = Paint("launcher", 0.3f, 0.38f, 0.26f, 0.35f, 1), red = Paint("dart", 0.9f, 0.1f, 0.1f, 0.4f, 0);
            Grip(t, Dark);
            Rod(t, new Vector3(0f, 0.14f, 0.08f), new Vector3(0f, -0.4f, 0.08f), 0.11f, green);       // the tube
            Rod(t, new Vector3(0f, 0.12f, 0.08f), new Vector3(0f, 0.16f, 0.08f), 0.135f, Dark);       // its ends
            Rod(t, new Vector3(0f, -0.37f, 0.08f), new Vector3(0f, -0.41f, 0.08f), 0.135f, Dark);
            Shape(t, PrimitiveType.Sphere, new Vector3(0f, -0.44f, 0.08f), new Vector3(0.08f, 0.14f, 0.08f), red); // the rocket's nose poking out
            Rod(t, new Vector3(0f, -0.2f, 0.135f), new Vector3(0f, -0.2f, 0.19f), 0.02f, Steel);      // the sight
            Part(t, new Vector3(0f, -0.2f, 0.195f), new Vector3(0.035f, 0.012f, 0.03f), Steel);
            Part(t, new Vector3(0f, -0.22f, 0.02f), new Vector3(0.03f, 0.04f, 0.07f), Dark);          // the front grip
        }

        private static void BoltThrower(Transform t)
        {
            Grip(t, Dark);
            Part(t, new Vector3(0f, -0.12f, 0.05f), new Vector3(0.045f, 0.4f, 0.05f), Wood);          // the stock
            Part(t, new Vector3(0f, -0.14f, 0.08f), new Vector3(0.015f, 0.34f, 0.012f), Steel);       // the rail
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Rod(t, new Vector3(0f, -0.3f, 0.06f), new Vector3(sx * 0.2f, -0.24f, 0.06f), 0.025f, Steel);   // the limbs, swept back
                Rod(t, new Vector3(sx * 0.2f, -0.24f, 0.06f), new Vector3(0f, -0.08f, 0.085f), 0.006f, Tape); // the string, drawn
            }
            Rod(t, new Vector3(0f, -0.08f, 0.09f), new Vector3(0f, -0.4f, 0.09f), 0.014f, Steel);     // the bolt
            Shape(t, PrimitiveType.Sphere, new Vector3(0f, -0.41f, 0.09f), new Vector3(0.03f, 0.05f, 0.03f), Dark);
        }

        // Gadgets: carried in the fist at the chest.
        private static void Battery(Transform t, bool many)
        {
            Material yellow = Paint("battery", 0.9f, 0.75f, 0.1f, 0.5f, 0), spark = Paint("glow_blue", 0.4f, 0.9f, 1f, 0f, 2);
            for (int i = many ? -1 : 0; i <= (many ? 1 : 0); i++)
            {
                Vector3 o = new Vector3(i * 0.075f, -0.05f, 0.06f + (i == 0 ? 0.02f : 0f));
                Rod(t, o + new Vector3(0f, 0f, -0.07f), o + new Vector3(0f, 0f, 0.06f), 0.07f, yellow);
                Rod(t, o + new Vector3(0f, 0f, -0.075f), o + new Vector3(0f, 0f, -0.03f), 0.072f, Dark);
                Rod(t, o + new Vector3(0f, 0f, 0.06f), o + new Vector3(0f, 0f, 0.085f), 0.03f, Steel);   // the terminal
                Shape(t, PrimitiveType.Sphere, o + new Vector3(0f, 0f, 0.095f), Vector3.one * 0.028f, spark);
            }
            if (many) Rod(t, new Vector3(-0.11f, -0.05f, 0.05f), new Vector3(0.11f, -0.05f, 0.05f), 0.02f, Dark); // taped together
        }

        private static void RepairKit(Transform t)
        {
            Material white = Paint("kit_white", 0.9f, 0.9f, 0.88f, 0.35f, 0), cross = Paint("kit_red", 0.85f, 0.1f, 0.1f, 0.3f, 0);
            Part(t, new Vector3(0f, -0.05f, 0.05f), new Vector3(0.16f, 0.12f, 0.1f), white);
            Part(t, new Vector3(0f, -0.05f, 0.102f), new Vector3(0.09f, 0.03f, 0.006f), cross);
            Part(t, new Vector3(0f, -0.05f, 0.102f), new Vector3(0.03f, 0.09f, 0.006f), cross);
            Rod(t, new Vector3(-0.04f, -0.05f, 0.1f), new Vector3(-0.04f, -0.05f, 0.125f), 0.012f, Dark);  // the handle
            Rod(t, new Vector3(0.04f, -0.05f, 0.1f), new Vector3(0.04f, -0.05f, 0.125f), 0.012f, Dark);
            Rod(t, new Vector3(-0.04f, -0.05f, 0.125f), new Vector3(0.04f, -0.05f, 0.125f), 0.012f, Dark);
        }

        private static void DecoyToy(Transform t)
        {
            Material pink = Paint("decoy", 1f, 0.45f, 0.75f, 0.2f, 0), eye = Paint("glow_white", 1f, 1f, 1f, 0f, 2);
            Vector3 c = new Vector3(0f, -0.05f, 0.07f);
            Shape(t, PrimitiveType.Sphere, c, new Vector3(0.13f, 0.12f, 0.14f), pink);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Shape(t, PrimitiveType.Sphere, c + new Vector3(sx * 0.045f, 0f, 0.085f), new Vector3(0.035f, 0.02f, 0.06f), pink); // ears
                Shape(t, PrimitiveType.Sphere, c + new Vector3(sx * 0.03f, -0.055f, 0.02f), Vector3.one * 0.028f, eye);
            }
        }

        private static void LidShield(Transform t)
        {
            Rod(t, new Vector3(-0.04f, -0.02f, 0.05f), new Vector3(-0.06f, -0.02f, 0.05f), 0.4f, Steel);       // the lid, edge on to your arm
            Rod(t, new Vector3(-0.06f, -0.02f, 0.05f), new Vector3(-0.068f, -0.02f, 0.05f), 0.3f, Paint("lid_ring", 0.45f, 0.47f, 0.5f, 0.6f, 1));
            Shape(t, PrimitiveType.Sphere, new Vector3(-0.075f, -0.02f, 0.05f), new Vector3(0.03f, 0.06f, 0.06f), Dark); // its knob
            Rod(t, new Vector3(-0.04f, -0.08f, 0.05f), new Vector3(0f, -0.08f, 0.05f), 0.014f, Dark);         // the strap your hand's through
            Rod(t, new Vector3(-0.04f, 0.04f, 0.05f), new Vector3(0f, 0.04f, 0.05f), 0.014f, Dark);
        }

        private static void Set(Transform bone, float x, float y, float z, float k)
        {
            if (bone == null) return;
            Quaternion q = Quaternion.Euler(x, y, z);
            bone.localRotation = k >= 1f ? q : Quaternion.Slerp(bone.localRotation, q, k);
        }

        public void Pose(RcItem w, float charge, float swing, float cast, bool heavy, bool blocking, float sinceBlocked, bool climbing, float climbY)
        {
            if (_upperR == null) return;
            // (the weapon stays in the fist however the arm is posed)
            if (_weapon != null && _weapon.transform.parent != null) _weapon.transform.localPosition = _weapon.transform.parent == _handL ? _gripL : _gripR;
            _charge = Mathf.MoveTowards(_charge, charge, Time.deltaTime * 6f);
            float c = _charge;
            bool band = false;

            if (climbing)
            {
                // Hand over hand.
                float a = Mathf.Sin(climbY * 7.5f) * 30f;
                Set(_upperR, -150f + a, 0f, -6f, 1f); Set(_lowerR, -35f - a * 0.5f, 0f, 0f, 1f);
                Set(_upperL, -150f - a, 0f, 6f, 1f); Set(_lowerL, -35f + a * 0.5f, 0f, 0f, 1f);
            }
            else if (cast < 0.45f)
            {
                // A gadget going off: both arms thrown up.
                float k = Mathf.Sin(Mathf.Clamp01(cast / 0.45f) * Mathf.PI);
                Set(_upperR, -150f, 0f, -18f, k);
                Set(_upperL, -150f, 0f, 18f, k);
            }
            else if (blocking && w != null)
            {
                // The weapon held up across you in both hands; it jolts when something lands on it.
                float jolt = Mathf.Clamp01(1f - sinceBlocked / 0.18f) * 14f;
                Set(_upperR, -62f + jolt, -32f, 0f, 1f); Set(_lowerR, -52f, 0f, 0f, 1f);
                Set(_upperL, -66f + jolt, 34f, 0f, 1f); Set(_lowerL, -48f, 0f, 0f, 1f);
            }
            else if (w == null)
            {
                // Bare hands: a shove.
                if (swing < 0.3f) { float k = Mathf.Sin(Mathf.Clamp01(swing / 0.3f) * Mathf.PI); Set(_upperR, -85f, -10f, 0f, k); Set(_upperL, -85f, 10f, 0f, k); }
            }
            else if (w.Kind == RcKind.Melee)
            {
                bool two = w.Zone >= 3 || heavy || c > 0f; // a crowbar, a sledgehammer and every heavy swing: both hands
                float length = heavy ? 0.4f : 0.3f;
                if (swing < length)
                {
                    // Over the shoulder and down through whatever's in front.
                    float k = Mathf.Clamp01(swing / length); k = k * k * (3f - 2f * k);
                    Set(_upperR, Mathf.Lerp(-168f, -8f, k), Mathf.Lerp(10f, -22f, k), -8f, 1f);
                    Set(_lowerR, Mathf.Lerp(-35f, -4f, k), 0f, 0f, 1f);
                    if (two) { Set(_upperL, Mathf.Lerp(-160f, -14f, k), Mathf.Lerp(26f, 30f, k), 8f, 1f); Set(_lowerL, Mathf.Lerp(-45f, -10f, k), 0f, 0f, 1f); }
                }
                else
                {
                    // At the ready: forearm level, the weapon standing up out of the fist. Winding up lifts it back over the shoulder.
                    Set(_upperR, Mathf.Lerp(-22f, -165f, c), Mathf.Lerp(0f, 10f, c), -6f, 1f);
                    Set(_lowerR, Mathf.Lerp(-80f, -38f, c), 0f, 0f, 1f);
                    if (two) { Set(_upperL, Mathf.Lerp(-30f, -158f, c), 28f, 6f, 1f); Set(_lowerL, Mathf.Lerp(-78f, -46f, c), 0f, 0f, 1f); }
                }
            }
            else if (IsSlingshot(w))
            {
                // The fork held out at arm's length; the other hand draws the band back to the cheek.
                float kick = swing < 0.12f ? 1f - swing / 0.12f : 0f;
                Set(_upperL, -86f, 8f, 0f, 1f); Set(_lowerL, -4f, 0f, 0f, 1f);
                Set(_upperR, Mathf.Lerp(-58f, -78f, c) + kick * 10f, -20f, 0f, 1f);
                Set(_lowerR, Mathf.Lerp(-45f, -128f, c) * (1f - kick), 0f, 0f, 1f);
                band = c > 0.02f;
            }
            else if (w.Kind == RcKind.Ranged)
            {
                // Aimed down the arm, steadied with the other hand; it's pulled in tight as it charges and kicks when it fires.
                float kick = swing < 0.15f ? (1f - swing / 0.15f) * 16f : 0f;
                Set(_upperR, -84f - kick + c * 6f, -8f, -4f, 1f); Set(_lowerR, -6f - c * 14f, 0f, 0f, 1f);
                Set(_upperL, -72f - kick * 0.5f, 30f, 0f, 1f); Set(_lowerL, -30f, 0f, 0f, 1f);
            }
            else
            {
                // A gadget: carried at the chest.
                Set(_upperR, -18f, 0f, -4f, 1f); Set(_lowerR, -98f, 0f, 0f, 1f);
            }

            // The slingshot's band: from the fork to the hand that's drawing it.
            if (band && _weapon != null && _handR != null)
            {
                if (_band == null)
                {
                    _band = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Object.Destroy(_band.GetComponent<Collider>());
                    _band.name = "KE_RcBand";
                    _band.GetComponent<Renderer>().sharedMaterial = Mat.Lit(new Color(0.5f, 0.12f, 0.1f), 0.1f);
                }
                Vector3 a = _weapon.transform.TransformPoint(new Vector3(0f, -0.2f, 0f)), b = _handR.position;
                _band.SetActive(true);
                _band.transform.position = (a + b) * 0.5f;
                _band.transform.rotation = Quaternion.LookRotation(b - a + Vector3.up * 0.0001f);
                _band.transform.localScale = new Vector3(0.09f, 0.012f, (b - a).magnitude);
            }
            else if (_band != null && _band.activeSelf) _band.SetActive(false);
        }
    }
}
