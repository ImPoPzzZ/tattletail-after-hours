# Recalled: lore lines before and after every job (for review)

*Made By: ImPoPzzZ & Sa1monfr. Written 2026-10-01 for 3.6. In the game since 3.6, as text only (no voices): the kids, Waygetter's announcements, the toys, the bosses, the Tape scenes and the notices. To change a line, change it here and keep its ID (the game's text is made from this file by tools/gen_voice.py and tools/gen_lines.py). Not in yet, waiting for Egypt: Pip's lines, Last Stop's radio and the book epigraphs.*

## How they'd show up in the game

Every job already has two lines: **TEXT** (what the board says) and **DONE** (what the kids say when you hand it in). These new lines add the story around them.

- **BEFORE** (`MIS-nn-B`): shown on the quest board under the job's title, and at the top of the J screen. A kid explains why the job matters in this town.
- **AFTER** (`MIS-nn-A`): shown once when you hand the job in, after the existing DONE line. The kids work out a little more of the truth.

Each is one to three short sentences by one kid (**J** = June, **T** = Theo, **M** = Moss; placeholder names). They know less than the player: Moss connects the clues, June knows the rules, Theo says what everyone is thinking.

## What the kids know, and when (the pacing of the reveal)

| Area | Jobs | The kids say / learn |
|---|---|---|
| The treehouse | 1 | There was a recall. The adults went on trucks "for safety". The toys came back wrong. The park is quiet. |
| The suburbs | 2-5 | Toys are made of parts, and they listen. A toy can be made to remember. Patches is the toys Waygetter threw away. |
| The mall | 6-9 | Waygetter wanted families in the shop. The lanterns keep the toys off. The Display Model watched. The trucks came to the loading dock. |
| The school | 10-13 | The Listening Program: a Tattletail on every teacher's desk. Every toy says the same thing: a recording. "Flagged" means "collected". |
| The junkyard | 14-17 | Waygetter sent everything that failed here. "Special families" got the gold ones. Papa keeps small shoes and says "tell her I kept them safe". |
| The Facility | 18-21 | The Recall Line is a belt and a railway. "Recall" means they want it all back. Mama is a recording in a deck. There are seven towns. |
| The Tape | 22-25 | The Tape's blink told Waygetter. A distress call from Egypt: the Resistance's base is under attack. It might be a trap. They decide to go. |

---

## The first world

### 1. The Treehouse *(Talk)*
- `MIS-01-B` **J:** "Main Street's the only road with lamps still lit. Follow it to the park. We're up in the oak. Don't run. They hear running."
- `MIS-01-A` **T:** "Welcome to the treehouse! It used to just be a treehouse. Now it's the only place the siren doesn't reach."
- `MIS-01-A2` **J:** "Most don't make it up Main Street at night. You did. Take the bat and the lid. Both help."

### 2. Strays *(Kill 8, the suburbs)*
- `MIS-02-B` **T:** "They used to be Tattletails. You know, the toy everybody had. Then the recall came and they came back wrong. They don't sleep. They don't stop. Break eight, and the street gets quieter."
- `MIS-02-A` **J:** "Eight fewer. It won't stop them coming back, but it buys us a night. Remember: they're not angry. They're doing what they were told."

### 3. Make Something *(Craft a Slingshot)*
- `MIS-03-B` **M:** "A toy is a bag of parts: springs, plastic, screws. Waygetter built them to come apart and go back together. Useful, for once. Take what they drop and make something."
- `MIS-03-A` **T:** "That sound! The whole street heard it. They hear everything. Maybe that's what they were made for."

### 4. The Garages *(Elite: a Jack-in-the-Box)*
- `MIS-04-B` **J:** "The Hendersons' garages. They kept the gift boxes in there: toys nobody unwrapped. One did itself. It jumps out at anyone who walks past."
- `MIS-04-A` **M:** "Springs snap back to where they started. Toys do too, if they're told often enough."

### 5. Patches *(Boss, the suburbs)*
- `MIS-05-B` **M:** "At the end of Oak Lane there was a Returns bin, where unwanted toys were left. Years of them. Something stitched them together. It's keeping every toy anyone ever gave back. Be careful. It isn't cruel. It's lonely, and very big."
- `MIS-05-A` **J:** "Gone. It was only holding on to what Waygetter threw away. It's strange to be sad about that. The mall's barricade will open for you now."

### 6. Shoplifters *(Kill 12, the mall)*
- `MIS-06-B` **T:** "The mall's where we got batteries. It's all still in there. Waygetter had a Tattletail booth in the middle, and people left their kids at it for hours. I'm just saying that's what they did."
- `MIS-06-A` **J:** "Twelve fewer. Check the loading dock sometime. That's where the trucks came for the adults. I don't want to talk about it."

### 7. Charge *(Bring 10 batteries)*
- `MIS-07-B` **J:** "The lanterns are old Waygetter safety lamps. They hum at a pitch the toys hate. When they go dark, so do we. Bring ten batteries."
- `MIS-07-A` **M:** "Listen. They hum. It's the only sound the toys won't go near. Whoever made them knew."

### 8. Hit and Run *(Elite: 2 RC Cars)*
- `MIS-08-B` **T:** "RC cars. The delivery kind. Waygetter made them to bring the toys to your door. Now they just run people over. Two in the lot."
- `MIS-08-A` **J:** "Delivery cars. That's what they were for: bringing toys. The collecting was done by the trucks."

### 9. The Display Model *(Boss, the mall)*
- `MIS-09-B` **M:** "In the shop window there was a giant Tattletail. The Display Model. It was never for sale. It was for watching: shoppers, children, anyone who looked. It watched people. Maybe it remembered them."
- `MIS-09-A` **T:** "Three metres of fur and glass, and you broke it. It watched us for years. I'm not scared of windows anymore. Mostly."

### 10. Detention *(Kill 14, the school)*
- `MIS-10-B` **J:** "The school's dark now. Waygetter put a Tattletail on every teacher's desk. It was called the Listening Program. They recorded all of us."
- `MIS-10-A` **M:** "Nobody's hiding in the lockers. I used to think the toys were hiding. They weren't. They were waiting for us to say something."

### 11. Voices *(Bring 6 voice boxes)*
- `MIS-11-B` **M:** "Every Tattletail has a voice box. If I know what they're saying, I know what they've been told. Bring me six."
- `MIS-11-A` **M:** "Six boxes. Six toys. The same voice, the same words: 'Mama's coming.' It isn't them talking. It's something playing through them."
- `MIS-11-A2` **J:** "A recording?" **M:** "A recording of Mama."

### 12. Hall Pass *(Elite: 2 Robots)*
- `MIS-12-B` **T:** "Robots walk the halls. They don't hurt kids. They record kids. If they catch you running, you get 'flagged'. I knew a boy who got flagged. He isn't around."
- `MIS-12-A` **J:** "'Flagged' means 'on the list for collection'. That's the word on the forms. I found one in the office. The list is long."

### 13. The Hall Monitor *(Boss, the school)*
- `MIS-13-B` **M:** "In the gym there's a tall robot with a searchlight. It's the one that flags. It isn't cruel. It watches until someone breaks a rule, then writes it down. It has never once stopped."
- `MIS-13-A` **T:** "It said 'Report: no incident.' Just like that. I think it was relieved. The junkyard's open now."

### 14. Scrap *(Kill 16, the junkyard)*
- `MIS-14-B` **J:** "Waygetter sent everything that failed to the junkyard. Cars, toys, whole trucks. There's a rail line through the middle, to a station. Lots of scrap and lots of toys. Break sixteen."
- `MIS-14-A` **T:** "You're getting good at this. Don't let it go to your head. I did that once. It went badly."

### 15. Gold *(Elite: a Gold Tattletail)*
- `MIS-15-B` **M:** "Waygetter made some Tattletails in gold. Collector's editions, kept for 'special families'. One got out."
- `MIS-15-A` **J:** "Real gold fur. 'Special families.' Waygetter had lists for everything."

### 16. Welded *(Elite: 2 Fused toys)*
- `MIS-16-B` **T:** "Somebody welded toys together by the crusher. That isn't a toy thing to do. Whoever did it is further in. Don't go alone."
- `MIS-16-A` **M:** "Welding takes hands. Papa's heavy, not clever. Either somebody helped him, or somebody used to."

### 17. Papa *(Boss, the junkyard)*
- `MIS-17-B` **J:** "Papa's the last one before the warehouse. Waygetter built him to clear what the siren leaves behind: the recalled things nobody wanted. He sits on the scrap and keeps what he finds. I've seen the shoes. Please don't tell Theo about the shoes."
- `MIS-17-A` **M:** "He said 'tell her I kept them safe.' Tell who?" **J:** "I don't know."

### 18. The Warehouse *(Reach the Facility)*
- `MIS-18-B` **T:** "At the top of Main Street there's a big gate. The Waygetter Facility. Mama's in there. If the toys are listening to anyone, they're listening to her. Please be quiet."
- `MIS-18-A` **J:** "You got inside. None of us ever did. The kids who tried, early on... we don't talk about them."

### 19. The Recall Line *(Kill 14, the Facility)*
- `MIS-19-B` **M:** "Inside there's a long belt. Toys come off the trucks at one end and pass beneath the Tape system at the other. That's the Recall Line. The trains bring them. The belt sends them. Break fourteen of its guards."
- `MIS-19-A` **T:** "Done. The Line's quiet. I always thought a 'recall' meant they fix the toy. It means they want their stuff back. All of it."

### 20. Diamond *(Elite: a Diamond Tattletail)*
- `MIS-20-B` **J:** "A Diamond Tattletail in the display hall. The top of the range. The eye alone could make armour. It looks at you like it knows what you had for dinner."
- `MIS-20-A` **T:** "It's beautiful. It's also still looking at me. Moss, put a cloth over it."

### 21. Mama *(Boss, the Facility)*
- `MIS-21-B` **M:** "I don't think she's a monster. I think she's a recording. There's a deck in her back. If the Tape's where the voice comes from, maybe stopping it changes something."
- `MIS-21-A` **J:** "It's over. For tonight. Wait... what's that light?"
- `MIS-21-A2` **M:** "There's a file in her chamber. Seven towns. We were number one."

---

## The new chain: The Tape and the call from Egypt (the end of the first world)

The beat: after Mama, news arrives that the Resistance's base in Egypt is under attack. The kids panic and send you to find Pip. You ask what if Waygetter is waiting for you (they must know a Mama went offline). They decide to go anyway and tell you to get your team, because losing the base would leave everyone on their own. The scenes' full dialogue is in `RECALLED_VOICE_LINES.md` section 2.

### 22. Take the Tape *(new, a Boss-drop job)*
- `MIS-22-B` **M:** "It's in her back, in a little deck. It's what keeps them going. Take it out and bring it here."
- `MIS-22-A` **J:** "It's heavy. And warm. Don't play it yet. Give it to Moss."

### 23. The Call *(new, a Talk job)*
- `MIS-23-B` **M:** "I'll play it low, so nothing outside hears it. It might tell us something."
- `MIS-23-A` **J:** "Somebody was calling. They're being attacked right now. They were calling for whoever took this Tape."

### 24. What Do We Do? *(new, a Talk job with one question)*
- `MIS-24-B` **T:** "We have to go! Right? We have to go. Why is everybody quiet? Why is June doing the face?"
- `MIS-24-A` **J:** "We go. If we lose that base, we're on our own out here. Bring your team. Everyone you trust."

### 25. The Recall Express *(new, gather your team and board)*
- `MIS-25-B` **M:** "A freight train is waiting at the junkyard station. It fits four. Bring as many as you can."

---

## What I need from you

1. Go through the IDs. Reply with the ones to **cut**, **change** or **add**.
2. Anything the kids shouldn't know yet (they reveal a lot by the school and the junkyard).
3. The new ending: do the kids sound like real kids now (panic, one worried question, then a decision)?
4. Are two lines per job enough, or do you want a third (the kid that isn't talking reacts)?
5. The speaker for each is a suggestion: tell me if a line sounds like a different kid.
6. Where to show them? My plan is the quest board and the J screen; tell me if you want them as a pop-up as well.
