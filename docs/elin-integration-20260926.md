# Develop integration and Elin quest fixes (2026-09-26)

Rebased onto upstream Develop at 0713b4b. That upstream tree had removed the gameplay fixes from 52b3175; this integration restores those fixes and retains all eight newer upstream file changes. The project-file conflict keeps both the disabled-event catalog and the updated database content entry.

Elin changes preserve per-player mechanism state, open the lever passage on one click, dispatch authored well-rope regions, and use the native question handshake that avoids prematurely advancing the descent movie. Lab frame refreshes no longer show the hidden Girl actor over Elin. The completed key quest does not clear the separate mirror side quest marker; that marker clears on its own completion flag.

Validation: full server/dependency rebuild passed with two existing async warnings. Elin 874, well 123, lever 30, lab/marker 81, Roca 159, and story regression 1,246 checks passed (2,513 total). Upstream integration 29, combat scheduling 210, and pacing/escape 39 additional checks passed. Pacing source tests require PowerShell 7; the packet fixtures run in Windows PowerShell with .NET Framework.

Portable focused checks:

```powershell
powershell -NoProfile -File .codex-verify/Test-ElinDisplay.ps1 -BuildDirectory <built-server-directory>
powershell -NoProfile -File .codex-verify/Test-ElinLever.ps1 -BuildDirectory <built-server-directory>
powershell -NoProfile -File .codex-verify/Test-WellTransition.ps1 -BuildDirectory <built-server-directory>
```

The build directory needs the server executable, dependency DLLs, SQLite native interop, and matching Data assets. These scripts isolate database writes under .codex-verify/elin-tests-fixtures. No player database, runtime binary, or generated fixture is part of this commit. The integrated build has not been deployed or replayed in a live client; the installed lab hotfix remains unchanged.
