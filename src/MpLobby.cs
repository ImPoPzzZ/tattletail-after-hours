using System;
using System.Collections;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Steam lobby lifecycle — friends-only, 2-player max, invite via the
    /// Steam overlay. Persistent DontDestroyOnLoad singleton created once
    /// from KaleidoscopeEndlessPlugin.Awake(), same shape as
    /// MainMenuButtonInjector but as a MonoBehaviour so it can run
    /// coroutines and poll MpNetworking.ListenData() every frame. Never
    /// calls SteamAPI.Init() — the base game's own SteamManager already
    /// does that.
    ///
    /// Step 1 (lobby-only) scope: this file only ever establishes the
    /// lobby/P2P connection, syncs run settings, and signals the guest to
    /// follow the host into the level. No gameplay state (movement,
    /// objectives, Mama, death) is synced from here — that's a later pass.
    /// </summary>
    public class MpLobby : MonoBehaviour
    {
        public static MpLobby Instance { get; private set; }

        public CSteamID LobbyId { get; private set; }
        public CSteamID PeerSteamID { get; private set; }
        public string PeerName { get; private set; } = "None";
        public bool LobbyExists { get; private set; }
        public bool IsHost { get; private set; }
        public bool HasPeer { get; private set; }

        public string LocalPlayerName => SteamFriends.GetPersonaName();

        private Callback<LobbyCreated_t> _lobbyCreatedCallback;
        private Callback<GameLobbyJoinRequested_t> _joinRequestedCallback;
        private Callback<LobbyEnter_t> _lobbyEnteredCallback;
        private Callback<LobbyChatUpdate_t> _lobbyChatUpdateCallback;

        private bool _coldStartChecked;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            MpNetworking.InitializeHandlers();

            _lobbyCreatedCallback = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            _joinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
            _lobbyEnteredCallback = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            _lobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
        }

        private void Update()
        {
            if (!_coldStartChecked)
            {
                _coldStartChecked = true;
                StartCoroutine(ColdStartJoinRoutine());
            }

            RcLobby.Tick(); // (Recalled's own lobby: separate from this one)

            if (!LobbyExists) return;
            MpNetworking.ListenData();
            CheckVersion();
        }

        // ── Versions ─────────────────────────────────────────────────────────
        // Both players write their version into their own lobby member data, which Steam keeps in sync (no packets
        // to time or retry). A friend on another version is treated as not there for Versus and Air Hockey, and
        // their game packets are ignored: two versions can read the same packet differently.

        private const string KEY_VERSION = "ke_version", KEY_NET = "ke_net";
        private const float VERSION_WAIT = 6f; // a friend with no version after this long has one from before 3.5
        private float _peerSince;
        private bool _mismatchShown;

        public enum PeerCompat { NoPeer, Checking, Same, Different }

        private void PublishVersion()
        {
            SteamMatchmaking.SetLobbyMemberData(LobbyId, KEY_VERSION, VersionTag.VERSION);
            SteamMatchmaking.SetLobbyMemberData(LobbyId, KEY_NET, VersionTag.NET_VERSION);
        }

        private string PeerData(string key)
        {
            if (!LobbyExists || !HasPeer) return "";
            return SteamMatchmaking.GetLobbyMemberData(LobbyId, PeerSteamID, key) ?? "";
        }

        /// <summary>The friend's After Hours version ("" = older than 3.5, or not known yet).</summary>
        public string PeerVersion { get { return PeerData(KEY_VERSION); } }

        public PeerCompat Compat
        {
            get
            {
                if (!LobbyExists || !HasPeer) return PeerCompat.NoPeer;
                string net = PeerData(KEY_NET);
                if (net.Length == 0) return Time.realtimeSinceStartup - _peerSince < VERSION_WAIT ? PeerCompat.Checking : PeerCompat.Different;
                return net == VersionTag.NET_VERSION ? PeerCompat.Same : PeerCompat.Different;
            }
        }

        /// <summary>The friend is on a different version: no Versus or Air Hockey together.</summary>
        public bool PeerIncompatible { get { return Compat == PeerCompat.Different; } }

        public string MismatchText
        {
            get
            {
                string theirs = PeerVersion;
                return (theirs.Length == 0 ? "Your friend has an older version of After Hours" : "Your friend is on After Hours " + theirs)
                     + ". You're on " + VersionTag.VERSION + ".\nVersus and Air Hockey need the same version.";
            }
        }

        private void CheckVersion()
        {
            if (_mismatchShown || !PeerIncompatible) return;
            _mismatchShown = true;
            KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [MP] Version mismatch: friend '" + PeerVersion + "' (net '" + PeerData(KEY_NET) + "'), us " + VersionTag.VERSION + " (net " + VersionTag.NET_VERSION + ").");
            VersusLobby.OnPeerChanged(false); // their seat empties
            if (HubMode.Active) HubMode.SetHudText(MismatchText);
        }

        // ── Host ─────────────────────────────────────────────────────────────

        public void CreateLobby()
        {
            if (LobbyExists) return;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [MP] Requesting lobby creation...");
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 2);
        }

        private void OnLobbyCreated(LobbyCreated_t callback)
        {
            if (RcLobby.Creating) { RcLobby.OnCreated(callback); return; } // a Recalled town's lobby, not this one

            if (callback.m_eResult != EResult.k_EResultOK)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: [MP] Lobby creation failed: " + callback.m_eResult);
                return;
            }

            LobbyId = new CSteamID(callback.m_ulSteamIDLobby);
            LobbyExists = true;
            IsHost = true;
            ResetPeerState();
            PublishVersion();
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [MP] Lobby created: " + LobbyId);
            if (_inviteWhenCreated) { _inviteWhenCreated = false; InviteFriend(); }
        }

        private bool _inviteWhenCreated;

        /// <summary>Versus INVITE PLAYER: opens Steam's invite window, creating the lobby first if needed.</summary>
        public void InviteOrCreate()
        {
            if (LobbyExists) { InviteFriend(); return; }
            _inviteWhenCreated = true;
            CreateLobby();
        }

        public void InviteFriend()
        {
            if (!LobbyExists) return;
            SteamFriends.ActivateGameOverlayInviteDialog(LobbyId);
        }

        // ── Join ─────────────────────────────────────────────────────────────

        // Fires when a friend accepts an invite while this game process is
        // already running.
        private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
        {
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [MP] GameLobbyJoinRequested for " + callback.m_steamIDLobby);
            JoinLobby(callback.m_steamIDLobby);
        }

        private void JoinLobby(CSteamID lobbyId)
        {
            RcLobby.Leave(); // (one Steam lobby at a time, whichever kind the invite turns out to be)
            if (LobbyExists) LeaveLobby();
            SteamMatchmaking.JoinLobby(lobbyId);
        }

        // A cold launch (game not already running) instead passes
        // "+connect_lobby <id>" on the command line — Valve's documented
        // behavior for invite-launches-a-fresh-process. We have to notice
        // and join it ourselves. Confirmed via real 2-player testing that
        // attempting this too early (before the "menu" scene, and before
        // Steam's own networking is warmed up on a cold launch) silently
        // fails — wait for the real menu scene first, then retry a few
        // times rather than assuming the first attempt lands.
        private IEnumerator ColdStartJoinRoutine()
        {
            ulong lobbyId = ParseColdStartLobbyId();
            if (lobbyId == 0) yield break;

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [MP] Cold-start invite detected (+connect_lobby " + lobbyId + ") — waiting for menu scene.");

            float waited = 0f;
            const float sceneTimeout = 30f;
            while (SceneManager.GetActiveScene().name != "menu" && waited < sceneTimeout)
            {
                yield return new WaitForSeconds(0.5f);
                waited += 0.5f;
            }

            const int maxAttempts = 3;
            for (int attempt = 1; attempt <= maxAttempts && !LobbyExists; attempt++)
            {
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [MP] Cold-start join attempt #" + attempt);
                JoinLobby(new CSteamID(lobbyId));
                yield return new WaitForSeconds(5f);
            }
        }

        private static ulong ParseColdStartLobbyId()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "+connect_lobby")
                {
                    ulong id;
                    if (ulong.TryParse(args[i + 1], out id)) return id;
                }
            }
            return 0;
        }

        private void OnLobbyEntered(LobbyEnter_t callback)
        {
            if (RcLobby.Claims(new CSteamID(callback.m_ulSteamIDLobby))) { RcLobby.OnEntered(callback); return; } // a Recalled town: it takes it from here

            var response = (EChatRoomEnterResponse)callback.m_EChatRoomEnterResponse;
            if (response != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [MP] Lobby join failed: " + response);
                return;
            }

            LobbyId = new CSteamID(callback.m_ulSteamIDLobby);
            LobbyExists = true;
            IsHost = SteamMatchmaking.GetLobbyOwner(LobbyId) == SteamUser.GetSteamID();
            PublishVersion();
            RefreshPeer();
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [MP] Entered lobby " + LobbyId + " (host=" + IsHost + ")");

            // Invited player: straight to their own Arcade, told to go to VERSUS.
            if (!IsHost) VersusLobby.OnJoinedAsGuest(SteamFriends.GetFriendPersonaName(SteamMatchmaking.GetLobbyOwner(LobbyId)));
        }

        private void OnLobbyChatUpdate(LobbyChatUpdate_t callback)
        {
            if (RcLobby.Exists && callback.m_ulSteamIDLobby == RcLobby.Id.m_SteamID) { RcLobby.OnChat(callback); return; }
            if (!LobbyExists || callback.m_ulSteamIDLobby != LobbyId.m_SteamID) return;
            RefreshPeer();
        }

        // ── Peer tracking ────────────────────────────────────────────────────

        private void RefreshPeer()
        {
            bool hadPeer = HasPeer;
            ResetPeerState();

            CSteamID localId = SteamUser.GetSteamID();
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(LobbyId);
            for (int i = 0; i < memberCount; i++)
            {
                CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(LobbyId, i);
                if (member == localId) continue;
                PeerSteamID = member;
                PeerName = SteamFriends.GetFriendPersonaName(member);
                HasPeer = true;
                break;
            }

            if (HasPeer && !hadPeer)
            {
                _peerSince = Time.realtimeSinceStartup;
                _mismatchShown = false;
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [MP] Peer connected: " + PeerName);
                SendWelcome();
                if (IsHost) MpLobbySettingsSync.BroadcastCurrentSettings();
                VersusLobby.OnPeerChanged(true);
            }
            else if (!HasPeer && hadPeer)
            {
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [MP] Peer disconnected.");
                VersusLobby.OnPeerChanged(false);
            }
        }

        private void SendWelcome()
        {
            using (var packet = new MpPacket(MpNetworking.PacketId.Welcome))
            {
                packet.Write("Hello from " + LocalPlayerName);
                MpNetworking.SendReliable(PeerSteamID, packet);
            }
        }

        public void OnWelcomeReceived()
        {
            // Connectivity confirmation only — HasPeer is already driven
            // by real lobby membership (RefreshPeer), not by this packet.
        }

        private void ResetPeerState()
        {
            PeerSteamID = CSteamID.Nil;
            PeerName = "None";
            HasPeer = false;
        }

        // ── Leave ────────────────────────────────────────────────────────────

        public void LeaveLobby()
        {
            if (!LobbyExists) return;
            SteamMatchmaking.LeaveLobby(LobbyId);
            LobbyId = CSteamID.Nil;
            LobbyExists = false;
            IsHost = false;
            ResetPeerState();
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [MP] Left lobby.");
        }
    }
}
