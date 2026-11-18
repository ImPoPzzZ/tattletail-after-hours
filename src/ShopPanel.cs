using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// The hub's shop screens (ENDLESS SHOP machine = shards, PAPA SHOP machine
    /// = Party Tokens, VERSUS SHOP machine = Versus Tokens). Category tabs on the left, that category's items in the
    /// middle (click to buy), and the hovered item's preview + description on
    /// the right. Same items and save file as the old menu shop (Shop.cs).
    /// </summary>
    public class ShopPanel : OverlayPanel
    {
        private static ShopPanel _endless, _party, _versus;

        private bool _isParty, _isVersus;
        private readonly List<ShopCategory> _categories = new List<ShopCategory>();
        private readonly List<Text> _tabs = new List<Text>();
        private readonly Dictionary<ShopCategory, List<KeyValuePair<Row, ShopItem>>> _rowsByCategory = new Dictionary<ShopCategory, List<KeyValuePair<Row, ShopItem>>>();
        private ShopCategory _current;
        private ShopItem _shown;
        private Text _balance, _info, _message;
        private RawImage _preview;
        private float _messageTimer;
        private PreviewStage _stage;   // VERSUS SHOP: Mama/Papa skins spin live instead of a picture
        private string _stageKey;

        protected override string Title { get { return _isVersus ? "VERSUS SHOP" : _isParty ? "PAPA'S PARTY SHOP" : "ENDLESS SHOP"; } }
        protected override Vector2 PanelSize { get { return new Vector2(1560f, 820f); } }

        public static void OpenEndless() { Open(ref _endless, false, "KE_EndlessShop"); }
        public static void OpenParty() { Open(ref _party, true, "KE_PartyShop"); }
        public static void OpenVersus() { Open(ref _versus, false, "KE_VersusShop", true); }

        private static void Open(ref ShopPanel instance, bool party, string name, bool versus = false)
        {
            if (instance == null)
            {
                var go = new GameObject(name);
                DontDestroyOnLoad(go);
                instance = go.AddComponent<ShopPanel>();
                instance._isParty = party; // set before Show() builds the UI
                instance._isVersus = versus;
            }
            instance.Show();
        }

        protected override void BuildContent(Transform p)
        {
            _categories.Clear();
            if (_isVersus) _categories.Add(ShopCategory.Skin);
            else if (!_isParty)
            {
                _categories.Add(ShopCategory.PowerUp);
                _categories.Add(ShopCategory.Passive);
            }
            _categories.Add(ShopCategory.Color);
            _categories.Add(ShopCategory.Beam);
            _categories.Add(ShopCategory.Extra);

            _balance = MakeText(p, "", 30, TextAnchor.MiddleRight, new Vector2(560f, 340f), new Vector2(400f, 50f), new Color(1f, 0.85f, 0.3f));

            // Tabs.
            float ty = 230f;
            foreach (var c in _categories)
            {
                ShopCategory cat = c;
                _tabs.Add(AddButton(p, new Vector2(-590f, ty), CategoryName(c, _isVersus), () => SelectCategory(cat)));
                ty -= 75f;
            }

            // Item rows for every category; only the selected category's are visible.
            foreach (var c in _categories)
            {
                var list = new List<KeyValuePair<Row, ShopItem>>();
                float y = 250f;
                foreach (var item in Shop.Items)
                {
                    if (item.Category != c || item.Party != _isParty || item.Versus != _isVersus) continue;
                    ShopItem captured = item;
                    Row row = AddToggle(p, -20f, y, item.Name, () => Buy(captured), () => StatusText(captured));
                    list.Add(new KeyValuePair<Row, ShopItem>(row, item));
                    y -= 58f;
                }
                _rowsByCategory[c] = list;
            }

            // Preview + description.
            var previewGO = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
            previewGO.transform.SetParent(p, false);
            var prt = (RectTransform)previewGO.transform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = new Vector2(580f, 140f);
            prt.sizeDelta = new Vector2(260f, 260f);
            _preview = previewGO.GetComponent<RawImage>();
            _preview.raycastTarget = false;
            _preview.enabled = false;

            _info = MakeText(p, "", 26, TextAnchor.UpperLeft, new Vector2(580f, -110f), new Vector2(360f, 220f), Color.white);
            _info.horizontalOverflow = HorizontalWrapMode.Wrap;
            _message = Fit(MakeText(p, "", 28, TextAnchor.MiddleCenter, new Vector2(580f, -270f), new Vector2(360f, 50f), Accent));

            AddButton(p, new Vector2(0f, -360f), "BACK", () =>
            {
                Hide();
                if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            });

            SelectCategory(_categories[0]);
        }

        private static string CategoryName(ShopCategory c, bool versus)
        {
            switch (c)
            {
                case ShopCategory.Skin: return "MAMA & PAPA";
                case ShopCategory.Extra: return versus ? "VOICE" : "VOICE & MAMA";
                case ShopCategory.PowerUp: return "POWER UPS";
                case ShopCategory.Passive: return "PASSIVES";
                case ShopCategory.Color: return "COLORS";
                case ShopCategory.Beam: return "BEAMS";
                default: return "VOICE & MAMA";
            }
        }

        private void SelectCategory(ShopCategory c)
        {
            _current = c;
            foreach (var kv in _rowsByCategory)
                foreach (var pair in kv.Value) pair.Key.SetVisible(kv.Key == c);
            for (int i = 0; i < _categories.Count; i++)
                _tabs[i].color = _categories[i] == c ? Accent : Color.white;
            var list = _rowsByCategory[c];
            ShowItem(list.Count > 0 ? list[0].Value : null);
        }

        private string Currency { get { return "Shards"; } }
        private int Balance { get { return Shop.Get(Shop.SHARDS_KEY); } }

        private static string StatusText(ShopItem item)
        {
            if (Shop.IsOwned(item.Key)) return item.Consumable ? "READY" : "OWNED";
            if (!Shop.RequirementMet(item)) return "LOCKED";
            return item.Price.ToString();
        }

        private void Buy(ShopItem item)
        {
            ShowItem(item);
            if (Shop.IsOwned(item.Key))
            {
                Say(item.Consumable ? "Already bought — used on your next run." : "You already own this.");
                return;
            }
            if (!Shop.RequirementMet(item))
            {
                var req = Shop.Find(item.Requires);
                Say("Buy " + (req != null ? req.Name : "the previous tier") + " first.");
                return;
            }
            if (Shop.TryBuy(item))
            {
                Say("Bought " + item.Name + "!");
                if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            }
            else Say("Not enough " + Currency + " — you need " + item.Price + ".");
        }

        private void Say(string text)
        {
            _message.text = text;
            _messageTimer = 3f;
        }

        private void ShowItem(ShopItem item)
        {
            _shown = item;
            if (item == null) { _info.text = ""; _preview.enabled = false; return; }

            string status = Shop.IsOwned(item.Key) ? (item.Consumable ? "Ready for your next run" : "Owned") : item.Price + " " + Currency;
            _info.text = item.Name + "\n" + status + "\n\n" + item.Description;

            if (item.Category == ShopCategory.Skin)
            {
                if (_stage == null) _stage = new PreviewStage(transform, new Vector3(0f, 2100f, 0f), 320, 320);
                if (_stageKey != item.Key)
                {
                    _stageKey = item.Key;
                    _stage.Show(VersusHunters.BuildBody("KE_ShopHunterPreview", VersusHunters.CodeForKey(item.Key)), 2.4f);
                }
                _preview.texture = _stage.Texture;
                _preview.enabled = true;
                return;
            }
            if (_stage != null) _stage.Clear();
            _stageKey = null;

            Texture2D tex = PreviewLibrary.HasPreviewSlot(item) ? PreviewLibrary.Get(item.Key) : null;
            _preview.texture = tex;
            _preview.enabled = tex != null;
        }

        protected override void Update()
        {
            base.Update();
            if (_stage != null) _stage.Tick(Visible && _stageKey != null);
            if (!Visible) return;

            // Hovering an item shows its preview and description.
            foreach (var pair in _rowsByCategory[_current])
            {
                if (pair.Value != _shown && (Hit(pair.Key.label.rectTransform) || Hit(pair.Key.value.rectTransform)))
                {
                    ShowItem(pair.Value);
                    break;
                }
            }

            if (_messageTimer > 0f)
            {
                _messageTimer -= Time.unscaledDeltaTime;
                if (_messageTimer <= 0f) _message.text = "";
            }
        }

        protected override void RefreshAll()
        {
            base.RefreshAll();
            if (_balance == null) return;
            _balance.text = Currency + ": " + Balance;
            if (!_rowsByCategory.ContainsKey(_current)) return;
            foreach (var pair in _rowsByCategory[_current])
            {
                string s = pair.Key.value.text;
                pair.Key.value.color = s == "OWNED" || s == "READY" ? Accent : (s == "LOCKED" ? new Color(0.5f, 0.5f, 0.5f) : new Color(1f, 0.85f, 0.3f));
            }
        }
    }
}
