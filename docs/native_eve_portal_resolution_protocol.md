# Native Eve.emg Portal Resolution Protocol

## Overview
This document specifies the multi-priority portal and warp resolution engine in Wonderland Online, driven entirely by native `Data/Eve.emg` binary data.

---

## Resolution Priority Order (`Map.LookupPortal`)

When a player steps on a portal tile or sends `AC 20 Sub 8 (PortalID, X, Y)`, the server resolves the destination through the following hierarchical pipeline:

1. **Local & Database Overrides (`Portals`, `PortalDataBase`)**:
   - Explicit database/GM portal mappings take initial precedence if configured.

2. **Geometric Reverse Matching (`pos(px, py)` vs Destination Return Warps)**:
   - Evaluates the player's physical step coordinate `pos(px, py)` against all destination maps' return coordinates within 600 world units.
   - Example: On World Map 60000 at `pos(3022, 3115)` (Kelan Village entrance), reverse-matches to Map 12000 (`(2882, 3009)` at distance 175.6).

3. **Direct Click ID Match (`clickID == portalID`)**:
   - Matches the client packet `portalID` directly with `WarpInfo.clickID` from `Eve.emg`.

4. **1-Based Index Match (`portalID <= WarpLoc.Count`)**:
   - Sequentially addresses the `n`-th warp entry in `WarpLoc` if click IDs deviate.

5. **Gray-Decoded Match (`GrayDecode(portalID)`)**:
   - Resolves bitwise Gray-code transformed portal indices.

6. **Single-Exit Fallback**:
   - If the current map possesses exactly one valid warp destination, it is used automatically (e.g. Map 10035, Map 12001).

7. **Invalid Map Emergency Recovery (`MapID < 1000`)**:
   - Safely returns trapped players to Holy Village (`Map 12000` at coordinates `892, 734`).

---

## Source Files
- [`wlo.pserver.core/Game/Maps/Map.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs) (`LookupPortal`)
- [`wlo.pserver.core/DataFiles/EveLoader.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataFiles/EveLoader.cs) (`LoadWarpEntries`, `LoadEntryExitEntries`)
