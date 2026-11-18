using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// The OUTFITS machine's screen: pick the boy or the girl (both free), or a DLC character
    /// from the "DLC CHARACTERS" dropdown (Dez), and an outfit
    /// (the first is free, the rest OUTFIT_PRICE shards each). A live preview shows the kid
    /// wearing whatever you're looking at, owned or not. This is your look in VERSUS.
    /// </summary>
    public class OutfitMenu : OverlayPanel
    {
        private static OutfitMenu _instance;

        private KidGender _gender;
        private int _skin;
        private Text _status, _buy, _balance, _note, _dlcButton, _edit;
        private Row _kidRow, _outfitRow;
        private bool _dlcOpen;
        private readonly System.Collections.Generic.List<Text> _dlcItems = new System.Collections.Generic.List<Text>();

        // Preview: the kid on a stage far away from everything (PreviewStage).
        private PreviewStage _stage;
        private RawImage _view;

        protected override string Title { get { return "OUTFITS"; } }
        protected override Vector2 PanelSize { get { return new Vector2(1300f, 820f); } }

        public static void Open() { GetOrCreate(ref _instance, "KE_OutfitMenu").Show(); }

        protected override void BuildContent(Transform p)
        {
            var frame = MakeImage(p, new Vector2(-360f, -10f), new Vector2(430f, 610f), Accent);
            MakeImage(frame.transform, Vector2.zero, new Vector2(422f, 602f), new Color(0.02f, 0.02f, 0.03f));
            var go = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(frame.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400f, 580f);
            _view = go.GetComponent<RawImage>();
            _view.raycastTarget = false;

            const float X = 250f;
            // DLC characters: a dropdown at the top of the column; while it's open, it covers the rest.
            _dlcButton = AddButton(p, new Vector2(X, 290f), "DLC CHARACTERS  v", ToggleDlc, 520f);
            _dlcButton.gameObject.AddComponent<RainbowCycler>();
            float y = 225f;
            foreach (KidGender g in KidSkins.DlcCharacters)
            {
                KidGender pick = g;
                var item = AddButton(p, new Vector2(X, y), KidSkins.CharacterName(g).ToUpperInvariant(), () => PickDlc(pick), 420f);
                item.gameObject.AddComponent<RainbowCycler>();
                item.gameObject.SetActive(false);
                _dlcItems.Add(item);
                y -= 65f;
            }

            _kidRow = AddStepper(p, X, 210f, "Kid", StepGender, () => KidSkins.CharacterName(_gender) + (KidSkins.IsDlc(_gender) ? " (DLC)" : ""));
            _outfitRow = AddStepper(p, X, 140f, "Outfit", StepOutfit, () => KidSkins.For(_gender)[_skin].Name);
            _status = Fit(MakeText(p, "", 30, TextAnchor.MiddleCenter, new Vector2(X, 50f), new Vector2(700f, 50f), Accent));
            _edit = AddButton(p, new Vector2(X, -160f), "EDIT CHARACTER", () => { Hide(); CreatorMenu.Open(); }, 460f);
            _buy = AddButton(p, new Vector2(X, -30f), "BUY", Buy, 420f);
            _balance = Fit(MakeText(p, "", 26, TextAnchor.MiddleCenter, new Vector2(X, -110f), new Vector2(700f, 40f), Dim));
            _note = Fit(MakeText(p, "Your look in VERSUS. Both kids and DLC characters are free.\nOutfits are " + KidSkins.OUTFIT_PRICE + " shards each.",
                24, TextAnchor.MiddleCenter, new Vector2(X, -210f), new Vector2(700f, 80f), Dim));

            AddButton(p, new Vector2(0f, -350f), "BACK", () =>
            {
                Hide();
                if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            });
        }

        protected override void OnShown()
        {
            _gender = KidSkins.Gender;
            _skin = KidSkins.Selected;
            SetDlcOpen(false);
            if (_stage == null) { _stage = new PreviewStage(transform, new Vector3(0f, 2000f, 0f), 400, 580); _view.texture = _stage.Texture; }
            Dress();
        }

        // You remembered who you are (Identity: the Arcade had erased it).
        protected override void OnHidden() { Identity.Remember(); }

        private void ToggleDlc() { SetDlcOpen(!_dlcOpen); }

        private void SetDlcOpen(bool open)
        {
            _dlcOpen = open;
            if (_dlcButton == null) return;
            _dlcButton.text = "[ DLC CHARACTERS  " + (open ? "^" : "v") + " ]";
            foreach (var item in _dlcItems) item.gameObject.SetActive(open);
            _kidRow.SetVisible(!open);
            _outfitRow.SetVisible(!open);
            _status.gameObject.SetActive(!open);
            _balance.gameObject.SetActive(!open);
            _note.gameObject.SetActive(!open);
        }

        private void PickDlc(KidGender g)
        {
            _gender = g;
            KidSkins.SetGender(g); // DLC characters are free
            _skin = KidSkins.SkinFor(g);
            SetDlcOpen(false);
            Dress();
            HubMode.SetHudText("You're playing as " + KidSkins.CharacterName(g) + "!");
        }

        private void StepGender(int dir)
        {
            // Boy -> Girl -> Custom (the Character Creator); a DLC character steps back to the kids.
            _gender = _gender == KidGender.Boy ? (dir > 0 ? KidGender.Girl : KidGender.Custom)
                    : _gender == KidGender.Girl ? (dir > 0 ? KidGender.Custom : KidGender.Boy)
                    : _gender == KidGender.Custom ? (dir > 0 ? KidGender.Boy : KidGender.Girl)
                    : KidGender.Boy;
            KidSkins.SetGender(_gender); // both kids are free
            _skin = KidSkins.SkinFor(_gender);
            Dress();
        }

        private void StepOutfit(int dir)
        {
            int n = KidSkins.For(_gender).Length;
            _skin = (_skin + dir + n) % n;
            KidSkins.Equip(_gender, _skin); // only if owned; otherwise you just see it
            Dress();
        }

        private void Buy()
        {
            if (KidSkins.Owned(_gender, _skin)) return;
            if (!KidSkins.Buy(_gender, _skin))
            {
                HubMode.SetHudText("You need " + KidSkins.OUTFIT_PRICE + " shards. You have " + Shards.Balance + ".");
                return;
            }
            HubMode.SetHudText("New outfit: " + KidSkins.For(_gender)[_skin].Name + "!");
        }

        protected override void RefreshAll()
        {
            base.RefreshAll();
            if (_status == null) return;
            bool owned = KidSkins.Owned(_gender, _skin);
            bool wearing = owned && KidSkins.Gender == _gender && KidSkins.Selected == _skin;
            _status.text = wearing ? "WEARING" : owned ? "OWNED" : "LOCKED";
            _status.color = owned ? Accent : new Color(1f, 0.4f, 0.4f);
            _buy.gameObject.SetActive(!owned && !_dlcOpen);
            _edit.gameObject.SetActive(_gender == KidGender.Custom && !_dlcOpen);
            _buy.text = "[ BUY  (" + KidSkins.OUTFIT_PRICE + " shards) ]";
            _balance.text = "You have " + Shards.Balance + " shards";
        }

        // ── Preview ──────────────────────────────────────────────────────────
        /// <summary>Puts the kid you're looking at (boy/girl, outfit) on the preview.</summary>
        private void Dress()
        {
            if (_stage == null) return;
            GameObject kid = _stage.Subject;
            if (kid != null && kid.name == "PreviewKid_" + _gender && _gender != KidGender.Custom) { kid.GetComponent<KidLook>().Set(_skin); return; }
            _stage.Show(ChildModel.Build("PreviewKid_" + _gender, _gender, _skin), ChildModel.HeightOf(_gender));
            if (_gender == KidGender.Rat) _stage.Frame(0.48f, 1.65f); // wider than he is tall: his tail stays in view as he turns
        }

        protected override void Update()
        {
            base.Update();
            if (_stage != null) _stage.Tick(Visible);
        }
    }
}
