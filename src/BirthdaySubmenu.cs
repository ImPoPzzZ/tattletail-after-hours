using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// The menu for Papa's 10th Birthday!: Customization (its own picks),
    /// the Party Shop (Party Tokens), and the button that starts the mode.
    /// Same clone-real-widgets recipe as the other submenus.
    /// </summary>
    public static class BirthdaySubmenu
    {
        private static GameObject _panel;
        private static MainMenu _menu;

        public static bool IsShowing { get { return _panel != null && _panel.activeSelf; } }

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
                KaleidoscopeEndlessPlugin.Log.LogError("KE: BirthdaySubmenu.Build — OptionsPanel template not found, cannot build.");
                return;
            }

            _panel = Object.Instantiate(optionsPanelTemplate, menu.mainMenuHolder.transform.parent, false);
            _panel.name = "KE_BirthdayPanel";
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
            const float rowHeight = 33f;
            const float titleHeight = 40f;
            const float spacing = 6f;

            if (titleTemplate != null)
            {
                GameObject title = MainMenuButtonInjector.CloneLabel(titleTemplate, _panel.transform, "PAPA'S 10TH BIRTHDAY!");
                MainMenuButtonInjector.PositionChild(title, y, titleHeight);
                y -= titleHeight + spacing + 30f;
            }

            GameObject customizationBtn = MainMenuButtonInjector.CloneButton(cycleTemplate, _panel.transform, "Customization", OnCustomization);
            MainMenuButtonInjector.PositionChild(customizationBtn, y, rowHeight);
            y -= rowHeight + spacing;

            GameObject shopBtn = MainMenuButtonInjector.CloneButton(cycleTemplate, _panel.transform, "Party Shop", OnShop);
            MainMenuButtonInjector.PositionChild(shopBtn, y, rowHeight);
            y -= rowHeight + spacing;

            if (enterTemplate != null)
            {
                GameObject enterBtn = MainMenuButtonInjector.CloneButton(enterTemplate, _panel.transform, "Enter the Party!", OnEnter);
                MainMenuButtonInjector.PositionChild(enterBtn, y, rowHeight);
                y -= rowHeight + spacing;
            }

            if (backTemplate != null)
            {
                GameObject backBtn = MainMenuButtonInjector.CloneButton(backTemplate, _panel.transform, "Back", OnBack);
                MainMenuButtonInjector.PositionChild(backBtn, y, rowHeight);
                y -= rowHeight + spacing;
            }

            panelRT.sizeDelta = new Vector2(panelRT.sizeDelta.x, -y + 10f);
            _panel.SetActive(false);
        }

        private static void OnCustomization()
        {
            if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            Hide();
            CustomizationSubmenu.Show(_menu, true);
        }

        private static void OnShop()
        {
            if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            Hide();
            ShopSubmenu.ShowParty(_menu);
        }

        private static void OnEnter()
        {
            EndlessSubmenu.StartBirthdayMode(_menu);
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
            _panel.transform.SetAsLastSibling();
        }

        public static void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }
    }
}
