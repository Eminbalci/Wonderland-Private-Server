# Implementation Plan: Quest NPC Dynamic Transformation and Visibility Engine

- [x] 1. Fix `unknownword2 == 2 && comparison == 0` in `EveEventRuntime.cs:MatchesCondition` to return `!hasQuest || quest.State != QuestState.InProgress`, correctly evaluating completed quests as inactive marks.
- [x] 2. Fix `PreEventInterpreter.cs:ExecuteActionBlock` for PreEvent `actionType == 4` (pose/animation), routing to `SendActorPose(player, clickId, anim, facing)` rather than falling through to `AC 22:4` with frame 00 00.
- [x] 3. Safeguard fallback frame in `ExecuteActionBlock` so character/sprite NPCs never receive `00 00` idle frame wipeout.
- [x] 4. Verify all Bangkok NPC visibility & transformation stages (Map 12600 NPC 3, NPC 7/8, NPC 11/12, NPC 13/14; Map 12601 NPC 5/6/7 Sajit & Nadul transformation) via standalone tests.
- [x] 5. Rebuild entire solution `Wonderland Private Server.sln` cleanly with 0 warnings and 0 errors.
- [x] 6. Update `docs/66_quest_npc_transformation_and_stage_swap_engine.md` and root `README.md`.
