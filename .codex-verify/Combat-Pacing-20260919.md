# Combat pacing and single escape - 2026-09-19

Scope: server only, as requested. WLRI/aLogin.exe is unchanged.

Evidence:
- ExecuteTurn waited only 400 ms for defend, 800 ms for catch, 1200 ms for spells/attacks and 1500 ms for combos.
- Native client AC50:1 at 0x3920a0 resets camera/action state through 0x398700 before starting the received action. No animation-complete ACK was confirmed in this client's battle handlers; timing below is a server estimate, not a measured or client-acknowledged duration.
- Native AC11:1 at 0x392bb4 starts leave animation state 0x27 via 0x37896c. The server previously also sent skill 60041 before that removal, replaying escape. Actual log at 10:23:49/50 contains player and pet flee commands, which formerly emitted two separate skill-animation packets before the leave packets.

Changes in PvEBattleManager.cs:
- Central pacing budgets: basic attack 2800 ms; skill/heal/buff/status 5500 ms; defend 1200 ms; catch 4000 ms; flee 3000 ms. Combo uses the longest member budget plus 750 ms.
- Await pacing before the next animation, death notification, or next-round prompt. Catch now waits before removing the captured target.
- Flee skips skill 60041 playback and uses one AC11:1 per living player/pet, pets first. Do not send leave animation to monsters or dead fighters. Do not play victory fanfare on flee.
- Close/release the scene after the departure budget. Claim completion synchronously and retain battle registration until cleanup so duplicate flee cannot replay the sequence or start a fresh encounter during exit.

Validation:
- Core Build and main Compile passed; existing async-without-await warnings remain in Tent.cs and WloWorldNode.cs.
- Test-CombatPacing.ps1: 28 executable checks using unchanged production methods with a controlled clock/transport, plus 3 packet-order/source checks.
- Test-CombatActionSelection.ps1: 17 executable checks (PowerShell 7).
- Test-PetNativeProtocol.ps1: 55 native layout/session checks (Windows PowerShell/.NET Framework).
- git diff --check on the changed source passed.
- Total: 103 checks. Native rendering and the actual duration of each skill have not been replayed. Test Flame Attack and Gale on separate targets, normal attacks, combo, capture, and fleeing with both character and pet after deployment.

Deployment: staged .codex-verify/main-bin/wlo.pserver.core.dll and .pdb. Running server currently locks bin/Debug; awaiting shutdown choice. User will start server manually after deployment.

Deployment completed: server exited during final checks; DLL was unlocked. Copied staged DLL/PDB to bin/Debug and verified both SHA256 hashes. Backup: .codex-verify/deploy-backup-20260919-104001-932-combat-pacing. Server left stopped for the user to start manually.
