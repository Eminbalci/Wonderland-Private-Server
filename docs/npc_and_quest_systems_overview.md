# Technical Architecture: Quests & NPCs System Overview

## 1. NPC Subsystems & Data Pipeline

### 1.1 Data Files
* **`Npc.dat` (`PhoenixData\DataFiles\PhxNpcDat.cs` / `Src\DataFiles\NpcManager.cs`):**
  * Contains NPC base templates, sprite IDs, elements (Fire, Water, Earth, Wind), level, attributes (STR, CON, INT, WIS, AGI), base HP/SP pools, and associated skills.
* **`Eve.emg` (`wlo.pserver.core\DataFiles\EveLoader.cs`):**
  * Map Event trigger file. Stores map placement coordinates (X, Y), Eve IDs, NPC Template IDs, and roaming radii.
* **`Talk.dat`:**
  * Contains NPC dialogue text scripts and conversation branches.

### 1.2 Network Action Codes (NPC Protocols)
* **`AC 15` (Entity Visibility & Map Spawning):**
  * Broadcasts NPC presence, position, orientation, and visual state when entering maps or within player visual range.
* **`AC 20` (NPC Interaction & Dialogue):**
  * `AC 20 Sub 1`: Client clicks an NPC -> Server resolves NPC ID via `Map.cs` / `QuestManager.cs` and returns talk frame.
  * `AC 20 Sub 8`: Portal / Warp stepping and vehicle transition.
* **`AC 22` (NPC Roaming & Movement):**
  * `AC 22 Sub 2`: Broadcasts real-time step-by-step movement waypoints and random wandering for dynamic NPCs.
* **`AC 11` & `AC 13` (NPC Combat & State):**
  * Controls initiating battles with wild NPCs / monsters and syncing NPC combat statistics.

---

## 2. Quest Engine & Lifecycle Architecture

### 2.1 Quest Manager & State Machine (`wlo.pserver.core\Game\QuestRelated\`)
* **State Lifecycle:** `NotStarted (0)` -> `InProgress (1)` -> `Completed (2)`.
* **Quest Types Supported:**
  1. `ItemCollection`: Requires player to gather specific items and quantities (e.g. Coconut for Robinson).
  2. `Dialogue`: Instant completion upon conversation trigger (e.g. Monkey companion recruitment).
  3. `MonsterBattle`: Triggers quest combat encounter (e.g. Save Niss from Wolf Guard).
* **Registered Quests:**
  * `Quest 1001`: *Robinson's Supplies* (Requires Item 41066 Coconut; Rewards 250 Gold, 150 EXP, Raft vehicle 48010).
  * `Quest 1002`: *The Little Monkey* (Rewards 200 Gold, 150 EXP, Monkey companion 10727).
  * `Quest 1003`: *Ill Grandma* (Requires Item 30259 Black Medicine; Rewards 500 Gold, 200 EXP, Item 30264).
  * `Quest 1004`: *Mary Lou's Lost Headband* (Requires Item 22061 Headband; Rewards 200 Gold, 100 EXP, Item 46015).
  * `Quest 1005`: *Save Niss* (Battle with Monster 11066; Rewards 300 Gold, 300 EXP, Niss companion 11066).

### 2.2 Quest Network Protocols
* **`AC 39` (Quest Journal & Journal Log):**
  * `AC 39 Sub 1`: Sends active quest log to client journal.
  * `AC 39 Sub 2`: Updates individual quest stage status.
* **`AC 52` (Quest Completion & Rewards):**
  * Broadcasts quest completion fanfare, gold/EXP awards, item additions, and companion unlocks.

### 2.3 Database Persistence
* **Table:** `charquest` (`wlo.pserver.core\DataBase\CharacterDataBase.cs`)
* **Schema:** `char_id`, `quest_id`, `state`, `completed_at`.

---

## 3. Client-Side Decompiled Routines (`aLogin_decompiled.c` / Client)
* **`FUN_003f7aec`:** Client-side entity validation routine checking whether actions/items apply to Player vs NPC/Pet, validating EXP limits, and checking item equipment constraints.
* **`TTalkMsgForm` / `TG_Form`:** Borland VCL dialogue form rendering NPC conversation boxes and branch choices.
