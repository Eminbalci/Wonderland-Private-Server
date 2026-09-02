# Eve.emg Binary String Encoding & Localization Architecture

## 1. Overview
The Wonderland Online server and client use `Eve.emg` binary database files to store map entities, NPC locations, entry/exit portals, mining nodes, chest rewards, and interactive event scripts.

Historically, the Chinese/Taiwanese game developers (Chinesegamer) authored internal entity notes and script titles in **Traditional Chinese (Big5 / CP950)** within fixed 20-byte string fields.

---

## 2. Root Cause of Random Characters & Mojibake

### A. Raw Character Casting (`(char)d[ptr]`)
Previously, `EveLoader.cs` iterated 20 bytes and appended `(char)d[ptr]`.
- Big5 is a multi-byte encoding where Chinese characters consist of two consecutive bytes (lead byte `0x81-0xFE` + trail byte `0x40-0xFE`).
- Casting each byte individually to `(char)` treated multi-byte sequences as individual Windows-1252 / ANSI characters.
- Example:
  - Raw Bytes: `B7 73 A4 E2 AB FC AB 6E BB 50 A6 D1 BE 7C A5 5B A4 4A`
  - ANSI Interpretation: `' ·s¤â«ü«n»P¦Ñ¾|¥[¤J '`
  - Big5 Interpretation (CP950): **`新手指南與老船長加入`** (*Beginner's Guide & Old Captain Joins*).

### B. Fixed-Size Null Byte Termination (`\0`)
Unused bytes in the 20-byte string buffer are padded with `0x00`.
- Appending `(char)0` embedded null characters into C# strings (`eventEntry.Name`).
- When passed to Windows WinForms controls (`RichTextBox`, `TextBox`), the underlying Win32 API (`SetWindowTextW`) treated the first `\0` as a string terminator, truncating the rest of the formatted event script branches, dialogues, and opcode trees.

---

## 3. Implementation Solution

### A. Centralized Big5/UTF-8 Decoder (`EveLoader.DecodeEveString`)
Located in [`wlo.pserver.core/DataFiles/EveLoader.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataFiles/EveLoader.cs):
```csharp
public static string DecodeEveString(byte[] buffer, int offset, int maxLength)
{
    if (buffer == null || offset < 0 || offset >= buffer.Length || maxLength <= 0) return "";
    int available = Math.Min(maxLength, buffer.Length - offset);
    if (available <= 0) return "";

    // Find actual length before first trailing null byte
    int len = 0;
    while (len < available && buffer[offset + len] != 0)
    {
        len++;
    }
    if (len == 0) return "";

    try
    {
        string decoded = Big5Encoding.GetString(buffer, offset, len).Trim('\0', ' ', '\r', '\n');
        if (!string.IsNullOrEmpty(decoded))
            return decoded;
    }
    catch { }

    try
    {
        return Encoding.UTF8.GetString(buffer, offset, len).Trim('\0', ' ', '\r', '\n');
    }
    catch
    {
        return Encoding.Default.GetString(buffer, offset, len).Trim('\0', ' ', '\r', '\n');
    }
}
```

### B. Event Entity Loaders Updated
All entity parsers in `EveLoader.cs` now use `DecodeEveString`:
1. `LoadNpcEntries` (NPC entities)
2. `LoadEntryEntries` (Portals / exits)
3. `LoadMiningEntries` (Gathering / mining points)
4. `LoadItemEntries` (Ground drops / static items)
5. `LoadEventEntries` (Interactive script entries)
6. `LoadGroupEntries` (NPC spawn groups)
7. `LoadWarpEntries` (Warp zones)
8. `LoadInteractiveEntries` (Switches / props)
9. `LoadBattleEntries` (Battle encounters)
10. `LoadpreEventEntries` (Pre-events)
11. `LoadgroupExtEntries` (Extended groups)

### C. GUI Localization & English Alias Mapping
Located in [`Src/Gui/MainForm1.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.cs):
- Decoded Chinese names are mapped to user-friendly English descriptions.
- Event visualizer now renders:
  ```text
  📜 Event Entry #8: '新手指南與老船長加入' (Beginner's Guide & Old Captain Joins) | Total Branches: 3
  ```
