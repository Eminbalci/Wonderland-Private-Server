# Companion Recruitment and Map NPC Despawn System

## Overview
This document specifies the companion pet recruitment protocol and map-level NPC despawn/hiding synchronization in Wonderland Online. When a player completes a quest or event yielding a companion pet, the pet is registered into the player's active pet roster and the corresponding overworld NPC instance is despawned.

---

## 1. Native `eve.dat` Companion Recruitment Workflow (e.g. Robinson Event 19)
The native event bytecode execution pipeline (`EveEventInterpreter`) processes multi-branch quest chains:
1. **Initial Trigger (`Sub #1`)**:
   - `Opcode 2: dialog2 == 5`: Plays chest break/open animation `AC 22:1`.
   - `Opcode 1: dialog3 == 48016`: Awards quest item (e.g., Robinson's Raft `#48016`) to `player.Inv`.
   - `Opcode 5: dialog1 == 12046, dialog2 == 1, dialog3 == 1`: Updates Quest #12046 to `InProgress` (Step 1).
2. **Cascading Story Dialogue (`Sub #3`)**:
   - Matches active quest state (`w1=12046, w2=1, w3=1`).
   - Streams full authentic multi-line dialogue sequence between Robinson, Burke the Tiger, and the player (`AC 20:1`).
   - On dialogue completion: updates Quest #12046 to `Completed` (`dialog2 == 2`) and Quest #12047 to `InProgress` (`dialog2 == 1`).
3. **Follow-Up Pet Recruitment & Despawn (`Sub #5`)**:
   - Matches newly activated quest state (`w1=12047, w2=1, w3=1`).
   - `Opcode 3: dialog2 == 12178`: Invokes `QuestManager.SendCompanionReward(player, 12178, "Robinson")` to add Robinson into `player.PlayerPets` and sends `AC 15:1` / `AC 19:1`.
   - `Opcode 2: dialog2 == 2`: Despawns Robinson from map view (`AC 22:10 [ClickID, 0xFF, 0xFF]`).
   - Re-evaluates flags: #15282 -> Completed, #15283 -> InProgress.

---

## 2. Dynamic NPC Despawn Protocol
* **`AC 22:4` (NPC List on Map Load)**:
  * NPCs for recruited companions have their state set to `0xFFFF` (Hidden/Recruited).
* **`AC 22:10` (Dynamic NPC State Update)**:
  * `[22, 10, ClickID (2B), 0xFF, 0xFF]` hides the specific NPC from the client view.

---

## 3. Companion Template Matching (`Player.HasRecruitedCompanion`)
- **Robinson**: NPC Template `12032` <-> Pet Template `12178` / `12032`
- **S.Monkey / Little Monkey**: NPC Template `17162` <-> Pet Template `10727`
- **Roca**: NPC Template `14161` <-> Pet Template `14001` / `14161`
- **Niss**: NPC Template `14162` <-> Pet Template `14002` / `14162`
- **Clive**: NPC Template `14163` <-> Pet Template `14003` / `14163`
- **Fred**: NPC Template `14164` <-> Pet Template `14004` / `14164`
- **Elin**: NPC Template `14165` <-> Pet Template `14005` / `14165`
- **Sam**: NPC Template `14166` <-> Pet Template `14006` / `14166`
- **Shizune**: NPC Template `14167` <-> Pet Template `14007` / `14167`
- **Suzan**: NPC Template `14168` <-> Pet Template `14008` / `14168`
