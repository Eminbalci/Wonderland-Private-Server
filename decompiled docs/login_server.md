# Login Server & Authentication Decompiled Specifications

This document outlines the client-side authentication handling, server registry parser (`SERVER.INI`), and sub-channel lists extracted from `alogin_analyzed/login_server.c`.

---

## 1. Memory Offsets & Session Registries

During authentication and channel parsing, the client uses the following structures and registers:

| Offset / Variable | Data Type | Purpose | Operations & Behaviors |
|---|---|---|---|
| `DAT_0071ef58` | `pointer` | Player Session Pointer | Stores active session GUIDs and socket configurations |
| `+0x264` | `int` | Login Socket ID | Active socket descriptor for authentication |
| `+0x268` | `int` | Player Session GUID 1 | Unique session token received on auth success |
| `+0x26c` | `int` | Player Session GUID 2 | Secondary session token received on auth success |
| `+0x270` | `byte` | Character Select Flag | Directs client which character slot index (0-1) is active |
| `+0x3c22` | `char` | Login State Status | `2` for logged-in, `0` for failed, `1` for pending |

---

## 2. Key Decompiled Functions

### `FUN_0033c310` (Login Response Handler)
- **C Signature**: `void FUN_0033c310(int *param_1, int param_2)`
- **Logic**:
  1. Reads the authentication result byte from the incoming packet data `cVar1 = *(char *)(param_2 + 1)`.
  2. **If `cVar1 == '\x01'` (Success)**:
     - Sets the login state flag at `player_struct + 0x3c22` to `2`.
     - Extracts primary session GUID (at packet offset `3`, length `4`) and writes it to `DAT_0071ef58 + 0x268`.
     - Extracts secondary session GUID (at packet offset `7`, length `4`) and writes it to `DAT_0071ef58 + 0x26c`.
     - Sets the character slot selection byte at `DAT_0071ef58 + 0x270` to the value at packet offset `10`.
     - Calls internal gui triggers `FUN_0033d100` and completes the login transitions.
  3. **If `cVar1 == '\x02'` (Login/Password Error)**:
     - Resets state flag `+0x3c22` to `0`.
     - Triggers system dialog popup: `"Login/Pwd error"`.
  4. **If `cVar1 == '\x03'` / `'\x04'` (Blocked Accounts/Security Alert)**:
     - Resets state flag `+0x3c22` to `0`.
     - Triggers security dialog warnings.

### `FUN_0032f674` (SERVER.INI Parser)
- **C Signature**: `void FUN_0032f674(undefined4 param_1, int param_2)`
- **Logic**:
  1. Loads the configuration file named `"SERVER.INI"` using file read pointers `PTR_DAT_004c8c60`.
  2. Loops through sections to extract server names, IP addresses, and communication ports.
  3. Compares connection statuses: if a selected channel's status port is unresolved, logs: `"Player offline"` to the UI panel `PTR_DAT_004c8e08 + 0x380`.
  4. Formats server names and channels inside the selection overlay using the string token `"Channel: %d"`.

### `FUN_0014c114` (Channel List Parser)
- **C Signature**: `void FUN_0014c114(int param_1, int param_2)`
- **Logic**:
  1. Loops through channel indexes up to a maximum capacity of **21 sub-channels** (`0x15`).
  2. Resolves channel classification codes based on the classification byte `bVar1`:
     - **Normal Channels (`local_11 = '\x02'`)**: Extracted from configuration block `PTR_DAT_004c8614`.
     - **PVP Channels (`local_11 = '\x01'`)**: Extracted from configuration block `PTR_DAT_004c9864`. Triggers PVP graphical labels.
     - **Event / Special Channels (`local_11 = '\x03'`)**: Extracted from configuration block `PTR_DAT_004c8b70`.
  3. Updates progress bars for server occupancy (green/yellow/red indicators) and maps appropriate UI buttons for player selection.

---

## 3. Network Protocol Packet Format

### Login Response Packet (Server -> Client)
- **Opcode**: `63` (0x3F)
- **Payload Structure**:
  - `status_code` (1 byte): `1` (Success), `2` (Invalid Password), `3`/`4` (Banned/Security).
  - `session_guid_1` (4 bytes): Key token.
  - `session_guid_2` (4 bytes): Security checksum token.
  - `character_slot` (1 byte): Slot index of character to load.
