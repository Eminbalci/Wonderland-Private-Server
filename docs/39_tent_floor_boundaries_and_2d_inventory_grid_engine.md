# 39. Tent Floor Boundaries & 2D Inventory Grid Engine

## 1. Overview & Architectural Summary
This technical document formalizes the resolution of two interconnected native client systems in Wonderland Online (WLO):
1. **Tent Interior Furniture Floor Bounds (`aLogin.exe` `0x3e6b78` & `0x3e5ccc`)**: Resolution of client rejection of default furniture (Resource Recycling ID `38055` and Work Platform ID `38049`) caused by out-of-bounds coordinates.
2. **2D Multi-Cell Inventory Grid Engine (`aLogin.exe` `0x3f686c`, `Data/Item.dat` 451-byte record layout)**: Proper decoding of item dimensions (`cellwidth` and `cellheight`) and implementation of 2D inventory grid collision, slot locking, and space calculation so multi-cell items like Raft (Width 4, Height 3) occupy their authentic multi-slot grid footprint in the player's bag.

---

## 2. Reverse Engineering of Tent Interior Floor Boundaries

### 2.1 Native Boundary Checks in `aLogin.exe`
Disassembly of `aLogin.exe` at `0x3e5ccc` reveals the static bounding boxes for tent floors:
- **Floor 0 (Base Ground Floor)**:
  - Left: `29` (`0x1D`)
  - Top: `38` (`0x26`)
  - Right: `44` (`0x2C`)
  - Bottom: `50` (`0x32`)
- **Floor 1 (Second Floor)**:
  - Left: `29` (`0x1D`), Top: `32` (`0x20`), Right: `44` (`0x2C`), Bottom: `50` (`0x32`)
- **Floor 2 (Third Floor / Enlarged)**:
  - Left: `29` (`0x1D`), Top: `27` (`0x1B`), Right: `44` (`0x2C`), Bottom: `50` (`0x32`)

At `0x3e6cad` and `0x3e6d82`, the client validates each placed item via `PtInRect`:
1. `PtInRect(FloorRect, Pt(X, Y))`
2. `PtInRect(FloorRect, Pt(X + Width - 1, Y + Height - 1))`

If either check fails, the client marks error code `1` (`0x3e6cb6` / `0x3e6d8b`) and discards the item immediately.

### 2.2 Coordinate Corrections
- Item `38055` (Resource Recycling / Çöp Kutusu): Dimensions `Width = 2, Height = 2`.
  - Position: `(34, 42)`.
  - Footprint: $X \in [34, 35] \subset [29, 44]$, $Y \in [42, 43] \subset [38, 50]$. Passed.
- Item `38049` (Work Platform / Çalışma Tezgahı): Dimensions `Width = 3, Height = 2`.
  - Position: `(37, 42)`.
  - Footprint: $X \in [37, 39] \subset [29, 44]$, $Y \in [42, 43] \subset [38, 50]$. Passed.
- Non-overlapping: Item 38055 covers columns 34–35, Item 38049 covers columns 37–39, leaving column 36 free.

---

## 3. Reverse Engineering of `Data/Item.dat` & 2D Grid Engine

### 3.1 Binary Record Format of `Data/Item.dat`
Analysis of `Data/Item.dat` (size: 3,267,044 bytes across 7,244 records):
- Record size: exactly `451` (`0x1C3`) bytes per item.
- Offset `0` (`0x00`): Item name length (byte).
- Offsets `1..20`: Reversed ASCII item name.
- Offsets `16..17`: ItemID (`ushort`, little-endian), encoded as `(raw ^ 0xEFC3) - 9`.
- Offset `406` (`0x196`): `cellwidth` (byte), encoded as `(raw ^ 0x9A) - 9`.
- Offset `407` (`0x197`): `cellheight` (byte), encoded as `(raw ^ 0x9A) - 9`.

### 3.2 Raft Footprint Verification
- Item `48010` (Raft): Record index 6102 $\implies$ raw bytes `0x97, 0x96` $\implies$ Width = `4`, Height = `3`.
- Item `48016` (Robinson Raft): Record index 6108 $\implies$ raw bytes `0x97, 0x96` $\implies$ Width = `4`, Height = `3`.

### 3.3 2D Inventory Bag Geometry (5 Columns x 10 Rows)
The WLO inventory bag consists of 50 slots arranged in a 5-column by 10-row grid:
- $X = (slot - 1) \pmod 5$ where $0 \le X \le 4$.
- $Y = (slot - 1) / 5$ where $0 \le Y \le 9$.
- $slot = Y \times 5 + X + 1$ where $1 \le slot \le 50$.

For an item of size $(W, H)$ placed at root slot $S$:
1. **Right Edge Constraint**: $X + W \le 5$ (prevents row-wrapping).
2. **Bottom Edge Constraint**: $Y + H \le 10$ (prevents overflowing 50 slots).
3. **Master/Slave Slot Allocation**:
   - Master Slot $S$: Contains `ItemID`, `Ammt`, `Parent = 0`, `isLocked = false`.
   - Slave Slots $(X + c, Y + r) \neq (X, Y)$: Have `ItemID = 0`, `Ammt = 0`, `Parent = S`, `isLocked = true`.
4. **Collision Prevention**: `CanFitItem` verifies that all $(W \times H)$ cells are unassigned (`ItemID == 0 && Parent == 0 && !isLocked`).
5. **Slot Deallocation**: On removal or move from root slot $S$, `UnlockSlaveSlots(S)` immediately resets all slave cells (`Parent == S`) to free cells (`Parent = 0, isLocked = false`).
6. **Capacity Accounting**: `FilledCount` accounts for both master and locked slave slots; `unFilledCount` equals $50 - \text{FilledCount}$.

---

## 4. Modified Classes & Database Tables

- [`PhxItemDat`](file:///D:/GitHub/Wonderland-Private-Server/PhoenixData/DataFiles/PhxItemDat.cs): Aligned file reading loop to 451-byte records, properly extracting `cellwidth` and `cellheight` alongside item metadata and populating lookup dictionary.
- [`Item`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Item.cs): Updated `Width` and `Height` virtual properties to query `Data.cellwidth` and `Data.cellheight`.
- [`Inventory`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Inventory.cs): Implemented `GetCol`, `GetRow`, `GetSlot`, `CanFitItem`, `FindAvailableSlot`, `LockSlaveSlots`, `UnlockSlaveSlots`, and `RebuildOccupancyGrid`; updated `AddItem`, `MoveItem`, `RemoveItem`, `CanAddItems`, `FilledCount`, and `unFilledCount`.
- [`Tent`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Tent/Tent.cs): Adjusted default furniture placement coordinates to $(34, 42)$ and $(37, 42)$.
- [`GameDataBase`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/GameDataBase.cs): Added `c.Inv.RebuildOccupancyGrid()` on player login.
- [`chartent_items`](file:///D:/GitHub/Wonderland-Private-Server/Data/ServerDataBase.db): Updated persisted coordinates for items `38055` and `38049` for player `10001`.
