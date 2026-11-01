# Implementation Plan: Immortal Xu Spawn and Job Trial Quest Chaining Diagnosis & Fix

- [x] 1. Inspect latest server log (`wlophoenixlogFile_20261101.txt`) and trace player `gmone`'s actions on Map 12625 and Map 12626.
- [x] 2. Identify why Immortal Xu (NPC 1, TemplateID 15316) is sent as hidden (`AC 22:4 despawn`) on Map 12626.
- [x] 3. Trace Map 12625 Accession Official (NPC 1, TemplateID 15305) event branches and root-cause why Quest 15090 was never granted.
- [x] 4. Implement fix in `wlo.pserver.core/Game/Maps/Code/EveEventRuntime.cs` to prevent in-progress quest mark checks (`w2 == 1`) from falsely matching unaccepted quests.
- [x] 5. Verify fix with standalone test scripts for both Map 12625 Event 0 and Map 12626 PreEvents.
- [x] 6. Build solution with 0 errors and 0 warnings.
- [x] 7. Update technical documentation in `docs/` and root `README.md`.
