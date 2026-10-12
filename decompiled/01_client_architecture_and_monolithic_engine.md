# Client Architecture and Monolithic Engine Specification

## 1. Executive Summary & Binary Roles

The client architecture for Wonderland Online: Rhodes Island consists of two primary PE32 executables:

```
+-----------------------------------------------------------------------------------+
|                            CLIENT SYSTEM TOPOLOGY                                 |
+-----------------------------------------------------------------------------------+
|                                                                                   |
|  [ Main.exe ] (1,490,944 Bytes / 1.42 MB)                                         |
|    - PE32 x86 compiled with Borland Delphi / C++Builder                           |
|    - Role: HTTP Patcher, Graphics Configuration, and Launcher Bridge              |
|    - Subcomponents:                                                               |
|        * TPackageUpdater: Uses WinHttp (WinHttpOpen, WinHttpConnect) to fetch     |
|          package manifests and delta archives from web0.DAT endpoints             |
|        * TSetScreenFM ("WL Setting"): Screen resolution and color depth configurator|
|        * TLinkToOfficeFM ("LinkToOffice"): Official portal hyperlinking           |
|        * Process Dispatch: Launches aLogin.exe via WinExec / ShellExecuteA        |
|                                                                                   |
|  [ aLogin.exe ] (5,382,656 Bytes / 5.13 MB)                                       |
|    - PE32 x86 compiled with Borland Delphi / C++Builder (VCL Runtime)             |
|    - Image Base: 0x00400000 | Entry Point: 0x004AC2D8 | 9,451 Subroutines        |
|    - Role: Monolithic Client Game Engine                                          |
|    - Subcomponents:                                                               |
|        * DirectDraw Rendering Engine (DXDraw1, 16-bit 65536 color mode)           |
|        * DirectSound Audio Engine (DXSound1, BGM/SFX playback via OGG/Vorbis)     |
|        * Direct3D Retained Mode Engine (D3DRM.dll) for spell/particle effects     |
|        * Server Selection & Authentication Handshake (Ports 6414 & 6416)          |
|        * 81 In-Game Form Systems (Auto-Combat bot, Alchemy, Banking, Stalls, etc.)|
|        * Complete Hidden GM Administration Suite (TGmForm / CY_GmForm)            |
|        * In-Game Event State Machines (Interserver PVP, Battle Royale, Trojan War)|
|                                                                                   |
+-----------------------------------------------------------------------------------+
```

---

## 2. Portable Executable (PE) Section Map (`aLogin.exe`)

```
+-----------+-------------------+---------------+---------------+---------------------------------------+
| Section   | Virtual Address   | Virtual Size  | Raw Data Size | Description                           |
+-----------+-------------------+---------------+---------------+---------------------------------------+
| CODE      | 0x00401000        | 4,830,192 B   | 4,830,208 B   | Executable machine instructions       |
| DATA      | 0x008AD000        | 117,116 B     | 117,248 B     | Initialized global data & VCL tables  |
| BSS       | 0x008CA000        | 3,603,825 B   | 0 B           | Uninitialized static BSS buffers      |
| .idata    | 0x00C3A000        | 11,728 B      | 11,776 B      | Win32 Import Address Table (IAT)      |
| .tls      | 0x00C3D000        | 20 B          | 0 B           | Thread Local Storage                  |
| .rdata    | 0x00C3E000        | 24 B          | 512 B         | Read-only runtime descriptors         |
| .reloc    | 0x00C3F000        | 298,472 B     | 298,496 B     | Base relocation fixup table           |
| .rsrc     | 0x00C88000        | 123,392 B     | 123,392 B     | Embedded VCL DFM forms, icons, bitmaps|
+-----------+-------------------+---------------+---------------+---------------------------------------+
```

---

## 3. Embedded Delphi Forms (DFM) in `aLogin.exe`

Inspection of `RT_RCDATA` binary resources reveals 12 distinct Delphi Form streams (`TPF0` magic signature):

| Offset (Hex / Dec) | Size (Bytes) | Form Class | Form Name | Purpose |
|---|---|---|---|---|
| `0x00513d64` (5324324) | 8,432 B | `TDelphiXDXPlayForm` | `DelphiXDXPlayForm` | DirectPlay / Multiplayer networking setup |
| `0x00515e54` (5332756) | 12,900 B | `TForm1` | `Form1` | Primary game viewport (`DXDraw1`, `DXSound1`) |
| `0x00519098` (5345656) | 524 B | `TForm2` | `Form2` | Modal dialog prompt |
| `0x005192a4` (5346180) | 7,252 B | `TForm3` | `Form3` | Terms of Service / EULA (`AgreeClick`) |
| `0x0051aee8` (5353432) | 652 B | `TForm4` | `Form4` | Notification alert box (`Button1Click`) |
| `0x0051b174` (5354084) | 7,424 B | `TForm5` | `Form5` | Server Selector & Channel cluster browser |
| `0x0051ce74` (5361508) | 12,784 B | `TGmForm` | `GmForm` | Complete 56-button GM Administration Suite |
| `0x00520074` (5374292) | 8,364 B | `TNMShow` | `NMShow` | In-game announcements and web promotional window |

---

## 4. Graphics & Audio Multimedia Stack

The client utilizes a hybrid DirectX 7/8 multimedia architecture:

* **DirectDraw (`DDraw.dll` / `ddraw.dll`):**
  * Primary surface renderer: `TDXDraw` (`DXDraw1`).
  * Resolution mode: 800x600 or 1024x768 at 16-bit high color (`65536` colors).
  * Framerate target: 60 FPS (configured via `Sinfo.txt`).
* **DirectSound & Audio Codecs (`DSound.dll`, `ogg.dll`, `vorbis.dll`, `vorbisfile.dll`):**
  * Background music playback stream: `vorbisfile.dll` streaming `.ogg` tracks from `Mp3s\` directory.
  * Sound effects: Waveform audio playback (`wav0151.wav`, etc.) from `sound\` directory.
* **Intel JPEG Library (`ijl15.dll`):**
  * Hardware-accelerated decompression of JPEG background cutscenes and character portraits (`Photo\`, `Photo\User\`).
