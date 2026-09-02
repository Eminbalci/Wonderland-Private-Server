# Eve Event Interpreter & Authentic Data Files Integration

## 1. Overview
The server leverages the official Wonderland Online `.dat` and `eve.Emg` ecosystem as the primary driver for all world events, dialogues, multi-step quest chains, chest drops, and rewards.

---

## 2. Integrated Data Files Ecosystem

1. **`eve.Emg` / `eve.dat`**:
   - Master map bytecode tables containing scene headers, collision blocks, warp portals, mining/gathering nodes, chests/items, NPC click triggers, condition branches (`Cdn`), and action opcodes (`Result`).
2. **`Talk.dat`**:
   - 13,763 authentic localized dialogues. Sent to clients using `AC 20:1` with 24-bit little-endian TalkIDs.
3. **`Npc.dat`**:
   - 982 NPC templates, monster drop tables, stats, elements, and sprite models.
4. **`Mark.dat`**:
   - 2,154 official quest definitions and F6 Quest Journal entries.
5. **`Item.dat` / `itemDat.wpdat`**:
   - 4,776 equipment, item, and food definitions.

---

## 3. Dynamic Condition Evaluator (`EveEventInterpreter.SelectMatchingBranch`)

The condition evaluation engine in [`EveEventInterpreter.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs) supports all native `unknownbyte1` branches:

- **`unknownbyte1 == 1` (Level Requirement)**:
  - Validates player character level meets minimum criteria (`player.Level >= sub.unknownword1`).
- **`unknownbyte1 == 2` (Item & Stack Requirement)**:
  - Validates player inventory has required item count (`player.Inv.ContainsItem(sub.unknownword3)` with count `>= sub.unknownword2`).
- **`unknownbyte1 == 3` (Quest State & Step Requirement)**:
  - Evaluates active quest progress step and completion status.
- **`unknownbyte1 == 4` (Companion Pet in Party)**:
  - Checks if required companion pet is in player party (`player.PlayerPets.Values.Any(p => p.PetID == sub.unknownword1)`).
- **`unknownbyte1 == 5` (Gold / Currency Requirement)**:
  - Checks if player has sufficient gold (`player.Gold >= sub.unknownword1`).
- **`unknownbyte1 == 7` (Dialogue Choice / Multi-Option Selection)**:
  - Dynamically routes dialogue options (`30 + choiceIndex`) into authentic response branches.

---

## 4. Supported Reward Opcodes
- **`Opcode 1`**: Eşya verme / eşya harcama (`AddItem` / `RemoveItem`).
- **`Opcode 2`**: Sandık açma (`AC 22:1`), kaynak toplama (`AC 22:10`).
- **`Opcode 3`**: Yoldaş / Pet katılımı (`QuestManager.SendCompanionReward`).
- **`Opcode 5`**: Görev adımı ve tamamlama bayrakları (`AC 24:1`).
- **`Opcode 6`**: Görev savaşları (`PvEBattleManager.StartPvEBattle`).
- **`Opcode 7`**: Sistem arayüzleri (Banka, Klinik, Alışveriş, Spawn kaydı, Harita ışınlanma).
- **`Opcode 8` / `13` / `186`**: Fanfare ve sinematik animasyonlar (`AC 186:12`).
- **`Opcode 9`**: Mini-oyunlar (`AC 57:1`).
- **`Opcode 10`**: Altın ödülü (`AddGold`).
- **`Opcode 11`**: Tecrübe puanı ödülü (`CurExp`).
- **`Opcode 12`**: Karakter model dönüşümleri (`AC 5:12`).
