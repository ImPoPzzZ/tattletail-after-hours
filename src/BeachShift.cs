using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Summer Vacation's job, Papa's-Freezeria style: run Mama's smoothie bar.
    /// Task 1 is "Go to work" (step behind the counter) and the day starts. Party Tattletails walk
    /// down from the dunes, along the gravel path, in through the door and line up at the counter.
    /// The one at the front orders (a ticket on the right of the screen, with a patience bar) and
    /// sits on one of the three stools; the rest wait in line. You make it: a cup, fruit, ice, the
    /// blender (stop it in the right texture zone, it keeps running if you walk away), toppings,
    /// then serve it at their seat. Every order is scored (order, blend, wait) and tips cash into
    /// the register. The day ends when its last customer has left; between days Mama's board sells
    /// upgrades, and the bell opens the next day (a DAY N screen, then you're back behind the bar).
    /// Every day adds a fruit and a topping, and the orders grow: cup sizes, ice, mixes, textures,
    /// two and three toppings, three-fruit mixes. Customers get impatient and say so.
    /// </summary>
    public class BeachShift : MonoBehaviour
    {
        public static BeachShift Instance { get; private set; }

        // ── The menu (unlocked in this order: 3 of each on day 1, one more of each every day) ──
        private static readonly string[] Fruits = { "Strawberry", "Banana", "Blueberry", "Mango", "Kiwi", "Pineapple", "Watermelon", "Peach", "Raspberry", "Coconut" };
        private static readonly Color[] FruitColors =
        {
            new Color(0.95f, 0.25f, 0.35f), new Color(1f, 0.86f, 0.3f), new Color(0.3f, 0.32f, 0.9f), new Color(1f, 0.6f, 0.12f), new Color(0.45f, 0.78f, 0.2f),
            new Color(0.98f, 0.78f, 0.1f), new Color(1f, 0.4f, 0.45f), new Color(1f, 0.72f, 0.55f), new Color(0.85f, 0.1f, 0.42f), new Color(0.96f, 0.94f, 0.88f),
        };
        private static readonly string[] Toppings = { "Cherry", "Lemon slice", "Umbrella", "Whipped cream", "Sprinkles", "Choc chips", "Mint leaf", "Orange slice", "Gummy bears", "Caramel" };
        private static readonly Color[] ToppingColors =
        {
            new Color(0.85f, 0.05f, 0.1f), new Color(1f, 0.95f, 0.3f), new Color(1f, 0.45f, 0.7f), new Color(0.98f, 0.97f, 0.94f), new Color(0.6f, 0.4f, 1f),
            new Color(0.3f, 0.18f, 0.1f), new Color(0.25f, 0.8f, 0.35f), new Color(1f, 0.55f, 0.1f), new Color(0.3f, 0.9f, 0.4f), new Color(0.8f, 0.5f, 0.15f),
        };
        private static readonly string[] Sizes = { "SMALL", "MEDIUM", "LARGE" };
        private static readonly float[] SizeScale = { 0.8f, 1f, 1.2f };
        private static readonly string[] Textures = { "CHUNKY", "SMOOTH", "EXTRA SMOOTH" };
        private static readonly float[] TextureIdeal = { 3.5f, 5.5f, 7.5f };
        private static readonly string[] TextureInk = { "#D2691E", "#1E9E3A", "#1E78C8" }; // chunky orange, smooth green, extra smooth blue
        private const int MAX_IN_CUP = 3;

        // What each day adds.
        private static int FruitCount(int d) { return Mathf.Min(Fruits.Length, 2 + d); }
        private static int ToppingCount(int d) { return Mathf.Min(Toppings.Length, 2 + d); }
        private static bool HasSizes(int d) { return true; } // every ticket says SMALL, MEDIUM or LARGE
        private static bool HasIce(int d) { return d >= 3; }
        private static bool HasMixes(int d) { return d >= 4; }
        private static bool HasTextures(int d) { return d >= 5; }
        private static bool HasTwoToppings(int d) { return d >= 6; }
        private static bool HasThreeToppings(int d) { return d >= 8; }
        private static bool HasTripleMixes(int d) { return d >= 9; }

        private static string NewToday(int d)
        {
            string feature;
            switch (d)
            {
                case 1: return "Take orders and make smoothies:\nthe right cup size, fruit, blender, topping,\nthen serve it at their seat!";
                case 2: feature = "Busier today: more customers!"; break;
                case 3: feature = "NEW: ICE! Some want ice: add it before blending."; break;
                case 4: feature = "NEW: MIXES! Some orders take two fruits."; break;
                case 5: feature = "NEW: TEXTURES! CHUNKY, SMOOTH or EXTRA SMOOTH:\nstop the blender in the right zone."; break;
                case 6: feature = "NEW: Some orders want two toppings."; break;
                case 8: feature = "NEW: Some orders want THREE toppings."; break;
                case 9: feature = "NEW: Three-fruit mixes!"; break;
                default: feature = "More customers, less patience. Good luck!"; break;
            }
            string items = "";
            if (FruitCount(d) > FruitCount(d - 1)) items += "New fruit: " + Fruits[FruitCount(d) - 1];
            if (ToppingCount(d) > ToppingCount(d - 1)) items += (items.Length > 0 ? "   ·   " : "") + "New topping: " + Toppings[ToppingCount(d) - 1];
            return feature + (items.Length > 0 ? "\n\n<color=#FFD27A>" + items + "</color>" : "")
                + (InspectorDay(d) ? "\n\n<color=#FF8A5C><b>The Tattletail INSPECTOR visits today!</b></color>" : "");
        }

        // ── What customers say ───────────────────────────────────────────────
        private static readonly string[] SayHurry = { "Hurry up!!", "Tick tock!", "Is it ready yet?", "Tattletail is waiting...", "I'm getting OLD here!", "Helloooo?!" };
        private static readonly string[] SayGaveUp =
        {
            "Ugh, forget it!", "Worst. Bar. EVER.", "Mama's gonna hear about this!", "I waited FOREVER! Bye!",
            "My fur got sunburnt waiting!", "Pfft. Tattletail go somewhere else.", "No smoothie? No tip!", "I could've built ten sand castles!",
        };
        private static readonly string[] SayBad = { "EW! That's not what I ordered!", "Blegh!", "Did you even READ the ticket?!", "Tattletail don't like. Bye.", "I'm telling Mama!", "Is this sand?!" };
        private static readonly string[] SayMeh = { "It's okay...", "Meh.", "Could be better.", "Hm. Fine." };
        private static readonly string[] SayGood = { "Yummy!", "Tasty!", "Mmm, nice!", "Tattletail likes!" };
        private static readonly string[] SayGreat = { "WOW!!! THANK YOU!", "BEST SMOOTHIE EVER!", "Tattletail LOVES it!", "PERFECT!!" };
        private static string Pick(string[] lines) { return lines[Random.Range(0, lines.Length)]; }

        // ── Customer moods (shown on the slip) and the Inspector ─────────────
        private enum Mood { Normal, Nice, Impatient, Aggressive, Judgy, Inspector }

        private class MoodInfo
        {
            public string Tag, Ink;
            public float Patience = 1f, Tip = 1f, HurryAt = HURRY_AT;
            public int ScoreShift;
            public string[] Order, Hurry, GaveUp, Great, Good, Meh, Bad;
            public string Usual = "Hi again! The usual, please!";
        }

        // ── Names, and the regulars who come back every day for the same thing ──
        private static readonly string[] Names =
        {
            "Bubbles", "Fuzzball", "Sprinkles", "Tater", "Muffin", "Pickles", "Noodle", "Biscuit", "Peaches", "Waffles",
            "Nugget", "Pudding", "Marshmallow", "Cupcake", "Jellybean", "Pom-Pom", "Wiggles", "Doodle", "Fluffy", "Pebbles",
            "Button", "Taffy", "Coco", "Mochi", "Churro", "Pretzel", "Gizmo", "Twinkle", "Honey", "Popcorn",
            "Scooter", "Squishy", "Ziggy", "Tofu", "Sunny", "Captain Fuzz", "Mr. Whiskers", "Lady Fluff", "Sir Cuddles", "Baby Tater",
        };
        private const int MAX_REGULARS = 5; // one new regular a day until there are five

        private class Regular { public int Index, Name, ColorPick, Visits; public Mood Mood; public Drink Order; }
        private readonly List<Regular> _regulars = new List<Regular>();
        private readonly Dictionary<int, Regular> _regularAt = new Dictionary<int, Regular>(); // today's spawn number -> regular
        private readonly List<string> _namesToday = new List<string>();

        private static string RegKey(int i, string field) { return "Beach Regular " + i + " " + field; }

        private void LoadRegulars()
        {
            _regulars.Clear();
            int n = Shop.Get("Beach Regulars");
            for (int i = 0; i < n; i++)
            {
                var r = new Regular { Index = i, Name = Shop.Get(RegKey(i, "Name")), ColorPick = Shop.Get(RegKey(i, "Color")), Visits = Shop.Get(RegKey(i, "Visits")), Mood = (Mood)Shop.Get(RegKey(i, "Mood")), Order = new Drink() };
                r.Order.Size = Shop.Get(RegKey(i, "Size"));
                r.Order.Ice = Shop.Get(RegKey(i, "Ice")) > 0;
                r.Order.Texture = Shop.Get(RegKey(i, "Texture"));
                for (int k = 0; k < MAX_IN_CUP; k++)
                {
                    int f = Shop.Get(RegKey(i, "Fruit" + k)) - 1, t = Shop.Get(RegKey(i, "Topping" + k)) - 1;
                    if (f >= 0) r.Order.Fruit.Add(f);
                    if (t >= 0) r.Order.Tops.Add(t);
                }
                if (r.Name >= Names.Length || r.Order.Fruit.Count == 0 || r.Order.Tops.Count == 0 || r.Mood == Mood.Inspector) continue; // a damaged entry
                _regulars.Add(r);
            }
        }

        private static void SaveRegular(Regular r)
        {
            int i = r.Index;
            Shop.SetValue(RegKey(i, "Name"), r.Name);
            Shop.SetValue(RegKey(i, "Color"), r.ColorPick);
            Shop.SetValue(RegKey(i, "Visits"), r.Visits);
            Shop.SetValue(RegKey(i, "Mood"), (int)r.Mood);
            Shop.SetValue(RegKey(i, "Size"), r.Order.Size);
            Shop.SetValue(RegKey(i, "Ice"), r.Order.Ice ? 1 : 0);
            Shop.SetValue(RegKey(i, "Texture"), r.Order.Texture);
            for (int k = 0; k < MAX_IN_CUP; k++)
            {
                Shop.SetValue(RegKey(i, "Fruit" + k), k < r.Order.Fruit.Count ? r.Order.Fruit[k] + 1 : 0);
                Shop.SetValue(RegKey(i, "Topping" + k), k < r.Order.Tops.Count ? r.Order.Tops[k] + 1 : 0);
            }
            Shop.SetValue("Beach Regulars", Mathf.Max(Shop.Get("Beach Regulars"), i + 1));
        }

        /// <summary>A name nobody at the bar has today (and no regular has).</summary>
        private int FreshName()
        {
            for (int tries = 0; tries < 50; tries++)
            {
                int n = Random.Range(0, Names.Length);
                bool taken = _namesToday.Contains(Names[n]);
                foreach (var r in _regulars) if (r.Name == n) taken = true;
                if (!taken) return n;
            }
            return Random.Range(0, Names.Length);
        }

        /// <summary>Today: maybe a new regular joins, and every regular gets a random spot among the day's customers.</summary>
        private void PlanRegulars()
        {
            _regularAt.Clear();
            _namesToday.Clear();
            if (_regulars.Count < Mathf.Min(MAX_REGULARS, _day))
            {
                var r = new Regular { Index = _regulars.Count, Name = FreshName(), ColorPick = Random.Range(0, PartyTattletailModels.ColorCount), Mood = RollMood(_day), Order = NewOrder() };
                _regulars.Add(r);
                SaveRegular(r);
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Beach] New regular: " + Names[r.Name] + " (" + r.Mood + ").");
            }
            int total = DayCustomers(_day), inspector = InspectorDay(_day) ? (total + 1) / 2 : -1;
            var free = new List<int>();
            for (int i = 1; i <= total; i++) if (i != inspector) free.Add(i);
            foreach (var r in _regulars)
            {
                if (free.Count == 0) break;
                int k = Random.Range(0, free.Count);
                _regularAt[free[k]] = r;
                free.RemoveAt(k);
                _namesToday.Add(Names[r.Name]);
            }
        }

        private static readonly MoodInfo[] Moods =
        {
            new MoodInfo // Normal
            {
                Order = new[] { "One {0} smoothie, please!" },
                Hurry = SayHurry, GaveUp = SayGaveUp, Great = SayGreat, Good = SayGood, Meh = SayMeh, Bad = SayBad,
            },
            new MoodInfo // Nice: patient, forgiving, tips well
            {
                Tag = "NICE", Ink = "#2E8B57", Patience = 1.3f, Tip = 1.2f, HurryAt = 0.2f, ScoreShift = 10, Usual = "Hiii! The usual, please! :)",
                Order = new[] { "Hi!! Could I get a {0} smoothie, please? Thank you!", "Hello! One {0} smoothie, if it's not too much trouble!" },
                Hurry = new[] { "No rush! ...well, a little rush.", "Take your time! (please hurry)" },
                GaveUp = new[] { "Aw... I'll come back another day!", "That's okay! Maybe next time!" },
                Great = new[] { "THANK YOU SO MUCH!!", "You're the best!!" },
                Good = new[] { "Yummy, thank you!", "Aww, thanks!" },
                Meh = new[] { "It's... nice! Thank you!", "Thanks anyway!" },
                Bad = new[] { "Oh... that's okay, thanks for trying!", "Hmm, not quite, but thank you!" },
            },
            new MoodInfo // Impatient: little patience, nags early and often
            {
                Tag = "IMPATIENT", Ink = "#D2691E", Patience = 0.6f, Tip = 1.1f, HurryAt = 0.6f, Usual = "The usual! Quick!",
                Order = new[] { "{0} smoothie. FAST.", "{0}! Quick quick quick!" },
                Hurry = new[] { "WHERE IS IT?!", "Come ON!", "I don't have all day!", "Faster! FASTER!" },
                GaveUp = new[] { "FORGET IT!", "Too slow! BYE!", "I'm OUTTA here!" },
                Great = new[] { "Finally! ...It's good.", "Fast AND good? Wow." },
                Good = new[] { "Took you long enough.", "About time." },
                Meh = new[] { "Whatever. Bye.", "Meh. Gotta go." },
                Bad = new[] { "Ugh, I waited for THIS?!", "Nope. Gone." },
            },
            new MoodInfo // Aggressive: rude about everything, tips badly
            {
                Tag = "AGGRESSIVE", Ink = "#C0392B", Patience = 0.9f, Tip = 0.7f, HurryAt = 0.45f, Usual = "The usual. You know the drill.",
                Order = new[] { "Gimme a {0} smoothie. And don't mess it up.", "{0}. Now. Don't make me ask twice." },
                Hurry = new[] { "Move it, slowpoke!", "Are you blending it with your FEET?!", "HEY! I'm WAITING!" },
                GaveUp = new[] { "THIS PLACE STINKS!", "You're the WORST!", "Never coming back, loser!", "Mama should FIRE you!" },
                Great = new[] { "...Fine. It's good. Don't get used to it.", "Huh. Not bad, loser." },
                Good = new[] { "Could be worse.", "Yeah, yeah. Whatever." },
                Meh = new[] { "Is this a joke?", "My grandma blends better." },
                Bad = new[] { "DISGUSTING!", "Are you TRYING to poison me?!", "Worst smoothie EVER!" },
            },
            new MoodInfo // Judgy: scores you harder; only perfection impresses them (and then they tip big)
            {
                Tag = "JUDGY", Ink = "#7D3C98", ScoreShift = -15, HurryAt = 0.4f, Usual = "The usual. Try not to ruin it this time.",
                Order = new[] { "One {0} smoothie. Let's see if you can manage that.", "{0} smoothie. I'll be watching." },
                Hurry = new[] { "Tsk. Slow.", "The service here is... lacking." },
                GaveUp = new[] { "One star.", "Pathetic.", "I expected nothing and I'm STILL disappointed." },
                Great = new[] { "Hm. Acceptable.", "...Fine. I have no notes." },
                Good = new[] { "The presentation is sloppy.", "It's fine. I've had better." },
                Meh = new[] { "Mediocre.", "Two stars." },
                Bad = new[] { "Revolting.", "Did a Tattletail make this? Oh wait." },
            },
            new MoodInfo // The Inspector: an insane order, judged harshly, paid handsomely
            {
                Tag = "INSPECTOR", Ink = "#B8860B", Patience = 0.85f, Tip = 3f, HurryAt = 0.4f, ScoreShift = -10,
                Order = new[] { "Inspection time. I'll have... THIS." },
                Hurry = new[] { "I'm timing you.", "Tick. Tock.", "This is going in my report." },
                GaveUp = new[] { "Walking out on an inspector. Noted.", "Failed. Utterly." },
                Great = new[] { "Impeccable. You pass.", "Outstanding. Mama will hear of this." },
                Good = new[] { "Adequate. I've seen better." },
                Meh = new[] { "Below standard. Noted." },
                Bad = new[] { "Unacceptable. This goes in my report." },
            },
        };

        private static bool InspectorDay(int d) { return d % 3 == 0; }

        private Mood RollMood(int d)
        {
            float r = Random.value;
            if (r < 0.18f) return Mood.Nice;
            float hard = Mathf.Min(0.6f, 0.12f * (d - 1)); // none on day 1; up to 60% of customers are trouble
            if (r < 0.18f + hard) { int k = Random.Range(0, 3); return k == 0 ? Mood.Impatient : k == 1 ? Mood.Aggressive : Mood.Judgy; }
            return Mood.Normal;
        }

        /// <summary>The Inspector's order: three fruits, three toppings, ice when there's ice, the hardest textures.</summary>
        private Drink InspectorOrder()
        {
            int d = _day;
            var w = new Drink();
            PickDistinct(w.Fruit, 3, FruitCount(d));
            PickDistinct(w.Tops, 3, ToppingCount(d));
            w.Size = Random.Range(0, 3);
            w.Ice = HasIce(d);
            w.Texture = HasTextures(d) ? (Random.value < 0.5f ? 0 : 2) : 1;
            return w;
        }

        /// <summary>A black top hat with a gold band, sat on top of the Inspector.</summary>
        private static void GiveInspectorHat(GameObject model)
        {
            bool any = false;
            var b = new Bounds();
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                if (r == null) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (!any) return;
            var hat = new GameObject("InspectorHat").transform; // built in meters, then parented keeping its size
            hat.position = new Vector3(b.center.x, b.max.y - 0.1f, b.center.z);
            var black = Mat.Lit(new Color(0.07f, 0.07f, 0.08f), 0.4f);
            BeachLayout.CylPart(hat, new Vector3(0f, 0.006f, 0f), new Vector3(0.3f, 0.006f, 0.3f), black);                                        // brim
            BeachLayout.CylPart(hat, new Vector3(0f, 0.11f, 0f), new Vector3(0.19f, 0.1f, 0.19f), black);                                          // crown
            BeachLayout.CylPart(hat, new Vector3(0f, 0.035f, 0f), new Vector3(0.195f, 0.02f, 0.195f), Mat.Lit(new Color(0.95f, 0.75f, 0.25f), 0.8f)); // gold band
            hat.SetParent(model.transform, true);
        }

        // ── Places (beach-local meters) ──────────────────────────────────────
        // Walking lanes, so nobody walks through anybody: in along ENTRY_X, the line along LANE_X, to a stool
        // along TO_STOOL_Z (behind the seated ones), out along EXIT_Z and through the front door at EXIT_X.
        private const float LANE_X = 14.25f, ENTRY_X = 12.9f, QUEUE_Z0 = -7.0f, QUEUE_STEP = 0.8f, TO_STOOL_Z = -6.6f, EXIT_Z = -6.1f;
        private const int QUEUE_SPOTS = 6; // a 7th spot would walk them into Mama in the corner
        private static readonly Vector3 SpawnPt = new Vector3(-4f, 0f, -18f);   // over the dunes behind the beach
        private static readonly Vector3 PathStart = new Vector3(0.75f, 0f, -7.45f);
        private static readonly Vector3 DoorOut = new Vector3(11.2f, 0f, -7.55f);
        private static readonly Vector3 DoorIn = new Vector3(12.45f, 0f, -7.4f);
        private static readonly Vector3 WorkSpot = new Vector3(15.3f, 0f, -8.9f);  // where a new day puts you: behind the bar, facing it
        private static readonly Vector3 ExitIn = new Vector3(BeachLayout.EXIT_X, 0f, BeachLayout.BZ1 - 0.4f);
        private static readonly Vector3 ExitOut = new Vector3(BeachLayout.EXIT_X, 0f, 1.2f);
        private static readonly Vector3 GonePt = new Vector3(23f, 0f, 1f);        // off along the beach, over the side dunes
        private static float StoolZ { get { return BeachLayout.COUNTER_Z + 0.65f; } }

        // ── Timing / scoring ─────────────────────────────────────────────────
        private const float SPEED = 1.5f, BLEND_WINDOW = 0.9f, BLEND_ZERO = 4.5f, BLEND_SHOWN = 10f, HURRY_AT = 0.35f;
        private static int DayCustomers(int d) { return 4 + 2 * Mathf.Min(d, 8); }                  // 6, 8, 10... 20
        // Patience and pace get harder until day 4, then stay there (the orders keep growing instead).
        private const int HARDEST_DAY = 4;
        private static float DayInterval(int d) { return 47f - 5f * Mathf.Min(d, HARDEST_DAY); }            // ~42 s on day 1 ... ~27 s from day 4
        private static float DayPairChance(int d) { return 0.08f * (Mathf.Min(d, HARDEST_DAY) - 1); }       // never two together on day 1, at most 24%
        private static float DayPatience(int d) { return 2f * (115f - 5f * Mathf.Min(d, HARDEST_DAY)); }    // 220 s on day 1, never under 190 s

        /// <summary>A cup being made, or what a customer ordered.</summary>
        private class Drink
        {
            public int Size = 1, Texture = 1;
            public bool Ice, Blended;
            public float BlendT;
            public readonly List<int> Fruit = new List<int>(), Tops = new List<int>();
        }

        private enum CState { Arriving, Queued, ToStool, Seated, Leaving }

        private class Customer
        {
            public GameObject Model; public string Color; public int Number;
            public CState State; public readonly List<Vector3> Route = new List<Vector3>();
            public Vector3 Pos; public float Yaw, FaceYaw = 180f, Bob, SitT;
            public int Stool = -1; public Drink Want; public float OrderedAt, Patience; public bool Hurried, Practice;
            public Mood Mood;
            public string Name; public Regular Regular;
            public Text Bubble; public GameObject BubbleGo; public float BubbleUntil;
        }

        private class Blender
        {
            public Transform T; public Interactable It; public Drink Cup; public float Progress; public int NextAlarm;
            public Renderer Jar; public Transform JarT; public AudioSource Sound; public GameObject Meter; public Text MeterText;
        }

        private enum Phase { Waiting, GoToWork, Tutorial, Open, Summary, Judging, Break, Loading }
        private enum Kind { Cups, Fruit, Ice, Blender, Topping, Serve, Trash, Bell, Board, Decor }
        private class Station { public Kind Kind; public int Index; }

        private Phase _phase = Phase.Waiting;
        private Transform _root;
        private readonly Dictionary<Interactable, Station> _stations = new Dictionary<Interactable, Station>();
        private readonly List<Customer> _customers = new List<Customer>();
        private readonly List<Customer> _queue = new List<Customer>();
        private readonly Customer[] _stool = new Customer[3];
        private const int BLENDERS = 4; // one to start, up to three more from Mama's board
        private readonly Blender[] _blenders = new Blender[BLENDERS];

        private int _day, _spawned, _served, _angry, _scoreSum, _tips, _pairPending, _cashAtStart;
        private float _spawnTimer, _phaseTimer, _loadT;
        private bool _loadReset;

        private Drink _held;
        private GameObject _heldModel;
        private readonly GameObject[] _cupStacks = new GameObject[3];
        private readonly GameObject[] _fruitBins = new GameObject[Fruits.Length];
        private readonly GameObject[] _toppingBowls = new GameObject[Toppings.Length];
        private GameObject _iceMachine;
        private Transform _trashT;
        private readonly Transform[] _serveT = new Transform[3];
        private readonly Dictionary<string, GameObject> _decor = new Dictionary<string, GameObject>();
        private Material _jarEmpty;
        private Transform _drawer;
        private float _drawerT;
        private Vector3 _registerPos;
        private Text _registerDisplay;
        private static AudioClip _ding, _whirr, _kaChing;

        // ── Lifecycle ────────────────────────────────────────────────────────
        private BeachMusic _music;

        private void Awake()
        {
            Instance = this;
            _music = BeachMusic.Create(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_heldModel != null) Destroy(_heldModel);
            BeachUpgradeMenu.Close();
        }

        public static void OnInteract(Interactable it) { if (Instance != null) Instance.Handle(it); }

        // ── Trailer hooks (TrailerDirector drives the bar; nothing here is saved) ──
        public static bool TrailerMode;
        public bool TrailerReady { get { return _phase != Phase.Waiting; } }

        /// <summary>Customers already on their way (at the given beach spots), with the given moods (Mood as int).</summary>
        public void TrailerCustomers(Vector3[] at, int[] moods)
        {
            for (int i = 0; i < at.Length; i++)
            {
                string color;
                var model = PartyTattletailModels.BuildRandomColored(_root.TransformPoint(at[i]), _root.rotation, _root, out color);
                if (model == null) continue;
                var c = new Customer
                {
                    Model = model, Color = color ?? "Tattletail", Number = 90 + i, State = CState.Arriving, Pos = at[i],
                    Mood = (Mood)moods[i], Want = NewOrder(), Patience = 100000f, Hurried = true, Name = Names[FreshName()],
                };
                c.Route.Add(DoorOut); c.Route.Add(DoorIn);
                _customers.Add(c);
                _queue.Add(c);
            }
        }

        private Vector3? _trailerLook;

        /// <summary>Customers waiting in line turn to look at this beach spot (the trailer's camera).</summary>
        public void TrailerLookAt(Vector3 at) { _trailerLook = at; }

        public void TrailerBlend()
        {
            var b = _blenders[0];
            if (b == null || b.Cup != null) return;
            var d = new Drink();
            d.Fruit.Add(0); d.Fruit.Add(1);
            b.Cup = d;
            StartBlender(b);
            b.Progress = 2.2f;
        }

        public void TrailerStopBlend()
        {
            foreach (var b in _blenders) if (b != null && b.Cup != null) { b.Cup = null; StopBlender(b); }
        }

        public void TrailerReceipt()
        {
            ShowReceipt("<b>BUBBLES</b>\n\nORDER . . . . . . 100%\nBLEND . . . . . . 100%\nWAIT . . . . . . . 96%\n<b>TOTAL . . . . . 98%</b>\n<color=#1E8E3E><b>TIP: $6</b></color>",
                        "PERFECT!", new Color(1f, 0.78f, 0.1f));
            RingRegister();
            PartySoundEffects.PlayYay();
        }

        public void TrailerCustomersLeave()
        {
            foreach (var c in _customers.ToArray())
            {
                if (c.State == CState.Leaving) continue;
                _queue.Remove(c);
                Leave(c, false);
            }
        }

        public void TrailerMamaWalk()
        {
            var mama = BeachLayout.Mama;
            if (mama == null) return;
            if (mama.GetComponent<MamaFloor>() == null) mama.AddComponent<MamaFloor>().Y = mama.transform.localPosition.y;
            var watch = mama.GetComponent<WatchPlayer>();
            if (watch != null) watch.enabled = false;
            _mamaRoute.Clear();
            _mamaRoute.Add(new Vector3(13.6f, 0f, -3.3f));
            _mamaRoute.Add(JudgeSpot);
        }

        public void TrailerJumpscare() { Jumpscare(); }

        /// <summary>After the jumpscare: the camera gets its tag and its ears back, Mama goes home.</summary>
        public void TrailerAfterScare()
        {
            if (_retagged != null) { _retagged.tag = _oldTag; _retagged = null; }
            var cam = PlayerCamera();
            var ear = cam != null ? cam.GetComponent<AudioListener>() : null;
            if (ear != null) ear.enabled = true;
            SendMamaHome();
        }

        /// <summary>The trailer's over: no leftover customers, blenders or receipts.</summary>
        public void TrailerCleanup()
        {
            foreach (var c in _customers) DropCustomer(c);
            _customers.Clear();
            _queue.Clear();
            for (int i = 0; i < _stool.Length; i++) _stool[i] = null;
            foreach (var b in _blenders) if (b != null && b.Cup != null) { b.Cup = null; StopBlender(b); }
            if (_receipt != null) { _receipt.gameObject.SetActive(false); _receiptT = -1f; }
            _mamaRoute.Clear();
            SendMamaHome();
        }

        private void SendMamaHome()
        {
            var mama = BeachLayout.Mama;
            if (mama == null) return;
            var floor = mama.GetComponent<MamaFloor>();
            mama.transform.localPosition = BeachLayout.MamaHome + Vector3.up * (floor != null ? floor.Y : BeachLayout.FLOOR_TOP);
            mama.transform.localRotation = Quaternion.Euler(0f, BeachLayout.MAMA_HOME_YAW, 0f);
            var watch = mama.GetComponent<WatchPlayer>();
            if (watch != null) watch.enabled = true;
        }

        private void Update()
        {
            var ctrl = BeachController.Instance;
            if (ctrl == null || !ctrl.Built || RM.fpsController == null) return;
            if (_phase == Phase.Waiting)
            {
                _root = ctrl.Root;
                try { BuildStations(); BuildDecor(); BuildHud(); }
                catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogError("KE: [Beach] Shift setup: " + e); enabled = false; return; }
                _day = BeachUpgrades.Day;
                LoadRegulars();
                RefreshStations();
                _phase = Phase.GoToWork;
                if (!TrailerMode) HubMode.SetHudText("Go to work");
            }

            float dt = Time.deltaTime;
            if (!TrailerMode) switch (_phase)
            {
                case Phase.GoToWork:
                    if (!BehindCounter()) break;
                    if (_day == 1 && !TutorialDone) { StartTutorial(); break; }
                    _music.PlayDay(); StartDay(); ShowCard("<size=54><b>DAY " + _day + "</b></size>\n\n" + NewToday(_day), 6f);
                    break;
                case Phase.Tutorial:
                    TickTutorial(dt);
                    break;
                case Phase.Open:
                    TickSpawns(dt);
                    if (_spawned >= DayCustomers(_day) && _customers.Count == 0) EndDay();
                    break;
                case Phase.Summary:
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f) StartJudging();
                    break;
                case Phase.Judging:
                    TickJudging(dt);
                    break;
                case Phase.Loading:
                    TickLoading(dt);
                    break;
            }
            TickMama(dt);
            TickPlacing();
            TickCustomers(dt);
            foreach (var b in _blenders) if (b != null) TickBlender(b, dt);
            TickRegister(dt);
            TickHud();
        }

        private bool BehindCounter()
        {
            Vector3 p = RM.fpsController.transform.position - _root.position;
            return p.x > BeachLayout.BX0 && p.x < BeachLayout.BX1 && p.z > BeachLayout.BZ0 && p.z < BeachLayout.COUNTER_Z - 0.2f;
        }

        // ── The day ──────────────────────────────────────────────────────────
        private void StartDay()
        {
            _spawned = _served = _angry = _scoreSum = _tips = _pairPending = 0;
            _cashAtStart = BeachUpgrades.Cash; // a failed day is replayed: its cash is taken back
            PlanRegulars();
            _spawnTimer = 3f;
            _phase = Phase.Open;
            BeachUpgradeMenu.Close();
            RefreshStations();
            HubMode.SetHudText("Day " + _day + ": make smoothies for the customers");
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Beach] Day " + _day + " opens: " + DayCustomers(_day) + " customers.");
        }

        private void TickSpawns(float dt)
        {
            if (_spawned >= DayCustomers(_day) || _queue.Count >= QUEUE_SPOTS) return;
            _spawnTimer -= dt;
            if (_spawnTimer > 0f) return;
            SpawnCustomer();
            if (_pairPending > 0) { _pairPending = 0; _spawnTimer = DayInterval(_day); }
            else if (Random.value < DayPairChance(_day)) { _pairPending = 1; _spawnTimer = 2.5f; } // a second one right behind
            else _spawnTimer = DayInterval(_day) * Random.Range(0.85f, 1.15f);
        }

        private void EndDay()
        {
            int total = _served + _angry;
            int avg = total > 0 ? _scoreSum / total : 0;
            Daily.Add("beach_days", 1); // (a day of Summer Vacation played, whichever day you are on)
            int bonus = _served > 0 ? 10 + 5 * (_day - 1) : 0;
            if (bonus > 0) Shards.Add(bonus);
            bool payday = _day % 7 == 0; // a full week: Mama pays your wage
            if (payday) { BeachUpgrades.AddCash(WEEKLY_PAY); RingRegister(); }
            _judgeAvg = avg;
            ShowCard("<size=46><b>DAY " + _day + " COMPLETE!</b></size>\n\n" +
                     "Customers served: " + _served + "/" + total + "\n" +
                     "Average score: " + avg + "%\n" +
                     "Tips: $" + _tips + "\n" +
                     (payday ? "<color=#7CFC8A>WEEKLY PAY: +$" + WEEKLY_PAY + "</color>\n" : "") +
                     "Day bonus: +" + bonus + " shards\n\n" +
                     "<color=#FF8080>Mama wants a word...</color>", 7f);
            _music.FadeOut();
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Beach] Day " + _day + " done: served " + _served + "/" + total + ", avg " + avg + "%, tips $" + _tips + ", bonus " + bonus + ".");
            _day++;
            BeachUpgrades.Day = _day;
            _phase = Phase.Summary;
            _phaseTimer = 6f;
            HubMode.SetHudText("Day complete!");
        }

        // The bell: a DAY N screen (with what's new), then you're back behind the bar and the day opens.
        private const float LOAD_IN = 0.35f, LOAD_HOLD = 3.4f, LOAD_OUT = 0.45f;

        private void BeginLoading() { BeginLoading("DAY " + _day, NewToday(_day)); }

        private void BeginLoading(string title, string subtitle)
        {
            CancelPlacing();
            _phase = Phase.Loading;
            _loadT = 0f;
            _loadReset = false;
            BeachUpgradeMenu.Close();
            _cardBox.SetActive(false);
            _coverDay.text = title;
            _coverNew.text = subtitle;
            _cover.alpha = 0f;
            _cover.gameObject.SetActive(true);
            _music.PlayDay();
        }

        private void TickLoading(float dt)
        {
            _loadT += dt;
            _coverLoading.text = "LOADING" + new string('.', 1 + (int)(_loadT * 2f) % 3);
            if (_loadT < LOAD_IN) { _cover.alpha = _loadT / LOAD_IN; return; }
            if (_loadT < LOAD_IN + LOAD_HOLD)
            {
                _cover.alpha = 1f;
                if (!_loadReset && _loadT > LOAD_IN + LOAD_HOLD - 0.2f) { _loadReset = true; ResetForDay(); }
                return;
            }
            if (_loadT < LOAD_IN + LOAD_HOLD + LOAD_OUT) { _cover.alpha = 1f - (_loadT - LOAD_IN - LOAD_HOLD) / LOAD_OUT; return; }
            _cover.gameObject.SetActive(false);
            StartDay();
        }

        private void ResetForDay()
        {
            _held = null;
            RefreshHeld();
            foreach (var b in _blenders) if (b != null && b.Cup != null) { b.Cup = null; StopBlender(b); }
            var fps = RM.fpsController;
            if (fps != null) fps.TeleportPlayer(_root.TransformPoint(WorkSpot + Vector3.up * (BeachLayout.FLOOR_TOP + 0.1f)), new Vector3(0f, 0f, 0f)); // facing the bar
            RefreshStations();
        }

        // ── Customers ────────────────────────────────────────────────────────
        private Drink NewOrder()
        {
            int d = _day;
            var w = new Drink();
            int nf = 1;
            if (HasMixes(d) && Random.value < Mathf.Min(0.75f, 0.45f + 0.08f * (d - 4))) nf = 2;  // 45% on day 4, up to 75%
            if (HasTripleMixes(d) && Random.value < 0.4f) nf = 3;
            PickDistinct(w.Fruit, nf, FruitCount(d));
            int nt = 1;
            if (HasTwoToppings(d) && Random.value < Mathf.Min(0.7f, 0.5f + 0.06f * (d - 6))) nt = 2;   // 50% on day 6, up to 70%
            if (HasThreeToppings(d) && Random.value < 0.45f) nt = 3;
            PickDistinct(w.Tops, nt, ToppingCount(d));
            w.Size = HasSizes(d) ? Random.Range(0, 3) : 1;
            w.Ice = HasIce(d) && Random.value < 0.5f;
            w.Texture = HasTextures(d) ? Random.Range(0, 3) : 1;
            return w;
        }

        private static void PickDistinct(List<int> into, int n, int count)
        {
            while (into.Count < n && into.Count < count)
            {
                int v = Random.Range(0, count);
                if (!into.Contains(v)) into.Add(v);
            }
        }

        private void SpawnCustomer()
        {
            _spawned++;
            string color;
            Regular reg;
            _regularAt.TryGetValue(_spawned, out reg); // a regular: their own color, mood and order, every day
            var model = PartyTattletailModels.BuildColored(_root.TransformPoint(SpawnPt), _root.rotation, _root, reg != null ? reg.ColorPick : -1, out color);
            if (model == null) return;
            Mood mood = InspectorDay(_day) && _spawned == (DayCustomers(_day) + 1) / 2 ? Mood.Inspector : reg != null ? reg.Mood : RollMood(_day);
            var c = new Customer
            {
                Model = model, Color = color ?? "Tattletail", Number = _spawned, State = CState.Arriving, Pos = SpawnPt, Mood = mood,
                Want = mood == Mood.Inspector ? InspectorOrder() : reg != null ? reg.Order : NewOrder(),
                Regular = reg, Name = mood == Mood.Inspector ? "The Inspector" : reg != null ? Names[reg.Name] : null,
                Patience = DayPatience(_day) * BeachUpgrades.PatienceMult * Moods[(int)mood].Patience,
            };
            if (c.Name == null) { c.Name = Names[FreshName()]; _namesToday.Add(c.Name); }
            if (mood == Mood.Inspector)
            {
                GiveInspectorHat(model);
                Hint("The TATTLETAIL INSPECTOR is here! Make it perfect.");
                Ding(RM.fpsController.transform.position);
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Beach] The Inspector arrives (customer #" + _spawned + ").");
            }
            c.Route.Add(PathStart); c.Route.Add(DoorOut); c.Route.Add(DoorIn);
            _customers.Add(c);
            _queue.Add(c);
        }

        private static Vector3 QueueSpot(int i) { return new Vector3(LANE_X, 0f, QUEUE_Z0 + QUEUE_STEP * Mathf.Max(0, i)); }

        private void TickCustomers(float dt)
        {
            for (int i = _customers.Count - 1; i >= 0; i--)
            {
                var c = _customers[i];
                if (c.Model == null) { DropCustomer(c); _customers.RemoveAt(i); continue; }
                bool walk = true;
                switch (c.State)
                {
                    case CState.Arriving:
                        if (c.Route.Count == 0)
                        {
                            c.State = CState.Queued;
                            Vector3 s = QueueSpot(_queue.IndexOf(c));
                            c.Route.Add(new Vector3(ENTRY_X, 0f, s.z));
                            c.Route.Add(s);
                        }
                        break;
                    case CState.Queued:
                    {
                        int idx = _queue.IndexOf(c);
                        Vector3 spot = QueueSpot(idx);
                        bool there = (c.Pos - spot).sqrMagnitude < 0.003f;
                        if (c.Route.Count == 0 && !there) c.Route.Add(spot); // the line moved up
                        c.FaceYaw = 180f;
                        if (TrailerMode && _trailerLook.HasValue) { Vector3 d = _trailerLook.Value - c.Pos; c.FaceYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }
                        if (idx == 0 && there && c.Route.Count == 0) { int st = FreeStool(); if (st >= 0) TakeOrder(c, st); }
                        break;
                    }
                    case CState.ToStool:
                    case CState.Seated:
                    {
                        c.FaceYaw = 180f;
                        if (c.Route.Count == 0) { c.State = CState.Seated; c.SitT = Mathf.Min(1f, c.SitT + dt * 3f); }
                        float left = 1f - (Time.time - c.OrderedAt) / c.Patience;
                        if (!c.Hurried && left < Moods[(int)c.Mood].HurryAt) { c.Hurried = true; Say(c, Pick(Moods[(int)c.Mood].Hurry), 3f); }
                        if (left <= 0f) Leave(c, true);
                        break;
                    }
                    case CState.Leaving:
                        if (c.SitT > 0f) { c.SitT -= dt * 3f; walk = false; } // hop off the stool first
                        else if (c.Route.Count == 0) { DropCustomer(c); _customers.RemoveAt(i); continue; }
                        break;
                }
                MoveCustomer(c, dt, walk);
                TickBubble(c);
            }
        }

        private void MoveCustomer(Customer c, float dt, bool walk)
        {
            bool moving = false;
            if (walk && c.Route.Count > 0)
            {
                Vector3 d = c.Route[0] - c.Pos; d.y = 0f;
                float step = SPEED * dt;
                if (d.sqrMagnitude > 0.0001f) c.Yaw = Mathf.MoveTowardsAngle(c.Yaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 360f * dt);
                if (d.magnitude <= step) { c.Pos = c.Route[0]; c.Route.RemoveAt(0); }
                else { c.Pos += d.normalized * step; moving = true; }
            }
            else c.Yaw = Mathf.MoveTowardsAngle(c.Yaw, c.FaceYaw, 240f * dt);

            // A waddle: little hops and a side-to-side rock (the models have no walk animation).
            if (moving) c.Bob += dt * 11f;
            else c.Bob = Mathf.MoveTowards(c.Bob, Mathf.Round(c.Bob / Mathf.PI) * Mathf.PI, dt * 6f);
            float seat = Mathf.Clamp01(c.SitT);
            float y = Ground(c.Pos.x, c.Pos.z) + Mathf.Abs(Mathf.Sin(c.Bob)) * 0.07f + BeachLayout.SEAT * seat + Mathf.Sin(seat * Mathf.PI) * 0.12f;
            c.Model.transform.localPosition = new Vector3(c.Pos.x, y, c.Pos.z);
            c.Model.transform.localRotation = Quaternion.Euler(0f, c.Yaw, Mathf.Sin(c.Bob) * 7f);
        }

        private static float Ground(float x, float z)
        {
            if (x > BeachLayout.BX0 - 0.1f && x < BeachLayout.BX1 && z > BeachLayout.BZ0 && z < BeachLayout.BZ1) return BeachLayout.FLOOR_TOP;
            return BeachController.Height(x, z) + 0.03f;
        }

        private int FreeStool()
        {
            for (int i = 0; i < _stool.Length; i++) if (_stool[i] == null) return i;
            return -1;
        }

        private void TakeOrder(Customer c, int stool)
        {
            _queue.Remove(c);
            c.Stool = stool;
            _stool[stool] = c;
            c.OrderedAt = Time.time;
            c.State = CState.ToStool;
            c.Route.Clear();
            c.Route.Add(new Vector3(BeachLayout.StoolX[stool], 0f, TO_STOOL_Z));
            c.Route.Add(new Vector3(BeachLayout.StoolX[stool], 0f, StoolZ));
            if (c.Regular != null && c.Regular.Visits > 0) Say(c, Moods[(int)c.Mood].Usual, 4f); // recognize them? you already know the order
            else Say(c, string.Format(Pick(Moods[(int)c.Mood].Order), FruitText(c.Want)), 4f);
            if (c.Regular != null) { c.Regular.Visits++; SaveRegular(c.Regular); }
            Ding(c.Model.transform.position + Vector3.up);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Beach] Order #" + c.Number + " (" + c.Name + (c.Regular != null ? ", regular" : "") + ", " + c.Color + ", " + c.Mood + ", seat " + (stool + 1) + "): " + TicketLines(c.Want, false).Replace("\n", " | ") + ".");
        }

        private void Leave(Customer c, bool angry)
        {
            if (c.Stool >= 0 && _stool[c.Stool] == c) _stool[c.Stool] = null;
            float sx = c.Stool >= 0 ? BeachLayout.StoolX[c.Stool] : c.Pos.x;
            c.Stool = -1;
            c.State = CState.Leaving;
            c.Route.Clear();
            c.Route.Add(new Vector3(sx, 0f, EXIT_Z));
            c.Route.Add(new Vector3(BeachLayout.EXIT_X, 0f, EXIT_Z));
            c.Route.Add(ExitIn); c.Route.Add(ExitOut); c.Route.Add(GonePt);
            if (angry)
            {
                _angry++;
                Say(c, Pick(Moods[(int)c.Mood].GaveUp), 4f);
                PartySoundEffects.PlayBoo();
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Beach] Customer #" + c.Number + " ran out of patience.");
            }
        }

        private void DropCustomer(Customer c)
        {
            _queue.Remove(c);
            if (c.Stool >= 0 && _stool[c.Stool] == c) _stool[c.Stool] = null;
            if (c.Model != null) Destroy(c.Model);
            if (c.BubbleGo != null) Destroy(c.BubbleGo);
        }

        // Text.canvas is null while its canvas is switched off (it only looks at active parents):
        // the canvas objects themselves are kept instead.
        private void Say(Customer c, string text, float seconds)
        {
            if (c.Bubble == null)
            {
                c.Bubble = WorldText.Create(_root, text, Color.white, 0.075f, TextAnchor.MiddleCenter, 3f);
                c.BubbleGo = c.Bubble.canvas.gameObject;
                var o = c.Bubble.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.9f);
                o.effectDistance = new Vector2(3f, -3f);
            }
            c.Bubble.text = text;
            c.BubbleGo.SetActive(true);
            c.BubbleUntil = Time.time + seconds;
        }

        private void TickBubble(Customer c)
        {
            var go = c.BubbleGo;
            if (go == null) return;
            if (Time.time > c.BubbleUntil) { if (go.activeSelf) go.SetActive(false); return; }
            go.transform.position = c.Model.transform.position + Vector3.up * 1.05f;
            Billboard(go.transform);
        }

        private static void Billboard(Transform t)
        {
            var cam = PlayerCamera();
            if (cam != null) t.rotation = Quaternion.LookRotation(t.position - cam.transform.position); // readable from the canvas's -Z side
        }

        private static Camera PlayerCamera() { return RM.fpsController != null ? RM.fpsController.GetComponentInChildren<Camera>() : null; }

        // ── Serving ──────────────────────────────────────────────────────────
        private static bool SameSet(List<int> a, List<int> b)
        {
            if (a.Count != b.Count) return false;
            foreach (int x in a) if (!b.Contains(x)) return false;
            return true;
        }

        private void Serve(Customer c)
        {
            Drink cup = _held, w = c.Want;
            int parts = 2, ok = 0;
            if (SameSet(cup.Fruit, w.Fruit)) ok++;
            if (SameSet(cup.Tops, w.Tops)) ok++;
            if (HasSizes(FeatureDay)) { parts++; if (cup.Size == w.Size) ok++; }
            if (HasIce(FeatureDay)) { parts++; if (cup.Ice == w.Ice) ok++; }
            int order = ok * 100 / parts;
            int blend = cup.Blended ? Mathf.RoundToInt(BlendScore(cup.BlendT, TextureIdeal[w.Texture])) : 0;
            float waited = Time.time - c.OrderedAt, grace = BeachUpgrades.WaitGrace;
            int wait = Mathf.RoundToInt(100f * Mathf.Clamp01(1f - Mathf.Max(0f, waited - grace) / Mathf.Max(1f, c.Patience - grace)));
            var mi = Moods[(int)c.Mood];
            int total = (order + blend + wait) / 3 + mi.ScoreShift;
            if (c.Mood == Mood.Inspector && order < 100) total /= 2;   // anything wrong on the order: he halves it
            total = Mathf.Clamp(total, 0, 100);
            float tipMult = c.Mood == Mood.Judgy && total >= 90 ? 1.5f : mi.Tip; // judgy customers pay for perfection
            int tip = c.Practice ? 0 : Mathf.RoundToInt(total * 0.06f * BeachUpgrades.TipMult * tipMult);
            if (c.Mood == Mood.Inspector && total >= 90 && !c.Practice) tip += 20;

            _served++;
            Daily.Add("served", 1);
            _scoreSum += total;
            _tips += tip;
            BeachUpgrades.AddCash(tip);
            RingRegister(); // ka-ching!
            _held = null;
            RefreshHeld();

            string grade = total >= 90 ? "PERFECT!" : total >= 70 ? "GREAT" : total >= 45 ? "OKAY" : "BAD";
            ShowReceipt("<b>" + (c.Mood == Mood.Inspector ? "THE INSPECTOR" : c.Name.ToUpper() + (c.Mood != Mood.Normal ? " (" + mi.Tag + ")" : "")) + "</b>\n\n" +
                        "ORDER . . . . . . " + order + "%\n" +
                        "BLEND . . . . . . " + blend + "%\n" +
                        "WAIT . . . . . . . " + wait + "%\n" +
                        "<b>TOTAL . . . . . " + total + "%</b>\n" +
                        "<color=#1E8E3E><b>TIP: $" + tip + "</b></color>",
                        grade, total >= 90 ? new Color(1f, 0.78f, 0.1f) : total >= 70 ? new Color(0.25f, 0.8f, 0.35f) : total >= 45 ? new Color(1f, 0.55f, 0.15f) : new Color(0.9f, 0.18f, 0.18f));
            Say(c, Pick(total >= 90 ? mi.Great : total >= 70 ? mi.Good : total >= 45 ? mi.Meh : mi.Bad), 3.5f);
            if (total >= 90) PartySoundEffects.PlayYay();
            else if (total < 45) PartySoundEffects.PlayBoo();
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Beach] Served #" + c.Number + " (" + c.Name + ", " + c.Mood + "): order " + order + ", blend " + blend + ", wait " + wait + " = " + total + "%, tip $" + tip + ".");
            Leave(c, false);
        }

        // ── Stations ─────────────────────────────────────────────────────────
        private void Handle(Interactable it)
        {
            Station s;
            if (!_stations.TryGetValue(it, out s)) return;
            switch (s.Kind)
            {
                case Kind.Cups:
                    if (_held != null) { Hint("Your hands are full."); return; }
                    _held = new Drink { Size = s.Index };
                    break;
                case Kind.Fruit:
                    if (_held == null) { Hint("Grab a cup first."); return; }
                    if (_held.Blended) { Hint("It's already blended."); return; }
                    if (_held.Fruit.Contains(s.Index)) { Hint("It already has " + Fruits[s.Index].ToLower() + "."); return; }
                    if (_held.Fruit.Count >= MAX_IN_CUP) { Hint("No room for more fruit."); return; }
                    _held.Fruit.Add(s.Index);
                    break;
                case Kind.Ice:
                    if (_held == null) { Hint("Grab a cup first."); return; }
                    if (_held.Blended) { Hint("Add ice before blending."); return; }
                    if (_held.Ice) { Hint("It already has ice."); return; }
                    _held.Ice = true;
                    break;
                case Kind.Blender:
                {
                    var b = _blenders[s.Index];
                    if (b == null) return;
                    if (b.Cup != null)
                    {
                        if (_held != null) { Hint("Your hands are full."); return; }
                        b.Cup.Blended = true;
                        b.Cup.BlendT = b.Progress;
                        _held = b.Cup;
                        b.Cup = null;
                        StopBlender(b);
                        Hint("Blended: " + ZoneLabel(_held.BlendT));
                    }
                    else
                    {
                        if (_held == null) { Hint("Bring a cup with fruit in it."); return; }
                        if (_held.Fruit.Count == 0) { Hint("Add fruit first."); return; }
                        if (_held.Blended) { Hint("That one's already blended."); return; }
                        b.Cup = _held;
                        _held = null;
                        StartBlender(b);
                    }
                    break;
                }
                case Kind.Topping:
                    if (_held == null || _held.Fruit.Count == 0) { Hint("Make a smoothie first."); return; }
                    if (!_held.Blended) { Hint("Blend it first."); return; }
                    if (_held.Tops.Contains(s.Index)) { Hint("It already has " + Toppings[s.Index].ToLower() + "."); return; }
                    if (_held.Tops.Count >= MAX_IN_CUP) { Hint("No room for more toppings."); return; }
                    _held.Tops.Add(s.Index);
                    break;
                case Kind.Serve:
                {
                    var c = _stool[s.Index];
                    if (c == null) { Hint("Nobody's sitting there."); return; }
                    if (_held == null) { Hint("Bring them their smoothie."); return; }
                    if (_held.Fruit.Count == 0) { Hint("That cup is empty!"); return; }
                    Serve(c);
                    return;
                }
                case Kind.Trash:
                    if (_held == null) { Hint("Nothing to throw away."); return; }
                    _held = null;
                    Hint("Thrown away.");
                    break;
                case Kind.Bell:
                    Ding(it.transform.position);
                    if (_phase == Phase.Break) BeginLoading();
                    else if (_phase == Phase.Summary || _phase == Phase.Judging) Hint("Mama isn't done with you yet.");
                    else if (_phase == Phase.Open) Hint("The bar is already open.");
                    else if (_phase == Phase.GoToWork) { _music.PlayDay(); StartDay(); }
                    return;
                case Kind.Board:
                    if (!BetweenDays) { Hint("Mama: \"Not now.\""); return; }
                    BeachUpgradeMenu.Open();
                    return;
                case Kind.Decor:
                    if (!BetweenDays) { Hint("Move decor between days."); return; }
                    StartPlacing(_movables[s.Index]);
                    return;
            }
            RefreshHeld();
        }

        private static float BlendScore(float t, float ideal)
        {
            float d = Mathf.Abs(t - ideal);
            return d <= BLEND_WINDOW ? 100f : Mathf.Clamp01(1f - (d - BLEND_WINDOW) / (BLEND_ZERO - BLEND_WINDOW)) * 100f;
        }

        /// <summary>Before textures (day 5) there's one target; after, three zones.</summary>
        private string ZoneLabel(float t)
        {
            if (!HasTextures(FeatureDay)) return t < 4.6f ? "TOO CHUNKY" : t <= 6.4f ? "PERFECT!" : "OVERBLENDED";
            return t < 2.5f ? "RAW" : t < 4.5f ? "CHUNKY" : t < 6.5f ? "SMOOTH" : t < 8.5f ? "EXTRA SMOOTH" : "OVERBLENDED";
        }

        private string ZoneColor(float t)
        {
            for (int i = 0; i < 3; i++)
            {
                if (!HasTextures(FeatureDay) && i != 1) continue;
                if (Mathf.Abs(t - TextureIdeal[i]) <= BLEND_WINDOW) return i == 0 ? "#FFA64D" : i == 1 ? "#5CFF7A" : "#5CD6FF";
            }
            return null;
        }

        private void StartBlender(Blender b)
        {
            b.Progress = 0f;
            b.NextAlarm = 0;
            if (b.Jar != null) b.Jar.sharedMaterial = Mat.Lit(MixColor(b.Cup), 0.6f);
            if (b.Sound != null) { b.Sound.pitch = 1f; b.Sound.Play(); }
            if (b.It != null) b.It.actionText = "Take the smoothie out";
        }

        private void StopBlender(Blender b)
        {
            if (b.Jar != null) b.Jar.sharedMaterial = _jarEmpty;
            if (b.JarT != null) b.JarT.localRotation = Quaternion.identity;
            if (b.Sound != null) b.Sound.Stop();
            if (b.It != null) b.It.actionText = "Blend";
            if (b.Meter != null) b.Meter.SetActive(false);
        }

        private void TickBlender(Blender b, float dt)
        {
            if (b.Cup == null || b.Meter == null) return;
            b.Progress += dt * 0.5f * BeachUpgrades.BlendSpeed * (TrailerMode ? 3f : 1f); // the zones are twice as long in real time
            float t = b.Progress;
            if (b.JarT != null) b.JarT.localRotation = Quaternion.Euler(0f, t * 900f, Mathf.Sin(t * 40f) * 1.5f); // it shakes
            if (b.Sound != null) b.Sound.pitch = 1f + Mathf.Min(t, 10f) * 0.03f;

            // Blender Alarm: a ding each time it reaches a texture zone.
            if (BeachUpgrades.Has("ALARM"))
            {
                int first = HasTextures(FeatureDay) ? 0 : 1, last = HasTextures(FeatureDay) ? 2 : 1;
                int zone = first + b.NextAlarm;
                if (zone <= last && t >= TextureIdeal[zone] - BLEND_WINDOW) { Ding(b.T.position + Vector3.up * 0.4f); b.NextAlarm++; }
            }

            var sb = new System.Text.StringBuilder();
            string label = ZoneLabel(t), col = ZoneColor(t) ?? (label == "OVERBLENDED" ? "#FF5A5A" : "#FFFFFF");
            sb.Append("<color=").Append(col).Append("><b>").Append(label).Append("</b></color>\n");
            const int N = 16;
            int filled = Mathf.Clamp(Mathf.RoundToInt(t / BLEND_SHOWN * N), 0, N);
            for (int i = 0; i < N; i++)
            {
                string zc = ZoneColor((i + 0.5f) / N * BLEND_SHOWN) ?? "#FFFFFF";
                sb.Append("<color=").Append(zc).Append(">").Append(i < filled ? '|' : '.').Append("</color>");
            }
            b.MeterText.text = sb.ToString();
            if (!b.Meter.activeSelf) b.Meter.SetActive(true);
            Billboard(b.Meter.transform);
        }

        private void BuildStations()
        {
            float ft = BeachLayout.FLOOR_TOP, kx = BeachLayout.KITCHEN_X, ky = ft + BeachLayout.KITCHEN_TOP, by = ft + BeachLayout.BAR_TOP;
            float staffZ = BeachLayout.COUNTER_Z - 0.14f, custZ = BeachLayout.COUNTER_Z + 0.12f, pz = BeachLayout.PREP_Z;
            var wood = Mat.Lit(new Color(0.55f, 0.38f, 0.24f), 0.15f);
            _jarEmpty = Mat.Lit(new Color(0.78f, 0.88f, 0.95f), 0.95f);

            // Kitchen counter (east wall, you face east): ten fruit bins, two rows; the back row sits on a riser.
            const float RISER = 0.12f;
            var riserT = new GameObject("FruitRiser").transform;
            riserT.SetParent(_root, false);
            BeachLayout.Part(riserT, new Vector3(kx + 0.16f, ky + RISER / 2f, -9.13f), new Vector3(0.3f, RISER, 1.7f), wood);
            for (int f = 0; f < Fruits.Length; f++)
            {
                bool back = f >= 5;
                float z = -8.47f - (f % 5) * 0.33f;
                var bin = MakeStation("Fruit" + f, new Vector3(kx + (back ? 0.16f : -0.16f), ky + (back ? RISER : 0f), z), new Vector3(0.3f, 0.3f, 0.3f), Kind.Fruit, f, "Add " + Fruits[f].ToLower());
                _fruitBins[f] = bin.gameObject;
                BeachLayout.Part(bin, new Vector3(0f, 0.05f, 0f), new Vector3(0.27f, 0.1f, 0.27f), wood);
                FruitPile(bin, f);
                if (back) Label(bin, new Vector3(-0.16f, -RISER / 2f, 0f), Fruits[f].ToUpper(), 90f);   // on the riser's face
                else Label(bin, new Vector3(-0.17f, -0.12f, 0f), Fruits[f].ToUpper(), 90f);             // on the counter's edge
            }

            // The prep counter in front of the back shelf (you face south): cups, ice, the blenders.
            var white = Mat.Lit(new Color(0.97f, 0.97f, 0.97f), 0.5f);
            for (int sz = 0; sz < 3; sz++)
            {
                var cups = MakeStation("Cups" + sz, new Vector3(13.55f + sz * 0.28f, ky, pz), new Vector3(0.26f, 0.45f, 0.3f), Kind.Cups, sz, "Take a " + Sizes[sz].ToLower() + " cup");
                _cupStacks[sz] = cups.gameObject;
                float sc = SizeScale[sz] * 1.1f;
                for (int i = 0; i < 5; i++) _stackCups.Add(CupModel(cups, new Vector3(0f, i * 0.028f * sc, 0f), sc)); // nested, a little higher each
                Label(cups, new Vector3(0f, -0.12f, 0.27f), Sizes[sz], 180f);
            }
            var ice = MakeStation("Ice", new Vector3(14.45f, ky, pz), new Vector3(0.3f, 0.5f, 0.32f), Kind.Ice, 0, "Add ice");
            _iceMachine = ice.gameObject;
            BeachLayout.Part(ice, new Vector3(0f, 0.2f, 0f), new Vector3(0.28f, 0.4f, 0.26f), Mat.Lit(new Color(0.92f, 0.94f, 0.96f), 0.6f));
            BeachLayout.Part(ice, new Vector3(0f, 0.26f, 0.131f), new Vector3(0.2f, 0.16f, 0.01f), Mat.Lit(new Color(0.4f, 0.75f, 1f), 0.8f));
            BeachLayout.Part(ice, new Vector3(0f, 0.1f, 0.14f), new Vector3(0.06f, 0.03f, 0.04f), Mat.Lit(new Color(0.3f, 0.3f, 0.32f), 0.6f));
            Label(ice, new Vector3(0f, 0.26f, 0.14f), "ICE", 180f);
            for (int i = 0; i < BLENDERS; i++) _blenders[i] = BuildBlender(new Vector3(14.85f + i * 0.36f, ky, pz), i);

            // Bar counter, staff side (you face north): the bell and the topping bowls.
            var bell = MakeStation("Bell", new Vector3(13.8f, by, staffZ), new Vector3(0.22f, 0.25f, 0.2f), Kind.Bell, 0, "Ring the bell");
            BeachLayout.CylPart(bell, new Vector3(0f, 0.01f, 0f), new Vector3(0.12f, 0.01f, 0.12f), Mat.Lit(new Color(0.2f, 0.2f, 0.2f), 0.5f));
            BeachLayout.SpherePart(bell, new Vector3(0f, 0.035f, 0f), new Vector3(0.1f, 0.07f, 0.1f), Mat.Lit(new Color(0.95f, 0.78f, 0.3f), 0.9f));
            Label(bell, new Vector3(0f, 0.14f, 0f), "BELL", 0f);
            // The topping counter (west wall, you face west): two rows of five, the back row on a riser.
            float tx = BeachLayout.TOPPING_X;
            var tRiser = new GameObject("ToppingRiser").transform;
            tRiser.SetParent(_root, false);
            BeachLayout.Part(tRiser, new Vector3(tx - 0.13f, ky + RISER / 2f, (BeachLayout.TOPPING_Z0 + BeachLayout.TOPPING_Z1) / 2f), new Vector3(0.26f, RISER, 1.85f), wood);
            for (int t = 0; t < Toppings.Length; t++)
            {
                bool back = t >= 5;
                float z = BeachLayout.TOPPING_Z0 - 0.2f - (t % 5) * 0.38f;
                var bowl = MakeStation("Topping" + t, new Vector3(tx + (back ? -0.13f : 0.13f), ky + (back ? RISER : 0f), z), new Vector3(0.24f, 0.25f, 0.3f), Kind.Topping, t, "Add " + Toppings[t].ToLower());
                _toppingBowls[t] = bowl.gameObject;
                BeachLayout.CylPart(bowl, new Vector3(0f, 0.025f, 0f), new Vector3(0.19f, 0.025f, 0.19f), white);
                ToppingPile(bowl, t);
                if (back) Label(bowl, new Vector3(0.14f, -RISER / 2f, 0f), Toppings[t].ToUpper(), 270f);   // on the riser's face
                else Label(bowl, new Vector3(0.17f, -0.12f, 0f), Toppings[t].ToUpper(), 270f);            // on the counter's edge
            }
            var toppingSign = WorldText.Create(_root, "TOPPINGS", new Color(1f, 0.45f, 0.65f), 0.09f, TextAnchor.MiddleCenter, 1.2f);
            toppingSign.canvas.transform.localPosition = new Vector3(BeachLayout.BX0 + 0.1f, ft + 1.65f, (BeachLayout.TOPPING_Z0 + BeachLayout.TOPPING_Z1) / 2f);
            toppingSign.canvas.transform.localRotation = Quaternion.Euler(0f, 270f, 0f);

            // A serving spot in front of each stool (on the customers' half of the counter).
            for (int s = 0; s < 3; s++)
            {
                var spot = MakeStation("Serve" + s, new Vector3(BeachLayout.StoolX[s], by, custZ), new Vector3(0.4f, 0.25f, 0.3f), Kind.Serve, s, "Serve seat " + (s + 1));
                _serveT[s] = spot;
                BeachLayout.CylPart(spot, new Vector3(0f, 0.004f, 0f), new Vector3(0.22f, 0.004f, 0.22f), Mat.Lit(new Color(0.2f, 0.55f, 0.95f), 0.3f));
                Label(spot, new Vector3(0f, 0.1f, -0.1f), "SEAT " + (s + 1), 0f);
            }

            BuildRegister(new Vector3(16.9f, by, staffZ - 0.02f));

            // The trash: a chute in the bar top, next to the bell (the toppings' old spot).
            var trash = MakeStation("Trash", new Vector3(14.3f, by, staffZ), new Vector3(0.24f, 0.15f, 0.22f), Kind.Trash, 0, "Throw it away");
            _trashT = trash;
            BeachLayout.CylPart(trash, new Vector3(0f, 0.004f, 0f), new Vector3(0.18f, 0.004f, 0.18f), Mat.Lit(new Color(0.55f, 0.56f, 0.58f), 0.7f));
            BeachLayout.CylPart(trash, new Vector3(0f, 0.006f, 0f), new Vector3(0.13f, 0.004f, 0.13f), Mat.Lit(new Color(0.05f, 0.05f, 0.05f), 0.2f));
            Label(trash, new Vector3(0f, 0.1f, 0f), "TRASH", 0f);

            // Mama's upgrade board, next to her in the front room.
            var board = MakeStation("Board", new Vector3(13.65f, ft, -1.45f), new Vector3(1f, 1.8f, 0.4f), Kind.Board, 0, "Mama's upgrades");
            foreach (float lx in new[] { -0.38f, 0.38f })
                BeachLayout.Part(board, new Vector3(lx, 0.75f, 0.05f), new Vector3(0.05f, 1.5f, 0.05f), wood);
            BeachLayout.Part(board, new Vector3(0f, 1.25f, 0f), new Vector3(0.94f, 0.74f, 0.04f), wood);
            BeachLayout.Part(board, new Vector3(0f, 1.25f, -0.022f), new Vector3(0.86f, 0.66f, 0.01f), Mat.Lit(new Color(0.12f, 0.22f, 0.16f), 0.1f));
            var chalk = WorldText.Create(board, "MAMA'S\nUPGRADES", new Color(0.95f, 0.95f, 0.9f), 0.11f, TextAnchor.MiddleCenter, 0.8f);
            chalk.canvas.transform.localPosition = new Vector3(0f, 1.32f, -0.03f);
            var sub = WorldText.Create(board, "between days only", new Color(1f, 0.85f, 0.4f), 0.045f, TextAnchor.MiddleCenter, 0.8f);
            sub.canvas.transform.localPosition = new Vector3(0f, 1.03f, -0.03f);

            // TODAY'S MENU, on the front room's east wall (Freezeria style): what can be ordered today.
            var menu = new GameObject("MenuBoard").transform;
            menu.SetParent(_root, false);
            menu.localPosition = new Vector3(BeachLayout.BX1 - BeachLayout.WALL_T / 2f - 0.03f, ft + 1.75f, -5.1f);
            BeachLayout.Part(menu, Vector3.zero, new Vector3(0.04f, 1.05f, 1.6f), wood);
            BeachLayout.Part(menu, new Vector3(-0.022f, 0f, 0f), new Vector3(0.01f, 0.95f, 1.5f), Mat.Lit(new Color(0.12f, 0.22f, 0.16f), 0.1f));
            _menuText = WorldText.Create(menu, "", new Color(0.95f, 0.95f, 0.9f), 0.05f, TextAnchor.UpperCenter, 1.4f);
            _menuText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _menuText.canvas.transform.localPosition = new Vector3(-0.03f, 0.2f, 0f);
            _menuText.canvas.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // read from the room (west)
        }

        private Text _menuText;

        private void UpdateMenuBoard()
        {
            if (_menuText == null) return;
            var sb = new System.Text.StringBuilder("<b><color=#FFD27A>TODAY'S MENU</color></b>\n<size=40>");
            for (int i = 0; i < FruitCount(_day); i++) sb.Append(i > 0 ? "  ·  " : "").Append(Fruits[i]);
            sb.Append("</size>\n<color=#FFB8D8>toppings</color>\n<size=40>");
            for (int i = 0; i < ToppingCount(_day); i++) sb.Append(i > 0 ? "  ·  " : "").Append(Toppings[i]);
            sb.Append("</size>");
            _menuText.text = sb.ToString();
        }

        /// <summary>What's in a fruit bin (by the fruit's look, not its smoothie color).</summary>
        private static void FruitPile(Transform bin, int f)
        {
            var r = new System.Random(20 + f);
            for (int k = 0; k < 9; k++)
            {
                var p = new Vector3(-0.08f + (k % 3) * 0.08f, 0.11f + (float)r.NextDouble() * 0.02f, -0.08f + (k / 3) * 0.08f);
                switch (f)
                {
                    case 1: BeachLayout.SpherePart(bin, p, new Vector3(0.04f, 0.04f, 0.12f), Mat.Lit(FruitColors[1], 0.45f)); break;                  // bananas
                    case 3: BeachLayout.SpherePart(bin, p, new Vector3(0.065f, 0.055f, 0.085f), Mat.Lit(FruitColors[3], 0.5f)); break;                // mangoes
                    case 4: BeachLayout.SpherePart(bin, p, new Vector3(0.06f, 0.05f, 0.07f), Mat.Lit(new Color(0.45f, 0.33f, 0.18f), 0.2f)); break;  // kiwis (fuzzy brown)
                    case 5: BeachLayout.Part(bin, p, Vector3.one * 0.045f, Mat.Lit(FruitColors[5], 0.4f)); break;                                     // pineapple chunks
                    case 6:                                                                                                                             // watermelon wedges
                        BeachLayout.Part(bin, p, new Vector3(0.07f, 0.04f, 0.03f), Mat.Lit(FruitColors[6], 0.4f));
                        BeachLayout.Part(bin, p + new Vector3(0f, -0.02f, 0f), new Vector3(0.072f, 0.01f, 0.032f), Mat.Lit(new Color(0.15f, 0.55f, 0.2f), 0.4f));
                        break;
                    case 7: BeachLayout.SpherePart(bin, p, Vector3.one * 0.07f, Mat.Lit(FruitColors[7], 0.3f)); break;                               // peaches
                    case 9: if (k % 2 == 0) BeachLayout.SpherePart(bin, p, Vector3.one * 0.09f, Mat.Lit(new Color(0.4f, 0.27f, 0.14f), 0.2f)); break; // coconuts
                    default: BeachLayout.SpherePart(bin, p, Vector3.one * (f == 0 ? 0.07f : 0.045f), Mat.Lit(FruitColors[f], 0.5f)); break;        // berries
                }
            }
        }

        private static void ToppingPile(Transform bowl, int t)
        {
            var m = Mat.Lit(ToppingColors[t], 0.5f);
            var r = new System.Random(40 + t);
            int n = t == 4 || t == 5 ? 14 : t == 8 ? 8 : 5;
            for (int k = 0; k < n; k++)
            {
                var p = new Vector3(Mathf.Cos(k * 1.26f) * 0.045f, 0.055f, Mathf.Sin(k * 1.26f) * 0.045f);
                var rp = new Vector3((float)r.NextDouble() * 0.1f - 0.05f, 0.055f, (float)r.NextDouble() * 0.1f - 0.05f);
                switch (t)
                {
                    case 0: BeachLayout.SpherePart(bowl, p, Vector3.one * 0.035f, m); break;
                    case 1: case 7: BeachLayout.CylPart(bowl, p, new Vector3(0.05f, 0.004f, 0.05f), m); break;
                    case 2: BeachLayout.CylPart(bowl, p + Vector3.up * 0.03f, new Vector3(0.06f, 0.006f, 0.06f), m); break;
                    case 3: BeachLayout.SpherePart(bowl, new Vector3(p.x * 0.5f, 0.06f, p.z * 0.5f), new Vector3(0.09f, 0.06f, 0.09f), m); break;
                    case 4: BeachLayout.Part(bowl, rp, new Vector3(0.018f, 0.006f, 0.006f), Mat.Lit(Color.HSVToRGB((float)r.NextDouble(), 0.7f, 1f), 0.5f)); break;
                    case 5: BeachLayout.Part(bowl, rp, Vector3.one * 0.014f, m); break;
                    case 6: BeachLayout.SpherePart(bowl, p, new Vector3(0.04f, 0.006f, 0.025f), m); break;
                    case 8: BeachLayout.SpherePart(bowl, rp, new Vector3(0.025f, 0.03f, 0.02f), Mat.Lit(Color.HSVToRGB((float)r.NextDouble(), 0.8f, 1f), 0.6f)); break;
                    default: BeachLayout.SpherePart(bowl, new Vector3(0f, 0.05f, 0f), new Vector3(0.15f, 0.03f, 0.15f), m); break;               // caramel pool
                }
            }
        }

        private Blender BuildBlender(Vector3 local, int index)
        {
            var steel = Mat.Lit(new Color(0.75f, 0.76f, 0.78f), 0.8f);
            var t = MakeStation("Blender" + index, local, new Vector3(0.3f, 0.6f, 0.32f), Kind.Blender, index, "Blend");
            var b = new Blender { T = t, It = t.GetComponent<Interactable>() };
            BeachLayout.Part(t, new Vector3(0f, 0.07f, 0f), new Vector3(0.22f, 0.14f, 0.22f), Mat.Lit(new Color(0.85f, 0.2f, 0.25f), 0.5f));
            BeachLayout.Part(t, new Vector3(0f, 0.08f, 0.112f), new Vector3(0.03f, 0.03f, 0.01f), steel); // button
            b.JarT = new GameObject("Jar").transform;
            b.JarT.SetParent(t, false);
            b.JarT.localPosition = new Vector3(0f, 0.28f, 0f);
            b.Jar = BeachLayout.CylPart(b.JarT, Vector3.zero, new Vector3(0.15f, 0.13f, 0.15f), _jarEmpty).GetComponent<Renderer>();
            BeachLayout.CylPart(b.JarT, new Vector3(0f, 0.14f, 0f), new Vector3(0.16f, 0.015f, 0.16f), Mat.Lit(new Color(0.15f, 0.15f, 0.16f), 0.5f)); // lid
            Label(t, new Vector3(0f, -0.12f, 0.27f), "BLENDER " + (index + 1), 180f);
            b.Sound = t.gameObject.AddComponent<AudioSource>();
            b.Sound.playOnAwake = false; // or the hidden blenders start whirring the moment they're shown
            b.Sound.clip = Whirr();
            b.Sound.loop = true;
            b.Sound.spatialBlend = 1f;
            b.Sound.minDistance = 1.5f;
            b.Sound.maxDistance = 18f;
            b.Sound.volume = 0.5f;
            b.MeterText = WorldText.Create(_root, "", Color.white, 0.045f, TextAnchor.MiddleCenter, 0.7f);
            b.MeterText.gameObject.AddComponent<Outline>().effectColor = Color.black;
            b.Meter = b.MeterText.canvas.gameObject;
            b.Meter.transform.position = t.position + Vector3.up * (0.7f + (index % 2) * 0.28f);
            b.Meter.SetActive(false);
            return b;
        }

        /// <summary>What's out on the counters: today's menu, and the second blender once bought.</summary>
        private void RefreshStations()
        {
            int d = _showAll ? 99 : _day;
            for (int i = 0; i < _cupStacks.Length; i++) if (_cupStacks[i] != null) _cupStacks[i].SetActive(i == 1 || HasSizes(d));
            foreach (var r in _stackCups) if (r != null) r.sharedMaterial = CupMaterial();
            for (int i = 0; i < _fruitBins.Length; i++) if (_fruitBins[i] != null) _fruitBins[i].SetActive(i < FruitCount(d));
            for (int i = 0; i < _toppingBowls.Length; i++) if (_toppingBowls[i] != null) _toppingBowls[i].SetActive(i < ToppingCount(d));
            if (_iceMachine != null) _iceMachine.SetActive(HasIce(d));
            for (int i = 1; i < BLENDERS; i++)
                if (_blenders[i] != null && _blenders[i].Cup == null) _blenders[i].T.gameObject.SetActive(_showAll || BeachUpgrades.Has("BLENDER" + (i + 1)));
            foreach (var kv in _decor) if (kv.Value != null) kv.Value.SetActive(_showAll || BeachUpgrades.Has(kv.Key));
            UpdateMenuBoard();
        }

        public void OnUpgradesChanged()
        {
            RefreshStations();
            UpdateRegisterDisplay();
        }

        /// <summary>An old cash register at the kitchen end of the bar: keys toward you, the display toward the customers, a drawer that pops out when it rings.</summary>
        private void BuildRegister(Vector3 local)
        {
            var reg = new GameObject("CashRegister").transform;
            reg.SetParent(_root, false);
            reg.localPosition = local;
            var cream = Mat.Lit(new Color(0.86f, 0.8f, 0.64f), 0.45f);
            var dark = Mat.Lit(new Color(0.12f, 0.12f, 0.13f), 0.5f);
            var brass = Mat.Lit(new Color(0.9f, 0.72f, 0.3f), 0.85f);
            BeachLayout.Part(reg, new Vector3(0f, 0.05f, 0.02f), new Vector3(0.3f, 0.1f, 0.3f), cream);              // base
            _drawer = new GameObject("Drawer").transform;
            _drawer.SetParent(reg, false);
            BeachLayout.Part(_drawer, new Vector3(0f, 0.03f, -0.12f), new Vector3(0.28f, 0.05f, 0.08f), cream);      // drawer front
            BeachLayout.Part(_drawer, new Vector3(0f, 0.03f, -0.165f), new Vector3(0.06f, 0.012f, 0.01f), brass);    // handle
            var keys = new GameObject("Keys").transform;                                                               // sloped keyboard
            keys.SetParent(reg, false);
            keys.localPosition = new Vector3(0f, 0.13f, -0.02f);
            keys.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            BeachLayout.Part(keys, Vector3.zero, new Vector3(0.28f, 0.06f, 0.2f), cream);
            for (int kx = 0; kx < 4; kx++)
                for (int kz = 0; kz < 3; kz++)
                    BeachLayout.Part(keys, new Vector3(-0.1f + kx * 0.066f, 0.035f, -0.06f + kz * 0.06f), new Vector3(0.04f, 0.015f, 0.04f), kz == 0 && kx == 3 ? brass : dark);
            BeachLayout.Part(reg, new Vector3(0f, 0.22f, 0.1f), new Vector3(0.03f, 0.1f, 0.03f), cream);             // display post
            BeachLayout.Part(reg, new Vector3(0f, 0.3f, 0.1f), new Vector3(0.2f, 0.07f, 0.05f), dark);               // display
            _registerDisplay = WorldText.Create(reg, "", new Color(0.4f, 1f, 0.5f), 0.04f, TextAnchor.MiddleCenter, 0.2f);
            _registerDisplay.canvas.transform.localPosition = new Vector3(0f, 0.3f, 0.128f);
            _registerDisplay.canvas.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // readable from the customers' side
            _registerPos = reg.position + Vector3.up * 0.2f;
            UpdateRegisterDisplay();
        }

        private void UpdateRegisterDisplay() { if (_registerDisplay != null) _registerDisplay.text = "$ " + BeachUpgrades.Cash; }

        private void RingRegister()
        {
            AudioSource.PlayClipAtPoint(KaChing(), _registerPos, 1f);
            _drawerT = 1.6f;
            UpdateRegisterDisplay();
        }

        private void TickRegister(float dt)
        {
            if (_drawer == null || _drawerT <= 0f) return;
            _drawerT -= dt;
            float open = _drawerT > 1.3f ? Mathf.InverseLerp(1.6f, 1.3f, _drawerT) : Mathf.Clamp01(_drawerT / 0.3f); // pop out, stay, slide back in
            _drawer.localPosition = new Vector3(0f, 0f, -0.16f * open);
        }

        private Transform MakeStation(string name, Vector3 local, Vector3 box, Kind kind, int index, string action)
        {
            var go = new GameObject("KE_Station_" + name);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = local;
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = box;
            col.center = new Vector3(0f, box.y / 2f, 0f);
            var it = PartyGameRunner.MakeInteractable(go, action, 0.5f);
            _stations[it] = new Station { Kind = kind, Index = index };
            return go.transform;
        }

        /// <summary>A small label; yaw 90 is read from the west, 180 from the north, 270 from the east, 0 from the south.</summary>
        private static void Label(Transform parent, Vector3 local, string text, float yaw)
        {
            var t = WorldText.Create(parent, text, new Color(0.15f, 0.1f, 0.05f), 0.032f, TextAnchor.MiddleCenter, 0.3f);
            t.canvas.transform.localPosition = local;
            t.canvas.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // ── Decor you can buy ────────────────────────────────────────────────
        private void BuildDecor()
        {
            float ft = BeachLayout.FLOOR_TOP;
            // Pink flamingo, on the sand by the door.
            var fl = DecorRoot("FLAMINGO", G(10.6f, -4.6f));
            var pink = Mat.Lit(new Color(1f, 0.45f, 0.65f), 0.4f);
            foreach (float lx in new[] { -0.03f, 0.03f }) BeachLayout.CylPart(fl, new Vector3(lx, 0.25f, 0f), new Vector3(0.012f, 0.25f, 0.012f), Mat.Lit(new Color(0.2f, 0.2f, 0.2f), 0.3f));
            BeachLayout.SpherePart(fl, new Vector3(0f, 0.58f, 0f), new Vector3(0.2f, 0.16f, 0.32f), pink);
            var neck = BeachLayout.CylPart(fl, new Vector3(0f, 0.76f, 0.12f), new Vector3(0.04f, 0.14f, 0.04f), pink);
            neck.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            BeachLayout.SpherePart(fl, new Vector3(0f, 0.92f, 0.17f), Vector3.one * 0.08f, pink);
            var beak = BeachLayout.CylPart(fl, new Vector3(0f, 0.9f, 0.23f), new Vector3(0.025f, 0.035f, 0.025f), Mat.Lit(new Color(0.15f, 0.15f, 0.15f), 0.3f));
            beak.transform.localRotation = Quaternion.Euler(110f, 0f, 0f);
            fl.localRotation = Quaternion.Euler(0f, 250f, 0f);
            AddMovable("FLAMINGO", "the flamingo", fl, new Vector3(0.45f, 1f, 0.5f));

            // String lights along the door side and the front, under the eaves.
            var lights = DecorRoot("LIGHTS", Vector3.zero);
            Color[] bulbs = { new Color(1f, 0.3f, 0.3f), new Color(1f, 0.85f, 0.25f), new Color(0.3f, 1f, 0.45f), new Color(0.35f, 0.6f, 1f), new Color(1f, 0.45f, 0.9f) };
            var bulbMats = new Material[bulbs.Length];
            for (int i = 0; i < bulbs.Length; i++) bulbMats[i] = Mat.Unlit(bulbs[i]);
            int n = 0;
            float y = ft + BeachLayout.WALL_H + 0.05f;
            for (float z = BeachLayout.BZ0; z <= BeachLayout.BZ1; z += 0.45f, n++)
                BeachLayout.SpherePart(lights, new Vector3(BeachLayout.BX0 - 0.35f, y - 0.08f * Mathf.Abs(Mathf.Sin(n * 0.7f)), z), Vector3.one * 0.07f, bulbMats[n % bulbMats.Length]);
            for (float x = BeachLayout.BX0; x <= BeachLayout.BX1; x += 0.45f, n++)
                BeachLayout.SpherePart(lights, new Vector3(x, y - 0.08f * Mathf.Abs(Mathf.Sin(n * 0.7f)), BeachLayout.BZ1 + 0.35f), Vector3.one * 0.07f, bulbMats[n % bulbMats.Length]);

            // Surfboard leaning on the front wall.
            var surf = DecorRoot("SURF", G(BeachLayout.BX1 + 0.7f, -5.6f));
            var board = new GameObject("Board").transform;                                   // planted in the sand, a little tilted
            board.SetParent(surf, false);
            board.localRotation = Quaternion.Euler(0f, 0f, 4f);
            BeachLayout.SpherePart(board, new Vector3(0f, 0.7f, 0f), new Vector3(0.06f, 1.9f, 0.5f), Mat.Lit(new Color(0.2f, 0.75f, 0.9f), 0.7f));
            BeachLayout.Part(board, new Vector3(0.03f, 0.7f, 0f), new Vector3(0.01f, 1.6f, 0.07f), Mat.Lit(new Color(1f, 0.95f, 0.85f), 0.5f)); // stripe
            AddMovable("SURF", "the surfboard", surf, new Vector3(0.3f, 1.7f, 0.6f));

            // Tiki torches either side of the door, flickering.
            var tiki = DecorRoot("TIKI", Vector3.zero);
            foreach (float z in new[] { BeachLayout.DOOR_Z0 - 0.35f, BeachLayout.DOOR_Z1 + 0.35f })
            {
                var torch = new GameObject("TikiTorch").transform;
                torch.SetParent(tiki, false);
                torch.localPosition = G(11.3f, z);
                BeachLayout.CylPart(torch, new Vector3(0f, 0.8f, 0f), new Vector3(0.05f, 0.8f, 0.05f), Mat.Lit(new Color(0.55f, 0.42f, 0.22f), 0.2f));
                BeachLayout.CylPart(torch, new Vector3(0f, 1.66f, 0f), new Vector3(0.12f, 0.08f, 0.12f), Mat.Lit(new Color(0.35f, 0.25f, 0.12f), 0.2f));
                var flame = new GameObject("Flame").transform;
                flame.SetParent(torch, false);
                flame.localPosition = new Vector3(0f, 1.8f, 0f);
                BeachLayout.SpherePart(flame, Vector3.zero, new Vector3(0.1f, 0.18f, 0.1f), Mat.Unlit(new Color(1f, 0.55f, 0.1f)));
                BeachLayout.SpherePart(flame, new Vector3(0f, -0.02f, 0f), new Vector3(0.06f, 0.1f, 0.06f), Mat.Unlit(new Color(1f, 0.9f, 0.4f)));
                var l = new GameObject("TorchLight").AddComponent<Light>();
                l.transform.SetParent(flame, false);
                l.type = LightType.Point;
                l.range = 4f;
                l.color = new Color(1f, 0.6f, 0.25f);
                l.shadows = LightShadows.None;
                flame.gameObject.AddComponent<Flicker>().Light = l;
                AddMovable(z < -8f ? "TIKI1" : "TIKI2", "the tiki torch", torch, new Vector3(0.3f, 2f, 0.3f));
            }
        }

        // ── Moving decor (between days) ──────────────────────────────────────
        private class Movable { public string Key, Name; public Transform T; public BoxCollider Col; }
        private readonly List<Movable> _movables = new List<Movable>();
        private Movable _placing;
        private Vector3 _placingFrom;
        private float _placingYawFrom, _placingYaw;

        private bool BetweenDays { get { return _phase == Phase.Break || _phase == Phase.GoToWork; } }

        private void AddMovable(string key, string name, Transform t, Vector3 box)
        {
            var col = t.gameObject.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = box;
            col.center = new Vector3(0f, box.y / 2f, 0f);
            var it = PartyGameRunner.MakeInteractable(t.gameObject, "Move " + name, 0.5f);
            _stations[it] = new Station { Kind = Kind.Decor, Index = _movables.Count };
            _movables.Add(new Movable { Key = key, Name = name, T = t, Col = col });
            float x, z, yaw;
            if (BeachUpgrades.DecorSpot(key, out x, out z, out yaw))
            {
                t.localPosition = new Vector3(x, Ground(x, z) - 0.03f, z);
                t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }

        private void StartPlacing(Movable m)
        {
            if (m == null || _placing != null) return;
            _placing = m;
            _placingFrom = m.T.localPosition;
            _placingYawFrom = _placingYaw = m.T.localEulerAngles.y;
            m.Col.enabled = false;
        }

        private void TickPlacing()
        {
            if (_placing == null) return;
            if (!BetweenDays) { CancelPlacing(); return; }
            _hint.text = "Placing " + _placing.Name + ":  left click to place  ·  scroll or R to turn  ·  right click to cancel";
            _hintUntil = Time.time + 0.2f;
            if (OverlayPanel.AnyOpen) return;
            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0f) _placingYaw += Mathf.Sign(scroll) * 15f;
            if (Input.GetKeyDown(KeyCode.R)) _placingYaw += 45f;
            var cam = PlayerCamera();
            RaycastHit hit;
            if (cam != null && WorldRays.Raycast(cam.transform.position, cam.transform.forward, 8f, out hit) && hit.normal.y > 0.6f)
            {
                Vector3 p = _root.InverseTransformPoint(hit.point);
                if (Mathf.Abs(p.x) < BeachController.WALK_X - 0.5f && p.z > BeachController.WALK_BACK + 0.5f && p.z < BeachController.SHORE)
                    _placing.T.localPosition = p;
            }
            _placing.T.localRotation = Quaternion.Euler(0f, _placingYaw, 0f);
            if (Input.GetMouseButtonDown(1)) { CancelPlacing(); return; }
            if (Input.GetMouseButtonDown(0))
            {
                Vector3 at = _placing.T.localPosition;
                BeachUpgrades.SaveDecorSpot(_placing.Key, at.x, at.z, _placingYaw);
                _placing.Col.enabled = true;
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Beach] Decor " + _placing.Key + " placed at (" + at.x.ToString("F2") + ", " + at.z.ToString("F2") + "), yaw " + _placingYaw + ".");
                _placing = null;
                Hint("Placed!");
            }
        }

        private void CancelPlacing()
        {
            if (_placing == null) return;
            _placing.T.localPosition = _placingFrom;
            _placing.T.localRotation = Quaternion.Euler(0f, _placingYawFrom, 0f);
            _placing.Col.enabled = true;
            _placing = null;
        }

        // ── Mama judges your day ─────────────────────────────────────────────
        private const int WEEKLY_PAY = 100, FIRED_BELOW = 50, PROUD_FROM = 80;
        private const float MAMA_SPEED = 0.8f, JUDGE_TALK = 5.5f;
        private static readonly Vector3 JudgeSpot = new Vector3(15.3f, 0f, -6.35f); // right across the bar from you
        private static readonly string[] SayProud = { "Mama is proud of you.", "Very good. Mama is pleased.", "Wonderful work today, sweetie." };
        private static readonly string[] SayOkay = { "Acceptable. Barely.", "Mama expects better tomorrow.", "Hmm. You may stay." };
        private static readonly string[] SayFired = { "Mama is... disappointed.", "That was not good enough.", "Mama doesn't like that." };
        private readonly List<Vector3> _mamaRoute = new List<Vector3>();
        private int _judgeAvg, _judgeStep;
        private float _judgeT, _mamaBob;
        private Camera _retagged;
        private string _oldTag;

        private void StartJudging()
        {
            if (BeachLayout.Mama != null && BeachLayout.Mama.GetComponent<MamaFloor>() == null)
                BeachLayout.Mama.AddComponent<MamaFloor>().Y = BeachLayout.Mama.transform.localPosition.y;
            _phase = Phase.Judging;
            _judgeStep = 0;
            _judgeT = 0f;
            _music.PlayDrone();
            HubMode.SetHudText("Mama wants a word...");
            var mama = BeachLayout.Mama;
            if (mama == null) { _judgeStep = 1; return; }
            var watch = mama.GetComponent<WatchPlayer>();
            if (watch != null) watch.enabled = false;
            _mamaRoute.Clear();
            _mamaRoute.Add(new Vector3(13.6f, 0f, -3.3f));
            _mamaRoute.Add(JudgeSpot);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Beach] Mama walks over to judge the day (" + _judgeAvg + "%).");
        }

        /// <summary>Mama's slow, stiff walk (her model has no walk animation): a heavy sway, a little bob.</summary>
        private void TickMama(float dt)
        {
            var mama = BeachLayout.Mama;
            if (mama == null || _mamaRoute.Count == 0) return;
            var t = mama.transform;
            Vector3 pos = t.localPosition, flat = new Vector3(pos.x, 0f, pos.z), d = _mamaRoute[0] - flat;
            float step = MAMA_SPEED * dt;
            if (d.magnitude <= step)
            {
                flat = _mamaRoute[0];
                _mamaRoute.RemoveAt(0);
                if (_mamaRoute.Count == 0) OnMamaArrived();
            }
            else flat += d.normalized * step;
            _mamaBob += dt * 5f;
            float baseY = BeachLayout.Mama.GetComponent<MamaFloor>() != null ? BeachLayout.Mama.GetComponent<MamaFloor>().Y : pos.y;
            t.localPosition = new Vector3(flat.x, baseY + Mathf.Abs(Mathf.Sin(_mamaBob)) * 0.035f, flat.z);
            if (d.sqrMagnitude > 0.0001f)
                t.localRotation = Quaternion.RotateTowards(t.localRotation, Quaternion.LookRotation(d) * Quaternion.Euler(0f, 0f, Mathf.Sin(_mamaBob) * 3f), 180f * dt);
        }

        private void OnMamaArrived()
        {
            var mama = BeachLayout.Mama;
            if (mama == null) return;
            if (TrailerMode) return; // the trailer turns her toward its camera
            if (_phase == Phase.Judging) { FacePlayer(mama.transform); _judgeStep = 1; _judgeT = 0f; return; }
            // Back home: she watches you again.
            mama.transform.localRotation = Quaternion.Euler(0f, BeachLayout.MAMA_HOME_YAW, 0f);
            var watch = mama.GetComponent<WatchPlayer>();
            if (watch != null) watch.enabled = true;
        }

        private void FacePlayer(Transform t)
        {
            Vector3 d = RM.fpsController.transform.position - t.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.001f) t.rotation = Quaternion.LookRotation(d);
        }

        private void TickJudging(float dt)
        {
            if (_judgeStep == 0) return; // still walking over
            _judgeT += dt;
            var mama = BeachLayout.Mama;
            if (_judgeStep == 1)
            {
                if (mama != null) FacePlayer(mama.transform);
                if (_judgeT < 1.2f) return; // she stares first
                _judgeStep = 2;
                _judgeT = 0f;
                if (_judgeAvg < FIRED_BELOW)
                {
                    MamaSays(Pick(SayFired), 3f);
                    _music.Stop();
                    return;
                }
                int tip = 0;
                if (_judgeAvg >= PROUD_FROM)
                {
                    tip = 20 + 5 * _day;
                    BeachUpgrades.AddCash(tip);
                    RingRegister();
                }
                MamaSays(Pick(_judgeAvg >= PROUD_FROM ? SayProud : SayOkay) + (tip > 0 ? "\n<color=#7CFC8A>Here. A little something extra: +$" + tip + "</color>" : ""), JUDGE_TALK);
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Beach] Mama's verdict: " + (_judgeAvg >= PROUD_FROM ? "proud" : "okay") + (tip > 0 ? ", tip $" + tip : "") + ".");
                return;
            }
            if (_judgeStep == 2)
            {
                if (_judgeAvg >= FIRED_BELOW)
                {
                    if (_judgeT < JUDGE_TALK) return;
                    // She goes back to her corner; the next day is up to you.
                    _music.FadeOut();
                    _mamaRoute.Clear();
                    _mamaRoute.Add(new Vector3(13.6f, 0f, -3.3f));
                    _mamaRoute.Add(BeachLayout.MamaHome);
                    _phase = Phase.Break;
                    HubMode.SetHudText("Buy upgrades at Mama's board, then ring the bell for Day " + _day);
                    return;
                }
                if (_judgeT < 2.4f) return;
                Jumpscare();
                _judgeStep = 3;
                _judgeT = 0f;
                return;
            }
            // Wait for the game's jumpscare to finish (it gives the camera its sound back at the end).
            if (_judgeStep == 3 && _judgeT > 1.5f && (RM.jumpscare == null || RM.jumpscare.holder == null || !RM.jumpscare.holder.activeSelf || _judgeT > 8f)) Fired();
        }

        private void MamaSays(string line, float seconds)
        {
            ShowCard("<size=40><color=#FF6A6A><b>MAMA</b></color></size>\n\n\"" + line + "\"", seconds);
            var mama = BeachLayout.Mama;
            if (mama != null) AudioController.Play("mama_alert_enter", mama.transform);
        }

        /// <summary>The game's own Mama jumpscare (it wants a main camera, which the beach's camera isn't tagged as); our own if that can't run.</summary>
        private void Jumpscare()
        {
            _cardBox.SetActive(false);
            if (KidVoice.Active) KidVoice.Mine(KidVoice.Kind.Caught);
            var cam = PlayerCamera();
            var js = RM.jumpscare;
            bool vanilla = false;
            try
            {
                if (js != null && cam != null && cam.GetComponent<AudioListener>() != null)
                {
                    if (Camera.main == null) { _retagged = cam; _oldTag = cam.tag; cam.tag = "MainCamera"; }
                    var mi = typeof(JumpscareManager).GetMethod("DoJumpscare");
                    if ((object)mi != null) { mi.Invoke(js, new object[] { null }); vanilla = true; }
                }
            }
            catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Beach] Game jumpscare failed (" + e.Message + ") — using ours."); }
            if (!vanilla && cam != null && BeachLayout.Mama != null)
            {
                // Ours: she's suddenly right in your face.
                var mt = BeachLayout.Mama.transform;
                mt.position = cam.transform.position + cam.transform.forward * 0.55f - Vector3.up * 1.9f;
                mt.rotation = Quaternion.LookRotation(-new Vector3(cam.transform.forward.x, 0f, cam.transform.forward.z));
                AudioController.Play("sting_scary", cam.transform);
            }
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Beach] Failed day — Mama's jumpscare (" + (vanilla ? "the game's" : "ours") + ").");
        }

        private void Fired()
        {
            if (_retagged != null) { _retagged.tag = _oldTag; _retagged = null; }
            var cam = PlayerCamera();
            var ear = cam != null ? cam.GetComponent<AudioListener>() : null;
            if (ear != null) ear.enabled = true;
            // Replay the failed day (EndDay already moved on to the next one); what it earned goes back.
            _day = Mathf.Max(1, _day - 1);
            BeachUpgrades.Day = _day;
            BeachUpgrades.SetCash(_cashAtStart);
            if (BeachLayout.Mama != null)
            {
                var mt = BeachLayout.Mama.transform;
                var floor = BeachLayout.Mama.GetComponent<MamaFloor>();
                mt.localPosition = BeachLayout.MamaHome + Vector3.up * (floor != null ? floor.Y : BeachLayout.FLOOR_TOP);
                OnMamaArrived();
            }
            RefreshStations();
            UpdateRegisterDisplay();
            BeginLoading("DAY " + _day + " AGAIN", "Mama was not happy.\nDo it again. Do it BETTER.");
        }

        private Transform DecorRoot(string id, Vector3 local)
        {
            var go = new GameObject("KE_Decor_" + id);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = local;
            _decor[id] = go;
            return go.transform;
        }

        private static Vector3 G(float x, float z) { return new Vector3(x, BeachController.Height(x, z), z); }

        // ── The cup in your hand ─────────────────────────────────────────────
        private static Color MixColor(Drink d)
        {
            if (d.Fruit.Count == 0) return Color.white;
            Color c = Color.black;
            foreach (int f in d.Fruit) c += FruitColors[f];
            return c / d.Fruit.Count;
        }

        private void RefreshHeld()
        {
            if (_heldModel != null) { Destroy(_heldModel); _heldModel = null; }
            var cam = PlayerCamera();
            if (_held == null || cam == null) return;
            _heldModel = new GameObject("KE_HeldCup");
            _heldModel.transform.SetParent(cam.transform, false);
            float dist = Mathf.Max(cam.nearClipPlane + 0.06f, 0.2f), near = dist / 0.42f;
            _heldModel.transform.localPosition = new Vector3(0.2f, -0.2f, 0.42f) * near;
            _heldModel.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
            _heldModel.transform.localScale = Vector3.one * SizeScale[_held.Size] * near;
            var t = _heldModel.transform;
            CupModel(t, Vector3.zero, 1f);
            if (_held.Ice && !_held.Blended)
                for (int k = 0; k < 3; k++) BeachLayout.Part(t, new Vector3(-0.02f + k * 0.02f, 0.15f, (k % 2) * 0.015f), Vector3.one * 0.018f, Mat.Lit(new Color(0.85f, 0.95f, 1f), 0.9f));
            if (_held.Fruit.Count == 0) return;
            if (!_held.Blended)
            {
                for (int k = 0; k < 5; k++)
                    BeachLayout.SpherePart(t, new Vector3(Mathf.Cos(k * 1.3f) * 0.022f, 0.14f, Mathf.Sin(k * 1.3f) * 0.022f), Vector3.one * 0.03f,
                        Mat.Lit(FruitColors[_held.Fruit[k % _held.Fruit.Count]], 0.5f)); // raw fruit
                return;
            }
            var fm = Mat.Lit(MixColor(_held), 0.5f);
            BeachLayout.CylPart(t, new Vector3(0f, 0.138f, 0f), new Vector3(0.083f, 0.004f, 0.083f), fm);
            BeachLayout.SpherePart(t, new Vector3(0f, 0.142f, 0f), new Vector3(0.08f, 0.04f, 0.08f), fm);                        // the smoothie dome
            var straw = BeachLayout.CylPart(t, new Vector3(0.015f, 0.19f, 0f), new Vector3(0.008f, 0.06f, 0.008f), Mat.Lit(new Color(1f, 0.5f, 0.7f), 0.4f));
            straw.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            bool cream = _held.Tops.Contains(3);
            float lift = cream ? 0.03f : 0f; // on top of the whipped cream
            if (cream) BeachLayout.SpherePart(t, new Vector3(0f, 0.16f, 0f), new Vector3(0.065f, 0.045f, 0.065f), Mat.Lit(ToppingColors[3], 0.3f));
            if (_held.Tops.Contains(9)) BeachLayout.SpherePart(t, new Vector3(0f, 0.158f + lift, 0f), new Vector3(0.06f, 0.012f, 0.06f), Mat.Lit(ToppingColors[9], 0.8f)); // caramel
            if (_held.Tops.Contains(0)) BeachLayout.SpherePart(t, new Vector3(-0.012f, 0.172f + lift, 0f), Vector3.one * 0.022f, Mat.Lit(ToppingColors[0], 0.7f));
            if (_held.Tops.Contains(1)) RimSlice(t, -1f, ToppingColors[1]);
            if (_held.Tops.Contains(7)) RimSlice(t, 1f, ToppingColors[7]);
            if (_held.Tops.Contains(2))
            {
                BeachLayout.CylPart(t, new Vector3(-0.02f, 0.19f + lift, -0.01f), new Vector3(0.004f, 0.05f, 0.004f), Mat.Lit(new Color(0.9f, 0.85f, 0.7f), 0.2f));
                BeachLayout.CylPart(t, new Vector3(-0.02f, 0.24f + lift, -0.01f), new Vector3(0.07f, 0.005f, 0.07f), Mat.Lit(ToppingColors[2], 0.3f));
            }
            if (_held.Tops.Contains(6)) BeachLayout.SpherePart(t, new Vector3(0.015f, 0.168f + lift, 0.012f), new Vector3(0.03f, 0.005f, 0.018f), Mat.Lit(ToppingColors[6], 0.4f));
            var r = new System.Random(5);
            if (_held.Tops.Contains(4))
                for (int k = 0; k < 10; k++)
                    BeachLayout.Part(t, new Vector3((float)r.NextDouble() * 0.05f - 0.025f, 0.165f + lift, (float)r.NextDouble() * 0.05f - 0.025f),
                        new Vector3(0.008f, 0.003f, 0.003f), Mat.Lit(Color.HSVToRGB((float)r.NextDouble(), 0.7f, 1f), 0.5f));
            if (_held.Tops.Contains(5))
                for (int k = 0; k < 7; k++)
                    BeachLayout.Part(t, new Vector3((float)r.NextDouble() * 0.05f - 0.025f, 0.166f + lift, (float)r.NextDouble() * 0.05f - 0.025f), Vector3.one * 0.006f, Mat.Lit(ToppingColors[5], 0.3f));
            if (_held.Tops.Contains(8))
                for (int k = 0; k < 3; k++)
                    BeachLayout.SpherePart(t, new Vector3(-0.02f + k * 0.02f, 0.17f + lift, 0.018f), new Vector3(0.012f, 0.016f, 0.01f), Mat.Lit(Color.HSVToRGB(k * 0.3f, 0.8f, 1f), 0.6f));
        }

        // ── Cups ─────────────────────────────────────────────────────────────
        private readonly List<Renderer> _stackCups = new List<Renderer>();
        private static Mesh _cupMesh;
        private static Material _cupPlain, _cupFancy;

        private static Material CupMaterial()
        {
            if (BeachUpgrades.Has("FANCY"))
            {
                if (_cupFancy == null) { _cupFancy = Mat.Lit(Color.white, 0.55f); _cupFancy.mainTexture = CupTexture(true); }
                return _cupFancy;
            }
            if (_cupPlain == null) { _cupPlain = Mat.Lit(Color.white, 0.35f); _cupPlain.mainTexture = CupTexture(false); }
            return _cupPlain;
        }

        private static Renderer CupModel(Transform parent, Vector3 local, float scale)
        {
            if (_cupMesh == null) _cupMesh = CupMesh();
            var go = new GameObject("Cup");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = _cupMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = CupMaterial();
            return r;
        }

        /// <summary>A paper cup: tapered, 14 cm tall, a rolled rim, open at the top (seen from inside too).</summary>
        private static Mesh CupMesh()
        {
            const int SEG = 24;
            const float RB = 0.034f, RT = 0.047f, H = 0.14f, RIM = 0.0035f;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            // Outside and inside walls (the inside is the same wall facing in).
            for (int side = 0; side < 2; side++)
            {
                int start = v.Count;
                for (int i = 0; i <= SEG; i++)
                {
                    float a = i / (float)SEG * Mathf.PI * 2f, c = Mathf.Cos(a), s = Mathf.Sin(a);
                    float shrink = side == 0 ? 0f : 0.0015f;
                    v.Add(new Vector3(c * (RB - shrink), 0f, s * (RB - shrink))); uv.Add(new Vector2(i / (float)SEG, 0f));
                    v.Add(new Vector3(c * (RT - shrink), H, s * (RT - shrink))); uv.Add(new Vector2(i / (float)SEG, side == 0 ? 1f : 0.02f));
                }
                for (int i = 0; i < SEG; i++)
                {
                    int k = start + i * 2;
                    if (side == 0) { tri.Add(k); tri.Add(k + 1); tri.Add(k + 2); tri.Add(k + 2); tri.Add(k + 1); tri.Add(k + 3); }
                    else { tri.Add(k); tri.Add(k + 2); tri.Add(k + 1); tri.Add(k + 2); tri.Add(k + 3); tri.Add(k + 1); }
                }
            }
            // Bottom.
            int cb = v.Count;
            v.Add(new Vector3(0f, 0.004f, 0f)); uv.Add(new Vector2(0.5f, 0.02f));
            for (int i = 0; i <= SEG; i++)
            {
                float a = i / (float)SEG * Mathf.PI * 2f;
                v.Add(new Vector3(Mathf.Cos(a) * RB, 0.004f, Mathf.Sin(a) * RB)); uv.Add(new Vector2(i / (float)SEG, 0.02f));
            }
            for (int i = 0; i < SEG; i++) { tri.Add(cb); tri.Add(cb + i + 2); tri.Add(cb + i + 1); tri.Add(cb); tri.Add(cb + i + 1); tri.Add(cb + i + 2); }
            // The rolled rim: a little ring sticking out at the top.
            int rs = v.Count;
            for (int i = 0; i <= SEG; i++)
            {
                float a = i / (float)SEG * Mathf.PI * 2f, c = Mathf.Cos(a), s = Mathf.Sin(a);
                v.Add(new Vector3(c * RT, H - RIM, s * RT)); uv.Add(new Vector2(i / (float)SEG, 0.99f));
                v.Add(new Vector3(c * (RT + RIM), H, s * (RT + RIM))); uv.Add(new Vector2(i / (float)SEG, 0.99f));
                v.Add(new Vector3(c * RT, H + RIM * 0.6f, s * RT)); uv.Add(new Vector2(i / (float)SEG, 0.99f));
            }
            for (int i = 0; i < SEG; i++)
            {
                int k = rs + i * 3;
                for (int j = 0; j < 2; j++) { tri.Add(k + j); tri.Add(k + j + 1); tri.Add(k + j + 3); tri.Add(k + j + 3); tri.Add(k + j + 1); tri.Add(k + j + 4); }
            }
            var m = new Mesh { name = "PaperCup" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>The print: plain cups are white with a teal band and pink wave; Fancy Cups are candy-striped with a gold band.</summary>
        private static Texture2D CupTexture(bool fancy)
        {
            const int W = 128, H = 64;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat };
            Color white = new Color(0.97f, 0.96f, 0.93f), teal = new Color(0.25f, 0.72f, 0.74f), pink = new Color(1f, 0.45f, 0.65f), gold = new Color(0.95f, 0.75f, 0.3f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float v = y / (float)H, u = x / (float)W;
                    Color c;
                    if (fancy)
                    {
                        bool stripe = Mathf.Repeat(u * 12f + v * 2.5f, 1f) < 0.5f;       // diagonal candy stripes
                        c = stripe ? pink : white;
                        if (v > 0.78f && v < 0.9f) c = gold;                                  // gold band
                        if ((v > 0.76f && v < 0.78f) || (v > 0.9f && v < 0.92f)) c = white;
                    }
                    else
                    {
                        c = white;
                        if (v > 0.35f && v < 0.62f) c = teal;                                 // the band
                        float wave = 0.485f + Mathf.Sin(u * Mathf.PI * 2f * 8f) * 0.05f;
                        if (Mathf.Abs(v - wave) < 0.025f) c = pink;                          // a pink wave through it
                        if (v < 0.04f) c = white * 0.92f;
                    }
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return tex;
        }

        private static void RimSlice(Transform cup, float side, Color c)
        {
            var slice = BeachLayout.CylPart(cup, new Vector3(0.035f * side, 0.15f, 0.01f), new Vector3(0.035f, 0.003f, 0.035f), Mat.Lit(c, 0.5f));
            slice.transform.localRotation = Quaternion.Euler(0f, 0f, 75f * -side);
        }

        private static string FruitText(Drink d)
        {
            var parts = new string[d.Fruit.Count];
            for (int i = 0; i < parts.Length; i++) parts[i] = Fruits[d.Fruit[i]];
            return string.Join("-", parts);
        }

        private static string ToppingText(Drink d)
        {
            var parts = new string[d.Tops.Count];
            for (int i = 0; i < parts.Length; i++) parts[i] = Toppings[d.Tops[i]];
            return string.Join(", ", parts);
        }

        private string Describe(Drink c)
        {
            string size = HasSizes(FeatureDay) ? Sizes[c.Size].ToLower() + " " : "";
            if (c.Fruit.Count == 0) return size + "empty cup" + (c.Ice ? " with ice" : "");
            string s = size + FruitText(c) + (c.Ice ? " + ice" : "") + (c.Blended ? " smoothie (" + ZoneLabel(c.BlendT) + ")" : " (not blended yet)");
            if (c.Tops.Count > 0) s += " + " + ToppingText(c);
            return s;
        }

        private string TicketLines(Drink w, bool colored)
        {
            string fruit = colored ? "<color=#" + Hex(MixColor(w) * 0.75f) + "><b>" + FruitText(w) + "</b></color>" : FruitText(w);
            string line2 = (HasSizes(FeatureDay) ? Sizes[w.Size] + " · " : "") + fruit;
            string line3 = "";
            if (HasIce(FeatureDay)) line3 = w.Ice ? "ICE" : "no ice";
            if (HasTextures(FeatureDay)) line3 += (line3.Length > 0 ? " · " : "") + (colored ? "<color=" + TextureInk[w.Texture] + "><b>" + Textures[w.Texture] + "</b></color>" : Textures[w.Texture]);
            return line2 + "\n" + (line3.Length > 0 ? line3 + "\n" : "") + "+ " + ToppingText(w);
        }

        // ── HUD ──────────────────────────────────────────────────────────────
        private Text _status, _heldText, _hint, _card, _coverDay, _coverNew, _coverLoading;
        private GameObject _cardBox;
        private CanvasGroup _cover;
        private float _hintUntil, _cardUntil;
        private readonly GameObject[] _ticket = new GameObject[3];
        private readonly Text[] _ticketText = new Text[3];
        private readonly RectTransform[] _ticketFill = new RectTransform[3];
        private readonly Image[] _ticketFillImg = new Image[3];

        private void BuildHud()
        {
            var ct = NewCanvas("KE_BeachHud", 880).transform;

            _status = MakeText(ct, new Vector2(0.3f, 0.93f), new Vector2(0.7f, 0.99f), 30, TextAnchor.UpperCenter, Color.white);
            _heldText = MakeText(ct, new Vector2(0.15f, 0.03f), new Vector2(0.85f, 0.08f), 28, TextAnchor.LowerCenter, new Color(1f, 0.95f, 0.8f));
            _hint = MakeText(ct, new Vector2(0.2f, 0.1f), new Vector2(0.8f, 0.16f), 30, TextAnchor.LowerCenter, new Color(1f, 0.85f, 0.5f));

            _cardBox = MakeBox(ct, new Vector2(0.33f, 0.48f), new Vector2(0.67f, 0.84f), new Color(0.08f, 0.06f, 0.1f, 0.82f));
            _card = MakeText(_cardBox.transform, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f), 32, TextAnchor.MiddleCenter, Color.white);
            _cardBox.SetActive(false);
            BuildReceipt(ct);

            for (int i = 0; i < 3; i++)
            {
                // A paper order slip: ruled lines, a red margin, a torn bottom edge, pinned up with a clip, a bit crooked.
                float top = 0.86f - i * 0.21f;
                _ticket[i] = MakeBox(ct, new Vector2(0.785f, top - 0.19f), new Vector2(0.985f, top), Color.white);
                var paper = _ticket[i].GetComponent<Image>();
                paper.sprite = PaperSprite();
                var sh = _ticket[i].AddComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.35f);
                sh.effectDistance = new Vector2(5f, -5f);
                _ticket[i].transform.localRotation = Quaternion.Euler(0f, 0f, i == 1 ? -1.6f : 1.3f);
                MakeBox(_ticket[i].transform, new Vector2(0.42f, 0.93f), new Vector2(0.58f, 1.04f), new Color(0.62f, 0.45f, 0.28f)); // the clip
                _ticketText[i] = MakeText(_ticket[i].transform, new Vector2(0.12f, 0.2f), new Vector2(0.97f, 0.9f), 21, TextAnchor.UpperLeft, new Color(0.12f, 0.12f, 0.3f));
                _ticketText[i].GetComponent<Outline>().enabled = false;
                _ticketText[i].fontStyle = FontStyle.Italic; // like it's written by hand
                var bg = MakeBox(_ticket[i].transform, new Vector2(0.12f, 0.09f), new Vector2(0.92f, 0.15f), new Color(0.3f, 0.25f, 0.22f, 0.35f));
                var fill = MakeBox(bg.transform, Vector2.zero, Vector2.one, Color.green);
                _ticketFill[i] = (RectTransform)fill.transform;
                _ticketFillImg[i] = fill.GetComponent<Image>();
                _ticket[i].SetActive(false);
            }

            // The DAY N screen (the bell).
            var cover = NewCanvas("KE_BeachDayCover", 31000);
            _cover = cover.AddComponent<CanvasGroup>();
            _cover.blocksRaycasts = false;
            MakeBox(cover.transform, Vector2.zero, Vector2.one, new Color(0.04f, 0.03f, 0.06f, 1f));
            _coverDay = MakeText(cover.transform, new Vector2(0f, 0.55f), new Vector2(1f, 0.75f), 150, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.3f));
            _coverNew = MakeText(cover.transform, new Vector2(0.12f, 0.25f), new Vector2(0.88f, 0.53f), 40, TextAnchor.UpperCenter, Color.white);
            _coverLoading = MakeText(cover.transform, new Vector2(0f, 0.08f), new Vector2(1f, 0.16f), 30, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.6f));
            cover.SetActive(false);
        }

        private GameObject NewCanvas(string name, int order)
        {
            var cgo = new GameObject(name);
            cgo.transform.SetParent(transform, false);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            return cgo;
        }

        private void TickHud()
        {
            if (_status == null) return;
            if (TrailerMode)
            {
                // Only the receipt shows in the trailer.
                _status.text = _heldText.text = _hint.text = "";
                _cardBox.SetActive(false);
                foreach (var t in _ticket) if (t.activeSelf) t.SetActive(false);
                TickReceipt();
                return;
            }
            _status.text = _phase == Phase.Open
                ? "DAY " + _day + "   ·   Customers " + (_served + _angry) + "/" + DayCustomers(_day) + "   ·   Tips $" + _tips
                : "Cash $" + BeachUpgrades.Cash;
            _heldText.text = _held != null ? "Holding: " + Describe(_held) : "";
            if (Time.time > _hintUntil) _hint.text = "";
            if (_cardBox.activeSelf && Time.time > _cardUntil) _cardBox.SetActive(false);
            TickReceipt();

            if (_demoTicket != null)
            {
                if (!_ticket[0].activeSelf) _ticket[0].SetActive(true);
                _ticketText[0].text = _demoTicket;
                _ticketFill[0].anchorMax = new Vector2(0.8f, 1f);
                _ticketFillImg[0].color = new Color(0.3f, 0.85f, 0.35f);
                return;
            }
            for (int i = 0; i < 3; i++)
            {
                var c = _stool[i];
                bool show = c != null && (c.State == CState.ToStool || c.State == CState.Seated);
                if (_ticket[i].activeSelf != show) _ticket[i].SetActive(show);
                if (!show) continue;
                var mood = Moods[(int)c.Mood];
                string who = c.Mood == Mood.Inspector ? "<color=" + mood.Ink + "><b>** THE INSPECTOR **</b></color>"
                    : c.Name.ToUpper() + (c.Mood != Mood.Normal ? "  <color=" + mood.Ink + "><b>" + mood.Tag + "</b></color>" : "");
                _ticketText[i].text = "<b>ORDER #" + c.Number + "</b>  ·  seat " + (i + 1) + "\n<size=17>" + who + "</size>\n" + TicketLines(c.Want, true);
                float left = Mathf.Clamp01(1f - (Time.time - c.OrderedAt) / c.Patience);
                _ticketFill[i].anchorMax = new Vector2(left, 1f);
                _ticketFillImg[i].color = left > 0.5f ? new Color(0.3f, 0.85f, 0.35f) : left > 0.25f ? new Color(1f, 0.8f, 0.2f) : new Color(0.95f, 0.25f, 0.2f);
            }
        }

        private void Hint(string text) { if (_hint != null) { _hint.text = text; _hintUntil = Time.time + 2.2f; } }

        // ── The tutorial (the first time day 1 starts) ───────────────────────
        // A tour of the bar with everything unlocked, a pin over each place, then day 1 with the basics.
        private const string TUTORIAL_KEY = "Beach Tutorial Done";
        private const float TUTORIAL_STEP = 14f; // seconds before a step moves on by itself
        private bool TutorialDone { get { return Shop.Get(TUTORIAL_KEY) > 0; } }
        private int FeatureDay { get { return _showAll ? 99 : _day; } }
        private bool _showAll;
        private string _demoTicket;
        private int _tutStep;
        private float _tutT;
        private Transform _pin;
        private Text _tutText;
        private GameObject _tutBox;

        private class TutStep { public string Text; public Vector3 Pin; public bool HasPin, Ticket; }
        private List<TutStep> _tut;

        private List<TutStep> TutorialSteps()
        {
            float ft = BeachLayout.FLOOR_TOP, ky = ft + BeachLayout.KITCHEN_TOP, by = ft + BeachLayout.BAR_TOP;
            var s = new List<TutStep>();
            s.Add(new TutStep { Text = "<b>Welcome to Mama's Smoothie Bar!</b>\nCustomers come in through the door, line up here and order at the counter. Then they wait on a stool.", Pin = new Vector3(LANE_X, ft + 1.4f, QUEUE_Z0), HasPin = true });
            s.Add(new TutStep { Text = "<b>Orders</b> come in on paper slips (right side of the screen): the cup size, the fruit, ice, the texture and the toppings. The bar at the bottom is their patience.", Ticket = true });
            s.Add(new TutStep { Text = "<b>1. CUPS</b>\nTake a cup of the right size: SMALL, MEDIUM or LARGE.", Pin = new Vector3(13.83f, ky + 0.6f, BeachLayout.PREP_Z), HasPin = true, Ticket = true });
            s.Add(new TutStep { Text = "<b>2. FRUIT</b>\nThe fruit is on the kitchen counter. You start with 3; a new one comes every day, up to all 10 you see here. Mixes want more than one.", Pin = new Vector3(BeachLayout.KITCHEN_X, ky + 0.7f, -9.13f), HasPin = true, Ticket = true });
            s.Add(new TutStep { Text = "<b>3. ICE</b>\nSome orders want ice (from day 3). Add it before blending.", Pin = new Vector3(14.45f, ky + 0.75f, BeachLayout.PREP_Z), HasPin = true, Ticket = true });
            s.Add(new TutStep { Text = "<b>4. BLENDER</b>\nPut the cup in and take it out when the meter is in the right zone. It keeps blending if you walk away! Mama sells up to three more blenders.", Pin = new Vector3(15.39f, ky + 0.9f, BeachLayout.PREP_Z), HasPin = true, Ticket = true });
            s.Add(new TutStep { Text = "<b>5. TOPPINGS</b>\nThe topping counter by the door. 3 to start, one more every day, up to all 10.", Pin = new Vector3(BeachLayout.TOPPING_X, ky + 0.7f, (BeachLayout.TOPPING_Z0 + BeachLayout.TOPPING_Z1) / 2f), HasPin = true, Ticket = true });
            s.Add(new TutStep { Text = "<b>6. SERVE</b>\nHand it over at the customer's seat on the bar. Every order is scored (order, blend, wait) and tips cash into the register.", Pin = new Vector3(BeachLayout.StoolX[1], by + 0.5f, BeachLayout.COUNTER_Z + 0.12f), HasPin = true });
            s.Add(new TutStep { Text = "<b>Oops?</b> Throw a cup away in the trash chute. The <b>bell</b> next to it opens the next day.", Pin = new Vector3(14.05f, by + 0.45f, BeachLayout.COUNTER_Z - 0.14f), HasPin = true });
            s.Add(new TutStep { Text = "<b>Mama</b> judges every day. 80% or better: she tips you. Under 50%... you'll find out, and you do the day again.\nBetween days, spend your tips at her board and move your decor around.", Pin = new Vector3(13.65f, ft + 2.15f, -1.45f), HasPin = true });
            s.Add(new TutStep { Text = "<b>Now you try!</b>\nA customer at seat 1 wants the SPECIAL: the biggest order there is. Follow the arrow." });
            return s;
        }

        private void StartTutorial()
        {
            _phase = Phase.Tutorial;
            _showAll = true;
            RefreshStations();
            _tut = TutorialSteps();
            _tutStep = -1;
            _music.PlayWork();
            HubMode.SetHudText("Mama's Smoothie Bar: the tour");
            if (_tutBox == null)
            {
                var ct = _status.transform.parent;
                // A small paper note, top left (the order slips are on the right).
                _tutBox = MakeBox(ct, new Vector2(0.015f, 0.6f), new Vector2(0.235f, 0.9f), Color.white);
                _tutBox.GetComponent<Image>().sprite = PaperSprite();
                var tsh = _tutBox.AddComponent<Shadow>();
                tsh.effectColor = new Color(0f, 0f, 0f, 0.35f);
                tsh.effectDistance = new Vector2(5f, -5f);
                _tutBox.transform.localRotation = Quaternion.Euler(0f, 0f, 1.5f);
                MakeBox(_tutBox.transform, new Vector2(0.42f, 0.95f), new Vector2(0.58f, 1.04f), new Color(0.62f, 0.45f, 0.28f)); // the clip
                _tutText = MakeText(_tutBox.transform, new Vector2(0.13f, 0.2f), new Vector2(0.95f, 0.9f), 19, TextAnchor.UpperLeft, new Color(0.12f, 0.12f, 0.3f));
                _tutText.GetComponent<Outline>().enabled = false;
                _tutText.fontStyle = FontStyle.Italic;
                var keys = MakeText(_tutBox.transform, new Vector2(0.13f, 0.07f), new Vector2(0.95f, 0.18f), 14, TextAnchor.LowerLeft, new Color(0.7f, 0.25f, 0.25f));
                keys.GetComponent<Outline>().enabled = false;
                keys.text = "ENTER: next  ·  BACKSPACE: skip";
            }
            if (_pin == null) _pin = BuildPin();
            _tutBox.SetActive(true);
            NextTutorialStep();
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Beach] Tutorial started.");
        }

        private void NextTutorialStep()
        {
            _tutStep++;
            _tutT = 0f;
            if (_tutStep >= _tut.Count) { StartPractice(); return; }
            var st = _tut[_tutStep];
            _tutText.text = st.Text;
            _pin.gameObject.SetActive(st.HasPin);
            if (st.HasPin) _pin.localPosition = st.Pin;
            _demoTicket = st.Ticket
                ? "<b>ORDER #1</b>  ·  seat 1\n<size=17>PURPLE TATTLETAIL</size>\nLARGE · <color=#B8452E><b>Strawberry-Banana</b></color>\nICE · <color=#1E78C8><b>EXTRA SMOOTH</b></color>\n+ Cherry, Whipped cream, Sprinkles"
                : null;
            if (!st.Ticket) _ticket[0].SetActive(false);
            Ding(RM.fpsController.transform.position);
        }

        private void TickTutorial(float dt)
        {
            _tutT += dt;
            if (_practice == null && _pin != null && _pin.gameObject.activeSelf)
            {
                var st = _tut[_tutStep];
                _pin.localPosition = st.Pin + Vector3.up * Mathf.Abs(Mathf.Sin(_tutT * 3f)) * 0.12f; // bouncing
                _pin.Rotate(0f, 120f * dt, 0f);
            }
            if (OverlayPanel.AnyOpen) return;
            if (Input.GetKeyDown(KeyCode.Backspace)) { EndTutorial(); return; }
            if (_practice != null) { TickPractice(dt); return; }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || _tutT > TUTORIAL_STEP) NextTutorialStep();
        }

        // ── The practice order: the arrow shows the next thing to do ─────────
        private Customer _practice;
        private float _practiceEnd;

        private void StartPractice()
        {
            _demoTicket = null;
            _ticket[0].SetActive(false);
            var w = new Drink { Size = 2, Ice = true, Texture = 2 };
            w.Fruit.Add(0); w.Fruit.Add(1);
            w.Tops.Add(0); w.Tops.Add(3); w.Tops.Add(4);
            Vector3 seat = new Vector3(BeachLayout.StoolX[0], 0f, StoolZ);
            string color;
            var model = PartyTattletailModels.BuildRandomColored(_root.TransformPoint(seat), _root.rotation, _root, out color);
            if (model == null) { EndTutorial(); return; }
            _practice = new Customer
            {
                Model = model, Color = color ?? "Tattletail", Number = 1, State = CState.Seated, SitT = 1f, Pos = seat, Yaw = 180f,
                Stool = 0, Want = w, OrderedAt = Time.time, Patience = 100000f, Hurried = true, Practice = true, Name = "Mochi",
            };
            _customers.Add(_practice);
            _stool[0] = _practice;
            _practiceEnd = -1f;
            Say(_practice, "I'll have the SPECIAL!", 5f);
        }

        private void TickPractice(float dt)
        {
            if (_practice.State == CState.Leaving)
            {
                // Served: let the score card show, then day 1.
                if (_practiceEnd < 0f) { _practiceEnd = 4f; _tutText.text = "<b>You did it!</b>\nDay 1 starts with just the basics. Good luck!"; _pin.gameObject.SetActive(false); }
                _practiceEnd -= dt;
                if (_practiceEnd <= 0f) EndTutorial();
                return;
            }
            string text;
            Vector3 at;
            PracticeHint(out text, out at);
            _tutText.text = "<b>MAKE THE SPECIAL</b>  (seat 1)\n" + text;
            _pin.gameObject.SetActive(true);
            _pin.localPosition = at + Vector3.up * Mathf.Abs(Mathf.Sin(_tutT * 3f)) * 0.12f;
            _pin.Rotate(0f, 120f * dt, 0f);
        }

        /// <summary>What to do next for the practice order, and where.</summary>
        private void PracticeHint(out string text, out Vector3 at)
        {
            Drink w = _practice.Want, h = _held;
            var blender = _blenders[0];
            foreach (var bl in _blenders) if (bl != null && bl.Cup != null) { blender = bl; break; } // whichever one they used
            const float ABOVE = 0.55f;
            if (blender.Cup != null)
            {
                at = blender.T.localPosition + Vector3.up * 0.95f;
                float t = blender.Progress;
                bool inZone = Mathf.Abs(t - TextureIdeal[w.Texture]) <= BLEND_WINDOW;
                text = inZone ? "<color=#5CD6FF><b>NOW! Take it out!</b></color> It's EXTRA SMOOTH."
                    : t > TextureIdeal[w.Texture] ? "Too long! Take it out anyway (it'll score lower)."
                    : "Blending... wait for <color=#5CD6FF><b>EXTRA SMOOTH</b></color> (the blue zone on the meter).";
                return;
            }
            if (h == null)
            {
                at = _cupStacks[w.Size].transform.localPosition + Vector3.up * ABOVE;
                text = "Take a <b>LARGE</b> cup.";
                return;
            }
            bool wrong = h.Size != w.Size;
            foreach (int f in h.Fruit) if (!w.Fruit.Contains(f)) wrong = true;
            foreach (int t in h.Tops) if (!w.Tops.Contains(t)) wrong = true;
            if (h.Blended && !h.Ice) wrong = true;
            if (wrong)
            {
                at = _trashT.localPosition + Vector3.up * 0.4f;
                text = "Oops, that's not right. Throw it in the <b>trash</b> and start again.";
                return;
            }
            if (!h.Blended)
            {
                foreach (int f in w.Fruit)
                    if (!h.Fruit.Contains(f)) { at = _fruitBins[f].transform.localPosition + Vector3.up * ABOVE; text = "Add <b>" + Fruits[f].ToLower() + "</b>."; return; }
                if (w.Ice && !h.Ice) { at = _iceMachine.transform.localPosition + Vector3.up * 0.75f; text = "Add <b>ice</b> (before blending!)."; return; }
                at = blender.T.localPosition + Vector3.up * 0.95f;
                text = "Put it in the <b>blender</b>.";
                return;
            }
            foreach (int t in w.Tops)
                if (!h.Tops.Contains(t)) { at = _toppingBowls[t].transform.localPosition + Vector3.up * ABOVE; text = "Add <b>" + Toppings[t].ToLower() + "</b>."; return; }
            at = _serveT[0].localPosition + Vector3.up * 0.45f;
            text = "Perfect! <b>Serve it</b> at seat 1.";
        }

        private void EndTutorial()
        {
            if (_practice != null && _practice.State != CState.Leaving) { DropCustomer(_practice); _customers.Remove(_practice); }
            _practice = null;
            Shop.SetValue(TUTORIAL_KEY, 1);
            _showAll = false;
            _demoTicket = null;
            _ticket[0].SetActive(false);
            if (_tutBox != null) _tutBox.SetActive(false);
            if (_pin != null) _pin.gameObject.SetActive(false);
            RefreshStations();
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Beach] Tutorial done.");
            _music.PlayDay();
            StartDay();
            ShowCard("<size=54><b>DAY " + _day + "</b></size>\n\n" + NewToday(_day), 6f);
        }

        /// <summary>A four-sided point, tip at the origin, 0.14 tall: the arrow's head.</summary>
        private static Mesh ArrowHead()
        {
            const float W = 0.09f, H = 0.14f;
            Vector3 tip = Vector3.zero;
            Vector3[] c = { new Vector3(-W, H, -W), new Vector3(W, H, -W), new Vector3(W, H, W), new Vector3(-W, H, W) };
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                int k = v.Count;
                v.Add(tip); v.Add(c[i]); v.Add(c[(i + 1) % 4]);
                t.Add(k); t.Add(k + 2); t.Add(k + 1); t.Add(k); t.Add(k + 1); t.Add(k + 2); // both sides
            }
            int b = v.Count;
            v.AddRange(c);
            t.Add(b); t.Add(b + 1); t.Add(b + 2); t.Add(b); t.Add(b + 2); t.Add(b + 3);
            var m = new Mesh { name = "ArrowHead" };
            m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>A glowing arrow that bounces over what the tutorial is talking about.</summary>
        private Transform BuildPin()
        {
            var pin = new GameObject("KE_TutorialPin").transform;
            pin.SetParent(_root, false);
            var glow = Mat.Unlit(new Color(1f, 0.8f, 0.2f));
            BeachLayout.CylPart(pin, new Vector3(0f, 0.28f, 0f), new Vector3(0.05f, 0.14f, 0.05f), glow);   // shaft
            var head = new GameObject("Head");
            head.transform.SetParent(pin, false);
            head.AddComponent<MeshFilter>().sharedMesh = ArrowHead();
            head.AddComponent<MeshRenderer>().sharedMaterial = glow;
            return pin;
        }

        // ── The receipt: slides up, then the grade stamp slaps onto it with a splash of color ──
        private const float RC_IN = 0.28f, RC_STAMP = 0.5f, RC_SLAP = 0.12f, RC_HOLD = 4.2f, RC_OUT = 0.35f;
        private RectTransform _receipt, _stamp, _splashRT;
        private CanvasGroup _receiptGroup;
        private Text _receiptText, _stampText;
        private Image _splash;
        private float _receiptT = -1f;
        private bool _slapped;
        private static Sprite _splat;
        private static AudioClip _thud;

        private void BuildReceipt(Transform ct)
        {
            var go = MakeBox(ct, new Vector2(0.39f, 0.4f), new Vector2(0.61f, 0.86f), Color.white);
            _receipt = (RectTransform)go.transform;
            go.GetComponent<Image>().sprite = PaperSprite();
            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.4f);
            sh.effectDistance = new Vector2(7f, -7f);
            _receiptGroup = go.AddComponent<CanvasGroup>();
            _receiptText = MakeText(_receipt, new Vector2(0.14f, 0.34f), new Vector2(0.95f, 0.93f), 25, TextAnchor.UpperLeft, new Color(0.12f, 0.12f, 0.3f));
            _receiptText.GetComponent<Outline>().enabled = false;
            _receiptText.fontStyle = FontStyle.Italic;

            // The stamp: a paint splash with the grade on top, near the bottom of the paper.
            var stamp = new GameObject("Stamp", typeof(RectTransform));
            stamp.transform.SetParent(_receipt, false);
            _stamp = (RectTransform)stamp.transform;
            _stamp.anchorMin = new Vector2(0.05f, 0.02f); _stamp.anchorMax = new Vector2(0.97f, 0.34f);
            _stamp.offsetMin = _stamp.offsetMax = Vector2.zero;
            var splash = MakeBox(_stamp, new Vector2(0.08f, -0.25f), new Vector2(0.92f, 1.25f), Color.white);
            _splashRT = (RectTransform)splash.transform;
            _splash = splash.GetComponent<Image>();
            _splash.sprite = SplatSprite();
            _stampText = MakeText(_stamp, Vector2.zero, Vector2.one, 50, TextAnchor.MiddleCenter, Color.white);
            _stampText.fontStyle = FontStyle.Bold;
            var o = _stampText.GetComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.9f);
            o.effectDistance = new Vector2(3f, -3f);
            go.SetActive(false);
        }

        private void ShowReceipt(string body, string grade, Color color)
        {
            if (_receipt == null) return;
            _receiptText.text = body;
            _stampText.text = grade;
            _splash.color = color;
            _receiptT = 0f;
            _slapped = false;
            _receipt.gameObject.SetActive(true);
            _receipt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-2.5f, 2.5f));
            TickReceipt();
        }

        private void TickReceipt()
        {
            if (_receipt == null || _receiptT < 0f) return;
            _receiptT += Time.deltaTime;
            float t = _receiptT;

            // The paper slides up, sits, then drops away.
            float y = 0f;
            if (t < RC_IN) { float k = 1f - t / RC_IN; y = -600f * k * k; }
            else if (t > RC_HOLD) y = -700f * Mathf.Clamp01((t - RC_HOLD) / RC_OUT);
            _receiptGroup.alpha = t > RC_HOLD ? 1f - Mathf.Clamp01((t - RC_HOLD) / RC_OUT) : 1f;

            // The stamp comes down from big and slaps on; the paper jolts; the paint splashes out.
            float s = t - RC_STAMP;
            if (s < 0f) { _stamp.gameObject.SetActive(false); }
            else
            {
                _stamp.gameObject.SetActive(true);
                float fall = Mathf.Clamp01(s / RC_SLAP);
                float scale = s < RC_SLAP ? Mathf.Lerp(2.8f, 0.92f, fall * fall) : Mathf.Lerp(0.92f, 1f, Mathf.Clamp01((s - RC_SLAP) / 0.08f));
                _stamp.localScale = Vector3.one * scale;
                _stamp.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-28f, -9f, fall));
                _stampText.color = new Color(1f, 1f, 1f, Mathf.Clamp01(s / 0.05f));
                if (s < RC_SLAP) { _splashRT.localScale = Vector3.zero; }
                else
                {
                    if (!_slapped)
                    {
                        _slapped = true;
                        var cam = PlayerCamera();
                        if (cam != null) AudioSource.PlayClipAtPoint(Thud(), cam.transform.position, 1f);
                    }
                    float sp = s - RC_SLAP;
                    _splashRT.localScale = Vector3.one * (sp < 0.09f ? Mathf.Lerp(0.3f, 1.18f, sp / 0.09f) : Mathf.Lerp(1.18f, 1f, Mathf.Clamp01((sp - 0.09f) / 0.12f)));
                    if (sp < 0.2f) y += Random.Range(-7f, 7f) * (1f - sp / 0.2f); // the jolt
                }
            }
            _receipt.anchoredPosition = new Vector2(0f, y);
            if (t > RC_HOLD + RC_OUT) { _receipt.gameObject.SetActive(false); _receiptT = -1f; }
        }

        /// <summary>A paint splat: a lumpy blob with droplets thrown around it (white; the Image tints it).</summary>
        private static Sprite SplatSprite()
        {
            if (_splat != null) return _splat;
            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var r = new System.Random(21);
            var drops = new List<Vector3>(); // x, y, radius
            for (int i = 0; i < 14; i++)
            {
                float a = (float)r.NextDouble() * Mathf.PI * 2f, d = 42f + (float)r.NextDouble() * 18f;
                drops.Add(new Vector3(64f + Mathf.Cos(a) * d, 64f + Mathf.Sin(a) * d * 0.7f, 2f + (float)r.NextDouble() * 4.5f));
            }
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = x - 64f, dy = (y - 64f) / 0.7f, d = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx);
                    float edge = 34f + 7f * Mathf.Sin(5f * a + 1f) + 4f * Mathf.Sin(9f * a + 2f) + 3f * Mathf.Sin(14f * a);
                    float alpha = Mathf.Clamp01(edge - d + 1f);
                    foreach (var dr in drops)
                    {
                        float dd = Vector2.Distance(new Vector2(x, y), new Vector2(dr.x, dr.y));
                        alpha = Mathf.Max(alpha, Mathf.Clamp01(dr.z - dd + 1f));
                    }
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            tex.Apply();
            return _splat = Sprite.Create(tex, new Rect(0f, 0f, N, N), new Vector2(0.5f, 0.5f));
        }

        /// <summary>The stamp hitting the paper: a short, heavy thump with a slap on top.</summary>
        private static AudioClip Thud()
        {
            if (_thud != null) return _thud;
            const int SR = 22050, LEN = SR / 4;
            var d = new float[LEN];
            var r = new System.Random(6);
            for (int i = 0; i < LEN; i++)
            {
                float t = i / (float)SR;
                d[i] = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(140f, 55f, Mathf.Min(1f, t / 0.08f)) * t) * Mathf.Exp(-t * 22f) * 0.8f
                     + ((float)r.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 90f) * 0.5f;
            }
            _thud = AudioClip.Create("KE_StampThud", LEN, 1, SR, false);
            _thud.SetData(d, 0);
            return _thud;
        }

        private static Sprite _paper;

        /// <summary>Notepad paper: ruled lines, a red margin, a torn (zigzag) bottom edge.</summary>
        internal static Sprite PaperSprite()
        {
            if (_paper != null) return _paper;
            const int W = 128, H = 128;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var r = new System.Random(8);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float grain = (float)r.NextDouble() * 0.03f;
                    Color c = new Color(0.99f - grain, 0.97f - grain, 0.89f - grain, 1f);
                    if (y < H - 20 && y > 10 && (H - y) % 12 == 0) c = Color.Lerp(c, new Color(0.55f, 0.7f, 0.95f, 1f), 0.6f); // ruled lines
                    if (x == 14 || x == 15) c = Color.Lerp(c, new Color(0.95f, 0.4f, 0.45f, 1f), 0.7f);                    // margin
                    int tooth = 6 - Mathf.Abs((x % 12) - 6);                                                                  // torn bottom
                    if (y < tooth) c.a = 0f;
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            _paper = Sprite.Create(tex, new Rect(0f, 0f, W, H), new Vector2(0.5f, 0.5f));
            return _paper;
        }

        private void ShowCard(string text, float seconds)
        {
            if (_card == null) return;
            _card.text = text;
            _cardBox.SetActive(true);
            _cardUntil = Time.time + seconds;
        }

        private static string Hex(Color c)
        {
            return ((int)(Mathf.Clamp01(c.r) * 255)).ToString("X2") + ((int)(Mathf.Clamp01(c.g) * 255)).ToString("X2") + ((int)(Mathf.Clamp01(c.b) * 255)).ToString("X2");
        }

        private static GameObject MakeBox(Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject("Box", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return go;
        }

        private static Text MakeText(Transform parent, Vector2 min, Vector2 max, int size, TextAnchor align, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.font = MainMenuButtonInjector.RealFont != null ? MainMenuButtonInjector.RealFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var o = go.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.85f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        // ── Sounds (synthesized) ─────────────────────────────────────────────
        private static void Ding(Vector3 at)
        {
            if (_ding == null)
            {
                const int SR = 22050, LEN = SR * 7 / 10;
                var d = new float[LEN];
                for (int i = 0; i < LEN; i++)
                {
                    float t = i / (float)SR;
                    d[i] = (Mathf.Sin(t * 1318f * Mathf.PI * 2f) + 0.5f * Mathf.Sin(t * 1976f * Mathf.PI * 2f)) * Mathf.Exp(-t * 6f) * 0.5f;
                }
                _ding = AudioClip.Create("KE_Ding", LEN, 1, SR, false);
                _ding.SetData(d, 0);
            }
            AudioSource.PlayClipAtPoint(_ding, at, 0.8f);
        }

        /// <summary>Ka-ching: the keys ratchet, the drawer thunks open, the bell rings.</summary>
        private static AudioClip KaChing()
        {
            if (_kaChing != null) return _kaChing;
            const int SR = 22050, LEN = SR * 9 / 10;
            var d = new float[LEN];
            var r = new System.Random(9);
            const float TAU = Mathf.PI * 2f;
            for (int i = 0; i < LEN; i++)
            {
                float t = i / (float)SR, v = 0f;
                for (int k = 0; k < 4; k++)
                {
                    float tk = t - k * 0.035f;
                    if (tk >= 0f && tk < 0.012f) v += ((float)r.NextDouble() * 2f - 1f) * (1f - tk / 0.012f) * 0.6f;
                }
                float td = t - 0.14f;
                if (td >= 0f) v += Mathf.Sin(td * 110f * TAU) * Mathf.Exp(-td * 25f) * 0.5f;
                float tb = t - 0.16f;
                if (tb >= 0f) v += (Mathf.Sin(tb * 2637f * TAU) + 0.6f * Mathf.Sin(tb * 3951f * TAU) + 0.3f * Mathf.Sin(tb * 5274f * TAU)) * Mathf.Exp(-tb * 5f) * 0.35f;
                d[i] = Mathf.Clamp(v, -1f, 1f);
            }
            _kaChing = AudioClip.Create("KE_KaChing", LEN, 1, SR, false);
            _kaChing.SetData(d, 0);
            return _kaChing;
        }

        private static AudioClip Whirr()
        {
            if (_whirr != null) return _whirr;
            const int SR = 22050, LEN = SR; // one second, a whole number of motor cycles: loops cleanly
            var d = new float[LEN];
            var r = new System.Random(3);
            float lp = 0f;
            for (int i = 0; i < LEN; i++)
            {
                float t = i / (float)SR;
                float saw = (t * 96f) % 1f * 2f - 1f;
                lp += (((float)r.NextDouble() * 2f - 1f) - lp) * 0.3f;
                d[i] = (saw * 0.35f + lp * 0.5f) * (0.8f + 0.2f * Mathf.Sin(t * 32f * Mathf.PI * 2f));
            }
            _whirr = AudioClip.Create("KE_Blender", LEN, 1, SR, false);
            _whirr.SetData(d, 0);
            return _whirr;
        }
    }

    /// <summary>Mama's standing height on the bar floor (her walk bobs around it).</summary>
    public class MamaFloor : MonoBehaviour { public float Y; }

    /// <summary>A torch flame: wobbles and flickers its light.</summary>
    public class Flicker : MonoBehaviour
    {
        public Light Light;
        private float _seed;

        private void Start() { _seed = Random.value * 100f; }

        private void Update()
        {
            float n = Mathf.PerlinNoise(_seed, Time.time * 6f);
            transform.localScale = new Vector3(1f, 0.85f + n * 0.35f, 1f);
            if (Light != null) Light.intensity = 0.8f + n * 0.7f;
        }
    }

    // A finished interaction at the beach goes to the smoothie bar's stations.
    [HarmonyLib.HarmonyPatch(typeof(Interactable), "OnInteractComplete")]
    public static class Patch_Interactable_OnInteractComplete_Beach
    {
        static void Postfix(Interactable __instance, bool fromAutoComplete)
        {
            if (fromAutoComplete || !BeachMode.Active || __instance == null || OverlayPanel.BlocksWorld) return;
            BeachShift.OnInteract(__instance);
        }
    }
}
