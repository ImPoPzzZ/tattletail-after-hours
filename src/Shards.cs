using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>Shard balance — stored in Shop.txt as "Shards = x" (see Shop.cs).</summary>
    public static class Shards
    {
        public static int Balance { get { return Shop.Get(Shop.SHARDS_KEY); } }
        public static void Add(int amount) { Shop.AddShards(amount); }
    }

    /// <summary>Purple shard icon + balance in the top-right corner of the main menu scene only (destroyed with the scene, never shown in a run).</summary>
    public static class ShardMenuIcon
    {
        private static GameObject _root;
        private static Text _label;
        private static Sprite _sprite;
        private static Sprite _tokenSprite;
        private static Image _iconImage;
        private static bool _partyShown;

        public static void Build(MainMenu menu)
        {
            if (_root != null) return;
            Transform canvas = menu.mainMenuHolder.transform.parent;
            while (canvas != null && canvas.GetComponent<Canvas>() == null) canvas = canvas.parent;
            if (canvas == null) canvas = menu.mainMenuHolder.transform.parent;

            _root = new GameObject("KE_ShardHud", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var rt = (RectTransform)_root.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-64f, -48f);
            rt.sizeDelta = new Vector2(160f, 32f);

            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(_root.transform, false);
            var iconRT = (RectTransform)iconGO.transform;
            iconRT.anchorMin = iconRT.anchorMax = iconRT.pivot = new Vector2(1f, 0.5f);
            iconRT.anchoredPosition = Vector2.zero;
            iconRT.sizeDelta = new Vector2(28f, 28f);
            var img = iconGO.GetComponent<Image>();
            img.sprite = GetSprite();
            img.raycastTarget = false;
            _iconImage = img;
            _partyShown = false;

            var textGO = new GameObject("Count", typeof(RectTransform), typeof(Text));
            textGO.transform.SetParent(_root.transform, false);
            var textRT = (RectTransform)textGO.transform;
            textRT.anchorMin = textRT.anchorMax = textRT.pivot = new Vector2(1f, 0.5f);
            textRT.anchoredPosition = new Vector2(-34f, 0f);
            textRT.sizeDelta = new Vector2(120f, 32f);
            _label = textGO.GetComponent<Text>();
            _label.font = MainMenuButtonInjector.RealFont != null ? MainMenuButtonInjector.RealFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
            _label.fontSize = MainMenuButtonInjector.RealFontSize;
            _label.color = new Color(0.85f, 0.65f, 1f);
            _label.alignment = TextAnchor.MiddleRight;
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            _label.raycastTarget = false;

            Refresh();
        }

        public static void Refresh()
        {
            if (_root == null || _label == null) return;

            // Shows Party Tokens while any Papa's 10th Birthday! menu is open, shards everywhere else.
            bool party = false; // (one currency now: shards, everywhere)
            if (party != _partyShown)
            {
                _partyShown = party;
                if (_iconImage != null) _iconImage.sprite = party ? GetTokenSprite() : GetSprite();
                _label.color = party ? new Color(1f, 0.85f, 0.35f) : new Color(0.85f, 0.65f, 1f);
            }

            string text = Shards.Balance.ToString();
            if (_label.text != text) _label.text = text;
        }

        // Party Token: a gold coin with a pink star, drawn in code like the shard.
        private static Sprite GetTokenSprite()
        {
            if (_tokenSprite != null) return _tokenSprite;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float alpha = Mathf.Clamp01((1f - r) * 24f);

                    // 5-point star: radius limit oscillates with the angle.
                    float ang = Mathf.Atan2(v, u) - Mathf.PI / 2f;
                    float starR = 0.32f + 0.22f * Mathf.Abs(Mathf.Cos(ang * 2.5f));
                    bool inStar = r < starR;

                    Color c;
                    if (r > 0.86f) c = new Color(0.72f, 0.5f, 0.1f);            // rim
                    else if (inStar) c = new Color(1f, 0.45f, 0.7f);             // pink star
                    else c = new Color(1f, 0.82f, 0.25f);                        // gold face
                    c.a = alpha;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            _tokenSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _tokenSprite;
        }

        // Procedural gem: tall purple diamond, lighter upper-left facet,
        // darker lower-right facet, pale outline, soft anti-aliased edge.
        private static Sprite GetSprite()
        {
            if (_sprite != null) return _sprite;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Abs(u) * 1.15f + Mathf.Abs(v) * 0.85f; // <=1 inside the gem
                    float alpha = Mathf.Clamp01((1f - d) * 24f);
                    Color c;
                    if (d > 0.88f) c = new Color(0.92f, 0.8f, 1f);          // outline
                    else if (u < 0f && v > 0f) c = new Color(0.78f, 0.5f, 1f);  // light facet
                    else if (u > 0f && v < 0f) c = new Color(0.38f, 0.1f, 0.68f); // dark facet
                    else c = new Color(0.6f, 0.25f, 0.92f);                  // base
                    c.a = alpha;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            _sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _sprite;
        }
    }
}
