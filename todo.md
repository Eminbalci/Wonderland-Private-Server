# Implementation Plan: AoE Multi-Target Per-Record Animation Synchronization

- [x] 1. Analyze `FUN_003abf3c` outer loop and `FUN_003ac7ac` target trigger semantics in `aLogin.exe`
- [x] 2. Refactor Phase 4 `pAttackAnim` in `PvEBattleManager.cs` to emit authentic 19-byte records (`{ 0x11, 0x00, actorX, actorY, skillId, 0, 1, targetX, targetY, 1, def, 1, stat, dmg, hit }`) per hit target
- [x] 3. Refactor Phase 2 `pSupport` in `PvEBattleManager.cs` to emit authentic 19-byte records per ally target with visual buff overlays
- [x] 4. Build solution with `dotnet build "Wonderland Private Server.sln"` and verify 0 warnings, 0 errors
- [x] 5. Verify multi-target record serialization and boundary alignments
- [x] 6. Update root `README.md` and `docs/60_aoe_multi_target_damage_hit_animation_sync.md`
