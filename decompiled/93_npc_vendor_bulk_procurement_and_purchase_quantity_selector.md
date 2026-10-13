# Document 93: NPC Vendor Bulk Procurement, Stack Limits, and Purchase Quantity Selector

## 1. Architectural Overview

Commerce in *Wonderland Online* requires transactional interfaces for purchasing consumable supplies (potions, arrows, alchemical catalysts, camping materials). When players buy stackable goods from municipal merchants, rather than executing sequential single-unit purchases, the client summons the **Purchase Quantity Selector Dialog** (`form_BuyQuant_Npc` and `form_BuyQ_Npc`).

This subsystem manages:
1. **Dynamic Quantity Input & Incrementing**: An interactive numeric input dialog ($75 \times 200$ px) supporting keyboard text entry, single-unit adjustments, and multi-unit stepping (`arrow_up1`, `arrow_dw1`).
2. **Hard stack Bounds Verification**: Clamping procurement to a strict upper bound of **1,250 units** (`0x4E2` decimal), preventing 16-bit inventory overflow defects.
3. **Multi-Constraint Capacity Validation**: Calculating purchasing ceilings based on current player gold purse, remaining bag slot availability, and total character carrying capacity (weight limits).
4. **Network Serialization**: Transmitting confirmed bulk purchase orders via Action Code 20:1 (`0x14:0x01`).

```
+-----------------------------------------------------------------------------------+
|                        NPC BULK PROCUREMENT SUBSYSTEM                             |
+-----------------------------------------------------------------------------------+
                                          |
                +-------------------------+-------------------------+
                |                                                   |
                v                                                   v
    [Quantity Modal: form_BuyQuant_Npc]                   [Validation & Capacity Engine]
    - Dimensions: 75x200 px                               - Ceiling: 1,250 Units (0x4E2)
    - Input Textbox + Increment Arrows                    - Total Price = Qty * UnitPrice
    - Submit Button: btn_Buy                              - Gold & Weight Thresholds
                |                                                   |
                +----------------- Network Dispatch ----------------+
                                          |
                                          v
                         [Action Code 20:1 Buy Order Packet]
```

---

## 2. VCL Form Hierarchy & UI Structure

The selector is constructed in `se_Shop.pas` / `HL_Shop.pas` (constructor anchored at RVA `0x002ce3f8`):

### 2.1 Form Specifications

| Attribute | Specification |
| :--- | :--- |
| **Pascal Source Unit** | `se_Shop.pas` / `HL_Shop.pas` |
| **Form Identifier** | `form_BuyQuant_Npc` (compact) / `form_BuyQ_Npc` (extended) |
| **Dimensions** | Width: 75 px (`0x4B`), Height: 200 px |
| **Subroutine Addresses** | Constructor: `FUN_002ce3f8`, Decrement: `FUN_002ce43c`, Increment: `LAB_002ce4d4` |
| **Max Quantity Ceiling** | 1,250 (`0x4E2` units) |

### 2.2 Control Hierarchy

```
form_BuyQuant_Npc (75 x 200 px)
 ├── TNumEditor (Offset 0x3a, Rect: [X: 20, Y: 70, W: 50, H: 20], MaxLen: 4 digits)
 ├── arrow_up1 (X: 53, Y: 27, W: 10, H: 9, Handler: LAB_002ce4d4)
 ├── arrow_dw1 (X: 53, Y: 40, W: 10, H: 9, Handler: FUN_002ce43c)
 └── btn_Buy (X: 25, Y: 149, W: 34, H: 25, Handler: LAB_002ce3c8)
```

---

## 3. Disassembly & Algorithmic Analysis

### 3.1 Decrement Stepping Handler (`FUN_002ce43c`)

```c
// Decompiled from aLogin.exe: FUN_002ce43c @ 0x002ce43c
void FUN_002ce43c(int param_1)
{
    short sVar2 = FUN_002ce3f8(param_1); // Query current valid quantity
    if (sVar2 == 0) {
        // Reset to minimum baseline (1 unit)
        FUN_00482a3c(*(undefined4 *)(param_1 + 0xe8), &DAT_002ce4d0); // "1"
    }
    else {
        // Read integer value from editor
        int currentQty = FUN_000195a0(*(undefined4 *)(*(int *)(param_1 + 0xe8) + 0x1a0));
        currentQty = currentQty - 1;
        if (currentQty < 1) {
            currentQty = 1; // Clamp lower bound to 1
        }
        // Write back updated string
        FUN_0001953c(currentQty, &local_8);
        FUN_00482a3c(*(undefined4 *)(param_1 + 0xe8), local_8);
    }
}
```

### 3.2 Max Stack Boundary Clamp (`FUN_002ce3f8`)

```c
// Decompiled from aLogin.exe: FUN_002ce3f8 @ 0x002ce3f8
int FUN_002ce3f8(int param_1, undefined4 param_2, int param_3)
{
    int validQty = 0;
    int local_c = param_3;
    FUN_00012da8(*(undefined4 *)(*(int *)(param_1 + 0xe8) + 0x1a0), &local_c);
    if (local_c == 0) {
        int parsed = FUN_000195a0(*(undefined4 *)(*(int *)(param_1 + 0xe8) + 0x1a0));
        // Hard constraint: parsed must be within 1 .. 1250 (0x4E2)
        if ((0 < parsed) && (parsed < 0x4e3)) {
            validQty = parsed;
        }
    }
    return validQty;
}
```

---

## 4. Multi-Constraint Procurement Evaluation

Prior to dispatching the purchase packet to the server, the client runs three verification gates:

$$\text{MaxAffordableQty} = \left\lfloor \frac{\text{Player Gold}}{\text{Unit Price}} \right\rfloor$$
$$\text{MaxWeightQty} = \left\lfloor \frac{\text{Max Carry Weight} - \text{Current Weight}}{\text{Unit Weight}} \right\rfloor$$
$$\text{AvailableSlots} = \text{Free Inventory Slots} \times \text{Item Stack Limit}$$
$$\text{FinalPurchasableLimit} = \min(1250, \text{MaxAffordableQty}, \text{MaxWeightQty}, \text{AvailableSlots})$$

If the user attempts to purchase beyond any threshold, the client raises immediate modal warnings:
- `"Not enough gold!"` (`Sound\SEB0300.wav`)
- `"Exceeds carrying weight!"`
- `"Inventory full!"`

---

## 5. Network Wire Protocol

### 5.1 Client -> Server: Action Code 20:1 Bulk NPC Buy Order

Dispatched when `btn_Buy` is pressed.

```
+---------------+---------------+---------------+---------------+---------------+
| Magic Header  | Length (LE)   | Action Code   | Sub-Opcode    | Vendor NPC ID |
| 0xF4 0x44     | 0x09 0x00     | 0x14          | 0x01          | 0x82 0x01     |
+---------------+---------------+---------------+---------------+---------------+
| Item ID (LE)  | Quantity (LE) | Expected Price|
| 0x3F 0x04     | 0xE2 0x04     | 0x00 0x50     | (1,250 units of item 1087)
+---------------+---------------+---------------+
```

### 5.2 Server -> Client: Action Code 20:2 Bulk Purchase Confirmation

```
+---------------+---------------+---------------+---------------+---------------+
| Magic Header  | Length (LE)   | Action Code   | Sub-Opcode    | Result Code   |
| 0xF4 0x44     | 0x08 0x00     | 0x14          | 0x02          | 0x00 (Success)|
+---------------+---------------+---------------+---------------+---------------+
| New Gold Total| RemainingSlots|
| 0x80 0x96 0x98| 0x12          | (Gold balance, free slots)
+---------------+---------------+
```

---

## 6. C# Server Implementation Blueprint

```csharp
// NpcVendorService.cs - Wonderland Private Server Bulk Procurement Engine
using System;

namespace Wonderland.Server.Economy
{
    public sealed class NpcVendorService
    {
        public const ushort MaxBulkQuantity = 1250;

        public bool ProcessBulkPurchase(
            PlayerCharacter player,
            ushort vendorNpcId,
            ushort itemId,
            ushort quantity,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            // 1. Quantity Range Check
            if (quantity == 0 || quantity > MaxBulkQuantity)
            {
                errorMessage = "Invalid purchase quantity.";
                return false;
            }

            // 2. Query Item and Vendor Inventory
            var itemTemplate = ItemDatabase.Get(itemId);
            if (itemTemplate == null)
            {
                errorMessage = "Item does not exist.";
                return false;
            }

            // 3. Compute Total Cost
            ulong totalCost = (ulong)itemTemplate.BuyPrice * quantity;
            if (player.Gold < totalCost)
            {
                errorMessage = "Not enough gold.";
                return false;
            }

            // 4. Weight Capacity Verification
            uint totalWeight = (uint)(itemTemplate.Weight * quantity);
            if (player.CurrentWeight + totalWeight > player.MaxCarryWeight)
            {
                errorMessage = "Carrying weight limit exceeded.";
                return false;
            }

            // 5. Inventory Slots Verification
            int requiredSlots = (int)Math.Ceiling((double)quantity / itemTemplate.StackLimit);
            if (player.Inventory.GetEmptySlotCount() < requiredSlots)
            {
                errorMessage = "Inventory full.";
                return false;
            }

            // 6. Execute Transaction
            player.Gold -= totalCost;
            player.Inventory.AddStack(itemId, quantity);
            player.CurrentWeight += totalWeight;

            // 7. Send Sync Packet
            player.SendPacket(BuildPurchaseSuccessPacket(player.Gold, (byte)player.Inventory.GetEmptySlotCount()));
            return true;
        }

        private byte[] BuildPurchaseSuccessPacket(ulong gold, byte freeSlots)
        {
            return new byte[]
            {
                0xF4, 0x44, // Magic
                0x08, 0x00, // Length
                0x14, 0x02, // Opcode: Bulk Buy Success
                0x00,       // Result Code
                (byte)(gold & 0xFF),
                (byte)((gold >> 8) & 0xFF),
                (byte)((gold >> 16) & 0xFF),
                freeSlots
            };
        }
    }
}
```

---

## 7. Verification & Cross-References

- **Disassembly Offsets**: `aLogin.exe` RVA `0x002ce3f8` (`form_BuyQuant_Npc`), `0x002ce43c` (Decrement), `0x002ce4d4` (Increment).
- **String References**: Line 293045 (`"form_BuyQuant_Npc"`), Line 293061 (`"arrow_up1"`), Line 293066 (`"arrow_dw1"`), Line 293073 (`"btn_Buy"`).
- **Hard Bounds**: `0x4E2` = 1,250 units max stack check.
- **Related Specs**:
  - [`10_trade_inventory_and_stall_vendor_networking.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/10_trade_inventory_and_stall_vendor_networking.md)
  - [`77_npc_vendor_liquidation_sell_back_ratios_and_buyback_safeguards.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/77_npc_vendor_liquidation_sell_back_ratios_and_buyback_safeguards.md)
