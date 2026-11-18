using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    public enum PartyGameType { FacePaint, RockPaperScissors, Balloon }

    /// <summary>
    /// The 7 hand-marked party tattletails (each a random-colored clone from
    /// PartyTattletailModels), each hosting one of 3 minigames, randomly
    /// assigned: Face Paint (hold interact repeatedly to dab on 4 colors),
    /// Rock-Paper-Scissors (pick a throw prop, tattletail throws random) and
    /// Balloon (interact once, wait for it to inflate — automatic). Winning
    /// any of them pays the same +20% Entertainment as a piñata.
    ///
    /// Bouncy castle: every 30s an idle tattletail may ask for it. Pick it up,
    /// click the castle to put it on, and it ragdolls around inside (only
    /// tattletails bounce, never the player). Entertainment -10% every 10s
    /// while one wants in and isn't on yet, +10% every 10s while it's happy,
    /// and -10% every 10s once it wants off (after 30s) and is still there.
    /// Clicking anywhere on the castle takes it off.
    /// </summary>
    public class PartyTattletailController : MonoBehaviour
    {
        public static PartyTattletailController Instance { get; private set; }

        private static readonly Vector3[] Marks =
        {
            new Vector3(3.4f, 1.1f, -1.9f), new Vector3(6.6f, 1.1f, -2.0f), new Vector3(4.7f, 1.1f, 6.6f),
            new Vector3(-0.1f, 1.1f, 3.1f), new Vector3(0.2f, 0.6f, 11.8f), new Vector3(1.2f, -2.0f, -0.5f),
            new Vector3(-0.5f, -2.0f, -1.9f),
        };

        private const float GAME_ENTERTAINMENT = 20f; // same payout as a normal piñata
        private const float COOLDOWN = 60f; // at least 1 minute before a tattletail offers its game again
        private const int PAINT_DABS_NEEDED = 4;
        private const float BALLOON_INFLATE_TIME = 4f;

        private enum State { Idle, Active, Cooldown }

        private class NPC
        {
            public GameObject GO;
            public Interactable It;
            public PartyGameType Type;
            public State State = State.Idle;
            public float Timer;
            public Vector3 OriginalPos;
            public bool WantsCastle; // asking to be carried to the bouncy castle

            // Face Paint
            public int PaintDabs;
            public readonly List<GameObject> PaintMarks = new List<GameObject>();

            // Rock-Paper-Scissors
            public readonly List<GameObject> RpsButtons = new List<GameObject>();

            // Balloon
            public GameObject Balloon;
        }

        private static readonly Color[] PaintColors =
        {
            new Color(0.85f, 0.2f, 0.25f), new Color(0.2f, 0.5f, 0.85f), new Color(0.9f, 0.8f, 0.15f), new Color(0.25f, 0.75f, 0.35f),
        };
        private static readonly Color[] BalloonColors =
        {
            new Color(0.8f, 0.2f, 0.3f), new Color(0.25f, 0.5f, 0.85f), new Color(0.9f, 0.75f, 0.15f), new Color(0.3f, 0.75f, 0.4f), new Color(0.75f, 0.35f, 0.8f),
        };

        private readonly List<NPC> _npcs = new List<NPC>();
        private Transform _root;

        // ── the bouncy castle ─────────────────────────────────────────────────
        private const float CASTLE_REQUEST_INTERVAL = 30f;  // every 30s...
        private const float CASTLE_REQUEST_CHANCE = 0.2f;   // ...20% chance an idle tattletail asks for the castle
        private const float CASTLE_WANTS_OFF_AT = 30f;      // after this long bouncing, it starts wanting off
        private const float CASTLE_TICK_INTERVAL = 10f;      // every 10s...
        private const float CASTLE_REWARD_AMOUNT = 10f;      // ...+10% Entertainment while it's still happy...
        private const float CASTLE_LOSS_AMOUNT = 10f;        // ...or -10% once it's past CASTLE_WANTS_OFF_AT and still stuck there
        private float _castleRequestTimer = CASTLE_REQUEST_INTERVAL;
        private Interactable _castleFloorIt;
        private NPC _carriedNpc;   // in the player's inventory right now, if any
        private NPC _castleOccupant; // currently bouncing on the castle, if any
        private Vector3 _castleBounceCenter;
        private float _castleTimer;
        private float _castleElapsed; // total time this occupant has been on the castle — drives the "shown only while on the castle" timer too
        private bool _castleWantsOff;
        private Interactable _castleVolumeIt;   // the whole castle is clickable, not just the floor
        private Vector3 _bounceVel, _spinVel;   // the occupant's ragdoll tumble
        private float _wantInTimer = CASTLE_TICK_INTERVAL;
        private const float BOUNCE_GRAVITY = 12f;

        /// <summary>For the HUD's castle timer, shown only while someone's actually on it.</summary>
        public bool HasCastleOccupant { get { return _castleOccupant != null; } }
        public float CastleElapsed { get { return _castleElapsed; } }
        public bool CastleWantsOff { get { return _castleWantsOff; } }

        public static void Setup(GameObject host)
        {
            var c = host.AddComponent<PartyTattletailController>();
            Instance = c;
            c.Build(host.transform);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Build(Transform parent)
        {
            _root = parent;
            foreach (var mark in Marks) Add(mark);

            PartyCastle.Spawn(parent);
            if (PartyCastle.FloorPad != null)
            {
                _castleFloorIt = PartyGameRunner.MakeInteractable(PartyCastle.FloorPad.gameObject, "Bouncy Castle", 0.5f);

                // A trigger around the whole castle, so clicking a wall or tower works too.
                var volume = new GameObject("KE_CastleClickVolume");
                volume.transform.SetParent(PartyCastle.FloorPad.parent, false);
                var box = volume.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = new Vector3(0f, (PartyCastle.WallHeight + 0.6f) * 0.5f, 0f);
                box.size = new Vector3(PartyCastle.Width + 0.6f, PartyCastle.WallHeight + 0.6f, PartyCastle.Depth + 0.6f);
                _castleVolumeIt = PartyGameRunner.MakeInteractable(volume, "Bouncy Castle", 0.5f);
            }

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Party] " + _npcs.Count + " party tattletail(s) placed.");
        }

        private void Add(Vector3 mark)
        {
            Vector3 pos = PartyMaterialSpawner.FloorUnder(mark);
            GameObject go = PartyTattletailModels.BuildRandomColored(pos, Quaternion.identity, _root);
            if (go == null) return;

            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.3f, 0f);
            col.size = new Vector3(0.6f, 0.6f, 0.6f);

            var n = new NPC { GO = go, Type = (PartyGameType)Random.Range(0, 3), OriginalPos = pos };
            n.It = PartyGameRunner.MakeInteractable(go, PromptFor(n), 0.4f);
            _npcs.Add(n);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Party] Tattletail at " + pos + " hosts " + n.Type + ".");
        }

        private static string PromptFor(NPC n)
        {
            if (n.WantsCastle) return "Wants to bounce on the castle! Pick them up!";
            switch (n.Type)
            {
                case PartyGameType.FacePaint: return n.PaintDabs == 0 ? "Paint Tattletail's Face" : "Paint Tattletail's Face (" + n.PaintDabs + "/" + PAINT_DABS_NEEDED + ")";
                case PartyGameType.RockPaperScissors: return n.State == State.Active ? "Pick Rock, Paper or Scissors!" : "Play Rock-Paper-Scissors";
                case PartyGameType.Balloon: return n.State == State.Active ? "Inflating..." : "Blow Up a Balloon";
                default: return "Play";
            }
        }

        private void Update()
        {
            var run = EndlessRunManager.Instance;
            if (run == null || !run.RunActive) return;

            UpdateCastleRequests();
            UpdateCastleOccupant();
            UpdateWantIn();
            UpdateCastlePrompt();

            foreach (var n in _npcs)
            {
                // A small idle bob + sway so they read as alive even if the animator's own
                // idle state doesn't do much on its own. Skipped for whichever one is currently
                // bouncing on the castle — that already has its own motion overriding position.
                if (n.GO != null && n.GO.activeSelf && n != _castleOccupant)
                {
                    float t = Time.time * 1.4f + n.GetHashCode();
                    float bob = Mathf.Sin(t) * 0.03f;
                    float sway = Mathf.Sin(t * 0.5f) * 4f;
                    n.GO.transform.position = n.OriginalPos + Vector3.up * (bob + 0.03f);
                    n.GO.transform.rotation = Quaternion.Euler(0f, sway, 0f);
                }

                if (n.State == State.Cooldown)
                {
                    n.Timer -= Time.deltaTime;
                    if (n.Timer <= 0f) { n.State = State.Idle; RefreshPrompt(n); }
                }
                else if (n.Type == PartyGameType.Balloon && n.State == State.Active)
                {
                    n.Timer -= Time.deltaTime;
                    if (n.Balloon != null)
                    {
                        float f = 1f - Mathf.Clamp01(n.Timer / BALLOON_INFLATE_TIME);
                        n.Balloon.transform.localScale = Vector3.one * Mathf.Lerp(0.02f, 0.22f, f);
                    }
                    if (n.Timer <= 0f) FinishBalloon(n);
                }
            }
        }

        private void RefreshPrompt(NPC n) { if (n.It != null) n.It.actionText = PromptFor(n); }

        // ── bouncy castle: request roll, bouncing, the repeating loss ────────
        private void UpdateCastleRequests()
        {
            _castleRequestTimer -= Time.deltaTime;
            if (_castleRequestTimer > 0f) return;
            _castleRequestTimer = CASTLE_REQUEST_INTERVAL;
            if (_carriedNpc != null || _castleOccupant != null) return; // one at a time
            if (Random.value >= CASTLE_REQUEST_CHANCE) return;

            var candidates = _npcs.FindAll(x => x.State == State.Idle && !x.WantsCastle);
            if (candidates.Count == 0) return;
            var pick = candidates[Random.Range(0, candidates.Count)];
            pick.WantsCastle = true;
            RefreshPrompt(pick);
            PartyGameRunner.Hint("A tattletail wants to bounce on the bouncy castle!");
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Party] A tattletail now wants the bouncy castle.");
        }

        private void UpdateCastleOccupant()
        {
            if (_castleOccupant == null) return;

            if (_castleOccupant.GO != null && PartyCastle.FloorPad != null) Tumble(_castleOccupant.GO.transform);

            _castleElapsed += Time.deltaTime;
            _castleTimer -= Time.deltaTime;
            if (_castleTimer > 0f) return;
            _castleTimer = CASTLE_TICK_INTERVAL;

            // The tick that crosses the "wants off" mark just posts the notice — no
            // reward/penalty on that same tick, the new phase starts fresh next tick.
            if (!_castleWantsOff && _castleElapsed >= CASTLE_WANTS_OFF_AT)
            {
                _castleWantsOff = true;
                PartyGameRunner.Hint("The tattletail wants to get off the bouncy castle — go get them!");
                if (_castleOccupant.It != null) _castleOccupant.It.actionText = "Take Tattletail off the Castle!";
                return;
            }

            if (PartyGameRunner.Instance == null) return;
            if (!_castleWantsOff)
            {
                PartyGameRunner.Instance.Entertainment = Mathf.Min(100f, PartyGameRunner.Instance.Entertainment + CASTLE_REWARD_AMOUNT);
                PartyGameRunner.Hint("Having a blast on the castle! +" + CASTLE_REWARD_AMOUNT + "% Entertainment!");
            }
            else
            {
                PartyGameRunner.Instance.Entertainment = Mathf.Max(0f, PartyGameRunner.Instance.Entertainment - CASTLE_LOSS_AMOUNT);
                PartyGameRunner.Hint("Nobody came to get the tattletail off the castle! -" + CASTLE_LOSS_AMOUNT + "% Entertainment!");
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Party] Left on the castle too long — Entertainment -" + CASTLE_LOSS_AMOUNT + "%.");
            }
        }

        // Ragdoll: thrown up off the bouncy floor, spinning on every axis, bouncing off the walls.
        private void Tumble(Transform t)
        {
            float dt = Time.deltaTime;
            float floorY = PartyCastle.FloorPad.position.y + 0.15f; // top of the pad
            float limit = PartyCastle.Width * 0.5f - 0.7f;
            Vector3 c = PartyCastle.FloorPad.position;

            _bounceVel.y -= BOUNCE_GRAVITY * dt;
            Vector3 p = t.position + _bounceVel * dt;
            if (p.y <= floorY)
            {
                p.y = floorY;
                _bounceVel = new Vector3(Random.Range(-1.8f, 1.8f), Random.Range(3.5f, 6f), Random.Range(-1.8f, 1.8f));
                _spinVel = Random.insideUnitSphere * 600f;
            }
            if (Mathf.Abs(p.x - c.x) > limit) { p.x = c.x + Mathf.Sign(p.x - c.x) * limit; _bounceVel.x = -_bounceVel.x; }
            if (Mathf.Abs(p.z - c.z) > limit) { p.z = c.z + Mathf.Sign(p.z - c.z) * limit; _bounceVel.z = -_bounceVel.z; }
            t.position = p;
            t.rotation = Quaternion.Euler(_spinVel * dt) * t.rotation;
        }

        // Waiting to get on (asked, or being carried over): -10% every 10s until it's on.
        private void UpdateWantIn()
        {
            bool waiting = _carriedNpc != null || _npcs.Exists(x => x.WantsCastle);
            if (!waiting) { _wantInTimer = CASTLE_TICK_INTERVAL; return; }
            _wantInTimer -= Time.deltaTime;
            if (_wantInTimer > 0f) return;
            _wantInTimer = CASTLE_TICK_INTERVAL;
            if (PartyGameRunner.Instance == null) return;
            PartyGameRunner.Instance.Entertainment = Mathf.Max(0f, PartyGameRunner.Instance.Entertainment - CASTLE_LOSS_AMOUNT);
            PartyGameRunner.Hint("A tattletail is still waiting for the bouncy castle! -" + CASTLE_LOSS_AMOUNT + "% Entertainment!");
        }

        private void UpdateCastlePrompt()
        {
            string text = _castleOccupant != null ? "Take Tattletail off the Castle"
                : _carriedNpc != null ? "Put Tattletail on the Castle"
                : "Bouncy Castle";
            if (_castleFloorIt != null) _castleFloorIt.actionText = text;
            if (_castleVolumeIt != null) _castleVolumeIt.actionText = text;
        }

        /// <summary>Clicking the castle: takes the bouncer off, or puts the carried tattletail on.</summary>
        public void UseCastle()
        {
            if (_castleOccupant != null) TakeOffCastle();
            else PlaceOnCastle();
        }

        public bool IsCastleFloor(GameObject go)
        {
            return (_castleFloorIt != null && _castleFloorIt.gameObject == go) || (_castleVolumeIt != null && _castleVolumeIt.gameObject == go);
        }
        public bool IsCastleOccupant(GameObject go) { return _castleOccupant != null && _castleOccupant.GO == go; }

        /// <summary>Interacting with a picked-up tattletail while it's holding the castle floor's own prompt.</summary>
        public void PlaceOnCastle()
        {
            if (_carriedNpc == null) { PartyGameRunner.Hint("You're not carrying a tattletail."); return; }
            if (_castleOccupant != null) { PartyGameRunner.Hint("Someone's already bouncing in there!"); return; }

            for (int i = 0; i < PartyInventory.SLOTS; i++)
                if (PartyInventory.Names[i] == "Tattletail") { PartyInventory.RemoveOne(i); break; }

            _castleOccupant = _carriedNpc;
            _carriedNpc = null;
            _castleWantsOff = false;
            _castleElapsed = 0f;
            _castleTimer = CASTLE_TICK_INTERVAL;
            _castleBounceCenter = PartyCastle.RandomPointInside();

            _castleOccupant.GO.SetActive(true);
            _castleOccupant.GO.transform.position = _castleBounceCenter;
            _bounceVel = new Vector3(0f, 5f, 0f);
            _spinVel = Random.insideUnitSphere * 600f;
            if (_castleOccupant.It != null) _castleOccupant.It.actionText = "The tattletail is having fun!";

            PartyGameRunner.Hint("The tattletail hopped onto the bouncy castle!");
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Party] A tattletail is now bouncing on the castle.");
        }

        /// <summary>Interacting with the tattletail while it's actually bouncing on the castle.</summary>
        public void TakeOffCastle()
        {
            if (_castleOccupant == null) return;
            var n = _castleOccupant;
            _castleOccupant = null;
            _castleElapsed = 0f;

            n.GO.transform.position = n.OriginalPos;
            n.GO.transform.rotation = Quaternion.identity;
            n.WantsCastle = false;
            RefreshPrompt(n);

            PartyGameRunner.Hint("Got them safely off the bouncy castle!");
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Party] Tattletail taken off the castle.");
        }

        public bool IsTattletail(GameObject go)
        {
            foreach (var n in _npcs) if (n.GO == go) return true;
            return false;
        }

        public bool IsRpsButton(GameObject go)
        {
            foreach (var n in _npcs)
                if (n.RpsButtons.Contains(go)) return true;
            return false;
        }

        // ── main tattletail interact ─────────────────────────────────────────
        public void Interact(GameObject go)
        {
            NPC n = _npcs.Find(x => x.GO == go);
            if (n == null) return;

            if (n.WantsCastle) { PickUp(n); return; }

            switch (n.Type)
            {
                case PartyGameType.FacePaint: PaintDab(n); break;
                case PartyGameType.RockPaperScissors: StartRps(n); break;
                case PartyGameType.Balloon: StartBalloon(n); break;
            }
        }

        // Picking up a tattletail that's asked for the castle: hides it and puts a "Tattletail" item in the hotbar.
        private void PickUp(NPC n)
        {
            if (_carriedNpc != null) { PartyGameRunner.Hint("You're already carrying a tattletail."); return; }
            int slot = PartyInventory.Add("Tattletail");
            if (slot < 0) { PartyGameRunner.Hint("Inventory full — make room first."); return; }

            n.WantsCastle = false;
            n.GO.SetActive(false);
            _carriedNpc = n;
            PartyGameRunner.Hint("Picked up the tattletail — bring it to the bouncy castle!");
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Party] Picked up a tattletail for the bouncy castle.");
        }

        // ── Face Paint: hold interact repeatedly, a colored dot appears each time ──
        private void PaintDab(NPC n)
        {
            if (n.State == State.Cooldown) return;

            n.PaintDabs++;
            Color c = PaintColors[(n.PaintDabs - 1) % PaintColors.Length];
            Vector3 local = new Vector3(Random.Range(-0.12f, 0.12f), 1.55f + Random.Range(-0.06f, 0.08f), Random.Range(0.12f, 0.2f));
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(dot.GetComponent<Collider>());
            dot.transform.SetParent(n.GO.transform, false);
            dot.transform.localPosition = local;
            dot.transform.localScale = Vector3.one * 0.05f;
            var mat = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("Diffuse"));
            mat.color = c;
            dot.GetComponent<Renderer>().sharedMaterial = mat;
            n.PaintMarks.Add(dot);

            if (n.PaintDabs >= PAINT_DABS_NEEDED)
            {
                PartyGameRunner.Hint("What a colorful face! +" + GAME_ENTERTAINMENT + "% Entertainment!");
                Win(n);
                foreach (var d in n.PaintMarks) if (d != null) Object.Destroy(d);
                n.PaintMarks.Clear();
                n.PaintDabs = 0;
            }
            else
            {
                PartyGameRunner.Hint("Dab " + n.PaintDabs + "/" + PAINT_DABS_NEEDED + "!");
                RefreshPrompt(n);
            }
        }

        // ── Rock-Paper-Scissors ───────────────────────────────────────────────
        private void StartRps(NPC n)
        {
            if (n.State != State.Idle) return;
            n.State = State.Active;
            RefreshPrompt(n);

            string[] labels = { "Rock", "Paper", "Scissors" };
            for (int i = 0; i < 3; i++)
            {
                var btn = BuildRpsIcon(i, n.GO.transform);
                var col = btn.AddComponent<BoxCollider>();
                col.size = new Vector3(0.22f, 0.22f, 0.22f);
                col.isTrigger = true;
                PartyGameRunner.MakeInteractable(btn, "Throw " + labels[i], 0.2f);
                n.RpsButtons.Add(btn);
            }
            PartyGameRunner.Hint("Rock, Paper or Scissors — pick one!");
        }

        private static GameObject BuildRpsIcon(int choice, Transform parent)
        {
            var go = new GameObject("KE_RpsButton_" + choice);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3((choice - 1) * 0.3f, 0.9f, 0.35f);
            Shader sh = Shader.Find("Standard") ?? Shader.Find("Diffuse");

            switch (choice)
            {
                case 0: // Rock — a gray lump
                    AddPart(go.transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.16f, new Color(0.5f, 0.5f, 0.52f), sh);
                    break;
                case 1: // Paper — a white square
                    AddPart(go.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(0.16f, 0.16f, 0.02f), new Color(0.9f, 0.9f, 0.85f), sh);
                    break;
                default: // Scissors — two crossed gray blades
                    var a = AddPart(go.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(0.03f, 0.16f, 0.02f), new Color(0.7f, 0.7f, 0.74f), sh);
                    a.transform.localRotation = Quaternion.Euler(0f, 0f, 30f);
                    var b = AddPart(go.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(0.03f, 0.16f, 0.02f), new Color(0.7f, 0.7f, 0.74f), sh);
                    b.transform.localRotation = Quaternion.Euler(0f, 0f, -30f);
                    break;
            }
            return go;
        }

        private static GameObject AddPart(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color color, Shader sh)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            var mat = new Material(sh);
            mat.color = color;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        public void ThrowRps(GameObject buttonGO)
        {
            NPC owner = null;
            int choice = -1;
            foreach (var n in _npcs)
            {
                int idx = n.RpsButtons.IndexOf(buttonGO);
                if (idx >= 0) { owner = n; choice = idx; break; }
            }
            if (owner == null) return;

            int npcChoice = Random.Range(0, 3);
            string[] labels = { "Rock", "Paper", "Scissors" };
            // 0 beats 2 (rock beats scissors), 1 beats 0 (paper beats rock), 2 beats 1 (scissors beats paper)
            int result = choice == npcChoice ? 0 : ((choice - npcChoice + 3) % 3 == 1 ? 1 : -1);

            foreach (var b in owner.RpsButtons) if (b != null) Destroy(b);
            owner.RpsButtons.Clear();

            if (result > 0)
            {
                PartyGameRunner.Hint("You threw " + labels[choice] + ", Tattletail threw " + labels[npcChoice] + " — you win! +" + GAME_ENTERTAINMENT + "% Entertainment!");
                Win(owner);
            }
            else if (result == 0)
            {
                PartyGameRunner.Hint("You both threw " + labels[choice] + " — tie! Try again.");
                owner.State = State.Idle;
                RefreshPrompt(owner);
            }
            else
            {
                PartyGameRunner.Hint("You threw " + labels[choice] + ", Tattletail threw " + labels[npcChoice] + " — you lose!");
                PartySoundEffects.PlayBoo();
                owner.State = State.Cooldown;
                owner.Timer = COOLDOWN;
                RefreshPrompt(owner);
            }
        }

        // ── Balloon (automatic) ───────────────────────────────────────────────
        private void StartBalloon(NPC n)
        {
            if (n.State != State.Idle) return;
            n.State = State.Active;
            n.Timer = BALLOON_INFLATE_TIME;
            RefreshPrompt(n);

            var b = new GameObject("KE_Balloon");
            b.transform.SetParent(n.GO.transform, false);
            b.transform.localPosition = new Vector3(0.3f, 1.3f, 0.1f);
            b.transform.localScale = Vector3.one * 0.02f;
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(b.transform, false);
            body.transform.localScale = new Vector3(1f, 1.25f, 1f);
            var mat = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
            mat.color = BalloonColors[Random.Range(0, BalloonColors.Length)];
            body.GetComponent<Renderer>().sharedMaterial = mat;
            var knot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(knot.GetComponent<Collider>());
            knot.transform.SetParent(b.transform, false);
            knot.transform.localPosition = Vector3.down * 0.55f;
            knot.transform.localScale = new Vector3(0.3f, 0.2f, 0.3f);
            knot.GetComponent<Renderer>().sharedMaterial = mat;

            n.Balloon = b;
            PartyGameRunner.Hint("Blowing up a balloon...");
        }

        private void FinishBalloon(NPC n)
        {
            PartyGameRunner.Hint("Pop! A balloon for the party! +" + GAME_ENTERTAINMENT + "% Entertainment!");
            if (n.Balloon != null) Destroy(n.Balloon);
            n.Balloon = null;
            Win(n);
        }

        // ── shared payout ─────────────────────────────────────────────────────
        private void Win(NPC n)
        {
            if (PartyGameRunner.Instance != null)
                PartyGameRunner.Instance.Entertainment = Mathf.Min(100f, PartyGameRunner.Instance.Entertainment + GAME_ENTERTAINMENT);
            PartySoundEffects.PlayYay();
            n.State = State.Cooldown;
            n.Timer = COOLDOWN;
            RefreshPrompt(n);
        }
    }
}
