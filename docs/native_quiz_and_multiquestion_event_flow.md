# Native Quiz & Multi-Question Choice Event Flow

## Overview
This document details the fully generic, data-driven execution flow for multi-step dialogues, nested choice prompts (quizzes/riddles), and outcome reward branch matching in the `EveEventInterpreter`.

## Protocol & Bytecode Architecture

### 1. Dialogue Step Progression
- **Advancement Protocol**: Dialogue progression is triggered via the authentic `AC 20:6` (`14 06`) packet sent when the client clicks the next dialogue hand icon, presses Enter, or presses Space.
- **Packet Queue**: Pending dialogue steps are enqueued in `Player.QueueData`. Each step is dispatched via the secure, encrypted `Player.Send(SendPacket)` pipeline upon receiving `AC 20:6`.
- **Emote Handling**: `AC 32:2` handles mouse hover and emote animation broadcasts and does not interfere with the dialogue queue.

### 2. Nested Choice & Quiz Branching
- **Choice Opcode**: Identified universally by `op.DialogPtr == 2 && op.dialog2 == 6`.
- **Question Layout Packet**:
  - `AC 20:1` with `fixed = 6` (choice mode).
  - Byte 15: Choice ID (`op.dialog3 & 0xFF`).
  - Byte 17: Layout flags (`0x01`).
- **Choice Callback**: When `AC 20:9` is received, the selected choice index (e.g., `0x1F` / 31) maps to the matching `unknownbyte1 == 7` branch for that `curQuestionId`.

### 3. Outcome & Reward Branch Resolution
When a choice branch terminates without scheduling further questions:
- **Success Identification**: If the selected choice satisfies the quiz conditions (`unknownword2 == targetChoiceVal` or `targetChoiceVal == 31`), the interpreter searches the parent event for the reward branch:
  - `s.unknownword1 == questId`
  - `s.unknownword4 == 773` (`0x0305` — Official WLO Success Reward flag) or `(s.unknownword4 & 0x0300) == 0x0300`.
- **Reward Execution**: The reward branch automatically executes its opcodes, including item grants (`DialogPtr == 1, dialog1 == 1, dialog3 == itemID`), flag updates, and completion dialogues.
- **Zero Hardcoding**: All quest IDs, question IDs, choice branches, and item rewards are resolved dynamically from `Eve.EMG` and `Talk.dat` binary structures.
