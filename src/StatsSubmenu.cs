using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Persistent, menu-accessed stats panel — replaces the old auto-popup
    /// StatsUI death screen entirely. Just 4 short all-time facts (see
    /// EndlessStats.cs for persistence), so unlike StatsUI's original
    /// paragraph-of-text problem (illegible against the cloned vanilla
    /// panel background), this fits the standard short-label submenu recipe
    /// fine — same clone-real-widgets/manual-PositionChild pattern as every
    /// other submenu here.
    /// </summary>
    public static class StatsSubmenu
    {
        private static GameObject _panel;
        private static MainMenu _menu;

        private static Text _lastTimeLabel;
        private static Text _longestTimeLabel;
        private static Text _objectivesLabel;
        private static Text _deathsLabel;
        private static Text _survivedLabel;

        public static void Build(MainMenu menu)
        {
            _menu = menu;

            GameObject optionsPanelTemplate = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "OptionsPanel");
            GameObject cycleTemplate        = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "Button_Sensitivity");
            GameObject backTemplate         = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "Button_Back");
            GameObject titleTemplate        = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "OptimizeText");

            if (optionsPanelTemplate == null)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: StatsSubmenu.Build — OptionsPanel template not found, cannot build.");
                return;
            }

            _panel = Object.Instantiate(optionsPanelTemplate, menu.mainMenuHolder.transform.parent, false);
            _panel.name = "KE_StatsPanel";
            // DestroyImmediate, not Destroy — Destroy() defers to end of
            // frame, and Build()+the first Show() (which populates/shows
            // the panel) can happen in that same frame on first click,
            // leaving the real vanilla OptionsPanel buttons (still fully
            // interactable) sitting under our own rows in the meantime.
            // Iterate backward by index, not foreach — DestroyImmediate
            // mutates the live child list, which a foreach can't handle.
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
                GameObject title = MainMenuButtonInjector.CloneLabel(titleTemplate, _panel.transform, "STATS");
                MainMenuButtonInjector.PositionChild(title, y, titleHeight);
                y -= titleHeight + spacing + 30f; // same title-glyph-overlap clearance every other submenu needs
            }

            // CloneButton, not CloneLabel — these aren't clickable (no
            // onClick listener gets attached), but reusing the real button
            // widget gives them the same hover/highlight feedback every
            // actual button on this panel has, instead of looking dead.
            GameObject lastTimeRow = MainMenuButtonInjector.CloneButton(cycleTemplate, _panel.transform, "", null);
            MainMenuButtonInjector.PositionChild(lastTimeRow, y, rowHeight);
            y -= rowHeight + spacing;
            _lastTimeLabel = lastTimeRow?.GetComponentInChildren<Text>(true);

            GameObject longestTimeRow = MainMenuButtonInjector.CloneButton(cycleTemplate, _panel.transform, "", null);
            MainMenuButtonInjector.PositionChild(longestTimeRow, y, rowHeight);
            y -= rowHeight + spacing;
            _longestTimeLabel = longestTimeRow?.GetComponentInChildren<Text>(true);

            GameObject objectivesRow = MainMenuButtonInjector.CloneButton(cycleTemplate, _panel.transform, "", null);
            MainMenuButtonInjector.PositionChild(objectivesRow, y, rowHeight);
            y -= rowHeight + spacing;
            _objectivesLabel = objectivesRow?.GetComponentInChildren<Text>(true);

            GameObject deathsRow = MainMenuButtonInjector.CloneButton(cycleTemplate, _panel.transform, "", null);
            MainMenuButtonInjector.PositionChild(deathsRow, y, rowHeight);
            y -= rowHeight + spacing;
            _deathsLabel = deathsRow?.GetComponentInChildren<Text>(true);

            GameObject survivedRow = MainMenuButtonInjector.CloneButton(cycleTemplate, _panel.transform, "", null);
            MainMenuButtonInjector.PositionChild(survivedRow, y, rowHeight);
            y -= rowHeight + spacing;
            _survivedLabel = survivedRow?.GetComponentInChildren<Text>(true);

            if (backTemplate != null)
            {
                GameObject backBtn = MainMenuButtonInjector.CloneButton(backTemplate, _panel.transform, "Back", OnBack);
                MainMenuButtonInjector.PositionChild(backBtn, y, rowHeight);
                y -= rowHeight + spacing;
            }

            panelRT.sizeDelta = new Vector2(panelRT.sizeDelta.x, -y + 10f);

            _panel.SetActive(false);
        }

        private static string FormatTime(int seconds)
        {
            int mins = seconds / 60;
            int secs = seconds % 60;
            return string.Format("{0:D2}:{1:D2}", mins, secs);
        }

        public static void Refresh()
        {
            if (_panel == null || !_panel.activeSelf) return;
            if (_lastTimeLabel != null) _lastTimeLabel.text = "Last Survival Time: " + FormatTime(EndlessStats.LastSurvivalTime);
            if (_longestTimeLabel != null) _longestTimeLabel.text = "Longest Survival Time: " + FormatTime(EndlessStats.LongestSurvivalTime);
            if (_objectivesLabel != null) _objectivesLabel.text = "Total Objectives Completed: " + EndlessStats.TotalObjectivesCompleted;
            if (_deathsLabel != null) _deathsLabel.text = "Total Deaths: " + EndlessStats.TotalDeaths;
            if (_survivedLabel != null) _survivedLabel.text = "Survived: " + EndlessStats.TotalSurvived;
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
            // Guarantee this panel paints (and raycast-hit-tests) in front
            // of any other still-active submenu sibling under the same
            // parent, in case a Hide() elsewhere didn't fully take.
            _panel.transform.SetAsLastSibling();
            Refresh();
        }

        public static void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }
    }
}
