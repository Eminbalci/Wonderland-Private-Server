# Ship Captain Storm Cutscene & Scene Transition Protocol

## Overview
Documents the reverse-engineered initial ship storm cutscene and chapter transition sequence driven dynamically by `Eve.emg` event opcodes and synced with the client via `AC 186` / `AC 20`, verified against `ilkgorevinanimasyonlukisimlari.pcapng`.

---

## Bytecode & Opcode Architecture (`Eve.emg` Map 10017 Event 11)

1. **Opcode 2 (Dialogue Steps 1 & 2)**:
   - `Ptr=2, d1=10, d2=1, d3=30124` -> Captain TalkID 30124 (0x75AC).
   - `Ptr=2, d1=10, d2=1, d3=30125` -> Captain TalkID 30125 (0x75AD).
2. **Opcode 8 (CG Movie #1 & Thunder SFX)**:
   - `Ptr=8, d1=2, d2=0, d3=0, d4=31488 (0x7B00)`:
     - `d4` MSB (`0x7B` = 123) is the thunder/storm rumble sound in `Wav.dat`.
     - Server dispatches `AC 186:12 [01 00 00 00 00]` (Cutscene Movie ID 1 initialization) and `AC 20:1 Step 3` (`14 01 00 00 00 03 05 00 00 00 02 7B 00 00 00 00 00 00`).
     - Marks `player.PlayingStormCutscene = true`.
3. **Client-Synchronized Cutscene Movie (`AC 186:9` & `AC 20:6`)**:
   - Client sends `AC 186:9 [01 00]` acknowledgment.
   - Server responds with `AC 186:9 [01 00 01 00 00 00 00]` playback confirmation.
   - Client plays the full ~22-second in-engine storm cutscene (CG Movie, Thunder SFX, violent screen shaking, character fear reactions).
   - When the client finishes the cutscene, it sends `AC 20:6` (`14 06`).
   - Server catches `AC 20:6` via `PlayingStormCutscene` check in `AC20.Recv6`:
     - Clears `PlayingStormCutscene = false`.
     - Marks `PendingBeachCutscene = true`.
     - Dispatches `AC 20:7` (`14 07` Warp Out).
     - Teleports player to Shipwreck Beach (Map 10035 pos `1038, 2235`).
4. **Beach Arrival Cutscene (`AC 12:1`)**:
   - Once on Map 10035, `AC 12:1` triggers the Robinson rescue scene, waking the player up, rendering camera pan and NPC animations, and registering Quest #12040.

---

## Code References
- Handled in [`EveEventInterpreter.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs) (Opcode 8).
- Cutscene synchronization & completion warp in [`AC20.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC20.cs) and [`AC186.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC186.cs).
- Beach arrival wake-up sequence in [`AC12.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC12.cs).
