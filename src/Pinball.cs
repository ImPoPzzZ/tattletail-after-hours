using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// TATTLETAIL PINBALL: a real 3D pinball machine in the Arcade, played in first person. Walk up, hold to play:
    /// your view drops onto the playfield and the flippers are yours. The table's physics live in PinballTable
    /// (2D, 1 ms steps); this builds the cabinet around it and runs the game: three balls, a saved high score,
    /// the hungry House (a scoop), three Tattletail pop bumpers, the ramp, the star-target bank (multiplier up),
    /// slingshots, the plunger, and secrets (three Houses on one ball; three ramps in a row).
    /// Art: tools/pinball_art.py (the backglass is the user's concept art).
    /// </summary>
    public class PinballGame : MonoBehaviour
    {
        public static PinballGame Instance { get; private set; }
        public static bool Playing { get; private set; }
        private static float _leftAt = -10f;
        /// <summary>While you play (and a moment after), the Arcade's own interactions stay off.</summary>
        public static bool BlocksWorld { get { return Playing || Time.unscaledTime < _leftAt + 0.6f; } }

        private const float TILT = 6.5f;
        private const string PREF_HIGH = "KE_PinballHigh";
        private static readonly Color[] BumperColors = { new Color(0.75f, 0.35f, 1f), new Color(1f, 0.85f, 0.2f), new Color(0.2f, 0.9f, 0.9f) };

        private readonly PinballTable _t = new PinballTable();
        private Transform _pf, _ball, _lFlip, _rFlip, _rod, _knob;
        private readonly Transform[] _bumperTops = new Transform[3];
        private readonly Light[] _bumperLights = new Light[3];
        private readonly float[] _bumperFlash = new float[3];
        private readonly Renderer[] _targets = new Renderer[4];
        private readonly bool[] _lit = new bool[4];
        private Renderer[] _windows;
        private Light _houseLight;
        private float _houseFlash, _bankResetAt = -1f;
        private Material _targetOn, _targetOff, _windowOn, _windowOff;
        private Text _ledLeft, _ledRight, _ledMid;

        private enum State { Live, Captured, Ramp, Drained, GameOver }
        private State _state = State.GameOver;
        private int _score, _high, _ballNo, _bankMult, _rampChain, _houseCount, _timeMult;
        private float _alive, _timer, _rampT, _lastRamp = -100f, _stillT;
        private string _msg = "";
        private float _msgUntil;
        private bool _prevL, _prevR, _prevPlunge;
        private bool _trailer;      // the trailer is filming: it plays itself
        private float _aiFlipUntil;

        // ── For the trailer ──────────────────────────────────────────────────
        /// <summary>The machine plays itself (flippers, sounds, lights, the LED scores) until stopped.</summary>
        public void TrailerPlay(bool on)
        {
            _trailer = on;
            if (on) { _score = 0; _ballNo = 1; NewBall(); _t.Pull = 0.8f; _t.Launch(); }
            else { _state = State.GameOver; _ball.gameObject.SetActive(false); _score = 0; }
        }

        /// <summary>Sets up a moment for a shot: "flip" (a ball dropping onto a flipper), "bumpers", "ramp", "house".</summary>
        public void TrailerStage(string what)
        {
            if (!_trailer) return;
            _state = State.Live;
            _ball.gameObject.SetActive(true);
            switch (what)
            {
                case "flip": _t.Pos = new Vector2(PinballTable.XC + 0.07f, 0.34f); _t.Vel = new Vector2(-0.05f, -0.3f); break;
                case "bumpers": _t.Pos = new Vector2(PinballTable.XC - 0.02f, 0.58f); _t.Vel = new Vector2(0.35f, 1.9f); break;
                case "ramp": _t.Pos = new Vector2(-0.2475f, 0.7f); _t.Vel = new Vector2(0f, 1.2f); break;
                case "house": _t.Pos = new Vector2(PinballTable.XC, PinballTable.DOOR_Y - 0.16f); _t.Vel = new Vector2(0f, 0.95f); break;
            }
        }

        // The trailer's flipper hands: flip when the ball comes down onto a flipper.
        private void AiFlip(out bool left, out bool right)
        {
            left = right = false;
            if (Time.time < _aiFlipUntil) { left = _t.Pos.x < PinballTable.XC; right = !left; return; }
            if (_state != State.Live || _t.Vel.y > 0.2f || _t.Pos.y > 0.27f || _t.InLane) return;
            PinballTable.Flipper f = _t.Pos.x < PinballTable.XC ? _t.LeftFlipper : _t.RightFlipper;
            if ((_t.Pos - f.Pivot).magnitude < 0.085f) { _aiFlipUntil = Time.time + 0.18f; left = f.Left; right = !f.Left; }
        }

        private FirstPersonController _fps;
        private Camera _cam;
        private Vector3 _camLocalPos;
        private Quaternion _camLocalRot;
        private GameObject _ui;
        private Text _uiScore, _uiMsg;
        private AudioSource _audio;
        private Interactable _prompt;

        // The "Play" prompt goes away while you're at the machine.
        private void ShowPrompt(bool on)
        {
            if (_prompt == null) return;
            _prompt.conditions.newStateOnConditionsMet = on ? Interactable.State.Enabled : Interactable.State.Hidden;
            if (on) _prompt.SetStateEnabled(); else _prompt.SetStateHidden();
        }

        // ── Building ─────────────────────────────────────────────────────────
        /// <summary>Builds the machine in the Arcade (room-local position, facing yaw) and makes it playable.</summary>
        public static void Build(Transform room, Vector3 pos, float yaw)
        {
            var root = new GameObject("PinballMachine");
            root.transform.SetParent(room, false);
            root.transform.localPosition = pos;
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var game = root.AddComponent<PinballGame>();
            game.BuildMachine();
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.0f, -0.05f);
            col.size = new Vector3(0.78f, 2.0f, 1.45f);
            game._prompt = PartyGameRunner.MakeInteractable(root, "Play: TATTLETAIL PINBALL", 0.4f);
            HubLayout.Register(root, HubMachine.Pinball);
        }

        // Table space (x across, y up the table, h above it) -> the playfield's local space.
        private static Vector3 V(float x, float y, float h) { return new Vector3(x, h, y); }
        private static Vector3 S(float x, float y, float h) { return new Vector3(x, h, y); }

        private static GameObject B(Transform parent, string name, Vector3 center, Vector3 size, Material m)
        {
            var go = Mat.Box(parent, name, center, size, m);
            Destroy(go.GetComponent<Collider>());
            return go;
        }

        private static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().material = m;
            Destroy(go.GetComponent<Collider>());
            return go;
        }

        private void BuildMachine()
        {
            Transform root = transform;
            Material body = Mat.Lit(new Color(0.14f, 0.07f, 0.22f), 0.45f);
            Material plastic = Mat.Lit(new Color(0.42f, 0.2f, 0.62f), 0.7f);
            Material chrome = Mat.Lit(new Color(0.78f, 0.78f, 0.84f), 0.92f);
            Material pink = Mat.Unlit(new Color(1f, 0.35f, 0.85f));
            Material dark = Mat.Lit(new Color(0.04f, 0.03f, 0.06f), 0.3f);
            Material rubber = Mat.Lit(new Color(0.95f, 0.95f, 0.95f), 0.25f);
            _targetOn = Mat.Unlit(new Color(1f, 0.85f, 0.2f));
            _targetOff = Mat.Lit(new Color(0.45f, 0.36f, 0.1f), 0.5f);
            _windowOn = Mat.Unlit(new Color(1f, 0.85f, 0.4f));
            _windowOff = Mat.Unlit(new Color(0.45f, 0.32f, 0.12f));

            // The playfield frame: tilted up toward the back. Local x = the player's right, z = up the table, y = its normal.
            float s = Mathf.Sin(TILT * Mathf.Deg2Rad), c = Mathf.Cos(TILT * Mathf.Deg2Rad);
            _pf = new GameObject("Playfield").transform;
            _pf.SetParent(root, false);
            _pf.localPosition = new Vector3(0f, 0.93f, 0.62f);
            _pf.localRotation = Quaternion.LookRotation(new Vector3(0f, s, -c), new Vector3(0f, c, s));

            // The art (a lit backdrop: Unlit, so the room's lighting never washes it out).
            ArtQuad(_pf, "Playfield art", V(0f, 0.58f, 0f), Quaternion.Euler(90f, 0f, 0f), 0.6f, 1.2f, "pinball_playfield.jpg");

            // Cabinet: the body under the playfield, side rails with neon, the back rail, the apron, the lockdown bar.
            B(_pf, "Body", V(0f, 0.56f, -0.13f), S(0.72f, 1.36f, 0.24f), body);
            foreach (float sx in new[] { -1f, 1f })
            {
                B(_pf, "Side rail", V(0.33f * sx, 0.56f, 0.04f), S(0.06f, 1.36f, 0.1f), body);
                B(_pf, "Side neon", V(0.33f * sx, 0.56f, 0.092f), S(0.03f, 1.36f, 0.004f), pink);
            }
            B(_pf, "Back rail", V(0f, 1.21f, 0.04f), S(0.6f, 0.06f, 0.1f), body);
            B(_pf, "Apron", V(-0.0325f, -0.045f, 0.012f), S(0.535f, 0.15f, 0.024f), body);
            B(_pf, "Lane cover", V(0.2575f, -0.054f, 0.012f), S(0.045f, 0.132f, 0.024f), body);
            B(_pf, "Lockdown bar", V(0f, -0.1f, 0.1f), S(0.72f, 0.05f, 0.03f), chrome);
            B(_pf, "Coin door", V(0f, -0.123f, -0.13f), S(0.22f, 0.004f, 0.12f), Mat.Lit(new Color(0.1f, 0.1f, 0.12f), 0.6f));
            foreach (float sx in new[] { -1f, 1f }) B(_pf, "Coin slot", V(0.045f * sx, -0.126f, -0.11f), S(0.02f, 0.004f, 0.04f), Mat.Unlit(new Color(1f, 0.15f, 0.1f)));

            // Legs (the back ones longer: the whole cabinet leans toward you).
            foreach (float sx in new[] { -1f, 1f })
            {
                B(root, "Leg", new Vector3(0.33f * sx, 0.345f, 0.6f), new Vector3(0.05f, 0.69f, 0.05f), chrome);
                B(root, "Leg", new Vector3(0.33f * sx, 0.41f, -0.5f), new Vector3(0.05f, 0.82f, 0.05f), chrome);
            }

            // The backbox: your concept art on the backglass, the red LED scores under it.
            B(root, "Backbox", new Vector3(0f, 1.51f, -0.66f), new Vector3(0.74f, 0.9f, 0.16f), body);
            ArtQuad(root, "Backglass", new Vector3(0f, 1.69f, -0.578f), Quaternion.Euler(0f, 180f, 0f), 0.66f, 0.395f, "pinball_backglass.jpg");
            B(root, "Score panel", new Vector3(0f, 1.32f, -0.579f), new Vector3(0.66f, 0.2f, 0.01f), Mat.Unlit(new Color(0.03f, 0.01f, 0.01f)));
            Color led = new Color(1f, 0.22f, 0.12f);
            _ledLeft = Led(root, new Vector3(-0.17f, 1.32f, -0.572f), led);
            _ledRight = Led(root, new Vector3(0.17f, 1.32f, -0.572f), led);
            _ledMid = Led(root, new Vector3(0f, 1.24f, -0.572f), new Color(1f, 0.55f, 0.15f));
            _ledMid.fontSize = Mathf.RoundToInt(_ledMid.fontSize * 0.7f);
            foreach (float sx in new[] { -1f, 1f }) B(root, "Backbox neon", new Vector3(0.37f * sx, 1.51f, -0.575f), new Vector3(0.012f, 0.9f, 0.012f), pink);
            B(root, "Backbox neon", new Vector3(0f, 1.965f, -0.575f), new Vector3(0.75f, 0.012f, 0.012f), pink);

            // Lights over the playfield.
            PfLight(V(PinballTable.XC, 0.35f, 0.35f), new Color(0.85f, 0.75f, 1f), 1.3f, 0.9f);
            PfLight(V(PinballTable.XC, 0.85f, 0.35f), new Color(0.85f, 0.75f, 1f), 1.3f, 0.9f);

            // The table's walls (the House draws its own).
            foreach (var seg in _t.Segs)
            {
                if (seg.Hidden || seg.Kind == PinballTable.Kind.Plunger) continue;
                if (seg.Kind == PinballTable.Kind.Target)
                    _targets[seg.Id] = Rail(seg, 0.03f, 0.01f, _targetOff, null).GetComponentInChildren<Renderer>();
                else if (seg.Kind == PinballTable.Kind.Sling)
                    Rail(seg, 0.03f, 0.012f, rubber, pink);
                else if (seg.Kind == PinballTable.Kind.Gate)
                    Rail(seg, 0.004f, 0.004f, chrome, null).transform.localPosition += V(0f, 0f, 0.022f);
                else
                    Rail(seg, 0.03f, 0.008f, plastic, pink);
            }

            // Pop bumpers, each with a Tattletail on top (the real model if the level has one to copy).
            foreach (var b in _t.Bumpers)
            {
                var holder = new GameObject("Bumper").transform;
                holder.SetParent(_pf, false);
                holder.localPosition = V(b.C.x, b.C.y, 0f);
                Color col = BumperColors[b.Id];
                Prim(PrimitiveType.Cylinder, holder, "Base", new Vector3(0f, 0.003f, 0f), new Vector3(0.066f, 0.003f, 0.066f), dark);
                Prim(PrimitiveType.Cylinder, holder, "Skirt", new Vector3(0f, 0.02f, 0f), new Vector3(0.058f, 0.014f, 0.058f), Mat.Unlit(col));
                Prim(PrimitiveType.Cylinder, holder, "Cap", new Vector3(0f, 0.036f, 0f), new Vector3(0.064f, 0.003f, 0.064f), rubber);
                var top = new GameObject("Top").transform;
                top.SetParent(holder, false);
                top.localPosition = new Vector3(0f, 0.039f, 0f);
                _bumperTops[b.Id] = top;
                var lgo = new GameObject("Bumper light");
                lgo.transform.SetParent(holder, false);
                lgo.transform.localPosition = new Vector3(0f, 0.08f, 0f);
                var l = lgo.AddComponent<Light>();
                l.type = LightType.Point; l.color = col; l.range = 0.28f; l.intensity = 0.4f; l.shadows = LightShadows.None;
                l.renderMode = LightRenderMode.ForcePixel;
                _bumperLights[b.Id] = l;
            }

            BuildHouse(dark);
            BuildRamp(chrome, pink);

            // Flippers: white bats with a pink neon top, turning on their pivots.
            _lFlip = Flipper(_t.LeftFlipper, rubber, pink);
            _rFlip = Flipper(_t.RightFlipper, rubber, pink);

            // The plunger (its knob sticks out of the front) and the ball.
            _rod = Prim(PrimitiveType.Cylinder, _pf, "Plunger rod", V(PinballTable.LANE_X, -0.07f, 0.012f), Vector3.one, chrome).transform;
            _rod.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _knob = Prim(PrimitiveType.Sphere, _pf, "Plunger knob", V(PinballTable.LANE_X, -0.16f, 0.012f), Vector3.one * 0.036f, Mat.Lit(new Color(0.85f, 0.1f, 0.15f), 0.8f)).transform;
            _ball = Prim(PrimitiveType.Sphere, _pf, "Ball", Vector3.zero, Vector3.one * PinballTable.BALL * 2f, Mat.Lit(new Color(0.86f, 0.86f, 0.92f), 0.96f)).transform;
            _ball.gameObject.SetActive(false);

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 0f;
            _audio.playOnAwake = false;
            _audio.volume = 0.6f;
            _high = PlayerPrefs.GetInt(PREF_HIGH, 0);
        }

        private void PfLight(Vector3 local, Color color, float intensity, float range)
        {
            var go = new GameObject("Playfield light");
            go.transform.SetParent(_pf, false);
            go.transform.localPosition = local;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = color; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;
        }

        private Text Led(Transform parent, Vector3 pos, Color color)
        {
            var t = WorldText.Create(parent, "", color, 0.042f, TextAnchor.MiddleCenter, 0.3f);
            t.canvas.transform.localPosition = pos;
            t.canvas.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // (world text reads from behind: turn it to face the player)
            return t;
        }

        // A wall along a table segment: its body, and (optionally) a neon strip along the top.
        private GameObject Rail(PinballTable.Seg seg, float height, float thick, Material m, Material top)
        {
            Vector2 d = seg.B - seg.A, mid = (seg.A + seg.B) * 0.5f;
            float len = d.magnitude + thick;
            var g = new GameObject("Rail");
            g.transform.SetParent(_pf, false);
            g.transform.localPosition = V(mid.x, mid.y, 0f);
            g.transform.localRotation = Quaternion.Euler(0f, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, 0f);
            B(g.transform, "Wall", new Vector3(0f, height * 0.5f, 0f), new Vector3(len, height, thick), m);
            if (top != null) B(g.transform, "Neon", new Vector3(0f, height + 0.0015f, 0f), new Vector3(len, 0.003f, thick * 0.7f), top);
            return g;
        }

        private Transform Flipper(PinballTable.Flipper f, Material m, Material top)
        {
            var pivot = new GameObject(f.Left ? "Left flipper" : "Right flipper").transform;
            pivot.SetParent(_pf, false);
            pivot.localPosition = V(f.Pivot.x, f.Pivot.y, 0f);
            B(pivot, "Bat", new Vector3(f.Length * 0.5f, 0.012f, 0f), new Vector3(f.Length, 0.022f, f.R0 + f.R1 + 0.004f), m);
            B(pivot, "Neon", new Vector3(f.Length * 0.5f, 0.0245f, 0f), new Vector3(f.Length * 0.9f, 0.003f, 0.008f), top);
            Prim(PrimitiveType.Cylinder, pivot, "Hub", new Vector3(0f, 0.012f, 0f), new Vector3(f.R0 * 2f, 0.011f, f.R0 * 2f), m);
            Prim(PrimitiveType.Cylinder, pivot, "Tip", new Vector3(f.Length, 0.012f, 0f), new Vector3(f.R1 * 2f, 0.011f, f.R1 * 2f), m);
            return pivot;
        }

        private void BuildHouse(Material dark)
        {
            float x = PinballTable.XC, y0 = PinballTable.DOOR_Y;
            Material wall = Mat.Lit(new Color(0.86f, 0.66f, 0.46f), 0.3f);
            Material roof = Mat.Lit(new Color(0.45f, 0.2f, 0.25f), 0.4f);
            var house = new GameObject("House").transform;
            house.SetParent(_pf, false);
            house.localPosition = V(x, y0, 0f);
            B(house, "Walls", V(0f, 0.0575f, 0.035f), S(0.14f, 0.115f, 0.07f), wall);
            // A gable roof (its ridge running front to back), the gable's face and the clock on it.
            foreach (float sx in new[] { -1f, 1f })
            {
                var r = B(house, "Roof", V(0.036f * sx, 0.0575f, 0.087f), S(0.09f, 0.125f, 0.008f), roof);
                r.transform.localRotation = Quaternion.Euler(0f, 0f, -33f * sx);
            }
            var gable = B(house, "Gable", V(0f, -0.001f, 0.07f), S(0.056f, 0.004f, 0.056f), wall);
            gable.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var clock = Prim(PrimitiveType.Cylinder, house, "Clock", V(0f, -0.004f, 0.09f), new Vector3(0.034f, 0.002f, 0.034f), Mat.Lit(new Color(0.97f, 0.95f, 0.88f), 0.5f));
            clock.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            B(house, "Clock hand", V(0f, -0.007f, 0.095f), S(0.002f, 0.002f, 0.012f), dark);
            B(house, "Clock hand", V(0.004f, -0.007f, 0.09f), S(0.009f, 0.002f, 0.002f), dark);
            // The door (its mouth) and the windows.
            B(house, "Door", V(0f, -0.001f, 0.024f), S(PinballTable.DOOR_HALF * 2f, 0.004f, 0.048f), Mat.Unlit(new Color(0.01f, 0.0f, 0.02f)));
            _windows = new Renderer[2];
            for (int i = 0; i < 2; i++)
                _windows[i] = B(house, "Window", V(i == 0 ? -0.047f : 0.047f, -0.001f, 0.045f), S(0.024f, 0.004f, 0.022f), _windowOff).GetComponent<Renderer>();
            var sign = WorldText.Create(house, "Hee\nHee", new Color(1f, 0.6f, 0.2f), 0.012f, TextAnchor.MiddleCenter, 0.05f);
            sign.canvas.transform.localPosition = V(0.05f, 0.03f, 0.13f);
            sign.canvas.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(-20f, 0f, 0f);
            var lgo = new GameObject("House light");
            lgo.transform.SetParent(house, false);
            lgo.transform.localPosition = V(0f, -0.04f, 0.05f);
            _houseLight = lgo.AddComponent<Light>();
            _houseLight.type = LightType.Point; _houseLight.color = new Color(1f, 0.75f, 0.35f); _houseLight.range = 0.25f; _houseLight.intensity = 0.3f;
            _houseLight.shadows = LightShadows.None; _houseLight.renderMode = LightRenderMode.ForcePixel;
        }

        // The ramp: a purple track on posts, with pink neon rails, from the left lane over the back and down the right side.
        private void BuildRamp(Material chrome, Material rail)
        {
            Material floor = Mat.Lit(new Color(0.55f, 0.25f, 0.8f), 0.8f);
            var ramp = new GameObject("Ramp").transform;
            ramp.SetParent(_pf, false);
            const int N = 32;
            for (int i = 0; i < N; i++)
            {
                Vector3 p0 = PinballTable.RampPoint(i / (float)N), p1 = PinballTable.RampPoint((i + 1) / (float)N);
                Vector3 a = V(p0.x, p0.y, p0.z), b = V(p1.x, p1.y, p1.z), dir = b - a;
                var piece = new GameObject("Ramp piece").transform;
                piece.SetParent(ramp, false);
                piece.localPosition = (a + b) * 0.5f;
                piece.localRotation = Quaternion.LookRotation(dir, Vector3.up);
                float len = dir.magnitude + 0.002f;
                B(piece, "Floor", new Vector3(0f, -0.0015f, 0f), new Vector3(0.048f, 0.003f, len), floor);
                foreach (float sx in new[] { -1f, 1f }) B(piece, "Rail", new Vector3(0.026f * sx, 0.01f, 0f), new Vector3(0.004f, 0.02f, len), rail);
                if (i % 4 == 2 && p0.z > 0.02f) B(ramp, "Post", V(p0.x, p0.y, p0.z * 0.5f), S(0.006f, 0.006f, p0.z), chrome);
            }
        }

        // A flat picture (same layout as Unity's Quad: faces -z, uv 0..1), with white vertex colors for UI/Default.
        internal static void ArtQuad(Transform parent, string name, Vector3 pos, Quaternion rot, float w, float h, string resource)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            var mesh = new Mesh { name = name };
            mesh.vertices = new[] { new Vector3(-w / 2f, -h / 2f, 0f), new Vector3(w / 2f, -h / 2f, 0f), new Vector3(-w / 2f, h / 2f, 0f), new Vector3(w / 2f, h / 2f, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.triangles = new[] { 0, 3, 1, 3, 0, 2 };
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = TexMat(resource);
        }

        // Unlit art. Tattletail's build has no "Unlit/Texture"; UI/Default is always there (every menu uses it).
        internal static Material TexMat(string resource)
        {
            Shader sh = Shader.Find("Unlit/Texture");
            if (sh == null) sh = Shader.Find("UI/Default");
            if (sh == null) sh = Shader.Find("Standard");
            if (sh == null) return Mat.Unlit(new Color(0.12f, 0.06f, 0.25f));
            var m = new Material(sh);
            if (m.HasProperty("unity_GUIZTestMode")) m.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.LessEqual); // (a UI shader in the world: never draw over the ball)
            byte[] bytes = null;
            using (var st = typeof(PinballGame).Assembly.GetManifestResourceStream(resource))
            {
                if (st == null) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Arcade] Missing art " + resource); return Mat.Unlit(new Color(0.12f, 0.06f, 0.25f)); }
                bytes = new byte[st.Length];
                int read = 0;
                while (read < bytes.Length) { int k = st.Read(bytes, read, bytes.Length - read); if (k <= 0) break; read += k; }
            }
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, true);
            tex.LoadImage(bytes);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.anisoLevel = 4;
            m.mainTexture = tex;
            return m;
        }

        private void Awake() { Instance = this; }

        private void Start()
        {
            // The Tattletails on the bumpers (the real model, recolored: purple, yellow, teal).
            for (int i = 0; i < 3; i++)
            {
                Transform top = _bumperTops[i];
                GameObject fig = null;
                try
                {
                    string label;
                    fig = PartyTattletailModels.BuildColored(top.position, Quaternion.LookRotation(-transform.forward, Vector3.up), top, i, out label);
                }
                catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Pinball] No Tattletail for a bumper: " + e.Message); }
                if (fig != null) FitOnto(fig, top, 0.07f);
                else FallbackTattletail(top, BumperColors[i]);
            }
            UpdateLeds();
        }

        // Scales a model to a height and stands it on the holder.
        internal static void FitOnto(GameObject fig, Transform holder, float height)
        {
            foreach (var col in fig.GetComponentsInChildren<Collider>(true)) Destroy(col);
            var rs = fig.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            fig.transform.localScale *= height / Mathf.Max(b.size.y, 1e-4f);
            b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            fig.transform.position += holder.position - new Vector3(b.center.x, b.min.y, b.center.z);
        }

        // If there's no Tattletail to copy: a fuzzy ball with ears and big eyes.
        internal static void FallbackTattletail(Transform top, Color color, float scale = 1f)
        {
            Material fur = Mat.Lit(color, 0.2f);
            var holder = new GameObject("Tattletail").transform; // (scaled: the same little figure, any size)
            holder.SetParent(top, false);
            holder.localScale = Vector3.one * scale;
            top = holder;
            Prim(PrimitiveType.Sphere, top, "Fluff", new Vector3(0f, 0.03f, 0f), new Vector3(0.05f, 0.055f, 0.05f), fur);
            foreach (float sx in new[] { -1f, 1f })
            {
                var ear = Prim(PrimitiveType.Capsule, top, "Ear", new Vector3(0.017f * sx, 0.062f, 0f), new Vector3(0.012f, 0.014f, 0.008f), fur);
                ear.transform.localRotation = Quaternion.Euler(0f, 0f, -20f * sx);
                // (it faces -z, like the real Tattletail model, so both turn the same way)
                Prim(PrimitiveType.Sphere, top, "Eye", new Vector3(0.011f * sx, 0.036f, -0.021f), Vector3.one * 0.013f, Mat.Lit(Color.white, 0.8f));
                Prim(PrimitiveType.Sphere, top, "Iris", new Vector3(0.011f * sx, 0.036f, -0.027f), Vector3.one * 0.007f, Mat.Lit(new Color(0.2f, 0.45f, 0.9f), 0.9f));
            }
        }

        // ── Playing ──────────────────────────────────────────────────────────
        /// <summary>Walks you up to the machine: your view drops onto the playfield and a new game starts.</summary>
        public void Enter()
        {
            if (Playing) return;
            _fps = RM.fpsController;
            _cam = _fps != null ? _fps.GetComponentInChildren<Camera>() : null;
            if (_cam == null) _cam = Camera.main;
            if (_fps == null || _cam == null) return;
            _camLocalPos = _cam.transform.localPosition;
            _camLocalRot = _cam.transform.localRotation;
            _fps.enabled = false;
            Playing = true;
            ShowPrompt(false);
            HubMode.SetHudText("");
            BuildUI();
            NewGame();
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Pinball] Playing.");
        }

        private void Leave()
        {
            if (!Playing) return;
            Playing = false;
            _leftAt = Time.unscaledTime;
            SaveHigh();
            if (_cam != null) { _cam.transform.localPosition = _camLocalPos; _cam.transform.localRotation = _camLocalRot; }
            if (_fps != null) _fps.enabled = true;
            if (_ui != null) Destroy(_ui);
            ShowPrompt(true);
            _state = State.GameOver;
            _ball.gameObject.SetActive(false);
            _t.LeftFlipper.Pressed = _t.RightFlipper.Pressed = false;
            UpdateLeds();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (Playing) Leave();
        }

        private void NewGame()
        {
            _score = 0;
            _ballNo = 1;
            NewBall();
            Say("BALL 1\nHold SPACE to pull the plunger back, let go to launch", 4f);
        }

        private void NewBall()
        {
            _alive = 0f; _bankMult = 0; _houseCount = 0; _rampChain = 0; _timeMult = 0; _stillT = 0f;
            for (int i = 0; i < 4; i++) _lit[i] = false;
            _t.Serve();
            _state = State.Live;
            _ball.gameObject.SetActive(true);
        }

        private int Mult { get { return Mathf.Min(8, 1 + _timeMult + _bankMult); } }

        private int Points(int n)
        {
            int got = n * Mult;
            _score += got;
            return got;
        }

        private void Say(string text, float seconds) { _msg = text; _msgUntil = Time.time + seconds; }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            bool left = false, right = false, plunge = false;
            if (Playing)
            {
                if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.E)) { Leave(); return; }
                left = Input.GetMouseButton(0) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.LeftShift);
                right = Input.GetMouseButton(1) || Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.RightShift);
                plunge = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
                if (left && !_prevL) Play(Sounds.Flip, 0.7f);
                if (right && !_prevR) Play(Sounds.Flip, 0.7f);
            }
            else if (_trailer)
            {
                AiFlip(out left, out right);
                if (left && !_prevL) Play(Sounds.Flip, 0.7f);
                if (right && !_prevR) Play(Sounds.Flip, 0.7f);
                if (_t.InLane && _t.Pos.y < 0.1f && _t.Vel.magnitude < 0.02f) { _t.Pull = 0.8f; _t.Launch(); }
            }
            _t.LeftFlipper.Pressed = left;
            _t.RightFlipper.Pressed = right;

            switch (_state)
            {
                case State.Live: TickLive(dt, plunge); break;
                case State.Captured:
                    _t.Tick(dt, false);
                    _timer -= dt;
                    if (_timer <= 0f)
                    {
                        _t.Pos = new Vector2(PinballTable.XC, PinballTable.DOOR_Y - 0.022f);
                        _t.Vel = new Vector2(Random.Range(-0.3f, 0.3f), -0.7f);
                        _ball.gameObject.SetActive(true);
                        _state = State.Live;
                        Play(Sounds.Launch, 0.5f);
                    }
                    break;
                case State.Ramp:
                    _t.Tick(dt, false);
                    _rampT += dt / 1.1f;
                    if (_rampT >= 1f)
                    {
                        Vector3 end = PinballTable.RampPath[PinballTable.RampPath.Length - 1];
                        _t.Pos = new Vector2(end.x, end.y);
                        _t.Vel = new Vector2(-0.05f, -0.4f);
                        _state = State.Live;
                    }
                    break;
                case State.Drained:
                    _t.Tick(dt, false);
                    _timer -= dt;
                    if (_timer <= 0f)
                    {
                        if (_trailer) { NewBall(); _t.Pull = 0.8f; _t.Launch(); } // (the trailer never runs out of balls)
                        else if (_ballNo >= 3) GameOver();
                        else { _ballNo++; NewBall(); Say("BALL " + _ballNo + "\nHold SPACE, let go to launch", 2.5f); }
                    }
                    break;
                case State.GameOver:
                    _t.Tick(dt, false);
                    if (Playing && plunge && !_prevPlunge) NewGame();
                    break;
            }
            _prevL = left; _prevR = right; _prevPlunge = plunge;

            if (_bankResetAt > 0f && Time.time > _bankResetAt) { _bankResetAt = -1f; for (int i = 0; i < 4; i++) _lit[i] = false; }
            Visuals(dt);
            UpdateLeds();
        }

        private void TickLive(float dt, bool plunge)
        {
            // The plunger: pull while held, let go to launch.
            if (_t.InLane && _t.Pos.y < 0.1f)
            {
                if (plunge) _t.Pull = Mathf.Min(1f, _t.Pull + dt * 1.1f);
                else if (_prevPlunge && _t.Pull > 0f) { Play(Sounds.Launch, 0.4f + _t.Pull * 0.6f); _t.Launch(); }
            }
            else _t.Pull = 0f;

            float prevY = _t.Pos.y;
            _t.Tick(dt, true);

            for (int i = 0; i < _t.HitBumpers.Count; i++)
            {
                int id = _t.HitBumpers[i].Id;
                Points(100);
                _bumperFlash[id] = 1f;
                Play(Sounds.Bump, 0.8f);
            }
            _t.HitBumpers.Clear();
            for (int i = 0; i < _t.HitSegs.Count; i++)
            {
                var seg = _t.HitSegs[i];
                if (seg.Kind == PinballTable.Kind.Sling) { Points(50); Play(Sounds.Sling, 0.7f); }
                else if (seg.Kind == PinballTable.Kind.Target) HitTarget(seg.Id);
            }
            _t.HitSegs.Clear();
            _t.FlipperHit = false;

            if (!_t.InLane) _alive += dt;
            int tm = Mathf.Min(3, (int)(_alive / 30f));
            if (tm > _timeMult) { _timeMult = tm; Say("STILL GOING!  MULTIPLIER x" + Mult, 2f); Play(Sounds.Jackpot, 0.6f); }

            if (_t.InHouseDoor) { EnterHouse(); return; }
            if (_t.EnteredRamp(prevY)) { EnterRamp(); return; }
            if (_t.Pos.y < -0.03f) { Drain(); return; }
            if (float.IsNaN(_t.Pos.x) || Mathf.Abs(_t.Pos.x) > 0.31f || _t.Pos.y > 1.2f) { _t.Serve(); return; } // (never seen; just in case)

            // A ball sitting still somewhere (not on the plunger, not held on a flipper) gets a nudge.
            bool holding = _t.LeftFlipper.Pressed || _t.RightFlipper.Pressed;
            if (_t.Vel.magnitude < 0.015f && !_t.InLane && !holding) _stillT += dt; else _stillT = 0f;
            if (_stillT > 3f) { _stillT = 0f; _t.Vel = new Vector2(Random.Range(-0.4f, 0.4f), 0.6f); }
        }

        private void HitTarget(int id)
        {
            if (_lit[id]) { Points(50); Play(Sounds.Sling, 0.5f); return; }
            _lit[id] = true;
            Points(250);
            Play(Sounds.Target, 0.7f);
            for (int i = 0; i < 4; i++) if (!_lit[i]) return;
            _bankMult = Mathf.Min(4, _bankMult + 1);
            int got = Points(2500);
            Say("ALL STARS!  +" + got.ToString("N0") + "  MULTIPLIER x" + Mult, 2.5f);
            Play(Sounds.Jackpot, 0.8f);
            _bankResetAt = Time.time + 1.2f;
        }

        private void EnterHouse()
        {
            _state = State.Captured;
            _timer = 1.4f;
            _ball.gameObject.SetActive(false);
            _houseCount++;
            _houseFlash = 1.4f;
            int got = Points(5000);
            Play(Sounds.House, 0.9f);
            if (_houseCount == 3)
            {
                got += Points(25000);
                Say("HEE HEE!  SECRET: IT'S STILL HUNGRY  +" + got.ToString("N0"), 3.5f);
                Play(Sounds.Jackpot, 0.9f);
                Voice("tt_laugh");
            }
            else Say("THE HOUSE ATE IT!  +" + got.ToString("N0") + (_houseCount == 1 ? "\nIt's hungry..." : ""), 2.5f);
        }

        private void EnterRamp()
        {
            _state = State.Ramp;
            _rampT = 0f;
            _rampChain = Time.time - _lastRamp < 12f ? _rampChain + 1 : 1;
            _lastRamp = Time.time;
            int got = Points(1000 * _rampChain);
            Play(Sounds.Ramp, 0.7f);
            if (_rampChain == 3)
            {
                got += Points(10000);
                Say("KEEP IT MOVING!  +" + got.ToString("N0"), 3f);
                Play(Sounds.Jackpot, 0.8f);
            }
            else Say("RAMP!  +" + got.ToString("N0") + (_rampChain > 1 ? "  (x" + _rampChain + " COMBO)" : ""), 2f);
        }

        private void Drain()
        {
            _state = State.Drained;
            _timer = 1.8f;
            _ball.gameObject.SetActive(false);
            Play(Sounds.Drain, 0.8f);
            Say(_ballNo >= 3 ? "LAST BALL LOST" : "BALL LOST", 1.7f);
        }

        private void GameOver()
        {
            _state = State.GameOver;
            Daily.Add("pinball_games", 1);
            Daily.Best("pinball_score", _score);
            int pay = _score > 0 ? Mathf.Min(100, 5 + _score / 10000) : 0; // shards for every finished game
            if (pay > 0) { Shards.Add(pay); HubMode.SetHudText("+" + pay + " shards"); }
            if (_score > _high)
            {
                _high = _score;
                SaveHigh();
                Say("NEW HIGH SCORE!  " + _score.ToString("N0") + "\nPress SPACE to play again", 999f);
                Play(Sounds.Jackpot, 0.9f);
                Voice("tt_laugh");
            }
            else Say("GAME OVER  -  " + _score.ToString("N0") + "\nPress SPACE to play again", 999f);
        }

        private void SaveHigh()
        {
            if (_score > PlayerPrefs.GetInt(PREF_HIGH, 0)) { PlayerPrefs.SetInt(PREF_HIGH, _score); PlayerPrefs.Save(); }
            _high = Mathf.Max(_high, PlayerPrefs.GetInt(PREF_HIGH, 0));
        }

        private void LateUpdate()
        {
            if (!Playing || _cam == null) return;
            if (_fps != null && _fps.enabled) _fps.enabled = false; // (the pause menu can wake it)
            // Standing at the front of the machine, looking down the playfield.
            _cam.transform.position = transform.TransformPoint(new Vector3(0f, 1.62f, 1.02f));
            _cam.transform.rotation = Quaternion.LookRotation(_pf.TransformPoint(V(PinballTable.XC, 0.55f, 0f)) - _cam.transform.position, Vector3.up);
        }

        // ── What you see ─────────────────────────────────────────────────────
        private void Visuals(float dt)
        {
            _lFlip.localRotation = Quaternion.Euler(0f, -_t.LeftFlipper.Dir, 0f);
            _rFlip.localRotation = Quaternion.Euler(0f, -_t.RightFlipper.Dir, 0f);
            float tip = 0.016f - _t.Pull * 0.04f, back = -0.16f - _t.Pull * 0.04f;
            _rod.localPosition = V(PinballTable.LANE_X, (tip + back) * 0.5f, 0.012f);
            _rod.localScale = new Vector3(0.01f, (tip - back) * 0.5f, 0.01f);
            _knob.localPosition = V(PinballTable.LANE_X, back, 0.012f);

            if (_state == State.Ramp)
            {
                Vector3 r = PinballTable.RampPoint(_rampT);
                _ball.localPosition = V(r.x, r.y, r.z + PinballTable.BALL);
            }
            else _ball.localPosition = V(_t.Pos.x, _t.Pos.y, PinballTable.BALL);

            // Bumpers flash and their Tattletails hop when hit; with nobody playing, the lights wander (attract mode).
            for (int i = 0; i < 3; i++)
            {
                _bumperFlash[i] = Mathf.Max(0f, _bumperFlash[i] - dt * 4f);
                float idle = Playing || _trailer ? 0.35f : 0.25f + 0.25f * Mathf.Sin(Time.time * 2.2f + i * 2.1f);
                _bumperLights[i].intensity = idle + _bumperFlash[i] * 2.2f;
                _bumperTops[i].localPosition = new Vector3(0f, 0.039f + _bumperFlash[i] * 0.012f, 0f);
            }
            for (int i = 0; i < 4; i++)
            {
                bool on = Playing || _trailer ? _lit[i] : Mathf.Repeat(Time.time * 2f - i * 0.5f, 2f) < 1f;
                if (_bankResetAt > 0f) on = Mathf.Repeat(Time.time * 8f, 1f) < 0.5f;
                if (_targets[i] != null) _targets[i].sharedMaterial = on ? _targetOn : _targetOff;
            }
            _houseFlash = Mathf.Max(0f, _houseFlash - dt);
            bool blink = _houseFlash > 0f && Mathf.Repeat(Time.time * 6f, 1f) < 0.5f;
            foreach (var w in _windows) w.sharedMaterial = blink || (!Playing && !_trailer && Mathf.Repeat(Time.time * 0.5f, 1f) < 0.5f) ? _windowOn : _windowOff;
            _houseLight.intensity = 0.3f + (blink ? 1.6f : 0f);

            if (_ui != null)
            {
                _uiScore.text = "SCORE  " + _score.ToString("N0") + "      BALL " + Mathf.Min(_ballNo, 3) + " / 3      x" + Mult + "      HIGH  " + _high.ToString("N0");
                bool showMsg = Time.time < _msgUntil;
                _uiMsg.text = showMsg ? _msg : "";
                if (showMsg) _uiMsg.color = new Color(1f, 0.85f, 0.3f, Mathf.Clamp01((_msgUntil - Time.time) * 2f));
            }
        }

        private string _lastLed;
        private void UpdateLeds()
        {
            string mid = Playing || _trailer ? (Time.time < _msgUntil && _msg.Length > 0 ? _msg.Split('\n')[0] : "BALL " + Mathf.Min(_ballNo, 3) + "   x" + Mult) : "HOLD TO PLAY!";
            string key = _high + "|" + _score + "|" + mid;
            if (key == _lastLed) return;
            _lastLed = key;
            _ledLeft.text = "HIGH SCORE\n" + _high.ToString("N0");
            _ledRight.text = "YOUR SCORE\n" + _score.ToString("N0");
            _ledMid.text = mid;
        }

        private void BuildUI()
        {
            Font font = MainMenuButtonInjector.RealFont != null ? MainMenuButtonInjector.RealFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
            _ui = new GameObject("KE_PinballUI");
            var canvas = _ui.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            var scaler = _ui.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            _uiScore = Label(font, 34, new Vector2(0.45f, 0.955f), new Vector2(1500f, 60f), new Color(1f, 0.55f, 0.85f));
            _uiMsg = Label(font, 54, new Vector2(0.5f, 0.66f), new Vector2(1600f, 160f), new Color(1f, 0.85f, 0.3f));
            var hint = Label(font, 24, new Vector2(0.5f, 0.035f), new Vector2(1800f, 40f), new Color(0.85f, 0.8f, 0.95f, 0.8f));
            hint.text = "LEFT CLICK / A: left flipper      RIGHT CLICK / D: right flipper      hold SPACE, let go: launch";
            var leave = Label(font, 30, new Vector2(0.9f, 0.955f), new Vector2(400f, 50f), new Color(1f, 1f, 1f, 0.9f));
            leave.text = "Q or E: step away";
        }

        private Text Label(Font font, int size, Vector2 anchor, Vector2 box, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(_ui.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = box;
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = size; t.alignment = TextAnchor.MiddleCenter; t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false;
            var o = go.AddComponent<Outline>();
            o.effectColor = new Color(0.1f, 0f, 0.15f, 0.9f);
            o.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        // ── Sound ────────────────────────────────────────────────────────────
        private enum Sounds { Flip, Bump, Sling, Target, House, Ramp, Launch, Drain, Jackpot }
        private static AudioClip[] _clips;

        private void Play(Sounds which, float volume)
        {
            if (_clips == null) MakeClips();
            _audio.PlayOneShot(_clips[(int)which], volume);
        }

        private void Voice(string id)
        {
            try { if (AudioController.IsValidAudioID(id)) AudioController.Play(id, transform); }
            catch (System.Exception) { }
        }

        private static void MakeClips()
        {
            _clips = new AudioClip[9];
            _clips[(int)Sounds.Flip] = Tone("KE_PbFlip", 160f, 90f, 0.09f, 40f, 0.6f);
            _clips[(int)Sounds.Bump] = Tone("KE_PbBump", 520f, 380f, 0.22f, 16f, 0.25f);
            _clips[(int)Sounds.Sling] = Tone("KE_PbSling", 900f, 600f, 0.08f, 45f, 0.5f);
            _clips[(int)Sounds.Target] = Tone("KE_PbTarget", 1320f, 1320f, 0.25f, 12f, 0.05f);
            _clips[(int)Sounds.House] = Tone("KE_PbHouse", 340f, 110f, 0.45f, 5f, 0.1f);
            _clips[(int)Sounds.Ramp] = Tone("KE_PbRamp", 300f, 1200f, 0.8f, 1.5f, 0.15f);
            _clips[(int)Sounds.Launch] = Tone("KE_PbLaunch", 95f, 70f, 0.35f, 9f, 0.5f);
            _clips[(int)Sounds.Drain] = Tone("KE_PbDrain", 220f, 70f, 0.7f, 3f, 0.05f);
            _clips[(int)Sounds.Jackpot] = Arpeggio();
        }

        // A tone sliding from f0 to f1, decaying, with a noisy click at the start.
        internal static AudioClip Tone(string name, float f0, float f1, float len, float decay, float noise)
        {
            const int SR = 22050;
            int n = (int)(len * SR);
            var d = new float[n];
            var r = new System.Random(name.Length * 31);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float f = Mathf.Lerp(f0, f1, t / len);
                phase += 2 * System.Math.PI * f / SR;
                float env = Mathf.Exp(-t * decay) * Mathf.Min(1f, t * 400f) * Mathf.Min(1f, (len - t) * 60f);
                float s = (float)(System.Math.Sin(phase) + 0.3 * System.Math.Sin(2 * phase));
                d[i] = (s * 0.5f + ((float)r.NextDouble() * 2f - 1f) * noise * Mathf.Exp(-t * 60f)) * env * 0.7f;
            }
            var clip = AudioClip.Create(name, n, 1, SR, false);
            clip.SetData(d, 0);
            return clip;
        }

        // Four rising notes: a jackpot.
        internal static AudioClip Arpeggio()
        {
            const int SR = 22050;
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            const float NOTE = 0.11f;
            int n = (int)((notes.Length * NOTE + 0.3f) * SR);
            var d = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                int start = (int)(k * NOTE * SR);
                for (int i = start; i < n; i++)
                {
                    float t = (i - start) / (float)SR;
                    float env = Mathf.Exp(-t * 7f) * Mathf.Min(1f, t * 300f);
                    d[i] += (Mathf.Sin(2f * Mathf.PI * notes[k] * t) + 0.25f * Mathf.Sin(4f * Mathf.PI * notes[k] * t)) * env * 0.25f;
                }
            }
            var clip = AudioClip.Create("KE_PbJackpot", n, 1, SR, false);
            clip.SetData(d, 0);
            return clip;
        }
    }
}
