# aLogin.exe Reverse Engineering and Protocol Specification

## 1. Executive Summary & Binary Identification

The client launcher and server selector [`aLogin.exe`](file:///D:/GitHub/ghidradecodeyapma/WLRI/aLogin.exe) is an authentic 32-bit Windows PE executable responsible for server list discovery, client patch validation, account authentication, and game client process dispatching.

```
+---------------------+---------------------------------------------------------+
| Binary Field        | Value                                                   |
+---------------------+---------------------------------------------------------+
| File Path           | D:\GitHub\ghidradecodeyapma\WLRI\aLogin.exe             |
| File Size           | 5,382,656 Bytes (5.13 MB)                               |
| Target Architecture | PE32 (x86 32-bit Little-Endian)                         |
| Image Base          | 0x00400000                                              |
| Entry Point (RVA)   | 0x004AC2D8                                              |
| Compiler Toolchain  | Borland Delphi / C++Builder (VCL Runtime)               |
| Decompiled Scale    | 9,451 Subroutines (Ghidra 12.1.2)                       |
+---------------------+---------------------------------------------------------+
```

---

## 2. Portable Executable (PE) Section Map

REA (`rea inspect-native-load-image` / PE parser) identifies 8 sections matching the standard Borland compiler layout:

```
+-----------+-------------------+---------------+---------------+---------------------------------------+
| Section   | Virtual Address   | Virtual Size  | Raw Data Size | Purpose                               |
+-----------+-------------------+---------------+---------------+---------------------------------------+
| CODE      | 0x00401000        | 4,830,192 B   | 4,830,208 B   | Executable machine instructions       |
| DATA      | 0x008AD000        | 117,116 B     | 117,248 B     | Initialized global data and VCL tables|
| BSS       | 0x008CA000        | 3,603,825 B   | 0 B           | Uninitialized static variables        |
| .idata    | 0x00C3A000        | 11,728 B      | 11,776 B      | Import Address Table (Win32 APIs)     |
| .tls      | 0x00C3D000        | 20 B          | 0 B           | Thread Local Storage                  |
| .rdata    | 0x00C3E000        | 24 B          | 512 B         | Read-only runtime descriptors         |
| .reloc    | 0x00C3F000        | 298,472 B     | 298,496 B     | Base relocation fixup table           |
| .rsrc     | 0x00C88000        | 123,392 B     | 123,392 B     | Embedded VCL DFM forms, icons, bitmaps|
+-----------+-------------------+---------------+---------------+---------------------------------------+
```

---

## 3. Embedded VCL Form Architecture (RT_RCDATA)

REA resource inspection (`rea inspect-pe-resources`) decodes 100 distinct resources, including binary Delphi forms (`TPF0` magic):

### 3.1 `TGmForm` (Offset `5361508`, 12,781 Bytes)
* **Form Class:** `TGmForm` (`GmForm`)
* **Primary Container:** `TPageControl` (`PageControl1`) with 5 functional tabs:
  - `TabSheet1`: Server diagnostics & debug log console (`Memo1`, `Memo9`).
  - `TabSheet2`: Active server channel matrix (`TStringGrid` `StringGrid1`).
  - `TabSheet5`: Account credentials and login configuration.
  - `TabSheet11` / `TabSheet12`: Network latency and patch metadata inspectors.
* **Lifecycle Handlers:** `FormCreate` (`0x0032f87e`) and `FormDestroy`.

### 3.2 `TNMShow` (Offset `5374292`, 7,654 Bytes)
* Secondary presentation modal displaying promotional banners, notices, and web redirect URLs.

---

## 4. Network Socket Topology & Connection Lifecycle

Decompilation analysis of `aLogin.exe` reveals dual Winsock client sockets managed under master context object `PTR_DAT_004c96d0`:

```
                             +-------------------+
                             |    aLogin.exe     |
                             | (Master Context)  |
                             +-------------------+
                               /               \
            Socket Index 1    /                 \   Socket Index 2
            (Context + 0x3dc)/                   \  (Context + 0x3e4)
                            v                     v
                +-----------------------+     +-----------------------+
                | Status / Mall Probe   |     | Primary Login Socket  |
                | Port 6416 (0x1910)    |     | Port 6414 TCP         |
                +-----------------------+     +-----------------------+
```

### 4.1 Server Status Probe Socket (`Context + 0x3dc`)
* **Function Anchor:** `FUN_00419709` / `FUN_00419828`
* **Target Port:** `0x1910` (decimal **6416**, Item Mall / Server Status port).
* **Protocol:** Connects to test server vitality, sends `0xC9` status probes, and disconnects after latency measurement.

### 4.2 Primary Login Socket (`Context + 0x3e4`)
* **Target Port:** **6414** TCP (Login Server authentication listener).
* **Connection Sequence:**
  1. Sets destination IP via `FUN_0007a6b0(socket, ipString)`.
  2. Binds local interface via `FUN_00079adc`.
  3. Transitions socket to active via `FUN_0007a660(socket, 1)`.
  4. Triggers auditory feedback via `sound\wav0151.wav`.

### 4.3 `SERVER.INI` Parsing Logic (`FUN_0032f87e` / Line 316796)
```c
FUN_00014178(&local_3c, *(undefined4 *)PTR_DAT_004c8c7c, "SERVER.INI");
(**(code **)(*piVar9 + 0x58))(piVar9, local_3c); // Load INI into TStringList
cVar5 = (char)((ulonglong)uVar7 / 100);          // Server cluster group (ID / 100)
// Sub-channel extraction: uVar7 % 100
```
Matches server channels against user-selected cluster names, formatting UI labels with `Role: ` and `Channel: ` descriptors.

---

## 5. Wire Framing & Encryption Alignment

Network communications initiated by `aLogin.exe` strictly conform to the Wonderland Online wire framing standard:

* **Magic Header:** UInt16 `0x44F4` (`17652` in little-endian format: `0xF4, 0x44`).
* **Symmetric Encryption:** Single-byte XOR key `0xAD` (decimal 173).
* **Length Serialization:** 16-bit little-endian payload length (Offset 2..3).
* **Action Code Layout:**
  - Byte 4: Action Code (`AC`).
  - Byte 5: Sub-Action Code.

This validates the implementation in [`IncomingPacket.cs`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/RCLibrary.Core.Networking/IncomingPacket.cs) and [`OutgoingPacket.cs`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/RCLibrary.Core.Networking/OutgoingPacket.cs).

---

## 6. Process Dispatching & Patcher Bridge

`aLogin.exe` implements three subprocess launch vectors:

1. **Auto-Updater:** `FUN_003bf3b4` calls `WinExec("Update.EXE", 1)` when version mismatch occurs.
2. **Patcher Helper:** Dispatches `Mainupdate.exe` to download delta assets.
3. **Game Client Executable:** Invokes `Main.exe` (or `WLO.exe`) via `ShellExecuteA("open", "Main.exe", ...)` upon successful authentication handshake.

---

## 7. REA Tooling Integration Commands

To reproduce and inspect these findings using REA:

```powershell
# Inspect PE resources and Delphi DFM forms
rea.cmd inspect-pe-resources "D:\GitHub\ghidradecodeyapma\WLRI\aLogin.exe"

# Disassemble specific subroutines
rea.cmd instructions "D:\GitHub\ghidradecodeyapma\WLRI\aLogin.exe" --address 0x004AC2D8

# Query Ghidra decompilation cross-references
rea.cmd xrefs "D:\GitHub\ghidradecodeyapma\WLRI\aLogin.exe" --address 0x00419828
```

---

## 8. Exhaustive Specifications

For the comprehensive breakdown of all 81 in-game forms, the complete 56-button GM tool suite, channel bitmasks, and server events, consult:
[`docs/18_complete_client_engine_and_gm_suite_analysis.md`](file:///D:/GitHub/Wonderland-Private-Server/docs/18_complete_client_engine_and_gm_suite_analysis.md).
