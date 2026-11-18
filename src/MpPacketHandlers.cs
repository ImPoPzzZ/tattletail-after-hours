namespace KaleidoscopeEndless
{
    /// <summary>
    /// Static handlers dispatched by MpNetworking.ListenData(), one per
    /// PacketId. Step 1 (lobby-only) scope: connectivity handshake, host's
    /// run-settings broadcast, and the "follow the host into the level"
    /// signal. No gameplay packets exist yet.
    /// </summary>
    public static class MpPacketHandlers
    {
        public static void OnWelcome(MpPacket packet)
        {
            string message = packet.ReadString();
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [MP] Welcome received: " + message);
            MpLobby.Instance?.OnWelcomeReceived();
        }

        public static void OnLobbySettings(MpPacket packet)
        {
            int difficultyIndex = packet.ReadInt();
            var mods = new bool[7]; // Diamond Flashlight moved to CustomizationSubmenu — no longer part of this synced set; Power Ups added as the 7th
            for (int i = 0; i < mods.Length; i++) mods[i] = packet.ReadBool();

            // Only ever meaningful on a client — the host is the one who
            // decided these values in the first place.
            if (MpLobby.Instance == null || MpLobby.Instance.IsHost) return;

            RunCustomizationSubmenu.ApplyHostDifficulty(difficultyIndex);
            RunModifiersSubmenu.ApplySyncedValues(mods);
        }

        public static void OnLevelTransition(MpPacket packet)
        {
            // Guest-only — the host is the one who triggered this.
            if (MpLobby.Instance == null || MpLobby.Instance.IsHost) return;
            EndlessSubmenu.OnHostStartedLevel();
        }
    }
}
