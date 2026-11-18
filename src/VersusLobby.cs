using Steamworks;
using UnityEngine.SceneManagement;

namespace KaleidoscopeEndless
{
    public enum VersusSeat { Empty = 0, Host = 1, Guest = 2, Bot = 3 }

    /// <summary>
    /// Who sits where on the Versus screen, shared between both games.
    /// The lobby host owns the seats: the guest asks (VersusClaim), the host
    /// decides and sends everyone the result (VersusState). Solo (no lobby)
    /// you're the "host" and it all stays local. START on either side starts
    /// both games (VersusStart), each in its own role.
    /// </summary>
    public static class VersusLobby
    {
        public static VersusSeat Mama, Survivor;

        /// <summary>The match is played in the Waygetter Facility instead of the house. The host decides; the guest sees it and may ask for a change.</summary>
        public static bool FacilityMap;

        private const int MAP_CLAIM = 100; // (a VersusClaim whose "role" is this asks the host to set the map: who = 0 house, 1 Facility)

        public static void SetMap(bool facility)
        {
            if (IsGuest && HasFriend)
            {
                using (var p = new MpPacket(MpNetworking.PacketId.VersusClaim))
                {
                    p.Write(MAP_CLAIM);
                    p.Write(facility ? 1 : 0);
                    MpNetworking.SendReliable(Lobby.PeerSteamID, p);
                }
                return;
            }
            FacilityMap = facility;
            Broadcast();
        }

        /// <summary>Shown the next time the Arcade finishes loading (e.g. "you joined X's lobby").</summary>
        public static string PendingArcadeMessage;

        private static MpLobby Lobby { get { return MpLobby.Instance; } }
        public static bool InLobby { get { return Lobby != null && Lobby.LobbyExists; } }
        public static bool HasFriend { get { return InLobby && Lobby.HasPeer && !Lobby.PeerIncompatible; } } // (a friend on another version doesn't count)
        public static bool IsGuest { get { return InLobby && !Lobby.IsHost; } }
        public static VersusSeat Me { get { return IsGuest ? VersusSeat.Guest : VersusSeat.Host; } }
        public static VersusSeat Friend { get { return IsGuest ? VersusSeat.Host : VersusSeat.Guest; } }
        public static string FriendName { get { return HasFriend ? Lobby.PeerName : "Friend"; } }

        /// <summary>Take a seat (for me, or the PLAYTEST bot), or Empty it.</summary>
        public static void Claim(VersusRole role, VersusSeat who)
        {
            if (IsGuest && HasFriend)
            {
                using (var p = new MpPacket(MpNetworking.PacketId.VersusClaim))
                {
                    p.Write((int)role);
                    p.Write((int)who);
                    MpNetworking.SendReliable(Lobby.PeerSteamID, p);
                }
                return;
            }
            Apply(role, who);
            Broadcast();
        }

        private static void Apply(VersusRole role, VersusSeat who)
        {
            if (role == VersusRole.Mama)
            {
                if (who != VersusSeat.Empty && who != VersusSeat.Bot && Survivor == who) Survivor = VersusSeat.Empty; // one seat each
                Mama = who;
            }
            else
            {
                if (who != VersusSeat.Empty && who != VersusSeat.Bot && Mama == who) Mama = VersusSeat.Empty;
                Survivor = who;
            }
        }

        /// <summary>Host: tell the guest how the seats look now.</summary>
        public static void Broadcast()
        {
            if (!HasFriend || IsGuest) return;
            using (var p = new MpPacket(MpNetworking.PacketId.VersusState))
            {
                p.Write((int)Mama);
                p.Write((int)Survivor);
                p.Write(FacilityMap ? 1 : 0);
                MpNetworking.SendReliable(Lobby.PeerSteamID, p);
            }
        }

        public static void OnClaim(MpPacket p) // host receives the guest's request
        {
            int roleRaw = p.ReadInt();
            int whoRaw = p.ReadInt();
            if (IsGuest) return;
            if (roleRaw == MAP_CLAIM) { FacilityMap = whoRaw == 1; Broadcast(); return; }
            var role = (VersusRole)roleRaw;
            var who = (VersusSeat)whoRaw;
            if (who == VersusSeat.Host) return; // the guest can only seat itself or a bot
            Apply(role, who);
            Broadcast();
        }

        public static void OnState(MpPacket p) // guest receives the host's seats
        {
            Mama = (VersusSeat)p.ReadInt();
            Survivor = (VersusSeat)p.ReadInt();
            FacilityMap = p.ReadInt() == 1;
        }

        /// <summary>From MpLobby when the other player arrives or leaves.</summary>
        public static void OnPeerChanged(bool connected)
        {
            if (connected) { Broadcast(); return; }
            if (Mama == VersusSeat.Guest || Mama == VersusSeat.Host && IsGuest) Mama = VersusSeat.Empty;
            if (Survivor == VersusSeat.Guest || Survivor == VersusSeat.Host && IsGuest) Survivor = VersusSeat.Empty;
        }

        /// <summary>Solo only: a fresh screen each time. In a lobby the seats persist.</summary>
        public static void ResetIfSolo()
        {
            if (!HasFriend) Mama = Survivor = VersusSeat.Empty;
        }

        public static bool Ready { get { return Mama != VersusSeat.Empty && Survivor != VersusSeat.Empty; } }

        /// <summary>START: both games go, each in its own seat.</summary>
        public static void Start()
        {
            if (!Ready) return;
            if (HasFriend)
                using (var p = new MpPacket(MpNetworking.PacketId.VersusStart))
                    MpNetworking.SendReliable(Lobby.PeerSteamID, p);
            StartLocal();
        }

        public static void OnStart(MpPacket p) { StartLocal(); }

        private static void StartLocal()
        {
            VersusMenu.CloseIfOpen();
            if (Mama == Me) VersusMode.StartMama(Survivor == VersusSeat.Bot);
            else if (Survivor == Me) VersusMode.StartSurvivor(Mama == VersusSeat.Bot);
        }

        /// <summary>Guest accepted an invite: into their own Arcade, with a note on what to do.</summary>
        public static void OnJoinedAsGuest(string hostName)
        {
            string msg = "You joined " + hostName + "'s lobby!\nGo to the VERSUS machine and press PLAYER JOIN.";
            if (HubMode.Active && HubController.Instance != null && HubController.Instance.Built)
            {
                HubMode.SetHudText(msg);
                return;
            }
            PendingArcadeMessage = msg;
            // On the menu (e.g. the invite launched the game) the Arcade loads on its own.
            if (SceneManager.GetActiveScene().name == "menu" || HubMode.Pending) return;
            // In a run or the story: leave it for the Arcade.
            SceneMonitor.ResetForNewRun();
            HubMode.Launch(null);
        }
    }
}
