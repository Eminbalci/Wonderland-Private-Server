# Technical Specification 42: Inventory Grid Footprint Standards & Transport Item Dimensions

## 1. Overview
In Wonderland Online (WLO), the player inventory bag consists of a 50-slot grid (5 columns by 10 rows). Every inventory item in the bag—including large water transport vehicles such as the Raft (`48010`, `48016`) and Sailboat (`48009`)—occupies exactly one single 1x1 inventory cell.

## 2. Problem Statement
Previously, player inventories displayed vehicles such as Rafts consuming multiple slots (e.g. 4x3 cells), which blocked surrounding slots and corrupted slot assignment.

## 3. Disassembly & Binary Analysis
1. **Item.dat Binary Structure:**
   - In `Item.dat` (record size 451 bytes):
     - Offset 406: `rawW` (`cellwidth = ((rawW ^ 0x9A) - 9)`)
     - Offset 407: `rawH` (`cellheight = ((rawH ^ 0x9A) - 9)`)
   - For Raft (`48010`): `rawW = 151` -> `w = 4`, `rawH = 150` -> `h = 3`.
2. **Footprint vs. Bag Semantics:**
   - Client reverse engineering of `aLogin.exe` (`FUN_003e6b78` and `FUN_003e695c`) confirms that `cellwidth` and `cellheight` define the **tent housing / world placement footprint** (the physical space occupied when placed as an interactive object on the ground), NOT the inventory bag space.
   - The native client inventory UI (`aLogin.exe` `FUN_002a2490` and `FUN_002d6ad8` AC 23) operates strictly on 1-slot indices (`1..50`). Multi-cell bag occupation was a server-side misinterpretation.

## 4. Implementation Details
In [`Inventory.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Inventory.cs):
- [`GetItemWidth`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Inventory.cs#L326-L331): Returns `1` for all items in the inventory bag.
- [`GetItemHeight`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Inventory.cs#L333-L338): Returns `1` for all items in the inventory bag.
- [`TentItem.Width`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Item.cs#L146) and [`TentItem.Height`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Item.cs#L145): Preserve authentic `Data.cellwidth` and `Data.cellheight` for housing placement and collision checks inside the personal tent.

## 5. Verification
- Verified `Inventory.GetItemWidth(48010) == 1` and `Inventory.GetItemHeight(48010) == 1`.
- Verified `TentItem.Width` and `TentItem.Height` retain authentic footprint dimensions (`4` and `3` for raft; `2` and `2` for workbench).
- Compiled solution with 0 warnings and 0 errors.
