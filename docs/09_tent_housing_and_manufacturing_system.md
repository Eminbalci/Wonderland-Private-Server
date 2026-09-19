# Tent Housing and Manufacturing System Specification

## 1. Architectural Overview

The tent housing subsystem provides instanced personal player spaces, interior furniture layout placement, visitor permission management, and complex manufacturing using authentic recipes from `Data/Compound2.dat`. The system is managed by [`Tent.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Tent/Tent.cs), [`Map.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs), [`Player.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Player.cs), [`AC64.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC64.cs), [`AC65.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC65.cs), and [`AC62.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC62.cs).

---

## 2. Tent Lifecycle & Virtual Interior Instances

```
[Player deploys tent on map] ---> Emits AC 23:15 -> Spawns tent overworld prop
       |
       v
[Player / Guest enters tent] ---> Client sends AC 65:1
       |
       v
[Capture Overworld Location] ---> Saves Player.TentReturnMap = (MapID, CurX, CurY)
       |                          Binds Tent.SetReturnLocation(MapID, CurX, CurY)
       v
[Server allocates virtual instance] ---> Warps to MapID = 100000 + CharID at (460, 700)
       |
       +---> Transmits chartent configurations (Wallpapers, flooring, 2nd floor expansion)
       |
       +---> Serializes all placed furniture records via AC 23:3 (21-byte records)
       |
       v
[Exit Tent Paths]
       +---> Client sends AC 65:3 (Exit Door) -> Warps to TentReturnMap; Tent remains deployed
       +---> Walks onto Doorway Portal 1 -> Warps to TentReturnMap; Tent remains deployed
       +---> Client sends AC 65:2 (Pack Up Tent) -> Evacuates all occupants to TentReturnMap -> Despawns tent prop
```

### 2.1 Persistence Architecture
* **`chartent` Table:** Persists global tent properties: `charID`, `locked`, `enlarged` (2nd floor), `tenttype`, `floor1Color`, `floor1wallpaper`, `floor2Color`, and `floor2wallpaper`.
* **`chartent_items` Table:** Persists placed furniture entities: `charID`, `itemID`, `posX`, `posY`, `floor` (1 or 2), and `rotate` (0..3).
* **`characters` Table Location Safeguard:** When a character is saved (`SaveCharacterData`) while inside a tent (`CurMap.Type == MapType.Tent` or `CurMap.MapID >= 60000`), the database writes the captured overworld coordinates from `Player.TentReturnMap` (or fallback `Player.PrevMap` / `Tent.OwnerMap`) to `location_map`, `location_x`, and `location_y`. This ensures players never log back in stranded inside ephemeral tent maps.

---

## 3. Overworld Location Preservation & Exit Mechanism

### 3.1 Return Location Tracking (`TentReturnMap`)
* **State Field:** `public WarpData TentReturnMap { get; set; }` on [`Player`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Player.cs).
* **Capture Points:**
  1. **Tent Entry Handler (`Map.onEnterTent`):** Whenever a player clicks on a tent prop to enter, `onEnterTent` preserves the player's exact overworld `CurX`, `CurY`, and map ID into `p.TentReturnMap` and `p.PrevMap` prior to invoking teleportation.
  2. **Teleport Execution (`Map.Teleport`):** Under `case TeleportType.Tent`, before coordinates are reset to the tent interior entrance (`460, 700`), the player's existing overworld coordinates are captured into `TentReturnMap`.
  3. **Map Property Setter (`Player.CurMap`):** When switching from any overworld map (`MapID < 60000`) to a tent map (`MapType.Tent` or `MapID >= 60000`), `TentReturnMap` is automatically secured.
  4. **Tent Binding (`Tent.SetReturnLocation`):** Dynamically updates `Tent.OwnerMap`, `Tent.OwnerMapID`, and links Doorway Portal 1's destination to the tent's deployed overworld map and entrance tile.

### 3.2 Exit Vectors & Disconnect Safety
1. **Exit Doorway Packet (`AC 65:3`):**
   - Executed via [`AC65.Recv3`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC65.cs).
   - Teleports the player back to `r.TentReturnMap` (or `r.Tent.OwnerMap` / `r.PrevMap`) using `TeleportType.CmD`.
   - Leaves the tent intact on the overworld map so guests and the owner can freely re-enter.
2. **Doorway Portal Collision (Portal ID 1):**
   - Stepping on the exit rug triggers [`Map.Teleport`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs) under `TeleportType.Regular`.
   - Identified via `Type == MapType.Tent || this is Tent || MapID >= 60000`.
   - Returns the player directly to `sender.TentReturnMap` (or `tent.OwnerMap`).
3. **Tent Pack-Up (`Tent.Close` / `AC 65:2`):**
   - When the tent owner folds the tent, [`Tent.Close`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Tent/Tent.cs) iterates through all occupants currently in `m_playerlist`.
   - Each occupant is safely warped back to their individual `TentReturnMap` before the instance map is destroyed.
4. **Disconnection & Crash Recovery:**
   - [`CharacterDataBase.SaveCharacterData`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/CharacterDataBase.cs) intercepts tent occupants.
   - If `currentMid >= 60000`, the stored DB coordinates default to `TentReturnMap`. Upon the next login, the character spawns outside the tent on the overworld.

---

## 4. Furniture Placement & Spatial Rotation (`AC 62`)

* **Placement (`AC 62:1`):** Player drags furniture from bag onto tent grid. Server checks item type, deducts from bag, allocates new item entry in `chartent_items`, and broadcasts `AC 23:3` to all visitors.
* **Movement & Rotation (`AC 62:3`):** Unpacks target coordinates and rotation angle (0, 90, 180, 270 degrees), updates collision grid, and commits change to database.

---

## 5. Tent Machine Manufacturing Engine (`AC 64`, `Compound2.dat`)

Crafting appliances inside tents (Workbench, Spinning Wheel, Sewing Machine, Metal Lathe, Furnace, Kitchen Stove) process multi-material recipes loaded from `Data/Compound2.dat`:

```
+---------------+-----------------------+-----------------------------------------------+
| Packet Code   | Direction             | Functional Responsibility                     |
+---------------+-----------------------+-----------------------------------------------+
| AC 64:1       | Client -> Server      | Requests production of formulaIndex           |
| AC 64:2       | Server -> Client      | Commits ingredient consumption from bag       |
| AC 64:4       | Server -> Client      | Plays crafting work animation & progress bar  |
| AC 64:9       | Server -> Client      | Deposits completed tool/item into player bag  |
| AC 5:5        | Server -> Client      | Plays completion sparkles and fanfare SFX     |
+---------------+-----------------------+-----------------------------------------------+
```

### 5.1 Recipe Verification Algorithm
1. Unpacks `formulaIndex` from `AC 64:1`.
2. Locates recipe struct `cBuildElement` in `cGlobal.gCompoundDat.buildList[formulaIndex]`.
3. Validates that the player is standing adjacent to the required manufacturing tool (`recipe.toolID`).
4. Iterates up to 5 required materials, validating item IDs and counts in `player.Inv`.
5. Deducts materials atomically from player inventory.
6. **Delivery Logic:**
   - Handheld tools (Tool IDs `38004`, `38031`, `38035`, `38036`, `38037`, `38040`, `38054`, `38058`) are deposited directly into the player's inventory bag via `AC 64:9`.
   - Large machinery and furniture are placed directly on the tent floor at coordinates `(42, 42)` via `AC 64:1`, `AC 64:2`, and `AC 64:4`.

---

## 6. Multi-Tenant Independence & Isolation Architecture

Every player on the server operates a fully isolated, persistent, independent tent housing instance:

### 6.1 Instance Segregation & Allocation
* **Unique Instance per Player:** Every [`Player`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Player.cs) allocates its own [`Tent`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Tent/Tent.cs) instance bound to their primary key `CharID`.
* **Database Isolation:** All furniture entries (`chartent_items`) and design properties (`chartent`) are queried and persisted strictly by `charID = '{player.CharID}'`, eliminating data crossover between players.
* **Indoor Deployment Safeguard:** [`Tent.Open`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Tent/Tent.cs#L101-L115) enforces that players cannot deploy a tent inside another tent (`_owner.CurMap is Game.Code.Tent || _owner.CurMap.Type == MapType.Tent`).

### 6.2 Overworld Multi-Tent Synchronization
* **Map Deployment Registry:** When a tent is pitched, [`Map.onTentOpened`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs#L1490-L1495) registers the tent into `Map.Tents` keyed by `tent.MapID` (`_owner.CharID`) and broadcasts `AC 65:1` (`[65, 1, CharID, 36002, X, Y, 0]`) to all peers on the map.
* **Joining Player Replication:** [`Map.SendOpenTents`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs#L1550-L1565) broadcasts all currently active, non-closed tents on that map to newly arrived players upon map load (`SendMapInfo`).

### 6.3 Visitor Permissions & Security Locking
* **Security Lock Enforcement:** In [`Map.onEnterTent`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs#L1500-L1545), if `targetTent.Locked` is active, non-owner players are blocked from entering and receive a HUD alert (`Çadır kilitli!`).
* **Furniture Modification Protection:** [`AC62.Recv1`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC62.cs#L50-L80) and [`AC62.Recv3`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC62.cs#L85-L105) verify that `p.CurMap == p.Tent`. Visitors cannot place, move, or steal furniture in another player's tent.
* **Real-Time Occupant Broadcast:** When the owner rearranges furniture, [`Tent.SendTentItemsToAll`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Tent/Tent.cs#L500-L515) broadcasts the updated furniture state to all guests currently inside the tent.

### 6.4 Clean Disconnection & Evacuation
* **Owner Disconnection:** In [`Player.OnConnectionLost`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Player.cs#L1860-L1880), if an owner disconnects while their tent is pitched, `m_tent.Close()` is immediately invoked, safely warping all inside occupants back to their recorded `TentReturnMap` on the overworld and removing the tent prop from the map.

