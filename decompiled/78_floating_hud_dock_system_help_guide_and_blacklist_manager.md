# Document 78: Floating HUD Dock, System Help Guide, and Blacklist Privacy Manager

## 1. Executive Summary & Engine Architecture

In Wonderland Online, client contextual guidance and communication privacy are governed by two specialized Delphi modules: the right-anchored floating help dock `Form_Module_1` ($223 \times 327$ px, RVA `0x00240515`) and the player block/ignore roster `form_modulePlus` ($350 \times 228$ px, RVA `0x002513d4`). Dissected from client subroutines in `se_Module.pas` and `se_ModulePlus.pas`, these engines manage player onboarding tutorials and bilateral harassment filtering.

`Form_Module_1` docks dynamically to the rightmost boundary of the primary DirectDraw display canvas (`*(DAT_004c96d0 + 0x2d0) + 0x38 - 0xdf`). It organizes 16 multimedia tutorials into 4 major chapters via selector subroutine `FUN_00240f54` (Newbie Basics `0x3e6f`, System Mechanics `0x3e72`, World Navigation `0x3e79`, and Advanced Synthesis `0x3e7d`). Concurrently, `form_modulePlus` enforces social privacy through a 10-slot blacklist table (`btn_unBlock`, `btn_Block`, `bar_H3`), suppressing private whispers, party requests, trade invitations, and mail parcels from targeted character IDs via Action Code `0x0E` (`14` decimal) Subcode `0x04`.

```
+------------------------------------------------------------------------------------+
|                   FLOATING HUD DOCK & BLACKLIST PRIVACY ARCHITECTURE               |
+------------------------------------------------------------------------------------+
                                       |
         +-----------------------------+-----------------------------+
         | Right-Docked Tutorial Guide                               | Social Blacklist Manager
         v                                                           v
  [Form_Module_1 (223x327 px)]                                [form_modulePlus (350x228 px)]
  - X Anchor: ScreenWidth - 223 px                            - 10 Blocked Character Slots
  - Page Flips: Btn_ArrowL_5 / ArrowR_5                       - Name Input: editorBG (Max 16 chars)
  - 4 Categories: 0x3E6F..0x3E7F                              - Unblock Buttons: btn_unBlock
  - Content Renderer: FUN_00240f54                            - Whisper / Trade Mutex Filter
         |                                                           |
         +-----------------------------+-----------------------------+
                                       |
                                       v
                 [Network Dispatcher: Action Code 0x0E (14)]
             Subcode 0x04: Blacklist Add / Remove Synchronization
```

---

## 2. Memory Layouts & Data Structures

### 2.1 Help Topic Category Matrix (`FUN_00240f54`)

| Mode (`param_2`) | Sub-Page (`param_3`) | Asset ID | Category Title | Topic Description |
|---|---|---|---|---|
| `0` | `1..3` | `0x3E6F`..`0x3E71` | Basic Controls | Movement, interface buttons, shortcuts, inventory |
| `1` | `1..7` | `0x3E72`..`0x3E78` | Game Systems | Pet care, amity, tent placement, furniture, crafting |
| `2` | `1..3` | `0x3E79`..`0x3E7B` | World Travel | Ship navigation, vehicle flight, portal transitions |
| `3` | `1..3` | `0x3E7D`..`0x3E7F` | Advanced Combat | Rebirth classes, skill affinities, alchemy compounding |

### 2.2 `form_modulePlus` Instance Layout (`param_1`)

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `void*` | `VTable` | DirectDraw component virtual method table |
| `+0x04` | `uint` | `FormHandle` | DirectDraw surface bitmap handle |
| `+0x4B` | `uint` | `IconBlock` | Block header banner icon handle (`"icon_block"`) |
| `+0x4C` | `int*` | `pBtnClose` | Dismiss dialog button (`"Btn_Close_1"`, $70 \times 25$ px) |
| `+0x4D` | `int*` | `pBtnCloseS` | Top-right small close button (`"Btn_Close_s_1"`, $17 \times 17$ px) |
| `+0x4E` | `int*` | `pBtnBlock` | Add character to blacklist button (`"btn_Block"`, $44 \times 25$ px) |
| `+0x4F..0x58`| `int*[10]` | `pBtnUnBlock` | Array of 10 individual unblock buttons (`"btn_unBlock"`) |
| `+0x59` | `int*` | `pEditName` | Nameplate text entry control (`"editorBG"`, $140 \times 22$ px) |
| `+0x5A` | `int*` | `pPanelContainer`| Background scroll container panel (`"panel10"`) |
| `+0x5B` | `int*` | `pScrollBar` | Vertical scrollbar slider (`"bar_H3"`, `"rail_H3"`) |
| `+0x5C` | `int*` | `pListBox` | Scrollable viewport client window |
| `+0x5D` | `uint` | `IconBlockMsg` | Blacklist information banner icon handle (`"icon_BlockMsg"`) |

---

## 3. Disassembled Subroutine Analysis

### 3.1 Right-Docked Help HUD Constructor (`FUN_00240500`)
Extracted from `aLogin_decompiled.c` lines 240510–240568:

```c
// Constructor for Form_Module_1 (223 x 327 px)
int * FUN_00240500(int *param_1, char param_2) {
    int iVar1;
    undefined4 uVar3;

    FUN_0048243c(param_1, 0);
    // Right-anchoring calculus: ScreenWidth - 223 px (0xdf)
    int dock_x = *(int *)(*(int *)(*(int *)PTR_DAT_004c96d0 + 0x2d0) + 0x38) - 223;
    
    // Instantiate surface: width 0xdf = 223 px, height 0x147 = 327 px
    (**(code **)(*param_1 + 8))(param_1, "Form_Module_1", dock_x, 223, 223, 0, 0, 1, 327, 223, 100);
    (**(code **)(*param_1 + 0x70))(param_1, 75, 90, 53, 80);

    // Close button
    param_1[0x4c] = FUN_00485788(PTR_PTR_0047f5f0, 1, param_1);
    (**(code **)(*(int *)param_1[0x4c] + 8))((int *)param_1[0x4c], "Btn_Close_1", dock_x / 2, 20, 56, 0, 0, 1, 20, 56, 292);

    // Left and Right page-flip arrow buttons
    param_1[0x4e] = FUN_00485788(PTR_PTR_0047f5f0, 1, param_1);
    (**(code **)(*(int *)param_1[0x4e] + 8))((int *)param_1[0x4e], "Btn_ArrowL_5", 75, 17, 17, 0, 0, 1, 17, 17, 50);
    *(undefined1 **)(param_1[0x4e] + 0x50) = &LAB_00240f44; // Prev page callback

    param_1[0x4f] = FUN_00485788(PTR_PTR_0047f5f0, 1, param_1);
    (**(code **)(*(int *)param_1[0x4f] + 8))((int *)param_1[0x4f], "Btn_ArrowR_5", 135, 17, 17, 0, 0, 1, 17, 17, 50);
    *(undefined1 **)(param_1[0x4f] + 0x50) = &LAB_00240f24; // Next page callback

    return param_1;
}
```

### 3.2 Blacklist Manager Constructor (`FUN_002513d4`)
Extracted from `aLogin_decompiled.c` lines 246590–246700:

```c
// Constructor for form_modulePlus (350 x 228 px)
int * FUN_002513d4(int *param_1, char param_2) {
    int iVar1;
    int *piVar2;

    FUN_0048243c(param_1, 0);
    (**(code **)(*param_1 + 8))(param_1, "form_modulePlus", 510, 206, 200, 0, 0, 1, 350, 228, 0);

    param_1[0x4b] = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "icon_block");
    param_1[0x5d] = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "icon_BlockMsg");

    // Scrollable container panel & vertical scrollbar bar_H3
    piVar2 = (int *)FUN_00484934(PTR_PTR_0047f408, 1, param_1);
    param_1[0x5a] = (int)piVar2;
    (**(code **)(*piVar2 + 8))(piVar2, "panel10", 16, 50, 50, 0, 0, 1, 208, 181, 63);

    piVar2 = (int *)FUN_0048d00c(PTR_PTR_0047ed9c, 1, param_1);
    param_1[0x5b] = (int)piVar2;
    FUN_0048dc00(piVar2, "bar_H3", 0, 130, 12, 0); // Scrollbar rail configuration

    // 10 Unblock buttons (vertical stride 20 px)
    iVar1 = 0;
    do {
        piVar2 = (int *)FUN_00485788(PTR_PTR_0047f5f0, 1, param_1[0x5c]);
        param_1[iVar1 + 0x4f] = (int)piVar2;
        (**(code **)(*piVar2 + 8))(piVar2, "btn_unBlock", 150, 20, 44, 0, 0, 1, 20, 44, iVar1 * 20 - 2);
        piVar2[0x2c] = iVar1; // Row slot index
        piVar2[0x1c] = (int)&LAB_002518ec; // Unblock callback
        iVar1 = iVar1 + 1;
    } while (iVar1 != 10);

    // Text box for entering new target nameplate
    piVar2 = (int *)FUN_004828a0(PTR_PTR_0047f6d4, 1, param_1);
    param_1[0x59] = (int)piVar2;
    (**(code **)(*piVar2 + 8))(piVar2, "editorBG", 20, 20, 50, 0, 0, 1, 22, 140, 200);

    // Block submission button
    piVar2 = (int *)FUN_00485788(PTR_PTR_0047f5f0, 1, param_1);
    param_1[0x4e] = (int)piVar2;
    (**(code **)(*piVar2 + 8))(piVar2, "btn_Block", 170, 25, 44, 0, 0, 1, 25, 44, 200);
    piVar2[0x14] = (int)&LAB_00251978; // Add to blocklist callback

    return param_1;
}
```

---

## 4. Network Wire Protocol: Action Code 14 (`0x0E`)

Social privacy and blacklist additions communicate under **Action Code 14** (`0x0E`):

### 4.1 Client Request: Add Player to Blacklist (`0x0E:0x04`)

```
Offset 0x00: [0x0E]            (Action Code 14: Friend & Social Presence)
Offset 0x01: [0x04]            (Subcode: Add to Blacklist)
Offset 0x02: [NameLength: uint8] (Byte length of character nameplate string)
Offset 0x03: [Name: char[N]]   (Target character nameplate to suppress)
```

### 4.2 Client Request: Remove from Blacklist (`0x0E:0x05`)

```
Offset 0x00: [0x0E]            (Action Code 14)
Offset 0x01: [0x05]            (Subcode: Remove from Blacklist)
Offset 0x02: [SlotIndex: uint8] (Target slot 0..9)
Offset 0x03: [CharId: uint32]  (32-bit Character GUID to unblock)
```

### 4.3 Server Response: Blacklist Synchronization (`0x0E:0x04`)

```
Offset 0x00: [0x0E]            (Action Code 14)
Offset 0x01: [0x04]            (Subcode: Blacklist Sync)
Offset 0x02: [Status: uint8]   (0x00: Success, 0x01: Player Not Found, 0x02: Roster Full)
Offset 0x03: [SlotIndex: uint8] (Assigned blacklist slot 0..9)
Offset 0x04: [CharId: uint32]  (32-bit Character GUID)
Offset 0x08: [NameLength: uint8]
Offset 0x09: [Name: char[N]]
```

---

## 5. C# Server Implementation Blueprint

```csharp
namespace Wonderland.Network.Handlers
{
    public static class BlacklistHandler
    {
        private const byte ACTION_CODE_SOCIAL = 0x0E;
        private const int MAX_BLACKLIST_ENTRIES = 10;

        public static void Register(PacketDispatcher dispatcher)
        {
            dispatcher.RegisterHandler(ACTION_CODE_SOCIAL, HandleSocialRequest);
        }

        public static void HandleSocialRequest(GameSession session, ReadOnlySpan<byte> packet)
        {
            if (packet.Length < 2) return;
            byte subcode = packet[1];

            switch (subcode)
            {
                case 0x04: // Add to Blacklist
                    HandleAddToBlacklist(session, packet[2..]);
                    break;
                case 0x05: // Remove from Blacklist
                    HandleRemoveFromBlacklist(session, packet[2..]);
                    break;
            }
        }

        private static void HandleAddToBlacklist(GameSession session, ReadOnlySpan<byte> data)
        {
            if (data.Length < 2) return;
            byte nameLen = data[0];
            if (data.Length < 1 + nameLen) return;
            string targetName = Encoding.UTF8.GetString(data.Slice(1, nameLen));

            var character = session.Character;
            if (character.Blacklist.Count >= MAX_BLACKLIST_ENTRIES)
            {
                SendBlacklistResponse(session, 0x02, 0, 0, string.Empty); // Full
                return;
            }

            var target = WorldManager.FindCharacterByName(targetName);
            if (target == null)
            {
                SendBlacklistResponse(session, 0x01, 0, 0, string.Empty); // Not found
                return;
            }

            if (!character.Blacklist.Contains(target.Id))
            {
                character.Blacklist.Add(target.Id);
                byte slot = (byte)(character.Blacklist.Count - 1);
                SendBlacklistResponse(session, 0x00, slot, target.Id, target.Name);
            }
        }

        private static void HandleRemoveFromBlacklist(GameSession session, ReadOnlySpan<byte> data)
        {
            if (data.Length < 5) return;
            byte slotIndex = data[0];
            uint targetId = BinaryPrimitives.ReadUInt32LittleEndian(data[1..5]);

            session.Character.Blacklist.Remove(targetId);
            session.SendSystemNotice("Player removed from block list.");
        }

        private static void SendBlacklistResponse(GameSession session, byte status, byte slot, uint targetId, string name)
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(name);
            byte[] response = new byte[9 + nameBytes.Length];
            response[0] = ACTION_CODE_SOCIAL;
            response[1] = 0x04;
            response[2] = status;
            response[3] = slot;
            BinaryPrimitives.WriteUInt32LittleEndian(response.AsSpan(4..8), targetId);
            response[8] = (byte)nameBytes.Length;
            nameBytes.CopyTo(response.AsSpan(9));
            session.Send(response);
        }
    }
}
```
