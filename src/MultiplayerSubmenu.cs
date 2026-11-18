using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Nested "submenu in the submenu" off EndlessSubmenu — Steam-invite
    /// lobby: host a lobby, invite a friend via the Steam overlay, see
    /// connection status, start the run once a peer's connected. No manual
    /// "join by code" here — joining only ever happens by accepting the
    /// Steam invite (MpLobby's GameLobbyJoinRequested_t/cold-start
    /// handling), same as this feature's original design. Same clone-real-
    /// widgets recipe as every other submenu in this project.
    ///
    /// Step 1 scope: this only gets both players connected and into the
    /// level together — no movement/objective/Mama/death sync exists yet.
    /// </summary>
    public static class MultiplayerSubmenu
    {
        private static GameObject _panel;
        private static MainMenu _menu;

        private static Text _lobbyButtonLabel;
        private static Button _runCustomizationButton;
        private static Button _inviteButton;
        private static Text _connectionLabel;
        private static Text _startGameLabel;
        private static Button _startGameButton;

        public static void Build(MainMenu menu)
        {
            _menu = menu;

            GameObject optionsPanelTemplate = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "OptionsPanel");
            GameObject cycleTemplate        = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "Button_Sensitivity");
            GameObject backTemplate         = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "Button_Back");
            GameObject titleTemplate        = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "OptimizeText");
            GameObject enterTemplate        = MainMenuButtonInjector.FindChildByName(menu.mainMenuHolder, "Button_NewGame");

            if (optionsPanelTemplate == null)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: MultiplayerSubmenu.Build — OptionsPanel template not found, cannot build.");
                return;
            }

            _panel = Object.Instantiate(optionsPanelTemplate, menu.mainMenuHolder.transform.parent, false);
            _panel.name = "KE_MultiplayerPanel";
            // DestroyImmediate, not Destroy — see StatsSubmenu.Build()'s
            // comment: Destroy() defers to end of frame, and a lazily-
            // built panel's Build()+first Show() can land in that same
            // frame, leaving the real vanilla OptionsPanel buttons (still
            // interactable) sitting under our own rows in the meantime.
            for (int i = _panel.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(_panel.transform.GetChild(i).gameObject);

            var mainPanelRT = menu.mainMenuHolder.GetComponent<RectTransform>();
            var panelRT = _panel.GetComponent<RectTransform>();
            if (mainPanelRT != null && panelRT != null)
            {
                panelRT.anchorMin = mainPanelRT.anchorMin;
                panelRT.anchorMax = mainPanelRT.anchorMax;
                panelRT.pivot = mainPanelRT.pivot;
                panelRT.anchoredPosition = mainPanelRT.anchoredPosition + new Vector2(0f, 100f);
            }

            var vlg = _panel.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) vlg.enabled = false;

            float y = -10f;
            const float buttonHeight = 33f;
            const float titleHeight = 40f;
            const float spacing = 6f;

            if (titleTemplate != null)
            {
                GameObject title = MainMenuButtonInjector.CloneLabel(titleTemplate, _panel.transform, "MULTIPLAYER");
                MainMenuButtonInjector.PositionChild(title, y, titleHeight);
                y -= titleHeight + spacing + 30f;
            }

            GameObject lobbyBtn = MainMenuButtonInjector.CloneButton(cycleTemplate, _panel.transform, "Host Game", OnLobbyButton);
            MainMenuButtonInjector.PositionChild(lobbyBtn, y, buttonHeight);
            y -= buttonHeight + spacing;
            _lobbyButtonLabel = lobbyBtn?.GetComponentInChildren<Text>(true);

            GameObject runCustomizationBtn = MainMenuButtonInjector.CloneButton(cycleTemplate, _panel.transform, "Run Customization", OnRunCustomization);
            MainMenuButtonInjector.PositionChild(runCustomizationBtn, y, buttonHeight);
            y -= buttonHeight + spacing;
            _runCustomizationButton = runCustomizationBtn?.GetComponent<Button>();

            GameObject inviteBtn = MainMenuButtonInjector.CloneButton(cycleTemplate, _panel.transform, "Invite Friend", OnInviteFriend);
            MainMenuButtonInjector.PositionChild(inviteBtn, y, buttonHeight);
            y -= buttonHeight + spacing;
            _inviteButton = inviteBtn?.GetComponent<Button>();

            if (titleTemplate != null)
            {
                GameObject connLabel = MainMenuButtonInjector.CloneLabel(titleTemplate, _panel.transform, "Connected: None");
                MainMenuButtonInjector.PositionChild(connLabel, y, buttonHeight);
                y -= buttonHeight + spacing;
                _connectionLabel = connLabel?.GetComponentInChildren<Text>(true);
                if (_connectionLabel != null) _connectionLabel.fontSize = 14;
            }

            if (enterTemplate != null)
            {
                GameObject startBtn = MainMenuButtonInjector.CloneButton(enterTemplate, _panel.transform, "Start Game", OnStartGame);
                MainMenuButtonInjector.PositionChild(startBtn, y, buttonHeight);
                y -= buttonHeight + spacing;
                _startGameButton = startBtn?.GetComponent<Button>();
                _startGameLabel = startBtn?.GetComponentInChildren<Text>(true);
            }

            if (backTemplate != null)
            {
                GameObject backBtn = MainMenuButtonInjector.CloneButton(backTemplate, _panel.transform, "Back", OnBack);
                MainMenuButtonInjector.PositionChild(backBtn, y, buttonHeight);
                y -= buttonHeight + spacing;
            }

            panelRT.sizeDelta = new Vector2(panelRT.sizeDelta.x, -y + 10f);

            _panel.SetActive(false);
        }

        private static void PlayGlitch()
        {
            if (Camera.main != null) AudioController.Play("vhs_glitch", Camera.main.transform);
        }

        private static void OnLobbyButton()
        {
            PlayGlitch();
            if (MpLobby.Instance == null) return;
            if (MpLobby.Instance.LobbyExists) MpLobby.Instance.LeaveLobby();
            else MpLobby.Instance.CreateLobby();
        }

        private static void OnRunCustomization()
        {
            if (MpLobby.Instance == null || !MpLobby.Instance.LobbyExists) { PlayGlitch(); return; }
            if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            Hide();
            RunCustomizationSubmenu.Show(_menu);
        }

        private static void OnInviteFriend()
        {
            if (MpLobby.Instance == null || !MpLobby.Instance.LobbyExists || !MpLobby.Instance.IsHost) return;
            PlayGlitch();
            MpLobby.Instance.InviteFriend();
        }

        private static void OnStartGame()
        {
            if (MpLobby.Instance == null || !MpLobby.Instance.IsHost || !MpLobby.Instance.HasPeer) { PlayGlitch(); return; }
            EndlessSubmenu.HostEnter(_menu);
        }

        private static void OnBack()
        {
            if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            Hide();
            EndlessSubmenu.Show(_menu);
        }

        public static void Show(MainMenu menu)
        {
            _menu = menu;
            if (_panel == null) Build(menu);
            if (_panel == null) return;
            // Refresh() no-ops unless the panel's already active — activate
            // first, then populate, or the very first open shows blank rows.
            _panel.SetActive(true);
            Refresh();
        }

        /// <summary>Live-updates lobby state while the panel is open — called every frame from KaleidoscopeEndlessPlugin.Update().</summary>
        public static void Refresh()
        {
            if (_panel == null || !_panel.activeSelf) return;
            var lobby = MpLobby.Instance;
            bool inLobby = lobby != null && lobby.LobbyExists;
            bool isHost = inLobby && lobby.IsHost;
            bool hasPeer = inLobby && lobby.HasPeer;

            if (_lobbyButtonLabel != null)
                _lobbyButtonLabel.text = inLobby ? "Leave Lobby" : "Host Game";

            if (_runCustomizationButton != null)
                _runCustomizationButton.interactable = inLobby;

            if (_inviteButton != null)
                _inviteButton.interactable = isHost;

            if (_connectionLabel != null)
                _connectionLabel.text = "Connected: " + (inLobby ? lobby.PeerName : "None");

            if (_startGameButton != null && _startGameLabel != null)
            {
                if (!inLobby) { _startGameLabel.text = "Start Game"; _startGameButton.interactable = false; }
                else if (isHost)
                {
                    _startGameLabel.text = hasPeer ? "Start Game" : "Start Game (waiting for peer)";
                    _startGameButton.interactable = hasPeer;
                }
                else
                {
                    _startGameLabel.text = "Waiting for host to start...";
                    _startGameButton.interactable = false;
                }
            }
        }

        public static void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }
    }
}
