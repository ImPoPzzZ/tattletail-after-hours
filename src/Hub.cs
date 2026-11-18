using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// v3.0 arcade HUB (test build). Reached from a temporary "HUB (test)"
    /// main menu button: loads the same level as Endless (so the real player,
    /// camera and controls come for free), then builds the arcade room far
    /// above the house and moves the player into it. Nothing else from
    /// Endless is set up. Layout: HubLayout. Placement tool: HubMarkTool.
    /// </summary>
    public static class HubMode
    {
        /// <summary>Armed by the menu button, consumed when the level loads.</summary>
        public static bool Pending;
        /// <summary>True while the player is in the hub level.</summary>
        public static bool Active;
        /// <summary>Set when a hub machine starts a mode: when that run ends (death, escape…), you land back in the hub instead of the menu.</summary>
        public static bool ReturnToHub;

        public static void Launch(MainMenu menu)
        {
            VersusMode.Reset(); // coming back to the Arcade ends any Versus session
            LoadingCover.Show(null);
            if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);

            EndlessRunManager.PendingBirthdayMode = false;
            EndlessTitle.PendingLucky = false;
            EndlessTitle.PendingTitle = "The Arcade";
            Gadgets.RunGadget = GadgetType.None;

            Pending = true;
            BeachMode.Reset();
            FactoryMode.Reset();
            RecalledMode.Reset();
            EndlessRunManager.PendingEndlessRun = true; // reuses SceneMonitor's "the mod owns this level load" gate

            GameManager.PlayLevel(LevelEnum._6_TheParty);
        }

        /// <summary>Started from the menu scene: loads the arcade instead of showing the menu.</summary>
        public static System.Collections.IEnumerator GoToHubFromMenu(MainMenu menu)
        {
            // Let the menu (and the mod's own menu-start resets) finish first.
            yield return null;
            yield return null;

            // An update check / download comes first: the title screen does not go on until it is over (and restarts the game after a download).
            yield return KaleidoscopeEndlessPlugin.Instance.StartCoroutine(AutoUpdate.GateAtTitle());

            // The game refuses a level load while it's still loading something
            // ("Already loading a scene") — and at startup the washing-machine
            // scavenger briefly loads 2_scene. Wait for both, then try a few times.
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                float waited = 0f;
                while (waited < 15f && (Night2AudioScavenger.Busy || (RM.loader != null && RM.loader.GetIsLoading())))
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }

                bool launched = false;
                try
                {
                    Launch(null);
                    launched = true;
                }
                catch (System.Exception e)
                {
                    KaleidoscopeEndlessPlugin.Log.LogError("KE: [Hub] Couldn't load the arcade: " + e.Message);
                }

                yield return null;
                if (launched)
                {
                    // (The game can say "Already loading a scene" and drop the request while it still looks busy: wait to see the level really arrive.)
                    float t = 0f;
                    while (t < 20f && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "menu") { t += Time.unscaledDeltaTime; yield return null; }
                    if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "menu") yield break; // on its way (or here)
                }

                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Hub] Arcade load didn't start (attempt " + attempt + "), retrying...");
                Pending = false;
                EndlessRunManager.PendingEndlessRun = false;
                yield return new WaitForSeconds(0.5f);
            }

            KaleidoscopeEndlessPlugin.Log.LogError("KE: [Hub] Gave up loading the arcade — showing the game's menu instead.");
            LoadingCover.Hide();
            if (menu != null && menu.mainMenuHolder != null) menu.mainMenuHolder.SetActive(true);
        }

        public static void Reset()
        {
            Pending = false;
            Active = false;
            ReturnToHub = false; // a machine sets it again right after leaving the hub
            HubController.DestroyInstance();
        }

        // While in the hub, the level's own story text ("Grab Tattletail" etc.)
        // is blocked; only the hub's own messages go through this.
        public static bool AllowHudText;

        public static void SetHudText(string text)
        {
            if (RM.hud == null) return;
            AllowHudText = true;
            try { RM.hud.SetNewQuest(text); }
            finally { AllowHudText = false; }
        }
    }

    /// <summary>
    /// The arcade replaces the old main menu: whenever the menu scene loads
    /// (game start, quitting a mode to the menu), its buttons are hidden and
    /// the hub loads instead. If that ever fails, the menu is shown so the
    /// player isn't stuck.
    /// </summary>
    [HarmonyLib.HarmonyPatch(typeof(MainMenu), "Start")]
    public static class Patch_MainMenu_Start_GoToHub
    {
        static void Postfix(MainMenu __instance)
        {
            // Cover the (now empty) menu right away, in the game's own font.
            Font font = null;
            if (__instance.mainMenuHolder != null)
            {
                var label = __instance.mainMenuHolder.GetComponentInChildren<UnityEngine.UI.Text>(true);
                if (label != null) font = label.font;
            }
            LoadingCover.Show(font);

            foreach (GameObject holder in new[] { __instance.mainMenuHolder, __instance.optionsMenuHolder, __instance.wipeDataMenuHolder, __instance.levelSelectHolder })
                if (holder != null) holder.SetActive(false);
            if (KaleidoscopeEndlessPlugin.Instance != null)
                KaleidoscopeEndlessPlugin.Instance.StartCoroutine(HubMode.GoToHubFromMenu(__instance));
        }
        // (No coroutines in Harmony patch classes: PatchAll reads every method's attributes.)
    }

    [HarmonyLib.HarmonyPatch(typeof(HUD), "SetNewQuest")]
    public static class Patch_HUD_SetNewQuest_Hub
    {
        static bool Prefix() { return !(HubMode.Active || BeachMode.Active || (FactoryMode.Active && !FactoryMode.EndlessPreview) || RecalledMode.Active || VersusMode.IsMama) || HubMode.AllowHudText; }   // (Endless in the Facility uses the quest text for its objectives) // the level's story text stays out of the hub, the beach and the Versus Mama side
    }

    public class HubController : MonoBehaviour
    {
        public static HubController Instance { get; private set; }

        // Far above the house: nothing from the level (walls, lights, sounds) reaches up here.
        public static readonly Vector3 Origin = new Vector3(0f, 400f, 0f);
        public const float WIDTH = 24f;   // x
        public const float DEPTH = 18f;   // z
        public const float HEIGHT = 4.5f;
        private const float WALL = 0.3f;

        private Transform _room;
        private bool _built;
        private float _readyTimer;

        public bool Built { get { return _built; } }

        public static void Create()
        {
            if (Instance != null) return;
            var go = new GameObject("KE_HubController");
            DontDestroyOnLoad(go);
            go.AddComponent<HubController>();
            go.AddComponent<VersionTag>(); // "EARLY ACCESS V3.1" bottom-right, arcade only
            go.AddComponent<HubMarkTool>(); // marks: everyone sees them; E places them (dev builds only)
            if (KaleidoscopeEndlessPlugin.DevTools) go.AddComponent<KidPreviewTool>(); // K = a walking Versus kid to look at
        }

        public static void DestroyInstance()
        {
            if (Instance != null) Destroy(Instance.gameObject);
            Instance = null;
        }

        private void Awake() { Instance = this; }

        private void OnDestroy()
        {
            RestoreAtmosphere();
            if (Instance == this) Instance = null;
        }

        // The level's fog and the camera's depth-of-field blur made the arcade
        // look foggy and smeared the signs — off while in the hub, restored
        // exactly as they were on the way out (the camera profile is shared
        // with the rest of the game).
        private bool _atmosphereChanged, _savedFog, _savedPpFog, _savedDof;
        private UnityEngine.PostProcessing.PostProcessingProfile _profile;

        private void ClearAtmosphere()
        {
            _savedFog = RenderSettings.fog;
            RenderSettings.fog = false;
            var cam = Camera.main;
            var ppb = cam != null ? cam.GetComponent<UnityEngine.PostProcessing.PostProcessingBehaviour>() : null;
            if (ppb != null && ppb.profile != null)
            {
                _profile = ppb.profile;
                _savedPpFog = _profile.fog.enabled;
                _savedDof = _profile.depthOfField.enabled;
                _profile.fog.enabled = false;
                _profile.depthOfField.enabled = false;
            }
            _atmosphereChanged = true;
        }

        private void RestoreAtmosphere()
        {
            if (!_atmosphereChanged) return;
            _atmosphereChanged = false;
            RenderSettings.fog = _savedFog;
            if (_profile != null)
            {
                _profile.fog.enabled = _savedPpFog;
                _profile.depthOfField.enabled = _savedDof;
            }
        }

        private float _dailyNext;
        private void Update()
        {
            // Safety net: the hub must never outlive hub mode (e.g. float above the house during a run).
            if (!HubMode.Active) { Destroy(gameObject); return; }

            var fps = RM.fpsController;
            if (fps == null) return;

            if (!_built)
            {
                // Wait until the level has actually handed the player control
                // (the intro is over), plus a moment, so nothing in the
                // level's own startup moves the player back afterwards.
                if (!GS.acceptInput) { _readyTimer = 0f; return; }
                _readyTimer += Time.deltaTime;
                if (_readyTimer < 0.75f) return;

                BuildRoom();
                ClearAtmosphere();
                _built = true;
                MovePlayerBack(fps); // where you were standing when you left (the first time: the entrance)
                if (RM.hud != null) RM.hud.ClearQuest(); // wipe the level's "Grab Tattletail" line
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Hub] Room built at " + Origin + ", player moved in.");
                if (!Identity.Awakened) ArcadeAwakening.Begin(); // a player's very first arrival (once, ever): you wake up as a blank mannequin (it lifts the loading screen)
                else LoadingCover.Hide();
                string daily = Daily.TakePending();   // daily objectives finished while you were away
                if (daily != null) VersusLobby.PendingArcadeMessage = VersusLobby.PendingArcadeMessage == null ? daily : VersusLobby.PendingArcadeMessage + System.Environment.NewLine + daily;
                if (VersusLobby.PendingArcadeMessage != null)
                {
                    HubMode.SetHudText(VersusLobby.PendingArcadeMessage);
                    VersusLobby.PendingArcadeMessage = null;
                }
                return;
            }

            if (Time.unscaledTime >= _dailyNext) { _dailyNext = Time.unscaledTime + 1f; DailyPapers.Refresh(); } // the daily papers stay current
            // Remember where you're standing: coming back to the Arcade puts you right there again.
            if (!ArcadeAwakening.Running && !PinballGame.Playing && !AirHockeyGame.Playing)
            {
                _lastSpot = fps.transform.position - Origin;
                _lastYaw = fps.transform.eulerAngles.y;
                _hasLastSpot = true;
            }

            if (Input.GetKeyDown(KeyCode.F9) && !PinballGame.Playing && !AirHockeyGame.Playing && !ArcadeAwakening.Running && !OverlayPanel.BlocksWorld)
            {
                if (TrailerDirector.Enabled && KaleidoscopeEndlessPlugin.DevTools)
                {
                    // record the trailer (dev builds, trailer mode on). The Recalled one is shot in the town: through the door, and it rolls once the town is up.
                    if (TrailerDirector.RecalledTake) { TrailerDirector.PendingRecalled = true; SceneMonitor.ResetForNewRun(); RecalledMode.Launch(); }
                    else TrailerDirector.Begin();
                }
                // (F9 used to open the Waygetter Facility as a free roam. That is off: the Facility is played as Endless, from the Endless machine's Map option.)
            }

            // Safety net: if the player ever ends up outside the room (fell
            // through, or the level moved them), put them back.
            Vector3 local = fps.transform.position - Origin;
            if (local.y < -5f || Mathf.Abs(local.x) > WIDTH || Mathf.Abs(local.z) > DEPTH)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Hub] Player left the room (" + local + ") — moving them back.");
                MovePlayerIn(fps);
            }
        }

        private static Vector3 _lastSpot;
        private static float _lastYaw;
        private static bool _hasLastSpot;

        /// <summary>Back where you were standing when you left the Arcade, facing the same way (the first time: the entrance).</summary>
        private static void MovePlayerBack(FirstPersonController fps)
        {
            if (!_hasLastSpot) { MovePlayerIn(fps); return; }
            fps.TeleportPlayer(Origin + _lastSpot + Vector3.up * 0.05f, new Vector3(0f, _lastYaw, 0f));
        }

        private static void MovePlayerIn(FirstPersonController fps)
        {
            // The game's own teleport: sets the position AND the look direction properly.
            fps.TeleportPlayer(Origin + HubLayout.SpawnLocal, new Vector3(0f, HubLayout.SPAWN_YAW, 0f));
        }

        // ── Room ─────────────────────────────────────────────────────────────
        private void BuildRoom()
        {
            _room = new GameObject("KE_HubRoom").transform;
            _room.SetParent(transform, false);
            _room.position = Origin;

            Material black = Lit(new Color(0.02f, 0.02f, 0.025f), 0.1f);

            // Chessboard floor: 1m tiles.
            var floor = Box("Floor", new Vector3(0f, -0.1f, 0f), new Vector3(WIDTH, 0.2f, DEPTH), CheckerMaterial());
            floor.GetComponent<Renderer>().material.mainTextureScale = new Vector2(WIDTH / 2f, DEPTH / 2f);

            Box("Ceiling", new Vector3(0f, HEIGHT + 0.1f, 0f), new Vector3(WIDTH, 0.2f, DEPTH), black);
            Box("WallNorth", new Vector3(0f, HEIGHT / 2f, DEPTH / 2f + WALL / 2f), new Vector3(WIDTH + WALL * 2f, HEIGHT, WALL), black);
            Box("WallSouth", new Vector3(0f, HEIGHT / 2f, -DEPTH / 2f - WALL / 2f), new Vector3(WIDTH + WALL * 2f, HEIGHT, WALL), black);
            Box("WallEast", new Vector3(WIDTH / 2f + WALL / 2f, HEIGHT / 2f, 0f), new Vector3(WALL, HEIGHT, DEPTH), black);
            Box("WallWest", new Vector3(-WIDTH / 2f - WALL / 2f, HEIGHT / 2f, 0f), new Vector3(WALL, HEIGHT, DEPTH), black);

            // Ceiling light bars: glowing tubes in dark fixtures, each with a real light under it.
            float[] xs = { -8f, -2.7f, 2.7f, 8f };
            float[] zs = { -4.5f, 4.5f };
            const float barLength = 6f;
            Material tube = Unlit(new Color(0.78f, 0.8f, 0.84f)); // dimmer than pure white so bloom doesn't blow the tubes out
            Material fixture = Lit(new Color(0.08f, 0.08f, 0.09f), 0.3f);
            foreach (float x in xs)
                foreach (float z in zs)
                    LightBar(new Vector3(x, HEIGHT - 0.12f, z), barLength, tube, fixture);

            HubLayout.Build(_room, HEIGHT); // walls, signs, machines, stats board
        }

        private void LightBar(Vector3 pos, float length, Material tubeMat, Material fixtureMat)
        {
            var housing = Box("LightFixture", pos + new Vector3(0f, 0.07f, 0f), new Vector3(0.28f, 0.08f, length + 0.2f), fixtureMat);
            Destroy(housing.GetComponent<Collider>());

            var bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bar.name = "LightTube";
            Destroy(bar.GetComponent<Collider>());
            bar.transform.SetParent(_room, false);
            bar.transform.localPosition = pos;
            bar.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // lie the cylinder along z
            bar.transform.localScale = new Vector3(0.12f, length / 2f, 0.12f);
            bar.GetComponent<Renderer>().material = tubeMat;

            var lightGO = new GameObject("TubeLight");
            lightGO.transform.SetParent(_room, false);
            lightGO.transform.localPosition = pos + new Vector3(0f, -0.35f, 0f);
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 7f;
            light.intensity = 0.85f;
            light.color = new Color(0.9f, 0.95f, 1f);
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel; // don't let these drop to low-quality vertex lighting
        }

        private GameObject Box(string name, Vector3 localPos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(_room, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().material = mat;
            return go;
        }

        private static Material Lit(Color c, float gloss) { return Mat.Lit(c, gloss); }
        private static Material Unlit(Color c) { return Mat.Unlit(c); }

        private static Material CheckerMaterial()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Color white = new Color(0.82f, 0.82f, 0.82f), dark = new Color(0.04f, 0.04f, 0.045f);
            tex.SetPixel(0, 0, white); tex.SetPixel(1, 1, white);
            tex.SetPixel(1, 0, dark); tex.SetPixel(0, 1, dark);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.Apply();

            var m = Lit(Color.white, 0.3f); // a little shine, like a polished arcade floor
            m.mainTexture = tex;
            return m;
        }
    }
}
