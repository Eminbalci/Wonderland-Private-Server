# Technical Specification: Tent Furniture Tool Accessory Attachment Protocol

## 1. Overview & Operational Objective

This specification details the reverse-engineered network protocol, data structures, and implementation for attaching tool accessories (such as Stone Knife `#38058` and Wooden Saw `#38060`) to tent appliances (specifically the Work Platform / Workbench `#38049`) in Wonderland Online.

Prior to this implementation:
1. Dragging a tool item from inventory onto the tent workbench did not consume the tool or attach it, leaving tool-dependent recipes (such as Raft `#48010` and Lauuan Wood `#45001`..`#45004`) locked in the client interface.
2. Character bag inventory (`storID = 0`) was never loaded during character login in [`CharacterDataBase.LoadCharacterData`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/CharacterDataBase.cs), leaving the 2D inventory occupancy grid unpopulated and allowing items to overlap into multi-cell footprints (e.g., Raft 4x3 cells).

---

## 2. Client Reverse-Engineering & Protocol Specifications

### A. Client-to-Server Tool Drop Packet (`AC 62:4`)

Reverse-engineering `aLogin.exe` at `0x00379684`–`0x003796bc` identified the client handler triggered when dragging an item from the bag onto a placed tent object:

```
[Header: F4 44 03 00] [ActionCode: 62] [SubCode: 4] [SlotIndex: 1 Byte] [TargetHostIndex: 2 Bytes LE]
```

- **`SlotIndex` (Byte):** 1-based bag inventory slot containing the tool (`1..50`).
- **`TargetHostIndex` (UInt16 LE):** 1-based index of the target tent appliance (e.g., `2` for Workbench placed in slot 2).

### B. Server-to-Client Tool Attachment Confirmation (`AC 62:5`)

Reverse-engineering `aLogin.exe` at `0x002ea52d` verified the server confirmation packet required by the client to attach the tool to the workbench model and unlock recipes:

```
[Header: F4 44 08 00] [ActionCode: 62] [SubCode: 5] [AccessoryIndex: 2 Bytes LE] [ToolItemID: 2 Bytes LE] [HostIndex: 2 Bytes LE]
```

- **`AccessoryIndex` (UInt16 LE):** Unique client index assigned to the accessory child entity.
- **`ToolItemID` (UInt16 LE):** Item ID of the attached tool (e.g., `38058` for Stone Knife).
- **`HostIndex` (UInt16 LE):** Index of the parent appliance entity (`rawTargetIndex`).

### C. 57-Byte Tent Item Layout in `AC 62:4` Synchronization

In `aLogin.exe` (line 416766):
```c
if ((local_38 == 0) && (local_17 == '\0')) {
    // Places item on floor only if parent == 0
}
```
Each tent item record in the `AC 62:4` sync packet is 57 bytes:

| Byte Offset | Size | Type | Description |
|---|---|---|---|
| `0..1` | 2 | UInt16 LE | Client item index (`1..N`) |
| `2..3` | 2 | UInt16 LE | Item ID (`ItemID`) |
| `4..7` | 4 | UInt32 LE | X coordinate |
| `8..11` | 4 | UInt32 LE | Y coordinate |
| `12..15` | 4 | UInt32 LE | Z elevation (1) |
| `16` | 1 | Byte | Rotation |
| `17..18` | 2 | UInt16 LE | Parent item index (`0` for root furniture, `hostIndex` for accessories) |
| `19..38` | 20 | 10x UInt16 LE | 10 accessory slots pointing to child accessory indices |
| `39..48` | 10 | Byte[] | Padding / unused |
| `49` | 1 | Byte | Floor index (`0` = 1st floor, `1` = 2nd floor) |
| `50..58` | 9 | Byte[] | Trailing padding (57 bytes total) |

By setting `parent = hostIndex` on accessory child records, the client registers the tool internally to unlock recipes while suppressing 3D floor placement.

---

## 3. Architecture & Implementation

### A. Data Models (`TentItem`)

In [`wlo.pserver.core/Game/Item.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Item.cs#L148):
- Added `public List<ushort> AttachedTools { get; } = new List<ushort>();`.
- Added `public bool HasTool(ushort toolId) => AttachedTools.Contains(toolId);`.
- Added `public bool AddAttachedTool(ushort toolId)` enforcing uniqueness and a maximum limit of 10 accessory slots.

### B. Action Code Dispatcher (`AC62.Recv4`)

In [`Src/Network/ActionCodes/AC62.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC62.cs#L240):
1. Unpacks `slotIndex` (1B) and `rawTargetIndex` (2B LE).
2. Verifies player is inside their own tent.
3. Validates tool item presence in inventory slot and checks target appliance is a Work Platform (`#38049`).
4. Deducts 1x tool from inventory via [`Player.Inv.RemoveItem`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Inventory.cs) (broadcasting `AC 23:9`).
5. Adds tool to `targetItem.AddAttachedTool(toolItemId)`.
6. Sends `AC 62:5` confirmation packet to client.
7. Broadcasts updated layout to all tent occupants via `SendTentItemsToAll()`.
8. Persists changes to SQLite database via `SaveTentData(p)`.

### C. Tent Layout Synchronization (`Tent.SendTentItemsToPlayer`)

In [`wlo.pserver.core/Game/PlayerRelated/Tent/Tent.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Tent/Tent.cs#L554):
- Packs host appliances with their 10 accessory slots containing child entity indices.
- Appends accessory entries with `parent = hostIdx` and `X = 0, Y = 0, floor = 0`.
- Implemented `CalculateTotalItemCount()` calculating root furniture items plus total attached tool accessories.

### D. Inventory Occupancy & Database Persistence

In [`wlo.pserver.core/DataBase/CharacterDataBase.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/CharacterDataBase.cs):
- In `LoadCharacterData`: Added bag inventory (`storID = 0`) loading with `t.Inv.RebuildOccupancyGrid()`, ensuring multi-cell items like Raft (`48010`, 4x3) lock slave slots on login.
- In `SaveTentData` / `LoadTent`: Added serialization and deserialization of the `accessories` column in `chartent_items`.
- Migrated `Data/ServerDataBase.db` and `bin/Debug/Data/ServerDataBase.db` schema with `ALTER TABLE chartent_items ADD COLUMN accessories TEXT;`.

---

## 4. Verification & Empirical Results

Automated simulation verified:
1. `AC 62:4` packet successfully parsed `slotIndex = 1` and `hostIndex = 2`.
2. 1x Stone Knife (`#38058`) was removed from bag slot 1 with `AC 23:9` sent.
3. Confirmation packet `AC 62:5` captured with payload `[AccessoryIndex=3, ToolItemID=38058, HostIndex=2]`.
4. `workbench.HasTool(38058)` evaluated to `True`.
5. `SendTentItemsToPlayer` serialized 3 items (2 furniture + 1 accessory child), confirming byte alignment.
