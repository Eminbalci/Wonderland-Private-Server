# NPC & Event Mapping Integrity and Teleportation Isolation

## 1. System Overview

In Wonderland Online (WLO), map interactions (NPC dialogues, quests, choices, battles, rewards, pet recruitment, storage, shops, minigames) are structured hierarchically inside `Eve.emg` per map instance.

### Multi-Key Resolution Hierarchy
An interaction initiated via `AC 20:1` is resolved using the tuple:
$$(\text{MapID}, \text{ClickID}, \text{TemplateID}, \text{EventID})$$

```
+-------------------------------------------------------------+
| Player Clicks NPC (AC 20:1)                                 |
+-------------------------------------------------------------+
                              |
                              v
+-------------------------------------------------------------+
| Map.ProcessInteraction(ClickID, Player)                     |
+-------------------------------------------------------------+
                              |
                              v
+-------------------------------------------------------------+
| EveEventInterpreter.TryExecute(Player, GameMap, ClickID)    |
| 1. Query MapData by MapID                                   |
| 2. Locate NpcListInfo by ClickID                            |
| 3. Verify TemplateID (TID) against SceneDataManager         |
| 4. Match authentic Event via npcEntry.Events [EventID]      |
| 5. Select branch from EventSubEntry matching QuestState     |
| 6. Execute authentic Bytecode Opcodes                       |
+-------------------------------------------------------------+
                              |
        +---------------------+---------------------+
        |                                           |
        v [Handled: return true]                    v [Unregistered Entity]
+-------------------------------+       +-------------------------------+
| Complete Event Execution      |       | Talk.dat Dialogue Fallback    |
| - Talk.dat Text Steps         |       | - ResolveTalkIdForNpc         |
| - Choices / Battles / Rewards |       | - Send AC 20:1 Dialogue Step  |
| - Close (AC 20:8, AC 5:4)     |       | - Close (AC 20:8, AC 5:4)     |
+-------------------------------+       +-------------------------------+
```

---

## 2. Event Lookup & Fallback Isolation

### Previous Vulnerability
1. **Unrestricted ClickID Fallback**: When an NPC had no explicit event array, the interpreter fell back to picking any event on that map whose index matched `clickId` (`mapData.Events.FirstOrDefault(e => e.clickID == clickId)`). If another map prop or script shared that numeric index, the wrong event would trigger.
2. **Legacy Hardcoded Quest Interceptors**: Old hardcoded handlers in `QuestNpc.Interact` and `QuestManager.TryHandleNpcQuest` attempted loose name and title matching (`currentName.Contains(p)`). When native execution returned false, these handlers intercepted the click and could trigger unprompted warps, reward cycles, or state resets.

### Hardened Implementation (`EveEventInterpreter.cs` & `QuestNpc.cs`)
- **Strict Linked Event Matching**:
  ```csharp
  EventsinMapEntries eventEntry = null;
  if (npcEntry != null && npcEntry.Events != null && npcEntry.Events.Count > 0)
  {
      foreach (var evId in npcEntry.Events)
      {
          eventEntry = mapData.Events?.FirstOrDefault(e => e.clickID == evId);
          if (eventEntry != null && eventEntry.SubEntry != null && eventEntry.SubEntry.Count > 0)
              break;
      }
  }

  // Only check direct clickID if the clicked object is an unlisted map prop/entity
  if (eventEntry == null && mapData.Events != null && (npcEntry == null || (npcEntry.Events == null || npcEntry.Events.Count == 0)))
  {
      eventEntry = mapData.Events.FirstOrDefault(e => e.clickID == clickId);
  }
  ```
- **Elimination of Hardcoded Quest Traps**:
  All obsolete hardcoded quest, cutscene, and recruitment blocks in `QuestNpc.cs` were purged. Non-event NPCs cleanly default to authentic `Talk.dat` dialogue extraction via `ResolveTalkIdForNpc(src)`.

---

## 3. Teleportation & Cutscene Isolation

1. **Opcode 8 SFX vs Cutscene Trigger**:
   - `Opcode 8` is authentic sound effects/fanfare/BGM.
   - Previous bug: `op.dialog1 == 2` in `Opcode 8` falsely triggered `AC 186:12` (Storm Cutscene) which ended up teleporting the player to Map 10035 when companion recruitment fanfare played.
   - Fixed: `Opcode 8` strictly triggers fanfare / sound effects (`Tools.FromFormat("bb", 20, 10)`), with `AC 186:12` restricted specifically to Ship Captain on `Map 10017 ClickID 10`.
2. **AC 186 Map Boundary Guard**:
   - `AC186.Recv9` enforces map ID boundaries (`p.CurMap.MapID == 10017` or ship maps `10024..10028`) before executing the shipwreck beach teleportation callback.

---

## 4. S.Monkey Authentic Recruitment & Dialogue Flow

- **Entity**: `S.Monkey` (TID 17162) on Map 11016 ClickID 1.
- **Dialogue Chain**: Authentic 16-step dialogue sequence (`TalkIDs 20038..20053`):
  - Steps 1..11: Initial discovery, CPR/revival, monkey following player, crying.
  - Full Team Gate: If party has 4 pets, routes to TalkID 31146 (team full) + 20048 (crying).
  - Successful Recruitment: Steps 12..16 (accidental mom misunderstanding, acceptance, pet recruitment via `QuestManager.SendCompanionReward(17162, "S.Monkey")`, Quest 12002 Completed, Quest 12003 InProgress, Fanfare SFX).
- **Post-Recruitment Interaction**: If already recruited, returns short authentic squeak dialogue (`TalkID 20042`).
