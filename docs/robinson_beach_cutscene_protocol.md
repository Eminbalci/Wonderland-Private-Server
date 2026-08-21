# Robinson Beach Rescue Cutscene Protocol (Map 10035 / Quest 12040)

## Overview
When a player experiences the shipwreck cutscene on the ship deck (Map 10017) or warps to the deserted island beach (Map 10035) for the first time without having completed or started Quest 12040, the authentic Robinson Crusoe rescue cutscene automatically triggers.

---

## StepQueue Architecture

Because cutscene steps in Wonderland Online involve multiple discrete TCP packets per dialog/camera event (e.g. animation opcode `AC 22:12` paired with dialog progression `AC 20:10`), the server utilizes `p.StepQueue` (`Queue<Action>`):

1. **Initial Trigger (PCAP Frame 0333)**:
   - Sets player emote to Lying Down (`AC 32:2 9`).
   - Clears screen (`AC 20:8`).
   - Focuses camera on Robinson NPC (`AC 22:11 6 FF FF`).
   - Locks player movement (`AC 6:2 1`).
   - Dispatches `AC 20:11` and `AC 20:10` to initiate the step progression loop.

2. **Step 1 (PCAP Frame 0337)**:
   - Robinson caring animation (`AC 22:12 2 11 0 5`).
   - Sends `AC 20:10`.

3. **Step 2 (PCAP Frame 0339)**:
   - Robinson wake-up dialogue (`AC 20:1` with TalkID `12008` / `0x2EE8`).

4. **Step 3 (PCAP Frame 0344)**:
   - Quest 12040 Start (`AC 24:1 0x08 0x2F 0x01`).
   - Sends `AC 20:10`.

5. **Step 4 (PCAP Frame 0346)**:
   - Fanfare step (`AC 20:10`).

6. **Step 5 (PCAP Frame 0348)**:
   - Quest Journal Update (`AC 24:5 0x61 0x00 0x01`).
   - Sends `AC 20:10`.

7. **Step 6 (PCAP Frame 0350)**:
   - Robinson walks into standing position (`AC 22:12 1 1 0 6`).
   - Sends `AC 20:10`.

8. **Completion (`OnInteractionComplete`)**:
   - Resets player emote from lying down (`p.Emote = 0`).
   - Saves Quest 12040 in progress.
   - Unlocks screen (`AC 20:8`) and player movement (`AC 5:4`).

---

## Source Files
- [`Src/Network/ActionCodes/AC12.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC12.cs)
- [`wlo.pserver.core/Game/Player.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Player.cs)
- [`Src/Network/ActionCodes/AC20.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC20.cs)
