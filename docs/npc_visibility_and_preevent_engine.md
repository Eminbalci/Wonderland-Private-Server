# NPC Visibility and PreEvent Dynamic Engine Protocol

## 1. Overview
In Wonderland Online, map NPC visibility is driven by two layers:
1. **Scene Base Initialization (`AC 22:4`)**:
   - Transmits initial NPC list with `state = 0x0000` (normal active map NPC).
   - Recruited companion NPCs and permanently broken chests/objects are flagged `0xFFFF` (hidden).

2. **Dynamic Storyline PreEvents (`Eve.emg` PreEvents bytecode via `AC 22:10`)**:
   - Evaluated dynamically per player upon entering any map.
   - Evaluates multi-chunk 7-byte condition blocks:
     - `Opcode 0x05`: Quest Mark/Flag conditions (`flagId`, `reqValue`, `compType`). Unstarted/default quest marks are `0`. Completed is `2`.
     - `Opcode 0x02`: Recruited Pet/Companion checks (`subType 2`, `petId`). Checks if player has recruited the pet (e.g. S.Monkey `17162`, Robinson `12032`).
     - `Opcode 0x01`: Always True / Unconditional branch.
   - Dispatches authentic 2-byte state actions from `data[8]` and `data[9]`:
     - `0xFF 0xFF`: Despawn / Hide NPC (e.g. unstarted quest actors, finished event NPCs).
     - `0x00 0x00`: Reveal / Spawn NPC.
     - `>0x00 0x00`: Visual / Animation state transition.

## 2. Issues Diagnosed & Resolved
1. **Unstarted Quest Flag Value Inversion**:
   - `GetPlayerFlagValue` previously defaulted to `2` for unstarted quests. Because `Flag == 2` represents "Completed" in WLO PreEvents (which triggers hide actions for recruited actors), default unstarted characters had actors like S.Monkey (`17162`) mistakenly hidden.
   - Default unstarted quest mark value corrected to `0`.

2. **Opcode 0x02 Companion Recruitment Evaluation**:
   - Implemented opcode 0x02 subType 2 evaluation to accurately inspect `player.PlayerPets` and recruited companion records.

3. **PreEvent Multi-Condition Truncation & Byte Alignment**:
   - Iterates through all 7-byte condition chunks. All non-zero condition chunks in a branch must evaluate to true.
