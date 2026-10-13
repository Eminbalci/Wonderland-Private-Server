# Document 99: Guild Headquarters Notebook, Member Roster, 10x10 Pixel Emblem Canvas, and Notice Dispatcher

## 1. Architectural Overview

In *Wonderland Online*, guild organizations (known canonically as **Legions** or **Army Groups**) provide communal frameworks for cooperative combat, guild wars, base housing, and social hierarchy. The client organizes all legion management into a unified 4-tab notebook interface in `se_ArmyGroup.pas` and `se_EnsignType.pas`.

The four tabs multiplex distinct governance responsibilities:
1. **Tab 1: Legion Overview (`Form_ArmyGrounpInfo_1`)**: Displays organization telemetry (Legion Name, Level, Guildmaster Chief, Member Count, Accumulated Treasury Funds, Territorial Prestige).
2. **Tab 2: Member Roster & Promotion (`Form_ArmyGrounp_1`)**: Interactive member roster tracking online status, levels, rebirth classes, join dates, and authority promotions.
3. **Tab 3: Custom Guild Emblem Canvas (`Form_Emblem_1`)**: A built-in $10 \times 10$ pixel raster canvas editor (`FUN_002888d8`) with an 8-color palette, allowing guildmasters to design custom legion flags displayed above member heads.
4. **Tab 4: Legion Notice Board (`Form_OrgNotice`)**: Rich text daily announcement bulletin (`FUN_00292994`) pushed to all guild members upon world connection.

```
+-----------------------------------------------------------------------------------+
|                        LEGION GUILD HEADQUARTERS ARCHITECTURE                     |
+-----------------------------------------------------------------------------------+
                                          |
                +-------------------------+-------------------------+
                |                                                   |
                v                                                   v
    [4-Tab Notebook: se_ArmyGroup.pas]                    [10x10 Pixel Emblem Engine]
    - Tab 1: Form_ArmyGrounpInfo_1                        - Form_Emblem_1 (FUN_002888d8)
    - Tab 2: Form_ArmyGrounp_1                            - 8 Canonical 16-Bit Colors
    - Tab 4: Form_OrgNotice                               - 100-Pixel In-Memory Bitmap
                |                                                   |
                +----------------- Network Dispatch ----------------+
                                          |
                                          v
                         [Action Code 31:3 Emblem Sync Packet]
```

---

## 2. VCL Form Hierarchy & UI Structure

The notebook tab switcher is orchestrated in `se_ArmyGroup.pas` (dispatcher at RVA `0x0028815c`):

### 2.1 Form Specifications

| Attribute | Specification |
| :--- | :--- |
| **Pascal Source Unit** | `se_ArmyGroup.pas` / `se_EnsignType.pas` |
| **Tab Identifiers** | `Form_ArmyGrounpInfo_1` (Info), `Form_ArmyGrounp_1` (Roster), `Form_Emblem_1` (Flag), `Form_OrgNotice` (Notice) |
| **Dimensions** | Width: 460 px, Height: 380 px |
| **Subroutine Addresses** | Tab Dispatcher: `FUN_0028815c`, Emblem Painter: `FUN_002888d8`, Notice Engine: `FUN_00292994` |
| **Emblem Matrix** | $10 \times 10$ Pixels (100 discrete colored blocks) |

### 2.2 Tab Switching Logic (`FUN_0028815c`)

```c
// Decompiled from aLogin.exe: Lines 263803 - 263818
if (*(int *)(param_1 + 0xfc) == 1) {
    // Tab 1: Information
    *(undefined4 *)(param_1 + 0x4c) = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "Form_ArmyGrounpInfo_1");
}
if (*(int *)(param_1 + 0xfc) == 2) {
    // Tab 2: Roster List
    *(undefined4 *)(param_1 + 0x4c) = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "Form_ArmyGrounp_1");
}
if (*(int *)(param_1 + 0xfc) == 3) {
    // Tab 3: Custom Emblem Painter
    *(undefined4 *)(param_1 + 0x4c) = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "Form_Emblem_1");
}
if (*(int *)(param_1 + 0xfc) == 4) {
    // Tab 4: Notice Board
    *(undefined4 *)(param_1 + 0x4c) = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "Form_OrgNotice");
    FUN_00292994(param_1); // Load daily announcement memo
}
```

---

## 3. 10x10 Pixel Emblem Canvas Engine (`FUN_002888d8`)

The emblem painter provides a $10 \times 10$ interactive drawing grid where guild leaders click individual blocks to paint them with one of 8 canonical indexed colors:

```c
// Decompiled from aLogin.exe: FUN_002888d8 @ 0x002888d8
void FUN_002888d8(int param_1, int *param_2, undefined4 *param_3)
{
    // 10x10 Grid Coordinate Calculation
    int gridX = (mouseX - CanvasLeft) / 12; // 12 px stride per block
    int gridY = (mouseY - CanvasTop)  / 12;

    if (gridX >= 0 && gridX < 10 && gridY >= 0 && gridY < 10) {
        int pixelIndex = gridY * 10 + gridX; // Index 0..99
        byte selectedColor = GetActivePaletteColor(); // 0..7
        
        // Update in-memory 100-byte emblem buffer
        *(byte *)(param_1 + 0x240 + pixelIndex) = selectedColor;
        
        // Render updated block to DirectDraw backbuffer surface
        RenderEmblemBlock(gridX, gridY, selectedColor);
    }
}
```

### 3.1 8-Color Emblem Palette

| Color Index | Color Name | 16-Bit RGB 565 Value | 24-Bit RGB Hex |
| :---: | :--- | :---: | :---: |
| `0` | Transparent / White | `0xFFFF` | `#FFFFFF` |
| `1` | Solid Black | `0x0000` | `#000000` |
| `2` | Crimson Red | `0xF800` | `#FF0000` |
| `3` | Forest Green | `0x07E0` | `#00FF00` |
| `4` | Royal Blue | `0x001F` | `#0000FF` |
| `5` | Golden Yellow | `0xFFE0` | `#FFFF00` |
| `6` | Deep Purple | `0x780F` | `#800080` |
| `7` | Cyan / Sky Blue | `0x07FF` | `#00FFFF` |

---

## 4. Legion Authority Ranks & Permissions

Legion members hold one of five operational rank tiers:

| Rank Tier | Rank Title | Permissions Bitmask | Privileges |
| :---: | :--- | :---: | :--- |
| `0` | **Chief (Leader)** | `0x1F` (All) | Full governance, transfer leadership, disband, edit emblem, withdraw funds |
| `1` | **Vice Chief** | `0x0F` | Invite members, kick members, edit notice, accept war challenges |
| `2` | **Captain** | `0x07` | Invite members, lead guild squads in territory trials |
| `3` | **Veteran** | `0x01` | Speak in guild channel, access communal vault deposits |
| `4` | **Recruit** | `0x00` | Standard membership, basic guild chat |

---

## 5. Network Wire Protocol

### 5.1 Client -> Server: Action Code 31:3 Upload Guild Emblem

```
+---------------+---------------+---------------+---------------+---------------+
| Magic Header  | Length (LE)   | Action Code   | Sub-Opcode    | Guild ID (LE) |
| 0xF4 0x44     | 0x67 0x00     | 0x1F          | 0x03          | 0x54 0x01     |
+---------------+---------------+---------------+---------------+---------------+
| 100 Bytes Raw Pixel Palette Array (10x10 grid, 1 byte per pixel: 0..7)        |
| [P0, P1, P2 ... P99]                                                          |
+-------------------------------------------------------------------------------+
```

### 5.2 Server -> Client: Action Code 31:4 Broadcast Guild Emblem Update

```
+---------------+---------------+---------------+---------------+---------------+
| Magic Header  | Length (LE)   | Action Code   | Sub-Opcode    | Guild ID (LE) |
| 0xF4 0x44     | 0x67 0x00     | 0x1F          | 0x04          | 0x54 0x01     |
+---------------+---------------+---------------+---------------+---------------+
| 100 Bytes Raw Pixel Palette Array                                             |
+-------------------------------------------------------------------------------+
```

---

## 6. C# Server Implementation Blueprint

```csharp
// GuildOrganizationService.cs - Wonderland Private Server Legion Engine
using System;
using System.Collections.Concurrent;

namespace Wonderland.Server.Guild
{
    public sealed class GuildEntity
    {
        public ushort GuildId { get; set; }
        public string Name { get; set; } = string.Empty;
        public uint LeaderCharacterId { get; set; }
        public byte Level { get; set; } = 1;
        public ulong TreasuryGold { get; set; }
        public byte[] EmblemPixels { get; set; } = new byte[100]; // 10x10 grid
        public string DailyNotice { get; set; } = string.Empty;
    }

    public sealed class GuildOrganizationService
    {
        private readonly ConcurrentDictionary<ushort, GuildEntity> _guilds = new();

        public bool TryUpdateEmblem(
            PlayerCharacter player,
            byte[] emblemData,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            if (emblemData == null || emblemData.Length != 100)
            {
                errorMessage = "Invalid emblem payload dimension.";
                return false;
            }

            if (!_guilds.TryGetValue(player.GuildId, out var guild))
            {
                errorMessage = "Guild does not exist.";
                return false;
            }

            // Only Leader (Chief) can change emblem
            if (guild.LeaderCharacterId != player.CharacterId)
            {
                errorMessage = "Only the Guild Chief may modify the emblem.";
                return false;
            }

            Buffer.BlockCopy(emblemData, 0, guild.EmblemPixels, 0, 100);

            // Broadcast updated emblem to all online guild members (Action Code 31:4)
            byte[] packet = new byte[4 + 1 + 1 + 2 + 100];
            packet[0] = 0xF4; packet[1] = 0x44; // Magic
            packet[2] = 0x68; packet[3] = 0x00; // Length: 104 bytes
            packet[4] = 0x1F; packet[5] = 0x04; // Opcode: Broadcast Emblem
            packet[6] = (byte)(guild.GuildId & 0xFF);
            packet[7] = (byte)((guild.GuildId >> 8) & 0xFF);
            Buffer.BlockCopy(guild.EmblemPixels, 0, packet, 8, 100);

            BroadcastToGuild(guild.GuildId, packet);
            return true;
        }

        private void BroadcastToGuild(ushort guildId, byte[] packet) { /* Broadcast */ }
    }
}
```

---

## 7. Verification & Cross-References

- **Disassembly Offsets**: `aLogin.exe` RVA `0x0028815c` (Tab Switcher), `0x002888d8` (Emblem Canvas Painter), `0x00292994` (Notice Loader).
- **String References**: Line 263804 (`"Form_ArmyGrounpInfo_1"`), Line 263808 (`"Form_ArmyGrounp_1"`), Line 263812 (`"Form_Emblem_1"`), Line 263816 (`"Form_OrgNotice"`).
- **Related Specs**:
  - [`31_guild_organization_hierarchy_and_emblem_system.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/31_guild_organization_hierarchy_and_emblem_system.md)
  - [`52_army_group_guild_organization_and_authority_hierarchy.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/52_army_group_guild_organization_and_authority_hierarchy.md)
  - [`57_guild_bylaws_rule_editor_hierarchy_permissions_and_notice_bulletin.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/57_guild_bylaws_rule_editor_hierarchy_permissions_and_notice_bulletin.md)
  - [`76_church_charity_tithe_and_legion_resource_donation_box.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/76_church_charity_tithe_and_legion_resource_donation_box.md)
