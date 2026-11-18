# Recalled
### Design document for After Hours' 4-player co-op RPG

*Made By: ImPoPzzZ & Sa1monfr. Started 2026-09-30. Status: the solo game for World 1 is built (first pass, not yet playtested); co-op and Online are not. See "What's built" below.*

---

## 1. What it is

A third-person co-op RPG inside Tattletail: After Hours, for 1 to 4 players. A door in the Arcade leads outside, into a town the recalled toys have taken over. You fight them with gear you craft from their parts, alone or with up to three friends.

It is its own game mode with its own story. It doesn't share rules, progress or currencies with Endless, Versus or the other modes.

It started as an MMO plan (about 100 players per server). On 2026-09-30 it became 4-player co-op: the spooky tone works with four kids and flashlights, not with a crowd, and four friends in a Steam lobby is something the mod already knows how to do.

### What's built (2026-09-30)

Dev builds only for now: **F10 in the Arcade**. The door in the Arcade isn't placed yet.

| Built (solo, first pass) | Not built yet |
|---|---|
| The whole town from the map: streets, the park, the treehouse with its three decks, all five zones with their interiors, the fence and gates, landmarks | 4-player co-op, and everything that needs it: Online matching, reviving, trading, emotes and pings, the host taking over |
| Third person, over the shoulder, with a crosshair; dodge; flashlight | The door in the Arcade, and its Solo / Friends / Online choice |
| Melee, ranged and gadget weapons in five tiers; armor; trinkets | Attack, dodge and strafe animations made in the model pipeline (arms are posed in code for now) |
| Strays in every color tier, packs, sight and hearing; the jack-in-the-box, RC cars, robots, fused toys | Two-floor interiors (the mall, the school and the warehouse are one floor) |
| All five bosses, with slams, charges, volleys and helpers; Mama hunting you through the warehouse before her fight | A map you can open |
| Levels and experience, loot, scrap piles, the backpack and stash, crafting, backpack upgrades | Later worlds |
| The quest chain (21 quests) and the quest board | |
| Forts and ziplines, level gates | |
| Dying: you wake in the treehouse, your materials stay where you fell | |
| The protected save | |
| The music: three layers per zone, the treehouse lullaby, Mama's theme | |

How it was checked, since it can't be played from here: the game's rules, the character, the save file and the whole town's layout run in tests outside the game. They prove every spawn, scrap pile, fort and boss arena can be reached on foot, that a shut gate seals its zone, that the quest chain can be finished and lands on levels 4, 7, 10, 13 and 15 at the five bosses, and that a changed or copied save is refused. What they can't show is how it plays: that needs a playtest.

### How to read this document

Every section is marked:

- **Decided**: ImPoPzzZ has said this is how it is.
- **Proposed**: a suggestion that fits the decisions, not yet approved. Change anything.

---

## 2. Decisions so far

| Topic | Decision |
|---|---|
| Name | Recalled. |
| Players | 4-player co-op (1 to 4). |
| Who you play with | At the door you choose: make a lobby with your friends, or play online with random people. |
| Entry | A door in the Arcade that leads outside. |
| Platform | Steam for playing together. No rented server. |
| Camera | Third person, always over the shoulder (to the side and behind), so you can aim. Your kid faces where you look. |
| Movement | Walking and running are 20% faster than in the rest of the mod. |
| Scope | A full RPG with everything an MMO has: bosses, health bars, stats, loot, crafting, materials, quests. |
| Relationship to the other modes | Its own mode and its own story. |
| Combat | Everything: melee, ranged and abilities. |
| Classes | None. Players build from gear. |
| Who you play as | The same kids from OUTFITS (including custom kids and DLC characters). |
| Setting | A town where recalled Waygetter toys have taken over, and the kids are the resistance. |
| Tone | Spooky, like Tattletail. |
| Darkness | Lit streets: street lamps and windows light the main paths; side streets and buildings are dark. |
| Tattletails | Enemies. |
| Papa | The boss of the junkyard: the last boss before the warehouse and Mama. |
| Mama | The final boss. |
| Levels | There are levels and experience. The level cap for World 1 is 15. |
| World 1 | The whole town (all five zones). Mama at level 15 is the end of World 1; later worlds are new places with a higher cap. |
| The Hideout | A treehouse. |
| Inventory | A limited backpack on you, plus a big stash in the treehouse that's safe when you die. |
| Dying | You're downed and a teammate can revive you. If nobody does, you wake up in the treehouse and the materials you were carrying stay where you died until you fetch them. |
| Saves | Harder to cheat than the mod's text files. People will try. Only Recalled gets this; the other modes keep their text saves. |
| If the host leaves | Another player takes over as host and the session continues. |
| Quest progress in co-op | Everyone in the session progresses: a quest finished together counts for every player there. |
| Backpack, experience pace, scaling with players | Left to the developers (section 9b). The numbers are starting points to tune in playtests. |
| Online: joining during a boss fight | A matched player waits at the door of the arena and joins when the fight ends, or for the next attempt. |
| Online: nobody else searching | You start alone and your group stays open; players drop in as they appear. No waiting screen. |
| Online: matching | By zone: you're matched with groups playing in a zone your level can enter. |
| Online: talking | Quick emotes and pings. No text chat. |
| Music | Recalled has its own music, all written in code. No recorded tracks. |
| Trading | Everything can be traded: gear and materials. Every item needs a level to equip, so a low-level player can be handed strong gear but can't use it yet. |

---

## 3. The setting (proposed, on top of the decided premise)

Waygetter recalled its toys, and the toys refused to go back. They took over a whole town. The adults are gone. The kids who are left are the resistance.

- **Always night, with lit streets** (decided). Street lamps and windows light the main paths. Side streets and the insides of buildings are dark, and that's where your flashlight matters.
- **The Hideout** is the kids' base, a treehouse (decided): the only warm, lit, safe place. It's where your group meets, crafts, stores loot and picks up quests.
- **The recall siren.** Every night the siren sounds and the toys reclaim everything. Each session is one night: your character keeps everything it earned, and the town is overrun again next time.

### Zones

Each zone is overrun by the toys and ends in a boss. The map, the layout of each zone and the treehouse are in `docs/recalled_world.html` (a proposal).

| Zone | Role |
|---|---|
| The Hideout (a treehouse) | Safe hub. No enemies. |
| The suburbs | First zone. Weak strays, the first boss. |
| The mall | Mid game. |
| The school | Mid game. |
| The junkyard | Late game. The best scrap. Papa is its boss. |
| The Waygetter warehouse | Endgame. Mama. |

---

## 4. Tone rules (decided: spooky like Tattletail; the rules are proposed)

The original game's tension comes from noise and light. Recalled keeps both, and a group of four makes them matter more: one loud player endangers everyone.

- **Noise matters.** Toys hear you. Running, fighting and loud weapons pull more of them in. A nail gun is strong but risky; a bat is quiet.
- **Light matters.** Your flashlight shows what's ahead, and it gives you away.
- **You hear them first.** Tattletails chatter, so you hear a pack before you see it. When they go quiet, they've noticed you.
- **Mama is a threat long before she's a fight.** Early on she can appear as something you can't beat: you hear her coming and you hide. The final fight is the payoff.

---

## 5. The player (decided: third person, the same kids, no classes)

- You play your kid from OUTFITS: your character, your outfit, your custom kid.
- There are no classes. What you can do comes from what you're holding and wearing.

### Proposed details

- **Two weapon slots** you can switch between in a fight, so one kid can do everything.
- **Armor and trinkets** push your stats in a direction: tanking, damage, healing, speed.
- **Stats** (all from gear): health, power, speed, cooldowns, healing.
- **Levels and experience** (decided): you earn experience and level up. World 1's cap is level 15; later worlds raise it.
- What levels do (proposed): they unlock gear tiers and zones, not abilities, so your build still comes from gear.
- Where experience comes from (proposed): defeating toys, finishing quests, and a big chunk for each boss.

---

## 6. Combat (decided: everything; the breakdown is proposed)

Your weapon decides how you fight.

| Type | Weapons, made from scrap | Feel |
|---|---|---|
| Melee | Bats, hockey sticks, a pipe wrench, pan lids as shields | Fast, up close, dodging. Quiet. |
| Ranged | Slingshots, nail guns, bottle-rocket launchers | Aiming, keeping distance. Loud. |
| Gadgets | Rigged batteries, circuit boards, a modified toy | Abilities on cooldowns: shock blasts, decoys, repair-heals, shields. |

Gadgets are this world's magic. A healer is a kid with a repair gadget and cooldown gear; a tank is a kid with pan lids and health gear. With four players, a group can cover every role, and a solo player can swap between them.

---

## 7. Enemies (decided: Tattletails are enemies, Papa and Mama are bosses)

### Tattletails (proposed tiers)

The game's own colors become the difficulty tiers. The mod already clones and recolors the real Tattletail model, so the basic enemy needs no new art.

| Tier | Color | Notes |
|---|---|---|
| Common | Purple (original) | Strays, in packs. |
| Tougher | Yellow, Teal | |
| Dangerous | Black, White | |
| Rare elites | Gold, Diamond | Drop the best parts. |

### Other toys (proposed)

- **Elites** with one trick each: a jack-in-the-box that ambushes, an RC car that rams, a robot that shoots.
- **Bosses**, one per zone: a huge or fused toy with a health bar and phases, designed for a group of four.

### Papa (decided: the junkyard's boss)

Papa guards the junkyard, the last zone before the warehouse. He's the last boss before Mama.

### Mama (decided: the final boss)

She's in the Waygetter warehouse, and the main quest chain builds toward her. Beating her at level 15 is the end of World 1.

---

## 8. Loot, crafting and quests (decided: they exist; the design is proposed)

- **Materials come from what you break**: springs, batteries, circuit boards, plastic, fur, eyes, voice boxes, screws. Rarer toys drop rarer parts.
- **Crafting** at the treehouse turns parts into weapons, armor and gadgets. Better benches unlock with progress. Crafting is how you get the build you want, instead of waiting for a lucky drop.
- **Loot is rolled separately for each player**, so nobody fights over drops.
- **Backpack and stash** (decided): you carry a limited backpack. The treehouse has a big stash that's safe when you die, so what you risk is what you're carrying.
- **Trading** (decided): everything can be traded, and every item needs a level to equip.
- **Quests** come from kids in the treehouse: clear a street, rescue someone, find a part. A main chain leads to the warehouse and Mama.

---

## 9. Playing together (decided: 4-player co-op on Steam; the details are proposed)

- **You choose at the door** (decided). Going outside gives you the choice of a lobby with your friends or playing online with random people. Proposed as three options:

| Option | What happens |
|---|---|
| Solo | You go out alone. Nobody can join. |
| Friends | You make a lobby and invite up to three friends, the way Versus already works for two. Only invited players can join. |
| Online | The game looks for an open group with a free spot and puts you in it. If there isn't one, it starts a group for you and others can be matched into it. |

- **How Online finds a group.** Open groups are Steam lobbies tagged as Recalled, which the game searches for (proposed). It only matches players on the same version (proposed), and into groups playing in a zone your level can enter (decided). No server browser: you pick Online and you're in.
- **Nobody around** (decided). If no group is found, you start alone with your group open, and players drop in as they appear. There's no waiting screen.
- **Boss fights** (decided). A player matched in during a boss fight waits at the arena's door and joins when the fight ends, or for the next attempt. The fight never changes size halfway.
- **Talking** (decided). Quick emotes (wave, thanks, help) and a ping to mark a spot or an enemy. No text chat, so there's nothing to moderate.
- **Removing a player** (proposed). The host can remove a player. The host's checks on joining characters (below) matter most here.
- **One player hosts.** The host's game runs the enemies and bosses, so everyone sees the same fight with the same health. The others send where they are and what they do.
- **Your character is yours.** Progress is saved on your own PC and comes with you into anyone's game.
- **Everyone progresses** (decided). A quest finished together counts for every player who was there. The session follows the host's point in the story. If you join a friend who's further along, you get credit for what you do together, and the earlier quests you skipped stay open for you to do later. Zones still need their level, so joining a friend can't carry a level 3 kid into the warehouse.
- **Solo works too.** You can play the whole thing alone, which also means players on the itch.io version (no Steam) can play it, just not with friends.
- **Same version needed.** The version check that already protects Versus applies here.

### Saves (decided: harder to cheat than text files; the design is proposed)

The mod's other saves are plain text (`Shop.txt`), which anyone can edit in Notepad, and they stay that way (decided). Recalled has its own separate save file, and it's different:

| Layer | What it does | What it stops |
|---|---|---|
| A binary file, encrypted | The save isn't readable or editable as text. | Opening it in Notepad and typing a bigger number. |
| Signed | The file carries a signature made with a secret inside the mod. A changed file fails the check and isn't loaded (the last good backup is used instead). | Editing with a hex editor or a generic save editor. |
| Tied to your Steam account | The signature includes your Steam ID. | Downloading someone else's maxed save. |
| Sanity checks on load | Level within the cap, items that exist, stats an item can really roll, no gear above your level. | Saves that are valid but impossible. |
| The host checks who joins | In co-op, the host's game runs the same sanity checks on each player's character. | Bringing an impossible character into a friend's game. |

**The limit, stated plainly:** with no server, this can't be made uncheatable. The secret has to be inside the mod, and someone determined can dig it out of the DLL or edit the game's memory while it runs. These layers stop casual cheating, which is nearly all of it. They don't stop a programmer who wants to cheat badly enough. The mod's source is in a private repository, which helps; it must stay private, and the secret must never be committed to it.

### What having no server means

| Consequence | How we handle it |
|---|---|
| Your character is saved on your own PC. | The save is encrypted, signed and checked (above). Every item needs a level to equip, so handed-out gear can't skip the game. |
| The game depends on the host staying. | Decided: if the host leaves, another player takes over as host and the session continues. Steam passes the lobby to another member by itself; that player's game starts running the enemies from the last state it saw. |

## 9b. Numbers (decided by ImPoPzzZ; starting points to tune in playtests)

### Backpack and stash

- **Backpack: 20 slots** to start. Materials stack up to 50 per slot; weapons, armor and gadgets take a slot each.
- **Four upgrades of +5 slots**, up to 40. Each is crafted at the treehouse from a part that only a zone's boss drops (suburbs, mall, school, junkyard), so a bigger backpack marks how far you've got.
- **Stash: 100 slots** in the treehouse, safe when you die, shared by nothing: it's yours alone.

### Experience pace

World 1 (level 1 to 15) should take about 12 to 15 hours, the same alone or in a group.

| Zone | Levels | Rough time |
|---|---|---|
| The suburbs | 1 to 4 | 2 hours |
| The mall | 4 to 7 | 3 hours |
| The school | 7 to 10 | 3 hours |
| The junkyard | 10 to 13 | 3 to 4 hours |
| The warehouse | 13 to 15 | 2 to 3 hours |

- The first levels come fast: level 2 within about ten minutes, so a new player feels progress straight away.
- Each boss gives a big chunk, roughly a third of a level.
- A zone's quests alone get you most of the way; you shouldn't have to grind strays to move on.

### Scaling with players

Yes, enemies get tougher with more players, so four friends don't flatten everything and a solo player isn't crushed.

| Players | Enemy and boss health | Pack size |
|---|---|---|
| 1 | 100% | normal |
| 2 | 160% | +1 |
| 3 | 220% | +2 |
| 4 | 280% | +3 |

- **Health goes up, damage doesn't.** Fights get longer and need teamwork, but a hit doesn't suddenly kill you because a friend joined.
- **Health grows slower than the group does** (four players deal about four times the damage against 2.8 times the health), so playing together is always a bit easier than alone. Grouping should never feel like a punishment.
- **Bosses add things at three or more players**: extra helpers, or an attack that needs two kids in different places.
- **Experience isn't split.** Everyone nearby gets the full amount, and loot is already rolled per player.
- **It adjusts live** when someone joins or leaves, including in the middle of a fight.

### More players later

If Recalled turns out well, raising the cap is possible. The plan for that (searchable lobbies, players only talking to the players near them, simpler models for far-away players) is recorded in this file's history.

---

## 9c. Music (decided: it has music, all written in code; the rest is proposed)

The mod already writes its music in code (the party loop, the beach's steel drums, the trailers, pinball), so nothing copyrighted ships and there are no audio files. Recalled uses the same approach, with a sound built from toys: music boxes, toy pianos, a wind-up mechanism ticking, a slightly out-of-tune lullaby.

### The tracks

| Where | Music |
|---|---|
| The treehouse | The one warm theme: a gentle music-box tune. Safe. |
| Each zone | A quiet, uneasy bed for exploring: low drones, a slow music-box figure, long silences. One per zone, each with its own color. |
| The recall siren | A short motif that returns across the soundtrack, heard at the start of a night. |
| Bosses | A full theme each. Papa's and Mama's are the big ones; Mama's should quote the treehouse lullaby, turned dark. |
| Leveling up, finishing a quest, a rare drop | Short jingles. |

### Music that follows the danger

Each zone's track has layers that fade in and out with what's happening:

1. **Exploring**: the quiet bed.
2. **Noticed**: the Tattletails have gone quiet. A pulse comes in, and the music box drops out.
3. **Fighting**: drums and a driving line.

Because the layers are generated to the same tempo and key, they can be faded in any combination without a seam. This is also a gameplay signal: the music tells you that you've been heard before you see anything.

### Silence

Tattletail's horror leans on hearing things. The exploring layer stays sparse, so chattering, footsteps and a pack going silent are never buried under music.

## 10. What we already have, and what's new

**Already in the mod:**

- The kid models, with outfits, the character creator and idle, walk and run animations.
- The pipeline that generates characters, which can make enemies and bosses too.
- Building worlds in code, with the mark tool for placement.
- Menus, saves, shops and currencies.
- Synthesized sound and music.
- Cloning and recoloring the real Tattletail and Mama models.
- A Steam lobby, a packet layer and a version check (two players today).

**New work:**

- A third-person camera and controls, and attack, dodge, hit and death animations for the kids.
- Combat: enemy AI, health, hit detection, damage numbers, boss health bars.
- The RPG systems: stats, levels, inventory, loot tables, materials, crafting, quests, and their screens.
- The world: the treehouse, the zones, the quest givers.
- Netcode for four players (today's is strictly two), with enemies run by the host.

---

## 11. Roadmap

Each step is playable on its own.

| Step | What | Why it's in this order |
|---|---|---|
| 1 | **A solo slice**: the third-person kid, the treehouse, the suburbs, one stray, one elite, one boss with a health bar, one weapon of each type, five materials, a few recipes, one quest chain. | If it isn't fun alone, friends won't fix it. |
| 2 | **Co-op for 4**: the same slice with friends, enemies run by the host, reviving. | Proves the netcode while the game is still small. |
| 2b | **Online**: the lobby search test, then matching with random players. | Builds on the friends lobby; only the way you find each other is new. |
| 3 | **The real systems**: levels, stats, the full inventory and stash, crafting, trading, a quest chain. | |
| 4 | **The door and the town**: the door in the Arcade, then the mall, the school and the junkyard with Papa. | |
| 5 | **The warehouse and Mama**: the end of World 1, at level 15. | |

---

## 12. Risks

- **Size.** Even as 4-player co-op, this is bigger than anything in the mod so far. The solo slice exists to find out early whether it's fun.
- **Quality bar.** The Waygetter Facility's story was scrapped for not being good enough. The same bar applies here: keep the first slice small and make it good.
- **Steam lobby search** is what Online matchmaking depends on, and it's untested in Tattletail. It's a quick test, and Solo and Friends don't need it.
- **Strangers.** Online means playing with people you don't know: cheated characters and bad behavior matter more there than with friends. The host's checks, removing a player and having no text chat are the tools.
- **The netcode rewrite.** Everything online in the mod assumes exactly one friend. Going to four touches all of it.
- **Performance.** Packs of furry Tattletails are expensive to draw (the fur is drawn in many layers). Pack sizes and how many are on screen need limits, tested on a slow PC.
- **Cheating.** Local saves can be protected against casual editing, not against a determined programmer (section 9).
- **Third-person controls.** The game was built first-person. The camera, the kid's movement and hit detection are all new, and they decide whether combat feels good.

---

## 13. Open questions

1. What the later worlds are (not decided yet, and nothing depends on it until World 1 exists).
