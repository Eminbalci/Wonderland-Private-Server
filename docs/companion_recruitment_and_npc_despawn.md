# Companion Recruitment and Map NPC Despawn System

## Overview
This document specifies the companion pet recruitment protocol and map-level NPC despawn/hiding synchronization in Wonderland Online. When a player actively possesses a recruited companion pet (with Amity >= 20, not abandoned / run away), the static NPC instance corresponding to that companion is automatically hidden across all maps.

---

## Network Protocol Details

### 1. Despawn Packets
* **`AC 22:4` (NPC List on Map Load)**:
  * NPCs for recruited companions have their state set to `0xFFFF` (Hidden/Recruited).
* **`AC 22:10` (Dynamic NPC State Update)**:
  * `[22, 10, ClickID (2B), 0xFF, 0xFF]` hides the specific NPC from the client view.

---

## Dynamic Companion Matching Engine (`Player.HasRecruitedCompanion`)

The server dynamically matches map NPCs against the player's active and roster pets using template IDs, pet IDs, and localized companion names:
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
- **General Match**: Case-insensitive substring and exact name matching for all unique companions.

### Runaway / Low Amity Safety
If a companion's Amity drops below 20 (runaway / deserted), `HasRecruitedCompanion` returns `false`, allowing the map NPC to become visible again for re-recruitment or story progression.

---

## Source Files
- [`wlo.pserver.core/Game/Player.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Player.cs) (`HasRecruitedCompanion`)
- [`wlo.pserver.core/Game/Maps/Map.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs) (`SendMapInfo`)
- [`wlo.pserver.core/Game/QuestRelated/QuestManager.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/QuestRelated/QuestManager.cs) (`SendCompanionReward`)
- [`Src/Gui/CharacterDataEditorForm.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Gui/CharacterDataEditorForm.cs)
