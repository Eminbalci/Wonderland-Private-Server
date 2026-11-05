# 84 - Server GUI World Studio Modernization & Client PK/Join Toggle Synchronization

## 1. Overview & Objective

This specification details the comprehensive modernization of:
1. **Server Administrator Console World Studio**: Modernizing the World section (`MainForm1.cs`, `MainForm1.ModernUi.cs`) to integrate directly with live runtime architecture (`MapManager`, active players, overworld bazaar stalls, dynamic NPCs, and portals).
2. **PK & Join Settings Button Protocol (AC 33)**: Correcting the client packet handling for the settings dialog (`form_module`, `FUN_003c4138`), resolving the failure of the PK toggle button to toggle in-game and persist.

---

## 2. Server GUI World Section Modernization

### 2.1 Navigation Architecture & Direct Tab Resolution (`MainForm1.ModernUi.cs`)
The Server GUI Modern Navigation Category "World" was previously using fragile string title matching (`FindTabByTitle`) which was susceptible to leading whitespace discrepancies in tab titles.

The category navigation was updated to bind directly to strongly-typed tab instances:
- **Live Map & NPCs**: `() => this.tabMapNpcStudio`
- **Portals**: `() => this.tabPagePortals`
- **NPC Names**: `() => this.tabNpcResolver`
- **Dialogue IDs**: `() => this.tabTalkResolver`

### 2.2 Live World & Map Entity Studio (`MainForm1.cs`)
Previously, `tabMapNpcStudio` only read static Eve quest entries from `Eve.Emg`. It was overhauled into a complete real-time World Entity Monitor:
- **Online Players on Map**: Discovers all active characters on the selected map from `MapManager.Instance.GetMap(mapId).PlayersList`, displaying character ID, name, level, vital HP/SP percentages, and live coordinates.
- **Active Player Bazaars**: Retrieves all active overworld stalls from `PlayerStallBazaarEngine.GetStalls()`, displaying stall owner, title, coordinate location, and item catalog count.
- **Portals & World Waypoints**: Reads portal connections from `PortalDataBase.GetPortalsForMap(mapId)` and resolves target destinations (`-> Map #X at (DstX, DstY)`).
- **Dynamic NPCs & Spawns**: Reflects in-memory dynamic entity spawners from `activeMap.NpcList`.
- **Static Quest NPCs**: Retains full Eve bytecode decompilation and branch inspection for static NPCs.

### 2.3 World Management Actions
The World Studio top control panel now includes administrative operations:
- **Warp Player Here (`btnWarpHere`)**: Allows the administrator to select an online player from GM Studio or the live player dropdown and teleport them directly to the inspected map.
- **Clear Ground Drops (`btnClearGround`)**: Cleans up ground items and dropped loot on the selected map instance (`activeMap.GroundItemList?.Clear()`).
- **Entity Inspector**: Clicking any entity in the grid displays rich inspector details in the event panel:
  - Players: Real-time attributes, stats, unallocated points, position, flags (PK, trade, team lock), gold/bank balances.
  - Stalls: Vendor name, shop title, and item-by-item catalog with quantities and gold prices.
  - Portals: Source portal ID, destination ID, and destination coordinates.

---

## 3. Client PK & Join Button Toggle Protocol (AC 33)

### 3.1 Delphi Client Protocol Analysis (`form_module` / `FUN_003c4138`)
Analysis of the decompiled client (`decompiled/aLogin_decompiled.c`, lines 382850-382920) revealed:
- Button Index 0 (PK Toggle): Invokes `FUN_002d6ad8(socket, 0x21, 1, 0)`. Dispatches **Action Code 33, SubCode 1, with 0 additional payload bytes**.
- Button Index 1 (Join/Fight Toggle): Invokes `FUN_002d6ad8(socket, 0x21, 2, 0)`. Dispatches **Action Code 33, SubCode 2, with 0 additional payload bytes**.

### 3.2 Root Cause in Legacy Server Handler
The previous implementation in `Src/Network/ActionCodes/AC33.cs`:
- Handled Subcode 1 by calling `HandleToggle`, which expected an extra byte (`r.Unpack8()`) for setting type (1 for PK, 2 for Join). Because the client sends 0 extra bytes, unpacking failed or returned 0 ("Unknown setting type: 0").
- Handled Subcode 2 as `SendCurrentSettings(p)` instead of toggling the Join mode.

### 3.3 Implementation Fix (`AC33.cs`)
`AC33.cs` was restructured to handle both native 0-byte client button dispatches and legacy/extended packets:
- **SubCode 1 (`HandleSubCode1`)**:
  - If buffer length <= 6 (0 extra bytes): Directly toggles PK mode (`TogglePk(p)`).
  - If buffer length > 6: Checks inner setting type for backward compatibility.
- **SubCode 2 (`HandleSubCode2`)**:
  - If buffer length <= 6 (0 extra bytes): Directly toggles Join mode (`ToggleJoin(p)`).
  - If buffer length > 6: Returns current serialized settings.
- **Persistence (`PersistSettings`)**:
  - Automatically saves the updated settings string (`PK JOIN CHAT TRADE`) to `charactersExtData.Settings` in SQLite via `cGlobal.gCharacterDataBase.SaveSettings(charID, settings)`.
- **Wire Confirmation & Multi-Button Sync**:
  - Broadcasts both single confirmation (`[33, 1, type, val]`) and complete serialized settings (`[33, 2, pk, join, trade, chat]`) via `p.Settings.ToArray()` to synchronize all visual buttons in the client's `form_module`.

---

## 4. Verification Evidence

1. **Compilation**: `Wonderland Private Server.sln` compiles with 0 errors and 0 warnings on .NET Framework 4.6.2.
2. **Settings Serialization & Wire Test**:
   - Initial settings: `PKABLE=True`, `JOINABLE=True`.
   - Toggle executed: `PKABLE=False`, `JOINABLE=False`.
   - Wire packet format: `F4-44-15-00-21-00-00-00-02-00-00-00-02-00-00-00-02-00-00-00-01-00-00-00-1F` (AC 33, SubCode 2, with current flags).
   - Test result: **PASS**.
3. **World Studio Real-Time Resolution**:
   - Entities load correctly for maps with online players, overworld bazaar stalls, dynamic spawns, and static Eve NPCs.
   - Ground drop clearing and player warp hooks execute cleanly without null reference exceptions.
