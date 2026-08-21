# Quest Engine & PreEvent Synchronization System

## 1. PreEvent Quest Flag Synchronization
- **AC 24:1 [QuestID, Step]:** Sets the current active quest step in the client's memory.
- **AC 24:2 [QuestID, Step]:** Updates the quest step in the client's Quest Journal.
- **AC 24:4 [Count, Entries...]:** Synchronizes the full Quest Journal.
- **AC 24:5 [QuestID, State]:** Marks quest state (1 = Completed).
- On every map transition (`Map.Warp_In`) and character login, `QuestManager.SendAllQuestFlags(player)` is executed. This enables the client's `PreEvent` evaluation engine to dynamically display or hide quest NPCs (e.g. Xaolan, Hijackers, Pirate Lea, Hood Girl, Grannie, Roca) based on the player's real-time storyline progress.

## 2. Core Multi-Step Quests & Companion Battles
- **Quest 1010 (Rescue Xaolan):** Approaching / battling Pirate Lea (14155) & Hijackers (12049, 12050) -> Victory recruits Xaolan (TID 14156) and sets completion flags to despawn hostage NPCs.
- **Quest 1005 (Save Niss):** Caged Niss interaction -> Wolf Guard battle (11066) -> Victory recruits Niss.
- **Quest 1007 (Roca's Trial):** Roca interaction -> Recruits Roca (14162 / 11001).
- **Quest 1012 (Little Red Riding Hood):** Hood Girl (12036) -> Grannie (14089) -> Wild Wolf (17437) battle.
- **Quest 1003 (Ill Grandma) & 1004 (Mary Lou's Headband):** Multi-stage delivery & gathering steps.
