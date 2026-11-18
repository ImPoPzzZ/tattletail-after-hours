using System.Collections.Generic;
using Steamworks;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Thin wrapper around SteamNetworking's P2P packet API + a packet-ID
    /// dispatch table. Reliability is Steam's own (k_EP2PSendReliable),
    /// not hand-rolled — unlike the earlier UDP-only rewrite of this mod's
    /// multiplayer, we're back on Steam's relay/NAT-punch, so there's no
    /// need for our own ACK/retry layer.
    ///
    /// Step 1 (lobby-only) only ever sends/receives Welcome, LobbySettings,
    /// and LevelTransition. IDs above are reserved for the next phase
    /// (movement/objective/Mama/death sync) so a later pass can add
    /// handlers here without renumbering anything already wired.
    /// </summary>
    public static class MpNetworking
    {
        public static class PacketId
        {
            public const int Welcome = 0;
            public const int LobbySettings = 1;
            public const int LevelTransition = 2;
            public const int VersusState = 3;   // host -> guest: who sits in which Versus seat
            public const int VersusClaim = 4;   // guest -> host: "put me (or a bot) in this seat"
            public const int VersusStart = 5;   // either -> other: START was pressed
            public const int VersusPose = 6;    // either -> other, ~10/s unreliable: where I am
            public const int Marks = 8;         // either -> other: placement marks (HubMarkTool), shown to both players
            public const int VersusEvent = 7;   // either -> other: noise, Mama away/back, parrot, flash kill, caught, saved, round over
            public const int AirHockey = 9;     // the Arcade's air hockey table: seats, mallets, the puck (host runs it), goals
            // Reserved for the gameplay-sync phase: Movement, ObjectiveStart,
            // ObjectiveInteractRequest, ObjectiveProgress, ObjectiveComplete,
            // MamaState, MamaEvent, RemoteCaught, PlayerDied, PlayerRespawned.
        }

        private delegate void PacketHandler(MpPacket packet);
        private static Dictionary<int, PacketHandler> _handlers;
        private static Callback<P2PSessionRequest_t> _sessionRequestCallback;

        public static void InitializeHandlers()
        {
            _handlers = new Dictionary<int, PacketHandler>
            {
                { PacketId.Welcome, MpPacketHandlers.OnWelcome },
                { PacketId.LobbySettings, MpPacketHandlers.OnLobbySettings },
                { PacketId.LevelTransition, MpPacketHandlers.OnLevelTransition },
                { PacketId.VersusState, VersusLobby.OnState },
                { PacketId.VersusClaim, VersusLobby.OnClaim },
                { PacketId.VersusStart, VersusLobby.OnStart },
                { PacketId.VersusPose, VersusNet.OnPose },
                { PacketId.VersusEvent, VersusNet.OnEvent },
                { PacketId.Marks, HubMarkTool.OnPacket },
                { PacketId.AirHockey, AirHockeyGame.OnPacket },
            };

            if (_sessionRequestCallback == null)
                _sessionRequestCallback = Callback<P2PSessionRequest_t>.Create(OnP2PSessionRequest);
        }

        // Steam requires explicitly accepting an inbound session before any
        // packet from that peer will actually arrive — only accept from the
        // one peer we're expecting (our own lobby's other member), never a
        // stranger.
        private static void OnP2PSessionRequest(P2PSessionRequest_t request)
        {
            // Someone in the same Recalled town (its own lobby, up to four).
            if (RcLobby.IsMember(request.m_steamIDRemote)) { SteamNetworking.AcceptP2PSessionWithUser(request.m_steamIDRemote); return; }

            var lobby = MpLobby.Instance;
            if (lobby == null || !lobby.LobbyExists) return;
            if (lobby.PeerSteamID.m_SteamID != 0 && request.m_steamIDRemote != lobby.PeerSteamID) return;

            SteamNetworking.AcceptP2PSessionWithUser(request.m_steamIDRemote);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [MP] Accepted P2P session from " + request.m_steamIDRemote);
        }

        public static void SendReliable(CSteamID target, MpPacket packet)
        {
            Send(target, packet, EP2PSend.k_EP2PSendReliable);
        }

        public static void SendUnreliable(CSteamID target, MpPacket packet)
        {
            Send(target, packet, EP2PSend.k_EP2PSendUnreliable);
        }

        private static void Send(CSteamID target, MpPacket packet, EP2PSend sendType)
        {
            if (target.m_SteamID == 0) return;
            if (MpLobby.Instance != null && MpLobby.Instance.PeerIncompatible) return; // another version: it could misread it
            byte[] data = packet.ToArray();
            bool ok = SteamNetworking.SendP2PPacket(target, data, (uint)data.Length, sendType);
            if (!ok)
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [MP] SendP2PPacket failed to " + target);
        }

        public static void ListenData()
        {
            uint size;
            while (SteamNetworking.IsP2PPacketAvailable(out size))
            {
                byte[] data = new byte[size];
                uint bytesRead;
                CSteamID sender;
                if (!SteamNetworking.ReadP2PPacket(data, size, out bytesRead, out sender)) break;

                if (MpLobby.Instance != null && MpLobby.Instance.PeerIncompatible) continue; // another version: we could misread it
                using (var packet = new MpPacket(data))
                {
                    int id = packet.ReadInt();
                    PacketHandler handler;
                    if (_handlers != null && _handlers.TryGetValue(id, out handler)) handler(packet);
                    else KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [MP] Received unknown packet ID " + id);
                }
            }
        }
    }
}
