using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// "EARLY ACCESS V3.1" / "PUBLIC VERSION 3.1" in the bottom-right corner,
    /// only while standing in the Arcade (it lives on the hub controller, so
    /// it goes away with the hub). Flip EARLY_ACCESS to false when shipping.
    /// </summary>
    /// <summary>The mod's Ko-fi: shown on the loading screen and in the Arcade corner, opened from Options. Only ever a tip jar — nothing in the mod is locked behind it.</summary>
    public static class Support
    {
        public const string URL = "https://ko-fi.com/impopzzz";
        public const string LINE = "Enjoying After Hours? Support the mod: ko-fi.com/impopzzz";
        public static void Open() { Application.OpenURL(URL); }

        public const string DISCORD_URL = "https://discord.gg/PjSQQrhsMx";
        public const string DISCORD_LINE = "Join Our Discord Server: discord.gg/PjSQQrhsMx";
        public static void OpenDiscord() { Application.OpenURL(DISCORD_URL); }
    }

    public class VersionTag : MonoBehaviour
    {
        public const string VERSION = "4.0";

        /// <summary>
        /// Who you can play online with (Versus, Air Hockey): only players with the same NET_VERSION. It follows
        /// VERSION, so a release changes one line; a hotfix that changes nothing online can set it to the version
        /// it still plays with instead (like 3.2.1 did with 3.2).
        /// </summary>
        public const string NET_VERSION = VERSION;
        public const bool EARLY_ACCESS = false; // release checklist: false for the public zip

        /// <summary>A private build never updates itself (and never gets replaced by the older public version): AutoUpdate stays off.</summary>
        public const bool PRIVATE_BUILD = false;

        public static string Label { get { return EARLY_ACCESS ? "EARLY ACCESS V" + VERSION : "PUBLIC VERSION " + VERSION; } }

        private GameObject _canvas;

        // Built in Start, never Awake: a Canvas built while Unity is still creating the object crashes 5.5.
        private void Start()
        {
            _canvas = new GameObject("KE_VersionTag", typeof(RectTransform));
            _canvas.transform.SetParent(transform, false);
            var canvas = _canvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // under the hub's screens (2100) and the loading screen
            var scaler = _canvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(_canvas.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-24f, 18f);
            rt.sizeDelta = new Vector2(600f, 40f);
            var t = go.GetComponent<Text>();
            t.font = MainMenuButtonInjector.RealFont != null ? MainMenuButtonInjector.RealFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize = 24;
            t.alignment = TextAnchor.LowerRight;
            t.color = new Color(1f, 1f, 1f, 0.6f);
            t.raycastTarget = false;
            t.text = Label;
            go.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);

            var sup = Object.Instantiate(go, _canvas.transform, false); // the Ko-fi line, just above
            ((RectTransform)sup.transform).anchoredPosition = new Vector2(-24f, 50f);
            ((RectTransform)sup.transform).sizeDelta = new Vector2(900f, 34f);
            var st = sup.GetComponent<Text>();
            st.fontSize = 20;
            st.color = new Color(1f, 0.75f, 0.85f, 0.6f);
            st.text = Support.LINE + "  (Options)";

            var dis = Object.Instantiate(sup, _canvas.transform, false); // and the Discord, above that
            ((RectTransform)dis.transform).anchoredPosition = new Vector2(-24f, 80f);
            var dt = dis.GetComponent<Text>();
            dt.color = new Color(0.65f, 0.7f, 1f, 0.75f);
            dt.text = Support.DISCORD_LINE + "  (Options)";

            _canvas.SetActive(false);
        }

        private void Update()
        {
            // Only once the player is actually standing in the room (after the loading screen).
            bool show = HubController.Instance != null && HubController.Instance.Built;
            if (_canvas != null && _canvas.activeSelf != show) _canvas.SetActive(show);
        }
    }
}
