# Todo List - Reborn Level 199 Progression & Overworld Stat 35 / AC 5:4 Synchronization

- [x] Reverse-verify `Equip.cs` EXP progression and cap logic up to Level 199 for Reborn characters
- [x] Add `SendStat(35, Level)` to `Send8_1()` in `Equip.cs` to directly update `[actor + 0x1f98]` in client memory
- [x] Trigger client `LevelUP` visual animation and `Sound\Wav0153.wav` via `AC 5:4` (`[5, 4, CharID]`) on level-up
- [x] Clamp wire EXP in `SendExp()` to `Math.Min((long)TotalExp, 0x76F1D017L)` to prevent client-side signed 32-bit integer overflow
- [x] Integrate Stat 35 and LevelUP sync in `SetLevel`, `AddExp`, and `RebornManager.PerformReborn`
- [x] Verify `AC02.cs` `:level` command allows setting level up to 199 for reborn characters
- [x] Execute automated test suite verifying level 100 -> 199 progression, wire EXP clamping, and packet emissions
- [x] Run `dotnet build "Wonderland Private Server.sln"` to ensure 0 errors and 0 warnings
- [x] Verify `Data/ServerDataBase.db` is 100% clean and unmodified
- [x] Document changes in `docs/74_reborn_level_199_progression_and_stat35_synchronization.md` and update `README.md`
- [x] Mark all todo items complete
