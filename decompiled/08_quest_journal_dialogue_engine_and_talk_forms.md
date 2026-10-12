# Quest Journal, Dialogue Engine, and Talk Forms Specification

## 1. Dialogue & Quest Memory Layout

The client manages dialogue states, text balloons, and quest entries using the following memory offsets:

| Offset | Type | Field Name | Description & Functional Operation |
|---|---|---|---|
| `+0x31a` | `char` | `DialogActiveFlag` | `1` if conversation UI is open, `0` if closed |
| `+0x31b` | `byte` | `DialogSeq1` | Sequence tracker for Option 1 bindings |
| `+0x31c` | `byte` | `DialogSeq2` | Sequence tracker for Option 2 bindings |
| `+0x38c` | `char` | `PanelSkinCode` | Dialogue panel style skin (`Form_Talk_3` vs `panel22`) |
| `+0x331` | `char` | `CutsceneState` | Bypasses dialogue choices during active cutscene replay |
| `+0x30c` | `pointer` | `ActiveOptionPtr` | Pointer to selected dialogue option structural node |
| `+0x2318` | `pointer` | `DialogueTextNode` | Active text block mapping |
| `+0x2130` | `int` | `BalloonX` | Horizontal screen layout coordinate for text balloons |
| `+0x2134` | `int` | `BalloonY` | Vertical screen layout coordinate for text balloons |

---

## 2. Key Decompiled Subroutines

### 2.1 Quest Journal Form Loader (`FUN_0030859c`)
* **Signature:** `void FUN_0030859c(int *param_1, char param_2)`
* **Logic:**
  1. Instantiates the UI window panel identified as `"form_taskview_1"`.
  2. Binds scrollbars (`bar_H4`, `rail_H4`) and navigation buttons (`Arrow_L3`, `Arrow_R3`).
  3. Formats pagination label: `"[Page] %d/%d"`.
  4. Binds window control buttons (`Btn_Close_1`) and quest abandon button (`Btn_Delete_1`).
  5. Dispatches network packet: **Opcode 39, Sub-opcode 1** (`AC 39:1`) to query player quest registry.

### 2.2 NPC Dialogue Form Manager (`FUN_004896f4`)
* **Signature:** `void FUN_004896f4(int *param_1, undefined4 param_2, int param_3, undefined1 param_4, char param_5)`
* **Logic:**
  1. Inspects dialog classification flag `*(char *)(*(int *)PTR_DAT_004c91d4 + 0x1d)`.
  2. If dialog is informational without branching choices, instantiates **`Form_Talk_3`** (or **`panel22`** if skin `+0x38c == 1`) to render a text balloon.
  3. If branching choices exist, instantiates **`Form_Talk_2`** or **`Form_Talk_1`** containing clickable option buttons.
  4. Sets active conversation flag `+0x31a = 1`.

### 2.3 Special Dialogue Avatars (`FUN_0040d550`)
* **Signature:** `void FUN_0040d550(int param_1)`
* **Logic:**
  Loads specialized large dialogue avatar interface `"TalkForm1"`, computing text wrapping boundaries and positioning speaker character portraits relative to NPC world coordinates.

---

## 3. Network Protocol Packet Format (`Opcode 39` / `0x27`)

| Sub-Opcode | Action Name | Direction | Payload Structure |
|---|---|---|---|
| `39:1` | Request Quest List | Client $\to$ Server | Empty payload |
| `39:7` | Abandon Quest | Client $\to$ Server | `QuestID: UInt32` (`FUN_0041892c`) |
| `39:10` | Query Quest Status 10 | Client $\to$ Server | `QuestID: UInt32` (`FUN_0041898c`) |
| `39:11` | Query Quest Status 11 | Client $\to$ Server | `QuestID: UInt32` (`FUN_0041896c`) |
| `39:12` | Guild Member Details | Client $\to$ Server | `GuildID: UInt32` (`FUN_0041894c`) |
| `39:16` | Quest Status Filter | Client $\to$ Server | `Filter1: Byte`, `Filter2: Byte`, `QuestID: UInt32` |
