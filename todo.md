# Implementation Plan: Pure Debuff and Status Skills Damage Prevention

- [x] 1. Investigate `Skill.dat` and `SkillManager.cs` for skill classifications (offensive vs pure status/debuff)
- [x] 2. Examine `PvEBattleManager.cs` Phase 4 damage calculation for skills with `actionType == "status"` or non-damaging debuff/crowd-control effects
- [x] 3. Implement condition to prevent direct damage on pure debuff/status skills (e.g. Freeze, Sleep, Seal, Confuse, Poison, Paralyze) while maintaining status effect application and packing 0 damage / statType 0 into `AC 50:1`
- [x] 4. Verify compilation and test damage/status packet emission for pure debuffs vs damaging offensive skills
- [x] 5. Update root `README.md` and create technical documentation `docs/56_pure_debuff_and_status_skill_damage_isolation.md`
