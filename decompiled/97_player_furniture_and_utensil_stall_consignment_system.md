# Document 97: Player Furniture and Utensil Stall Consignment Vending Engine

## 1. Architectural Overview

In *Wonderland Online*, player housing (the Tent Camp system) relies on manufactured appliances, crafting tools, and furniture (stoves, sewing machines, metal furnaces, cupboards, bathtubs). To support commerce in these bulky domestic assets, players utilize the **Utensil Stall Consignment Vending Engine** (`Form_Buy1Utensil_1` and `Form_Buy3Utensil_1`), identified internally as **`BeStallUtenSil`**.

Unlike standard consumable items that stack up to 1,250 units, domestic utensils possess individual state records:
1. **Durability & Operational Integrity**: Each consigned appliance retains remaining structural durability points (`CurrentDurability / MaxDurability`). Broken utensils ($0$ durability) are forbidden from sale.
2. **Manufactured Quality & Material Tier**: Preserving alchemy potential grades, craft quality modifiers, and embedded gemstone sockets.
3. **Multi-Slot Showcase Presentation**: Displaying 1 or 3 large furniture plots per vending window, rendering high-resolution isometric furniture sprites directly on the stall viewport.
4. **Network Synchronization**: Managing atomic ownership transfer and placement registry migrations via Action Code 20:8 (`0x14:0x08`).

```
+-----------------------------------------------------------------------------------+
|                        UTENSIL & APPLIANCE STALL ARCHITECTURE                     |
+-----------------------------------------------------------------------------------+
                                          |
                +-------------------------+-------------------------+
                |                                                   |
                v                                                   v
    [Stall Viewport: Form_Buy3Utensil_1]                  [Furniture Integrity Engine]
    - Dimensions: 489x655 px                              - Internal Tag: BeStallUtenSil
    - 3 Large Appliance Showcase Plots                    - Non-Zero Durability Gate
    - Buy / Close Action Triggers                         - Tent Space & Weight Verification
                |                                                   |
                +----------------- Network Wire Link ---------------+
                                          |
                                          v
                      [Action Code 20:8 Utensil Purchase Packet]
```

---

## 2. VCL Form Hierarchy & UI Structure

The utensil stall dialog is constructed in `HL_Shop.pas` and `CY_TradeMenu.pas` (constructor anchored at RVA `0x0034ca9c`):

### 2.1 Form Specifications

| Attribute | Specification |
| :--- | :--- |
| **Pascal Source Unit** | `HL_Shop.pas` / `CY_TradeMenu.pas` |
| **Form Identifiers** | `Form_Buy1Utensil_1` (1 slot), `Form_Buy3Utensil_1` (3 slots) |
| **Dimensions** | Width: 489 px (`0x1E9`), Height: 655 px (`0x28F`) |
| **Subroutine Addresses** | Constructor: `FUN_0034ca9c`, Purchase: `FUN_0034bdbc`, Close: `FUN_0034bfd8` |
| **Internal Tag** | `"BeStallUtenSil"` |

### 2.2 Control Hierarchy (`Form_Buy3Utensil_1`)

```
Form_Buy3Utensil_1 (489 x 655 px)
 ├── UtensilSlot[0]: [Sprite Canvas, Name, Durability, Price] (X: 120, Y: 162, W: 32, H: 32)
 ├── UtensilSlot[1]: [Sprite Canvas, Name, Durability, Price] (X: 324, Y: 162, W: 32, H: 32)
 ├── UtensilSlot[2]: [Sprite Canvas, Name, Durability, Price] (X: 528, Y: 162, W: 32, H: 32)
 ├── PriceInput[0..2]: TNumEditor (MaxLen: 9 digits, Gold Price)
 ├── Btn_Buy_1 (X: 443, Y: 405, W: 56, H: 20, Handler: FUN_0034bdbc)
 └── Btn_CLose_1 (X: 443, Y: 405, W: 56, H: 20, Handler: FUN_0034bfd8)
```

---

## 3. Disassembly & Algorithmic Analysis

### 3.1 Utensil Slot Initialization (`FUN_0034ca9c`)

```c
// Decompiled from aLogin.exe: Lines 327028 - 327095
(**(code **)(*param_1 + 8))(param_1, "Form_Buy3Utensil_1", 0xf, 0x1e9, 0x28f, 0, 0, 1, 0x1e9, 0x28f, 0x4b);

// Iterate through 3 Utensil Showcase Plots
for (iVar2 = 1; iVar2 < 4; iVar2++) {
    piVar3 = (int *)FUN_00371074(PTR_PTR_003336fc, 1, param_1);
    param_1[iVar2 + 0x3b] = (int)piVar3;
    // Layout slot viewport (Stride: 204 px / 0xCC)
    (**(code **)(*piVar3 + 8))(piVar3, 0, (iVar2 - 1) * 0xcc + 0x78, 0x20, 0x20, 0, 0, 1, 0x20, 0x20, 0xa2);
    
    iVar1 = param_1[iVar2 + 0x3b];
    *(int *)(iVar1 + 0xb0) = iVar2;
    // Set internal component identifier
    FUN_00013f00(iVar1 + 4, "BeStallUtenSil");
}

// Register Buy and Close Action Buttons
RegisterButton(param_1 + 0x45, "Btn_Buy_1", &FUN_0034bdbc);
RegisterButton(param_1 + 0x43, "Btn_CLose_1", &FUN_0034bfd8);
```

---

## 4. Consigned Appliance Categories & Space Rules

| Category | Typical Items | Grid Footprint | Weight | Durability Rule |
| :--- | :--- | :---: | :---: | :--- |
| **Crafting Tool** | Wood Stove, Alchemy Pot, Spinning Wheel | $2 \times 2$ (4 tiles) | 120 kg | Must have $\ge 1$ point |
| **Comfort Furniture** | Luxury Bed, Sofa, Dining Table | $3 \times 3$ (9 tiles) | 200 kg | Must have $\ge 1$ point |
| **Sanitation Appliance**| Bathtub, Shower, Spa Pool | $2 \times 3$ (6 tiles) | 250 kg | Must have $\ge 1$ point |
| **Container Vault** | Cupboard, Cabinet, Safe Chest | $1 \times 2$ (2 tiles) | 150 kg | **Must be 100% empty** |

> [!IMPORTANT]
> When purchasing container appliances (Cupboards / Cabinets), the engine mandates that the seller's container inventory must be completely evacuated prior to stall listing. Selling an occupied chest is blocked by validation gate `FUN_0034b890`.

---

## 5. Network Wire Protocol

### 5.1 Client -> Server: Action Code 20:8 Buy Utensil Order

```
+---------------+---------------+---------------+---------------+---------------+
| Magic Header  | Length (LE)   | Action Code   | Sub-Opcode    | Stall Owner ID|
| 0xF4 0x44     | 0x09 0x00     | 0x14          | 0x08          | 0x82 0x04 0x00|
+---------------+---------------+---------------+---------------+---------------+
| Slot Index    | Agreed Gold Price (uint32 LE) |
| 0x01          | 0x00 0x28 0x6B 0x00 (7.02M G) |
+---------------+-------------------------------+
```

### 5.2 Server -> Client: Action Code 20:9 Utensil Transfer Broadcast

```
+---------------+---------------+---------------+---------------+---------------+
| Magic Header  | Length (LE)   | Action Code   | Sub-Opcode    | Result Code   |
| 0xF4 0x44     | 0x0C 0x00     | 0x14          | 0x09          | 0x00 (Success)|
+---------------+---------------+---------------+---------------+---------------+
| Item ID (LE)  | Durability    | MaxDurability | Potential Lvl |
| 0x45 0x08     | 0xE6 0x00     | 0xFA 0x00     | 0x03          |
+---------------+---------------+---------------+---------------+
```

---

## 6. C# Server Implementation Blueprint

```csharp
// UtensilStallService.cs - Wonderland Private Server Furniture Vending Engine
using System;

namespace Wonderland.Server.Economy
{
    public sealed class UtensilStallService
    {
        public bool BuyConsignedUtensil(
            PlayerCharacter buyer,
            PlayerCharacter seller,
            byte slotIndex,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            // 1. Verify Stall State
            if (!seller.IsStallOpen || seller.UtensilStall == null)
            {
                errorMessage = "Stall is no longer available.";
                return false;
            }

            var utensil = seller.UtensilStall.GetItemAt(slotIndex);
            if (utensil == null)
            {
                errorMessage = "Item already sold.";
                return false;
            }

            // 2. Durability Check
            if (utensil.CurrentDurability <= 0)
            {
                errorMessage = "Broken furniture cannot be sold.";
                return false;
            }

            // 3. Buyer Gold Check
            if (buyer.Gold < utensil.Price)
            {
                errorMessage = "Not enough gold.";
                return false;
            }

            // 4. Buyer Inventory / Tent Capacity Check
            if (buyer.Inventory.GetEmptySlotCount() < 1)
            {
                errorMessage = "No inventory space to receive utensil.";
                return false;
            }

            // 5. Currency Transfer & Ownership Migration
            buyer.Gold -= utensil.Price;
            seller.StallGoldVault += utensil.Price;

            seller.UtensilStall.RemoveItemAt(slotIndex);
            buyer.Inventory.AddItem(utensil.ItemEntity);

            // 6. Broadcast Synchronizations
            seller.UtensilStall.BroadcastUpdate();
            return true;
        }
    }
}
```

---

## 7. Verification & Cross-References

- **Disassembly Offsets**: `aLogin.exe` RVA `0x0034ca9c` (`Form_Buy3Utensil_1`), `0x0034bdbc` (Buy Handler), `0x0034bfd8` (Close Handler).
- **String References**: Line 327028 (`"Form_Buy3Utensil_1"`), Line 327044 (`"BeStallUtenSil"`), Line 327088 (`"Btn_Buy_1"`), Line 327533 (`"Form_Buy1Utensil_1"`).
- **Related Specs**:
  - [`26_tent_housing_furniture_placement_and_interior_layout_engine.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/26_tent_housing_furniture_placement_and_interior_layout_engine.md)
  - [`36_player_stall_vending_store_and_street_commerce_mechanics.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/36_player_stall_vending_store_and_street_commerce_mechanics.md)
  - [`42_tent_cupboard_storage_cabinet_chest_inventory_management.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/42_tent_cupboard_storage_cabinet_chest_inventory_management.md)
