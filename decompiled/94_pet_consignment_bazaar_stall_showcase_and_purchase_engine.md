# Document 94: Pet Consignment Bazaar, Multi-Tier Stall Showcase, and Purchase Engine

## 1. Architectural Overview

In *Wonderland Online*, player street commerce extends beyond item wares to include companion pets and mounts. Through the **Pet Stall Consignment Bazaar**, players deploy public street stalls where other adventurers can inspect, compare, and purchase leveled or reborn humanoid and beast companions.

The pet vending subsystem is structured across three responsive display tiers:
1. **Tier-1 Pet Showcase (`Form_BuyPet1Stall_1`)**: Single-row showcase ($441 \times 243$ px) displaying up to 5 companion pets.
2. **Tier-2 Pet Showcase (`Form_BuyPet2Stall_1`)**: Double-row showcase ($441 \times 442$ px) accommodating up to 10 companion pets.
3. **Tier-3 Pet Showcase (`Form_BuyPet3Stall_1`)**: Expanded triple-row bazaar showcase ($441 \times 644$ px) accommodating up to 15 companion pets.

The subsystem manages pet ownership validation (Amity $\ge 20$, equipment unbinding), stat preview telemetry (Level, Element, Rebirth stage, current HP/SP, core attributes), gold escrow transfers, and roster capacity checks.

```
+-----------------------------------------------------------------------------------+
|                        PET CONSIGNMENT BAZAAR ARCHITECTURE                        |
+-----------------------------------------------------------------------------------+
                                          |
                +-------------------------+-------------------------+
                |                                                   |
                v                                                   v
    [Stall UI: Form_BuyPet1..3Stall_1]                    [Consignment Validation Engine]
    - Dynamic Multi-Tier Viewports                        - Amity Threshold Gate (>= 20)
    - 5 to 15 Pet Display Slots                           - Reborn Tier & Level Inspection
    - Stat Tooltips & Roster Preview                      - Max 4 Active Roster Slots
                |                                                   |
                +----------------- Network Wire Link ---------------+
                                          |
                                          v
                      [Action Code 20:7 Pet Purchase Order]
```

---

## 2. VCL Form Hierarchy & UI Structure

The pet stall showcase is implemented in `HL_Shop.pas` and `CY_TradeMenu.pas` (constructor anchored at RVA `0x00347e78`):

### 2.1 Form Specifications

| Attribute | Specification |
| :--- | :--- |
| **Pascal Source Unit** | `HL_Shop.pas` / `CY_TradeMenu.pas` |
| **Form Identifiers** | `Form_BuyPet1Stall_1` (Tier 1), `Form_BuyPet2Stall_1` (Tier 2), `Form_BuyPet3Stall_1` (Tier 3) |
| **Tier 1 Dimensions** | Width: 441 px (`0x1B9`), Height: 243 px (`0xF3`) |
| **Tier 2 Dimensions** | Width: 441 px (`0x1B9`), Height: 442 px (`0x1BA`) |
| **Tier 3 Dimensions** | Width: 441 px (`0x1B9`), Height: 644 px (`0x284`) |
| **Subroutine Addresses** | Constructor: `FUN_00347e78`, Inspector: `FUN_00348120` |

### 2.2 Control Hierarchy (Tier-1 Stall)

```
Form_BuyPet1Stall_1 (441 x 243 px)
 ├── PetSlot[0]: [Icon, Name, LV, Element, Price, Btn_Inspect, Btn_Buy] (X: 34, Y: 34, W: 308, H: 35)
 ├── PetSlot[1]: [Icon, Name, LV, Element, Price, Btn_Inspect, Btn_Buy] (X: 34, Y: 69, W: 308, H: 35)
 ├── PetSlot[2]: [Icon, Name, LV, Element, Price, Btn_Inspect, Btn_Buy] (X: 34, Y: 104, W: 308, H: 35)
 ├── PetSlot[3]: [Icon, Name, LV, Element, Price, Btn_Inspect, Btn_Buy] (X: 34, Y: 139, W: 308, H: 35)
 ├── PetSlot[4]: [Icon, Name, LV, Element, Price, Btn_Inspect, Btn_Buy] (X: 34, Y: 174, W: 308, H: 35)
 └── Btn_Close_1 (X: 395, Y: 17, W: 17, H: 17)
```

---

## 3. Disassembly & Algorithmic Analysis

### 3.1 Tier Selection & Form Instantiation (`FUN_00347e78`)

```c
// Decompiled from aLogin.exe: Lines 325490 - 325585
puVar6 = (undefined1 *)((iVar9 + -5) / 0xe1); // Calculate tier index
param_1[0xaa] = (int)puVar6;

if (puVar6 < (undefined1 *)0x2) {
    // 1 to 5 Pets: Instantiate Tier 1 (441 x 243 px)
    (**(code **)(*param_1 + 8))(param_1, "Form_BuyPet1Stall_1", 0x19);
    for (iVar9 = 1; iVar9 < 6; iVar9++) {
        // Layout 5 vertical pet slot rows (Stride 35 px / 0x23)
        (**(code **)(*(int *)param_1[iVar9 + 0x3b] + 8))
            ((int *)param_1[iVar9 + 0x3b], 0, (iVar9 - 1) * 0x23 + 0x22);
    }
}
else if (puVar6 == (undefined1 *)0x2) {
    // 6 to 10 Pets: Instantiate Tier 2 (441 x 442 px)
    (**(code **)(*param_1 + 8))(param_1, "Form_BuyPet2Stall_1", 0x19);
    for (iVar9 = 1; iVar9 < 6; iVar9++) {
        (**(code **)(*(int *)param_1[iVar9 + 0x3b] + 8))
            ((int *)param_1[iVar9 + 0x3b], 0, (iVar9 - 1) * 0x23 + 0x20);
    }
}
else if (puVar6 == (undefined1 *)0x3) {
    // 11 to 15 Pets: Instantiate Tier 3 (441 x 644 px)
    (**(code **)(*param_1 + 8))(param_1, "Form_BuyPet3Stall_1", 0x19);
}
```

---

## 4. Consignment Rules & Transaction Safety

To safeguard game balance and prevent companion abandonment, the stall engine enforces four strict transactional rules:

1. **Amity Floor Constraint**:
   Pets with Amity below **20** cannot be registered into a stall. This prevents players from discarding disobedient or low-loyalty pets.
2. **Equipment Stripping / Binding**:
   All 6 equipment slots on the companion (Weapon, Head, Body, Hands, Feet, Special) must either be stripped into the player's inventory or sold bundled with the pet.
3. **Active Companion Roster Cap (Max 4 Pets)**:
   The buyer must have at least one empty companion slot in their active party roster (`MaxActivePets = 4`). If all 4 slots are occupied, the purchase is aborted with `"Companion slots full!"`.
4. **Gold Purse Escrow**:
   The buyer must possess sufficient gold in their inventory. On transaction completion, gold is debited immediately and placed in the stall owner's revenue vault, while the pet's binary data block (attributes, learned skills, current amity) is assigned to the buyer's actor ID.

---

## 5. Network Wire Protocol

### 5.1 Client -> Server: Action Code 20:5 Open / Inspect Pet Stall

```
+---------------+---------------+---------------+---------------+---------------+
| Magic Header  | Length (LE)   | Action Code   | Sub-Opcode    | Stall Owner ID|
| 0xF4 0x44     | 0x06 0x00     | 0x14          | 0x05          | 0x3A 0x10 0x00|
+---------------+---------------+---------------+---------------+---------------+
```

### 5.2 Server -> Client: Action Code 20:6 Pet Stall Data Stream

```
+---------------+---------------+---------------+---------------+---------------+
| Magic Header  | Length (LE)   | Action Code   | Sub-Opcode    | Pet Count     |
| 0xF4 0x44     | Variable      | 0x14          | 0x06          | 0x03          |
+---------------+---------------+---------------+---------------+---------------+
| [Pet Slot Record 1: 48 bytes (NPC ID, Level, Reborn, Amity, Stats, Price)]... |
+-------------------------------------------------------------------------------+
```

### 5.3 Client -> Server: Action Code 20:7 Execute Pet Purchase

```
+---------------+---------------+---------------+---------------+---------------+
| Magic Header  | Length (LE)   | Action Code   | Sub-Opcode    | Stall Owner ID|
| 0xF4 0x44     | 0x0B 0x00     | 0x14          | 0x07          | 0x3A 0x10 0x00|
+---------------+---------------+---------------+---------------+---------------+
| Stall Slot    | Agreed Price (uint32 LE)      |
| 0x02          | 0x00 0x50 0x3E 0x00 (4.08M G) |
+---------------+-------------------------------+
```

---

## 6. C# Server Implementation Blueprint

```csharp
// PetStallService.cs - Wonderland Private Server Companion Vending Engine
using System;
using System.Collections.Concurrent;

namespace Wonderland.Server.Economy
{
    public sealed class PetStallService
    {
        public bool BuyConsignedPet(
            PlayerCharacter buyer,
            PlayerCharacter seller,
            byte stallSlot,
            out string failureReason)
        {
            failureReason = string.Empty;

            // 1. Verify Seller Stall State
            if (!seller.IsStallOpen || seller.PetStall == null)
            {
                failureReason = "Stall is no longer active.";
                return false;
            }

            var petEntry = seller.PetStall.GetPetAt(stallSlot);
            if (petEntry == null)
            {
                failureReason = "Pet is no longer available.";
                return false;
            }

            // 2. Verify Buyer Roster Capacity (Max 4 companions)
            if (buyer.CompanionRoster.Count >= 4)
            {
                failureReason = "Your companion roster is full (max 4).";
                return false;
            }

            // 3. Verify Buyer Funds
            if (buyer.Gold < petEntry.Price)
            {
                failureReason = "Insufficient gold to purchase companion.";
                return false;
            }

            // 4. Atomic Currency Transfer
            buyer.Gold -= petEntry.Price;
            seller.StallGoldVault += petEntry.Price;

            // 5. Transfer Pet Ownership
            var pet = petEntry.PetEntity;
            seller.PetStall.RemovePetAt(stallSlot);
            pet.OwnerCharacterId = buyer.CharacterId;
            buyer.CompanionRoster.Add(pet);

            // 6. Broadcast Stall Update to Viewers
            seller.PetStall.BroadcastUpdate();
            return true;
        }
    }
}
```

---

## 7. Verification & Cross-References

- **Disassembly Offsets**: `aLogin.exe` RVA `0x00347e78` (`Form_BuyPet1Stall_1`), `0x00347f15` (`Form_BuyPet2Stall_1`), `0x00347fb2` (`Form_BuyPet3Stall_1`).
- **String References**: Line 325503 (`"Form_BuyPet1Stall_1"`), Line 325540 (`"Form_BuyPet2Stall_1"`), Line 325577 (`"Form_BuyPet3Stall_1"`).
- **Related Specs**:
  - [`06_companion_pet_system_lifecycle_and_ai_combat_subroutines.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/06_companion_pet_system_lifecycle_and_ai_combat_subroutines.md)
  - [`36_player_stall_vending_store_and_street_commerce_mechanics.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/36_player_stall_vending_store_and_street_commerce_mechanics.md)
  - [`81_companion_pet_rebirth_ascension_and_npc_upgrade_system.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/81_companion_pet_rebirth_ascension_and_npc_upgrade_system.md)
