# Capture completion and rejection reasons - 2026-09-19

Scope: server only. Native client and persisted pets unchanged.

Findings:
- Runtime database read-only inspection and login logs confirm Mizaki has four carried pets: Robinson (slot 1), Xaolan (slot 2), Grape Mons (slots 3 and 4). Full roster and duplicate-template checks each force capture failure, regardless of HP or random roll. The two existing Grape Mons were preserved.
- Existing probability formula: clamp(75 + 5 * levelDifference + 20 * (1 - hpFraction), 15, 98), with an 85% floor if player level is at least monster level. A level 7 player capturing a level 2 monster below half HP reaches 98% before roster restrictions. Formula unchanged; probability comparison now uses the conventional strict roll < chance boundary.
- Native capture success at 0x379a22 already removes the target through 0x391468; other outcomes proceed via 0x378f10. Client has result/recovery states after the initial capture effect. The next server action resets animation state, so the old 3500 ms budget could cut off recovery.

Changes:
- Reserve 6000 ms for capture, including approach, result and return. All other action timings unchanged. This is a server estimate, not a measured animation duration or client completion ACK.
- Remove the extra AC11:1 leave packet after capture success; native skill 10008 handles removal.
- Report separate full-roster, duplicate-monster, combined full/duplicate, and random-resistance failures.
- Log HP, levels, chance, roll and outcome for each attempt so future reports can be diagnosed.

Validation:
- Main Compile/referenced core compilation passed; existing CS1998 warnings only.
- Test-CaptureCompletion.ps1: 64 executable checks using the unchanged production capture phase, fake transport/roster and controlled clock. Covers success, RNG boundaries, repeated failed attempts, full roster, duplicates, combined restrictions, no early mutations, no double leave.
- Test-DefenseCapture.ps1: 21 executable + 1 source check.
- Test-CombatPacing.ps1: 28 executable + 3 source checks.
- Test-CombatActionSelection.ps1: 17 checks.
- Test-PetNativeProtocol.ps1: 55 checks against staged binaries.
- Total 189 checks; source whitespace check passed.
- Live replay still needed for failed capture returning both fighters and successful capture returning the actor without interruption.

Deployment pending: staged DLL/PDB in .codex-verify/main-bin, current server process holds runtime files. User will close and restart server manually.

Deployment completed 2026-09-19 11:00:03: runtime DLL/PDB hashes match staged build. Backup: D:\Game Private\Wonderland-Private-Server\.codex-verify\deploy-backup-20260919-110003-453-capture-completion. Server left OFF for manual start.
