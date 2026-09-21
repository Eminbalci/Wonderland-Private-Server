# Critical hits - 2026-09-19

## Source of item chances
- Previous Equip.Crit and EquipmentCell.Crit returned zero; active PvEBattleManager never rolled or multiplied critical damage. Battle.Apply_Crit is an unused legacy stub and is not the active path.
- Original client tooltip244102..24413b and2477a6..2477cd checks special137 and nonzero grade, then displays grade*2+10 percent. Native loader3ce2e4..3ce334 decodes these bytes. Its object adds four bytes before disk records: native0x31/0x33 correspond to Item.dat45/47, not49/51.
- Configure-CriticalHits.py extracts217 flagged item IDs from original Item.dat into Data/critical_hits.json. Sky Sword11101 / +1 11103:24%; +2 11105:30%; +5 11048:36%. Steel Falchion10068 has a native170% value; combat caps per-item and total chances at100%.
- EquipmentCell and Equip inherit Item.Crit. EquipManager sums only equipped slots. Pet uses its own six Eq_* IDs; no owner fallback. Unknown/unflagged equipment has0%; no invented innate monster/player chance.

## Damage behavior
- User asked to fix Crit. Multiplier preference question was optional and unanswered during implementation;1.5x was explicitly announced as the provisional default. It is configurable in critical_hits.json.
- Each damaging attack action rolls once per attacker through the existing locked random generator, including ordinary attacks, skills and individual combo participants. Crit applies after HotBlooded/combo, before shield/defense reduction. Fractional damage rounds down, integer overflow saturates.
- Heal/buff/status/capture/flee/defend actions do not roll a critical damage multiplier. Existing status skill classification/damage logic is unchanged.
- The same final damage is sent in50:1 and subtracted once from the target's server HP. No new51:1 immediate absolute HP packet, GM message, popup or client binary patch was added.
- Native target hit-result6 accepts0/1 only (39915c); changing it to2 would skip damage. Existing defense reaction7 remains0/1. This implementation does not claim a dedicated critical visual marker; game rendering remains to be replayed after deployment.

## Validation
- Compiled successfully; only existingCS1998 warnings at Tent.cs155/WloWorldNode.cs255.
- Test-CriticalHits.ps1:7887 checks, including every client item record's chance, full0..99 probability boundaries, equipment swaps/removal, capped rates, pet ownership isolation and8 real asynchronous ExecuteTurn scenarios (normal/critical skill, basic attack, shield, defend, HotBlooded, pet, mixed combo). Packet damage and server HP agree.
- Test-StatAllocation136, Test-CombatVitals48, Test-CombatSpeed210, Test-MallForging3535 all pass on the new baseline stage.
- Deferred combat timing build also passes7887 critical checks.

## Staging, not deployment
- critical-hit-bin and refreshed client-gacha-bin/mall-forging-bin/stat-allocation-bin include all pending work with baseline combat5000ms/+750ms. client-gacha-deploy-manifest.json now tracks critical_hits.json as well as refreshed binaries.
- combat-handoff-bin retains the deferred4500ms/+250ms timing and includes this fix. Source retains that deferred timing.
- Startup loads critical_hits.json through PathHelper alongside existing gacha/forging configuration. No packed PhxItemInfo schema change or saved character migration.
- Deploy-ClientGacha.py rehearsal passed on a database copy: one retired Lucky offer removed,199 retained, other tables unchanged. Live DB, runtime binaries and client unchanged. User previously deferred deployment.


Deployment 2026-09-19T14:42:57: all pending changes applied from combat-handoff-bin, including4500ms/+250ms timing; eight runtime file hashes and original client verified; character tables unchanged. Backup: D:\Game Private\Wonderland-Private-Server\.codex-verify\deploy-backup-20260919-144257-032053-all-pending. Server/game left stopped for user restart.
