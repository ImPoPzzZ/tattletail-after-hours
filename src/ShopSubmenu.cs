using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Shop: a root panel with one button per category (Power Ups, Tattletail
    /// Colors, Flashlight Beams, Voice &amp; Mama); each category is its own
    /// panel of buyable rows plus a preview pane (the 3D render + a
    /// description) that follows whichever row is hovered/clicked. Same
    /// clone-real-widgets recipe as the other submenus.
    /// </summary>
    public static class ShopSubmenu
    {
        private static readonly ShopMenu _endless = new ShopMenu(false);
        private static readonly ShopMenu _party = new ShopMenu(true);

        /// <summary>The Endless shop (shards).</summary>
        public static void Show(MainMenu menu) { _endless.Show(menu); }
        /// <summary>Papa's 10th Birthday!'s shop (Party Tokens).</summary>
        public static void ShowParty(MainMenu menu) { _party.Show(menu); }
        public static bool PartyOpen { get { return _party.IsOpen; } }

        public static void Refresh() { _endless.Refresh(); _party.Refresh(); }
        public static void Hide() { _endless.Hide(); _party.Hide(); }
    }

    /// <summary>One shop (root panel + its category panels). The Endless shop and the birthday shop are two instances of this.</summary>
    public class ShopMenu
    {
        private readonly bool _isParty;
        private GameObject _panel;
        private MainMenu _menu;
        private Text _balanceLabel;
        private readonly List<ShopCategoryPanel> _categories = new List<ShopCategoryPanel>();

        public ShopMenu(bool party) { _isParty = party; }

        public bool IsOpen
        {
            get
            {
                if (_panel != null && _panel.activeSelf) return true;
                foreach (var cat in _categories) if (cat.IsOpen) return true;
                return false;
            }
        }

        private void Build(MainMenu menu)
        {
            _menu = menu;

            GameObject optionsPanelTemplate = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "OptionsPanel");
            GameObject cycleTemplate        = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "Button_Sensitivity");
            GameObject backTemplate         = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "Button_Back");
            GameObject titleTemplate        = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "OptimizeText");

            if (optionsPanelTemplate == null)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: ShopMenu.Build — OptionsPanel template not found, cannot build.");
                return;
            }

            _panel = Object.Instantiate(optionsPanelTemplate, menu.mainMenuHolder.transform.parent, false);
            _panel.name = _isParty ? "KE_PartyShopPanel" : "KE_ShopPanel";
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
                GameObject title = MainMenuButtonInjector.CloneLabel(titleTemplate, _panel.transform, _isParty ? "PARTY SHOP" : "SHOP");
                MainMenuButtonInjector.PositionChild(title, y, titleHeight);
                y -= titleHeight + spacing + 30f;
            }

            GameObject balanceRow = MainMenuButtonInjector.CloneButton(cycleTemplate, _panel.transform, "", null);
            MainMenuButtonInjector.PositionChild(balanceRow, y, rowHeight);
            y -= rowHeight + spacing;
            _balanceLabel = balanceRow?.GetComponentInChildren<Text>(true);

            _categories.Clear();
            if (!_isParty)
            {
                AddCategoryButton(cycleTemplate, "Power Ups", ShopCategory.PowerUp, ref y, rowHeight, spacing);
                AddCategoryButton(cycleTemplate, "Passives", ShopCategory.Passive, ref y, rowHeight, spacing);
            }
            AddCategoryButton(cycleTemplate, "Tattletail Colors", ShopCategory.Color, ref y, rowHeight, spacing);
            AddCategoryButton(cycleTemplate, "Flashlight Beams", ShopCategory.Beam, ref y, rowHeight, spacing);
            AddCategoryButton(cycleTemplate, "Voice & Mama", ShopCategory.Extra, ref y, rowHeight, spacing);

            if (backTemplate != null)
            {
                GameObject backBtn = MainMenuButtonInjector.CloneButton(backTemplate, _panel.transform, "Back", OnBack);
                MainMenuButtonInjector.PositionChild(backBtn, y, rowHeight);
                y -= rowHeight + spacing;
            }

            panelRT.sizeDelta = new Vector2(panelRT.sizeDelta.x, -y + 10f);
            _panel.SetActive(false);
        }

        private void AddCategoryButton(GameObject template, string label, ShopCategory category, ref float y, float rowHeight, float spacing)
        {
            var cat = new ShopCategoryPanel(category, label.ToUpperInvariant(), _isParty, this);
            _categories.Add(cat);
            GameObject btn = MainMenuButtonInjector.CloneButton(template, _panel.transform, label, () => OpenCategory(cat));
            MainMenuButtonInjector.PositionChild(btn, y, rowHeight);
            y -= rowHeight + spacing;
        }

        private void OpenCategory(ShopCategoryPanel cat)
        {
            if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            if (_panel != null) _panel.SetActive(false);
            cat.Show(_menu);
        }

        /// <summary>Category panel's Back button.</summary>
        public void ReturnToShopRoot()
        {
            Show(_menu);
        }

        public void Refresh()
        {
            if (_panel != null && _panel.activeSelf && _balanceLabel != null)
                _balanceLabel.text = "Shards: " + Shards.Balance;
            foreach (var cat in _categories) cat.Refresh();
        }

        private void OnBack()
        {
            if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            Hide();
            if (_isParty) BirthdaySubmenu.Show(_menu);
            else EndlessSubmenu.Show(_menu);
        }

        public void Show(MainMenu menu)
        {
            _menu = menu;
            if (_panel == null) Build(menu);
            if (_panel == null) return;
            foreach (var cat in _categories) cat.Hide();
            _panel.SetActive(true);
            _panel.transform.SetAsLastSibling();
            Refresh();
        }

        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
            foreach (var cat in _categories) cat.Hide();
        }
    }

    /// <summary>One shop category: buyable rows on the left, preview pane on the right.</summary>
    public class ShopCategoryPanel
    {
        private readonly ShopCategory _category;
        private readonly string _title;
        private readonly List<ShopItem> _items = new List<ShopItem>();
        private readonly List<Text> _rowLabels = new List<Text>();

        private GameObject _panel;
        private GameObject _previewRoot;
        private RawImage _previewImage;
        private Text _caption;
        private ShopItem _selected;
        private readonly ShopMenu _owner;

        public bool IsOpen { get { return _panel != null && _panel.activeSelf; } }

        public ShopCategoryPanel(ShopCategory category, string title, bool party, ShopMenu owner)
        {
            _category = category;
            _title = title;
            _owner = owner;
            foreach (var item in Shop.Items)
                if (item.Category == category && item.Party == party && !item.Versus) _items.Add(item);
        }

        private void Build(MainMenu menu)
        {
            GameObject optionsPanelTemplate = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "OptionsPanel");
            GameObject cycleTemplate        = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "Button_Sensitivity");
            GameObject backTemplate         = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "Button_Back");
            GameObject titleTemplate        = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "OptimizeText");
            if (optionsPanelTemplate == null) return;

            Transform parent = menu.mainMenuHolder.transform.parent;
            _panel = Object.Instantiate(optionsPanelTemplate, parent, false);
            _panel.name = "KE_ShopCategory_" + _category + (_items.Count > 0 && _items[0].Party ? "_Party" : "");
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
                GameObject title = MainMenuButtonInjector.CloneLabel(titleTemplate, _panel.transform, _title);
                MainMenuButtonInjector.PositionChild(title, y, titleHeight);
                y -= titleHeight + spacing + 30f;
            }

            foreach (var item in _items)
            {
                ShopItem captured = item;
                GameObject row = MainMenuButtonInjector.CloneButton(cycleTemplate, _panel.transform, RowLabel(item), () => OnRowClicked(captured));
                MainMenuButtonInjector.PositionChild(row, y, rowHeight);
                y -= rowHeight + spacing;
                _rowLabels.Add(row != null ? row.GetComponentInChildren<Text>(true) : null);

                if (row != null)
                {
                    var trigger = row.AddComponent<EventTrigger>();
                    var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                    enter.callback.AddListener(delegate { SetSelected(captured, null); });
                    trigger.triggers.Add(enter);
                }
            }

            if (backTemplate != null)
            {
                GameObject backBtn = MainMenuButtonInjector.CloneButton(backTemplate, _panel.transform, "Back", OnBack);
                MainMenuButtonInjector.PositionChild(backBtn, y, rowHeight);
                y -= rowHeight + spacing;
            }

            panelRT.sizeDelta = new Vector2(panelRT.sizeDelta.x, -y + 10f);
            BuildPreviewPane(parent, panelRT);
            _panel.SetActive(false);
            _previewRoot.SetActive(false);
        }

        private void BuildPreviewPane(Transform parent, RectTransform panelRT)
        {
            _previewRoot = new GameObject("KE_ShopPreview_" + _category + (_items.Count > 0 && _items[0].Party ? "_Party" : ""), typeof(RectTransform), typeof(Image));
            _previewRoot.transform.SetParent(parent, false);
            var rt = (RectTransform)_previewRoot.transform;
            rt.anchorMin = panelRT.anchorMin;
            rt.anchorMax = panelRT.anchorMax;
            rt.pivot = panelRT.pivot;
            rt.sizeDelta = new Vector2(256f, 372f);
            rt.anchoredPosition = panelRT.anchoredPosition + new Vector2(panelRT.sizeDelta.x * 0.5f + 20f + 128f, 0f);
            var bg = _previewRoot.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.6f);
            bg.raycastTarget = false;

            var imgGO = new GameObject("Render", typeof(RectTransform), typeof(RawImage));
            imgGO.transform.SetParent(_previewRoot.transform, false);
            var imgRT = (RectTransform)imgGO.transform;
            imgRT.anchorMin = imgRT.anchorMax = imgRT.pivot = new Vector2(0.5f, 1f);
            imgRT.anchoredPosition = new Vector2(0f, -8f);
            imgRT.sizeDelta = new Vector2(240f, 240f);
            _previewImage = imgGO.GetComponent<RawImage>();
            _previewImage.raycastTarget = false;
            _previewImage.enabled = false;

            var textGO = new GameObject("Caption", typeof(RectTransform), typeof(Text));
            textGO.transform.SetParent(_previewRoot.transform, false);
            var textRT = (RectTransform)textGO.transform;
            textRT.anchorMin = textRT.anchorMax = textRT.pivot = new Vector2(0.5f, 1f);
            textRT.anchoredPosition = new Vector2(0f, -252f);
            textRT.sizeDelta = new Vector2(236f, 112f);
            _caption = textGO.GetComponent<Text>();
            _caption.font = MainMenuButtonInjector.RealFont != null ? MainMenuButtonInjector.RealFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
            _caption.fontSize = Mathf.Max(10, Mathf.RoundToInt(MainMenuButtonInjector.RealFontSize * 0.75f));
            _caption.color = Color.white;
            _caption.alignment = TextAnchor.UpperCenter;
            _caption.horizontalOverflow = HorizontalWrapMode.Wrap;
            _caption.verticalOverflow = VerticalWrapMode.Overflow;
            _caption.raycastTarget = false;
        }

        private static string RowLabel(ShopItem item)
        {
            if (Shop.IsOwned(item.Key))
                return item.Name + (item.Consumable ? "  [READY]" : "  [OWNED]");
            if (!Shop.RequirementMet(item)) return item.Name + "  [locked]";
            return item.Name + "  (" + item.Price + ")";
        }

        private void SetSelected(ShopItem item, string message)
        {
            _selected = item;
            if (_caption == null || _previewImage == null) return;

            var sb = new System.Text.StringBuilder();
            sb.Append(item.Name);
            if (Shop.IsOwned(item.Key)) sb.Append(item.Consumable ? "  -  ready for your next run" : "  -  owned");
            else sb.Append("  -  ").Append(item.Price).Append(" ").Append(Shop.CurrencyName(item));
            sb.Append("\n").Append(item.Description);

            Texture2D tex = PreviewLibrary.HasPreviewSlot(item) ? PreviewLibrary.Get(item.Key) : null;
            if (tex != null)
            {
                _previewImage.texture = tex;
                _previewImage.enabled = true;
            }
            else
            {
                _previewImage.enabled = false;
                if (PreviewLibrary.HasPreviewSlot(item)) sb.Append("\n(3D preview appears after your next run.)");
            }

            if (!string.IsNullOrEmpty(message)) sb.Append("\n\n").Append(message);
            _caption.text = sb.ToString();
        }

        private void OnRowClicked(ShopItem item)
        {
            if (Shop.IsOwned(item.Key))
            {
                SetSelected(item, item.Consumable ? "Already bought — it's used up on your next run." : "You already own this.");
                return;
            }
            if (!Shop.RequirementMet(item))
            {
                if (Camera.main != null) AudioController.Play("vhs_glitch", Camera.main.transform);
                var req = Shop.Find(item.Requires);
                SetSelected(item, "Buy " + (req != null ? req.Name : "the previous tier") + " first.");
                return;
            }
            if (Shop.TryBuy(item))
            {
                if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
                Refresh();
                SetSelected(item, "Bought!");
            }
            else
            {
                if (Camera.main != null) AudioController.Play("vhs_glitch", Camera.main.transform);
                SetSelected(item, "Not enough " + Shop.CurrencyName(item) + " — you need " + item.Price + ".");
            }
        }

        public void Refresh()
        {
            if (_panel == null || !_panel.activeSelf) return;
            for (int i = 0; i < _items.Count && i < _rowLabels.Count; i++)
            {
                if (_rowLabels[i] == null) continue;
                string text = RowLabel(_items[i]);
                if (_rowLabels[i].text != text) _rowLabels[i].text = text;
            }
        }

        private void OnBack()
        {
            if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            Hide();
            _owner.ReturnToShopRoot();
        }

        public void Show(MainMenu menu)
        {
            if (_panel == null) Build(menu);
            if (_panel == null) return;
            _panel.SetActive(true);
            _panel.transform.SetAsLastSibling();
            _previewRoot.SetActive(true);
            _previewRoot.transform.SetAsLastSibling();
            Refresh();
            if (_items.Count > 0) SetSelected(_selected ?? _items[0], null);
        }

        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
            if (_previewRoot != null) _previewRoot.SetActive(false);
        }
    }
}
