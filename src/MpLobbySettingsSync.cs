namespace KaleidoscopeEndless
{
    /// <summary>
    /// Host-authoritative run-settings broadcast — difficulty and the 7 Run
    /// Modifiers toggles are shared/consistent for both players (they drive
    /// the one shared Mama's difficulty scaling), unlike Tattletail color/
    /// voice/Mama-cosmetic-voice, which stay per-player local picks in
    /// EndlessSubmenu exactly as they already were. Called by RunCustomizationSubmenu
    /// on every host edit and by MpLobby the moment a peer connects, so a
    /// late-joining guest immediately sees the host's current picks instead
    /// of stale defaults.
    /// </summary>
    public static class MpLobbySettingsSync
    {
        public static void BroadcastCurrentSettings()
        {
            var lobby = MpLobby.Instance;
            if (lobby == null || !lobby.IsHost || !lobby.HasPeer) return;

            using (var packet = new MpPacket(MpNetworking.PacketId.LobbySettings))
            {
                packet.Write(RunCustomizationSubmenu.DifficultyIndex);
                bool[] mods = RunModifiersSubmenu.GetValues();
                foreach (bool mod in mods) packet.Write(mod);
                MpNetworking.SendReliable(lobby.PeerSteamID, packet);
            }
        }
    }
}
