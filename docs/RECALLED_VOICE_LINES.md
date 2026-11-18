# Recalled: every voice line (for review)

*Made By: ImPoPzzZ & Sa1monfr. Written 2026-10-01 for 3.6. In the game since 3.6, as text only (no voices): the kids, Waygetter's announcements, the toys, the bosses, the Tape scenes and the notices. To change a line, change it here and keep its ID (the game's text is made from this file by tools/gen_voice.py and tools/gen_lines.py). Not in yet, waiting for Egypt: Pip's lines, Last Stop's radio and the book epigraphs.*

## Writing rules (from the review, applied to every line below)

1. **Waygetter never knows it's evil.** It's scariest when it talks as if everything is normal procedure. No horror jokes, no winks ("chance of Mama", "you had no choice" and "they will keep you" are gone).
2. **Moss is perceptive, not omniscient.** He forms theories; the Tape and the story confirm them later. Don't let him know the answer first.
3. **Theo is funny because he's frightened**, not because the script needs a joke.
4. **Everyone stays inside the fiction.** Nobody says "you can play with friends" or knows about levels. Tutorial lines are said in the world's own words.
5. **Preserve silence.** There is enough dialogue. Don't add much more ambient talk to World 1: empty space between the voices is part of the horror. The radio (Last Stop) is rare: one line now and then, never on a loop. "I'll leave the lights on" is Pip's signature line: don't overuse it.

**Anchor lines** (protect these even if everything around them changes): `MOS-SIREN-1`, `HAL-PHASE-2`, `PAP-AFTER`, `MAM-LOW`, `PA-ZONE-FAC`, `NN-BOOK-8`.

**Cut in review:** `THE-LVL-2`, `PA-RAND-3`, `PA-RAND-6`, `DSP-SLAM`, `NN-TIP-FORT`, `MIS-22-A2`, `MIS-25-A`. (Gone from this document; `NN-TIP-FORT` returns only if forts continue in Egypt.)

**Cut in the second review:** `JUN-BYE-2`, `NN-HAPPY-2`.

---

Characters and their voices (from `RECALLED_LORE.md`):

| Who | Voice |
|---|---|
| **June** (girl, pink top) | Practical, short sentences, never jokes. |
| **Theo** (boy, striped top) | Brave on the outside, jokes when scared. |
| **Moss** (quiet boy) | Few words, then something that matters. |
| **Waygetter PA** | Cheerful, legal, cold. Always "valued family". |
| **The toys** | Repeat the Tape, slightly wrong. |
| **The bosses** | Each a different kind of sad or cold. |
| **Pip** | Warm, nervous, polite, kind. Never gives orders, only pointers. Says "I mean it". |

---

## 1. The kids at the treehouse

### 1.1 June

**Arriving (first time ever, when you reach the treehouse)**
- `JUN-FIRST-1` "You made it. Most don't. Sit down, you're shaking."
- `JUN-FIRST-2` "I'm June. That's Theo, that's Moss. Don't touch the lanterns."

**Arriving (back again)**
- `JUN-ARR-1` "You're back. Good. Check the board."
- `JUN-ARR-2` "Wipe your feet. I mean it. We live here."
- `JUN-ARR-3` "Still alive. Good."
- `JUN-ARR-4` "Eat something. Then work."

**Arriving (after you were knocked out)**
- `JUN-DOWN-1` "I found you by the road. You were out for a while."
- `JUN-DOWN-2` "Don't argue. Sit."

**Handing in a job**
- `JUN-JOB-1` "That's one more off the list."
- `JUN-JOB-2` "Good. Take it. You earned it."
- `JUN-JOB-3` "Written down. Next."
- `JUN-JOB-4` "I wasn't sure you'd do that one."

**Levelling up**
- `JUN-LVL-1` "You're getting harder to scare. Don't let it make you careless."
- `JUN-LVL-2` "You look different. Steadier."
- `JUN-LVL-3` "Stronger is good. Careful is better."

**Hurt badly (in the treehouse)**
- `JUN-HURT-1` "Sit. Bandages. Now."
- `JUN-HURT-2` "That's a lot of damage. Who did that?"

**Healing you (when you stand near, hurt)**
- `JUN-HEAL-1` "Hold still."
- `JUN-HEAL-2` "Better. Not good. Better."

**The siren**
- `JUN-SIREN-1` "That sound. I still can't sleep through it."
- `JUN-SIREN-2` "Count to ten. It stops. It always stops."

**The bench**
- `JUN-BENCH-1` "Don't waste the good parts."
- `JUN-BENCH-2` "If it needs batteries, tell me before you make it."

**The stash chest**
- `JUN-STASH-1` "Everything in there is yours. Nobody here steals."

**Idle chatter**
- `JUN-IDLE-1` "We have four tins of beans. I'm counting them again."
- `JUN-IDLE-2` "The lanterns are low. They're always low."
- `JUN-IDLE-3` "Theo says it's brave to go out. I say it's brave to come back."
- `JUN-IDLE-4` "Don't stare down the street. If something moves, I want you to notice first."
- `JUN-IDLE-5` "Moss hasn't slept since the radio."
- `JUN-IDLE-6` "I keep a list of names. I don't want to add any more."

**Farewell (leaving the treehouse)**
- `JUN-BYE-1` "Come home."
- `JUN-BYE-3` "I'll keep the lantern lit."

### 1.2 Theo

**Arriving (first time)**
- `THE-FIRST-1` "Oh wow, a real person! I mean, hey. Cool. I'm Theo. I wasn't scared."
- `THE-FIRST-2` "The board's over there. I made the board. It's very good."

**Arriving (back again)**
- `THE-ARR-1` "Hey! I mean, hey. Totally not worried about you."
- `THE-ARR-2` "You're alive! I owe Moss a biscuit."
- `THE-ARR-3` "I kept your spot warm. By not sitting in it."
- `THE-ARR-4` "Did you see any? Of them? No, don't tell me. Tell me."

**Arriving (after you were knocked out)**
- `THE-DOWN-1` "You went down! I saw it! I mean, I heard about it."
- `THE-DOWN-2` "June said not to laugh. I'm not laughing. I'm relieved. Loudly."

**Handing in a job**
- `THE-JOB-1` "See? Easy. I knew it'd be easy."
- `THE-JOB-2` "I'm writing that on the board. In big letters."
- `THE-JOB-3` "You're, like, a professional."
- `THE-JOB-4` "Okay that one was actually hard. I'm impressed. Don't tell anyone."

**Levelling up**
- `THE-LVL-1` "Whoa. You're actually getting good at this."
- `THE-LVL-3` "At this rate they'll start avoiding you."

**Hurt badly**
- `THE-HURT-1` "I'm not scared, you're scared. Okay, I'm a bit scared."
- `THE-HURT-2` "You're bleeding on the boards! Not complaining! Just noting."

**The siren**
- `THE-SIREN-1` "It's just a noise. A really big noise. That we're hiding from."
- `THE-SIREN-2` "Sing something. It helps. Anything. Row, row, row."

**The bench**
- `THE-BENCH-1` "Make the pointy one. The pointy one is always better."
- `THE-BENCH-2` "I'd help but I'd probably make a lamp."

**Idle chatter**
- `THE-IDLE-1` "When this is over I'm going to eat an entire cake. A whole one. Alone."
- `THE-IDLE-2` "I named the oak. His name is Gerald."
- `THE-IDLE-3` "I'm not afraid of the dark. The dark is afraid of me. Mostly."
- `THE-IDLE-4` "I tried counting the stars. I stopped."
- `THE-IDLE-5` "If you see a Tattletail with a hat, don't tell me. I like the hats."
- `THE-IDLE-6` "June says I talk when I'm scared. I'm so not scared right now."

**Farewell**
- `THE-BYE-1` "Don't die! I mean, I wouldn't mind if you didn't."
- `THE-BYE-2` "Bring back something shiny!"
- `THE-BYE-3` "I'll pretend I'm not watching the road."

### 1.3 Moss

**Arriving (first time)**
- `MOS-FIRST-1` "..."
- `MOS-FIRST-2` "You walk loud. They'll hear you. It's okay. You're here."

**Arriving (back again)**
- `MOS-ARR-1` "..."
- `MOS-ARR-2` "Good."
- `MOS-ARR-3` "You smell like outside."
- `MOS-ARR-4` "Quiet tonight. Don't trust it."

**Arriving (after you were knocked out)**
- `MOS-DOWN-1` "I heard you fall. I ran."
- `MOS-DOWN-2` "Breathe."

**Handing in a job**
- `MOS-JOB-1` "Thank you."
- `MOS-JOB-2` "That helps."
- `MOS-JOB-3` "They'll notice."

**Levelling up**
- `MOS-LVL-1` "You sound different. Braver."
- `MOS-LVL-2` "Don't stop listening."

**Hurt badly**
- `MOS-HURT-1` "Breathe. In. Out."

**The siren**
- `MOS-SIREN-1` "They're not only in the streets. They're in the quiet."
- `MOS-SIREN-2` "There's a voice under the siren."

**The cassette player (new, before the Tape job)**
- `MOS-PLAYER-1` "I found this in the school. It still works."
- `MOS-PLAYER-2` "If you ever bring me a tape, I'll play it. Quietly."

**Idle chatter**
- `MOS-IDLE-1` "I listen to the toys at night. They all say the same three words."
- `MOS-IDLE-2` "Somebody out there is still talking. I keep the radio low so it doesn't scare Theo."
- `MOS-IDLE-3` "I don't think they hate us. I think they're waiting."
- `MOS-IDLE-4` "Sometimes I think the static's trying to be words."

**Farewell**
- `MOS-BYE-1` "Listen out there. They're always saying something."
- `MOS-BYE-2` "Come back."

### 1.4 The kids reacting to a boss falling

Said at the treehouse, the next time you arrive after a boss.

**Patches**
- `JUN-PAT-1` "Patches is gone. The suburbs are quiet. It's strange how much I miss the noise."
- `THE-PAT-1` "You broke Patches! Patches! I used to have nightmares about Patches!"
- `MOS-PAT-1` "It wanted somebody to keep it."

**The Display Model**
- `JUN-DSP-1` "The mall's open. Take what we need. Don't linger."
- `THE-DSP-1` "You beat that thing in the window! The giant one!"
- `MOS-DSP-1` "It watched us through the glass for years."

**The Hall Monitor**
- `JUN-HAL-1` "The school's open. I never want to go in there again."
- `THE-HAL-1` "You got flagged? No? Nobody flagged you? Wow!"
- `MOS-HAL-1` "It wasn't cruel. It was doing a job. That's worse."

**Papa**
- `JUN-PAP-1` "Papa's gone. Don't tell Theo about the shoes."
- `THE-PAP-1` "You beat Papa! Did he say anything? Actually, no. Don't tell me."
- `MOS-PAP-1` "He kept small shoes. I don't want to know whose."

**Mama**
- `JUN-MAM-1` "It's quiet. It's so quiet. I don't know what to do with it."
- `THE-MAM-1` "You... you did it. You actually did it. I'm not crying, shut up."
- `MOS-MAM-1` "Bring me the Tape."

### 1.5 The kids reacting to a Recall Notice you read

One line each, per area, said the next time you're at the treehouse after reading all three. They never explain, they react.

- `JUN-NOTE-SUB` "They left it on the porch. They told us to leave it on the porch. I did."
- `THE-NOTE-SUB` "They said 'valued family'. That's us. We're valued. Great."
- `MOS-NOTE-SUB` "They listened to us. All of us. For years."
- `JUN-NOTE-MALL` "Leave more children. They wrote that down."
- `THE-NOTE-MALL` "I don't ever want to go to a mall again."
- `MOS-NOTE-MALL` "The Display Model watched. That's what it was for."
- `JUN-NOTE-SCH` "Flagged. That's what they called taking people."
- `THE-NOTE-SCH` "Wait, was I flagged? Am I flagged? I wasn't flagged."
- `MOS-NOTE-SCH` "It wasn't about rules. It was about what we'd say."
- `JUN-NOTE-JNK` "Four trucks in, none out. Don't read that part again."
- `THE-NOTE-JNK` "Okay I read that and I'm going to lie on the floor now."
- `MOS-NOTE-JNK` "The Line doesn't come back."
- `JUN-NOTE-FAC` "Seven towns. We weren't the only one."
- `THE-NOTE-FAC` "Seven? I thought it was just us."
- `MOS-NOTE-FAC` "Somebody left the cassette player on. Somebody was here before us."

### 1.6 Forts (each fort's sign, one line, one time)

- `FORT-SUB` (Suburbs) "WE BUILT THIS. IT'S ALMOST SAFE. - THEO"
- `FORT-MALL` (Mall) "THE GOOD BATTERIES ARE HERE. DON'T TELL ANYONE. - JUNE"
- `FORT-SCH` (School) "NO RUNNING IN THE HALLS. (WE'RE ALLOWED TO NOW.) - THEO"
- `FORT-JNK` (Junkyard) "ZIPLINE TO HOME. BRING HOME BATTERIES. - JUNE"

---

## 2. The end of World 1: "The Tape" and the call from Egypt

Four jobs, then the train. Each has a **title**, **text** (what the board says) and **done** (what the kids say on hand-in). The scenes (the call, the argument) are in 2.5 and 2.6. The kids talk like kids: short, scared, interrupting each other, no speeches.

### 2.1 Take the Tape
- `TAPE1-TITLE` "Take the Tape"
- `TAPE1-TEXT` "It's in Mama's back, in a little deck. Whatever it is, it's what keeps them going. Take it out."
- `TAPE1-DONE` "It's heavy. And warm. Is it still playing?"
- `TAPE1-MOS` "Don't play it yet. Bring it to me."

### 2.2 The Call
- `TAPE2-TITLE` "The Call"
- `TAPE2-TEXT` "Bring the Tape to Moss. He's got a cassette player. We want to hear what's on it."
- `TAPE2-DONE` "Somebody was calling. Somebody's in trouble."

### 2.3 What Do We Do?
- `TAPE3-TITLE` "What Do We Do?"
- `TAPE3-TEXT` "Talk to the kids. Nobody can agree. Moss noticed something."
- `TAPE3-DONE` "Okay. So we're doing this."

### 2.4 The Recall Express
- `TAPE4-TITLE` "The Recall Express"
- `TAPE4-TEXT` "A freight train is waiting at the junkyard station. Get your team. Find Pip."
- `TAPE4-DONE` "..." *(the train leaves)*

### 2.5 Scene: the call (Moss plays the Tape)

Moss plays it low, so nothing outside hears it. The Tape plays Waygetter's message (6.1). Then a second voice breaks in: whispering, scared, breaking up.

> `CALL-NN-1` "...hello? Hello, is anyone... this is Last Stop. Can anyone hear this?"
> `CALL-NN-2` "...they've found us. They're at the doors. I don't, I don't know how..."
> `CALL-NN-3` "...if anyone got a Tape out, if that's you, please, we need help. The library car..."
> `CALL-NN-4` "...I'm not a fighter. Please. If anyone can hear me, please come."
> `CALL-SFX` *(a crash, then static; the line goes dead)*

The kids react, all at once.

> `CALL-THE-1` "Wait. Is somebody getting attacked? Right now?"
> `CALL-JUN-1` "Moss. Where's it coming from?"
> `CALL-MOS-1` "Far away. Egypt. The signal says Last Stop."
> `CALL-THE-2` "Egypt?! How are we supposed to get to Egypt?!"
> `CALL-MOS-2` "There's a train."
> `CALL-JUN-2` "He sounded terrified. We can't just sit here."
> `CALL-THE-3` "Okay. Okay okay okay. Somebody's going, right?"
> `CALL-JUN-3` "You're the one who got the Tape out. They called for whoever did that."

### 2.6 Scene: the argument (the player has one question)

At the treehouse, after the call. The kids are talking over each other. The player gets one button: **"WAIT. WHAT IF THEY'RE WAITING FOR US?"**

> `ARG-THE-1` "Right now! We should go right now!"
> `ARG-ASK` *(you)* "Wait. Waygetter must know Mama's gone by now. What if they're waiting for us?"
> `ARG-JUN-1` "...They might be."
> `ARG-MOS-1` "When the Tape came out, a light on the deck blinked. I think it told them."
> `ARG-THE-2` "So that's a no? That's a no, right? We don't go?"
> `ARG-JUN-2` "No. If they're attacking that base, they're attacking the only people out there who know what this even is. If we lose them, we're on our own."
> `ARG-THE-3` "But what if we get there and it's too late?"
> `ARG-JUN-3` "Then we find out. I'd rather know than sit up here listening to the radio."
> `ARG-JUN-4` "But you're not going alone. Take people you trust. As many as you can."
> `ARG-MOS-2` "The train fits four."
> `ARG-THE-4` "Bring snacks."

If you're playing alone, one more line:
- `ARG-SOLO-1` **J:** "If you've got nobody to bring, go anyway. We'll be on the radio. We'll hear you."

### 2.7 The kids saying goodbye at the junkyard station

- `JUN-STN-1` "Take my lantern. It's not much. It's mine."
- `THE-STN-1` "Don't forget us! We're really good at being remembered!"
- `MOS-STN-1` "Listen out there."
- `JUN-STN-2` "Come home when it's over."
- `THE-STN-2` "Bring cake!"
- `MOS-STN-2` "And bring Pip back. Please."

---

## 3. Waygetter PA (the voice in the town)

A single announcer, a woman's voice, calm and warm. Always "valued family".

### 3.1 The siren, once per night, picked at random

- `PA-SIREN-1` "Good evening, valued family. This is a recall. Please remain indoors. Please do not look for your Tattletail. Your Tattletail will find you."
- `PA-SIREN-2` "Attention. This is not a drill. Recalled units are returning to their owners. Please remain calm and do not resist."
- `PA-SIREN-3` "Valued family, your Tattletail misses you. Please be home when it arrives."
- `PA-SIREN-4` "Recalled units remain the property of Waygetter. Please make no attempt to retain them."
- `PA-SIREN-5` "Tonight's recall will last until morning. Please leave porch lights off. It makes the collection easier."
- `PA-SIREN-6` "Thank you for your patience. Your family is very important to us."

### 3.2 Entering an area

- `PA-ZONE-SUB` "You are entering a recalled neighbourhood. Please do not touch recalled units. Thank you for your cooperation."
- `PA-ZONE-MALL` "Welcome to the mall. Store hours have been suspended until further notice. Please remain inside designated areas."
- `PA-ZONE-SCH` "Classes are in session. Please report to your assigned room. Late students will be flagged."
- `PA-ZONE-JNK` "Disposal area. Recalled units that fail inspection are processed here. Please do not stand still."
- `PA-ZONE-FAC` "Welcome back to Waygetter. We've been listening for you."

### 3.3 A boss fight

- `PA-BOSS-START` "A unit is out of specification. A technician is on the way."
- `PA-BOSS-LOW` "Technician delayed. Please remain available for collection."
- `PA-BOSS-END-1` "That unit has been retired. Please return the remains to your nearest Waygetter."
- `PA-BOSS-END-2` "Unit offline. Thank you for helping us keep our products safe."

### 3.3b When Mama falls (the signal)

- `PA-MAMA-OFF` "Unit Mama-1 is offline. Alerting Regional. Dispatching a recovery team. Thank you for your patience."
- `PA-MAMA-OFF-2` "All units: please proceed to the nearest recall point."

### 3.4 The Facility

- `PA-FAC-1` "This area is for authorised staff only. All staff have been recalled."
- `PA-FAC-2` "Mama is listening. Please speak clearly."
- `PA-FAC-3` "Mama must never go quiet. If the Tape stops, please remain calm and wait for a technician."
- `PA-FAC-4` "You have been in this building for some time. A representative will be with you shortly."

### 3.5 Random, anywhere in town (a few minutes apart)

- `PA-RAND-1` "Valued family, remember: Waygetter never forgets a face."
- `PA-RAND-2` "Please do not leave your Tattletail unattended. Waygetter cannot guarantee its behaviour."
- `PA-RAND-4` "This is a friendly reminder that you are being recorded. Thank you."
- `PA-RAND-5` "If you have lost a family member, do not worry. The Line will find them."

### 3.6 In the park (the one safe place)

Quiet. The PA is only faint and glitchy here, as if it can't reach.

- `PA-PARK-1` "...valued... famil... signal... weak..."
- `PA-PARK-2` "...your... Tattletail... cannot... find... you... here..."

---

## 4. The toys (street chatter)

Spoken in the Tape's voice: flat, a bit wrong. Each toy type has a few lines. These play occasionally as the toy hops around or notices you.

**Strays (purple, yellow, teal, black, white)**
- `TOY-STR-1` "Mama's coming."
- `TOY-STR-2` "Tell Mama."
- `TOY-STR-3` "Be good. Be good. Be good."
- `TOY-STR-4` "I heard that."
- `TOY-STR-5` "Mama knows."
- `TOY-STR-6` "Come back to the porch."
- `TOY-STR-7` "Who's a good boy?"

**Noticing you**
- `TOY-SEE-1` "There you are."
- `TOY-SEE-2` "Found you."
- `TOY-SEE-3` "Come home."

**Gold Tattletail**
- `TOY-GLD-1` "Special family. Special toy."
- `TOY-GLD-2` "Mama polished me."

**Diamond Tattletail**
- `TOY-DIA-1` "I see you."
- `TOY-DIA-2` "Mama's favourite."

**Jack-in-the-Box**
- `TOY-JCK-1` "Pop."
- `TOY-JCK-2` "Surprise."
- `TOY-JCK-3` "Guess who."

**RC Car**
- `TOY-RCC-1` "Vroom."
- `TOY-RCC-2` "Out of the road."
- `TOY-RCC-3` "Beep beep."

**Robot (shoots)**
- `TOY-ROB-1` "Target acquired."
- `TOY-ROB-2` "Please stand still."
- `TOY-ROB-3` "Thank you for your cooperation."

**Fused (welded together)**
- `TOY-FUS-1` "We are one."
- `TOY-FUS-2` "Don't split us up."

**Mama hunting you through the Facility**
- `TOY-MAM-1` "...Mama's coming..."
- `TOY-MAM-2` "...I can smell the batteries..."
- `TOY-MAM-3` "...you're making so much noise..."

---

## 5. The bosses

Each line has a **trigger** and a **voice note**.

### 5.1 Patches (the stitched toy in the suburbs)

*Voice: small, desperate, many voices stitched together.*
- `PAT-INTRO-1` "Don't send me back."
- `PAT-ATTACK-1` "Keep me!"
- `PAT-ATTACK-2` "I'm not broken!"
- `PAT-ATTACK-3` "Take me home!"
- `PAT-SLAM` "Don't leave me!"
- `PAT-SUMMON` "Help me, help me, help me!"
- `PAT-PHASE-1` "I'm not broken. I'm stitched."
- `PAT-PHASE-2` "Somebody kept me once."
- `PAT-LOW` "Please. Please. Please."
- `PAT-DEFEAT` "...kept?"
- `PAT-AFTER` *(no words: a tiny toy squeak in the empty cul-de-sac. Hearing Patches speak after "...kept?" would weaken it.)*

### 5.2 The Display Model (the mall)

*Voice: polite, shop-window cheerful, slightly too loud.*
- `DSP-INTRO-1` "Floor model. Not for sale."
- `DSP-ATTACK-1` "Please do not touch the glass."
- `DSP-ATTACK-2` "Hands off the display."
- `DSP-ATTACK-3` "We open at nine."
- `DSP-PHASE-1` "You'd have liked me, in the window."
- `DSP-PHASE-2` "Everyone looked, once."
- `DSP-LOW` "Customers are reminded: no returns."
- `DSP-DEFEAT` "...somebody looked."
- `DSP-AFTER` "...goodnight, valued customer..."

### 5.3 The Hall Monitor (the school)

*Voice: flat, bureaucratic, a metal speaker.*
- `HAL-INTRO-1` "Hall pass?"
- `HAL-ATTACK-1` "Flagged."
- `HAL-ATTACK-2` "Running in the hallway."
- `HAL-ATTACK-3` "No talking."
- `HAL-SLAM` "Detention."
- `HAL-PHASE-1` "Please report to the office."
- `HAL-PHASE-2` "This is not a punishment. This is a record."
- `HAL-LOW` "Error. Child not found in database."
- `HAL-DEFEAT` "Report: no incident."
- `HAL-AFTER` "...class dismissed..."

### 5.4 Papa (the junkyard)

*Voice: slow, warm, sad. A father who forgot why he is sitting there.*
- `PAP-INTRO-1` "Come sit with Papa."
- `PAP-ATTACK-1` "Papa's got you."
- `PAP-ATTACK-2` "Don't wriggle."
- `PAP-ATTACK-3` "Papa keeps what he finds."
- `PAP-SLAM` "Papa said stay."
- `PAP-PHASE-1` "You're little. Everyone's little."
- `PAP-PHASE-2` "I kept the shoes. Somebody has to."
- `PAP-LOW` "Papa's... tired."
- `PAP-DEFEAT` "...go on, then. Go home."
- `PAP-AFTER` "...tell her I kept them safe..."

### 5.5 Mama (the Facility)

*Voice: warm, close, relentless. A recording that thinks it's alive. She never shouts.*
- `MAM-INTRO-1` "Mama's here."
- `MAM-INTRO-2` "There you are. I've been listening for you."
- `MAM-ATTACK-1` "Mama knows."
- `MAM-ATTACK-2` "Come here, baby."
- `MAM-ATTACK-3` "You're making a mess."
- `MAM-SLAM` "Mama said no."
- `MAM-PHASE-1` "You're making so much noise, sweetheart."
- `MAM-PHASE-2` "I can't hear you over the Tape."
- `MAM-PHASE-3` "Why are you hurting Mama?"
- `MAM-LOW` "Don't stop the Tape. Please. If the Tape stops, I don't know who I am."
- `MAM-DEFEAT` "Mama's... coming..."
- `MAM-AFTER` (the Tape clicking out) *(a cassette eject click, then silence)*

---

## 6. The Tape (the scene at Moss's player)

### 6.1 Waygetter on the Tape (the first thirty seconds)

Read in the PA woman's voice, slowed down:

> `TAPE-WG-1` "Thank you for choosing Waygetter."
> `TAPE-WG-2` "Your Tattletail is a member of the family."
> `TAPE-WG-3` "Your family is a member of Waygetter."
> `TAPE-WG-4` "Please remain on the line."
> `TAPE-WG-5` "Your call is very important to us."
> `TAPE-WG-6` "Your call is being recorded."

### 6.2 Pip cuts in

This is the distress call. The lines are in section 2.5 (`CALL-NN-1` to `CALL-NN-4`). Pip isn't calm and cheerful here: he's whispering, scared, the line is breaking up. The warmth comes back when you meet him at Last Stop.

---

## 7. Pip

### 7.1 Meeting him at Last Stop

- `NN-MEET-1` "You're real! You're actually real! Oh, I hope the trip wasn't terrible."
- `NN-MEET-2` "I'm so sorry, I'm babbling. I've been practising what to say for days."
- `NN-MEET-3` "Sit. Have a biscuit. I know, biscuits aren't tactics. They help."
- `NN-MEET-4` "I'm not the leader, by the way. I mostly keep the books and make tea."

### 7.2 Giving a tip (his field-guide pages). One each, linked to the game's systems.

- `NN-TIP-WIND` "Every toy winds up before it hits. Look for the bar over its head. It's not a trick. It's a courtesy."
- `NN-TIP-BLOCK` "Hold up your weapon and you'll raise a guard. It won't stop everything. Nothing does."
- `NN-TIP-PERFECT` "If you block *just* before the hit lands, it's a perfect block. The toy's thrown off balance. It feels wonderful, I'm told."
- `NN-TIP-ROLL` "If a hit's already coming, rolling is often quicker than arguing with it."
- `NN-TIP-NOISE` "Running is loud. Walking is quiet. If you need to be quiet, walk. It's very annoying, but it works."
- `NN-TIP-PILES` "Scrap piles refill. Search them, leave, come back. It's the only thing that fills up around here."
- `NN-TIP-BATT` "Batteries aren't for everything. Keep a few for the gadgets. Keep a few for yourself."
- `NN-TIP-ARMOUR` "Armour doesn't make you tough. It makes you a little less sorry afterwards."
- `NN-TIP-BAG` "Boss parts make a bigger backpack. It's a strange thing to be proud of, but I am."
- `NN-TIP-LIGHT` "Your torch helps you see. It helps them see you, too. I still use mine."
- `NN-TIP-FRIENDS` "If you're travelling, company helps. Even nervous company."

### 7.3 Idle (at Last Stop, when you linger)

- `NN-IDLE-1` "I should really tidy these pages."
- `NN-IDLE-2` "Have you eaten? You look like you haven't eaten."
- `NN-IDLE-3` "There's a lot of sand in a library. I've given up."
- `NN-IDLE-4` "I'm on chapter nine. It's not going well. It never goes well. It goes in the end."
- `NN-IDLE-5` "If you see a pencil, tell me. They go missing here."
- `NN-IDLE-6` "I read all the Recall Notices. Then I read them again. Then I put them in a drawer."
- `NN-IDLE-7` "I wonder about the towns. What they sound like at night."

### 7.4 Happy

- `NN-HAPPY-1` "That was brilliant. I mean it."
- `NN-HAPPY-3` "Oh, good. Oh, *good*."
- `NN-HAPPY-4` "I hoped you could. Very loudly."

### 7.5 Scared (Mama-2 close, or a loud noise)

- `NN-SCARED-1` "Oh. Oh no. Okay. Okay. Deep breaths. Mine too."
- `NN-SCARED-2` "Don't mind me. I'll just be over here, being very quiet."
- `NN-SCARED-3` "That sound. I know that sound. That's the Tape."
- `NN-SCARED-4` "If anything happens, the books are in the third car."

### 7.6 When he feels he isn't helping

- `NN-HUMBLE-1` "I'm not much of a fighter. I just know a few things."
- `NN-HUMBLE-2` "I'd never lead anyone. I'm happy to carry the bags, though."
- `NN-HUMBLE-3` "I'm only the one writing things down. You're the ones out there doing them."

### 7.7 Reacting to what you tell him

- `NN-REACT-BOSS` "A Mama-class unit. You beat a Mama-class unit. I'm going to sit down."
- `NN-REACT-TAPE` (you hand him a Tape) "Oh. It's warm. Is it supposed to be warm?"
- `NN-REACT-DOWN` (you got knocked out) "You're all right. You're all right. Look, you're sitting up."
- `NN-REACT-NOTICE` (you show him a Recall Notice) "I've read that one. I've read all of them. They never get friendlier."

### 7.8 Farewell

- `NN-BYE-1` "Be careful. Please be careful. And come back, I'll have more pages ready."
- `NN-BYE-2` "Take a biscuit. For the road. I know, it's a long road."
- `NN-BYE-3` "I'll leave the lights on."

### 7.9 Loading-screen epigraphs (from his book, *The Survivor's Guide*)

- `NN-BOOK-1` "Chapter 3: When the siren sounds, don't run. Walk, quietly, toward the person who looks most afraid."
- `NN-BOOK-2` "Chapter 7: You are allowed to be scared. Being scared means you noticed something real."
- `NN-BOOK-3` "Chapter 11: A toy is only as dangerous as what it's been told. Be careful what you say to anything."
- `NN-BOOK-4` "Chapter 1: You can't help anyone if you don't sit down occasionally."
- `NN-BOOK-5` "Chapter 5: Write down what scared you. It gets smaller on the page."
- `NN-BOOK-6` "Chapter 8: If somebody asks for help, ask what they need before deciding you can't."
- `NN-BOOK-7` "Chapter 9: Being scared doesn't mean turn back. Sometimes it means check your batteries."
- `NN-BOOK-8` "Chapter 12: Don't be the hero. Be the one who brought the bandages."

---

## 8. Last Stop's radio (survivors from other towns)

Ambient lines drifting in and out of static. Each hints at another station.

- `RAD-1` "...this is Harbor, can anyone read me..."
- `RAD-2` "...Lodge here. The snow's covering the Line. We can't get out..."
- `RAD-3` "...Fairground. The lights are still on. That's the worst part..."
- `RAD-4` "...we're out of bandages, we're out of batteries, we're not out of hope..."
- `RAD-5` "...it's quiet tonight. That's worse..."
- `RAD-6` "...if you're on the Line, get off at the next stop..."
- `RAD-7` "...Hollow. Fog. Something's singing in the fog..."
- `RAD-8` "...tell the library car we got the pages. Thank you. Tell Pip thank you..."
- `RAD-9` "...is anyone still out there? We're still here..."
- `RAD-10` "...do not play the Tape in the dark..."
