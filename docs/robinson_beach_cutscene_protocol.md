# Robinson Beach Rescue Cutscene Protocol (Map 10035 / Quest 12040)

## Overview
Documents the authentic 9-step dialogue sequence and rescue flow on Map 10035 (Shipwreck Beach / Desert Island) when arriving from the starter ship prologue, derived directly from `Eve.emg` (Map 10035 Event 8) and `Talk.dat` (`TalkIDs 20304..20312`).

---

## Complete Dialogue & Cutscene Flow

1. **Beach Arrival (`AC 12:1`)**:
   - Camera focuses on Robinson (`AC 22:11 ClickID 6`).
   - Player is placed in lying down posture (`Emote = 9`).
   - Robinson rushes over to the player (`AC 22:12`).
2. **Authentic 9-Step Dialogue Sequence (`TalkIDs 20304..20312`)**:
   - **Step 1 (20304 - Player / Portrait 7)**: "Cough cough... Huh? Where am I?"
   - **Step 2 (20305 - Robinson / Portrait 3)**: "This is a deserted island. How did you end up here?"
   - **Step 3 (20306 - Player / Portrait 7)**: "I was on the ship then suddenly it started violently shaking and I lost consciousness."
   - **Step 4 (20307 - Robinson / Portrait 3)**: "I think you must have been shipwrecked and drifted to this island. You are still alive, which is a great blessing."
   - **Step 5 (20308 - Robinson / Portrait 3)**: "It has been 28 years since I drifted to this island."
   - **Step 6 (20309 - Player / Portrait 7)**: "Oh, dear! 28 years ago? Didn't you look for ways to go back?"
   - **Step 7 (20310 - Robinson / Portrait 3)**: "Yes, I did. But I've only been to the islands nearby..."
   - **Step 8 (20311 - Player / Portrait 7)**: "My name is Player."
   - **Step 9 (20312 - Robinson / Portrait 3)**: "Player, you look fine. Walk around if you are free. You can ask me if you have any questions."
3. **Completion & State Unlock**:
   - Quest 12040 registered (`InProgress`, Step 1).
   - Player stands up (`Emote = 0`).
   - Movement unlocked (`AC 6:2 [0]`, `AC 20:8`, `AC 5:4`).

---

## Code References
- Handled in [`AC12.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC12.cs).
- Cutscene trigger and ship-to-beach transition in [`AC186.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC186.cs).

- [`Src/Network/ActionCodes/AC20.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC20.cs)
