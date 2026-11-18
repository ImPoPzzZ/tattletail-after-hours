using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Summer Vacation's bar: the day you're on, the cash in the register (tips) and Mama's upgrades,
    /// all kept in Shop.txt. Upgrades are bought at Mama's board between days.
    /// </summary>
    public static class BeachUpgrades
    {
        public class Upgrade
        {
            public string Id, Name, Desc, Requires;
            public int Price;
            public bool Decor;
        }

        public static readonly Upgrade[] All =
        {
            new Upgrade { Id = "TIPJAR",   Name = "Tip Jar",          Desc = "+20% tips",                                      Price = 75 },
            new Upgrade { Id = "ALARM",    Name = "Blender Alarm",    Desc = "the blender dings when it hits the right texture", Price = 90 },
            new Upgrade { Id = "CUSHIONS", Name = "Comfy Cushions",   Desc = "customers stay patient 20% longer",               Price = 110 },
            new Upgrade { Id = "PEANUTS",  Name = "Free Peanuts",     Desc = "20 more seconds before the wait score drops",     Price = 120 },
            new Upgrade { Id = "TURBO1",   Name = "Turbo Blender",    Desc = "blends 30% faster",                               Price = 150 },
            new Upgrade { Id = "FANCY",    Name = "Fancy Cups",       Desc = "+15% tips, and prettier cups",                    Price = 200 },
            new Upgrade { Id = "TURBO2",   Name = "Turbo Blender II", Desc = "blends 60% faster",                               Price = 300, Requires = "TURBO1" },
            new Upgrade { Id = "BLENDER2", Name = "Second Blender",   Desc = "blend two smoothies at once",                     Price = 350 },
            new Upgrade { Id = "BLENDER3", Name = "Third Blender",    Desc = "three at once",                                   Price = 550, Requires = "BLENDER2" },
            new Upgrade { Id = "BLENDER4", Name = "Fourth Blender",   Desc = "four at once: blender maniac",                    Price = 800, Requires = "BLENDER3" },
            new Upgrade { Id = "FLAMINGO", Name = "Pink Flamingo",    Desc = "decor: waiting customers get distracted (+5% patience)", Price = 50, Decor = true },
            new Upgrade { Id = "LIGHTS",   Name = "String Lights",    Desc = "decor: pretty lights to stare at (+5% patience)",        Price = 75, Decor = true },
            new Upgrade { Id = "SURF",     Name = "Surfboard",        Desc = "decor: something to talk about (+5% patience)",          Price = 75, Decor = true },
            new Upgrade { Id = "TIKI",     Name = "Tiki Torches",     Desc = "decor: ooh, fire (+5% patience)",                        Price = 100, Decor = true },
        };

        private const string DAY_KEY = "Beach Day", CASH_KEY = "Beach Cash", UP_PREFIX = "Beach Upgrade ";

        public static int Day
        {
            get
            {
                int d = Shop.Get(DAY_KEY);
                if (d <= 0) d = Mathf.Max(1, PlayerPrefs.GetInt("KE_BeachDay", 1)); // the first test builds kept it here
                return d;
            }
            set { Shop.SetValue(DAY_KEY, value); }
        }

        public static int Cash { get { return Shop.Get(CASH_KEY); } }
        public static void AddCash(int amount) { if (amount > 0) Shop.SetValue(CASH_KEY, Cash + amount); }

        public static bool Has(string id) { return Shop.Get(UP_PREFIX + id) > 0; }

        public static void SetCash(int amount) { Shop.SetValue(CASH_KEY, Mathf.Max(0, amount)); }

        // Where the player put each movable piece of decor (cm and degrees, beach-local).
        public static bool DecorSpot(string key, out float x, out float z, out float yaw)
        {
            x = Shop.Get("Beach Decor " + key + " X") / 100f - 100f;
            z = Shop.Get("Beach Decor " + key + " Z") / 100f - 100f;
            yaw = Shop.Get("Beach Decor " + key + " Yaw");
            return Shop.Get("Beach Decor " + key + " Set") > 0;
        }

        public static void SaveDecorSpot(string key, float x, float z, float yaw)
        {
            // Shop.txt keeps non-negative whole numbers: centimetres shifted by 100 m.
            Shop.SetValue("Beach Decor " + key + " X", Mathf.RoundToInt((x + 100f) * 100f));
            Shop.SetValue("Beach Decor " + key + " Z", Mathf.RoundToInt((z + 100f) * 100f));
            Shop.SetValue("Beach Decor " + key + " Yaw", Mathf.RoundToInt(Mathf.Repeat(yaw, 360f)));
            Shop.SetValue("Beach Decor " + key + " Set", 1);
        }

        public static Upgrade Find(string id)
        {
            foreach (var u in All) if (u.Id == id) return u;
            return null;
        }

        public static bool Available(Upgrade u) { return string.IsNullOrEmpty(u.Requires) || Has(u.Requires); }

        public static bool TryBuy(Upgrade u)
        {
            if (u == null || Has(u.Id) || !Available(u) || Cash < u.Price) return false;
            Shop.SetValue(CASH_KEY, Cash - u.Price);
            Shop.SetValue(UP_PREFIX + u.Id, 1);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Beach] Bought " + u.Name + " for $" + u.Price + ".");
            if (BeachShift.Instance != null) BeachShift.Instance.OnUpgradesChanged();
            return true;
        }

        // ── What they do ─────────────────────────────────────────────────────
        public static float BlendSpeed { get { return Has("TURBO2") ? 1.6f : Has("TURBO1") ? 1.3f : 1f; } }
        /// <summary>Cushions +20%; every piece of decor distracts the waiting customers a little more (+5% each).</summary>
        public static float PatienceMult
        {
            get
            {
                float m = Has("CUSHIONS") ? 1.2f : 1f;
                foreach (var u in All) if (u.Decor && Has(u.Id)) m += 0.05f;
                return m;
            }
        }
        public static float WaitGrace { get { return 50f + (Has("PEANUTS") ? 20f : 0f); } }
        public static float TipMult { get { return 1f + (Has("TIPJAR") ? 0.2f : 0f) + (Has("FANCY") ? 0.15f : 0f); } }
    }

    /// <summary>Mama's upgrade board: buy with the bar's cash, between days.</summary>
    public class BeachUpgradeMenu : OverlayPanel
    {
        private static BeachUpgradeMenu _instance;
        private Text _cash;
        private readonly Text[] _buy = new Text[BeachUpgrades.All.Length];
        private readonly Text[] _names = new Text[BeachUpgrades.All.Length];

        protected override string Title { get { return "MAMA'S UPGRADES"; } }
        protected override Vector2 PanelSize { get { return new Vector2(1240f, 960f); } }

        public static void Open() { GetOrCreate(ref _instance, "KE_BeachUpgradeMenu").Show(); }
        public static void Close() { if (_instance != null) _instance.Hide(); }

        protected override void BuildContent(Transform p)
        {
            _cash = MakeText(p, "", 34, TextAnchor.MiddleCenter, new Vector2(0f, 360f), new Vector2(900f, 50f), new Color(0.5f, 1f, 0.6f));
            for (int i = 0; i < BeachUpgrades.All.Length; i++)
            {
                var u = BeachUpgrades.All[i];
                float y = 305f - i * 46f;
                _names[i] = Fit(MakeText(p, "", 25, TextAnchor.MiddleLeft, new Vector2(-190f, y), new Vector2(760f, 42f), Color.white));
                _names[i].supportRichText = true;
                var captured = u;
                _buy[i] = AddButton(p, new Vector2(400f, y), "", () => Buy(captured), 300f);
                _buy[i].resizeTextMaxSize = 28;
                _buy[i].rectTransform.sizeDelta = new Vector2(300f, 42f); // rows are 46 apart: no overlapping click areas
            }
            AddButton(p, new Vector2(0f, -420f), "CLOSE", () => Hide(), 300f);
        }

        private void Buy(BeachUpgrades.Upgrade u)
        {
            if (!BeachUpgrades.TryBuy(u)) return;
            if (Camera.main != null) AudioController.Play("music_sting_positive", Camera.main.transform);
        }

        protected override void RefreshAll()
        {
            base.RefreshAll();
            if (_cash == null) return;
            int cash = BeachUpgrades.Cash;
            _cash.text = "Cash in the register: $" + cash;
            for (int i = 0; i < BeachUpgrades.All.Length; i++)
            {
                var u = BeachUpgrades.All[i];
                bool owned = BeachUpgrades.Has(u.Id), open = BeachUpgrades.Available(u);
                _names[i].text = "<b>" + u.Name + "</b>  <color=#B8B8C0>- " + u.Desc + "</color>";
                if (owned) { _buy[i].text = "OWNED"; _buy[i].color = new Color(0.5f, 1f, 0.6f); }
                else if (!open) { _buy[i].text = "needs " + BeachUpgrades.Find(u.Requires).Name; _buy[i].color = Dim; }
                else if (cash < u.Price) { _buy[i].text = "$" + u.Price; _buy[i].color = new Color(1f, 0.45f, 0.4f); }
                else { _buy[i].text = "[ BUY  $" + u.Price + " ]"; _buy[i].color = Color.white; }
            }
        }
    }
}
