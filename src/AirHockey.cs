using Steamworks;
using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// TATTLETAIL AIR HOCKEY: a real 3D table in the Arcade, 1v1, first to 7. Walk up to an end and hold to play:
    /// your view drops behind your mallet and the mouse (or WASD) moves it. Alone, a Tattletail AI takes the other
    /// end (it gets a little sharper when you're winning). With your friend in the lobby, whoever sits at the other
    /// end plays you for real: the lobby host runs the puck, the guest sends where their mallet is.
    /// Physics: AirHockeyTable. Art: tools/airhockey_art.py (after the user's concept).
    /// </summary>
    public class AirHockeyGame : MonoBehaviour
    {
        public static AirHockeyGame Instance { get; private set; }
        public static bool Playing { get; private set; }
        private static float _leftAt = -10f;
        public static bool BlocksWorld { get { return Playing || Time.unscaledTime < _leftAt + 0.6f; } }

        private const float SH = 0.78f;          // the surface's height
        private const int WIN = 7;
        private static readonly Color RED = new Color(1f, 0.24f, 0.28f), BLUE = new Color(0.3f, 0.55f, 1f);
        private static readonly float L = AirHockeyTable.L, W = AirHockeyTable.W;

        private readonly AirHockeyTable _t = new AirHockeyTable();
        private readonly Transform[] _mallets = new Transform[2];
        private Transform _puck, _topper;
        private readonly Text[] _boardScore = new Text[4]; // [face*2 + seat]
        private readonly Text[] _boardName = new Text[4];

        private enum Phase { Play, Goal, Over }
        private Phase _phase = Phase.Over;
        private readonly int[] _score = new int[2];
        private float _phaseT, _clock;
        private int _seat = -1;                  // this player's seat (0: the +x end, red; 1: the -x end, blue)
        private string _msg = "";
        private float _msgUntil;

        private FirstPersonController _fps;
        private Camera _cam;
        private Vector3 _camLocalPos;
        private Quaternion _camLocalRot;
        private Interactable _prompt;
        private GameObject _ui;
        private Text _uiScore, _uiMsg;
        private AudioSource _audio, _air;
        private static AudioClip _sHit, _sRail, _sGoal, _sWin, _sAir;

        // ── Online ───────────────────────────────────────────────────────────
        private const int OP_SEAT = 0, OP_MALLET = 1, OP_STATE = 2, OP_EVENT = 3;
        private const int EV_START = 0, EV_GOAL = 1, EV_WIN = 2;
        /// <summary>Which end your friend is playing at (-1: they aren't).</summary>
        public static int RemoteSeat = -1;
        private static Vector2 _remoteMallet;
        private float _sendT;
        private bool _wasVersus;
        private bool _trailer;       // the trailer is filming: the Tattletails play for real
        private float _staged;

        // ── For the trailer ──────────────────────────────────────────────────
        /// <summary>The two Tattletails play a fast rally, 4 - 4 on the board, until stopped.</summary>
        public void TrailerRally(bool on)
        {
            _trailer = on;
            _score[0] = _score[1] = on ? 4 : 0;
            _t.Reset(0);
            _t.PuckVel = new Vector2(-2.5f, 1.2f);
            UpdateBoard();
        }

        /// <summary>Red scores: a hard shot into the blue goal (the blue Tattletail caught out of position).</summary>
        public void TrailerGoal()
        {
            _t.Puck = new Vector2(0.25f, 0.04f);
            _t.PuckVel = new Vector2(-4.6f, -0.05f);
            _t.Mallet[1] = _t.Target[1] = new Vector2(-0.72f, 0.36f);
            _staged = Time.time + 0.7f;
        }

        private static bool FriendHere { get { return VersusNet.Online && VersusNet.RemoteFresh && VersusNet.RemoteCtx == VersusNet.CTX_ARCADE && MpLobby.Instance != null; } }
        private static bool IsHost { get { return MpLobby.Instance == null || MpLobby.Instance.IsHost; } }
        /// <summary>You and your friend at opposite ends: a real match.</summary>
        private bool Versus { get { return Playing && FriendHere && RemoteSeat >= 0 && RemoteSeat != _seat; } }
        private int Other { get { return 1 - _seat; } }

        // ── Building ─────────────────────────────────────────────────────────
        public static void Build(Transform room, Vector3 pos, float yaw)
        {
            var root = new GameObject("AirHockeyTable");
            root.transform.SetParent(room, false);
            root.transform.localPosition = pos;
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var game = root.AddComponent<AirHockeyGame>();
            game.BuildTable();
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.42f, 0f);
            col.size = new Vector3(L + 0.2f, 0.84f, W + 0.2f);
            game._prompt = PartyGameRunner.MakeInteractable(root, "Play: TATTLETAIL AIR HOCKEY", 0.4f);
            HubLayout.Register(root, HubMachine.AirHockey);
        }

        // Table space (x along, y across, h up) -> the table's local space.
        private static Vector3 P(float x, float y, float h) { return new Vector3(x, h, y); }

        private static GameObject B(Transform parent, string name, Vector3 center, Vector3 size, Material m)
        {
            var go = Mat.Box(parent, name, center, size, m);
            Destroy(go.GetComponent<Collider>());
            return go;
        }

        private static GameObject Cyl(Transform parent, string name, Vector3 pos, float radius, float height, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            go.GetComponent<Renderer>().material = m;
            Destroy(go.GetComponent<Collider>());
            return go;
        }

        private void BuildTable()
        {
            Transform t = transform;
            Material body = Mat.Lit(new Color(0.2f, 0.08f, 0.3f), 0.45f);
            Material rail = Mat.Lit(new Color(0.3f, 0.12f, 0.45f), 0.7f);
            Material dark = Mat.Lit(new Color(0.03f, 0.02f, 0.05f), 0.3f);
            Material pink = Mat.Unlit(new Color(1f, 0.35f, 0.85f)), red = Mat.Unlit(RED), blue = Mat.Unlit(BLUE);
            float rw = 0.08f, sideZ = W / 2f + rw / 2f, endX = L / 2f + rw / 2f;

            // The cabinet, its art down both long sides, a darker foot.
            B(t, "Body", P(0f, 0f, 0.445f), new Vector3(L + 2f * rw, 0.65f, W + 2f * rw), body); // (its top 1 cm under the surface: no flicker)
            B(t, "Foot", P(0f, 0f, 0.06f), new Vector3(L, 0.12f, W - 0.1f), dark);
            PinballGame.ArtQuad(t, "Side art", P(0f, W / 2f + rw + 0.002f, 0.47f), Quaternion.Euler(0f, 180f, 0f), L + 2f * rw, 0.5f, "airhockey_side.jpg");
            PinballGame.ArtQuad(t, "Side art", P(0f, -W / 2f - rw - 0.002f, 0.47f), Quaternion.identity, L + 2f * rw, 0.5f, "airhockey_side.jpg");
            foreach (float sx in new[] { -1f, 1f })
                B(t, "Under-glow", P(sx * (L / 2f + rw + 0.003f), 0f, 0.2f), new Vector3(0.006f, 0.03f, W), sx > 0f ? red : blue);

            // The surface.
            PinballGame.ArtQuad(t, "Surface", P(0f, 0f, SH), Quaternion.Euler(90f, 0f, 0f), L, W, "airhockey_surface.jpg");

            // Rails: pink down the sides, red at the +x end, blue at the -x end, each end split by its goal.
            foreach (float sy in new[] { -1f, 1f })
            {
                B(t, "Rail", P(0f, sy * sideZ, SH + 0.025f), new Vector3(L + 2f * rw, 0.05f, rw), rail);
                B(t, "Rail neon", P(0f, sy * (sideZ - 0.02f), SH + 0.052f), new Vector3(L + 2f * rw, 0.004f, 0.02f), pink);
            }
            float endLen = W / 2f + rw - AirHockeyTable.GOAL_HALF;
            foreach (float sx in new[] { -1f, 1f })
            {
                Material neon = sx > 0f ? red : blue;
                foreach (float sy in new[] { -1f, 1f })
                {
                    float cy = sy * (AirHockeyTable.GOAL_HALF + endLen / 2f);
                    B(t, "End rail", P(sx * endX, cy, SH + 0.025f), new Vector3(rw, 0.05f, endLen), rail);
                    B(t, "End neon", P(sx * (endX - 0.02f), cy, SH + 0.052f), new Vector3(0.02f, 0.004f, endLen), neon);
                    // The cut corner.
                    var c = B(t, "Corner", P(sx * (L / 2f - AirHockeyTable.CORNER / 2f + 0.01f), sy * (W / 2f - AirHockeyTable.CORNER / 2f + 0.01f), SH + 0.025f),
                        new Vector3(AirHockeyTable.CORNER * 1.41f, 0.05f, 0.03f), rail);
                    c.transform.localRotation = Quaternion.Euler(0f, sx * sy > 0f ? -45f : 45f, 0f);
                }
                B(t, "Goal", P(sx * endX, 0f, SH - 0.005f), new Vector3(rw + 0.002f, 0.04f, AirHockeyTable.GOAL_HALF * 2f), Mat.Unlit(new Color(0.01f, 0f, 0.02f)));
            }

            // The arch over the middle, with the scoreboard (readable from both ends) and a Tattletail on top.
            float archY = W / 2f + rw + 0.06f;
            foreach (float sy in new[] { -1f, 1f })
            {
                var post = Mat.Box(t, "Arch post", P(0f, sy * archY, 1.0f), new Vector3(0.08f, 2.0f, 0.08f), body); // (solid: you walk around it)
                post.GetComponent<Renderer>().material = body;
                foreach (float sx in new[] { -1f, 1f })
                    B(t, "Arch neon", P(sx * 0.041f, sy * archY, 1.0f), new Vector3(0.004f, 2.0f, 0.02f), sx > 0f ? red : blue);
            }
            B(t, "Arch top", P(0f, 0f, 2.02f), new Vector3(0.12f, 0.08f, 2f * archY + 0.08f), body);
            B(t, "Scoreboard", P(0f, 0f, 1.78f), new Vector3(0.12f, 0.36f, 0.9f), dark);
            for (int face = 0; face < 2; face++)
            {
                float fx = face == 0 ? 0.062f : -0.062f, yaw = face == 0 ? 270f : 90f; // (world text reads from behind: face it out)
                for (int s = 0; s < 2; s++)
                {
                    float y = s == 0 ? -0.22f : 0.22f;
                    _boardScore[face * 2 + s] = BoardText(P(fx, y, 1.8f), yaw, s == 0 ? RED : BLUE, 0.16f);
                    _boardName[face * 2 + s] = BoardText(P(fx, y, 1.66f), yaw, s == 0 ? RED : BLUE, 0.04f);
                }
            }
            _topper = new GameObject("Topper").transform;
            _topper.SetParent(t, false);
            _topper.localPosition = P(0f, 0f, 2.06f);

            // Light over the table.
            var lgo = new GameObject("Table light");
            lgo.transform.SetParent(t, false);
            lgo.transform.localPosition = P(0f, 0f, 1.5f);
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(0.8f, 0.65f, 1f); l.intensity = 1.2f; l.range = 2.4f;
            l.shadows = LightShadows.None; l.renderMode = LightRenderMode.ForcePixel;

            // Mallets and the puck (it glows).
            for (int s = 0; s < 2; s++)
            {
                var m = new GameObject(s == 0 ? "Red mallet" : "Blue mallet").transform;
                m.SetParent(t, false);
                Color c = s == 0 ? RED : BLUE;
                Cyl(m, "Base", new Vector3(0f, 0.01f, 0f), AirHockeyTable.MALLET, 0.02f, Mat.Lit(c, 0.6f));
                Cyl(m, "Ring", new Vector3(0f, 0.021f, 0f), AirHockeyTable.MALLET * 0.75f, 0.004f, Mat.Unlit(Color.Lerp(c, Color.white, 0.4f)));
                Cyl(m, "Handle", new Vector3(0f, 0.045f, 0f), 0.018f, 0.05f, Mat.Lit(c * 0.8f, 0.5f));
                var knob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                knob.transform.SetParent(m, false);
                knob.transform.localPosition = new Vector3(0f, 0.075f, 0f);
                knob.transform.localScale = Vector3.one * 0.045f;
                knob.GetComponent<Renderer>().material = Mat.Lit(c, 0.7f);
                Destroy(knob.GetComponent<Collider>());
                _mallets[s] = m;
            }
            _puck = new GameObject("Puck").transform;
            _puck.SetParent(t, false);
            Cyl(_puck, "Disc", new Vector3(0f, 0.006f, 0f), AirHockeyTable.PUCK, 0.012f, Mat.Unlit(new Color(0.45f, 0.9f, 1f)));
            var pl = new GameObject("Puck glow").AddComponent<Light>();
            pl.transform.SetParent(_puck, false);
            pl.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            pl.type = LightType.Point; pl.color = new Color(0.4f, 0.85f, 1f); pl.intensity = 1.2f; pl.range = 0.35f;
            pl.shadows = LightShadows.None; pl.renderMode = LightRenderMode.ForcePixel;

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 0f; _audio.playOnAwake = false; _audio.volume = 0.7f;
            _air = gameObject.AddComponent<AudioSource>();
            _air.spatialBlend = 0.6f; _air.loop = true; _air.playOnAwake = false; _air.volume = 0.1f; _air.maxDistance = 6f;
            UpdateBoard();
        }

        private Text BoardText(Vector3 pos, float yaw, Color c, float size)
        {
            var txt = WorldText.Create(transform, "", c, size, TextAnchor.MiddleCenter, 0.4f);
            txt.canvas.transform.localPosition = pos;
            txt.canvas.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return txt;
        }

        private void Awake() { Instance = this; }

        private void Start()
        {
            // The Tattletail on top of the arch (the real model if there's one to copy).
            GameObject fig = null;
            try
            {
                string label;
                fig = PartyTattletailModels.BuildColored(_topper.position, Quaternion.LookRotation(-transform.right, Vector3.up), _topper, 0, out label);
            }
            catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [AirHockey] No Tattletail for the arch: " + e.Message); }
            if (fig != null) PinballGame.FitOnto(fig, _topper, 0.3f);
            else PinballGame.FallbackTattletail(_topper, new Color(0.55f, 0.33f, 0.78f), 4f);
            Place();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (Playing) Leave();
        }

        // ── Playing ──────────────────────────────────────────────────────────
        /// <summary>Takes the nearer end (the other one if your friend's there) and starts a game.</summary>
        public void Enter()
        {
            if (Playing) return;
            _fps = RM.fpsController;
            _cam = _fps != null ? _fps.GetComponentInChildren<Camera>() : null;
            if (_cam == null) _cam = Camera.main;
            if (_fps == null || _cam == null) return;
            Vector3 me = transform.InverseTransformPoint(_fps.transform.position);
            _seat = me.x >= 0f ? 0 : 1;
            if (FriendHere && RemoteSeat == _seat) _seat = 1 - _seat;
            _camLocalPos = _cam.transform.localPosition;
            _camLocalRot = _cam.transform.localRotation;
            // Stand at your end (so your friend sees you there), facing the table.
            float s = _seat == 0 ? 1f : -1f;
            _fps.TeleportPlayer(transform.TransformPoint(P(s * (L / 2f + 0.6f), 0f, 0.05f)), new Vector3(0f, transform.eulerAngles.y + (_seat == 0 ? 270f : 90f), 0f)); // (clear of the table's edge)
            _fps.enabled = false;
            Playing = true;
            ShowPrompt(false);
            HubMode.SetHudText("");
            BuildUI();
            if (_air.clip == null) _air.clip = AirClip();
            _air.Play();
            SendSeat();
            _wasVersus = Versus;
            if (_wasVersus) { if (IsHost) StartMatch(true); else Say("Playing your friend!", 2.5f); }
            else StartMatch(false);
            if (_topper.childCount > 0) _topper.GetChild(0).rotation = Quaternion.LookRotation(-transform.right * s, Vector3.up); // (Tattletails face their -z: turn it to look at you)
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [AirHockey] Playing at seat " + _seat + (_wasVersus ? " (vs friend)" : " (vs Tattletail)"));
        }

        private void Leave()
        {
            if (!Playing) return;
            Playing = false;
            _leftAt = Time.unscaledTime;
            _seat = -1;
            SendSeat();
            if (_cam != null) { _cam.transform.localPosition = _camLocalPos; _cam.transform.localRotation = _camLocalRot; }
            if (_fps != null) _fps.enabled = true;
            if (_ui != null) Destroy(_ui);
            _air.Stop();
            ShowPrompt(true);
            _phase = Phase.Over;
            _t.Reset(0);
            UpdateBoard();
        }

        private void ShowPrompt(bool on)
        {
            if (_prompt == null) return;
            _prompt.conditions.newStateOnConditionsMet = on ? Interactable.State.Enabled : Interactable.State.Hidden;
            if (on) _prompt.SetStateEnabled(); else _prompt.SetStateHidden();
        }

        private void StartMatch(bool announce)
        {
            _score[0] = _score[1] = 0;
            _phase = Phase.Play;
            _clock = 0f;
            _t.Reset(Random.Range(0, 2));
            Say(Versus ? "YOU vs YOUR FRIEND\nFirst to " + WIN + "!" : "YOU vs TATTLETAIL\nFirst to " + WIN + "!", 2.5f);
            if (announce) SendEvent(EV_START, 0);
            UpdateBoard();
        }

        private void Say(string text, float seconds) { _msg = text; _msgUntil = Time.time + seconds; }

        private string Name(int seat)
        {
            if (seat == _seat) return "YOU";
            if (Playing && Versus) return "FRIEND";
            return "TATTLETAIL";
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            _clock += dt;

            if (Playing)
            {
                if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.E)) { Leave(); return; }
                // Your friend sat down or got up: the match changes hands.
                bool versus = Versus;
                if (versus != _wasVersus)
                {
                    _wasVersus = versus;
                    if (versus) { if (IsHost) StartMatch(true); else Say("Your friend sat down!", 2.5f); }
                    else Say("Your friend stepped away.\nTattletail takes over!", 3f);
                }
                MoveMyMallet(dt);
            }

            bool hostRuns = !Versus || IsHost; // the one who runs the puck
            if (Playing && !Versus) _t.Target[Other] = _t.AiTarget(Other, _clock, Mathf.Clamp(1.8f + 0.15f * (_score[_seat] - _score[Other]), 1.4f, 2.6f));
            if (Versus) { _t.Target[RemoteSeat] = _remoteMallet; _t.MaxSpeed[RemoteSeat] = 6f; }
            if (!Playing) // (nobody playing: the Tattletails play each other, slowly; for the trailer, for real)
            {
                float sp = _trailer ? 2.4f : 1.2f;
                _t.Target[0] = _t.AiTarget(0, _clock, sp);
                if (Time.time > _staged) _t.Target[1] = _t.AiTarget(1, _clock + 5f, sp);
            }

            _t.Tick(dt, _phase == Phase.Play || !Playing);
            if (_t.PuckHit) { Play(_sHit, 0.8f); _t.PuckHit = false; }
            if (_t.RailHit) { Play(_sRail, 0.5f); _t.RailHit = false; }

            if (hostRuns) RunRules(dt);
            if (Playing && FriendHere && RemoteSeat >= 0) Send(dt);
            Place();
            if (_ui != null) UpdateUI();
        }

        private void RunRules(float dt)
        {
            int conceded = _t.Conceded;
            if (!Playing)
            {
                if (_trailer && conceded >= 0) { _score[1 - conceded]++; UpdateBoard(); Play(_sGoal, 0.9f); }
                if (conceded >= 0 || _t.PuckVel.magnitude < 0.02f && _clock % 8f < dt) _t.Reset(Random.Range(0, 2));
                return;
            }
            switch (_phase)
            {
                case Phase.Play:
                    if (conceded >= 0) Goal(1 - conceded);
                    break;
                case Phase.Goal:
                    _phaseT -= dt;
                    if (_phaseT <= 0f) { _phase = Phase.Play; _t.Reset(_lastConceded); }
                    break;
                case Phase.Over:
                    if (Input.GetKeyDown(KeyCode.Space) && (!Versus || IsHost)) StartMatch(Versus);
                    break;
            }
        }

        private int _lastConceded;
        private void Goal(int scorer)
        {
            _score[scorer]++;
            _lastConceded = 1 - scorer;
            if (_score[scorer] >= WIN)
            {
                _phase = Phase.Over;
                Won(scorer);
                if (Versus) SendEvent(EV_WIN, scorer);
            }
            else
            {
                _phase = Phase.Goal;
                _phaseT = 1.3f;
                Scored(scorer);
                if (Versus) SendEvent(EV_GOAL, scorer);
            }
            _t.Puck = new Vector2(100f, 100f); // (off the table until the next serve)
            _t.PuckVel = Vector2.zero;
            UpdateBoard();
        }

        private void Scored(int scorer)
        {
            Play(_sGoal, 0.9f);
            if (scorer == _seat) Say("GOAL!  " + _score[_seat] + " - " + _score[Other], 1.5f);
            else Say((Versus ? "YOUR FRIEND SCORES" : "TATTLETAIL SCORES") + "  " + _score[_seat] + " - " + _score[Other], 1.5f);
            if (!Versus && scorer != _seat && Random.value < 0.4f) Voice("tt_laugh");
        }

        private void Won(int winner)
        {
            if (winner == _seat) { Daily.Add("hockey_wins", 1); if (_score[Other] == 0) Daily.Add("shutout", 1); }
            int pay = winner == _seat ? 40 : 10; // shards for every finished match, more for the win
            Shards.Add(pay); HubMode.SetHudText("+" + pay + " shards");
            Play(_sWin, 0.9f);
            string end = (!Versus || IsHost) ? "\nPress SPACE for a rematch" : "\nWaiting for a rematch...";
            if (winner == _seat) { Say("YOU WIN!  " + _score[_seat] + " - " + _score[Other] + end, 999f); if (!Versus) Voice("tt_k_cry"); }
            else { Say((Versus ? "YOUR FRIEND WINS  " : "TATTLETAIL WINS!  ") + _score[_seat] + " - " + _score[Other] + end, 999f); if (!Versus) Voice("tt_laugh"); }
        }

        // The mouse (or WASD) moves your mallet: forward is toward the other end, whichever end you're at.
        private void MoveMyMallet(float dt)
        {
            float s = _seat == 0 ? 1f : -1f;
            float fwd = Input.GetAxisRaw("Mouse Y") * 0.012f, side = Input.GetAxisRaw("Mouse X") * 0.012f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) fwd += 2.5f * dt;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) fwd -= 2.5f * dt;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) side += 2.5f * dt;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) side -= 2.5f * dt;
            Vector2 target = _t.Target[_seat] + new Vector2(-s * fwd, s * side);
            _t.Target[_seat] = AirHockeyTable.Clamp(_seat, target);
            _t.MaxSpeed[_seat] = 6f;
        }

        private void LateUpdate()
        {
            if (!Playing || _cam == null) return;
            if (_fps != null && _fps.enabled) _fps.enabled = false; // (the pause menu can wake it)
            float s = _seat == 0 ? 1f : -1f;
            _cam.transform.position = transform.TransformPoint(P(s * (L / 2f + 0.5f), 0f, 1.45f));
            _cam.transform.rotation = Quaternion.LookRotation(transform.TransformPoint(P(s * 0.1f, 0f, SH)) - _cam.transform.position, Vector3.up);
        }

        private void Place()
        {
            for (int s = 0; s < 2; s++) _mallets[s].localPosition = P(_t.Mallet[s].x, _t.Mallet[s].y, SH);
            bool onTable = Mathf.Abs(_t.Puck.x) < L / 2f + 0.2f;
            _puck.gameObject.SetActive(onTable);
            if (onTable) _puck.localPosition = P(_t.Puck.x, _t.Puck.y, SH);
        }

        private string _boardKey;
        private void UpdateBoard()
        {
            string key = _score[0] + "|" + _score[1] + "|" + _seat + "|" + Playing + "|" + Versus + "|" + _trailer;
            if (key == _boardKey) return;
            _boardKey = key;
            for (int face = 0; face < 2; face++)
                for (int s = 0; s < 2; s++)
                {
                    _boardScore[face * 2 + s].text = Playing || _trailer ? _score[s].ToString("00") : "--";
                    _boardName[face * 2 + s].text = Playing ? Name(s) : (s == 0 ? "RED" : "BLUE");
                }
        }

        private void UpdateUI()
        {
            UpdateBoard();
            _uiScore.text = "YOU  " + _score[_seat] + "   -   " + _score[Other] + "  " + Name(Other) + "        first to " + WIN;
            bool show = Time.time < _msgUntil;
            _uiMsg.text = show ? _msg : "";
            if (show) _uiMsg.color = new Color(1f, 0.85f, 0.3f, Mathf.Clamp01((_msgUntil - Time.time) * 2f));
        }

        private void BuildUI()
        {
            Font font = MainMenuButtonInjector.RealFont != null ? MainMenuButtonInjector.RealFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
            _ui = new GameObject("KE_AirHockeyUI");
            var canvas = _ui.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            var scaler = _ui.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            _uiScore = Label(font, 36, new Vector2(0.45f, 0.955f), new Vector2(1500f, 60f), _seat == 0 ? RED : BLUE);
            _uiMsg = Label(font, 56, new Vector2(0.5f, 0.66f), new Vector2(1600f, 180f), new Color(1f, 0.85f, 0.3f));
            Label(font, 24, new Vector2(0.5f, 0.035f), new Vector2(1800f, 40f), new Color(0.85f, 0.8f, 0.95f, 0.8f)).text = "MOUSE (or WASD): move your mallet      keep it out of your goal, get it into theirs";
            Label(font, 30, new Vector2(0.9f, 0.955f), new Vector2(400f, 50f), new Color(1f, 1f, 1f, 0.9f)).text = "Q or E: step away";
        }

        private Text Label(Font font, int size, Vector2 anchor, Vector2 box, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(_ui.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = box;
            var txt = go.GetComponent<Text>();
            txt.font = font; txt.fontSize = size; txt.alignment = TextAnchor.MiddleCenter; txt.color = color;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow; txt.verticalOverflow = VerticalWrapMode.Overflow; txt.raycastTarget = false;
            var o = go.AddComponent<Outline>();
            o.effectColor = new Color(0.1f, 0f, 0.15f, 0.9f);
            o.effectDistance = new Vector2(2f, -2f);
            return txt;
        }

        // ── Online: seats, mallets, the puck ─────────────────────────────────
        private void SendSeat()
        {
            if (MpLobby.Instance == null || !MpLobby.Instance.LobbyExists) return;
            using (var p = new MpPacket(MpNetworking.PacketId.AirHockey))
            {
                p.Write(OP_SEAT); p.Write(_seat);
                MpNetworking.SendReliable(MpLobby.Instance.PeerSteamID, p);
            }
        }

        private void SendEvent(int kind, int arg)
        {
            if (MpLobby.Instance == null || !MpLobby.Instance.LobbyExists) return;
            using (var p = new MpPacket(MpNetworking.PacketId.AirHockey))
            {
                p.Write(OP_EVENT); p.Write(kind); p.Write(arg); p.Write(_score[0]); p.Write(_score[1]);
                MpNetworking.SendReliable(MpLobby.Instance.PeerSteamID, p);
            }
        }

        // 30 times a second: the host sends the puck, its mallet and the score; the guest sends its mallet.
        private void Send(float dt)
        {
            _sendT += dt;
            if (_sendT < 1f / 30f) return;
            _sendT = 0f;
            CSteamID peer = MpLobby.Instance.PeerSteamID;
            using (var p = new MpPacket(MpNetworking.PacketId.AirHockey))
            {
                if (IsHost)
                {
                    p.Write(OP_STATE);
                    p.Write(_t.Puck.x); p.Write(_t.Puck.y); p.Write(_t.PuckVel.x); p.Write(_t.PuckVel.y);
                    p.Write(_seat); p.Write(_t.Mallet[_seat].x); p.Write(_t.Mallet[_seat].y);
                    p.Write(_score[0]); p.Write(_score[1]); p.Write((int)_phase);
                }
                else
                {
                    p.Write(OP_MALLET); p.Write(_t.Mallet[_seat].x); p.Write(_t.Mallet[_seat].y);
                }
                MpNetworking.SendUnreliable(peer, p);
            }
        }

        /// <summary>MpNetworking hands us every Air Hockey packet.</summary>
        public static void OnPacket(MpPacket p)
        {
            int op = p.ReadInt();
            var g = Instance;
            switch (op)
            {
                case OP_SEAT:
                    RemoteSeat = p.ReadInt();
                    break;
                case OP_MALLET:
                    _remoteMallet = new Vector2(p.ReadFloat(), p.ReadFloat());
                    break;
                case OP_STATE:
                {
                    Vector2 puck = new Vector2(p.ReadFloat(), p.ReadFloat()), vel = new Vector2(p.ReadFloat(), p.ReadFloat());
                    int seat = p.ReadInt();
                    Vector2 mallet = new Vector2(p.ReadFloat(), p.ReadFloat());
                    int s0 = p.ReadInt(), s1 = p.ReadInt(), phase = p.ReadInt();
                    if (seat >= 0 && seat <= 1) { RemoteSeat = seat; _remoteMallet = mallet; }
                    if (g == null || !g.Versus || IsHost) return;
                    // The host's puck is the real one: close enough, ease toward it; far off, jump.
                    g._t.Puck = (puck - g._t.Puck).magnitude > 0.15f ? puck : Vector2.Lerp(g._t.Puck, puck, 0.4f);
                    g._t.PuckVel = vel;
                    g._score[0] = s0; g._score[1] = s1;
                    g._phase = (Phase)phase;
                    break;
                }
                case OP_EVENT:
                {
                    int kind = p.ReadInt(), arg = p.ReadInt(), s0 = p.ReadInt(), s1 = p.ReadInt();
                    if (g == null || !Playing) return;
                    g._score[0] = s0; g._score[1] = s1;
                    if (kind == EV_START) { g._phase = Phase.Play; g.Say("YOU vs YOUR FRIEND\nFirst to " + WIN + "!", 2.5f); }
                    else if (kind == EV_GOAL) g.Scored(arg);
                    else if (kind == EV_WIN) { g._phase = Phase.Over; g.Won(arg); }
                    break;
                }
            }
        }

        // ── Sound ────────────────────────────────────────────────────────────
        private void Play(AudioClip c, float volume)
        {
            if (_sHit == null)
            {
                _sHit = PinballGame.Tone("KE_AhHit", 700f, 520f, 0.07f, 55f, 0.7f);
                _sRail = PinballGame.Tone("KE_AhRail", 420f, 300f, 0.08f, 45f, 0.5f);
                _sGoal = PinballGame.Tone("KE_AhGoal", 180f, 170f, 0.6f, 3f, 0.2f);
                _sWin = PinballGame.Arpeggio();
            }
            if (c == null) c = _sHit;
            if (Playing || _trailer) _audio.PlayOneShot(c, volume);
        }

        private void Voice(string id)
        {
            try { if (AudioController.IsValidAudioID(id)) AudioController.Play(id, transform); }
            catch (System.Exception) { }
        }

        // The table's blower: soft, steady air.
        private static AudioClip AirClip()
        {
            if (_sAir != null) return _sAir;
            const int SR = 22050, N = SR * 2;
            var d = new float[N];
            var r = new System.Random(11);
            float lp = 0f;
            for (int i = 0; i < N; i++)
            {
                lp += (((float)r.NextDouble() * 2f - 1f) - lp) * 0.08f;
                d[i] = lp * 0.9f;
            }
            for (int i = 0; i < 2000; i++) { float k = i / 2000f; d[i] = d[i] * k + d[N - 2000 + i] * (1f - k); } // (a seamless loop)
            _sAir = AudioClip.Create("KE_AhAir", N - 2000, 1, SR, false);
            var loop = new float[N - 2000];
            System.Array.Copy(d, loop, loop.Length);
            _sAir.SetData(loop, 0);
            return _sAir;
        }
    }
}
