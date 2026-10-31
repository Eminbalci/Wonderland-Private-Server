# Implementation Plan: Automatic Non-Click Dialogue Architecture & Running Fix

- [x] 1. Investigate non-click automatic dialogue systems in Wonderland Online (walk-on regions, story arrival, client proximity auto-click, overhead bubbles).
- [x] 2. Audit `AC06.cs` and `EveEventRuntime.cs` for walk-on triggers (`TryExecuteRegion`, `Entry_Points`).
- [x] 3. Fix missing running packet handler (`AC 6, Sub 2`) in `AC06.cs` so running players trigger walk-on region auto-dialogues and warps.
- [x] 4. Verify solution builds with 0 warnings and 0 errors.
- [x] 5. Create technical documentation in `docs/69_walk_on_region_running_subcode_and_auto_dialogue.md` and update `README.md`.
