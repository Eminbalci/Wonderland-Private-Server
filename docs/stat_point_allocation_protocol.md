# Character & Companion Stat Point Allocation Protocol (`AC 8`)

This document specifies the packet architecture, payload layouts, stat calculations, skill unlocking progression, and persistence layer for character and companion/pet stat point allocation from the in-game status window (Alt+A) in Wonderland Online.

---

## 1. Overview & Mechanics

When a player levels up (or gains levels through combat EXP or GM commands):
1. The server grants **+3 unallocated stat points** per player level gained (`SkillPoints += 3`).
2. The server dispatches an `AC 8:1` stat synchronization packet containing the total available unallocated points under **Stat 38** (`SendStat(38, SkillPoints)`).
3. The client user opens the Status Window (`Alt+A`) and distributes points to **STR**, **CON**, **INT**, **WIS**, or **AGI** via the `+` buttons and confirms.
4. The client dispatches an `AC 8 Sub 1` (or direct stat subcode) allocation packet to the server.
5. The server validates point availability, decrements unallocated points, updates base stats, unlocks progression skills, synchronizes updated stats, and persists character records to the SQLite database.

---

## 2. Packet Specifications

### 2.1 Server -> Client: Stat Point Broadcast (`AC 8 Sub 1`)
- **Header**: `F4 44 [Len16] 08 01`
- **Payload Format**: `[StatID (1B)] [TargetType (1B: 1=Self)] [Value (4B LE)] [00 00 00 00 (4B)]`
  - `Stat 38`: Total Available Unallocated Stat Points (`SkillPoints`).
  - `Stat 28`: Base Strength (`STR`).
  - `Stat 29`: Base Constitution (`CON`).
  - `Stat 27`: Base Intelligence (`INT`).
  - `Stat 33`: Base Wisdom (`WIS`).
  - `Stat 30`: Base Agility (`AGI`).

### 2.2 Client -> Server: Stat Point Allocation Request (`AC 8 Sub 1` / `AC 8 Sub [StatID]`)
The server accommodates multi-batch, single-target, and direct subcode allocation formats:
- **Batch Format**:
  - `F4 44 [Len16] 08 01 [TargetType (1B: 0=Player, 1..4=Pet)] [Count (1B)] [ (StatID 1B, Amount 4B/2B/1B)... ]`
- **Single Stat with Target**:
  - `F4 44 [Len16] 08 01 [TargetType (1B)] [StatID (1B)] [Amount (4B/2B/1B)]`
- **Direct Stat Allocation**:
  - `F4 44 [Len16] 08 01 [StatID (1B)] [Amount (4B/2B/1B)]`
- **Direct Subcode Allocation**:
  - `F4 44 [Len16] 08 [StatID (1B: 27/28/29/30/33)] [Amount (4B/2B/1B)]`

---

## 3. Stat Attribute Mappings & Formulas

| Stat ID | Name | Affected Secondary Attributes | Progression / Unlocks |
| :--- | :--- | :--- | :--- |
| **`28`** | **STR** | Physical Attack (`FullAtk`), Max HP | Unlocks melee/weapon combat stunts |
| **`29`** | **CON** | Physical Defense (`FullDef`), Max HP | Unlocks defensive stunts & survival thresholds |
| **`27`** | **INT** | Magical Attack (`FullMatk`), Max SP | Unlocks elemental attack spells |
| **`33`** | **WIS** | Magical Defense (`FullMdef`), Max SP | Unlocks healing, buffing, and sealing skills |
| **`30`** | **AGI** | Turn Order Speed (`FullSpd`) | Determines turn priority in PvE/PvP combat |

---

## 4. Implementation Details

- **[`AC08.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC08.cs)**:
  - Universal payload decoder supporting single-entry, multi-entry batches, and pet slot allocations.
  - Decrements `SkillPoints`, updates `baseStr`/`baseCon`/`baseInt`/`baseWis`/`baseAgi`.
  - Dispatches immediate `Send8_1(true)` stat refresh and system chat confirmation.
  - Automatically triggers `SkillManager.CheckAndUnlockProgressionSkills(player)`.
  - Synchronizes to database via `DataBase.CharacterDataBase.GlobalInstance.WritePlayer()`.
- **[`Equip.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Equip.cs)**:
  - `SetLevel(byte targetLvl)` awards `(targetLvl - oldLvl) * 3` stat points upon level increase.
- **[`AC02.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC02.cs)**:
  - Added `:points <amount>` / `:sp <amount>` GM command for custom testing and stat point grants.

---

## 5. Server GUI Live Stat Point Management

The server administration interface ([`MainForm1`](file:///d:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.cs)) provides direct real-time controls on the **Cheat** tab:
1. **Target Player Dropdown** (`comboBox_OnlinePlayers`): Select the active in-game character.
2. **Points Amount Selector** (`numStatPoints`): Choose number of stat points (1 to 99,999).
3. **➕ Give Points Button** (`btnGiveStatPoints`):
   - Increments target player's `SkillPoints` by specified amount.
   - Immediately dispatches `Send8_1(true)` to update client UI status bars and available points.
   - Saves record to SQLite database.
   - Dispatches in-game system message confirmation.
4. **🔄 Reset Stats Button** (`btnResetStats`):
   - Resets character base stats (`STR`, `CON`, `INT`, `WIS`, `AGI`) back to 10.
   - Refunds all spent points back into available `SkillPoints`.
   - Synchronizes client UI in real time.
5. **Database Stats Tab Synchronization** (`dgvStats` / `btnEditStat`):
   - Editing Stat 38 or any base stat in the database viewer immediately synchronizes live online characters in memory.

