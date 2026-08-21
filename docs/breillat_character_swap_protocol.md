# Breillat Character Swap Protocol

## Overview
Documents the reverse-engineered secret quest and character transformation mechanism from `brelliatlayerdegistirdim.pcapng` and `Eve.emg` Event 4 (Map 10027 NPC ClickID 5 - Breillat).

---

## Quest Mechanism & Conditions
1. **Talk Count Trigger**:
   - Talking to Breillat 10 times with a new character (without vouchers) triggers the secret swap branch (`Sub #6`).
2. **Dialogue Sequence**:
   - `0x0777B0` (30640): Breillat complains about maid duties on the ship.
   - `0x0777B1` (30641): Breillat proposes trading places with the player.
   - `0x0777B2` (30642): Dialog confirmation.
   - `AC 20:1 [Step 4, SubCode 6]`: Interactive Choice Prompt (Yes / No).
3. **Player Choices (`AC 20:9`)**:
   - **Option 0x1E (30 - Yes)**:
     - Triggers `Sub #8`:
     - Dialogue `0x0977B4` (30644) + `0x0977B6` (30646).
     - Despawns Breillat NPC #5 from map via `AC 22:10 [05 00 FF FF]`.
     - Completes Quest #50030, Starts Quest #50031.
     - **Opcode 12 (Character Transformation)**:
       - BodyStyle set to `Big_Female`, HairStyle to `Breillat`.
       - Character visual model transformed to Breillat (`TemplateID 13`) via `AC 5:12 [CharID 4B, 13 1B]`.
       - Grants starting Maid Dress (`#21991`) and Maid Shoes (`#21009`).
       - Plays fanfare `AC 20:10`.
   - **Option 0x1F (31 - No)**:
     - Triggers `Sub #9`:
     - Dialogue `0x0A76F0` (30448): Decline dialogue, retains original character.

---

## Code References
- Handled dynamically in [`EveEventInterpreter.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs).
- Choice packet handling in [`AC20.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC20.cs) via `player.OnDialogueChoice`.
