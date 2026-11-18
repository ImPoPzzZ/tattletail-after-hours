using HarmonyLib;
using UnityEngine;

namespace KaleidoscopeEndless
{
    // ─────────────────────────────────────────────────────────────────────────
    // PATCH 1: Intercept death at RequestJumpscareDeath — the guaranteed
    //          entry point for all Mama kills (confirmed via decompile:
    //          MamaManager.BaseBehavior() calls this directly the instant
    //          Mama gets within mamaTouchKillDist and has line of sight, and
    //          OnNoise() also calls it directly if she's alert/close/has LOS
    //          when a noise fires — e.g. footsteps). Per explicit design:
    //          getting caught this way (proximity or noise) is always a
    //          real, immediate death — no mercy, ever. Mercy now lives
    //          exclusively in the flashlight-kill counter below (PATCH 1b).
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(MamaManager), "RequestJumpscareDeath")]
    public static class Patch_MamaManager_RequestJumpscareDeath
    {
        static bool Prefix()
        {
            if (EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive)
                return true;

            // Just teleported, or a Toy Parrot is down: she can't catch you (see MamaTeleportGrace).
            if (MamaTeleportGrace.PlayerProtected)
            {
                KaleidoscopeEndlessPlugin.Log.LogInfo(MamaTeleportGrace.ParrotDown
                    ? "KE: Mama caught player while the parrot is down — blocked, she's after the parrot."
                    : "KE: Mama caught player right after teleporting — blocked by the " + MamaTeleportGrace.GRACE_SECONDS + "s grace.");
                return false;
            }

            if (!EndlessRunManager.Instance.OnMamaCaughtPlayer())
            {
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Mama caught player — close call, not fatal yet.");
                return false; // block this catch entirely
            }

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: RequestJumpscareDeath called.");
            if (KidVoice.Active) KidVoice.Mine(KidVoice.Kind.Caught); // your kid screams as she gets you
            // Let it play through to OnJumpscareFinished.
            return true;
        }
    }

    [HarmonyPatch(typeof(MamaManager), "OnJumpscareFinished")]
    public static class Patch_MamaManager_OnJumpscareFinished
    {
        static bool Prefix()
        {
            if (EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive)
                return true;

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: OnJumpscareFinished — ending run.");
            EndlessRunManager.Instance.EndRun();
            return false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PATCH 3: Fully freeze the vanilla story quest system during endless.
    //          Objectives are now handled entirely by EndlessRunManager, so
    //          the vanilla QuestOrder must never activate, start, or complete
    //          a quest on its own — otherwise unrelated player actions (e.g.
    //          putting Tattletail down) silently advance the vanilla story.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(QuestOrder), "ActivateNextQuest")]
    public static class Patch_QuestOrder_ActivateNextQuest
    {
        static bool Prefix()
        {
            if (!HubMode.Active && (EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive)) return true;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Blocked vanilla ActivateNextQuest (story frozen during endless).");
            return false;
        }
    }

    [HarmonyPatch(typeof(QuestOrder), "StartQuesting")]
    public static class Patch_QuestOrder_StartQuesting
    {
        static bool Prefix()
        {
            if (!HubMode.Active && (EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive)) return true;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Blocked vanilla StartQuesting (story frozen during endless).");
            return false;
        }
    }

    [HarmonyPatch(typeof(QuestOrder), "CompleteCurrentQuest")]
    public static class Patch_QuestOrder_CompleteCurrentQuest
    {
        static bool Prefix()
        {
            if (!HubMode.Active && (EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive)) return true;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Blocked vanilla CompleteCurrentQuest (story frozen during endless).");
            return false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PATCH 3b: Individual Interactables can also push text onto the quest
    //           HUD directly (Interactable.OnInteractComplete's updateHUDText/
    //           hint), completely independent of QuestOrder — e.g. the
    //           Tattletail-carry pickup telling the player to "put him down"
    //           at a charging station. That's just as much "the story
    //           talking" as a QuestOrder quest is, so suppress it the same
    //           way during endless, without touching anything else
    //           OnInteractComplete does (sounds, recolor, egg pickup, etc).
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(Interactable), "OnInteractComplete")]
    public static class Patch_Interactable_OnInteractComplete_SuppressVanillaHUD
    {
        private struct SavedFields { public bool updateHUDText; public string hint; }

        static void Prefix(Interactable __instance, out SavedFields __state)
        {
            __state = new SavedFields { updateHUDText = __instance.updateHUDText, hint = __instance.hint };
            if (EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive) return;
            __instance.updateHUDText = false;
            __instance.hint = string.Empty;
        }

        static void Postfix(Interactable __instance, SavedFields __state)
        {
            __instance.updateHUDText = __state.updateHUDText;
            __instance.hint = __state.hint;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PATCH 3c: Keep any real feed/groom/charge care stations usable at all
    //           times. Interactable.ConditionUpdate() re-evaluates its
    //           InteractableConditions every single frame and overwrites
    //           currentState — so a one-time force-enable gets silently
    //           undone the very next frame. Identify each station by what it
    //           actually does (any Interactable whose action list includes
    //           FeedTattletail/GroomTattletail/ChargeTattletail — no name-
    //           guessing needed) and force it Enabled every frame instead,
    //           same as doors are force-unlocked.
    //
    //           Charging itself no longer depends on any vanilla object at
    //           all — see ChargingStation.cs. The one vanilla prop that
    //           looked like a candidate ('TattletailPlanningProp') turned
    //           out to have too many independent hidden side effects (HUD
    //           text, actionText, a Trigger component, a UnityEvent, still
    //           setting Tattletail down even with its action list rewritten
    //           to charge-only) and is now disabled outright instead. This
    //           patch is kept in case any other genuine care station exists
    //           elsewhere in the level.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(Interactable), "ConditionUpdate")]
    public static class Patch_Interactable_ConditionUpdate_ForceCareStations
    {
        // Some care stations are combined "put Tattletail down" props (e.g.
        // a crib) whose actionText describes the physical action, not the
        // effect — relabel once per instance so the on-screen prompt says
        // what it actually does instead of "[Put Tattletail Down]".
        private static readonly System.Collections.Generic.HashSet<Interactable> _relabeled =
            new System.Collections.Generic.HashSet<Interactable>();

        static bool Prefix(Interactable __instance)
        {
            if (EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive) return true;

            // ConditionUpdate() only ever runs for active-and-enabled
            // components, so this reliably catches 'TattletailPlanningProp'
            // the moment it actually becomes queryable — unlike a one-time
            // FindObjectsOfType search at level-setup time (ChargingStation
            // ran too early and never found it, since FindObjectsOfType
            // skips inactive objects and this one apparently isn't active
            // yet at that point).
            //
            // SetActive(false) on the whole GameObject was wrong — it hides
            // the visual mesh too, leaving nothing for the player to see or
            // walk toward at that spot. Strip only the interaction pieces
            // (destroy the Interactable component so it can't be raycast-
            // clicked anymore, destroy any Trigger component, clear
            // onInteractEvent) and leave the object visible as a landmark —
            // ChargingStation.cs's own trigger, at the same coordinates,
            // handles the actual charging.
            if (__instance.gameObject.name == "TattletailPlanningProp")
            {
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Neutralizing vanilla 'TattletailPlanningProp' interaction (kept visible as landmark).");
                var trigger = __instance.GetComponent<Trigger>();
                if (trigger != null) UnityEngine.Object.Destroy(trigger);
                __instance.onInteractEvent.RemoveAllListeners();
                UnityEngine.Object.Destroy(__instance);
                return false;
            }

            if (__instance.actions == null || __instance.actions.interactActions == null) return true;

            bool charges = false, feeds = false, grooms = false;
            foreach (var a in __instance.actions.interactActions)
            {
                if (a == InteractableActions.Actions.ChargeTattletail) charges = true;
                if (a == InteractableActions.Actions.FeedTattletail) feeds = true;
                if (a == InteractableActions.Actions.GroomTattletail) grooms = true;
            }
            if (!charges && !feeds && !grooms) return true;

            if (_relabeled.Add(__instance))
            {
                var parts = new System.Collections.Generic.List<string>();
                if (charges) parts.Add("Charge");
                if (feeds) parts.Add("Feed");
                if (grooms) parts.Add("Groom");
                __instance.actionText = string.Join(" & ", parts.ToArray()) + " Tattletail";
            }

            __instance.SetStateEnabled();
            return false; // skip vanilla condition gating entirely for this object
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PATCH 3c-2: Charge Tattletail directly whenever a care station is
    //             interacted with, instead of trusting InteractableActions'
    //             ChargeTattletail/FeedTattletail/GroomTattletail cases to
    //             actually fire — vanilla mechanisms on repurposed objects
    //             in this level have repeatedly turned out unreliable
    //             (leftover UnityEvents, Trigger components, etc. with
    //             unknown side effects). Cheap and idempotent even if the
    //             vanilla action list also happens to work correctly.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(Interactable), "OnInteractComplete")]
    public static class Patch_Interactable_OnInteractComplete_DirectCare
    {
        static void Postfix(Interactable __instance, bool fromAutoComplete)
        {
            if (EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive) return;
            if (fromAutoComplete) return;
            if (__instance.actions == null || __instance.actions.interactActions == null) return;
            if (RM.tattletail == null) return;

            var needs = RM.tattletail.GetNeeds();
            if (needs == null) return;

            foreach (var a in __instance.actions.interactActions)
            {
                if (a == InteractableActions.Actions.ChargeTattletail) needs.Charge();
                if (a == InteractableActions.Actions.FeedTattletail) needs.Feed();
                if (a == InteractableActions.Actions.GroomTattletail) needs.Groom();
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PATCH 3c-3: Detect interacting with the real noise source during the
    //             Silence the Noise objective. The marker's own action list
    //             is intentionally empty (see NoiseObjective.cs) — its
    //             identity, not a vanilla action, is what matters here.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(Interactable), "OnInteractComplete")]
    public static class Patch_Interactable_OnInteractComplete_NoiseObjective
    {
        static void Postfix(Interactable __instance, bool fromAutoComplete)
        {
            if (EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive) return;
            if (fromAutoComplete) return;
            if (NoiseObjective.Instance == null || !NoiseObjective.Instance.IsTheActiveMarker(__instance)) return;
            EndlessRunManager.Instance.OnNoiseSourceFound();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PATCH 3c-4: Detect interacting with the real washing machine during the
    //             WashingMachine objective. The machine's own action list is
    //             intentionally empty (see WashingMachineObjective.cs) — its
    //             identity, not a vanilla action, is what matters here. Same
    //             recipe as PATCH 3c-3 for the noise objective.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(Interactable), "OnInteractComplete")]
    public static class Patch_Interactable_OnInteractComplete_WashingMachine
    {
        static void Postfix(Interactable __instance, bool fromAutoComplete)
        {
            if (EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive) return;
            if (fromAutoComplete) return;
            if (WashingMachineObjective.Instance == null || !WashingMachineObjective.Instance.IsTheMachine(__instance)) return;
            EndlessRunManager.Instance.OnWashingMachineFound();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PATCH 3c-5: Detect interacting with the spawned Tattletail's-friend
    //             clone during the FindFriend objective. Same recipe as
    //             PATCH 3c-3/3c-4.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(Interactable), "OnInteractComplete")]
    public static class Patch_Interactable_OnInteractComplete_TattletailFriend
    {
        static void Postfix(Interactable __instance, bool fromAutoComplete)
        {
            if (EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive) return;
            if (fromAutoComplete) return;
            if (TattletailFriendObjective.Instance == null || !TattletailFriendObjective.Instance.IsTheFriend(__instance)) return;
            EndlessRunManager.Instance.OnFriendFound();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PATCH 3c-6: Detect interacting with one of the spawned vase-break
    //             triggers during the BreakVases objective. Same recipe as
    //             PATCH 3c-3/3c-4/3c-5.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(Interactable), "OnInteractComplete")]
    public static class Patch_Interactable_OnInteractComplete_VaseObjective
    {
        static void Postfix(Interactable __instance, bool fromAutoComplete)
        {
            if (EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive) return;
            if (fromAutoComplete) return;
            if (VaseObjective.Instance == null || !VaseObjective.Instance.IsAVaseTrigger(__instance)) return;
            EndlessRunManager.Instance.OnVaseBroken(__instance);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PATCH 3c-7: Detect interacting with one of the spawned candle clones
    //             during the LightCandles objective. Same recipe as
    //             PATCH 3c-3/3c-4/3c-5/3c-6.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(Interactable), "OnInteractComplete")]
    public static class Patch_Interactable_OnInteractComplete_CandleObjective
    {
        static void Postfix(Interactable __instance, bool fromAutoComplete)
        {
            if (EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive) return;
            if (fromAutoComplete) return;
            if (CandleObjective.Instance == null || !CandleObjective.Instance.IsACandleTrigger(__instance)) return;
            EndlessRunManager.Instance.OnCandleLit(__instance);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PATCH 3c-7b: Detect grabbing a spawned Power Up coin. Not routed
    //             through EndlessRunManager like the objective triggers
    //             above — grabbing a coin is an independent side-mechanic,
    //             not part of the current objective's completion.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(Interactable), "OnInteractComplete")]
    public static class Patch_Interactable_OnInteractComplete_PowerUpObjective
    {
        static void Postfix(Interactable __instance, bool fromAutoComplete)
        {
            if (EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive) return;
            if (fromAutoComplete) return;
            if (PowerUpObjective.Instance == null || !PowerUpObjective.Instance.IsPowerUpCoin(__instance)) return;
            PowerUpObjective.Instance.OnCoinGrabbed(__instance);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PATCH 3c-8: Detect interacting with the ringing phone (exfil). Same
    //             recipe as PATCH 3c-3/3c-4/3c-5/3c-6/3c-7 — not gated on
    //             an active objective, since the phone can ring during rest
    //             too.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(Interactable), "OnInteractComplete")]
    public static class Patch_Interactable_OnInteractComplete_PhoneObjective
    {
        static void Postfix(Interactable __instance, bool fromAutoComplete)
        {
            if (EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive) return;
            if (fromAutoComplete) return;
            if (PhoneObjective.Instance == null || !PhoneObjective.Instance.IsThePhone(__instance)) return;
            EndlessRunManager.Instance.OnPhoneAnswered();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PATCH 3c-9: Detect finishing a rewind on the currently-tracked tape
    //             (whether the real single-spawn objective or a
    //             SpawnAllForTesting() position-check spawn — both go
    //             through the same IsATapeTrigger() check) and fire the
    //             real Mama noise event, same call vase-break uses
    //             (RM.mamaManager.OnNoise). Only routes into the actual
    //             objective-complete flow when RewindTape is genuinely the
    //             current objective — during position-checking there's no
    //             active RewindTape objective, so this just pings Mama for
    //             noise-testing and stops there, same as before.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(Interactable), "OnInteractComplete")]
    public static class Patch_Interactable_OnInteractComplete_TapeObjective
    {
        static void Postfix(Interactable __instance, bool fromAutoComplete)
        {
            if (EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive) return;
            if (fromAutoComplete) return;
            if (TapeObjective.Instance == null || !TapeObjective.Instance.IsATapeTrigger(__instance)) return;

            Vector3 pos = __instance.transform.position;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Tape] Rewind complete on '" + __instance.gameObject.name + "' — notifying Mama.");
            RM.mamaManager?.OnNoise(pos, true);

            if (EndlessRunManager.Instance.CurrentObjective == ObjectiveType.RewindTape)
                EndlessRunManager.Instance.OnTapeRewound();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PATCH 3c-10: Detect interacting with the radio during the
    //              SilenceSpeaker objective. Same recipe as PATCH 3c-4
    //              (WashingMachine) — a single fixed-spot objective, no
    //              slot system needed.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(Interactable), "OnInteractComplete")]
    public static class Patch_Interactable_OnInteractComplete_SpeakerObjective
    {
        static void Postfix(Interactable __instance, bool fromAutoComplete)
        {
            if (EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive) return;
            if (fromAutoComplete) return;
            if (SpeakerObjective.Instance == null || !SpeakerObjective.Instance.IsTheSpeaker(__instance)) return;
            EndlessRunManager.Instance.OnSpeakerSilenced();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PATCH 3c-11: Detect finishing a restart on the fuse box during the
    //              RestartFuseBox objective. Same recipe as PATCH 3c-4
    //              (WashingMachine) — a single fixed-spot objective, no
    //              slot system needed.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(Interactable), "OnInteractComplete")]
    public static class Patch_Interactable_OnInteractComplete_FuseBoxObjective
    {
        static void Postfix(Interactable __instance, bool fromAutoComplete)
        {
            if (EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive) return;
            if (fromAutoComplete) return;
            if (FuseBoxObjective.Instance == null || !FuseBoxObjective.Instance.IsTheFuseBox(__instance)) return;
            EndlessRunManager.Instance.OnFuseBoxRestarted();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PATCH 3c-12: Detect interacting with any of the 5 True Ending easter
    //              egg objects (2 tapes, 2 friends, 1 diamond). Not gated on
    //              a specific CurrentObjective — this runs alongside the
    //              normal objective rotation for the whole run, same as the
    //              phone. TryHandleInteract() itself no-ops for anything
    //              that isn't one of its own spawned GameObjects.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(Interactable), "OnInteractComplete")]
    public static class Patch_Interactable_OnInteractComplete_TrueEndingEasterEgg
    {
        static void Postfix(Interactable __instance, bool fromAutoComplete)
        {
            if (EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive) return;
            if (fromAutoComplete) return;
            TrueEndingEasterEgg.Instance?.TryHandleInteract(__instance);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PATCH 9: Silence every background Tattletail's idle chatter during
    //          endless — this level has several other real, already-
    //          active tattletail3@Friend_N instances scattered around as
    //          decoration/quest-template props (confirmed via the earlier
    //          FRIEND-DIAG scan), each carrying its own live
    //          TattletailController and independently calling RequestTalk
    //          on an idle timer — the "hihihi" loop. Confirmed via
    //          decompile that RequestTalk() is the single method that
    //          plays this audio (AudioController.Play(soundName,
    //          tattletail.transform)) — and, not just an annoyance, every
    //          successful call also fires RM.mamaManager.OnNoise() at
    //          that instance's own position, meaning these background
    //          props have been quietly pinging real Mama noise events the
    //          whole run. Prefix-blocking RequestTalk outright for every
    //          instance except the real player-controlled one (RM.tattletail)
    //          fixes both at once, without disabling the component itself —
    //          disabling risks freezing its Animator mid-pose (the same
    //          concern noted for a puppet Mama elsewhere in this project),
    //          whereas blocking just this one method leaves everything
    //          else about the object (idle animation, visuals) untouched.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(TattletailController), "RequestTalk",
        new System.Type[] { typeof(string), typeof(bool), typeof(TattletailAnimator.Macro), typeof(TattletailAnimator.MacroFinishedCallback), typeof(bool), typeof(bool) })]
    public static class Patch_TattletailController_RequestTalk_SilenceBackground
    {
        static bool Prefix(TattletailController __instance, string soundName)
        {
            // The pick-up line ("you're up, we're having a party for Mama,
            // come help us") — blocked in Endless only, including before
            // the run is technically active (it fires right as Tattletail
            // is picked up).
            if (SceneMonitor.IsEndlessActive && soundName != null &&
                soundName.IndexOf("having_a_party", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TALK-DIAG] RequestTalk('" + soundName + "') BLOCKED (party pick-up line, Endless).");
                return false;
            }

            if (EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive)
            {
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TALK-DIAG] RequestTalk('" + soundName + "') on '" +
                    (__instance == null ? "NULL" : __instance.gameObject.name) + "' ALLOWED — run not active.");
                return true;
            }

            // Vanilla's intro "wake up" line (tt_wakeup / tt_k_please_wake_up /
            // edu_tt_wakeup depending on voice pack — confirmed via the same
            // grep-resources.assets trick that found tt_go_away_mama) loops
            // forever in Endless because whatever vanilla quest state gates
            // it never advances here (quest order is frozen — see PATCH 3).
            // Blocked unconditionally, even on the real player-controlled
            // Tattletail, unlike every other line below which stays
            // instance-gated.
            if (soundName != null && soundName.IndexOf("wake", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TALK-DIAG] RequestTalk('" + soundName + "') BLOCKED (wake-up line, Endless).");
                return false;
            }

            bool isRealTattletail = __instance == RM.tattletail;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TALK-DIAG] RequestTalk('" + soundName + "') on '" +
                (__instance == null ? "NULL" : __instance.gameObject.name) + "' isRealTattletail=" + isRealTattletail +
                " RM.tattletail='" + (RM.tattletail == null ? "NULL" : RM.tattletail.gameObject.name) + "' -> " +
                (isRealTattletail ? "ALLOWED" : "BLOCKED"));
            if (isRealTattletail) return true; // the real, player-controlled one talks normally
            return false; // every other TattletailController instance (background decor, our own spawned friend clone, etc.) stays silent
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PATCH 3d (diagnostic): Log the full configuration of whatever
    //          Interactable the player actually hovers, once per object.
    //          The "put Tattletail down" prompt keeps showing up at what's
    //          supposedly the charging station even after force-enabling and
    //          relabeling anything with a ChargeTattletail action — meaning
    //          that assumption is probably wrong. This logs ground truth
    //          instead of guessing further: name, actionText, full action
    //          list, state, hint/quest/task, and position for every object
    //          the crosshair lands on.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(Interactable), "OnHover")]
    public static class Patch_Interactable_OnHover_Diagnostic
    {
        private static readonly System.Collections.Generic.HashSet<Interactable> _logged =
            new System.Collections.Generic.HashSet<Interactable>();

        static void Postfix(Interactable __instance)
        {
            if (EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive) return;
            if (!_logged.Add(__instance)) return;

            string actionsStr = "none";
            if (__instance.actions != null && __instance.actions.interactActions != null)
            {
                actionsStr = "";
                foreach (var a in __instance.actions.interactActions) actionsStr += a + ",";
            }

            KaleidoscopeEndlessPlugin.Log.LogInfo(
                "KE: HOVER '" + __instance.gameObject.name + "'" +
                " actionText='" + __instance.actionText + "'" +
                " state=" + __instance.currentState +
                " actions=[" + actionsStr + "]" +
                " hint='" + __instance.hint + "'" +
                " disabledHint='" + __instance.disabledHint + "'" +
                " quest='" + __instance.quest + "'" +
                " task='" + __instance.task + "'" +
                " updateHUDText=" + __instance.updateHUDText +
                " newHUDQuest='" + __instance.newHUDQuest + "'" +
                " pos=" + __instance.transform.position);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PATCH 5: Block the vanilla level-complete and level-restart paths while
    //          our run is active, so the game doesn't accidentally load another
    //          scene mid-run.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(GameManager), "OnLevelComplete")]
    public static class Patch_GameManager_OnLevelComplete
    {
        static bool Prefix()
        {
            if (EndlessRunManager.Instance != null && EndlessRunManager.Instance.RunActive)
            {
                KaleidoscopeEndlessPlugin.Log.LogInfo("Blocked vanilla level complete — run still active.");
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(GameManager), "OnLevelRestart")]
    public static class Patch_GameManager_OnLevelRestart
    {
        static bool Prefix()
        {
            if (EndlessRunManager.Instance != null && EndlessRunManager.Instance.RunActive)
            {
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: OnLevelRestart intercepted — ending run.");
                EndlessRunManager.Instance.EndRun();
                return false;
            }
            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PATCH: Force PlayerActionController to always allow interaction during
    //        endless. canInteract may be false, and state check blocks us.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(PlayerActionController), "Update")]
    public static class Patch_PlayerActionController_Update
    {
        static void Prefix(PlayerActionController __instance)
        {
            if (EndlessRunManager.Instance != null && EndlessRunManager.Instance.RunActive)
                __instance.SetCanInteract(true);
        }
    }



    // ─────────────────────────────────────────────────────────────────────────
    // PATCH 6c: Make Mama always react to noise during endless — vanilla
    //           OnNoise returns early if currentPatrolNode is null or
    //           lastNoiseTimer > 0. During endless we want her to always
    //           respond to the player making noise.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(MamaManager), "OnNoise")]
    public static class Patch_MamaManager_OnNoise
    {
        private static float _nextForcedAlert;
        static void Postfix(MamaManager __instance, Vector3 noisePosition, bool bigNoise)
        {
            if (EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive) return;
            if (MamaTeleportGrace.IgnoresNoiseAt(noisePosition)) return; // the parrot is down: only its noise counts

            // If mama has no current patrol node, force her to move toward noise.
            var mamaField = typeof(MamaManager).GetField("mamaScript",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var stateField = typeof(MamaManager).GetField("currentState",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if ((object)mamaField == null || (object)stateField == null) return;

            Mama mamaScript = mamaField.GetValue(__instance) as Mama;
            if (mamaScript == null) return;
            if (mamaScript.currentPatrolNode != null) return; // vanilla handled it
            if (Time.time < _nextForcedAlert) return; // (once in a while: every noise used to restart her alert and her voice lines)
            _nextForcedAlert = Time.time + 8f;

            // Mama has no patrol node — force her into alert state directly.
            MamaState state = (MamaState)stateField.GetValue(__instance);
            if (state == MamaState.INACTIVE) return;

            var setAlertMethod = typeof(MamaManager).GetMethod("SetAlert",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if ((object)setAlertMethod != null)
            {
                setAlertMethod.Invoke(__instance, null);
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Forced Mama alert (no patrol node).");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PATCH 7 (diagnostic): Log every real audio ID the game actually plays,
    //          once each — deliberately NOT gated on RunActive, since this is
    //          meant to catch vanilla sounds during a normal (non-Endless)
    //          playthrough too, e.g. Night 2's washing machine noise. All of
    //          AudioController's other Play(...) overloads are convenience
    //          wrappers that ultimately call this exact 6-argument one
    //          (confirmed via decompile), so patching only this one overload
    //          catches every call in the game regardless of which overload
    //          vanilla code used. Same "log ground truth instead of
    //          guessing" approach WashingMachineObjective.StartObjective()
    //          already uses for its own audio item, just applied globally so
    //          we don't have to already know the real audio ID in advance.
    // ─────────────────────────────────────────────────────────────────────────
    // ─────────────────────────────────────────────────────────────────────────
    // PATCH 10: Block vanilla's intro "wake up" line at the real single
    //          choke point. The RequestTalk-based block in PATCH 9 doesn't
    //          catch this one — confirmed via the [AUDIO-DIAG] log that it's
    //          played directly (AudioController.Play("tt_wakeup", ...) on a
    //          "Wakeup Sound" GameObject), bypassing TattletailController.
    //          RequestTalk entirely. Gated on SceneMonitor.IsEndlessActive
    //          rather than EndlessRunManager.RunActive since this fires
    //          before RunActive goes true (during level load) — and NOT
    //          gated at all during a normal story Night 6 playthrough, where
    //          this line is the real, intended intro beat.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(AudioController), "Play", new System.Type[] { typeof(string), typeof(Vector3), typeof(Transform), typeof(float), typeof(float), typeof(float) })]
    public static class Patch_AudioController_Play_BlockWakeUp
    {
        static bool Prefix(string audioID)
        {
            if (!SceneMonitor.IsEndlessActive) return true;
            if (audioID == null) return true;
            bool wake = audioID.IndexOf("wake", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool party = audioID.IndexOf("having_a_party", System.StringComparison.OrdinalIgnoreCase) >= 0; // pick-up line, Endless only
            if (!wake && !party) return true;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [AUDIO-DIAG] Play('" + audioID + "') BLOCKED (" + (party ? "party pick-up" : "wake-up") + " line, Endless).");
            return false;
        }
    }

    [HarmonyPatch(typeof(AudioController), "Play", new System.Type[] { typeof(string), typeof(Vector3), typeof(Transform), typeof(float), typeof(float), typeof(float) })]
    public static class Patch_AudioController_Play_Diagnostic
    {
        private static readonly System.Collections.Generic.HashSet<string> _logged = new System.Collections.Generic.HashSet<string>();

        static void Postfix(string audioID, Transform parentObj, AudioObject __result)
        {
            // "tt_" voice IDs log every time (not deduped) with which
            // GameObject played them, so we can finally tell whether
            // "tt_laugh" (seen firing with no RequestTalk() call at all) is
            // coming from the real carried Tattletail or a background decor
            // clone. No reflection/stack-trace here — that's what froze
            // menu input last time; this is just one extra safe string.
            bool isVoiceId = audioID != null && audioID.StartsWith("tt_");
            if (!isVoiceId && !_logged.Add(audioID)) return; // non-voice IDs: once per unique ID, otherwise a full night floods this with repeat footstep/ambience calls

            var item = AudioController.GetAudioItem(audioID);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [AUDIO-DIAG] Play('" + audioID + "') on '" +
                (parentObj == null ? "NULL" : parentObj.name) + "' result=" +
                (__result == null ? "NULL" : "non-null IsPlaying=" + __result.IsPlaying() + " volumeTotal=" + __result.volumeTotal) +
                " soundMuted=" + AudioController.IsSoundMuted() + " globalVolume=" + AudioController.GetGlobalVolume() +
                (item != null ? " itemVolume=" + item.Volume + " loop=" + item.Loop + " spatialBlend=" + item.spatialBlend +
                    " category=" + (item.category != null ? item.category.Name + " catVolume=" + item.category.VolumeTotal : "null")
                    : " item=NULL"));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PATCH 8: "Low Battery Mode" run modifier — flashlight drains 2x as
    //          fast. Confirmed via decompile that FlashlightController.
    //          LateUpdate() drains its own private _lightTimer by a
    //          hardcoded "1f * Time.deltaTime" every frame (not a named,
    //          reflectable multiplier field like TattletailNeeds' rates —
    //          see RunModifiers.cs for that pattern) — so instead of
    //          fighting the inline constant, this Postfix just applies one
    //          more identical -1*dt on top, doubling the total rate without
    //          touching vanilla's own drain/shake/flicker logic at all.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(FlashlightController), "LateUpdate")]
    public static class Patch_FlashlightController_LateUpdate_LowBattery
    {
        private static readonly System.Reflection.FieldInfo LightTimerField =
            typeof(FlashlightController).GetField("_lightTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        static void Postfix(FlashlightController __instance)
        {
            if (EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive) return;
            if (!EndlessRunManager.Instance.ModLowBattery) return;
            if ((object)LightTimerField == null) return;

            float current = (float)LightTimerField.GetValue(__instance);
            LightTimerField.SetValue(__instance, Mathf.Max(0f, current - 1f * Time.deltaTime));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PATCH 6: Intercept the PatrolBehavior "all needs zero = death" check.
    //          In vanilla, mama kills you when all Tattletail stats reach 0.
    //          In endless mode we manage needs ourselves.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(MamaManager), "PatrolBehavior")]
    public static class Patch_MamaManager_PatrolBehavior
    {
        // We can't easily prefix a private method; instead we lock all needs
        // via our MamaController wrapper during the run.
        // See MamaController.cs for the needs lock logic.
    }
}
