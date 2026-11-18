# Endless: Psychological Traits (built in 3.7)

*Made By: ImPoPzzZ & Sa1monfr. The 34-trait list and its proposed effects, 2026-10-05. Built in 3.7 (section 56 of the complete reference). Numbers are starting points. Differences from this design: Cold Blood is now the first flashlight kill in each objective not counting, Inseparable is needs drain 35% slower and care stations 20% faster, and Environmental Fixation points out the exact spot after 20 s (room hints are already on in Endless).*

## What a "hold" is

The time you keep left click held on something to finish using it (the game's hold-to-interact bar): (the game raises every hold to at least 0.9 s) vase 0.9 s, candle 0.9 s, phone 0.9 s, washing machine 1.2 s, Tattletail's friend 1.2 s, charging Tattletail 5 s. Most holds are short, so hold bonuses are set big (25 to 50%) to be felt.

## How it works

- **You choose two traits** on the Endless setup screen, next to the gadget (a "Profile" card). Each trait has a **bonus and a price**, so nothing is free.
- **Steady traits** (S) lean positive; **conditions** (C) lean dark. Pick any mix. **Traits never add shards** (difficulty stays the only shard dial); the only shard effects are prices (Survivor, Environmental Fixation, Compulsive Secrecy).
- **Unlocking** (open question): all open from the start, or earned from your stats (`EndlessStats`: longest survival, objectives, deaths, exfils), or bought with shards.
- **Combos** between two chosen traits get a name and a small extra, and your **title** on the card and the end-of-run screen is built from them ("The Methodical Survivor"). The end screen can read like Waygetter's file on you.
- **Not in Versus** at first (noise changes would make the hunter's kill meter uneven).
- All hooks already exist: hold times (`MakeInteractable`), noise range (Mama's `OnNoise`), alert time and patrol speed (`MamaController`), flashlight kills (`CLOSE_CALL_LIMIT`), needs drain (`RunModifiers`), rest length (`REST_DURATION`), phone timing (`LOOP_LENGTH`), shard payout (`EndlessRunManager`), hints (`RoomHints`), coins (`PowerUpObjective`), gadget cooldowns (`Gadgets`), speed (`FirstPersonController` fields).

## The 34 traits

| # | Trait | Type | Bonus | Price |
|---|---|---|---|---|
| 1 | Paranoid | C | A heartbeat and a screen-edge pulse when Mama is within ~12 m | A false alarm now and then |
| 2 | Violent Tendencies | C | Vases break 50% faster and 30% quieter | Everything else you touch is 20% louder |
| 3 | Lone Wolf | S | Gadget cooldowns -20%, speed +5% | Tattletail's needs drain 15% faster |
| 4 | Professional | S | Mama's first alert in each objective comes 10 s later | Rests are 20 s instead of 30 |
| 5 | Fearless | S | 4 flashlight kills allowed instead of 3 | Mama's alert time +15% |
| 6 | Reliable | S | Care stations (charge, feed, brush) 30% faster | Gadget cooldowns +15% |
| 7 | Tormented | C | Adrenaline: +12% speed while Mama is within 8 m | A bad-memory moment every few objectives: lights dip, a whisper hides Mama's footsteps for 4 s |
| 8 | Aggressive Behaviour | C | Speed +8%, break and light objectives 35% faster | Your running is 25% louder |
| 9 | Survivor | S | Start with a Totem of Undying | -15% shards |
| 10 | Calm Under Pressure | S | While Mama is within 8 m your holds are 30% faster and your noise 20% quieter | Tattletail's needs drain 10% faster |
| 11 | Methodical | S | Interaction holds 35% shorter | You move 5% slower |
| 12 | Relentless | S | +10% speed during objectives | Rests cut to 15 s |
| 13 | Dependable | S | The phone's exfil bonus is 40% instead of 25% | The phone rings every 4th objective, not every 3rd |
| 14 | Impatient | C | Holds 25% shorter, speed +5% | Flashlight Survival lasts 20% longer |
| 15 | Persistent Curiosity | S | Power-up coins appear twice as often and reveal from farther | Picking one up makes a small noise |
| 16 | Unhealthy Attachment | C | Tattletail's needs drain 25% slower | Distracted: while any need is below 25% you move 10% slower and holds are 20% longer (his crying at 0 is already in the game) |
| 17 | Compulsive Observation | S | Hidden objectives pulse a hint within 6 m | Flashlight drains 10% faster (you keep it on) |
| 18 | Fear of Abandonment | C | Each flashlight shake charges 20% more | With the flashlight off for 10 s you are 10% louder |
| 19 | Separation Anxiety | C | Care-station holds 40% shorter | Objective holds +20% |
| 20 | Hypervigilant Behaviour | C | Mama's teleports are flagged on screen with a direction | Your running is 20% louder |
| 21 | Environmental Fixation | C | Room hints always on, and after 20 s of searching they name the exact spot | -10% shards |
| 22 | Repetitive Behaviour | S | Each repeat of an objective type is 25% faster (up to 50%) | The first time you meet a type its hold is 25% slower |
| 23 | Unwarranted Suspicion | C | Gadget reach +30% (Flash Camera range, Parrot pull) | 10% of objective prompts are harmless decoys |
| 24 | Compulsive Checking | C | Once per run you may skip an objective and take the next | Every hold is 25% longer |
| 25 | Anomalous Attachment | C | Each objective Tattletail changes color and gives one small random buff (speed, hold, needs) | and one small random debuff |
| 26 | Unresolved Fear | C | +15% speed while Mama is actively hunting you | Mama starts every objective already alerted |
| 27 | Reality Avoidance | C | Mama's detection of you -25% | The HUD is stripped: no timer, no quest text |
| 28 | Distorted Perception | C | Holds take half as long while Mama is hunting you | Harmless phantom Mamas appear; the screen warps now and then |
| 29 | Intrusive Thoughts | C | Whispers sometimes point toward the nearest objective | Others give a random debuff for 8 s |
| 30 | Paranoid Fixation | C | A pointer to Mama when she is within 15 m | Her patrol speed +10% |
| 31 | Compulsive Secrecy | S | Mama loses interest 20% sooner | The phone's exfil bonus is halved |
| 32 | Unstable Attachment | C | A random gadget replaces yours each objective, each with -25% cooldown | You never choose which |
| 33 | Persistent Dread | C | Tattletail's needs freeze while Mama is hunting | Mama's patrol speed +15% and alert time +10% |
| 34 | Unexplained Behaviour | C | A random pair of the other traits each objective, always one steady and one condition | None: pure chaos |

## Combos (names and a small extra)

| Pair | Name | Extra |
|---|---|---|
| Paranoid + Hypervigilant | Nothing Gets Past You | Both warnings merge into one clearer cue |
| Lone Wolf + Professional | The Contractor | +5% shards |
| Survivor + Tormented | Back From It | After a catch the Totem recharges once per run |
| Methodical + Compulsive Checking | By The Book | Skipped objective gives +10 shards |
| Fearless + Calm Under Pressure | Cold Blood | The first flashlight kill in each objective does not count |
| Separation Anxiety + Unhealthy Attachment | Inseparable | Needs drain 35% slower, and care stations are 20% faster |

## Exclusions

Lone Wolf cannot pair with Fear of Abandonment, Separation Anxiety, Unhealthy Attachment, Unstable Attachment or Anomalous Attachment. Two traits with the same hook in opposite directions cannot pair (Impatient + Methodical).

## Wording

Keep every label fictional and in-universe (a Waygetter-style "profile"), never a real diagnosis. Rename or soften Violent Tendencies, Unhealthy Attachment and similar if they feel too heavy.

## Build notes

An `EndlessTraits` class (ids, bonus/price table, combo table, apply points), a Profile `OverlayPanel` on the Endless setup screen, a small HUD row of two icons, `Shop.txt` keys for unlocks and the last choice (like the gadget), a title/end-screen line. About 700 lines.
