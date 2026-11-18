using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Recalled's HUD: your health and experience (bottom left), your two weapons (bottom right), the quest you're
    /// on and which way it is (top right), what you can use here (middle), what you just picked up (right), a
    /// boss's health (top), the damage you deal (over the toys), the crosshair, and under it how far your
    /// slingshot is drawn.
    /// </summary>
    public class RcHud : MonoBehaviour
    {
        private Canvas _canvas;
        private RectTransform _rootRt;
        private Font _font;
        private Image _hp, _shield, _xp, _boss, _hurt, _black, _charge, _chargeBack, _vignette;
        private Text _hpText, _level, _w1, _w2, _questTitle, _questLine, _questWhere, _prompt, _toast, _toastSub, _bossName, _diedText;
        private float _whereAt, _dark, _downClock = -1f;
        // The quest arrow: it points along the way you'd walk (round fences, through gates, to the stairs), not through walls.
        private RectTransform _arrow;
        private Text _arrowText;
        private RcField _field;
        private Vector3 _wayPoint, _fieldGoal;
        private bool _wayOn;
        private int _fieldSpots;
        private float _wayAt, _walk;
        private Text _team;
        private class Mark { public Text T; public Vector3 World; public float Born = -100f; }
        private readonly List<Mark> _marks = new List<Mark>();
        private int _markNext;
        private GameObject _bossGo;
        private readonly List<string> _feed = new List<string>();
        private readonly List<float> _feedAt = new List<float>();
        private Text _feedText;
        private float _toastAt = -10f, _hurtAt = -10f, _diedAt = -10f, _shakeUntil;

        // The voices: who is talking and what they say, in a text box at the bottom of the screen.
        private struct SubLine { public string Who, Text; public bool Keep; public float Dies; }
        private readonly List<SubLine> _subs = new List<SubLine>();
        private GameObject _subGo;
        private CanvasGroup _subGroup;
        private Text _subName, _subBody, _subLetter;
        private Image _subFace, _subTab, _subTabEdge;
        private string _subWho = "", _subText = "";
        private float _subStart, _subEnd;

        private class Num { public Text T; public Vector3 World; public float Born; }
        private readonly List<Num> _nums = new List<Num>();
        private int _numNext;

        private static readonly Color Accent = new Color(0.95f, 0.7f, 0.15f), Dim = new Color(0.75f, 0.75f, 0.78f);

        private void Start()
        {
            _font = MainMenuButtonInjector.RealFont != null ? MainMenuButtonInjector.RealFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
            var go = new GameObject("KE_RcHud", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 60;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            var root = new GameObject("Root", typeof(RectTransform));
            root.transform.SetParent(go.transform, false);
            _rootRt = (RectTransform)root.transform;
            _rootRt.anchorMin = Vector2.zero; _rootRt.anchorMax = Vector2.one; _rootRt.offsetMin = _rootRt.offsetMax = Vector2.zero;
            Transform r = root.transform;

            // The edges of the screen are dark, and close in when it's dangerous.
            _vignette = Img(r, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000f, 4000f), new Color(0f, 0f, 0f, 0.8f));
            _vignette.rectTransform.anchorMin = Vector2.zero; _vignette.rectTransform.anchorMax = Vector2.one;
            _vignette.rectTransform.offsetMin = _vignette.rectTransform.offsetMax = Vector2.zero;
            _vignette.sprite = VignetteSprite();
            _hurt = Img(r, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000f, 4000f), new Color(0.7f, 0f, 0f, 0f));

            // Crosshair.
            Img(r, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(5f, 5f), new Color(1f, 1f, 1f, 0.75f));
            // ...and under it, how far a held click has charged.
            _chargeBack = Img(r, new Vector2(0.5f, 0.5f), new Vector2(0f, -34f), new Vector2(126f, 12f), new Color(0f, 0f, 0f, 0.6f));
            _charge = Bar(r, new Vector2(0.5f, 0.5f), new Vector2(-60f, -34f), new Vector2(120f, 6f), Color.white);

            // Health, shield, experience.
            Vector2 bl = new Vector2(0f, 0f);
            Img(r, bl, new Vector2(210f, 70f), new Vector2(344f, 30f), new Color(0f, 0f, 0f, 0.6f));
            _hp = Bar(r, bl, new Vector2(42f, 70f), new Vector2(336f, 22f), new Color(0.78f, 0.13f, 0.12f));
            _shield = Bar(r, bl, new Vector2(42f, 90f), new Vector2(336f, 6f), new Color(1f, 0.85f, 0.4f));
            _hpText = Txt(r, bl, new Vector2(210f, 70f), new Vector2(336f, 26f), 20, TextAnchor.MiddleCenter, Color.white);
            Img(r, bl, new Vector2(210f, 44f), new Vector2(344f, 12f), new Color(0f, 0f, 0f, 0.6f));
            _xp = Bar(r, bl, new Vector2(42f, 44f), new Vector2(336f, 6f), Accent);
            _level = Txt(r, bl, new Vector2(210f, 104f), new Vector2(336f, 30f), 24, TextAnchor.MiddleLeft, Accent);

            // Your two weapons.
            Vector2 br = new Vector2(1f, 0f);
            _w1 = Txt(r, br, new Vector2(-230f, 84f), new Vector2(420f, 32f), 24, TextAnchor.MiddleRight, Color.white);
            _w2 = Txt(r, br, new Vector2(-230f, 50f), new Vector2(420f, 32f), 24, TextAnchor.MiddleRight, Color.white);

            // The quest.
            Vector2 tr = new Vector2(1f, 1f);
            _questTitle = Txt(r, tr, new Vector2(-250f, -50f), new Vector2(460f, 34f), 26, TextAnchor.MiddleRight, Accent);
            _questLine = Txt(r, tr, new Vector2(-250f, -84f), new Vector2(460f, 30f), 20, TextAnchor.MiddleRight, Color.white);
            _questWhere = Txt(r, tr, new Vector2(-250f, -112f), new Vector2(460f, 26f), 18, TextAnchor.MiddleRight, Dim);

            // The quest arrow, top middle: a shaft and a head, turned to point the way.
            var arrow = new GameObject("Arrow", typeof(RectTransform));
            arrow.transform.SetParent(r, false);
            _arrow = (RectTransform)arrow.transform;
            _arrow.anchorMin = _arrow.anchorMax = new Vector2(0.5f, 1f);
            _arrow.anchoredPosition = new Vector2(0f, -150f);
            _arrow.sizeDelta = Vector2.zero;
            Vector2 mid = new Vector2(0.5f, 0.5f);
            Img(_arrow, mid, new Vector2(0f, -9f), new Vector2(16f, 46f), new Color(0f, 0f, 0f, 0.7f));
            Img(_arrow, mid, new Vector2(0f, 18f), new Vector2(34f, 34f), new Color(0f, 0f, 0f, 0.7f)).rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Img(_arrow, mid, new Vector2(0f, -9f), new Vector2(10f, 40f), Accent);
            Img(_arrow, mid, new Vector2(0f, 18f), new Vector2(27f, 27f), Accent).rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            _arrowText = Txt(r, new Vector2(0.5f, 1f), new Vector2(0f, -205f), new Vector2(500f, 26f), 20, TextAnchor.MiddleCenter, Color.white);

            // What you just got.
            _feedText = Txt(r, new Vector2(1f, 0.5f), new Vector2(-270f, 0f), new Vector2(500f, 260f), 20, TextAnchor.MiddleRight, Dim);

            _prompt = Txt(r, new Vector2(0.5f, 0.5f), new Vector2(0f, -210f), new Vector2(900f, 40f), 28, TextAnchor.MiddleCenter, Color.white);
            _toast = Txt(r, new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(1400f, 80f), 64, TextAnchor.MiddleCenter, Accent);
            _toastSub = Txt(r, new Vector2(0.5f, 1f), new Vector2(0f, -256f), new Vector2(1400f, 40f), 26, TextAnchor.MiddleCenter, Color.white);

            // A boss's health.
            _bossGo = new GameObject("Boss", typeof(RectTransform));
            _bossGo.transform.SetParent(r, false);
            var brt = (RectTransform)_bossGo.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 1f); brt.anchoredPosition = new Vector2(0f, -80f); brt.sizeDelta = Vector2.zero;
            Img(_bossGo.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(808f, 26f), new Color(0f, 0f, 0f, 0.7f));
            _boss = Bar(_bossGo.transform, new Vector2(0.5f, 0.5f), new Vector2(-400f, 0f), new Vector2(800f, 18f), new Color(0.85f, 0.1f, 0.1f));
            _bossName = Txt(_bossGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(800f, 36f), 30, TextAnchor.MiddleCenter, Color.white);
            _bossGo.SetActive(false);

            for (int i = 0; i < 16; i++)
            {
                Text t = Txt(r, Vector2.zero, Vector2.zero, new Vector2(260f, 36f), 28, TextAnchor.MiddleCenter, Color.white);
                t.gameObject.SetActive(false);
                _nums.Add(new Num { T = t, Born = -10f });
            }

            // Friends in town (top left), and the marks they leave.
            _team = Txt(r, new Vector2(0f, 1f), new Vector2(250f, -110f), new Vector2(440f, 160f), 22, TextAnchor.UpperLeft, Color.white);
            for (int i = 0; i < 5; i++)
            {
                Text t = Txt(r, Vector2.zero, Vector2.zero, new Vector2(400f, 60f), 22, TextAnchor.MiddleCenter, new Color(0.4f, 0.9f, 1f));
                t.gameObject.SetActive(false);
                _marks.Add(new Mark { T = t });
            }

            BuildSub(r);
            _black = Img(r, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000f, 4000f), new Color(0f, 0f, 0f, 0f));
            _diedText = Txt(r, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 160f), 58, TextAnchor.MiddleCenter, new Color(0.9f, 0.2f, 0.15f, 0f));
            if (_pendingTitle != null) { Toast(_pendingTitle, _pendingSub); _pendingTitle = null; }
        }

        private void BuildSub(Transform r)
        {
            _subGo = new GameObject("Voice", typeof(RectTransform), typeof(CanvasGroup));
            _subGo.transform.SetParent(r, false);
            var rt = (RectTransform)_subGo.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f); rt.anchoredPosition = Vector2.zero; rt.sizeDelta = Vector2.zero;
            _subGroup = _subGo.GetComponent<CanvasGroup>();
            Transform p = _subGo.transform;
            Vector2 mid = new Vector2(0.5f, 0.5f), c = new Vector2(0f, 135f);
            Color wood = new Color(0.20f, 0.12f, 0.07f), gold = new Color(0.62f, 0.44f, 0.20f);
            Img(p, mid, c, new Vector2(1120f, 190f), new Color(0f, 0f, 0f, 0.4f));
            Img(p, mid, c, new Vector2(1104f, 174f), wood);
            Img(p, mid, c, new Vector2(1096f, 166f), gold);
            Img(p, mid, c, new Vector2(1086f, 156f), new Color(0.115f, 0.08f, 0.06f, 0.95f));
            Vector2 face = c + new Vector2(-540f + 85f, 0f);
            Img(p, mid, face, new Vector2(122f, 122f), gold);
            _subFace = Img(p, mid, face, new Vector2(114f, 114f), new Color(0.3f, 0.2f, 0.12f));
            _subLetter = Txt(p, mid, face, new Vector2(114f, 114f), 70, TextAnchor.MiddleCenter, Color.white);
            Vector2 tab = c + new Vector2(-540f + 230f, 78f);
            _subTabEdge = Img(p, mid, tab, new Vector2(300f, 42f), gold);
            _subTab = Img(p, mid, tab, new Vector2(294f, 36f), new Color(0.23f, 0.155f, 0.10f));
            _subName = Txt(p, mid, tab, new Vector2(286f, 36f), 24, TextAnchor.MiddleCenter, Color.white);
            _subBody = Txt(p, mid, c + new Vector2(80f, 0f), new Vector2(900f, 136f), 28, TextAnchor.UpperLeft, new Color(0.96f, 0.92f, 0.82f));
            _subBody.lineSpacing = 1.1f;
            _subBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _subGo.SetActive(false);
        }

        /// <summary>Someone says something: it's shown in the box at the bottom, in turn. Lines that aren't kept are dropped if they've waited too long to matter.</summary>
        public void Sub(string who, string text, bool keep)
        {
            if (text == null || text.Length == 0) return;
            _subs.Add(new SubLine { Who = who ?? "", Text = text, Keep = keep, Dies = Time.time + 6f });
            while (_subs.Count > 8) _subs.RemoveAt(0);
        }

        /// <summary>Is someone talking, or waiting to?</summary>
        public bool SubBusy { get { return _subs.Count > 0 || _subText.Length > 0; } }

        private void UpdateSub()
        {
            if (_subGo == null) return;
            bool hidden = OverlayPanel.AnyOpen || (Rc.Player != null && Rc.Player.Cinematic);
            if (_subText.Length == 0)
            {
                for (int i = _subs.Count - 1; i >= 0; i--) if (!_subs[i].Keep && Time.time > _subs[i].Dies) _subs.RemoveAt(i);
                if (hidden || _subs.Count == 0) { if (_subGo.activeSelf) _subGo.SetActive(false); return; }
                SubLine s = _subs[0];
                _subs.RemoveAt(0);
                _subWho = s.Who; _subText = s.Text;
                _subStart = Time.time;
                _subEnd = Time.time + s.Text.Length / 80f + 1.6f + Mathf.Min(7f, s.Text.Length * 0.04f);
                Color k = RcTalk.ColorOf(s.Who);
                _subFace.color = new Color(k.r * 0.45f, k.g * 0.45f, k.b * 0.45f);
                _subLetter.text = s.Who.Length > 0 ? s.Who.Substring(0, 1).ToUpper() : "!";
                _subLetter.color = Color.Lerp(k, Color.white, 0.35f);
                _subName.text = s.Who.ToUpper();
                _subName.color = Color.Lerp(k, Color.white, 0.25f);
                _subTab.gameObject.SetActive(s.Who.Length > 0); _subTabEdge.gameObject.SetActive(s.Who.Length > 0); _subName.gameObject.SetActive(s.Who.Length > 0);
                _subGo.SetActive(true);
            }
            if (hidden)   // (a menu is up, or the trailer is on: the line waits for you)
            {
                _subGroup.alpha = 0f;
                _subStart += Time.deltaTime; _subEnd += Time.deltaTime;
                return;
            }
            float age = Time.time - _subStart;
            int shown = Mathf.Min(_subText.Length, (int)(age * 80f));
            _subBody.text = _subText.Substring(0, shown);
            _subGroup.alpha = Mathf.Clamp01(age * 6f) * Mathf.Clamp01((_subEnd - Time.time) * 2.5f);
            if (Time.time >= _subEnd) { _subText = ""; _subWho = ""; }
        }

        private static Sprite VignetteSprite()
        {
            const int N = 128;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy) / 1.25f;          // a little wider than tall
                    float a = Mathf.Clamp01((d - 0.42f) / 0.58f);
                    px[y * N + x] = new Color32(0, 0, 0, (byte)(a * a * 255f));
                }
            t.SetPixels32(px);
            t.wrapMode = TextureWrapMode.Clamp;
            t.Apply();
            return Sprite.Create(t, new Rect(0f, 0f, N, N), new Vector2(0.5f, 0.5f));
        }

        private Text Txt(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color c)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var t = go.GetComponent<Text>();
            t.font = _font; t.fontSize = fontSize; t.alignment = align; t.color = c;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var o = go.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.85f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        private static Image Img(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color c)
        {
            var go = new GameObject("Image", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = c; img.raycastTarget = false;
            return img;
        }

        /// <summary>A bar that fills from its left edge (pos is its left end; scale x is how full).</summary>
        private static Image Bar(Transform parent, Vector2 anchor, Vector2 leftPos, Vector2 size, Color c)
        {
            Image img = Img(parent, anchor, leftPos, size, c);
            img.rectTransform.pivot = new Vector2(0f, 0.5f);
            return img;
        }

        private static void Fill(Image bar, float k) { bar.rectTransform.localScale = new Vector3(Mathf.Clamp01(k), 1f, 1f); }

        // ── What the game tells it ───────────────────────────────────────────
        public void Prompt(string text) { if (_prompt != null) _prompt.text = text ?? ""; }

        private string _pendingTitle, _pendingSub;

        public void Toast(string title, string sub)
        {
            if (_toast == null) { _pendingTitle = title; _pendingSub = sub; return; } // (the HUD is built a frame after it's added)
            _toast.text = title; _toastSub.text = sub ?? "";
            _toastAt = Time.time;
        }

        public void Feed(string line)
        {
            _feed.Add(line); _feedAt.Add(Time.time);
            while (_feed.Count > 7) { _feed.RemoveAt(0); _feedAt.RemoveAt(0); }
        }

        /// <summary>H: every control, in the feed.</summary>
        public void Controls()
        {
            Feed("WASD move   SHIFT run   SPACE jump   ALT roll   F flashlight");
            Feed("LEFT CLICK attack: hold to draw a slingshot, or to wind up a heavy swing");
            Feed("RIGHT CLICK (hold) block with your weapon; just before a hit lands it's a perfect block");
            Feed("An orange bar over a toy's head fills as it winds up a hit: roll (ALT) or block as it fills.   1 / 2 / Q switch weapon");
            Feed("E use   TAB backpack   J what to do next   M map   P friends   V ride a mount (you make one at the bench)");
            Feed("With friends:  G mark what you're looking at   5 6 7 8 say something");
        }

        public void Hurt() { _hurtAt = Time.time; }
        public void Shake(float seconds) { _shakeUntil = Time.time + seconds; }

        private string _diedLine = "";

        public void Died(bool helpable, bool carrying)
        {
            _diedAt = Time.time;
            _downClock = -1f;
            _diedLine = helpable ? "YOU'RE DOWN" : "THE TOYS GOT YOU" + (carrying ? "\n<size=26>What you were carrying stays where you fell.</size>" : "");
            if (_diedText != null) _diedText.text = _diedLine;
        }

        /// <summary>Down with a friend about: how long until you wake in the treehouse instead (-1: not that).</summary>
        public void DownClock(float seconds) { _downClock = seconds; }

        /// <summary>A mark in the world everyone looks at for a few seconds (a friend's ping, or what they said).</summary>
        public void Ping(Vector3 world, string who)
        {
            if (_marks.Count == 0) return;
            Mark m = _marks[_markNext];
            _markNext = (_markNext + 1) % _marks.Count;
            m.World = world; m.Born = Time.time;
            m.T.text = "v\n" + who;
            m.T.gameObject.SetActive(true);
        }

        public void Number(Vector3 world, string text, Color c)
        {
            if (_nums.Count == 0) return;
            Num n = _nums[_numNext];
            _numNext = (_numNext + 1) % _nums.Count;
            n.World = world + new Vector3(Random.Range(-0.2f, 0.2f), 0f, Random.Range(-0.2f, 0.2f));
            n.Born = Time.time;
            n.T.text = text; n.T.color = c;
            n.T.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (_canvas == null || Rc.Char == null || Rc.Player == null) return;
            RcCharacter c = Rc.Char;
            RcPlayer p = Rc.Player;

            Fill(_hp, p.Health / p.MaxHealth);
            Fill(_shield, p.Shield / 160f);
            _hpText.text = Mathf.CeilToInt(Mathf.Max(0f, p.Health)) + " / " + Mathf.RoundToInt(p.MaxHealth) + (c.Armor > 0 ? "      armor " + c.Armor : "");
            int into, need;
            c.LevelProgress(out into, out need);
            Fill(_xp, need > 0 ? into / (float)need : 1f);
            _level.text = "LEVEL " + c.Level + (need > 0 ? "" : "  (MAX)");

            _w1.text = WeaponLine(0); _w2.text = WeaponLine(1);
            _w1.color = p.Slot == 0 ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            _w2.color = p.Slot == 1 ? Color.white : new Color(1f, 1f, 1f, 0.45f);

            RcQuest q = c.CurrentQuest;
            if (q == null) { _questTitle.text = RcWorlds.Egypt ? "EGYPT COMPLETE" : "WORLD 1 COMPLETE"; _questLine.text = RcWorlds.Egypt ? "The Line goes on: the Recall Express is at Last Stop." : "The Recall Express is waiting at the junkyard station."; }
            else if (q.World != RcWorlds.Now) { _questTitle.text = q.Title; _questLine.text = q.World == RcWorlds.EGYPT ? "Ride the Recall Express to Egypt" : "Ride the Recall Express home"; }
            else
            {
                _questTitle.text = q.Title;
                if (c.QuestComplete) _questLine.text = "Done: hand it in at the treehouse";
                else if (q.Kind == RcQuestKind.Talk) _questLine.text = "Talk to the kids in the treehouse";
                else if (q.Kind == RcQuestKind.Bring) _questLine.text = RcData.Item(q.Target).Name + "  " + (c.Carried(q.Target) + RcCharacter.Count(c.Stash, q.Target)) + " / " + q.Count;
                else if (q.Kind == RcQuestKind.Craft) _questLine.text = "Craft: " + RcData.Item(q.Target).Name;
                else if (q.Kind == RcQuestKind.Boss) _questLine.text = "Break " + RcData.BossNames[q.Zone];
                else if (q.Kind == RcQuestKind.Reach) _questLine.text = "Get inside " + RcData.ZoneNames[q.Zone].ToLower();
                else if (q.Kind == RcQuestKind.Scene) _questLine.text = "Talk to the kids in the treehouse";
                else if (q.Kind == RcQuestKind.Station) _questLine.text = "Board the Recall Express, at the junkyard station";
                else _questLine.text = (q.Kind == RcQuestKind.Elite ? RcData.Enemy(q.Target).Name : RcData.ZoneNames[q.Zone]) + "  " + c.ActiveProgress + " / " + q.Count;
            }

            // Which way the job is (worked out a few times a second: it builds a string).
            if (Time.time >= _whereAt)
            {
                _whereAt = Time.time + 0.3f;
                Vector3 at = p.TownPos;
                string where = RcGuide.Where(c, at.x, at.y, at.z);
                _questWhere.text = (where.Length > 0 ? where + "      " : "") + "[J] what to do";
            }

            Arrow(c, p);

            float charge = p.Charge;
            bool charging = charge > 0f;
            if (_charge.gameObject.activeSelf != charging) { _charge.gameObject.SetActive(charging); _chargeBack.gameObject.SetActive(charging); }
            if (charging) { Fill(_charge, charge); _charge.color = charge >= 1f ? Accent : charge < 0.3f ? new Color(1f, 1f, 1f, 0.4f) : Color.white; }

            // The feed fades line by line.
            while (_feed.Count > 0 && Time.time - _feedAt[0] > 9f) { _feed.RemoveAt(0); _feedAt.RemoveAt(0); }
            _feedText.text = string.Join("\n", _feed.ToArray());

            float ta = Time.time - _toastAt;
            float a = ta < 0.3f ? ta / 0.3f : ta < 3f ? 1f : Mathf.Clamp01(1f - (ta - 3f) / 0.8f);
            _toast.color = new Color(Accent.r, Accent.g, Accent.b, a);
            _toastSub.color = new Color(1f, 1f, 1f, a);

            RcEnemy boss = Rc.Enemies != null ? Rc.Enemies.Boss : null;
            if (_bossGo.activeSelf != (boss != null)) _bossGo.SetActive(boss != null);
            if (boss != null) { Fill(_boss, boss.Hp / boss.MaxHp); _bossName.text = boss.Def.Name; }

            _hurt.color = new Color(0.7f, 0f, 0f, Mathf.Clamp01(0.32f - (Time.time - _hurtAt) * 0.9f) + (p.Health < p.MaxHealth * 0.25f && !p.Dead ? 0.08f + Mathf.Sin(Time.time * 5f) * 0.04f : 0f));
            // Down: the screen goes dark until you're up again (in the treehouse, or where you fell).
            _dark = Mathf.MoveTowards(_dark, p.Dead ? 1f : 0f, Time.deltaTime * 2f);
            _black.color = new Color(0f, 0f, 0f, _dark * (_downClock >= 0f ? 0.6f : 0.9f));
            _diedText.color = new Color(0.9f, 0.2f, 0.15f, _dark);
            if (p.Dead && _downClock >= 0f) _diedText.text = _diedLine + "\n<size=26>A friend can help you up.   " + Mathf.CeilToInt(_downClock) + " s   (SPACE: wake in the treehouse)</size>";

            // Friends: name, level, health.
            if (RcNet.Peers.Count == 0) { if (_team.text.Length > 0) _team.text = ""; }
            else
            {
                string team = "";
                for (int i = 0; i < RcNet.Peers.Count; i++)
                {
                    RcPeer f = RcNet.Peers[i];
                    team += f.Name + (!f.Live ? "   (on the way)" : "   level " + f.Level + "   " + (f.Down ? "<color=#ff5040>DOWN</color>" : Mathf.RoundToInt(f.Hp01 * 100f) + "%")) + "\n";
                    if (f.Live && f.Mission.Length > 0) team += "   <color=#bbbbbb>their job: " + f.Mission + "</color>\n";
                }
                if (_team.text != team) _team.text = team;
            }

            // The dark at the edges: heavier with toys about, and throbbing when you're low.
            int danger = Rc.Enemies != null ? Rc.Enemies.Danger : 0;
            float low = p.Health < p.MaxHealth * 0.3f && !p.Dead ? 0.12f + Mathf.Abs(Mathf.Sin(Time.time * 4.5f)) * 0.14f : 0f;
            _vignette.color = new Color(0f, 0f, 0f, Mathf.Clamp01(0.72f + danger * 0.1f + low));

            UpdateSub();
            _rootRt.anchoredPosition = Time.time < _shakeUntil ? new Vector2(Random.Range(-7f, 7f), Random.Range(-7f, 7f)) : Vector2.zero;

            // Damage numbers float up from where they were dealt.
            Camera cam = FactoryController.PlayerCam;
            float scale = _canvas.scaleFactor > 0.01f ? _canvas.scaleFactor : 1f;
            for (int i = 0; i < _marks.Count; i++)
            {
                Mark m = _marks[i];
                float age = Time.time - m.Born;
                bool on = age < 6f && cam != null;
                Vector3 sp = on ? cam.WorldToScreenPoint(m.World) : Vector3.zero;
                if (on && sp.z < 0.1f) on = false;
                if (m.T.gameObject.activeSelf != on) m.T.gameObject.SetActive(on);
                if (!on) continue;
                m.T.rectTransform.anchoredPosition = new Vector2(sp.x / scale, sp.y / scale + 30f);
                Color mc = m.T.color; mc.a = Mathf.Clamp01(6f - age); m.T.color = mc;
            }
            for (int i = 0; i < _nums.Count; i++)
            {
                Num n = _nums[i];
                float age = Time.time - n.Born;
                if (age > 0.9f || cam == null) { if (n.T.gameObject.activeSelf) n.T.gameObject.SetActive(false); continue; }
                Vector3 sp = cam.WorldToScreenPoint(n.World + Vector3.up * age * 0.9f);
                if (sp.z < 0.1f) { n.T.gameObject.SetActive(false); continue; }
                n.T.rectTransform.anchoredPosition = new Vector2(sp.x / scale, sp.y / scale);
                Color col = n.T.color; col.a = Mathf.Clamp01(1.4f - age * 1.6f); n.T.color = col;
            }
        }

        /// <summary>
        /// The arrow at the top: up means straight ahead of where you're looking. It points along the way you'd
        /// walk to the job, and says how long that walk is. (For the first second after the job changes, and
        /// anywhere there's no way on foot, it points straight at it.)
        /// </summary>
        private void Arrow(RcCharacter c, RcPlayer p)
        {
            Vector3 at = p.TownPos;
            float x, y, z; string name;
            bool on = RcGuide.Target(c, at.x, at.z, out x, out y, out z, out name) && !p.Dead;
            if (_arrow.gameObject.activeSelf != on) { _arrow.gameObject.SetActive(on); _arrowText.gameObject.SetActive(on); }
            if (!on) return;
            Vector3 goal = new Vector3(x, y, z);
            // How far every spot in town is from the job, on foot (RcField): worked out again when the job moves
            // (or the Facility's floors are added), a few thousand spots a frame. The arrow points downhill.
            RcNav nav = RcNav.Instance;
            if (_field == null) _field = new RcField(nav);
            if (!_field.Started || (goal - _fieldGoal).sqrMagnitude > 1f || nav.SpotCount != _fieldSpots)
            {
                _fieldGoal = goal; _fieldSpots = nav.SpotCount;
                _field.Begin(x, y, z);
                _wayAt = 0f;
            }
            if (!_field.Done) _field.Work(3000);
            if (Time.time >= _wayAt)
            {
                _wayAt = Time.time + 0.15f;
                float fx, fy, fz;
                _wayOn = _field.Toward(at.x, at.y, at.z, 9, out fx, out fy, out fz, out _walk);
                if (_wayOn) _wayPoint = new Vector3(fx, fy, fz);
            }
            Vector3 to = (_wayOn ? _wayPoint : goal) - at;
            float far = Mathf.Sqrt((goal.x - at.x) * (goal.x - at.x) + (goal.z - at.z) * (goal.z - at.z));
            Camera cam = FactoryController.PlayerCam;
            float facing = cam != null ? cam.transform.eulerAngles.y : p.Yaw;
            float turn = Mathf.DeltaAngle(facing, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg); // right of where you look: positive
            bool here = far < 5f && Mathf.Abs(goal.y - at.y) < 2f;
            _arrow.localRotation = Quaternion.Euler(0f, 0f, here ? 180f : -turn);      // (at it: the arrow points down)
            float pulse = here ? 1f + Mathf.Sin(Time.time * 6f) * 0.12f : 1f;
            _arrow.localScale = new Vector3(pulse, pulse, 1f);
            _arrowText.text = here ? "here" : Mathf.RoundToInt(_wayOn ? Mathf.Max(far, _walk) : far) + " m" + (goal.y - at.y > 2f ? "   (upstairs)" : at.y - goal.y > 2f ? "   (downstairs)" : "");
        }

        private static string WeaponLine(int slot)
        {
            RcItem it = Rc.Char.Weapon(slot);
            if (it == null) return (slot + 1) + "   (empty)";
            string s = (slot + 1) + "   " + it.Name;
            if (it.Kind == RcKind.Gadget)
            {
                float w = Rc.Player.GadgetWait(slot);
                s += w > 0f ? "   " + Mathf.CeilToInt(w * it.Rate * Rc.Char.CooldownMult) + "s" : "   ready";
            }
            return s;
        }
    }

    /// <summary>Shared by Recalled's menus: TAB, ESC or E closes them, and the names they show.</summary>
    public abstract class RcMenu : OverlayPanel
    {
        protected string Note = "";

        private int _shownFrame;

        protected override bool Rpg { get { return true; } }

        // (the menus' warm lantern colors in place of the options menu's cyan)
        protected new static readonly Color Accent = Lantern;
        protected new static readonly Color Dim = new Color(0.78f, 0.70f, 0.58f);

        /// <summary>The text box at the bottom of the screen (null until AddTalk).</summary>
        protected RcTalk Talk;

        /// <summary>Builds the text box: a name tab, a colored square for who's talking, and the words. Say() fills it.</summary>
        protected RcTalk AddTalk()
        {
            var t = new RcTalk();
            var h = new GameObject("Talk", typeof(RectTransform));
            h.transform.SetParent(Root, false);
            var hr = (RectTransform)h.transform;
            hr.anchorMin = Vector2.zero; hr.anchorMax = Vector2.one; hr.offsetMin = hr.offsetMax = Vector2.zero;
            Transform p = h.transform;
            Vector2 c = new Vector2(0f, -392f), size = new Vector2(1500f, 220f);
            MakeImage(p, c, size + new Vector2(34f, 34f), new Color(0f, 0f, 0f, 0.5f));
            MakeImage(p, c, size + new Vector2(18f, 18f), Wood);
            MakeImage(p, c, size + new Vector2(10f, 10f), GoldDim);
            Image box = MakeImage(p, c, size, Ink);
            Rule(p, c, size - new Vector2(22f, 22f), 2f, new Color(GoldDim.r, GoldDim.g, GoldDim.b, 0.55f));
            Vector2 face = c + new Vector2(-size.x / 2f + 115f, 0f);
            MakeImage(p, face, new Vector2(186f, 186f), GoldDim);
            t.Portrait = MakeImage(p, face, new Vector2(176f, 176f), Plate);
            t.Letter = MakeText(p, "", 110, TextAnchor.MiddleCenter, face, new Vector2(176f, 176f), Lantern);
            Vector2 tab = c + new Vector2(-size.x / 2f + 240f, size.y / 2f);
            t.TabEdge = MakeImage(p, tab, new Vector2(330f, 58f), GoldDim);
            t.TabFill = MakeImage(p, tab, new Vector2(322f, 50f), Plate);
            t.Name = Fit(MakeText(p, "", 30, TextAnchor.MiddleCenter, tab, new Vector2(310f, 48f), Lantern));
            t.Body = MakeText(p, "", 31, TextAnchor.UpperLeft, c + new Vector2(105f, 0f), new Vector2(1240f, 176f), Cream);
            t.Body.lineSpacing = 1.1f;
            t.Body.horizontalOverflow = HorizontalWrapMode.Wrap;   // (MakeText doesn't wrap: the words ran off the box)
            t.Hint = MakeText(p, "", 22, TextAnchor.MiddleRight, c + new Vector2(size.x / 2f - 170f, -size.y / 2f + 24f), new Vector2(280f, 30f), Dim);
            t.Holder = h;
            t.Rect = box.rectTransform;
            h.SetActive(false);
            Talk = t;
            return t;
        }

        /// <summary>The key that opened this menu: it closes it too.</summary>
        protected virtual KeyCode OwnKey { get { return KeyCode.None; } }

        protected override void Update()
        {
            base.Update();
            // (not on the frame it opened: the TAB that opened the backpack would close it again)
            if (Visible && Time.frameCount > _shownFrame && (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Backspace) || (OwnKey != KeyCode.None && Input.GetKeyDown(OwnKey)))) { Hide(); return; }
            if (Talk != null && Visible && Time.frameCount > _shownFrame)
                Talk.Tick(Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || (Input.GetMouseButtonDown(0) && Hit(Talk.Rect)));
        }

        /// <summary>How far along the current job is, in words.</summary>
        public static string ProgressLine(RcCharacter c)
        {
            RcQuest q = c.CurrentQuest;
            if (q == null) return "";
            if (q.World != RcWorlds.Now) return "This job is " + (q.World == RcWorlds.EGYPT ? "in Egypt: ride the Recall Express" : "in the town: ride the Recall Express home");
            if (c.QuestComplete) return RcWorlds.Egypt ? "Done: hand it in at Last Stop's quest board" : "Done: hand it in at the treehouse's quest board";
            switch (q.Kind)
            {
                case RcQuestKind.Bring: return RcData.Item(q.Target).Name + ":  " + (c.Carried(q.Target) + RcCharacter.Count(c.Stash, q.Target)) + " / " + q.Count + "   (backpack and stash together)";
                case RcQuestKind.Kill: return "Toys broken in " + RcData.ZoneNames[q.Zone].ToLower() + ":  " + c.ActiveProgress + " / " + q.Count;
                case RcQuestKind.Elite: return RcData.Enemy(q.Target).Name + " broken:  " + c.ActiveProgress + " / " + q.Count;
                case RcQuestKind.Craft: return "Not made yet:  " + RcData.Item(q.Target).Name;
                case RcQuestKind.Boss: return RcData.BossNames[q.Zone] + " is still standing";
                case RcQuestKind.Scene: return "Listen to the kids at the quest board";
                case RcQuestKind.Station: return "Board the Recall Express at the junkyard station";
                default: return "Not done yet";
            }
        }

        protected override void OnShown() { Note = ""; _shownFrame = Time.frameCount; if (Talk != null) Talk.Clear(); }

        /// <summary>The kid's words about the job you're on: why it matters (when it's new to you), then what to do.</summary>
        protected void Brief(RcCharacter c, bool story)
        {
            RcQuest q = c.CurrentQuest;
            Talk.Clear();
            if (q == null) { Talk.Say(RcWorlds.Egypt ? "Pip" : "June", WorldDone); return; }
            if (q.World != RcWorlds.Now) { Talk.Say("Tip", "Your next job, \"" + q.Title + "\", is " + (q.World == RcWorlds.EGYPT ? "in Egypt" : "back in the town") + ". Ride the Recall Express: its platform is on the map."); return; }
            int job = c.ActiveQuest;
            if (story) foreach (RcSay s in RcLines.Before(job)) Talk.Add(s.Who, s.Text);
            Talk.Add(RcLines.Teller(job), q.Text);
        }

        /// <summary>Says a list of lines by their IDs, in order.</summary>
        protected void AddIds(string[] ids)
        {
            for (int i = 0; i < ids.Length; i++) { RcSay s = RcVoiceData.Line(ids[i]); Talk.Add(s.Who, s.Text); }
        }

        protected static string WorldDone { get { return RcWorlds.Egypt ? "That's everything on the board. Mama-2's broadcast is quiet, for now. The Line goes on from here, whenever you're ready." : "That's everything on the board. The train's still waiting at the junkyard station, whenever you're ready. We'll keep the lantern lit."; } }

        protected static string Label(RcStack s)
        {
            RcItem it = RcData.Item(s.Id);
            if (it == null) return "?";
            return s.Count > 1 ? it.Name + "  x" + s.Count : it.Name;
        }

        /// <summary>Everything an item does, in words: what it is, what it takes to use, and its numbers.</summary>
        protected static string Describe(RcItem it)
        {
            if (it == null) return "";
            string kind = it.Kind == RcKind.Material ? "Material" : it.Kind == RcKind.Melee ? "Melee weapon" : it.Kind == RcKind.Ranged ? "Ranged weapon" : it.Kind == RcKind.Gadget ? "Gadget" : it.Kind == RcKind.Armor ? "Armor" : it.Kind == RcKind.Mount ? "Mount" : "Trinket";
            string s = it.Name + "   (" + kind + (it.IsGear ? ", needs level " + it.Level : "") + ")\n" + it.Desc;
            switch (it.Kind)
            {
                case RcKind.Material: s += "\nUsed at the crafting bench."; break;
                case RcKind.Melee:
                    s += "\n" + it.Power + " damage a swing, " + it.Rate.ToString("0.0") + " swings a second, reaches " + it.Range.ToString("0.0") + " m. Keep the click held to wind up a heavy swing: up to " + (it.Power * 2f) + ".";
                    s += "\nRight click blocks three quarters of a hit from the front; press it just before a hit lands for a perfect block (no damage, and your next blow counters for more). Toys hear it from " + it.Noise + " m. Goes in a weapon slot.";
                    break;
                case RcKind.Ranged:
                    s += "\n" + it.Power + " damage at a full draw, which takes " + (1f / it.Rate).ToString("0.0") + " s (hold the click, let go to fire). Carries " + it.Range + " m.";
                    s += "\nRight click blocks half of a hit from the front; press it just before a hit lands for a perfect block (no damage, and your next blow counters for more). Toys hear it from " + it.Noise + " m. Goes in a weapon slot.";
                    break;
                case RcKind.Gadget:
                    if (it.Ability == RcAbility.Shock) s += "\n" + it.Power + " damage to every toy within " + it.Range + " m, and they're stunned for a moment (not bosses).";
                    else if (it.Ability == RcAbility.Repair) s += "\nUse it on yourself, any time.";
                    else if (it.Ability == RcAbility.Decoy) s += "\nDropped a few steps in front of you. Bosses and Mama ignore it.";
                    else if (it.Ability == RcAbility.Shield) s += "\nIt takes the damage before your health does.";
                    s += "\nClick to use it; ready again after " + it.Rate + " s. Toys hear it from " + it.Noise + " m. Goes in a weapon slot.";
                    break;
                case RcKind.Armor: s += "\nArmor points cut every hit you take by that many percent: toys, shots and slams alike, on top of blocking. Worn in the armor slot: one at a time."; break;
                case RcKind.Trinket: s += "\nWorn in a trinket slot: you have two."; break;
                case RcKind.Mount: s += "\n+" + it.SpeedPct + "% speed. Keep it in your backpack and press V to get on (V again to get off). You can't fight or roll while you ride, a hit throws you off, and it's as loud as running."; break;
            }
            return s;
        }

        /// <summary>How an item compares with what's in its slot now ("" when there's nothing to say).</summary>
        protected static string Versus(RcItem it)
        {
            if (it == null || Rc.Char == null) return "";
            RcCharacter c = Rc.Char;
            if (it.IsGear && !RecalledRules.CanEquip(c.Level, it.Level)) return "You're level " + c.Level + ": you can make it, but not use it until level " + it.Level + ".";
            if (it.Kind == RcKind.Armor)
            {
                RcItem now = RcData.Item(c.Equip[2]);
                return now == null ? "You're wearing no armor." : now.Id == it.Id ? "You're wearing one." : "You're wearing " + now.Name + " (" + now.Armor + " armor, +" + now.Health + " health).";
            }
            if (it.Kind == RcKind.Melee || it.Kind == RcKind.Ranged)
            {
                string have = "";
                for (int i = 0; i < 2; i++)
                {
                    RcItem w = c.Weapon(i);
                    if (w != null && w.Kind == it.Kind) have += (have.Length > 0 ? ", " : "") + w.Name + " (" + w.Power + " damage)";
                }
                return have.Length > 0 ? "You have: " + have + "." : "";
            }
            return "";
        }
    }

    /// <summary>TAB: what you're wearing and carrying. Click an item to see it, then EQUIP or DROP. Click what you're wearing to take it off.</summary>
    public class RcBagMenu : RcMenu
    {
        private static RcBagMenu _instance;
        private readonly Text[] _slots = new Text[RcCharacter.EQUIP_SLOTS];
        private readonly Text[] _cells = new Text[40];
        private Text _stats, _detail, _count;
        private int _sel = -1;
        private static readonly string[] SlotNames = { "Weapon 1", "Weapon 2", "Armor", "Trinket", "Trinket" };

        protected override string Title { get { return "BACKPACK"; } }
        protected override Vector2 PanelSize { get { return new Vector2(1640f, 900f); } }

        public static void Open() { GetOrCreate(ref _instance, "KE_RcBagMenu").Show(); }

        protected override void OnShown() { base.OnShown(); _sel = -1; }

        protected override void BuildContent(Transform p)
        {
            MakeText(p, "WEARING", 26, TextAnchor.MiddleLeft, new Vector2(-590f, 330f), new Vector2(400f, 40f), Accent);
            for (int i = 0; i < _slots.Length; i++)
            {
                int slot = i;
                _slots[i] = AddButton(p, new Vector2(-590f, 270f - i * 62f), "", () => TakeOff(slot), 420f);
                _slots[i].alignment = TextAnchor.MiddleLeft;
            }
            Inset(p, new Vector2(-590f, -160f), new Vector2(440f, 240f));
            _stats = MakeText(p, "", 24, TextAnchor.UpperLeft, new Vector2(-590f, -160f), new Vector2(420f, 220f), Dim);
            _count = MakeText(p, "", 26, TextAnchor.MiddleLeft, new Vector2(140f, 330f), new Vector2(900f, 40f), Accent);
            for (int i = 0; i < _cells.Length; i++)
            {
                int index = i;
                _cells[i] = AddButton(p, new Vector2(-250f + (i % 5) * 212f, 270f - (i / 5) * 62f), "", () => { _sel = index; Note = ""; }, 206f);
                _cells[i].fontSize = 22;
            }
            Inset(p, new Vector2(174f, -290f), new Vector2(1090f, 130f));
            _detail = Fit(MakeText(p, "", 24, TextAnchor.UpperLeft, new Vector2(174f, -290f), new Vector2(1060f, 110f), Cream));
            AddButton(p, new Vector2(-160f, -385f), "EQUIP", Equip, 240f);
            AddButton(p, new Vector2(120f, -385f), "DROP", Drop, 240f);
            _give = AddButton(p, new Vector2(440f, -385f), "GIVE", Give, 300f);
            AddButton(p, new Vector2(-590f, -385f), "CLOSE", Hide, 240f);
        }

        private void TakeOff(int slot)
        {
            if (Rc.Char.Equip[slot] == 0) return;
            if (!Rc.Char.Unequip(slot)) { Note = "Your backpack is full."; return; }
            Rc.Player.GearChanged();
        }

        private void Equip()
        {
            if (_sel < 0 || _sel >= Rc.Char.Backpack.Count) { Note = "Pick something first."; return; }
            string block = Rc.Char.EquipBlock(_sel);
            if (block != null) { Note = block; return; }
            Rc.Char.EquipFromBackpack(_sel, -1);
            Rc.Player.GearChanged();
            _sel = -1;
            Note = "";
        }

        private Text _give;

        // Trading: what you've picked goes to the friend standing next to you. (What doesn't fit in their backpack comes back.)
        private void Give()
        {
            if (_sel < 0 || _sel >= Rc.Char.Backpack.Count) { Note = "Pick something first."; return; }
            if (Rc.Char.Backpack[_sel].Id == RcData.TAPE) { Note = "Keep the Tape: you'll need it."; return; }
            RcPeer to = Rc.Player.NearestFriend(5f);
            if (to == null) { Note = "Stand next to the friend you're giving it to."; return; }
            RcStack st = Rc.Char.Backpack[_sel];
            Note = "Gave " + Label(st) + " to " + to.Name + ".";
            Rc.Char.Backpack.RemoveAt(_sel);
            RcNet.Gift(to, st.Id, st.Count, false);
            _sel = -1;
            Rc.Save();
        }

        private void Drop()
        {
            if (_sel < 0 || _sel >= Rc.Char.Backpack.Count) { Note = "Pick something first."; return; }
            if (Rc.Char.Backpack[_sel].Id == RcData.TAPE) { Note = "Keep the Tape: you'll need it."; return; }
            Note = "Dropped " + Label(Rc.Char.Backpack[_sel]) + ".";
            Rc.Char.Backpack.RemoveAt(_sel);
            _sel = -1;
        }

        protected override void RefreshAll()
        {
            base.RefreshAll();
            if (_stats == null || Rc.Char == null) return;
            RcCharacter c = Rc.Char;
            for (int i = 0; i < _slots.Length; i++)
            {
                RcItem it = RcData.Item(c.Equip[i]);
                _slots[i].text = SlotNames[i] + ":  " + (it != null ? it.Name : "(nothing)");
                _slots[i].color = it != null ? Color.white : Dim;
            }
            _stats.text = "Level " + c.Level + "\nHealth " + Mathf.RoundToInt(c.MaxHealth) + "\nArmor " + c.Armor + "  (" + c.Armor + "% less damage)\nDamage x" + c.PowerMult.ToString("0.00") + "\nSpeed x" + c.SpeedMult.ToString("0.00")
                + "\nGadgets recharge x" + (1f / c.CooldownMult).ToString("0.00") + "\nHealing x" + c.HealingMult.ToString("0.00");
            RcPeer friend = Rc.Player != null ? Rc.Player.NearestFriend(5f) : null;
            _give.gameObject.SetActive(RcNet.Peers.Count > 0);
            _give.text = friend != null ? "GIVE TO " + friend.Name.ToUpper() : "GIVE";
            _give.color = friend != null ? Color.white : Dim;
            _count.text = "CARRYING   " + c.Backpack.Count + " / " + c.BackpackSlots + "     (materials here are dropped if you fall; the stash is safe)";
            for (int i = 0; i < _cells.Length; i++)
            {
                bool slot = i < c.BackpackSlots, has = i < c.Backpack.Count;
                _cells[i].gameObject.SetActive(slot);
                if (!slot) continue;
                _cells[i].text = has ? Label(c.Backpack[i]) : "-";
                _cells[i].color = i == _sel ? Accent : has ? Color.white : new Color(1f, 1f, 1f, 0.25f);
            }
            _detail.text = Note.Length > 0 ? Note : _sel >= 0 && _sel < c.Backpack.Count ? Describe(RcData.Item(c.Backpack[_sel].Id)) : "Click an item to see it. Click what you're wearing to take it off.  (TAB closes)";
        }
    }

    /// <summary>The chest in the treehouse: click something to move it across. What's in the stash is safe when you fall.</summary>
    public class RcStashMenu : RcMenu
    {
        private static RcStashMenu _instance;
        private readonly Text[] _bag = new Text[40];
        private readonly Text[] _stash = new Text[RecalledRules.STASH_SLOTS];
        private Text _info;

        protected override string Title { get { return "YOUR STASH"; } }
        protected override Vector2 PanelSize { get { return new Vector2(1860f, 980f); } }

        public static void Open() { GetOrCreate(ref _instance, "KE_RcStashMenu").Show(); }

        protected override void BuildContent(Transform p)
        {
            MakeText(p, "BACKPACK", 26, TextAnchor.MiddleCenter, new Vector2(-640f, 380f), new Vector2(500f, 40f), Accent);
            MakeText(p, "STASH  (safe)", 26, TextAnchor.MiddleCenter, new Vector2(300f, 380f), new Vector2(500f, 40f), Accent);
            for (int i = 0; i < _bag.Length; i++)
            {
                int index = i;
                _bag[i] = AddButton(p, new Vector2(-860f + (i % 4) * 148f, 330f - (i / 4) * 66f), "", () => Move(true, index), 144f);
                _bag[i].fontSize = 20;
            }
            for (int i = 0; i < _stash.Length; i++)
            {
                int index = i;
                _stash[i] = AddButton(p, new Vector2(-240f + (i % 10) * 114f, 330f - (i / 10) * 66f), "", () => Move(false, index), 110f);
                _stash[i].fontSize = 20;
            }
            Inset(p, new Vector2(0f, -390f), new Vector2(1560f, 56f));
            _info = MakeText(p, "", 24, TextAnchor.MiddleCenter, new Vector2(0f, -390f), new Vector2(1500f, 40f), Cream);
            AddButton(p, new Vector2(0f, -442f), "CLOSE", Hide, 240f);
        }

        private void Move(bool toStash, int index)
        {
            if (!Rc.Char.Move(toStash, index)) Note = toStash ? "The stash is full." : "Your backpack is full.";
            else Note = "";
        }

        protected override void OnHidden() { Rc.Save(); RcVoice.Menu("STASH"); }

        protected override void RefreshAll()
        {
            base.RefreshAll();
            if (_info == null || Rc.Char == null) return;
            RcCharacter c = Rc.Char;
            for (int i = 0; i < _bag.Length; i++)
            {
                bool slot = i < c.BackpackSlots, has = i < c.Backpack.Count;
                _bag[i].gameObject.SetActive(slot);
                if (!slot) continue;
                _bag[i].text = has ? Label(c.Backpack[i]) : "-";
                _bag[i].color = has ? Color.white : new Color(1f, 1f, 1f, 0.25f);
            }
            for (int i = 0; i < _stash.Length; i++)
            {
                bool has = i < c.Stash.Count;
                _stash[i].text = has ? Label(c.Stash[i]) : "-";
                _stash[i].color = has ? Color.white : new Color(1f, 1f, 1f, 0.25f);
            }
            _info.text = Note.Length > 0 ? Note : "Click something to move it across.   Backpack " + c.Backpack.Count + " / " + c.BackpackSlots + ",  stash " + c.Stash.Count + " / " + RecalledRules.STASH_SLOTS;
        }
    }

    /// <summary>The crafting bench: every recipe your bench can make, what it needs and what you have. Click one to read what it makes, then MAKE IT.</summary>
    public class RcCraftMenu : RcMenu
    {
        private static RcCraftMenu _instance;
        private const int ROWS = 7;
        private readonly Text[] _rows = new Text[ROWS];
        private readonly List<RcRecipe> _list = new List<RcRecipe>();
        private Text _info, _pageText, _make;
        private int _page, _sel = -1;

        protected override string Title { get { return "CRAFTING BENCH"; } }
        protected override Vector2 PanelSize { get { return new Vector2(1640f, 900f); } }

        public static void Open() { GetOrCreate(ref _instance, "KE_RcCraftMenu").Show(); }

        protected override void OnShown() { base.OnShown(); _page = 0; _sel = -1; }
        protected override void OnHidden() { Rc.Save(); RcVoice.Menu("BENCH"); }

        protected override void BuildContent(Transform p)
        {
            for (int i = 0; i < ROWS; i++)
            {
                int row = i;
                _rows[i] = AddButton(p, new Vector2(0f, 310f - i * 60f), "", () => { int pick = _page * ROWS + row; if (pick < _list.Count) { _sel = pick; Note = ""; } }, 1540f);
                _rows[i].alignment = TextAnchor.MiddleLeft;
                _rows[i].fontSize = 26;
            }
            Inset(p, new Vector2(0f, -215f), new Vector2(1540f, 230f));
            _info = Fit(MakeText(p, "", 24, TextAnchor.UpperLeft, new Vector2(0f, -215f), new Vector2(1500f, 210f), Cream));
            AddButton(p, new Vector2(-600f, -385f), "< BACK", () => { _page = Mathf.Max(0, _page - 1); _sel = -1; Note = ""; }, 240f);
            _pageText = MakeText(p, "", 24, TextAnchor.MiddleCenter, new Vector2(-400f, -385f), new Vector2(160f, 40f), Dim);
            AddButton(p, new Vector2(-200f, -385f), "MORE >", () => { if ((_page + 1) * ROWS < _list.Count) { _page++; _sel = -1; } Note = ""; }, 240f);
            _make = AddButton(p, new Vector2(200f, -385f), "MAKE IT", Make, 300f);
            AddButton(p, new Vector2(600f, -385f), "CLOSE", Hide, 240f);
        }

        private void Make()
        {
            if (_sel < 0 || _sel >= _list.Count) { Note = "Click a recipe first: it says here what it makes."; return; }
            RcRecipe r = _list[_sel];
            string block = Rc.Char.CraftBlock(r);
            if (block != null) { Note = block; return; }
            Rc.Char.Craft(r);
            if (r.Makes != 0) Rc.Char.OnCrafted(r.Makes);
            Note = r.Makes == 0 ? "Your backpack holds " + Rc.Char.BackpackSlots + " now." : "Made: " + RcData.Item(r.Makes).Name + ". It's in your backpack (TAB to equip it).";
            if (r.Makes == 0) _sel = -1; // (that recipe's gone from the list)
            Rc.Music.Jingle(RcMusic.J_QUEST);
        }

        protected override void RefreshAll()
        {
            base.RefreshAll();
            if (_info == null || Rc.Char == null) return;
            RcCharacter c = Rc.Char;
            // Everything up to the tier after the one you've unlocked (so you can see what's next).
            _list.Clear();
            foreach (RcRecipe r in RcData.Recipes)
            {
                if (r.World != RcWorlds.Now || r.Zone > c.Bosses + 1) continue;
                if (r.Makes == 0 && r.Needs[0].Id - RcData.THREAD < c.BackpackUpgrades) continue; // already made
                _list.Add(r);
            }
            for (int i = 0; i < ROWS; i++)
            {
                int index = _page * ROWS + i;
                bool has = index < _list.Count;
                _rows[i].gameObject.SetActive(has);
                if (!has) continue;
                RcRecipe r = _list[index];
                RcItem it = RcData.Item(r.Makes);
                string s = it != null ? it.Name + (it.Kind == RcKind.Mount ? "  (a mount: +" + it.SpeedPct + "% speed)" : "  (level " + it.Level + ")") : "Bigger backpack  (+" + RecalledRules.BACKPACK_STEP + " slots)";
                s += "      needs  ";
                for (int k = 0; k < r.Needs.Length; k++)
                    s += (k > 0 ? ",  " : "") + RcData.Item(r.Needs[k].Id).Name + " " + c.Have(r.Needs[k].Id) + "/" + r.Needs[k].Count;
                _rows[i].text = s;
                _rows[i].color = index == _sel ? Accent : c.CraftBlock(r) == null ? Color.white : r.Zone > c.Bosses ? new Color(1f, 1f, 1f, 0.3f) : Dim;
            }
            _pageText.text = (_page + 1) + " / " + Mathf.Max(1, (_list.Count + ROWS - 1) / ROWS);
            if (_sel >= _list.Count) _sel = -1;
            if (_sel < 0)
            {
                _info.text = Note.Length > 0 ? Note : "Click a recipe to read what it makes. White ones you can make now. The bench uses your backpack and your stash together.";
                _make.color = Dim;
                return;
            }
            // What the picked recipe makes, in full, and why you can't make it if you can't.
            RcRecipe pick = _list[_sel];
            RcItem made = RcData.Item(pick.Makes);
            string text = made != null ? Describe(made)
                : "Bigger backpack\nFive more slots in your backpack, for good (it holds " + c.BackpackSlots + " now, " + RecalledRules.BackpackSlots(RecalledRules.BACKPACK_UPGRADES) + " at most). Each one needs a part from a different boss.";
            string versus = Versus(made), block = c.CraftBlock(pick);
            if (versus.Length > 0) text += "\n" + versus;
            if (Note.Length > 0) text += "\n<color=#ffd040>" + Note + "</color>";
            else if (block != null) text += "\n<color=#ff7060>" + block + "</color>";
            _info.text = text;
            _make.color = block == null ? Color.white : Dim;
        }
    }

    /// <summary>The quest board in the treehouse: the job you're on, where you hand it in, and the kids' words in the text box below.</summary>
    public class RcQuestMenu : RcMenu
    {
        private static RcQuestMenu _instance;
        private Text _title, _jobNo, _progress, _reward, _turnIn, _ask;
        private bool _asked, _sceneDone;

        protected override string Title { get { return RcWorlds.Egypt ? "THE RESISTANCE" : "THE KIDS"; } }
        protected override Vector2 PanelSize { get { return new Vector2(1300f, 560f); } }
        protected override Vector2 PanelOffset { get { return new Vector2(0f, 110f); } }

        public static void Open() { GetOrCreate(ref _instance, "KE_RcQuestMenu").Show(); }

        protected override void OnShown()
        {
            base.OnShown();
            if (Rc.Char == null) return;
            _asked = _sceneDone = false;
            bool tapeBack = Rc.Char.EnsureTape();
            bool story = Rc.Char.InSession || !Rc.Char.QuestTold; // (the story of a job once; after that just what to do)
            Rc.Char.OnTalked(); Rc.Char.QuestTold = true;
            RcQuest q = Rc.Char.CurrentQuest;
            if (q != null && q.Kind == RcQuestKind.Scene && !Rc.Char.QuestComplete) { Talk.Clear(); AddIds(RcScenes.Argue); return; }   // (they're arguing: you get one word in)
            Brief(Rc.Char, story);
            if (tapeBack) Talk.Add("Tip", "The Tape is back in your backpack.");
        }

        protected override void OnHidden() { Rc.Save(); }

        protected override void BuildContent(Transform p)
        {
            AddTalk();
            _title = Fit(MakeText(p, "", 44, TextAnchor.MiddleCenter, new Vector2(0f, 160f), new Vector2(1100f, 60f), Accent));
            _jobNo = MakeText(p, "", 24, TextAnchor.MiddleCenter, new Vector2(0f, 108f), new Vector2(1100f, 34f), Dim);
            Inset(p, new Vector2(0f, -10f), new Vector2(1120f, 150f));
            _progress = MakeText(p, "", 28, TextAnchor.MiddleCenter, new Vector2(0f, 15f), new Vector2(1060f, 40f), Color.white);
            _reward = Fit(MakeText(p, "", 24, TextAnchor.MiddleCenter, new Vector2(0f, -45f), new Vector2(1060f, 70f), Dim));
            _ask = AddButton(p, new Vector2(0f, -122f), "WAIT. WHAT IF THEY'RE WAITING FOR US?", Ask, 1000f);
            _turnIn = AddButton(p, new Vector2(-330f, -190f), "HAND IT IN", TurnIn, 340f);
            AddButton(p, new Vector2(40f, -190f), "HOW?", () => { Hide(); RcObjectiveMenu.Open(); }, 240f);
            AddButton(p, new Vector2(340f, -190f), "CLOSE", Hide, 240f);
        }

        // The one thing you say in the argument: and then they answer.
        private void Ask()
        {
            _asked = true;
            Talk.Clear();
            AddIds(RcScenes.ArgueAfter);
            if (RcNet.Peers.Count == 0) { RcSay s = RcVoiceData.Line("ARG-SOLO-1"); Talk.Add(s.Who, s.Text); }
        }

        protected override void Update()
        {
            base.Update();
            // The argument counts as heard once the last line is up.
            if (_asked && !_sceneDone && Visible && Talk != null && Talk.Done && Rc.Char != null) { _sceneDone = true; Rc.Char.OnScene(); Rc.Save(); }
        }

        private void TurnIn()
        {
            RcQuest q = Rc.Char.CurrentQuest;
            if (q == null || !Rc.Char.QuestComplete) return;
            int job = Rc.Char.ActiveQuest;
            int xp = Rc.Char.TurnIn();
            if (xp < 0) { Talk.Say("Tip", "Make room in your backpack first: there's a reward to carry."); return; }
            Talk.Clear();
            if (job == 22) AddIds(RcScenes.Call);   // (Moss plays the Tape)
            Talk.Add(RcLines.Teller(job), q.Done);
            foreach (RcSay s in RcLines.After(job)) Talk.Add(s.Who, s.Text);
            if (q.Kind == RcQuestKind.Talk) Talk.Add("Tip", "Press TAB to open your backpack, click an item, then EQUIP.");
            Rc.Player.AddXp(xp);
            Daily.Add("jobs", 1);
            int pay = q.Kind == RcQuestKind.Boss ? 100 : 20; Shards.Add(pay); Rc.Hud.Feed("+" + pay + " shards");   // (everything pays shards)
            Rc.Music.Jingle(RcMusic.J_QUEST);
            if (RcVoice.I != null) RcVoice.I.JobDone();
            RcQuest next = Rc.Char.CurrentQuest;
            if (next != null) Rc.Char.QuestTold = true;
            if (next != null && next != q)                                   // (and then the next job, in their words)
            {
                int nj = Rc.Char.ActiveQuest;
                foreach (RcSay s in RcLines.Before(nj)) Talk.Add(s.Who, s.Text);
                Talk.Add(RcLines.Teller(nj), next.Text);
            }
            else if (next == null) Talk.Add("June", WorldDone);
            if (Rc.World != null) Rc.World.BuildDueForts(true); // (the first job in a zone gets the kids building its fort)
            Rc.Save();
        }

        protected override void RefreshAll()
        {
            base.RefreshAll();
            if (_title == null || Rc.Char == null) return;
            RcCharacter c = Rc.Char;
            RcQuest q = c.CurrentQuest;
            if (q == null)
            {
                _title.text = RcWorlds.Egypt ? "EGYPT COMPLETE" : "WORLD 1 COMPLETE";
                _jobNo.text = "";
                _progress.text = ""; _reward.text = "";
                _turnIn.gameObject.SetActive(false);
                _ask.gameObject.SetActive(false);
                return;
            }
            _title.text = q.Title.ToUpper();
            _jobNo.text = "job " + RcWorlds.JobNo(c.ActiveQuest) + " of " + RcWorlds.JobsIn(c.ActiveQuest);
            bool done = c.QuestComplete;
            if (q.Kind == RcQuestKind.Bring) _progress.text = RcData.Item(q.Target).Name + ":  " + (c.Carried(q.Target) + RcCharacter.Count(c.Stash, q.Target)) + " / " + q.Count;
            else if (q.Kind == RcQuestKind.Kill || q.Kind == RcQuestKind.Elite) _progress.text = c.ActiveProgress + " / " + q.Count;
            else _progress.text = done ? "Done" : "Not yet";
            _progress.color = done ? Accent : Color.white;
            string rw = "Reward:  ";
            if (q.Kind == RcQuestKind.Boss) rw += "what it drops,  ";
            else rw += q.Xp + " experience,  ";
            for (int i = 0; i < q.Reward.Length; i++) rw += (i > 0 ? ",  " : "") + RcData.Item(q.Reward[i].Id).Name + (q.Reward[i].Count > 1 ? " x" + q.Reward[i].Count : "");
            _reward.text = rw;
            _turnIn.gameObject.SetActive(done);
            _ask.gameObject.SetActive(q.Kind == RcQuestKind.Scene && !done && !_asked && Talk != null && !Talk.Typing && !Talk.HasMore);
        }
    }

    /// <summary>J: the job you're on, spelled out. Every step of how to do it, how far along you are, which way it is and what it pays; what the kids said is in the text box below.</summary>
    public class RcObjectiveMenu : RcMenu
    {
        private static RcObjectiveMenu _instance;
        private Text _title, _steps, _progress, _where, _reward, _notices;

        protected override string Title { get { return "WHAT TO DO"; } }
        protected override Vector2 PanelSize { get { return new Vector2(1560f, 780f); } }
        protected override Vector2 PanelOffset { get { return new Vector2(0f, 140f); } }
        protected override KeyCode OwnKey { get { return KeyCode.J; } }

        public static void Open() { GetOrCreate(ref _instance, "KE_RcObjectiveMenu").Show(); }

        protected override void OnShown()
        {
            base.OnShown();
            if (Rc.Char == null) return;
            Brief(Rc.Char, true);
        }

        protected override void BuildContent(Transform p)
        {
            AddTalk();
            _title = Fit(MakeText(p, "", 38, TextAnchor.MiddleCenter, new Vector2(0f, 262f), new Vector2(1440f, 56f), Color.white));
            Inset(p, new Vector2(0f, 40f), new Vector2(1440f, 360f));
            _steps = Fit(MakeText(p, "", 27, TextAnchor.UpperLeft, new Vector2(0f, 40f), new Vector2(1390f, 335f), Cream));
            _steps.lineSpacing = 1.15f;
            _progress = MakeText(p, "", 28, TextAnchor.MiddleCenter, new Vector2(0f, -175f), new Vector2(1400f, 40f), Accent);
            _where = MakeText(p, "", 26, TextAnchor.MiddleCenter, new Vector2(0f, -220f), new Vector2(1400f, 40f), Color.white);
            _reward = Fit(MakeText(p, "", 24, TextAnchor.MiddleCenter, new Vector2(0f, -262f), new Vector2(1400f, 40f), Dim));
            AddButton(p, new Vector2(-440f, -325f), "MAP", () => { Hide(); RcMapMenu.Open(); }, 240f);
            _notices = AddButton(p, new Vector2(0f, -325f), "NOTICES", () => { Hide(); RcNoticeMenu.Open(-1); }, 420f);
            AddButton(p, new Vector2(440f, -325f), "CLOSE", Hide, 240f);
        }

        protected override void RefreshAll()
        {
            base.RefreshAll();
            if (_title == null || Rc.Char == null || Rc.Player == null) return;
            RcCharacter c = Rc.Char;
            RcQuest q = c.CurrentQuest;
            _notices.text = "NOTICES  " + c.NoticeCount + " / " + RcNotices.COUNT;
            if (q == null)
            {
                _title.text = RcWorlds.Egypt ? "EGYPT COMPLETE" : "WORLD 1 COMPLETE";
                _steps.text = RcWorlds.Egypt ? "Every job on the board is done, and Mama-2's broadcast is quiet.\n\nThe Line goes on from here: the Recall Express is at the platform in Last Stop. The toys still come back, the bosses can be fought again, and the bench still has things you haven't made."
                    : "Every job the kids had is done, and Mama is gone for tonight.\n\nThe Recall Express is waiting at the junkyard station, east of the park, but Last Stop, where it goes, is under development. The toys still come back, the bosses can be fought again, and the bench still has things you haven't made.";
                _progress.text = ""; _where.text = ""; _reward.text = "";
                return;
            }
            _title.text = q.Title.ToUpper() + "     (job " + RcWorlds.JobNo(c.ActiveQuest) + " of " + RcWorlds.JobsIn(c.ActiveQuest) + (c.InSession ? (c.SessionQuest < c.Quest ? ", your friend's: you've done this one, you're helping" : ", your friend's: you're doing it together") : "") + ")";
            var sb = new System.Text.StringBuilder();
            if (c.QuestComplete) sb.Append("It's done. Go to the treehouse in the park, climb to the first deck, press E at the quest board and press HAND IT IN.\n\nHow it was done:\n");
            if (q.Steps != null)
                for (int i = 0; i < q.Steps.Length; i++) sb.Append(i + 1).Append(".  ").Append(q.Steps[i]).Append('\n');
            _steps.text = sb.ToString();
            _progress.text = ProgressLine(c);
            Vector3 at = Rc.Player.TownPos;
            _where.text = RcGuide.Where(c, at.x, at.y, at.z) + "      (north is up Main Street, away from the Arcade)";
            string rw = "Pays:  " + (q.Kind == RcQuestKind.Boss ? "what it drops" : q.Xp + " experience");
            for (int i = 0; i < q.Reward.Length; i++) rw += ",  " + RcData.Item(q.Reward[i].Id).Name + (q.Reward[i].Count > 1 ? " x" + q.Reward[i].Count : "");
            _reward.text = rw;
        }
    }

    /// <summary>
    /// M: the town from above with the roofs off, drawn from the same plan the town is built from. You, the
    /// treehouse, the forts you've built, each zone's boss, and where your job is.
    /// </summary>
    public class RcMapMenu : RcMenu
    {
        private static RcMapMenu _instance;
        private static Texture2D _tex;
        private static int _texWorld = -1;
        private const int PX = 2;                              // texture pixels per meter
        private const float SHOW = 1.9f;                       // screen units per meter
        private static readonly Vector2 Center = new Vector2(0f, -8f);
        private RawImage _map;
        private RectTransform _you, _nose, _goal, _home;
        private readonly RectTransform[] _friends = new RectTransform[RcLobby.SIZE - 1];
        private readonly RectTransform[] _forts = new RectTransform[4], _bosses = new RectTransform[5];
        private readonly Text[] _names = new Text[5];
        private Text _legend;

        protected override string Title { get { return "THE TOWN"; } }
        protected override Vector2 PanelSize { get { return new Vector2(1160f, 1050f); } }
        protected override KeyCode OwnKey { get { return KeyCode.M; } }

        public static void Open() { GetOrCreate(ref _instance, "KE_RcMapMenu").Show(); }

        /// <summary>Paints the plan top-down: everything that stands on the ground, nothing above head height.</summary>
        private class Painter : RcSink
        {
            public readonly int W = (int)(RcTown.X1 - RcTown.X0) * PX, H = (int)(RcTown.Z1 - RcTown.Z0) * PX;
            public readonly Color32[] Px;
            private readonly Dictionary<RcMat, Color32> _colors = new Dictionary<RcMat, Color32>();

            public Painter() { Px = new Color32[W * H]; }

            private Color32 ColorOf(RcMat m)
            {
                Color32 c;
                if (_colors.TryGetValue(m, out c)) return c;
                Color k = RcMeshSink.FlatColor(m) * 2.3f; // (the map's own flat colors, brightened)
                c = new Color(Mathf.Min(1f, k.r + 0.03f), Mathf.Min(1f, k.g + 0.03f), Mathf.Min(1f, k.b + 0.03f), 1f);
                _colors[m] = c;
                return c;
            }

            public override void Box(RcMat m, float x0, float y0, float z0, float x1, float y1, float z1, bool collide)
            {
                if (m == RcMat.None || y0 >= 2.7f || y1 < -0.5f) return;
                int ax = Mathf.Max(0, Mathf.FloorToInt((x0 - RcTown.X0) * PX)), bx = Mathf.Min(W - 1, Mathf.CeilToInt((x1 - RcTown.X0) * PX) - 1);
                int az = Mathf.Max(0, Mathf.FloorToInt((z0 - RcTown.Z0) * PX)), bz = Mathf.Min(H - 1, Mathf.CeilToInt((z1 - RcTown.Z0) * PX) - 1);
                if (bx < ax) bx = ax; if (bz < az) bz = az;                 // (a thin wall is still a line)
                if (ax >= W || az >= H) return;
                Color32 c = ColorOf(m);
                for (int z = az; z <= bz && z < H; z++) for (int x = ax; x <= bx && x < W; x++) Px[z * W + x] = c;
            }

            public override void Walk(RcMat m, float x0, float y0, float z0, float x1, float y1, float z1) { Box(m, x0, y0, z0, x1, y1, z1, true); }

            public override void Cyl(RcMat m, float x, float y0, float z, float radius, float height, bool collide)
            {
                if (m == RcMat.None || y0 >= 2.7f) return;
                Color32 c = ColorOf(m);
                int cx = Mathf.RoundToInt((x - RcTown.X0) * PX), cz = Mathf.RoundToInt((z - RcTown.Z0) * PX), r = Mathf.Max(1, Mathf.RoundToInt(radius * PX));
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int px = cx + dx, pz = cz + dz;
                        if (dx * dx + dz * dz <= r * r && px >= 0 && pz >= 0 && px < W && pz < H) Px[pz * W + px] = c;
                    }
            }

            public override void Ball(RcMat m, float x, float y, float z, float rx, float ry, float rz) { if (y < 2.7f) Cyl(m, x, 0f, z, (rx + rz) * 0.5f, 1f, false); }
            public override void Light(float x, float y, float z, int color, float range, float intensity) { }
        }

        private static void Paint()
        {
            var painter = new Painter();
            // The Facility isn't in the plan: its lot and its building, as two plain shapes.
            float fx = RcTown.FAC_X, fz = RcTown.FAC_Z;
            if (!RcWorlds.Egypt)
            {
                painter.Box(RcMat.Road, fx + RcTown.FAC_LOT_X0, 0f, fz + RcTown.FAC_LOT_Z0, fx + RcTown.FAC_LOT_X1, 0f, fz, false);
                painter.Box(RcMat.Concrete, fx, 0f, fz, fx + RcTown.FAC_W, 0f, fz + RcTown.FAC_D, false);
                painter.Box(RcMat.Floor, fx + 0.5f, 0f, fz + 0.5f, fx + RcTown.FAC_W - 0.5f, 0f, fz + RcTown.FAC_D - 0.5f, false);
            }
            for (int part = RcTown.PART_CORE; part < 5; part++) RcTown.Build(painter, part);
            _texWorld = RcWorlds.Now;
            _tex = new Texture2D(painter.W, painter.H, TextureFormat.RGB24, false);
            _tex.filterMode = FilterMode.Bilinear;
            _tex.wrapMode = TextureWrapMode.Clamp;
            _tex.SetPixels32(painter.Px);
            _tex.Apply(false, true);
        }

        private static Vector2 At(float x, float z)
        {
            return Center + new Vector2((x - (RcTown.X0 + RcTown.X1) * 0.5f) * SHOW, (z - (RcTown.Z0 + RcTown.Z1) * 0.5f) * SHOW);
        }

        private RectTransform Mark(Transform p, float size, Color c) { return MakeImage(p, Vector2.zero, new Vector2(size, size), c).rectTransform; }

        protected override void BuildContent(Transform p)
        {
            var go = new GameObject("Map", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(p, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Center;
            rt.sizeDelta = new Vector2((RcTown.X1 - RcTown.X0) * SHOW, (RcTown.Z1 - RcTown.Z0) * SHOW);
            _map = go.GetComponent<RawImage>();
            _map.raycastTarget = false;

            for (int z = 0; z < 5; z++)
            {
                _names[z] = MakeText(p, "", 22, TextAnchor.MiddleCenter, At((RcTown.Zone[z, 0] + RcTown.Zone[z, 2]) * 0.5f, RcTown.Zone[z, 3]) + new Vector2(0f, 14f), new Vector2(500f, 30f), Color.white);
                _names[z].gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
                _bosses[z] = Mark(p, 16f, new Color(1f, 0.2f, 0.15f));
            }
            for (int f = 0; f < 4; f++) _forts[f] = Mark(p, 12f, new Color(0.35f, 0.7f, 1f));
            _home = Mark(p, 16f, new Color(0.3f, 1f, 0.4f));
            _home.anchoredPosition = At(0f, 0f);
            _goal = Mark(p, 20f, new Color(1f, 0.8f, 0.15f));
            for (int i = 0; i < _friends.Length; i++) _friends[i] = Mark(p, 12f, new Color(0.3f, 0.95f, 1f));
            _you = Mark(p, 14f, Color.white);
            _nose = Mark(p, 7f, Color.white);
            _legend = Fit(MakeText(p, "", 22, TextAnchor.MiddleCenter, new Vector2(0f, -452f), new Vector2(1100f, 56f), Dim));
            AddButton(p, new Vector2(0f, -495f), "CLOSE", Hide, 240f);
        }

        protected override void OnShown()
        {
            base.OnShown();
            if (_tex == null || _texWorld != RcWorlds.Now)
            {
                try { Paint(); }
                catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Recalled] Couldn't draw the map: " + e.Message); }
            }
            if (_map != null) _map.texture = _tex;
        }

        protected override void RefreshAll()
        {
            base.RefreshAll();
            if (_you == null || Rc.Char == null || Rc.Player == null) return;
            RcCharacter c = Rc.Char;
            Vector3 at = Rc.Player.TownPos;
            _you.anchoredPosition = At(at.x, at.z);
            Vector3 fwd = Quaternion.Euler(0f, Rc.Player.Yaw, 0f) * Vector3.forward;
            _nose.anchoredPosition = At(at.x, at.z) + new Vector2(fwd.x, fwd.z) * 13f;

            for (int i = 0; i < _friends.Length; i++)
            {
                bool there = i < RcNet.Peers.Count && RcNet.Peers[i].Live;
                _friends[i].gameObject.SetActive(there);
                if (there) _friends[i].anchoredPosition = At(RcNet.Peers[i].Pos.x, RcNet.Peers[i].Pos.z);
            }

            float x, y, z; string name;
            bool goal = RcGuide.Target(c, at.x, at.z, out x, out y, out z, out name);
            _goal.gameObject.SetActive(goal);
            if (goal)
            {
                _goal.anchoredPosition = At(x, z);
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.25f;
                _goal.localScale = new Vector3(pulse, pulse, 1f);
            }
            for (int f = 0; f < 4; f++)
            {
                _forts[f].gameObject.SetActive(c.HasFort(f));
                _forts[f].anchoredPosition = At(RcTown.Fort[f, 0], RcTown.Fort[f, 1]);
            }
            for (int i = 0; i < 5; i++)
            {
                bool open = RecalledRules.CanEnter((RecalledZone)i, c.Level, c.Bosses);
                _names[i].text = RcData.ZoneNames[i].ToUpper() + (open ? "" : "   (level " + RecalledRules.GateLevel((RecalledZone)i) + (i > 0 ? ", after " + RcData.BossNames[i - 1] : "") + ")");
                _names[i].color = open ? Color.white : new Color(1f, 1f, 1f, 0.5f);
                _bosses[i].gameObject.SetActive(open);
                _bosses[i].anchoredPosition = At(RcTown.Arena[i, 0], RcTown.Arena[i, 1]);
            }
            _legend.text = "White: you.   Yellow: " + (goal ? name : "nothing left to do") + ".   Green: the treehouse.   Red: a boss.   Blue: a fort you built." + (RcNet.Peers.Count > 0 ? "   Cyan: friends." : "") + "\nNorth is up.";
        }
    }

    /// <summary>P, in the town: who's here with you, and how to get more of them.</summary>
    public class RcPartyMenu : RcMenu
    {
        private static RcPartyMenu _instance;
        private Text _list, _status, _open, _online, _leave;

        protected override string Title { get { return "FRIENDS"; } }
        protected override Vector2 PanelSize { get { return new Vector2(1200f, 820f); } }
        protected override KeyCode OwnKey { get { return KeyCode.P; } }

        public static void Open() { GetOrCreate(ref _instance, "KE_RcPartyMenu").Show(); }

        protected override void BuildContent(Transform p)
        {
            Inset(p, new Vector2(0f, 190f), new Vector2(1040f, 230f));
            _list = MakeText(p, "", 30, TextAnchor.UpperCenter, new Vector2(0f, 190f), new Vector2(1000f, 210f), Color.white);
            Inset(p, new Vector2(0f, 20f), new Vector2(1080f, 90f));
            _status = Fit(MakeText(p, "", 24, TextAnchor.MiddleCenter, new Vector2(0f, 20f), new Vector2(1060f, 80f), Cream));
            _open = AddButton(p, new Vector2(0f, -70f), "", Friends, 640f);
            _online = AddButton(p, new Vector2(0f, -140f), "", Online, 640f);
            _leave = AddButton(p, new Vector2(0f, -210f), "LEAVE THEM", () => { RcLobby.Leave(); Note = "You're on your own again."; }, 640f);
            Inset(p, new Vector2(0f, -295f), new Vector2(1120f, 96f));
            Fit(MakeText(p, "Up to four in a town. Everyone keeps their own character, loot and jobs (each of you is on your own job, and the list above says which); a toy broken near you counts for you too. Toys are tougher the more of you there are.\nG marks what you're looking at. 5 6 7 8 say something. GIVE in the backpack hands an item to the friend beside you.", 22, TextAnchor.MiddleCenter, new Vector2(0f, -295f), new Vector2(1100f, 90f), Dim));
            AddButton(p, new Vector2(0f, -370f), "CLOSE", Hide, 240f);
        }

        private void Friends() { Note = ""; RcLobby.OpenForFriends(true); }

        private void Online()
        {
            Note = "";
            if (RcLobby.Exists) { Note = "You're already in a town with others. Leave them first."; return; }
            RcLobby.GoOnline(Rc.Char != null ? Rc.Char.Bosses : 0);
        }

        protected override void RefreshAll()
        {
            base.RefreshAll();
            if (_list == null) return;
            string who = SteamName() + "   (you" + (RcLobby.Exists && RcLobby.IsHost ? ", running the town" : "") + ")\n";
            for (int i = 0; i < RcNet.Peers.Count; i++)
            {
                RcPeer f = RcNet.Peers[i];
                who += f.Name + (f.Refused ? "   (turned away)" : !f.Live ? "   (on the way)" : "   level " + f.Level + (f.Down ? "   DOWN" : "") + (f.Mission.Length > 0 ? "   job: " + f.Mission : "")) + "\n";
            }
            _list.text = who;
            _status.text = Note.Length > 0 ? Note : RcLobby.Status.Length > 0 ? RcLobby.Status : "You're out here alone.";
            _open.text = RcLobby.Exists ? "INVITE FRIENDS" : "OPEN YOUR TOWN TO FRIENDS";
            _online.text = "FIND OTHER PLAYERS ONLINE";
            _online.color = RcLobby.Exists ? Dim : Color.white;
            _leave.gameObject.SetActive(RcLobby.Exists);
        }

        private static string SteamName()
        {
            try { return Steamworks.SteamFriends.GetPersonaName(); }
            catch (System.Exception) { return "You"; }
        }
    }

    /// <summary>
    /// The door in the Arcade: who you go outside with. INVITE FRIENDS opens your town to them and brings up Steam's
    /// invite window right here; the screen then lists who has joined, and the first button takes you all out.
    /// </summary>
    public class RcDoorMenu : RcMenu
    {
        private static RcDoorMenu _instance;
        private Text _go, _invite, _online, _info, _leave, _dest, _intro;
        private bool _canEgypt;   // (taken the Recall Express once)

        protected override string Title { get { return "OUTSIDE"; } }
        protected override Vector2 PanelSize { get { return new Vector2(1100f, 800f); } }

        public static void Open() { GetOrCreate(ref _instance, "KE_RcDoorMenu").Show(); }

        protected override void BuildContent(Transform p)
        {
            _intro = Fit(MakeText(p, "", 26, TextAnchor.MiddleCenter, new Vector2(0f, 260f), new Vector2(960f, 80f), Dim));
            _go = AddButton(p, new Vector2(0f, 165f), "", GoOut, 620f);
            _invite = AddButton(p, new Vector2(0f, 90f), "", Invite, 620f);
            _online = AddButton(p, new Vector2(0f, 15f), "", Online, 620f);
            Inset(p, new Vector2(0f, -110f), new Vector2(1000f, 170f));
            _info = Fit(MakeText(p, "", 24, TextAnchor.MiddleCenter, new Vector2(0f, -110f), new Vector2(980f, 160f), Dim));
            _dest = AddButton(p, new Vector2(0f, -245f), "", () => { RecalledMode.Dest = RecalledMode.Dest == RcWorlds.EGYPT ? RcWorlds.TOWN : RcWorlds.EGYPT; RefreshAll(); }, 620f);
            _leave = AddButton(p, new Vector2(-200f, -330f), "ON YOUR OWN AGAIN", () => RcLobby.Leave(), 420f);
            AddButton(p, new Vector2(240f, -330f), "STAY", Hide, 240f);
        }

        // Alone, or with whoever's joined: the town's lobby (if there is one) comes along.
        private void GoOut()
        {
            Hide();
            Go();
        }

        // Opens your town to friends and brings up Steam's invite window. Again: invite more.
        private void Invite() { RcLobby.OpenForFriends(true); }

        private void Online()
        {
            if (RcLobby.Exists) return; // (you're already with people: leave them first)
            Hide();
            Go();
            string note;
            RcCharacter c = Rc.Char != null ? Rc.Char : RecalledSave.Load(out note); // (how far you've got: towns near that are tried first)
            RcLobby.GoOnline(c != null ? c.Bosses : 0);
        }

        protected override void OnShown()
        {
            base.OnShown();
            string note;
            RcCharacter c = Rc.Char != null ? Rc.Char : RecalledSave.Load(out note);
            _canEgypt = RcWorlds.EgyptOpen && c != null && c.Quest >= RcWorlds.TOWN_JOBS;
            if (!_canEgypt) RecalledMode.Dest = RcWorlds.TOWN;
        }

        protected override void RefreshAll()
        {
            base.RefreshAll();
            if (_go == null) return;
            bool party = RcLobby.Exists;
            bool follow = party && !RcLobby.IsHost;   // (a friend's party goes where its host is)
            int world = follow ? RcLobby.HostWorld : RecalledMode.Dest;
            _intro.text = world == RcWorlds.EGYPT ? "Egypt. The Line ran out into the desert, and so did the recall. Last Stop is the camp the Resistance holds." : "The town the recalled toys took over. The kids who are left have a treehouse in the park.";
            _dest.gameObject.SetActive(_canEgypt && !follow);
            _dest.text = "DESTINATION:  " + (world == RcWorlds.EGYPT ? "EGYPT (LAST STOP)" : "THE TOWN");
            string[] names = RcLobby.OtherNames();
            _go.text = party ? (names.Length > 0 ? "GO OUT TOGETHER" : "GO OUT") : "GO OUT ALONE";
            _invite.text = party ? "INVITE MORE FRIENDS" : "INVITE FRIENDS";
            _online.text = "GO OUT ONLINE";
            _online.color = party ? Dim : Color.white;
            _leave.gameObject.SetActive(party);
            if (party)
            {
                string who = names.Length == 0 ? "Nobody has joined yet. They can join while you wait here, or once you're outside." : "With you:  " + string.Join(",  ", names);
                _info.text = "Your town is open to the friends you invite (" + RcLobby.Members + " of " + RcLobby.SIZE + ").\n" + who
                    + "\nA friend who accepts is taken straight outside. The toys come out when you do: you run the town.";
                _info.color = Color.white;
            }
            else
            {
                _info.text = (RcLobby.Status.Length > 0 ? RcLobby.Status + "\n" : "")
                    + "INVITE FRIENDS: opens your town to up to three friends and brings up Steam's invite list. You can invite more out there too (P).\nONLINE: you're put in a town with other players near your own progress; if there's none, yours opens and they drop in.\nEveryone keeps their own character.";
                _info.color = Dim;
            }
        }

        /// <summary>Through the door: the level loads and the town is built.</summary>
        public static void Go()
        {
            SceneMonitor.ResetForNewRun();
            RecalledMode.Launch(RcLobby.Exists && !RcLobby.IsHost ? RcLobby.HostWorld : RecalledMode.Dest);
        }
    }
}
