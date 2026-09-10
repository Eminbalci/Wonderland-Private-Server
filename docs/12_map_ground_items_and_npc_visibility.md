# Map Ground Items and Pre-Event NPC Visibility Subsystem

## Overview

This document specifies the network architecture and runtime lifecycle for native map ground items (`ItemAreas`) and dynamic NPC visibility states driven by `Eve.emg` PreEvents within the Wonderland Online private server emulator.

---

## 1. Ground Item Subsystem (AC 23:4 / AC 23:2 / AC 23:6)

### 1.1 Data Source & In-Memory Representation

Native map ground items are parsed from the `ItemAreas` category of [`Eve.emg`](file:///D:/GitHub/Wonderland-Private-Server/Data/Eve.emg) during map instantiation in [`Map.Load_FinalData`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs).

Each item is represented by [`MapGroundItem`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs):
- `Slot (byte)`: Sequential terrain identifier index (1-based, up to 255).
- `ClickID (ushort)`: Client-side entity handle.
- `ItemID (ushort)`: Item ID registered in `Item.dat`.
- `Name (string)`: Display name resolved from item data or dictionary.
- `X (ushort)`, `Y (ushort)`: World terrain pixel coordinates.
- `RespawnSeconds (int)`: Respawn interval loaded from `Eve.emg` `unknownword1` (defaults to 120s if unset).
- `IsPickedUp (bool)`: Runtime state flag.
- `RespawnTime (DateTime)`: Absolute timestamp when the item becomes eligible to respawn.

### 1.2 Protocol Specification

Analysis of authentic official game captures (`ilkgorevtamami.pcapng`, Frame 75) confirms the true wire protocol:

| Action | Direction | AC Code | Format String / Layout | Wire Layout | Description |
| :--- | :--- | :--- | :--- | :--- | :--- |
| Map Entry Spawning | S -> C | `AC 23:4` | Multi-item payload | `[23, 4, [0x03, Slot:w, ItemID:d, X:w, Y:w, 0:d] * N]` | Batched in [`Map.SendMapInfo`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs). Exactly 15 bytes per active item. |
| Respawn Broadcast | S -> C | `AC 23:4` | Multi-item payload | `[23, 4, [0x03, Slot:w, ItemID:d, X:w, Y:w, 0:d]]` | Broadcast by [`Map.Process`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs) to all players when `now >= RespawnTime`. |
| Pickup Request | C -> S | `AC 23:2` | `bb` | `[23, 2, Slot:b]` | Client sends targeted item terrain slot index on click. Handled by [`Map.onItemPickup`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs). |
| Pickup Ack (Picker) | S -> C | `AC 23:2` | `bbwb` | `[23, 2, Slot:w, 1:b]` | Informs the picker that the item at `Slot` was successfully claimed. |
| Pickup Banner | S -> C | `AC 23:6` | `bbwb + 28B` | `[23, 6, ItemID:w, 1:b, 0x00 * 28]` | Triggers the gold item popup window indicating item acquisition. |
| Despawn (Broadcast) | S -> C | `AC 23:2` | `bbwb` | `[23, 2, Slot:w, 0:b]` | Broadcasts item slot removal to all other observers on the map. |

> [!NOTE]
> Each item entry in the `AC 23:4` payload starts with entity type byte `0x03`, followed by 2-byte slot, 4-byte ItemID, 2-byte X, 2-byte Y, and 4-byte flags/respawn (15 bytes per record). In `AC 23:2`, the 16-bit payload corresponds to the item's terrain `Slot`, not its `ItemID`.

---

## 2. Pre-Event Driven NPC Visibility & Entity Isolation

### 2.1 Problem & Root Cause

In official WLO maps (such as Map 12000 - Kelan Village), certain NPCs exist in multiple stages or alternate positions:
- **ClickID 27**: Lina (`TID 14049`, pos `628, 1175`).
- **ClickID 28**: Lina's lost Shiba Inu (`TID 11003`, pos `592, 1192` - standing next to Lina).
- **ClickID 29**: Lina's permanent Shiba Inu (`TID 11003`, pos `624, 1259` - sitting near Lina).
- **ClickID 20**: Lina's lost Shiba Inu in the pig pen (`TID 11003`, pos `2535, 1543`).
- **ClickID 18, 19**: Quest-specific Baby Bees (`TID 10003`, pos `232, 1192` and `312, 1072`).
- **ClickID 37**: Wild Baby Bee (`TID 10003`, pos `472, 1272`).

Map 12000 defines PreEvents:
- **PreEvent #6**: When Quest 13046 ("The Lost Dog") is `NotStarted` (`req_state = 2`), action hides ClickID 28.
- **PreEvent #5**: When Quest 13046 is `InProgress` (`req_state = 1`), action hides ClickID 28.
- **PreEvent #4**: When Quest 13046 is `NotStarted`, action hides ClickID 20 (pig pen).
- PreEvents also hide quest-specific Baby Bees (`ClickID 18, 19`) leaving only the authentic wild Baby Bee (`ClickID 37`) visible.

Client investigation revealed:
1. Setting `state = 0xFFFF` inside `AC 22:4` alone is ignored by the WLO client engine for entity suppression.
2. To genuinely suppress an NPC sprite from rendering on client screens, the server must dispatch **both** `AC 22:10` and `AC 22:11` packets with parameter `(0xFF, 0xFF)`:
   - `AC 22:10`: `[22, 10, ClickID:w, 0xFF, 0xFF]`
   - `AC 22:11`: `[22, 11, ClickID:w, 0xFF, 0xFF]`

### 2.2 Implementation Architecture

1. **Initial NPC List Construction (`AC 22:4`)**:
   - In [`Map.SendMapInfo`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs), every NPC's state is checked against [`PreEventInterpreter.ShouldNpcBeVisible`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/QuestRelated/PreEventInterpreter.cs):
     ```csharp
     else if (!Game.QuestRelated.PreEventInterpreter.ShouldNpcBeVisible(t, (ushort)this.MapID, (ushort)npc.CickID))
     {
         state = 0xFFFF; // Flagged hidden
     }
     ```

2. **Dual Action Suppression (`AC 22:10` + `AC 22:11`)**:
   - Immediately following the `AC 22:4` batch in `Map.SendMapInfo`, the server evaluates all map NPCs.
   - Any NPC that is recruited as a companion (`isRecruited`), broken permanently, or evaluated as hidden by PreEvents (`isHiddenByPreEvent`) receives both suppression packets:
     ```csharp
     if (isRecruited || isHiddenByPreEvent || (qn.IsBroken && qn.RespawnTime == DateTime.MaxValue))
     {
         tmp.Add(Tools.FromFormat("bbwbb", 22, 10, (ushort)qn.CickID, (byte)0xFF, (byte)0xFF));
         tmp.Add(Tools.FromFormat("bbwbb", 22, 11, (ushort)qn.CickID, (byte)0xFF, (byte)0xFF));
     }
     ```

3. **Dynamic Dialogue & Quest Step Progression**:
   - In [`EveEventInterpreter.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs):
     - `case 2:` handles `op.dialog2 == 2 || op.dialog2 == 3` with `op.dialog4 == 65280` (0xFF00) to send dynamic hide packets (`AC 22:10` and `AC 22:11`).
     - `case 5:` advances quest steps for in-progress multi-step events and automatically synchronizes visibility state per player.
