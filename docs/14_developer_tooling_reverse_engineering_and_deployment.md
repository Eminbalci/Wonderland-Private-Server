# Developer Tooling, Reverse Engineering, and Deployment Operations

## 1. Architectural Overview

This document outlines the deployment requirements, debugging harnesses, packet capture workflows, client disassembly bridges, and automated bot testing frameworks for Wonderland Online Private Server.

---

## 2. Build Prerequisites & Compilation

* **Development Environment:** Visual Studio 2022 (v17.11+).
* **Target Framework:** .NET Framework 4.6.2 (`net462`).
* **Clean Build Command:**
  ```powershell
  dotnet build "Wonderland Private Server.sln" --configuration Debug
  ```
* **Compilation Invariant:** The entire solution compiles cleanly with **0 errors and 0 warnings**.

---

## 3. Client Configuration & Server Deployment

### 3.1 Client Network Setup (`SERVER.INI`)
The official client connects using `SERVER.INI` located in the game root folder:
```ini
[Server]
IP=127.0.0.1
Port=6414
```

### 3.2 Dynamic Portability & Launcher
* The server automatically resolves the client executable (`aLogin.exe`) via [`PathHelper.GetClientExecutablePath()`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/RCLibrary.Core/PathHelper.cs).
* Pressing **F5** in the GUI administration window launches the client immediately.
* Client patch redirects (`web0.DAT`) are dynamically served by [`EmbeddedWebServer.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Network/EmbeddedWebServer.cs).

---

## 4. Asynchronous Diagnostic Logging Engine

Logging is centralized within [`DebugSystem.cs`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/RCLibrary.Core/DebugSystem.cs):
* **Non-Blocking Write Queue:** Logs are enqueued to a background writer thread, eliminating I/O stalls during high-frequency network events.
* **Hex Dumps:** Unhandled or malformed Action Codes automatically produce formatted hex dumps showing packet offsets and decrypted payloads.
* **Console Toolbar:** Provides live search filtering, auto-scroll freeze, clipboard copy, and file export options.

---

## 5. PCAP Capture Analysis & Wireshark Workflows

Protocol alignment is verified against raw official packet captures:
* **Wireshark Display Filter:**
  ```wireshark
  tcp.port == 6414 || tcp.port == 6415 || tcp.port == 6416
  ```
* **Byte Alignment Verification:**
  - Header Magic: `0xF4, 0x44` (little-endian UInt16 `0x44F4`).
  - Action Code Offset: Byte index 4.
  - Sub-Action Code Offset: Byte index 5.
  - Encryption: Symmetric XOR with key `0xAD` (decimal 173).

---

## 6. Ghidra Reverse Engineering Bridge & Disassembly Addresses

The server's protocol implementations have been cross-referenced against the decompiled client binaries (`aLogin.exe` and `WLO.exe`):

```
+-------------------+-----------------------+-------------------------------------------------------+
| Virtual Address   | Subroutine Identifier | Functionality in Official Client                      |
+-------------------+-----------------------+-------------------------------------------------------+
| 0x004121A8        | FUN_004121a8          | Ground.MMG terrain geometry and tile height loader    |
| 0x0043D390        | FUN_0043d390          | Dynamic collision grid bitmask modifier (*pbVar & 0xfb)|
| 0x00432674        | FUN_00432674          | AC 22:4 concealment handler (sets *(actor+0x1eec) = 2)|
| 0x0043D58C        | FUN_0043d58c          | Entity rendering loop (skips actors with state 2 or 4)|
| 0x00778628        | TCalculator           | Formula.dat EXP exponent math (fyl2x / f2xm1 opcodes) |
+-------------------+-----------------------+-------------------------------------------------------+
```

---

## 7. Automated Client Simulation Framework (`wlo.pserver.Bot`)

The [`wlo.pserver.Bot`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.Bot/wlo.pserver.Bot.csproj) project provides headless client automation:
* **Connection Stress Testing:** Spawns concurrent synthetic clients to stress login handshake queues (`Port 6414`) and world server ticks (`Port 6415`).
* **Pathfinding & Portal Traversal:** Simulates movement packets (`AC 5:1`) across overworld portals (`AC 21:1`) to detect boundary stalls.
* **Combat Session Automation:** Submits turn actions (`AC 50:1`) to validate 30-second battle round timers and victory fanfares.
