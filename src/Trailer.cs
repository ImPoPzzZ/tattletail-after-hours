using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// A self-playing trailer, for recording (dev builds only: F9 in the Arcade). It takes over the
    /// player's camera (so the game's own post effects stay), hides every HUD, and plays a scripted
    /// sequence with its own music, title cards and captions:
    ///   Part A (the Arcade and the house under it, one level): a heartbeat on black, the Arcade,
    ///   Mama in the dark house with a flickering flashlight, the machines.
    ///   "NOW HIRING." covers the load to the beach.
    ///   Part B (the beach): the bar, customers, the blender, a PERFECT! stamp, the moods, Mama
    ///   walking over, 49%, the jumpscare, the logo.
    /// Nothing it does is saved. When it ends you're left at the beach with control back.
    /// </summary>
    public partial class TrailerDirector : MonoBehaviour
    {
        public static TrailerDirector Instance { get; private set; }

        /// <summary>On while a trailer is being recorded (dev builds, F9 in the Arcade); off for every release.</summary>
        public const bool Enabled = false;

        /// <summary>
        /// The 3.4 trailer (THE ARCADE UPDATE): all in the Arcade, no loads. Black, the pinball machine (it plays itself:
        /// flippers, bumpers, the ramp, the House eating the ball), air hockey (a fast Tattletail rally and a goal), the
        /// Rat King waddling in, the logo. False: the 3.3 trailer (the house and the beach).
        /// </summary>
        private const bool ARCADE = true;
        private const float OPEN_END = 3.6f, LOGO_T = 36f, ARCADE_END = 44f;
        private GameObject _rat;
        private const float RAT_T0 = 31.6f, RAT_T1 = 36f;
        private static readonly Vector3 RatFrom = new Vector3(-9.4f, 0.05f, 1.0f), RatTo = new Vector3(-8.05f, 0.05f, 0.35f);

        /// <summary>F9 in the Arcade (dev builds): roll it.</summary>
        public static void Begin()
        {
            if (Instance != null) return;
            var go = new GameObject("KE_Trailer");
            DontDestroyOnLoad(go);
            go.AddComponent<TrailerDirector>();
        }

        // Where a shot's coordinates live.
        private const int WORLD = 0, HUB = 1, BEACH = 2;

        private class Shot { public float T0, T1; public int Space; public Vector3 P0, P1, L0, L1, Anchor; public bool Flash, HasAnchor, Ready; public int Boss = -1; public float Fog = 1f; }
        private class Card { public float T0, T1; public string Text; public Color Color = Color.white; public bool Hit; public int Size = 96; public bool Black = true; public bool Scary; }
        private class Caption { public float T0, T1; public string Text; public Color Color; }
        private class Cue { public float T; public string What; public bool Done; }

        private readonly List<Shot> _shotsA = new List<Shot>(), _shotsB = new List<Shot>();
        private readonly List<Card> _cardsA = new List<Card>(), _cardsB = new List<Card>();
        private readonly List<Caption> _capsA = new List<Caption>(), _capsB = new List<Caption>();
        private readonly List<Cue> _cuesA = new List<Cue>(), _cuesB = new List<Cue>();

        private int _part; // 0 = A, 1 = loading, 2 = B, 3 = done
        private float _t, _loadWait;
        private const float PART_A_END = 37.4f, PART_B_END = 47.5f;
        private static readonly Vector3 AggroCam = new Vector3(13.45f, 1.05f, -5.75f); // front room, in front of the line

        private Camera _cam;
        private bool _fovSet;
        private float _fovWas;
        private FirstPersonController _fps;
        private Vector3 _camLocalPos;
        private Quaternion _camLocalRot;
        private Light _flash;
        private float _flashDieAt = -1f;
        private GameObject _mama1, _mama2;
        private readonly List<Canvas> _hidden = new List<Canvas>();
        private float _hideScan;
        private string _allowCanvas;

        private Canvas _canvas;
        private Image _black, _white, _capBar;
        private Text _cardText, _capText, _subText;
        private AudioSource _music, _sfx;

        // ── Setup ────────────────────────────────────────────────────────────
        private void Awake()
        {
            Instance = this;
            if (RECALLED) { AwakeRecalled(); return; }
            if (ARCADE)
            {
                BuildOverlay();
                _music = gameObject.AddComponent<AudioSource>();
                _music.playOnAwake = false; _music.spatialBlend = 0f;
                _sfx = gameObject.AddComponent<AudioSource>();
                _sfx.playOnAwake = false; _sfx.spatialBlend = 0f;
                BuildArcade();
                _music.clip = TrailerMusic.Arcade();
                _music.volume = 0.8f;
                _music.Play();
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Trailer] Rolling (3.4, the Arcade).");
                return;
            }
            BeachShift.TrailerMode = true;
            // Build the beach now, quietly (the opening is on black): no load later.
            BeachController.Background = true;
            BeachMode.Active = true;
            BeachController.Create();
            BuildOverlay();
            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false; _music.spatialBlend = 0f;
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false; _sfx.spatialBlend = 0f;
            BuildPartA();
            BuildPartB();
            _music.clip = TrailerMusic.PartA();
            _music.volume = 0.9f;
            _music.Play();
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Trailer] Rolling.");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_rat != null) Destroy(_rat);
            if (RECALLED) DestroyRecalled();
            ReleaseCamera();
            if (ARCADE) { foreach (var c in _hidden) if (c != null) c.enabled = true; return; }
            BeachShift.TrailerMode = false;
            // The beach goes away (and takes its sunny sky with it): you're back in the Arcade.
            BeachMode.Active = false;
            BeachController.Background = false;
            foreach (var c in _hidden) if (c != null) c.enabled = true;
        }

        // ── The script ───────────────────────────────────────────────────────
        // Room spots (HUB space). Pinball: its table point (x across, y up it, h above it) -> the room.
        private static Vector3 Pb(float x, float y, float h) { return new Vector3(-6.95f - x, 0.93f + y * 0.1132f + h * 0.9936f, -2.01f - y * 0.9936f + h * 0.1132f); }

        private void BuildArcade()
        {
            Color pink = new Color(1f, 0.35f, 0.85f), cyan = new Color(0.35f, 0.85f, 1f), gold = new Color(1f, 0.85f, 0.25f);
            float xc = PinballTable.XC;
            _cardsA.Add(new Card { T0 = 0.3f, T1 = OPEN_END, Text = "THE ARCADE IS OPEN LATE.", Size = 84, Scary = true });
            _cuesA.Add(new Cue { T = 0.1f, What = "pb_on" });
            // Down the hall to the glowing pinball machine.
            _shotsA.Add(new Shot { T0 = OPEN_END, T1 = 9.2f, Space = HUB, P0 = new Vector3(-9.6f, 1.55f, -0.1f), P1 = new Vector3(-7.45f, 1.5f, -1.05f), L0 = new Vector3(-7.4f, 1.3f, -2.9f), L1 = Pb(xc, 0.55f, 0f) });
            _capsA.Add(new Caption { T0 = 4.2f, T1 = 9f, Text = "TATTLETAIL PINBALL  ·  NEW IN 3.4", Color = pink });
            // The flippers: a ball drops onto the right one, SNAP.
            _cuesA.Add(new Cue { T = 9.25f, What = "pb_flip" });
            _shotsA.Add(new Shot { T0 = 9.2f, T1 = 11.6f, Space = HUB, P0 = Pb(xc + 0.02f, -0.08f, 0.17f), P1 = Pb(xc + 0.02f, -0.05f, 0.15f), L0 = Pb(xc, 0.2f, 0f), L1 = Pb(xc, 0.25f, 0f) });
            // The Tattletail bumpers going off.
            _cuesA.Add(new Cue { T = 11.65f, What = "pb_bumpers" });
            _shotsA.Add(new Shot { T0 = 11.6f, T1 = 14.2f, Space = HUB, P0 = Pb(xc + 0.03f, 0.5f, 0.2f), P1 = Pb(xc + 0.02f, 0.54f, 0.17f), L0 = Pb(xc, 0.72f, 0.04f), L1 = Pb(xc, 0.72f, 0.04f) });
            _capsA.Add(new Caption { T0 = 11.8f, T1 = 14f, Text = "TATTLETAIL BUMPERS", Color = new Color(0.75f, 0.35f, 1f) });
            // Up the ramp and over the top.
            _cuesA.Add(new Cue { T = 14.25f, What = "pb_ramp" });
            _shotsA.Add(new Shot { T0 = 14.2f, T1 = 16.8f, Space = HUB, P0 = Pb(-0.34f, 0.55f, 0.32f), P1 = Pb(-0.32f, 0.6f, 0.3f), L0 = Pb(-0.15f, 0.95f, 0.08f), L1 = Pb(0.05f, 1.05f, 0.1f) });
            _capsA.Add(new Caption { T0 = 14.4f, T1 = 16.6f, Text = "THE RAMP", Color = pink });
            _cardsA.Add(new Card { T0 = 16.8f, T1 = 18.3f, Text = "IT'S HUNGRY...", Hit = true, Size = 130, Scary = true });
            // The House eats the ball.
            _cuesA.Add(new Cue { T = 18.45f, What = "pb_house" });
            _shotsA.Add(new Shot { T0 = 18.3f, T1 = 21.3f, Space = HUB, P0 = Pb(xc, 0.74f, 0.11f), P1 = Pb(xc, 0.79f, 0.1f), L0 = Pb(xc, 0.95f, 0.05f), L1 = Pb(xc, 0.95f, 0.05f) });
            // Air hockey: low along the table, a fast rally; then the scoreboard as red scores.
            _cuesA.Add(new Cue { T = 19f, What = "ah_on" });
            _shotsA.Add(new Shot { T0 = 21.3f, T1 = 26.2f, Space = HUB, P0 = new Vector3(-1.75f, 1.0f, -7.85f), P1 = new Vector3(-1.95f, 0.95f, -8.0f), L0 = new Vector3(-3.4f, 0.78f, -8.11f), L1 = new Vector3(-3.3f, 0.78f, -8.15f) });
            _capsA.Add(new Caption { T0 = 21.7f, T1 = 26f, Text = "TATTLETAIL AIR HOCKEY  ·  1v1  ·  FIRST TO 7", Color = cyan });
            _cuesA.Add(new Cue { T = 25.75f, What = "ah_goal" });
            _shotsA.Add(new Shot { T0 = 26.2f, T1 = 28.6f, Space = HUB, P0 = new Vector3(-2.25f, 1.45f, -8.11f), P1 = new Vector3(-2.4f, 1.55f, -8.11f), L0 = new Vector3(-3.1f, 1.72f, -8.11f), L1 = new Vector3(-3.1f, 1.74f, -8.11f) });
            _cardsA.Add(new Card { T0 = 28.6f, T1 = 30.2f, Text = "PLAY YOUR FRIENDS.", Hit = true, Size = 120, Color = cyan });
            _cardsA.Add(new Card { T0 = 30.2f, T1 = RAT_T0, Text = "ALL HAIL...", Size = 110, Color = gold });
            _cuesA.Add(new Cue { T = 30.2f, What = "rat" });
            // The Rat King waddles up to the camera.
            _shotsA.Add(new Shot { T0 = RAT_T0, T1 = RAT_T1, Space = HUB, P0 = new Vector3(-7.05f, 0.62f, -0.15f), P1 = new Vector3(-7.25f, 0.58f, -0.05f), L0 = RatFrom + new Vector3(0f, 0.62f, 0f), L1 = RatTo + new Vector3(0f, 0.6f, 0f) });
            _cardsA.Add(new Card { T0 = RAT_T0 + 1.2f, T1 = RAT_T1, Text = "THE RAT KING.", Hit = true, Size = 130, Color = gold, Black = false });
            _cuesA.Add(new Cue { T = LOGO_T + 0.4f, What = "logo" });
        }

        private void BuildPartA()
        {
            _cardsA.Add(new Card { T0 = 0.3f, T1 = 5f, Text = "TATTLETAIL HAD ONE LAST NIGHT.", Size = 70, Scary = true });
            _cardsA.Add(new Card { T0 = 5f, T1 = 7f, Text = "NOT ANYMORE.", Size = 90, Scary = true, Hit = true });
            // The Arcade: from the doorway, turning toward the machines.
            _shotsA.Add(new Shot { T0 = 6.9f, T1 = 14f, Space = HUB, P0 = new Vector3(-6.8f, 0.9f, 4.3f), P1 = new Vector3(-9.2f, 1.45f, 6.6f), L0 = new Vector3(-11f, 1.3f, 8.5f), L1 = new Vector3(-7.57f, 1.25f, 8.58f) });
            _cardsA.Add(new Card { T0 = 14f, T1 = 15.6f, Text = "NO LAST NIGHT.", Hit = true, Size = 130, Scary = true });
            // The house (Mama shots are filled in from real patrol spots once we start).
            _shotsA.Add(new Shot { T0 = 15.6f, T1 = 22.6f, Space = WORLD, Flash = true });
            _capsA.Add(new Caption { T0 = 16.4f, T1 = 21.8f, Text = "ENDLESS  ·  SURVIVE MAMA AS LONG AS YOU CAN", Color = new Color(0.8f, 0.35f, 1f) });
            _cardsA.Add(new Card { T0 = 22.6f, T1 = 24f, Text = "SHE IS LISTENING.", Hit = true, Size = 120, Scary = true });
            _shotsA.Add(new Shot { T0 = 24f, T1 = 27.6f, Space = WORLD, Flash = true });
            _cuesA.Add(new Cue { T = 26.9f, What = "flashdie" });
            _cardsA.Add(new Card { T0 = 27.6f, T1 = 29f, Text = "PLAY AS MAMA.", Hit = true, Size = 130, Scary = true, Color = new Color(1f, 0.25f, 0.25f) });
            // Machine close-ups.
            _shotsA.Add(new Shot { T0 = 29f, T1 = 31.2f, Space = HUB, P0 = new Vector3(-9.6f, 1.45f, 6.9f), P1 = new Vector3(-10.3f, 1.35f, 6.5f), L0 = new Vector3(-11.4f, 1.3f, 6.39f), L1 = new Vector3(-11.4f, 1.3f, 6.39f) });
            _capsA.Add(new Caption { T0 = 29.2f, T1 = 31.1f, Text = "VERSUS  ·  1v1 ONLINE  ·  MAMA OR PAPA VS THE CHILD", Color = new Color(0.9f, 0.15f, 0.35f) });
            _shotsA.Add(new Shot { T0 = 31.2f, T1 = 33f, Space = HUB, P0 = new Vector3(-9.8f, 1.4f, 4f), P1 = new Vector3(-10.4f, 1.35f, 4.4f), L0 = new Vector3(-11.4f, 1.3f, 4.47f), L1 = new Vector3(-11.4f, 1.3f, 4.47f) });
            _capsA.Add(new Caption { T0 = 31.3f, T1 = 32.9f, Text = "PAPA'S 10TH BIRTHDAY!", Color = new Color(1f, 0.6f, 0.15f) });
            _shotsA.Add(new Shot { T0 = 33f, T1 = 34.8f, Space = HUB, P0 = new Vector3(-2.2f, 1.4f, -3f), P1 = new Vector3(-1.4f, 1.35f, -3.4f), L0 = new Vector3(-0.3f, 1.3f, -3.5f), L1 = new Vector3(-0.3f, 1.3f, -3.5f) });
            _capsA.Add(new Caption { T0 = 33.1f, T1 = 34.7f, Text = "BUILD YOUR OWN KID", Color = new Color(0.3f, 1f, 0.6f) });
            _shotsA.Add(new Shot { T0 = 34.8f, T1 = PART_A_END, Space = HUB, P0 = new Vector3(-8.4f, 1.3f, 6.39f), P1 = new Vector3(-7.1f, 1.35f, 6.39f), L0 = new Vector3(-6.2f, 1.35f, 6.39f), L1 = new Vector3(-6.2f, 1.35f, 6.39f) });
            _cardsA.Add(new Card { T0 = PART_A_END, T1 = 9999f, Text = "NOW HIRING!", Size = 140, Color = new Color(1f, 0.85f, 0.25f) });
            _cuesA.Add(new Cue { T = PART_A_END + 0.05f, What = "ding" });
        }

        private void BuildPartB()
        {
            Color gold = new Color(1f, 0.85f, 0.25f);
            _cardsB.Add(new Card { T0 = -99f, T1 = 1.2f, Text = "NOW HIRING!", Size = 140, Color = gold });
            _cuesB.Add(new Cue { T = 0f, What = "calypso" });
            _cuesB.Add(new Cue { T = 0f, What = "customers" });
            // Over the sea, craning in toward the bar.
            _shotsB.Add(new Shot { T0 = 0f, T1 = 6f, Space = BEACH, P0 = new Vector3(-2f, 6f, 13f), P1 = new Vector3(5f, 3.2f, 3.5f), L0 = new Vector3(6f, 1f, -6f), L1 = new Vector3(12f, 1.5f, -6.5f) });
            _capsB.Add(new Caption { T0 = 1.5f, T1 = 5.8f, Text = "SUMMER VACATION  ·  NEW IN 3.3", Color = gold });
            // Customers on the gravel path, walking to the door.
            _shotsB.Add(new Shot { T0 = 6f, T1 = 11f, Space = BEACH, P0 = new Vector3(8.8f, 0.9f, -5.4f), P1 = new Vector3(9.6f, 1f, -5.6f), L0 = new Vector3(5f, 0.6f, -7.5f), L1 = new Vector3(11.5f, 1f, -7.6f) });
            // Inside: the line, the stools, the checkered floor.
            _shotsB.Add(new Shot { T0 = 11f, T1 = 16f, Space = BEACH, P0 = new Vector3(13.1f, 1.55f, -2f), P1 = new Vector3(13.6f, 1.35f, -4.3f), L0 = new Vector3(15f, 1f, -7.3f), L1 = new Vector3(15.4f, 1f, -7.4f) });
            _capsB.Add(new Caption { T0 = 11.4f, T1 = 15.8f, Text = "MAMA'S SMOOTHIE BAR  ·  PAPA'S FREEZERIA STYLE", Color = new Color(1f, 0.45f, 0.65f) });
            _cuesB.Add(new Cue { T = 15.2f, What = "blend" });
            // The blender.
            _shotsB.Add(new Shot { T0 = 16f, T1 = 20f, Space = BEACH, P0 = new Vector3(14.85f, 1.85f, -9.0f), P1 = new Vector3(14.85f, 1.7f, -9.3f), L0 = new Vector3(14.85f, 1.45f, -9.95f), L1 = new Vector3(14.85f, 1.5f, -9.95f) });
            _capsB.Add(new Caption { T0 = 16.2f, T1 = 19.8f, Text = "BLEND IT JUST RIGHT", Color = new Color(0.35f, 1f, 0.5f) });
            _cuesB.Add(new Cue { T = 20f, What = "unblend" }); // the whirring stops with the shot
            // Serving: the receipt and the stamp.
            _shotsB.Add(new Shot { T0 = 20f, T1 = 24f, Space = BEACH, P0 = new Vector3(15.15f, 1.5f, -8.7f), P1 = new Vector3(15.15f, 1.4f, -8.45f), L0 = new Vector3(15.15f, 1.1f, -7.2f), L1 = new Vector3(15.15f, 1.1f, -7.2f) });
            _cuesB.Add(new Cue { T = 20.3f, What = "receipt" });
            // The moods, one quick close-up each.
            float[] mx = { 15.15f, 15.95f, 16.7f, 14.25f };
            float[] mz = { -7.2f, -7.2f, -7.2f, -7.0f };
            string[] mood = { "NICE.", "IMPATIENT.", "JUDGY.", "AGGRESSIVE." };
            Color[] mc = { new Color(0.3f, 0.9f, 0.5f), new Color(1f, 0.55f, 0.15f), new Color(0.7f, 0.4f, 1f), new Color(1f, 0.25f, 0.25f) };
            _cuesB.Add(new Cue { T = 25.2f, What = "lookhere" });
            for (int i = 0; i < 4; i++)
            {
                float t0 = 24f + i * 0.8f;
                if (i == 3)
                    _shotsB.Add(new Shot { T0 = t0, T1 = t0 + 0.8f, Space = BEACH, P0 = AggroCam + new Vector3(-0.1f, 0.05f, 0.1f), P1 = AggroCam, L0 = new Vector3(mx[i], 0.95f, mz[i]), L1 = new Vector3(mx[i], 0.9f, mz[i]) });
                else if (i == 2)
                    _shotsB.Add(new Shot { T0 = t0, T1 = t0 + 0.8f, Space = BEACH, P0 = new Vector3(mx[i] - 0.45f, 1.65f, -8.5f), P1 = new Vector3(mx[i] - 0.4f, 1.6f, -8.35f), L0 = new Vector3(mx[i], 1.1f, mz[i]), L1 = new Vector3(mx[i], 1.1f, mz[i]) });
                else
                    _shotsB.Add(new Shot { T0 = t0, T1 = t0 + 0.8f, Space = BEACH, P0 = new Vector3(mx[i] + 0.1f, 1.35f, -8.45f), P1 = new Vector3(mx[i], 1.3f, -8.3f), L0 = new Vector3(mx[i], 1.1f, mz[i]), L1 = new Vector3(mx[i], 1.1f, mz[i]) });
                _cardsB.Add(new Card { T0 = t0, T1 = t0 + 0.8f, Text = mood[i], Size = 130, Color = mc[i], Black = false, Hit = true });
            }
            _cardsB.Add(new Card { T0 = 27.2f, T1 = 29.5f, Text = "BUT AT THE END OF EVERY DAY...", Size = 72, Scary = true });
            _cuesB.Add(new Cue { T = 27.2f, What = "drone" });
            _cuesB.Add(new Cue { T = 27.2f, What = "leave" });
            _cuesB.Add(new Cue { T = 27.2f, What = "mama" });
            // Mama walks over. You're behind the bar.
            _shotsB.Add(new Shot { T0 = 29.5f, T1 = 35.5f, Space = BEACH, P0 = new Vector3(15.3f, 1.55f, -9.2f), P1 = new Vector3(15.3f, 1.5f, -8.8f), L0 = new Vector3(14.2f, 1.9f, -4.2f), L1 = new Vector3(15.3f, 2.1f, -6.35f) });
            _cardsB.Add(new Card { T0 = 35.5f, T1 = 36.6f, Text = "MAMA JUDGES YOU.", Hit = true, Size = 130, Scary = true, Color = new Color(1f, 0.2f, 0.2f) }); // says what the jumpscare is about
            _shotsB.Add(new Shot { T0 = 36.6f, T1 = 39f, Space = BEACH, P0 = new Vector3(15.3f, 1.5f, -8.8f), P1 = new Vector3(15.3f, 1.5f, -8.8f), L0 = new Vector3(15.3f, 2.1f, -6.35f), L1 = new Vector3(15.3f, 2.1f, -6.35f) });
            _cuesB.Add(new Cue { T = 36.6f, What = "scare" });
            _cuesB.Add(new Cue { T = 39f, What = "afterscare" });
            // The logo.
            _cardsB.Add(new Card { T0 = 39f, T1 = PART_B_END, Text = "", Size = 10 });
            _cuesB.Add(new Cue { T = 39.4f, What = "logo" });
        }

        // ── Every frame ──────────────────────────────────────────────────────
        private void Update()
        {
            if (RECALLED && !_started) { WaitForTown(); return; }
            _t += Time.deltaTime;
            HideHud();
            if (_part == 0)
            {
                if (RECALLED) { UpdateRecalled(); return; }
                if (!ARCADE && _mama1 == null && _t > 0.2f) PlaceHouseShots();
                // One Mama at a time: each only while her own shot is on.
                if (_mama1 != null) _mama1.SetActive(_t < 22.8f);
                if (_mama2 != null) _mama2.SetActive(_t >= 22.8f);
                RunCues(_cuesA);
                if (Input.GetKeyDown(KeyCode.Escape)) { Finish(); return; }
                if (ARCADE)
                {
                    if (_rat != null)
                    {
                        float k = Mathf.InverseLerp(RAT_T0 - 0.4f, RAT_T1 - 0.3f, _t);
                        _rat.transform.position = HubController.Origin + Vector3.Lerp(RatFrom, RatTo, k);
                        if (k >= 1f) { Vector3 d = HubController.Origin + new Vector3(-7.15f, 0f, -0.1f) - _rat.transform.position; d.y = 0f; _rat.transform.rotation = Quaternion.RotateTowards(_rat.transform.rotation, Quaternion.LookRotation(d), 200f * Time.deltaTime); }
                    }
                    if (_t > ARCADE_END) { Finish(); return; }
                    DrawOverlay();
                    return;
                }
                if (_t > PART_A_END + 0.6f) _part = 1;
            }
            if (_part == 1)
            {
                // NOW HIRING: the beach was built in the background; cut to it as soon as it's ready.
                var bc = BeachController.Instance;
                if (bc != null && bc.Built && BeachShift.Instance != null && BeachShift.Instance.TrailerReady) StartPartB();
            }
            else if (_part == 2)
            {
                RunCues(_cuesB);
                if (_t > PART_B_END || Input.GetKeyDown(KeyCode.Escape)) Finish();
            }
            DrawOverlay();
        }

        private void LateUpdate()
        {
            if (_part == 1 || _part == 3) return;
            var shots = _part == 0 ? _shotsA : _shotsB;
            Shot s = null;
            foreach (var sh in shots) if (_t >= sh.T0 && _t < sh.T1) s = sh;
            if (s == null)
            {
                // Between shots (a title card): wait at the NEXT shot's start, so the card fades onto it, not onto the last one.
                foreach (var sh in shots) if (sh.T0 > _t && (s == null || sh.T0 < s.T0)) s = sh;
                if (s == null) return;
            }
            if (!GrabCamera()) return;
            if (RECALLED && s.Space == RC)
            {
                if (!_fovSet) { _fovSet = true; _fovWas = _cam.fieldOfView; }
                _cam.fieldOfView = 50f; // (the game's own is wide: the toys are half a meter tall, and everything looks far away)
                if (s.Boss >= 0 && !s.Ready && Rc.Enemies != null) PrepareBoss(s);
                _fog = Mathf.Lerp(_fog, s.Fog, Mathf.Clamp01(Time.deltaTime * 2f));
                RecalledController.FogScale = _fog;
            }
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(s.T0, s.T1, _t));
            Vector3 origin = s.Space == HUB ? HubController.Origin : s.Space == BEACH ? BeachController.Origin : s.Space == RC ? Rc.Origin : Vector3.zero;
            Vector3 pos = origin + Vector3.Lerp(s.P0, s.P1, k), look = origin + Vector3.Lerp(s.L0, s.L1, k);
            if (s.Space == RC && s.Boss < 0) pos = KeepOutOfWalls(look, pos); // (a boss shot is framed on its own: pulling it in put the lens against Mama's back)
            _cam.transform.position = pos;
            if ((look - pos).sqrMagnitude > 0.0001f) _cam.transform.rotation = Quaternion.LookRotation(look - pos);

            // The flashlight: on for house shots, flickering, and dying at the end.
            bool on = s.Flash && (_flashDieAt < 0f || _t < _flashDieAt || (_t - _flashDieAt) % 0.18f < 0.05f && _t - _flashDieAt < 0.6f);
            _flash.enabled = on;
            _flash.transform.position = pos;
            _flash.transform.rotation = _cam.transform.rotation;
            _flash.intensity = 0.75f + Mathf.PerlinNoise(_t * 9f, 0f) * 0.25f - (Mathf.PerlinNoise(_t * 23f, 5f) > 0.8f ? 0.5f : 0f);

            // Mama walking over faces the camera once she's there.
            if (_part == 2 && _t > 33.5f && BeachLayout.Mama != null)
            {
                var mt = BeachLayout.Mama.transform;
                Vector3 d = _cam.transform.position - mt.position; d.y = 0f;
                if (d.sqrMagnitude > 0.01f) mt.rotation = Quaternion.RotateTowards(mt.rotation, Quaternion.LookRotation(d), 120f * Time.deltaTime);
            }
        }

        private void RunCues(List<Cue> cues)
        {
            foreach (var c in cues)
            {
                if (c.Done || _t < c.T) continue;
                c.Done = true;
                try { DoCue(c.What); }
                catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Trailer] Cue '" + c.What + "' failed: " + e.Message); }
            }
        }

        private void DoCue(string what)
        {
            var shift = BeachShift.Instance;
            switch (what)
            {
                default: DoRecalledCue(what); break;
                case "ding": _sfx.PlayOneShot(TrailerMusic.Bell(), 0.9f); break;
                case "flashdie": _flashDieAt = _t; break;
                case "calypso": _music.clip = BeachMusic.WorkClip(); _music.loop = true; _music.volume = 0.55f; _music.Play(); break;
                case "drone": _music.clip = BeachMusic.DroneClip(); _music.loop = true; _music.volume = 0.8f; _music.Play(); _sfx.PlayOneShot(TrailerMusic.Boom(), 0.8f); break;
                case "customers":
                    if (shift != null) shift.TrailerCustomers(new[] { new Vector3(-2f, 0f, -7.45f), new Vector3(-3.6f, 0f, -7.45f), new Vector3(-5.2f, 0f, -7.45f), new Vector3(-6.8f, 0f, -7.45f) }, new[] { 1, 2, 4, 3 });
                    break;
                case "blend": if (shift != null) shift.TrailerBlend(); break;
                case "unblend": if (shift != null) shift.TrailerStopBlend(); break;
                case "lookhere": if (shift != null) shift.TrailerLookAt(AggroCam); break;
                case "receipt": _allowCanvas = "KE_BeachHud"; if (shift != null) shift.TrailerReceipt(); break;
                case "leave": _allowCanvas = null; if (shift != null) shift.TrailerCustomersLeave(); break;
                case "mama": if (shift != null) shift.TrailerMamaWalk(); break;
                case "scare": _music.Stop(); if (shift != null) shift.TrailerJumpscare(); break;
                case "afterscare": if (shift != null) shift.TrailerAfterScare(); break;
                case "logo": _sfx.PlayOneShot(TrailerMusic.Boom(), 1f); _sfx.PlayOneShot(TrailerMusic.PanChord(), 0.8f); break;
                case "pb_on": if (PinballGame.Instance != null) PinballGame.Instance.TrailerPlay(true); break;
                case "pb_flip": if (PinballGame.Instance != null) PinballGame.Instance.TrailerStage("flip"); break;
                case "pb_bumpers": if (PinballGame.Instance != null) PinballGame.Instance.TrailerStage("bumpers"); break;
                case "pb_ramp": if (PinballGame.Instance != null) PinballGame.Instance.TrailerStage("ramp"); break;
                case "pb_house": if (PinballGame.Instance != null) PinballGame.Instance.TrailerStage("house"); break;
                case "ah_on": if (AirHockeyGame.Instance != null) AirHockeyGame.Instance.TrailerRally(true); break;
                case "ah_goal": if (AirHockeyGame.Instance != null) AirHockeyGame.Instance.TrailerGoal(); break;
                case "rat":
                    _rat = ChildModel.Build("KE_TrailerRat", KidGender.Rat, 0);
                    _rat.transform.position = HubController.Origin + RatFrom;
                    _rat.transform.rotation = Quaternion.LookRotation(RatTo - RatFrom);
                    break;
            }
        }

        private void StartPartB()
        {
            _part = 2;
            _t = 0f;
            BeachController.Instance.ShowDaylight();
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Trailer] Part B (the beach).");
        }

        private void Finish()
        {
            if (_part == 3) return;
            _part = 3;
            _music.Stop();
            if (ARCADE)
            {
                if (PinballGame.Instance != null) PinballGame.Instance.TrailerPlay(false);
                if (AirHockeyGame.Instance != null) AirHockeyGame.Instance.TrailerRally(false);
                if (_rat != null) Destroy(_rat);
            }
            else if (BeachShift.Instance != null) BeachShift.Instance.TrailerCleanup();
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Trailer] Cut.");
            Destroy(gameObject);
        }

        // ── Mama in the house ────────────────────────────────────────────────
        /// <summary>Two of Mama's real hiding spots with the most room in front, and the camera pushing in on each.</summary>
        private void PlaceHouseShots()
        {
            var set = RM.patrolNodeSet;
            var picks = new List<KeyValuePair<float, Transform>>();
            if (set != null && set.patrolNodes != null)
                foreach (var n in set.patrolNodes)
                {
                    if (n == null) continue;
                    Vector3 chest = n.transform.position + Vector3.up * 1.6f, fwd = n.transform.forward;
                    fwd.y = 0f;
                    if (fwd.sqrMagnitude < 0.01f) continue;
                    fwd.Normalize();
                    RaycastHit hit;
                    float room = WorldRays.Raycast(chest, fwd, 7f, out hit) ? hit.distance : 7f;
                    if (room > 3f) picks.Add(new KeyValuePair<float, Transform>(room + Random.value, n.transform));
                }
            picks.Sort((a, b) => b.Key.CompareTo(a.Key));
            // The second spot must be somewhere else in the house (not the same corner twice).
            for (int j = 1; j < picks.Count; j++)
                if (Vector3.Distance(picks[j].Value.position, picks[0].Value.position) > 5f) { var keep = picks[j]; picks.RemoveAt(j); picks.Insert(1, keep); break; }
            for (int i = 0; i < 2 && i < picks.Count; i++)
            {
                var node = picks[i].Value;
                float room = Mathf.Min(picks[i].Key - 1f, 6f) - 0.4f;
                var mama = VersusBots.BuildMamaBody("KE_TrailerMama" + i);
                if (mama != null) { mama.transform.position = node.position; mama.transform.rotation = node.rotation; }
                if (i == 0) _mama1 = mama; else _mama2 = mama;
                Vector3 fwd = node.forward; fwd.y = 0f; fwd.Normalize();
                var shot = _shotsA.Find(s => s.Space == WORLD && s.P0 == Vector3.zero);
                if (shot == null) break;
                // Her face, from her real size (the model is shorter than a guess would say).
                float faceY = node.position.y + 1.4f;
                if (mama != null)
                {
                    bool any = false;
                    var b = new Bounds();
                    foreach (var r in mama.GetComponentsInChildren<Renderer>())
                    {
                        if (r == null) continue;
                        if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                    }
                    if (any) faceY = b.min.y + (b.max.y - b.min.y) * 0.82f;
                }
                Vector3 face = new Vector3(node.position.x, faceY, node.position.z);
                float eye = faceY - node.position.y - 0.2f; // a little below her eyes: she looms
                shot.P0 = node.position + fwd * Mathf.Min(room, 3.8f) + Vector3.up * eye;
                shot.P1 = node.position + fwd * (i == 0 ? 1.9f : 1.4f) + Vector3.up * eye;
                shot.L0 = shot.L1 = face;
            }
            if (_mama1 == null) _mama1 = new GameObject("KE_TrailerNoMama"); // don't search again
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Trailer] House shots from " + picks.Count + " open patrol spots.");
        }

        // ── The camera: the player's own (its post effects stay), moved by us ──
        private bool GrabCamera()
        {
            var fps = RM.fpsController;
            if (fps == null) return false;
            if (fps != _fps)
            {
                _fps = fps;
                _cam = fps.GetComponentInChildren<Camera>();
                if (_cam == null) return false;
                _camLocalPos = _cam.transform.localPosition;
                _camLocalRot = _cam.transform.localRotation;
                if (_flash == null)
                {
                    _flash = new GameObject("KE_TrailerFlashlight").AddComponent<Light>();
                    _flash.transform.SetParent(transform, false);
                    _flash.type = LightType.Spot;
                    _flash.range = 14f;
                    _flash.spotAngle = 70f;
                    _flash.color = new Color(1f, 0.93f, 0.8f);
                    _flash.shadows = LightShadows.Soft;
                }
            }
            if (_fps.enabled) _fps.enabled = false; // no mouse look, no walking (the jumpscare turns it back on)
            return _cam != null;
        }

        private void ReleaseCamera()
        {
            if (_fps != null) _fps.enabled = true;
            if (_cam != null) { _cam.transform.localPosition = _camLocalPos; _cam.transform.localRotation = _camLocalRot; }
            if (_flash != null) _flash.enabled = false;
            _fps = null;
            _cam = null;
        }

        /// <summary>Every screen-space canvas but ours is switched off (checked a few times a second: new ones appear after loads).</summary>
        private void HideHud()
        {
            _hideScan -= Time.unscaledDeltaTime;
            if (_hideScan > 0f) return;
            _hideScan = 0.2f;
            foreach (var c in FindObjectsOfType<Canvas>())
            {
                if (c == null || c == _canvas || c.renderMode == RenderMode.WorldSpace || !c.isRootCanvas) continue;
                bool allowed = _allowCanvas != null && c.gameObject.name == _allowCanvas;
                if (allowed) { if (!c.enabled) c.enabled = true; continue; }
                if (c.enabled) { c.enabled = false; if (!_hidden.Contains(c)) _hidden.Add(c); }
            }
        }

        // ── The overlay: title cards, captions, the logo ─────────────────────
        private CartoonText _cardFx, _capFx, _subFx;
        private Font _happyFont, _scaryFont;

        private void BuildOverlay()
        {
            // The trailer is only ever recorded on a PC, so Windows' own fonts are fair game.
            var fallback = MainMenuButtonInjector.RealFont != null ? MainMenuButtonInjector.RealFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
            _happyFont = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI Black", "Arial Black", "Arial" }, 64) ?? fallback;
            _scaryFont = Font.CreateDynamicFontFromOSFont(new[] { "Impact", "Arial Black", "Arial" }, 64) ?? fallback;

            var cgo = new GameObject("KE_TrailerOverlay");
            cgo.transform.SetParent(transform, false);
            _canvas = cgo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32760;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            _black = Img(cgo.transform, Vector2.zero, Vector2.one, Color.black);
            _white = Img(cgo.transform, Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, 0f));
            _capBar = Img(cgo.transform, new Vector2(0.05f, 0.1f), new Vector2(0.056f, 0.17f), Color.white);
            _capText = Txt(cgo.transform, new Vector2(0.066f, 0.1f), new Vector2(0.95f, 0.17f), 44, TextAnchor.MiddleLeft, _happyFont, out _capFx);
            _cardText = Txt(cgo.transform, new Vector2(0.03f, 0.25f), new Vector2(0.97f, 0.75f), 96, TextAnchor.MiddleCenter, _happyFont, out _cardFx);
            _subText = Txt(cgo.transform, new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.42f), 40, TextAnchor.UpperCenter, _happyFont, out _subFx);
        }

        /// <summary>Scary: bone white (or the card's red) with a near-black outline and a blood-red side. Happy: the color bright on top, deeper below, a white outline and a dark side.</summary>
        private static void Style(CartoonText fx, bool scary, Color c, int size)
        {
            float k = size / 110f;
            fx.Scary = scary;
            fx.OutlineSize = 7.5f * k;
            fx.ExtrudeDepth = 11f * k;
            fx.ExtrudeSteps = 6;
            if (scary)
            {
                bool white = c.r > 0.9f && c.g > 0.9f;
                fx.FillTop = white ? new Color(0.97f, 0.96f, 0.92f) : Color.Lerp(c, Color.white, 0.2f);
                fx.FillBottom = white ? new Color(0.66f, 0.64f, 0.62f) : new Color(c.r * 0.55f, c.g * 0.3f, c.b * 0.3f);
                fx.OutlineColor = new Color(0.06f, 0.02f, 0.02f);
                fx.ExtrudeColor = new Color(0.42f, 0.02f, 0.05f);
            }
            else
            {
                fx.FillTop = Color.Lerp(c, Color.white, 0.35f);
                fx.FillBottom = new Color(c.r, c.g * 0.72f, c.b * 0.5f);
                fx.OutlineColor = Color.white;
                fx.ExtrudeColor = new Color(c.r * 0.45f, c.g * 0.3f, c.b * 0.25f);
            }
        }

        private void DrawOverlay()
        {
            var cards = _part == 2 ? _cardsB : _cardsA;
            var caps = _part == 2 ? _capsB : _capsA;
            Card card = null;
            foreach (var c in cards) if (_t >= c.T0 && _t < c.T1) card = c;
            if (_part == 1) card = _cardsA[_cardsA.Count - 1]; // NOW HIRING while the beach gets ready

            float blackA = 0f, textA = 0f, scale = 1f;
            if (card != null)
            {
                float since = _part == 1 ? _t - PART_A_END : card.T0 < 0f ? _t + 1f : _t - card.T0;
                textA = _part == 1 ? 1f : Mathf.Clamp01((card.T1 - _t) / 0.3f);
                blackA = card.Black ? (card.T0 < 0f ? Mathf.Clamp01((card.T1 - _t) / 1.2f) : Mathf.Clamp01((card.T1 - _t) / 0.25f)) : 0f;
                if (_part == 1) blackA = 1f;
                _cardText.font = card.Scary ? _scaryFont : _happyFont;
                _cardText.text = card.Text;
                _cardText.fontSize = card.Size;
                _cardText.color = new Color(1f, 1f, 1f, textA);
                Style(_cardFx, card.Scary, card.Color, card.Size);
                _cardFx.T = since;
                _cardFx.Slam = card.Hit;
                _cardFx.Wave = !card.Scary;
                _cardFx.Stagger = card.Scary ? 0.05f : 0.06f;
                _cardFx.Refresh();
                scale = card.Hit ? 1f : 1f + since * 0.012f;
                if (card.Hit && since < 0.03f && card.T0 >= 0f) _sfx.PlayOneShot(TrailerMusic.Boom(), 0.9f);
            }
            if (_part == 0 && _t < (ARCADE ? OPEN_END : 6.9f)) blackA = 1f; // the opening is on black
            _black.color = new Color(0f, 0f, 0f, blackA);
            _cardText.gameObject.SetActive(card != null);
            _cardText.rectTransform.localScale = Vector3.one * scale;
            float flash = card != null && card.Hit && card.T0 >= 0f ? Mathf.Clamp01(1f - (_t - card.T0) / 0.15f) * 0.5f : 0f;
            _white.color = new Color(1f, 1f, 1f, flash);

            // The logo at the very end: big, yellow, bouncing in letter by letter; the lines under it type themselves out.
            float lt = RECALLED ? R_LOGO_T : ARCADE ? LOGO_T : 39.4f;
            bool logo = ARCADE || RECALLED ? _t >= lt : _part == 2 && _t >= lt;
            _subText.gameObject.SetActive(logo && _t > lt + 0.9f);
            if (logo)
            {
                _cardText.gameObject.SetActive(true);
                _cardText.font = _happyFont;
                _cardText.text = "TATTLETAIL:\nAFTER HOURS";
                _cardText.fontSize = 150;
                _cardText.lineSpacing = 0.9f;
                _cardText.color = Color.white;
                _cardText.rectTransform.localScale = Vector3.one;
                _cardText.rectTransform.anchorMin = new Vector2(0.03f, 0.45f);
                _cardText.rectTransform.anchorMax = new Vector2(0.97f, 0.95f);
                Style(_cardFx, false, new Color(1f, 0.85f, 0.2f), 150);
                _cardFx.T = _t - lt;
                _cardFx.Slam = false;
                _cardFx.Wave = true;
                _cardFx.Stagger = 0.045f;
                _cardFx.Refresh();
                _black.color = Color.black;
                _subText.text = RECALLED ? "A CO-OP GAME IN A TOWN AT NIGHT  ·  NEW: IMPOPZZZ & SA1MONFR\n\n3.5  THE RECALLED UPDATE\n\nFREE MOD  ·  STEAM & ITCH.IO\nJOIN OUR DISCORD  ·  discord.gg/PjSQQrhsMx"
                    : ARCADE ? "PINBALL  ·  AIR HOCKEY  ·  THE RAT KING\n\n3.4  THE ARCADE UPDATE\n\nFREE MOD  ·  STEAM & ITCH.IO\nJOIN OUR DISCORD  ·  discord.gg/PjSQQrhsMx"
                    : "ENDLESS  ·  VERSUS  ·  PAPA'S PARTY  ·  SUMMER VACATION\n\n3.3  THE SUMMER UPDATE\n\nFREE MOD  ·  STEAM & ITCH.IO";
                _subText.color = Color.white;
                Style(_subFx, false, new Color(1f, 0.6f, 0.85f), 40);
                _subFx.FillTop = Color.white;
                _subFx.FillBottom = new Color(1f, 0.85f, 0.95f);
                _subFx.OutlineColor = new Color(0.4f, 0.08f, 0.5f);
                _subFx.ExtrudeColor = new Color(0.2f, 0.03f, 0.28f);
                _subFx.ExtrudeSteps = 3; // (long lines: every letter is copied per layer, and one UI mesh stops at 65k vertices - the 3.4 take lost these lines)
                _subFx.T = _t - (lt + 0.9f);
                _subFx.Stagger = 0.022f;
                _subFx.Wave = false;
                _subFx.Refresh();
                _music.volume = Mathf.Max(0f, _music.volume - Time.deltaTime * 0.2f);
            }

            Caption cap = null;
            foreach (var c in caps) if (_t >= c.T0 && _t < c.T1) cap = c;
            bool showCap = cap != null && !logo && (_part != 1);
            _capText.gameObject.SetActive(showCap);
            _capBar.gameObject.SetActive(showCap);
            if (showCap)
            {
                float a = Mathf.Clamp01((cap.T1 - _t) / 0.25f);
                _capText.text = cap.Text;
                _capText.color = new Color(1f, 1f, 1f, a);
                Style(_capFx, false, cap.Color, 44);
                _capFx.FillTop = Color.white;
                _capFx.FillBottom = Color.Lerp(Color.white, cap.Color, 0.35f);
                _capFx.OutlineColor = new Color(cap.Color.r * 0.5f, cap.Color.g * 0.5f, cap.Color.b * 0.5f);
                _capFx.ExtrudeColor = new Color(0f, 0f, 0f, 0.7f);
                _capFx.OutlineSize = 3.5f;
                _capFx.ExtrudeDepth = 5f;
                _capFx.ExtrudeSteps = 3;
                _capFx.T = _t - cap.T0;
                _capFx.Stagger = 0.012f;
                _capFx.Wave = false;
                _capFx.Refresh();
                _capBar.color = new Color(cap.Color.r, cap.Color.g, cap.Color.b, a * Mathf.Clamp01((_t - cap.T0) / 0.2f));
            }
        }

        private static Image Img(Transform parent, Vector2 min, Vector2 max, Color c)
        {
            var go = new GameObject("Img", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var i = go.AddComponent<Image>();
            i.color = c;
            i.raycastTarget = false;
            return i;
        }

        private static Text Txt(Transform parent, Vector2 min, Vector2 max, int size, TextAnchor align, Font font, out CartoonText fx)
        {
            var go = new GameObject("Txt", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.supportRichText = false; // the letters are colored by CartoonText
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            fx = go.AddComponent<CartoonText>();
            return t;
        }
    }

    /// <summary>
    /// Chunky 3D cartoon letters for the trailer: a top-to-bottom gradient fill, a thick outline, a
    /// stepped 3D side under it, and per-letter motion: happy letters pop in with a bounce and wave,
    /// scary ones flicker in and shake. One mesh effect (stacked Outline/Shadow components would
    /// multiply the vertex count for every layer).
    /// </summary>
    public class CartoonText : BaseMeshEffect
    {
        public float T;
        public bool Scary, Slam, Wave;
        public float Stagger = 0.05f, OutlineSize = 7f, ExtrudeDepth = 10f;
        public int ExtrudeSteps = 6;
        public Color FillTop = Color.white, FillBottom = Color.white, OutlineColor = Color.white, ExtrudeColor = Color.black;

        private readonly List<UIVertex> _in = new List<UIVertex>(), _out = new List<UIVertex>();
        private static Vector2[] _ring;

        public void Refresh() { if (graphic != null) graphic.SetVerticesDirty(); }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            if (_ring == null)
            {
                _ring = new Vector2[12];
                for (int i = 0; i < 12; i++) _ring[i] = new Vector2(Mathf.Cos(i * Mathf.PI / 6f), Mathf.Sin(i * Mathf.PI / 6f));
            }
            _in.Clear();
            vh.GetUIVertexStream(_in);
            int quads = _in.Count / 6;

            // Move each letter and give it its gradient.
            for (int q = 0; q < quads; q++)
            {
                Vector3 c = Vector3.zero;
                float minY = float.MaxValue, maxY = float.MinValue;
                for (int k = 0; k < 6; k++) { var p = _in[q * 6 + k].position; c += p; minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
                c /= 6f;
                float a, s, rot;
                Vector3 off;
                Animate(q, out a, out s, out rot, out off);
                Quaternion r = Quaternion.Euler(0f, 0f, rot);
                for (int k = 0; k < 6; k++)
                {
                    var v = _in[q * 6 + k];
                    float g = maxY > minY ? (v.position.y - minY) / (maxY - minY) : 1f;
                    float a0 = v.color.a / 255f;
                    v.position = c + r * ((v.position - c) * s) + off;
                    Color fill = Color.Lerp(FillBottom, FillTop, g);
                    fill.a = a0 * a;
                    v.color = fill;
                    _in[q * 6 + k] = v;
                }
            }

            // Layers, back to front: the 3D side, the outline, the fill.
            _out.Clear();
            Vector3 dir = new Vector3(0.45f, -1f, 0f);
            for (int step = ExtrudeSteps; step >= 1; step--)
            {
                float d = ExtrudeDepth * step / ExtrudeSteps;
                Color ec = Color.Lerp(ExtrudeColor, ExtrudeColor * 0.6f, step / (float)ExtrudeSteps);
                ec.a = ExtrudeColor.a;
                foreach (var o in _ring) Copy(dir * d + (Vector3)(o * OutlineSize), ec);
            }
            foreach (var o in _ring) Copy((Vector3)(o * OutlineSize), OutlineColor);
            foreach (var o in _ring) Copy((Vector3)(o * OutlineSize * 0.5f), OutlineColor);
            _out.AddRange(_in);
            vh.Clear();
            vh.AddUIVertexTriangleStream(_out);
        }

        private void Copy(Vector3 offset, Color col)
        {
            for (int i = 0; i < _in.Count; i++)
            {
                var v = _in[i];
                float a = v.color.a / 255f;
                v.position += offset;
                Color cc = col;
                cc.a *= a;
                v.color = cc;
                _out.Add(v);
            }
        }

        private void Animate(int q, out float a, out float s, out float rot, out Vector3 off)
        {
            float x = Slam ? Mathf.Clamp01(T / 0.16f) : Mathf.Clamp01((T - q * Stagger) / 0.35f);
            off = Vector3.zero;
            if (Scary)
            {
                // Flickers in, then shakes; now and then a letter glitches sideways.
                a = x;
                if (x < 1f && Mathf.PerlinNoise(q * 3.3f, T * 30f) > 0.55f) a *= 0.25f;
                s = Slam ? Mathf.Lerp(1.8f, 1f, 1f - (1f - x) * (1f - x)) : 1f;
                off.x = (Mathf.PerlinNoise(q * 7.1f, T * 9f) - 0.5f) * 5f;
                off.y = (Mathf.PerlinNoise(q * 3.7f + 50f, T * 9f) - 0.5f) * 5f - (Slam ? 0f : (1f - x) * 24f);
                if (Mathf.PerlinNoise(T * 4f, q * 1.3f) > 0.8f) off.x += 9f;
                rot = (Mathf.PerlinNoise(q * 5.3f + 100f, T * 3f) - 0.5f) * 6f;
            }
            else
            {
                // Pops in with a bounce, then bobs along like a wave.
                const float C1 = 1.70158f, C3 = C1 + 1f;
                float back = 1f + C3 * Mathf.Pow(x - 1f, 3f) + C1 * Mathf.Pow(x - 1f, 2f);
                a = Mathf.Clamp01(x * 3f);
                s = back;
                rot = (1f - x) * -25f + (Wave ? Mathf.Sin(T * 2.4f + q * 0.9f) * 4f : 0f);
                off.y = Wave ? Mathf.Sin(T * 3.2f + q * 0.55f) * 5f : 0f;
            }
        }
    }

    /// <summary>The trailer's own sounds: the dark first half (heartbeat, drone, hits, a riser), booms, a bell, a steel-drum chord.</summary>
    public static class TrailerMusic
    {
        private const int SR = 22050;
        private const float TAU = Mathf.PI * 2f;
        private static AudioClip _a, _boom, _bell, _chord;

        public static AudioClip PartA()
        {
            if (_a != null) return _a;
            var b = new float[(int)(SR * 38f)];
            var r = new System.Random(3);
            // Heartbeat on black (0-7 s).
            for (float t = 0.4f; t < 7f; t += 0.9f) { Thump(b, t, 0.7f); Thump(b, t + 0.22f, 0.5f); }
            // A low drone swelling under everything from the Arcade on (7-37.4 s).
            float lp = 0f;
            for (int i = (int)(7f * SR); i < (int)(37.4f * SR) && i < b.Length; i++)
            {
                float t = i / (float)SR, env = Mathf.Clamp01((t - 7f) / 2.5f) * (0.55f + 0.45f * Mathf.Clamp01((t - 7f) / 30f));
                lp += (((float)r.NextDouble() * 2f - 1f) - lp) * 0.01f;
                b[i] += (Mathf.Sin(TAU * 55f * t) * 0.3f + Mathf.Sin(TAU * 82.4f * t) * 0.15f + Mathf.Sin(TAU * 110.2f * t) * 0.08f + lp * 0.9f) * env * 0.6f;
            }
            // A ticking pulse through the machine montage (29-34.8 s).
            for (float t = 29f; t < 34.8f; t += 60f / 132f) Tick(b, t, r);
            // A riser into NOW HIRING (34.8-37.4 s), then silence.
            for (int i = (int)(34.8f * SR); i < (int)(37.4f * SR); i++)
            {
                float t = i / (float)SR, k = (t - 34.8f) / 2.6f;
                b[i] += (((float)r.NextDouble() * 2f - 1f) * 0.25f * k + Mathf.Sin(TAU * (200f + 900f * k * k) * t) * 0.1f * k) * k;
            }
            return _a = Finish("KE_TrailerA", b);
        }

        private static AudioClip _arcade;

        /// <summary>
        /// The 3.4 trailer's track (44 s): a heartbeat on black, then an arcade groove (a 16th-note square-wave
        /// arpeggio over Am-F-C-G at 120 bpm, a kick on the beat, hats between) that drops out for the spooky cards
        /// (IT'S HUNGRY, ALL HAIL) and rises into the logo.
        /// </summary>
        public static AudioClip Arcade()
        {
            if (_arcade != null) return _arcade;
            var b = new float[(int)(SR * 44f)];
            var r = new System.Random(34);
            for (float t = 0.3f; t < 3.4f; t += 0.9f) { Thump(b, t, 0.7f); Thump(b, t + 0.22f, 0.5f); }
            float[][] chords =
            {
                new[] { 220f, 261.6f, 329.6f, 440f }, new[] { 174.6f, 220f, 261.6f, 349.2f },
                new[] { 261.6f, 329.6f, 392f, 523.3f }, new[] { 196f, 246.9f, 293.7f, 392f },
            };
            const float BEAT = 0.5f, START = 3.6f;
            for (float t = START; t < 36f; t += BEAT / 4f)
            {
                bool quiet = (t > 16.8f && t < 18.3f) || (t > 30.2f && t < 31.6f);
                if (quiet) continue;
                int step = Mathf.RoundToInt((t - START) / (BEAT / 4f));
                float[] ch = chords[(step / 16) % 4];
                int[] pattern = { 0, 1, 2, 3, 2, 1, 2, 3 };
                Square(b, t, ch[pattern[step % 8]] * (step % 16 >= 8 ? 2f : 1f), 0.11f, 0.09f);
                if (step % 4 == 0) { Thump(b, t, 0.55f); Square(b, t, ch[0] * 0.5f, 0.45f, 0.08f); }
                if (step % 4 == 2) Tick(b, t, r);
            }
            // Low drone under the spooky cards; a riser into the logo.
            for (int i = (int)(16.8f * SR); i < (int)(18.3f * SR); i++) { float t = i / (float)SR; b[i] += Mathf.Sin(TAU * 55f * t) * 0.35f + Mathf.Sin(TAU * 58.3f * t) * 0.2f; }
            for (int i = (int)(30.2f * SR); i < (int)(31.6f * SR); i++) { float t = i / (float)SR; b[i] += Mathf.Sin(TAU * 110f * t) * 0.18f * (t - 30.2f); }
            for (int i = (int)(34f * SR); i < (int)(36f * SR); i++)
            {
                float t = i / (float)SR, k = (t - 34f) / 2f;
                b[i] += (((float)r.NextDouble() * 2f - 1f) * 0.22f * k + Mathf.Sin(TAU * (300f + 1100f * k * k) * t) * 0.1f * k) * k;
            }
            return _arcade = Finish("KE_TrailerArcade", b);
        }

        private static AudioClip _recalled, _clang;

        /// <summary>
        /// The 3.5 trailer's track (55 s): a heartbeat on black; a slow dread under the street (a drone, a low pulse, ticks); a drum
        /// pulse that speeds up as the toys rush; then a driving minor groove (130 bpm, Dm-Bb-F-C) for the fight that drops to
        /// almost nothing in the treehouse, comes back for the friends, and turns heavy for the five bosses; a riser into the logo.
        /// </summary>
        public static AudioClip Recalled()
        {
            if (_recalled != null) return _recalled;
            var b = new float[(int)(SR * 55f)];
            var r = new System.Random(35);
            for (float t = 0.3f; t < 3.4f; t += 0.9f) { Thump(b, t, 0.7f); Thump(b, t + 0.22f, 0.5f); }

            // The street: a drone swelling under it, a low pulse, and now and then a tick in the dark.
            float lp = 0f;
            for (int i = (int)(3.6f * SR); i < (int)(10.8f * SR); i++)
            {
                float t = i / (float)SR, env = Mathf.Clamp01((t - 3.6f) / 2f);
                lp += (((float)r.NextDouble() * 2f - 1f) - lp) * 0.008f;
                b[i] += (Mathf.Sin(TAU * 55f * t) * 0.28f + Mathf.Sin(TAU * 73.4f * t) * 0.12f + Mathf.Sin(TAU * 146.8f * t) * 0.05f * Mathf.Sin(t * 0.7f) + lp * 0.7f) * env * 0.6f;
            }
            for (float t = 4.2f; t < 10.6f; t += 1.2f) Thump(b, t, 0.4f);
            foreach (float t in new[] { 5.0f, 6.3f, 7.4f, 8.9f, 9.7f }) Tick(b, t, r);

            // They rush: the pulse speeds up, and noise rises into the silence before FIGHT BACK.
            float step = 0.6f;
            for (float t = 10.8f; t < 16.1f; t += step) { Thump(b, t, 0.3f + 0.5f * (t - 10.8f) / 5.3f); step = Mathf.Max(0.16f, step * 0.93f); }
            for (int i = (int)(13f * SR); i < (int)(16.1f * SR); i++)
            {
                float t = i / (float)SR, k = (t - 13f) / 3.1f;
                b[i] += (((float)r.NextDouble() * 2f - 1f) * 0.2f * k + Mathf.Sin(TAU * (150f + 700f * k * k) * t) * 0.09f * k) * k;
            }

            // The groove.
            float[][] chords =
            {
                new[] { 293.7f, 349.2f, 440f, 587.3f }, new[] { 233.1f, 293.7f, 349.2f, 466.2f },
                new[] { 349.2f, 440f, 523.3f, 698.5f }, new[] { 261.6f, 329.6f, 392f, 523.3f },
            };
            float[] roots = { 146.8f, 116.5f, 174.6f, 130.8f };
            const float BEAT = 0.46f, START = 17.6f;
            for (float t = START; t < 36f; t += BEAT / 4f)
            {
                bool cozy = t >= 23.6f && t < 28.8f, quiet = t >= 28.8f && t < 30.3f;
                if (quiet) continue;
                int st = Mathf.RoundToInt((t - START) / (BEAT / 4f));
                float[] ch = chords[(st / 16) % 4];
                int[] pattern = { 0, 1, 2, 3, 2, 1, 2, 3 };
                Square(b, t, ch[pattern[st % 8]] * (st % 16 >= 8 ? 2f : 1f), 0.11f, cozy ? 0.05f : 0.09f);
                if (cozy) continue;
                if (st % 4 == 0) { Thump(b, t, 0.6f); Square(b, t, roots[(st / 16) % 4], 0.4f, 0.09f); }
                if (st % 8 == 4) Snare(b, t, r);
                if (st % 4 == 2) Tick(b, t, r);
            }

            // The bosses: heavy, four on the floor, a rising drone, and a riser into the logo.
            for (float t = 37.4f; t < 45.6f; t += 0.35f) Thump(b, t, 0.8f);
            for (int i = (int)(36f * SR); i < (int)(45.6f * SR); i++)
            {
                float t = i / (float)SR, k = Mathf.Clamp01((t - 36f) / 9.6f);
                b[i] += (Mathf.Sin(TAU * (49f + 18f * k) * t) * 0.3f + Mathf.Sin(TAU * (98f + 36f * k) * t) * 0.12f) * (0.5f + 0.5f * k);
            }
            for (int i = (int)(43.4f * SR); i < (int)(45.6f * SR); i++)
            {
                float t = i / (float)SR, k = (t - 43.4f) / 2.2f;
                b[i] += (((float)r.NextDouble() * 2f - 1f) * 0.22f * k + Mathf.Sin(TAU * (300f + 1300f * k * k) * t) * 0.1f * k) * k;
            }
            return _recalled = Finish("KE_TrailerRecalled", b);
        }

        /// <summary>A sword-on-shield clang: inharmonic partials ringing out over a burst of noise.</summary>
        public static AudioClip Clang()
        {
            if (_clang != null) return _clang;
            var b = new float[(int)(SR * 1.6f)];
            var r = new System.Random(5);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                b[i] = (Mathf.Sin(TAU * 620f * t) * 0.5f + Mathf.Sin(TAU * 1310f * t) * 0.35f + Mathf.Sin(TAU * 2115f * t) * 0.25f + Mathf.Sin(TAU * 3080f * t) * 0.15f) * Mathf.Exp(-t * 5f) * 0.6f
                     + ((float)r.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 60f) * 0.6f;
            }
            return _clang = Finish("KE_TrailerClang", b);
        }

        private static void Snare(float[] b, float at, System.Random r)
        {
            int s = (int)(at * SR);
            for (int i = 0; i < SR / 6 && s + i < b.Length; i++)
            {
                float t = i / (float)SR;
                b[s + i] += (((float)r.NextDouble() * 2f - 1f) * 0.35f + Mathf.Sin(TAU * 190f * t) * 0.2f) * Mathf.Exp(-t * 22f);
            }
        }

        // A soft square-ish note (odd harmonics), plucked.
        private static void Square(float[] b, float at, float f, float len, float amp)
        {
            int s = (int)(at * SR), n = (int)(len * SR);
            for (int i = 0; i < n && s + i < b.Length; i++)
            {
                float t = i / (float)SR, p = TAU * f * t;
                b[s + i] += (Mathf.Sin(p) + Mathf.Sin(3f * p) / 3f + Mathf.Sin(5f * p) / 5f) * Mathf.Exp(-t * 14f) * Mathf.Min(1f, t * 600f) * amp;
            }
        }

        public static AudioClip Boom()
        {
            if (_boom != null) return _boom;
            var b = new float[SR * 2];
            var r = new System.Random(8);
            float ph = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                ph += TAU * Mathf.Lerp(90f, 32f, Mathf.Min(1f, t / 0.5f)) / SR;
                b[i] = Mathf.Sin(ph) * Mathf.Exp(-t * 2.2f) * 0.9f + ((float)r.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 14f) * 0.4f;
            }
            return _boom = Finish("KE_TrailerBoom", b);
        }

        public static AudioClip Bell()
        {
            if (_bell != null) return _bell;
            var b = new float[SR * 2];
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)SR;
                b[i] = (Mathf.Sin(TAU * 1318f * t) + 0.5f * Mathf.Sin(TAU * 1976f * t) + 0.25f * Mathf.Sin(TAU * 2637f * t)) * Mathf.Exp(-t * 2.5f) * 0.35f;
            }
            return _bell = Finish("KE_TrailerBell", b);
        }

        /// <summary>A big steel-drum C major chord for the logo.</summary>
        public static AudioClip PanChord()
        {
            if (_chord != null) return _chord;
            var b = new float[SR * 4];
            foreach (float f in new[] { 261.6f, 329.6f, 392f, 523.3f, 659.3f })
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR, p = TAU * f * t;
                    b[i] += (Mathf.Sin(p) + 0.5f * Mathf.Sin(2f * p) * Mathf.Exp(-t * 4f) + 0.2f * Mathf.Sin(3.01f * p) * Mathf.Exp(-t * 8f)) * Mathf.Min(1f, t * 400f) * Mathf.Exp(-t * 1.3f) * 0.12f;
                }
            return _chord = Finish("KE_TrailerChord", b);
        }

        private static void Thump(float[] b, float at, float amp)
        {
            int s = (int)(at * SR);
            float ph = 0f;
            for (int i = 0; i < SR / 4 && s + i < b.Length; i++)
            {
                float t = i / (float)SR;
                ph += TAU * Mathf.Lerp(70f, 40f, Mathf.Min(1f, t / 0.12f)) / SR;
                b[s + i] += Mathf.Sin(ph) * Mathf.Exp(-t * 18f) * amp;
            }
        }

        private static void Tick(float[] b, float at, System.Random r)
        {
            int s = (int)(at * SR);
            for (int i = 0; i < SR / 30 && s + i < b.Length; i++)
                b[s + i] += (((float)r.NextDouble() * 2f - 1f) * 0.15f + Mathf.Sin(TAU * 3000f * i / SR) * 0.08f) * Mathf.Exp(-(i / (float)SR) * 120f);
        }

        private static AudioClip Finish(string name, float[] b)
        {
            for (int i = 0; i < b.Length; i++) b[i] = b[i] / (1f + Mathf.Abs(b[i]));
            var clip = AudioClip.Create(name, b.Length, 1, SR, false);
            clip.SetData(b, 0);
            return clip;
        }
    }
}
