# Client Save Configuration and Chat Channel Constraints Specification

## 1. Client Configuration Save File (`user\save.dat`)

The client persists user preferences, graphic configurations, audio volumes, and hotkeys to `user\save.dat`:

* **File Location:** `user\save.dat`
* **File Encryption:** Single-byte XOR obfuscation with key **`121`** (`0x79` hex).
* **Persisted Data:**
  * Action bar hotkey bindings (Horizontal, Vertical, Mini).
  * Screen resolution mode and fullscreen toggle.
  * Audio volumes: BGM volume, SFX volume, audio mute flags.
  * Auto-decline toggles for player trades, team invitations, and duels.
  * Chat filter preferences.

---

## 2. Chat Log Colors Configuration

The client parses custom RGB color settings for chat channels from configuration lines:

| Setting Key | Configuration Pattern | Destination Chat Window / Channel |
|---|---|---|
| `ChannelColor1` | `ChannelColor1=` | World / System announcements |
| `ChannelColor2` | `ChannelColor2=` | Local area proximity chat |
| `ChannelColor3` | `ChannelColor3=` | Private whispers |
| `ChannelColor4` | `ChannelColor4=` | Party / Team communications |
| `ChannelColor5` | `ChannelColor5=` | Organization / Guild communications |
| `MsgChannel` | `MsgChannel=` | Modal dialog message prompts |

---

## 3. Chat Transmission Constraints & Validations

Before transmitting chat action codes to the server, the client enforces strict validation checks:

### 3.1 Channel Toggle Checks
If a user tries to broadcast across a channel disabled in their system settings, the client suppresses transmission:
* `"World channel is Off"`
* `"Local channel is Off"`
* `"Whisper channel is Off"`
* `"Team channel is Off"`

### 3.2 Global Shouting Item Requirement (Radio Set)
To prevent server-wide spam, broadcasting to the World channel checks whether the character holds the **Radio Set** item in their inventory:
* **Validation Error:** `"(System):World Channel requires Radio Set"`.
* **Action:** Transmission is blocked unless a Radio Set is present in the player's 50-slot bag.

### 3.3 Channel Cluster Alignment
For social interactions (team invitations, player trade, and friend invites), participants must be connected to the exact same server subchannel:
* **Validation Error:** `"Both must be on same server channel"`.
