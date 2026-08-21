# Dialogue Speaker and Portrait Protocol (`AC 20:1`)

## Overview
Documents how the Wonderland Online client resolves speaker portraits and NPC identity headers from `AC 20:1` dialogue packets based on `eve.dat` bytecode opcodes.

---

## Protocol Specification

### Packet Layout (`AC 20:1`)
- `Byte 0`: `20` (Action Code)
- `Byte 1`: `1` (Subcode: Dialogue)
- `Bytes 2-4`: `00 00 00` (Session padding)
- `Byte 5`: `StepNum` (1-indexed sequence counter)
- `Byte 6`: `01` (Fixed dialogue mode) or `06` (Multiple choice selection prompt)
- `Byte 7`: `PortraitMode`:
  - `3`: NPC Portrait Frame
  - `7`: Player Portrait Frame
- `Byte 8`: `SpeakerClickID`:
  - When `PortraitMode == 3`: The specific `ClickID` on the current map whose name and portrait sprite are displayed in the dialog window header (e.g. `ClickID 1` for Robinson Crusoe, `ClickID 8` for Burke the Tiger).
  - When `PortraitMode == 7`: Set to `0` (Player character name and face).
- `Byte 9`: `00`
- `Bytes 10-13`: `01 00 00 00` (Flags)
- `Byte 14`: `00`
- `Bytes 15-17`: `TalkID` (24-bit Little Endian integer referencing localized dialog strings in client `Talk.dat`).

---

## `eve.dat` Bytecode Opcode Mapping

| Opcode | `dialog1` | `dialog2` | `dialog3` | Speaker Target | `PortraitMode` | `SpeakerClickID` |
|---|---|---|---|---|---|---|
| **Opcode 2** | `NPC ClickID` (e.g. 1) | `1` (NPC) | `TalkID` | Specified NPC (e.g. Robinson) | `3` | `op.dialog1` |
| **Opcode 2** | `NPC ClickID` (e.g. 8) | `1` (NPC) | `TalkID` | Specified NPC (e.g. Burke) | `3` | `op.dialog1` |
| **Opcode 2** | `Any` | `2` (Player) | `TalkID` | Player Character | `7` | `0` |
| **Opcode 1** | `2` (Player) | `TalkID` | `1` | Player Character | `7` | `0` |
| **Opcode 1** | `NPC ClickID` | `TalkID` | `1` | Specified NPC | `3` | `op.dialog1` |

---

## Code References
- Handled in [`EveEventInterpreter.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs) within `RunSubOpcodes` and `ExecuteOpcode`.
