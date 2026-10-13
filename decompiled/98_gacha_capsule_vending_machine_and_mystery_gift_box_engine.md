# Document 98: Gacha Capsule Vending Machine, Crank Kinematics, and Prize Drop Chute Engine

## 1. Architectural Overview

*Wonderland Online* incorporates automated amusement vending and promotional gacha machines located in municipal centers (e.g., Welling Village, Kelan Village, Kyoto). Represented visually as classic Japanese toy capsule dispensers, these devices operate through **`Form_Capsulem`** in `se_Capsule.pas`.

The gacha capsule subsystem manages two specialized machine configurations:
1. **General Item Capsule (`"Capsule"`)**: Dispenses consumable buffs, catalytic compounding items, cosmetic apparel, and rare battle equipment.
2. **Housing Furniture Capsule (`"FurnitureCapsule"`)**: Dispenses decorative tent adornments, rare wallpaper rolls, luxury appliances, and companion furniture.

The client simulates the full physical experience: token insertion into an animated coin slot (`icon_rail2`), mechanical 360-degree crank handle rotation (`FUN_00342987`), internal tumbling sphere physics inside the glass globe, capsule chute delivery, and an unboxing reveal sequence accompanied by pyrotechnic particle bursts.

```
+-----------------------------------------------------------------------------------+
|                        GACHA CAPSULE MACHINE ARCHITECTURE                         |
+-----------------------------------------------------------------------------------+
                                          |
                +-------------------------+-------------------------+
                |                                                   |
                v                                                   v
    [Vending UI: Form_Capsulem]                           [Gacha Kinematics Engine]
    - Dimensions: 343x243 px                              - Coin Slot: icon_rail2
    - Machine Type: Capsule / FurnitureCapsule            - 360-degree Crank Rotation
    - Glass Globe Ball Tumbler                            - Chute Drop & Prize Reveal
                |                                                   |
                +----------------- Network Protocol ----------------+
                                          |
                                          v
                      [Action Code 74:11 Capsule Spin Packet]
```

---

## 2. VCL Form Hierarchy & UI Structure

The capsule machine is defined in `se_Capsule.pas` (constructor anchored at RVA `0x00233646`):

### 2.1 Form Specifications

| Attribute | Specification |
| :--- | :--- |
| **Pascal Source Unit** | `se_Capsule.pas` / `cMZ_Capsule.pas` |
| **Form Identifier** | `Form_Capsulem` |
| **Dimensions** | Width: 343 px (`0x157`), Height: 243 px (`0xF3`) |
| **Subroutine Addresses** | Constructor: `FUN_00233646`, Crank Turn: `FUN_00342987`, Prize Open: `FUN_00343010` |
| **Machine Types** | `"Capsule"` (General), `"FurnitureCapsule"` (Tent Housing) |

### 2.2 Control Hierarchy

```
Form_Capsulem (343 x 243 px)
 ├── TCapsuleGlobe (Offset 0x3b, Glass Sphere Viewport, W: 160, H: 140)
 ├── TCrankHandle (Offset 0x3f, Interactive Rotational Knob, Handler: FUN_00342987)
 ├── TCoinSlot (Offset 0x40, Token Ingress Sensor: icon_rail2)
 ├── TDropChute (Offset 0x41, Mechanical Exit Funnel)
 ├── Btn_Turn_1 (X: 240, Y: 180, W: 56, H: 20)
 └── Btn_Close_1 (X: 310, Y: 15, W: 17, H: 17)
```

---

## 3. Disassembly & Algorithmic Analysis

### 3.1 Machine Mode Dispatching (`FUN_00233646`)

```c
// Decompiled from aLogin.exe: Lines 233646 - 233944
(**(code **)(*param_1 + 8))(param_1, "Form_Capsulem", iVar4, 0, 0, 0, 0, 0, 0x157, 0xf3, iVar2);

// Check configuration flag (General Item vs Housing Furniture)
if (isFurnitureMode == 0) {
    FUN_00013f00(param_1 + 0x3f, "Capsule");
} else {
    FUN_00013f00(param_1 + 0x3f, "FurnitureCapsule");
}

// Bind Coin Insertion Track
local_1c = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "icon_rail2");
param_1[0xc4] = local_1c;
```

### 3.2 Mechanical Crank Rotation & Ratchet Sound (`FUN_00342987`)

When the player clicks and drags the crank handle, `FUN_00342987` computes angular displacement $\theta \in [0^\circ, 360^\circ]$:

```c
void OnCrankRotate(int handle, int mouseX, int mouseY)
{
    float angle = atan2(mouseY - CenterY, mouseX - CenterX);
    if (angle > LastAngle + 0.5f) {
        // Trigger mechanical ratchet click
        PlaySound("Sound\\SEB0300.wav");
        LastAngle = angle;
    }
    if (TotalRotation >= 360.0f) {
        // Full turn completed: Lock crank and trigger chute drop
        LockCrank();
        DispatchCapsuleSpinPacket();
    }
}
```

---

## 4. Drop Rate Distribution & Loot Tier Matrix

Capsule machines draw from predefined weighted loot tables configured in the server database:

| Tier | Probability | Prize Classification | Visual Effect on Drop |
| :---: | :---: | :--- | :--- |
| **Common** | $60.0\%$ | Minor HP/SP restorative meals, low-tier alchemical stone | White capsule, standard drop sound |
| **Uncommon** | $25.0\%$ | Medium restorative food, return scrolls, tent mats | Green capsule, pleasant chime |
| **Rare** | $10.0\%$ | High-tier weapons, potential enhancement pills | Blue capsule, golden sparkle particles |
| **Jackpot** | $5.0\%$ | Exclusive vehicle parts, maid outfits, rare companion tickets | Rainbow capsule, full pyrotechnic burst (`HL_NewFireworks`) |

---

## 5. Network Wire Protocol

### 5.1 Client -> Server: Action Code 74:11 Capsule Machine Spin

Sent when the crank completes a full 360-degree rotation.

```
+---------------+---------------+---------------+---------------+---------------+
| Magic Header  | Length (LE)   | Action Code   | Sub-Opcode    | Machine ID    |
| 0xF4 0x44     | 0x06 0x00     | 0x4A          | 0x0B          | 0x04 (Kelan)  |
+---------------+---------------+---------------+---------------+---------------+
| Mode Flag     | Token Item ID |
| 0x01 (Furn)   | 0x42 0x5D     | (Capsule Coin)
+---------------+---------------+
```

### 5.2 Server -> Client: Action Code 74:12 Prize Roll & Delivery Broadcast

```
+---------------+---------------+---------------+---------------+---------------+
| Magic Header  | Length (LE)   | Action Code   | Sub-Opcode    | Result Code   |
| 0xF4 0x44     | 0x0A 0x00     | 0x4A          | 0x0C          | 0x00 (Success)|
+---------------+---------------+---------------+---------------+---------------+
| Loot Tier     | Item ID (LE)  | Quantity (LE) |
| 0x04 (Jackpot)| 0x14 0x5E     | 0x01 0x00     |
+---------------+---------------+---------------+
```

---

## 6. C# Server Implementation Blueprint

```csharp
// CapsuleGachaService.cs - Wonderland Private Server Capsule Vending Engine
using System;
using System.Security.Cryptography;

namespace Wonderland.Server.Minigames
{
    public sealed class CapsuleGachaService
    {
        public const ushort CapsuleCoinItemId = 23874;

        public bool TrySpinCapsule(
            PlayerCharacter player,
            byte machineId,
            bool isFurnitureMode,
            out ushort awardedItemId,
            out byte prizeTier)
        {
            awardedItemId = 0;
            prizeTier = 1;

            // 1. Verify Player Has Capsule Coin / Token
            if (!player.Inventory.HasItem(CapsuleCoinItemId, 1))
            {
                return false;
            }

            // 2. Consume Token
            player.Inventory.RemoveItem(CapsuleCoinItemId, 1);

            // 3. Roll Cryptographic Random [0..9999]
            int roll = RandomNumberGenerator.GetInt32(0, 10000);

            if (roll < 500) // 5%
            {
                prizeTier = 4; // Jackpot
                awardedItemId = isFurnitureMode ? (ushort)28101 : (ushort)24018; // Rare Maid / Luxury Bath
            }
            else if (roll < 1500) // 10%
            {
                prizeTier = 3; // Rare
                awardedItemId = isFurnitureMode ? (ushort)28050 : (ushort)22010;
            }
            else if (roll < 4000) // 25%
            {
                prizeTier = 2; // Uncommon
                awardedItemId = (ushort)12005;
            }
            else // 60%
            {
                prizeTier = 1; // Common
                awardedItemId = (ushort)11002;
            }

            // 4. Deliver Prize Item to Player Inventory
            player.Inventory.AddItem(awardedItemId, 1);

            // 5. Send Result Packet
            player.SendPacket(new byte[]
            {
                0xF4, 0x44, // Magic
                0x0A, 0x00, // Length
                0x4A, 0x0C, // Opcode: Capsule Result
                0x00,       // Success
                prizeTier,
                (byte)(awardedItemId & 0xFF),
                (byte)((awardedItemId >> 8) & 0xFF),
                0x01, 0x00  // Quantity 1
            });

            return true;
        }
    }
}
```

---

## 7. Verification & Cross-References

- **Disassembly Offsets**: `aLogin.exe` RVA `0x00233646` (`Form_Capsulem`), `0x00342987` (Crank Handler).
- **String References**: Line 233646 (`"Form_Capsulem"`), Line 233681 (`"Capsule"`), Line 233943 (`"FurnitureCapsule"`), Line 342940 (`"icon_rail2"`).
- **Related Specs**:
  - [`09_item_mall_microtransactions_and_vip_service_architecture.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/09_item_mall_microtransactions_and_vip_service_architecture.md)
  - [`60_carnival_tabletop_eight_twelve_magnet_and_exp_box_engines.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/60_carnival_tabletop_eight_twelve_magnet_and_exp_box_engines.md)
  - [`85_carnival_lottery_wheel_lotto_ball_physics_and_pyrotechnic_fireworks.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/85_carnival_lottery_wheel_lotto_ball_physics_and_pyrotechnic_fireworks.md)
