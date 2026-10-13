# Document 75: Member Point Card Recharge, Serial Key Verification, and Billing Gateway Bridge

## 1. Executive Summary & Engine Architecture

In Wonderland Online, subscription billing, point card redemption, and character account slot authorization are managed through the Member Card interface `Form_MemCard2` ($397 \times 371$ px) and its single-slot counterpart `Form_MemCard` (`DAT_006dbc9c`). Dissected from client subroutines in `se_MemCard.pas` at RVA `0x001ba44c` (constructor), packet demux `FUN_001bbb14` (RVA `0x001bbb14`), status sync `FUN_001b9e10` (RVA `0x001b9e10`), and uncachet callback `FUN_001bb834` (RVA `0x001bb834`), this subsystem controls game time balances, sealed/unsealed character states ("Cachet"), and payment serial validation.

The client maintains an interactive 3-character slot management container. When subscription or point card time expires, characters enter a sealed state (`Cachet = 1`), preventing login until unsealed using account-level points (`Btn_UnCachet_2`). The remaining subscription time is visually represented as a 108-pixel progress gauge and queried dynamically through `icon_Time_1` ("Check Time Left"). Serial card redemptions bridge to the external billing gateway (Chinese Gamer / MyCard billing web endpoints) via Action Code `0x4F` (`79` decimal) and Action Code `0x3E` (`62` decimal).

```
+------------------------------------------------------------------------------------+
|                MEMBER POINT CARD & BILLING GATEWAY ARCHITECTURE                    |
+------------------------------------------------------------------------------------+
                                       |
                                       v
                     [Form_MemCard2 Container (397x371 px)]
             3 Interactive Character Slots (Vertical Stride 0x52 = 82 px)
                                       |
         +-----------------------------+-----------------------------+
         | Visual Character Roster                                   | Time Calculation & Unseal Gate
         v                                                           v
  [Slot Elements (1..3)]                                      [Time Gauge & State Manager]
  - Name, Job, Element, Rebirth                               - Gauge Ratio: (Points * 500,000) / 108
  - Level: Main_Word_Lv + Num_White3_1                        - Sealed Flag: *(actor + 0xb1) = 2
  - Online/Offline: icon_online_1 / icon_offline_1            - Time Check: icon_Time_1
  - Mail Alert: icon_mail_1                                   - Unseal Request: Btn_UnCachet_2
         |                                                           |
         +-----------------------------+-----------------------------+
                                       |
                                       v
                 [Network Dispatcher: Action Code 0x4F (79)]
         Subcode 0x01: Point Card Serial Submission
         Subcode 0x02: Character Unseal (UnCachet) Request
         Subcode 0x03: Remaining Time Balance Inquiry
```

---

## 2. Memory Layouts & Data Structures

### 2.1 Account Slot Record Structure (`0x44` = 68 Bytes per Slot)
Inside `Form_MemCard2` (`param_1 + 0x138 + slot_index * 4`), each character profile record maintains:

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `void*` | `VTable` | Delphi control virtual table pointer |
| `+0x04` | `uint` | `CharacterId` | Unique 32-bit character database GUID |
| `+0x08` | `byte` | `Level` | Current character level (1..199) |
| `+0x09` | `char[17]`| `CharacterName`| Null-terminated character nameplate |
| `+0x1A` | `byte` | `BodyType` | Visual body asset model index |
| `+0x1B` | `byte` | `JobType` | Class / Job index (0: None, 1..6: Rebirth classes) |
| `+0x1C` | `byte` | `Element` | Elemental affinity (1: Earth, 2: Water, 3: Fire, 4: Wind) |
| `+0x1D` | `byte` | `HairStyle` | Hair visual sprite index |
| `+0x1E` | `byte` | `HairColor` | Hair palette tint index |
| `+0x1F` | `byte` | `SkinColor` | Skin tone palette index |
| `+0x28` | `char[14]`| `GuildName` | Legion / Army Group nameplate string |
| `+0x37` | `char[14]`| `MapName` | Last logged zone / map display string |
| `+0x4C` | `uint` | `CurrentExp` | Current experience points accrued |
| `+0x50` | `uint` | `MaxExp` | Total experience points required for next level |
| `+0xB1` | `byte` | `AccountState` | Account lock status (0: Unlocked, 1: Expired, 2: Sealed/Cachet) |
| `+0xB4` | `byte` | `RebirthState` | Ascension status (0: Normal, 1: Reborn) |

### 2.2 `Form_MemCard2` Instance Layout (`param_1`)

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `void*` | `VTable` | DirectDraw component virtual method table |
| `+0x04` | `uint` | `FormHandle` | DirectDraw surface bitmap handle |
| `+0x3A` | `int*` | `pBtnClose` | Bottom cancel button (`"btn_close_1"`, $56 \times 20$ px) |
| `+0x3B` | `int*` | `pBtnCloseS` | Top-right close button (`"Btn_Close_s_1"`, $18 \times 18$ px) |
| `+0x3C..0x3E`| `int*[3]` | `pBtnUnCachet` | Array of 3 character unseal buttons (`"Btn_UnCachet_2"`) |
| `+0x3F..0x41`| `int*[3]` | `pIconTime` | Array of 3 time check buttons (`"icon_Time_1"`) |
| `+0x42` | `uint` | `ColorG` | Green color palette handle (`"Color_G"`) |
| `+0x52` | `uint` | `WordLv` | Level label sprite (`"Main_Word_Lv"`) |
| `+0x53` | `uint` | `NumWhite3` | Digit font texture (`"Num_White3_1"`) |
| `+0x54` | `uint` | `IconOnline` | Online indicator sprite handle (`"icon_online_1"`) |
| `+0x55` | `uint` | `IconOffline`| Offline indicator sprite handle (`"icon_offline_1"`) |
| `+0x5A..0x5C`| `int*[3]` | `pIconMail` | Mail alert indicators (`"icon_mail_1"`) |
| `+0x112..0x124`| `uint[3]`| `TimeSeconds` | Remaining active time in seconds for slots 1..3 |
| `+0x128..0x130`| `uint[3]`| `GaugeWidth` | Computed progress bar width in pixels (0..108 px) |
| `+0x138..0x140`| `int*[3]` | `pSlotRecords`| Pointers to the 3 slot character records |

---

## 3. Disassembled Subroutine Analysis

### 3.1 Form Construction & 3-Slot Grid Layout (`FUN_001ba44c`)
Extracted from `aLogin_decompiled.c` lines 193320–193430:

```c
// Constructor for Form_MemCard2
int * FUN_001ba44c(int *param_1, char param_2) {
    uint uVar1;
    int iVar2, iVar4;
    int *piVar3;

    // Centering calculation relative to main DirectDraw display surface
    uVar1 = *(int *)(*(int *)(*(int *)PTR_DAT_004c96d0 + 0x2d0) + 0x3c) - 397; // 0x18d = 397 px
    iVar2 = (int)uVar1 >> 1;
    uVar1 = *(int *)(*(int *)(*(int *)PTR_DAT_004c96d0 + 0x2d0) + 0x38) - 371; // 0x173 = 371 px
    iVar4 = (int)uVar1 >> 1;

    // Instantiate Form_MemCard2 DirectDraw surface
    (**(code **)(*param_1 + 8))(param_1, "Form_MemCard2", iVar4, 0, 0, 0, 0, 0, 397, 371, iVar2);

    // Initialize asset handles
    param_1[0x42] = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "Color_G");
    param_1[0x52] = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "Main_Word_Lv");
    param_1[0x53] = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "Num_White3_1");
    param_1[0x54] = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "icon_online_1");
    param_1[0x55] = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "icon_offline_1");

    // Loop 1..3: Instantiate Mail Icons, UnCachet Buttons, Time Check Icons
    iVar2 = 1;
    do {
        // Mail Icon
        iVar4 = FUN_00484934(PTR_PTR_0047f408, 1, param_1);
        param_1[iVar2 + 0x59] = iVar4;
        (**(code **)(*(int *)param_1[iVar2 + 0x59] + 8))(
            (int *)param_1[iVar2 + 0x59], "icon_mail_1", 0x137, 15, 20, 0, 0, 1, 15, 20, (iVar2 - 1) * 82 + 95
        );

        // UnCachet Button (Unseal Character)
        piVar3 = (int *)FUN_00485788(PTR_PTR_0047f5f0, 1, param_1);
        param_1[iVar2 + 0x3b] = (int)piVar3;
        (**(code **)(*piVar3 + 8))(
            piVar3, "Btn_UnCachet_2", 0x120, 20, 56, 0, 0, 1, 20, 56, (iVar2 - 1) * 82 + 147
        );
        piVar3[0x2c] = iVar2; // Slot index
        piVar3[0x1d] = (int)param_1;
        piVar3[0x1c] = (int)&LAB_001b9fc4; // UnCachet click handler

        // Time Check Icon ("Check Time Left")
        piVar3 = (int *)FUN_00485788(PTR_PTR_0047f5f0, 1, param_1);
        param_1[iVar2 + 0x3e] = (int)piVar3;
        (**(code **)(*piVar3 + 8))(
            piVar3, "icon_Time_1", 0x104, 21, 21, 0, 0, 1, 21, 21, (iVar2 - 1) * 82 + 146
        );
        piVar3[0x2c] = iVar2;
        piVar3[0x1d] = (int)param_1;
        piVar3[0x1c] = (int)&LAB_001bca84; // Time check click handler
        FUN_00480ae4(piVar3, "Check Time Left");

        iVar2 = iVar2 + 1;
    } while (iVar2 != 4);

    return param_1;
}
```

### 3.2 Time Gauge Calculus & Slot Parsing (`FUN_001bbb14`)
Extracted from `aLogin_decompiled.c` lines 193714–193815:

```c
// Incoming Account Slot & Subscription Status Synchronizer
void FUN_001bbb14(int param_1, int packet_data) {
    byte slot_idx = *(byte *)(packet_data + 6); // Slot 1..3
    uint player_id = FUN_00112f58(*(undefined4 *)PTR_DAT_004c87fc, *(undefined4 *)(packet_data + 3));

    // Verify player session integrity
    if (player_id == *(int *)(*(int *)PTR_DAT_004c9918 + 4)) {
        // Unpack time seconds
        uint time_seconds = FUN_00112f58(*(undefined4 *)PTR_DAT_004c87fc, *(undefined4 *)(packet_data + 8));
        *(uint *)(param_1 + 0x112 + (uint)slot_idx * 6) = time_seconds;

        // Visual Progress Gauge Width Calculus (Max 108 px / 0x6C)
        // Formula: BarWidth = ((TierIndex + 1) * 500,000) / 108
        uint gauge_factor = ((*(byte *)(param_1 + 0x117 + (uint)slot_idx * 6) + 1) * 500000) / 108;
        *(uint *)(param_1 + 0x128 + (uint)slot_idx * 4) = gauge_factor;
        byte bar_pixels = FUN_00017002(time_seconds, 0, 108, gauge_factor, gauge_factor >> 0x1f);
        *(byte *)(param_1 + 0x137 + (uint)slot_idx) = (bar_pixels > 108) ? 108 : bar_pixels;

        // Parse Character Name, Guild Name, Map Name
        byte name_len = *(byte *)(packet_data + 0x1d);
        // ... string copies to character record ...

        // Set Account State to Unsealed / Active
        *(byte *)(*(int *)(param_1 + 0x138 + (uint)slot_idx * 4) + 0xb1) = 2;

        // Enable UI buttons for active slot
        FUN_00480670(*(undefined4 *)(param_1 + 0xec + (uint)slot_idx * 4), 1);
        FUN_00480670(*(undefined4 *)(param_1 + 0x164 + (uint)slot_idx * 4), 1);
        FUN_00480670(*(undefined4 *)(param_1 + 0xf8 + (uint)slot_idx * 4), 1);
    }
}
```

---

## 4. Network Wire Protocol: Action Code 79 (`0x4F`)

Billing point card redemption and character unsealing communicate under **Action Code 79** (`0x4F`):

### 4.1 Client Request: Unseal Character (UnCachet) (`0x4F:0x02`)

```
Offset 0x00: [0x4F]            (Action Code 79: Member Card Service)
Offset 0x01: [0x02]            (Subcode: Request Character Unseal)
Offset 0x02: [SlotIndex: uint8] (Target Slot 1..3)
Offset 0x03: [CharId: uint32]   (32-bit Character GUID)
Offset 0x07: [PointsCost: uint32] (Points to deduct from account wallet)
```

### 4.2 Server Response: Slot State & Time Balance (`0x4F:0x02`)

```
Offset 0x00: [0x4F]            (Action Code 79)
Offset 0x01: [0x02]            (Subcode: Unseal Result)
Offset 0x02: [Status: uint8]   (0x00: Success, 0x01: Insufficient Points, 0x02: DB Error)
Offset 0x03: [SlotIndex: uint8] (Target Slot 1..3)
Offset 0x04: [NewBalance: uint32] (Remaining account points)
Offset 0x08: [ExpiryTime: uint32] (Unix timestamp of new expiration date)
```

### 4.3 Client Request: Time Balance Inquiry (`0x4F:0x03`)

```
Offset 0x00: [0x4F]            (Action Code 79)
Offset 0x01: [0x03]            (Subcode: Time Check)
Offset 0x02: [SlotIndex: uint8] (Target Slot 1..3)
```

Server returns notification floating dialog: `"Your account has %d hours and %d minutes remaining"`.

---

## 5. C# Server Implementation Blueprint

```csharp
namespace Wonderland.Network.Handlers
{
    public static class MemberCardHandler
    {
        private const byte ACTION_CODE_MEMCARD = 0x4F;

        public static void Register(PacketDispatcher dispatcher)
        {
            dispatcher.RegisterHandler(ACTION_CODE_MEMCARD, HandleMemberCardRequest);
        }

        public static void HandleMemberCardRequest(GameSession session, ReadOnlySpan<byte> packet)
        {
            if (packet.Length < 2) return;
            byte subcode = packet[1];

            switch (subcode)
            {
                case 0x02: // Unseal character
                    HandleUnsealCharacter(session, packet[2..]);
                    break;
                case 0x03: // Time inquiry
                    HandleTimeInquiry(session, packet[2..]);
                    break;
            }
        }

        private static void HandleUnsealCharacter(GameSession session, ReadOnlySpan<byte> data)
        {
            if (data.Length < 9) return;
            byte slotIndex = data[0];
            uint characterId = BinaryPrimitives.ReadUInt32LittleEndian(data[1..5]);
            uint pointsCost = BinaryPrimitives.ReadUInt32LittleEndian(data[5..9]);

            var account = session.Account;
            if (account.MemberPoints < pointsCost)
            {
                // Send failure: Insufficient Points
                SendUnsealResponse(session, 0x01, slotIndex, account.MemberPoints, 0);
                return;
            }

            account.MemberPoints -= pointsCost;
            var character = account.Characters.FirstOrDefault(c => c.Id == characterId);
            if (character != null)
            {
                character.IsSealed = false;
                character.SubscriptionExpiry = DateTime.UtcNow.AddDays(30);
                SendUnsealResponse(session, 0x00, slotIndex, account.MemberPoints, (uint)((DateTimeOffset)character.SubscriptionExpiry).ToUnixTimeSeconds());
            }
        }

        private static void SendUnsealResponse(GameSession session, byte status, byte slot, uint balance, uint expiry)
        {
            byte[] response = new byte[12];
            response[0] = ACTION_CODE_MEMCARD;
            response[1] = 0x02;
            response[2] = status;
            response[3] = slot;
            BinaryPrimitives.WriteUInt32LittleEndian(response.AsSpan(4..8), balance);
            BinaryPrimitives.WriteUInt32LittleEndian(response.AsSpan(8..12), expiry);
            session.Send(response);
        }

        private static void HandleTimeInquiry(GameSession session, ReadOnlySpan<byte> data)
        {
            byte slotIndex = data[0];
            var character = session.Account.Characters.ElementAtOrDefault(slotIndex - 1);
            if (character != null)
            {
                var remaining = character.SubscriptionExpiry - DateTime.UtcNow;
                string msg = remaining.TotalSeconds > 0 
                    ? $"Time left: {(int)remaining.TotalHours}h {remaining.Minutes}m."
                    : "Account time has expired. Please recharge.";
                session.SendSystemNotice(msg);
            }
        }
    }
}
```
