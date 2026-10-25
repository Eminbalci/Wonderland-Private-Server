# Technical Specification: Tent Furniture Tool Accessory Attachment Protocol

## 1. Overview & Operational Objective

This specification details the reverse-engineered network protocol, memory layout, data structures, and server implementation for attaching tool accessories (such as Stone Knife `#38058` and Wooden Saw `#38060`) to tent appliances (specifically the Work Platform / Workbench `#38049`) and managing tent object spatial occupancy in Wonderland Online.

Prior to this implementation:
1. Dragging a tool item from inventory onto the tent workbench consumed the item, but crafting recipes requiring that tool (such as Raft `#48010` and Lauuan Wood `#45001`..`#45004`) did not unlock in the client's crafting interface.
2. The 57-byte tent object packet payload was misaligned by 2 bytes: the 10 accessory slots were written starting at Offset 17 instead of Offset 19, causing the client loop (which evaluates `ebx * 2 + 0x2b` starting at `ebx = 1`) to read slot 1 at Offset 19 as empty (`0`).
3. Calling full tent synchronization (`AC 62:4`) immediately following accessory attachment (`AC 62:5`) reset client-side memory structures, overwriting live attachments and wiping slot 1.
4. Tent items were placed using unpopulated `PhxItemInfo`, defaulting multi-cell objects like Raft (`48010`, 4x3 cells) and Workbench (`38049`, 3x2 cells) to 1x1 cells on the server, allowing other items to be placed inside them.
5. Crafting completion broadcast `AC 5:5` with ID 0, which triggered client-side player model transformation / NPC disguise instead of visual effects.

---

## 2. Client Reverse-Engineering & Protocol Specifications

### A. Client-to-Server Tool Drop Packet (`AC 62:4`)

Disassembly of `aLogin.exe` at `0x0042db19`–`0x0042ddcf` identified the packet sent when dropping an inventory item onto an appliance in the tent:

```
[Header: F4 44 05 00] [ActionCode: 62 (0x3E)] [SubCode: 4] [SlotIndex: 1 Byte] [TargetHostIndex: 2 Bytes LE]
```

- **`SlotIndex` (Byte):** 1-based bag inventory slot containing the tool (`1..50`).
- **`TargetHostIndex` (UInt16 LE):** 1-based index of the target tent appliance (e.g., `2` for Workbench placed at index 2).

### B. Server-to-Client Tool Attachment Confirmation (`AC 62:5`)

Disassembly of `aLogin.exe` at `0x0042ddcf` identified the incremental attachment handler:

```
[Header: F4 44 08 00] [ActionCode: 62 (0x3E)] [SubCode: 5] [AccessoryIndex: 2 Bytes LE] [ToolItemID: 2 Bytes LE] [HostIndex: 2 Bytes LE]
```

Client execution path:
1. Reads `AccessoryIndex` at offset 2, `ToolItemID` at offset 4, and `HostIndex` at offset 6.
2. `0x0042de7c: call 0x4133fc` sets `tentObjects[AccessoryIndex].ItemID = ToolItemID`.
3. `0x0042de8b: call 0x4162f0` loops `esi = 1..10`, finds the first empty slot at `hostItem + esi*2 + 0x2b`, sets `hostItem.slots[esi] = AccessoryIndex`, and sets `accessory.parent = HostIndex`.
4. `0x0042deac: call 0x21fca8` checks if the crafting window (`0x4ca5e8`) is open and immediately refreshes the recipe list.
5. **Architectural Requirement:** `AC 62:5` must be broadcast to all occupants in the tent. Full `AC 62:4` layout reload must NOT be broadcast immediately behind `AC 62:5`, as `0x004162a8` would reset client memory and disrupt the active UI session.

### C. Client Crafting Tool Collector & 57-Byte Tent Object Memory Layout

Disassembly of `aLogin.exe` at `0x00439b56`–`0x00439c25`:
```asm
0x00439b92: mov ebx, 1
0x00439b97: movzx eax, si                      ; si = hostItem clientIndex
0x00439b9a: imul eax, eax, 0x39               ; 0x39 = 57 bytes per item
0x00439b9d: mov edx, dword ptr [0x4caa28]
0x00439ba3: mov edx, dword ptr [edx]
0x00439ba5: mov edx, dword ptr [edx + 0x523c] ; base tent array
0x00439bab: lea eax, [edx + eax]
0x00439bae: mov ax, word ptr [eax + ebx*2 + 0x2b] ; read host.slots[ebx]
0x00439bb3: test ax, ax
0x00439bb6: jbe 0x439c21
...
0x00439c0f: mov ax, word ptr [edx + eax + 0x1a] ; read child.ItemID
0x00439c1c: mov word ptr [ecx + edx*2 - 2], ax   ; append tool to crafting list
0x00439c21: inc ebx
0x00439c22: cmp ebx, 0xb
0x00439c25: jne 0x439b97
```

Relative to struct base (`+ 0x1a`):
- `ebx = 1`: `1 * 2 + 0x2b - 0x1a = 19` (Offset 19..20 = Slot 1)
- `ebx = 2`: `2 * 2 + 0x2b - 0x1a = 21` (Offset 21..22 = Slot 2)
- ...
- `ebx = 10`: `10 * 2 + 0x2b - 0x1a = 37` (Offset 37..38 = Slot 10)
- `ebx = 0`: `0 * 2 + 0x2b - 0x1a = 17` (Offset 17..18 = Reserved padding, never read by client loop)

Disassembly of `0x0042aff4` (item unpacker):
- Offset 15..16 (`0x43 - 0x34 = 15`): `Parent` index. If non-zero, floor placement is skipped.
- Offset 48 (`0x43 - 0x13 = 48`): `Floor` index.

#### Complete Byte-Exact 57-Byte Record Definition:

| Byte Offset | Size | Type | Field Description |
|---|---|---|---|
| `0..1` | 2 | UInt16 LE | Item ID (`ItemID`) |
| `2..5` | 4 | UInt32 LE | X coordinate |
| `6..9` | 4 | UInt32 LE | Y coordinate |
| `10..13` | 4 | UInt32 LE | Z elevation (always `1`) |
| `14` | 1 | Byte | Rotation (`0..3`) |
| `15..16` | 2 | UInt16 LE | Parent index (`0` for host appliances, `hostIdx` for accessories) |
| `17..18` | 2 | UInt16 LE | **Reserved Padding / Slot 0** (must be `0`) |
| `19..20` | 2 | UInt16 LE | **Accessory Slot 1** (`ebx = 1`) -> child `clientIdx` |
| `21..22` | 2 | UInt16 LE | **Accessory Slot 2** (`ebx = 2`) -> child `clientIdx` |
| `23..38` | 16 | 8x UInt16 LE | **Accessory Slots 3..10** (`ebx = 3..10`) |
| `39..47` | 9 | Byte[] | Padding (9 bytes) |
| `48` | 1 | Byte | Floor index (`0` = 1st floor, `1` = 2nd floor) |
| `49..56` | 8 | Byte[] | Trailing padding (8 bytes, total = 57 bytes) |

---

## 3. Architecture & Implementation

### A. Tent Serialization & Synchronization (`Tent.cs`)

In [`wlo.pserver.core/Game/PlayerRelated/Tent/Tent.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Tent/Tent.cs):
- Replaced shifted offsets with exact byte-aligned 57-byte struct packing.
- `nextAccessoryIndex` dynamically initialized to `rootCount + 1` ensuring accessory client IDs are sequential with host furniture items (`1..N` for hosts, `N+1..` for accessories).
- Child accessory items packed with `parent = hostIdx`, `Z = 1`, and identical 57-byte layout.

### B. Action Code Dispatcher (`AC62.cs`)

In [`Src/Network/ActionCodes/AC62.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC62.cs):
- `Recv4`: Sets `p.Tent.IsDirty = true`, calculates sequential `accessoryIndex`, broadcasts `AC 62:5` to all tent occupants via `p.Tent.Broadcast(confirmation)`, and saves tent state with `cGlobal.gCharacterDataBase?.SaveTentData(p, force: true)`.
- Omitted immediate `SendTentItemsToAll()` broadcast to protect the client's live session and active crafting window.
- `Recv1` & `Recv3`: Enforces bounding box collision detection using `p.Tent.IsAreaOccupied`. Rejects overlapping placements or moves with system message `"Alan dolu!"`.
- `Recv1`: Corrected confirmation packet payload to match the 57-byte struct.

### C. Spatial Occupancy & Item Dimensions (`Tent.cs`, `Inventory.cs`)

In [`wlo.pserver.core/Game/PlayerRelated/Tent/Tent.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Tent/Tent.cs):
- In `PlaceItem`, populated `TentItem` with real `PhxItemInfo` loaded from `_owner?.Inv?.ItemDat?.GetItemByID(itemID)`.
- Multi-cell items now possess authentic dimensions: Raft (`48010`) occupies 4x3 cells, Workbench (`38049`) occupies 3x2 cells, and Trash Can (`38055`) occupies 2x2 cells.
- Implemented `IsAreaOccupied(x, y, w, h, floor, ignoreItem)` taking rotation into account (swapping width and height on rotations 1 and 3).

### D. Inventory Material Deduction & Crafting Disguise Fix (`Inventory.cs`, `AC64.cs`)

- In [`wlo.pserver.core/Game/Inventory.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Inventory.cs): Updated `RemoveItem(itemId, count)` to handle unstacked items with `this[a].Ammt == 0` (evaluating as `Math.Max((byte)1, this[a].Ammt)`), preventing crafting failures on single materials.
- In [`Src/Network/ActionCodes/AC64.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC64.cs): Removed `r.CurMap?.Broadcast(Tools.FromFormat("bbdw", 5, 5, r.CharID, (ushort)0));` which was causing character transformation into an NPC upon craft completion.
