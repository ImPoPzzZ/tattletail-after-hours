using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Full-screen "loading" cover shown while the arcade loads — hides the
    /// empty menu at startup and the level load on the way back to the hub.
    /// Shown by HubMode.Launch / the menu patch, hidden once the player is
    /// standing in the arcade (or if the arcade fails to load).
    /// </summary>
    public class LoadingCover : MonoBehaviour
    {
        private static LoadingCover _instance;

        private const float MAX_SHOWN = 45f; // never leave the screen covered forever
        private const float FADE = 0.4f;

        private static readonly string[] Tips =
        {
            "Hold left click on a machine to use it.",
            "Your saves live in Documents\\Tattletail After Hours.",
            "Get gadgets from the Gumball Machine in the shop.",
            "Mama is always listening.",
            "Papa gets hungry. Papa gets loud.",
            "Tweak blur, bloom and glow at the OPTIONS machine.",
        };

        private CanvasGroup _group;
        private Text _loading;
        private Image[] _bars;
        private float _shownFor;
        private float _fadeOut = -1f;

        public static void Show(Font font)
        {
            if (_instance == null)
            {
                var go = new GameObject("KE_LoadingCover", typeof(RectTransform));
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<LoadingCover>();
                _instance.Build(font);
            }
            _instance._shownFor = 0f;
            _instance._fadeOut = -1f;
            _instance._group.alpha = 1f;
            _instance.gameObject.SetActive(true);
        }

        /// <summary>Is the cover up (not fading out)?</summary>
        public static bool Up { get { return _instance != null && _instance.gameObject.activeSelf && _instance._fadeOut < 0f; } }

        /// <summary>Called every frame while something is still being built: if anything took the cover down, it goes back up.</summary>
        public static void Keep() { if (!Up) Show(null); }

        public static void Hide()
        {
            if (_instance == null || !_instance.gameObject.activeSelf) return;
            if (_instance._fadeOut < 0f) _instance._fadeOut = FADE;
        }

        private void Build(Font font)
        {
            if (font == null) font = MainMenuButtonInjector.RealFont;
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000; // above the game's own menu and loading graphics
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = true;

            var bg = NewImage(transform, Vector2.zero, Vector2.zero, Color.black);
            var bgRT = bg.rectTransform;
            bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one; bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;

            NewText(transform, "TATTLETAIL: AFTER HOURS", font, 64, new Vector2(0f, 90f), new Color(1f, 1f, 1f, 0.95f));
            _loading = NewText(transform, "LOADING", font, 34, new Vector2(0f, -10f), new Color(1f, 1f, 1f, 0.7f));

            // A row of little blocks cycling through colors, kaleidoscope style.
            _bars = new Image[9];
            for (int i = 0; i < _bars.Length; i++)
                _bars[i] = NewImage(transform, new Vector2((i - (_bars.Length - 1) / 2f) * 34f, -80f), new Vector2(22f, 22f), Color.white);

            NewText(transform, Tips[Random.Range(0, Tips.Length)], font, 26, new Vector2(0f, -425f), new Color(1f, 1f, 1f, 0.45f));
            NewText(transform, Support.DISCORD_LINE, font, 22, new Vector2(0f, -470f), new Color(0.65f, 0.7f, 1f, 0.6f));
            NewText(transform, Support.LINE, font, 22, new Vector2(0f, -500f), new Color(1f, 0.75f, 0.85f, 0.5f));
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            _loading.text = "LOADING" + new string('.', 1 + (int)(t * 2f) % 3);
            for (int i = 0; i < _bars.Length; i++)
            {
                float wave = Mathf.Sin(t * 4f - i * 0.6f) * 0.5f + 0.5f;
                Color c = Color.HSVToRGB(Mathf.Repeat(t * 0.15f + i / (float)_bars.Length, 1f), 0.75f, 1f);
                c.a = 0.35f + wave * 0.65f;
                _bars[i].color = c;
                _bars[i].rectTransform.localScale = Vector3.one * (0.7f + wave * 0.5f);
            }

            _shownFor += Time.unscaledDeltaTime;
            if (_shownFor > MAX_SHOWN) Hide();

            if (_fadeOut >= 0f)
            {
                _fadeOut -= Time.unscaledDeltaTime;
                _group.alpha = Mathf.Clamp01(_fadeOut / FADE);
                if (_fadeOut <= 0f) gameObject.SetActive(false);
            }
        }

        private static Image NewImage(Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject("Image", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static Text NewText(Transform parent, string text, Font font, int size, Vector2 pos, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(1600f, 90f);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.color = color;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }
    }
}
