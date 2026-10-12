# Complete 9,451 Function Inventory and Subsystem Catalog

## 1. Inventory Summary & Methodology

Through automated analysis of the 518,522 decompiled C lines in [`aLogin_decompiled.c`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c), all **9,451 subroutines** have been cataloged with virtual addresses, code volume (lines), string references, call graphs, and architectural domain assignments.

```
+---------------------------------------------------+---------------+-------------------+
| Architectural Domain                              | Function Count| Decompiled Lines  |
+---------------------------------------------------+---------------+-------------------+
| General Game Logic & Auxiliary Handlers           | 4,610         | 296,998 Lines     |
| Borland Delphi RTL & Base VCL Core                | 3,252         | 102,469 Lines     |
| DelphiX DirectX Components & Controls             | 1,042         |  36,823 Lines     |
| Binary Asset Deserializers & EMG Loaders          |   190         |   8,461 Lines     |
| UI Forms & In-Game Interfaces                     |    87         |  12,289 Lines     |
| Application Entry & System Runtime                |    68         |   5,140 Lines     |
| Economy, Inventory & Compounding                  |    63         |  11,306 Lines     |
| Combat & Battlefield Engine                       |    41         |   9,581 Lines     |
| Pet Companions & Mounts                           |    33         |  14,924 Lines     |
| Multimedia & DirectX Engine                       |    21         |   4,685 Lines     |
| Mini-Games & Entertainment                        |    15         |   2,480 Lines     |
| Network Protocols & Cryptography                  |    12         |   1,291 Lines     |
| Map, Scene & Spatial World                        |     8         |   1,312 Lines     |
| GM Administration & Anti-Cheat                    |     6         |     874 Lines     |
| Dialogue, Quests & Events                         |     3         |     430 Lines     |
+---------------------------------------------------+---------------+-------------------+
| TOTAL DECOMPILED SCALE                            | 9,451         | 509,063 Lines     |
+---------------------------------------------------+---------------+-------------------+
```

---

## 2. Architectural Domain Breakdown

### 2.1 Borland Delphi RTL & Base VCL Core (3,252 Functions / 102,469 Lines)
* **Address Range:** `0x00010000` - `0x000affff`
* **Core Responsibilities:**
  * FastMM memory allocation & deallocation (`RTL_FreeMem_000127ac`, `RTL_MoveMem_000128fc`).
  * Long Pascal string manipulation routines (`RTL_LStrClr_00013eac`, `RTL_LStrCat_00014134`, `RTL_LStrCopy_00014334`).
  * Base VCL classes: `TObject`, `TPersistent`, `TComponent`, `TControl`, `TWinControl`, `TGraphicControl`, `TCanvas`.
  * Window message dispatch loops: `WM_PAINT`, `WM_LBUTTONDOWN`, `WM_MOUSEMOVE`, `WM_SIZE`, `WM_DESTROY`.

### 2.2 DelphiX DirectX Components & Controls (1,042 Functions / 36,823 Lines)
* **Address Range:** `0x000b0000` - `0x0013ffff`
* **Core Responsibilities:**
  * `TDXDraw`: Hardware canvas controller linking to `DDraw.dll` (`IDirectDraw7`, `IDirectDrawSurface7`).
  * `TDXSound`: Digital sound controller linking to `DSound.dll` (`IDirectSoundBuffer`).
  * `TDXPaintBox`: Hardware double-buffered drawing surface.
  * Winsock socket adapters: `TClientSocket`, `TCustomWinSocket` non-blocking wrappers.

### 2.3 Network Protocols & Cryptography (12 Functions / 1,291 Lines)
* **Address Range:** `0x00140000` - `0x00170000`
* **Key Functions:**
  * `FUN_00153d6c`: Master single-byte symmetric XOR encryption algorithm (default key `0xAD`).
  * `FUN_001630d0`: Outgoing transmission engine de-queuing packets from `DAT_006c9864`.
  * `FUN_00124a1c`: TCP stream buffer framer validating `0x44F4` magic headers.
  * `FUN_00115a38`: DirectInput hardware scancode translation table.

### 2.4 UI Forms & In-Game Interfaces (87 Functions / 12,289 Lines)
* **Address Range:** `0x00170000` - `0x00270000`
* **Key Functions:**
  * `FUN_001d6294`: Master UI dispatcher and `Form_AutoPlayerPlus_1/2` bot engine.
  * `FUN_00235340`: `Form_Compound_1` alchemy compounding interface.
  * `FUN_0021b57c`: `Form_ATM_List` in-game banking terminal.
  * `FUN_0022bb00`: `Form_Capsulem` gacha capsule machine.
  * `FUN_0019de44`: `Form_CreateDupMis` instance dungeon generator.
  * `FUN_001aed4c`: `Form_EightTwelve` 8-12 dice gambling minigame.
  * `FUN_001f62d4`: `Form_blueprint_1` and `Form_MakeThing_1` tent crafting.

### 2.5 General Game Logic & Actor Handlers (4,610 Functions / 296,998 Lines)
* **Address Range:** `0x00270000` - `0x0037ffff`
* **Core Responsibilities:**
  * Active actor array traversal: `DAT_006c7368` (local player) and scene entity array `local_8 + 0x13c + index * 4`.
  * Map loading & decompression: `FUN_00322f90` allocating scene geometry.
  * MapData decryption: `FUN_0032cd88` loading tile matrices via XOR key `0x190C`.
  * NPC interaction & distance checks: `FUN_0031d874` enforcing $\le 169$ pixel delta.

### 2.6 Turn-Based Combat & PK Battlefield (41 Functions / 9,581 Lines)
* **Address Range:** `0x00380000` - `0x0041ffff`
* **Key Functions:**
  * `FUN_003a6f18`: PK challenge validator evaluating map flags and active player states.
  * `FUN_003a7154`: PK distance verification enforcing $< 271$ pixel delta.
  * `FUN_003a35c8`: Combat turn acknowledgement and animation sequence completion.
  * `FUN_003de310`: Companion battle summoner and slot activator.

### 2.7 Dialogue, Quests & Events (3 Functions / 430 Lines)
* **Address Range:** `0x00420000` - `0x0048ffff`
* **Key Functions:**
  * `FUN_004896f4`: Dialogue form layout coordinator (`Form_Talk_1`, `Form_Talk_2`, `Form_Talk_3`, `panel22`).
  * `FUN_0040d550`: Specialized quest dialogue avatar interface (`TalkForm1`).
  * `FUN_0030859c`: Quest journal task list manager (`form_taskview_1`).

### 2.8 Binary Asset Deserializers & EMG Loaders (190 Functions / 8,461 Lines)
* **Address Range:** `0x00490000` - `0x0049ffff`
* **Key Functions:**
  * `FUN_00497c2c`: Master binary scene loader validating format version `0x70` (112).
  * `FUN_00498208`: 19 sequential binary record readers (`Read SceneHead.`, `Read Npc.`, `Read Door.`, `Read Mine.`, `Read Goods.`, `Read Eve.`, `Read Battle.MemberAry`, `Read Convey.`, `Read Group.`, `Read LeadEve.`).
  * `FUN_0049f5e8`: Alchemy material classifier (23 material categories: `0x01` Flower to `0x3C` Ref Oil).

### 2.9 Application Entry & Startup Runtime (68 Functions / 5,140 Lines)
* **Address Range:** `0x004a0000` - `0x004ac300`
* **Key Functions:**
  * `0x004ac2d8`: Primary executable entry point (RVA `0x000ac2d8` + ImageBase `0x00400000`).
  * VCL application initialization, `TApplication.CreateForm`, and Windows event loop.
