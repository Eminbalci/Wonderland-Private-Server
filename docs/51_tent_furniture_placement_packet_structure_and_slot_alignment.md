# Technical Specification: Tent Furniture Placement Packet Structure and Slot Offset Alignment

## 1. Overview & Operational Objective

This specification documents the root cause, reverse-engineered network packet layout, and resolution for the defect preventing players from placing furniture items from their inventory onto the tent floor in Wonderland Online (`aLogin.exe`).

### Problem Statement
When a player dragged furniture (such as Wooden Basin `#38017`, Workbench `#38049`, etc.) from their inventory to place it onto the tent floor, the server rejected the placement with:
```
[Tent] AC62,1 Place: Bag=32 Slot=40 Pos=(637534208,16777216) Flr=0
[Tent] Rejected AC62 placement: Invalid slot 40 for test
```
The furniture remained stuck in the player's inventory and could not be deployed inside the tent.

---

## 2. Reverse-Engineering Client Packet Serialization in `aLogin.exe`

Disassembly of `C:\Games\WLRI\aLogin.exe` at `0x42769a` (ActionCode `0x3E:1` dispatch) and `0x2d610c` (network packet serializer for AC 62 Subcode 1) revealed the exact 14-byte payload structure:

```assembly
; 0x2d610c: ActionCode 62:1 packet builder
0x2d6112: mov dl, byte ptr [ebp - 5]       ; ActionCode (0x3E = 62)
0x2d6132: mov dl, byte ptr [ebp - 6]       ; SubCode (0x01 = 1)
0x2d616d: mov dl, byte ptr [edx + 0x1bd8]  ; Inventory Slot Index (1 Byte: 1..50)
0x2d61b0: mov edx, dword ptr [eax + 0x1ba1] ; X coordinate (UInt32 LE, 4 Bytes)
0x2d61d5: mov edx, dword ptr [eax + 0x1ba5] ; Y coordinate (UInt32 LE, 4 Bytes)
0x2d61fa: mov edx, dword ptr [eax + 0x1ba9] ; Z elevation (UInt32 LE, 4 Bytes)
0x2d6220: mov dl, byte ptr [edx + 0x1bad]  ; Floor index (1 Byte: 0 or 1)
```

### Exact Packet Wire Layout (`Len = 20 Bytes`):
```
[Header: F4 44 10 00] [ActionCode: 3E] [SubCode: 01] [SlotIndex: 1B] [X: 4B LE] [Y: 4B LE] [Z: 4B LE] [Floor: 1B]
```

| Byte Offset | Size | Field | Example Values | Description |
|---|---|---|---|---|
| `0..3` | 4 | Header | `F4 44 10 00` | Packet magic `0x44F4` + Payload Length `16` (`0x0010`) |
| `4` | 1 | ActionCode | `3E` (62) | Tent Action Code |
| `5` | 1 | SubCode | `01` (1) | Place item from inventory |
| `6` | 1 | SlotIndex | `16` (22) or `20` (32) | 1-based bag slot index containing the furniture |
| `7..10` | 4 | X Coordinate | `28 00 00 00` (40) | Floor tile anchor X coordinate (`29..44`) |
| `11..14` | 4 | Y Coordinate | `26 00 00 00` (38) | Floor tile anchor Y coordinate (`38..50`) |
| `15..18` | 4 | Z Coordinate | `01 00 00 00` (1) | Floor elevation layer |
| `19` | 1 | Floor Index | `00` (0) | Floor level (`0` = Ground floor, `1` = Upstairs) |

---

## 3. Root Cause Analysis

Prior to this fix, [`Src/Network/ActionCodes/AC62.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC62.cs) `Recv1` unpacked fields with an erroneous extra byte:
```csharp
byte bagIndex = r.Unpack8();        // Erroneously unpacked phantom bag index!
byte slotIndex = r.Unpack8();       // Ate 1st byte of X (e.g. 40)
uint x = r.Unpack32();              // Ate 3 bytes of X + 1st byte of Y (0x26000000 = 637534208)
uint y = r.Unpack32();              // Ate 3 bytes of Y + 1st byte of Z (0x01000000 = 16777216)
uint floor = r.Unpack32();          // Ate 3 bytes of Z + Floor byte
```

Because of this 1-byte shift:
1. `bagIndex` consumed the real `slotIndex` (`32`).
2. `slotIndex` read the lowest byte of `X` (`40`).
3. `p.Inv[40]` was empty, triggering an invalid slot rejection (`Rejected AC62 placement: Invalid slot 40`).
4. If slot 40 happened to have an item, `X` was evaluated as `637534208`, failing `IsInFloorBounds` bounds validation.

---

## 4. Implementation Details

In [`Src/Network/ActionCodes/AC62.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC62.cs) `Recv1`:
```csharp
byte slotIndex = r.Unpack8();       // 1-based bag slot index (1..50)
uint x = r.Unpack32();              // 32-bit integer LITTLE ENDIAN
uint y = r.Unpack32();              // 32-bit integer LITTLE ENDIAN
uint z = r.Unpack32();              // 32-bit integer LITTLE ENDIAN
if (z == 0) z = 1;
byte floor = 0;                     // Floor index (1B)
try { floor = r.Unpack8(); } catch { }
if (floor > 1) floor = 0;
```

Placement flow:
1. Extracts `placeItemId = p.Inv[slotIndex].ItemID`.
2. Validates spatial boundaries via `p.Tent.IsInFloorBounds((int)x, (int)y, itemW, itemH, (int)floor)` and `IsAreaOccupied`.
3. Deducts 1 item from inventory via `p.Inv.RemoveItem(slotIndex, 1, true)`.
4. Instantiates item in tent room via `p.Tent.PlaceItem(placeItemId, (int)x, (int)y, (int)floor, 0)`.
5. Transmits 61-byte `AC 62:1` confirmation to client (triggering sprite allocation at `0x435920` and UI update at `0x42deac`).
6. Synchronizes room state to all occupants via `p.Tent.SendTentItemsToAll()` and commits to database via `SaveTentData(p, force: true)`.

---

## 5. Verification

1. **Compilation:** Solution compiled successfully with `dotnet build "Wonderland Private Server.sln"`: 0 Warnings, 0 Errors.
2. **Wire Compatibility:** Unpacking matches the 20-byte client transmission `F4 44 10 00 3E 01 [Slot: 1B] [X: 4B] [Y: 4B] [Z: 4B] [Floor: 1B]`.
3. **Database Consistency:** Placed items are persisted into `chartent_items` with exact coordinates, floor, rotation, and accessory slots.
