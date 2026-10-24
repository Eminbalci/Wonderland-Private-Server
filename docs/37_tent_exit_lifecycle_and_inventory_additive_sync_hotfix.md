# Tent Exit Lifecycle and Inventory Additive Desynchronization Specification

## 1. Overview
This specification details the root causes and architectural fixes for:
1. Inventory item quantities displaying inflated values (e.g. 35 and 50) due to redundant additive inventory packet broadcasts on vehicle movement.
2. Tent exit failures where players were trapped inside personal tent virtual map instances due to EVE event collision and opcode handling.

---

## 2. Inventory Item Additive Desynchronization (`AC 23:5`)

### 2.1 Native Wire Protocol Analysis
In the native client binary (`aLogin.exe`), `AC 23:5` (`0x17:0x05`) is dispatched to `0x003dc9d0` and `0x003d7cdc`. The native implementation treats `AC 23:5` as an incremental additive update (`slot.Count += packet.Ammt`), not an absolute snapshot.
Stackable items are clamped to 50 (`min(50, slot.Count + packet.Ammt)`).

### 2.2 Defect Root Cause in Vehicle Movement Wear
In `wlo.pserver.core/Game/Inventory.cs:ApplyVehicleWear`:
```csharp
item.Damage = (byte)Math.Min(100, item.Damage + wearAmount);
if (item.Damage == 100) Game.PlayerRelated.VehicleManager.WreckVehicle(owner, vehicleItemId);
else owner.Send(new SendPacket(GetAC23_5()));
```
On each step taken on a raft (`48016`), `ApplyVehicleWear` transmitted `GetAC23_5()`. Across 34 sailing steps on Map 11016:
- Single-quantity items (`34038`, `34058`, `34169`, `41066`) accumulated +1 per step, reaching exactly 35.
- Multi-quantity consumables accumulated beyond 50, clamping to the client ceiling of 50.
The SQLite database (`ServerDataBase.db` -> `inventory`) was uncorrupted and preserved authentic quantities.

### 2.3 Resolution
- **`wlo.pserver.core/Game/Inventory.cs`:** Removed `else owner.Send(new SendPacket(GetAC23_5()));`. Vehicle damage is evaluated internally; item removal upon wreck is dispatched via `AC 23:9`.
- **`Src/Network/ActionCodes/AC29.cs`:** Removed redundant `p.Send(new SendPacket(p.Inv.GetAC23_5()));` calls on storage deposit and withdrawal where item deltas are already handled by `RemoveItem` (`AC 23:9`) and `AddItem` (`AC 23:5`).

---

## 3. Tent Entry Coordinate Recording and Exit Lifecycle (`AC 65:11` / `AC 20:8`)

### 3.1 Overworld Position Capture on Tent Entry
When a player enters or deploys a tent:
- **`Map.onEnterTent`:** Records `this.MapID`, `p.CurX`, and `p.CurY` into `p.TentReturnMap` and `p.PrevMap`.
- **`Tent.SetReturnLocation`:** Configures `_ownerMap` and `_mapx, _mapy` on the target tent instance and updates `exitPortal` 1.
- **`TeleportType.Tent`:** Re-evaluates overworld map coordinates before setting interior coordinates `(460, 700)`.
- **`Player.CurMap` Setter:** Automatically captures exterior `TentReturnMap` whenever switching from a non-tent map to a tent map.

### 3.2 Native Exit Opcode Alignment (`AC 65:11` and `AC 20:8`)
The client can trigger tent exit in two distinct manners:
1. **Doorway Click (`AC 65:11` / `AC 65:3`):** Dispatched from `FUN_0040d9b0`. `AC65.ProcessPkt` mapped only `case 3:`. `case 11` fell through to unhandled logging.
   - **Resolution:** Mapped `case 11:` alongside `case 3:` in `AC65.ProcessPkt` to `Recv3`.
2. **Doorway Step (`AC 20:8` Portal 1):** Stepping onto the exit rug sends `AC 20:8` with `portalID = 1`.
   - **Resolution:** Added explicit tent exit handler directly in `AC20.Recv8` to immediately warp the player back to `p.TentReturnMap` via `TeleportType.CmD`.

### 3.3 Root Cause of Tent Exit Deadlock: Virtual MapID Collision with `EveDat`
In `wlo.pserver.core/Game/PlayerRelated/Tent/Tent.cs`:
```csharp
public override uint MapID => _owner.CharID;
```
For a character with `CharID = 10001`, `CurMap.MapID` evaluated to `10001`.
Map `10001` in Wonderland Online is the authored world map for **Astrologer Cave (Laura's Tent) in Kelan Village**.
When the player stepped on the exit portal (`AC 20:8` with `portalID = 1`):
1. `EveEventInterpreter.TryExecuteEntry` loaded `EveDat.GetMapData(10001)`.
2. Map `10001` has an entry point with `clickID = 1`.
3. `InsideEntry` failed because interior tent coordinates `(399, 722)` did not match Astrologer Cave's entrance rectangle.
4. `TryExecuteEntry` transmitted `bb 20 8` (`[RESTORE UI / DIALOG END]`) and returned `true`.
5. `AC20.Recv8` returned immediately without executing `Teleport(...)`, permanently trapping the player inside the tent.

### 3.4 Complete Structural Resolutions
1. **`wlo.pserver.core/Game/Maps/Code/EveEventRuntime.cs`:**
   - In `TryExecuteEntry`, `TryExecuteRegion`, and `TryExecuteStoryArrival`: Added immediate guard `if (map is Game.Code.Tent || map?.Type == MapType.Tent) return false;`. Virtual tent maps never execute authored world map EVE scripts.
2. **`Src/Network/ActionCodes/AC20.cs`:**
   - In `Recv8`: Guarded both `TryExecuteRegion` and `TryExecuteEntry` with `!isTent`.
   - Bypassed cooldown and spawn proximity guards for tent interiors.
   - Added direct tent exit resolution routing to `p.TentReturnMap` -> `p.Tent.OwnerMap` -> `curTent.OwnerMap` -> `p.PrevMap` -> fallback Map 12000.
3. **`wlo.pserver.core/Game/Maps/Map.cs`:**
   - `mapData` property now returns `null` for `this.Type == MapType.Tent || this is Tent`, preventing collision with authored EVE datasets.
   - `LookupPortal` guarantees tent exit resolution without falling through to `EveDat.WarpLoc`.
   - Added Astrologer Cave (Map 10001) exit portal fallback to Map 10000 (Kelan Village).
   - In `Warp_In`: Enforced `PlayerFlag.InTent` addition when entering a tent, and removal when entering non-tent maps.
4. **`wlo.pserver.core/DataBase/CharacterDataBase.cs`:**
   - Updated offline character save fallback coordinates from `Map 10001 (500, 500)` to `Map 12000 (892, 734)`.
5. **Database Registration (`ServerDataBase.db`):**
   - Registered Map 10001 Portal 1 to Map 10000 `(1892, 1655)` in `portals` and `warp_destinations`.


### 3.5 Return Coordinate Preservation Hotfix (Resolution of Interior `(460, 700)` Overwrite)
#### Defect Root Cause:
In `Map.Teleport(TeleportType.Tent)`, `sender.CurX` and `sender.CurY` were prematurely reassigned to tent interior spawn coordinates `(460, 700)` immediately prior to invoking `targetTent.Warp_In`.
Inside `targetTent.Warp_In`, assigning `src.CurMap = this` triggered `Player.CurMap`'s property setter in `Player.cs`:
```csharp
TentReturnMap = new WarpData() { DstMap = base.CurMap.MapID, DstX_Axis = CurX, DstY_Axis = CurY };
```
Because `CurX` was already 460 and `CurY` was already 700, `TentReturnMap` was overwritten with interior spawn coordinates `(460, 700)`. When players subsequently exited the tent, they were teleported to the correct overworld map, but at `(460, 700)` instead of where they entered.

#### Resolutions Applied:
1. **`wlo.pserver.core/Game/Player.cs`:** Guarded `Player.CurMap` setter to only assign `TentReturnMap` if not already captured and explicitly reject interior coordinates `(460, 700)`.
2. **`wlo.pserver.core/Game/Maps/Map.cs`:** Removed premature mutation of `sender.CurX` and `sender.CurY` in `Teleport(TeleportType.Tent)`. `Warp_In` cleanly sets interior coordinates during map initialization.
3. **`wlo.pserver.core/Game/PlayerRelated/Tent/Tent.cs`:** Guarded `SetReturnLocation` to reject interior coordinates `(460, 700)`.
4. **`Src/Network/ActionCodes/AC20.cs` & `AC65.cs`:** Enhanced exit destination resolution to reject interior coordinates `(460, 700)` and fallback to `p.Tent.X, p.Tent.Y` (where the tent was placed) if `TentReturnMap` was tainted.
