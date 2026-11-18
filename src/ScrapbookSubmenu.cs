using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// "Mama's Scrapbook" — a purely cosmetic sticker collection reached
    /// from the Endless submenu. A paged grid (no real page-turn animation —
    /// see the conversation that led here: not worth it for this engine,
    /// Next/Prev arrows read as "a scrapbook" well enough) of every sticker
    /// in Scrapbook.cs, locked ones dimmed, plus a Loot Box button that
    /// spends shards for a random undiscovered one. Same clone-real-widgets
    /// recipe as every other submenu.
    /// </summary>
    public static class ScrapbookSubmenu
    {
        private const int COLS = 4;
        private const int ROWS = 3;
        private const int PER_PAGE = COLS * ROWS;
        private const float CELL = 48f;
        private const float CELL_SPACING = 6f;

        private static GameObject _panel;
        private static MainMenu _menu;
        private static Text _balanceLabel;
        private static Text _captionLabel;
        private static Text _pageLabel;
        private static readonly List<Image> _cellImages = new List<Image>();
        private static readonly List<Outline> _cellOutlines = new List<Outline>();
        private static int _page;
        private static GameObject _gridRoot;

        public static bool IsOpen { get { return _panel != null && _panel.activeSelf; } }

        private static void Build(MainMenu menu)
        {
            _menu = menu;

            GameObject optionsPanelTemplate = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "OptionsPanel");
            GameObject cycleTemplate        = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "Button_Sensitivity");
            GameObject backTemplate         = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "Button_Back");
            GameObject titleTemplate        = MainMenuButtonInjector.FindChildByName(menu.optionsMenuHolder, "OptimizeText");
            if (optionsPanelTemplate == null)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: ScrapbookSubmenu.Build — OptionsPanel template not found.");
                return;
            }

            _panel = Object.Instantiate(optionsPanelTemplate, menu.mainMenuHolder.transform.parent, false);
            _panel.name = "KE_ScrapbookPanel";
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
                // Explicitly wide: the stock OptionsPanel template is only
                // just wide enough for a single stacked button (~226px) —
                // too narrow for a 4-wide grid plus the Prev/Next arrows at
                // its edges. Left at the template's default width, the
                // grid's own rightmost column sat close enough to the ">"
                // button's click area that clicks could land on the sticker
                // cell underneath instead of the arrow — which only seemed
                // to depend on lock state because that's what changed
                // between test pages, not because lock state actually
                // matters here. Explicit, generous width kills the overlap
                // outright.
                panelRT.sizeDelta = new Vector2(340f, panelRT.sizeDelta.y);
            }

            var vlg = _panel.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) vlg.enabled = false;

            float y = -10f;
            const float rowHeight = 33f;
            const float titleHeight = 40f;
            const float spacing = 6f;

            if (titleTemplate != null)
            {
                GameObject title = MainMenuButtonInjector.CloneLabel(titleTemplate, _panel.transform, "MAMA'S SCRAPBOOK");
                MainMenuButtonInjector.PositionChild(title, y, titleHeight);
                y -= titleHeight + spacing;
            }

            GameObject balanceRow = MainMenuButtonInjector.CloneButton(cycleTemplate, _panel.transform, "", null);
            MainMenuButtonInjector.PositionChild(balanceRow, y, rowHeight);
            y -= rowHeight + spacing;
            _balanceLabel = balanceRow != null ? balanceRow.GetComponentInChildren<Text>(true) : null;

            GameObject lootBtn = MainMenuButtonInjector.CloneButton(cycleTemplate, _panel.transform,
                "Open Loot Box (" + Scrapbook.LOOTBOX_PRICE + " Shards)", OnOpenLootBox);
            MainMenuButtonInjector.PositionChild(lootBtn, y, rowHeight);
            y -= rowHeight + spacing;

            // The grid itself.
            float gridHeight = ROWS * CELL + (ROWS - 1) * CELL_SPACING;
            GameObject gridRoot = new GameObject("Grid", typeof(RectTransform));
            gridRoot.transform.SetParent(_panel.transform, false);
            MainMenuButtonInjector.PositionChild(gridRoot, y, gridHeight);
            _gridRoot = gridRoot;
            y -= gridHeight + spacing;

            float gridWidth = COLS * CELL + (COLS - 1) * CELL_SPACING;
            _cellImages.Clear();
            _cellOutlines.Clear();
            for (int i = 0; i < PER_PAGE; i++)
            {
                int col = i % COLS;
                int row = i / COLS;
                GameObject cellGO = new GameObject("Cell" + i, typeof(RectTransform), typeof(Image));
                cellGO.transform.SetParent(gridRoot.transform, false);
                var cellRT = (RectTransform)cellGO.transform;
                cellRT.anchorMin = cellRT.anchorMax = new Vector2(0.5f, 1f);
                cellRT.pivot = new Vector2(0f, 1f);
                float startX = -gridWidth / 2f;
                cellRT.anchoredPosition = new Vector2(startX + col * (CELL + CELL_SPACING), -row * (CELL + CELL_SPACING));
                cellRT.sizeDelta = new Vector2(CELL, CELL);

                var img = cellGO.GetComponent<Image>();
                img.preserveAspect = true;
                img.color = new Color(1f, 1f, 1f, 0f); // hidden until Refresh() fills it in

                // Rarity ring — color set per-entry in Refresh().
                var outline = cellGO.AddComponent<Outline>();
                outline.effectDistance = new Vector2(2f, -2f);

                int captured = i;
                var trigger = cellGO.AddComponent<EventTrigger>();
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(delegate { OnHoverCell(captured); });
                trigger.triggers.Add(enter);

                _cellImages.Add(img);
                _cellOutlines.Add(outline);
            }

            // Page arrows either side of a "Page X/Y" label.
            GameObject prevBtn = MainMenuButtonInjector.CloneButton(cycleTemplate, _panel.transform, "<", () => ChangePage(-1));
            var prevRT = prevBtn != null ? prevBtn.GetComponent<RectTransform>() : null;
            if (prevRT != null)
            {
                prevRT.anchorMin = prevRT.anchorMax = new Vector2(0f, 1f);
                prevRT.pivot = new Vector2(0f, 1f);
                prevRT.anchoredPosition = new Vector2(0f, y);
                prevRT.sizeDelta = new Vector2(50f, rowHeight);
            }

            GameObject nextBtn = MainMenuButtonInjector.CloneButton(cycleTemplate, _panel.transform, ">", () => ChangePage(1));
            var nextRT = nextBtn != null ? nextBtn.GetComponent<RectTransform>() : null;
            if (nextRT != null)
            {
                nextRT.anchorMin = nextRT.anchorMax = new Vector2(1f, 1f);
                nextRT.pivot = new Vector2(1f, 1f);
                nextRT.anchoredPosition = new Vector2(0f, y);
                nextRT.sizeDelta = new Vector2(50f, rowHeight);
            }
            // Belt-and-suspenders: guarantee the arrows are always the
            // topmost raycast targets in their area, regardless of the
            // width fix above.
            if (prevBtn != null) prevBtn.transform.SetAsLastSibling();
            if (nextBtn != null) nextBtn.transform.SetAsLastSibling();

            GameObject pageLabelGO = new GameObject("PageLabel", typeof(RectTransform), typeof(Text));
            pageLabelGO.transform.SetParent(_panel.transform, false);
            var pageLabelRT = (RectTransform)pageLabelGO.transform;
            pageLabelRT.anchorMin = new Vector2(0f, 1f);
            pageLabelRT.anchorMax = new Vector2(1f, 1f);
            pageLabelRT.pivot = new Vector2(0.5f, 1f);
            pageLabelRT.anchoredPosition = new Vector2(0f, y);
            pageLabelRT.sizeDelta = new Vector2(0f, rowHeight);
            _pageLabel = pageLabelGO.GetComponent<Text>();
            _pageLabel.font = MainMenuButtonInjector.RealFont != null ? MainMenuButtonInjector.RealFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
            _pageLabel.fontSize = MainMenuButtonInjector.RealFontSize;
            _pageLabel.color = Color.white;
            _pageLabel.alignment = TextAnchor.MiddleCenter;
            // Full-width and created (and re-sibling-ordered) after the
            // Prev/Next buttons below it in the same row — a plain Text
            // defaults to raycastTarget=true, so left alone this invisible-
            // background label was sitting on top of the whole row and
            // eating every click meant for either arrow. It's just a
            // label; it never needs to catch a click itself.
            _pageLabel.raycastTarget = false;
            y -= rowHeight + spacing;

            // Caption: shows the hovered sticker's name (or the Loot Box result).
            GameObject captionGO = new GameObject("Caption", typeof(RectTransform), typeof(Text));
            captionGO.transform.SetParent(_panel.transform, false);
            var captionRT = (RectTransform)captionGO.transform;
            captionRT.anchorMin = new Vector2(0f, 1f);
            captionRT.anchorMax = new Vector2(1f, 1f);
            captionRT.pivot = new Vector2(0.5f, 1f);
            captionRT.anchoredPosition = new Vector2(0f, y);
            captionRT.sizeDelta = new Vector2(0f, rowHeight);
            _captionLabel = captionGO.GetComponent<Text>();
            _captionLabel.font = _pageLabel.font;
            _captionLabel.fontSize = Mathf.Max(10, MainMenuButtonInjector.RealFontSize - 2);
            _captionLabel.color = new Color(0.85f, 0.85f, 0.85f);
            _captionLabel.alignment = TextAnchor.MiddleCenter;
            _captionLabel.raycastTarget = false;
            _captionLabel.text = "";
            y -= rowHeight + spacing;

            if (backTemplate != null)
            {
                GameObject backBtn = MainMenuButtonInjector.CloneButton(backTemplate, _panel.transform, "Back", OnBack);
                MainMenuButtonInjector.PositionChild(backBtn, y, rowHeight);
                y -= rowHeight + spacing;
            }

            panelRT.sizeDelta = new Vector2(panelRT.sizeDelta.x, -y + 10f);
            _panel.SetActive(false);
        }

        private static int PageCount()
        {
            return Mathf.Max(1, Mathf.CeilToInt(Scrapbook.Entries.Length / (float)PER_PAGE));
        }

        private static void ChangePage(int delta)
        {
            if (Camera.main != null) AudioController.Play("vhs_glitch", Camera.main.transform);
            int count = PageCount();
            _page = (_page + delta + count) % count;
            Refresh();
        }

        private static void OnHoverCell(int slot)
        {
            int idx = _page * PER_PAGE + slot;
            if (idx < 0 || idx >= Scrapbook.Entries.Length) { _captionLabel.text = ""; return; }
            var e = Scrapbook.Entries[idx];
            _captionLabel.text = Scrapbook.IsOwned(e.Key)
                ? e.Name + "  [" + e.Rarity + "]"
                : "???  (" + e.Rarity + ", locked)";
        }

        private static void OnOpenLootBox()
        {
            if (Shop.Get(Shop.SHARDS_KEY) < Scrapbook.LOOTBOX_PRICE)
            {
                if (Camera.main != null) AudioController.Play("vhs_glitch", Camera.main.transform);
                _captionLabel.text = "Not enough shards — you need " + Scrapbook.LOOTBOX_PRICE + ".";
                return;
            }

            var won = Scrapbook.TryOpenLootBox();
            if (won == null)
            {
                _captionLabel.text = "You've collected everything the Loot Box has to offer!";
                return;
            }
            if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            _captionLabel.text = "You got: " + won.Name + "  [" + won.Rarity + "]!";
            KaleidoscopeEndlessPlugin.Instance.StartCoroutine(PlayLootAnimation(won));
        }

        /// <summary>Jumps to the won sticker's own page, then flies a copy of its
        /// art from the Loot Box button into its slot before revealing it for real.</summary>
        private static System.Collections.IEnumerator PlayLootAnimation(StickerEntry won)
        {
            int idx = Scrapbook.IndexOf(won);
            if (idx < 0 || _gridRoot == null) { Refresh(); yield break; }

            _page = idx / PER_PAGE;
            int slot = idx % PER_PAGE;
            Refresh();

            var destImg = _cellImages[slot];
            var destOutline = _cellOutlines[slot];
            Color revealColor = destImg.color;
            Color revealOutline = destOutline.effectColor;
            destImg.color = new Color(1f, 1f, 1f, 0f); // hide the real cell until the flight lands
            destOutline.effectColor = Color.clear;

            var destRT = destImg.GetComponent<RectTransform>();
            // Cell's pivot is top-left, so its center is half a cell right and half a cell down from its anchoredPosition.
            Vector2 endPos = destRT.anchoredPosition + new Vector2(destRT.sizeDelta.x * 0.5f, -destRT.sizeDelta.y * 0.5f);
            Vector2 startPos = new Vector2(endPos.x, endPos.y + CELL * 3f); // drifts down into place from above the grid

            GameObject fly = new GameObject("LootFly", typeof(RectTransform), typeof(Image));
            fly.transform.SetParent(_gridRoot.transform, false);
            fly.transform.SetAsLastSibling();
            var flyRT = (RectTransform)fly.transform;
            flyRT.anchorMin = flyRT.anchorMax = new Vector2(0.5f, 1f);
            flyRT.pivot = new Vector2(0.5f, 0.5f);
            flyRT.sizeDelta = new Vector2(CELL, CELL);
            flyRT.anchoredPosition = startPos;
            var flyImg = fly.GetComponent<Image>();
            flyImg.preserveAspect = true;
            flyImg.sprite = StickerImages.Get(won.Key);

            const float duration = 0.5f;
            float startScale = 1.6f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float p = Mathf.Clamp01(t / duration);
                float eased = 1f - (1f - p) * (1f - p); // ease-out
                flyRT.anchoredPosition = Vector2.Lerp(startPos, endPos, eased);
                float scale = Mathf.Lerp(startScale, 1f, eased);
                flyRT.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            Object.Destroy(fly);
            destImg.color = revealColor;
            destOutline.effectColor = revealOutline;

            // A little pop on landing.
            float bounce = 0f;
            const float bounceDuration = 0.2f;
            while (bounce < bounceDuration)
            {
                bounce += Time.deltaTime;
                float p = bounce / bounceDuration;
                float s = 1f + 0.25f * Mathf.Sin(p * Mathf.PI);
                destRT.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            destRT.localScale = Vector3.one;
        }

        private static void OnBack()
        {
            if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            Hide();
            EndlessSubmenu.Show(_menu);
        }

        public static void Refresh()
        {
            if (_panel == null || !_panel.activeSelf) return;
            if (_balanceLabel != null) _balanceLabel.text = "Shards: " + Shards.Balance + "   |   Collected: " + Scrapbook.OwnedCount() + "/" + Scrapbook.Entries.Length;
            if (_pageLabel != null) _pageLabel.text = "Page " + (_page + 1) + "/" + PageCount();

            for (int i = 0; i < _cellImages.Count; i++)
            {
                int idx = _page * PER_PAGE + i;
                var img = _cellImages[i];
                var outline = _cellOutlines[i];
                if (idx >= Scrapbook.Entries.Length) { img.color = new Color(1f, 1f, 1f, 0f); outline.effectColor = Color.clear; continue; }

                var e = Scrapbook.Entries[idx];
                var sprite = StickerImages.Get(e.Key);
                img.sprite = sprite;
                if (sprite == null) { img.color = new Color(1f, 1f, 1f, 0f); outline.effectColor = Color.clear; continue; }
                bool owned = Scrapbook.IsOwned(e.Key);
                img.color = owned ? Color.white : new Color(0.12f, 0.12f, 0.12f, 1f);
                outline.effectColor = owned ? e.RarityColor : new Color(0.3f, 0.3f, 0.3f, 0.6f);
            }
        }

        public static void Show(MainMenu menu)
        {
            _menu = menu;
            if (_panel == null) Build(menu);
            if (_panel == null) return;
            _panel.SetActive(true);
            _panel.transform.SetAsLastSibling();
            _captionLabel.text = "";
            Refresh();
        }

        public static void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }
    }
}
