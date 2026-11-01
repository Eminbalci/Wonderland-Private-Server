# Map 12626 The Immortal Xu Spawn & Map 12625 Accession Trial Quest Chaining Fix

## 1. Executive Summary & Root Cause Analysis

When a player (such as character `gmone`) progressed through the Bangkok Monk quest line (`Quest 13188` completed, `Quest 13189` in progress) and entered Map 12626 (Immortal Xu's Sanctuary), the NPC **The Immortal Xu** (ClickID `1`, TemplateID `15316`, world coordinates `1069, 694`) failed to spawn and was completely missing from the map.

Log inspection of `bin/Debug/Logs/wlophoenixlogFile_20261101.txt` revealed that on map entry, the server explicitly sent an actor hide packet:
```
[ActorVisibility] Sent SendActorHide (AC 22:4 despawn) for ClickID 1 at 1069,694 to gmone
AC=23, Sub=138: 01 00 FF FF 2D 04 B6 02 02 18 FC E7 03 00
```
(ClickID `1`, State `0xFFFF`, EntityType `2` [Hidden], Duration `0x03E7FC18` [Despawn code]).

### 1.1 PreEvent Lifecycle on Map 12626
Decompilation of Map 12626 in `Data/eve.Emg` showed:
- Initial NPC visibility in `Npc.dat`: `unknownbyte1 = 4` (hidden by default).
- PreEvent 0 Rule 0: `Quest 15090 < 1` -> **Action: HIDE NPC 1**.
- PreEvent 0 Rule 1: `Quest 15090 == 1` & `Quest 15091 == 0` -> **Action: SHOW NPC 1 with Quest Marker (0D:1:8)**.
- PreEvent 0 Rule 2: `Quest 15090 >= 2` & `Quest 15091 == 0` -> **Action: SHOW NPC 1**.

Therefore, Immortal Xu is an authored quest actor who is **only** revealed when `Quest 15090` (Job Transfer Trial / Rebirth) is accepted and active at Step 1 or higher.

### 1.2 The Root Cause on Map 12625 (Accession Official)
Tracing earlier actions in the server log revealed that `gmone` visited Map 12625 (Accession Hall) and clicked NPC 1 (Accession Official, TemplateID 15305):
```
[EveEventInterpreter] Executing native Event for Map #12625, NPC #1 'Accession Official' -> Event #1 (' 轉職任務').
EveEventRuntime: Player=gmone Map=12625 Event=1 Branch=1 Step=1/2 Opcode=2 (d1=1, d2=1, d3=25641, d4=0)
```
In `Eve.emg`, Event 0 ClickID 1 on Map 12625 has 12 branches:
- **Branch 0 (index 0):** `Type=5, w1=15090, w2=1, w4=2307` (`comparison = 3 (<=)`, `value = 9`). Actions: Dialogue 25641 & 25640 ("You are currently taking the trial...").
- **Branch 3 (index 3):** `Type=5, w1=15090, w2=2, w4=0` (Quest 15090 not active), `Level < 100`. Actions: Dialogue 60912 ("You need to be level 100...").
- **Branch 6 (index 6):** `Type=5, w1=15090, w2=2, w4=0` (Quest 15090 not active), `Level >= 100`, `Quest 13189 == 1`. Actions: Dialogue 25635 ("Do you want to take the trial?"), Question 1 (Yes/No choice). Choosing Yes (`Branch 10`) grants `Quest 15090 (Step 1)`.

**The Defect:**
In `EveEventRuntime.cs:MatchesCondition`:
```csharp
PlayerQuest quest = null;
bool hasQuest = player.Quests != null && player.Quests.TryGetValue(condition.unknownword1, out quest);
bool active = hasQuest && (quest.State == QuestState.InProgress || quest.State == QuestState.Completed);
int mark = 0;
if (active) mark = quest.Step > 0 ? quest.Step : (quest.State == QuestState.Completed ? 1 : 0);
...
if (condition.unknownword2 != 1) return false;
return CompareValue(mark, value, comparison);
```
When `gmone` (who had never started Quest 15090, so `hasQuest == false` and `active == false`) clicked Accession Official:
1. `mark` defaulted to `0`.
2. Branch 0's condition was `mark <= 9`.
3. `CompareValue(0, 9, 3)` evaluated `0 <= 9` as `true`!
4. `FindBranch` immediately matched **Branch 0** (the in-progress dialogue) and completely shadowed **Branch 6** (the quest offer dialogue)!
5. Accession Official spoke as if the player was already taking the trial, never gave the dialogue prompt, and never started `Quest 15090`.
6. When `gmone` subsequently warped into Map 12626, PreEvent 0 Rule 0 (`Quest 15090 < 1`) evaluated to `true` (`0 < 1`), despawning The Immortal Xu.

---

## 2. Technical Modifications

### 2.1 In-Progress Step Guard in `EveEventRuntime.cs`
In WLO EVE bytecode, `unknownword2 == 1` represents an inspection of the **in-progress quest step/mark** (1..N). An unstarted or inactive quest (`!active`) does not have an active step and cannot satisfy in-progress range checks:
- Comparisons for ranges `comparison == 3` (`<= value`), `comparison == 4` (`>= value`), `comparison == 2` (`> value`), or `comparison == 1` (`< value` where `value > 1`) must require `active == true`.
- In contrast, `comparison == 1` (`< 1`) or `comparison == 5` (`== 0`) legitimately tests whether the quest is unstarted/absent (`mark == 0`).

Updated `EveEventRuntime.cs:MatchesCondition`:
```csharp
if (condition.unknownword2 != 1) return false;
// When checking an in-progress quest mark (w2 == 1):
// An unstarted quest (!active) is not at any in-progress step (1..N).
// Comparisons for in-progress step ranges (<= value, or < value where value > 1, or >= value, or > value)
// require the quest to be active; otherwise 0 <= 9 (Map 12625) or 0 < 3 (Map 11013) erroneously matches unaccepted quests.
if (!active && (comparison == 3 || (comparison == 1 && value > 1) || comparison == 2 || comparison == 4))
{
    return false;
}
return CompareValue(mark, value, comparison);
```

---

## 3. Verification & Results

- **Unit Verification (`test_immortal_xu_e2e.ps1`):**
  - **Before accepting trial:** Player has Level 100 and Quest 13189 step 1, but no Quest 15090.
    - Map 12625 Event 0 correctly selects **Branch 6** (Trial Quest Offer), not Branch 0.
    - Map 12626 Immortal Xu visibility is `False` (hidden prior to trial).
  - **After accepting trial:** `Quest 15090` is active at Step 1.
    - Map 12626 Immortal Xu visibility evaluates to `True` (spawns with quest marker).
    - Map 12625 Event 0 now correctly selects **Branch 0** (In-Progress dialogue).
- **Compilation:**
  - `Wonderland Private Server.sln` built with **0 errors, 0 warnings**.
- **Database Safety:**
  - Database (`ServerDataBase.db`) was untouched and unmodified.
