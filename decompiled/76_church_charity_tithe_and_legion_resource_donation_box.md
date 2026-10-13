# Document 76: Church Charity Tithe, Donation Box, and Legion Resource Storage System

## 1. Executive Summary & Engine Architecture

In Wonderland Online, both municipal church charity (earning character karma and luck buffs) and legion / army group resource tithes (funding guild fortress upgrades, research, and siege machinery) operate through the shared donation framework implemented in `form_Donate` ($422 \times 325$ px, RVA `0x0025072a`). Dissected from client subroutines in `se_Donate.pas` at RVA `0x0025072a` (constructor), inventory synchronizer `FUN_002510e8` (RVA `0x002510e8`), and gold adjuster handlers `LAB_00250d80` and `LAB_00250e78`, this engine governs material tithing and treasury management.

The interface presents a dual-layout panel: on the left, a 50-slot grid ($5 \times 10$ inventory matrix) dynamically filtered to accept only Type '5' materials (raw construction timber, mineral ores, stone slabs, and donation tokens); on the right, 7 editable numeric fields (`editorBG`, $50 \times 20$ px) with gold counter increment arrows (`btn_Bank_Gold_L` and `btn_Bank_Gold_R`) corresponding to 7 organizational resource categories (`0x9060`..`0x9067`). For legion treasuries, a privileged withdrawal button (`icon_withDraw`) is conditionally unlocked for guild officers possessing the treasury clearance bitmask.

```
+------------------------------------------------------------------------------------+
|                    DONATION BOX & LEGION TITHE ENGINE ARCHITECTURE                 |
+------------------------------------------------------------------------------------+
                                       |
                                       v
                        [form_Donate Dialog (422x325 px)]
                                       |
         +-----------------------------+-----------------------------+
         | 50-Slot Material Grid (Left)                              | 7-Category Resource Vault (Right)
         v                                                           v
  [Inventory Scanner: FUN_002510e8]                          [7 Numeric Inputs: editorBG]
  - 5x10 Slot Matrix (36 px stride)                           - 30 px Vertical Stride (0x1e)
  - Type Filter: Item.dat local_1b9 == '5'                    - Gold Adjustment: btn_Bank_Gold_L/R
  - Material Icons: icon_weaponry, icon_ItemGrids             - Resource Categories: 0x9060..0x9067
  - Quantity Tooltip: FUN_00480ae4                            - Officer Gate: icon_withDraw
         |                                                           |
         +-----------------------------+-----------------------------+
                                       |
                                       v
                 [Network Dispatcher: Action Code 0x1C (28)]
             Subcode 0x05: Legion Resource Tithe / Church Charity
```

---

## 2. Memory Layouts & Data Structures

### 2.1 Donation Resource Categories (`0x9060`..`0x9067`)
The 7 input fields on the right panel represent organizational and municipal resource attributes:

| Category Index | Resource Asset ID | Attribute Name | Description |
|---|---|---|---|
| `0` | `0x9060` | `Gold Coins` | Liquid currency for guild upkeep or church tithe |
| `1` | `0x9061` | `Timber Logs` | Processed wood for tent expansions and siege ladders |
| `2` | `0x9062` | `Stone Blocks` | Granite and marble for fortress wall durability |
| `3` | `0x9063` | `Iron Ore` | Metallic minerals for manufacturing equipment |
| `4` | `0x9064` | `Food Rations` | Culinary provisions for guild war stamina reserves |
| `5` | `0x9066` | `Energy Crystals` | Magic stones for guild technology research |
| `6` | `0x9067` | `Contribution Karma` | Personal charity points / legion merit score |

### 2.2 `form_Donate` Instance Layout (`param_1`)

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `void*` | `VTable` | Delphi component virtual method table pointer |
| `+0x04` | `uint` | `FormHandle` | DirectDraw surface bitmap handle |
| `+0x3A..0x6A`| `int*[50]` | `pItemSlots` | Array of 50 interactive grid slot controls |
| `+0x6C` | `uint` | `ResIconFood` | Food provisions icon handle (`0x9064`) |
| `+0x6D` | `uint` | `ResIconMerit` | Contribution merit icon handle (`0x9067`) |
| `+0x6E` | `uint` | `ResIconCrystals`| Energy crystal icon handle (`0x9066`) |
| `+0x6F` | `uint` | `ResIconTimber` | Timber log icon handle (`0x9061`) |
| `+0x70` | `uint` | `ResIconGold` | Gold coin icon handle (`0x9060`) |
| `+0x71` | `uint` | `ResIconIron` | Iron ore icon handle (`0x9063`) |
| `+0x72` | `uint` | `ResIconStone` | Stone block icon handle (`0x9062`) |
| `+0x73` | `uint` | `Panel16` | Background panel container handle |
| `+0x74` | `uint` | `IconWithdraw` | Guild withdrawal button handle (`"icon_withDraw"`) |
| `+0x7B` | `uint` | `IconWeaponry` | Weaponry asset sprite handle |
| `+0x7E` | `int*` | `pBtnClose` | Close button (`"Btn_Close_1"`, $70 \times 25$ px) |
| `+0x7F` | `int*` | `pBtnCloseS` | Top-right close button (`"Btn_Close_s_1"`, $17 \times 17$ px) |
| `+0x80` | `uint` | `IconItemGrids`| Item grid background border handle |
| `+0x81..0x87`| `int*[7]` | `pEditBoxes` | 7 numeric text entry boxes (`"editorBG"`) |
| `+0x88` | `int*` | `pBtnGoldL` | Left gold decrement button (`"btn_Bank_Gold_L"`) |
| `+0x89` | `int*` | `pBtnGoldR` | Right gold increment button (`"btn_Bank_Gold_R"`) |

---

## 3. Disassembled Subroutine Analysis

### 3.1 Form Construction & 50-Slot Matrix Initialization (`FUN_0025072a`)
Extracted from `aLogin_decompiled.c` lines 246352–246465:

```c
// Constructor for form_Donate (422 x 325 px)
int * FUN_0025072a(int *param_1, char param_2) {
    int iVar1, iVar3;
    int *piVar2;

    FUN_0048eb38(param_1, 0);
    // Instantiate surface: width 0x1a6 = 422 px, height 0x145 = 325 px
    (**(code **)(*param_1 + 8))(param_1, "form_Donate", 0x17a, 0, 0, 0, 0);

    param_1[0x7b] = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "icon_weaponry");
    param_1[0x80] = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "icon_ItemGrids");

    // Close buttons
    piVar2 = (int *)FUN_00485788(PTR_PTR_0047f5f0, 1, param_1);
    param_1[0x7f] = (int)piVar2;
    (**(code **)(*piVar2 + 8))(piVar2, "Btn_Close_s_1", param_1[8] - 0x17, 17, 17, 0, 0, 1, 17, 17, 5);

    // 50 Inventory Grid Slots (5 cols x 10 rows, stride 0x24 = 36 px)
    iVar3 = 1;
    do {
        piVar2 = (int *)FUN_003ef37c(PTR_PTR_0024f28c, 1, param_1);
        param_1[iVar3 + 0x39] = (int)piVar2;
        int col_x = ((iVar3 - 1) % 5) * 36 + 20; // 0x24 = 36, offset 0x14 = 20
        int row_y = ((iVar3 - 1) / 5) * 36 + 70; // 0x24 = 36, offset 0x46 = 70
        (**(code **)(*piVar2 + 8))(piVar2, 0, col_x, 32, 32, 0, 0, 1, 36, 36, row_y);
        
        iVar1 = param_1[iVar3 + 0x39];
        *(int *)(iVar1 + 0xb0) = iVar3; // Slot index
        *(int **)(iVar1 + 0x74) = param_1;
        *(undefined1 **)(iVar1 + 0x70) = &LAB_00250d5c; // Slot click callback
        iVar3 = iVar3 + 1;
    } while (iVar3 != 51);

    // 7 Numeric Edit Boxes for Resources (editorBG, width 50, height 20, vertical stride 30 px)
    iVar3 = 0;
    do {
        piVar2 = (int *)FUN_004828a0(PTR_PTR_0047f6d4, 1, param_1);
        param_1[iVar3 + 0x81] = (int)piVar2;
        (**(code **)(*piVar2 + 8))(piVar2, "editorBG", 0x140, 20, 50, 0, 0, 1, 21, 74, iVar3 * 30 + 56);
        FUN_004829dc(piVar2, 2);  // Integer numeric mode
        FUN_00482ae0(piVar2, 10); // Max 10 digits
        iVar3 = iVar3 + 1;
    } while (iVar3 != 7);

    // Increment / Decrement Gold Buttons
    piVar2 = (int *)FUN_00485788(PTR_PTR_0047f5f0, 1, param_1);
    param_1[0x88] = (int)piVar2;
    (**(code **)(*piVar2 + 8))(piVar2, "btn_Bank_Gold_L", 200, 22, 27, 0, 0, 1, 22, 27, 108);
    piVar2[0x14] = (int)&LAB_00250d80;

    piVar2 = (int *)FUN_00485788(PTR_PTR_0047f5f0, 1, param_1);
    param_1[0x89] = (int)piVar2;
    (**(code **)(*piVar2 + 8))(piVar2, "btn_Bank_Gold_R", 200, 22, 27, 0, 0, 1, 22, 27, 182);
    piVar2[0x14] = (int)&LAB_00250e78;

    return param_1;
}
```

### 3.2 Inventory Type '5' Material Gate (`FUN_002510e8`)
Extracted from `aLogin_decompiled.c` lines 246520–246556:

```c
// Scan player inventory and populate eligible donation slots
void FUN_002510e8(int param_1) {
    int iVar3 = 1;
    char item_type;
    undefined1 item_record[15];

    do {
        // Retrieve item template from Item.dat cache
        ushort item_id = *(ushort *)(*(int *)(*(int *)PTR_DAT_004c9918 + 0x27e8 + iVar3 * 4) + 4);
        FUN_003e8db4(*(undefined4 *)PTR_DAT_004c94f8, item_id, item_record);
        item_type = item_record[14]; // Byte 14: Item Category Code

        int slot_ctrl = *(int *)(param_1 + 0xe4 + iVar3 * 4);
        if (item_type == '5') { // '5' = Raw Materials & Donation Items
            // Populate slot with item icon, count, and durability
            *(undefined4 *)(slot_ctrl + 0x4c) = *(undefined4 *)(*(int *)(*(int *)PTR_DAT_004c9918 + 0x27e8 + iVar3 * 4) + 0x14);
            *(uint *)(slot_ctrl + 0x114) = (uint)*(byte *)(*(int *)(*(int *)PTR_DAT_004c9918 + 0x27e8 + iVar3 * 4) + 0x11);
            *(uint *)(slot_ctrl + 0xdc) = (uint)item_id;
            FUN_00480ae4(slot_ctrl, local_1cc); // Set item name tooltip
        } else {
            // Disallow non-material items (weapons, armor, consumable potions)
            *(undefined4 *)(slot_ctrl + 0xdc) = 0;
            *(undefined4 *)(slot_ctrl + 0x4c) = 0xffffffff;
            *(undefined4 *)(slot_ctrl + 0x114) = 0;
            FUN_00480ae4(slot_ctrl, 0);
        }
        iVar3 = iVar3 + 1;
    } while (iVar3 != 51);
}
```

---

## 4. Network Wire Protocol: Action Code 28 (`0x1C`)

Legion resource deposits and municipal church charity communicate under **Action Code 28** (`0x1C`):

### 4.1 Client Request: Deposit Resource Tithe (`0x1C:0x05`)

```
Offset 0x00: [0x1C]            (Action Code 28: Organization Operations)
Offset 0x01: [0x05]            (Subcode: Resource Tithe Deposit)
Offset 0x02: [Category: uint8] (0: Gold, 1: Timber, 2: Stone, 3: Iron, 4: Food, 5: Crystals)
Offset 0x03: [Amount: uint32]  (Quantity of currency or materials contributed)
Offset 0x07: [SourceSlot: uint8] (Inventory Slot 1..50 if depositing physical items, 0 for gold)
```

### 4.2 Server Response: Treasury & Karma Sync (`0x1C:0x05`)

```
Offset 0x00: [0x1C]            (Action Code 28)
Offset 0x01: [0x05]            (Subcode: Deposit ACK)
Offset 0x02: [Status: uint8]   (0x00: Success, 0x01: Inventory Mismatch, 0x02: Vault Full)
Offset 0x03: [Category: uint8] (Resource Index 0..5)
Offset 0x04: [NewTotal: uint32] (Updated guild treasury balance for category)
Offset 0x08: [KarmaEarned: uint16] (Contribution merit or church karma awarded)
```

---

## 5. C# Server Implementation Blueprint

```csharp
namespace Wonderland.Network.Handlers
{
    public static class DonationBoxHandler
    {
        private const byte ACTION_CODE_GUILD = 0x1C;

        public static void Register(PacketDispatcher dispatcher)
        {
            dispatcher.RegisterHandler(ACTION_CODE_GUILD, HandleDonationRequest);
        }

        public static void HandleDonationRequest(GameSession session, ReadOnlySpan<byte> packet)
        {
            if (packet.Length < 2) return;
            byte subcode = packet[1];

            if (subcode == 0x05) // Tithe Deposit
            {
                HandleTitheDeposit(session, packet[2..]);
            }
        }

        private static void HandleTitheDeposit(GameSession session, ReadOnlySpan<byte> data)
        {
            if (data.Length < 6) return;
            byte category = data[0];
            uint amount = BinaryPrimitives.ReadUInt32LittleEndian(data[1..5]);
            byte slotIndex = data[5];

            var character = session.Character;
            var guild = character.Guild;

            if (category == 0) // Gold Tithe
            {
                if (character.Gold < amount)
                {
                    SendTitheResponse(session, 0x01, category, 0, 0);
                    return;
                }

                character.Gold -= amount;
                if (guild != null)
                {
                    guild.TreasuryGold += amount;
                    guild.AddMerit(character, (ushort)(amount / 1000));
                }
                else
                {
                    // Municipal church donation -> award karma
                    character.Karma += (ushort)(amount / 500);
                }

                SendTitheResponse(session, 0x00, category, (uint)(guild?.TreasuryGold ?? 0), (ushort)(amount / 1000));
            }
            else // Material Item Tithe (Timber, Stone, Iron, Food, Crystals)
            {
                var item = character.Inventory.GetItem(slotIndex);
                if (item == null || item.Quantity < amount || item.Template.Category != 5)
                {
                    SendTitheResponse(session, 0x01, category, 0, 0);
                    return;
                }

                character.Inventory.RemoveItem(slotIndex, (int)amount);
                if (guild != null)
                {
                    guild.AddResource(category, amount);
                    ushort meritGain = (ushort)(amount * item.Template.Grade);
                    guild.AddMerit(character, meritGain);
                    SendTitheResponse(session, 0x00, category, guild.GetResource(category), meritGain);
                }
            }
        }

        private static void SendTitheResponse(GameSession session, byte status, byte category, uint total, ushort karma)
        {
            byte[] response = new byte[10];
            response[0] = ACTION_CODE_GUILD;
            response[1] = 0x05;
            response[2] = status;
            response[3] = category;
            BinaryPrimitives.WriteUInt32LittleEndian(response.AsSpan(4..8), total);
            BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(8..10), karma);
            session.Send(response);
        }
    }
}
```
