using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// The card that pops up after the Gumball Machine: a big gumball in the
    /// gadget's color, its name and what it does, with EQUIP / OK.
    /// </summary>
    public class GumballReveal : OverlayPanel
    {
        private static GumballReveal _instance;
        private static Sprite _circle;

        private GadgetType _gadget;
        private Image _ball, _shine;
        private Text _name, _info, _equipped, _dlcTag;
        private float _popTime;

        protected override string Title { get { return "NEW GADGET!"; } }
        protected override Vector2 PanelSize { get { return new Vector2(900f, 640f); } }

        /// <summary>Shows the card after a short beat, so the gumball can roll out first.</summary>
        public static void ShowAfterDrop(GadgetType got)
        {
            if (KaleidoscopeEndlessPlugin.Instance != null)
                KaleidoscopeEndlessPlugin.Instance.StartCoroutine(ShowLater(got));
        }

        private static IEnumerator ShowLater(GadgetType got)
        {
            yield return new WaitForSeconds(0.9f);
            if (!HubMode.Active) yield break;
            var panel = GetOrCreate(ref _instance, "KE_GumballReveal");
            panel._gadget = got;
            panel.Show();
        }

        protected override void BuildContent(Transform p)
        {
            _ball = MakeImage(p, new Vector2(0f, 150f), new Vector2(170f, 170f), Color.white);
            _ball.sprite = Circle();
            _shine = MakeImage(_ball.transform, new Vector2(-35f, 35f), new Vector2(45f, 45f), new Color(1f, 1f, 1f, 0.55f));
            _shine.sprite = Circle();

            _dlcTag = Fit(MakeText(p, "", 24, TextAnchor.MiddleCenter, new Vector2(0f, -30f), new Vector2(700f, 36f), Color.white)); // under the name
            _name = Fit(MakeText(p, "", 50, TextAnchor.MiddleCenter, new Vector2(0f, 20f), new Vector2(800f, 70f), Color.white));
            _info = Fit(MakeText(p, "", 26, TextAnchor.UpperCenter, new Vector2(0f, -135f), new Vector2(780f, 140f), Dim));
            _equipped = Fit(MakeText(p, "", 26, TextAnchor.MiddleCenter, new Vector2(0f, -215f), new Vector2(780f, 40f), Accent));

            AddButton(p, new Vector2(-200f, -275f), "EQUIP", () =>
            {
                Gadgets.Selected = _gadget;
                Click();
            });
            AddButton(p, new Vector2(200f, -275f), "OK", () =>
            {
                Hide();
                if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            });
        }

        protected override void OnShown()
        {
            Color c = Gadgets.GumballColor(_gadget);
            _ball.color = c;
            _name.text = Gadgets.Name(_gadget).ToUpper();
            _name.color = c;
            _info.text = Gadgets.Description(_gadget) + "\n\nPress 1 during an Endless run to use it.";
            _dlcTag.text = Gadgets.IsDlc(_gadget) ? "* RAINBOW GUMBALL: " + Gadgets.DLC_VERSION + " DLC GADGET *" : "";
            _popTime = 0f;
        }

        protected override void Update()
        {
            base.Update();
            if (!Visible) return;

            // Pop in (overshoot, then settle), then a gentle bob.
            _popTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_popTime / 0.45f);
            float pop = t < 1f ? Mathf.Sin(t * Mathf.PI * 0.75f) / Mathf.Sin(Mathf.PI * 0.75f) : 1f;
            float bob = Mathf.Sin(_popTime * 2.5f) * 0.03f;
            _ball.rectTransform.localScale = Vector3.one * (pop + (t >= 1f ? bob : 0f));

            if (Gadgets.IsDlc(_gadget))
            {
                Color rc = Gadgets.Rainbow(Time.unscaledTime * 0.35f);
                _ball.color = rc;
                _name.color = rc;
                _dlcTag.color = Gadgets.Rainbow(Time.unscaledTime * 0.35f + 0.5f);
            }

            _equipped.text = Gadgets.Selected == _gadget ? "Equipped! It's ready for your next Endless run." : "";
        }

        private static Sprite Circle()
        {
            if (_circle != null) return _circle;
            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.ARGB32, false);
            float r = N / 2f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d))); // 1px soft edge
                }
            tex.Apply();
            _circle = Sprite.Create(tex, new Rect(0f, 0f, N, N), new Vector2(0.5f, 0.5f));
            return _circle;
        }
    }
}
