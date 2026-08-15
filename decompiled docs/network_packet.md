# Network & Packet Dispatcher Decompiled Specifications

This document outlines the client-side socket managers, threading loops, Winsock APIs, TCP packet framing, and primary dispatch tables extracted from `alogin_analyzed/network_packet.c`.

---

## 1. Alt Seviye Winsock APIs & Error Codes

The client utilizes Windows Socket 2 (Winsock) interfaces for asynchronous TCP communication. Key configurations and error checks include:

| Winsock API / Code | Value | Context | Usage |
|---|---|---|---|
| `connect()` | - | Connection request | Initiates TCP handshakes asynchronously |
| `recv()` | - | Packet read | Retrieves raw bytes from TCP stream |
| `send()` | - | Packet write | Pushes raw bytes to TCP socket buffer |
| `ioctlsocket()` | `0x4004667f` | FIONBIO | Toggles non-blocking socket mode |
| `WSAEWOULDBLOCK` | `0x2733` (10035) | Socket non-blocking delay | Checked in `FUN_0007a284` during read loop |
| `MSG_PEEK` | - | Socket inspection | Tests pending buffer without consuming data |

---

## 2. Key Decompiled Functions

### `FUN_000799c8` (Socket Connect)
- **C Signature**: `void FUN_000799c8(int param_1)`
- **Logic**:
  1. Initializes socket structures and triggers `FUN_000798ec` to reset buffers.
  2. Sets non-blocking state using `ioctlsocket(..., FIONBIO, &mode)`.
  3. Calls Winsock `connect()` with target socket descriptor and server address pointer (`param_1 + 0x18`).
  4. Tests if the socket handle is valid (`!= -1`). Stores status at `param_1 + 8`.

### `FUN_0007a284` (Socket Recv Wrapper)
- **C Signature**: `void FUN_0007a284(int *param_1, char *param_2, int param_3)`
- **Logic**:
  1. Calls `recv()` on the target socket `param_1[1]`.
  2. If the return byte count is `0xffffffff`, checks `WSAGetLastError()`.
  3. If error is `WSAEWOULDBLOCK` (`0x2733`), silently yields execution to next tick.
  4. For any other error, invokes connection close `*param_1 + 8` and prints alert dialog.

### `FUN_0012479c` & `FUN_0007a0fc` (Socket Send Wrappers)
- **C Signatures**: `void FUN_0012479c(int param_1, int param_2, uint param_3)`, `void FUN_0007a0fc(...)`
- **Logic**:
  1. Validates queue transmission states.
  2. Pushes payload through `send()` in a loop until all `param_3` bytes are pushed.
  3. If `send()` fails with error, calls `FUN_00125dbc` to log the failure and closes socket connections.

---

## 3. TCP Framing & Thread Workers

### `FUN_00124a1c` (Recv Buffer Framer)
- **Signature**: `int FUN_00124a1c(int param_1)`
- **Algorithm**:
  ```
  Loop:
    1. Read 2 bytes (Header Signature). Check if equals 0x44F4.
    2. If invalid, slide buffer by 1 byte and re-scan.
    3. If valid, read next 2 bytes (Big-Endian packet length).
    4. Validate if available buffer size >= packet length.
    5. If complete, forward payload to main packet dispatcher.
  ```

### `FUN_00124ff0` & `FUN_00124df4` (Thread Workers)
- **Logic**:
  - Alıcı Thread (`FUN_00124ff0`): Infinite loop calling Winsock `recv` on socket descriptor. Buffers inputs into the framer queue.
  - Gönderici Thread (`FUN_00124df4`): Monitors giden paket kuyruğu (TX queue). Flushes queue blocks sequentially when socket is ready.

---

## 4. Main Packet Dispatch Tables

Incoming frames are directed through two primary dispatcher modules depending on whether they affect gameplay data or UI layout.

### `FUN_00115a38` (Main Packet Dispatcher 1)
Translates packet payloads based on the first byte opcode:

- **`0x02` (Chat)**: Decodes local, whisper, guild, and system chat boxes.
- **`0x05` (Visuals)**: Forces player appearance/mount refreshments.
- **`0x06` (Movement)**: Syncs grid coordinates and maps movements.
- **`0x0b` (Combat)**: Starts battles, flees, or checks PK ranges.
- **`0x0c` (Warp)**: Handles maps warping and coordinate adjustments.
- **`0x0e` (Friend/Mail)**: Parses friend list and letter mailboxes.
- **`0x0f` (Pet)**: Manages pet summon slots and AI modes.
- **`0x14` (Interaction)**: Binds NPC conversation options.
- **`0x17` (Item)**: Adds, moves, deletes, or wears equipment items.
- **`0x19` (Trade)**: Resolves trade requests and stalls slots.
- **`0x27` (Quest)**: Loads quest list entries.
- **`0x32` (Battle Turn)**: Resolves combat cards and turn animations.
- **`0x3f` (Login)**: Parses login validation responses.

### `FUN_0010e218` (Main Packet Dispatcher 2)
Distributes packet arrays directly to active interface forms:
- Binds weapon graphics overlays.
- Updates character card stats.
- Updates pet info panels.
- Controls Nesne Market balances.
