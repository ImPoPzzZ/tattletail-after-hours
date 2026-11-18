using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// The text box at the bottom of the screen: who is talking, and what they say, typed out a few letters at a time.
    /// Click it or press SPACE: the first press shows the whole line, the next one goes to the next line.
    /// (RcMenu.AddTalk builds the pieces; this class is what they do.)
    /// </summary>
    public class RcTalk
    {
        public GameObject Holder;
        public RectTransform Rect;                   // the box itself (what a click has to land on)
        public Text Name, Body, Hint, Letter;
        public Image Portrait, TabEdge, TabFill;     // (the name tab: gone when nobody is named)

        private const float CPS = 70f;               // letters a second
        private readonly List<string> _who = new List<string>(), _say = new List<string>();
        private int _i, _shown;
        private float _acc;

        public bool Typing { get { return _i < _say.Count && _shown < _say[_i].Length; } }
        public bool HasMore { get { return _i < _say.Count - 1; } }
        public bool Empty { get { return _say.Count == 0; } }
        /// <summary>The last line is all on screen.</summary>
        public bool Done { get { return _say.Count > 0 && !HasMore && !Typing; } }

        /// <summary>Starts over with one line.</summary>
        public void Say(string who, string text)
        {
            _who.Clear(); _say.Clear();
            _i = 0;
            Add(who, text);
        }

        /// <summary>One more line, after the ones already there.</summary>
        public void Add(string who, string text)
        {
            _who.Add(who); _say.Add(text);
            if (_say.Count == 1) Begin();
        }

        public void Clear()
        {
            _who.Clear(); _say.Clear();
            _i = 0;
            if (Holder != null) Holder.SetActive(false);
        }

        private void Begin()
        {
            _shown = 0; _acc = 0f;
            Holder.SetActive(true);
            string who = _who[_i];
            Color c = ColorOf(who);
            Name.text = who.Length > 0 ? who.ToUpper() : "";
            if (TabEdge != null) { TabEdge.gameObject.SetActive(who.Length > 0); TabFill.gameObject.SetActive(who.Length > 0); }
            Name.color = Color.Lerp(c, Color.white, 0.25f);
            Portrait.color = new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f, 1f);
            Letter.text = who.Length > 0 ? who.Substring(0, 1).ToUpper() : "!";
            Letter.color = Color.Lerp(c, Color.white, 0.35f);
            Body.text = "";
            Hint.text = "";
        }

        /// <summary>Every frame while the box is up; advance = the player clicked it or pressed SPACE.</summary>
        public void Tick(bool advance)
        {
            if (_i >= _say.Count) return;
            string s = _say[_i];
            if (_shown < s.Length)
            {
                _acc += Time.unscaledDeltaTime * CPS;
                int n = (int)_acc;
                if (n > 0) { _shown = Mathf.Min(s.Length, _shown + n); _acc -= n; }
                if (advance) _shown = s.Length;
                Body.text = s.Substring(0, _shown);
                return;
            }
            Body.text = s;
            Hint.text = HasMore && Mathf.Repeat(Time.unscaledTime, 1.2f) < 0.8f ? "click or SPACE" : "";
            if (advance && HasMore) { _i++; Begin(); }
        }

        /// <summary>Each of the kids, and everyone else, has a color: their name, and the square beside the words.</summary>
        public static Color ColorOf(string who)
        {
            switch (who.ToLower())
            {
                case "june": return new Color(0.95f, 0.58f, 0.28f);
                case "theo": return new Color(0.55f, 0.85f, 0.45f);
                case "moss": return new Color(0.45f, 0.65f, 0.95f);
                case "pip": return new Color(0.95f, 0.8f, 0.3f);
                case "waygetter": return new Color(0.80f, 0.82f, 0.85f);
                case "mama": return new Color(0.90f, 0.25f, 0.25f);
                case "patches": return new Color(0.85f, 0.70f, 0.55f);
                case "display model": return new Color(0.95f, 0.75f, 0.35f);
                case "hall monitor": return new Color(0.55f, 0.80f, 0.85f);
                case "papa": return new Color(0.75f, 0.50f, 0.30f);
                case "a toy": return new Color(0.80f, 0.55f, 0.85f);
                case "radio": return new Color(0.60f, 0.85f, 0.70f);
                case "you": return new Color(0.95f, 0.95f, 0.95f);
                default: return new Color(0.70f, 0.66f, 0.58f);
            }
        }
    }
}
