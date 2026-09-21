# Combat command selection fix - 2026-09-19

Client checked: D:\Game Private\WLRI\aLogin.exe (native disassembly).

Confirmed cause: HandleBattleAction sent AC52:1 between character and pet commands. Native AC52:1 handler 0x39783c clears all fighters' selected/ack flags (0x21d1, 0x21d2, 0x21ba) and calls 0x3a0c98 with fighter index 1. It starts a round; it does not select a requested pet. AC50:6 at 0x3970e4 only writes fighter field 0x2784. The client already advances to the next owned fighter through 0x398264 -> 0x3981d8, checking owner ID at 0x21dc and living HP. Server resets therefore reopen the character menu.

Change: remove mid-round AC50:6/AC52:1 and never remap a duplicate/invalid source grid to another owned fighter. Keep AC53:5 acknowledgements and round-start prompts. This also prevents a repeated character defend command from being recorded as the pet's choice.

Validation: Test-CombatActionSelection.ps1 executes the production handler with isolated transport/skill/turn doubles: 17 checks passed (both attacks, each actor defending separately, duplicate defend, invalid/foreign/dead actor, PvP side, dead owner, pet first, processing/finished battle). Core Build and main Compile passed. Full main Build packaging is blocked by the pre-existing missing Data/ServerDataBase.db.temp; no database created or changed.

Native menu placement and the reported defense animation have not been replayed in the client. If the animation persists after this fix, capture the selected skills and actual AC50:1 packets; do not assume the rendering symptom has been proven fixed by the handler tests.

Prepared core binary: .codex-verify/main-bin/wlo.pserver.core.dll. Running server locks bin/Debug/wlo.pserver.core.dll, so the fix is not active until that file is replaced with the server stopped and restarted.

Deployment: copied validated wlo.pserver.core.dll and .pdb into bin/Debug, SHA256 matches staged build. Backup: .codex-verify/deploy-backup-20260919-102202-893-combat-selection. Old process exited via safe FormClosing. Automatic restart was canceled; user subsequently requested leaving server closed and will start it manually. No live gameplay validation performed.
