# SPD ordering including capture - 2026-09-19

Cause: ExecuteTurn ran all support actions, then all captures, all player/pet attacks, and only then all enemy attacks. The old pipeline did not use SPD to order these actions.

Change:
- BuildSpeedOrderedActions snapshots actor SPD for the current round and sorts descending across attacks/status, capture, support and PvE monsters. Grid position deterministically breaks ties instead of packet arrival order.
- Execute existing packet/damage/outcome code sequentially for each scheduled step, retaining animation waits.
- Dual combo only joins adjacent offensive actions targeting the same coordinate; it cannot pull a slow actor ahead of capture, support or an enemy turn.
- Existing defend setup and flee behavior remain outside this queue.
- Each actor is rechecked when its step runs. Dead/captured monsters cannot retaliate. Capture does not retarget a different monster if the selected target died earlier.
- Timing budgets, packet layouts, capture probability and pet data unchanged.

Validation:
- Main Compile/core compilation passed; pre-existing CS1998 warnings only.
- Test-CombatSpeed.ps1: 210 checks invoke scheduler from compiled core; mixed SPD, reversed arrival order, stable ties, support, PvP, combo boundaries, 100 randomized actor sequences.
- Test-CaptureCompletion.ps1: 70 checks, including dead/stunned actor and selected target dying while another monster survives.
- Test-DefenseCapture.ps1: 22 checks.
- Test-CombatPacing.ps1: 31 checks.
- Test-CombatActionSelection.ps1: 17 checks.
- Test-PetNativeProtocol.ps1: 55 checks.
- Total 405. Source whitespace check and scoped comparison against pre-turn source passed. Native client gameplay not replayed.

Deployment pending server shutdown. Staged DLL/PDB in .codex-verify/main-bin; user starts server manually after deployment.

Deployment completed 2026-09-19 11:09:59: runtime DLL/PDB match staged SHA256. Backup: D:\Game Private\Wonderland-Private-Server\.codex-verify\deploy-backup-20260919-110959-311-combat-spd. Server left OFF for manual start.
