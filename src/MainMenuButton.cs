using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    // Hide our submenus when vanilla submenus open
    [HarmonyPatch(typeof(MainMenu), "SetOptionsActive")]
    public static class Patch_MainMenu_SetOptionsActive
    {
        static void Postfix() { EndlessSubmenu.Hide(); RunModifiersSubmenu.Hide(); MultiplayerSubmenu.Hide(); ShopSubmenu.Hide(); RunCustomizationSubmenu.Hide(); CustomizationSubmenu.Hide(); StatsSubmenu.Hide(); BirthdaySubmenu.Hide(); ScrapbookSubmenu.Hide(); GadgetSubmenu.Hide(); }
    }

    [HarmonyPatch(typeof(MainMenu), "SetLevelSelectActive")]
    public static class Patch_MainMenu_SetLevelSelectActive
    {
        static void Postfix() { EndlessSubmenu.Hide(); RunModifiersSubmenu.Hide(); MultiplayerSubmenu.Hide(); ShopSubmenu.Hide(); RunCustomizationSubmenu.Hide(); CustomizationSubmenu.Hide(); StatsSubmenu.Hide(); BirthdaySubmenu.Hide(); ScrapbookSubmenu.Hide(); GadgetSubmenu.Hide(); }
    }

    [HarmonyPatch(typeof(MainMenu), "SetWipeDataActive")]
    public static class Patch_MainMenu_SetWipeDataActive
    {
        static void Postfix() { EndlessSubmenu.Hide(); RunModifiersSubmenu.Hide(); MultiplayerSubmenu.Hide(); ShopSubmenu.Hide(); RunCustomizationSubmenu.Hide(); CustomizationSubmenu.Hide(); StatsSubmenu.Hide(); BirthdaySubmenu.Hide(); ScrapbookSubmenu.Hide(); GadgetSubmenu.Hide(); }
    }

    // When main menu reopens, hide submenus
    [HarmonyPatch(typeof(MainMenu), "SetMainMenuActive")]
    public static class Patch_MainMenu_SetMainMenuActive
    {
        static void Postfix(MainMenu __instance)
        {
            EndlessSubmenu.Hide();
            RunModifiersSubmenu.Hide();
            MultiplayerSubmenu.Hide();
            ShopSubmenu.Hide();
            RunCustomizationSubmenu.Hide();
            CustomizationSubmenu.Hide();
            StatsSubmenu.Hide(); BirthdaySubmenu.Hide();
            ScrapbookSubmenu.Hide();
            GadgetSubmenu.Hide();
        }
    }

    // ── ENDLESS button ────────────────────────────────────────────────────────
    // Built by cloning real vanilla menu buttons (Button_Quit for the main
    // button, OptionsPanel/Button_Sensitivity/Button_MotionBlur/Button_Back
    // for the submenu — see [MENU-DIAG] dump) instead of hand-building
    // Image+Text rectangles with a generic font. Cloned buttons are parented
    // directly into the real menu hierarchy (as a child of the real
    // mainMenuHolder, a sibling of the real optionsMenuHolder), so they
    // inherit vanilla's own Canvas/EventSystem/VerticalLayoutGroup/sprite
    // art for free, and get destroyed/rebuilt automatically with the "menu"
    // scene exactly like every other vanilla menu element — no more custom
    // DontDestroyOnLoad root or manual visibility polling needed.
    //
    // Labels are a deliberate exception to "clone the real thing": vanilla's
    // text uses SuperTextMesh, a custom mesh-based renderer with its own
    // character-reveal coroutine system and dynamic-font-atlas event
    // subscriptions. Four separate fix attempts (forcing readDelay=0,
    // calling ShowAllText() to fast-forward the reveal, a per-frame
    // force-show component) all failed to make cloned instances of it
    // render text — something kept resetting it back to an unrevealed
    // state, and even a fully "correct" (reading=false, correct vertex
    // count) state still didn't render anything, meaning it wasn't a
    // reveal-timing bug at all. Rather than keep fighting an unreliable
    // component instance-for-instance, labels use a plain, well-understood
    // UnityEngine.UI.Text component instead — still assigned the game's own
    // real font asset (captured once from a real SuperTextMesh's .font
    // field), so it looks the same, just without SuperTextMesh's fragile
    // internals in the way.
    public static class MainMenuButtonInjector
    {
        public static GameObject ButtonGO { get; private set; }
        public static GameObject BirthdayButtonGO { get; private set; }

        private static Font _realFont;
        private static int _realFontSize = 18;
        public static Font RealFont => _realFont;
        public static int RealFontSize => _realFontSize;

        public static void TryBuild()
        {
            if (ButtonGO != null) return; // still alive from this scene instance
            var menu = Object.FindObjectOfType<MainMenu>();
            if (menu == null) return;

            try
            {
                TryBuildInternal(menu);
            }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: TryBuild failed: " + e.Message + "\n" + e.StackTrace);
                if (ButtonGO != null) { Object.Destroy(ButtonGO); ButtonGO = null; }
            }
        }

        private static void TryBuildInternal(MainMenu menu)
        {
            GameObject quitTemplate = FindChildByName(menu.mainMenuHolder, "Button_Quit");
            if (quitTemplate == null)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: Button_Quit template not found — cannot build ENDLESS button.");
                return;
            }

            // Diagnostic: submenu content reports correct positions but is
            // neither visible nor hoverable/clickable at all — that points
            // at Canvas/GraphicRaycaster inheritance, not layout. Walk up
            // from mainMenuHolder to the scene root logging every ancestor
            // that has a Canvas/CanvasScaler/GraphicRaycaster, so we know
            // for certain whether there's one shared Canvas our sibling
            // panel correctly inherits, or whether each real panel
            // (MainPanel/OptionsHolder/etc) has its own separate Canvas
            // that a same-level sibling wouldn't pick up.
            LogAncestorChain("mainMenuHolder", menu.mainMenuHolder.transform);

            var realStm = quitTemplate.GetComponentInChildren<SuperTextMesh>(true);
            if (realStm != null)
            {
                _realFont = realStm.font;
                if (realStm.size > 0f) _realFontSize = Mathf.RoundToInt(realStm.size);
            }

            ButtonGO = CloneButton(quitTemplate, quitTemplate.transform.parent, "ENDLESS", () =>
            {
                if (menu.mainMenuHolder != null) menu.mainMenuHolder.SetActive(false);
                EndlessSubmenu.Show(menu);
            });
            // Slot it in right above Quit instead of wherever Instantiate happened to append it.
            ButtonGO.transform.SetSiblingIndex(quitTemplate.transform.GetSiblingIndex());

            // Papa's 10th Birthday! — the separate free-roam mode, right under ENDLESS.
            BirthdayButtonGO = CloneButton(quitTemplate, quitTemplate.transform.parent, "Papa's 10th Birthday!", () =>
            {
                if (menu.mainMenuHolder != null) menu.mainMenuHolder.SetActive(false);
                BirthdaySubmenu.Show(menu);
            });
            BirthdayButtonGO.transform.SetSiblingIndex(quitTemplate.transform.GetSiblingIndex());

            // v3.0 arcade HUB — testing only, until it replaces this whole menu.
            var hubBtn = CloneButton(quitTemplate, quitTemplate.transform.parent, "HUB (test)", () =>
            {
                HubMode.Launch(menu);
            });
            hubBtn.transform.SetSiblingIndex(quitTemplate.transform.GetSiblingIndex());

            EndlessSubmenu.Build(menu);
            ShardMenuIcon.Build(menu);

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: UI built!");
        }

        public static void LogAncestorChain(string label, Transform t)
        {
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [CANVAS-DIAG] Ancestor chain from '" + label + "':");
            while (t != null)
            {
                var canvas = t.GetComponent<Canvas>();
                var scaler = t.GetComponent<CanvasScaler>();
                var raycaster = t.GetComponent<GraphicRaycaster>();
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [CANVAS-DIAG]   " + t.gameObject.name +
                    " layer=" + t.gameObject.layer +
                    " Canvas=" + (canvas != null ? ("renderMode=" + canvas.renderMode + " sortingOrder=" + canvas.sortingOrder + " worldCamera=" + (canvas.worldCamera != null ? canvas.worldCamera.name : "null") + " overrideSorting=" + canvas.overrideSorting) : "none") +
                    " CanvasScaler=" + (scaler != null ? "yes" : "none") +
                    " GraphicRaycaster=" + (raycaster != null ? "yes" : "none"));
                t = t.parent;
            }
        }

        public static GameObject FindChildByName(GameObject root, string name)
        {
            if (root == null) return null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.gameObject.name == name) return t.gameObject;
            return null;
        }

        /// <summary>Clones a real vanilla button (Button + Image + child text object), strips its old click behavior, wires a new one and a new label.</summary>
        public static GameObject CloneButton(GameObject template, Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            if (template == null) return null;
            GameObject clone = Object.Instantiate(template, parent, false);
            clone.name = "KE_Btn_" + label;
            clone.SetActive(true);

            // The cloned Button's onClick carries over whatever persistent
            // (Inspector-wired) call the source vanilla button had — e.g.
            // Button_Quit calls MainMenu.OnPressButtonQuit(). UnityEvent.
            // RemoveAllListeners() only clears runtime listeners, NOT
            // persistent ones (UnityEventTools, which can strip those, is
            // Editor-only and unavailable in a build) — so leaving the old
            // Button component in place would fire BOTH the vanilla action
            // and ours (imagine clicking "ENDLESS" and it also quits the
            // game). Replace the whole component instead: capture its
            // visual settings, destroy it, add a brand new one with an
            // empty event.
            var oldBtn = clone.GetComponent<Button>();
            ColorBlock colors = oldBtn != null ? oldBtn.colors : ColorBlock.defaultColorBlock;
            Selectable.Transition transition = oldBtn != null ? oldBtn.transition : Selectable.Transition.ColorTint;
            if (oldBtn != null) Object.DestroyImmediate(oldBtn);

            var btn = clone.AddComponent<Button>();
            btn.transition = transition;
            btn.colors = colors;
            btn.targetGraphic = clone.GetComponent<Image>();
            if (onClick != null) btn.onClick.AddListener(onClick);

            SetLabel(clone, label);
            return clone;
        }

        /// <summary>Clones a real vanilla plain text object (no Button) — used for the submenu title.</summary>
        public static GameObject CloneLabel(GameObject template, Transform parent, string text)
        {
            if (template == null) return null;
            GameObject clone = Object.Instantiate(template, parent, false);
            clone.name = "KE_Label";
            clone.SetActive(true);
            SetLabel(clone, text);

            // Our only current use is the submenu title, cloned from
            // "OptimizeText" — a small unrelated footnote text box
            // (100x100), nowhere near wide enough for a title sitting above
            // full-width (226-wide) buttons. Force a sane size instead of
            // inheriting the footnote's own tiny one.
            var rt = clone.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(226f, 40f);

            return clone;
        }

        /// <summary>
        /// Finds the cloned label's SuperTextMesh child and replaces it with
        /// a plain UI Text using the real vanilla font — see the class
        /// comment for why SuperTextMesh itself isn't used directly.
        /// Returns the Text component so callers can update it later
        /// (color/voice/mama toggles, color-cycle button).
        /// </summary>
        public static Text SetLabel(GameObject clone, string text)
        {
            var stm = clone.GetComponentInChildren<SuperTextMesh>(true);
            GameObject textGO = stm != null ? stm.gameObject : null;
            if (textGO == null) return null;

            Object.DestroyImmediate(stm);
            var mf = textGO.GetComponent<MeshFilter>();
            if (mf != null) Object.DestroyImmediate(mf);
            var mr = textGO.GetComponent<MeshRenderer>();
            if (mr != null) Object.DestroyImmediate(mr);

            textGO.SetActive(true);
            var uiText = textGO.AddComponent<Text>();
            uiText.font = _realFont != null ? _realFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
            uiText.fontSize = _realFontSize;
            uiText.color = Color.white;
            uiText.alignment = TextAnchor.MiddleLeft;
            uiText.horizontalOverflow = HorizontalWrapMode.Overflow;
            uiText.verticalOverflow = VerticalWrapMode.Overflow;
            uiText.text = text;
            return uiText;
        }

        /// <summary>
        /// Anchors a child to its parent's top edge, stretched to full
        /// width, at a given Y offset and height — deterministic top-down
        /// stacking, independent of VerticalLayoutGroup (which was proven
        /// unreliable for reused/resized panels in this codebase — see
        /// EndlessSubmenu.Build()'s comments for the full story).
        /// </summary>
        public static void PositionChild(GameObject child, float y, float height)
        {
            if (child == null) return;
            var rt = child.GetComponent<RectTransform>();
            if (rt == null) return;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(0f, height);
        }

    }

    // ── Endless submenu — shown instead of mainMenuHolder ────────────────────
    // Built by cloning OptionsPanel (real background + VerticalLayoutGroup)
    // as the panel shell, then Button_Sensitivity (a "press to cycle a
    // value" button — reused for the color picker) and Button_MotionBlur
    // (a "press to toggle On/Off" button — reused for the two voice/Mama
    // toggles), same real widgets the vanilla options menu already uses for
    // an identical interaction pattern, instead of a custom swatch grid and
    // hand-drawn checkboxes.
    public static class EndlessSubmenu
    {
        private static GameObject _panel;
        private static MainMenu _menu;
        private static int _difficultyIndex = 1; // default Normal

        private struct DifficultyOpt
        {
            public string label; public float mult;
            public DifficultyOpt(string l, float m) { label = l; mult = m; }
        }
        // mult feeds straight into EndlessRunManager's existing
        // _difficultyMult — the same knob that scales Mama's alert timer/
        // patrol speed up and flashlight-survival time down — so this is a
        // real difficulty change (fixed for the whole run), not cosmetic.
        private static readonly DifficultyOpt[] DIFFICULTIES =
        {
            new DifficultyOpt("Easy",       0.7f),
            new DifficultyOpt("Normal",     1.0f),
            new DifficultyOpt("Hard",       1.4f),
            new DifficultyOpt("Impossible", 2.0f),
        };

        private static Text _difficultyLabel;
        private static Text _gadgetLabel;

        private static string GadgetRowLabel() { return "Gadget: " + Gadgets.Name(Gadgets.Selected); }

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
                KaleidoscopeEndlessPlugin.Log.LogError("KE: EndlessSubmenu.Build — OptionsPanel template not found, cannot build submenu.");
                return;
            }

            _panel = Object.Instantiate(optionsPanelTemplate, menu.mainMenuHolder.transform.parent, false);
            _panel.name = "KE_SubmenuPanel";
            // DestroyImmediate, not Destroy — see StatsSubmenu.Build()'s
            // comment: Destroy() defers to end of frame, and a lazily-
            // built panel's Build()+first Show() can land in that same
            // frame, leaving the real vanilla OptionsPanel buttons (still
            // interactable) sitting under our own rows in the meantime.
            for (int i = _panel.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(_panel.transform.GetChild(i).gameObject);

            // OptionsPanel's own serialized anchoredPosition was meant to
            // be read relative to ITS real parent, OptionsHolder — a
            // wrapper we deliberately skip (we only need OptionsPanel's
            // background+layout, not the WipeData/OptimizeText siblings
            // bundled inside OptionsHolder). Cloning straight onto the
            // Canvas root keeps that stale offset, but now with nothing to
            // compensate for it — [CANVAS-DIAG]/[SUBMENU-DIAG] showed our
            // panel landing ~350 units away from MainPanel's own position
            // in world space (X -8.4..-5.7 vs MainPanel's -4.1..-1.3),
            // almost certainly outside the ScreenSpaceCamera Canvas's
            // visible frustum — invisible AND unraycastable for the same
            // reason. Just take over MainPanel's own anchor/pivot/position
            // instead, so our panel sits exactly where the button list it's
            // replacing was.
            var mainPanelRT = menu.mainMenuHolder.GetComponent<RectTransform>();
            var panelRT = _panel.GetComponent<RectTransform>();
            if (mainPanelRT != null && panelRT != null)
            {
                panelRT.anchorMin = mainPanelRT.anchorMin;
                panelRT.anchorMax = mainPanelRT.anchorMax;
                panelRT.pivot = mainPanelRT.pivot;
                // 8 rows (Customization/Difficulty/Run Modifiers/Gadget/
                // Stats/Shop/Enter/Back).
                panelRT.anchoredPosition = mainPanelRT.anchoredPosition + new Vector2(0f, 60f);
            }

            MainMenuButtonInjector.LogAncestorChain("KE_SubmenuPanel", _panel.transform);

            // No hover/click at all on submenu buttons despite correct
            // positions strongly suggests a CanvasGroup somewhere in this
            // subtree with alpha=0/interactable=false/blocksRaycasts=false
            // — vanilla's OptionsPanel may use one for a fade-in effect
            // that only some OTHER script (which we never call) turns back
            // on. Check the panel itself and every ancestor up to the root.
            for (Transform t = _panel.transform; t != null; t = t.parent)
            {
                var cg = t.GetComponent<CanvasGroup>();
                if (cg != null)
                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [CANVASGROUP-DIAG] '" + t.gameObject.name + "' alpha=" + cg.alpha +
                        " interactable=" + cg.interactable + " blocksRaycasts=" + cg.blocksRaycasts + " ignoreParentGroups=" + cg.ignoreParentGroups);
            }

            // VerticalLayoutGroup turned out unreliable for this reused
            // panel: first pass, children never got laid out at all
            // (deactivating the panel right after building it meant its
            // deferred layout pass never ran); after forcing that rebuild,
            // children WERE evenly spaced relative to each other but the
            // whole stack landed entirely outside the panel's own bounds
            // ([SUBMENU-DIAG]'s GetWorldCorners() showed every child's Y
            // range below the panel's own Y range — not a rendering bug,
            // an actual wrong-position bug). Rather than keep fighting
            // whatever padding/anchor assumption is mismatched for this
            // reused panel, disable the LayoutGroup entirely and position
            // every child ourselves — simple, deterministic top-down
            // stacking, same self-contained-over-fragile-vanilla-system
            // approach already used throughout this mod.
            var vlg = _panel.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) vlg.enabled = false;

            float y = -10f; // small top padding
            const float buttonHeight = 33f;
            const float titleHeight = 40f;
            const float spacing = 6f;

            if (titleTemplate != null)
            {
                GameObject title = MainMenuButtonInjector.CloneLabel(titleTemplate, _panel.transform, "ENDLESS MODE");
                MainMenuButtonInjector.PositionChild(title, y, titleHeight);
                y -= titleHeight + spacing;
            }

            GameObject customizationBtn = MainMenuButtonInjector.CloneButton(cycleTemplate, _panel.transform,
                "Customization", OnCustomization);
            MainMenuButtonInjector.PositionChild(customizationBtn, y, buttonHeight);
            y -= buttonHeight + spacing;

            GameObject difficultyBtn = MainMenuButtonInjector.CloneButton(cycleTemplate, _panel.transform,
                "Difficulty: " + DIFFICULTIES[_difficultyIndex].label, CycleDifficulty);
            MainMenuButtonInjector.PositionChild(difficultyBtn, y, buttonHeight);
            y -= buttonHeight + spacing;
            _difficultyLabel = difficultyBtn?.GetComponentInChildren<Text>(true);

            GameObject modifiersBtn = MainMenuButtonInjector.CloneButton(cycleTemplate, _panel.transform,
                "Run Modifiers", OnRunModifiers);
            MainMenuButtonInjector.PositionChild(modifiersBtn, y, buttonHeight);
            y -= buttonHeight + spacing;

            GameObject gadgetBtn = MainMenuButtonInjector.CloneButton(cycleTemplate, _panel.transform,
                GadgetRowLabel(), OnGadget);
            MainMenuButtonInjector.PositionChild(gadgetBtn, y, buttonHeight);
            y -= buttonHeight + spacing;
            _gadgetLabel = gadgetBtn?.GetComponentInChildren<Text>(true);

            GameObject statsBtn = MainMenuButtonInjector.CloneButton(cycleTemplate, _panel.transform,
                "Stats", OnStats);
            MainMenuButtonInjector.PositionChild(statsBtn, y, buttonHeight);
            y -= buttonHeight + spacing;

            GameObject shopBtn = MainMenuButtonInjector.CloneButton(cycleTemplate, _panel.transform,
                "Shop", OnShop);
            MainMenuButtonInjector.PositionChild(shopBtn, y, buttonHeight);
            y -= buttonHeight + spacing;

            // Mama's Scrapbook — switched off for v3.0; uncomment this and OnScrapbook() to bring it back.
            // GameObject scrapbookBtn = MainMenuButtonInjector.CloneButton(cycleTemplate, _panel.transform,
            //     "Mama's Scrapbook", OnScrapbook);
            // MainMenuButtonInjector.PositionChild(scrapbookBtn, y, buttonHeight);
            // y -= buttonHeight + spacing;

            if (enterTemplate != null)
            {
                GameObject enterBtn = MainMenuButtonInjector.CloneButton(enterTemplate, _panel.transform, "Enter the Kaleidoscope", OnEnter);
                MainMenuButtonInjector.PositionChild(enterBtn, y, buttonHeight);
                y -= buttonHeight + spacing;
            }

            if (backTemplate != null)
            {
                GameObject backBtn = MainMenuButtonInjector.CloneButton(backTemplate, _panel.transform, "Back", OnBack);
                MainMenuButtonInjector.PositionChild(backBtn, y, buttonHeight);
                y -= buttonHeight + spacing;
            }

            // Resize the panel itself to fit exactly what we just stacked
            // into it instead of trusting whatever height the original
            // (differently-sized) OptionsPanel happened to have.
            panelRT.sizeDelta = new Vector2(panelRT.sizeDelta.x, -y + 10f);

            _panel.SetActive(false);
        }

        private static void PlayGlitch()
        {
            if (Camera.main != null) AudioController.Play("vhs_glitch", Camera.main.transform);
        }

        public static string DifficultyName { get { return DIFFICULTIES[_difficultyIndex].label; } }

        public static void StepDifficulty(int dir)
        {
            PlayGlitch();
            _difficultyIndex = ((_difficultyIndex + dir) % DIFFICULTIES.Length + DIFFICULTIES.Length) % DIFFICULTIES.Length;
            if (_difficultyLabel != null) _difficultyLabel.text = "Difficulty: " + DIFFICULTIES[_difficultyIndex].label;
        }

        private static void CycleDifficulty()
        {
            PlayGlitch();
            _difficultyIndex = (_difficultyIndex + 1) % DIFFICULTIES.Length;
            if (_difficultyLabel != null)
                _difficultyLabel.text = "Difficulty: " + DIFFICULTIES[_difficultyIndex].label;
        }

        private static void OnCustomization()
        {
            if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            Hide();
            CustomizationSubmenu.Show(_menu);
        }

        private static void OnRunModifiers()
        {
            if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            Hide();
            RunModifiersSubmenu.Show(_menu);
        }

        private static void OnStats()
        {
            if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            Hide();
            StatsSubmenu.Show(_menu);
        }

        private static void OnShop()
        {
            if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            Hide();
            ShopSubmenu.Show(_menu);
        }

        private static void OnGadget()
        {
            if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            Hide();
            GadgetSubmenu.Show(_menu);
        }

        // private static void OnScrapbook()
        // {
        //     if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
        //     Hide();
        //     ScrapbookSubmenu.Show(_menu);
        // }

        /// <summary>Cosmetic picks (color/voice/Mama/Diamond Flashlight) are always this player's own local choice, solo or multiplayer alike — see CustomizationSubmenu.cs.</summary>
        private static void ApplyLocalCosmeticPending()
        {
            if (VersusMode.IsSurvivor) CustomizationSubmenu.SetVersusMode(); // Versus picks (VERSUS SHOP)
            else CustomizationSubmenu.SetMode(false); // Endless picks (shard shop)
            EndlessRunManager.PendingRecolor = CustomizationSubmenu.ColorRecolor;
            EndlessRunManager.PendingGoldColor = CustomizationSubmenu.IsGoldColor;
            EndlessRunManager.PendingDiamondColor = CustomizationSubmenu.IsDiamondColor;
            EndlessRunManager.PendingKaleidoscopeVoice = CustomizationSubmenu.KaleidoscopeVoice;
            EndlessRunManager.PendingKaleidoscopeMama = CustomizationSubmenu.KaleidoscopeMama;
            EndlessRunManager.PendingBirthdayMode = false; // any Endless launch is not the birthday mode
            EndlessTitle.RollNight(); // picks the night's title; 1% chance to flip Endless <-> Kaleidoscope Night and their skins
            // Single-run Shop power ups: read what's owned and use it up (Shop.txt back to 0) as the run launches.
            RunPowerUps.Begin();
            EndlessRunManager.PendingModDiamondFlashlight = RunPowerUps.DiamondFlashlight;
            EndlessRunManager.PendingBeamColor = CustomizationSubmenu.BeamColorName;
            Gadgets.RunGadget = Gadgets.SelectedIfOwned; // never a locked gadget
        }

        /// <summary>
        /// Launches Papa's 10th Birthday!: same level and setup as Endless,
        /// but no difficulty, modifiers, shop power ups, Lucky/Kaleidoscope
        /// night rolls or objectives. Only the player's own cosmetic picks
        /// (color, voice, Mama skin, beam color) carry over.
        /// </summary>
        public static void StartBirthdayMode(MainMenu menu)
        {
            _menu = menu;
            if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);

            CustomizationSubmenu.SetMode(true); // birthday picks (Party Token shop)
            EndlessRunManager.PendingRecolor = CustomizationSubmenu.ColorRecolor;
            EndlessRunManager.PendingGoldColor = CustomizationSubmenu.IsGoldColor;
            EndlessRunManager.PendingDiamondColor = CustomizationSubmenu.IsDiamondColor;
            EndlessRunManager.PendingKaleidoscopeVoice = CustomizationSubmenu.KaleidoscopeVoice;
            EndlessRunManager.PendingKaleidoscopeMama = CustomizationSubmenu.KaleidoscopeMama;
            EndlessRunManager.PendingBeamColor = CustomizationSubmenu.BeamColorName;

            RunPowerUps.Clear();
            Gadgets.RunGadget = GadgetType.None; // gadgets are Endless-only
            EndlessRunManager.PendingModDiamondFlashlight = false;
            EndlessRunManager.PendingDifficultyMult = 1f;
            EndlessRunManager.PendingDifficultyLabel = "Normal";
            EndlessRunManager.PendingModLowBattery = false;
            EndlessRunManager.PendingModNoRest = false;
            EndlessRunManager.PendingModMaxMamaAggro = false;
            EndlessRunManager.PendingModBatteriesNotIncluded = false;
            EndlessRunManager.PendingModAlwaysHungry = false;
            EndlessRunManager.PendingModBrushingIsKey = false;
            EndlessRunManager.PendingModPowerUps = false;

            EndlessTitle.PendingLucky = false;
            EndlessTitle.PendingTitle = EndlessTitle.BIRTHDAY;
            EndlessRunManager.PendingBirthdayMode = true;
            EndlessRunManager.PendingEndlessRun = true;

            Hide();
            if (menu != null && menu.mainMenuHolder != null)
                menu.mainMenuHolder.SetActive(true);

            GameManager.PlayLevel(LevelEnum._6_TheParty);
        }

        /// <summary>Difficulty + the 6 remaining Run Modifiers are host-authoritative/shared in multiplayer (RunCustomizationSubmenu/RunModifiersSubmenu, already synced), unlike solo where this submenu's own local picks are used.</summary>
        private static void ApplySharedRunSettingsPending()
        {
            EndlessRunManager.PendingDifficultyMult = RunCustomizationSubmenu.DifficultyMult;
            EndlessRunManager.PendingDifficultyLabel = RunCustomizationSubmenu.DifficultyLabel;
            bool[] mods = RunModifiersSubmenu.GetValues();
            EndlessRunManager.PendingModLowBattery = mods[0];
            EndlessRunManager.PendingModNoRest = mods[1];
            EndlessRunManager.PendingModMaxMamaAggro = mods[2];
            EndlessRunManager.PendingModBatteriesNotIncluded = mods[3];
            EndlessRunManager.PendingModAlwaysHungry = mods[4];
            EndlessRunManager.PendingModBrushingIsKey = mods[5];
            EndlessRunManager.PendingModPowerUps = mods[6];
        }

        /// <summary>Host clicks "Start Game" in MultiplayerSubmenu — broadcasts LevelTransition so the guest follows, then loads the level locally.</summary>
        public static void HostEnter(MainMenu menu)
        {
            _menu = menu;
            if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            ApplyLocalCosmeticPending();
            ApplySharedRunSettingsPending();
            EndlessRunManager.PendingEndlessRun = true;

            var lobby = MpLobby.Instance;
            if (lobby != null && lobby.HasPeer)
            {
                using (var packet = new MpPacket(MpNetworking.PacketId.LevelTransition))
                    MpNetworking.SendReliable(lobby.PeerSteamID, packet);
            }

            MultiplayerSubmenu.Hide();
            if (menu != null && menu.mainMenuHolder != null)
                menu.mainMenuHolder.SetActive(true);

            GameManager.PlayLevel(LevelEnum._6_TheParty);
        }

        /// <summary>Guest received the host's LevelTransition packet — follow into the level with the synced shared settings + this player's own local cosmetic picks.</summary>
        public static void OnHostStartedLevel()
        {
            ApplyLocalCosmeticPending();
            ApplySharedRunSettingsPending();
            EndlessRunManager.PendingEndlessRun = true;

            MultiplayerSubmenu.Hide();
            RunCustomizationSubmenu.Hide();
            if (_menu != null && _menu.mainMenuHolder != null)
                _menu.mainMenuHolder.SetActive(true);

            GameManager.PlayLevel(LevelEnum._6_TheParty);
        }

        private static void OnEnter()
        {
            Hide();
            LaunchEndless();
        }

        /// <summary>Starts an Endless run with the current menu picks — used by the menu's Enter button and the hub's Endless machine.</summary>
        public static void LaunchEndless() { LaunchEndless(false); }

        /// <summary>facility: the Waygetter Facility instead of the house (dev builds): the same run, the same settings.</summary>
        public static void LaunchEndless(bool facility)
        {
            if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            ApplyLocalCosmeticPending();
            EndlessRunManager.PendingDifficultyMult = DIFFICULTIES[_difficultyIndex].mult;
            EndlessRunManager.PendingDifficultyLabel = DIFFICULTIES[_difficultyIndex].label;
            EndlessRunManager.PendingModLowBattery = RunModifiersSubmenu.ModLowBattery;
            EndlessRunManager.PendingModNoRest = RunModifiersSubmenu.ModNoRest;
            EndlessRunManager.PendingModMaxMamaAggro = RunModifiersSubmenu.ModMaxMamaAggro;
            EndlessRunManager.PendingModBatteriesNotIncluded = RunModifiersSubmenu.ModBatteriesNotIncluded;
            EndlessRunManager.PendingModAlwaysHungry = RunModifiersSubmenu.ModAlwaysHungry;
            EndlessRunManager.PendingModBrushingIsKey = RunModifiersSubmenu.ModBrushingIsKey;
            EndlessRunManager.PendingModPowerUps = RunModifiersSubmenu.ModPowerUps;
            EndlessRunManager.PendingEndlessRun = true;
            Hide();
            if (_menu != null && _menu.mainMenuHolder != null)
                _menu.mainMenuHolder.SetActive(true);

            if (facility) { FactoryMode.LaunchEndlessPreview(); return; }
            GameManager.PlayLevel(LevelEnum._6_TheParty);
        }

        private static void OnBack()
        {
            if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            Hide();
            if (_menu != null && _menu.mainMenuHolder != null)
                _menu.mainMenuHolder.SetActive(true);
        }

        public static void Show(MainMenu menu)
        {
            _menu = menu;
            if (_panel == null) Build(menu);
            if (_panel == null) return;
            _panel.SetActive(true);
            if (_gadgetLabel != null) _gadgetLabel.text = GadgetRowLabel();
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Submenu shown");

            // Diagnostic: user reported the submenu "shows nothing" — dump
            // exactly what's actually under the panel (child count, active
            // state, RectTransform size/position, whether each has an
            // Image/Text) instead of guessing again.
            var panelRT = _panel.GetComponent<RectTransform>();
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [SUBMENU-DIAG] panel active=" + _panel.activeInHierarchy +
                " childCount=" + _panel.transform.childCount +
                " rectSize=" + panelRT?.rect.size +
                " anchorMin=" + panelRT?.anchorMin + " anchorMax=" + panelRT?.anchorMax +
                " pivot=" + panelRT?.pivot + " anchoredPos=" + panelRT?.anchoredPosition);
            LogWorldCorners("panel", panelRT);

            // Known-good reference for comparison — MainPanel definitely
            // renders correctly on screen.
            var mainPanelRT = _menu.mainMenuHolder != null ? _menu.mainMenuHolder.GetComponent<RectTransform>() : null;
            if (mainPanelRT != null)
            {
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [SUBMENU-DIAG] MainPanel (known-good reference) anchorMin=" + mainPanelRT.anchorMin +
                    " anchorMax=" + mainPanelRT.anchorMax + " pivot=" + mainPanelRT.pivot + " anchoredPos=" + mainPanelRT.anchoredPosition);
                LogWorldCorners("MainPanel", mainPanelRT);
            }

            foreach (Transform child in _panel.transform)
            {
                var img = child.GetComponent<Image>();
                var txt = child.GetComponentInChildren<Text>(true);
                var childRT = child.GetComponent<RectTransform>();
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [SUBMENU-DIAG]   " + child.gameObject.name +
                    " active=" + child.gameObject.activeInHierarchy +
                    " pos=" + childRT?.anchoredPosition +
                    " size=" + childRT?.rect.size +
                    " image=" + (img != null ? ("sprite=" + (img.sprite != null ? img.sprite.name : "null") + " color=" + img.color) : "none") +
                    " text=" + (txt != null ? ("'" + txt.text + "' active=" + txt.gameObject.activeInHierarchy + " color=" + txt.color) : "none"));
                LogWorldCorners("  " + child.gameObject.name, childRT);
            }
        }

        private static void LogWorldCorners(string label, RectTransform rt)
        {
            if (rt == null) return;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [SUBMENU-DIAG]   " + label + " worldCorners bottomLeft=" + corners[0] + " topRight=" + corners[2] +
                " screen=" + Screen.width + "x" + Screen.height);
        }

        public static void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }
    }
}
