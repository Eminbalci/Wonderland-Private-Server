# Document 74: Guild Code of Conduct, Bylaws, and Organization Rule Editor

## 1. Executive Summary & Engine Architecture

In Wonderland Online, guild organizations (Army Groups) maintain internal governance through customizable bylaws and codes of conduct edited via `Form_EditRule_1`. Dissected from client subroutines at RVA `0x00314400` (constructor and memo container setup, vertical scrollbar integration `FUN_0048dc00`, and save callback `LAB_003149f0`), this engine provides a multi-line formatted text editor for composing, editing, and publishing guild regulations.

The dialog window `Form_EditRule_1` ($415 \times 347$ px) operates within the Army Group authority hierarchy. Access is restricted through the 5-tier permission bitmask (`Authority Bit 0x04: CAN_EDIT_NOTICE_AND_RULES`). Text entered into the memo area is checked against profanity blacklists, formatted with automatic word wrapping, and committed to the guild database via Action Code `0x1F` (`31` decimal) Subcode `0x07`, immediately replicating to the guild bulletin board (`Form_OrgNotice`) across all connected legion members.

```
+------------------------------------------------------------------------------------+
|                         GUILD RULE EDITOR ENGINE ARCHITECTURE                      |
+------------------------------------------------------------------------------------+
                                      |
                                      v
                      [Form_EditRule_1 Dialog (415x347 px)]
                 Multi-Line Memo Box (310x270 px, 200 Char Cap)
                                      |
         +----------------------------+----------------------------+
         | Text Layout & Scroller                                  | Authority & Sanitization Gate
         v                                                         v
  [Input Container: param_1[0x4E]]                          [Permission Validator]
  - Software Blinking Caret: inputCursor                    - Authority Bitmask: Bit 0x04 Required
  - Vertical Scrollbar: bar_H4 + Arrows                     - Filter: "Don't use Illegal words"
  - Line Stride: 16 Pixels per Row                          - Max Buffer: 200 Characters
         |                                                         |
         +----------------------------+----------------------------+
                                      |
                                      v
                 [Network Dispatcher: Action Code 0x1F (31)]
           Subcode 0x07: Commit Guild Rules / Bylaws Broadcast
```

---

## 2. Memory Layouts & Data Structures

### 2.1 Guild Bylaws Record (`0xD0` = 208 Bytes)
Within the Army Group memory structure at `PTR_DAT_004c9620`, the active bylaws are maintained as a 208-byte structure:

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `uint` | `GuildId` | Master Army Group organization identifier |
| `+0x04` | `uint` | `EditorCharacterId`| Character GUID of the leader or officer who authored the rule |
| `+0x08` | `char[16]` | `EditorName` | Nameplate string of the authoring officer |
| `+0x18` | `uint` | `LastUpdated` | Unix timestamp of the most recent rule revision |
| `+0x1C` | `ushort` | `TextLength` | Byte length of the bylaws string |
| `+0x1E` | `char[186]`| `RuleText` | Null-terminated bylaws and conduct text (max 200 characters) |

### 2.2 `Form_EditRule_1` Instance Layout (`param_1`)

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `void*` | `VTable` | Delphi component virtual method table pointer |
| `+0x04` | `uint` | `FormHandle` | DirectDraw surface bitmap handle |
| `+0x4C` | `int*` | `pMemoContainer`| Parent bounding panel for the rule editor |
| `+0x4E` | `int*` | `pTextMemo` | Interactive multi-line text editor control |
| `+0x4F` | `uint` | `BtnOk` | Commit and save rules button (`"btn_ok_2"`) |
| `+0x50` | `uint` | `BtnCancel` | Dismiss editor button (`"Btn_Cancel_3"`) |
| `+0x51` | `int*` | `pScrollBar` | Vertical scrollbar slider control (`"bar_H4"`) |
| `+0x54` | `uint` | `BtnCloseS1` | Top-right small close button (`"Btn_Close_s_1"`) |
| `+0x55` | `uint` | `BtnArrowUp` | Scroll up arrow control (`"Btn_ArrowUp_1"`) |
| `+0x56` | `uint` | `BtnArrowDown` | Scroll down arrow control (`"Btn_ArrowDn_1"`) |

---

## 3. UI Form Geometry, Coordinates & Controls

### 3.1 `Form_EditRule_1` Layout Metrics
- **Dimensions:** Width `0x19F` (415 pixels), Height `0x15B` (347 pixels).
- **Positioning:** Centered at $(X = \text{0x32}, Y = \text{0x1FE})$.
- **Memo Editor Area (`param_1[0x4E]`):**
  - Horizontal anchor: $X = \text{0x19}$ (25 px), $Y = \text{0x3B}$ (59 px).
  - Width: `0x136` (310 pixels), Height: `0x10E` (270 pixels).
  - Blinking input caret: `"inputCursor"`.
  - Buffer limit: 200 characters (`piVar2[0x39] = 200`).
- **Scrollbar (`param_1[0x51]`):**
  - Track anchor: $X = \text{0x12D}$ (301 px), $Y = \text{0x50}$ (80 px).
  - Slider track: `"bar_H4"`, height `0x109` (265 px), width `0xF` (15 px).
  - Arrow buttons:
    - `"Btn_ArrowUp_1"`: $X = 300$ px, $Y = \text{0x3B}$ (59 px), size $17 \times 17$ px.
    - `"Btn_ArrowDn_1"`: $X = 300$ px, $Y = \text{0x15E}$ (350 px), size $17 \times 17$ px.
- **Action Buttons:**
  - `"btn_ok_2"`: $X = \text{0x7A}$ (122 px), $Y = \text{0x17A}$ (378 px), size $20 \times 39$ px. (Callback: `LAB_003149f0`)
  - `"Btn_Cancel_3"`: $X = \text{0xB9}$ (185 px), $Y = \text{0x17A}$ (378 px), size $20 \times 39$ px.
  - `"Btn_Close_s_1"`: $X = \text{0x129}$ (297 px), $Y = \text{0x18}$ (24 px), size $18 \times 18$ px.

```
+-----------------------------------------------------------------+
| Form_EditRule_1: Guild Bylaws Editor (415 x 347 px)             |
+-----------------------------------------------------------------+
| [Header Title: Edit Army Group Rules]                       [X] |
+-----------------------------------------------------------------+
| +-----------------------------------------------+ +-----------+ |
| | 1. Respect all guild members during combat.   | | [^] Up    | |
| | 2. Mandatory attendance for Saturday GvG wars | |           | |
| | 3. Keep guild vault donations active.         | | [=] bar   | |
| | 4. No unauthorized PK in capital cities.      | |     H4    | |
| | _inputCursor                                  | |           | |
| |                                               | | [v] Down  | |
| +-----------------------------------------------+ +-----------+ |
+-----------------------------------------------------------------+
|              [ btn_ok_2: Save Rules ]   [ Btn_Cancel_3 ]        |
+-----------------------------------------------------------------+
```

---

## 4. Network Protocol, Wire Frames & Action Codes

### 4.1 Packet Framing
Guild rule revisions are submitted using Action Code `0x1F` (`31` decimal), Subcode `0x07`:
- **Client Request (C2S):** `[0x1F, 0x07, TextLength: ushort, RuleText: char[TextLength]]`
- **Server Broadcast (S2C):** `[0x1F, 0x07, GuildId: uint, EditorName: string, RuleText: string]`

### 4.2 C# Packet Structure Definitions

```csharp
namespace Wonderland.Network.Packets.Guild
{
    /// <summary>
    /// Client-to-server request to commit revised Army Group bylaws.
    /// Wire Header: Action Code 0x1F, Subcode 0x07.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct C2S_UpdateGuildRulesPacket
    {
        public byte ActionCode;       // 0x1F (31)
        public byte SubCode;          // 0x07
        public ushort TextLength;     // Length of rules text (max 200)
        // Trailing: RuleText string
    }

    /// <summary>
    /// Server-to-client broadcast distributing updated guild rules to online members.
    /// Wire Header: Action Code 0x1F, Subcode 0x07.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct S2C_GuildRulesSyncPacket
    {
        public byte ActionCode;       // 0x1F (31)
        public byte SubCode;          // 0x07
        public uint GuildId;          // Target Army Group ID
        public uint EditorId;         // Authoring officer GUID
        // Trailing: EditorName\0, RuleText\0
    }
}
```

---

## 5. Authority Bitmask & Text Sanitization Pipeline

1. **Authority Gate (`Form_setauthority_1`):**
   - A player character must hold authority bitmask `0x04` (`CAN_EDIT_NOTICE_AND_RULES`). If unprivileged, opening the editor is blocked:
     `"Not the owner, can't set sale"` / `"Forbidden:"`
2. **Profanity Filter:**
   - Client verifies characters against illegal character patterns:
     `"Don't use Illegal words"`
3. **Character Budget Enforcement:**
   - Text buffer is strictly clamped at 200 characters. Trailing characters beyond 200 are truncated before network dispatch.

---

## 6. Private Server Implementation Guide & C# Production Code

The following service manages guild bylaws persistence, officer authority validation, and live broadcast synchronization.

```csharp
using System;
using System.Collections.Concurrent;
using Wonderland.Network.Packets.Guild;

namespace Wonderland.Server.Services.Guild
{
    public interface IGuildRuleService
    {
        bool UpdateRules(PlayerCharacter officer, string ruleText, out string error);
        string GetRules(uint guildId);
    }

    public sealed class GuildRuleService : IGuildRuleService
    {
        private const byte AuthorityBitNotice = 0x04; // Permission bit to edit rules/notices
        private const int MaxRuleLength = 200;

        private readonly ConcurrentDictionary<uint, string> _guildRules = new();

        public bool UpdateRules(PlayerCharacter officer, string ruleText, out string error)
        {
            error = string.Empty;

            if (officer == null) throw new ArgumentNullException(nameof(officer));

            if (officer.GuildId == 0)
            {
                error = "Not in an Army Group";
                return false;
            }

            // 1. Authority Verification
            if ((officer.GuildAuthority & AuthorityBitNotice) == 0 && !officer.IsGuildLeader)
            {
                error = "Insufficient Army Group authority";
                return false;
            }

            // 2. Length Sanitization
            if (string.IsNullOrEmpty(ruleText))
            {
                ruleText = string.Empty;
            }
            else if (ruleText.Length > MaxRuleLength)
            {
                ruleText = ruleText.Substring(0, MaxRuleLength);
            }

            // 3. Persist
            _guildRules[officer.GuildId] = ruleText;

            // 4. Broadcast to all online guild members
            var syncPacket = new S2C_GuildRulesSyncPacket
            {
                ActionCode = 0x1F,
                SubCode = 0x07,
                GuildId = officer.GuildId,
                EditorId = officer.CharacterId
            };

            officer.CurrentGuild?.Broadcast(syncPacket, officer.Name, ruleText);
            return true;
        }

        public string GetRules(uint guildId)
        {
            return _guildRules.TryGetValue(guildId, out var rules) ? rules : string.Empty;
        }
    }
}
```
