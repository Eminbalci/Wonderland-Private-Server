# Document 77: NPC Vendor Liquidation, Sell-Back Ratios, and Buyback Safeguards

## 1. Executive Summary & Engine Architecture

In Wonderland Online, player liquidation of inventory items to NPC merchants occurs through the dual-mode sales interface implemented in `HL_Shop.pas`. Dissected from client subroutines at RVA `0x00386b04` (`FUN_00386b04`), selling queue reset `FUN_00383b64` (RVA `0x00383b64`), store mode switcher `FUN_00383c10` (RVA `0x00383c10`), and sales transaction dispatcher `FUN_00386ec0` (RVA `0x00386ec0`), this engine manages the conversion of player equipment and commodities into gold currency.

The client partitions vendor transactions into two distinct graphical states:
1. `Form_SaleEquip_1` (Mode `0`): Handles weapons, armors, helmets, boots, and accessories with durability checks.
2. `Form_SaleThing_1` (Mode `1`): Handles general commodities, raw materials, alchemy ingredients, and potions.

Both modes share a 10-slot staging queue (`param_1 + 0xf4 + slot * 4`, indices 1..10) enabling batch sales. The liquidation value enforces a strict 50% depreciation ratio ($\lfloor \text{Price} / 2 \rfloor$) modulated by remaining durability. Accidental sales of high-tier items (Grade $> 10$, socketed jewels, or potential enhancements) trigger an interceptor confirmation modal via `FUN_00367770`.

```
+------------------------------------------------------------------------------------+
|               NPC VENDOR LIQUIDATION & BUYBACK SAFEGUARD ARCHITECTURE               |
+------------------------------------------------------------------------------------+
                                       |
                                       v
                     [NPC Shop Window: HL_Shop.pas Container]
                                       |
         +-----------------------------+-----------------------------+
         | Mode 0: Form_SaleEquip_1                                  | Mode 1: Form_SaleThing_1
         v                                                           v
  [Equipment Staging Panel]                                   [Commodity Staging Panel]
  - 10 Batch Sales Slots (1..10)                              - 10 Batch Sales Slots (1..10)
  - Durability Check: +0xFA                                   - Stack Size Splitter
  - Potential Warning Safeguard                               - Zero-Value Warning
         |                                                           |
         +-----------------------------+-----------------------------+
                                       |
                                       v
               [Depreciation Engine: FUN_00386ec0 Calculation]
         SalePrice = Floor(BasePrice * 0.50 * (CurDura / MaxDura))
                                       |
                                       v
                 [Network Dispatcher: Action Code 0x14 (20)]
               Subcode 0x06: Batch Item Liquidation to Vendor
```

---

## 2. Memory Layouts & Data Structures

### 2.1 10-Slot Sales Queue Element Structure (`param_1 + 0xf4 + iVar2 * 4`)

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `void*` | `VTable` | DirectDraw slot control virtual table pointer |
| `+0x18` | `int` | `DisplayPosX` | Absolute screen X coordinate ($54$ px / `0x36`) |
| `+0x1C` | `int` | `DisplayPosY` | Screen Y coordinate ($471$ px for Equip, $461$ px for Things) |
| `+0x4C` | `uint` | `IconHandle` | DirectDraw sprite surface handle (`0xffffffff` when empty) |
| `+0xDC` | `uint` | `ItemId` | 16-bit Item ID from `Item.dat` (0 when slot empty) |
| `+0xFA` | `ushort` | `Durability` | Current durability remaining on equipment item |
| `+0x114`| `uint` | `StackQuantity`| Number of items in stack being liquidated |

### 2.2 Vendor Shop Controller Layout (`HL_Shop.pas`, `param_1`)

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x4C` | `uint` | `ActiveFormSurface`| DirectDraw handle for current mode (`Form_SaleEquip_1` / `Form_SaleThing_1`) |
| `+0xE8` | `byte` | `StoreCategory` | Current vendor category (1: EquipStore, 2: ThingStore, 3: ActivityExchange) |
| `+0xF4..0x11C`| `int*[10]` | `pSaleSlots` | Pointers to the 10 staging slot controls |
| `+0x120..0x12B`| `byte[11]` | `SlotOccupied` | Boolean flags tracking staging slot occupancy |
| `+0x140`| `uint` | `TotalBatchGold` | Running sum of gold value for staged items |
| `+0x146`| `byte` | `SelectedSlot` | Currently focused inventory slot index |
| `+0x1CC`| `byte` | `SaleMode` | Active sales mode (0: Equip, 1: Things) |
| `+0x1CD`| `byte` | `IsQueueDirty` | Staging queue modification flag |
| `+0x1D0`| `int*` | `pBtnSell` | Execute batch liquidation button (`"Btn_Sale_1"`) |

---

## 3. Disassembled Subroutine Analysis

### 3.1 Mode Switching: Equip vs. Things Liquidation (`FUN_00386b04`)
Extracted from `aLogin_decompiled.c` lines 350058–350095:

```c
// Toggle between Equipment and General Items liquidation tabs
void FUN_00386b04(int param_1, char mode_flag) {
    int iVar1;
    undefined4 uVar2;

    if (mode_flag == '\0') { // Mode 0: Equipment Sales
        uVar2 = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "Form_SaleEquip_1");
        *(undefined4 *)(param_1 + 0x4c) = uVar2;
        
        // Re-anchor staging buttons at Y = 0x1d7 (471 px)
        iVar1 = *(int *)(param_1 + 0x1c0);
        *(undefined4 *)(iVar1 + 0x18) = 0x36;  // X = 54 px
        *(undefined4 *)(iVar1 + 0x1c) = 0x1d7; // Y = 471 px
        iVar1 = *(int *)(param_1 + 0x1c4);
        *(undefined4 *)(iVar1 + 0x18) = 0x81;  // X = 129 px
        *(undefined4 *)(iVar1 + 0x1c) = 0x1d7; // Y = 471 px
    } else if (mode_flag == '\x01') { // Mode 1: General Items Sales
        uVar2 = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "Form_SaleThing_1");
        *(undefined4 *)(param_1 + 0x4c) = uVar2;

        // Re-anchor staging buttons at Y = 0x1cd (461 px)
        iVar1 = *(int *)(param_1 + 0x1c0);
        *(undefined4 *)(iVar1 + 0x18) = 0x36;  // X = 54 px
        *(undefined4 *)(iVar1 + 0x1c) = 0x1cd; // Y = 461 px
        iVar1 = *(int *)(param_1 + 0x1c4);
        *(undefined4 *)(iVar1 + 0x18) = 0x81;  // X = 129 px
        *(undefined4 *)(iVar1 + 0x1c) = 0x1cd; // Y = 461 px
    } else {
        return;
    }

    *(char *)(param_1 + 0x1cc) = mode_flag;
    FUN_00386aec(param_1); // Reset staging slot selections
}
```

### 3.2 Staging Queue Reset (`FUN_00383b64`)
Extracted from `aLogin_decompiled.c` lines 349112–349147:

```c
// Clear the 10-item batch liquidation staging queue
void FUN_00383b64(int param_1) {
    int iVar1, iVar2;

    iVar2 = 1;
    do {
        iVar1 = *(int *)(param_1 + 0xf4 + iVar2 * 4);
        *(undefined4 *)(iVar1 + 0xdc) = 0;          // Clear Item ID
        *(undefined4 *)(iVar1 + 0x4c) = 0xffffffff; // Clear Texture handle
        *(undefined4 *)(iVar1 + 0x114) = 0;         // Clear Stack count
        *(undefined2 *)(iVar1 + 0xfa) = 0;          // Clear Durability
        iVar2 = iVar2 + 1;
    } while (iVar2 != 11);

    FUN_00012d10(param_1 + 0x120, 11, 0); // Zero occupancy flags
    *(undefined4 *)(param_1 + 0x140) = 0;  // Reset Total Gold Sum
    *(undefined1 *)(param_1 + 0x146) = 0;  // Reset selection index
}
```

### 3.3 Liquidation Price Depreciation Calculus
The client computes individual item sell prices based on `Item.dat` template fields:

$$\text{LiquidationPrice} = \max\left(1, \left\lfloor \frac{\text{BaseCost}}{2} \times \frac{\text{CurDurability}}{\text{MaxDurability}} \right\rfloor\right) \times \text{Quantity}$$

Items with zero remaining durability sell for a flat nominal floor of 1 Gold Coin.

---

## 4. Network Wire Protocol: Action Code 20 (`0x14`)

Vendor liquidation communicates under **Action Code 20** (`0x14`):

### 4.1 Client Request: Batch Sell Items (`0x14:0x06`)

```
Offset 0x00: [0x14]            (Action Code 20: NPC Interaction & Trade)
Offset 0x01: [0x06]            (Subcode: Batch Sell to Vendor)
Offset 0x02: [NpcId: uint32]   (Target Merchant NPC Unique Identifier)
Offset 0x06: [ItemCount: uint8](Number of distinct staged entries 1..10)
For each staged entry:
  Offset +0x00: [SlotIndex: uint8] (Inventory Slot 1..50)
  Offset +0x01: [Quantity: uint16] (Amount being liquidated)
```

### 4.2 Server Response: Liquidation Confirmation (`0x14:0x06`)

```
Offset 0x00: [0x14]            (Action Code 20)
Offset 0x01: [0x06]            (Subcode: Sell Result ACK)
Offset 0x02: [Status: uint8]   (0x00: Success, 0x01: Item Missing, 0x02: Untradable)
Offset 0x03: [GoldEarned: uint32] (Total gold awarded from batch)
Offset 0x07: [NewBalance: uint32] (Updated total gold in player purse)
```

---

## 5. C# Server Implementation Blueprint

```csharp
namespace Wonderland.Network.Handlers
{
    public static class VendorLiquidationHandler
    {
        private const byte ACTION_CODE_NPC = 0x14;

        public static void Register(PacketDispatcher dispatcher)
        {
            dispatcher.RegisterHandler(ACTION_CODE_NPC, HandleNpcTradeRequest);
        }

        public static void HandleNpcTradeRequest(GameSession session, ReadOnlySpan<byte> packet)
        {
            if (packet.Length < 2) return;
            byte subcode = packet[1];

            if (subcode == 0x06) // Batch Sell
            {
                HandleBatchSell(session, packet[2..]);
            }
        }

        private static void HandleBatchSell(GameSession session, ReadOnlySpan<byte> data)
        {
            if (data.Length < 5) return;
            uint npcId = BinaryPrimitives.ReadUInt32LittleEndian(data[0..4]);
            byte itemCount = data[4];

            var character = session.Character;
            int offset = 5;
            uint totalGoldEarned = 0;

            for (int i = 0; i < itemCount; i++)
            {
                if (offset + 3 > data.Length) break;
                byte slotIndex = data[offset];
                ushort quantity = BinaryPrimitives.ReadUInt16LittleEndian(data[(offset + 1)..(offset + 3)]);
                offset += 3;

                var item = character.Inventory.GetItem(slotIndex);
                if (item == null || item.Quantity < quantity || !item.Template.CanSellToNpc)
                {
                    continue; // Skip invalid or locked items
                }

                // Calculate 50% depreciated price
                uint unitPrice = (uint)Math.Max(1, item.Template.Cost / 2);
                if (item.Template.MaxDurability > 0)
                {
                    unitPrice = (uint)Math.Max(1, (unitPrice * item.Durability) / item.Template.MaxDurability);
                }

                uint saleValue = unitPrice * quantity;
                totalGoldEarned += saleValue;

                character.Inventory.RemoveItem(slotIndex, quantity);
            }

            character.Gold += totalGoldEarned;

            // Send confirmation packet
            byte[] response = new byte[11];
            response[0] = ACTION_CODE_NPC;
            response[1] = 0x06;
            response[2] = 0x00; // Success
            BinaryPrimitives.WriteUInt32LittleEndian(response.AsSpan(3..7), totalGoldEarned);
            BinaryPrimitives.WriteUInt32LittleEndian(response.AsSpan(7..11), character.Gold);
            session.Send(response);
        }
    }
}
```
