# Gameplay integration — 2026-09-26

Upstream: `origin/Develop` at `0713b4b4bae2c8cabf772e0cdbc97180dcc1e2c2`. Fetch and merge completed with `Already up to date`; no merge conflict was produced. Code commit: `1b3e2a4` (following local integration `19a0b5d`).

Full source build: success, two pre-existing CS1998 warnings in Tent.cs and WloWorldNode.cs. NuGet restore required the Windows runtime identifier; this was supplied to the validation build without editing project settings.

| Suite | Checks |
| --- | ---: |
| Native loot / ground transfers | 9,372 |
| Inventory protocol / state | 493 |
| Vehicles | 135 |
| Clive | 401 |
| Maka / Roca | 474 |
| Fred finale | 534 |
| Roca death | 486 |
| Total | 11,895 |

The first Fred/Roca fixture runs were rejected because their empty maps had no registered NPCs. The committed fixtures now populate actors from native map data and model movement to the actor before clicking; server NPC/proximity validation remains enabled. Both final runs passed, including isolated persistence/reload checks.

New portable checks (Windows PowerShell / .NET Framework):

```powershell
powershell -NoProfile -File .codex-verify/Test-NativeLootGround.ps1 -BuildDirectory <build-output>
powershell -NoProfile -File .codex-verify/story-regression/Test-FredFinale.ps1 -BuildDirectory <build-output> -ClientDataDirectory <client-data>
powershell -NoProfile -File .codex-verify/story-regression/Test-RocaDeath.ps1 -BuildDirectory <build-output> -ClientDataDirectory <client-data>
```

The build output must contain the server EXE, dependency DLLs, SQLite interop and matching Data assets. Tests use repository native assets; Fred/Roca also compare independent client EVE data. Generated databases stay under `.codex-verify` and must not be committed.

The full integrated build was not deployed. Installed core/host hashes and tracked `Data/ServerDataBase.db` remained unchanged. The running server continued normal autosaves to its separate runtime database during validation. No runtime database was staged or restored.

[PR title and body](PR-gameplay-fixes-20260926.md) describe only local changes against the target above. The previously restored gameplay changes are part of the actual PR diff because upstream `0713b4b` had removed them; see [earlier integration notes](elin-integration-20260926.md).
