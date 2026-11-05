# 76. wloweb Map Warp Parity and Arrived State Lifecycle

## 1. Executive Summary

This specification documents the 1:1 map warp and portal resolution parity between the C# server engine and `wloweb` (`Wonderland-Gonline`).

Previously, portal transition in C# relied on artificial wall-clock cooldowns (`elapsedMs < 2500`) and arbitrary spawn-point Euclidean distance guards (`spawnDist < 120px`). This caused legitimate immediate warps (or fast player movements into adjacent warps) to be falsely rejected with `AC 20:8` unfreeze signals. In `wloweb`, portal bouncing on arrival is prevented via the discrete `Arrived` state lifecycle: upon spawning on a map, `Arrived` is set to `true`, suppressing immediate portal triggers until the client issues a valid movement waypoint (`AC 06:1`), which resets `Arrived` to `false`.

Furthermore, portal lookup, door-to-portal linking (`DoorPortal`), and scene transition targets (`WarpEntry`) have been aligned with `wloweb`'s `internal/world/portal.go` and `internal/server/world.go`.

---

## 2. Core Architecture and State Machine

### 2.1 The `Arrived` Lifecycle
- **Definition:** Added `public bool Arrived { get; set; } = true;` to [`Player.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Player.cs).
- **Initialization & Map Entry:**
  - On login or map spawn (`Warp_In` in [`Map.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs)), `src.Arrived = true`.
- **Movement Departure:**
  - In [`AC06.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC06.cs) (`Recv1`), when `movedDistance > 0`, `p.Arrived = false`.
- **Portal Step Suppression (`AC 20:8`):**
  - In [`AC20.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC20.cs) (`Recv8`) and `Teleport` (`TeleportType.Regular`), if `p.Arrived == true` on a non-tent map:
    - Emits debug log: `[AC20.Recv8] Player {p.CharName} arrived flag active. Ignoring immediate portal re-trigger.`
    - Emits client unfreeze: `Tools.FromFormat("bb", 20, 8)`
    - Exits without executing portal bounce.

### 2.2 Portal Lookup Priority Hierarchy (`LookupPortal`)
In [`Map.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs):
1. **Tent Maps (`MapType.Tent`):** Returns player to recorded overworld spawn (`TentReturnMap` or `PrevMap`).
2. **Explicit Catalog Overrides (`MapTransitions`):**
   - Map 10001 (Astrologer Laura Cave) Portal 1 $\to$ Map 10000 at `(1892, 1655)`.
3. **Carnie Map (11094):**
   - If player has `CarnieReturnMap`, returns to saved visit origin.
   - Fallback: Map 11016 (Starter Beach) at `(1181, 243)`.
4. **Database & Override Tables:** Checks local dictionary overrides and `PortalDataBase` if configured.
5. **EVE EMG Map Warps (`mapData.WarpLoc`):**
   - **Priority A (ClickID Direct Match):** `warp.clickID == portalID && warp.mapID > 0`.
   - **Priority B (Geometric Reverse Match):** When player `(px, py)` is valid, searches target map return warps within `dist < 600`.
   - **Priority C (1-Based Index Match):** `portalID >= 1 && portalID <= warps.Count && warps[portalID - 1].mapID > 0`.
   - **Priority D (Gray Code Decoding):** Decodes `grayID = GrayDecode(portalID)`, matches `warp.clickID == grayID`.
   - **Priority E (Single-Exit Fallback):** If map has exactly 1 valid warp destination, select it.
6. **Invalid / Test Map Emergency Recovery:**
   - If `MapID < 1000`, teleports to Map 12000 at `(892, 734)`.

### 2.3 Door and Scene Transition APIs
- **`DoorPortal(ushort clickID, out ushort portal)`:**
  - Implemented on `IMap` and `GameMap`.
  - Scans `mapData.Npclist` for objects containing `"door"` in their name with valid link array (`unknownbytearray2[0]`), returning the linked portal ID.
- **`WarpEntry(ushort click, out ushort dstMap, out ushort dstX, out ushort dstY)`:**
  - Implemented on `IMap` and `GameMap`.
  - Resolves transition target records in EVE Opcode 1 (`dialog1 == 3`, `transitionType`).
  - Integrated into [`EveEventInterpreter.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs).

---

## 3. Verification & Parity Matrix

| Feature | wloweb (`portal.go` / `world.go`) | C# Server (`Map.cs` / `AC20.cs`) | Parity Status |
| :--- | :--- | :--- | :--- |
| **Arrived State Flag** | `c.arrived` (`true` on map enter, `false` on move) | `Player.Arrived` (`true` on map enter, `false` on move) | 1:1 Matched |
| **Bounce Guard** | Rejects portal trigger if `c.arrived == true` | Rejects portal trigger if `p.Arrived == true` | 1:1 Matched |
| **Timer / Distance Hack** | None (pure event/state based) | Removed 2500ms cooldown and 120px distance check | 1:1 Matched |
| **Carnie Fallback** | `11016, 1181, 243` | `11016, 1181, 243` | 1:1 Matched |
| **Astrologer Laura Exit** | `10000, 1892, 1655` | `10000, 1892, 1655` | 1:1 Matched |
| **DoorPortal API** | `m.DoorPortal(click)` | `CurMap.DoorPortal(clickID, out portal)` | 1:1 Matched |
| **WarpEntry API** | `w.WarpEntry(mapID, click)` | `CurMap.WarpEntry(click, out dstMap, out dstX, out dstY)` | 1:1 Matched |
| **Prelude Packets** | `20:7`, `23:32`, `23:112`, `23:132` | `20:7`, `23:32`, `23:112`, `23:132` | 1:1 Matched |
