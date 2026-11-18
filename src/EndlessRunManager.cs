using UnityEngine;

namespace KaleidoscopeEndless
{
    public enum ObjectiveType
    {
        None,
        WashingMachine,
        FlashlightSurvival,
        SilenceNoise,
        FindFriend,
        BreakVases,
        LightCandles,
        RewindTape,
        SilenceSpeaker,
        RestartFuseBox,
    }

    public class EndlessRunManager : MonoBehaviour
    {
        public static EndlessRunManager Instance { get; private set; }

        public bool RunActive { get; private set; }
        public float RunTime { get; private set; }
        public int ObjectivesCompleted { get; private set; }
        public int CloseCalls { get; private set; }
        public int TotalCloseCalls { get; private set; }
        public int LoopNumber { get; private set; }
        public ObjectiveType CurrentObjective { get; private set; }
        // Set before level loads from the customization overlay
        public static RecolorType PendingRecolor = RecolorType.ORIGINAL;
        // Not a real vanilla RecolorType — see GoldRecolor.cs. When true,
        // SceneBootstrap.StartEndless() applies the custom gold tint on top
        // of (instead of) the normal PendingRecolor path.
        public static bool PendingGoldColor = false;
        // Not a real vanilla RecolorType either — see DiamondRecolor.cs.
        public static bool PendingDiamondColor = false;
        public static bool PendingKaleidoscopeVoice = false;
        public static bool PendingKaleidoscopeMama = false;
        // Fixed value for _difficultyMult for the whole run, chosen from
        // the submenu's Easy/Normal/Hard/Impossible picker — no longer
        // auto-ramps per loop on top of this (see CompleteObjective()).
        public static float PendingDifficultyMult = 1f;
        // Label text only (e.g. "Hard") — set alongside PendingDifficultyMult
        // so a human-readable difficulty name is available without needing
        // to reverse-lookup a float back to a label.
        public static string PendingDifficultyLabel = "Normal";

        // ── Run Modifiers ────────────────────────────────────────────────────
        // Set independently (any combination, not mutually exclusive) from
        // RunModifiersSubmenu; copied into instance state in StartRun() the
        // same way PendingDifficultyMult is. See RunModifiersSubmenu.cs for
        // the UI and RunModifiers.cs for the reflection-based need-drain-rate
        // application.
        public static bool PendingModLowBattery = false;
        public static bool PendingModNoRest = false;
        public static bool PendingModMaxMamaAggro = false;
        public static bool PendingModBatteriesNotIncluded = false;
        public static bool PendingModAlwaysHungry = false;
        public static bool PendingModBrushingIsKey = false;
        public static bool PendingModPowerUps = false;
        // Set from the single-run "Diamond Flashlight" Shop power up (RunPowerUps).
        public static bool PendingModDiamondFlashlight = false;
        // Flashlight beam color name picked on the menu (must be a Shop purchase), null = default beam.
        public static string PendingBeamColor = null;

        // True only when the player actually clicked "Enter the
        // Kaleidoscope" — set by EndlessSubmenu.OnEnter()/OnHostStartedLevel()
        // right before GameManager.PlayLevel(_6_TheParty), and consumed
        // (cleared back to false) the moment SceneMonitor sees it
        // (SceneMonitor.OnUnitySceneLoaded/TryInjectNow). _6_TheParty is
        // also the real vanilla "Night 6: The Party" story level — without
        // this flag, SceneMonitor had no way to tell "the player wants
        // Endless" apart from "the player naturally reached this level
        // during a normal story playthrough," and would silently hijack a
        // normal playthrough into an endless run the moment the player
        // picked up their flashlight. Confirmed as a real, reported
        // game-breaking bug.
        public static bool PendingEndlessRun = false;

        // Papa's 10th Birthday! — a separate free-roam mode that reuses all of
        // Endless's level setup (doors open, story stripped out, cosmetics)
        // but has no objectives, Mama, phone, events or shards. Set by the
        // menu's birthday button, cleared by every Endless launch path.
        public static bool PendingBirthdayMode = false;
        public bool BirthdayMode { get; private set; }

        private float _difficultyMult = 1f;
        // Human-readable record of what the last (or current) run was
        // configured with — read by StatsUI, not used for any actual
        // gameplay logic (that's _difficultyMult/the Mod* flags below).
        public string DifficultyLabel { get; private set; } = "Normal";

        // Run Modifiers — copied from the Pending* statics in StartRun().
        // ModLowBattery/ModMaxMamaAggro/ModNoRest are public since other
        // classes (a Harmony patch, MamaController, StatsUI) need to
        // read them at runtime. BatteriesNotIncluded/AlwaysHungry/
        // BrushingIsKey don't drive any ongoing per-frame logic (they're
        // applied once, directly, in StartRun() — see RunModifiers.cs) but
        // are still recorded here so StatsUI can report which were on.
        public bool ModLowBattery { get; private set; }
        public bool ModMaxMamaAggro { get; private set; }
        public bool ModNoRest { get; private set; }
        public bool ModBatteriesNotIncluded { get; private set; }
        public bool ModAlwaysHungry { get; private set; }
        public bool ModBrushingIsKey { get; private set; }
        public bool ModPowerUps { get; private set; }
        public bool ModDiamondFlashlight { get; private set; }

        // True only on the exact run where the corresponding survival
        // milestone was actually crossed (see Tick()) — StatsUI uses these
        // to show "just unlocked" once, never repeated on a later run.

        // Mama's own difficulty scaling (alert duration, patrol speed —
        // see MamaController.ActivateMamaForObjective) is deliberately kept
        // separate from _difficultyMult here: "No More Mama!" should only
        // ever affect how aggressive SHE is, not the whole run's other
        // difficulty-scaled numbers (e.g. the flashlight-survival timer).
        private const float MAX_MAMA_AGGRO_MULT = 10f;
        private float MamaDifficultyMult() => ModMaxMamaAggro ? MAX_MAMA_AGGRO_MULT : _difficultyMult;
        /// <summary>For gadgets that send Mama away and let her come back at this run's difficulty.</summary>
        public float CurrentMamaDifficulty { get { return MamaDifficultyMult(); } }

        private bool _objectiveActive;
        private float _objectiveTimer;
        private bool _inRestPhase;
        public bool InRestPhase { get { return _inRestPhase; } }
        private float _restTimer;
        private bool _phoneRinging;
        private float _phoneTimer;
        private string _currentObjectiveHudText;
        private float _flashlightSurvivalBaseTime;
        private bool _flashlightWasGlowing;

        /// <summary>Read by MamaController.ActivateMamaForObjective() (can't (re)activate mid-call) and the objective-interact handlers below (all pause while ringing).</summary>
        public bool PhoneIsRinging => _phoneRinging;

        // Randomized per objective start (see StartFlashlightSurvival) so
        // the survive time isn't always the same — 90 kept as the max.
        private static readonly float[] FlashlightSurvivalTimeOptions = { 30f, 60f, 90f };
        private static int CLOSE_CALL_LIMIT { get { return EndlessTraits.CloseCallLimit(); } }   // (3; a trait can change it)
        private static float REST_DURATION { get { return EndlessTraits.RestSeconds(); } }        // (30 s; a trait can change it)
        private const float PHONE_RING_DURATION     = 60f;

        // SilenceNoise deliberately left out of rotation — since it's
        // restricted to the washer only (kitchen/garage were never
        // reliable), it's now mechanically identical to WashingMachine
        // ("walk to the washer, interact"). Having both active would mean
        // doing that exact same action twice in the same loop. Its code
        // (NoiseObjective.cs, StartSilenceNoise/OnNoiseSourceFound, the
        // Harmony patch) is left in place, not deleted, in case
        // kitchen/garage positions get confirmed later and it's worth
        // reviving as a real 3-spot search again.
        private static readonly ObjectiveType[] Sequence =
        {
            ObjectiveType.WashingMachine,
            ObjectiveType.FlashlightSurvival,
            ObjectiveType.FindFriend,
            ObjectiveType.BreakVases,
            ObjectiveType.LightCandles,
            ObjectiveType.RewindTape,
            ObjectiveType.SilenceSpeaker,
            ObjectiveType.RestartFuseBox,
        };

        // How many completed objectives make up one "loop" — the phone
        // rings once every LOOP_LENGTH completions (see CompleteObjective()).
        // Deliberately smaller than Sequence.Length: the shuffled bag below
        // is one continuous pool that keeps refilling/drawing regardless of
        // loop boundaries, so loops don't need to (and currently don't)
        // line up with a full pass through every objective type.
        private static int LOOP_LENGTH { get { return EndlessTraits.LoopLength(); } }   // (every 3rd objective; a trait can change it)
        private const int VERSUS_OBJECTIVES_BEFORE_EXFIL = 10; // Versus: 10 tasks before the phone first rings

        // Shuffled bag: every objective type goes in once per refill, drawn
        // in a randomized order with no immediate repeats across a refill
        // boundary — independent of the LOOP_LENGTH-based phone-ring cycle
        // above.
        private readonly System.Collections.Generic.List<ObjectiveType> _objectiveQueue =
            new System.Collections.Generic.List<ObjectiveType>();
        private ObjectiveType _lastDequeuedType = ObjectiveType.None;

        // TESTING ONLY — forces this type to be the very first objective
        // of the run (once, then the rest of the bag stays randomized
        // normally) so it can be reached immediately instead of waiting on
        // random chance. FindFriend check done — back to null, every run
        // fully randomized again.
        public static ObjectiveType? DebugForceFirstObjective = null;

        private ObjectiveType NextObjectiveType()
        {
            bool isVeryFirstDraw = _lastDequeuedType == ObjectiveType.None;

            if (_objectiveQueue.Count == 0)
            {
                _objectiveQueue.AddRange(Sequence);
                if (FactoryMode.EndlessPreview) _objectiveQueue.Remove(ObjectiveType.FlashlightSurvival); // (the Facility has no "keep the flashlight open" objective)
                for (int i = _objectiveQueue.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    var tmp = _objectiveQueue[i]; _objectiveQueue[i] = _objectiveQueue[j]; _objectiveQueue[j] = tmp;
                }

                // Each bag is shuffled independently, so its first item can
                // coincidentally match the last item of the previous bag —
                // the two back-to-back repeats seen in testing
                // (FlashlightSurvival->FlashlightSurvival,
                // FindFriend->FindFriend) were exactly this boundary case.
                // Swapping with any other slot is always safe here since
                // every item in a freshly-built bag is a distinct type.
                if (_objectiveQueue[0] == _lastDequeuedType && _objectiveQueue.Count > 1)
                {
                    var tmp = _objectiveQueue[0]; _objectiveQueue[0] = _objectiveQueue[1]; _objectiveQueue[1] = tmp;
                }

                if (isVeryFirstDraw && DebugForceFirstObjective.HasValue)
                {
                    int idx = _objectiveQueue.IndexOf(DebugForceFirstObjective.Value);
                    if (idx > 0)
                    {
                        var tmp = _objectiveQueue[0]; _objectiveQueue[0] = _objectiveQueue[idx]; _objectiveQueue[idx] = tmp;
                    }
                }
            }
            ObjectiveType next = _objectiveQueue[0];
            _objectiveQueue.RemoveAt(0);
            _lastDequeuedType = next;
            return next;
        }

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Tick()
        {
            if (!RunActive) return;
            RunTime += Time.deltaTime;
            if (BirthdayMode) return; // free roam: no events, rest, phone or objectives

            _eventTimer += Time.deltaTime;
            if (_eventTimer >= EndlessEvents.TIMED_INTERVAL)
            {
                _eventTimer = 0f;
                EndlessEvents.RollTimed(this);
            }

            // Checked before rest/objective — the phone fully overrides
            // whatever else is going on the same way rest phase already
            // does (same "set text once, plain countdown in code not on
            // screen" shape as StartRestPhase()/EndRestPhase() below).
            if (_phoneRinging)
            {
                _phoneTimer -= Time.deltaTime;
                if (_phoneTimer <= 0f) EndPhoneRing();
                return;
            }

            if (_inRestPhase)
            {
                _restTimer -= Time.deltaTime;
                // Even throttled to once-per-second, re-pushing SuperTextMesh
                // text kept restarting its character-reveal coroutine before
                // it could finish drawing — visibly "bouncing" mid-word every
                // second instead of ever completing. Set it once in
                // StartRestPhase() and never touch it again for the rest of
                // the rest period — a static duration instead of a live
                // countdown, but it actually stays readable.
                if (_restTimer <= 0f) EndRestPhase();
                return;
            }

            if (_objectiveActive) TickObjective();
        }

        // ── Phone (exfil) ────────────────────────────────────────────────────
        // Mirrors StartRestPhase()/EndRestPhase() exactly on purpose — same
        // "set the HUD text once, count down in code, never re-push it"
        // shape, just with a real interactable instead of a fixed timer and
        // a way out (answering) instead of an inevitable end.

        /// <summary>Called once per completed loop from CompleteObjective() — the phone rings.</summary>
        private void StartPhoneRing()
        {
            if (_phoneRinging) return;
            _phoneRinging = true;
            _phoneTimer = PHONE_RING_DURATION;
            PhoneObjective.Instance?.SpawnMarker();
            AudioController.Play("phone_ring_loop", RM.fpsController != null ? RM.fpsController.transform : null);
            _currentObjectiveHudText = "The phone is ringing!\nPick it up to exfil, or let it ring to keep playing. (" + (int)PHONE_RING_DURATION + "s)";
            if (RM.hud != null) RM.hud.SetNewQuest(_currentObjectiveHudText);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Phone] Ringing — 60s to answer.");
        }

        /// <summary>Timeout path — call missed, nothing changes except Mama can resume.</summary>
        private void EndPhoneRing()
        {
            _phoneRinging = false;
            PhoneObjective.Instance?.DespawnMarker();
            AudioController.Stop("phone_ring_loop");
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Phone] Not answered in time — call missed, next objective.");
            // The ring was the break between objectives — carry straight on.
            StartNextObjective();
        }

        /// <summary>Called from the phone's OnInteractComplete Harmony patch — answered, exfil.</summary>
        public void OnPhoneAnswered()
        {
            if (!_phoneRinging) return;
            _phoneRinging = false;
            PhoneObjective.Instance?.DespawnMarker();
            AudioController.Stop("phone_ring_loop");
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Phone] Answered — exfil.");
            EndRun(false); // not a death
        }

        public void StartRun()
        {
            DiamondFlashlight.ResetCache(); // fresh level = fresh real prefab values to cache from, not a previous run's already-boosted ones
            BirthdayMode = PendingBirthdayMode;
            if (BirthdayMode) { StartBirthdayRun(); return; }
            RunActive            = true;
            RunTime              = 0f;
            LuckyNight           = EndlessTitle.PendingLucky;
            _eventTimer          = 0f;
            _eventRolledThisRun  = false;
            _giftBlocks          = 0;
            ResetChaos();
            _runShardsEarned     = 0;
            ObjectivesCompleted  = 0;
            LoopNumber           = 0;
            _difficultyMult      = PendingDifficultyMult;
            DifficultyLabel      = PendingDifficultyLabel;
            ModLowBattery        = PendingModLowBattery;
            ModMaxMamaAggro      = PendingModMaxMamaAggro;
            ModNoRest            = PendingModNoRest;
            ModBatteriesNotIncluded = PendingModBatteriesNotIncluded;
            ModAlwaysHungry      = PendingModAlwaysHungry;
            ModBrushingIsKey     = PendingModBrushingIsKey;
            ModPowerUps          = PendingModPowerUps;
            ModDiamondFlashlight = PendingModDiamondFlashlight;
            // Baseline balance change, every run, not a togglable Run
            // Modifier — hunger/dirtiness always get a survivability
            // buffer ("+40% HP"). Always Hungry/Brushing is Key halve
            // that buffer instead of stacking a separate speed-up on top
            // (see RunModifiers.cs for why: stacking landed net faster
            // than unmodified vanilla). Battery has no buffer to begin
            // with, so its own modifier is unaffected.
            RunModifiers.ApplyNeedBalanceBuffs(PendingModAlwaysHungry, PendingModBrushingIsKey);
            RunModifiers.ApplyBatteryModifier(PendingModBatteriesNotIncluded);
            CloseCalls      = 0;
            TotalCloseCalls = 0;
            CurrentObjective     = ObjectiveType.None;
            _objectiveQueue.Clear();
            _lastDequeuedType    = ObjectiveType.None;
            // Answering the exfil phone call ends the run via EndRun()
            // regardless of what was going on at the time — including
            // mid-rest-phase, since the 60s ring outlasts the 30s rest.
            // Previously EndRun() was only ever reached from an active
            // objective (both death causes gate on _objectiveActive), so
            // this was never reachable before; now it must be reset here
            // or a stale true/leftover timer would corrupt the very first
            // Tick() of the next run.
            _inRestPhase = false;
            _restTimer   = 0f;

            // Every objective (including the very first one of the run) now
            // comes from the normal shuffled-bag queue — no more forcing a
            // specific type first. Leaving the queue empty here is enough:
            // StartNextObjective() -> NextObjectiveType() refills and
            // shuffles it the moment it's asked for the first objective.
            // All testing is done; every run's order is a fresh shuffle now.

            PhoneObjective.Instance?.DespawnMarker();
            _phoneRinging = false;
            _phoneTimer   = 0f;
            PowerUpObjective.Instance?.CleanUp();
            RunPowerUps.ApplyToRun(); // Shop power ups bought for this run (start totem, head start battery, steady hands)
            EndlessTraits.BeginRun(); // 3.7: the profile (your two traits)

            // Phone does NOT ring at run start — that was only ever a debug
            // shortcut for testing the call quickly. Real design: it rings
            // once every full loop is completed (see CompleteObjective()).

            TeleportPickupsNearPlayer();
            StartNextObjective();
        }

        /// <summary>
        /// Papa's 10th Birthday!: the run is "active" (so every Endless patch
        /// — open doors, frozen story, care stations — stays on) but nothing
        /// is scheduled: no objectives, no Mama, no phone, no events, no
        /// shards, no modifiers. Just the pickups to grab and a house to
        /// walk around in.
        /// </summary>
        private void StartBirthdayRun()
        {
            RunActive            = true;
            RunTime              = 0f;
            LuckyNight           = false;
            ObjectivesCompleted  = 0;
            LoopNumber           = 0;
            CloseCalls           = 0;
            TotalCloseCalls      = 0;
            _difficultyMult      = 1f;
            DifficultyLabel      = "Normal";
            ModLowBattery = ModMaxMamaAggro = ModNoRest = ModBatteriesNotIncluded = false;
            ModAlwaysHungry = ModBrushingIsKey = ModPowerUps = ModDiamondFlashlight = false;
            _runShardsEarned     = 0;
            _objectiveActive     = false;
            _inRestPhase         = false;
            CurrentObjective     = ObjectiveType.None;
            _objectiveQueue.Clear();
            ResetChaos();
            _phoneRinging = false;
            PhoneObjective.Instance?.DespawnMarker();
            PowerUpObjective.Instance?.CleanUp();

            TeleportPickupsNearPlayer();
            BirthdayDecor.Spawn(); // Mama at the first marked spot, the party table at the second
            RecipeCard.Spawn();    // every recipe, on a kitchen wall
            if (RM.hud != null) RM.hud.SetNewQuest(BirthdayTask.TEXT); // the mode's one permanent task
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Birthday] Papa's 10th Birthday! started — free roam, no objectives.");
        }

        /// <summary>
        /// Finds any Interactable configured to grant the flashlight or the
        /// Tattletail (i.e. the level's normal pickup objects) and moves
        /// them right in front of the player, so picking them up still goes
        /// through the game's own normal interaction system.
        /// </summary>
        private void TeleportPickupsNearPlayer()
        {
            if (FactoryMode.EndlessPreview) return; // (the Facility: Tattletail is already in your arms, and the house's pickups are far away)
            if (RM.fpsController == null)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: fpsController is null — cannot place pickups.");
                return;
            }

            Vector3 basePos = RM.fpsController.transform.position
                             + RM.fpsController.transform.forward * 1.3f;
            Vector3 offset = Vector3.zero;
            int found = 0;

            Interactable[] all = UnityEngine.Object.FindObjectsOfType<Interactable>();
            foreach (var it in all)
            {
                if (it == null || it.actions == null || it.actions.interactActions == null) continue;

                bool isFlashlight = false, isTattletail = false;
                foreach (var a in it.actions.interactActions)
                {
                    if (a == InteractableActions.Actions.FlashlightCarry) isFlashlight = true;
                    if (a == InteractableActions.Actions.TattletailCarry ||
                        a == InteractableActions.Actions.TattletailCarrySilent) isTattletail = true;
                }

                if (!isFlashlight && !isTattletail) continue;

                it.transform.position = basePos + offset;
                it.transform.rotation = Quaternion.identity;
                offset += new Vector3(0.6f, 0f, 0f);
                found++;

                KaleidoscopeEndlessPlugin.Log.LogInfo(
                    "KE: Moved pickup '" + it.gameObject.name + "' (" +
                    (isFlashlight ? "flashlight" : "tattletail") + ") next to player.");
            }

            if (found == 0)
                KaleidoscopeEndlessPlugin.Log.LogWarning(
                    "KE: No flashlight/tattletail pickup Interactables found in this level.");
        }

        /// <summary>Called by PhoneObjective when a call goes unanswered — whatever objective/rest transition happened while ringing had its own Mama (re)activation silently skipped, so retry it now. No-op during rest (Mama's never active then anyway) or once the run's already over.</summary>
        public void ResumeMamaAfterMissedCall()
        {
            if (!RunActive || _inRestPhase) return;
            MamaController.Instance?.ActivateMamaForObjective(MamaDifficultyMult());
        }

        // Tattletail's scared "Mama's coming" warning line. tt_mama_conversation
        // (used previously) turned out to be an unrelated, mundane line —
        // this is vanilla's actual line for the moment, confirmed via
        // decompile: MamaSpawner.StartActivation() calls
        // RM.tattletail.RequestTalk("tt_warning", useSpecificMacro: true,
        // TattletailAnimator.Macro.TalkScared, null, mustPlay: true) at
        // exactly the moment Mama activates. RequestTalk (rather than
        // AudioController.Play directly) also plays the scared talk
        // animation and queues the request if Tattletail's already talking,
        // instead of just dropping it.
        private void PlayMamaConversationLine()
        {
            if (RM.tattletail == null)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: PlayMamaConversationLine skipped — RM.tattletail is null.");
                return;
            }
            VersusNet.QuietTattletailLine(); // Versus: warning the child isn't a noise that gives them away
            RM.tattletail.RequestTalk("tt_warning", true, TattletailAnimator.Macro.TalkScared, null, true);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Playing tt_warning (scared).");
        }

        /// <summary>isDeath=false for a voluntary exfil (PhoneObjective) — doesn't count toward EndlessStats' TotalDeaths, but still records survival time same as any other run end.</summary>
        public void EndRun(bool isDeath = true)
        {
            if (VersusMode.IsSurvivor) VersusNet.SendRoundOver(isDeath ? VersusNet.Over.Died : VersusNet.Over.Escaped);
            if (!BirthdayMode) EndlessStats.RecordRunEnd(RunTime, isDeath);

            // Exfil bonus: a voluntary exit (answering the phone) pays 25% of the shards earned this run.
            if (!isDeath && _runShardsEarned > 0)
            {
                int bonus = Mathf.RoundToInt(_runShardsEarned * EndlessTraits.ExfilFraction());
                if (bonus > 0) Shards.Add(bonus);
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Shards] Exfil bonus +" + bonus + " (25% of " + _runShardsEarned + ").");
            }
            _runShardsEarned = 0;
            EndlessTraits.EndRun();
            ResetChaos();

            RunActive        = false;
            _objectiveActive = false;
            CurrentObjective = ObjectiveType.None;

            // Stop Mama music and any still-ringing phone before the level unloads.
            AudioController.Stop("mama_music");
            AudioController.Stop("phone_ring_loop");
            PhoneObjective.Instance?.DespawnMarker();
            _phoneRinging = false;
            TrueEndingEasterEgg.Instance?.CleanUp();
            PowerUpObjective.Instance?.CleanUp();
            GadgetController.Instance?.CleanUp();

            // Started from a hub machine: death, escape, whatever — you land back in the arcade, not the menu.
            if (HubMode.ReturnToHub)
            {
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Run over — back to the arcade HUB.");
                SceneMonitor.ResetForNewRun(); // also clears ReturnToHub
                HubMode.Launch(null);
                return;
            }

            // Unlock cursor immediately.
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            GS.acceptInput = true;

            // Load the menu scene — this frees the mouse and kills the level.
            // Async so the game keeps rendering while the menu loads instead of hard-freezing on a synchronous load.
            UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("menu");

            // Scene loads can re-lock the cursor — reassert once more a frame later.
            StartCoroutine(UnlockCursorAfterLoad());
        }

        private System.Collections.IEnumerator UnlockCursorAfterLoad()
        {
            yield return null;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        // Routes objective quest text through here (instead of calling
        // RM.hud.SetNewQuest directly) so a close call's temporary "She
        // almost got you!" message has something to restore afterward —
        // see OnMamaCaughtPlayer(). Nothing was restoring it before, so the
        // mercy message permanently overwrote whatever objective text was
        // showing.
        private void SetObjectiveQuestText(string text)
        {
            _primaryBaseText = text;
            if (_chaosRound) text = ChaosText();
            _currentObjectiveHudText = text;
            // Still record what the objective's real text is (so restoring
            // after the call gets the right thing), but don't let it
            // actually overwrite the phone's own message on screen — this
            // fires unconditionally from BeginObjective() the moment the
            // very next objective starts, which happens synchronously
            // right after RequestRing() sets the phone's text.
            if (PhoneIsRinging) return;
            if (RM.hud != null && !EndlessTraits.HideHud) RM.hud.SetNewQuest(text);
        }

        // ── Random events (see EndlessEvents.cs) ─────────────────────────────
        // Testing override: true forces a random event on the very first objective of every run (off for release).
        public static bool DebugForceEventOnFirstObjective = false;

        private readonly System.Collections.Generic.List<ObjectiveType> _chaosPending = new System.Collections.Generic.List<ObjectiveType>();
        private bool _chaosRound;   // Chaos Round: two objectives at once, both pay double shards
        private bool _primaryDone;
        private string _primaryBaseText = "";
        private bool _eventRolledThisRun;
        // After a Gift from Mama: 0% chance of another for the rest of that task AND the whole next task — re-enabled when the task after that starts.
        private int _giftBlocks;
        public bool GiftAllowed { get { return _giftBlocks <= 0; } }
        public void BlockGiftsForNextTask() { _giftBlocks = 2; }
        private float _eventTimer; // counts up to EndlessEvents.TIMED_INTERVAL between timed-event rolls

        public string CurrentObjectiveHudText { get { return _currentObjectiveHudText; } }

        /// <summary>Lucky Night (10% of runs, rolled at launch — see EndlessTitle): every shard payout this run is doubled.</summary>
        public bool LuckyNight { get; private set; }

        private void ResetChaos()
        {
            _chaosPending.Clear();
            _chaosRound = false;
            _primaryDone = false;
        }

        // The objective that started the round, or any Chaos Round objective still pending.
        private bool IsLive(ObjectiveType type)
        {
            return _objectiveActive && ((CurrentObjective == type && !_primaryDone) || _chaosPending.Contains(type));
        }

        private string ObjectiveTaskText(ObjectiveType t)
        {
            switch (t)
            {
                case ObjectiveType.WashingMachine: return "turn off the washing machine";
                case ObjectiveType.SilenceNoise: return "silence the noise";
                case ObjectiveType.SilenceSpeaker: return "turn off the speaker";
                case ObjectiveType.RestartFuseBox: return "restart the fuse box";
                case ObjectiveType.RewindTape: return "rewind the tape";
                case ObjectiveType.FindFriend: return "find Tattletail's friend";
                case ObjectiveType.BreakVases:
                    return VaseObjective.Instance != null
                        ? "break the vases (" + VaseObjective.Instance.BrokenCount + "/" + VaseObjective.Instance.TargetCount + ")"
                        : "break the vases";
                case ObjectiveType.LightCandles:
                    return CandleObjective.Instance != null
                        ? "light the candles (" + CandleObjective.Instance.LitCount + "/" + CandleObjective.Instance.TargetCount + ")"
                        : "light the candles";
                default: return t.ToString();
            }
        }

        // "Chaos round: do X and Y" — lists whatever is still left to do.
        private string ChaosText()
        {
            var tasks = new System.Collections.Generic.List<string>();
            if (!_primaryDone) tasks.Add(ObjectiveTaskText(CurrentObjective));
            foreach (var t in _chaosPending) tasks.Add(ObjectiveTaskText(t));
            return "Chaos round: do " + string.Join(" and ", tasks.ToArray()) + "\n(double shards!)";
        }

        /// <summary>
        /// Chaos Round event: a second objective runs alongside the current
        /// one, and both pay double shards. Only pairs objectives that
        /// can't step on each other (no Flashlight Survival; the washing
        /// machine and the noise spot share a location so they're never
        /// paired). Returns false if nothing could be paired.
        /// </summary>
        public bool TryStartChaosRound()
        {
            if (!_objectiveActive || _chaosRound) return false;
            if (CurrentObjective == ObjectiveType.None || CurrentObjective == ObjectiveType.FlashlightSurvival) return false;

            var pool = new System.Collections.Generic.List<ObjectiveType>
            {
                ObjectiveType.WashingMachine, ObjectiveType.SilenceNoise, ObjectiveType.SilenceSpeaker,
                ObjectiveType.RestartFuseBox, ObjectiveType.RewindTape,
            };
            pool.Remove(CurrentObjective);
            if (FactoryMode.EndlessPreview) pool.Remove(ObjectiveType.SilenceNoise); // (retired, and its spot is in the house)
            if (CurrentObjective == ObjectiveType.WashingMachine) pool.Remove(ObjectiveType.SilenceNoise);
            if (CurrentObjective == ObjectiveType.SilenceNoise) pool.Remove(ObjectiveType.WashingMachine);
            if (pool.Count == 0) return false;

            ObjectiveType pick = pool[Random.Range(0, pool.Count)];
            _chaosRound = true;
            _chaosPending.Add(pick);

            // Just the objective's own setup — Mama is already hunting for the round.
            switch (pick)
            {
                case ObjectiveType.WashingMachine: WashingMachineObjective.Instance?.StartObjective(); break;
                case ObjectiveType.SilenceNoise: NoiseObjective.Instance?.StartObjective(); break;
                case ObjectiveType.SilenceSpeaker: SpeakerObjective.Instance?.StartObjective(); break;
                case ObjectiveType.RestartFuseBox: FuseBoxObjective.Instance?.StartObjective(); break;
                case ObjectiveType.RewindTape: TapeObjective.Instance?.StartObjective(DecideIndicesFor(ObjectiveType.RewindTape)); break;
            }

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Event] Chaos Round — '" + CurrentObjective + "' + '" + pick + "'.");
            SetObjectiveQuestText(_primaryBaseText);
            return true;
        }

        /// <summary>
        /// Every objective's "done" path funnels through here. Normally that's
        /// just: Mama goes away, the round completes. During a Chaos Round
        /// the first one done pays out and keeps going; only the last one
        /// ends the round.
        /// </summary>
        private void FinishObjective(ObjectiveType finished)
        {
            bool isSecondary = _chaosPending.Contains(finished) && !(CurrentObjective == finished && !_primaryDone);
            if (isSecondary) _chaosPending.Remove(finished);
            else _primaryDone = true;

            if (_chaosPending.Count > 0 || !_primaryDone)
            {
                // Partial completion: pay for this one, keep Mama and the round going.
                EndlessStats.RecordObjectiveCompleted();
                AwardShards();
                SetObjectiveQuestText(_primaryBaseText); // rebuilds the "Chaos round: do ..." line with what's left
                return;
            }

            MamaController.Instance?.DeactivateMama();
            CompleteObjective();
        }

        private void StartNextObjective()
        {
            CurrentObjective = NextObjectiveType();
            int[] indices = DecideIndicesFor(CurrentObjective);
            BeginObjective(CurrentObjective, indices);
        }

        private void BeginObjective(ObjectiveType type, int[] indices)
        {
            // Moved here from CompleteObjective()/StartRun() — this line is
            // Mama's "she's coming" warning, so it should only ever play
            // right as an objective (and her hunt) is actually starting.
            // With rest now happening after every single objective instead
            // of only at loop boundaries, calling it from CompleteObjective()
            // meant it fired right as REST began instead — backwards, since
            // she's leaving at that point, not coming. StartNextObjective()
            // covers both the very first objective of a run and every one
            // after a rest period (both call this), so nothing loses the
            // line, it just fires at the right moment now.
            PlayMamaConversationLine();

            // All four needs drain for real — that's the resource-management
            // layer. The feed/groom/charge stations that resolve them are
            // force-kept-enabled in GamePatches.cs regardless of vanilla
            // story-progress conditions, since this repurposed level never
            // satisfies those naturally.
            var needs = RM.tattletail?.GetNeeds();
            if (needs != null) needs.UnlockAllNeeds();
            _objectiveActive  = true;
            _objectiveTimer   = 0f;
            CloseCalls   = 0;
            bool firstObjectiveOfRun = ObjectivesCompleted == 0 && !_eventRolledThisRun;
            _eventRolledThisRun = true;
            if (_giftBlocks > 0) _giftBlocks--;
            ResetChaos();

            CleanUpInactiveObjectiveClones();

            // "Power Ups" run modifier — one coin per round (each new
            // objective), independent of whatever the objective itself is.
            EndlessTraits.OnObjectiveStart(type);
            if (ModPowerUps || EndlessTraits.CoinsWithoutModifier()) PowerUpObjective.Instance?.SpawnCoin();

            KaleidoscopeEndlessPlugin.Log.LogInfo("Objective: " + type + " loop=" + LoopNumber + " diff=" + _difficultyMult);

            switch (type)
            {
                case ObjectiveType.WashingMachine:
                    StartWashingMachine();
                    break;
                case ObjectiveType.FlashlightSurvival:
                    StartFlashlightSurvival(indices);
                    break;
                case ObjectiveType.SilenceNoise:
                    StartSilenceNoise();
                    break;
                case ObjectiveType.FindFriend:
                    StartFindFriend(indices);
                    break;
                case ObjectiveType.BreakVases:
                    StartBreakVases(indices);
                    break;
                case ObjectiveType.LightCandles:
                    StartLightCandles(indices);
                    break;
                case ObjectiveType.RewindTape:
                    StartRewindTape(indices);
                    break;
                case ObjectiveType.SilenceSpeaker:
                    StartSilenceSpeaker();
                    break;
                case ObjectiveType.RestartFuseBox:
                    StartRestartFuseBox();
                    break;
            }

            // Random events: 1% per objective (debug: one on the first objective of every run).
            EndlessEvents.OnObjectiveStart(this, type, firstObjectiveOfRun);
        }

        /// <summary>Rolls the RNG each objective type needs. WashingMachine/SilenceNoise need none.</summary>
        private int[] DecideIndicesFor(ObjectiveType type)
        {
            switch (type)
            {
                case ObjectiveType.FlashlightSurvival:
                    return new int[] { Random.Range(0, FlashlightSurvivalTimeOptions.Length) };
                case ObjectiveType.FindFriend:
                    return TattletailFriendObjective.Instance != null ? TattletailFriendObjective.Instance.DecideIndices() : new int[0];
                case ObjectiveType.BreakVases:
                    return VaseObjective.Instance != null ? VaseObjective.Instance.DecideIndices() : new int[0];
                case ObjectiveType.LightCandles:
                    return CandleObjective.Instance != null ? CandleObjective.Instance.DecideIndices() : new int[0];
                case ObjectiveType.RewindTape:
                    return TapeObjective.Instance != null ? TapeObjective.Instance.DecideIndices() : new int[0];
                default:
                    return new int[0];
            }
        }

        // Everything else pauses while the phone's ringing — per explicit
        // design, the phone fully overrides the current objective; the
        // player's only choice during a call is wait it out or answer it.
        // (PhoneIsRinging itself is declared up near _phoneRinging.)

        private void TickObjective()
        {
            if (PhoneIsRinging) return;
            _objectiveTimer += Time.deltaTime;
            if (CurrentObjective == ObjectiveType.FlashlightSurvival)
                TickFlashlightSurvival();
        }

        // Shards scale with the difficulty picked on the menu: Easy x0.5
        // (5 per objective), Normal x1.0 (10), Hard x1.5, Impossible x2.0.
        private int _runShardsEarned; // shards paid out so far this run — the exfil bonus is 25% of this

        private float ShardDifficultyMult()
        {
            switch (DifficultyLabel)
            {
                case "Easy": return 0.5f;
                case "Hard": return 1.5f;
                case "Impossible": return 2.0f;
                default: return 1f;
            }
        }

        // 10 shards per completed objective, +10 for every Run Modifier
        // that's on (except Power Ups), all times the difficulty multiplier.
        private void AwardShards()
        {
            if (VersusNet.AwardObjectiveTokens()) return; // Versus pays Versus Tokens instead
            int mods = 0;
            if (ModLowBattery) mods++;
            if (ModMaxMamaAggro) mods++;
            if (ModNoRest) mods++;
            if (ModBatteriesNotIncluded) mods++;
            if (ModAlwaysHungry) mods++;
            if (ModBrushingIsKey) mods++;
            // (the Power Ups modifier deliberately doesn't count toward the bonus)
            int amount = Mathf.RoundToInt((10 + 10 * mods) * ShardDifficultyMult() * Passives.ShardMult * EndlessTraits.ShardMult() * (_chaosRound ? 2f : 1f) * (LuckyNight ? 2f : 1f)); // Shard Boost passive; Chaos Round and Lucky Night each double it
            Shards.Add(amount);
            _runShardsEarned += amount;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Shards] +" + amount + " (" + mods + " modifier(s) active).");
        }

        private void CompleteObjective()
        {
            _objectiveActive = false;
            ObjectivesCompleted++;
            Daily.Best("run_obj", ObjectivesCompleted);
            if (_difficultyMult >= 1.39f) Daily.Add("hard_obj", 1);
            EndlessStats.RecordObjectiveCompleted();
            AwardShards();

            // A loop is a fixed 3 completed objectives (not a full pass
            // through Sequence — the shuffled bag above is a separate,
            // bigger pool that just keeps refilling and drawing regardless
            // of loop boundaries). LoopNumber still counts these for the
            // death-screen stats, but _difficultyMult no longer climbs with
            // it — now that difficulty is a real player-chosen setting
            // (Easy/Normal/Hard/Impossible), auto-ramping on top of that
            // pick would mean two things fighting over "how hard is this
            // run", so it just stays at whatever PendingDifficultyMult set
            // for the whole run.
            // Exfil time: the ringing phone IS the break (60s, lights on, Mama
            // gone) instead of a 30s rest on top of it. Not answered in time
            // -> the next objective starts straight away (EndPhoneRing).
            bool ring = VersusMode.IsSurvivor
                ? ObjectivesCompleted >= VERSUS_OBJECTIVES_BEFORE_EXFIL && (ObjectivesCompleted - VERSUS_OBJECTIVES_BEFORE_EXFIL) % LOOP_LENGTH == 0
                : ObjectivesCompleted % LOOP_LENGTH == 0;
            if (ring)
            {
                LoopNumber++;
                CleanUpInactiveObjectiveClones();
                if (RM.lightsController != null && !RM.lightsController.AreLightsOn())
                    RM.lightsController.RequestLightsOn(null);
                StartPhoneRing();
                return;
            }

            // "No Rest for the Wicked" — skip straight to the next
            // objective instead of the usual rest phase.
            if (ModNoRest) StartNextObjective();
            else StartRestPhase();
        }

        // ── Washing machine ───────────────────────────────────────────────────
        // A real, self-contained find-and-interact objective — see
        // WashingMachineObjective.cs. No longer depends on an in-scene
        // Interactable or the vanilla quest system (both were unreliable in
        // this repurposed level), and no longer a bare survive-the-timer
        // stand-in either: you actually have to find the machine (audible,
        // clickable) and turn it off, same shape as Silence the Noise, while
        // Mama hunts you the whole time.
        private void StartWashingMachine()
        {
            try
            {
                WashingMachineObjective.Instance?.StartObjective();
                MamaController.Instance?.ActivateMamaForObjective(MamaDifficultyMult());
                SetObjectiveQuestText("Something's running in the house...\nFind the washing machine and turn it off!");
            }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: StartWashingMachine crashed: " + e.Message + "\n" + e.StackTrace);
            }
        }

        /// <summary>Called from Patch_Interactable_OnInteractComplete_WashingMachine when the washing machine is found.</summary>
        public void OnWashingMachineFound()
        {
            if (PhoneIsRinging) return;
            if (!IsLive(ObjectiveType.WashingMachine)) return;
            ApplyWashingMachineFound();
        }

        private void ApplyWashingMachineFound()
        {
            WashingMachineObjective.Instance?.CleanUp();
            FinishObjective(ObjectiveType.WashingMachine);
        }

        // ── Flashlight survival ───────────────────────────────────────────────
        private void StartFlashlightSurvival(int[] indices)
        {
            try
            {
                // This level has no flashlight pickup Interactables, so we
                // force-carry via the game's own SetFlashlight(true) — same
                // call the game makes when the player grabs the pickup.
                if (RM.flashlight != null)
                {
                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [FS1] calling SetFlashlight(true)");
                    RM.flashlight.SetFlashlight(true);

                    // SetFlashlight(true) internally re-runs vanilla's own
                    // UpdateSpecialFlashlightStatus() (confirmed via
                    // decompile), which silently resets currentColor/
                    // currentMaxChargeTimeModifier back to whatever
                    // GD.hasSpecialFlashlight dictates on THIS player's
                    // save — undoing the Diamond tint/charge boost every
                    // time this objective starts, not just once (this is
                    // exactly what caused a real "flashlight turned gold"
                    // report). Re-applied BEFORE MaxLight() below, since
                    // MaxLight() fills the charge using whatever
                    // currentMaxChargeTimeModifier is active at the moment
                    // it's called — calling it after our re-apply, not
                    // before, is what actually tops it up to the correct
                    // (taller) Diamond ceiling instead of the reverted one.
                    GoldenFlashlight.Apply(RM.flashlight);
                    if (ModDiamondFlashlight)
                        DiamondFlashlight.Apply(RM.flashlight);
                    FlashlightBeam.ApplyPending(RM.flashlight);

                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [FS2] calling MaxLight");
                    RM.flashlight.MaxLight();
                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [FS3] flashlight force-equipped");
                }
                else
                {
                    KaleidoscopeEndlessPlugin.Log.LogWarning("KE: RM.flashlight is null — cannot give flashlight.");
                }

                MamaController.Instance?.ActivateMamaForObjective(MamaDifficultyMult());

                // Index into FlashlightSurvivalTimeOptions — decided by
                // DecideIndicesFor() (host/solo) or received from the host
                // (client), rather than rolled locally here, so both sides
                // of a multiplayer run agree on the survive time.
                int timeIndex = (indices != null && indices.Length > 0) ? indices[0] : Random.Range(0, FlashlightSurvivalTimeOptions.Length);
                _flashlightSurvivalBaseTime = FlashlightSurvivalTimeOptions[timeIndex];
                float limit = _flashlightSurvivalBaseTime / Mathf.Sqrt(_difficultyMult) * EndlessTraits.FlashlightSurvivalMult();
                SetObjectiveQuestText("Keep flashlight open!\nSurvive for " + (int)limit + "s");

                _flashlightWasGlowing = RM.flashlight != null && RM.flashlight.IsGlowing();
            }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: StartFlashlightSurvival crashed: " + e.Message + "\n" + e.StackTrace);
            }
        }

        private void TickFlashlightSurvival()
        {
            // Used to force MaxLight() every tick here — user reported it
            // breaks the flashlight (likely fighting whatever vanilla state
            // change happens when Mama actually gets to it). Only set once,
            // at objective start, now.

            // Detect flashlight kills by polling IsGlowing() (a plain public
            // vanilla method — no Harmony patch at all) instead of hooking
            // FlashlightController.KillLight() directly. Confirmed via a
            // clean A/B test that patching KillLight — even with an empty,
            // exception-free Prefix/Postfix — silently broke
            // LightsController's lights-out fade coroutine (KillLight is
            // the first real line inside EaseOutLightsRoutine). No managed
            // exception was ever thrown; disabling the patch fixed lights
            // immediately, re-enabling broke them again. Rather than dig
            // into why a Harmony patch on this specific method interferes
            // with that specific coroutine (old Mono runtime per BepInEx
            // logs — plausible obscure JIT/patching interaction), just
            // don't patch it — this polling approach gets the same "flash-
            // light was killed" signal without touching the shared method
            // at all.
            if (RM.flashlight != null)
            {
                bool isGlowing = RM.flashlight.IsGlowing();
                if (_flashlightWasGlowing && !isGlowing)
                    OnFlashlightKilledEvent();
                _flashlightWasGlowing = isGlowing;
            }

            float limit = _flashlightSurvivalBaseTime / Mathf.Sqrt(_difficultyMult) * EndlessTraits.FlashlightSurvivalMult();
            if (_objectiveTimer >= limit)
            {
                MamaController.Instance?.DeactivateMama();
                CompleteObjective();
            }
        }

        // ── Silence the noise ─────────────────────────────────────────────────
        private void StartSilenceNoise()
        {
            try
            {
                NoiseObjective.Instance?.StartObjective();
                MamaController.Instance?.ActivateMamaForObjective(MamaDifficultyMult());
                SetObjectiveQuestText("Something's making noise in the house...\nFind it and turn it off!");
            }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: StartSilenceNoise crashed: " + e.Message + "\n" + e.StackTrace);
            }
        }

        /// <summary>Called from Patch_Interactable_OnInteractComplete_NoiseObjective when the real noise source is found.</summary>
        public void OnNoiseSourceFound()
        {
            if (PhoneIsRinging) return;
            if (!IsLive(ObjectiveType.SilenceNoise)) return;
            NoiseObjective.Instance?.CleanUp();
            FinishObjective(ObjectiveType.SilenceNoise);
        }

        // Rough "Look in: Kitchen, Garage" lines for the hidden objectives (RoomHints).
        private static string FriendHint() { return TattletailFriendObjective.Instance != null ? RoomHints.LookIn(TattletailFriendObjective.Instance.RemainingPositions()) : ""; }
        private static string VaseHint() { return VaseObjective.Instance != null ? RoomHints.LookIn(VaseObjective.Instance.RemainingPositions()) : ""; }
        private static string CandleHint() { return CandleObjective.Instance != null ? RoomHints.LookIn(CandleObjective.Instance.RemainingPositions()) : ""; }
        private static string TapeHint() { return TapeObjective.Instance != null ? RoomHints.LookIn(TapeObjective.Instance.RemainingPositions()) : ""; }

        // ── Find Tattletail's friend ──────────────────────────────────────────
        private void StartFindFriend(int[] indices)
        {
            try
            {
                TattletailFriendObjective.Instance?.StartObjective(indices ?? new int[0]);
                MamaController.Instance?.ActivateMamaForObjective(MamaDifficultyMult());
                // User-marked spots (FriendPositions in TattletailFriendObjective)
                // now span multiple rooms/floors, not just the basement, so
                // the hint is back to the general "somewhere in the house".
                SetObjectiveQuestText("Tattletail has a friend hiding somewhere in the house...\nFind them!" + FriendHint());
            }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: StartFindFriend crashed: " + e.Message + "\n" + e.StackTrace);
            }
        }

        /// <summary>Called from Patch_Interactable_OnInteractComplete_TattletailFriend when the friend is found.</summary>
        public void OnFriendFound()
        {
            if (PhoneIsRinging) return;
            if (!IsLive(ObjectiveType.FindFriend)) return;
            ApplyFriendFound();
        }

        private void ApplyFriendFound()
        {
            TattletailFriendObjective.Instance?.CleanUp();
            FinishObjective(ObjectiveType.FindFriend);
        }

        // ── Break the vases ───────────────────────────────────────────────────
        private void StartBreakVases(int[] indices)
        {
            try
            {
                VaseObjective.Instance?.StartObjective(indices ?? new int[0]);
                MamaController.Instance?.ActivateMamaForObjective(MamaDifficultyMult());
                int target = VaseObjective.Instance != null ? VaseObjective.Instance.TargetCount : 3;
                SetObjectiveQuestText("Something needs breaking...\nBreak the vases! (0/" + target + ")" + VaseHint());
            }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: StartBreakVases crashed: " + e.Message + "\n" + e.StackTrace);
            }
        }

        /// <summary>Called from Patch_Interactable_OnInteractComplete_VaseObjective when a vase is broken.</summary>
        public void OnVaseBroken(Interactable interactable)
        {
            if (PhoneIsRinging) return;
            if (!IsLive(ObjectiveType.BreakVases)) return;
            if (VaseObjective.Instance == null) return;

            int slot = VaseObjective.Instance.GetSlotIndex(interactable.gameObject);
            if (slot < 0) return;

            ApplyVaseBreak(slot);
        }

        private void ApplyVaseBreak(int slot)
        {
            bool allDone = VaseObjective.Instance.BreakVaseAtSlot(slot);

            if (allDone)
            {
                FinishObjective(ObjectiveType.BreakVases);
            }
            else
            {
                SetObjectiveQuestText("Something needs breaking...\nBreak the vases! (" +
                    VaseObjective.Instance.BrokenCount + "/" + VaseObjective.Instance.TargetCount + ")" + VaseHint());
            }
        }

        // ── Light the candles ─────────────────────────────────────────────────
        private void StartLightCandles(int[] indices)
        {
            try
            {
                CandleObjective.Instance?.StartObjective(indices ?? new int[0]);
                MamaController.Instance?.ActivateMamaForObjective(MamaDifficultyMult());

                // Real vanilla voice line, confirmed via the game's own
                // asset data (not guessed) — "tt_light_the_candles".
                if (RM.tattletail != null)
                    RM.tattletail.RequestTalk("tt_light_the_candles", true, TattletailAnimator.Macro.Talk, null, true);

                int target = CandleObjective.Instance != null ? CandleObjective.Instance.TargetCount : 3;
                SetObjectiveQuestText("Light the candles!\nFind and light 3 candles. (0/" + target + ")" + CandleHint());
            }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: StartLightCandles crashed: " + e.Message + "\n" + e.StackTrace);
            }
        }

        /// <summary>Called from Patch_Interactable_OnInteractComplete_CandleObjective when a candle is lit.</summary>
        public void OnCandleLit(Interactable interactable)
        {
            if (PhoneIsRinging) return;
            if (!IsLive(ObjectiveType.LightCandles)) return;
            if (CandleObjective.Instance == null) return;

            int slot = CandleObjective.Instance.GetSlotIndex(interactable.gameObject);
            if (slot < 0) return;

            ApplyCandleLight(slot);
        }

        private void ApplyCandleLight(int slot)
        {
            bool allDone = CandleObjective.Instance.LightCandleAtSlot(slot);

            if (allDone)
            {
                CandleObjective.Instance?.CleanUp();
                FinishObjective(ObjectiveType.LightCandles);
            }
            else
            {
                SetObjectiveQuestText("Light the candles!\nFind and light 3 candles. (" +
                    CandleObjective.Instance.LitCount + "/" + CandleObjective.Instance.TargetCount + ")" + CandleHint());
            }
        }

        // ── Rewind the tape ───────────────────────────────────────────────────
        private void StartRewindTape(int[] indices)
        {
            try
            {
                TapeObjective.Instance?.StartObjective(indices ?? new int[0]);
                MamaController.Instance?.ActivateMamaForObjective(MamaDifficultyMult());
                SetObjectiveQuestText("A tape needs rewinding...\nFind it and hold to rewind!" + TapeHint());
            }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: StartRewindTape crashed: " + e.Message + "\n" + e.StackTrace);
            }
        }

        /// <summary>Called from Patch_Interactable_OnInteractComplete_TapeObjective when the tape finishes rewinding.</summary>
        public void OnTapeRewound()
        {
            if (PhoneIsRinging) return;
            if (!IsLive(ObjectiveType.RewindTape)) return;

            TapeObjective.Instance?.CleanUp();
            FinishObjective(ObjectiveType.RewindTape);
        }

        // ── Silence the speaker ───────────────────────────────────────────────
        private void StartSilenceSpeaker()
        {
            try
            {
                SpeakerObjective.Instance?.StartObjective();
                MamaController.Instance?.ActivateMamaForObjective(MamaDifficultyMult());
                SetObjectiveQuestText("A speaker is blasting static somewhere in the house...\nFind it and turn it off!");
            }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: StartSilenceSpeaker crashed: " + e.Message + "\n" + e.StackTrace);
            }
        }

        /// <summary>Called from Patch_Interactable_OnInteractComplete_SpeakerObjective when the speaker is found.</summary>
        public void OnSpeakerSilenced()
        {
            if (PhoneIsRinging) return;
            if (!IsLive(ObjectiveType.SilenceSpeaker)) return;

            SpeakerObjective.Instance?.CleanUp();
            FinishObjective(ObjectiveType.SilenceSpeaker);
        }

        // ── Restart the fuse box ──────────────────────────────────────────────
        private void StartRestartFuseBox()
        {
            try
            {
                FuseBoxObjective.Instance?.StartObjective();
                MamaController.Instance?.ActivateMamaForObjective(MamaDifficultyMult());
                SetObjectiveQuestText("The fuse box needs restarting...\nHold to restart it!");
            }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: StartRestartFuseBox crashed: " + e.Message + "\n" + e.StackTrace);
            }
        }

        /// <summary>Called from Patch_Interactable_OnInteractComplete_FuseBoxObjective when the fuse box finishes restarting.</summary>
        public void OnFuseBoxRestarted()
        {
            if (PhoneIsRinging) return;
            if (!IsLive(ObjectiveType.RestartFuseBox)) return;

            FuseBoxObjective.Instance?.CleanUp();
            FinishObjective(ObjectiveType.RestartFuseBox);
        }

        /// <summary>
        /// Called from Patch_MamaManager_RequestJumpscareDeath — Mama's
        /// touch/line-of-sight kill (BaseBehavior's proximity check, or
        /// OnNoise() calling this directly when she's alert/close/has LOS
        /// when a noise fires, e.g. footsteps). Per explicit design: making
        /// noise and getting caught is always a real, immediate death, in
        /// every objective including flashlight survival — no mercy here at
        /// all anymore, EXCEPT the Power Ups "Totem of Undying" ability
        /// (up to 3 stacked saves) — checked first since it's the only
        /// remaining way to survive this specific catch.
        /// </summary>
        public bool OnMamaCaughtPlayer()
        {
            if (PowerUpObjective.Instance != null && PowerUpObjective.Instance.TryConsumeTotem(MamaDifficultyMult()))
            {
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [PowerUp] Totem of Undying saved the run.");
                EndlessTraits.OnTotemUsed();
                return false;
            }
            return true;
        }

        /// <summary>
        /// Called from Patch_FlashlightController_KillLight whenever Mama
        /// actually kills the flashlight's light (a distinct vanilla event
        /// from the touch/LOS kill above — confirmed via decompile). Only
        /// meaningful during flashlight survival. Three kills and the run
        /// ends for real; the first two just show a warning and let the
        /// player keep going (flashlight stays dead until they shake it
        /// back on — no auto-recharge here, same reasoning as removing the
        /// per-tick MaxLight() call).
        /// </summary>
        public void OnFlashlightKilledEvent()
        {
            if (!_objectiveActive || CurrentObjective != ObjectiveType.FlashlightSurvival) return;
            if (EndlessTraits.ColdBloodForgives()) return; // 3.7 (Cold Blood): the first one in each objective does not count

            CloseCalls++;
            TotalCloseCalls++;

            if (CloseCalls >= CLOSE_CALL_LIMIT)
            {
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Flashlight killed " + CloseCalls + "/" + CLOSE_CALL_LIMIT + " — fatal.");
                MamaController.Instance?.TriggerJumpscare();
                return;
            }

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Flashlight killed " + CloseCalls + "/" + CLOSE_CALL_LIMIT + " — one more and she gets you.");
            if (RM.hud != null) RM.hud.SetNewQuest("She almost got you! (" + CloseCalls + "/" + CLOSE_CALL_LIMIT + ")\nKeep flashlight open!");
            StartCoroutine(RestoreQuestTextAfterCloseCall(CurrentObjective));
        }

        // Nothing was restoring the objective's own quest text after a
        // close call — the mercy message permanently overwrote it (looked
        // like the objective had "reset" even though nothing about its
        // actual state had changed). Guarded on the same objective still
        // being active so this can't clobber a legitimately newer quest
        // text if the objective completed/changed in the meantime.
        private System.Collections.IEnumerator RestoreQuestTextAfterCloseCall(ObjectiveType forObjective)
        {
            yield return new WaitForSeconds(2.5f);
            if (CurrentObjective == forObjective && _objectiveActive && RM.hud != null)
                RM.hud.SetNewQuest(_currentObjectiveHudText);
        }

        // Vase/candle clones should only ever exist while their own
        // objective is the current one — spawned clones are already
        // self-cleaning on normal completion (vases destroy themselves per
        // break, candles clean up in OnCandleLit), but this is a defensive
        // catch-all for any other transition (a run ending mid-objective,
        // rest phase starting, etc.) so nothing from a stale objective can
        // ever be left standing in the world.
        private void CleanUpInactiveObjectiveClones()
        {
            if (CurrentObjective != ObjectiveType.BreakVases) VaseObjective.Instance?.CleanUp();
            if (CurrentObjective != ObjectiveType.LightCandles) CandleObjective.Instance?.CleanUp();
        }

        // ── Rest phase ────────────────────────────────────────────────────────
        /// <summary>3.7 (Compulsive Checking): give up on this objective and take the next one. No pay for it.</summary>
        public bool TraitSkipObjective()
        {
            if (!RunActive || !_objectiveActive || _inRestPhase || _phoneRinging) return false;
            VaseObjective.Instance?.CleanUp();
            CandleObjective.Instance?.CleanUp();
            WashingMachineObjective.Instance?.CleanUp();
            NoiseObjective.Instance?.CleanUp();
            TattletailFriendObjective.Instance?.CleanUp();
            TapeObjective.Instance?.CleanUp();
            SpeakerObjective.Instance?.CleanUp();
            FuseBoxObjective.Instance?.CleanUp();
            MamaController.Instance?.DeactivateMama();
            ResetChaos();
            _objectiveActive = false;
            StartNextObjective();
            return true;
        }

        /// <summary>3.7: shards a trait combo pays (counts toward the exfil bonus like any other).</summary>
        public void TraitPay(int shards)
        {
            Shards.Add(shards);
            _runShardsEarned += shards;
        }

        private void StartRestPhase()
        {
            _inRestPhase = true;
            _restTimer   = REST_DURATION;
            CleanUpInactiveObjectiveClones();

            // Lights come back on for the rest period — a real visible
            // breather to go with Mama being gone, not just a text message.
            // RequestLightsOut() gets called again the moment the next
            // objective starts (MamaController.ActivateMamaForObjective),
            // so this doesn't need its own teardown here.
            if (RM.lightsController != null && !RM.lightsController.AreLightsOn())
                RM.lightsController.RequestLightsOn(null);

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Rest phase started (" + REST_DURATION + "s), RM.hud=" + (RM.hud != null));
            // Static text, no live countdown — SuperTextMesh's reveal
            // coroutine restarts on every .text assignment, so anything
            // that re-pushes this every second (or even just when the
            // displayed number changes) never gets a chance to finish
            // drawing before the next update yanks it back to square one.
            // Remembered as the "current" task text too, so anything that
            // temporarily replaces it (Gift from Mama's notice, a close
            // call, the phone) restores the REST text afterward instead of
            // the previous objective's stale one.
            _currentObjectiveHudText = "Rest Period... She's Gone... For Now...\n" + (int)REST_DURATION + "s";
            if (RM.hud != null) RM.hud.SetNewQuest(_currentObjectiveHudText);
        }

        private void EndRestPhase()
        {
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Rest phase ended.");
            _inRestPhase = false;
            StartNextObjective();
        }
    }
}
