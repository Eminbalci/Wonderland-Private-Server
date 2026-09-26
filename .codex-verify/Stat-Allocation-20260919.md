# Stat allocation / POINT display - 2026-09-19

## Report and evidence
- Screenshot: level9 Mizaki, HUD189/417 HP and136/0 SP; inventory189/290 HP,136/103 SP, POINT136, CON16/INT0/AGI4.
- Runtime log13:27:24 records the native request F4 44 09 00 08 01 00 01 21 03 00 00 00. Server correctly applied WIS+3 (base4), POINT0 and unlocked Fire Blast11005.
- AC08 then sent login initialization5:3 and Send8_1(true), which heals current vitals. CheckAndUnlockProgressionSkills sent another5:3 after the correct8:1 updates. This replaced client state after learning the skill.
- Native5:3 dispatcher2e0b2b calls4381c4; this writes the full character model, not just learned skills. Its legacy packet layout/static values are not suitable as a mid-session refresh. Login already follows with authoritative stat synchronization.
- Old staged binary reproduced serverWIS4/POINT0, unwantedSP136 refill and a5:3 packet after the final8:1. This is packet/harness evidence, not an in-game visual replay.

## Change
- AC08 unlocks skills then sends Send8_1() with no level-up healing. Existing point spending, persistence and pet allocation are unchanged.
- Progression skill unlock retains incremental8:2,5:12,5:11 and skill-book refresh; removes the redundant login snapshot so learned skills cannot overwrite current stats/POINT.
- No live character data was edited. No client patch required for this change.

## Validation
- Build passed; only the two existingCS1998 warnings in Tent.cs and WloWorldNode.cs.
- Test-StatAllocation.ps1:136 checks, including native WIS+3 request, Fire Blast unlock, five stats, single/multiple allocation, exact spending, insufficient points, no duplicate skill, current vitals preserved and native HUD replay.
- Test-HudVitals.ps1:62; Test-CombatVitals.ps1:48; Test-InventoryLogin.ps1:22. Total268 checks on the baseline staged build.
- Deferred combat build additionally passes the same136 allocation checks.
- Actual game replay pending deployment: allocate points across a skill threshold; verify POINT, STR/CON/INT/WIS/AGI and HP/SP in inventory/HUD; relog; enter combat.

## Pending deployment
- stat-allocation-bin, refreshed mall-forging-bin and client-gacha-bin include all prior pending forging/client-gacha work, with baseline combat5000ms/+750ms. Manifest refreshed for the four binaries.
- combat-handoff-bin includes this fix plus previously deferred4500ms/+250ms combat timing. Source retains the deferred timing.
- NOT DEPLOYED. Prior user explicitly deferred combined gacha/client restoration and combat-delay deployment. No runtime binaries, client executable or live DB were changed.
- Process inspection14:17 found neither Wonderland Private Server.exe nor aLogin.exe running; no process was stopped by this task.


Deployment 2026-09-19T14:42:57: all pending changes applied from combat-handoff-bin, including4500ms/+250ms timing; eight runtime file hashes and original client verified; character tables unchanged. Backup: D:\Game Private\Wonderland-Private-Server\.codex-verify\deploy-backup-20260919-144257-032053-all-pending. Server/game left stopped for user restart.
