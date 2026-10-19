# Narrative Cutscene and Scene Gate Lifecycle Specification

## Overview

This specification details the narrative cutscene sequencing, scene gate lifecycle, and dialogue synchronization engine for the early game acts in Wonderland Online:
1. **Act 1 Shipwreck & Captain Storyline** (Maps 10014..10018 / Event 11): HUD restoration and scene unfreeze mechanics.
2. **Kran Island S. Monkey Recruitment Storyline** (Map 11016 / Quest 12002): CG movie trigger and cinematic animation synchronization.
3. **Robinson Beach Arrival Sequence** (Maps 10035/10039 / Quest 12040 / Event 8): Cutscene timeline progression, camera panning, NPC movement, posture recovery, and dialogue branching.

---

## 1. Captain Storyline & Client Scene Gate (`AC 5:4`)

### Defect Analysis
When players trigger Event 11 in Captain's Cabin (Map 10017), the server dispatches opcode 8:
```
AC 20:1 Type 5 Mode 2 [20, 1, 0, 0, 0, 3, 5, 0, 0, 0, 2, 0x7B, 0, 0, 0, 0, 0, 0]
```
In the client engine (`Main.exe`), Mode 2 invokes `FUN_004743e8(2)`, which hides all core HUD forms:
- `Form_Minimap_1`
- `Form_Chat`
- `Form_Status`
- `Form_Module_1`

Previously, `Player.CancelInteraction()` and normal dialogue completion exit paths omitted `AC 5:4` under the mistaken premise that it replayed map-entry UI. Consequently, when dialogue ended or was dismissed, the HUD remained permanently invisible until the player disconnected or changed maps.

### Technical Resolution
The server dispatches `AC 5:4` on all dialogue completion and interaction cancellation paths:
- `Player.CancelInteraction()` (`wlo.pserver.core/Game/Player.cs`)
- `AC20.Recv6` dialogue completion handler (`Src/Network/ActionCodes/AC20.cs`)
- `AC20.AdvanceBeachCutscene` Step 6 completion (`Src/Network/ActionCodes/AC20.cs`)
- `EveEventInterpreter` companion recruitment completion (`wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs`)

### Wire Protocol Framing
```
Server -> Client: AC 5:4
Byte 0..3: Header (0xF4, 0x44, 0x02, 0x00)
Byte 4:    0x05 (ActionCode 5: System/Movement)
Byte 5:    0x04 (SubCode 4: Restore Scene Gate & HUD Visibility)
```

### 1.4 Ship Storm Cutscene Handshake Protocol (`AC 186:9` -> `AC 20:6` -> `AC 20:7`)
When the Captain's dialogue concludes with Opcode 8 (`dialog4 == 31488 / 0x7B00`), the server initiates the storm cinematic sequence without artificial timers:
1. `AC 20:1 Type 5 Mode 2 Cutscene 0x7B00` (Thunder & Screen Shake Cinematic, Frame 0318).
2. `AC 186:12` (CG Movie #1 Trigger, Frame 0321).
3. The server flags `player.PlayingStormCutscene = true` and `player.StormCutsceneAckReceived = false`.

**Packet Handshake Mechanics:**
- Client returns `AC 186:9 [01, 00]` (Frame 0322) acknowledging Cutscene #1 start.
- Server responds with playback status `AC 186:9 [01, 00, 01, 00, 00, 00, 00]` (Frame 0325) and sets `player.StormCutsceneAckReceived = true`.
- Any premature `AC 20:6` received prior to the `AC 186:9` handshake is absorbed without state mutation.
- When the cutscene and animation finish rendering on the client, the client dispatches `AC 20:6` (Frame 0327).
- In [`AC20.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC20.cs#L235), `Recv6` verifies `StormCutsceneAckReceived == true`, clears the cutscene state, flags `PendingBeachCutscene = true`, emits `AC 20:7` (Frame 0328 Warp Out), and teleports the character to Rhode Island Beach (`Map 10035`, `1038, 2235`). Zero blind thread sleeps or arbitrary delays are used.

---

## 2. S. Monkey Recruitment CG Movie Trigger

### Defect Analysis
In Map 11016 (Starter Beach), interacting with S. Monkey (NPC #1, Template ID 17162) triggers Event 1. In `eve.Emg`, SubEntry 6 defines Opcode 8 with Cutscene ID 12000. Previously, `EveEventInterpreter.TryExecute` intercepted S. Monkey recruitment to directly grant the pet, but omitted both the authentic full-screen CG movie packet (`AC 186:12`) and the in-engine cinematic animation packet (`AC 20:1 Type 5`).

### Technical Resolution
Upon completion of the 16-step dialogue sequence (`OnInteractionComplete`), the server now executes:
1. `AC 186:12`: Full-screen CG movie trigger for Cutscene ID 12000.
2. `AC 20:1 Type 5`: Cinematic camera/animation frame for Cutscene ID 12000.
3. `AC 22:10`: NPC despawn frame for ClickID 1.
4. `QuestManager.SendCompanionReward`: Roster registration and pet attribute sync.
5. `QuestManager.SendQuestUpdate`: Quest 12002 (Completed) and Quest 12003 (InProgress).
6. `AC 20:10`: Fanfare audio SFX (`BGM 11011`).
7. `AC 6:2 [0]`, `AC 20:8`, and `AC 5:4`: Posture reset, movement unlock, and HUD restoration.

### Wire Protocol Framing
```
CG Movie Packet:
Server -> Client: AC 186:12
Byte 0..3: Header (0xF4, 0x44, 0x07, 0x00)
Byte 4:    0xBA (186: Media/Cutscene)
Byte 5:    0x0C (12: Movie Playback)
Byte 6..7: 0xE0, 0x2E (12000: Cutscene ID, UInt16 Little-Endian)
Byte 8..10: 0x00, 0x00, 0x00 (Padding)

Cinematic Cutscene Frame:
Server -> Client: AC 20:1 Type 5
Byte 0..3: Header (0xF4, 0x44, 0x0E, 0x00)
Byte 4:    0x14 (20: Dialogue/Cutscene)
Byte 5:    0x01 (1: Script Action)
Byte 6..8: 0x00, 0x00, 0x00 (Padding)
Byte 9:    0x01 (Step 1)
Byte 10:   0x05 (Type 5: Cutscene Animation)
Byte 11..13: 0x00, 0x00, 0x00 (Padding)
Byte 14:   0x01 (Mode 1)
Byte 15..18: 0xE0, 0x2E, 0x00, 0x00 (12000: Cutscene ID, UInt32 Little-Endian)
Byte 19..21: 0x00, 0x00, 0x00 (Padding)
```

---

## 3. Robinson Beach Arrival Sequence (`AC12`, `AC20`)

### Defect Analysis
When players warp onto South Island Beach (Map 10035 / 10039), the client plays Cutscene 12008 (lying unconscious on the beach). The sequence was previously broken by two bugs:
1. `AC12.cs` contained timer delays that invoked `AC20.AdvanceBeachCutscene(p, forceComplete: true)`. This prematurely aborted Steps 2 through 5, skipping the camera pan to Robinson and Robinson walking over to the player.
2. `AC20.cs:181` prematurely marked `p.Quests[12040] = InProgress` at Step 2. In `eve.Emg` Event 8, Robinson's first-contact rescue dialogue (SubEntry 0, TalkIDs 20304..20312) requires `Quest 12040 == NotStarted`. Setting it to `InProgress` caused `SelectMatchingBranch` to skip SubEntry 0 and select SubEntry 2 (the empty quest reminder branch), eliminating the rescue conversation.
3. At the end of the sequence, the player remained stuck in posture 1 (sitting/unconscious) with speed 0 due to an unresolved `AC 5:30 [1]` immobilize flag.

### Technical Resolution
1. **Pure Packet Progression & Direct Cutscene Trigger**: In `AC12.cs`, receiving `AC 12:1` initializes `BeachCutsceneStep = 2`, sends Emote 9, immobilize `AC 5:30 [1]`, scene staging (`AC 20:8`, `AC 22:11`, `AC 6:2 [1]`, `AC 22:12` targeting Actor 1 Robinson), and dispatches the authentic 18-byte `BuildEventFrame` for Cutscene ID 12008 (`[20, 1, 0, 0, 0, 1, 5, 0, 0, 0, 1, 12008 (uint32), 0, 0, 1]`). Previous corrupted byte alignments (misaligned speaker bytes) are eliminated.
2. **Actor Staging & Approach**: In `AC12.cs`, `AC 22:12` explicitly targets `Actor 1` (Robinson on Map 10035/10039), ensuring Robinson performs the intended rescue walk toward the player rather than targeting invalid or non-humanoid actors.
3. **Step 2-5 Progression**: Each client `AC 20:6` advances the timeline through Step 3 (`AC 24:1` + `AC 20:10`), Step 4 (`AC 20:10`), Step 5 (`AC 24:5` + `AC 20:10`), and Step 6 (`AC 22:12` targeting Actor 1 + `AC 20:10`).
4. **Quest State Preservation**: Removed premature `p.Quests[12040] = InProgress` from Step 2. Quest 12040 remains `NotStarted` until Robinson initiates the rescue dialogue.
5. **Step 6 Mobilization & Posture Recovery**:
   - Dispatches `AC 5:30 [0]` (`[5, 30, 0, CharID, 0]`) to clear the movement freeze.
   - Dispatches `AC 20:8` to unlock player controls.
   - Dispatches `AC 6:2 [0]` and broadcasts `AC 32:2` (Posture 0) to stand the player up.
   - Dispatches `AC 5:4` to restore client movement speed and unfreeze the scene gate.
   - Triggers `EveEventInterpreter.TryExecute(p, p.CurMap, 1)` to run Robinson Event 8 SubEntry 0 (TalkID 20304).

---

## 4. Robinson Duplicate Dialogue Prevention & Click Debounce

### Defect Analysis
In previous iterations, Robinson on Map 10035/10039 engaged the player in two disjointed dialogues (first his rescue speech TalkIDs 20304..20312, followed by the dialogue closing, and immediately re-opening with Beginner's Guide choices TalkIDs 20317..20326). Root-cause analysis revealed:
1. **Disjointed Event Branches**: In `eve.Emg` Event 8, Branch 1 (SubEntry 0) concludes by setting Quest 12040 to Step 1 (`Opcode 5`), and calling `finish()`. This closed the window, fired `CancelInteraction()`, and sent `AC 20:8` + `AC 5:4`.
2. **Immediate Hitbox Re-click**: The player cursor, resting on Robinson's sprite, immediately submitted an `AC 20:1` packet to the server, which matched Branch 3 (SubEntry 2, requiring Quest 12040 Step 1), causing a second jarring dialogue popup.

### Technical Resolution
1. **Recv1 Interaction State & Debounce Guards**: In `AC20.cs:Recv1`, all incoming `AC 20:1` packets are logged with detailed diagnostic telemetry. Clicks arriving during cutscenes (`PlayingStormCutscene`, `BeachCutsceneActive`), active EVE sessions (`NativeEventActive`), or within the `NpcClickResumeAt` window are rejected with `AC 20:8` and logged with the exact millisecond cooldown remaining.
2. **Seamless In-Session Branch Auto-Transition**: In `EveEventRuntime.cs`, when Robinson's Event 8 Branch 1 completes, rather than invoking `finish()` and abruptly terminating the session, the engine detects Map 10035/10039 Event 8 Branch 1 and seamlessly transitions directly to Branch 3 (Question 1: Beginner's Guide) within the same uninterrupted dialogue session.
3. **EVE Opcode Tracing & Wire Telemetry**: Added per-step tracing in `EveEventRuntime.advance()` outputting opcode type, actor targets, speech TalkIDs, choice questions, and cutscene movie values to the debug log stream.

---

## 5. Verification and Edge Cases

### Edge Cases Handled
1. **Dialogue Interruption / Window Close**: If a player cancels dialogue via `CancelInteraction()`, `AC 5:4` is guaranteed to fire, preventing HUD disappearance.
2. **Packet Drop During Beach Cutscene**: If the client fails to return `AC 20:6` ACKs during Steps 2..5, the 15-second watchdog in `AC12.cs` gracefully mobilizes the player and starts the Robinson dialogue without leaving the player stranded.
3. **Repeated S. Monkey Interaction**: If a player clicks S. Monkey after recruitment, `EveEventInterpreter` sends squeak dialogue `#20038` and unfreezes the scene gate via `AC 5:4`.
4. **Multi-Map Beach Compatibility**: Supports both Map 10035 (Rhode Island / South Island Shore) and Map 10039.
5. **Trailing Click Suppression**: Closing dialogue bubbles over an NPC hitbox cannot re-trigger subsequent quest branches within the debounce window. Legitimately re-clicking the NPC after the window correctly opens the subsequent quest branch.
