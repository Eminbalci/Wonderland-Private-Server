# Ship Captain Storm Cutscene Protocol

## Overview
Documents the reverse-engineered initial ship storm cutscene and shipwreck sequence from `ilkgorevinanimasyonlukisimlari.pcapng` (Frames 1834–2415) when interacting with the Captain on the Ship Deck (Map 10017, TemplateID 14003 / ClickID 10).

---

## Packet Sequence Flow
1. **Interaction Start (`AC 20:1`)**:
   - Player clicks Captain on Map 10017 (`AC 20 Sub 1 [0A 00]`).
   - Movement lock: `AC 6 Sub 2 [01]`.
   - **Step 1**: Captain Dialogue `0x0175AC` (30124) ("What's the matter?").
   - **Step 2**: Captain Dialogue `0x0175AD` (30125) ("It seems that there is a storm approaching...").
2. **Storm Cutscene Trigger (`AC 186:12`)**:
   - Upon dialogue completion, `EveEventInterpreter` triggers storm animation cutscene `Opcode 8`:
     - `AC 186 Sub 12 [02 00 01]`
3. **Client Cutscene Acknowledgment (`AC 186:9`)**:
   - Client sends `AC 186 Sub 9 [01 00]` upon animation completion.
   - Server responds with `AC 186 Sub 9 [01 00 01 00 00 00 00]` + `AC 20 Sub 1 [Step 3, Sound 0x027B0000]` (Storm thunder/lighting & screen shake).
   - Server immediately teleports player to **Map 10035 (Shipwreck Beach / Desert Island)** at coordinates `X: 1038, Y: 2235`.
4. **Beach Arrival Cutscene (`AC 12`)**:
   - `AC 12` sets `p.Emote = 9` (lying on beach).
   - Robinson rushes over (`AC 22:12`), wakes the player up (`TalkID 12008`), registers Quest #12040, and unlocks player movement (`AC 20:8`, `AC 5:4`).

---

## Code References
- Handled in [`EveEventInterpreter.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs) (Map 10017 Event 11).
- Cutscene confirmation & shipwreck warp in [`AC186.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC186.cs).
- Beach arrival wake-up sequence in [`AC12.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC12.cs).
