# Implementation Plan: Reborn Job Skills Award and Overworld Level Sync Fix

- [x] 1. Analyze requirements: Reborn job skill mapping (Murder, Holy Shield, Knight's Spirit, Curse, Rest, Foretell), authentic level drop formula (-99), and `:level` command overworld packet synchronization.
- [x] 2. Implement Reborn Job Skill granting in `RebornManager.cs` and `SkillManager.cs`:
  - Map `RebornJob.Killer` (1) -> 15241 (`Murder`)
  - Map `RebornJob.Warrior` (2) -> 15240 (`Holy Shield`)
  - Map `RebornJob.Knight` (3) -> 15242 (`Knight's Spirit`)
  - Map `RebornJob.Wit` (4) -> 15243 (`Curse`)
  - Map `RebornJob.Priest` (5) -> 15244 (`Rest`)
  - Map `RebornJob.Seer` (6) -> 15245 (`Foretell`)
  - Unlock job skill upon rebirth in `RebornManager.PerformReborn` with full AC 8:1, AC 5:12, AC 5:11 packets.
  - Add reborn job skill check in `SkillManager.GetQualifiedProgressionSkills` for login / persistence consistency.
  - Align rebirth level reduction formula to `Math.Max(1, player.Level - 99)`.
- [x] 3. Fix `:level` / `:lvl` overworld level & EXP synchronization in `AC02.cs` and `Equip.cs`:
  - Call `p.Eqs.SendExp()`, `p.Send_5_3()`, and `p.Eqs.Send8_1(true)` so the client overworld HUD, status screen, and state update immediately without requiring battle entry.
  - Enhance `Equip.cs:SetLevel` to trigger `SendExp()`, `player.Send_5_3()`, and `Send8_1(true)` when caller is `Player`.
- [x] 4. Verify job skill unlock, level formula, and overworld level packet generation with automated script in `.codex-verify/`.
- [x] 5. Build entire solution and verify 0 errors and 0 warnings.
- [x] 6. Update `README.md`, write technical documentation in `docs/73_reborn_job_skills_and_overworld_level_synchronization.md`, and ensure `Data/ServerDataBase.db` is 100% untouched.
