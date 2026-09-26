# Combat handoff pacing - 2026-09-19

User reports long pause from allied action to enemy turn. There is no separate side-switch delay: every action awaits its animation estimate. Before patch, spell/support/status5000ms plus combo750ms; basic2300ms; catch6000ms.

Small tuning: spell/support/status4500ms and combo margin250ms. Spell combo5750->4750ms, basic combo3050->2550ms, single spell5000->4500ms. Basic solo, enemy attack, capture return and escape budgets remain unchanged. SPD scheduling unchanged. No client or database changes.

Main Compile passed with existing CS1998 warnings. Existing pacing/escape28 + source3, defense/capture21 + source1, action-selection17 and speed-order suite passed. Old test stub updated for preexisting Eqs.Send8_1 call; timing expectations updated to4500ms. Speed suite requires Windows PowerShell/.NET Framework. These tests validate server logic, not measured live client animation duration.

Stage: .codex-verify/combat-handoff-bin. Deploy-CombatHandoff.ps1 requires stopped server, backs up four binaries, verifies copied hashes and unchanged database, leaves server stopped. No client update needed.
`nRefreshed pending build with original-client gacha code. Still NOT deployed; client-gacha-bin retains5000ms/750ms timing, combat-handoff-bin retains4500ms/250ms.

2026-09-19 Staging refreshed with current Forging support (see Mall-Forging-20260919.md). Deferred4500/250 timing remains, not deployed.


Deployment 2026-09-19T14:42:57: all pending changes applied from combat-handoff-bin, including4500ms/+250ms timing; eight runtime file hashes and original client verified; character tables unchanged. Backup: D:\Game Private\Wonderland-Private-Server\.codex-verify\deploy-backup-20260919-144257-032053-all-pending. Server/game left stopped for user restart.
