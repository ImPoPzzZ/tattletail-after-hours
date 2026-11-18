# Tattletail: After Hours

> **Newer and bigger:** `docs/AFTER_HOURS_3.6_COMPLETE_REFERENCE.md` covers everything as of 3.6 (every game, system, 3D model, sound, tool and number). This document is kept for its history and its detail.

### A Design & Engineering Document

*Made By: ImPoPzzZ & Sa1monfr. Internal name: Kaleidoscope Endless (the DLL, the plugin folder and the BepInEx GUID keep it). Current as of 3.5 Early Access.*

---

## Preface — What This Document Is

Tattletail: After Hours (called Kaleidoscope Endless until the 3.1 rebrand) is a BepInEx/HarmonyX mod for *Tattletail*, a 2016 Unity horror game about a cursed talking toy. It started with one idea: take Night 6 — "The Party," a normal one-off story level — and turns it into a self-sustaining survival loop: five objectives shuffle endlessly, a shared antagonist AI hunts the player between them, resources drain in real time, and every so often the phone rings with a way out.

None of this existed in the base game. Night 6 has no survival mode, no objective rotation, no difficulty scaling, and no exfil mechanic. Everything described below was built from nothing, against a closed-source Unity game, using decompilation to find the real mechanisms to hook into and Harmony to intercept them without ever touching the original assembly.

Since 3.0 it has grown into a whole game around that loop. The old main menu is gone: the game opens in **the Arcade**, a walkable room full of machines. Each machine is a mode or a screen: Endless, Papa's 10th Birthday!, Versus (a 1v1 online match, hunter against child), Summer Vacation (a smoothie-bar job on a beach), a pinball machine, an air hockey table, three shops, a gumball machine for gadgets, and an OUTFITS machine for your playable kid (with a full character creator and free DLC characters).

This document exists to answer two questions in full: **what does the mod actually do**, and **how was each piece actually built**. It is organized as a set of chapters, one per system or mechanic, written for a reader who wants the real story — including the false starts, the bugs that turned out to matter, and the reasoning behind choices that aren't obvious just from reading the code.

**How it's laid out.** Chapters 1–21 are the original Endless design (written at v2.6), corrected where the numbers have since changed. Chapters 22–35 cover everything from 3.0 on. Appendix C is the version history.

---

## Chapter 1 — Overview

At the highest level, Kaleidoscope Endless replaces a five-minute scripted level with an indefinite survival loop built from six systems working together:

- **An objective rotation** of eight tasks — turn off the washing machine, survive with a lit flashlight, break three vases, light three candles, find Tattletail's hidden friend, rewind the tape, silence the speaker, restart the fuse box — drawn from a shuffled bag so every bag uses all eight exactly once, in a random order, with no back-to-back repeats. (v2.6 had the first five; the last three came later, see Chapter 24.)
- **A shared threat, Mama**, the game's real antagonist AI, repurposed to hunt the player for the duration of each objective, scaled in aggression by a difficulty setting the player picks up front.
- **A resource-management layer** — Tattletail's hunger, cleanliness, and battery all drain for real, and have to be tended at care stations between objectives, the same as they would in the base game's story levels.
- **A rest period** between objectives (30 seconds, lights back on, Mama gone) so the loop has a rhythm instead of being wall-to-wall pressure.
- **An exfil mechanic**: once every three completed objectives, the phone rings. Sixty seconds to decide — answer it and end the run clean, or let it ring out and keep playing.
- **A permanent progression layer** sitting outside any single run: shards earned per objective, spent in the shop on power ups and cosmetics (Gold and Diamond Tattletail among them). At v2.6 Gold and Diamond were time unlocks (10 and 20 minutes survived); the shard shop replaced that. Everything is saved in `Documents\Tattletail After Hours\` so it survives mod updates and reinstalls.

At v2.6 all of this was reached from an **Endless** button on the main menu with its own submenu tree (Chapter 20). Since 3.0 you walk up to the ENDLESS machine in the Arcade instead: it opens a setup screen (difficulty, run modifiers, gadget) and PLAY starts the run. When the run ends — death, the phone, or holding Esc — you land back in the Arcade. A small HUD element in the top-right corner tracks how long the current run has lasted.

The chapters that follow go system by system — what it does, and exactly how it was built.

---

## Chapter 2 — Foundations: How The Mod Is Built

### The stack

Kaleidoscope Endless is a [BepInEx](https://github.com/BepInEx/BepInEx) plugin targeting .NET 4.6 / C# 7.3, the runtime *Tattletail*'s Unity 5.5 build actually ships with. It uses [HarmonyX](https://github.com/BepInEx/HarmonyX) for runtime method patching — the mechanism that lets the mod intercept, replace, or observe calls inside the game's own compiled `Assembly-CSharp.dll` without ever modifying that file on disk. The whole project builds with a single `dotnet build KaleidoscopeEndless.csproj`, which has a post-build step that copies the resulting DLL straight into the game's `BepInEx/plugins` folder — the loop the entire mod was built against, start to finish: change code, build, launch the real game, read the log, repeat.

There is no unit-test harness, and there couldn't sensibly be one — almost everything this mod does only means anything inside a live Unity scene, wired to real game objects that don't exist outside of a running instance of *Tattletail*. Verification is real playtesting plus a disciplined logging habit (see Chapter 3).

### Decompile first, guess never

*Tattletail* is closed-source. Every mechanism this mod hooks into — `MamaManager`'s kill checks, `FlashlightController`'s charge math, `Interactable`'s state machine, `SpookyPhone`, `Candle`, `TattletailNeeds`' drain rates — was confirmed by decompiling `Assembly-CSharp.dll` (via `ilspycmd`) and reading the real implementation, not by guessing from observed behavior. This mattered constantly. A few examples that shaped real decisions elsewhere in this document:

- `MamaManager.BaseBehavior()` hardcodes the exact proximity/line-of-sight kill check Mama uses — confirming, rather than assuming, that catching the player is unconditional once she's close enough with a clear line of sight.
- `FlashlightController.LateUpdate()` drains its battery timer via a hardcoded `1f * Time.deltaTime` — not a named multiplier field — which is why "Low Battery Mode" (Chapter 5) works by adding a second, identical drain on top rather than editing a rate.
- `Interactable.OnInteractComplete()`'s `newStateOnInteract` has no inline default, so an `Interactable` built at runtime via `AddComponent` needs it set explicitly to `Enabled` or it silently goes `Hidden` and disables its own collider after the very first use — the actual cause of an early charging-station bug that looked, at first glance, like a Harmony patch issue.
- The real "golden" (vanilla) flashlight's charge-time boost is a fixed `specialMaxChargeTimeModifier` of `1.25` — a public field. Diamond Flashlight's own multiplier is defined *relative to that number*, not to the plain flashlight, which is the actual intent behind "60% more than the golden flashlight."

The pattern shows up everywhere in the codebase as inline comments citing the decompiled source of truth, because a wrong assumption about a closed engine's internals is a debugging session, not a five-minute fix.

### Where the mod attaches: SceneMonitor and the door trigger

*Tattletail*'s Night 6 ("The Party," internally `LevelEnum._6_TheParty`) is a real, normal story level. The mod cannot simply take it over the moment it loads, because a player legitimately replaying Night 6 for the story would hit the exact same scene load — and early on, that's exactly what happened: picking up the flashlight during a normal story playthrough silently converted it into an Endless run. The fix was a one-shot flag, `EndlessRunManager.PendingEndlessRun`, set only when the player actually clicks **Enter the Kaleidoscope** from the menu, and consumed (cleared) the instant the level's setup actually runs. Nothing about the level itself distinguishes "the player wants Endless" from "the player is just replaying the story" — the flag is the only signal, and it has to be armed deliberately and disarmed immediately.

Once armed, `SceneMonitor` (in `GamePatches.cs`) does the rest in two stages, mirroring the level's own real loading rhythm:

1. **On scene load** (`GameManager.OnSceneLoadComplete`, backed up by Unity's own `SceneManager.sceneLoaded`), it instantiates every persistent controller the mod needs — `EndlessRunManager`, `MamaController`, `EndlessHud`, `PhoneObjective`, `ChargingStation`, `NoiseObjective`, `WashingMachineObjective`, `TattletailFriendObjective`, `VaseObjective`, `CandleObjective`, `DebugLocationMarker` — and arms a flag, `WaitingForDoorTrigger`. It does **not** start the run yet.
2. **On the first door open**, a Harmony postfix on `InteractableActions.PlayActions` (filtered to the `FlashlightCarry` action — the moment the player actually picks up the flashlight and, per the level's normal flow, opens the first door) fires `SceneMonitor.StartEndless()` — recolor/voice application, location-resolution for every objective, `EndlessRunManager.StartRun()`, and forcing every interior door open (except the parent's bedroom — deliberately left closed; propping it open lets the player stand inside a room Mama can never enter, a real permanent safe-zone bug caught in testing).

This two-stage handoff — "get everything ready" then "start on the player's own first real action" — exists because starting a run synchronously with the scene load races against the level's own `Awake()`/`Start()` calls still populating vanilla references (`RM.fpsController`, `RM.tattletail`, etc.). Waiting for the door trigger guarantees those are already live.

### The vanilla story system is fully frozen

Night 6 still has its own vanilla quest system (`QuestOrder`) underneath everything the mod does. Left alone, it would keep trying to activate/complete/advance quests on its own, driven by unrelated player actions (setting Tattletail down, boredom timers). Three Harmony prefixes — on `QuestOrder.ActivateNextQuest`, `StartQuesting`, and `CompleteCurrentQuest` — return `false` (skip the original method entirely) for the whole duration of a run, so the vanilla story layer is inert and Endless's own `EndlessRunManager` is the only thing driving objective state.

---

## Chapter 3 — Engineering Lessons Learned The Hard Way

A few recurring bug classes shaped how almost every later system in this mod was built. They're worth stating plainly, because the comments throughout the codebase assume this context.

### `Destroy()` is not `DestroyImmediate()`

Unity's `Object.Destroy()` defers the actual removal to the end of the current frame. Almost every object in this mod that ever needs its old components stripped and new ones added **in the same call** — a submenu panel rebuilding its rows, an objective clone shedding a leftover vanilla `Interactable` before adding its own — hit the same bug shape twice, independently, before the pattern was recognized: the *old* component or child object was still technically alive and interactable for the rest of that frame, coexisting with the freshly added replacement. Concretely, this caused a stale vanilla phone `Interactable` to sit underneath a freshly spawned one on the real "Landline" prop, and it caused leftover vanilla `OptionsPanel` buttons to remain clickable underneath a freshly built submenu's own rows. The fix, applied everywhere this pattern occurs, is `DestroyImmediate()` instead — with one caveat: it mutates a live child list while iterating, so any loop doing this over `transform` children has to walk backward by index (`for (int i = count - 1; i >= 0; i--)`), not `foreach`. `GetComponentsInChildren<T>()`, by contrast, returns a plain array snapshot, so `DestroyImmediate` while iterating *that* is already safe.

### Clone real instances; never modify them in place

A recurring, better-in-hindsight pattern across every objective: resolve a *real* reference object once — a real vase, a real candle, a real Tattletail-friend model, the real "Landline" phone prop — purely to read its position or clone its mesh, and then build the actual interactive object as a **brand-new `GameObject`**, never touching the original. The alternative — bolting a fresh `Interactable` onto the *real* prop in place — was tried for the phone specifically, and broke: the real prop's collider/child hierarchy was never fully known, so there was no guarantee an interaction raycast could actually land on it. Spawning a fresh, fully-understood object at the same position sidesteps the whole class of problem, at the cost of one extra `Instantiate` call. Every objective in Chapters 6–11 follows this recipe.

### `harmony.PatchAll()` must never be allowed to take the whole plugin down

Harmony's `PatchAll()` walks every `[HarmonyPatch]`-annotated class in the assembly and applies them in one pass. If any single patch throws during that pass — and one, an unrelated jump-disable attempt, genuinely did mid-development — the *entire* `Awake()` call that contains `PatchAll()` dies with it, silently, taking down everything that was scheduled to run afterward in the same method. The visible symptom was "the main menu button is missing," which has no obvious connection to "a Harmony patch threw an exception." `KaleidoscopeEndlessPlugin.Awake()` now wraps `PatchAll()` in its own `try/catch`, purely so one bad patch can never again cascade into an unrelated, hard-to-diagnose failure.

### SuperTextMesh doesn't survive being cloned

Vanilla menu text uses `SuperTextMesh`, a third-party mesh-based text renderer with its own character-reveal coroutine and dynamic font-atlas subscriptions. Every attempt to make a *cloned* `SuperTextMesh` instance actually render text — forcing `readDelay = 0`, calling `ShowAllText()`, a per-frame force-show component — failed, and not because of a reveal-timing bug: even a state that looked fully "correct" by every readable property still rendered nothing. Rather than keep fighting an instance-for-instance-unreliable third-party component, every label in this mod's UI is a plain `UnityEngine.UI.Text`, using the game's own real font asset (captured once from a real `SuperTextMesh.font` field so it still looks native) instead of `SuperTextMesh` itself. This single decision is why every submenu in Chapter 20 looks the way it does.

### Log ground truth instead of guessing twice

Several objectives' real-world positions, audio clips, and component names were found by building a small diagnostic that dumps what's *actually* in the scene — every `Transform` whose name contains a keyword, every real `Candle` component, every `AudioSource` that's playing or about to — rather than guessing an offset or a name and hoping. The washing machine's real running sound (`dryer_clunk`, a plain `AudioClip` on a raw `AudioSource` named "Washing Machine Sound," not anything registered with the game's own `AudioController`) was found exactly this way, after a placeholder sound had been standing in for it.

---

## Chapter 4 — The Core Loop: EndlessRunManager

`EndlessRunManager` is the single state machine every other system in this mod ultimately answers to. It is a `DontDestroyOnLoad` singleton, ticked once per frame from the plugin's own `Update()`, and its job is to own exactly one thing at a time: which objective is active, whether the run is resting, or whether the phone is ringing.

### The shuffled bag

Objectives don't cycle through a fixed list, and they aren't picked independently at random either — both would let the same objective repeat back-to-back, which testing actually surfaced (`FlashlightSurvival → FlashlightSurvival`, `FindFriend → FindFriend`) as a real, noticeable problem. Instead, `NextObjectiveType()` maintains a **shuffled-bag queue**: refill it with one of each of the eight active objective types, Fisher–Yates shuffle it, and hand items out front-to-back until it's empty, then refill and reshuffle. This guarantees every bag uses all eight exactly once. The one gap a naive shuffle doesn't close on its own is the *boundary* between two bags — bag A's last item can coincidentally equal bag B's first, since each bag is shuffled independently. A one-line check swaps bag B's first slot with its second whenever that happens, closing the loophole without touching the rest of the shuffle's fairness.

### Run state

A run carries a fixed **difficulty multiplier** (picked once, from Easy/Normal/Hard/Impossible — see Chapter 5) that no longer auto-escalates lap over lap; a set of independent **Run Modifier** toggles (also Chapter 5); and three simple counters — `ObjectivesCompleted`, `LoopNumber` (how many times the phone's three-objective cycle has come round), and `RunTime`, which climbs every frame a run is active and is what both the Gold/Diamond unlock checks and the top-right HUD timer read from.

`Tick()` is a strict priority ladder, checked in this exact order every frame:

1. **Is the phone ringing?** If so, count its 60-second timer down and do nothing else this frame (Chapter 13).
2. **Is the run resting?** If so, count its 30-second timer down and do nothing else this frame.
3. **Otherwise**, tick whatever objective is currently active.

This ordering is deliberate and load-bearing: the phone overrides rest, and rest overrides an objective, never the other way around — a ringing phone always wins, full stop, regardless of what else was happening the instant it started.

### Rest phases

Between objectives, the run enters a fixed 30-second rest phase: lights come back on, Mama is inactive, and the HUD shows a static "Rest Period... She's Gone... For Now..." message. That message is deliberately **set once and never touched again** for the whole rest period, rather than re-pushed every second with a live countdown — because the vanilla text renderer's reveal animation restarts on every `.text` assignment, so a live countdown never gets a chance to finish drawing before the next update yanks it back to the start. A run modifier, "No Rest for the Wicked," skips this phase entirely and chains straight into the next objective.

### Objective quest text and the close-call mercy message

All quest text goes through one gate, `SetObjectiveQuestText()`, which does two things: it records what the *current objective's own* text actually is (`_currentObjectiveHudText`), and it pushes that text to the HUD — unless the phone is currently ringing, in which case the phone's own message stays on screen and the objective's text is merely remembered for later restoration. This single indirection is what makes two otherwise-awkward interactions work cleanly: a phone call that starts the instant an objective begins doesn't get its message immediately overwritten by that objective's own opening line, and a temporary "She almost got you!" close-call warning (Chapter 7) can restore the *real* objective text afterward instead of leaving the mercy message stuck on screen permanently, which is exactly what happened before this restoration path existed.

### Death and mercy

Getting physically caught by Mama — touch or line-of-sight, at any point, in any objective including Flashlight Survival — is always an immediate, unconditional death. There is no mercy on that path anymore, by explicit design. The **only** mercy mechanism left is the flashlight-kill counter, described in Chapter 7: three flashlight kills (not touch-catches) during Flashlight Survival specifically, and only the third one is fatal.

---

## Chapter 5 — Difficulty & Run Modifiers

### Difficulty

Difficulty is a single player-chosen multiplier — Easy (0.7×), Normal (1.0×), Hard (1.4×), Impossible (2.0×) — fixed for the whole run rather than auto-ramping lap over lap the way an earlier version did. It feeds two separate places: Mama's own aggression scaling (alert-state duration and patrol speed, see Chapter 12) and the Flashlight Survival timer, which shrinks by `1/√difficulty` rather than linearly, so the hardest setting doesn't make that objective trivially short.

A separate, much larger multiplier exists purely for the "No More Mama!" run modifier below (10×, fixed) — deliberately kept independent of the main difficulty dial, so that toggle affects only how aggressive Mama specifically is, without also compressing every other difficulty-scaled number in the run.

### Run Modifiers

Seven independent, freely-combinable toggles, all off by default (the seventh, Power Ups, came after v2.6: see Chapter 24):

| Modifier | Effect | How it works |
|---|---|---|
| Low Battery Mode | Flashlight drains twice as fast | A Harmony postfix on `FlashlightController.LateUpdate()` applies a second, identical `-1 × Time.deltaTime` drain on top of vanilla's own hardcoded rate — there's no named multiplier field to hook, so this doubles the *effective* rate by addition instead |
| No Rest for the Wicked | No rest phase between objectives | `CompleteObjective()` branches straight to the next objective instead of `StartRestPhase()` |
| No More Mama! | Maxes out Mama's aggression | Pins her difficulty multiplier to a fixed 10× regardless of the main difficulty setting |
| Batteries Not Included | Tattletail's charge drains 50% faster | Reflection-multiplies `TattletailNeeds`' private `batteryDecreasePerSecond` field once at run start |
| Always Hungry | Tattletail's hunger drains 50% faster | Same mechanism, `hungerDecreasePerSecond` |
| Brushing is Key | Tattletail's cleanliness drains 50% faster | Same mechanism, `dirtinessDecreasePerSecond` |
| Power Ups | A random-ability coin may spawn each objective | 20% roll per objective (`PowerUpObjective.cs`); doesn't add to the shard bonus |

The three need-drain modifiers share one small helper, `RunModifiers.ApplyNeedDrainModifiers()`, because `TattletailNeeds` computes its actual drain-per-second fields once, privately, in its own `Awake()`, with no public multiplier to hook — reflection is the only way in, and it's applied exactly once at run start (fresh each level load, so it can never compound across multiple runs).

---

## Chapter 6 — The Washing Machine

The simplest objective, and the template every other "find and interact" objective borrowed its shape from. The player is told something's running in the house; find the washing machine and turn it off, while Mama hunts.

The real vanilla washing machine prop and the vanilla quest system that would normally point at it both turned out unreliable in this repurposed level, so the objective is entirely self-contained: a fresh, invisible `GameObject` is placed at the machine's real resolved position (found by keyword search against every `Transform` in the scene, cached once and reused every time this objective comes up) with its own solid collider and a from-scratch `Interactable` — "Turn Off Washing Machine," a 1.2-second hold. No separate visual marker is needed since the real washing machine's own mesh is still sitting exactly where it always was; only its interaction pieces are replaced.

The running noise itself is the real game's own audio — `dryer_clunk`, a raw `AudioClip` living directly on a plain Unity `AudioSource` named "Washing Machine Sound," not anything routed through the game's own `AudioController`. That clip has to be captured *once, very early* (as soon as the level loads, well before the objective actually starts) because the real GameObject carrying it only seems to exist briefly early in the level's lifetime — the `AudioClip` asset reference itself, once captured, stays valid regardless of what later happens to the object it came from, so the resolve step and the objective-start step run on deliberately different timers.

---

## Chapter 7 — Flashlight Survival

The one objective with no interactable at all: hold the flashlight open and simply survive a timer (30, 60, or 90 seconds, picked randomly per attempt, then scaled by `1/√difficulty`) while Mama hunts.

The flashlight itself is force-equipped via the game's own `SetFlashlight(true)` and `MaxLight()` calls — there's no physical flashlight pickup anywhere in this level to walk up to — and, if the Diamond Flashlight run modifier is active, the Diamond boost (Chapter 17) is re-applied *immediately before* the light is topped up, not after, because equipping the flashlight silently re-runs vanilla's own status logic and would otherwise revert the boost the instant it's re-applied.

### Flashlight kills are mercy, not death

This is the one place in the whole mod where getting "caught" doesn't automatically end the run. Mama killing the flashlight's light (a distinct event from a touch/line-of-sight catch, confirmed via decompile) is detected by polling `FlashlightController.IsGlowing()` once per frame rather than patching `KillLight()` directly — an earlier attempt to patch that method directly, even with a completely empty, exception-free patch body, silently broke the game's own lights-out fade coroutine for reasons that were never fully root-caused (an old Mono JIT interaction was the leading theory), and polling sidesteps the whole class of problem. The first two flashlight kills in a single attempt show a warning ("She almost got you! (2/3)") and let the player keep going — the light stays dark until they shake it back on, with no auto-recharge. The third is fatal.

---

## Chapter 8 — Break the Vases

Break three of six possible marked spots around the house, each one played out as a genuinely loud action Mama can actually hear.

All six spawn positions were **hand-marked in-game** by the developer using a temporary debug tool (`DebugLocationMarker`, bound to the E key — see Chapter 3's sibling systems), rather than reused from wherever the game's own six vase props happen to sit, for full control over placement instead of being stuck with awkward original spots. A real vase is resolved once, purely as a clone template — never touched in place — then scaled up 1.6× and tinted a solid green (props were originally reported as too small to notice; green was a specific stylistic choice), spawned at 3 of the 6 marked spots chosen randomly at objective start.

Breaking one plays a small procedural shard-burst (physics cubes flying outward under gravity, no collider so they can't get stuck in geometry, auto-cleaned after 3 seconds — there was no existing shatter VFX to reuse), the real `shatter` audio item (an existing, previously-unused vanilla sound), and — critically — calls `RM.mamaManager.OnNoise()` at the break position, the same call the game's own noise system uses. Breaking a vase is real, mechanical risk, not a free objective step.

Each spawned clone is tracked by a **slot index** — its ordinal position (0, 1, 2) in the chosen spawn set — rather than by direct object reference, which is what actually lets `EndlessRunManager.OnVaseBroken()` identify *which* vase was hit and update "Break the vases! (1/3)" progress text correctly.

---

## Chapter 9 — Light the Candles

Structurally identical to Break the Vases — six hand-marked spots, three chosen per attempt, slot-indexed clone tracking — but built around `Candle`, a real, dedicated vanilla component confirmed via decompile (`SetCandle(bool)` plays a flame particle system and restores the model's emissive glow). Clones start unlit (`startOn = false`); one small subtlety is that `Candle.SetCandle()` reads a material field the component only assigns inside its *own* `Start()` — calling it manually the instant a clone is spawned would hit that field before it's ready and throw, so the clone's `startOn` flag is set instead and the component's own `Start()` handles lighting itself correctly on its own schedule.

Lit candles stay lit as more are found (no re-extinguishing), and once lit, a candle's `Interactable` is destroyed outright rather than merely ignored — otherwise it would sit there hoverable forever, prompting "[Light]" on an object that visibly already has nothing left to do.

---

## Chapter 10 — Find Tattletail's Friend

Find and interact with a second, friendly Tattletail figure hidden somewhere in the house. Unlike the vanilla night 3/4 hide-and-seek quest this borrows its theme from, the actual `QuestSeek`/`QuestHide` components those nights use don't exist anywhere in this scene — there's no working vanilla mechanism left to reactivate here, so this is a fully self-built objective.

A real, already-placed, active Tattletail-friend model instance is resolved once as a clone template (checked against a list of the game's own internal naming conventions for these props), then spawned at one of six hand-marked spots, chosen at random each attempt. The interaction itself, "Say Hi," is a 1.2-second hold — deliberately the longest hold of any objective, since finding a hidden object is meant to be the payoff on its own, with the interaction itself a small, calm confirmation rather than another timed challenge.

---

## Chapter 11 — Silence the Noise *(retired from rotation)*

An earlier, related objective — find and turn off a noise source — is still fully implemented (`NoiseObjective.cs`, its own Harmony detection patch) but deliberately excluded from the active five-objective rotation. It originally randomized between three possible locations (kitchen, garage, the washer), but the kitchen and garage spots were never more than guessed offsets from unrelated landmarks and were never reliably findable in practice; restricted down to just the washer per explicit direction, it became mechanically identical to the Washing Machine objective — "walk to the washer, interact with it" — and having both active in the same rotation would mean doing that exact same action twice in one lap. The code is deliberately left in place rather than deleted, in case real kitchen/garage positions get confirmed later and it's worth reviving as a genuine three-spot search again.

---

## Chapter 12 — Mama: The Threat

`MamaController` is a thin, purpose-built wrapper around the game's own `MamaManager` — it doesn't reimplement her AI, it repurposes the real thing, bypassing the parts of her normal activation path (`MamaSpawner`) that would conflict with how this mod already manages lighting and quest state.

### Activation

`ActivateMamaForObjective(difficultyMult)` is called at the start of every objective. Rather than going through `MamaSpawner.StartActivation()` — which calls `RequestLightsOut()` internally (conflicting with the mod's own call) and polls a quest-order field the mod has already frozen — it calls `InitializeNewMama()` directly to build her model and script, resets her internal patrol-node-set field via reflection (required before `InitializeNewMama` can register a fresh one), then calls `ActivateMama()` directly to put her into patrol state. Suspense music (`mama_music`) is started manually, since bypassing `MamaSpawner` means nothing else starts it. Two numbers scale with the difficulty multiplier passed in: her **alert-state duration** (`alertTimerMax`, read via reflection — no public setter exists), raised from a much lower vanilla base so she's noticeably more active even at the easiest setting, and her **patrol speed** (`minimumNodePatrolTime` on the `Mama` component, also reflection-only) — lower means she lingers on each patrol node for less time, i.e. moves faster.

### The kill path

Every Mama kill — touch-proximity or line-of-sight during a noise event — funnels through one real vanilla method, `MamaManager.RequestJumpscareDeath()`, confirmed via decompile as the single guaranteed entry point for both cases. A Harmony prefix on it asks `EndlessRunManager.OnMamaCaughtPlayer()` — which, per Chapter 4's design, always returns `true` now: every catch is fatal, unconditionally, with mercy living exclusively in the flashlight-kill counter (Chapter 7). A second patch, on `OnJumpscareFinished`, ends the run once her kill animation completes.

### Making her actually respond to noise

Vanilla's own `OnNoise()` silently no-ops if Mama currently has no assigned patrol node, or if her internal noise-cooldown timer hasn't expired — both conditions that are far more likely to be true in this heavily-repurposed level than in a normal story night. A Harmony postfix forces her into an alert state directly (via reflection into her private state machine) whenever she has no patrol node assigned, so noise — including a broken vase — reliably provokes a reaction instead of occasionally being silently swallowed.

---

## Chapter 13 — The Exfil Phone

Once every three completed objectives (`LOOP_LENGTH = 3`), the phone rings. The player has 60 seconds to decide: walk up and answer it, ending the run cleanly (not a death — tracked separately in Stats), or ignore it and keep playing. While it's ringing, everything else — the current objective, Mama's activation — is fully paused; the phone is not a side quest, it fully overrides the run for its duration.

### Why it looks like the rest phase

The entire ring/timer/quest-text state machine in `EndlessRunManager` (`StartPhoneRing()`/`EndPhoneRing()`/`OnPhoneAnswered()`) deliberately mirrors `StartRestPhase()`/`EndRestPhase()` line for line: set the HUD text exactly once, count a plain timer down in code, never re-push the text. This wasn't a coincidence of taste — it's a direct instruction to copy a mechanism already proven to work, once an earlier, independently-built version of the phone ran into the exact same "text keeps restarting its reveal animation" problem the rest phase had already solved.

### The marker is a clone, never the real prop

`PhoneObjective` resolves the real, in-scene "Landline" phone prop (a genuine `SpookyPhone` component, confirmed via decompile, with a name-based fallback in case this particular scene's copy doesn't carry that component) purely to read its position — the exact same "clone, never modify in place" recipe as every objective in Chapters 6–10, and for the exact same reason. An earlier version bolted a fresh `Interactable` directly onto the real prop, stripping its original vanilla one first — which surfaced two compounding bugs at once: the deferred-`Destroy()` issue from Chapter 3 (old and new interactable briefly coexisting), and a real prop whose actual collider hierarchy was never fully known, meaning there was no guarantee a raycast could land on it at all. The current version spawns its own fresh `GameObject` — "End Run?" as the prompt, an 0.8-second hold — at the real phone's position instead, exactly mirroring `NoiseObjective.cs`'s washing-machine-marker pattern, and never touches the real prop.

### Why it doesn't ring at the start of a run

An earlier debug build rang the phone immediately at the start of every run, purely to make the mechanic fast to test without waiting through a full lap. That debug shortcut was later found still wired into `StartRun()` — the phone was, unintentionally, always the very first thing a player encountered — and was removed once flagged; the real, intended trigger is exclusively "once per completed lap," fired from `CompleteObjective()` the moment `ObjectivesCompleted` crosses a multiple of `LOOP_LENGTH` (3). (It was "once per lap of five" at v2.6; with eight objectives a full lap took too long.) Answering pays an exfil bonus: 25% of the shards earned that run.

### Mama defers to the phone

`MamaController.ActivateMamaForObjective()` checks `EndlessRunManager.PhoneIsRinging` before doing anything and silently no-ops if it's true — so an objective transition that happens to land mid-ring can't accidentally activate her while the player is meant to be safe deciding. Once the call resolves (answered or missed), `EndlessRunManager.ResumeMamaAfterMissedCall()` retries whatever activation was skipped, so a missed call doesn't leave her permanently inert for the rest of the run.

---

## Chapter 14 — Resource Management & The Charging Station

Tattletail's four needs — hunger, cleanliness, battery, and boredom (locked off entirely, since nothing in this mod's objective loop ever meaningfully engages it) — drain for real during a run, the same resource-management layer the base game's story nights use, tended at care stations exactly like a normal playthrough.

### Why the real charging prop was abandoned

The obvious candidate for a charging station — `TattletailPlanningProp`, a real vanilla object sitting exactly where the level's own story already points to a care spot — turned out to carry a pile of independent hidden side effects: its own HUD text, its own `actionText`, a `Trigger` component, and a wired `UnityEvent`, none of which were safe to trust for a clean, predictable interaction. Rather than keep neutralizing side effects one at a time, it's now fully stripped of its interactive pieces (`Interactable` and `Trigger` destroyed outright, `onInteractEvent` cleared) but left **visually intact** as a landmark — the player still sees it and knows where to walk. A brand-new `ChargingStation` object, built entirely from scratch at the exact same coordinates, with nothing pre-wired and nothing borrowed from the scene's original configuration, handles the actual interaction: "Charge Tattletail," a 5-second hold (originally 1.2s, then 10s, settled at 5s after direct feedback).

One subtlety cost real debugging time: `Interactable.newStateOnInteract` has no inline default and is `Hidden` (0) unless explicitly set. Left unset, the station worked exactly once — after the first successful charge, `OnInteractComplete()` applied that default state and disabled its own collider permanently, and a separate patch forcing its *state* back to `Enabled` every frame didn't touch the *collider*, which had been switched off through a completely different code path. Setting `newStateOnInteract = Interactable.State.Enabled` explicitly at construction time was the actual fix.

Any other real feed/groom/charge station elsewhere in the level is force-kept-enabled every frame via a Harmony patch on `Interactable.ConditionUpdate()` (which vanilla re-evaluates every single frame, silently undoing any one-time force-enable), and a second patch fires the actual need-restoring effect (`needs.Charge()`/`Feed()`/`Groom()`) directly whenever any such station is interacted with — deliberately not trusting the vanilla action-list dispatch to reliably fire it, since vanilla mechanisms on repurposed objects in this level have proven unreliable more than once.

---

## Chapter 15 — Tattletail Customization: Colors & Voices

Reachable from Endless → Customization, and entirely a per-player local choice — even in multiplayer, where difficulty and run modifiers are host-authoritative (Chapter 21), cosmetic picks always stay local to whoever's screen they're set on.

**Tattletail color** offers the game's own five real vanilla recolors (Original, Yellow, Teal, Black, White — confirmed via decompiling `RecolorType`, which has no more entries than that) plus two mod-original tiers, Gold and Diamond, both locked behind survival-time unlocks (Chapter 16). **Kaleidoscope Voice** swaps every one of Tattletail's vanilla audio lines for the game's own alternate "educational" narrated set (`TattletailController.TattletailType.EDUCATIONAL`) — a real, existing, exposed enum, not a custom audio replacement. **Kaleidoscope Mama** is a separate, off-by-default toggle that flips the same per-level "kaleidoscope" flag for Mama's own model/eye-cycling/recolor/bark-audio, applied at the moment she (re)activates for each objective, not once at level start — the flag lives on the level's own `LevelData` object and has to be set immediately before she rebuilds herself.

---

## Chapter 16 — Shards, the Shop & Gold/Diamond

Progression is a shard economy (`Shop.cs`, persisted in `Shop.txt` in `Documents\Tattletail After Hours\`; `SaveFiles.cs` moved older saves there from the install folder and from `Documents\Kaleidoscope Endless\`). Every objective pays 10 shards, plus 10 for every active Run Modifier (Power Ups doesn't count), all times the difficulty's shard multiplier (Easy ×0.5, Normal ×1, Hard ×1.5, Impossible ×2). Chaos Round and Lucky Night each double it (Chapter 24), and answering the phone adds 25% of the run's shards. Shards buy single-run Power Ups (Head Start Battery, Sharper Eyes, Coin Magnet, Bigger Pockets, Steady Hands, Starting Totem, Diamond Flashlight; consumed to `= 0` when a run launches) and permanent cosmetics (Tattletail colors incl. Gold/Diamond, flashlight beam colors, Kaleidoscope Voice/Mama). The old time-based Gold/Diamond unlocks were removed. The Shop shows 3D preview renders of each color, baked once from cloned in-level models by `PreviewBaker.cs` into `ShopPreviews/`.

### Why neither is a real vanilla recolor

Decompiling `RecolorType` confirmed it has exactly seven values, none of them gold or diamond, and `RecolorManager` only ever swaps in pre-made Material assets wired up per-enum-value directly in the scene — there is no material to swap to and no enum slot to add one under. Both tiers are self-contained tint operations instead, applied directly to the model's real renderers.

### The actual recolor recipe, and why it took several attempts

A full renderer inventory (logged once per application, kept as an ongoing diagnostic) confirmed Tattletail's fur is one renderer running a custom shader (`FurShaderControllerV1`/`Custom/FurShaderV1`), and the ears are two separate renderers on a plain Standard-shader material shared with the eyes, feet, and flesh — which must never be touched, since a material is normally shared across every renderer using it (Unity auto-instantiates a per-renderer copy only once `.materials`, not `.sharedMaterials`, is actually touched, which both recolor scripts rely on to avoid bleeding a tint into unrelated parts).

Getting the fur right took several real attempts. Fighting the custom fur shader's own properties directly (`_Color`, `_FurColor`, texture stripping) got the ears looking correct every time (they were already forced onto a plain Standard shader) while the fur kept landing wrong at every step — purple, then brown, then flat cream with no tint at all once a texture-strip attempt apparently hit the shader's own white fallback. The eventual fix, per direct instruction ("make the fur like the ears"), was to stop fighting the custom shader and force the fur's base coat and shell material onto the *same* plain Standard shader the ears already used successfully — at the cost of losing the shell's own per-layer fur displacement (length/wind/cutoff, normally driven every frame via a `MaterialPropertyBlock`), which Standard simply ignores, so the "fur" renders as a solid shiny surface rather than fuzzy strands. That trade was already implicit in how the ears looked, and is the explicit ask for the fur too.

Even the final color choice took a real correction: a first pass used a near-max-brightness gold that looked fine as an albedo under a *lit* shader, but `Unlit/Color` — the shader both tints ultimately land on, chosen specifically because this scene's uneven, often near-zero direct lighting made any *lit* shader swing unpredictably between "way too dark" and "blown-out glowing halo" depending on where the player happened to be standing — renders flat and unattenuated, and that same near-max value blew straight through the game's Bloom post-process into a solid white glow. Both Gold and Diamond's final colors are pulled well below max brightness specifically to stay a flat, readable tint under Unlit rendering instead of becoming an unintended light source.

**Superseded in 3.2 and 3.3.** The flat Unlit tint was replaced twice. 3.2 made both tiers lit fur colors that react to the house's light, like Mama's and Papa's skins. 3.3 (`FurShine.cs`) went back to the game's **real** fur shader and changes only its strand color (`_FurColor`), exactly the way the game's own Yellow and Teal recolors work, so Gold and Diamond get the same soft layered fur as the original. Small sparkles (a soft four-point star on `Particles/Alpha Blended`) twinkle around him.

---

## Chapter 17 — The Diamond Flashlight

A separate Run Modifier, locked behind the same 20-minute Diamond unlock, applied once at level start rather than patched per-frame: a longer-charging, brighter, blue-white-tinted flashlight.

**Charge time** is pinned to `1.6×` the real vanilla "golden" flashlight's own charge-time boost (`specialMaxChargeTimeModifier`, a public field confirmed at `1.25` via decompile) — not `1.6×` the plain flashlight, since this mod never actually sets `GD.hasSpecialFlashlight` true, so the flashlight's live modifier would otherwise still be sitting at a plain 1× baseline. **Charge-per-shake** is pinned so that exactly two shakes from empty always reaches the new, taller ceiling, regardless of what a player's own save file might otherwise apply there.

**Brightness** scales two genuinely different things by two different amounts, discovered the hard way: the beam itself is a real dynamic Unity `Light`, and boosting it by the same amount as everything else blew straight through this game's Bloom post-process specifically during Flashlight Survival — the one objective that's otherwise total darkness with nothing else on screen to dilute the bloom — so the beam gets a more modest ×1.3, while the glow baked into the prop's own material (not a scene light, no bloom risk) gets a fuller ×1.9. Both multipliers scale from the *real, live* baseline value read directly off the flashlight the first time this runs each level — not a hardcoded assumed default, which was the actual cause of an earlier "still dim" report: the component's field initializers (`0.3`/`1`) are compile-time defaults that don't necessarily match the real serialized prefab values, and hardcoding them risked silently *lowering* an already-higher real value instead of raising it.

**Color** requires touching two separate things for the same reason the beam and glow needed separate multipliers: `currentColor` alone only drives the emissive glow parts (re-applied every frame by the game's own code), while the beam's actual `Light.color` is set exactly once by vanilla and never revisited — so the beam has to be tinted directly, once, right here. The carried prop's own visible body is recolored too, using the same forced-`Unlit/Color` technique from Chapter 16, deliberately skipping every renderer the game's own glow-pulse animation writes to every frame (`glowingRenderers`), since `Unlit/Color` has no property for that animation to drive and would otherwise just freeze it on whatever color it happened to be tinted.

---

## Chapter 18 — Stats: Tracking a Life of Runs

A small, permanent, menu-accessed panel (Endless → Stats) reporting five all-time facts: last survival time, longest survival time, total objectives completed across every run ever played, total deaths, and total successful exfils (phone calls answered). This replaced an earlier design — an automatic pop-up shown right after every death — because a full paragraph of text proved illegible against the cloned vanilla panel background it had to render on top of; five short labeled facts fit this mod's standard short-row submenu recipe cleanly, where a paragraph never could.

Persistence uses the same save folder as the shop (Chapter 16; at v2.6 this was the game's install folder) — a plain `Stats.txt`, five `Key=Value` lines, read once and cached, rewritten on every objective completion and every run end. Survival times are stored as whole seconds (a plain `int`), not raw floating-point — an explicit, direct correction after an early build wrote absurdly over-precise fractional-second values (`9.283902...`) into the file; nobody needs sub-second precision here, and truncating to whole seconds keeps the file actually readable.

The five value rows are deliberately built from the same real, clickable button widget every other row in this menu uses (`CloneButton`, just with no click handler attached) rather than a plain text label — purely so they get the same hover-highlight feedback as everything else on the panel, instead of looking inert and "dead" next to rows that do respond to the cursor.

---

## Chapter 19 — The HUD

A small, hand-built overlay canvas — not cloned from any vanilla panel, since none of vanilla's busy background art was needed here — showing a single top-right `MM:SS` timer, visible for the full duration of any active run. It's created once per Endless run alongside `EndlessRunManager`/`MamaController` and simply reads `EndlessRunManager.RunTime` every frame. Objective and phone quest text deliberately do **not** route through this overlay; that channel is, and always has been, the game's own real HUD quest-text system (`RM.hud.SetNewQuest`, via `EndlessRunManager`) — this stays a single small corner readout, not a second competing text surface.

---

## Chapter 20 — The Menu System: Building UI From Scratch

*Legacy since 3.0: the main menu's buttons are hidden and the Arcade loads instead (Chapter 22). The Arcade's own screens use `OverlayPanel` (Chapter 22), not cloned menu widgets. This chapter is kept because the lessons still apply, and a few submenus (Scrapbook, Papa's party) still use this recipe.*

Every menu screen this mod adds — the top-level Endless button, and the full Customization/Difficulty/Run Modifiers/Stats/Multiplayer/Run Customization submenu tree beneath it — is built by **cloning real vanilla widgets** directly out of the game's own main menu (`Button_Quit`, `OptionsPanel`, `Button_Sensitivity`, `Button_MotionBlur`, `Button_Back`, `OptimizeText`), rather than hand-building generic `Image`+`Text` rectangles from scratch. Cloned buttons are parented directly into the real menu hierarchy — a sibling of the real options panel, a child of the real `mainMenuHolder` — so they inherit the game's own `Canvas`/`EventSystem`/sprite art for free, and are destroyed and rebuilt automatically every time the "menu" scene (re)loads, exactly like every other piece of vanilla menu UI, with no custom `DontDestroyOnLoad` root or manual visibility polling required anywhere.

### Replacing a click handler safely

A cloned vanilla `Button` carries over whatever *persistent* (Inspector-wired) click behavior its source template had — `Button_Quit`'s clone, left alone, would still quit the game on click, in addition to whatever this mod wires up. `UnityEvent.RemoveAllListeners()` only clears *runtime* listeners, not persistent ones (the API that can strip those, `UnityEventTools`, is Editor-only and unavailable in a build), so the actual fix is to replace the whole `Button` component outright: capture its visual transition/color settings, destroy it, add a fresh one with an empty event, and only then wire up the new click handler.

### Why layout is manual, not `VerticalLayoutGroup`

Every submenu panel is a straight clone of `OptionsPanel`, which ships with its own `VerticalLayoutGroup` — and every attempt to actually rely on it for a reused, resized panel failed in a different way. First, children never laid out at all, because deactivating the panel immediately after building it meant its deferred layout pass never got a chance to run. After forcing that rebuild, children *were* evenly spaced relative to each other, but the entire stack landed measurably outside the panel's own visible bounds — not a rendering glitch, a real, measured wrong-position bug, confirmed by comparing `GetWorldCorners()` on the panel against its children. Rather than keep chasing whatever padding or anchor assumption a reused, differently-sized panel violates, every submenu simply disables its `VerticalLayoutGroup` outright and positions every child itself — a small shared helper, `MainMenuButtonInjector.PositionChild()`, anchors each row to the panel's top edge at an explicit Y offset and height, deterministic and independent of any layout system's assumptions. It is more code than trusting a layout group would have been, and it has worked correctly every single time since.

### The submenu tree

**Endless** (the top-level panel) has just six rows: Customization, Difficulty, Run Modifiers, Stats, Multiplayer, and Enter the Kaleidoscope. Everything that used to be flat, individual toggles at this level — Tattletail color, voice, cosmetic Mama voice — now lives one level down, inside **Customization**, specifically so this top panel could stay short. **Run Modifiers**' six toggles are freely combinable (Chapter 5); in multiplayer, a guest sees the exact same panel in a read-only state, mirroring whatever the host has chosen (Chapter 21). **Stats** and the rest of the tree follow the same recipe throughout.

---

## Chapter 21 — Multiplayer: Steam Lobbies

*Step 1 below (lobby and shared settings) is still the transport everything online runs on. The gameplay sync it was waiting for became Versus in 3.1 (Chapter 25), and air hockey rides the same lobby (Chapter 31).*

A friends-only, two-player-max co-op layer, reached from Endless → Multiplayer. This is deliberately scoped as **Step 1**: establishing the lobby connection, syncing shared run settings, and getting both players into the level together — no in-run gameplay state (movement, objective progress, Mama, death) is synchronized yet. That is explicitly the next phase of work, built on top of a connection layer that's already proven solid.

### Why Steam, not raw sockets

An intermediate version of this system was rewritten to use plain UDP sockets with a hand-rolled ACK/retry reliability layer and an `IP:PORT` "join code," specifically to drop the dependency on Steamworks. That version worked, but reintroduced exactly the class of problem Steam's own networking exists to solve — manual port forwarding, NAT traversal, no relay fallback — for a two-player co-op feature where Steam's existing infrastructure already solves all of it for free. The mod ultimately returned to Steam P2P. This was made possible by a fact confirmed directly against the game's own decompiled assemblies rather than assumed: the full Steamworks.NET class set (`SteamAPI`, `SteamNetworking`, `SteamMatchmaking`, `SteamFriends`, `CSteamID`) is already baked into `Assembly-CSharp-firstpass.dll`, sitting right next to the game's own main assembly — no new external dependency to source, just one more `<Reference>` in the project file. The base game's own `SteamManager` already calls `SteamAPI.Init()` on startup and throws if initialized twice, so this mod's own networking code is careful never to call it again.

### Packets

`MpPacket` is a minimal binary reader/writer — write primitives in a fixed order, read them back in the same order — with nothing proprietary about its shape. Every packet begins with its own integer ID; `MpNetworking` maintains the dispatch table from ID to handler and wraps `SteamNetworking.SendP2PPacket`/`ReadP2PPacket` directly. Reliability is Steam's own (`k_EP2PSendReliable`), not a hand-rolled layer — a deliberate simplification once the design returned to Steam's own transport, since Steam already guarantees delivery on that send mode.

Step 1 only ever sends three packet types: `Welcome` (a connectivity confirmation, informational only — real peer membership is driven by actual Steam lobby membership, not by this packet arriving), `LobbySettings` (the host's current difficulty and Run Modifier picks, described below), and `LevelTransition` (the signal that tells a connected guest to follow the host into the level). Every later ID needed for real gameplay sync — movement, objective start/progress/complete, Mama state, death/respawn — is already reserved in the enum, deliberately left unimplemented, so the next phase of work can add handlers without renumbering anything already wired and working.

### Lobby lifecycle

`MpLobby` is a persistent, `DontDestroyOnLoad` singleton, created once from the plugin's own `Awake()` — unlike the run-scoped controllers in Chapter 2, it has to exist starting at the main menu, before any run begins, since hosting or joining happens entirely from the menu. Hosting calls `SteamMatchmaking.CreateLobby()` (friends-only, capacity 2); joining happens two ways — accepting a Steam overlay invite while the game is already running (`GameLobbyJoinRequested_t`), or a **cold start**, where accepting an invite launches an entirely fresh game process with `+connect_lobby <id>` on its command line, Valve's own documented convention for this case. The cold-start path retries the join a few times with real delays between attempts, because a real two-player test confirmed that joining immediately — before the "menu" scene has actually loaded, and before Steam's own networking is fully warmed up on a fresh launch — silently fails.

The moment a second lobby member is actually detected (`RefreshPeer()`, driven by real Steam lobby membership, not by any packet), the host immediately broadcasts its current run settings, so a guest who joins mid-configuration sees the host's actual current picks instead of stale defaults.

### Host-authoritative shared settings

Difficulty and the six Run Modifiers are **host-authoritative** — the host picks, `MpLobbySettingsSync.BroadcastCurrentSettings()` sends the current state on every host edit and on every new peer connection, and the guest's own copies of `RunCustomizationSubmenu`/`RunModifiersSubmenu` render those values read-only. This has to be a single agreed value between the two players, not two independent local guesses, because both settings ultimately scale the *one* shared Mama both players will eventually be hunted by — letting each player pick their own would mean the two players' clients disagreeing about how dangerous she is. Cosmetic picks (Tattletail color, voice, cosmetic Mama voice — Chapter 15) stay exactly the opposite: always local to whichever player's own screen they're set on, solo or multiplayer alike, since nothing about them needs to agree between two players.

---

## Chapter 22 — The Arcade

Since 3.0 the game never shows its main menu. A Harmony postfix on `MainMenu.Start` hides the menu's buttons, covers the screen with the mod's own loading screen (`LoadingCover`: the title, a row of color-cycling blocks, a tip, the Discord and Ko-fi lines), and loads the Arcade instead. If the Arcade ever fails to load, the real menu is shown so the player is never stuck.

### How it's built

The Arcade isn't a scene. `HubMode.Launch` loads Night 6 — the same level Endless uses, so the real player, camera, controls and HUD come for free — and `HubController` waits until the level hands the player control, then builds a room far above the house (room origin `(0, 400, 0)`) and moves the player into it. The level's fog and depth of field are switched off while you're up there, the level's story text is blocked (only the Arcade's own messages get through `HubMode.SetHudText`), and the game's own title card reads "The Arcade". Everything else in the level keeps running underneath, unseen.

Loading Night 6 over and over had one dangerous side effect: the game's `GameManager.PlayLevel()` saves whatever level it loads as the story's "where you left off", so Continue would always have dropped players into Night 6. `StorySave.cs` keeps the story's real save point safe from the mod's own modes.

### Placing things: the mark tool

Nothing in the Arcade was placed by guessing coordinates. `HubMarkTool` (dev builds only, E) lets the designer walk to a spot, name it, and log its room-local position and facing: `PROP 'name' at local (x, z) facing yaw`. The same tool marked the Summer Vacation beach and the Waygetter factory. `HubLayout.cs` then places every wall, sign and machine from those marks. It is the Endless `DebugLocationMarker` idea (Chapter 8) grown up.

### The machines

Each machine is a cabinet with a lit marquee and a hold-to-use `Interactable`. `HubLayout.Register` maps each one to a `HubMachine`, and `HubLaunch.Use` does the rest:

| Machine | What it does |
|---|---|
| NEW GAME / CONTINUE | The story, exactly like the old menu's buttons |
| ENDLESS | Setup screen (difficulty, run modifiers, gadget), then the run |
| PAPA'S 10TH BIRTHDAY! | Setup screen, then the party (Chapter 27) |
| VERSUS | The Versus seats screen (Chapter 25) |
| SUMMER VACATION | The beach (Chapter 29) |
| ENDLESS SHOP / PAPA SHOP / VERSUS SHOP | The three shops: shards, Party Tokens, Versus Tokens |
| GUMBALLS | A gadget for 150 shards (Chapter 23) |
| OUTFITS | Your kid, outfits, DLC characters, the Character Creator (Chapter 26) |
| KALEIDOSCOPE | The game's own Memories content, unlocked by beating the story |
| LEVEL SELECT | Unlocked by beating the story |
| OPTIONS | Effect sliders, sensitivity, Discord and Ko-fi buttons |
| QUIT | Quits |

Plus the pinball machine (Chapter 30), the air hockey table (Chapter 31), a stats board, a how-to-play board, and a real mirror. The Waygetter Facility (Chapter 33) has no machine: F9 in the Arcade takes you there.

### Screens: OverlayPanel

The Arcade's screens aren't cloned menu widgets (Chapter 20). `OverlayPanel` (in `OptionsMenu.cs`) is a full-screen, mouse-driven panel with its own hit-testing, so it works inside any level without a UI `EventSystem`. While one is open the player is frozen and the cursor is free, and a patch stops the machine behind it from being used. Subclasses just lay out rows (sliders, steppers, toggles) and buttons, and get `OnShown`/`OnHidden` hooks.

**Options** holds four effect sliders (`BlurFilter.cs`): Blur (depth of field), Bloom, Glow (the lens-dirt haze) and Film (grain), each 0–100% where 100% is the game's original look. The tuned defaults are Blur 0%, Bloom 25%, Glow 25%, Film 0%.

### The mirror

A full-length mirror next to OUTFITS really reflects: the room, the other player, and you. You're first person, so your own body only exists for the mirror: `HubMirror` builds your kid in your outfit, walking and jumping when you do, and a second camera sits at your eye's reflection behind the wall and films the room into the glass. Since 3.5 it is also where you "Remember who you are" (Chapter 32).

### The corner

Bottom right, only in the Arcade: `EARLY ACCESS V3.5` or `PUBLIC VERSION 3.4` (`VersionTag.EARLY_ACCESS`, flipped when a version ships), and above it "Join Our Discord Server: discord.gg/PjSQQrhsMx" and the Ko-fi line. After Hours is free and always will be; nothing is locked behind the Ko-fi.

---

## Chapter 23 — Gadgets & the Gumball Machine

A gadget is one extra tool per Endless run, separate from the shop's single-run Power Ups (both work in the same run). You get gadgets from the GUMBALLS machine (150 shards a pull): a reveal card shows the gumball in the gadget's color, what it does, and EQUIP. The equipped gadget is picked on the Endless setup screen, remembered between sessions, and used in the run with the 1 key from a one-slot bar with a visible cooldown.

| Gadget | Effect |
|---|---|
| Plunge-O-Launch | Aim at the floor: a plunger sticks there and yanks you to it. Fast, but the loud squelch tells Mama where you are |
| Yo-Yo | A shorter, quiet Plunge-O-Launch (Mama won't hear it), 2 uses |
| Music Box | A lullaby sends Mama away for a while; afterward you move slower and everything sounds muffled (100 s cooldown) |
| Fortune Cookie | Feeds Tattletail and reads your fortune, which sometimes hints at a hidden gift coin (cooldown: 1.5× how long his hunger lasts) |
| Flash Camera | When Mama is close and in front of you, the flash sends her away; she turns up somewhere else a few seconds later |
| Spare Battery | Fully charges Tattletail on the spot, no charging station needed |
| Toy Parrot | The DLC gadget (rainbow gumball): a toy that squawks a pitched-up imitation of Tattletail crying. Mama goes after the parrot and ignores you completely while it's down (60 s cooldown in Endless, 80 s in Versus) |

Two related safety rules live in `MamaTeleportGrace.cs`: Mama can't touch you for 1.5 seconds after she teleports (she used to land on top of players), and not at all while a Toy Parrot is down.

---

## Chapter 24 — Endless Since v2.6

### Three more objectives

- **Rewind the Tape**: a VHS tape at one of six hand-marked spots; hold to rewind it.
- **Silence the Speaker**: one fixed spot. It was scoped as "Silence the Radio", but the real prop in this scene turned out to be named `Speaker`.
- **Restart the Fuse Box**: a fuse box built on a hand-picked wall (warning triangle, FUSE BOX plate, status light). The box is on the wall in every run; only its hold-to-restart trigger comes and goes with the objective.

**Room hints** ("Look in: Kitchen, Garage") help with the hidden ones. Rooms come from the house's own `HouseLight` components, each tagged with the room it lights: a spot belongs to the nearest light on the same floor. Versus turns the hints off (you have to search).

### Events and nights

- **Chaos Round**: 15% chance each time an objective starts. Shards for it are doubled.
- **Timed events** (currently Gift from Mama): a 10% roll every 20 seconds.
- **The title card rolls the night** (`EndlessTitle.cs`). 10% of runs are a **Lucky Night** (every shard payout doubled). Otherwise it reads "Endless Night", or "Kaleidoscope Night" with both Kaleidoscope skins on, and a 1% flip swaps the two, skins included.
- **Power Ups**, the seventh run modifier: each new objective has a 20% chance to spawn a spinning coin at a hand-marked spot, holding a random ability you only see when you get close.
- **Golden Flashlight** is a shop passive: it applies exactly what the game's own golden flashlight does (its public `special*` values) without touching the story save's `hasSpecialFlashlight`.

### Kid voices

Since 3.3 your kid (Chapter 26) is heard in Endless and Papa's party too: a hup when you jump, an oof when you land, a scream, gasp or whimper when you see Mama, and a scream when she gets you. It's only sound: your kid's voice never draws Mama.

### The True Ending easter egg

A hidden seven-step sequence at hand-marked spots: rewind two tapes, say hi to two of Tattletail's friends and to a Diamond Tattletail (each answers with the game's real "go away, Mama" line), and the chain-link gate past the garden disappears. Behind it a portal opens; walking into it ends the run.

The game has a real `KaleidoscopePortal` prop, but only inside the `house_kaleidoscope` scene, and loading an extra 8 MB level just to copy it caused a visible hitch (`KaleidoscopePortalScavenger.cs` keeps both ways of getting it, neither automatic). So the portal is built in code (`PortalFx.cs`, 3.5): two spiral layers turning against each other around a bright core, a glowing rim with the kaleidoscope's rainbow running round it, sparks pulled in on a tightening spiral, and a flickering purple light; it pops open when the gate goes. It's drawn with the game's particle shaders: `UI/Default`, which works for the pinball art in the Arcade, drew nothing out there.

---

## Chapter 25 — Versus

A 1v1 online match on top of the Steam lobby (Chapter 21): one player is the **child** (a normal Endless run, their tasks and all), the other is the **hunter**, Mama or (from 3.2) Papa, who's free too.

### Seats and sync

The VERSUS machine shows two seats shared with the friend in your lobby (`VersusLobby`). The host owns the seats: the guest asks (`VersusClaim`), the host decides and tells everyone (`VersusState`). An empty seat offers INVITE PLAYER. An invite lands you in the Arcade, and you see each other there before the match.

`VersusNet` sends **poses** about ten times a second, unreliable (where each player is and which way they face, in the Arcade or in the match), and **events** reliably (the child's noises, catches, voice lines, ability use). `VersusAvatars` draws the other player from the poses.

### The hunter

The hunter can't walk. They see the house in third person from one of Mama's real spawn spots (the level's patrol nodes), and their tools are:

1. **Map**: a top-down view of the floor with a dot on every spawn spot; click one to teleport. Noises ping on the map.
2. **Kill Light**: kill the child's flashlight.
3. **Mama's Gaze** (Mama): within 5 m with the child in sight, they freeze in fear for 2 seconds, shaking, and the trembling makes noise (30 s cooldown). **Papa's Absence** (Papa): for 30 seconds the child can't see him (60 s cooldown after).
4. **Kill meter**: every noise the child makes fills it. At 4 the hunter can catch them. It rises at most once every 1.5 s, and every 5 s of quiet takes one away. Loud noises count once. Tattletail's talking counts once per line (it used to count every frame of a line: "instant deaths"), and his "Mama's coming!" warning doesn't count.

A catch plays Mama's kill in first person on the child's screen (the lunge, the scream, claw slashes, the kid falling back), then both land back in the Arcade. The lights go out while the hunter hunts and come back for rests and the phone, and a minimap shows the child where they are.

### Tokens and the Versus Shop

Versus pays **Versus Tokens** instead of shards: 1000 to the hunter for a catch, 200 to the child per objective. They buy Mama skins (Kaleidoscope, Golden, Shadow, Crimson), Papa skins (Golden, Midnight, Crimson, Rainbow), Tattletail colors, beams and the Kaleidoscope Voice for your Versus look. Hovering a skin spins the model right there (`PreviewStage`: a model far away on its own layer, its own lights and camera, rendered into the panel).

---

## Chapter 26 — The Kids

### The model

The child in Versus, the body in the mirror and the kid on the OUTFITS screen are the same thing: a sculpted, skinned kid about 1.1 m tall. There were no kid assets in the game, so they're built offline by `tools/kid_model.py`: the body is described as smooth blended shapes (SDFs), meshed with marching cubes, weighted to a small skeleton, and animated (idle, walk, run, scared). It is written to a compact `KID2` `.bin` (bones, vertices, one submesh per material, the clips) that's embedded in the DLL. Every model stays under Unity's 65k-vertex mesh limit.

### Characters and outfits

`KidGender` is the character: Boy, Girl, Custom, and the DLC characters **Dez** (3.2), **Kaz** (3.2) and **the Rat King** (3.4), all free. DLC never costs real money: it's a mod. Each character has five outfits: the first is free, the rest are 100 shards each. The Rat King's are crowns (Gold, Silver, Ruby, Diamond, Kaleidoscope). Your look is sent to your friend as a single number (`gender × 100 + outfit`), so they see exactly who you picked.

### The Character Creator

"Kid: Custom" opens EDIT CHARACTER (`CreatorMenu`, 3.2): body frame, pants or skirt, skin tone, head shape; hair style and color; eyes, eye color, brows, nose, mouth; sliders for spacing and sizes; extras (a default expression, glasses, freckles, a band-aid, eye circles, Tattletail-colored accents). It's built from a kit of pre-made pieces (`tools/creator_kit.py`) snapped onto the kid. When Mama catches you or her Gaze freezes you, your face goes terrified.

### Voices

`KidVoice.cs` synthesizes every sound (no recordings): a buzzing "throat" at the voice's pitch, shaped by three mouth resonances (formants) into vowels, plus breath noise. Each sound is a handful of keyframes, so there are many of them (screams, shrieks, gasps, panting, "no no no"). Boy, Girl, Dez and Kaz have their own voices; a custom kid picks one of five voice types and a pitch.

---

## Chapter 27 — Papa's 10th Birthday!

A party instead of a hunt. Papa's Hunger runs from full to empty in 3:00, the party's Entertainment in 2:30. The party only ends when Hunger hits 0; Entertainment at 0 doesn't end it, but Mama comes hunting until it recovers.

- **Feed Papa**: every food is made from exactly two ingredients, in either order (`PartyGame.cs`). Ingredients appear at 18 hand-marked spots (a random one every 30 s, if the spot is empty), four permanent food machines (popcorn, hot dogs, nachos, waffles and crêpes) each already hold their main ingredient, and a recipe card hangs on the kitchen wall.
- **Keep the party fun**: seven party Tattletails in random colors each host one of three minigames (Face Paint, Rock-Paper-Scissors, Balloon; a win is worth the same as a piñata), piñatas appear around the house, and there's a bouncy castle you can carry a Tattletail onto.
- **Party Tokens**: 100 for every full minute the party goes on, spent at the PAPA SHOP.
- **The music** is an original party loop synthesized in code (bass, chords, lead, drums), so there's nothing copyrighted to ship. The children's cheer and boo on minigames are WAVs next to the DLL.

The task line on the HUD is permanent in this mode (`BirthdayTask.cs`): anything that tries to write it gets the party's own text instead.

---

## Chapter 28 — Mama's Scrapbook

A purely cosmetic sticker collection. Six stickers match a real Tattletail color and unlock the moment you buy that color. The rest come from pulls at 1000 shards each: that price is intentional endgame content for players who've bought everything else. The art is plain PNGs next to the DLL (`StickerImages.cs`).

---

## Chapter 29 — Summer Vacation (3.3)

Mama's smoothie bar on a sunny beach, a Papa's Freezeria-style job, single-player.

The beach is built like the Arcade: Night 6 loads, then the beach is built far from the house (at y 300) with a real sky and sun, sand, dunes with palm trees and a sea you can't enter. It was laid out from marks placed by both designers with the E tool.

**The job** (`BeachShift.cs`): customers (party Tattletails) walk in from the dunes, line up, order at the counter and wait on a stool. You pick the cup size, add the fruit, blend and stop the blender in the right zone (it keeps going if you walk away), add toppings, and serve before their patience runs out. Every order gets a receipt with its score (order, blend, wait) and a tip, and a grade stamp slaps onto it.

- **Days** add a fruit and a topping each (10 of each), and orders grow: ice, mixes, textures, more toppings.
- **Customers** have names and moods (nice, impatient, aggressive, judgy). A new **regular** joins every day, up to five, and always orders the same. **The Tattletail Inspector** visits every third day: an insane order, harsh judging, triple pay.
- **Mama judges your day**. 80% or better: she's proud, and tips you. 50% or better: you stay. Under 50%: she jumpscares you, and you do the day again.
- **Pay**: shards for every day, and a $100 weekly wage every 7th day. Tips buy **Mama's upgrades** between days (tip jar, blender alarm, cushions, peanuts, turbo blenders, up to four blenders, fancy cups, decor you place yourself). The day, the register and the upgrades are kept in `Shop.txt`.
- **A tutorial** the first time: a tour of the bar, then a practice order with an arrow showing what to do.
- **Music**: a steel-drum jingle when a day starts, a calypso loop while the bar is open, a low drone while Mama judges; all synthesized (`BeachMusic.cs`).

---

## Chapter 30 — Tattletail Pinball (3.4)

A real pinball machine in the Arcade's hall, played in first person: hold to play and your view drops onto the playfield. Left click (or A) and right click (or D) are the flippers, hold SPACE to pull the plunger, Q or E steps away.

**Physics are 2D** (`PinballTable.cs`): the table is flat maths in meters (walls, slingshots, star targets, three pop bumpers, the House's door, the ramp lane, the shooter lane, two flippers) stepped in 1 ms substeps, and the 3D machine just draws it. The layout: the **House** eats the ball through its door for big points; three **Tattletail bumpers** (the real model, recolored) hop and light up; the **ramp** sends the ball over the top and back, and ramps in a row combo; four **star targets** raise the multiplier; the longer a ball lives, the higher the multiplier (up to ×8). Three balls a game, with the high score saved on the backbox's red LED display. The art is embedded JPGs from `tools/pinball_art.py`.

**How it was debugged.** The table class compiles on its own against small UnityEngine stubs, so it runs in a headless harness outside the game: thousands of simulated balls, logging where they escape or get stuck. That caught a sealed inlane, ramp false triggers, pocket traps, a flat gate, a ball leaking out at the bottom left, the House letting fast balls pass through, and the plunger springing back through the ball on launch ("ball lost").

---

## Chapter 31 — Tattletail Air Hockey (3.4)

A 1v1 table by the hall's south wall, first to 7. Hold at either end to play: your view drops behind your mallet and the mouse (or WASD) moves it. SPACE for a rematch, Q or E to leave.

**Physics** (`AirHockeyTable.cs`) are 2D like pinball: the puck glides on air, the rails are convex with their normals pointing to the middle (so the puck can't slip out at a joint), the corners are cut so it never sits in one, the goal mouths are open, walls are resolved last (so a mallet can't push the puck through one), and the puck is damped and capped (5 m/s) so energy can't run away.

**Alone**, a Tattletail plays the other end. It guards its goal, lines up strikes and banks shots off the rails, and gets a little sharper when you're winning. **With a friend** in your lobby you sit at opposite ends: the host runs the puck and the guest sends its mallet (`PacketId.AirHockey = 9`). If one of you walks away, the Tattletail takes over. The scoreboard on the arch reads from both ends.

---

## Chapter 32 — The Awakening (3.5, Sa1monfr's idea)

The story: your consciousness is trapped in the Arcade. The first time a player ever arrives, they wake up as a **blank wooden mannequin**: black, a glitch, then you look down as the body forms out of drifting wood chips, your hands come up in front of you and turn over, you look around the room, and it's yours. "...where am I?", then "Find a mirror."

You stay a mannequin — in the mirror, and to your Versus friend — until you look in the mirror and choose **"Remember who you are"**. That opens OUTFITS; closing it gives you your look back ("...right. That's me."). It happens once per player, ever (saved as `KE_Awakened_v2` / `KE_Remembered_v2` in PlayerPrefs; the "_v2" gave early testers who saw a black version the wooden one once). Every other return to the Arcade puts you right where you were standing, facing the same way, with no cutscene.

How it's built (`Awakening.cs`):

- **The body** is the Boy kid, posed by hand: arms raised and twisted in `LateUpdate`, the head shrunk away (you're looking out of it), scaled so its eyes are where yours are. The camera takes over (the FPS controller is off) and sits at the face's front.
- **The wood**: kid meshes have no UVs, so the mannequin gets a copy of the mesh mapped from the rest pose, so the grain runs up the body and limbs, and a procedural tiling beech-grain texture.
- **The title card**: the game shows "The Arcade" over the start, so the awakening pauses under it, and the card's own opening eyes open straight onto the body forming.
- **A lesson**: the game draws its whole HUD, title card included, on a plane 2 cm in front of the camera. Pulling the near clip plane out to 2 cm silently erased the HUD; it stays at 1 cm.

---

## Chapter 33 — The Waygetter Facility (free roam)

A one-long-night story mode in the abandoned Waygetter Electronics factory was built during 3.4 and scrapped on 2026-09-28: the story wasn't up to the standard of the rest of the mod. The building stayed (`Factory*.cs`: three floors, decay, care stations, a found-footage camcorder with a real battery and a working flip-out screen), and since 3.5 you can explore it: **F9 in the Arcade** takes you there. There's no machine for it and no story: you arrive by the car with the camcorder, Tattletail waits in the open trunk, and Mama isn't there. The story code (`FactoryStory.cs`) was deleted and can be recovered from git.

---

## Chapter 34 — Presentation & Trailers

Every major update gets a trailer, a teaser, a YouTube description and a Discord post (under 2000 characters). Public write-ups credit "Made By: ImPoPzzZ & Sa1monfr" and link the Discord (discord.gg/PjSQQrhsMx) and the Ko-fi.

**Trailers are recorded in the game.** `Trailer.cs` (`TrailerDirector`, dev builds only, F9 in the Arcade) takes over the camera — so the game's own post effects stay — hides every HUD, and plays a scripted sequence with its own synthesized music, title cards and captions. Machines expose hooks for it (pinball plays itself through staged moments; air hockey plays a scripted rally and goal). The recorded take is cut with `tools/trailer/cut_trailer.py` (ffmpeg, loudness-normalized) into the trailer and the teaser. `TrailerDirector.Enabled` is false in every release.

**A lesson from the 3.4 trailer**: `CartoonText` copies every letter about a hundred times (outline and 3D sides), and long text blows Unity's 65k-vertex UI mesh limit and silently draws nothing. Keep trailer text short.

---

## Chapter 35 — Building & Shipping

### Old Mono rules

*Tattletail* runs Unity 5.5 on an old Mono runtime, and some perfectly valid C# kills the plugin there:

- No `System.Func`/`System.Action` and no LINQ (the mod has its own delegates; see `CompatShims.cs`).
- No coroutines inside Harmony patch classes: `PatchAll` reads every method's attributes, and the compiler's `IteratorStateMachineAttribute` doesn't exist here.
- No call to a `params` method with zero arguments (the compiler emits `Array.Empty`, which doesn't exist here).

The built DLL is scanned for these before every release.

### What's in the game's build

Only shaders the game ships can be found at runtime. `Unlit/Texture` is **not** there (asking for it once stopped the Arcade loading). `Unlit/Color`, `Standard`, `Particles/Alpha Blended` and `UI/Default` are. For unlit textured art in the Arcade, `UI/Default` on a custom quad (white vertex colors, `unity_GUIZTestMode` set to LessEqual) works. Out in the house level the particle shaders are the safe choice (Chapter 24).

### Releases

- **Dev tools** (the E marker tools, trailer mode) are off unless `BepInEx\config\com.kaleidoscope.endless.cfg` turns them on. Release zips never ship that file.
- **Every release** ships as a full zip (BepInEx included, ready to drop in), an itch.io zip, and a one-click installer (`Install Tattletail After Hours.exe`, `tools/installer/make_installer.py`), scanned with Windows Defender and tested before upload.
- **`VersionTag.EARLY_ACCESS`** is true while a version is in development and flipped to false when it ships. The release is tagged in git (`v3.4` etc.).
- **Changelogs are for players**: plain language, shipped features only.


---

## Appendix A — Constants & Numbers Reference

| System | Constant | Value |
|---|---|---|
| Objective loop | Rest phase duration | 30s |
| Objective loop | Close-call limit (Flashlight Survival) | 3 flashlight kills |
| Objective loop | Flashlight Survival timer options | 30 / 60 / 90s, then ÷ √difficulty |
| Exfil phone | Ring duration | 60s |
| Exfil phone | Trigger | Every 3 completed objectives |
| Exfil phone | Answer bonus | 25% of the run's shards |
| Difficulty | Easy / Normal / Hard / Impossible | 0.7× / 1.0× / 1.4× / 2.0× |
| Mama | "No More Mama!" aggression multiplier | 10× (fixed, independent of main difficulty) |
| Run Modifiers | Need-drain speed-up (3 toggles) | 1.5× (50% faster) each |
| Shards | Per objective | 10, +10 per active modifier (not Power Ups), × difficulty (0.5 / 1 / 1.5 / 2) |
| Shards | Chaos Round / Lucky Night | ×2 each |
| Endless events | Chaos Round / timed event roll | 15% per objective / 10% every 20 s |
| Endless title | Lucky Night chance | 10% per run |
| Shop | Outfit price | 100 shards |
| Gumball Machine | Price | 150 shards |
| Mama's Scrapbook | Sticker pull | 1000 shards (intentional endgame) |
| Papa's party | Party Tokens | 100 per full minute survived |
| Papa's party | Hunger / Entertainment full to empty | 3:00 / 2:30 |
| Gold / Diamond (v2.6 only) | Survival time required | 600 s / 1200 s (replaced by the shop) |
| Diamond Flashlight | Charge-time multiplier | 1.6× the real "golden" flashlight's own 1.25× boost |
| Diamond Flashlight | Beam / glow brightness multipliers | 1.3× / 1.9× off the real live baseline |
| Objectives | Vase / Candle / Friend spot pool | 6 hand-marked spots, 3 (or 1, for Friend) chosen per attempt |
| Objectives | Active rotation | 8 (the retired Silence the Noise stays out) |
| Charging station | Hold time | 5s |
| Vase / Candle / Friend / Phone | Hold time | 0.6s / 0.6s / 1.2s / 0.8s |

## Appendix B — File Map

All source is in `src/`. Tools that build assets offline are in `tools/`.

| Area | Files | Role |
|---|---|---|
| Plugin | `KaleidoscopeEndlessPlugin.cs`, `CompatShims.cs`, `SaveFiles.cs`, `StorySave.cs` | Entry point (guarded `PatchAll()`, the DevTools config), old-Mono shims, the save folder, keeping the story's save point safe |
| Level hooks | `SceneBootstrap.cs`, `GamePatches.cs`, `WorldRays.cs` | `SceneMonitor` (level loads, run start), gameplay Harmony patches, raycasts that ignore the player's own body |
| Endless core | `EndlessRunManager.cs`, `EndlessEvents.cs`, `EndlessTitle.cs`, `EndlessHud.cs`, `EndlessStats.cs`, `MamaController.cs`, `MamaTeleportGrace.cs`, `RunModifiers.cs`, `RoomHints.cs` | The run state machine, events, the night's title, the timer, stats, Mama, the modifiers, room hints |
| Objectives | `WashingMachineObjective.cs`, `TattletailFriendObjective.cs`, `VaseObjective.cs`, `CandleObjective.cs`, `TapeObjective.cs`, `SpeakerObjective.cs`, `FuseBoxObjective.cs`, `NoiseObjective.cs` (retired), `PhoneObjective.cs`, `PowerUpObjective.cs`, `ChargingStation.cs` | The eight objectives, the phone, Power Up coins, the charging station |
| Easter egg | `TrueEndingEasterEgg.cs`, `PortalFx.cs`, `KaleidoscopePortalScavenger.cs` | The True Ending sequence and its portal |
| Economy | `Shop.cs`, `Shards.cs`, `ShopPanel.cs`, `ShopSubmenu.cs`, `PreviewBaker.cs`, `PreviewStage.cs`, `Scrapbook.cs`, `ScrapbookSubmenu.cs`, `StickerImages.cs` | Shards and tokens, the three shops, preview renders, Mama's Scrapbook |
| Cosmetics | `GoldRecolor.cs`, `DiamondRecolor.cs`, `FurShine.cs`, `FlashlightBeam.cs`, `DiamondFlashlight.cs`, `GoldenFlashlight.cs` | Tattletail colors, beams, flashlights |
| Gadgets | `Gadgets.cs`, `GadgetController.cs`, `GadgetSubmenu.cs`, `GumballReveal.cs`, `ToyParrot.cs` | The seven gadgets, the in-run bar, the gumball reveal card |
| The Arcade | `Hub.cs`, `HubLayout.cs`, `HubMarkTool.cs`, `HubMirror.cs`, `OptionsMenu.cs`, `BlurFilter.cs`, `LoadingCover.cs`, `VersionTag.cs` | Building the room, the machines, the mark tool, the mirror, `OverlayPanel` and Options, effect sliders, the loading screen, the corner tag and Discord line |
| The Awakening | `Awakening.cs` | Identity (the mannequin), the one-time awakening |
| Arcade games | `Pinball.cs`, `PinballTable.cs`, `AirHockey.cs`, `AirHockeyTable.cs` | The two machines (3D) and their 2D physics |
| Kids | `ChildModel.cs`, `CharacterKit.cs`, `CreatorMenu.cs`, `OutfitMenu.cs`, `KidVoice.cs` | The kid model and outfits, the Character Creator, OUTFITS, the synthesized voices |
| Versus | `VersusMode.cs`, `VersusMenu.cs`, `VersusLobby.cs`, `VersusNet.cs`, `VersusMama.cs` | The match, the seats, the sync, the hunter's side |
| Online | `MpPacket.cs`, `MpNetworking.cs`, `MpPacketHandlers.cs`, `MpLobby.cs`, `MpLobbySettingsSync.cs` | Steam P2P transport and the lobby |
| Papa's 10th Birthday! | `PartyGame.cs`, `PartyWorld.cs`, `PartyMachines.cs`, `PartyFoods.cs`, `PartyTattletails.cs`, `PartyTattletailGames.cs`, `PartyCastle.cs`, `PartySoundEffects.cs`, `BirthdayDecor.cs`, `BirthdayMusic.cs`, `BirthdayTask.cs`, `BirthdaySubmenu.cs`, `RecipeCard.cs` | The party mode |
| Summer Vacation | `Beach.cs`, `BeachLayout.cs`, `BeachShift.cs`, `BeachUpgrades.cs`, `BeachMusic.cs` | The beach, the smoothie bar job, upgrades, music |
| Waygetter Facility | `Factory.cs`, `FactoryLayout.cs`, `FactoryFloors.cs`, `FactoryDecay.cs`, `FactoryStations.cs`, `FactoryCamcorder.cs`, `FactoryCamcorderBattery.cs` | The factory, free roam (F9 in the Arcade) |
| Trailers | `Trailer.cs` | `TrailerDirector` (dev builds, F9) |
| Legacy menus | `MainMenuButton.cs`, `CustomizationSubmenu.cs`, `RunModifiersSubmenu.cs`, `RunCustomizationSubmenu.cs`, `MultiplayerSubmenu.cs`, `StatsSubmenu.cs` | The v2.6 main-menu tree (Chapter 20) |
| Dev diagnostics | `DebugLocationMarker.cs`, `Night2AudioScavenger.cs` | The Endless E-marker, the washer-sound scavenger |
| Tools | `tools/kid_model.py`, `tools/creator_kit.py`, `tools/pinball_art.py`, `tools/airhockey_art.py`, `tools/factory_*.py`, `tools/trailer/`, `tools/installer/` | Kid models, creator pieces, machine art, factory textures, the trailer cut, the installer |

---

## Appendix C — Version History

| Version | Name | Highlights |
|---|---|---|
| v2.6 | Kaleidoscope Endless | Endless: the objective loop, Mama, the phone, the shard shop, Steam lobby step 1 (this document's Chapters 1–21) |
| 3.0 | The Arcade | The Arcade replaces the main menu; gadgets and the Gumball Machine; Papa's 10th Birthday!; saves move to Documents |
| 3.1 | After Hours | Rebrand to Tattletail: After Hours; Versus (Mama vs the child, online); the kid model; OUTFITS |
| 3.2 | The Character Update | Papa joins Versus; the Versus Shop and tokens; Mama's Gaze and Papa's Absence; the Character Creator; kid voices; Dez and Kaz; Versus balance |
| 3.2.1 | | The Arcade mirror; kids jump; the Ko-fi |
| 3.3 | The Summer Update | Summer Vacation; kid voices in Endless; real fur for Gold and Diamond |
| 3.4 | The Arcade Update | Tattletail Pinball; Tattletail Air Hockey; the Rat King; Discord links |
| 3.5 (Early Access) | | The Awakening and the wooden mannequin; the easter egg's real portal |


---

*Tattletail: After Hours — design document, current as of 3.5 Early Access (2026-09-29). Made By: ImPoPzzZ & Sa1monfr.*
