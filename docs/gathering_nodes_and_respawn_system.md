# Gathering Nodes, Chests & Eve.dat Drop Resolution Protocol

## Overview
All interactive treasure chests, crates, gathering props, and ground items across all 1,119 maps in Wonderland Online are parsed and executed directly from the native `eve.dat` / `eve.emg` event bytecode engine (`EveEventInterpreter`).

---

## 1. Native `eve.dat` Item Drops & Interaction Protocol
- **Direct Event Resolution**:
  - `QuestNpc.Interact` invokes `EveEventInterpreter.TryExecute(src, gmap, (ushort)this.CickID)` at top priority.
  - Matches NPC `ClickID` against `mapData.Npclist` and `mapData.Events` to load the exact subentry.
- **Opcodes Executed**:
  1. **`Opcode 1` (Item Grant)**:
     - Awards authentic item ID (e.g. Map 10036 Chests `#32074`, `#32075`, Crate `#32032`, Coconut `#41066`, Raft `#48016`) and exact count.
     - Synchronizes player backpack inventory: `AC 23:5`.
     - Displays authentic obtain notification: `AC 23:57 [0, "Obtain {ItemName}"]`.
     - Plays fanfare audio effect: `AC 20:10`.
  2. **`Opcode 2` (Prop Open / Break / Despawn Animation)**:
     - `dialog2 == 5`: Chest open / prop break animation broadcast to map via `AC 22:1`.
     - `dialog2 == 2`: Gathering node despawn animation broadcast via `AC 22:10`.
     - Sets `qn.IsBroken = true` and `qn.RespawnTime = DateTime.Now.AddSeconds(60)`.
  3. **`Opcode 5` (Quest / Map State Flags)**:
     - Saves persistent chest/quest state flags in `player.Quests`.
  4. **Session Release**:
     - Dispatches `AC 20:8` and `AC 5:4` to release player movement locks.

---

## 2. Empty State & Respawn Mechanics
- If an opened chest or harvested gathering node is clicked while `IsBroken` is true:
  - Dispatches `AC 23:57` with remaining respawn duration.
- When `DateTime.Now >= RespawnTime`:
  - Resets `IsBroken = false` and broadcasts un-hide packet `AC 22:10 [ClickID, 0, 0]`.
