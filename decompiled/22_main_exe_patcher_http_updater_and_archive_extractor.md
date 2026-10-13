# Main.exe Patcher, HTTP/FTP Update Engine & Archive Extractor

## 1. Executive Summary & Executable Topology

The Wonderland Online: Rhodes Island client distribution contains two primary executables:
1. [`aLogin.exe`](file:///D:/GitHub/ghidradecodeyapma/WLRI/aLogin.exe) (5.38 MB): The monolithic game engine containing all game mechanics, direct rendering, audio, in-game forms, GM tools, and packet dispatchers.
2. [`Main.exe`](file:///D:/GitHub/ghidradecodeyapma/WLRI/Main.exe) (1.49 MB, PE32 x86 GUI): The Rhodes Island bootstrap launcher, dual-protocol updater, graphics mode configurator, and web bulletin host.

```
+-----------------------------------------------------------------------------------+
|                           Main.exe OPERATIONAL TOPOLOGY                           |
+-----------------------------------------------------------------------------------+
|                                                                                   |
|  [ Bootstrap & Window Config ]                                                    |
|    - TMainFM (0x158D40): Main Launcher Dashboard (Image1, FormEvents)             |
|    - TSetScreenFM (0x163384): Resolution & Color Depth Config ("WL Setting")      |
|    - TLinkToOfficeFM (0x1503DC): Web Portal & IE ActiveControl (WebBrowser1)      |
|    - Form1 / Form3 (0xE9790 / 0x13D8A4): Visual Splash / Backdrop Dialogs         |
|    - Form2 (0x134DF0): Update Mirror & Package Selector (ListBox1..3)             |
|    - TNMShow (0x16159C): FastNet Transfer Status Dialog (Label1..11, OKBtn)       |
|                                                                                   |
|  [ Dual-Protocol Update Client Engine ]                                           |
|    - HTTP Engine: WinHttp (WinHttpOpen, WinHttpConnect, WinHttpOpenRequest)       |
|    - FTP Engine: NetMasters FastNet (NMFTP1, TNMHTTP, TNMURL, TPowersock)         |
|    - Remote Mirrors: web0.dat (HTTP URLs) and isp1.dat / ISP0.DAT (FTP hosts)     |
|                                                                                   |
|  [ Manifest Synchronization & Differential Patching ]                             |
|    - Local Manifest: PackagesVersion_z.dat                                        |
|    - Remote Staging: PackagesVersion_z.server.tmp                                 |
|    - Download Directory: Download\                                                |
|    - Telemetry / Update Audit: Send.dat                                           |
|                                                                                   |
|  [ Archive Decompression & CRC32 Verification ]                                   |
|    - Embedded Engine: Gilles Vollant unzip 0.15 (1998)                            |
|    - Integrity Verification: CRC32 checksum, byte size validation                 |
|    - Atomic Replacement: Extracts to temp, overwrites target client files         |
|                                                                                   |
|  [ Process Handoff ]                                                              |
|    - Launches aLogin.exe via WinExec(..., SW_SHOWNORMAL) at RVA 0x000BC224        |
|    - Clean shutdown via FormClose / Halt                                          |
|                                                                                   |
+-----------------------------------------------------------------------------------+
```

---

## 2. Delphi VCL Form Structure & Component Mapping

Analysis of the `RT_RCDATA` binary DFM resources (`TPF0` signatures) inside [`Main.exe`](file:///D:/GitHub/ghidradecodeyapma/WLRI/Main.exe) reveals 7 functional forms:

| Form Class | Internal Identifier | Raw Resource Offset | Key Components & Controls | Functional Purpose |
|---|---|---|---|---|
| `TMainFM` | `MainFM` | `0x158D40` | `Image1` (`TImage`), `FormClose`, `FormCreate`, `FormDestroy`, `FormShow` | Master launcher shell, update progress display, launch trigger |
| `TSetScreenFM` | `SetScreenFM` | `0x163384` | `Image1`..`Image8` (`TImage`), `Image1MouseDown`, `Image6Click`..`8Click` | Resolution mode configurator ("WL Setting"): 800x600, 1024x768, windowed/full |
| `TLinkToOfficeFM` | `LinkToOfficeFM` | `0x1503DC` | `WebBrowser1` (`TWebBrowser`), `Button1` (`Button1Click`) | Embedded Internet Explorer ActiveX host for official server announcements |
| `TForm2` | `Form2` | `0x134DF0` | `ListBox1`..`ListBox3`, `Image0c/s`, `Image1c/s`, `Image2c/s`, `MouseDown` | Mirror server selector and update package download queue viewer |
| `TNMShow` | `NMShow` | `0x16159C` | `Label1`..`Label11`, `Image1`, `OKBtn`, `Button1` | FastNet NetMasters network connection diagnostic dialog |
| `TForm1` | `Form1` | `0x0E9790` | `Image1` (`TImage`), `TJPEGImage` | Primary launcher background graphic holder |
| `TForm3` | `Form3` | `0x13D8A4` | `Image1` (`TImage`), `TJPEGImage` | Secondary decorative graphic frame |

---

## 3. Dual-Protocol Network Update Architecture

[`Main.exe`](file:///D:/GitHub/ghidradecodeyapma/WLRI/Main.exe) supports both HTTP and FTP distribution channels for game asset updates:

### 3.1 WinHttp HTTP Transport
The primary update mechanism relies on Microsoft Windows HTTP Services (`winhttp.dll`):
- `WinHttpOpen`: Initializes session handle with custom user agent.
- `WinHttpConnect`: Connects to mirror server defined in `web0.dat`.
- `WinHttpOpenRequest`: Requests remote manifest `PackagesVersion_z.dat` or package zip archives.
- `WinHttpSendRequest` & `WinHttpReceiveResponse`: Validates HTTP status code (`HTTP status=200`).
- `WinHttpReadData`: Streams binary package payload into `Download\` directory.

### 3.2 NetMasters FastNet FTP Transport
As fallback or alternate mirror mode, [`Main.exe`](file:///D:/GitHub/ghidradecodeyapma/WLRI/Main.exe) embeds NetMasters FastNet components:
- `NMFTP1Connect`: Connects to anonymous FTP servers on port 21 configured in `isp1.dat` / `ISP0.DAT`.
- `NMFTP1HostResolved` / `NMFTP1Status`: Monitors transmission socket states.
- `NMFTP1PacketSent` / `NMFTP1PacketRecvd`: Feeds download gauge progress bars.

---

## 4. Manifest Synchronization & Differential Patching Pipeline

```
+-------------------+        +--------------------+
| Local Manifest    |        | Remote Mirror      |
| PackagesVersion_z |        | PackagesVersion_z  |
+-------------------+        +--------------------+
          |                            |
          +------------+---------------+
                       |
                       v
         [ Differential Version Compare ]
         - Inspect "Version=" and "High Version="
         - Enumerate missing package revisions
                       |
                       v
            [ Download Missing Zips ]
            - Save to "Download\<pkg_id>.zip"
            - Size check: Downloaded size vs Expected
                       |
                       v
            [ Archive Integrity Check ]
            - Run CRC32 verification
            - Inspect ZIP local headers
                       |
                       v
            [ Atomic Unpack & Apply ]
            - Unzip files into target root / data\
            - Update local PackagesVersion_z.dat
```

### Manifest Specifications:
- `PackagesVersion_z.dat`: Text/binary hybrid manifest containing sequential release version identifiers, target file paths, expected uncompressed byte sizes, and CRC32 verification sums.
- `PackagesVersion_z.server.tmp`: Temporary file written during remote download before validation passes.
- `Send.dat`: Telemetry file uploaded to reporting endpoint logging client patch outcome (`Patch Succeeded` / `Patch Failed`).

---

## 5. Archive Decompression & Extraction Engine

[`Main.exe`](file:///D:/GitHub/ghidradecodeyapma/WLRI/Main.exe) statically embeds the Gilles Vollant `unzip 0.15 (1998)` deflate decompression library:
- **Header Parsing:** Reads ZIP central directory (`Cannot read ZIP directory`, `Cannot read first ZIP entry`).
- **CRC Validation:** Computes CRC32 checksum over decompressed streams against the archive header record (`ZIP CRC check failed. Code=`).
- **Buffer Safety:** Validates decompressed byte size against manifest specifications (`ZIP extracted size mismatch. Expected=`, `ZIP extracted size is larger than expected`).
- **Atomic File Overwrite:** Uncompressed assets (`data\*.dat`, `data\*.MMG`, `data\*.emg`) replace outdated files in the client root.

---

## 6. Process Handoff to aLogin.exe

Upon completion of the update lifecycle (or when all packages are verified up to date), [`Main.exe`](file:///D:/GitHub/ghidradecodeyapma/WLRI/Main.exe) launches the main game engine:

```assembly
; Disassembly at RVA 0x000BC200 in Main.exe
push    1                   ; uCmdShow = SW_SHOWNORMAL (1)
lea     eax, [ebp-8]        ; Path to "alogin.exe"
call    WinExec             ; WinExec("alogin.exe", SW_SHOWNORMAL)
call    CloseMainForm       ; Closes patcher GUI
```

String literal `"alogin.exe"` is located at RVA `0x000BC224` in `CODE` section.
Main.exe exits cleanly, relinquishing system resources to [`aLogin.exe`](file:///D:/GitHub/ghidradecodeyapma/WLRI/aLogin.exe).
