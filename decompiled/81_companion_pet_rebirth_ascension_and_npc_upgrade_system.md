# Document 81: Companion Pet Rebirth Ascension, Reincarnation Stats, and NPC Upgrade System

## 1. Executive Summary & Engine Architecture

In Wonderland Online, companion human NPCs and animal pets (such as Roca, Niss, Clive, Fred, Sam, and riding mounts) achieve ultimate transcendence through the Pet Rebirth / Newborn Upgrade engine implemented in `form_NPCUpgrade` ($360 \times 422$ px, RVA `0x00245705`). Dissected from client subroutines in `Se_NpcUpgrade.pas` and `DCJ_AbilityUpgradeManage.pas` at RVA `0x00245705` (constructor), prerequisite validator `FUN_0024e808` (RVA `0x0024e808`), and directional selectors `FUN_0024eb74` and `FUN_0024eb24`, this subsystem governs companion reincarnation.

Companion rebirth requires strict qualification criteria verified by `FUN_0024e808`:
1. The companion must attain a minimum level of 100 (`*(iVar1 + 0x1f98) >= 100`).
2. The companion must maintain an amity intimacy rating of 100 (`*(iVar1 + 0x21c5) >= 100`, evaluated via threshold byte `0x3B`).
3. The player must possess an authentic rebirth catalyst item of Category Code `'%'` (e.g., *Star of Destiny* or *Rebirth Heart*).

Upon ascension, the companion's appearance updates with an angelic/celestial halo or aura ("newborn" banner), their level resets to 1, their base attribute coefficients are increased by +15% to +25%, unique transcendent skills unlock (e.g., Roca's *X-Strike*, Niss's *Thunderbolt Wave*), and the companion unlocks the ability to wear player-grade forged equipment via Action Code `0x0F` (`15` decimal) and Action Code `0x23` (`35` decimal).

```
+------------------------------------------------------------------------------------+
|               COMPANION PET REBIRTH & NPC UPGRADE ENGINE ARCHITECTURE               |
+------------------------------------------------------------------------------------+
                                       |
                                       v
                     [form_NPCUpgrade Container (360x422 px)]
                                       |
         +-----------------------------+-----------------------------+
         | Qualification Validator: FUN_0024e808                     | Rebirth Allocation & Visuals
         v                                                           v
  [Prerequisite Gates]                                        [Post-Ascension Transformation]
  - Pet Level: >= 100 (0x1f98)                                - Reset Level to 1, Preserve Amity (100)
  - Intimacy Amity: >= 100 (0x21c5)                           - Attribute Multiplier: +20% Base Stats
  - Catalyst: Item Category '%' (Item.dat)                    - Unlock Celestial Skills (Skill.dat)
  - Arrows: Arrow_L2 / Arrow_R2                               - Aura: "newborn" Title Sprite
         |                                                           |
         +-----------------------------+-----------------------------+
                                       |
                                       v
                 [Network Dispatcher: Action Code 0x0F (15)]
              Subcode 0x0A: Companion Rebirth Ascension Handshake
```

---

## 2. Memory Layouts & Data Structures

### 2.1 Companion Pet Rebirth Memory Record (`0x2200` Bytes in `PTR_DAT_004c9918`)

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x04` | `uint` | `CompanionId` | Unique 32-bit companion GUID |
| `+0x08` | `ushort`| `NpcTemplateId`| Base companion archetype ID from `Npc.dat` |
| `+0x1F98` | `byte` | `Level` | Current companion level (1..199) |
| `+0x1F82` | `byte` | `RebirthStatus`| Ascension flag (0: Normal, 1: Reborn / Newborn) |
| `+0x21C5` | `byte` | `AmityIntimacy`| Loyalty / Friendship score (0..100) |
| `+0x21C6` | `byte` | `Element` | Elemental affinity (1: Earth, 2: Water, 3: Fire, 4: Wind) |
| `+0x21C8` | `uint` | `RebirthSkillId`| Newly unlocked transcendent ability ID |
| `+0x21CC` | `ushort`| `AllocatedStr` | Post-rebirth bonus Strength points |
| `+0x21CE` | `ushort`| `AllocatedCon` | Post-rebirth bonus Constitution points |
| `+0x21D0` | `ushort`| `AllocatedInt` | Post-rebirth bonus Intelligence points |
| `+0x21D2` | `ushort`| `AllocatedWis` | Post-rebirth bonus Wisdom points |
| `+0x21D4` | `ushort`| `AllocatedAgi` | Post-rebirth bonus Agility points |

### 2.2 `form_NPCUpgrade` Instance Layout (`param_1`)

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `void*` | `VTable` | Delphi component virtual method table pointer |
| `+0x04` | `uint` | `FormHandle` | DirectDraw surface bitmap handle |
| `+0x37` | `uint` | `SpriteNewborn`| Newborn banner aura texture handle (`"newborn"`) |
| `+0x3A` | `int*` | `pBtnCloseS` | Top-right small close button (`"Btn_Close_s_1"`, $17 \times 17$ px) |
| `+0x3B` | `int*` | `pBtnOk` | Commit rebirth button (`"btn_ok_1"`, $70 \times 25$ px) |
| `+0x3C` | `int*` | `pBtnCancel` | Cancel button (`"btn_cancel"`, $70 \times 25$ px) |
| `+0x3D` | `int*` | `pArrowL` | Select previous companion button (`"Arrow_L2"`, $27 \times 19$ px) |
| `+0x3E` | `int*` | `pArrowR` | Select next companion button (`"Arrow_R2"`, $27 \times 19$ px) |
| `+0x51..0x54`| `uint[4]`| `IconStars` | Star rating decorative icons (`"icon_01"`..`"icon_04"`) |
| `+0x55` | `uint` | `IconGrade` | Transcendent grade indicator (`"icon_09"`) |
| `+0x108`| `int*` | `pListSource` | Active companion candidate container |
| `+0x10C`| `int*` | `pListTarget` | Preview post-ascension candidate container |

---

## 3. Disassembled Subroutine Analysis

### 3.1 Form Construction & Surface Binding (`FUN_00245705`)
Extracted from `aLogin_decompiled.c` lines 245700–245763:

```c
// Constructor for form_NPCUpgrade (360 x 422 px)
int * FUN_00245705(int *param_1, char param_2) {
    int iVar1, iVar3;
    int *piVar2;

    FUN_0048eb38(param_1, 0);
    // Instantiate DirectDraw surface: width 0x168 = 360 px, height 0x1a6 = 422 px
    (**(code **)(*param_1 + 8))(param_1, "form_NPCUpgrade", 0x17a, 0, 0, 0, 0, 0, 360, 422, 0);

    param_1[0x37] = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "newborn");

    // Close button
    piVar2 = (int *)FUN_00485788(PTR_PTR_0047f5f0, 1, param_1);
    param_1[0x3a] = (int)piVar2;
    (**(code **)(*piVar2 + 8))(piVar2, "Btn_Close_s_1", param_1[8] - 0x17, 17, 17, 0, 0, 1, 17, 17, 5);

    // Confirmation button (btn_ok_1, X = 113, Y = 314)
    piVar2 = (int *)FUN_00485788(PTR_PTR_0047f5f0, 1, param_1);
    param_1[0x3b] = (int)piVar2;
    (**(code **)(*piVar2 + 8))(piVar2, "btn_ok_1", 113, 25, 70, 0, 0, 1, 25, 70, 314);
    piVar2[0x14] = (int)&LAB_0024e6dc;

    // Directional companion selector arrows
    piVar2 = (int *)FUN_00485788(PTR_PTR_0047f5f0, 1, param_1);
    param_1[0x3d] = (int)piVar2;
    (**(code **)(*piVar2 + 8))(piVar2, "Arrow_L2", 42, 19, 27, 0, 0, 1, 19, 27, 132);
    piVar2[0x14] = (int)FUN_0024eb74;

    piVar2 = (int *)FUN_00485788(PTR_PTR_0047f5f0, 1, param_1);
    param_1[0x3e] = (int)piVar2;
    (**(code **)(*piVar2 + 8))(piVar2, "Arrow_R2", 155, 19, 27, 0, 0, 1, 19, 27, 132);
    piVar2[0x14] = (int)FUN_0024eb24;

    // Load star decorative sprites (icon_01..icon_04)
    iVar1 = 1;
    do {
        FUN_0001953c(iVar1, &local_10);
        FUN_00014178(&local_c, "icon_0", local_10);
        param_1[iVar1 + 0x50] = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, local_c);
        iVar1 = iVar1 + 1;
    } while (iVar1 != 5);
    param_1[0x55] = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "icon_09");

    return param_1;
}
```

### 3.2 Prerequisite Eligibility Validation (`FUN_0024e808`)
Extracted from `aLogin_decompiled.c` lines 245785–245805:

```c
// Validates companion level, intimacy amity, and inventory catalyst
void FUN_0024e808(int param_1) {
    int iVar4 = 1;
    undefined1 item_record[15];

    // Scan player bag for Rebirth Catalyst (Category Code '%')
    do {
        int bag_slot = *(int *)(*(int *)PTR_DAT_004c9918 + 0x27e8 + iVar4 * 4);
        if (bag_slot != 0) {
            FUN_003e8db4(*(undefined4 *)PTR_DAT_004c94f8, *(ushort *)(bag_slot + 4), item_record);
            if (item_record[14] == '%') { // Rebirth Catalyst Item
                // Check all roster companion pets (1..4)
                int pet_slot = 1;
                do {
                    int pet_actor = *(int *)(*(int *)PTR_DAT_004c9918 + 0x21e8 + pet_slot * 4);
                    if (pet_actor != 0) {
                        byte pet_level = *(byte *)(pet_actor + 0x1f98);
                        byte pet_amity = *(byte *)(pet_actor + 0x21c5);

                        // Qualification Gate: Level >= 100 AND Amity >= 100 (threshold 0x3b = 59)
                        if (pet_level >= 100 && pet_amity >= 100) {
                            // Populate candidate list in dialog
                            FUN_000219ac(*(undefined4 *)(param_1 + 0x108), pet_actor);
                            FUN_000219ac(*(undefined4 *)(param_1 + 0x10c), pet_actor);
                        }
                    }
                    pet_slot = pet_slot + 1;
                } while (pet_slot != 5);
            }
        }
        iVar4 = iVar4 + 1;
    } while (iVar4 != 51);
}
```

---

## 4. Network Wire Protocol: Action Code 15 (`0x0F`)

Companion rebirth communicates under **Action Code 15** (`0x0F`):

### 4.1 Client Request: Commit Companion Rebirth (`0x0F:0x0A`)

```
Offset 0x00: [0x0F]            (Action Code 15: Pet & Companion Management)
Offset 0x01: [0x0A]            (Subcode: Rebirth Ascension Request)
Offset 0x02: [PetSlot: uint8]  (Companion Roster Index 1..4)
Offset 0x03: [CharId: uint32]  (Companion Database GUID)
Offset 0x07: [CatalystSlot: uint8] (Inventory Slot containing '%' item)
```

### 4.2 Server Response: Ascension Status & Stat Reset (`0x0F:0x0A`)

```
Offset 0x00: [0x0F]            (Action Code 15)
Offset 0x01: [0x0A]            (Subcode: Rebirth Result ACK)
Offset 0x02: [Status: uint8]   (0x00: Success, 0x01: Underlevel, 0x02: Low Amity, 0x03: Missing Item)
Offset 0x03: [PetSlot: uint8]  (Companion Slot 1..4)
Offset 0x04: [NewLevel: uint8] (Reset to 1)
Offset 0x05: [NewbornFlag: uint8] (Set to 1: Reborn)
Offset 0x06: [NewSkillId: uint16] (Unlocked Transcendent Skill ID)
Offset 0x08: [BonusPoints: uint16] (Bonus stat points awarded based on pre-rebirth level)
```

---

## 5. C# Server Implementation Blueprint

```csharp
namespace Wonderland.Network.Handlers
{
    public static class CompanionRebirthHandler
    {
        private const byte ACTION_CODE_PET = 0x0F;

        public static void Register(PacketDispatcher dispatcher)
        {
            dispatcher.RegisterHandler(ACTION_CODE_PET, HandlePetRebirthRequest);
        }

        public static void HandlePetRebirthRequest(GameSession session, ReadOnlySpan<byte> packet)
        {
            if (packet.Length < 2) return;
            byte subcode = packet[1];

            if (subcode == 0x0A) // Rebirth Handshake
            {
                HandleCompanionAscension(session, packet[2..]);
            }
        }

        private static void HandleCompanionAscension(GameSession session, ReadOnlySpan<byte> data)
        {
            if (data.Length < 6) return;
            byte petSlot = data[0];
            uint companionId = BinaryPrimitives.ReadUInt32LittleEndian(data[1..5]);
            byte catalystSlot = data[5];

            var character = session.Character;
            var companion = character.PetRoster.FirstOrDefault(p => p.Id == companionId);

            if (companion == null || companion.Level < 100 || companion.Amity < 100)
            {
                SendRebirthResponse(session, 0x01, petSlot, 0, 0, 0); // Disqualified
                return;
            }

            var catalyst = character.Inventory.GetItem(catalystSlot);
            if (catalyst == null || catalyst.Template.Category != '%')
            {
                SendRebirthResponse(session, 0x03, petSlot, 0, 0, 0); // Missing item
                return;
            }

            // Consume catalyst
            character.Inventory.RemoveItem(catalystSlot, 1);

            // Calculate bonus points based on pre-rebirth level (Level 100 = +20, Level 199 = +60)
            ushort bonusPoints = (ushort)(20 + (companion.Level - 100) * 0.4);

            // Execute Rebirth Ascension
            companion.IsReborn = true;
            companion.Level = 1;
            companion.Experience = 0;
            companion.StatPoints += bonusPoints;
            companion.UnlockRebirthSkill();

            SendRebirthResponse(session, 0x00, petSlot, 1, companion.RebirthSkillId, bonusPoints);
            session.SendSystemNotice($"{companion.Name} has achieved celestial rebirth!");
        }

        private static void SendRebirthResponse(GameSession session, byte status, byte slot, byte level, ushort skillId, ushort bonusPts)
        {
            byte[] response = new byte[10];
            response[0] = ACTION_CODE_PET;
            response[1] = 0x0A;
            response[2] = status;
            response[3] = slot;
            response[4] = level;
            response[5] = 1; // Newborn flag
            BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(6..8), skillId);
            BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(8..10), bonusPts);
            session.Send(response);
        }
    }
}
```
