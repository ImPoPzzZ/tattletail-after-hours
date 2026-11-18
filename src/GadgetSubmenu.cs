using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// "Choose your gadget" — picked with the mouse from the Endless menu
    /// before a run. Hovering a gadget explains what it does; clicking
    /// equips it. Same clone-real-widgets recipe as the other submenus.
    /// </summary>
    public static class GadgetSubmenu
    {
        private static GameObject _panel;
        private static MainMenu _menu;
        private static Text _caption;
        private static readonly List<Text> _rowLabels = new List<Text>();

        private static void Build(MainMenu menu)
        {
            _menu = menu;

            GameObject optionsPanelTemplate = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "OptionsPanel");
            GameObject cycleTemplate        = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "Button_Sensitivity");
            GameObject backTemplate         = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "Button_Back");
            GameObject titleTemplate        = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "OptimizeText");
            if (optionsPanelTemplate == null)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: GadgetSubmenu.Build — OptionsPanel template not found.");
                return;
            }

            _panel = Object.Instantiate(optionsPanelTemplate, menu.mainMenuHolder.transform.parent, false);
            _panel.name = "KE_GadgetPanel";
            for (int i = _panel.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(_panel.transform.GetChild(i).gameObject);

            var mainPanelRT = menu.mainMenuHolder.GetComponent<RectTransform>();
            var panelRT = _panel.GetComponent<RectTransform>();
            if (mainPanelRT != null && panelRT != null)
            {
                panelRT.anchorMin = mainPanelRT.anchorMin;
                panelRT.anchorMax = mainPanelRT.anchorMax;
                panelRT.pivot = mainPanelRT.pivot;
                panelRT.anchoredPosition = mainPanelRT.anchoredPosition + new Vector2(0f, 60f);
                panelRT.sizeDelta = new Vector2(300f, panelRT.sizeDelta.y); // room for the descriptions
            }

            var vlg = _panel.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) vlg.enabled = false;

            float y = -10f;
            const float rowHeight = 33f;
            const float titleHeight = 40f;
            const float spacing = 6f;
            const float captionHeight = 150f;

            if (titleTemplate != null)
            {
                GameObject title = MainMenuButtonInjector.CloneLabel(titleTemplate, _panel.transform, "CHOOSE YOUR GADGET");
                MainMenuButtonInjector.PositionChild(title, y, titleHeight);
                y -= titleHeight + spacing;
            }

            _rowLabels.Clear();
            foreach (var g in Gadgets.All)
            {
                GadgetType captured = g;
                GameObject row = MainMenuButtonInjector.CloneButton(cycleTemplate, _panel.transform, RowLabel(g), () => OnPick(captured));
                MainMenuButtonInjector.PositionChild(row, y, rowHeight);
                y -= rowHeight + spacing;
                _rowLabels.Add(row != null ? row.GetComponentInChildren<Text>(true) : null);

                if (row != null)
                {
                    var trigger = row.AddComponent<EventTrigger>();
                    var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                    enter.callback.AddListener(delegate { ShowDescription(captured); });
                    trigger.triggers.Add(enter);
                }
            }

            // Description of whichever gadget is hovered (or equipped).
            var captionGO = new GameObject("Caption", typeof(RectTransform), typeof(Text));
            captionGO.transform.SetParent(_panel.transform, false);
            var crt = (RectTransform)captionGO.transform;
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = new Vector2(0f, y);
            crt.sizeDelta = new Vector2(-16f, captionHeight);
            _caption = captionGO.GetComponent<Text>();
            _caption.font = MainMenuButtonInjector.RealFont != null ? MainMenuButtonInjector.RealFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
            _caption.fontSize = Mathf.Max(10, MainMenuButtonInjector.RealFontSize - 4);
            _caption.color = new Color(0.9f, 0.9f, 0.9f);
            _caption.alignment = TextAnchor.UpperCenter;
            _caption.horizontalOverflow = HorizontalWrapMode.Wrap;
            _caption.verticalOverflow = VerticalWrapMode.Overflow;
            _caption.raycastTarget = false; // a label — must never swallow clicks meant for the rows
            y -= captionHeight + spacing;

            if (backTemplate != null)
            {
                GameObject backBtn = MainMenuButtonInjector.CloneButton(backTemplate, _panel.transform, "Back", OnBack);
                MainMenuButtonInjector.PositionChild(backBtn, y, rowHeight);
                y -= rowHeight + spacing;
            }

            panelRT.sizeDelta = new Vector2(panelRT.sizeDelta.x, -y + 10f);
            _panel.SetActive(false);
        }

        private static string RowLabel(GadgetType g)
        {
            return Gadgets.Name(g) + (Gadgets.Selected == g ? "  [EQUIPPED]" : "");
        }

        private static void ShowDescription(GadgetType g)
        {
            if (_caption != null) _caption.text = Gadgets.Name(g) + "\n\n" + Gadgets.Description(g);
        }

        private static void OnPick(GadgetType g)
        {
            if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            Gadgets.Selected = g;
            Refresh();
            ShowDescription(g);
        }

        private static void Refresh()
        {
            for (int i = 0; i < Gadgets.All.Length && i < _rowLabels.Count; i++)
                if (_rowLabels[i] != null) _rowLabels[i].text = RowLabel(Gadgets.All[i]);
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
            _panel.SetActive(true);
            _panel.transform.SetAsLastSibling();
            Refresh();
            ShowDescription(Gadgets.Selected);
        }

        public static void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }
    }
}
