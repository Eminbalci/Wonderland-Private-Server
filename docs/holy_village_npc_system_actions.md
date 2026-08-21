# Holy Village & Map NPC System Actions & Choice Resolution Protocol

## Overview
This document specifies the correct handling of NPC dialogue choice branch resolution (`AC 20:9`) and System Action Opcodes (`Opcode 7`) in the Wonderland Online native event script engine (`EveEventInterpreter`).

---

## 1. Official PCAP Reverse-Engineered Village NPC Protocols

From `propsshop.pcapng`, `propskeeper.pcapng`, and `witchdoctor.pcapng` packet captures:

### 1.1 Props Shop (`propsshop.pcapng`)
1. **Initial Interaction:**
   - Server sends Step 1 Choice ID 5 (Choice Count 1):
     `AC 20:1` payload `00 00 00 01 06 03 [ClickID] 00 00 00 00 00 00 05 00 01` ("Can you get some Hill Pepper for me?")
2. **Choice 1 ("Yes" / `0x1E`):**
   - Server sends Step 1 Choice ID 6 (Choice Count 2: Buy / Sell):
     `AC 20:1` payload `00 00 00 01 06 03 [ClickID] 00 00 00 00 00 00 06 00 02`
3. **Choice Selection (`0x28` Buy / `0x29` Sell):**
   - Server sends `AC 20:8` (Close Dialog)
   - Server sends `AC 35:12` with Catalog ID `0x0001FB85` (Opens Shop UI)
4. **Choice 2 ("No" / `0x1F`):**
   - Server sends `AC 27:3`, `AC 20:9`, `AC 20:8` (Farewell)

### 1.2 Props Keeper (`propskeeper.pcapng`)
1. Player clicks NPC 38 (TemplateID 14134):
   - Server sends:
     - `AC 29:6`
     - `AC 20:9`
     - `AC 35:12` with ID `0x00019898` (Opens Props Storage Vault UI)
     - `AC 20:8` (Close Dialog)

### 1.3 Witch Doctor (`witchdoctor.pcapng`)
1. Player clicks NPC 22 (TemplateID 14151):
   - Server sends Step 1 Choice ID 3:
     `AC 20:1` payload `00 00 00 01 06 03 [ClickID] 00 00 00 00 00 00 03 00 01` (1=Heal, 2=Save Memory Point, 3=Cancel)
2. **Choice 1 (`0x1E` - Heal):**
   - Full HP/SP recovery (`AC 8:3`, `AC 5:18`, `AC 31:2 [FF FF FF FF]`, `AC 20:9`, `AC 20:8`)
3. **Choice 2 (`0x1F` - Save Point):**
   - DB save, `AC 20:1` TalkID `0x0379B6` ("Memory point saved!"), `AC 5:21 [01]`, `AC 20:10` (Fanfare music), `AC 20:8`
4. **Choice 3 (`0x20` - Cancel):**
   - `AC 31:7`, `AC 20:9`, `AC 20:8`

---

## 2. Dialogue Choice Normalization (`AC 20:9`)

When players interact with NPCs offering multiple choices (e.g. Shops, Witch Doctors, Ferrymen, In-Game Guides), the game client sends option indices in varying base encodings:
- **Base 40 (`0x28, 0x29, 0x2A...`)**: Used in standard 2-choice and 3-choice option menus.
- **Base 30 (`0x1E, 0x1F, 0x20...`)**: Used in standard multi-branch dialogues.
- **Base 1 (`1, 2, 3...`)**: Used in numbered prompts.

### Normalization Logic:
```csharp
int branchIdx = (choice >= 0x28) ? (choice - 0x28) : ((choice >= 0x1E) ? (choice - 0x1E) : Math.Max(0, choice - 1));
ushort targetChoiceVal = (ushort)(30 + branchIdx);
```
This maps option `0x28` (Choice 1) to `unknownword2 = 30`, `0x29` (Choice 2) to `31`, and `0x2A` (Choice 3) to `32`, accurately executing the player's selected branch without dropping interaction state.

---

## 3. Opcode 7 System Actions vs Map Teleports

In `Data/Eve.emg` event scripts, `Opcode 7` parameters are categorized by the magnitude of `dialog1`:
- **`dialog1 < 1000` (System Action Codes & Client UI Triggers)**:
  - **`1`**: Weapon Shop UI -> Dispatches `AC 35:12` with Catalog ID `0x0001FB84` + `AC 20:8`.
  - **`2`**: Props / Item Shop UI -> Dispatches `AC 35:12` with Catalog ID `0x0001FB85` + `AC 20:8`.
  - **`3`**: Armor / Equipment Shop UI -> Dispatches `AC 35:12` with Catalog ID `0x0001FB83` + `AC 20:8`.
  - **`4`**: Props Keep / Storage Bank & Vault UI -> Dispatches `AC 29:6`, `AC 20:9`, `AC 35:12` (`0x00019898`), `AC 20:8`.
  - **`5`**: Save Respawn / Memory Point -> Saves `characters.location_map, location_x, location_y`, sends `AC 20:1` TalkID `0x0379B6`, `AC 5:21 [01]`, `AC 20:10` (Fanfare music), `AC 20:8`.
  - **`6` / `7`**: Witch Doctor / Clinic Full HP/SP Heal & Revive -> Heals Player + all Companions, dispatches `AC 8:3`, `AC 5:18`, `AC 31:2 [FF FF FF FF]`, `AC 20:9`, `AC 20:8`.
  - **`9`**: Stock Keep / Hotel / Guild Vault -> Dispatches `AC 29:6`, `AC 20:9`, `AC 35:12` (`0x00019898`), `AC 20:8`.
- **`dialog1 >= 1000` (Actual Map Teleports)**:
  - Dispatches `map.Teleport()` to target map (e.g., `Map 10000+`).

---

## Source Files
- [`wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs)
- [`wlo.pserver.core/Game/Maps/Code/QuestNpc.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/QuestNpc.cs)
