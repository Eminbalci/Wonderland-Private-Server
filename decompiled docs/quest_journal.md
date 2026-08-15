# Quest Journal & Dialogue Decompiled Specifications

This document outlines the client-side quest lists, dialogue managers, form loader structures, and quest sub-opcode frames extracted from `alogin_analyzed/quest_journal.c`.

---

## 1. Memory Offsets & UI Form Registers

During dialogue layout rendering and quest tracking, the client utilizes the following offsets relative to player/interface pointers:

| Offset | Data Type | Field / Purpose | Description & Actions |
|---|---|---|---|
| `+0x31a` | `char` | Dialog Active Flag | `1` if conversation is active, `0` if closed |
| `+0x31b` | `byte` | Dialog Sequence 1 | Sequence validation tracker for Option 1 bindings |
| `+0x31c` | `byte` | Dialog Sequence 2 | Sequence validation tracker for Option 2 bindings |
| `+0x38c` | `char` | Panel Skin Code | Toggles dialogue panels (`panel22` vs `Form_Talk_3`) |
| `+0x331` | `char` | Direct Cutscene State | Bypasses option checks if active |
| `+0x30c` | `pointer` | Active Dialogue Option | Pointer to the chosen dialogue option structural properties |
| `+0x2318` | `pointer` | Dialogue Text Node | Active text block mapping |
| `+0x2130` | `int` | Dialogue Position X | Horizontal layout offset for text balloons |
| `+0x2134` | `int` | Dialogue Position Y | Vertical layout offset for text balloons |

---

## 2. Key Decompiled Functions

### `FUN_0030859c` (Quest Journal Form Loader)
- **C Signature**: `void FUN_0030859c(int *param_1, char param_2)`
- **Logic**:
  1. Instantiates the UI window panel identified as `"form_taskview_1"`.
  2. Binds scrollbars (`bar_H4` / `rail_H4`) and navigation buttons (`Arrow_L3` / `Arrow_R3`).
  3. Formats pages counter text block using label string mapping `[Page] %d/%d`.
  4. Binds actions close (`Btn_Close_1`) and task discard (`Btn_Delete_1`).
  5. Dispatches Opcode `39` (0x27) Sub-opcode `1` to retrieve the current active quest list.

### `FUN_004896f4` (NPC Dialogue Form Manager)
- **C Signature**: `void FUN_004896f4(int *param_1, undefined4 param_2, int param_3, undefined1 param_4, char param_5)`
- **Logic**:
  1. Identifies the NPC's dialog type flag `*(char *)(*(int *)PTR_DAT_004c91d4 + 0x1d)`.
  2. If the dialog has no branching choices, instantiates **`Form_Talk_3`** (or **`panel22`** if `+0x38c` skin code is `1`) to render a text bubble/balloon.
  3. If branching choices exist, instantiates **`Form_Talk_2`** (or **`Form_Talk_1`** depending on layout sizes) containing clickable buttons for choice options.
  4. Stores active dialogue status (`+0x31a` = `1`) and registers choice sequences in the player structure.

### `FUN_0040d550` (Quest Special Dialogue Form Loader)
- **C Signature**: `void FUN_0040d550(int param_1)`
- **Logic**:
  1. Checks if a valid quest talk block is registered at `param_1 + 0x2318`.
  2. Resolves and loads the specialized UI panel named `"TalkForm1"`.
  3. Computes layout bounds for text rendering depending on string length.
  4. Positions character avatars and updates layout coordinates (`+0x2130`/`+0x2134`) relative to the NPC's screen coordinate base.

---

## 3. Network Protocol Packet Format

Quest interactions and abandon actions use Opcode **`39`** (`0x27`).

### Quest Action Packets (Client -> Server)
- **Opcode**: `39` (0x27)
- **Sub-opcodes**:
  - `1` (Request Quest List): Payload is empty.
  - `7` (Abandon Quest): Payload = Quest ID (4 bytes) (`FUN_0041892c`).
  - `10` (Query Status 10): Payload = Quest ID (4 bytes) (`FUN_0041898c`).
  - `11` (Query Status 11): Payload = Quest ID (4 bytes) (`FUN_0041896c`).
  - `12` (Guild Members Details): Payload = Guild ID (4 bytes) (`FUN_0041894c`).
  - `16` (Status Filter Record): Payload = Filter Param 1 (1 byte) + Filter Param 2 (1 byte) + Quest ID (4 bytes) (`FUN_0041a110`).
