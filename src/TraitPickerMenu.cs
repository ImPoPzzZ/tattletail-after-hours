using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// The PROFILE screen (3.7), opened from the Endless setup screen. It is drawn as Waygetter's file on you: a headline, the two
    /// slots as cards, the 34 traits in two groups (steady in green, conditions in orange) as rows that light up under the
    /// mouse, and a paper card that spells out the bonus and the price of whatever you point at. Click a row to pick it (two at
    /// most) and click again to drop it; one that can't pair with your other pick is greyed out and says why.
    /// </summary>
    public class TraitPickerMenu : OverlayPanel
    {
        private static TraitPickerMenu _instance;

        private static readonly Color SteadyTint = new Color(0.50f, 0.88f, 0.58f), ConditionTint = new Color(1f, 0.64f, 0.38f);
        private static readonly Color RowBase = new Color(0.10f, 0.10f, 0.13f, 0.92f), RowHover = new Color(0.19f, 0.21f, 0.27f, 0.97f);
        private static readonly Color PaperInk = new Color(0.13f, 0.09f, 0.07f), PaperColor = new Color(0.94f, 0.90f, 0.80f, 1f);

        private readonly Image[] _plate = new Image[EndlessTraits.Count], _mark = new Image[EndlessTraits.Count];
        private readonly Outline[] _edge = new Outline[EndlessTraits.Count];
        private readonly Text[] _name = new Text[EndlessTraits.Count];
        private readonly Image[] _slotBg = new Image[2];
        private readonly Outline[] _slotEdge = new Outline[2];
        private readonly Text[] _slotText = new Text[2];
        private Text _title, _detail;
        private int _hover = -1, _justOpened = -1;
        private string _note = "";

        protected override string Title { get { return "YOUR PROFILE"; } }
        protected override Vector2 PanelSize { get { return new Vector2(1640f, 940f); } }
        protected override int SortOrder { get { return 2200; } }

        public static void Open() { GetOrCreate(ref _instance, "KE_TraitPickerMenu").Show(); }

        protected override void OnShown() { _note = ""; _hover = -1; _justOpened = Time.frameCount; }

        protected override void BuildContent(Transform p)
        {
            Fit(MakeText(p, "WAYGETTER ELECTRONICS   -   LISTENING PROGRAM   -   SUBJECT FILE", 20, TextAnchor.MiddleCenter, new Vector2(0f, 372f), new Vector2(1300f, 30f), new Color(0.62f, 0.62f, 0.7f)));
            MakeImage(p, new Vector2(0f, 354f), new Vector2(1500f, 2f), new Color(0.75f, 0.2f, 0.18f, 0.8f));

            // The headline: what your two picks make you.
            _title = Fit(MakeText(p, "", 44, TextAnchor.MiddleCenter, new Vector2(0f, 318f), new Vector2(1300f, 56f), new Color(0.92f, 0.3f, 0.24f)));

            // The two slots.
            for (int i = 0; i < 2; i++)
            {
                Vector2 at = new Vector2(i == 0 ? -300f : 300f, 252f);
                _slotBg[i] = MakeImage(p, at, new Vector2(560f, 84f), new Color(0.09f, 0.09f, 0.12f, 0.95f));
                _slotEdge[i] = _slotBg[i].gameObject.AddComponent<Outline>();
                _slotEdge[i].effectDistance = new Vector2(2f, -2f);
                _slotText[i] = Fit(MakeText(p, "", 28, TextAnchor.MiddleCenter, at, new Vector2(520f, 76f), Color.white));
            }

            // The traits, in two groups.
            float[] cx = { -545f, 0f, 545f };
            Fit(MakeText(p, "STEADY", 24, TextAnchor.MiddleCenter, new Vector2(cx[0], 190f), new Vector2(490f, 30f), SteadyTint));
            Fit(MakeText(p, "CONDITIONS", 24, TextAnchor.MiddleCenter, new Vector2((cx[1] + cx[2]) / 2f, 190f), new Vector2(1000f, 30f), ConditionTint));
            MakeImage(p, new Vector2(cx[0], 172f), new Vector2(490f, 2f), new Color(SteadyTint.r, SteadyTint.g, SteadyTint.b, 0.6f));
            MakeImage(p, new Vector2((cx[1] + cx[2]) / 2f, 172f), new Vector2(1035f, 2f), new Color(ConditionTint.r, ConditionTint.g, ConditionTint.b, 0.6f));

            int steadyRow = 0, condRow = 0;
            for (int i = 0; i < EndlessTraits.Count; i++)
            {
                bool steady = EndlessTraits.IsSteady(i);
                int col, row;
                if (steady) { col = 0; row = steadyRow++; }
                else { int n = condRow++; col = n < 11 ? 1 : 2; row = n < 11 ? n : n - 11; }
                Vector2 at = new Vector2(cx[col], 146f - row * 34f);
                Color tint = steady ? SteadyTint : ConditionTint;
                _plate[i] = MakeImage(p, at, new Vector2(490f, 31f), RowBase);
                _edge[i] = _plate[i].gameObject.AddComponent<Outline>();
                _edge[i].effectColor = new Color(0f, 0f, 0f, 0f);
                _edge[i].effectDistance = new Vector2(2f, -2f);
                _mark[i] = MakeImage(p, at + new Vector2(-232f, 0f), new Vector2(8f, 21f), new Color(tint.r, tint.g, tint.b, 0.35f));
                _name[i] = Fit(MakeText(p, EndlessTraits.NameOf(i), 22, TextAnchor.MiddleLeft, at + new Vector2(14f, 0f), new Vector2(440f, 29f), tint));
            }

            // The paper card: what you're pointing at.
            MakeImage(p, new Vector2(0f, -359f), new Vector2(1500f, 104f), new Color(0.35f, 0.3f, 0.22f, 1f));
            MakeImage(p, new Vector2(0f, -359f), new Vector2(1494f, 98f), PaperColor);
            _detail = Fit(MakeText(p, "", 24, TextAnchor.MiddleLeft, new Vector2(0f, -359f), new Vector2(1450f, 94f), PaperInk));
            _detail.supportRichText = true;

            AddButton(p, new Vector2(-660f, 420f), "CLEAR", () => { EndlessTraits.Clear(); _note = ""; }, 240f);
            AddButton(p, new Vector2(660f, 420f), "DONE", Hide, 240f);
        }

        protected override void Update()
        {
            base.Update();
            if (!Visible) return;
            _hover = -1;
            for (int i = 0; i < _plate.Length; i++)
                if (_plate[i] != null && Hit(_plate[i].rectTransform)) { _hover = i; break; }
            if (Input.GetMouseButtonDown(0) && _hover >= 0 && _justOpened != Time.frameCount)
            {
                _note = EndlessTraits.Toggle(_hover);
                Click();
            }
        }

        protected override void RefreshAll()
        {
            base.RefreshAll();
            if (_title == null) return;
            int a = EndlessTraits.Pick(0), b = EndlessTraits.Pick(1);
            _title.text = a < 0 && b < 0 ? "NO PROFILE YET" : EndlessTraits.TitleOf(a, b).ToUpper();

            int[] picks = { a, b };
            for (int i = 0; i < 2; i++)
            {
                int id = picks[i];
                Color tint = id < 0 ? new Color(0.5f, 0.5f, 0.55f) : (EndlessTraits.IsSteady(id) ? SteadyTint : ConditionTint);
                _slotEdge[i].effectColor = id < 0 ? new Color(0.35f, 0.35f, 0.4f, 0.8f) : tint;
                _slotBg[i].color = id < 0 ? new Color(0.08f, 0.08f, 0.1f, 0.95f) : new Color(tint.r * 0.22f, tint.g * 0.22f, tint.b * 0.22f, 0.97f);
                _slotText[i].color = Color.white;
                _slotText[i].text = id < 0 ? "SLOT " + (i + 1) + "\nempty: pick a trait below"
                    : "SLOT " + (i + 1) + "   -   " + (EndlessTraits.IsSteady(id) ? "STEADY" : "CONDITION") + "\n" + EndlessTraits.NameOf(id).ToUpper();
            }

            int other = a >= 0 && b >= 0 ? -1 : (a >= 0 ? a : b);
            for (int i = 0; i < _plate.Length; i++)
            {
                if (_plate[i] == null) continue;
                bool picked = i == a || i == b;
                bool blocked = !picked && other >= 0 && EndlessTraits.Conflicts(i, other);
                bool hot = i == _hover;
                Color tint = EndlessTraits.IsSteady(i) ? SteadyTint : ConditionTint;
                Color plate = RowBase;
                if (hot && !blocked) plate = RowHover;
                if (picked) plate = new Color(tint.r * 0.28f, tint.g * 0.28f, tint.b * 0.28f, 0.98f);
                _plate[i].color = plate;
                _edge[i].effectColor = picked ? tint : (hot && !blocked ? new Color(tint.r, tint.g, tint.b, 0.5f) : new Color(0f, 0f, 0f, 0f));
                _mark[i].color = picked ? tint : new Color(tint.r, tint.g, tint.b, blocked ? 0.12f : 0.35f);
                _name[i].color = blocked ? new Color(0.38f, 0.38f, 0.42f) : (picked ? Color.white : tint);
            }

            string s;
            if (_note.Length > 0) s = "<color=#A61F1A>" + _note + "</color>";
            else if (_hover >= 0)
            {
                bool steady = EndlessTraits.IsSteady(_hover);
                s = "<b>" + EndlessTraits.NameOf(_hover).ToUpper() + "</b>   " + (steady ? "<color=#1B6B2E>STEADY</color>" : "<color=#B3590F>CONDITION</color>")
                    + "\n<color=#1B6B2E><b>BONUS</b></color>  " + EndlessTraits.BonusOf(_hover)
                    + "\n<color=#8A1F1A><b>PRICE</b></color>  " + EndlessTraits.PriceOf(_hover);
            }
            else s = "<b>HOW IT WORKS</b>\nPoint at a trait to read its bonus and its price. Click to pick it (two at most), click again to drop it.\nTraits never earn extra shards: they change how the run plays.";
            if (_note.Length == 0 && a >= 0 && b >= 0 && EndlessTraits.ComboOf(a, b) != EndlessTraits.Combo.None)
                s += "\n<color=#8A6A10><b>COMBO  " + EndlessTraits.ComboName(EndlessTraits.ComboOf(a, b)).ToUpper() + "</b></color>  " + EndlessTraits.ComboNote(EndlessTraits.ComboOf(a, b));
            _detail.text = s;
        }
    }
}
