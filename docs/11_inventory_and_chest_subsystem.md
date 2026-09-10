# 11 - Inventory, Starter Pack & Chest Loot Subsystem

## 1. Overview

The Inventory, Starter Pack, and Map Chest Subsystems manage character item storage, beginner progression packages, and interactive world loot containers across Wonderland Online.

```
                  Client Action
                        │
        ┌───────────────┴───────────────┐
        v                               v
Character Creation / Login      Map Chest Click (ClickID)
        │                               │
        v                               v
StarterPackManager.Deliver       EveEventInterpreter
        │                               │
        v                               v
Inventory.AddItem               GetPlayerFreeSlots >= 1?
 (Slots 1..8, 1x1)              ┌───────┴───────┐
        │                       v               v
        v                     [YES]           [NO]
AC 23:6 + AC 23:5 Sync          │               │
                                v               v
                          Grant Loot      "Inventory full!"
                          (Opcode 1)      Abort Sub-Sequence
                                │         (Prevent Opcode 5)
                                v
                          Mark Complete
                          (Opcode 5)
```

---

## 2. Character Bag Inventory Architecture

### Grid Topology & Dimensionality
- **Total Capacity**: 50 individual inventory slots indexed 1 through 50.
- **Physical Dimensions**: Strictly 1x1 per item in character bags.
- **Dimensional Properties**:
  - Base class [`Item.Height`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Item.cs) and [`Item.Width`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Item.cs) return strictly `1`.
  - Multi-cell dimensions (`cellwidth`, `cellheight` in `itemDat.wpdat`) represent floor grid tiles inside player tents and are overridden exclusively in [`TentItem`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Item.cs).
  - Bag items never allocate or pollute neighboring slot indices with non-zero `Parent` pointers.

### Capacity Calculations
- **[`FilledCount`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Inventory.cs)**: Returns the total count of non-empty slots:
  $$\text{FilledCount} = \sum_{s=1}^{50} [s.\text{ItemID} > 0]$$
- **[`unFilledCount`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Inventory.cs)**: Returns the remaining free slots:
  $$\text{unFilledCount} = 50 - \text{FilledCount}$$

### Placement Algorithm (`Inventory.AddItem`)
```
Input: Item item, byte at, bool sendData
Output: int addedTotal

1. If at in [1..50]:
   a. If slot[at] is empty -> copy item, clamp amount (Stackable ? min(item.Ammt, 50) : 1), return added.
   b. If slot[at].ItemID == item.ItemID and slot[at].SpaceLeft > 0 -> stack up to SpaceLeft, return added.
2. If at == 0 (dynamic placement):
   a. Pass 1 (Stackable items only):
      For each slot s in 1..50 where slot[s].ItemID == item.ItemID and slot[s].SpaceLeft > 0:
         toStack = min(needed, slot[s].SpaceLeft)
         slot[s].Ammt += toStack
         needed -= toStack
         Emit AC 23:6
   b. Pass 2 (Remaining quantity):
      For each slot s in 1..50 where slot[s].ItemID == 0:
         toPlace = item.Stackable ? min(needed, 50) : 1
         slot[s] = item (Ammt = toPlace, Parent = 0)
         needed -= toPlace
         Emit AC 23:6
3. If addedTotal > 0 and sendData:
   Emit AC 23:5 full inventory state synchronization packet.
```

---

## 3. Authentic Starter Pack Subsystem

### Specification
Managed via [`StarterPackManager`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/StarterPackManager.cs) and backed by SQLite table `starter_items`.

| Order | Item ID | Item Name | Quantity | Item Type | Stackable | Description |
| :---: | :---: | :--- | :---: | :---: | :---: | :--- |
| 1 | 34038 | Notepad | 1 | 25 | Yes | Beginner guide and notepad |
| 2 | 34058 | Remote Control | 1 | 25 | Yes | Auto-combat assistant controller |
| 3 | 32176 | Fugu Hot Pot | 50 | 23 | Yes | Full recovery food |
| 4 | 34014 | Tao Rice Ball | 10 | 25 | Yes | Pet and character food |
| 5 | 34026 | Protective EXP Pill | 5 | 25 | Yes | Prevents EXP loss upon death |
| 6 | 34169 | Bamboo Dragonfly | 1 | 25 | Yes | Starter flying mount vehicle |
| 7 | 34190 | 10X Holy EXP Potion | 3 | 25 | Yes | Boosts experience gain |
| 8 | 34253 | Training Ticket | 5 | 25 | Yes | Training island pass |

### Self-Healing Database Verification
- [`StarterPackManager.VerifyTable()`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/StarterPackManager.cs) detects obsolete or non-existent IDs (such as `23050, 23051, 48050, 57001, 34542, 21742`) and automatically flushes and reseeds the authentic catalog upon server boot.

### Delivery Lifecycle
1. **Character Creation (`AC09`)**: Delivered prior to writing the initial player record into the database.
2. **Login Fallback (`WorldServer.CommenceLogin`)**: If `(player.Level <= 1)` and `!StarterPackManager.HasAnyStarterItem(player)`, the server auto-delivers the starter items into empty slots.

---

## 4. Map Chest Loot & Quest Flag Safeguard

### Bytecode Event Execution & Quantity Decoding
In Wonderland Online `Eve.emg` event scripts, chest props (e.g. Map 10017, Map 10024..10028, Map 10035) typically contain three sequential opcodes:
1. **Opcode 2 (`dialog2 == 5`)**: Prop Break / Chest Open animation (`AC 22:1`).
2. **Opcode 1 (`dialog1 == 1, dialog3 == ItemID, dialog4 == Mode/Count`)**: Grant or take items:
   - `dialog3`: Item ID (e.g. 32073, 32074, 32075, 41066, 48016).
   - `dialog4`: 16-bit signed encoding where the high byte indicates the item quantity:
     $$\text{countHigh} = (\text{short})\text{dialog4} \gg 8$$
     - If $\text{countHigh} < 0$ (e.g. `0xFF00` = -1, `0xFE00` = -2): Item removal/consumption opcode with quantity $|\text{countHigh}|$.
     - If $\text{countHigh} > 0$ (e.g. `0x0C00` = 12, `0x0800` = 8, `0x0D00` = 13, `0x0100` = 1): Authentic item drop quantity granted to character inventory.
     - Fallback: If high byte is zero, falls back to `dialog2`.
3. **Opcode 5 (`dialog1 == QuestID, dialog2 == 2`)**: Register quest / chest completion flag in `charquest`.

### Capacity Check & Abort Guard
When Opcode 1 executes:
- The interpreter queries [`GetPlayerFreeSlots(player)`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs).
- If `freeSlots < 1` and the player does not have existing stackable capacity for the item:
  1. Sends notification: `[23, 57, 0, "Inventory is full!"]`.
  2. Returns `false`.
  3. **Immediate Sub-Sequence Abort**: [`EveEventInterpreter.RunSubOpcodes`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs) immediately breaks execution, preventing Opcode 5 from marking the chest as completed.
  4. The chest remains unopened and can be accessed again once bag space is cleared.

### Branch Selection & "Empty..." Response
- In [`SelectMatchingBranch`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs):
  - Checks if `sub.unknownbyte1 == 3` (chest) or any Opcode 5 flag inside the candidate sub-entry is already present in `player.Quests` with `QuestState.Completed`.
  - If completed, the candidate is discarded.
  - When no valid uncompleted branch remains for a chest prop, the server dispatches `[23, 57, 0, "Empty..."]` and unlocks player movement.

---

## 5. Vehicle & Raft Lifecycle and Shore Wreck Subsystem

### Overview
Wooden rafts (`ItemID 48016` / `48010`) allow maritime navigation across water maps (e.g. Map 10036 and Map 11016). When a player lands on the shore or the raft reaches maximum damage (100 wear), the raft breaks into pieces and must be completely removed from character inventory.

### Shore Landing Protocol (`AC 15:10`)
1. **Client Trigger**: When player clicks on the beach shore while mounted on a raft, client emits `AC 15 Sub 10` (`0F 0A 10 [vehicleId:2B]`).
2. **Server Execution (`VehicleManager.WreckVehicle`)**:
   - Resets `player.ActiveVehicleID = 0` and `player.RideVehicle("")`.
   - Sends `AC 15 Sub 14` (`[15, 14, vehicleType, charId:4B, 0xD6, 0x01, 0, 0, 0, 0]`) to freeze vehicle motion.
   - **Slot Resolution & Deletion**:
     - Scans player inventory slots 1 through 50 to locate the matching raft item.
     - Calls [`player.Inv.RemoveItem(foundSlot, 1, senddata: true)`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Inventory.cs).
     - Emits `AC 23 Sub 9` (`[23, 9, foundSlot, 1]`) indicating the exact bag slot to remove.
     - Emits `AC 23 Sub 5` full inventory state packet to guarantee client-server synchronization.
   - Sends `AC 15 Sub 15` (`[15, 15, charId:4B, vehicleId:2B]`) to trigger vehicle wreck particle animation.
   - Sends `AC 15 Sub 11` (`[15, 11, vehicleType, charId:4B]`) to restore foot-walking animation.
   - Saves character state to database via [`player.SaveCharacterData()`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Player.cs).
   - Sends notification message: `"The wooden raft broke apart upon landing on the shore. You are now walking on foot."`.
