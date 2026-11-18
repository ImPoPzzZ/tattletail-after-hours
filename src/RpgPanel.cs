using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// The RPG look for Recalled's menus (a panel can opt in with Rpg): a wooden frame with a gold edge and a title banner,
    /// buttons drawn as plates that light up under the mouse, and framed boxes for text. Every other menu is unchanged.
    /// </summary>
    public abstract partial class OverlayPanel
    {
        protected static readonly Color Wood = new Color(0.20f, 0.12f, 0.07f, 1f);
        protected static readonly Color GoldDim = new Color(0.62f, 0.44f, 0.20f, 1f);
        protected static readonly Color Gold = new Color(0.90f, 0.68f, 0.30f, 1f);
        protected static readonly Color Lantern = new Color(1f, 0.84f, 0.48f, 1f);
        protected static readonly Color Ink = new Color(0.115f, 0.08f, 0.06f, 0.985f);
        protected static readonly Color InsetFill = new Color(0.06f, 0.04f, 0.03f, 0.96f);
        protected static readonly Color Plate = new Color(0.23f, 0.155f, 0.10f, 1f);
        protected static readonly Color PlateHot = new Color(0.40f, 0.26f, 0.13f, 1f);
        protected static readonly Color Cream = new Color(0.96f, 0.92f, 0.82f, 1f);

        private readonly Dictionary<Text, Image> _plates = new Dictionary<Text, Image>();
        internal int BuiltWorld = -1;

        /// <summary>True: the panel is drawn in the RPG style.</summary>
        protected virtual bool Rpg { get { return false; } }

        /// <summary>Where the panel sits (RPG panels only): up a bit when a text box takes the bottom of the screen.</summary>
        protected virtual Vector2 PanelOffset { get { return Vector2.zero; } }

        /// <summary>The whole screen (the panel's parent): where the bottom text box goes.</summary>
        protected Transform Root { get { return _panel.transform; } }

        protected Font UiFont { get { return _font; } }

        /// <summary>Draws the frame and the title; returns what the menu's own content goes on (centered on the panel).</summary>
        private Transform BuildRpgFrame(Transform root)
        {
            Vector2 s = PanelSize, o = PanelOffset;
            MakeImage(root, o, s + new Vector2(34f, 34f), new Color(0f, 0f, 0f, 0.5f));
            MakeImage(root, o, s + new Vector2(18f, 18f), Wood);
            MakeImage(root, o, s + new Vector2(10f, 10f), GoldDim);
            MakeImage(root, o, s, Ink);
            Rule(root, o, s - new Vector2(26f, 26f), 2f, new Color(GoldDim.r, GoldDim.g, GoldDim.b, 0.6f));
            for (int i = 0; i < 4; i++)
            {
                Vector2 c = new Vector2((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f);
                Diamond(root, o + new Vector2(c.x * (s.x / 2f - 13f), c.y * (s.y / 2f - 13f)), 18f, Gold);
            }

            Vector2 t = o + new Vector2(0f, s.y / 2f - 50f);
            MakeImage(root, t, new Vector2(760f, 80f), GoldDim);
            MakeImage(root, t, new Vector2(752f, 72f), Plate);
            Diamond(root, t + new Vector2(-396f, 0f), 20f, Gold);
            Diamond(root, t + new Vector2(396f, 0f), 20f, Gold);
            MakeText(root, Title, 46, TextAnchor.MiddleCenter, t, new Vector2(740f, 70f), Lantern);

            var go = new GameObject("Content", typeof(RectTransform));
            go.transform.SetParent(root, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = o;
            rt.sizeDelta = Vector2.zero;
            return go.transform;
        }

        /// <summary>A dark framed box behind some text (draw it before the text so the text lands on top).</summary>
        protected void Inset(Transform p, Vector2 pos, Vector2 size)
        {
            MakeImage(p, pos, size + new Vector2(6f, 6f), new Color(GoldDim.r, GoldDim.g, GoldDim.b, 0.8f));
            MakeImage(p, pos, size, InsetFill);
        }

        /// <summary>A hollow rectangle (four thin lines).</summary>
        protected static void Rule(Transform p, Vector2 pos, Vector2 size, float w, Color c)
        {
            MakeImage(p, pos + new Vector2(0f, size.y / 2f), new Vector2(size.x, w), c);
            MakeImage(p, pos - new Vector2(0f, size.y / 2f), new Vector2(size.x, w), c);
            MakeImage(p, pos + new Vector2(size.x / 2f, 0f), new Vector2(w, size.y), c);
            MakeImage(p, pos - new Vector2(size.x / 2f, 0f), new Vector2(w, size.y), c);
        }

        protected static void Diamond(Transform p, Vector2 pos, float size, Color c)
        {
            Image d = MakeImage(p, pos, new Vector2(size, size), c);
            d.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        /// <summary>The plate behind a button.</summary>
        private void AttachPlate(Image plate, Text t)
        {
            var edge = plate.gameObject.AddComponent<Outline>();
            edge.effectColor = GoldDim;
            edge.effectDistance = new Vector2(2f, -2f);
            _plates[t] = plate;
        }

        /// <summary>Plates follow their button: hidden when it's hidden, lit when the mouse is on it.</summary>
        private void HoverPlates()
        {
            foreach (var b in _buttons)
            {
                Image plate;
                if (!_plates.TryGetValue(b.Key, out plate)) continue;
                bool on = b.Key.gameObject.activeSelf;
                if (plate.gameObject.activeSelf != on) plate.gameObject.SetActive(on);
                if (!on) continue;
                bool hot = Hit(b.Key.rectTransform);
                plate.color = hot ? PlateHot : Plate;
                plate.GetComponent<Outline>().effectColor = hot ? Gold : GoldDim;
            }
        }
    }
}
