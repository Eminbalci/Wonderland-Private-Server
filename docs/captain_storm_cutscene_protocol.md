# Ship Captain Storm Cutscene Protocol

## Overview
Documents the reverse-engineered initial ship storm cutscene and shipwreck sequence from `ilkgorevinanimasyonlukisimlari.pcapng` (Frames 1834–2415) when interacting with the Captain on the Ship Deck (Map 10024–10028, TemplateID 10002 / ClickID 10).

---

## Packet Sequence Flow
1. **Interaction Start (`AC 20:1`)**:
   - Player clicks Captain (`AC 20 Sub 1 [0A 00]`).
   - Movement lock: `AC 6 Sub 2 [01]`.
   - **Step 1**: Captain Dialogue `0x0175AC` (30124) ("Everything is calm, the voyage goes well...").
   - **Step 2**: Captain Dialogue `0x0175AD` (30125) ("Wait... look at the horizon! Dark clouds!").
2. **Storm Cutscene Trigger (`AC 186:12`)**:
   - Upon dialogue completion, server triggers storm animation cutscene:
     - `AC 186 Sub 12 [01 00 00 00 00]`
3. **Client Cutscene Acknowledgment & Progression (`AC 186:9` & `AC 20:1`)**:
   - Client sends `AC 186 Sub 9 [01 00]`.
   - Server responds with `AC 186 Sub 9 [01 00 01 00 00 00 00]` + `AC 20 Sub 1 [Step 3, Sound 0x027B0000]` (Storm thunder/lighting & screen shake).
4. **Cutscene Finish & Map Teleport (`AC 12:246`)**:
   - Client signals animation complete with `AC 20 Sub 6`.
   - Server teleports player to **Map 10035 (Shipwreck Beach / Desert Island)** at coordinates `X: 1038, Y: 2235`.
   - Movement unlock (`AC 20:8` + `AC 5:4`).

---

## Code References
- Handled in [`QuestNpc.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/QuestNpc.cs) under Captain NPC interaction block.
- Cutscene confirmation & progression in [`AC186.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC186.cs).
