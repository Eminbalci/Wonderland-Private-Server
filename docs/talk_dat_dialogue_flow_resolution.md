# Talk.dat Dialogue Flow Resolution & Eve Opcode Decoding

Technical specifications for Talk ID lookup, Talk.dat binary decoding, Eve bytecode translation, and event interpretation in the **Map Events & Dialogue Flow Studio** and runtime server engine.

---

## 1. Background & Root Cause Analysis

### Eve vs English Talk.dat Localization Divergence
1. **Original Eve Bytecode Compilation**:
   - `Eve.emg` bytecode scripts were compiled against the original Taiwanese/Chinese version of `Talk.dat`.
   - Event opcodes contain raw dialogue IDs (`20304..20326`, `28422..28427`, `30120..30145`, `30647..30692`, `30973..30980`, etc.).
2. **English Talk.dat Re-Indexing & Header Collisions**:
   - In the localized English `Talk.dat` (17,494 records of 292 bytes each), dialogue categories were re-ordered and translated:
     - **Robinson Beach & Guide (Map 10035/10036 Event 8)**: Records `#1400..#1435` (Talk IDs `41919, 41916, 41917, 41906, 41907...`).
     - **Robinson Companion Recruitment (Map 10036 Event 19)**: Records `#10863..#10874` (Talk IDs `29406, 29407...`).
     - **Ship Cabin & Deck NPCs (Map 10001, 10017)**:
       - `30120`: Record `#7021` (Gentleman: *"When the sea wind is blowing, it feels great."*)
       - `30124`: Record `#7054` (Sailor Collision: *"Captain, we've collided with something just now and water is flowing into the boiler room. Our ship is going to sink."*)
       - `30125`: Record `#7055` (Captain Collision Response: *"What... What!"*)
       - `30126`: Record `#7026` (Visitor: *"I did not expect to be able to travel around the world."*)
       - `30128`: Record `#7034` (Crew: *"Please enjoy this journey with your heart."*)
       - `30130`: Record `#7015` (Jack: *"I'm flying, Jack! It's wonderful..."*)
       - `30131`: Record `#7020` (Bellman: *"Contact our service staff anytime if you have any question or suggestion. Wish you a pleasant journey."*)
       - `30137`: Record `#7032` (Persian Cat: *"`#swav1541/#sMeow~`"*)
       - `30138`: Record `#7033` (Girl: *"I've been looking forward to this journey. What about Kedi?"*)
       - `30141`: Record `#7023` (Crew: *"I'm sailor here. Please inform us if you are not satisfied with our service."*)
       - `30142`: Record `#7022` (Crew on Deck: *"Have a pleasant journey."*)
     - **Kelan Village Xaolan, Grandma, Hijackers, Mayor, and Roca (Map 12001, 12002)**: Records `#7550..#7587` (Talk IDs `39726, 39712, 39742...`).

---

## 2. Updated Lookup Pipeline (`PhxTalkDat.cs`)

```
[ Incoming idOrOffset / TalkID ]
               │
               ▼
    1. Canonical Storyline & Eve Script Translation Table
       ├── Robinson Tutorial & Guide (20304..20326, 20354..20355) ──► Records 1400..1427
       ├── Robinson Pet Recruitment (28422..28435)                 ──► Records 10863..10876
       ├── Ship Cabin & Deck NPCs (30120..30142 per-ID table)      ──► Records 7015..7055
       ├── Kelan Village Xaolan & Grandma (30647..30692)           ──► Records 7566..7587
       ├── Kelan Mayor & Roca (30973..30980, 31146)                ──► Records 7555..7561, 7586
       └── NO MATCH
               │
               ▼
    2. Binary byte offset in `_talkByOffset`?
         ├── YES ──► Return dialogue text
         └── NO
               │
               ▼
    3. Record index (0..17494) in `_talkByIndex`?
         ├── YES ──► Return dialogue text
         └── NO
               │
               ▼
    4. Eve Chapter Base Offset Subtraction (`id - 60000`, `id - 50000`, `id - 40000`, `id - 30000`, `id - 20000`) in `_talkByIndex`?
         ├── YES ──► Return dialogue text
         └── NO
               │
               ▼
    5. Direct `_talkById.TryGetValue(idOrOffset)` (Authentic raw Talk.dat headers)
         ├── YES ──► Return dialogue text
         └── NO
               │
               ▼
    6. Masked 16-bit `pureId = idOrOffset & 0xFFFF` in `_talkById`
```

---

## 3. Eve Bytecode Item Grant vs Quest Flag Separation

### Opcode 1 (`DialogPtr = 1`)
- **Real Item Grant**: `dialog1 == 1 && dialog3 > 0`
  - Item ID: `dialog3` (e.g. `32075` = Space Remote, `32074` = Design Plan, `32032` = Wood Crate, `41066` = Coconut, `48016` = Bamboo Raft).
  - Count: `Math.Max(1, (int)dialog2)`.
  - Action: Invokes `player.Inv.AddItem((ushort)dialog3, (byte)count)` + sends Fanfare Sound `AC 20:10` and inventory sync `AC 23:5`.
- **Quest Flag Set**: `dialog1 == 2 && dialog3 >= 10000` $\rightarrow$ `🚩 [QUEST FLAG]: Set Flag #dialog3 -> Step dialog2`.
- **Player / NPC Dialogue**: `dialog2 > 0` or `dialog3 > 0` $\rightarrow$ Spoken Dialogue line.

### Opcode 5 (`DialogPtr = 5`)
- **Quest Flag Tracker**: `dialog1 >= 12000 && dialog1 < 20000`
  - `12000..19999` are internal WLO Quest Flag tracking variables (e.g. `12040`, `12041`, `12026`, `12027`, `15282`, `15283`).
  - State: `dialog2 == 2 || dialog4 >= 32768` $\rightarrow$ `Completed`, `dialog2 == 1` $\rightarrow$ `In-Progress`.
  - Suppressed from real item grant/consume lists to avoid displaying or giving weapon index collisions (e.g. `War Bronze Spear (12040)`).
- **Real Item Consume / Grant**: `dialog1 < 12000 || dialog1 >= 20000`
  - `dialog2 == 2` $\rightarrow$ `🔻 [CONSUME ITEM] Item #dialog1 x dialog3`.
  - `dialog2 == 1` $\rightarrow$ `🎁 [GRANT ITEM] Item #dialog1 x dialog3`.
