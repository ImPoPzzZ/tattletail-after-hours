using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    // ══ items and recipes ════════════════════════════════════════════════════
    /// <summary>
    /// What can be crafted in Papa's 10th Birthday!. Every finished food is made
    /// from exactly two ingredients, in either order. Two items that aren't a
    /// recipe just fail and reset.
    /// </summary>
    public static class PartyItems
    {
        // Every finished food is made from exactly two ingredients (either order).
        private static readonly string[][] Recipes =
        {
            new[] { "Corn Kernels", "Butter", "Popcorn" },
            new[] { "Sugar", "Food Coloring", "Cotton Candy" },
            new[] { "Cream", "Cones", "Ice Cream" },
            new[] { "Hot Dogs", "Buns", "Hot Dog" },
            new[] { "Nacho Chips", "Cheese", "Nachos" },
            new[] { "Apples", "Sugar", "Candy Apple" },
            new[] { "Flour", "Milk", "Waffle & Crepe" },
            new[] { "Flour", "Eggs", "Pancakes" },
        };

        // The finished foods — what Papa can eat. Every one fills his hunger bar by the same 30% (out of 100).
        private static readonly Dictionary<string, int> Foods = new Dictionary<string, int>
        {
            { "Popcorn", 30 }, { "Cotton Candy", 30 }, { "Ice Cream", 30 }, { "Hot Dog", 30 },
            { "Nachos", 30 }, { "Candy Apple", 30 }, { "Waffle & Crepe", 30 }, { "Pancakes", 30 },
        };

        public static readonly string[] FoodNames = { "Popcorn", "Cotton Candy", "Ice Cream", "Hot Dog", "Nachos", "Candy Apple", "Waffle & Crepe", "Pancakes" };

        public static bool IsFood(string name) { return name != null && Foods.ContainsKey(name); }
        public static int FeedValue(string name) { int v; return name != null && Foods.TryGetValue(name, out v) ? v : 0; }

        /// <summary>The result of combining two items, or null if they don't go together.</summary>
        public static string Combine(string a, string b)
        {
            if (a == null || b == null) return null;
            foreach (var r in Recipes)
                if ((r[0] == a && r[1] == b) || (r[0] == b && r[1] == a)) return r[2];
            return null;
        }

        /// <summary>The two ingredients a finished food is made from, or null if it's not a recipe result.</summary>
        public static string[] RecipeFor(string food)
        {
            foreach (var r in Recipes) if (r[2] == food) return new[] { r[0], r[1] };
            return null;
        }
    }

    // ══ the inventory ════════════════════════════════════════════════════════
    public static class PartyInventory
    {
        public const int SLOTS = 10;
        public const int MAX_STACK = 64;
        public static readonly string[] Names = new string[SLOTS];
        public static readonly int[] Counts = new int[SLOTS];
        public static int Version; // bumped on every change so the hotbar knows to redraw

        public static void Clear()
        {
            for (int i = 0; i < SLOTS; i++) { Names[i] = null; Counts[i] = 0; }
            Version++;
        }

        public static bool CanHold(int slot, string name)
        {
            return Names[slot] == null || (Names[slot] == name && Counts[slot] < MAX_STACK);
        }

        /// <summary>Stacks onto an existing slot of the same item, else takes the first empty one. Returns the slot, or -1 if full.</summary>
        public static int Add(string name)
        {
            for (int i = 0; i < SLOTS; i++)
                if (Names[i] == name && Counts[i] < MAX_STACK) { Counts[i]++; Version++; return i; }
            for (int i = 0; i < SLOTS; i++)
                if (Names[i] == null) { Names[i] = name; Counts[i] = 1; Version++; return i; }
            return -1;
        }

        public static void RemoveOne(int slot)
        {
            if (Names[slot] == null) return;
            Counts[slot]--;
            if (Counts[slot] <= 0) { Names[slot] = null; Counts[slot] = 0; }
            Version++;
        }

        public static void PutInSlot(int slot, string name)
        {
            if (Names[slot] == name) Counts[slot]++;
            else { Names[slot] = name; Counts[slot] = 1; }
            Version++;
        }
    }

    // ══ the game: keys, crafting, feeding, bars ══════════════════════════════
    public class PartyGameRunner : MonoBehaviour
    {
        public static PartyGameRunner Instance { get; private set; }

        // Slot picked with a number key (-1 = none). The first pick of a craft, or the food to feed Papa.
        public int Selected = -1;
        public float PapaHunger = 100f;      // 0..100 — both bars start full
        public float Entertainment = 100f;  // 0..100 — filled by piñatas for now; tattletail games/objectives are next
        public float PartyElapsed;   // seconds the party has gone on for (endless — only ends if both bars hit 0)
        public readonly Color[] FlashColor = new Color[PartyInventory.SLOTS];
        public readonly float[] FlashTime = new float[PartyInventory.SLOTS];

        private const float HUNGER_SECONDS_TO_EMPTY = 180f;       // Papa's Hunger runs from full to empty in 3:00
        private const float ENTERTAINMENT_SECONDS_TO_EMPTY = 150f; // Entertainment runs from full to empty in 2:30
        private const int MINUTE_TOKENS = 30;            // every full minute of party survived pays this many Party Tokens
        private float _tokenTimer = 60f;

        private static readonly KeyCode[] Keys =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
            KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0,
        };

        private Interactable _papa;
        private PartyHud _hud;
        private bool _over;
        private bool _mamaActive; // Entertainment hit 0 — she's hunting until it recovers

        // The party has no timer — it only ends if Papa's Hunger hits 0. Entertainment
        // hitting 0 is a separate danger: Mama starts hunting until it recovers (see Update()).
        private System.Collections.IEnumerator LoseParty(EndlessRunManager run)
        {
            Hint("Papa's starving! The party's over!");
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Party] Papa's Hunger hit 0 — ending the session.");
            MamaController.Instance?.DeactivateMama();
            yield return new WaitForSeconds(3f);
            if (run != null && run.RunActive) run.EndRun(true);
        }

        public static void Begin(GameObject host)
        {
            PartyInventory.Clear();
            var runner = host.AddComponent<PartyGameRunner>();
            runner._hud = host.AddComponent<PartyHud>();
            runner._hud.Init(runner);
        }

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            var run = EndlessRunManager.Instance;
            if (run == null || !run.RunActive) return;

            PapaHunger = Mathf.Max(0f, PapaHunger - (100f / HUNGER_SECONDS_TO_EMPTY) * Time.deltaTime);
            Entertainment = Mathf.Max(0f, Entertainment - (100f / ENTERTAINMENT_SECONDS_TO_EMPTY) * Time.deltaTime);
            PartyElapsed = run.RunTime;

            // Entertainment at 0 doesn't end the party — it summons Mama, until Entertainment recovers.
            bool wantMama = Entertainment <= 0f;
            if (wantMama != _mamaActive)
            {
                _mamaActive = wantMama;
                if (wantMama)
                {
                    MamaController.Instance?.ActivateMamaForObjective(1f);
                    Hint("Nobody's having fun... Mama's coming!");
                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Party] Entertainment hit 0 — Mama is now hunting.");
                }
                else
                {
                    MamaController.Instance?.DeactivateMama();
                    Hint("Mama's gone... for now.");
                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Party] Entertainment recovered — Mama backed off.");
                }
            }

            if (!_over && PapaHunger <= 0f)
            {
                _over = true;
                StartCoroutine(LoseParty(run));
            }

            _tokenTimer -= Time.deltaTime;
            if (!_over && _tokenTimer <= 0f)
            {
                _tokenTimer += 60f;
                Shop.AddPartyTokens(MINUTE_TOKENS);
                Daily.Add("party_minutes", 1);
                Hint("+" + MINUTE_TOKENS + " shards!");
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Party] Another minute survived — +" + MINUTE_TOKENS + " shards.");
            }
            for (int i = 0; i < FlashTime.Length; i++) if (FlashTime[i] > 0f) FlashTime[i] -= Time.deltaTime;

            for (int i = 0; i < Keys.Length; i++)
                if (Input.GetKeyDown(Keys[i])) OnSlotKey(i);

            // Keep the selection valid, and Papa's prompt honest about what you're holding.
            if (Selected >= 0 && PartyInventory.Names[Selected] == null) Selected = -1;
            if (_papa != null)
            {
                string held = Selected >= 0 ? PartyInventory.Names[Selected] : null;
                _papa.actionText = PartyItems.IsFood(held) ? "Feed Papa: " + held : "Feed Papa (press a food's number first)";
            }
        }

        // ── number keys ───────────────────────────────────────────────────────
        private void OnSlotKey(int slot)
        {
            if (PartyInventory.Names[slot] == null) { Selected = -1; return; }   // empty box: cancel
            if (Selected < 0) { Selected = slot; return; }                        // first pick — box lights up
            if (Selected == slot) { Selected = -1; return; }                      // same box again — put it down

            int first = Selected;
            Selected = -1;
            TryCraft(first, slot);
        }

        private void TryCraft(int a, int b)
        {
            string na = PartyInventory.Names[a], nb = PartyInventory.Names[b];
            string result = PartyItems.Combine(na, nb);
            if (result == null)
            {
                Flash(a, Color.red); Flash(b, Color.red);
                Hint(na + " and " + nb + " don't go together.");
                return;
            }

            // Take one of each, then put the result in a slot: the first pick's box if it's free
            // (or already holds that item), else the second's, else any box that can hold it.
            string keepNa = na, keepNb = nb;
            int cntA = PartyInventory.Counts[a], cntB = PartyInventory.Counts[b];
            PartyInventory.RemoveOne(a);
            PartyInventory.RemoveOne(b);

            int target = -1;
            if (PartyInventory.CanHold(a, result)) target = a;
            else if (PartyInventory.CanHold(b, result)) target = b;
            else for (int i = 0; i < PartyInventory.SLOTS && target < 0; i++) if (PartyInventory.CanHold(i, result)) target = i;

            if (target < 0)
            {
                // No room — undo.
                PartyInventory.Names[a] = keepNa; PartyInventory.Counts[a] = cntA;
                PartyInventory.Names[b] = keepNb; PartyInventory.Counts[b] = cntB;
                PartyInventory.Version++;
                Flash(a, Color.red); Flash(b, Color.red);
                Hint("No room to make " + result + ".");
                return;
            }

            PartyInventory.PutInSlot(target, result);
            Flash(target, Color.green);
            Hint("Made " + result + "!");
        }

        private void Flash(int slot, Color c) { FlashColor[slot] = c; FlashTime[slot] = 0.6f; }

        public static void Hint(string text)
        {
            if (RM.hud != null) RM.hud.SetNewHint(text, 2.5f);
        }

        // ── interactions (called from the Harmony patch below) ───────────────
        public void HandleInteract(Interactable it)
        {
            if (it == null) return;

            if (_papa != null && it == _papa) { FeedPapa(); return; }

            if (PartyTattletailController.Instance != null)
            {
                if (PartyTattletailController.Instance.IsCastleOccupant(it.gameObject))
                {
                    PartyTattletailController.Instance.TakeOffCastle();
                    return;
                }
                if (PartyTattletailController.Instance.IsTattletail(it.gameObject))
                {
                    PartyTattletailController.Instance.Interact(it.gameObject);
                    return;
                }
                if (PartyTattletailController.Instance.IsRpsButton(it.gameObject))
                {
                    PartyTattletailController.Instance.ThrowRps(it.gameObject);
                    return;
                }
                if (PartyTattletailController.Instance.IsCastleFloor(it.gameObject))
                {
                    PartyTattletailController.Instance.UseCastle();
                    return;
                }
            }

            if (PartyMachineController.Instance != null && PartyMachineController.Instance.IsMachine(it.gameObject))
            {
                string held = Selected >= 0 ? PartyInventory.Names[Selected] : null;
                int slot = Selected;
                if (PartyMachineController.Instance.TryFeed(it.gameObject, held))
                {
                    Flash(slot, new Color(0.3f, 1f, 0.4f));
                    PartyInventory.RemoveOne(slot);
                    if (PartyInventory.Names[slot] == null) Selected = -1;
                }
                return;
            }

            if (PartyMaterialSpawner.Instance != null && PartyMaterialSpawner.Instance.IsPinata(it.gameObject))
            {
                bool golden = PartyMaterialSpawner.Instance.IsGoldenPinata;
                int n = PartyMaterialSpawner.Instance.BreakPinata();
                Hint(golden
                    ? "The GOLDEN pi\u00f1ata burst! " + n + " foods dropped, Entertainment is full!"
                    : "The pi\u00f1ata burst! " + n + " foods dropped, +20% Entertainment!");
                return;
            }

            if (PartyMaterialSpawner.Instance != null && PartyMaterialSpawner.Instance.IsItem(it.gameObject))
            {
                string name = PartyMaterialSpawner.Instance.PeekName(it.gameObject);
                if (name == null) return;
                int slot = PartyInventory.Add(name);
                if (slot < 0) { Hint("Inventory full."); return; }
                PartyMaterialSpawner.Instance.Take(it.gameObject);
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Party] picked up '" + name + "' into slot " + (slot + 1) + ".");
                Flash(slot, new Color(1f, 1f, 1f));
                Hint("Picked up " + name + ".");
            }
        }

        private void FeedPapa()
        {
            string held = Selected >= 0 ? PartyInventory.Names[Selected] : null;
            if (!PartyItems.IsFood(held))
            {
                Hint("Press the number of a finished food first.");
                return;
            }

            int slot = Selected;
            PapaHunger = Mathf.Min(100f, PapaHunger + PartyItems.FeedValue(held));
            PartyInventory.RemoveOne(slot);
            Flash(slot, new Color(1f, 0.8f, 0.2f));
            if (PartyInventory.Names[slot] == null) Selected = -1;
            Hint("Papa enjoyed the " + held + "!");
        }

        // ── Papa's interaction ───────────────────────────────────────────────
        public void AttachPapa(GameObject papa)
        {
            _papa = MakeInteractable(papa, "Feed Papa", 1.2f);
        }

        private static int? _layer;
        public static Interactable MakeInteractable(GameObject go, string actionText, float holdTime)
        {
            // Copy the interaction layer from a confirmed-clickable Interactable rather than guessing.
            if (!_layer.HasValue)
            {
                foreach (var existing in Object.FindObjectsOfType<Interactable>())
                    if (existing != null) { _layer = existing.gameObject.layer; break; }
            }
            if (_layer.HasValue) go.layer = _layer.Value;

            var it = go.AddComponent<Interactable>();
            it.actionText = actionText;
            it.holdTime = holdTime;
            it.highlight = true;
            it.setInactiveOnInteract = false;
            it.honks = false;
            it.newStateOnInteract = Interactable.State.Enabled;
            it.currentState = Interactable.State.Enabled;
            it.holdAudioName = string.Empty;
            it.onInteractEvent = new UnityEngine.Events.UnityEvent();
            it.conditions = new InteractableConditions();
            it.actions = new InteractableActions { interactActions = new InteractableActions.Actions[0] };
            return it;
        }
    }

    // Routes a finished interaction to the party game (pickups, feeding Papa).
    [HarmonyPatch(typeof(Interactable), "OnInteractComplete")]
    public static class Patch_Interactable_OnInteractComplete_Party
    {
        static void Postfix(Interactable __instance, bool fromAutoComplete)
        {
            if (fromAutoComplete) return;
            if (EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive || !EndlessRunManager.Instance.BirthdayMode) return;
            if (PartyGameRunner.Instance != null) PartyGameRunner.Instance.HandleInteract(__instance);
        }
    }

    // ══ mini renders of items for the hotbar ═════════════════════════════════
    public static class PartyIcons
    {
        private static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();
        private static readonly Vector3 Rig = new Vector3(0f, -6000f, 0f);
        private static int _logged;

        public static Texture Get(string name)
        {
            Texture2D tex;
            if (_cache.TryGetValue(name, out tex) && tex != null) return tex;
            try { tex = Render(name); }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Party] icon render failed for '" + name + "': " + e.Message + " " + e.StackTrace);
                tex = null;
            }
            if (tex != null) _cache[name] = tex;
            return tex;
        }

        private static Texture2D Render(string name)
        {
            GameObject model = PartyFoods.BuildHeld(name, Rig, Quaternion.identity);
            if (model == null) return null;

            Bounds b = new Bounds(Rig, Vector3.zero);
            bool any = false;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                if (r == null) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (!any) { Object.DestroyImmediate(model); return null; }

            // Icons are drawn flat (Unlit, each part in its own color): they can't depend on
            // this dark house's lighting to be visible.
            Shader unlit = Shader.Find("Unlit/Color");
            if (unlit != null)
            {
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                {
                    if (r == null) continue;
                    Material[] mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        Color c = (mats[i] != null && mats[i].HasProperty("_Color")) ? mats[i].color : Color.white;
                        var um = new Material(unlit);
                        um.color = new Color(Mathf.Min(1f, c.r * 1.25f), Mathf.Min(1f, c.g * 1.25f), Mathf.Min(1f, c.b * 1.25f), 1f);
                        mats[i] = um;
                    }
                    r.sharedMaterials = mats;
                }
            }

            float radius = b.extents.magnitude;
            var camGO = new GameObject("KE_IconCam");
            var cam = camGO.AddComponent<Camera>();
            cam.enabled = false; // rendered by hand
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.orthographic = true;
            cam.orthographicSize = radius * 1.02f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = radius * 8f;
            Vector3 dir = new Vector3(0.6f, 0.5f, 1f).normalized;   // front, a little right, a little above
            camGO.transform.position = b.center + dir * radius * 3f;
            camGO.transform.LookAt(b.center);

            var lights = new List<GameObject>();
            lights.Add(MakeLight(camGO.transform.position + (camGO.transform.up + camGO.transform.right) * radius, 1.7f, radius));
            lights.Add(MakeLight(camGO.transform.position - camGO.transform.right * radius * 1.5f, 0.9f, radius));

            var rt = new RenderTexture(128, 128, 16, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();

            // Copy the render into an ordinary texture (RenderTexture.active + ReadPixels) so it
            // doesn't depend on a RenderTexture surviving — same route the shop previews use.
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            if (_logged < 4)
            {
                _logged++;
                Color mid = tex.GetPixel(rt.width / 2, rt.height / 2);
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Party] icon '" + name + "' rendered (radius " + radius.ToString("F2") + ", unlit=" + (unlit != null) + ", centre pixel " + mid + ").");
            }

            cam.targetTexture = null;
            rt.Release();
            Object.Destroy(rt);
            Object.DestroyImmediate(model);
            Object.DestroyImmediate(camGO);
            foreach (var l in lights) Object.DestroyImmediate(l);
            return tex;
        }

        private static GameObject MakeLight(Vector3 pos, float intensity, float radius)
        {
            var go = new GameObject("KE_IconLight");
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = radius * 12f;
            l.intensity = intensity;
            l.color = new Color(1f, 0.97f, 0.9f);
            return go;
        }
    }

    // ══ the on-screen hotbar and bars ════════════════════════════════════════
    public class PartyHud : MonoBehaviour
    {
        private const float SLOT = 76f, GAP = 8f;

        private PartyGameRunner _game;
        private Font _font;
        private readonly Image[] _bg = new Image[PartyInventory.SLOTS];
        private readonly Image[] _glow = new Image[PartyInventory.SLOTS];
        private readonly RawImage[] _icon = new RawImage[PartyInventory.SLOTS];
        private readonly Text[] _count = new Text[PartyInventory.SLOTS];
        private readonly Text[] _fallback = new Text[PartyInventory.SLOTS];
        private Text _tip;
        private RectTransform _hungerFill, _entertainmentFill;   // the two party bars' fills
        private RectTransform _hungerRoot, _entertainmentRoot;    // the two party bars' whole root (repositioned every frame)
        private bool _barsBuilt;
        private int _framesSeen;
        private Text _timer;
        private Text _castleTimer;
        private int _shownVersion = -1;
        private GameObject _canvasGO;

        public void Init(PartyGameRunner game)
        {
            _game = game;
            _font = MainMenuButtonInjector.RealFont != null ? MainMenuButtonInjector.RealFont : Resources.GetBuiltinResource<Font>("Arial.ttf");

            _canvasGO = new GameObject("KE_PartyHudCanvas");
            _canvasGO.transform.SetParent(transform, false);
            var canvas = _canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 950;
            var scaler = _canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Party] HUD init — building hotbar and timer.");
            BuildHotbar();
            BuildTimer();

            // The two bars are built a few frames later (see TryBuildBars), once the game's own HUD has laid itself out.
        }

        // The party countdown, top right (where the run timer sits in the other modes).
        private void BuildTimer()
        {
            _timer = MakeText(_canvasGO.transform, "PartyTimer", 46, TextAnchor.UpperRight, new Color(0.95f, 0.85f, 0.55f));
            var rt = _timer.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-46f, -36f);
            rt.sizeDelta = new Vector2(420f, 60f);
            _timer.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);

            // Only shown while a tattletail is actually on the bouncy castle.
            _castleTimer = MakeText(_canvasGO.transform, "CastleTimer", 32, TextAnchor.UpperRight, new Color(0.6f, 0.9f, 0.6f));
            var crt = _castleTimer.rectTransform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1f, 1f);
            crt.anchoredPosition = new Vector2(-46f, -90f);
            crt.sizeDelta = new Vector2(420f, 46f);
            _castleTimer.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            _castleTimer.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_canvasGO != null) Destroy(_canvasGO);
        }

        private Text MakeText(Transform parent, string name, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private void BuildHotbar()
        {
            var bar = new GameObject("Hotbar", typeof(RectTransform));
            bar.transform.SetParent(_canvasGO.transform, false);
            var rt = (RectTransform)bar.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(PartyInventory.SLOTS * SLOT + (PartyInventory.SLOTS - 1) * GAP, SLOT);
            rt.anchoredPosition = new Vector2(0f, 26f);

            for (int i = 0; i < PartyInventory.SLOTS; i++)
            {
                var slot = new GameObject("Slot" + (i + 1), typeof(RectTransform), typeof(Image), typeof(Outline));
                slot.transform.SetParent(bar.transform, false);
                var srt = (RectTransform)slot.transform;
                srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0f, 0.5f);
                srt.sizeDelta = new Vector2(SLOT, SLOT);
                srt.anchoredPosition = new Vector2(i * (SLOT + GAP), 0f);
                _bg[i] = slot.GetComponent<Image>();
                _bg[i].color = new Color(0.04f, 0.04f, 0.06f, 0.78f);
                _bg[i].raycastTarget = false;
                var outline = slot.GetComponent<Outline>();
                outline.effectColor = new Color(0.7f, 0.7f, 0.78f, 0.9f);
                outline.effectDistance = new Vector2(2f, -2f);

                // Light-up overlay (selected / crafted / failed).
                var glow = new GameObject("Glow", typeof(RectTransform), typeof(Image));
                glow.transform.SetParent(slot.transform, false);
                var grt = (RectTransform)glow.transform;
                grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one; grt.offsetMin = grt.offsetMax = Vector2.zero;
                _glow[i] = glow.GetComponent<Image>();
                _glow[i].color = Color.clear;
                _glow[i].raycastTarget = false;

                var icon = new GameObject("Icon", typeof(RectTransform), typeof(RawImage));
                icon.transform.SetParent(slot.transform, false);
                var irt = (RectTransform)icon.transform;
                irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one; irt.offsetMin = new Vector2(8f, 8f); irt.offsetMax = new Vector2(-8f, -8f);
                _icon[i] = icon.GetComponent<RawImage>();
                _icon[i].raycastTarget = false;
                _icon[i].enabled = false;

                // Shown only if an item's mini render couldn't be made.
                _fallback[i] = MakeText(slot.transform, "Fallback", 16, TextAnchor.MiddleCenter, new Color(1f, 0.95f, 0.75f));
                var frt = _fallback[i].rectTransform;
                frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;
                _fallback[i].enabled = false;

                var num = MakeText(slot.transform, "Number", 20, TextAnchor.UpperLeft, new Color(0.85f, 0.85f, 0.9f, 0.9f));
                num.text = ((i + 1) % 10).ToString();
                var nrt = num.rectTransform;
                nrt.anchorMin = nrt.anchorMax = nrt.pivot = new Vector2(0f, 1f);
                nrt.anchoredPosition = new Vector2(6f, -4f);
                nrt.sizeDelta = new Vector2(30f, 24f);

                _count[i] = MakeText(slot.transform, "Count", 26, TextAnchor.LowerRight, Color.white);
                _count[i].gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
                var crt = _count[i].rectTransform;
                crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1f, 0f);
                crt.anchoredPosition = new Vector2(-5f, 3f);
                crt.sizeDelta = new Vector2(60f, 30f);
            }

            // What the selected box is / what to do with it, above the hotbar.
            _tip = MakeText(_canvasGO.transform, "Tip", 28, TextAnchor.LowerCenter, new Color(1f, 0.95f, 0.7f));
            var trt = _tip.rectTransform;
            trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0.5f, 0f);
            trt.anchoredPosition = new Vector2(0f, 26f + SLOT + 12f);
            trt.sizeDelta = new Vector2(1000f, 40f);
            _tip.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
        }

        /// <summary>
        /// Draws the two party bars in the same column as the game's own
        /// BATT / FEED / GROOM bars, continuing the list below them (3 and 4
        /// row spacings under GROOM). Used to compute this live from the
        /// real vanilla bars' on-screen position via reflection, with a
        /// debounce/stability-lock to survive a bad initial reading — but
        /// live testing showed the opposite failure mode too: a briefly
        /// CORRECT reading would later get hijacked by a subsequent
        /// reading that was different but happened to hold steady, landing
        /// the bars somewhere else on screen entirely after a second or so.
        /// Across every session where the live tracking DID land correctly,
        /// it converged to within a few pixels of this same fixed formula —
        /// so this just uses that known-good position directly and skips
        /// the fragile live tracking altogether (see [[feedback_prefer_self_contained_mechanics]]).
        /// </summary>
        private void TryBuildBars()
        {
            float k = Screen.height / 1080f;
            Vector2 size = new Vector2(120f * k, 20f * k);
            Vector2 c1 = new Vector2(340f * k, Screen.height - 290f * k);
            Vector2 c2 = new Vector2(340f * k, Screen.height - 330f * k);

            float scale = _canvasGO.GetComponent<Canvas>().scaleFactor;
            _hungerFill = BuildBarAt("PAPA'S HUNGER", new Color(1f, 0.72f, 0.2f), c1 / scale, size / scale, out _hungerRoot);
            _entertainmentFill = BuildBarAt("ENTERTAINMENT", new Color(0.9f, 0.35f, 0.85f), c2 / scale, size / scale, out _entertainmentRoot);
            _barsBuilt = true;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Party] bars drawn at " + c1 + " px, size " + size + ".");
        }

        // A labelled bar centred at `center` (canvas units, from the bottom-left). Label sits to its left, right-aligned, like the game's.
        private RectTransform BuildBarAt(string label, Color color, Vector2 center, Vector2 size, out RectTransform rootOut)
        {
            var root = new GameObject(label + " Bar", typeof(RectTransform));
            root.transform.SetParent(_canvasGO.transform, false);
            var rrt = (RectTransform)root.transform;
            rrt.anchorMin = rrt.anchorMax = new Vector2(0f, 0f);
            rrt.pivot = new Vector2(0.5f, 0.5f);
            rrt.anchoredPosition = center;
            rrt.sizeDelta = size;

            var back = new GameObject("Back", typeof(RectTransform), typeof(Image), typeof(Outline));
            back.transform.SetParent(root.transform, false);
            var brt = (RectTransform)back.transform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
            back.GetComponent<Image>().color = new Color(0.04f, 0.04f, 0.07f, 0.85f);
            back.GetComponent<Image>().raycastTarget = false;
            back.GetComponent<Outline>().effectColor = new Color(color.r, color.g, color.b, 0.7f);
            back.GetComponent<Outline>().effectDistance = new Vector2(2f, -2f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(root.transform, false);
            var frt = (RectTransform)fill.transform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.pivot = new Vector2(0f, 0.5f);
            frt.offsetMin = new Vector2(2f, 2f); frt.offsetMax = new Vector2(-2f, -2f);
            fill.GetComponent<Image>().color = color;
            fill.GetComponent<Image>().raycastTarget = false;

            var text = MakeText(root.transform, "Label", Mathf.Max(14, Mathf.RoundToInt(size.y * 1.1f)), TextAnchor.MiddleRight, color);
            text.text = label;
            var lrt = text.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0f, 0.5f);
            lrt.pivot = new Vector2(1f, 0.5f);
            lrt.anchoredPosition = new Vector2(-12f, 0f);
            lrt.sizeDelta = new Vector2(600f, size.y * 1.6f);
            text.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            rootOut = rrt;
            return frt;
        }

        private void Update()
        {
            var run = EndlessRunManager.Instance;
            bool show = run != null && run.RunActive;
            if (_canvasGO != null && _canvasGO.activeSelf != show) _canvasGO.SetActive(show);
            if (!show || _game == null) return;

            if (_shownVersion != PartyInventory.Version) { RefreshSlots(); _shownVersion = PartyInventory.Version; }

            for (int i = 0; i < PartyInventory.SLOTS; i++)
            {
                Color c = Color.clear;
                if (_game.FlashTime[i] > 0f)
                {
                    c = _game.FlashColor[i];
                    c.a = Mathf.Clamp01(_game.FlashTime[i] / 0.6f) * 0.7f;   // fades out
                }
                else if (_game.Selected == i) c = new Color(1f, 0.9f, 0.3f, 0.4f);
                _glow[i].color = c;
            }

            if (!_barsBuilt) { if (++_framesSeen > 8) TryBuildBars(); }
            if (_hungerFill != null) SetFill(_hungerFill, _game.PapaHunger / 100f);
            if (_entertainmentFill != null) SetFill(_entertainmentFill, _game.Entertainment / 100f);

            int elapsed = Mathf.FloorToInt(_game.PartyElapsed);
            string timerText = string.Format("PARTY  {0:D2}:{1:D2}", elapsed / 60, elapsed % 60);
            if (_timer.text != timerText) _timer.text = timerText;
            _timer.color = new Color(0.95f, 0.85f, 0.55f);

            var tattletails = PartyTattletailController.Instance;
            bool onCastle = tattletails != null && tattletails.HasCastleOccupant;
            if (_castleTimer.gameObject.activeSelf != onCastle) _castleTimer.gameObject.SetActive(onCastle);
            if (onCastle)
            {
                int cs = Mathf.FloorToInt(tattletails.CastleElapsed);
                _castleTimer.text = string.Format("CASTLE  {0:D2}:{1:D2}", cs / 60, cs % 60);
                _castleTimer.color = tattletails.CastleWantsOff ? new Color(1f, 0.4f, 0.3f) : new Color(0.6f, 0.9f, 0.6f);
            }

            string tip = "";
            if (_game.Selected >= 0)
            {
                string n = PartyInventory.Names[_game.Selected];
                tip = PartyItems.IsFood(n) ? n + " — hold interact on Papa to feed, or press another number to combine" : n + " — press another number to combine";
            }
            if (_tip.text != tip) _tip.text = tip;
        }

        private static void SetFill(RectTransform fill, float value)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        }

        private void RefreshSlots()
        {
            for (int i = 0; i < PartyInventory.SLOTS; i++)
            {
                string name = PartyInventory.Names[i];
                if (name == null)
                {
                    _icon[i].enabled = false; _fallback[i].enabled = false; _count[i].text = "";
                    continue;
                }
                Texture tex = PartyIcons.Get(name);
                if (tex == null) KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Party] no icon for '" + name + "' — showing its name instead.");
                _icon[i].texture = tex;
                _icon[i].enabled = tex != null;
                _fallback[i].enabled = tex == null;
                _fallback[i].text = tex == null ? name : "";
                _count[i].text = PartyInventory.Counts[i] > 1 ? PartyInventory.Counts[i].ToString() : "";
            }
        }
    }
}
