using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// The "the game is updating" notice. While the title screen's loading cover is up it sits big under the cover's coloured blocks (with a progress
    /// bar while files download); anywhere else it is a small note in the bottom-left corner. AutoUpdate calls Set / SetProgress.
    /// </summary>
    public class UpdateIndicator : MonoBehaviour
    {
        private static string _text;
        private static float _until;      // unscaled time the note goes away (0 = stays until replaced)
        private static float _progress = -1f;   // 0..1, or -1 = no bar

        /// <summary>Shows a note. seconds = 0 keeps it up until the next Set / Clear.</summary>
        public static void Set(string text, float seconds)
        {
            _text = text;
            _until = seconds > 0f ? Time.unscaledTime + seconds : 0f;
            if (seconds > 0f || text == null) _progress = -1f;
        }

        public static void SetProgress(float p) { _progress = Mathf.Clamp01(p); }

        public static void Clear() { _text = null; _progress = -1f; }

        private RectTransform _root;
        private Text _label;
        private RectTransform _barBack, _barFill;
        private bool _big;
        private bool _built;

        private void Start()
        {
            Build();
        }

        private void Build()
        {
            if (_built) return;
            _built = true;
            Font font = MainMenuButtonInjector.RealFont;
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var cgo = new GameObject("KE_UpdateIndicatorCanvas", typeof(RectTransform));
            cgo.transform.SetParent(transform, false);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32100;   // above the loading cover (32000)
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var rootGo = new GameObject("Root", typeof(RectTransform));
            rootGo.transform.SetParent(cgo.transform, false);
            _root = (RectTransform)rootGo.transform;

            var tgo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            tgo.transform.SetParent(_root, false);
            var trt = (RectTransform)tgo.transform;
            trt.anchorMin = new Vector2(0f, 0f); trt.anchorMax = new Vector2(1f, 1f);
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            _label = tgo.GetComponent<Text>();
            _label.font = font;
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            _label.verticalOverflow = VerticalWrapMode.Overflow;
            _label.raycastTarget = false;
            var shadow = tgo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(2f, -2f);

            _barBack = NewBar(_root, "BarBack", new Color(1f, 1f, 1f, 0.18f));
            _barFill = NewBar(_barBack, "BarFill", new Color(0.55f, 0.95f, 0.65f, 0.95f));
            _barFill.anchorMin = new Vector2(0f, 0f); _barFill.anchorMax = new Vector2(0f, 1f);
            _barFill.offsetMin = _barFill.offsetMax = Vector2.zero;
            _big = true;
            Layout(false);
            _root.gameObject.SetActive(false);
        }

        private static RectTransform NewBar(Transform parent, string name, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return (RectTransform)go.transform;
        }

        /// <summary>big = under the loading cover's blocks; small = the corner note.</summary>
        private void Layout(bool big)
        {
            if (_big == big && _root.sizeDelta.x > 0f) return;
            _big = big;
            if (big)
            {
                _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
                _root.pivot = new Vector2(0.5f, 0.5f);
                _root.sizeDelta = new Vector2(1500f, 80f);
                _root.anchoredPosition = new Vector2(0f, -190f);
                _label.fontSize = 34;
                _label.alignment = TextAnchor.UpperCenter;
                _label.color = new Color(0.8f, 1f, 0.85f, 1f);
                _barBack.anchorMin = _barBack.anchorMax = new Vector2(0.5f, 0.5f);
                _barBack.pivot = new Vector2(0.5f, 1f);
                _barBack.sizeDelta = new Vector2(720f, 22f);
                _barBack.anchoredPosition = new Vector2(0f, -54f);
            }
            else
            {
                _root.anchorMin = _root.anchorMax = new Vector2(0f, 0f);
                _root.pivot = new Vector2(0f, 0f);
                _root.sizeDelta = new Vector2(900f, 50f);
                _root.anchoredPosition = new Vector2(24f, 24f);
                _label.fontSize = 24;
                _label.alignment = TextAnchor.MiddleLeft;
                _label.color = new Color(0.85f, 1f, 0.9f, 0.95f);
                _barBack.anchorMin = _barBack.anchorMax = new Vector2(0f, 0f);
                _barBack.pivot = new Vector2(0f, 1f);
                _barBack.sizeDelta = new Vector2(360f, 8f);
                _barBack.anchoredPosition = new Vector2(0f, -2f);
            }
        }

        private void Update()
        {
            if (!_built) Build();
            bool show = !string.IsNullOrEmpty(_text) && (_until <= 0f || Time.unscaledTime < _until);
            if (!show)
            {
                if (_root.gameObject.activeSelf) _root.gameObject.SetActive(false);
                return;
            }
            if (!_root.gameObject.activeSelf) _root.gameObject.SetActive(true);
            Layout(LoadingCover.Up);
            _label.text = _text;
            // The bar: real progress while files download; a sliding block while it is still checking (a note that stays up with no progress yet).
            bool real = _progress >= 0f;
            bool bar = real || _until <= 0f;
            if (_barBack.gameObject.activeSelf != bar) _barBack.gameObject.SetActive(bar);
            if (real)
            {
                _barFill.anchorMin = new Vector2(0f, 0f);
                _barFill.anchorMax = new Vector2(Mathf.Clamp01(_progress), 1f);
            }
            else if (bar)
            {
                float x = Mathf.PingPong(Time.unscaledTime * 0.6f, 0.75f);
                _barFill.anchorMin = new Vector2(x, 0f);
                _barFill.anchorMax = new Vector2(x + 0.25f, 1f);
            }
        }
    }
}
