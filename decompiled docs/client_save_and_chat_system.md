# Client Save & Chat System Decompiled Specifications

This document outlines the client-side profile configuration reader (`save.dat`), chat log channel classifications, interface text color settings, and message delivery item constraints extracted from `aLogin.exe.1.c`.

---

## 1. Client Save File (`user\save.dat`)

The client saves local player configurations to a file named `save.dat` located under the `user\` subdirectory.

### A. Load & Save Routines
- **Path**: `user\save.dat`
- **File Encryption**: Obfuscated using client-side XOR encryption keys (**`121`** / `0x79`).
- **Data Saved**:
  - Hotkey mapping arrays.
  - Screen dimensions and graphics options.
  - System toggles (sound effects, BGM volumes, background loops).
  - Macros, trade auto-declines, and friend settings.

---

## 2. Chat Log Colors Configuration

The client parses custom text color configurations for different chat channels, saving them inside localized key-value structures:

| Settings Key | Config String | Represented Chat Channel |
|---|---|---|
| `ChannelColor1` | `ChannelColor1=` | World / System Chat Logs |
| `ChannelColor2` | `ChannelColor2=` | Local Area Chat Logs |
| `ChannelColor3` | `ChannelColor3=` | Whisper / Private Chat Logs |
| `ChannelColor4` | `ChannelColor4=` | Party / Team Chat Logs |
| `ChannelColor5` | `ChannelColor5=` | Guild Chat Logs |
| `MsgChannel` | `MsgChannel=` | System Dialog Message Prompts |

---

## 3. Chat Channels Delivery & Constraints

The client performs checks on message routing channels before dispatching packet packets to the server:

### A. Muted Channel Alerts
If a player attempts to send or receive messages on a channel that they have toggled off in their system options, the client halts execution and logs:
- `"World channel is Off"`
- `"Local channel is Off"`
- `"Whisper channel is Off"`
- `"Team channel is Off"`

### B. Global Shouting Item Requirement (Radio Set)
To prevent global chat spam, sending a shout packet to the World channel checks for an inventory item constraint:
- **Alert Log**: `"(System):World Channel requires Radio Set"`.
- **Logic**: The client checks if the player holds the **Radio Set** item in their inventory slots. If not found, blocks packet dispatch and displays the error.

### C. Server Channel Alignment
For interaction packets (like team invitations or trades), the client confirms both participants reside on the same server channel:
- **Validation Alert**: `"Both must be on same server channel"`.
