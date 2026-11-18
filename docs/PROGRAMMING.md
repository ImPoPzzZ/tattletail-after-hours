# Tattletail: After Hours — Programming Guide

> **Newer and bigger:** `docs/AFTER_HOURS_3.6_COMPLETE_REFERENCE.md` covers everything as of 3.6 (every game, system, 3D model, sound, tool and number). This document is kept for its history and its detail.


How the mod is built: every system, where its code lives, and how it works, with real excerpts from the source. (For what the mod *does* as a player, see the changelogs; this document is only about the code.)

- **Game:** Tattletail (Waygetter Electronics, 2016), Unity **5.5**, Mono runtime (pre-.NET 4.5).
- **Mod stack:** BepInEx 5 (loader) + HarmonyX (runtime patching), C# compiled against the game's own `Assembly-CSharp.dll` / `UnityEngine.dll`.
- **Size:** ~26,000 lines of C# in 93 files (`src/`), ~1,300 lines of Python tools (`tools/`), generated binary assets in `assets/`.
- **Internal name:** everything is still namespaced `KaleidoscopeEndless` (plugin GUID `com.kaleidoscope.endless`, `KaleidoscopeEndless.dll`): renaming them would leave players with two copies of the mod after an update. The player-facing name is "Tattletail: After Hours".

---

## Contents
1. [Build, deploy, release](#1-build-deploy-release)
2. [Surviving the old Mono runtime](#2-surviving-the-old-mono-runtime)
3. [Boot sequence](#3-boot-sequence)
4. [Harmony patches: bending the vanilla game](#4-harmony-patches-bending-the-vanilla-game)
5. [Building things from scratch at runtime](#5-building-things-from-scratch-at-runtime)
6. [The Arcade (hub)](#6-the-arcade-hub)
7. [Endless mode](#7-endless-mode)
8. [Economy, saves and shop previews](#8-economy-saves-and-shop-previews)
9. [Tattletail's colors, real fur and sparkles](#9-tattletails-colors-real-fur-and-sparkles)
10. [Papa's 10th Birthday!](#10-papas-10th-birthday)
11. [Online: Steam lobby and Versus netcode](#11-online-steam-lobby-and-versus-netcode)
12. [Versus: the hunter](#12-versus-the-hunter)
13. [The kids: models, animation, outfits, the Character Creator](#13-the-kids-models-animation-outfits-the-character-creator)
14. [Sound synthesis: voices and music without audio files](#14-sound-synthesis-voices-and-music-without-audio-files)
15. [Rendering tricks: preview stages and the mirror](#15-rendering-tricks-preview-stages-and-the-mirror)
16. [Python asset pipeline](#16-python-asset-pipeline)
17. [Utilities](#17-utilities)
18. [Lessons learned (the rules the codebase follows)](#18-lessons-learned)

---

## 1. Build, deploy, release

`KaleidoscopeEndless.csproj` targets net46 and references the game's managed DLLs by path. A post-build target copies the DLL straight into the game:

```xml
<Target Name="CopyToPlugins" AfterTargets="Build">
  <Copy ... DestinationFolder="$(GameRoot)\BepInEx\plugins\KaleidoscopeEndless" />
</Target>
```

So `dotnet build -c Debug` = compile + install. Binary assets (the kid models, the Character Creator kit) are **embedded resources** in the DLL:

```xml
<EmbeddedResource Include="assets\kid_boy.bin" LogicalName="kid_boy.bin" />
<EmbeddedResource Include="assets\creator_kit.bin" LogicalName="creator_kit.bin" />
```

**Release checklist** (every zip): `VersionTag.EARLY_ACCESS = false` for public builds and the version matching `[BepInPlugin]`; all `Debug*` flags off; no dev config (`com.kaleidoscope.endless.cfg`, which holds `DevTools = true`), no saves or logs in the zip; the DLL scanned for forbidden types (section 2); zips written with Python's `zipfile` (forward slashes). Versions are git tags (`v3.1`, `v3.2`, `v3.2.1`, ...).

---

## 2. Surviving the old Mono runtime

Tattletail's Mono predates .NET 4.5. Code that *compiles* fine can still crash at load time. The rules:

- **No `System.Func` / `System.Action` / LINQ.** They're .NET 3.5+/4 types the runtime may not resolve; one reference can make a whole type fail to load. Use custom delegates instead (`public delegate void ClickHandler();`), or `UnityEngine.Events.UnityAction`.
- **Reflection handles compare as `(object)x != null`.** `FieldInfo != null` compiles to `FieldInfo.op_Inequality`, which only exists in .NET 4.
- **Coroutines need a shim.** The compiler tags every `IEnumerator` method with `IteratorStateMachineAttribute`, missing from old mscorlib. `CompatShims.cs` defines it so the compiler uses the local copy:

```csharp
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    internal sealed class IteratorStateMachineAttribute : Attribute
    {
        public IteratorStateMachineAttribute(Type stateMachineType) { StateMachineType = stateMachineType; }
        public Type StateMachineType { get; private set; }
    }
}
```

- **Only shaders that exist in the build.** `Shader.Find("Unlit/Texture")` returns null in Tattletail's build, and `new Material(null)` threw inside the Arcade's construction (stuck loading screen). Shaders known to exist: `Standard`, `Diffuse`, `Unlit/Color`, `Particles/Alpha Blended`, `Custom/FurShaderV1`, plus the built-in UI/Sprites ones.

Before every zip, the built DLL is disassembled and grepped: `System.Func`/`System.Action` must not appear, and `System.Reflection.*::op_Equality/op_Inequality` must count 0.

---

## 3. Boot sequence

**`KaleidoscopeEndlessPlugin.Awake()`** — everything that could fail is isolated so one broken piece can't take the rest down:

```csharp
try
{
    var harmony = new Harmony("com.kaleidoscope.endless");
    harmony.PatchAll();
}
catch (System.Exception e)
{
    Log.LogError("KE: harmony.PatchAll() failed: " + e.Message + "\n" + e.StackTrace);
}
SceneMonitor.Init();
try { SaveFiles.Init(); } catch (System.Exception e) { ... }   // move old saves first
OptionsMenu.ApplySavedVolume();
try { Shop.Init(); } catch (System.Exception e) { ... }
new GameObject("KE_MpLobby").AddComponent<MpLobby>();          // persistent Steam lobby
```

(An unguarded `PatchAll()` once threw on a single bad patch and silently killed the whole plugin.)

`Update()` ticks the per-frame systems inside one try/catch: `EndlessRunManager.Tick()`, `BlurFilter.Tick()`, `VersusNet.Tick()`, `KidVoice.Tick()`.

**`SceneMonitor` (SceneBootstrap.cs)** decides what a level load means. Every mod mode (the Arcade, Endless, Papa's party, Versus) loads the story's **Night 6** level and repurposes it; a "pending" flag set before `GameManager.PlayLevel` says which:

```csharp
if (VersusMode.PendingMamaLoad) { ... VersusMamaController.Create(); return; }   // hunter side
if (HubMode.Pending) { ... HubMode.Active = true; HubController.Create(); return; } // the Arcade
HubMode.Reset();
SetupMonoBehaviours();   // Endless / party / Versus child: objective controllers etc.
```

`SetupMonoBehaviours()` instantiates the run's controllers (EndlessRunManager, MamaController, one controller per objective, GadgetController, ...) and arms the start trigger: the run starts when the player picks up the flashlight (a postfix on `InteractableActions.PlayActions`), which calls `StartEndless()`.

`StorySave.cs` protects the real story's save: since the mod's modes load Night 6, the game would otherwise record "you reached Night 6".

---

## 4. Harmony patches: bending the vanilla game

`GamePatches.cs` and several feature files patch the game's own classes. Patterns used:

**Prefix that vetoes** (return `false` = skip the original). Mama catching the player in Endless:

```csharp
[HarmonyPatch(typeof(MamaManager), "RequestJumpscareDeath")]
public static class Patch_MamaManager_RequestJumpscareDeath
{
    static bool Prefix()
    {
        if (EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive)
            return true;                                  // story mode: untouched
        if (MamaTeleportGrace.PlayerProtected) return false; // just teleported / parrot down
        if (!EndlessRunManager.Instance.OnMamaCaughtPlayer()) return false; // Totem save etc.
        if (KidVoice.Active) KidVoice.Mine(KidVoice.Kind.Caught);          // your kid screams
        return true;
    }
}
```

**Postfix that listens.** Every noise the game reports feeds Versus:

```csharp
[HarmonyPatch(typeof(MamaManager), "OnNoise")]
public static class Patch_MamaManager_OnNoise_Versus
{
    static void Postfix(Vector3 noisePosition, bool bigNoise) { VersusNet.OnLocalNoise(noisePosition, bigNoise); }
}
```

**Private members via Harmony's injection** (`___fieldName`). The kid's landing voice only after a jump:

```csharp
[HarmonyPatch(typeof(FirstPersonController), "PlayLandingSound")]
public static class Patch_FPS_PlayLandingSound_Voice
{
    static void Postfix(FirstPersonController __instance, bool ___m_Jumping) { ... }
}
```

Other patched areas: the vanilla quest system is frozen during Endless (`QuestOrder.ActivateNextQuest` and friends return false), `OnJumpscareFinished` ends the run instead of the story's game over, `Interactable.OnInteractComplete` routes the mod's own interactables (hub machines, party pickups), `Interactable.OnInteract` is blocked while a menu is open, audio lines like Tattletail's wake-up line are muted in Endless.

Private game state is read with cached reflection handles (`FieldInfo` looked up once), e.g. `MamaController` reads `MamaManager`'s state and current Mama object for the Flash Camera and the kid's reactions.

---

## 5. Building things from scratch at runtime

The mod ships **no Unity scenes or prefabs**. Everything is created in code:

- **Interactables** (anything you hold left click on) are built from a bare `Interactable` component rather than reusing the level's props, whose vanilla quest wiring proved unreliable:

```csharp
public static Interactable MakeInteractable(GameObject go, string actionText, float holdTime)
{
    // Copy the interaction layer from a confirmed-clickable Interactable rather than guessing.
    ...
    var it = go.AddComponent<Interactable>();
    it.actionText = actionText;
    it.holdTime = holdTime;
    it.highlight = true;
    it.setInactiveOnInteract = false;
    it.newStateOnInteract = Interactable.State.Enabled;
    it.currentState = Interactable.State.Enabled;
    it.onInteractEvent = new UnityEngine.Events.UnityEvent();
    it.conditions = new InteractableConditions();
    it.actions = new InteractableActions { interactActions = new InteractableActions.Actions[0] };
    return it;
}
```

- **Geometry**: arcade machines, walls, signs, party foods, the bouncy castle, the fuse box are primitives (`Mat.Box`) with generated materials (`Mat.Lit(color, gloss)` = Standard shader).
- **Placement** comes from the developer's in-game marks: a dev-only tool (`HubMarkTool` in the Arcade, `DebugLocationMarker` in runs, E key, enabled by the `DevTools` config) logs exact positions, which are pasted into tables like `FriendPositions`, `Walls`, `Machines`.
- **World-space text** uses tiny world-space UI canvases instead of `TextMesh` (whose font shader draws through walls).
- **Models of Tattletail/Mama** are clones of the level's real ones (`Object.Instantiate`), with their interaction components stripped.

---

## 6. The Arcade (hub)

`Hub.cs` (`HubController`, `HubMode`): loads Night 6, builds a room above the house, puts the player in it and hides the level's story UI. `HubLayout.cs` builds the contents from tables:

```csharp
new MachineDef { id = HubMachine.Versus, title = "VERSUS", blurb = "Mama vs the child\n(1v1)",
                 pos = new Vector3(-11.61f, 0f, 6.39f), yaw = 90f, color = new Color(0.9f, 0.15f, 0.35f) },
```

Each machine is a cabinet + lit marquee + an `Interactable`; completing the hold calls `HubLaunch.Use(machine)` which opens a screen or starts a mode.

**Menus** are `OverlayPanel` subclasses (`OptionsMenu.cs`): full-screen Canvas, custom mouse hit-testing (no EventSystem needed), rows of sliders/steppers/toggles/buttons, freezing the player while open:

```csharp
public void Show()
{
    if (Visible) return;
    if (_panel == null) BuildUI();   // built on first open, never in Awake (building a Canvas during object creation crashed Unity 5.5)
    _panel.SetActive(true);
    _openCount++;
    if (RM.fpsController != null) RM.fpsController.enabled = false;
    GS.acceptInput = false;
    OnShown();
    RefreshAll();
}
```

`OverlayPanel.BlocksWorld` (a menu is open, or closed less than 0.3 s ago) stops the click that closes a menu from re-triggering the machine behind it.

Screens: `EndlessSetupMenu`, `PapaSetupMenu`, `OptionsMenu`, `ShopPanel` (three currencies), `OutfitMenu`, `CreatorMenu`, `VersusMenu`, `LevelSelectMenu`. Also: `LoadingCover` (the fake loading screen covering level loads), `VersionTag` (EARLY ACCESS / PUBLIC corner label + the Ko-fi line), `StatsBoard`, `HowToPlayBoard`, `HubMirror` (section 15).

---

## 7. Endless mode

### The run loop — `EndlessRunManager.cs`
Owns the run: time, objectives completed, rest phases, the phone, difficulty, payouts. Objectives come from a **shuffled bag** (every type once per refill, no back-to-back repeat across refills):

```csharp
if (_objectiveQueue.Count == 0)
{
    _objectiveQueue.AddRange(Sequence);
    for (int i = _objectiveQueue.Count - 1; i > 0; i--)       // Fisher-Yates
    {
        int j = Random.Range(0, i + 1);
        var tmp = _objectiveQueue[i]; _objectiveQueue[i] = _objectiveQueue[j]; _objectiveQueue[j] = tmp;
    }
    if (_objectiveQueue[0] == _lastDequeuedType && _objectiveQueue.Count > 1)
    { var tmp = _objectiveQueue[0]; _objectiveQueue[0] = _objectiveQueue[1]; _objectiveQueue[1] = tmp; }
}
```

After each objective: a rest phase (lights on, Mama gone), and every `LOOP_LENGTH` objectives the **phone** rings instead (`PhoneObjective`: answer to escape with a bonus, or let it ring). Versus uses the same manager with `VERSUS_OBJECTIVES_BEFORE_EXFIL = 10`.

### Objectives — one controller each
`WashingMachineObjective`, `NoiseObjective`, `TattletailFriendObjective`, `VaseObjective`, `CandleObjective`, `TapeObjective`, `SpeakerObjective`, `FuseBoxObjective`, plus flashlight survival inside the manager. Common shape: a singleton MonoBehaviour created at level setup, `StartObjective()` spawns its props at hand-marked spots with fresh `Interactable`s, completion calls back into the manager. Some reuse real game components found by decompiling (candles use the game's `Candle.SetCandle(bool)`). Sounds that aren't in memory at runtime are scavenged: `Night2AudioScavenger` additively loads another night's scene once to grab the real washing-machine clip.

`RoomHints.cs` names the room of a hidden objective from the house's own `HouseLight` components (nearest light on the same floor), Endless only.

### Mama — `MamaController.cs`
A wrapper around the game's `MamaManager`: activates her per objective (skipping `MamaSpawner`, which fought the mod's lights), scales her alert timer and patrol speed with difficulty, turns the lights out while she hunts, and exposes helpers for gadgets (`GetActiveMama()`, `LureTo(pos)`). `MamaTeleportGrace` blocks catches right after she teleports or while a Toy Parrot is down.

### Run flavour
- `EndlessTitle`: the title card roll (10% Lucky Night = double shards; 1% Kaleidoscope Night flip).
- `EndlessEvents`: random events (Chaos Round etc.).
- `RunModifiers` + `RunModifiersSubmenu`: 7 stackable modifiers; need-drain modifiers work by scaling `TattletailNeeds`' private decrease-per-second fields.
- `PowerUpObjective`: coins (Max Stats, Strong Battery, Totem of Undying).
- `Gadgets` + `GadgetController`: one gadget per run on key 1. E.g. the Flash Camera only works if Mama is within range, roughly on screen, and nothing but Mama (or the player/Tattletail) is on the line to her chest (`Physics.RaycastAll` filtered by `IsChildOf`); the Music Box sets `MamaController.SuppressedUntil`; the `ToyParrot` is a placed decoy looping a pitched-up cry that lures her (`MamaController.LureTo`).
- `EndlessHud`: the mod's own HUD layer.

---

## 8. Economy, saves and shop previews

**Saves** live in `Documents\Tattletail After Hours\` (`SaveFiles.cs`; old `Kaleidoscope Endless` saves are migrated on first launch), never in the game folder, so updates never wipe progress.

**`Shop.txt`** is a plain `Key = value` file: currencies, and 0/1 ownership per item. Writes are atomic:

```csharp
string tmp = FilePath + ".tmp";
File.WriteAllLines(tmp, lines.ToArray());
if (File.Exists(FilePath)) File.Replace(tmp, FilePath, BackupPath);   // keeps Shop.txt.bak
else File.Move(tmp, FilePath);
```

Three currencies: Shards (Endless), Party Tokens (Papa's party), Versus Tokens. The same cosmetic exists per mode under prefixed keys (`"Party Color Gold"`, `"Versus Color Gold"`), so each mode has its own unlocks. `Passives` applies bought permanent upgrades at run start. `Scrapbook` is a sticker collection with a 1000-shard loot box.

**Shop previews** (`PreviewBaker.cs`): the shop shows a 3D render per color/beam, but they're baked on the player's machine, not shipped. At run start, before any recolor, pristine clones of Tattletail and the flashlight are moved to a hidden rig far from the house, recolored per item, rendered by a manual camera into a `RenderTexture`, read back and saved as PNGs:

```csharp
var rt = new RenderTexture(320, 320, 24, RenderTextureFormat.ARGB32);
cam.targetTexture = rt;
... cam.Render(); ... ReadPixels ... EncodeToPNG → ShopPreviews\color_gold_v4.png
```

When a look changes, its file key gets a new suffix (`" v4"`) so every player re-bakes it.

---

## 9. Tattletail's colors, real fur and sparkles

The game has a `RecolorManager` that swaps whole materials per `RecolorType` (ORIGINAL, YELLOW, TEAL, BLACK, WHITE). Gold and Diamond don't exist in the game.

**The fur** is `Custom/FurShaderV1`, drawn by `FurShaderControllerV1` as N shells per frame with `Graphics.DrawMesh`, each layer's length/cutoff passed via a `MaterialPropertyBlock`. Reading the game's material assets showed the game's own Yellow/Teal fur only change the shader's **`_FurColor`**. `FurShine.cs` does the same, on both the renderer's materials *and* the controller's cached `FurMaterial` (which is what the shells are drawn with):

```csharp
foreach (var c in root.GetComponentsInChildren<FurShaderControllerV1>(true))
{
    var r = c.GetComponent<Renderer>();
    Material[] mats = r.materials;                 // per-instance copies
    for (int i = 0; i < mats.Length; i++) Tint(mats[i], fur);
    r.materials = mats;
    if (c.FurMaterial != null) { var copy = new Material(c.FurMaterial); Tint(copy, fur); c.FurMaterial = copy; }
}
...
m.SetColor("_FurColor", fur);
m.SetColor("_Color", Color.white);
```

Sparkles are a code-built `ParticleSystem` (local space, sphere shape sized from the model's bounds, twinkling size curve) using a generated four-point-star texture on `Particles/Alpha Blended`.

**Timing**: `EarlyColor` waits until Tattletail, the recolor manager and the flashlight exist, then calls `SceneMonitor.ApplyChosenColor()` (bake previews → capture a pristine copy → recolor), so the story's purple Tattletail never shows. `PristineTattletail` keeps that untouched copy; Tattletail's friends and the party Tattletails are cloned from it and given a random color from the full pool (`PartyTattletailModels.ApplyRandomColor`).

---

## 10. Papa's 10th Birthday!

A second mode on the same level: `PartyWorld` (18 marked spots spawn random ingredients every 30 s), `PartyGame` (the inventory, the "two ingredients → one food" crafting table, feeding Papa, the Entertainment meter), `PartyMachines` (4 food machines), `PartyFoods` (foods built from primitives), `RecipeCard`, `BirthdayDecor` (a cloned Mama dressed as Papa: white fur, top hat, monocle), `PartyTattletails` + `PartyTattletailGames` (random-colored clones hosting face paint / rock-paper-scissors / balloons), `PartyCastle` (a bouncy castle where tattletails ragdoll), `BirthdayMusic` (section 14), `BirthdayTask` (keeps one permanent HUD task line).

---

## 11. Online: Steam lobby and Versus netcode

**Transport** (Steamworks.NET: its types are in the game's own `Assembly-CSharp-firstpass.dll`, and the game already calls `SteamAPI.Init()` itself, so the mod only uses the API):
- `MpLobby`: friends-only 2-player lobby, invites through the Steam overlay, persistent across scenes.
- `MpNetworking`: P2P send/receive + a `PacketId → handler` dispatch table; `SendReliable` / `SendUnreliable`.
- `MpPacket`: a minimal binary writer/reader (write primitives in order, read them back in the same order).
- `VersusLobby`: who sits in the MAMA and SURVIVOR seats (the host owns the seats; the guest asks).

**`VersusNet.cs`**: two channels.

*Poses*, ~10/s, unreliable: context (Arcade or match), position, yaw, presence, a skin code (which kid + outfit, or which hunter + skin), and the custom kid's spec string:

```csharp
p.Write(ctx);
p.Write(pos.x); p.Write(pos.y); p.Write(pos.z);
p.Write(yaw);
p.Write(present);
p.Write(ctx == CTX_MATCH && VersusMode.IsMama ? VersusHunters.LocalCode : KidSkins.Code);
p.Write(KidSkins.Gender == KidGender.Custom ? CharacterSpec.LocalString : "");
MpNetworking.SendUnreliable(MpLobby.Instance.PeerSteamID, p);
```

*Events*, reliable, one enum: `Noise, MamaAway, MamaBack, FlashKill, Caught, Saved, RoundOver, ParrotLure, Gaze, Absence, Voice`. Each is `(int ev, Vector3, float, int)`; e.g. a kid's voice line packs `kind | (seed << 4)` so both games synthesize the identical sound.

**Authority**: the survivor's game runs the real Endless run (objectives, rests, phone, gadgets), so it decides when Mama may be around (`MamaAway`/`MamaBack`) and reports every noise; the hunter's game owns the kill meter and sends `Caught`.

**`VersusAvatars`** draws the other player: a kid model (rebuilt when their character or custom spec changes) at their feet position, smoothed, or the hunter's body. `WorldRays.FeetOf()` turns a controller position into feet (on the floor when standing, in the air mid-jump) using rays that ignore the local player's own capsule.

Survivor-side noise filtering: noises at the Toy Parrot don't count, and Tattletail's talking (which the game reports **every frame** of a line) is collapsed to one noise per line:

```csharp
if (tt != null && tt.tattletail != null && (pos - tt.tattletail.transform.position).sqrMagnitude < 0.0001f)
{
    bool sameLine = Time.time - _tattleNoiseAt < 0.5f;
    _tattleNoiseAt = Time.time;
    if (sameLine) return;
}
```

---

## 12. Versus: the hunter

`VersusMama.cs` (`VersusMamaController`): the hunter never walks. The game's own camera is moved behind a body standing at one of Mama's real patrol nodes (`RM.patrolNodeSet`), with night vision (raised ambient + a view light).

- **1 MAP**: an orthographic camera above the floor, near clip cutting the ceiling, renders a top-down picture once per floor; UI dots over each patrol node teleport on click; survivor noises appear as red pings. **Minimap**: the same render for the current floor in a corner, with a "you" dot and a view-direction bar.
- **2 KILL LIGHT**: within 7 m and line of sight → `FlashKill` event → the survivor's flashlight dies.
- **3 MAMA'S GAZE / PAPA'S ABSENCE**: Gaze freezes the child 2 s (their FPS controller disabled, camera shake, trembling noises and voice; on the hunter's screen the kid cowers via `KidAnimator.Scare`). Absence hides Papa's body on the survivor's side for 30 s.
- **4 KILL METER**:

```csharp
private void Noise(Vector3 pos, bool big)
{
    if (!_ready || _over || _mama == null || !_present) return;
    AddPing(pos);
    if (_awaitingCatch || Time.time < _nextNoiseAt) return;
    float dist = Vector3.Distance(pos, _mama.transform.position);
    if (dist > NOISE_RANGE) return;                  // 5 m
    _nextNoiseAt = Time.time + NOISE_COOLDOWN;       // at most one point per 1.5 s
    _quiet = 0f;
    _meter = Mathf.Min(METER_MAX, _meter + 1f);      // 4 to catch
    if (_meter >= METER_MAX) Catch();
}
```

(and every 5 s of quiet removes a point). The catch plays a first-person kill cinematic: the camera creeps in, lunges, the game's jumpscare scream plays, three claw slashes and a red flash animate on a UI overlay, and the kid model flinches and falls backwards (`KidAnimator.Flinch`).

`VersusHunters` encodes hunter + skin as one int code and builds the body (Mama clone, or Mama dressed as Papa, tinted per skin). Versus Tokens are awarded on both sides.

---

## 13. The kids: models, animation, outfits, the Character Creator

### Preset characters — `ChildModel.cs`
Boy, Girl, Dez and Kaz are generated by `tools/kid_model.py` into `KID2` binaries (embedded). `ChildModel.Load` parses the skeleton (bone names, parents, local offsets), baked animation clips (idle / walk / run / scared: per-frame hips offset + per-bone quaternions) and a skinned mesh with submeshes per material. `Build()` creates the bone hierarchy, a `SkinnedMeshRenderer`, and:

- **`KidAnimator`**: blends idle → walk → run by the model's measured speed (stride scaled by leg length), a scared pose, the flinch-and-fall, a trembling scare, and an in-air pose driven by vertical speed:

```csharp
float vy = (transform.position.y - _lastY) / Time.deltaTime;
_vy = Mathf.Lerp(_vy, vy, Mathf.Clamp01(Time.deltaTime * 12f));
_air = Mathf.MoveTowards(_air, Mathf.Abs(_vy) > AIR_SPEED && _flinchAt < 0f ? 1f : 0f, Time.deltaTime * 7f);
for (int j = 0; j < _jumpBones.Length; j++)   // knees up, arms up
    Bones[_jumpBones[j]].localRotation *= Quaternion.Euler(_jumpAngles[j] * _air, 0f, 0f);
```

- **`KidLook`**: the outfit, by recoloring the named materials (`shirtA`, `shirtB`, `sleeve`, `pants`, `shoe`, `bow`, `letters`); rainbow outfits animate; optional Tattletail-colored accents.

`KidSkins` lists every outfit per character, the ownership keys and the network code (`gender * 100 + outfit`).

### The Character Creator — `CharacterKit.cs`, `CreatorMenu.cs`
`tools/creator_kit.py` produces `creator_kit.bin` (`KIT1`, quantized: uint16 positions, int8 normals, byte bone weights): 6 bodies (3 frames × pants/skirt, skinned), 3 heads, 7 hairstyles, eyes/brows/noses/mouths, glasses, band-aids, freckle dots, eye circles, and for each head a **face grid**: the z of the face surface on a 5 mm grid.

A kid is a **`CharacterSpec`**: 29 bytes (version, styles, colors, 8 sliders as sbytes, extras, voice), base64'd so it can ride in every pose packet:

```csharp
var b = new byte[]
{
    3, Frame, Bottom, Head, Hair, HairColor, Skin, Eyes, EyeColor, Brows, Nose, Mouth,
    (byte)EyeSpacing, (byte)EyeHeight, (byte)EyeSize, (byte)BrowHeight, (byte)BrowAngle, (byte)MouthHeight, (byte)MouthSize, (byte)NoseSize,
    Expression, Glasses, Freckles, BandAid, EyeCircles, Accent, AccentColor, Voice, (byte)VoicePitch,
};
return System.Convert.ToBase64String(b);
```

`CharacterKit.Build()` snaps a kid together: bones, the body mesh, the head rigid on the head bone, hair scaled from the round head to the chosen skull. Every face feature is **placed on the face grid** and turned to the surface normal, so sliders slide features *along the face*:

```csharp
float z = grid.Sample(x, y) + 0.0015f;                     // bilinear on the face grid
Vector3 n = grid.Normal(x, y);                             // central differences
Quaternion rot = Quaternion.FromToRotation(Vector3.forward, n) * Quaternion.Euler(0f, 0f, mirror ? -angle : angle);
Rigid(piece, head, new Vector3(x, y, z) - headRest, new Vector3(mirror ? -size : size, size, size), rot, mats, spec);
```

The left eye/brow is the right one mirrored with a negative x scale. `KidFace` rebuilds the face with the "Terrified" expression while the kid is scared. `CreatorMenu` is an `OverlayPanel` with six tabs, a live `PreviewStage` preview that zooms onto the face, saving on every change and rebuilding at most ~12 times a second.

---

## 14. Sound synthesis: voices and music without audio files

### Kid voices — `KidVoice.cs`
A small **formant synthesizer**. Each sound is a list of keyframes `(time, pitch, loudness, voicing, breath, vowel)`; helpers build syllables and breaths; the sound library is code (jump "hup", land "oof", 14 reactions to seeing Mama, trembling under the Gaze, a caught scream), randomized per play with a seed.

Rendering at 22,050 Hz: a band-limited sawtooth "glottis" (PolyBLEP), softened by a one-pole low-pass, mixed with breath noise, run through **four cascaded resonators** (formants F1–F4 of the vowel, scaled per voice type), then a lip-radiation high-pass and an air-noise "hiss" path:

```csharp
float saw = 2f * phase - 1f - PolyBlep(phase, dt);
glot += (saw - glot) * 0.6f;                                   // softer highs, like a throat
float x = voiced * src + (breath + breathy * voiced) * noise * 0.5f;
for (int f = 0; f < 4; f++)                                    // formant resonators (Klatt style)
{
    float y = ca[f] * x + cb[f] * y1[f] + cc[f] * y2[f];
    y2[f] = y1[f]; y1[f] = y;
    x = y;
}
float o = x - 0.92f * lips; lips = x;                          // radiation at the lips: brighter
float s = (o + breath * hiss * 0.12f) * amp * trem;
```

Resonator coefficients (updated every 32 samples as keyframes interpolate): `c = -exp(-2πB/SR)`, `b = 2·exp(-πB/SR)·cos(2πF/SR)`, `a = 1 - b - c` (unity gain at DC, so four can be chained). Screams are then normalized and driven through `tanh`. Voice types change base pitch, formant scale (smaller mouth = higher), breathiness and a period-alternating rasp. The result is an `AudioClip.Create(...)` + `SetData(...)`, played 2D for yourself or 3D (linear roll-off to 30 m) at the other player's kid. The synthesis math was checked offline with a Python port measuring energy per frequency band.

### Party music — `BirthdayMusic.cs`
An original party loop composed and synthesized in code (bass, arpeggiated chords, lead melody, drums) into an `AudioClip`.

---

## 15. Rendering tricks: preview stages and the mirror

**`PreviewStage`**: live 3D previews inside menus. The model sits far from the level on layer 31; point lights with `cullingMask = 1 << 31` light only that layer; a dedicated camera renders it into a `RenderTexture` shown by a `RawImage`; the model spins on a `Turntable`. Used by OUTFITS, the Character Creator and the VERSUS SHOP.

**`HubMirror`**: a real planar mirror without a custom shader. A second camera sits at the **eye's reflection** behind the wall and renders through the glass with an **off-center frustum fitted to the glass corners**, near plane on the glass (nothing behind the wall leaks in):

```csharp
Vector3 n = _holder.forward, center = transform.position, eye = main.transform.position;
float d = Vector3.Dot(eye - center, n);
_cam.transform.position = eye - 2f * d * n;                          // the eye's reflection
_cam.transform.rotation = Quaternion.LookRotation(n, _holder.up);
Vector3 a = _cam.transform.InverseTransformPoint(transform.TransformPoint(new Vector3(-0.5f, -0.5f, 0f)));
Vector3 b = _cam.transform.InverseTransformPoint(transform.TransformPoint(new Vector3(0.5f, 0.5f, 0f)));
float near = d + 0.02f, far = ..., k = near / d;
_cam.projectionMatrix = OffCenter(Mathf.Min(a.x, b.x) * k, Mathf.Max(a.x, b.x) * k,
                                  Mathf.Min(a.y, b.y) * k, Mathf.Max(a.y, b.y) * k, near, far);
```

The picture is flipped left-right via texture tiling and shown as emission on a black Standard material (unaffected by room light). The player, who is first person and has no body, gets a kid model on layer 30, which only the mirror camera renders. It renders only in `OnWillRenderObject` for the main camera, i.e. only when the glass is on screen.

---

## 16. Python asset pipeline

`tools/kid_model.py` sculpts characters from **signed distance functions**: ellipsoids and capsules per body part, blended with a smooth-min (`smin`) for soft joins or hard unions for clothes/hair, sampled on a voxel grid and meshed with **marching cubes** (scikit-image). Each vertex is labeled by which shape "owns" it, which gives its **material** (skin, shirt, hair, ...) and its **region** (torso, left arm, ...); skin weights are computed per region so limbs never drag each other. Face details (eyes, brows, letters on a shirt badge) are separate small meshes placed by raymarching onto the surface. Animations are pose functions of the cycle phase (`pose_idle`, `pose_walk`, `pose_run`, `pose_scared`) sampled into 30 frames of per-bone quaternions (`bake()`). Output: `KID2` binaries + glTF previews for a three.js browser viewer.

`tools/creator_kit.py` reuses the same machinery to build every Character Creator piece separately, samples each head's face into a z-grid, and quantizes everything into `KIT1` (~3 MB).

---

## 17. Utilities

- **`WorldRays`**: raycasts that ignore the local player's own collider (`RaycastAll` + filter). Fixed the other player's kid ending up on your head and the Toy Parrot landing on you; `FeetOf()` places avatars.
- **`Mat`**: `Lit(color, gloss)`, `Unlit(color)`, `Box(...)`.
- **`SaveFiles`**, **`StorySave`**, **`Shards`**, **`EndlessStats`**.
- **`BlurFilter`**: 0–100% strength sliders for the camera's post effects.
- **`StickerImages`**: loads sticker PNGs from next to the DLL.
- **`LoadingCover`**, **`VersionTag`** (+ `Support`: the Ko-fi link).

---

## 18. Lessons learned

The codebase follows these because each one was learned from a real bug:

1. **Guard every risky startup step**: one unguarded `PatchAll()` failure once removed the whole mod.
2. **Stay inside old Mono**: no Func/Action/LINQ, `(object)` null checks on reflection types, the iterator-attribute shim; verify the built DLL, not just the source.
3. **Only shaders that are in the build**: a missing shader returned null and froze the Arcade's loading.
4. **Don't depend on vanilla quest/interactable state**: build self-contained objectives and interactables.
5. **Fix mechanics instead of removing them**.
6. **Read how the game does it first**: decompile the relevant class (`ilspycmd`) or read its assets (`UnityPy`). Gold/Diamond fur went wrong three times by guessing, and was right the first time after reading the game's own recolor materials.
7. **Watch what the game reports, not what you expect it to**: Tattletail's talking reports a noise every frame, which turned into "instant deaths" once noises filled a meter.
8. **Rays ignore yourself**: floor checks hit the local player's capsule.
9. **Saves outside the game folder, atomic writes**.
10. **Release builds are stripped** of dev tools and debug flags, every time.
