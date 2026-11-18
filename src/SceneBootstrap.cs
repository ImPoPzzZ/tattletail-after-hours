using GabroMedia;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    // ── Vanilla hook (backup path) ────────────────────────────────────────────
    [HarmonyPatch(typeof(GameManager), "OnSceneLoadComplete")]
    public static class Patch_GameManager_OnSceneLoadComplete_Inject
    {
        static void Postfix()
        {
            SceneMonitor.OnLevelReady();
        }
    }

    // ── Reset when player returns to main menu ────────────────────────────────
    [HarmonyPatch(typeof(MainMenu), "Start")]
    public static class Patch_MainMenu_Start_ResetInject
    {
        static void Postfix()
        {
            SceneMonitor.ResetForNewRun();
        }
    }

    // ── Force all doors unlocked during endless ───────────────────────────────
    [HarmonyPatch(typeof(BriansEngine), "IsItUnlocked")]
    public static class Patch_BriansEngine_IsItUnlocked
    {
        static bool Prefix(ref bool __result)
        {
            if (EndlessRunManager.Instance != null && EndlessRunManager.Instance.RunActive)
            {
                __result = true;
                return false; // skip vanilla check
            }
            return true;
        }
    }
    // Hook PlayActions POSTFIX so we fire AFTER the hold completes and
    // GS.acceptInput has been restored — SetFlashlight fires mid-hold
    // which permanently locks input.
    [HarmonyPatch(typeof(InteractableActions), "PlayActions")]
    public static class Patch_InteractableActions_FlashlightCarry
    {
        static void Postfix(Interactable caller, bool fromAutoComplete)
        {
            if (!SceneMonitor.WaitingForDoorTrigger) return;
            if (caller.actions.interactActions == null) return;

            foreach (var action in caller.actions.interactActions)
            {
                if (action == InteractableActions.Actions.FlashlightCarry)
                {
                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Flashlight carry complete — starting endless.");
                    SceneMonitor.WaitingForDoorTrigger = false;
                    EndlessRunManager.Instance?.StartCoroutine(SceneMonitor.OpenAllDoorsDelayed());
                    SceneMonitor.StartEndless();
                    return;
                }
            }
        }
    }

    public static class SceneMonitor
    {
        private static LevelEnum _lastInjectedLevel = LevelEnum.None;
        private static bool _subscribed;

        /// <summary>True while we're in TheParty level, waiting for the
        /// player to touch the first door.</summary>
        public static bool WaitingForDoorTrigger { get; set; }

        /// <summary>True from the moment a scene-load is confirmed to be an
        /// Endless run (PendingEndlessRun consumed) through to returning to
        /// the main menu — wider than EndlessRunManager.RunActive, which
        /// isn't true yet during the earliest part of level load. Used by
        /// GamePatches.cs to block the vanilla "wake up" line without
        /// touching it during a normal story Night 6 playthrough.</summary>
        public static bool IsEndlessActive { get; private set; }

        public static void Init()
        {
            if (_subscribed) return;
            _subscribed = true;
            SceneManager.sceneLoaded += OnUnitySceneLoaded;
        }

        public static void ResetForNewRun()
        {
            _lastInjectedLevel = LevelEnum.None;
            WaitingForDoorTrigger = false;
            IsEndlessActive = false;
            // Defensive: if the player somehow bailed back to the menu
            // after clicking Enter but before the level actually finished
            // loading/injecting, don't leave a stale flag armed for their
            // next unrelated visit to this level.
            EndlessRunManager.PendingEndlessRun = false;
            HubMode.Reset();
            VersusBots.Clear();
            // Put the level's own title text back, so the next load (hub → Endless,
            // Endless → hub) gets its own title instead of the previous one's.
            EndlessTitle.Restore();
        }

        private static void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Unity scene loaded: " + scene.name + " (" + mode + ")");
            ForceHideLoadingScreen();

            // Diagnostic: a sound reported as playing "when the game loads"
            // never showed up in the AudioController.Play() diagnostic
            // patch — timing like that strongly suggests a plain Unity
            // AudioSource baked directly into the level scene (playOnAwake),
            // not something routed through AudioController's code path at
            // all. Not gated on isTheParty/RunActive below — needs to see
            // every scene, including normal story nights.
            if (AudioSourceSceneDiagnostic.Instance == null)
                new GameObject("KE_AudioSourceSceneDiagnostic").AddComponent<AudioSourceSceneDiagnostic>();

            // Main-menu-only, one-shot: scavenge Night 2's own scene for
            // the real washing machine sound. Never triggered mid-run —
            // additively loading an unrelated level's scene during an
            // active Endless run is real risk this mod has no reason to
            // take, and the main menu is always safe/idle.
            if (scene.name == "menu")
            {
                EndlessTitle.Restore(); // the story night gets its own title text back
                Night2AudioScavenger.TryScavenge();
                // Already sitting in this loaded scene — no additive
                // load/unload needed, just grab the real AudioClip
                // reference directly.
                TapeObjective.TryCaptureMenuAmbienceClip();
                // Free, no scene load — checks whether KaleidoscopePortal
                // happens to also be Resources-loadable independent of
                // house_kaleidoscope.unity. If not, this just logs that and
                // does nothing further: KaleidoscopePortalScavenger.
                // ScavengeViaSceneLoad() (real ~8MB additive scene load,
                // confirmed to cause a noticeable menu disruption) is kept
                // available but intentionally not auto-triggered here.
                KaleidoscopePortalScavenger.TryResourcesLoad();
                LogSceneBuildIndex();
            }

            // The washing-machine scavenger's temporary 2_scene is never a
            // real level load — it once consumed the arcade's pending flag.
            if (mode == LoadSceneMode.Additive && Night2AudioScavenger.Busy) return;

            // We care about the main house scene for _6_TheParty.
            // Scene name is "house" (same as vanilla night 1 levels).
            bool isTheParty = GD.currentLevel == LevelEnum._6_TheParty;
            if (!isTheParty) return;

            // _6_TheParty is also the real vanilla "Night 6: The Party"
            // story level — only ever activate Endless here if the player
            // actually chose it from the menu (see
            // EndlessRunManager.PendingEndlessRun's comment). A normal
            // story playthrough reaching this level naturally must stay
            // 100% vanilla — this was a real, reported game-breaking bug
            // (picking up the flashlight during a normal Night 6
            // playthrough silently converted it into an endless run).
            if (!EndlessRunManager.PendingEndlessRun) return;

            // Set as early as possible — before PendingEndlessRun even gets
            // consumed below — so patches gated on IsEndlessActive (the
            // wake-up-line block) catch every occurrence from this exact
            // scene load onward, not just once RunActive eventually goes
            // true several seconds later.
            IsEndlessActive = true;

            if (PendingInject.Instance == null)
                new GameObject("KE_PendingInject").AddComponent<PendingInject>();
        }

        public static void OnLevelReady()
        {
            TryInjectNow();
        }

        public static void TryInjectNow()
        {
            LevelEnum current = GD.currentLevel;
            if (current != LevelEnum._6_TheParty) return;
            if (!EndlessRunManager.PendingEndlessRun) return; // see OnUnitySceneLoaded's comment — same gate, second entry point (GameManager.OnSceneLoadComplete)
            if (current == _lastInjectedLevel) return;
            _lastInjectedLevel = current;

            // Consumed — never let a later, unrelated visit to this level
            // (e.g. actually playing Night 6 for real afterward) re-trigger
            // from a stale flag.
            EndlessRunManager.PendingEndlessRun = false;

            if (VersusMode.PendingMamaLoad && !VersusMode.Facility) // (the Facility's hunter is set up once the Facility is built: FactoryFreeRoam)
            {
                VersusMode.PendingMamaLoad = false;
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Setting up Versus (Mama side).");
                VersusMamaController.Create();
                return;
            }

            if (BeachMode.Pending)
            {
                BeachMode.Pending = false;
                HubMode.Reset();
                BeachMode.Active = true;
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Setting up Summer Vacation (the beach).");
                BeachController.Create();
                return;
            }

            if (RecalledMode.Pending)
            {
                RecalledMode.Pending = false;
                HubMode.Reset();
                RecalledMode.Active = true;
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Setting up Recalled (the town).");
                RecalledController.Create();
                return;
            }

            if (FactoryMode.Pending)
            {
                FactoryMode.Pending = false;
                HubMode.Reset();
                FactoryMode.Active = true;
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Setting up Night Shift (the factory).");
                FactoryController.Create();
                return;
            }

            if (HubMode.Pending)
            {
                HubMode.Pending = false;
                HubMode.Active = true;
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Setting up the arcade HUB.");
                HubController.Create();
                return;
            }

            HubMode.Reset(); // any mode's level load wipes the hub first — it must never float above the house
            BeachMode.Reset(); // (and the beach)
            FactoryMode.Reset(); // (and the factory)
            RecalledMode.Reset(); // (and Recalled's town)
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Setting up for TheParty level.");
            SetupMonoBehaviours();
        }

        /// <summary>
        /// The player's Tattletail color (vanilla recolors, Gold, Diamond). Called the moment
        /// Tattletail exists in the level (EarlyColor), so you never see the story's purple
        /// Tattletail first, and again when the run starts. Safe to call twice.
        /// </summary>
        public static void ApplyChosenColor()
        {
            // Bake the shop's 3D previews from pristine clones (before any recolor below touches the real model) — no-op once every preview exists.
            PreviewBaker.BeginBake();
            PristineTattletail.Capture(); // same reason: friends and party Tattletails get their own colors, not yours

            // Apply the color the player chose on the menu.
            if (RM.recolorManager != null && RM.tattletail != null &&
                EndlessRunManager.PendingRecolor != RecolorType.ORIGINAL)
            {
                RM.recolorManager.RecolorObject(
                    RM.tattletail.tattletail,
                    EndlessRunManager.PendingRecolor);
                KaleidoscopeEndlessPlugin.Log.LogInfo(
                    "KE: Recolor applied: " + EndlessRunManager.PendingRecolor);
            }

            // Gold isn't a real vanilla RecolorType — see GoldRecolor.cs
            // for why this is a separate, self-contained tint step
            // instead of another RecolorManager.RecolorObject() call.
            if (EndlessRunManager.PendingGoldColor && RM.tattletail != null)
                GoldRecolor.Apply(RM.tattletail.tattletail);

            // Diamond isn't a real vanilla RecolorType either — see
            // DiamondRecolor.cs.
            if (EndlessRunManager.PendingDiamondColor && RM.tattletail != null)
                DiamondRecolor.Apply(RM.tattletail.tattletail);

        }

        /// <summary>
        /// Instantiate all persistent MonoBehaviours. Do NOT start the run
        /// yet — we wait for the player to open the first door.
        /// </summary>
        /// <summary>Endless in the Waygetter Facility: the same setup and the same run start as the house (FacilityRunStarter calls this a few seconds in).</summary>
        public static void StartFacilityRun()
        {
            SetupMonoBehaviours();
            WaitingForDoorTrigger = false; // (no first door here)
            StartEndless();
        }

        private static void SetupMonoBehaviours()
        {
            try
            {
                if (EndlessRunManager.Instance == null)
                    new GameObject("EndlessRunManager").AddComponent<EndlessRunManager>();
                if (MamaController.Instance == null)
                    new GameObject("MamaController").AddComponent<MamaController>();
                if (EndlessHud.Instance == null)
                    new GameObject("EndlessHud").AddComponent<EndlessHud>();
                EndlessTraits.Reset();
                if (EndlessTraitsRuntime.Instance == null)
                    new GameObject("KE_Traits").AddComponent<EndlessTraitsRuntime>();
                if (PhoneObjective.Instance == null)
                    new GameObject("KE_PhoneObjectiveController").AddComponent<PhoneObjective>();
                if (ChargingStation.Instance == null)
                    new GameObject("KE_ChargingStationController").AddComponent<ChargingStation>();
                if (NoiseObjective.Instance == null)
                    new GameObject("KE_NoiseObjectiveController").AddComponent<NoiseObjective>();
                if (WashingMachineObjective.Instance == null)
                    new GameObject("KE_WashingMachineObjectiveController").AddComponent<WashingMachineObjective>();
                // Start the real-clip resolve as early as the level loads —
                // see WashingMachineObjective.ResolveClip()'s comment for
                // why this can't wait until StartEndless() like
                // ResolveLocation() does.
                WashingMachineObjective.Instance?.ResolveClip();
                if (TattletailFriendObjective.Instance == null)
                    new GameObject("KE_TattletailFriendObjectiveController").AddComponent<TattletailFriendObjective>();
                if (VaseObjective.Instance == null)
                    new GameObject("KE_VaseObjectiveController").AddComponent<VaseObjective>();
                if (CandleObjective.Instance == null)
                    new GameObject("KE_CandleObjectiveController").AddComponent<CandleObjective>();
                if (TapeObjective.Instance == null)
                    new GameObject("KE_TapeObjectiveController").AddComponent<TapeObjective>();
                if (SpeakerObjective.Instance == null)
                    new GameObject("KE_SpeakerObjectiveController").AddComponent<SpeakerObjective>();
                // No FuseBoxObjective resolve call needed anywhere — its
                // position and sound are both fully fixed/generated, no
                // async resolution required at all.
                if (FuseBoxObjective.Instance == null)
                    new GameObject("KE_FuseBoxObjectiveController").AddComponent<FuseBoxObjective>();
                if (KaleidoscopeEndlessPlugin.DevTools && !FactoryMode.Active && DebugLocationMarker.Instance == null) // E-key marker, dev builds only (the Facility has its own marking tool)
                    new GameObject("KE_DebugLocationMarker").AddComponent<DebugLocationMarker>();
                if (TrueEndingEasterEgg.Instance == null)
                    new GameObject("KE_TrueEndingEasterEggController").AddComponent<TrueEndingEasterEgg>();
                if (PowerUpObjective.Instance == null)
                    new GameObject("KE_PowerUpObjectiveController").AddComponent<PowerUpObjective>();
                if (GadgetController.Instance == null)
                    new GameObject("KE_GadgetController").AddComponent<GadgetController>();
                // Don't call Setup()/ResolveLocations() here — this runs
                // before the level's gameplay scenes (house_night/Player/
                // 6_scene) even finish loading, well before any Interactable
                // becomes active. Called from StartEndless() instead, once
                // "Endless started!" has already fired.

                // Your Tattletail color as soon as he's in the level (not only once the run starts).
                new GameObject("KE_EarlyColor").AddComponent<EarlyColor>();

                // Arm the door trigger hook.
                WaitingForDoorTrigger = true;

                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: MonoBehaviours ready, waiting for first door.");
            }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: Setup error: " + e.Message + "\n" + e.StackTrace);
            }
        }

        /// <summary>
        /// Called from the door patch the moment the player opens the first
        /// door. Starts endless mode.
        /// </summary>
        public static void StartEndless()
        {
            try
            {
                // The vanilla quest system (QuestOrder) is fully blocked
                // during endless runs — see PATCH 3 in GamePatches.cs. The
                // washing machine no longer depends on it at all; it's a
                // real, self-contained object built by
                // WashingMachineObjective.cs instead.
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Vanilla quest order frozen for the duration of the run.");

                // Keep vanilla HUD visible — we use SetNewQuest directly.
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Keeping vanilla HUD for quest text.");

                // Lock boredom so OnBoredomMin doesn't crash via ActivateNextQuest.
                if (RM.tattletail != null)
                    RM.tattletail.GetNeeds().LockBoredom();

                ApplyChosenColor(); // again at the start: in case the level recolored him since he appeared

                // "Diamond Flashlight" run modifier — 60% longer charge +
                // a matching tint, see DiamondFlashlight.cs. Independent of
                // the Diamond Tattletail color pick above (this is a Run
                // Modifiers toggle, not a color option).
                GoldenFlashlight.Apply(RM.flashlight); // Shop's permanent golden flashlight — first, so the one-run Diamond below overrides it
                if (EndlessRunManager.PendingModDiamondFlashlight && RM.flashlight != null)
                    DiamondFlashlight.Apply(RM.flashlight);

                // Shop passives (walking speed, flashlight charge).
                Passives.ApplyToRun();

                // Shop cosmetic beam color — after the Diamond tint so it wins the shared beam color.
                FlashlightBeam.ApplyPending(RM.flashlight);

                // Apply the voice the player chose on the menu (swaps every
                // Tattletail audio line to the alternate narrated set).
                if (RM.tattletail != null)
                {
                    RM.tattletail.SetTattletailType(EndlessRunManager.PendingKaleidoscopeVoice
                        ? TattletailController.TattletailType.EDUCATIONAL
                        : TattletailController.TattletailType.STANDARD);
                    KaleidoscopeEndlessPlugin.Log.LogInfo(
                        "KE: Tattletail voice set: " + (EndlessRunManager.PendingKaleidoscopeVoice ? "Kaleidoscope" : "Standard"));
                }

                // Every run's objective order is now fully randomized
                // (EndlessRunManager.StartRun() no longer forces anything
                // first), which means ANY objective type could end up first
                // — so every type's locations need to be resolved before
                // StartRun() below (which starts the first one
                // synchronously), not just whichever used to be forced.
                // Same lesson learned the hard way with the washing
                // machine: an objective with unresolved locations can't
                // start. NoiseObjective is the one exception — SilenceNoise
                // stays out of the rotation entirely (see the comment on
                // Sequence in EndlessRunManager), so it can resolve later.
                CandleObjective.Instance?.ResolveLocations();
                VaseObjective.Instance?.ResolveLocations();
                WashingMachineObjective.Instance?.ResolveLocation();
                // Must resolve before StartRun() below too — it rings
                // immediately as part of StartRun(), synchronously.
                PhoneObjective.Instance?.ResolveLocation();
                TapeObjective.Instance?.ResolveLocations();
                SpeakerObjective.Instance?.ResolveLocation();
                if (!EndlessRunManager.PendingBirthdayMode && !FactoryMode.EndlessPreview) TrueEndingEasterEgg.Instance?.ResolveLocations(); // story secret — cut from Papa's 10th Birthday!

                EndlessRunManager.Instance?.StartRun();
                if (!FactoryMode.EndlessPreview) ChargingStation.Instance?.Setup(); // (the Facility has its own care stations)
                if (!FactoryMode.EndlessPreview) NoiseObjective.Instance?.ResolveLocations();
                if (!FactoryMode.EndlessPreview) LogFriendQuestCandidates();
                if (!FactoryMode.EndlessPreview) LogCandleCandidates();
                if (!FactoryMode.EndlessPreview) LogTapeTextureCandidates();

                // TESTING ONLY — spawns all 6 marked tape spots at once so
                // they can be walked to and verified in one pass instead of
                // waiting on random chance across many runs. Flip
                // TapeObjective.DebugSpawnAllTapes off once positions are
                // confirmed good and the real 1-of-6 objective is wired in.
                if (TapeObjective.DebugSpawnAllTapes)
                    TapeObjective.Instance?.SpawnAllForTesting();

                GS.acceptInput = true;
                ForceHideLoadingScreen();
                if (LoadingScreenWatchdog.Instance == null)
                    new GameObject("KE_LoadingWatchdog").AddComponent<LoadingScreenWatchdog>();

                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Endless started!");
            }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: StartEndless error: " + e.Message + "\n" + e.StackTrace);
            }
        }

        // ── Diagnostic for the upcoming "find Tattletail's friend" objective ──
        // Unity scene layout (object positions, which prefab is wired into
        // QuestHide.friendToHide, etc.) isn't visible from decompiled code —
        // only from the live scene. Resources.FindObjectsOfTypeAll (not
        // FindObjectsOfType) is used deliberately so inactive objects are
        // included too, same lesson learned from TattletailPlanningProp
        // being invisible to a plain FindObjectsOfType search early on.
        private static void LogFriendQuestCandidates()
        {
            try
            {
                // "present"/"gift"/"box"/"wrapped" added per user: this level
                // has decorative present boxes with a Tattletail figure
                // inside — a Christmas-themed stand-in for the vanilla
                // hide-and-seek friend prop, and guaranteed to already exist
                // in this scene (unlike QuestSeek/QuestHide's object, which
                // may not).
                string[] keywords = { "friend", "hide", "seek", "buddy", "snuggle", "companion", "chatter", "present", "gift", "box", "wrapped" };
                Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
                foreach (Transform t in allTransforms)
                {
                    if (t == null || !t.gameObject.scene.IsValid()) continue; // skip prefab/asset-only instances
                    string nameLower = t.gameObject.name.ToLowerInvariant();
                    foreach (string kw in keywords)
                    {
                        if (nameLower.Contains(kw))
                        {
                            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [FRIEND-DIAG] Candidate '" + t.gameObject.name +
                                "' active=" + t.gameObject.activeInHierarchy + " pos=" + t.position);
                            break;
                        }
                    }
                }

                QuestSeek[] seekComponents = Resources.FindObjectsOfTypeAll<QuestSeek>();
                foreach (QuestSeek qs in seekComponents)
                {
                    if (qs == null || !qs.gameObject.scene.IsValid()) continue;
                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [FRIEND-DIAG] QuestSeek on '" + qs.gameObject.name +
                        "' active=" + qs.gameObject.activeInHierarchy + " pos=" + qs.transform.position);
                }

                QuestHide[] hideComponents = Resources.FindObjectsOfTypeAll<QuestHide>();
                foreach (QuestHide qh in hideComponents)
                {
                    if (qh == null || !qh.gameObject.scene.IsValid()) continue;
                    string friendInfo = (qh.friendToHide != null)
                        ? qh.friendToHide.name + " active=" + qh.friendToHide.activeInHierarchy + " pos=" + qh.friendToHide.transform.position
                        : "null";
                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [FRIEND-DIAG] QuestHide on '" + qh.gameObject.name +
                        "' active=" + qh.gameObject.activeInHierarchy + " pos=" + qh.transform.position + " friendToHide=" + friendInfo);
                }

                if (seekComponents.Length == 0 && hideComponents.Length == 0)
                    KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [FRIEND-DIAG] No QuestSeek/QuestHide components found in this scene at all — the hide-and-seek friend prop may not exist in TheParty's scene hierarchy.");
            }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: LogFriendQuestCandidates crashed: " + e.Message + "\n" + e.StackTrace);
            }
        }

        // ── Diagnostic for the upcoming "light the candles" objective ──────────
        // Candle is a real, dedicated vanilla component (SetCandle(bool) —
        // plays a flame ParticleSystem + restores emissive glow) confirmed
        // via decompile, so we can search for real Candle instances
        // directly instead of guessing by name like the vase scan had to.
        // The "Light the candles" voice line's audio ID isn't visible in
        // decompiled code either (quest-linked audio IDs live in scene/
        // quest asset data, not the DLL) — testing plausible candidates
        // against the live AudioController catalog instead of guessing one
        // and hoping.
        private static void LogCandleCandidates()
        {
            try
            {
                Candle[] candles = Resources.FindObjectsOfTypeAll<Candle>();
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [CANDLE-DIAG] " + candles.Length + " Candle components found:");
                foreach (Candle c in candles)
                {
                    if (c == null || !c.gameObject.scene.IsValid()) continue;
                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [CANDLE-DIAG] '" + c.gameObject.name + "' active=" + c.gameObject.activeInHierarchy +
                        " pos=" + c.transform.position + " startOn=" + c.startOn);
                }

                string[] candidateAudioIDs = { "tt_light_candles", "tt_candles", "tt_lightcandle", "tt_light_candle", "tt_findcandles", "tt_find_candles", "light_candles", "candles" };
                foreach (string id in candidateAudioIDs)
                {
                    bool valid = AudioController.IsValidAudioID(id);
                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [CANDLE-DIAG] Audio ID '" + id + "' valid=" + valid);
                }
            }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: LogCandleCandidates crashed: " + e.Message + "\n" + e.StackTrace);
            }
        }

        // ── Diagnostic for the "Rewind the Tapes" objective's placeholder art ──
        // TapeObjective's own real-prop search (Transform name match, live
        // scene objects) already came up empty — no VHS-tape GameObject
        // exists in this level. This is one level lower: scan every
        // Texture2D/Material actually loaded in memory right now for a
        // name suggestive of tape/VHS/cassette/static/label art, the same
        // "log ground truth instead of guessing an asset name" approach as
        // every other real-asset resolve in this project. Only catches
        // what's already loaded for THIS scene — a texture nothing here
        // currently references won't show up even if it exists elsewhere
        // in the game's data.
        private static void LogTapeTextureCandidates()
        {
            try
            {
                string[] keywords = { "tape", "vhs", "cassette", "static", "label" };

                Texture2D[] textures = Resources.FindObjectsOfTypeAll<Texture2D>();
                int texHits = 0;
                foreach (var tex in textures)
                {
                    if (tex == null) continue;
                    string nameLower = tex.name.ToLowerInvariant();
                    foreach (string kw in keywords)
                    {
                        if (nameLower.Contains(kw))
                        {
                            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TAPE-TEX-DIAG] Texture2D '" + tex.name + "' " + tex.width + "x" + tex.height);
                            texHits++;
                            break;
                        }
                    }
                }

                Material[] materials = Resources.FindObjectsOfTypeAll<Material>();
                int matHits = 0;
                foreach (var mat in materials)
                {
                    if (mat == null) continue;
                    string nameLower = mat.name.ToLowerInvariant();
                    foreach (string kw in keywords)
                    {
                        if (nameLower.Contains(kw))
                        {
                            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TAPE-TEX-DIAG] Material '" + mat.name + "' shader=" +
                                (mat.shader != null ? mat.shader.name : "none"));
                            matHits++;
                            break;
                        }
                    }
                }

                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TAPE-TEX-DIAG] Scanned " + textures.Length + " textures / " + materials.Length +
                    " materials — " + texHits + " texture / " + matHits + " material name match(es).");
            }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: LogTapeTextureCandidates crashed: " + e.Message + "\n" + e.StackTrace);
            }
        }

        // ── Diagnostic for the Version 2.0 portal easter egg ───────────────────
        // Static analysis of the game's own data files found "KaleidoscopePortal"
        // (plus "houseKaleidoscopeWarped") embedded directly in 'level15'
        // (grep of the raw file), with sharedassets15.assets confirmed as the
        // only .assets file referencing it — strongly suggesting build index 15
        // is the actual "opening the kaleidoscope" scene. Scene names can be
        // stripped from a release build, but SceneUtility.GetScenePathByBuildIndex
        // reads straight from BuildSettings (always present, needed for
        // LoadScene(int)), so this is ground truth instead of a guess — logs
        // every build index's path once, main-menu-only, same safety reasoning
        // as Night2AudioScavenger (never touches anything mid-run).
        private static void LogSceneBuildIndex()
        {
            try
            {
                int count = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;
                for (int i = 0; i < count; i++)
                {
                    string path = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [SCENE-DIAG] buildIndex " + i + " -> '" + path + "'");
                }
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [SCENE-DIAG] " + count + " scene(s) in build settings — index 15 is the one to check for the Kaleidoscope portal.");
            }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: LogSceneBuildIndex crashed: " + e.Message + "\n" + e.StackTrace);
            }
        }

        public static System.Collections.IEnumerator OpenAllDoorsDelayed()
        {
            yield return null;
            yield return null;

            int opened = 0;

            // IsItUnlocked is already patched to return true during endless.
            // We just need to visually open the door animators.

            // _6_TheParty uses HouseManager/HouseDoor, not DoorTrigger.
            // Call SetDoor(type, true) for every DoorType.
            HouseManager hm = Object.FindObjectOfType<HouseManager>();
            if (hm != null && hm.houseDoors != null)
            {
                HouseManager.DoorType[] doorTypes = (HouseManager.DoorType[])
                    System.Enum.GetValues(typeof(HouseManager.DoorType));
                foreach (HouseManager.DoorType dt in doorTypes)
                {
                    // ParentBedroom ("Mom's door") left closed on purpose —
                    // confirmed game-breaking when propped open: Mama can
                    // never catch the player while they're standing inside
                    // that room, turning it into a permanent safe zone.
                    // Force it explicitly closed rather than just skipping
                    // it, so its state is guaranteed regardless of whatever
                    // it defaulted to.
                    if (dt == HouseManager.DoorType.ParentBedroom)
                    {
                        try
                        {
                            hm.SetDoor(dt, false);
                            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Kept door closed (Mama can't catch you inside): " + dt);
                        }
                        catch (System.Exception e)
                        {
                            KaleidoscopeEndlessPlugin.Log.LogWarning("KE: SetDoor(false) failed for " + dt + ": " + e.Message);
                        }
                        continue;
                    }

                    try
                    {
                        hm.SetDoor(dt, true);
                        KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Opened door: " + dt);
                        opened++;
                    }
                    catch (System.Exception e)
                    {
                        KaleidoscopeEndlessPlugin.Log.LogWarning("KE: SetDoor failed for " + dt + ": " + e.Message);
                    }
                }
            }
            else
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: HouseManager not found or houseDoors null.");
            }

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Total doors opened: " + opened);

            // BasementDoor is a real walk-up Interactable (confirmed via
            // ChargingStation's layer-source lookup), separate from
            // HouseManager's ambient DoorType system above — it needs an
            // actual interaction to open, not a SetDoor() call. Auto-firing
            // OnInteractComplete(fromAutoComplete: true) is the same sanctioned
            // vanilla mechanism used elsewhere (e.g. MamaManager auto-completing
            // interactions) instead of simulating a real player interaction.
            Interactable[] allInteractables = Object.FindObjectsOfType<Interactable>();
            foreach (Interactable it in allInteractables)
            {
                if (it != null && it.gameObject.name == "BasementDoor")
                {
                    try
                    {
                        it.OnInteractComplete(true);
                        KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Auto-opened BasementDoor.");
                    }
                    catch (System.Exception e)
                    {
                        KaleidoscopeEndlessPlugin.Log.LogWarning("KE: Auto-open BasementDoor failed: " + e.Message);
                    }
                    break;
                }
            }

            // Background decorative Tattletail "friend" props (source of the
            // "hihihi" idle-chatter loop — confirmed via [AUDIO-DIAG]: tt_laugh
            // repeats on 'tattletail3@Friend_1'/'tattletail3@Friend_2', bypassing
            // RequestTalk entirely) and the story's interactable Christmas
            // presents are both pure clutter in Endless mode. Deactivating
            // instead of destroying — cheaper, and no longer anything this
            // mod needs to clone from: TattletailFriendObjective/
            // TrueEndingEasterEgg both clone RM.tattletail.tattletail (the
            // player's own always-present model) instead of these props,
            // so deactivating them here can't race anything.
            DeactivateByKeywords("FRIEND-REMOVE", FriendKeywords);
            DeactivateByKeywords("PRESENT-REMOVE", PresentKeywords);
        }

        private static readonly string[] FriendKeywords = { "tattletail3@friend", "friendplacer", "friends" };
        private static readonly string[] PresentKeywords =
        {
            "presentreveal", "giftbox", "tattletailbox", "ttbox",
            "boxnormal", "boxopen", "boxclosed", "boxtop", "boxbottom", "christmaslightsbox"
        };

        private static void DeactivateByKeywords(string logTag, string[] keywords)
        {
            try
            {
                Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
                int count = 0;
                foreach (Transform t in all)
                {
                    if (t == null || !t.gameObject.scene.IsValid()) continue;
                    if (!t.gameObject.activeSelf) continue; // already inactive, nothing to do
                    if (t.gameObject.name.StartsWith("KE_")) continue; // never touch our own spawned objects

                    string nameLower = t.gameObject.name.ToLowerInvariant();
                    foreach (string kw in keywords)
                    {
                        if (nameLower.Contains(kw))
                        {
                            t.gameObject.SetActive(false);
                            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [" + logTag + "] Deactivated '" + t.gameObject.name + "'");
                            count++;
                            break;
                        }
                    }
                }
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [" + logTag + "] Deactivated " + count + " object(s).");
            }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: DeactivateByKeywords(" + logTag + ") crashed: " + e.Message + "\n" + e.StackTrace);
            }
        }

        public static void ForceHideLoadingScreen()
        {
            var loader = Object.FindObjectOfType<SceneLoader>();
            if (loader != null && loader.graphicsHolder != null && loader.graphicsHolder.activeSelf)
            {
                loader.graphicsHolder.SetActive(false);
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Force-hid vanilla loading screen.");
            }
            GS.acceptInput = true;
        }

        public static void CheckScene() { }
    }

    /// <summary>
    /// Waits briefly after Unity reports the scene loaded, then triggers
    /// our setup. Gives vanilla Awake()/Start() time to populate RM.*.
    /// </summary>
    public class PendingInject : MonoBehaviour
    {
        public static PendingInject Instance { get; private set; }
        private float _timer;
        private const float WAIT_TIME = 1.5f;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < WAIT_TIME) return;
            SceneMonitor.TryInjectNow();
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// One-shot, per-scene-load diagnostic: a couple seconds after any
    /// scene loads (enough time for the scene's own Awake()/Start() calls
    /// to have started any playOnAwake AudioSources), scans every
    /// AudioSource actually in the scene and logs the ones that are
    /// playing or set to auto-play — clip name, loop, volume, position.
    /// Complements Patch_AudioController_Play_Diagnostic in GamePatches.cs,
    /// which only ever sees sounds routed through AudioController.Play();
    /// this catches the other case, a sound baked directly into the level
    /// as a plain Unity AudioSource component.
    /// </summary>
    public class AudioSourceSceneDiagnostic : MonoBehaviour
    {
        public static AudioSourceSceneDiagnostic Instance { get; private set; }
        private float _timer;
        private const float WAIT_TIME = 2f;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < WAIT_TIME) return;

            AudioSource[] sources = Resources.FindObjectsOfTypeAll<AudioSource>();
            int loggedCount = 0;
            foreach (var src in sources)
            {
                if (src == null || !src.gameObject.scene.IsValid()) continue; // skip prefab/asset-only instances
                if (src.clip == null) continue;
                if (!src.isPlaying && !src.playOnAwake) continue; // only ones actually making (or about to make) noise

                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [AUDIOSRC-DIAG] '" + src.gameObject.name + "' clip='" + src.clip.name +
                    "' isPlaying=" + src.isPlaying + " playOnAwake=" + src.playOnAwake + " loop=" + src.loop +
                    " volume=" + src.volume + " mute=" + src.mute + " pos=" + src.transform.position);
                loggedCount++;
            }
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [AUDIOSRC-DIAG] Scanned " + sources.Length + " AudioSource components, " + loggedCount + " playing/auto-play.");

            Destroy(gameObject);
        }
    }

    public class LoadingScreenWatchdog : MonoBehaviour
    {
        public static LoadingScreenWatchdog Instance { get; private set; }
        private float _timer;
        private const float WATCH_DURATION = 5f;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            SceneMonitor.ForceHideLoadingScreen();
            if (_timer >= WATCH_DURATION)
                Destroy(gameObject);
        }
    }
}

namespace KaleidoscopeEndless
{
    /// <summary>
    /// An untouched copy of the level's Tattletail, taken before the player's color goes on:
    /// Tattletail's friends and the party Tattletails are cloned from it, then given a random
    /// color of their own (cloning the player's Gold/Diamond one would carry its fur and sparkles).
    /// </summary>
    public static class PristineTattletail
    {
        private static GameObject _copy, _from;

        public static void Capture()
        {
            GameObject src = RM.tattletail != null ? RM.tattletail.tattletail : null;
            if (src == null || (_copy != null && _from == src)) return; // once per level: later calls come after the recolor
            if (_copy != null) Object.Destroy(_copy);
            _copy = Object.Instantiate(src, src.transform.position, src.transform.rotation);
            _copy.name = "KE_PristineTattletail";
            _copy.SetActive(false);
            _from = src;
        }

        /// <summary>What to clone: the untouched copy, or the real one if there isn't a copy (then it wears the player's color).</summary>
        public static GameObject Source
        {
            get { return _copy != null ? _copy : (RM.tattletail != null ? RM.tattletail.tattletail : null); }
        }
    }

    /// <summary>Waits for the level's Tattletail (and the flashlight, which the shop's pictures also need), then gives him the player's color straight away.</summary>
    public class EarlyColor : MonoBehaviour
    {
        private int _readyFrames;
        private float _giveUpAt;

        private void Start() { _giveUpAt = Time.unscaledTime + 60f; }

        private void Update()
        {
            if (Time.unscaledTime > _giveUpAt) { Destroy(gameObject); return; } // the run start still applies it
            bool ready = RM.tattletail != null && RM.tattletail.tattletail != null && RM.recolorManager != null && RM.flashlight != null;
            _readyFrames = ready ? _readyFrames + 1 : 0;
            if (_readyFrames < 3) return; // a couple of frames for the level's own setup
            SceneMonitor.ApplyChosenColor();
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Tattletail's color applied as soon as he appeared.");
            Destroy(gameObject);
        }
    }
}
