# Technical Specification: Tent Furniture Tool Accessory Persistence and Client Formula Unlock Protocol

## 1. Overview & Operational Objective

This specification details the reverse-engineered client validation engine, memory layout, network serialization, and server-side synchronization for attaching tool items (such as Stone Knife `#38058` and Wooden Saw `#38036`) onto tent appliances (specifically Work Platform / Workbench `#38049`) in Wonderland Online (`aLogin.exe`).

### Problem Statement
When players attached tool accessories to the Work Platform, the tools were deleted from the inventory and correctly saved in SQLite database table `chartent_items` (`accessories = '38058,38036'`). However:
1. Upon crafting an item (such as Wooden Basin `#38017`), re-entering the tent, or restarting the client, all tool-dependent recipes (Raft `#48010`, Lauan Wood `#45001`..`#45004`, Wooden Basin `#38017`) vanished from the Work Platform interface, leaving only the 7 base formulas.
2. In client binary `aLogin.exe`, tent accessory items sent via `AC 62:4` were being rejected and discarded during deserialization, leaving `tentArray[accessoryIndex]` all zeroes (`ItemID = 0`), which prevented the client recipe tool collector from identifying the attached tools.

---

## 2. Reverse-Engineering Client Preconditions in `aLogin.exe`

Decompilation and disassembly of `C:\Games\WLRI\aLogin.exe` across the packet deserializer (`0x42d8ea`), item unpacker (`0x42aff4`), item inserter (`0x4133fc`), collision validator (`0x4169e0`), floor grid engine (`0x4167c4`), and recipe tool collector (`0x439b92`) revealed three strict preconditions required for the client to register attached tools:

### A. Coordinate Precondition: `X == 0 && Y == 0 && Z == 0` (`0x416ae1` - `0x416af1`)
```assembly
0x416ae1: test esi, esi                    ; X == 0 ?
0x416ae3: jne 0x416af1
0x416ae5: cmp dword ptr [ebp - 0x47], 0    ; Y == 0 ?
0x416ae9: jne 0x416af1
0x416aeb: cmp dword ptr [ebp - 0x43], 0    ; Z == 0 ?
0x416aef: je 0x416b27                      ; Jumps over PtInPolygon!
0x416af1: lea ecx, [ebp - 0x218]
0x416af7: mov edx, dword ptr [ebp - 0x47]
0x416afa: mov eax, esi
0x416afc: call 0x20cd4                     ; WorldToScreen coords
0x416b01: lea eax, [ebp - 0x218]
0x416b0e: lea edx, [ebx + edx*8 + 0x2e58]  ; Floor polygon
0x416b15: call 0x485db8                    ; PtInPolygon(point, polygon)
0x416b1a: test al, al
0x416b1c: jne 0x416b27
0x416b1e: mov byte ptr [ebp - 1], 1
0x416b22: jmp 0x416db5                     ; Returns error code 1 (REJECTED)
```
- **Flaw:** Previously, the server transmitted `Z = 1` for accessory entries in `SendTentItemsToPlayer`.
- **Consequence:** Because `Z != 0`, `0x416aeb` failed to jump, routing coordinate `(0, 0)` into `PtInPolygon`. Because `(0, 0)` lies outside the tent floor room polygon, `0x485db8` returned `0`, aborting the item placement.

### B. Floor Offset Precondition: `Offset 48 != 0` (`0x413496` - `0x4134b4`)
```assembly
0x413485: mov edx, edi                     ; ItemID
0x413487: mov eax, dword ptr [ebx + 0x2ec4]
0x41348d: call 0x41c900
0x413492: test al, al
0x413494: jne 0x4134b4
0x413496: cmp byte ptr [ebp - 0xa], 0      ; Offset 48 (Floor Index)
0x41349a: jne 0x4134b4                     ; Bypasses 0x4167c4 if Floor != 0!
0x41349c: mov al, byte ptr [ebp - 0xb]
0x41349f: push eax
0x4134a0: mov ecx, esi
0x4134a2: lea edx, [ebp - 0x3a]
0x4134a5: mov eax, ebx
0x4134a7: call 0x4167c4                    ; 2D Floor Grid Collision Check
0x4134ac: test al, al
0x4134ae: je 0x413535                      ; Aborts item insertion if 0!
0x4134b4: inc word ptr [ebx + 0x18]        ; Increments total item count
0x4134b8: movzx eax, si                    ; clientIndex
0x4134bb: imul edi, eax, 0x39              ; 57 bytes per item
0x4134c0: lea edi, [ebx + edi + 0x1a]      ; Destination in tentArray
0x4134c4: lea esi, [ebp - 0x3a]            ; Source 57-byte struct
0x4134cc: rep movsd                        ; Copies item into tentArray!
```
- **Flaw:** Previously, the server transmitted `Floor = 0` (ground floor) for accessory entries.
- **Consequence:** Because `Floor == 0`, `0x413496` fell through to `0x4167c4`. Inside `0x4167c4`, the client converted `(X=0, Y=0)` to 2D grid cell indices via `(X - 29 + 1) = -28` and `(Y - 27 + 1) = -26`, failing boundary verification in `0x416628` and aborting before `rep movsd` could copy the item into `tentArray`.
- **Resolution:** Setting `Floor = 1` for accessories instructs `0x413496` to jump directly to `0x4134b4`, bypassing ground grid tile checks and successfully storing the accessory in `tentArray`.

### C. Client Recipe Tool Collector Execution (`0x439b92` - `0x439c25`)
```assembly
0x439b92: mov ebx, 1
0x439b97: movzx eax, si                    ; si = host appliance clientIndex (e.g., 2)
0x439b9a: imul eax, eax, 0x39
0x439b9d: mov edx, dword ptr [0x4caa28]
0x439ba3: mov edx, dword ptr [edx]
0x439ba5: mov edx, dword ptr [edx + 0x523c] ; tentArray base
0x439bab: lea eax, [edx + eax]
0x439bae: mov ax, word ptr [eax + ebx*2 + 0x2b] ; reads host.slots[ebx]
0x439bb3: test ax, ax
0x439bb6: jbe 0x439c21
0x439bfb: movzx eax, di                    ; di = child accessoryIndex (e.g., 4)
0x439bfe: imul eax, eax, 0x39
0x439c01: mov edx, dword ptr [0x4caa28]
0x439c07: mov edx, dword ptr [edx]
0x439c09: mov edx, dword ptr [edx + 0x523c]
0x439c0f: mov ax, word ptr [edx + eax + 0x1a] ; reads tentArray[child].ItemID
0x439c1c: mov word ptr [ecx + edx*2 - 2], ax   ; appends tool to available tools list!
0x439c21: inc ebx
0x439c22: cmp ebx, 0xb
0x439c25: jne 0x439b97
```
Once `tentArray[4]` and `tentArray[5]` are populated with `38058` and `38036`, the loop extracts the tool IDs and immediately evaluates unlocked crafting recipes in `Compound2.dat`.

---

## 3. Byte-Exact 57-Byte Tent Item Serialization Specification

| Offset | Size | Type | Floor Furniture (Host) | Attached Accessory (Child) |
|---|---|---|---|---|
| `0..1` | 2 | UInt16 LE | Appliance `ItemID` (e.g. `38049`) | Tool `ItemID` (e.g. `38058`, `38036`) |
| `2..5` | 4 | UInt32 LE | Tile X coordinate (`29..44`) | `0` |
| `6..9` | 4 | UInt32 LE | Tile Y coordinate (`38..50`) | `0` |
| `10..13` | 4 | UInt32 LE | Elevation `Z = 1` | `0` *(Bypasses `PtInPolygon`)* |
| `14` | 1 | Byte | Rotation (`0..3`) | `0` |
| `15..16` | 2 | UInt16 LE | Parent Index: `0` | Parent Index: `hostClientIndex` (`2`) |
| `17..18` | 2 | UInt16 LE | Reserved / Slot 0: `0` | Reserved / Slot 0: `0` |
| `19..38` | 20 | 10x UInt16 LE | `accessorySlots[1..10]` (`4, 5, 0..0`) | All `0` |
| `39..47` | 9 | Byte[9] | Zero padding | Zero padding |
| `48` | 1 | Byte | Floor Index (`0` = Ground, `1` = Upstairs) | `1` *(Bypasses ground grid collision)* |
| `49..56` | 8 | Byte[8] | Trailing padding | Trailing padding |

Each entry is prefixed with its 2-byte UInt16 LE `clientIndex` (`1..N` for floor objects, `N+1..` for accessories). Total length per item entry is `59` bytes.

---

## 4. Implementation Details

### A. Tent Serialization Engine (`Tent.cs`)
In [`wlo.pserver.core/Game/PlayerRelated/Tent/Tent.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Tent/Tent.cs) inside `SendTentItemsToPlayer`:
- Updated accessory entry packing:
  ```csharp
  pkt.Pack32(0); // Z = 0 (Bypasses PtInPolygon at 0x416ae1 in aLogin.exe)
  ...
  pkt.Pack8(1);  // Offset 48: floor = 1 (Bypasses ground grid 2D collision at 0x413496 in aLogin.exe)
  ```

### B. Action Code Dispatcher (`AC62.cs`)
In [`Src/Network/ActionCodes/AC62.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC62.cs):
1. **Tool Attachment (`Recv4`):**
   - Dispatches incremental `AC 62:5` confirmation.
   - Follows with `p.Tent.SendTentItemsToAll()` (`AC 62:4`), refreshing all occupants' client memory with valid accessory records.
   - Persists state to SQLite via `SaveTentData(p, force: true)`.
2. **Furniture Pickup (`Recv2`):**
   - Added automatic tool refunding: if an appliance containing attached tools is picked up, all attached tool items are restored into the player's bag inventory before removing the host item.

---

## 5. Verification

1. **Compilation:** Built solution with `dotnet build "Wonderland Private Server.sln"` resulting in 0 Warnings and 0 Errors.
2. **Database Integrity:** Verified `chartent_items` row `381` retains `accessories = '38058,38036'` for Workbench `#38049`.
3. **Client Acceptance:** With `Z = 0` and `Floor = 1`, `aLogin.exe` passes both `0x416ae1` and `0x413496`, populating `tentArray[4]` and `tentArray[5]` and unlocking tool-dependent recipes.
