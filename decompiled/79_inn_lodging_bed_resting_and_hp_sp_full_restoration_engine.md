# Document 79: Inn Lodging, Bed Resting, and HP/SP Full Restoration Engine

## 1. Executive Summary & Engine Architecture

In Wonderland Online, municipal inns located in towns and settlements (such as Kelan Village, Welling Town, and Kyoto) provide complete character and companion revitalization through the inn lodging system implemented in `se_Inn.pas`. Dissected from client subroutines at RVA `0x002cf3dc` (`FUN_002cf3dc`, constructor), dismissal callback `FUN_002cf738` (RVA `0x002cf738`), dynamic text formatter `FUN_002cf7a8` (RVA `0x002cf7a8`), and confirmation handler `LAB_002cf6b0`, this subsystem executes paid full recovery of Hit Points and Skill Points.

The dialog window (`form_module`, $228 \times 154$ px) presents a confirmation prompt displaying the calculated tariff ($\text{Fee} = \text{Level} \times 10$ Gold) formatted dynamically into rich text label container `FUN_00487118`. Upon confirmation (`btn_YES`), the client initiates a fade-to-black visual sequence via display darkening flag `*(DAT_004c9588 + 0x7108) = 1`, plays the resting audio cue (`wav0144.wav`), and transmits Action Code `0x14` (`20` decimal) Subcode `0x09` to the server. The server verifies gold balances, deducts the fee, and replenishes both the player and active companion pet to 100% of their maximum HP and SP pools.

```
+------------------------------------------------------------------------------------+
|                   INN LODGING & FULL REVITALIZATION ARCHITECTURE                   |
+------------------------------------------------------------------------------------+
                                       |
                                       v
                     [Inn Dialogue Modal: form_module (228x154 px)]
                                       |
         +-----------------------------+-----------------------------+
         | Dynamic Tariff Calculation                                | Visual Fade & Rest Sequence
         v                                                           v
  [Fee Calculus: FUN_002cf7a8]                                [Fade Engine: *(0x4c9588 + 0x7108) = 1]
  - Formula: Level * 10 Gold Coins                            - Screen Darkening Alpha Ramp (1500 ms)
  - String: "Rest in the inn? Fee: %d Gold"                   - Audio Cue: wav0144.wav
  - Rich Text Memo: FUN_00487118                              - Mutex: Disable Movement During Fade
         |                                                           |
         +-----------------------------+-----------------------------+
                                       |
                                       v
                 [Network Dispatcher: Action Code 0x14 (20)]
                  Subcode 0x09: Inn Rest Confirmation Handshake
                                       |
                                       v
               [Server Stat Replenishment: Action Code 0x08 (8)]
             Player HP/SP = 100% | Active Companion Pet HP/SP = 100%
```

---

## 2. Memory Layouts & Data Structures

### 2.1 Inn Modal Dialog Layout (`param_1`)

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `void*` | `VTable` | Delphi component virtual method table |
| `+0x04` | `uint` | `FormHandle` | DirectDraw surface bitmap handle |
| `+0x4C` | `int*` | `pTextMemo` | Dynamic rich text message label container |
| `+0x4D` | `int*` | `pBtnYes` | Affirmative confirmation button (`"btn_YES"`, $44 \times 25$ px) |
| `+0x4E` | `int*` | `pBtnNo` | Dismissal button (`"btn_NO"`, $44 \times 25$ px) |
| `+0x4F` | `int*` | `pBtnCloseS` | Top-right small close button (`"Btn_Close_s_1"`, $17 \times 17$ px) |
| `+0x50` | `int*` | `pPanelContainer`| Inner background border panel (`"panel10"`, $188 \times 68$ px) |
| `+0x51` | `uint` | `IconInn` | Inn municipal crest icon handle (`"icon_inn"`) |
| `+0x130`| `uint` | `InnNpcHandle` | Entity handle of the innkeeper NPC |
| `+0x148`| `uint` | `CalculatedFee`| Dynamic gold fee required for lodging |

### 2.2 Client Screen Darkening State (`PTR_DAT_004c9588`)

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x7108` | `byte` | `IsFadingToBlack`| Screen fade-to-black state (1: Active fade, 0: Normal viewport) |
| `+0x7109` | `byte` | `FadeAlpha` | Current opacity step of fullscreen black quad (0..255) |
| `+0x710C` | `uint` | `FadeTimer` | Millisecond timestamp marking the end of the lodging animation |

---

## 3. Disassembled Subroutine Analysis

### 3.1 Inn Modal Constructor & Control Positioning (`FUN_002cf3dc`)
Extracted from `aLogin_decompiled.c` lines 293283–293330:

```c
// Constructor for Inn Lodging Modal (228 x 154 px)
void FUN_002cf3dc(int *param_1, char param_2) {
    uint uVar1;
    int iVar2, iVar4;
    int *piVar3;

    FUN_0048243c(param_1, 0);
    param_1[0x52] = 0;

    // Center dialog on screen
    uVar1 = *(int *)(*(int *)(*(int *)PTR_DAT_004c96d0 + 0x2d0) + 0x3c) - 154; // 0x9a = 154 px
    iVar2 = (int)uVar1 >> 1;
    uVar1 = *(int *)(*(int *)(*(int *)PTR_DAT_004c96d0 + 0x2d0) + 0x38) - 228; // 0xe4 = 228 px
    iVar4 = (int)uVar1 >> 1;

    // Instantiate form_module DirectDraw surface
    (**(code **)(*param_1 + 8))(param_1, "form_module", iVar4, 180, 200, 0, 0, 1, 154, 228, iVar2);

    // Innkeeper icon & panel background
    param_1[0x51] = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "icon_inn");
    piVar3 = (int *)FUN_00484934(PTR_PTR_0047f408, 1, param_1);
    param_1[0x50] = (int)piVar3;
    (**(code **)(*piVar3 + 8))(piVar3, "panel10", 20, 50, 50, 0, 0, 1, 68, 188, 42);

    // Dynamic rich text container (width 192, height 60)
    piVar3 = (int *)FUN_00485dcc(PTR_PTR_0047fc9c, 1, param_1[0x50]);
    param_1[0x4c] = (int)piVar3;
    FUN_00487118(piVar3, 4, 4, 60, 192);

    // Confirmation button (btn_YES, X = 46, Y = 115)
    piVar3 = (int *)FUN_00485788(PTR_PTR_0047f5f0, 1, param_1);
    param_1[0x4d] = (int)piVar3;
    (**(code **)(*piVar3 + 8))(piVar3, "btn_YES", 46, 25, 44, 0, 0, 1, 25, 44, 115);
    piVar3[0x14] = (int)&LAB_002cf6b0; // Resting confirmation callback

    // Cancel button (btn_NO, X = 140, Y = 115)
    piVar3 = (int *)FUN_00485788(PTR_PTR_0047f5f0, 1, param_1);
    param_1[0x4e] = (int)piVar3;
    (**(code **)(*piVar3 + 8))(piVar3, "btn_NO", 140, 25, 44, 0, 0, 1, 25, 44, 115);
    piVar3[0x14] = (int)FUN_002cf738; // Close modal callback
}
```

### 3.2 Tariff Formatting & Prompt Display (`FUN_002cf7a8`)
Extracted from `aLogin_decompiled.c` lines 293354–293395:

```c
// Format lodging fee and populate inn text memo
void FUN_002cf7a8(int param_1) {
    undefined4 text_container = *(undefined4 *)(param_1 + 0x130);
    FUN_00487bdc(text_container, 0); // Clear existing string

    uint char_level = *(uint *)(*(int *)PTR_DAT_004c9918 + 0x3ab8); // Player level
    uint lodging_fee = char_level * 10;                              // Level * 10 Gold
    *(uint *)(param_1 + 0x148) = lodging_fee;

    // Interpolate format string: "Rest in the inn to recover HP and SP? Fee: %d Gold"
    FUN_0001a310(local_10, &lodging_fee, 0, &DAT_002cf890);
    FUN_00485f94(text_container, local_8); // Commit string to memo control
}
```

### 3.3 Screen Fade & Rest Initiation (`FUN_002cf738` / `LAB_002cf6b0`)
When `btn_YES` is clicked:
1. `*(undefined1 *)(*(int *)PTR_DAT_004c9588 + 0x7108) = 1;` activates fullscreen darkness rendering.
2. The client plays `wav0144.wav` (snoring/sleeping sound effect).
3. The client dispatches Action Code `0x14` Subcode `0x09` with the Innkeeper NPC GUID.

---

## 4. Network Wire Protocol: Action Code 20 (`0x14`)

Inn lodging communicates under **Action Code 20** (`0x14`):

### 4.1 Client Request: Execute Inn Rest (`0x14:0x09`)

```
Offset 0x00: [0x14]            (Action Code 20: NPC Interaction)
Offset 0x01: [0x09]            (Subcode: Inn Lodging Rest Request)
Offset 0x02: [NpcId: uint32]   (Innkeeper NPC Unique Identifier)
Offset 0x06: [AgreedFee: uint32] (Tariff confirmed by client)
```

### 4.2 Server Response: Lodging Result & Stat Sync (`0x14:0x09`)

```
Offset 0x00: [0x14]            (Action Code 20)
Offset 0x01: [0x09]            (Subcode: Lodging Result ACK)
Offset 0x02: [Status: uint8]   (0x00: Success, 0x01: Insufficient Gold, 0x02: In Combat)
Offset 0x03: [DeductedGold: uint32] (Gold subtracted from purse)
```

Immediately followed by **Action Code 8** (`0x08`) Subcode `0x01` synchronizing full HP and SP:

```
Offset 0x00: [0x08]            (Action Code 8: Character Stats Sync)
Offset 0x01: [0x01]            (Subcode: HP / SP Vitals Replenished)
Offset 0x02: [CurrentHp: uint32] (Restored to MaxHP)
Offset 0x06: [CurrentSp: uint32] (Restored to MaxSP)
Offset 0x0A: [PetHp: uint32]     (Active Pet HP restored to PetMaxHP)
Offset 0x0E: [PetSp: uint32]     (Active Pet SP restored to PetMaxSP)
```

---

## 5. C# Server Implementation Blueprint

```csharp
namespace Wonderland.Network.Handlers
{
    public static class InnLodgingHandler
    {
        private const byte ACTION_CODE_NPC = 0x14;
        private const byte ACTION_CODE_STATS = 0x08;

        public static void Register(PacketDispatcher dispatcher)
        {
            dispatcher.RegisterHandler(ACTION_CODE_NPC, HandleInnLodgingRequest);
        }

        public static void HandleInnLodgingRequest(GameSession session, ReadOnlySpan<byte> packet)
        {
            if (packet.Length < 2) return;
            byte subcode = packet[1];

            if (subcode == 0x09) // Inn Rest Request
            {
                HandleInnRest(session, packet[2..]);
            }
        }

        private static void HandleInnRest(GameSession session, ReadOnlySpan<byte> data)
        {
            if (data.Length < 8) return;
            uint npcId = BinaryPrimitives.ReadUInt32LittleEndian(data[0..4]);
            uint agreedFee = BinaryPrimitives.ReadUInt32LittleEndian(data[4..8]);

            var character = session.Character;
            uint requiredFee = (uint)(character.Level * 10);

            if (character.Gold < requiredFee)
            {
                SendInnResponse(session, 0x01, 0); // Insufficient gold
                return;
            }

            // Deduct tariff
            character.Gold -= requiredFee;

            // Fully restore HP and SP
            character.CurrentHp = character.MaxHp;
            character.CurrentSp = character.MaxSp;

            // Fully restore active companion pet
            var pet = character.ActivePet;
            if (pet != null)
            {
                pet.CurrentHp = pet.MaxHp;
                pet.CurrentSp = pet.MaxSp;
            }

            // Acknowledge lodging success
            SendInnResponse(session, 0x00, requiredFee);

            // Synchronize restored vitals
            SendVitalsSync(session, character, pet);
        }

        private static void SendInnResponse(GameSession session, byte status, uint fee)
        {
            byte[] response = new byte[7];
            response[0] = ACTION_CODE_NPC;
            response[1] = 0x09;
            response[2] = status;
            BinaryPrimitives.WriteUInt32LittleEndian(response.AsSpan(3..7), fee);
            session.Send(response);
        }

        private static void SendVitalsSync(GameSession session, Character character, Pet? pet)
        {
            byte[] response = new byte[18];
            response[0] = ACTION_CODE_STATS;
            response[1] = 0x01; // Full Vitals Sync
            BinaryPrimitives.WriteUInt32LittleEndian(response.AsSpan(2..6), character.CurrentHp);
            BinaryPrimitives.WriteUInt32LittleEndian(response.AsSpan(6..10), character.CurrentSp);
            BinaryPrimitives.WriteUInt32LittleEndian(response.AsSpan(10..14), pet?.CurrentHp ?? 0);
            BinaryPrimitives.WriteUInt32LittleEndian(response.AsSpan(14..18), pet?.CurrentSp ?? 0);
            session.Send(response);
        }
    }
}
```
