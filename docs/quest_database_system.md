# Quest Database & Storyline Management System

## Overview
The Quest Database System provides persistence, in-memory caching, live GUI editing, and clean separation between client quest journal logs (`Mark.dat`) and real server-side quests.

## Data Sources & Architecture

1. **Client Quest Marks (`Mark.dat`) vs. Server Storyline Quests:**
   - `Mark.dat` contains only raw client quest journal string entries and stage logs (#01, #02, etc.) used by the client UI. It does not define quest-givers, conditions, dialogues, or item rewards.
   - Server quests are stored in the SQLite database table `game_quests` and seeded from `Data/quests.json`.

2. **Clean Authentic Quests Seed (`Data/quests.json`):**
   - Contains legitimate, authentic Wonderland Online storyline, companion, and side quests:
     - **Quest 97:** *Stranded on Newbie Beach* (Robinson)
     - **Quest 1001:** *Robinson's Raft Supplies* (3 Coconuts -> Raft & Robinson Companion)
     - **Quest 1002:** *The Little Monkey* (Kelan Woods Monkey Companion)
     - **Quest 1003:** *Ill Grandma* (Kelan Village multi-step medicine quest)
     - **Quest 1004:** *Mary Lou's Headband* (Kelan Village)
     - **Quest 1005:** *Save Niss* (Maka Cave battle vs. Wolf Guard -> Niss Companion)
     - **Quest 1007:** *Roca's Trial* (Mayor's House -> Roca Companion)
     - **Quest 1010:** *Rescue Xaolan* (Battle vs. Pirate Lea -> Xaolan Companion)
     - **Quest 1012:** *Little Red Riding Hood* (Multi-step cottage wolf battle)
     - **Quest 10035:** *Astrologer Laura's Celestial Tent* (Space Tent & Key reward)
     - **Quest 1020:** *Mayor's Urgent Courier* (Dispatch letter delivery)
     - **Quest 1025:** *Lost Sheep of the Pasture* (North Island shepherd)
     - **Quest 1030:** *Old Sam's Broken Fishing Rod* (Bamboo Fishing Rod reward)
     - **Quest 1040:** *Trial of the Fire Guardian* (Holy Mountain Cave battle)

3. **Auto-Clean & DB Synchronization:**
   - `QuestDataBase.Initialize` automatically cleans out old dummy entries from `game_quests` and populates the clean authentic set from `Data/quests.json` (1,077 total quests).
   - The Quest DB Manager interface in the Server GUI (`MainForm1`) auto-synchronizes with `game_quests` on load and tab focus (`tabQuests.Enter`), displaying all quest parameters, dialogues, battle monster IDs, item requirements, and rewards in real-time.
   - Includes full-text search across Quest IDs, Quest Names, and NPC Names with in-memory fallback to `QuestManager.AllQuests`.

## Source Files
- [`Src/Gui/MainForm1.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.cs)
- [`wlo.pserver.core/DataBase/QuestDataBase.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/QuestDataBase.cs)
- [`wlo.pserver.core/Game/QuestRelated/QuestManager.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/QuestRelated/QuestManager.cs)
- [`RCLibrary/RCLibrary.Core/DataBase.cs`](file:///d:/GitHub/Wonderland-Private-Server/RCLibrary/RCLibrary.Core/DataBase.cs)
