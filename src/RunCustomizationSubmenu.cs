using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Nested "submenu in the submenu" off MultiplayerSubmenu — the
    /// shared-run-settings counterpart to EndlessSubmenu's per-player
    /// cosmetic picks (Tattletail color/voice/Mama-voice stay local to
    /// each player's own screen, unchanged). Difficulty and the 7 Run
    /// Modifiers scale the one shared host-authoritative Mama, so they
    /// have to be a single agreed value, not two independent guesses —
    /// the host picks, MpLobbySettingsSync broadcasts, the guest sees a
    /// read-only mirror. Same clone-real-widgets/manual-PositionChild
    /// recipe as every other submenu in this project.
    /// </summary>
    public static class RunCustomizationSubmenu
    {
        private static GameObject _panel;
        private static MainMenu _menu;

        private struct DifficultyOpt
        {
            public string label; public float mult;
            public DifficultyOpt(string l, float m) { label = l; mult = m; }
        }
        private static readonly DifficultyOpt[] DIFFICULTIES =
        {
            new DifficultyOpt("Easy",       0.7f),
            new DifficultyOpt("Normal",     1.0f),
            new DifficultyOpt("Hard",       1.4f),
            new DifficultyOpt("Impossible", 2.0f),
        };

        private static int _difficultyIndex = 1; // default Normal

        public static int DifficultyIndex => _difficultyIndex;
        public static float DifficultyMult => DIFFICULTIES[_difficultyIndex].mult;
        public static string DifficultyLabel => DIFFICULTIES[_difficultyIndex].label;

        private static Text _difficultyLabel;
        private static Text _statusLabel;

        public static void Build(MainMenu menu)
        {
            _menu = menu;

            GameObject optionsPanelTemplate = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "OptionsPanel");
            GameObject cycleTemplate        = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "Button_Sensitivity");
            GameObject backTemplate         = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "Button_Back");
            GameObject titleTemplate        = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "OptimizeText");

            if (optionsPanelTemplate == null)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: RunCustomizationSubmenu.Build — OptionsPanel template not found, cannot build.");
                return;
            }

            _panel = Object.Instantiate(optionsPanelTemplate, menu.mainMenuHolder.transform.parent, false);
            _panel.name = "KE_RunCustomizationPanel";
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
                panelRT.anchoredPosition = mainPanelRT.anchoredPosition + new Vector2(0f, 110f);
            }

            var vlg = _panel.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) vlg.enabled = false;

            float y = -10f;
            const float buttonHeight = 33f;
            const float titleHeight = 40f;
            const float spacing = 6f;

            if (titleTemplate != null)
            {
                GameObject title = MainMenuButtonInjector.CloneLabel(titleTemplate, _panel.transform, "RUN CUSTOMIZATION");
                MainMenuButtonInjector.PositionChild(title, y, titleHeight);
                y -= titleHeight + spacing + 30f; // same title-overlap clearance RunModifiersSubmenu needed
            }

            GameObject difficultyBtn = MainMenuButtonInjector.CloneButton(cycleTemplate, _panel.transform,
                "Difficulty: " + DifficultyLabel, CycleDifficulty);
            MainMenuButtonInjector.PositionChild(difficultyBtn, y, buttonHeight);
            y -= buttonHeight + spacing;
            _difficultyLabel = difficultyBtn?.GetComponentInChildren<Text>(true);

            GameObject modifiersBtn = MainMenuButtonInjector.CloneButton(cycleTemplate, _panel.transform,
                "Run Modifiers", OnRunModifiers);
            MainMenuButtonInjector.PositionChild(modifiersBtn, y, buttonHeight);
            y -= buttonHeight + spacing;

            if (titleTemplate != null)
            {
                GameObject status = MainMenuButtonInjector.CloneLabel(titleTemplate, _panel.transform, "");
                MainMenuButtonInjector.PositionChild(status, y, buttonHeight);
                y -= buttonHeight + spacing;
                _statusLabel = status?.GetComponentInChildren<Text>(true);
                if (_statusLabel != null) _statusLabel.fontSize = 12;
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

        private static bool IsHost => MpLobby.Instance != null && MpLobby.Instance.IsHost;

        private static void CycleDifficulty()
        {
            if (!IsHost) { PlayGlitch(); return; } // guest: host-controlled, look only

            PlayGlitch();
            _difficultyIndex = (_difficultyIndex + 1) % DIFFICULTIES.Length;
            if (_difficultyLabel != null)
                _difficultyLabel.text = "Difficulty: " + DifficultyLabel;
            MpLobbySettingsSync.BroadcastCurrentSettings();
        }

        /// <summary>Applies the host's synced difficulty pick — never called on host/solo.</summary>
        public static void ApplyHostDifficulty(int index)
        {
            if (index < 0 || index >= DIFFICULTIES.Length) return;
            _difficultyIndex = index;
            if (_difficultyLabel != null)
                _difficultyLabel.text = "Difficulty: " + DifficultyLabel;
        }

        private static void OnRunModifiers()
        {
            if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            Hide();
            RunModifiersSubmenu.Show(_menu, true, !IsHost);
        }

        private static void OnBack()
        {
            PlayGlitch();
            Hide();
            MultiplayerSubmenu.Show(_menu);
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

        /// <summary>Live-updates the panel while it's open — called from KaleidoscopeEndlessPlugin.Update() same as the other submenus.</summary>
        public static void Refresh()
        {
            if (_panel == null || !_panel.activeSelf) return;
            if (_difficultyLabel != null) _difficultyLabel.text = "Difficulty: " + DifficultyLabel;
            if (_statusLabel != null)
                _statusLabel.text = IsHost
                    ? "You choose difficulty & modifiers for both players."
                    : "Host is choosing difficulty & modifiers.";
        }

        public static void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }
    }
}
