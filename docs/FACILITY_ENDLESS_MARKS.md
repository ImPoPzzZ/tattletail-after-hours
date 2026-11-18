# Endless in the Waygetter Facility: what to mark

Dev build (BepInEx config: `DevTools = true`). Endless machine -> **Map: Waygetter Facility** -> PLAY. Mama is off. You have the flashlight and the camcorder.

**E** opens the naming panel. On an empty name press a digit for the item (it numbers itself: Vase 1, Vase 2, ...), click **Prop**, press Enter, then walk to the spot and press **E** to set it down (it faces where you stood). **Z** undoes the last one.
Every mark is written to `BepInEx\facility_marks.txt` (and to `LogOutput.log`) as
`PROP 'Vase 3' at local (14.20, 22.50) height 0.02 facing 135`. Height 0 is the ground floor, about 4 is upstairs, about -4 the basement. Send that file.

Stand where the item should sit (a vase on the floor: on the floor; on a table: stand at the table and say so in the name, e.g. `Vase 4 table`).

| Key | Item | How many spots | What the objective does with them |
|---|---|---|---|
| 1 | Vase | 6 or more | Break 3 of them (hold 0.6 s). Breaking is a noise. |
| 2 | Candle | 6 or more | Light 3 of them (hold 0.6 s). |
| 3 | Friend | 6 or more | Tattletail's friend hides at one: "Say Hi" (hold 1.2 s). |
| 4 | Tape | 6 or more | A VHS tape to rewind (hold) at one of them. |
| 5 | WashingMachine | 3 or more | Turn it off (hold 1.2 s); it hums so you can find it. |
| 6 | Speaker | 3 or more | Silence it; it plays so you can find it. |
| 7 | FuseBox | 3 or more, **on a wall** | Restart it (hold); a warning triangle and a red light. |
| 8 | Coin | 10 or more | Power Up coins (Max Stats, Strong Battery, Totem), 20% per objective. |
| 9 | Charger | 2 or more | Charge Tattletail (hold 5 s). The Facility already has charging docks. |
| 0 | Phone | 2 or more | Rings every third objective; answer it to leave. |

Also useful, with any name you like (the tool is not limited to the list): `Start` (where a run begins), `Exit`, `Mama` spots later, and walls (`Wall`) for places that are fake.

## The objectives in normal Endless

1. **Washing machine**: find the running washing machine and turn it off.
2. **Flashlight survival**: keep the flashlight lit for 30 / 60 / 90 s (shorter on harder difficulties).
3. **Break 3 vases** (of 6 spots).
4. **Light 3 candles** (of 6 spots).
5. **Find Tattletail's friend** (one of 6 spots; hold to say hi).
6. **Rewind the tape** (one of 6 spots).
7. **Silence the speaker** (one fixed spot).
8. **Restart the fuse box** (a wall spot).

Retired: **Silence the noise** (it duplicated the washing machine).
Between them: rest periods (30 s, Mama away), Power Up coins, the charging station, the phone every third objective (answer it to escape), Chaos Round and Lucky Night, and the True Ending easter egg (needs two tapes, two friends and a Diamond friend).
