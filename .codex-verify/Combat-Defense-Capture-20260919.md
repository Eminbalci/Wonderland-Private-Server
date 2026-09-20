# Combat defense, timing, failed capture - 2026-09-19

Scope: server only. Native client and game database unchanged.

Changes:
- Selecting defend only records the eligible living actor for this turn; no self-cast or selection delay. Incoming attack packets set the native defense reaction only for the target that selected defend. Existing defense damage reduction remains; spell Shielded status is separate.
- Reduced action budgets by 500 ms: basic attack 2300; skill/heal/buff/status 5000; capture 3500; flee 2500. Combo remains longest member plus 750. Victory/defeat waits reduced to 1300/700. These are estimates, not client animation-complete acknowledgments.
- Capture success sends skill 10008; failure sends skill 10009. Gameplay outcome and pet capacity checks remain unchanged.

Native evidence from the actual WLRI client:
- AC50 target reaction byte 1 selects defense skill 60021 (0x39911b); normal reaction is 0.
- Successful capture skill 10008 removes its target in the native animation path at 0x379a22. The server previously sent it even on failure.
- Actual data/Skill.dat records identify 10008 as Capture and 10009 as Fail to Capture.

Validation:
- Main Compile and referenced core compilation passed (existing CS1998 warnings only).
- Test-CombatPacing.ps1: 28 executable checks + 3 source/order checks.
- Test-CombatActionSelection.ps1: 17 executable checks.
- Test-DefenseCapture.ps1: 21 executable checks + 1 source check, using extracted production packet builders/defense phase/timing method.
- Test-PetNativeProtocol.ps1: 55 checks against staged assemblies under Windows PowerShell/.NET Framework.
- Total 125 checks. Source diff whitespace check passed.
- Native gameplay replay still required: defend selection stays idle until attacked; pet and player defense independent; failed capture keeps monster visible; shorter pacing does not interrupt tested skills.

Deployment pending: running server holds bin/Debug/wlo.pserver.core.dll. Staged DLL/PDB in .codex-verify/main-bin. User asked to close server before copy and will start it manually afterward.

Deployment completed at 2026-09-19 10:50:43: DLL/PDB copied to bin/Debug; SHA256 verified for both. Backup: D:\Game Private\Wonderland-Private-Server\.codex-verify\deploy-backup-20260919-105043-149-defense-capture. Server left stopped for manual start.
