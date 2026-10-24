# Technical Specification 41: Tent Housing Interior Floor Bounds Protocol & Furniture Lifecycle

## 1. Problem Statement
Players entering their personal tent reported that the default beginner furniture items—Resource Recycling / Trash Can (`38055`) and Work Platform / Low Workbench (`38049`)—did not render or spawn on the floor, preventing the player from seeing, moving, or picking them up into inventory.

## 2. Root Cause Analysis (Disassembly & Reverse Engineering)
Static analysis of client binary `aLogin.exe` (`FUN_003fd644` lines 417894–418520, `FUN_003fb18c` lines 416658–416860, `FUN_003e3594` lines 398460–398535, `FUN_00404e4c` lines 422440–422507, `FUN_003e5ccc` lines 400290–400307, `FUN_003e67c0` lines 400983–401004, and `FUN_003e6b78` lines 401244–401360) revealed the exact failure mechanisms:

1. **Tent Interior Map (`63507` / `0xF813`):**
   - When entering personal housing, the client switches to map ID `63507` (0xF813).
   - In `FUN_003fd644`, the client validates housing map state using `FUN_0049ed0c(MapID)` (`0xF80C < MapID < 0xFFDD`).
2. **Floor Bounding Box Rectangles:**
   - In `FUN_003e5ccc`, the client calculates the floor bounding rectangle based on `TentType`:
     - `TentType = 1` (Standard Tent): `Rect(Left=29, Top=38, Right=44, Bottom=50)`
     - `TentType = 2` (Enlarged 1): `Rect(Left=29, Top=32, Right=44, Bottom=50)`
     - `TentType = 3` (Enlarged 2): `Rect(Left=29, Top=27, Right=44, Bottom=50)`
     - `TentType = 0` (Uninitialized): `Rect(Left=0, Top=0, Right=0, Bottom=0)`
3. **Mandatory Floor Initialization Packet (`AC 65:12`):**
   - The client only updates its internal floor type array (`*(char *)(*(int *)PTR_DAT_004c9588 + 0x7325 + floor)`) when it receives `AC 65, SubCode 12` (`[65, 12, TentType, Floor]`).
   - Handler `FUN_00404e4c` executes `FUN_003e5ccc(local_5, local_18)` and `FUN_003e3dbc(..., local_18, floor)`, configuring the floor bounds and loading the interior tent sprite (`10005` for standard tent).
   - Because the server previously never transmitted `AC 65:12`, the client floor bounds remained `(0, 0, 0, 0)`.
4. **Z-Layer Elevation Grid Requirement (`FUN_003e67c0`):**
   - In `FUN_003e67c0(param_1, coords, slot, grid)`:
     ```c
     if ((((0 < iVar1) && (iVar1 < 0x11)) && (0 < iVar2)) &&
        (((iVar2 < 0x19 && (0 < iVar3)) && (iVar3 < 5))))
     ```
   - `iVar1 = PosX - 28`, `iVar2 = PosY - 26`, and `iVar3 = Z (Layer / Elevation)`.
   - The client strictly checks `0 < iVar3 && iVar3 < 5`.
   - Furniture sitting on the ground floor **MUST HAVE Z = 1**.
   - When the server sent `Z = 0`, the client evaluated `0 < 0` as FALSE, rejecting and discarding the item in `FUN_003e3594`.
5. **Exact 57-Byte Item Layout in `AC 62:4`:**
   - Offset 0..1 (USHORT): `ItemID` (`38055` / `38049`)
   - Offset 2..5 (INT32): `PosX` (Tile X, e.g. 34 / 37)
   - Offset 6..9 (INT32): `PosY` (Tile Y, e.g. 42)
   - Offset 10..13 (INT32): `Z` (Layer / Elevation: **MUST BE 1**)
   - Offset 14 (BYTE): `Rotate` (0..3)
   - Offset 15..46 (32 BYTES): Zero padding
   - Offset 47 (BYTE): `Floor` (Tent floor index: `0` for downstairs, `1` for upstairs)
   - Offset 48..56 (9 BYTES): Trailing zero padding
   - Total per item: 57 bytes (`0x39`) + 2 bytes `clientIndex` (`0x3b = 59` bytes).

## 3. Implementation Details

### A. Tent Initialization & Properties ([`Tent.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Tent/Tent.cs))
- Default `_tenttype = 1`.
- Normalized `TentType` property: minimum 1.
- Implemented `EnsureDefaultItemsIfEmpty()`: generates `PlaceItem(38055, 34, 42, 0, 0)` and `PlaceItem(38049, 37, 42, 0, 0)` if empty.
- Updated [`SendTentItemsToPlayer`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Tent/Tent.cs#L512-L545):
  - Packs `pkt.Pack32(1)` for Z-layer.
  - Packs `pkt.PackArray(new byte[32])`.
  - Packs `pkt.Pack8((byte)item.floor)` at offset 47.
  - Packs `pkt.PackArray(new byte[9])`.

### B. Map Load & Sync Protocol ([`AC12.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC12.cs) and [`Tent.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Tent/Tent.cs))
Ordered transmission sequence upon entering personal tent:
1. `AC 65:12` -> `[65, 12, TentType, 0]` (configures Floor 0 bounds & loads sprite `10005`).
2. `AC 65:12` (Floor 1 if `Enlarged`).
3. `AC 65:7` -> `[65, 7, Locked ? 1 : 0, 0]` (lock state).
4. `AC 62:14` -> `[62, 14, Floor1Color (uint16)]` (floor texture).
5. `AC 62:15` -> `[62, 15, Floor1Wallpaper (uint16)]` (wall texture).
6. `AC 62:4` -> `[62, 4, OwnerCharID (uint32), (ItemEntry: clientIndex (uint16) + 57 bytes item data)...]`.
- Added fallback sync in `AC12.Recv1` even when `Warping` flag was previously consumed.

### C. Housing Action Confirmations ([`AC62.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC62.cs))
- [`Recv1`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC62.cs#L27-L91) (Item Placement): Transmits authentic 61-byte `AC 62:1` packet to client with slot index, item ID, coordinates, Z=1, rotate, and floor byte.
- [`Recv2`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC62.cs#L92-L167) (Item Pickup): Removes item from tent, adds to player inventory with `AddItem`, and sends `[62, 2, rawIndex:2]`.
- [`Recv3`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC62.cs#L168-L230) (Item Move/Rotate): Transmits `[62, 3, rawIndex:2, x:4, y:4, z:4 (1), rot:1]`.

### D. Database Synchronization ([`CharacterDataBase.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/CharacterDataBase.cs))
- In `WriteNewPlayer`: Automatically seeds initial `chartent` and `chartent_items` rows (`38055` at 34,42; `38049` at 37,42).
- In `LoadTentData`: Calls `EnsureDefaultItemsIfEmpty()` and persists them if the player has 0 items.

## 4. Verification & Testing
- Executed packet decoding verification script against client `aLogin.exe` disassembly:
  - Total payload length: 124 bytes.
  - Client item count calculation: `(123 - 5) / 59 == 2`.
  - Item 1: ItemID `38055`, X `34`, Y `42`, Z `1`, Rotate `0`, Floor `0`.
  - Item 2: ItemID `38049`, X `37`, Y `42`, Z `1`, Rotate `0`, Floor `0`.
  - Client `FUN_003e67c0` grid boundary check: `0 < (34-28) < 17`, `0 < (42-26) < 25`, `0 < 1 < 5` -> PASSED.
- Built solution with `dotnet build`: 0 Warnings, 0 Errors.
