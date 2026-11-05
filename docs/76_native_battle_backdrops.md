# Native battle backdrop regions

Battles previously chose scenery through a map-ID fallback. `Ground.MMG`
already contains a default selector per map and optional spatial overrides.
The server now loads that metadata during startup and resolves the initiating
leader's position once for all participants. This applies to the main PvE/team
PvP path and the older duel entry point.

## Native format and evidence

The named archive ends with 29-byte index entries and a UInt16 entry count.
Each map's terrain tail follows the collision grid, sound zones and objects:

| Field | Size |
| --- | --- |
| Unresolved map parameter, usually 100 | Byte |
| Default backdrop selector | Byte |
| Regional override count | Byte |
| Each region: selector, left, top, right, bottom | Byte + four UInt16s |

Alogin `FUN_003f830c` reads these fields into the map structure;
`FUN_003fa0b8` draws the region rectangles and labels them using scene
`59000 + selector`. `FUN_0038e4f4` reads AC11:250's backdrop selector separately
from the following formation metadata byte. The packet now names these two
bytes explicitly and retains zero compatibility metadata.

Region records keep their native order. Selection uses the first matching
rectangle with inclusive edges, followed by the map default. Exact overlap
edge tie-breaking on the closed Legacy server remains unverified.

## Resource loading and failure behavior

The loader uses `PathHelper.GetDataFilePath("Ground.MMG")`, like other native
resources. It reads the original archive directly; no generated JSON, new
settings, SQL schema changes or additional bundled assets are required.

A successful load atomically publishes a complete immutable catalog. Battles
perform only an in-memory map lookup and a scan of that map's rectangles.
Missing or malformed archives log a warning and preserve the previous catalog;
on first-load failure, the compatibility selector remains available. Unknown
scene selectors are retained for the native client's missing-scene fallback.

Startup writes `[BattleBackdrop] Loaded Ground.MMG backdrops for N maps.`
Restart after replacing the archive. No new account or database setup is needed.

## Build and validation

From Visual Studio Developer PowerShell, build the solution:

```powershell
MSBuild 'Wonderland Private Server.sln' /restore /p:RestorePackagesConfig=true /p:Configuration=Debug /m:1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/Run-GroundBackdropTests.ps1
```

The tests compile against the production assemblies and use independently
authored native bytes. They cover bounds, record ordering, overlaps, truncated
and malformed archives, unknown map IDs and default selection. No client data
or database is needed for this command.

Optionally test a native archive:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/Run-GroundBackdropTests.ps1 -GroundPath Data/Ground.MMG
```

This additionally checks the manager against the explicitly supplied archive
and confirms missing/corrupt reloads cannot replace a good catalog. Known
positions and counts are asserted only for the exact reference archive SHA-256
recorded in the test; other client versions still receive the general checks.
The reference has 1,147 maps and 1,398 region rectangles across 101 maps. Native
archives are not included with the tests. Interactive live battle validation
is separate from these automated decoder and manager checks.
