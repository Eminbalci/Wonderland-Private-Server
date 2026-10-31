# Technical Documentation: Quest NPC Dynamic Despawn and Spawn Synchronization Engine

## 1. Overview
In Wonderland Online, quest progression, storyline dialogues, and combat encounters frequently trigger dynamic overworld actor lifecycle events:
- Hiding/despawning an NPC (e.g. an NPC running away, turning into an enemy, or departing after receiving items).
- Revealing/spawning a new NPC (e.g. an ally appearing, a boss spawning on the battlefield, or a post-quest replacement merchant).
- Swapping actor states between multi-step quest phases across all 1,119 game maps.

This engine unifies official `Eve.emg` PreEvent condition evaluation, event script opcode execution, combat victory synchronization, and quest reward transitions into a coherent, packet-accurate server-client model.

---

## 2. Architecture & Root-Cause Resolution

### 2.1 Post-Completion PreEvent Mark Evaluation Bug
- **Prior Defect:** In `EveEventRuntime.cs:MatchesCondition` under `case 5` (quest mark checking), `quest.State == QuestState.InProgress` was strictly enforced. Whenever a quest or mark was in `QuestState.Completed`, `active` was evaluated as `false`, causing `mark` to drop to `0`. Consequently:
  - Conditions checking `condition.unknownword2 == 2` (unstarted check) returned `true` (false-positive).
  - PreEvent rules requiring mark progress or completion steps (e.g. `condition.unknownword2 == 1` and `value >= 1`) failed to match.
  - As a result, post-quest replacement NPCs never spawned and completed quest actors failed to despawn.
- **Resolution:** Updated `case 5` to consider both `QuestState.InProgress` and `QuestState.Completed` as valid active states:
  ```csharp
  bool hasQuest = player.Quests != null && player.Quests.TryGetValue(condition.unknownword1, out quest);
  bool active = hasQuest && (quest.State == QuestState.InProgress || quest.State == QuestState.Completed);
  int mark = 0;
  if (active) mark = quest.Step > 0 ? quest.Step : (quest.State == QuestState.Completed ? 1 : 0);
  if (condition.unknownword2 == 2) return !active;
  if (condition.unknownword2 != 1) return false;
  return CompareValue(mark, value, comparison);
  ```

### 2.2 PreEvent Map Context Null Propagation
- **Prior Defect:** In `EveEventRuntime.cs:MatchesPreEventCondition`, `MatchesCondition` was called with `map = null`. This caused any PreEvent rule relying on `MissionVariables`, map-aware prop states, or door mechanisms to abort immediately.
- **Resolution:** Passed `player?.CurMap as GameMap` to `MatchesCondition`, providing map metadata for all 1,119 maps.

### 2.3 Opcode 5 Active-Event Synchronization Suppression
- **Prior Defect:** In `EveEventInterpreter.cs:ExecuteOpcode` (Opcode 5), visibility synchronization was gated behind `if (map != null && !player.NativeEventActive)`. Because `player.NativeEventActive` is `true` throughout cutscenes and dialogues, overworld NPCs were prevented from updating their visibility mid-event or immediately upon receiving flags.
- **Resolution:** Removed `&& !player.NativeEventActive`, enabling immediate dispatch of `PreEventInterpreter.EvaluateMapPreEvents` whenever quest marks or stage steps change.

### 2.4 PreEvent Precedence Over Runtime State Caches
- **Prior Defect:** In `PreEventInterpreter.cs:ShouldNpcBeVisible`, `player.NativeActorVisibility` was queried before any PreEvents. Once an actor was temporarily hidden or shown during an opcode, that cached state permanently shadowed authored PreEvent rules for the remainder of the map visit.
- **Resolution:**
  - `NativeActorVisibility` is now only checked as an immediate override while `player.NativeEventActive` is active.
  - When evaluating persistent map rules, matching authored PreEvents in `mapData.PreEvents` take precedence.
  - If no authored PreEvent rule touches the actor, `NativeActorVisibility` is consulted as fallback before checking initial template flags (`unknownbyte1 & 1`).

### 2.5 Cross-Map Quest Step Spawn & Despawn
- **Prior Defect:** `ShouldNpcBeVisible` skipped quest step evaluations if `qDef.MapID != mapId`, preventing multi-map storyline quests from spawning or despawning actors on foreign maps.
- **Resolution:** Allowed `qDef.RelocateToMapID` and individual `QuestStep.SpawnNpcClickIDs` / `QuestStep.DespawnNpcClickIDs` to evaluate across all maps.

### 2.6 Missing Spawn Handling in CompleteQuest & GrantRewards
- **Prior Defect:** `QuestManager.GrantRewards` and `QuestManager.CompleteQuest` processed `qDef.DespawnNpcClickIDs`, but completely omitted `qDef.SpawnNpcClickIDs`.
- **Resolution:** Symmetrically iterate `qDef.SpawnNpcClickIDs` and invoke `PreEventInterpreter.SendActorShow(player, clickId)`.

### 2.7 Battle Victory NPC Despawn & Visibility Sync
- **Prior Defect:** In `PvEBattleManager.cs` and `EveEventRuntime.cs:RunOutcome`, winning a battle against a quest NPC that had no outcome branch left the monster standing on the overworld map without calling `SyncPerPlayerNpcVisibility`.
- **Resolution:** Added automatic overworld actor hide dispatch (`PreEventInterpreter.SendActorHide(player, clickId)`) and visibility synchronization upon victory.

---

## 3. Wire Protocol Reference (AC 22:4)

When revealing or concealing actors on the client map, the server dispatches packet ActionCode 22, SubCode 4:

### 3.1 Concealment / Despawn Frame
- **Packet:** `F4 44 10 00 16 04 [ClickID:w] [State:w=0xFFFF] [X:w] [Y:w] [EntityType:b=2] [Duration:d=0x03E7FC18] [StateFlag:b=0]`
- **Client Processing:** Disassembles to `FUN_0043d390`:
  - `*(actor + 0x1eec) = 2` (Hidden state).
  - Actor is removed from rendering loop and map collision grid.

### 3.2 Reveal / Spawn Frame
- **Packet:** `F4 44 10 00 16 04 [ClickID:w] [Frame:w] [X:w] [Y:w] [EntityType:b=1] [Duration:d=0] [StateFlag:b=0]`
- **Client Processing:** Disassembles to `FUN_004464dc`:
  - `*(actor + 0x1eec) = 1` (Visible state).
  - Actor coordinates are positioned at `(X, Y)`.
  - Frame is set to `QuestNpc.GetIdleAnimationFrame(templateId)` (`0x00FF` for animated sprites, `0` for interactive props).
  - Actor is restored to the active render list and collision grid.

---

## 4. Verification Suite

All modifications were verified with standalone integration tests in `test_quest_npc_sync.cs`:
1. `TestSendActorHideAndShow`: Verified `SendActorHide` and `SendActorShow` packet emission and `HiddenNpcClickIDs` state tracking.
2. `TestShouldNpcBeVisibleWithQuestSteps`: Verified multi-step quest stage spawn and despawn transitions.
3. `TestMatchesConditionCompletedState`: Verified `MatchesPreEventCondition` correctly evaluates `QuestState.Completed` for active checks (`unknownword2 == 1`) and unstarted checks (`unknownword2 == 2`).
4. `TestGrantRewardsSpawnAndDespawn`: Verified symmetric actor hiding and actor revealing upon quest completion.
5. `TestBattleVictorySync`: Verified error-free per-player NPC visibility synchronization.

Result: 13 Passed, 0 Failed.
Solution compilation: 0 Warnings, 0 Errors.
