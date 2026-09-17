# Developer Tooling, Packet Capture, and Reverse Engineering

## 1. Architectural Overview

The Wonderland Online private server project includes integrated diagnostic tooling, packet capture/replay facilities, disk-backed diagnostic logging, and reverse engineering workflows using Ghidra MCP for decompiling the official client binaries (`aLogin.exe`, `MAIN.EXE`, `Wlo.dll`).

---

## 2. Diagnostic Logging Subsystem (`DebugSystem`)

The diagnostic logging engine in [`Phoenix.Core.System.DebugSystem`](file:///D:/GitHub/Wonderland-Private-Server/Phoenix.Core/System/DebugSystem.cs#L11) provides thread-safe, non-blocking asynchronous event reporting:

### 2.1 File Output & Rotation
* **Log Directory:** `bin/Debug/Logs/` or relative `Logs/`.
* **File Naming Pattern:** `wlophoenixlogFile_{yyyyMMdd}.txt`.
* **Log Rotation:** Enforces a maximum file size of `2,500,000` bytes (2.5 MB) with an automatic 10-generation rotating archive (`MaxlogfileCnt = 10`).
* **Timestamp Precision:** Formatted as `[yyyy-MM-dd HH:mm:ss] [TYPE] Message`.

### 2.2 Severity Classification (`DebugItemType`)
1. `Info_Light`: Routine operational notifications (session connected, map warped).
2. `Info_Heavy`: Detailed system traces (full packet payloads, database queries).
3. `Warning`: Non-fatal irregularities (portal cooldown debounce, item slot full).
4. `Error`: Unhandled exceptions, SQL query failures, network socket drops.
5. `Packet`: Serialized wire dumps (hexadecimal frame inspection).

---

## 3. Packet Capture & Proxy Architecture

To reverse engineer and verify official game mechanics without guesswork, network traffic between the official client and server is analyzed through packet capturing tools:

### 3.1 Packet Capture File (PCAP) Analysis
* Raw TCP streams captured on ports `6414` (Login), `6415` (World), and `6416` (Item Mall) are ingested.
* Sequences are parsed down to single protocol frames, matching `0x44F4` headers and action opcodes.
* Used to verify exact frame-by-frame sequences:
  * **PCAP Frame 75:** Ground item pickup validation (`AC 23:2` slot response + `AC 23:6` gold banner).
  * **PCAP Frame 530 & 614:** Staged pig quest states (`AC 22:4` entity state masks).
  * **PCAP Seq 979..1000:** Sequence of `AC 23:102` map load completion followed by `AC 22:10` and `AC 22:11` concealment frames.
  * **PCAP Frame 2928..3000:** Kelan Beach shipwreck cutscene animation progression (`AC 20:10` + `AC 24:1` + `AC 22:12` + `AC 20:8`).
  * **PCAP Frame 6992:** Wooden raft shore wreck 7-step sequence (`AC 15:14 -> AC 23:9 -> AC 15:15 -> AC 15:11 -> AC 5:4`).

---

## 4. Ghidra MCP Binary Reverse Engineering Bridge

The development workspace connects directly to the Ghidra MCP server (`ghidra-mcp`), allowing programmatic decompilation and analysis of official client executables:

### 4.1 Core Capabilities
* **`search_functions_by_name` / `list_functions`:** Discovers client event handlers, packet dispatch tables, and cryptographic routines.
* **`decompile_function_by_address`:** Extracts C/C++ pseudocode directly from binary memory offsets (e.g., investigating client `Eve` opcode evaluators or `AC 22` scene entity rendering tables).
* **`list_strings`:** Identifies string literals, internal protocol action names, and resource paths (`Npc.dat`, `Talk.dat`, `Eve.emg`).
* **`get_xrefs_to` / `get_xrefs_from`:** Traces cross-references to locate caller hierarchies and state machines.

### 4.2 Practical Application in Wonderland Online
* **NPC Drop Encryption:** Recovered the XOR cipher `(rawId ^ 0x5209) - 1` from the client's `Npc.dat` loading routine.
* **Scene Entity Packet:** Confirmed that Byte 8 in the 14-byte `AC 22:4` packet determines entity concealment (`1` = Visible, `2` = Hidden/Ghost).
* **Pre-Event Condition Bytecode:** Verified the 7-byte condition chunk structure and opcode `0x05` flag evaluation layout by decompiling the client's event engine.
