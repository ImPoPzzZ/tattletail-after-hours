using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// The Character Creator (from the OUTFITS screen, with "Kid: Custom"). Four tabs —
    /// BODY (frame, bottoms, skin tone, head shape), HAIR (style, color), FACE (eyes, eye color,
    /// brows, nose, mouth) and TWEAK (sliders: eye spacing/height/size, brow height/angle, mouth
    /// height/size, nose size) and EXTRAS (expression, glasses, freckles, band-aid, eye circles,
    /// Tattletail-colored accents) and VOICE (voice type, pitch, buttons to hear it) — around a live
    /// preview that zooms onto the face for the face tabs.
    /// Starts from the Boy or Girl preset, or Random. Everything saves as you go.
    /// </summary>
    public class CreatorMenu : OverlayPanel
    {
        private static CreatorMenu _instance;

        private enum Tab { Body, Hair, Face, Tweak, Extras, Voice }
        private readonly List<Text> _voiceButtons = new List<Text>();
        private Tab _tab;
        private CharacterSpec _spec;
        private readonly Dictionary<Tab, List<Row>> _rows = new Dictionary<Tab, List<Row>>();
        private readonly Dictionary<Tab, Text> _tabButtons = new Dictionary<Tab, Text>();
        private PreviewStage _stage;
        private RawImage _view;
        private bool _dirty;
        private float _rebuildAt;

        protected override string Title { get { return "CREATE YOUR KID"; } }
        protected override Vector2 PanelSize { get { return new Vector2(1500f, 900f); } }

        public static void Open() { GetOrCreate(ref _instance, "KE_CreatorMenu").Show(); }

        protected override void BuildContent(Transform p)
        {
            var frame = MakeImage(p, new Vector2(-420f, -10f), new Vector2(470f, 660f), Accent);
            MakeImage(frame.transform, Vector2.zero, new Vector2(462f, 652f), new Color(0.02f, 0.02f, 0.03f));
            var go = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(frame.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(440f, 630f);
            _view = go.GetComponent<RawImage>();
            _view.raycastTarget = false;

            const float X = 280f;
            float tx = X - 380f;
            foreach (Tab t in new[] { Tab.Body, Tab.Hair, Tab.Face, Tab.Tweak, Tab.Extras, Tab.Voice })
            {
                Tab tab = t;
                _tabButtons[t] = AddButton(p, new Vector2(tx, 320f), t.ToString().ToUpperInvariant(), () => SetTab(tab), 142f);
                tx += 148f;
                _rows[t] = new List<Row>();
            }

            float y = 230f;
            _rows[Tab.Body].Add(AddStepper(p, X, y, "Body", d => Step(ref _spec.Frame, d, 3), () => CharacterSpec.Frames[_spec.Frame]));
            _rows[Tab.Body].Add(AddStepper(p, X, y - 70f, "Bottoms", d => Step(ref _spec.Bottom, d, 2), () => CharacterSpec.Bottoms[_spec.Bottom]));
            _rows[Tab.Body].Add(AddStepper(p, X, y - 140f, "Skin Tone", d => Step(ref _spec.Skin, d, CharacterSpec.SkinTones.Length), () => CharacterSpec.SkinNames[_spec.Skin]));
            _rows[Tab.Body].Add(AddStepper(p, X, y - 210f, "Head Shape", d => Step(ref _spec.Head, d, 3), () => CharacterSpec.Heads[_spec.Head]));

            _rows[Tab.Hair].Add(AddStepper(p, X, y, "Hairstyle", d => Step(ref _spec.Hair, d, CharacterSpec.Hairs.Length), () => CharacterSpec.Hairs[_spec.Hair]));
            _rows[Tab.Hair].Add(AddStepper(p, X, y - 70f, "Hair Color", d => Step(ref _spec.HairColor, d, CharacterSpec.HairColors.Length), () => CharacterSpec.HairColorNames[_spec.HairColor]));

            _rows[Tab.Face].Add(AddStepper(p, X, y, "Eyes", d => Step(ref _spec.Eyes, d, CharacterSpec.EyeStyles.Length), () => CharacterSpec.EyeStyles[_spec.Eyes]));
            _rows[Tab.Face].Add(AddStepper(p, X, y - 70f, "Eye Color", d => Step(ref _spec.EyeColor, d, CharacterSpec.EyeColors.Length), () => CharacterSpec.EyeColorNames[_spec.EyeColor]));
            _rows[Tab.Face].Add(AddStepper(p, X, y - 140f, "Eyebrows", d => Step(ref _spec.Brows, d, CharacterSpec.BrowStyles.Length), () => CharacterSpec.BrowStyles[_spec.Brows]));
            _rows[Tab.Face].Add(AddStepper(p, X, y - 210f, "Nose", d => Step(ref _spec.Nose, d, CharacterSpec.Noses.Length), () => CharacterSpec.Noses[_spec.Nose]));
            _rows[Tab.Face].Add(AddStepper(p, X, y - 280f, "Mouth", d => Step(ref _spec.Mouth, d, CharacterSpec.Mouths.Length), () => CharacterSpec.Mouths[_spec.Mouth]));

            _rows[Tab.Extras].Add(AddStepper(p, X, y, "Expression", d => Step(ref _spec.Expression, d, CharacterSpec.Expressions.Length), () => CharacterSpec.Expressions[_spec.Expression]));
            _rows[Tab.Extras].Add(AddStepper(p, X, y - 70f, "Glasses", d => Step(ref _spec.Glasses, d, CharacterSpec.GlassesStyles.Length), () => CharacterSpec.GlassesStyles[_spec.Glasses]));
            _rows[Tab.Extras].Add(AddStepper(p, X, y - 140f, "Freckles", d => Step(ref _spec.Freckles, d, CharacterSpec.FreckleAmounts.Length), () => CharacterSpec.FreckleAmounts[_spec.Freckles]));
            _rows[Tab.Extras].Add(AddStepper(p, X, y - 210f, "Band-Aid", d => Step(ref _spec.BandAid, d, CharacterSpec.BandAids.Length), () => CharacterSpec.BandAids[_spec.BandAid]));
            _rows[Tab.Extras].Add(AddStepper(p, X, y - 280f, "Eye Circles", d => Step(ref _spec.EyeCircles, d, CharacterSpec.EyeCircleStyles.Length), () => CharacterSpec.EyeCircleStyles[_spec.EyeCircles]));
            _rows[Tab.Extras].Add(AddStepper(p, X, y - 350f, "Accents", d => Step(ref _spec.Accent, d, CharacterSpec.Accents.Length),
                () => _spec.Accent == 1 ? "Tattletail (" + CharacterSpec.FurNames[_spec.AccentColor] + ")" : "Outfit"));

            _rows[Tab.Voice].Add(AddStepper(p, X, y, "Voice", d => { Step(ref _spec.Voice, d, KidVoice.Types.Length); Hear(KidVoice.Kind.Jump); }, () => KidVoice.Types[_spec.Voice]));
            _rows[Tab.Voice].Add(AddSlider(p, X, y - 70f, "Pitch",
                () => (_spec.VoicePitch + 100f) / 200f,
                v => { _spec.VoicePitch = (sbyte)Mathf.RoundToInt(Mathf.Clamp01(v) * 200f - 100f); CharacterSpec.Local = _spec; },
                () => (_spec.VoicePitch > 0 ? "+" : "") + _spec.VoicePitch));
            _voiceButtons.Add(AddButton(p, new Vector2(X - 250f, y - 180f), "JUMP", () => Hear(KidVoice.Kind.Jump), 220f));
            _voiceButtons.Add(AddButton(p, new Vector2(X, y - 180f), "LAND", () => Hear(KidVoice.Kind.Land), 220f));
            _voiceButtons.Add(AddButton(p, new Vector2(X + 250f, y - 180f), "SEE MAMA", () => Hear(KidVoice.Kind.SeeMama), 240f));
            _voiceButtons.Add(AddButton(p, new Vector2(X - 125f, y - 260f), "MAMA'S GAZE", () => Hear(KidVoice.Kind.Terrified), 280f));
            _voiceButtons.Add(AddButton(p, new Vector2(X + 185f, y - 260f), "CAUGHT", () => Hear(KidVoice.Kind.Caught), 220f));

            Slider(p, X, y, "Eye Spacing", 0);
            Slider(p, X, y - 60f, "Eye Height", 1);
            Slider(p, X, y - 120f, "Eye Size", 2);
            Slider(p, X, y - 180f, "Brow Height", 3);
            Slider(p, X, y - 240f, "Brow Angle", 4);
            Slider(p, X, y - 300f, "Mouth Height", 5);
            Slider(p, X, y - 360f, "Mouth Size", 6);
            Slider(p, X, y - 420f, "Nose Size", 7);

            AddButton(p, new Vector2(-560f, -390f), "BOY", () => Preset(CharacterSpec.BoyPreset()), 200f);
            AddButton(p, new Vector2(-340f, -390f), "GIRL", () => Preset(CharacterSpec.GirlPreset()), 200f);
            AddButton(p, new Vector2(-110f, -390f), "RANDOM", () => Preset(CharacterSpec.Random()), 240f);
            AddButton(p, new Vector2(420f, -390f), "DONE", () =>
            {
                Hide();
                if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
                OutfitMenu.Open(); // back to the outfits screen
            }, 240f);
        }

        // Sliders store -100..100; the track shows 0..1.
        private void Slider(Transform p, float x, float y, string name, int which)
        {
            _rows[Tab.Tweak].Add(AddSlider(p, x, y, name,
                () => (Get(which) + 100f) / 200f,
                v => { Set(which, (sbyte)Mathf.RoundToInt(Mathf.Clamp01(v) * 200f - 100f)); Changed(); },
                () => (Get(which) > 0 ? "+" : "") + Get(which)));
        }

        private sbyte Get(int which)
        {
            switch (which)
            {
                case 0: return _spec.EyeSpacing; case 1: return _spec.EyeHeight; case 2: return _spec.EyeSize; case 3: return _spec.BrowHeight;
                case 4: return _spec.BrowAngle; case 5: return _spec.MouthHeight; case 6: return _spec.MouthSize; default: return _spec.NoseSize;
            }
        }

        private void Set(int which, sbyte v)
        {
            switch (which)
            {
                case 0: _spec.EyeSpacing = v; break; case 1: _spec.EyeHeight = v; break; case 2: _spec.EyeSize = v; break; case 3: _spec.BrowHeight = v; break;
                case 4: _spec.BrowAngle = v; break; case 5: _spec.MouthHeight = v; break; case 6: _spec.MouthSize = v; break; default: _spec.NoseSize = v; break;
            }
        }

        private void Hear(KidVoice.Kind kind) { KidVoice.Preview(kind, _spec.Voice, _spec.VoicePitch / 100f); }

        private void Step(ref byte field, int dir, int count)
        {
            field = (byte)((field + dir + count) % count);
            Changed();
        }

        private void Preset(CharacterSpec spec)
        {
            _spec = spec;
            Changed();
        }

        private void Changed()
        {
            CharacterSpec.Local = _spec; // saves as you go
            _spec.AccentColor = CharacterSpec.Local.AccentColor; // (a preset or RANDOM starts without it)
            _dirty = true;
        }

        private void SetTab(Tab tab)
        {
            _tab = tab;
            foreach (var kv in _rows)
                foreach (var r in kv.Value) r.SetVisible(kv.Key == tab);
            foreach (var kv in _tabButtons) kv.Value.color = kv.Key == tab ? Accent : Color.white;
            foreach (var b in _voiceButtons) b.gameObject.SetActive(tab == Tab.Voice);
            Frame();
        }

        protected override void OnShown()
        {
            _spec = CharacterSpec.Local;
            if (_stage == null) { _stage = new PreviewStage(transform, new Vector3(0f, 2200f, 0f), 440, 630); _view.texture = _stage.Texture; }
            Rebuild();
            SetTab(Tab.Body);
        }

        private void Rebuild()
        {
            _dirty = false;
            _rebuildAt = Time.unscaledTime;
            _stage.Show(CharacterKit.Build("KE_CreatorPreview", _spec, KidSkins.SkinFor(KidGender.Custom)), ChildModel.HEIGHT);
            Frame();
        }

        // Face tabs: the camera moves in on the face.
        private void Frame()
        {
            if (_stage == null) return;
            bool face = _tab == Tab.Face || _tab == Tab.Tweak || _tab == Tab.Hair || _tab == Tab.Voice || (_tab == Tab.Extras && _spec.Accent == 0);
            if (face) _stage.Frame(ChildModel.HEIGHT * 0.86f, 0.42f);
            else _stage.Frame(ChildModel.HEIGHT * 0.5f, ChildModel.HEIGHT * 1.2f); // accents on: the whole kid, sleeves to shoes
            _stage.Spin(face ? 0f : 30f);
        }

        protected override void Update()
        {
            base.Update();
            if (_stage == null) return;
            if (_dirty && Time.unscaledTime - _rebuildAt > 0.08f) Rebuild(); // sliders rebuild at most ~12 times a second
            _stage.Tick(Visible);
        }
    }
}
