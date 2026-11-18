using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Nested "submenu in the submenu" off EndlessSubmenu — per-player
    /// cosmetic picks, extracted out of EndlessSubmenu itself so that
    /// panel could shrink back down to a short list of category buttons
    /// (Customization/Difficulty/Run Modifiers/Stats/Multiplayer/Enter/
    /// Back) instead of every individual toggle living at the top level.
    /// Tattletail color, Kaleidoscope Voice, Kaleidoscope Mama, and flashlight
    /// beam color are Shop purchases (only owned ones can be picked) and are all local to
    /// this player's own screen, solo or multiplayer alike — unlike
    /// Difficulty/Run Modifiers, which are shared/host-synced in
    /// multiplayer because they scale the one shared Mama.
    /// </summary>
    public static class CustomizationSubmenu
    {
        private static GameObject _panel;
        private static MainMenu _menu;

        // Three separate sets of picks and ownership: [0] = Endless (shards),
        // [1] = Papa's 10th Birthday! (Party Tokens, "Party " keys in Shop.txt),
        // [2] = Versus (Versus Tokens, "Versus " keys). M says which one is being read.
        private static int _mode;
        private static bool _party { get { return _mode == 1; } }
        private static int M { get { return _mode; } }
        private static readonly bool[] _voiceKal = new bool[3];
        private static readonly bool[] _mamaKal = new bool[3]; // off by default per request — opt-in, not opt-out
        private static readonly int[] _colorIdx = new int[3];
        private static readonly int[] _beamIdx = { -1, -1, -1 };  // -1 = default beam

        // Ownership in the current mode's shop.
        private static bool Own(string key) { return Shop.IsOwned(_mode == 1 ? "Party " + key : _mode == 2 ? "Versus " + key : key); }

        /// <summary>Which mode's picks the public getters below read. Endless launch paths set false, the birthday launch sets true.</summary>
        public static void SetMode(bool party) { _mode = party ? 1 : 0; }
        /// <summary>Versus picks (the VERSUS SHOP's items).</summary>
        public static void SetVersusMode() { _mode = 2; }
        public static bool PartyOpen { get { return _party && _panel != null && _panel.activeSelf; } }

        private struct ColorOpt
        {
            public string label; public RecolorType recolor; public bool isGold; public bool isDiamond; public string shopKey;
            public ColorOpt(string l, RecolorType r) { label = l; recolor = r; isGold = false; isDiamond = false; shopKey = null; }
            public ColorOpt(string l, RecolorType r, string key) { label = l; recolor = r; isGold = false; isDiamond = false; shopKey = key; }
            public ColorOpt(string l, RecolorType r, bool gold, bool diamond = false) { label = l; recolor = r; isGold = gold; isDiamond = diamond; shopKey = "Color " + l; }
        }
        // Gold/Diamond aren't real vanilla RecolorTypes (see GoldRecolor.cs/
        // DiamondRecolor.cs) — their recolor field is just ORIGINAL as a
        // placeholder; PendingGoldColor/PendingDiamondColor are what
        // actually route to the real tints. Every color except Original is
        // a Shop purchase (Shop.txt "Color <Name> = 1").
        private static readonly ColorOpt[] OPTS =
        {
            new ColorOpt("Original", RecolorType.ORIGINAL),
            new ColorOpt("Yellow",   RecolorType.YELLOW, "Color Yellow"),
            new ColorOpt("Teal",     RecolorType.TEAL,   "Color Teal"),
            new ColorOpt("Black",    RecolorType.BLACK,  "Color Black"),
            new ColorOpt("White",    RecolorType.WHITE,  "Color White"),
            new ColorOpt("Gold",     RecolorType.ORIGINAL, true),
            new ColorOpt("Diamond",  RecolorType.ORIGINAL, false, true),
        };

        // ── Public API for MainMenuButton.cs's shared pending-field helpers ────
        public static RecolorType ColorRecolor => OPTS[_colorIdx[M]].recolor;
        public static bool IsGoldColor => OPTS[_colorIdx[M]].isGold;
        public static bool IsDiamondColor => OPTS[_colorIdx[M]].isDiamond;
        public static bool KaleidoscopeVoice => _voiceKal[M];
        public static bool KaleidoscopeMama => _mamaKal[M];
        /// <summary>Name of the flashlight beam color picked (must be owned), or null for the default beam.</summary>
        public static string BeamColorName => _beamIdx[M] >= 0 ? FlashlightBeam.Colors[_beamIdx[M]].name : null;

        private static Text _colorLabel;
        private static Text _voiceLabel;
        private static Text _mamaLabel;
        private static Text _beamLabel;

        public static void Build(MainMenu menu)
        {
            _menu = menu;

            GameObject optionsPanelTemplate = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "OptionsPanel");
            GameObject cycleTemplate        = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "Button_Sensitivity");
            GameObject toggleTemplate       = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "Button_MotionBlur");
            GameObject backTemplate         = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "Button_Back");
            GameObject titleTemplate        = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "OptimizeText");

            if (optionsPanelTemplate == null)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: CustomizationSubmenu.Build — OptionsPanel template not found, cannot build submenu.");
                return;
            }

            _panel = Object.Instantiate(optionsPanelTemplate, menu.mainMenuHolder.transform.parent, false);
            _panel.name = "KE_CustomizationPanel";
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
                panelRT.anchoredPosition = mainPanelRT.anchoredPosition + new Vector2(0f, 95f);
            }

            var vlg = _panel.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) vlg.enabled = false;

            float y = -10f;
            const float buttonHeight = 33f;
            const float titleHeight = 40f;
            const float spacing = 6f;

            if (titleTemplate != null)
            {
                GameObject title = MainMenuButtonInjector.CloneLabel(titleTemplate, _panel.transform, "CUSTOMIZATION");
                MainMenuButtonInjector.PositionChild(title, y, titleHeight);
                y -= titleHeight + spacing;
            }

            GameObject colorBtn = MainMenuButtonInjector.CloneButton(cycleTemplate, _panel.transform,
                ColorLabel(_colorIdx[M]), CycleColor);
            MainMenuButtonInjector.PositionChild(colorBtn, y, buttonHeight);
            y -= buttonHeight + spacing;
            _colorLabel = colorBtn?.GetComponentInChildren<Text>(true);

            GameObject voiceBtn = MainMenuButtonInjector.CloneButton(toggleTemplate, _panel.transform,
                VoiceLabel(), ToggleVoice);
            MainMenuButtonInjector.PositionChild(voiceBtn, y, buttonHeight);
            y -= buttonHeight + spacing;
            _voiceLabel = voiceBtn?.GetComponentInChildren<Text>(true);

            GameObject mamaBtn = MainMenuButtonInjector.CloneButton(toggleTemplate, _panel.transform,
                MamaLabel(), ToggleMama);
            MainMenuButtonInjector.PositionChild(mamaBtn, y, buttonHeight);
            y -= buttonHeight + spacing;
            _mamaLabel = mamaBtn?.GetComponentInChildren<Text>(true);

            GameObject beamBtn = MainMenuButtonInjector.CloneButton(cycleTemplate, _panel.transform,
                BeamLabel(), CycleBeam);
            MainMenuButtonInjector.PositionChild(beamBtn, y, buttonHeight);
            y -= buttonHeight + spacing;
            _beamLabel = beamBtn?.GetComponentInChildren<Text>(true);

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

        private static bool ColorOwned(int idx)
        {
            string key = OPTS[idx].shopKey;
            return key == null || Own(key);
        }

        private static string ColorLabel(int idx)
        {
            return "Tattletail Color: " + OPTS[idx].label;
        }

        // Cycles through Original + whatever's been bought in the Shop.
        private static void CycleColor()
        {
            PlayGlitch();
            for (int step = 1; step <= OPTS.Length; step++)
            {
                int next = (_colorIdx[M] + step) % OPTS.Length;
                if (ColorOwned(next)) { _colorIdx[M] = next; break; }
            }
            if (_colorLabel != null)
                _colorLabel.text = ColorLabel(_colorIdx[M]);
        }

        private static bool BeamOwned(int idx)
        {
            return Own("Beam " + FlashlightBeam.Colors[idx].name);
        }

        private static string BeamLabel()
        {
            return "Flashlight Beam: " + (_beamIdx[M] < 0 ? "Default" : FlashlightBeam.Colors[_beamIdx[M]].name);
        }

        private static void CycleBeam()
        {
            PlayGlitch();
            // Order: Default, then each owned beam color, then back to Default.
            int next = _beamIdx[M];
            for (int step = 0; step <= FlashlightBeam.Colors.Length; step++)
            {
                next++;
                if (next >= FlashlightBeam.Colors.Length) { next = -1; break; }
                if (BeamOwned(next)) break;
            }
            _beamIdx[M] = next;
            if (_beamLabel != null) _beamLabel.text = BeamLabel();
        }

        private static string VoiceLabel()
        {
            if (!Own("Extra Kaleidoscope Voice")) return "Kaleidoscope Voice: LOCKED (Shop)";
            return "Kaleidoscope Voice: " + (_voiceKal[M] ? "On" : "Off");
        }

        private static string MamaLabel()
        {
            if (!Own("Extra Kaleidoscope Mama")) return "Kaleidoscope Mama: LOCKED (Shop)";
            return "Kaleidoscope Mama: " + (_mamaKal[M] ? "On" : "Off");
        }

        private static void ToggleVoice()
        {
            PlayGlitch();
            if (!Own("Extra Kaleidoscope Voice")) { _voiceKal[M] = false; return; }
            _voiceKal[M] = !_voiceKal[M];
            if (_voiceLabel != null) _voiceLabel.text = VoiceLabel();
        }

        private static void ToggleMama()
        {
            PlayGlitch();
            if (!Own("Extra Kaleidoscope Mama")) { _mamaKal[M] = false; return; }
            _mamaKal[M] = !_mamaKal[M];
            if (_mamaLabel != null) _mamaLabel.text = MamaLabel();
        }

        // ── For the hub's Endless setup screen ───────────────────────────────
        public static string ColorName { get { return OPTS[_colorIdx[M]].label; } }
        /// <summary>The Tattletail color picked for Endless, or for Versus (a custom kid's accents follow it).</summary>
        public static int ColorIndex(bool versus) { return _colorIdx[versus ? 2 : 0]; }
        public static string BeamName { get { return _beamIdx[M] < 0 ? "Default" : FlashlightBeam.Colors[_beamIdx[M]].name; } }
        public static bool VoiceOwned { get { return Own("Extra Kaleidoscope Voice"); } }
        public static bool MamaOwned { get { return Own("Extra Kaleidoscope Mama"); } }
        public static void ToggleVoicePublic() { ToggleVoice(); }
        public static void ToggleMamaPublic() { ToggleMama(); }
        public static void Refresh() { RefreshLabels(); }

        /// <summary>Steps the color forward/back through Original + owned colors.</summary>
        public static void StepColor(int dir)
        {
            PlayGlitch();
            for (int step = 1; step <= OPTS.Length; step++)
            {
                int next = ((_colorIdx[M] + dir * step) % OPTS.Length + OPTS.Length) % OPTS.Length;
                if (ColorOwned(next)) { _colorIdx[M] = next; break; }
            }
            if (_colorLabel != null) _colorLabel.text = ColorLabel(_colorIdx[M]);
        }

        /// <summary>Steps the beam forward/back through Default + owned beams.</summary>
        public static void StepBeam(int dir)
        {
            PlayGlitch();
            int n = FlashlightBeam.Colors.Length + 1; // slot 0 = Default
            int cur = _beamIdx[M] + 1;
            for (int step = 1; step <= n; step++)
            {
                int next = ((cur + dir * step) % n + n) % n;
                if (next == 0 || BeamOwned(next - 1)) { _beamIdx[M] = next - 1; break; }
            }
            if (_beamLabel != null) _beamLabel.text = BeamLabel();
        }

        /// <summary>Re-reads what's owned — called whenever the panel is shown (the Shop may have changed it).</summary>
        private static void RefreshLabels()
        {
            if (!ColorOwned(_colorIdx[M])) _colorIdx[M] = 0;
            if (_beamIdx[M] >= 0 && !BeamOwned(_beamIdx[M])) _beamIdx[M] = -1;
            if (!Own("Extra Kaleidoscope Voice")) _voiceKal[M] = false;
            if (!Own("Extra Kaleidoscope Mama")) _mamaKal[M] = false;

            if (_colorLabel != null) _colorLabel.text = ColorLabel(_colorIdx[M]);
            if (_voiceLabel != null) _voiceLabel.text = VoiceLabel();
            if (_mamaLabel != null) _mamaLabel.text = MamaLabel();
            if (_beamLabel != null) _beamLabel.text = BeamLabel();
        }

        private static void OnBack()
        {
            PlayGlitch();
            Hide();
            if (_party) BirthdaySubmenu.Show(_menu);
            else EndlessSubmenu.Show(_menu);
        }

        /// <summary>party = Papa's 10th Birthday!'s customization (its own picks, its own Party Token shop).</summary>
        public static void Show(MainMenu menu, bool party = false)
        {
            _mode = party ? 1 : 0;
            _menu = menu;
            if (_panel == null) Build(menu);
            if (_panel == null) return;
            _panel.SetActive(true);
            RefreshLabels();
        }

        public static void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }
    }
}
