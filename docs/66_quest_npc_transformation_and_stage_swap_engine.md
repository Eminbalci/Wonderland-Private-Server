# Quest NPC Dynamic Transformation and Visibility Engine Specification

## 1. Overview & Problem Definition
In Wonderland Online (EVE script architecture), NPCs frequently change visual states, transform into different actors, or disappear permanently/temporarily during dialogue or upon completing specific quest steps:
- **Map 12600 (Bangkok Main Street)**: NPC #3 (Uncle, TID 14729) accepts Quest #13188 (Buddha's Chamber). Once the 3 consecrated relics (Items 30164, 30165, 30166) are handed in, the event executes `Opcode 2: d1=3, d2=2` (Hide NPC 3) and sets Quest 13188 to Completed while starting Quest 13189 at Step 1.
- **Map 12601 (Bangkok Residence)**: NPC #5 (Sajit sitting) transforms into NPC #7 (Sajit standing) during dialogue, and later both Sajit and Nadul depart.

Prior to this fix, after dialogues finished or upon scene sync, NPCs were resetting back to their initial un-transformed/visible states because the EVE condition evaluator was incorrectly interpreting bytecode lifecycle state queries (`unknownword2 == 2`).

---

## 2. Root Cause Analysis
In [EveEventRuntime.cs](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventRuntime.cs):
1. **Case 5 (Quest Conditions):**
   ```csharp
   // Before:
   if (condition.unknownword2 == 2)
   {
       return !active;
   }
   ```
   EVE bytecode uses `unknownword2 == 2` for two distinct purposes:
   - When `comparison == 0`: checks `!active` (NotStarted / Inactive).
   - When `comparison > 0`: `value` contains the lifecycle state:
     - `1`: InProgress
     - `2`: NotStarted
     - `3`: Completed
   Because the code always returned `!active`, any branch or PreEvent rule expecting `Quest == Completed` (value = 3) or `Quest == InProgress` (value = 1) evaluated to `false` whenever the quest was accepted or completed.

2. **Player Gender Checks (`unknownbyte1 == 2, unknownword1 == 3`):**
   - EVE bytecode checks player gender via `unknownword2 == 7` (Male) and `unknownword2 == 8` (Female). These conditions were previously unhandled and evaluated to `false`, causing gender-specific dialogue branches to fail.

---

## 3. Implemented Fixes
In [EveEventRuntime.cs](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventRuntime.cs#L225-L270):
1. Corrected `case 5` in `MatchesCondition`:
   ```csharp
   if (condition.unknownword2 == 2)
   {
       if (condition.comparison == 0) return !active;
       int lifecycleState = 2; // Not started / inactive
       if (active)
       {
           lifecycleState = (q.State == QuestState.Completed) ? 3 : 1;
       }
       return CompareValue(lifecycleState, condition.value, condition.comparison);
   }
   ```
2. Implemented player gender verification:
   ```csharp
   if (condition.unknownbyte1 == 2 && condition.unknownword1 == 3)
   {
       bool isMale = player.Body == 1 || player.Body == 3;
       if (condition.unknownword2 == 7) return isMale;
       if (condition.unknownword2 == 8) return !isMale;
       return true;
   }
   ```

---

## 4. Verification & Build
- `Wonderland Private Server.sln` built cleanly with **0 warnings and 0 errors**.
- Map 12600 NPC #3 correctly hides on relic turn-in and stays hidden across subsequent interactions.
