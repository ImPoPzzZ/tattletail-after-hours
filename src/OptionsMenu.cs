using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    // The mod's own delegate types: this game's old Mono runtime can't load
    // .NET 4's System.Func/System.Action (they live in a different assembly
    // there), and using them made the whole plugin fail to load.
    public delegate float FloatGetter();
    public delegate void FloatSetter(float value);
    public delegate string TextGetter();
    public delegate void StepHandler(int dir);
    public delegate void ClickHandler();

    /// <summary>
    /// A full-screen, mouse-driven settings panel that works inside any level
    /// (its own hit-testing, so no UI EventSystem is needed). While open the
    /// player is frozen and the cursor is free. Subclasses just lay out rows
    /// (sliders, steppers, toggles) and bottom buttons.
    /// </summary>
    public abstract partial class OverlayPanel : MonoBehaviour
    {
        public static bool AnyOpen { get { return _openCount > 0; } }
        private static int _openCount;
        private static float _closedAt = -10f;
        private static int _closedFrame = -1;
        private static readonly List<OverlayPanel> _openPanels = new List<OverlayPanel>();   // (so a screen opened from another one is the only one that hears clicks)

        /// <summary>A menu is open, or just closed (the click that closed it mustn't land on the machine behind it).</summary>
        public static bool BlocksWorld { get { return _openCount > 0 || Time.unscaledTime - _closedAt < 0.3f; } }

        protected static readonly Color Accent = new Color(0.2f, 0.9f, 1f);
        protected static readonly Color Dim = new Color(0.72f, 0.72f, 0.75f);

        /// <summary>Which panel draws on top when two are open (a screen opened from another one is higher).</summary>
        protected virtual int SortOrder { get { return 2100; } }
        protected abstract string Title { get; }
        protected abstract Vector2 PanelSize { get; }
        protected abstract void BuildContent(Transform panel);

        private GameObject _panel;
        private Font _font;
        private readonly List<Row> _rows = new List<Row>();
        private readonly List<KeyValuePair<Text, ClickHandler>> _buttons = new List<KeyValuePair<Text, ClickHandler>>();
        private Row _dragging;
        private bool _openedThisFrame;

        public bool Visible { get { return _panel != null && _panel.activeSelf; } }

        protected class Row
        {
            public Text label, value, left, right;
            public RectTransform track, fill;
            public FloatGetter get01;
            public FloatSetter set01;
            public TextGetter valueText;
            public ClickHandler onClick;
            public StepHandler onStep;

            public bool Visible { get { return label.gameObject.activeSelf; } }

            public void SetVisible(bool v)
            {
                foreach (Graphic g in new Graphic[] { label, value, left, right })
                    if (g != null) g.gameObject.SetActive(v);
                if (track != null) track.gameObject.SetActive(v);
            }
        }

        // The UI is built the first time the screen opens, never in Awake:
        // building a Canvas while Unity is still creating the object hard-
        // crashed the game (the shop screens, 2026-09-24).
        public void Show()
        {
            if (Visible) return;
            if (_panel == null) BuildUI();
            _panel.SetActive(true);
            _openCount++;
            _openPanels.Add(this);
            _openedThisFrame = true;
            if (RM.fpsController != null) RM.fpsController.enabled = false;
            GS.acceptInput = false;
            OnShown();
            RefreshAll();
            if (Rpg) HoverPlates();
        }

        public void Hide()
        {
            if (!Visible) return;
            _panel.SetActive(false);
            _openCount = Mathf.Max(0, _openCount - 1);
            _openPanels.Remove(this);
            _closedAt = Time.unscaledTime;
            _closedFrame = Time.frameCount;
            _dragging = null;
            PlayerPrefs.Save();
            // (a screen opened from another one, like the profile screen: the one beneath is still open, so the player stays frozen)
            if (_openCount == 0)
            {
                if (RM.fpsController != null) RM.fpsController.enabled = true;
                GS.acceptInput = true;
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
            OnHidden();
        }

        protected virtual void OnShown() { }
        protected virtual void OnHidden() { }

        private void OnDestroy()
        {
            if (Visible) Hide(); // never leave the player frozen
        }

        protected virtual void Update()
        {
            if (!Visible) return;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            // The hold-to-interact that opened this may still be releasing — ignore its click.
            if (_openedThisFrame) { _openedThisFrame = false; return; }
            if (Rpg) HoverPlates();

            // A click belongs to the top screen only: not to one beneath it, and not (the same frame) to what a screen it just closed was covering.
            bool covered = false;
            for (int i = 0; i < _openPanels.Count; i++) if (_openPanels[i] != this && _openPanels[i].SortOrder > SortOrder) covered = true;
            if (Input.GetMouseButtonDown(0) && !covered && _closedFrame != Time.frameCount)
            {
                foreach (var b in _buttons)
                    if (b.Key.gameObject.activeInHierarchy && Hit(b.Key.rectTransform)) { Click(); b.Value(); return; } // hidden buttons don't count

                foreach (var r in _rows)
                {
                    if (!r.Visible) continue;
                    if (r.track != null && Hit(r.track)) { _dragging = r; break; }
                    if (r.onStep != null)
                    {
                        if (Hit(r.left.rectTransform)) { r.onStep(-1); Click(); break; }
                        if (Hit(r.right.rectTransform)) { r.onStep(1); Click(); break; }
                    }
                    if (r.onClick != null && (Hit(r.label.rectTransform) || Hit(r.value.rectTransform))) { r.onClick(); Click(); break; }
                }
            }

            if (_dragging != null)
            {
                Vector2 local;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_dragging.track, Input.mousePosition, null, out local))
                {
                    Rect rect = _dragging.track.rect;
                    _dragging.set01(Mathf.InverseLerp(rect.xMin, rect.xMax, local.x));
                }
                if (!Input.GetMouseButton(0)) _dragging = null;
            }

            RefreshAll();
        }

        protected static bool Hit(RectTransform rt) { return RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, null); }

        protected static void Click()
        {
            if (Camera.main != null) AudioController.Play("vhs_glitch", Camera.main.transform);
        }

        protected virtual void RefreshAll()
        {
            foreach (var r in _rows)
            {
                if (!r.Visible) continue;
                if (r.valueText != null) r.value.text = r.valueText();
                if (r.fill != null) r.fill.anchorMax = new Vector2(Mathf.Clamp01(r.get01()), 1f);
                if (r.onClick != null) r.value.color = r.value.text == "ON" ? Accent : Dim;
            }
        }

        // ── Row builders ─────────────────────────────────────────────────────
        // Every row fits inside one column, ROW_HALF to either side of x:
        // label on the left half, its control on the right half. Long text
        // shrinks to fit instead of spilling into its neighbours.
        protected const float ROW_HALF = 380f;

        protected Row AddSlider(Transform p, float x, float y, string name, FloatGetter get01, FloatSetter set01, TextGetter valueText)
        {
            var r = new Row { get01 = get01, set01 = set01, valueText = valueText };
            r.label = Label(p, x, y, name, 360f);
            var track = MakeImage(p, new Vector2(x + 150f, y), new Vector2(240f, 14f), new Color(0.2f, 0.2f, 0.24f));
            r.track = track.rectTransform;
            var fill = MakeImage(track.transform, Vector2.zero, Vector2.zero, Accent);
            r.fill = fill.rectTransform;
            r.fill.anchorMin = Vector2.zero;
            r.fill.pivot = new Vector2(0f, 0.5f);
            r.fill.offsetMin = r.fill.offsetMax = Vector2.zero;
            r.value = Fit(MakeText(p, "", 26, TextAnchor.MiddleRight, new Vector2(x + 335f, y), new Vector2(90f, 50f), Dim));
            _rows.Add(r);
            return r;
        }

        protected Row AddStepper(Transform p, float x, float y, string name, StepHandler onStep, TextGetter valueText)
        {
            var r = new Row { onStep = onStep, valueText = valueText };
            r.label = Label(p, x, y, name, 360f);
            r.left = MakeText(p, "<", 40, TextAnchor.MiddleCenter, new Vector2(x + 45f, y), new Vector2(50f, 50f), Accent);
            r.value = Fit(MakeText(p, "", 26, TextAnchor.MiddleCenter, new Vector2(x + 200f, y), new Vector2(200f, 50f), Color.white));
            r.right = MakeText(p, ">", 40, TextAnchor.MiddleCenter, new Vector2(x + 355f, y), new Vector2(50f, 50f), Accent);
            _rows.Add(r);
            return r;
        }

        /// <summary>valueText should return "ON"/"OFF" (anything else, e.g. "LOCKED", shows dim).</summary>
        protected Row AddToggle(Transform p, float x, float y, string name, ClickHandler onClick, TextGetter valueText)
        {
            var r = new Row { onClick = onClick, valueText = valueText };
            r.label = Label(p, x, y, name, 360f);
            r.value = Fit(MakeText(p, "", 26, TextAnchor.MiddleCenter, new Vector2(x + 200f, y), new Vector2(200f, 50f), Accent));
            _rows.Add(r);
            return r;
        }

        protected Text AddHeader(Transform p, float x, float y, string text)
        {
            return Fit(MakeText(p, text, 26, TextAnchor.MiddleLeft, new Vector2(x, y), new Vector2(ROW_HALF * 2f, 40f), Accent));
        }

        /// <summary>A row's name: left-aligned, starting at the column's left edge.</summary>
        private Text Label(Transform p, float x, float y, string name, float width)
        {
            return Fit(MakeText(p, name, 28, TextAnchor.MiddleLeft, new Vector2(x - ROW_HALF + width / 2f, y), new Vector2(width, 50f), Color.white));
        }

        /// <summary>Shrinks the text (down to 14px) if it doesn't fit its box.</summary>
        protected static Text Fit(Text t)
        {
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 14;
            t.resizeTextMaxSize = t.fontSize;
            return t;
        }

        protected Text AddButton(Transform p, Vector2 pos, string text, ClickHandler onClick, float width = 300f)
        {
            Image plate = null;
            if (Rpg) plate = MakeImage(p, pos, new Vector2(width, 54f), Plate); // (under the text)
            Text t = Fit(MakeText(p, Rpg ? text : "[ " + text + " ]", 34, TextAnchor.MiddleCenter, pos, new Vector2(width, 60f), Color.white));
            if (plate != null) AttachPlate(plate, t);
            _buttons.Add(new KeyValuePair<Text, ClickHandler>(t, onClick));
            return t;
        }

        // ── UI ───────────────────────────────────────────────────────────────
        private void BuildUI()
        {
            _font = MainMenuButtonInjector.RealFont != null ? MainMenuButtonInjector.RealFont : Resources.GetBuiltinResource<Font>("Arial.ttf");

            var canvasGO = new GameObject("KE_OverlayCanvas", typeof(RectTransform));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortOrder;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            _panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(canvasGO.transform, false);
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = prt.offsetMax = Vector2.zero;
            var dim = _panel.GetComponent<Image>();
            dim.color = Rpg ? new Color(0.02f, 0.012f, 0.01f, 0.8f) : new Color(0f, 0f, 0f, 0.7f);
            dim.raycastTarget = false;

            if (Rpg) { BuildContent(BuildRpgFrame(_panel.transform)); return; }

            var box = MakeImage(_panel.transform, Vector2.zero, PanelSize, new Color(0.05f, 0.05f, 0.07f, 0.97f));
            var outline = box.gameObject.AddComponent<Outline>();
            outline.effectColor = Accent;
            outline.effectDistance = new Vector2(3f, -3f);

            MakeText(_panel.transform, Title, 52, TextAnchor.MiddleCenter, new Vector2(0f, PanelSize.y / 2f - 50f), new Vector2(800f, 70f), Accent);
            BuildContent(_panel.transform);
        }

        protected Text MakeText(Transform parent, string text, int size, TextAnchor anchor, Vector2 pos, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        protected static Image MakeImage(Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject("Image", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        protected static T GetOrCreate<T>(ref T instance, string name) where T : OverlayPanel
        {
            // (Recalled's menus say different things in each world: made again when you've changed worlds)
            if (instance != null && instance.BuiltWorld != RcWorlds.Now && instance is RcMenu) { Destroy(instance.gameObject); instance = null; }
            if (instance == null)
            {
                var go = new GameObject(name);
                DontDestroyOnLoad(go);
                instance = go.AddComponent<T>();
                instance.BuiltWorld = RcWorlds.Now;
            }
            return instance;
        }
    }

    /// <summary>
    /// Everything the game's own options menu has (sensitivity, invert mouse,
    /// brightness, motion blur, anti-aliasing, ambient occlusion) plus a master
    /// volume slider. Saved exactly where the game saves them and applied live.
    /// </summary>
    public class OptionsMenu : OverlayPanel
    {
        private static OptionsMenu _instance;
        private const string VOLUME_KEY = "KE_VOLUME";
        private const float BRIGHT_MIN = 2.5f, BRIGHT_MAX = 5.5f; // same range as the game's own brightness screen

        protected override string Title { get { return "OPTIONS"; } }
        protected override Vector2 PanelSize { get { return new Vector2(860f, 980f); } }

        public static void Open() { GetOrCreate(ref _instance, "KE_OptionsMenu").Show(); }

        /// <summary>Applies the saved volume — call once at startup.</summary>
        public static void ApplySavedVolume()
        {
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VOLUME_KEY, 1f));
        }

        private static void ApplyVisuals()
        {
            try { if (RM.fpsController != null) RM.fpsController.UpdateProcessingProfile(); }
            catch (Exception e) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Options] Couldn't apply visuals live: " + e.Message); }
        }

        /// <summary>0–100% effect strength; 100% = the game's original look (defaults in BlurFilter).</summary>
        private void FxSlider(Transform p, float y, string name, BlurFilter.Fx fx)
        {
            AddSlider(p, 0f, y, name, () => BlurFilter.Get(fx), v => BlurFilter.Set(fx, v), () => Mathf.RoundToInt(BlurFilter.Get(fx) * 100f) + "%");
        }

        protected override void BuildContent(Transform p)
        {
            float y = 330f;
            const float step = 58f;

            AddSlider(p, 0f, y, "Volume",
                () => AudioListener.volume,
                v => { AudioListener.volume = v; PlayerPrefs.SetFloat(VOLUME_KEY, v); },
                () => Mathf.RoundToInt(AudioListener.volume * 100f) + "%");
            y -= step;
            AddSlider(p, 0f, y, "Brightness",
                () => Mathf.InverseLerp(BRIGHT_MIN, BRIGHT_MAX, GD.brightness),
                v => { GD.brightness = Mathf.Lerp(BRIGHT_MIN, BRIGHT_MAX, v); GD.SaveBrightness(); ApplyVisuals(); },
                () => Mathf.RoundToInt(Mathf.InverseLerp(BRIGHT_MIN, BRIGHT_MAX, GD.brightness) * 100f) + "%");
            y -= step;
            AddStepper(p, 0f, y, "Mouse Sensitivity",
                d => { GD.mouseSensitivity = Mathf.Clamp(GD.mouseSensitivity + d, GD.minSensitivity, GD.maxSensitivity); GD.SaveSensitivity(); },
                () => GD.mouseSensitivity.ToString());
            y -= step;
            AddToggle(p, 0f, y, "Invert Mouse", () => { GD.invert = !GD.invert; GD.SaveInvert(); }, () => GD.invert ? "ON" : "OFF");
            y -= step;
            AddToggle(p, 0f, y, "Motion Blur", () => { GD.motionBlur = !GD.motionBlur; GD.SaveMotionBlur(); ApplyVisuals(); }, () => GD.motionBlur ? "ON" : "OFF");
            y -= step;
            AddToggle(p, 0f, y, "Anti-Aliasing", () => { GD.AA = !GD.AA; GD.SaveAA(); ApplyVisuals(); }, () => GD.AA ? "ON" : "OFF");
            y -= step;
            AddToggle(p, 0f, y, "Ambient Occlusion", () => { GD.AO = !GD.AO; GD.SaveAO(); ApplyVisuals(); }, () => GD.AO ? "ON" : "OFF");
            y -= step;
            FxSlider(p, y, "Blur", BlurFilter.Fx.Blur); y -= step;
            FxSlider(p, y, "Bloom", BlurFilter.Fx.Bloom); y -= step;
            FxSlider(p, y, "Glow", BlurFilter.Fx.Glow); y -= step;
            FxSlider(p, y, "Film Grain & Fringing", BlurFilter.Fx.Film);

            AddButton(p, new Vector2(-170f, -370f), "SUPPORT THE MOD", Support.Open, 420f); // opens Ko-fi in the browser
            AddButton(p, new Vector2(0f, -440f), "JOIN OUR DISCORD SERVER", Support.OpenDiscord, 560f); // opens the invite in the browser
            AddButton(p, new Vector2(230f, -370f), "CLOSE", () =>
            {
                Hide();
                if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            }, 260f);
        }
    }

    /// <summary>
    /// The Endless machine's setup screen: difficulty, gadget, run modifiers
    /// and cosmetics (the same picks the old menu used), then PLAY.
    /// </summary>
    public class EndlessSetupMenu : OverlayPanel
    {
        private static EndlessSetupMenu _instance;
        private Text _details;
        private Row _gadgetRow, _profileRow, _mapRow;
        private readonly List<Row> _modRows = new List<Row>();

        /// <summary>Dev builds only: Endless in the Waygetter Facility (Mama off, the marking tool on) instead of the house.</summary>
        public static bool FacilityMap;
        private static bool FacilityChosen { get { return FacilityMap; } }

        protected override string Title { get { return "ENDLESS"; } }
        protected override Vector2 PanelSize { get { return new Vector2(1640f, 940f); } }

        public static void Open() { GetOrCreate(ref _instance, "KE_EndlessSetupMenu").Show(); }

        protected override void OnShown()
        {
            CustomizationSubmenu.SetMode(false); // Endless picks, not the party's
            CustomizationSubmenu.Refresh();      // drop anything no longer owned
        }

        // Two columns, one thing per row, and ONE box underneath for whatever you point at (it used to be four boxes squeezed in).
        //   left:  RUN (difficulty, gadget, profile, map), then LOOK (color, beam, voice, Mama)
        //   right: the run modifiers, then DETAILS
        protected override void BuildContent(Transform p)
        {
            const float L = -410f, R = 410f, step = 55f; // columns span ±ROW_HALF: -790..-30 and 30..790

            AddHeader(p, L, 360f, "RUN");
            AddStepper(p, L, 305f, "Difficulty", EndlessSubmenu.StepDifficulty, () => EndlessSubmenu.DifficultyName);
            _gadgetRow = AddStepper(p, L, 250f, "Gadget", StepGadget, () => Gadgets.Name(Gadgets.SelectedIfOwned) + (Gadgets.IsDlc(Gadgets.SelectedIfOwned) ? " (DLC)" : ""));
            _profileRow = AddToggle(p, L, 195f, "Profile (2 traits)", TraitPickerMenu.Open, () => EndlessTraits.ShortSummary());
            _mapRow = AddStepper(p, L, 140f, "Map", StepMap, () => FacilityChosen ? "Waygetter Facility" : "The House");

            AddHeader(p, L, 60f, "LOOK");
            AddStepper(p, L, 5f, "Tattletail Color", CustomizationSubmenu.StepColor, () => CustomizationSubmenu.ColorName);
            AddStepper(p, L, -50f, "Flashlight Beam", CustomizationSubmenu.StepBeam, () => CustomizationSubmenu.BeamName);
            AddToggle(p, L, -105f, "Kaleidoscope Voice", CustomizationSubmenu.ToggleVoicePublic,
                () => !CustomizationSubmenu.VoiceOwned ? "LOCKED" : (CustomizationSubmenu.KaleidoscopeVoice ? "ON" : "OFF"));
            AddToggle(p, L, -160f, "Kaleidoscope Mama", CustomizationSubmenu.ToggleMamaPublic,
                () => !CustomizationSubmenu.MamaOwned ? "LOCKED" : (CustomizationSubmenu.KaleidoscopeMama ? "ON" : "OFF"));

            AddHeader(p, R, 360f, "RUN MODIFIERS  ·  +10 shards each per objective (not Power Ups)");
            float y = 305f;
            for (int i = 0; i < RunModifiersSubmenu.Count; i++)
            {
                int idx = i;
                _modRows.Add(AddToggle(p, R, y, RunModifiersSubmenu.Name(idx), () => RunModifiersSubmenu.Toggle(idx), () => RunModifiersSubmenu.Get(idx) ? "ON" : "OFF"));
                y -= step;
            }

            AddHeader(p, R, -105f, "DETAILS  ·  point at anything");
            _details = Fit(MakeText(p, "", 24, TextAnchor.UpperLeft, new Vector2(R, -225f), new Vector2(ROW_HALF * 2f, 200f), Dim));

            AddButton(p, new Vector2(-220f, -405f), "BACK", () =>
            {
                Hide();
                if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            });
            AddButton(p, new Vector2(220f, -405f), "PLAY", () =>
            {
                Hide();
                if (FacilityChosen) HubLaunch.StartFacilityEndless(); else HubLaunch.StartEndless();
            });
        }

        private static void StepMap(int dir) { FacilityMap = !FacilityMap; }

        /// <summary>Cycles None + the gadgets you own (from the Gumball Machine).</summary>
        internal static void StepGadget(int dir)
        {
            var choices = new List<GadgetType> { GadgetType.None };
            foreach (var g in Gadgets.All) if (Gadgets.IsOwned(g)) choices.Add(g);
            int n = choices.Count;
            int i = Mathf.Max(0, choices.IndexOf(Gadgets.SelectedIfOwned));
            Gadgets.Selected = choices[((i + dir) % n + n) % n];
        }

        protected override void RefreshAll()
        {
            base.RefreshAll();
            if (_details == null) return;
            if (_mapRow != null) _mapRow.SetVisible(true);

            string gadget = Gadgets.SelectedIfOwned == GadgetType.None ? Gadgets.Description(GadgetType.None) : Gadgets.Name(Gadgets.SelectedIfOwned) + ": " + Gadgets.Description(Gadgets.SelectedIfOwned);
            string profile = EndlessTraits.Describe();

            for (int i = 0; i < _modRows.Count; i++)
                if (Hit(_modRows[i].label.rectTransform) || Hit(_modRows[i].value.rectTransform))
                { _details.text = RunModifiersSubmenu.Name(i) + ": " + RunModifiersSubmenu.Description(i); return; }
            if (_gadgetRow != null && (Hit(_gadgetRow.label.rectTransform) || Hit(_gadgetRow.value.rectTransform))) { _details.text = "YOUR GADGET\n" + gadget; return; }
            if (_profileRow != null && (Hit(_profileRow.label.rectTransform) || Hit(_profileRow.value.rectTransform))) { _details.text = "YOUR PROFILE\n" + profile; return; }
            if (_mapRow != null && (Hit(_mapRow.label.rectTransform) || Hit(_mapRow.value.rectTransform)))
            { _details.text = "MAP" + System.Environment.NewLine + "The House is the normal Endless. The Waygetter Facility is the abandoned factory: you carry a camcorder with night vision, and when Mama comes the lights go out."; return; }
            _details.text = "YOUR GADGET\n" + gadget + "\n\nYOUR PROFILE\n" + profile;
        }
    }


    /// <summary>
    /// Papa's 10th Birthday! machine's setup screen: the party's own look
    /// (bought with Party Tokens, separate from Endless), then PLAY.
    /// </summary>
    public class PapaSetupMenu : OverlayPanel
    {
        private static PapaSetupMenu _instance;

        protected override string Title { get { return "PAPA'S 10TH BIRTHDAY!"; } }
        protected override Vector2 PanelSize { get { return new Vector2(900f, 560f); } }

        public static void Open() { GetOrCreate(ref _instance, "KE_PapaSetupMenu").Show(); }

        protected override void OnShown()
        {
            CustomizationSubmenu.SetMode(true); // party picks, owned with Party Tokens
            CustomizationSubmenu.Refresh();
        }

        protected override void BuildContent(Transform p)
        {
            const float X = 0f;
            AddHeader(p, X, 150f, "LOOK");
            AddStepper(p, X, 95f, "Tattletail Color", CustomizationSubmenu.StepColor, () => CustomizationSubmenu.ColorName);
            AddStepper(p, X, 40f, "Flashlight Beam", CustomizationSubmenu.StepBeam, () => CustomizationSubmenu.BeamName);
            AddToggle(p, X, -15f, "Kaleidoscope Voice", CustomizationSubmenu.ToggleVoicePublic,
                () => !CustomizationSubmenu.VoiceOwned ? "LOCKED" : (CustomizationSubmenu.KaleidoscopeVoice ? "ON" : "OFF"));
            AddToggle(p, X, -70f, "Kaleidoscope Mama", CustomizationSubmenu.ToggleMamaPublic,
                () => !CustomizationSubmenu.MamaOwned ? "LOCKED" : (CustomizationSubmenu.KaleidoscopeMama ? "ON" : "OFF"));

            AddButton(p, new Vector2(-200f, -200f), "BACK", () =>
            {
                Hide();
                if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            });
            AddButton(p, new Vector2(200f, -200f), "PLAY", () =>
            {
                Hide();
                HubLaunch.StartPapas();
            });
        }
    }

    /// <summary>
    /// The LEVEL SELECT machine: replay any story night (unlocked by beating
    /// the story, same as the game's own Level Select).
    /// </summary>
    public class LevelSelectMenu : OverlayPanel
    {
        private static LevelSelectMenu _instance;

        private static readonly LevelEnum[] Nights =
        {
            LevelEnum._1_ANightToRemember, LevelEnum._2_MinorMischief, LevelEnum._3_MamasBoy, LevelEnum._4_FunnyGames,
            LevelEnum._5_VideoHorrorSystem, LevelEnum._6_TheParty, LevelEnum._7_ThereSheIs, LevelEnum._8_Christmas,
        };

        protected override string Title { get { return "LEVEL SELECT"; } }
        protected override Vector2 PanelSize { get { return new Vector2(1100f, 640f); } }

        public static void Open() { GetOrCreate(ref _instance, "KE_LevelSelectMenu").Show(); }

        protected override void BuildContent(Transform p)
        {
            for (int i = 0; i < Nights.Length; i++)
            {
                LevelEnum level = Nights[i];
                float x = i < 4 ? -260f : 260f;
                float y = 170f - (i % 4) * 85f;
                string name = NightName(level);
                if (!name.StartsWith("Night")) name = "Night " + (i + 1) + ": " + name; // the game's own name may already say it
                AddButton(p, new Vector2(x, y), name, () =>
                {
                    Hide();
                    HubLaunch.PlayStoryLevel(level);
                }, 500f);
            }

            AddButton(p, new Vector2(0f, -250f), "BACK", () =>
            {
                Hide();
                if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            });
        }

        private static string NightName(LevelEnum level)
        {
            try
            {
                var data = Utils.GetLevelFlow().GetLevelByEnum(level);
                if (data != null && !string.IsNullOrEmpty(data.levelName)) return data.levelName;
            }
            catch { }
            return level.ToString().Substring(3).Replace('_', ' ');
        }
    }
}
