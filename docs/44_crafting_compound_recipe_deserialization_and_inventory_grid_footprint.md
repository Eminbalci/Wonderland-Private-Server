# Technical Specification 44: Crafting Compound Recipe Deserialization & Inventory Grid Multi-Cell Footprint

## 1. Overview
This specification documents the resolution of two interconnected core gameplay defects in Wonderland Online (WLO):
1. **Crafting Failure Defect ("Yetersiz malzeme!")**: When players possessed required materials and interacted with a manufacturing appliance (such as Low Workbench `38049` or Woodworking Lathe `38058`), crafting failed with a "Yetersiz malzeme!" (Insufficient materials) error due to packet pointer desynchronization and record 0 header contamination in `Compound2.dat`.
2. **Visual Footprint & Grid Overlap Defect**: Large items such as Rafts (`48010`, `48016`) rendered across a 4-column by 3-row grid space on the client inventory interface, but the server evaluated them as 1x1, allowing other bag items to occupy adjacent slots and visually render inside or underneath the Raft sprite.

---

## 2. Root Cause Analysis

### 2.1 Action Code 64 Packet Pointer Offset (`AC64.cs`)
In the Wonderland Online protocol architecture, incoming client packet buffers follow this structure:
- Bytes `0..1`: Protocol magic header (`0xF4, 0x44`).
- Bytes `2..3`: 16-bit packet payload length (little-endian).
- Byte `4`: Action Code (`AC`, e.g. `64` / `0x40`).
- Byte `5`: Sub-action Code (`SubCode`, e.g. `1` for manufacturing request).
- Byte `6+`: Action payload.

In [`Player.ProcessSocket`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Player.cs#L1200), the packet pointer is initialized to 4 and unpacks byte 4, leaving `p.Ptr = 5`.
In [`AC64.ProcessPkt`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC64.cs), `p.SetPtr(6)` was omitted. Consequently:
- `benchIndex = p.Unpack8()` unpacked byte 5 (which contained the subcode `1` rather than `benchIndex`).
- `formulaIndex = p.Unpack16()` unpacked byte 6 (`benchIndex`) and byte 7 (the low byte of `formulaIndex`).
- The resulting `formulaIndex` integer was bitwise scrambled (e.g., formula 526 was unpacked as 3585), resulting in out-of-bounds recipe lookups or referencing incorrect formulas whose required materials differed from the player's inventory.

### 2.2 Record 0 Header Contamination (`Compound2.dat`)
Disassembly of client binary `aLogin.exe` at `0x2BC80C` reveals:
- `Data/Compound2.dat` begins with a 65-byte version header where offset 0 contains word `6` (`0x0006`) followed by 63 zero bytes.
- Recipes are indexed starting at 1 (`esi = 1` at `0x2BC41D`) through record 788.
- The previous implementation in [`Compound2Dat.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/DataFiles/Compound2Dat.cs) treated record 0 as an active recipe and passed it through XOR decryption (`wordXor` and `byteXor`), generating corrupted required material counts (`materialAmmt1 = 208`) and misaligning zero-based array lookups against 1-based formula indices sent by the client.

### 2.3 Double Database Collision (`MainForm1.cs`)
In `Form1_Load`, `Compound.dat` (an obsolete 168-record database with version 2 header) was loaded and appended with `Compound2.dat`, shifting all recipe indices by 168. `aLogin.exe` does not reference `Compound.dat` and exclusively consumes `Compound2.dat`.

### 2.4 Inventory Grid Multi-Cell Footprint (`Inventory.cs`)
[`Inventory.GetItemWidth`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Inventory.cs#L326) and [`Inventory.GetItemHeight`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Inventory.cs#L333) previously forced `return 1;` for all inventory bag items. While this allowed 1x1 placement, the client graphical UI renders large transport items like Raft (`48010`) using authentic dimensions from `Item.dat` (`cellwidth = 4`, `cellheight = 3`). Because slave cells were unlocked on the server, subsequent items were placed in slots 37..39, 41..44, and 46..49, visually clipping through the Raft.

---

## 3. Implementation Details

### 3.1 AC64 Packet Synchronization & Safe Delivery
In [`AC64.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC64.cs):
- Added `p.SetPtr(6);` in `ProcessPkt` to align byte reading directly with the payload:
  - `benchIndex = p.Unpack8();` (Byte 6)
  - `formulaIndex = p.Unpack16();` (Bytes 7..8)
- Replaced single-slot `RemoveItemById` with multi-slot atomic deduction `r.Inv.RemoveItem(formula.materialID, formula.materialAmmt)`.
- Implemented delivery branching based on `ItemDat` properties:
  - **Furniture Machinery / Tent Appliances:** Where `(itemData.ItemType == 29 || itemData.ItemType == 30) && itemData.ItemType != 39 && (itemData.cellwidth > 1 || itemData.cellheight > 1)`:
    - Places the physical appliance on tent floor `(42, 42)` via `r.Tent.PlaceItem`.
    - Dispatches `AC 64:1`, `AC 64:2`, and `AC 64:4` for tent workbench animation.
  - **Transport Vehicles & Inventory Items:** Where `itemData.ItemType == 39` (Raft `48010`/`48016`), handheld tools (`cellwidth == 1 && cellheight == 1`), equipment, consumables, or materials:
    - Adds the crafted item to player bag via `r.Inv.AddItem(outId, count)`.
    - Dispatches `AC 64:9` (`[64, 9]`) acknowledging direct inventory receipt.
- Broadcasts sparkles effect `AC 5:5` and sends confirmation system notification.

### 3.2 Compound2 Deserialization Engine
In [`Compound2Dat.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/DataFiles/Compound2Dat.cs):
- Added guard in `cBuildElement.Load`:
  ```csharp
  buildCode = (ushort)index;
  if (index == 0)
  {
      resultID = 0; planID = 0; toolID = 0; ammtRecv = 0;
      materialID1 = materialID2 = materialID3 = materialID4 = materialID5 = 0;
      materialAmmt1 = materialAmmt2 = materialAmmt3 = materialAmmt4 = materialAmmt5 = 0;
      buildTime = 0;
      return;
  }
  ```
- Guaranteed `Loaded = true` on completion.

### 3.3 Database Alignment in Server Startup
In [`MainForm1.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.cs#L158):
- Removed obsolete `Compound.dat` loader and append operation, ensuring `cGlobal.gCompoundDat` loads `Compound2.dat` exclusively with identical 1-based indexing matching client expectations.

### 3.4 Multi-Cell Footprint Restoration
In [`Inventory.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Inventory.cs#L326-L337):
- Restored `GetItemWidth` and `GetItemHeight`:
  ```csharp
  public byte GetItemWidth(ushort itemId)
  {
      var info = ItemDat?.GetItemByID(itemId);
      if (info != null && info.cellwidth > 0 && info.cellwidth <= 10) return info.cellwidth;
      return 1;
  }

  public byte GetItemHeight(ushort itemId)
  {
      var info = ItemDat?.GetItemByID(itemId);
      if (info != null && info.cellheight > 0 && info.cellheight <= 10) return info.cellheight;
      return 1;
  }
  ```
- When Raft (`48010`) is added or loaded into the bag, `LockSlaveSlots` locks all 11 surrounding slave slots with `Parent = rootSlot, isLocked = true`.
- `CanFitItem` evaluates slave slot status, blocking items from overlapping into the Raft graphic.

---

## 4. Empirical Verification

Automated test script [`.codex-verify/Test-CraftingAndRaftFootprint.ps1`](file:///D:/GitHub/Wonderland-Private-Server/.codex-verify/Test-CraftingAndRaftFootprint.ps1) verified:
1. **Recipe Deserialization:** Loaded all 789 records from `Compound2.dat`, verified record 0 is zeroed, recipe 526 requires `43002 x1` and `45007 x1` (yielding Stone Knife `38058`), and recipe 744 requires `45007 x5` and `37132 x9` (yielding Raft `48010`).
2. **Multi-Cell Grid Integrity:** Added Raft (`48010`) to inventory. Verified 4x3 footprint, confirmed 1 master slot and exactly 11 slave slots locked, and verified `CanFitItem` rejects adjacent overlapping placement.
3. **End-to-End AC 64:1 Execution:** Dispatched client packet `[F4 44 05 00 40 01 01 0E 02]` with materials present in inventory. Ingredients were consumed atomically, item `38058` was placed in inventory slot 1, `AC 23:9`, `AC 23:5`, `AC 64:9`, and `AC 23:57` packets were emitted, and no errors occurred.
4. **Compilation:** `dotnet build` succeeded with 0 Warnings and 0 Errors.
