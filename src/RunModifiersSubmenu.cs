using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Nested "submenu in the submenu" — opened from EndlessSubmenu's
    /// "Run Modifiers" button. Follows EndlessSubmenu.cs's exact
    /// established recipe: clone real vanilla widgets, plain Text labels,
    /// manual top-down PositionChild stacking.
    ///
    /// All modifiers are independent on/off toggles — any combination can
    /// be active at once, per explicit request.
    /// </summary>
    public static class RunModifiersSubmenu
    {
        private static GameObject _panel;
        private static MainMenu _menu;

        private static readonly string[] ModNames =
        {
            "Low Battery Mode",       // flashlight drains 2x as fast
            "No Rest for the Wicked", // no rest periods between objectives
            "No More Mama!",          // maxes out Mama's aggressiveness
            "Batteries Not Included", // Tattletail's charge drops 50% faster
            "Always Hungry",          // Tattletail's feed bar drops 50% faster
            "Brushing is Key",        // Tattletail's groom bar drops 50% faster
            "Power Ups",              // one random-ability coin spawns per round (see PowerUpObjective.cs)
        };

        // Sized off ModNames.Length rather than a hardcoded literal — a
        // mismatched literal here is exactly the kind of bug that already
        // bit the multiplayer LobbySettings payload once (see
        // MpPacketHandlers.OnLobbySettings).
        private static readonly bool[] _values = new bool[ModNames.Length];
        private static readonly Text[] _labels = new Text[ModNames.Length];

        // Set true when opened from RunCustomizationSubmenu as a
        // multiplayer guest — rows display the host's synced values but
        // can't be toggled locally. Solo/host play never sets this.
        public static bool IsReadOnly;
        private static bool _cameFromRunCustomization;

        // Public read-only view for EndlessSubmenu to copy into
        // EndlessRunManager.PendingMod* at Enter time (solo/host path).
        public static bool ModLowBattery => _values[0];
        public static bool ModNoRest => _values[1];
        public static bool ModMaxMamaAggro => _values[2];
        public static bool ModBatteriesNotIncluded => _values[3];
        public static bool ModAlwaysHungry => _values[4];
        public static bool ModBrushingIsKey => _values[5];
        public static bool ModPowerUps => _values[6];

        // For the hub's Endless setup screen.
        public static int Count => ModNames.Length;
        public static string Name(int i) { return ModNames[i]; }

        // Player-facing, same order as ModNames (shown on the hub's Endless screen).
        private static readonly string[] ModDescriptions =
        {
            "Your flashlight drains twice as fast.",
            "No breathers: the next objective starts right away.",
            "Mama is at her most aggressive the whole run.",
            "Tattletail's charge drops 50% faster.",
            "Tattletail gets hungry 50% faster.",
            "Tattletail needs brushing 50% more often.",
            "Each round there's a chance a coin spawns somewhere in the house. Walk near it to see its ability, grab it to use it. Doesn't give bonus shards.",
        };

        public static string Description(int i) { return i >= 0 && i < ModDescriptions.Length ? ModDescriptions[i] : ""; }
        public static bool Get(int i) { return _values[i]; }
        public static void Toggle(int i) { _values[i] = !_values[i]; }

        public static void Build(MainMenu menu)
        {
            _menu = menu;

            GameObject optionsPanelTemplate = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "OptionsPanel");
            GameObject toggleTemplate       = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "Button_MotionBlur");
            GameObject backTemplate         = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "Button_Back");
            GameObject titleTemplate        = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "OptimizeText");

            if (optionsPanelTemplate == null)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: RunModifiersSubmenu.Build — OptionsPanel template not found, cannot build.");
                return;
            }

            _panel = Object.Instantiate(optionsPanelTemplate, menu.mainMenuHolder.transform.parent, false);
            _panel.name = "KE_RunModifiersPanel";
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
                // Title + 6 toggles + Back is taller than the simple
                // category-button submenus — same recurring "Back gets cut
                // off, bump this offset" issue every time a row gets added,
                // addressed up front here with a bigger offset than those.
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
                GameObject title = MainMenuButtonInjector.CloneLabel(titleTemplate, _panel.transform, "RUN MODIFIERS");
                MainMenuButtonInjector.PositionChild(title, y, titleHeight);
                // The title's actual rendered glyphs (wavy/glitchy title
                // font+effect) extend well past the nominal titleHeight box
                // — confirmed by a real screenshot showing it overlapping
                // "Low Battery Mode" so badly it was unreadable unless
                // highlighted. Extra clearance here, not just titleHeight
                // + spacing, pushes every row down away from it.
                y -= titleHeight + spacing + 30f;
            }

            for (int i = 0; i < ModNames.Length; i++)
            {
                int idx = i; // local copy — closures over a for-loop variable would all share the same final value otherwise
                GameObject btn = MainMenuButtonInjector.CloneButton(toggleTemplate, _panel.transform,
                    ModLabel(idx), () => OnToggleMod(idx));
                MainMenuButtonInjector.PositionChild(btn, y, buttonHeight);
                y -= buttonHeight + spacing;
                _labels[idx] = btn?.GetComponentInChildren<Text>(true);
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

        private static string ModLabel(int idx)
        {
            return ModNames[idx] + ": " + (_values[idx] ? "On" : "Off");
        }

        private static void PlayGlitch()
        {
            if (Camera.main != null) AudioController.Play("vhs_glitch", Camera.main.transform);
        }

        private static void OnToggleMod(int idx)
        {
            if (IsReadOnly)
            {
                PlayGlitch();
                return; // host-controlled in multiplayer — guest can only look
            }

            PlayGlitch();
            _values[idx] = !_values[idx];
            if (_labels[idx] != null)
                _labels[idx].text = ModLabel(idx);

            if (MpLobby.Instance != null && MpLobby.Instance.IsHost)
                MpLobbySettingsSync.BroadcastCurrentSettings();
        }

        /// <summary>Copy of the 7 current toggle values, for MpLobbySettingsSync to send.</summary>
        public static bool[] GetValues()
        {
            var copy = new bool[_values.Length];
            System.Array.Copy(_values, copy, _values.Length);
            return copy;
        }

        /// <summary>Applies host-synced values on the client — never called on host/solo.</summary>
        public static void ApplySyncedValues(bool[] values)
        {
            for (int i = 0; i < _values.Length && i < values.Length; i++)
            {
                _values[i] = values[i];
                if (_labels[i] != null) _labels[i].text = ModLabel(i);
            }
        }

        private static void OnBack()
        {
            if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            Hide();
            if (_cameFromRunCustomization) RunCustomizationSubmenu.Show(_menu);
            else EndlessSubmenu.Show(_menu);
        }

        public static void Show(MainMenu menu)
        {
            Show(menu, false, false);
        }

        /// <summary>Opened from RunCustomizationSubmenu — readOnly is true for a connected multiplayer guest (host's values only, not editable here).</summary>
        public static void Show(MainMenu menu, bool cameFromRunCustomization, bool readOnly)
        {
            _menu = menu;
            _cameFromRunCustomization = cameFromRunCustomization;
            IsReadOnly = readOnly;
            if (_panel == null) Build(menu);
            if (_panel == null) return;
            for (int i = 0; i < _labels.Length; i++)
                if (_labels[i] != null) _labels[i].text = ModLabel(i);
            _panel.SetActive(true);
        }

        public static void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }
    }
}
