# Ship Captain Storm Cutscene & Scene Transition Protocol

## Overview
Documents the reverse-engineered initial ship storm cutscene and chapter transition sequence driven dynamically by `Eve.emg` event opcodes and synced with the client via `AC 186` / `AC 20`.

---

## Bytecode & Opcode Architecture (`Eve.emg` Map 10017 Event 11)

1. **Opcode 2 (Dialogue Steps 1 & 2)**:
   - `Ptr=2, d1=10, d2=1, d3=30124` -> Captain TalkID 30124 (0x75AC).
   - `Ptr=2, d1=10, d2=1, d3=30125` -> Captain TalkID 30125 (0x75AD).
2. **Opcode 8 (CG Cutscene & Thunder SFX)**:
   - `Ptr=8, d1=2, d2=0, d3=0, d4=31488 (0x7B00)`:
     - `d4` MSB (`0x7B` = 123) is the thunder/storm rumble sound in `Wav.dat`.
     - `EveEventInterpreter` dispatches `AC 186:12` (Cutscene ID 1) and `AC 20:1 Step 3` (Sound channel `d1`, SoundID `0x7B`).
3. **ActionCode 186 Synchronization (`AC 186:9`)**:
   - Client sends `[AC=186][Sub=9][cutsceneId=1 (2B)]`.
   - Server acknowledges via `AC186.cs` with `[AC=186][Sub=9][cutsceneId (2B)][status=1 (1B)][reserved=0 (4B)]`.
4. **Opcode 1 (Dynamic Scene / Chapter Transition)**:
   - `Ptr=1, d1=3, d2=1, d3=0, d4=0`:
     - In `Eve.emg`, `DialogPtr == 1, dialog1 == 3` defines story scene/chapter transitions.
     - `d2 == 1` on starter ship maps resolves transition to **Map 10035 (Shipwreck Beach / Desert Island)** at `X: 1038, Y: 2235`.
     - `OnInteractionComplete` registers the warp and marks `p.PendingBeachCutscene = true`.
5. **Beach Arrival Cutscene (`AC 12:1`)**:
   - Once on Map 10035, the client initiates `AC 12:1`, triggering Robinson's rescue scene, waking player up (`TalkID 12008`), and registering Quest #12040.

---

## Code References
- Handled in [`EveEventInterpreter.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs) (Opcodes 1, 2, 8).
- Cutscene synchronization in [`AC186.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC186.cs).
- Beach arrival wake-up sequence in [`AC12.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC12.cs).

