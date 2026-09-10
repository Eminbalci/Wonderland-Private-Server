# AC 186 Cutscene Loop and NPC Dialogue Repetition Resolution Specification

## 1. Overview
This document specifies the root causes, official PCAP evidence, and technical implementations resolving two critical server-client desynchronization issues:
1. **Infinite `AC 186` Cutscene Packet Flood**: Continuous 50-100 pkt/s ping-pong exchange of `AC 186:9` and `AC 186:12` packets between the client and server following cutscene playback.
2. **NPC Dialogue Looping / Repetition**: NPCs on maps (e.g. Lina NPC #27, Doll NPC #12 on Map 12000) continually repeating their initial dialogue step without advancing quest progression or responding to user dialogue progression.

---

## 2. Root Cause Analysis

### 2.1 Infinite `AC 186` Ping-Pong Flood
- **Faulty Code Location**: [Src/Network/ActionCodes/AC186.cs](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC186.cs)
- **Mechanism**:
  1. The server initiates a cutscene (e.g., Storm Cutscene on Map 10017) by sending `AC 186:12` (`ba 0c 01 00 00 00 00`).
  2. The client plays the cutscene and informs the server that playback has concluded by sending `AC 186:9` (`ba 09 <cutsceneId>`).
  3. In `AC186.Recv9`, the server correctly acknowledged playback with `AC 186:9` (`ba 09 <cutsceneId> 01 00 00 00 00`), but erroneously re-dispatched `AC 186:12` immediately afterwards.
  4. Receiving an unprompted `AC 186:12` causes the WLO client to immediately fire another `AC 186:9` acknowledgment packet back to the server.
  5. This resulted in an uncontrolled, high-frequency recursive feedback loop (over 4,800 packets logged in a single session) saturating the TCP socket and starving client-side event processing.

### 2.2 NPC Dialogue Repetition & Stuck State
- **Faulty Code Locations**:
  - [wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs)
  - [Src/Network/ActionCodes/AC20.cs](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC20.cs)
- **Mechanisms**:
  1. **Destructive Quiz Fallback in Choice Processing**:
     - In `EveEventInterpreter.cs`, when a player clicked an option in an interactive dialogue choice prompt (`player.OnDialogueChoice`), the interpreter executed the choice branch (`RunSubOpcodes(choiceSub)`).
     - However, the code assumed every choice was a multi-question quiz. If no dedicated quiz outcome branch (`unknownword4 == 773` or `769`) matched, it executed:
       `resultBranch = SelectMatchingBranch(player, map, clickId, eventEntry, choiceSub);`
     - Because quest flags are not committed until dialogue concludes, `SelectMatchingBranch` re-selected Sub #1 (the unstarted quest branch) and immediately invoked `RunSubOpcodes(Sub #1)`, purging queued dialogue and re-sending Step 1.
  2. **Unexecuted Post-Dialogue Opcodes (`OnInteractionComplete`)**:
     - For choice branches, `player.OnInteractionComplete` was never hooked up.
     - As a consequence, deferred action opcodes (such as Opcode 5 setting quest status to `InProgress`, or Opcode 1 granting quest starter items) never ran when the player finished reading. The quest state remained permanently unstarted in memory and database.
  3. **Byte 17 Protocol Discrepancy (`AC 20:1`)**:
     - The server packed `(byte)((talkId24 >> 16) & 0xFF)` at Byte 17, which encoded the NPC ClickID (e.g. 27).
     - Official PCAP cross-analysis demonstrates Byte 17 strictly encodes the 1-indexed `SubEntry` number (`sub.subIndex`, e.g. 0x01, 0x03, 0x07). Out-of-range sub-indices caused the client script engine to fail state-machine attachment.
  4. **Speaker ClickID Desynchronization for Player Portraits**:
     - When `portrait == 7` (Player portrait window), the server transmitted `speakerClickId = (byte)clickId`.
     - Official PCAP captures establish that for player portraits, `speakerClickId` must be strictly `0`.
  5. **Missing Dialogue UI Mode Synchronization (`AC 6:2 [1]`)**:
     - The server sent `AC 6:2 [0]` upon dialogue completion to restore HUD and controls, but never dispatched `AC 6:2 [1]` at interaction onset. Official traffic confirms `AC 6:2 [1]` is required to lock input and engage the client dialogue progression pipeline.

---

## 3. Official PCAP Traffic Verification

Analysis of official Wireshark captures (`brelliatlayerdegistirdim.pcapng`, `2tanenpcylekonustumikisinindegoreviyoktu.pcapng`, `ilkgorevtamami.pcapng`, `robinsonlakonusma.pcapng`):

### 3.1 `AC 186` Sequence
- Server -> Client: `AC 186:12` (Cutscene start / camera director)
- Client -> Server: `AC 186:9`  (`ba 09 <cutsceneId>`)
- Server -> Client: `AC 186:9`  (`ba 09 <cutsceneId> 01 00 00 00 00`)
- Stream ends cleanly; no `AC 186:12` returned.

### 3.2 Dialogue Initiation Sequence
- Client -> Server: `AC 20:1` `[ClickID (2B)]`
- Server -> Client: `AC 6:2`  `[1]` (Enter dialogue UI state)
- Server -> Client: `AC 20:1` `[18 bytes authentic dialogue packet]`

### 3.3 `AC 20:1` Byte Layout (18 Bytes Total)
- `[0]`: Action Code `0x14` (20)
- `[1]`: SubCode `0x01` (1)
- `[2..4]`: Session Padding `0x00, 0x00, 0x00`
- `[5]`: Step Number (`1, 2, 3...`)
- `[6]`: Dialog Subtype `0x01` (Dialogue line) / `0x06` (Choice prompt)
- `[7]`: Portrait Window `0x03` (NPC) / `0x07` (Player)
- `[8]`: Speaker ClickID (`0x00` if Portrait == 7; NPC ClickID if Portrait == 3)
- `[9]`: Padding `0x00`
- `[10..13]`: Flags (`0x01, 0x00, 0x00, 0x00` for Dialogue / `0x00, 0x00, 0x00, 0x00` for Choice)
- `[14]`: Padding `0x00`
- `[15]`: Talk / Choice ID LSB (`talkId & 0xFF`)
- `[16]`: Talk / Choice ID MSB (`(talkId >> 8) & 0xFF`)
- `[17]`: SubEntry Index (`(byte)(sub != null ? sub.subIndex : 1)`)

---

## 4. Implementation Details

### 4.1 [`Src/Network/ActionCodes/AC186.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC186.cs)
- Removed duplicate `resp12` dispatch inside `Recv9`.
- Acknowledges cutscene completion with `AC 186:9` without triggering client re-request loops.

### 4.2 [`wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs)
- **Session Cleanup**: Reset `player.OnDialogueChoice = null;` and `player.OnInteractionComplete = null;` at start of `ProcessInteraction`.
- **UI Mode Sync**: Dispatched `Tools.FromFormat("bbb", 6, 2, 1)` prior to sending the first dialogue or choice packet; ensured `Tools.FromFormat("bbb", 6, 2, 0)` is sent on completion.
- **Byte 17 Formatting**: Replaced `(talkId24 >> 16)` with `(byte)(sub != null ? sub.subIndex : 1)` across `RunSubOpcodes`, `ExecuteOpcode`, and `BuildDialoguePacket`.
- **Speaker Normalization**: Forced `speakerClickId = 0` whenever `portrait == 7`.
- **Choice Completion Engine**:
  - Cleared `postDialogueOpcodes` prior to running choice sub-branch.
  - Eliminated the destructive `SelectMatchingBranch` fallback. Restricted outcome branch checks strictly to events containing quiz marker words (`unknownword4 == 773` or `769`).
  - Registered `player.OnInteractionComplete` to execute all deferred action opcodes (e.g. Opcode 5 quest state updates), close dialogue (`AC 20:8`), and release controls (`AC 5:4`).

### 4.3 [`Src/Network/ActionCodes/AC20.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC20.cs)
- Added `Tools.FromFormat("bbb", 6, 2, 0)` in `Recv6` when interaction completes to guarantee full UI unlock.

---

## 5. Empirical Verification Suite
An end-to-end automated verification suite was developed and executed in [`dev_scripts/VerifyDialogueAndAc186.cs`](file:///D:/GitHub/Wonderland-Private-Server/dev_scripts/VerifyDialogueAndAc186.cs) against real binary data (`Data/eve.Emg` and `Data/Talk.dat`):

### Results Summary
```
=================================================
STARTING EMPIRICAL VERIFICATION SUITE
=================================================

--- Testing Group 1: AC 186 Cutscene Acknowledgment & Flood Prevention ---
[PASS] Test #1: AC186.Recv9 sends exactly one acknowledgment packet
[PASS] Test #2: Sent packet is AC 186 Subcode 9 (CG playback ack)
[PASS] Test #3: Server NEVER re-dispatches AC 186:12 (Infinite loop prevention verified)

--- Testing Group 2: AC 20:1 Dialogue Packet Structure ---
[PASS] Test #4: Loaded eve.Emg successfully
[PASS] Test #5: EveEventInterpreter.TryExecute returned true for Map 12000 NPC #27 'Lina'
[PASS] Test #6: Dispatched AC 6:2 [1] (Dialogue UI lock mode) before Step 1
[PASS] Test #7: Dispatched AC 20:1 dialogue Step 1
[PASS] Test #8: AC 20:1 packet length is exactly 18 bytes (Official specification)
[PASS] Test #9: Dialogue step is 1
[PASS] Test #10: Dialogue subtype is 1 (normal speech)
[PASS] Test #11: Lina portrait is 3 (NPC)
[PASS] Test #12: Lina speaker ClickID is 27
[PASS] Test #13: TalkID is 30601 ('My little dog is missing'), got 30601
[PASS] Test #14: Byte 17 is 1 (SubEntry 1 index, NOT clickId 27), got 1
[PASS] Test #15: QueueData contains 3 queued steps (Steps 2, 3, and Choice Step 4)
[PASS] Test #16: Step 2 TalkID is 30234 ('What happened?'), got 30234
[PASS] Test #17: Step 2 Portrait is 7 (Player speech)
[PASS] Test #18: Step 2 Speaker is strictly 0 for Player portrait (Official PCAP rule), got 0
[PASS] Test #19: Step 2 SubIndex is 1, got 1
[PASS] Test #20: Step 4 Subtype is 6 (Choice Prompt)
[PASS] Test #21: Step 4 SubIndex is 1, got 1
[PASS] Test #22: player.OnDialogueChoice callback is actively registered

--- Testing Group 3: Dialogue Choice Selection & Quest State Transition ---
[PASS] Test #23: Dispatched choice response dialogue packet
[PASS] Test #24: Server did NOT re-run Sub #1 initial dialogue (TalkID is 11030)
[PASS] Test #25: Response is Sub #3 (Accepted branch), got SubIndex 3
[PASS] Test #26: player.OnInteractionComplete is registered for choice completion
[PASS] Test #27: Deferred Opcode 5 executed: Quest #13046 is now InProgress in player.Quests!
[PASS] Test #28: Dispatched AC 6:2 [0] (Restore HUD / Exit Dialogue UI)
[PASS] Test #29: Dispatched AC 20:8 (Close dialogue window)
[PASS] Test #30: Dispatched AC 5:4 (Restore player movement)

--- Testing Group 4: Second NPC Interaction (No Looping) ---
[PASS] Test #31: Second click on Lina processed successfully
[PASS] Test #32: Second interaction sent dialogue packet
[PASS] Test #33: Lina did NOT repeat Step 1 ('My dog is missing')! TalkID is #30604
[PASS] Test #34: Lina correctly executed InProgress branch Sub #4, got SubIndex 5
   -> Second Dialogue Text: 'My little dog is always hungry. I think it might be looking for food in village. Can you help me find it?'

=================================================
RESULTS: 34 / 34 TESTS PASSED (100%)
=================================================
```
