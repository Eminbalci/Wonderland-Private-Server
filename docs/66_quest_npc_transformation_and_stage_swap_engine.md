# Quest NPC Dynamic Transformation and Visibility Engine Specification

## 1. Overview & Problem Definition
In Wonderland Online (EVE script architecture), NPCs frequently change visual states, transform into different actors, or disappear permanently/temporarily during dialogue or upon completing specific quest steps:
- **Map 12600 (Bangkok Main Street)**: NPC #3 (Bangkok Monk / Old Uncle, TID 14729) accepts Quest #13188 (Buddha's Chamber). During meditation, PreEvent 0 sets the monk's meditating pose via `actionType == 4` (Anim 46, Facing 1). When the 3 consecrated relics (Items 30164, 30165, 30166) are handed in, the event executes `Opcode 2: d1=3, d2=2` (Hide NPC 3) and sets Quest 13188 to Completed while starting Quest 13189 at Step 1.
- **Map 12601 (Bangkok Residence)**: NPC #5 (Girl Sajit, TID 15330) took dubious flu medication from a crooked merchant, turning into a girl. Upon brewing the antidote, Event 8 executes `Opcode 2: d1=5, d2=2` (Hide Girl Sajit) and `Opcode 2: d1=7, d2=3` (Show Boy Sajit, TID 15329). When Quest 15266 is completed and Quest 15267 begins, both Sajit and Nadul depart home, hiding NPCs 5, 6, and 7.

Prior to this fix:
1. In Map 12600 Event 20, completing dialogue or setting Quest 13188 Step 10 caused NPC #3 to disappear unexpectedly from the client viewport because `PreEventInterpreter.cs:ExecuteActionBlock` did not handle PreEvent `actionType == 4`, falling through to dispatch `AC 22:4` with frame `00 00`, setting the animated sprite's frame to empty/invisible.
2. In Map 12601 Event 8, after completing Quest 15266, Girl Sajit reappeared while Boy Sajit vanished and Nadul remained because `w2 == 2 && comparison == 0` returned `!active`, evaluating to `false` for completed quests.
3. PreEvent rules with multiple conditions (e.g. `w6 >> 8` condition counts) were incorrectly partitioned.

---

## 2. Root Cause Analysis
1. **Unrecognized PreEvent ActionType 4 in `ExecuteActionBlock`:**
   - In EVE bytecode PreEvents, `actionOp == 0x02` uses `actionType` to specify entity commands:
     - `actionType == 2`: Hide actor (`SendActorHide`)
     - `actionType == 3`: Show actor (`SendActorShow`)
     - `actionType == 4` & `actionType == 7`: Actor animation/pose (`SendActorPose`, AC 22:9)
     - `actionType == 5`: Prop state mechanism (`AC 22:1`)
     - `actionType == 10`: Facing / pathing (`AC 22:1`)
   - Because `actionType == 4` was missing from the check, `PreEvent 0 Sub 0` (`02-03-00-04-00-2E-...`) fell into the `else` block, which constructed an `AC 22:4` packet with frame `00 00`. For sprite-based character NPCs, frame `00 00` overwrites the sprite frame, making the NPC vanish from rendering.

2. **Completed Quests in `unknownword2 == 2 && comparison == 0`:**
   - When a quest completes in WLO, mark removal (`AC 24:4`) occurs, removing the quest from active notebook tracking.
   - Consequently, in EVE bytecode:
     - `w2 == 1`: checks that the quest is in progress (`State == QuestState.InProgress`).
     - `w2 == 2`: checks that the quest is NOT in progress (`!hasQuest || quest.State != QuestState.InProgress`).
   - Previously, `unknownword2 == 2 && comparison == 0` returned `!active`. Because completed quests had `active == true`, this returned `false`, breaking post-quest branches and PreEvents (such as Map 12600 Event 20 Sub 19 and Map 12601 PreEvent 2 Sub 2).

3. **PreEvent Multi-Condition Rule Grouping:**
   - EVE bytecode encodes condition counts in `unknownword6 >> 8` on the action-bearing subentry. The interpreter now groups the subsequent `condCount - 1` condition records with the parent action.

---

## 3. Implemented Fixes

### 3.1 `EveEventRuntime.cs:MatchesCondition`
```csharp
if (condition.unknownword2 == 2)
{
    if (comparison > 0)
    {
        int lifecycleState = !active ? 2 : (quest.State == QuestState.Completed ? 3 : 1);
        return CompareValue(lifecycleState, value, comparison);
    }
    // If comparison operator is 0, w2 == 2 checks that the quest mark is not active in notebook
    // (either not accepted yet or already completed/archived).
    return !hasQuest || quest.State != QuestState.InProgress;
}
```

### 3.2 `PreEventInterpreter.cs:ExecuteActionBlock`
```csharp
if (actionType == 7 || actionType == 4)
{
    // EVE action 2/7 and 2/4 specify the actor animation in word 3,
    // with facing in byte 13 (or state1); it is an actor pose/animation, not a prop frame.
    byte facing = (data.Length > 13 && data[13] != 0) ? data[13] : state1;
    SendActorPose(player, clickId, BitConverter.ToUInt16(data, 5), facing);
}
else if (actionType == 2)
{
    SendActorHide(player, clickId);
}
else if (actionType == 3)
{
    SendActorShow(player, clickId);
}
...
else
{
    if (player.HiddenNpcClickIDs.Contains(clickId)) return;
    GetNpcCoordinates(player, clickId, out ushort x, out ushort y);

    // Safeguard: Ensure animated character NPCs never receive frame 00 00 which blanks out their sprite
    var mapNpc = (player.CurMap as GameMap)?.NpcList?.OfType<Game.Maps.QuestNpc>()
        .FirstOrDefault(n => n.CickID == clickId);
    uint templateId = mapNpc?.TemplateID ?? DataBase.GameDataBase.GlobalInstance?.EveDat?
        .GetMapData((ushort)player.MapID)?.Npclist?.FirstOrDefault(n => n.clickId == clickId)?.npcId ?? 0;
    ushort idleFrame = Game.Maps.QuestNpc.GetIdleAnimationFrame(templateId);
    byte frame1 = state1;
    byte frame2 = state2;
    if (frame1 == 0 && frame2 == 0 && idleFrame != 0)
    {
        frame1 = (byte)(idleFrame & 0xFF);
        frame2 = (byte)(idleFrame >> 8);
    }

    SendPacket p = new SendPacket();
    p.Pack8(22);
    p.Pack8(4);
    p.Pack16(clickId);
    p.Pack8(frame1);
    p.Pack8(frame2);
    p.Pack16(x);
    p.Pack16(y);
    p.Pack8(1);
    p.Pack32(0);
    p.Pack8(0);
    player.Send(p);
}
```

---

## 4. Verification Suite Results
- Standalone verification script executed across all quest lifecycle stages:
  - **Stage 0 (No Quests)**:
    - NPC 5 (Girl Sajit): Visible (True)
    - NPC 6 (Nadul): Visible (True)
    - NPC 7 (Boy Sajit): Hidden (False)
  - **Stage 1 (Quest 15266 Step 1 - Accepted)**:
    - NPC 5 (Girl Sajit): Visible (True)
    - NPC 6 (Nadul): Visible (True)
    - NPC 7 (Boy Sajit): Hidden (False)
  - **Stage 2 (Quest 15266 Step 5 - Transformed to Boy)**:
    - NPC 5 (Girl Sajit): Hidden (False)
    - NPC 6 (Nadul): Visible (True)
    - NPC 7 (Boy Sajit): Visible (True)
  - **Stage 3 (Quest 15266 Completed, Quest 15267 Step 1 - Departed Home)**:
    - NPC 5 (Girl Sajit): Hidden (False)
    - NPC 6 (Nadul): Hidden (False)
    - NPC 7 (Boy Sajit): Hidden (False)
  - **Map 12600 (Old Uncle NPC 3)**:
    - Initial & Step 10: Visible (True), Meditating pose (Anim 46, AC 22:9)
- `Wonderland Private Server.sln` compilation: **0 warnings, 0 errors**.
