# Technical Analysis: `aLogin_decompiled.c`

## 1. Overview
* **File Path:** `decompiled/aLogin_decompiled.c`
* **File Size:** ~17.44 MB (17,443,024 bytes)
* **Total Lines:** 518,521 lines
* **Total Function Blocks:** 9,452 decompiled functions
* **Origin:** Decompiled Ghidra output from the official `aLogin.exe` binary (Wonderland Online Client Launcher & Patcher).
* **Compiler / Framework:** Borland C++ Builder 5/6 with Visual Component Library (VCL) runtime.

---

## 2. Architecture & Runtime Subsystems

### 2.1 Entry Point & VCL Lifecycle
* **Entry Point (`entry` / `WinMain`):** Initializes the VCL `TApplication` instance, setting up message handling via `MsgWaitForMultipleObjects` and `DispatchMessageA`.
* **GUI / VCL Components:**
  * `TG_Form`, `TRe_CompoundForm`, `TSe_FixedForm`, `TTalkMsgForm`, `TR_s_PsockWindowClass_004b87b0`.
  * Visual forms handle server selection lists, login credential inputs, patch progress bars, and announcement links.

### 2.2 Configuration & Server List Parser (`SERVER.INI`)
* **Function:** `FUN_0032f7f0` (`@ 0x0032f7f0`)
* **Workflow:**
  1. Instantiates internal VCL string list / INI parser.
  2. Reads `SERVER.INI` located in the root client directory.
  3. Parses server nodes, channel IDs, server names, IP addresses, and ports.
  4. Formats server status (e.g. handles offline status `"Player offline"` and populates GUI server lists).

### 2.3 Account Persistence Subsystem
* **File:** `user\AccountList.dat`
* **Save Handler (`FUN_0041cacc` @ `0x0041cacc`):**
  * Iterates over saved user accounts in the UI combo box/list.
  * Encodes and writes account names to `user\AccountList.dat`.
* **Load Handler (`FUN_0041cbe4` @ `0x0041cbe4`):**
  * Checks if `user\AccountList.dat` exists.
  * Populates account name history into login form dropdown.

### 2.4 Networking & Socket Subsystem
* **Winsock APIs:** `WSAStartup`, `socket`, `connect`, `ioctlsocket`, `send`, `recv`, `closesocket`.
* **Connection Handler (`FUN_000799c8` @ `0x000799c8`):**
  * Connects TCP socket to the IP/Port resolved from `SERVER.INI`.
  * Sets up asynchronous non-blocking notification handlers.
* **Packet Transmission Engine (`FUN_00079f88` @ `0x00079f88`):**
  * Manages outbound packet queue (4 KB buffer blocks).
  * Handles `WSAEWOULDBLOCK` (`0x2733` / 10035) with incremental buffer offset retries.

### 2.5 Patcher & Updater Execution Subsystem
* **Auto-Update Trigger (`FUN_003bf3b4` @ `0x003bf3b4`):**
  * Calls `WinExec("Update.EXE", 1)` when client version mismatch or update signal is received.
* **Web Navigation Handlers:**
  * Uses `ShellExecuteA` to open official announcements / registration portals (e.g. Chinese Gamer / Dragon Gamer HK URLs).

### 2.6 Protocol & Packet Dispatchers
* Contains 3,590+ switch cases across network and UI dispatchers.
* Core packet parsing and opcode dispatchers:
  * `FUN_00440308`: Opcode byte indexing and normalization switch.
  * `FUN_0049eb74` / `FUN_0049ec20`: Action Code (AC) sub-packet dispatchers.
  * `FUN_0045fb20` / `FUN_00426a50`: Game response code routers.

---

## 3. Server Emulation Compatibility Matrix

| Client Component | Target File / Config | Private Server Implementation |
| :--- | :--- | :--- |
| Server List & Endpoints | `SERVER.INI` | Private Server listens on `127.0.0.1` |
| Account History | `user\AccountList.dat` | Local client-side cache |
| Login Protocol | Winsock TCP Packet stream | Handled in `wlo.pserver.core` / `Src\Server` |
| Patcher Hand-off | `Update.EXE` | Bypassed in private server environments |
