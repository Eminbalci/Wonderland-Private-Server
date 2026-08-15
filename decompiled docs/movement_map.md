# Harita ve Yürüme Algoritmaları Decompiled Specifications

This document outlines the client-side map loading boundaries, MapData XOR decryption, NPC range verifications, and visual entity synchronization extracted from `alogin_analyzed/movement_map.c`.

---

## 1. Memory Offsets & Grid Boundaries

The client translates harita (map) grid dimensions and visual coordinate structures using the following offsets:

| Offset | Data Type | Representation / Purpose | Calculation Logic |
|---|---|---|---|
| `+0x14` | `int` | Grid Width | Pixel Width divided by Block Size (`+0x38`) |
| `+0x18` | `int` | Grid Height | Pixel Height divided by Block Size (`+0x38`) |
| `+0x38` | `byte` | Block Size | Grid scaling denominator (e.g. `20` pixels) |
| `+0x27e8` | `pointer` | Mount Coordinates Array | Base address array for companion/mount visual positioning |
| `+0x20` | `int` | Entity Position X | Visual X coordinate inside mount struct |
| `+0x24` | `int` | Entity Position Y | Visual Y coordinate inside mount struct |
| `+0x20b5` | `char` | Fishing State Flag | If `\x01`, character is currently fishing |
| `+0x20f6` | `char` | Collecting State Flag | If `\x01`, character is currently gathering/collecting |
| `+0x2231`-`3` | `char` | Stall Modes | Non-zero values block NPC/PK actions |

---

## 2. Key Decompiled Functions

### `FUN_0032d924` (Map Dimension Loader)
- **C Signature**: `void FUN_0032d924(int param_1, int param_2, int param_3)`
- **Logic**:
  1. Computes Grid Width (`+0x14`) and Grid Height (`+0x18`) by dividing pixel arguments by the scaling Block Size `+0x38`.
  2. Saves boundary values to local buffers at `+0x1c` and `+0x20`.
  3. Pre-allocates harita grid arrays inside buffer memory (`param_1 + 8`).

### `FUN_0032cd88` (MapData Loader 1 - XOR Decrypter)
- **C Signature**: `void FUN_0032cd88(int param_1)`
- **Logic**:
  1. XOR decrypts the selected Map ID value using key **`0x190c`** (6412) to format the destination file name.
  2. Reads map files from path `"user\Map\<XOR_MapID>.MapData"`.
  3. Loops through MapData using 36-byte records (`0x24`) to parse monster spawn regions, trigger bounds, and obstacle collision flags.
  4. Renders tiles using blit operations (`ro_Normal_Blt`) scaled by Block Size.

### `FUN_0032d5b8` (MapData Loader 2 - Portal Parser)
- **C Signature**: `void FUN_0032d5b8(int param_1)`
- **Logic**:
  1. Verifies if `"user\Map\"` directory exists. If missing, automatically creates the directory.
  2. Parses doorway portals, warp destination map IDs, and coordinates from the decrypted MapData stream.
  3. Marks walking path restrictions (non-walkable grids).

### `FUN_0031d874` (NPC Distance & Interaction Checker)
- **C Signature**: `void FUN_0031d874(int param_1)`
- **Logic**:
  1. Blocks clicks if character is busy: checks `+0x20b5` (alert: `"Fishing, can't act"`) or `+0x20f6` (alert: `"Collecting, can't act"`).
  2. Verifies Stall states (`+0x2231`-`+0x2233`) and busy codes.
  3. Calculates distance delta:
     $$\Delta X = |X_{\text{player}} - X_{\text{target}}| \le 169 \text{ pixels } (0xa9)$$
     $$\Delta Y = |Y_{\text{player}} - Y_{\text{target}}| \le 169 \text{ pixels } (0xa9)$$
     If either delta exceeds **169 pixels**, the action is cancelled.
  4. If verified, dispatches interaction request Opcode `20`, Sub-opcode `1`.

### `FUN_0015013c` (Visual Coordinates Sync)
- **C Signature**: `void FUN_0015013c(int param_1, uint param_2)`
- **Logic**:
  1. Retrieves active mount database index `param_2`.
  2. Extracts mount coordinates from player array `player_ptr + 0x27e8 + param_2 * 4` (X at offset `+0x20`, Y at offset `+0x24`).
  3. Writes coordinate values directly to visual entity offsets `+0xe0` and `+0xe4` to align character and mount sprites.
