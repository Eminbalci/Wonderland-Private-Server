# Map Engine, Scene Simulation, and Entity Lifecycle

## 1. Architectural Overview

The map engine manages virtual world instances, player viewport synchronization, NPC pathfinding and roaming AI, native ground item harvesting and respawn loops, and entity concealment isolation. 

World maps are loaded dynamically via [`MapManager`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/MapManager.cs) and ticked on a background thread (`MapTickLoop`) with a 500ms cycle in [`WorldServer`](file:///D:/GitHub/Wonderland-Private-Server/Src/Server/WorldServer.cs#L231).

---

## 2. Map Coordinate System & Viewport Simulation

Wonderland Online uses an isometric tile grid coordinate system:
* **Coordinates:** `X` (Horizontal) and `Y` (Vertical) represented as unsigned 16-bit integers (`0..65535`).
* **Directions:** 8 cardinal and intercardinal orientations (`0 = North`, `1 = North-East`, `2 = East`, `3 = South-East`, `4 = South`, `5 = South-West`, `6 = West`, `7 = North-West`).
* **Movement Speed:** Standard land walking velocity is `2`, with mounts and vehicles scaling up to higher velocity tiers (`3..5`).

---

## 3. Scene Table & Entity Replication Protocol

### 3.1 14-Byte Scene Record (`AC 22:4`)
When a player enters a map, the server serializes all resident entities into an [`AC 22:4`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs#L1305) packet stream:

```
+---------------+---------------+---------------------------------------+
| Offset (Byte) | Type          | Description                           |
+---------------+---------------+---------------------------------------+
| 0..1          | UInt16        | ClickID (Scene Entry Identifier)      |
| 2..3          | UInt16        | State (0x00FF = Living Actor,         |
|               |               |        0x0001 = Opened Chest/Prop,    |
|               |               |        0x0000 = Intact Chest/Prop)    |
| 4..5          | UInt16        | Grid X Coordinate                     |
| 6..7          | UInt16        | Grid Y Coordinate                     |
| 8             | Byte          | Entity Type (1 = Visible, 2 = Hidden) |
| 9             | Byte          | Unused / Padding (0x00)               |
| 10..13        | UInt32        | Extended Flags (0x00000000)           |
+---------------+---------------+---------------------------------------+
```

### 3.2 Handshake & Concealment Sequencing
To prevent visual flickering or ghost NPCs from rendering before client scripts take effect, entity suppression executes within a strict temporal packet window:

```mermaid
sequenceDiagram
    autonumber
    participant Server
    participant Client

    Server->>Client: AC 22:4 (Scene NPC Table - initial positions)
    Server->>Client: AC 23:4 (Ground Items on Map)
    Server->>Client: AC 23:102 (Map Load Complete Signal)
    Note over Server,Client: Concealment Isolation Window
    loop Every Hidden Actor (Recruited Companion / Completed Quest Actor)
        Server->>Client: AC 22:10 (Actor Unbind / Conceal Frame - ClickID, 0xFF, 0xFF)
        Server->>Client: AC 22:11 (Stage Isolation Frame - ClickID, 0xFF, 0xFF)
    end
    Server->>Client: AC 20:8 (Unfreeze Player Movement - Input Unlock)
    Server->>Client: AC 5:4 (Set Walking Speed)
```

1. **Map Ready Signal:** Server issues `AC 23:102`.
2. **Actor Concealment (`AC 22:10` & `AC 22:11`):** Server dispatches dual concealment frames for recruited companions (e.g., Robinson on Map 11016), completed stage props, and conditional actors filtered by [`PreEventInterpreter`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/QuestRelated/PreEventInterpreter.cs).
3. **Input Unlock:** Server issues `AC 20:8` to grant player mobility only after actor isolation is committed.

---

## 4. NPC Roaming & Behavior AI

Every NPC in [`QuestNpc`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/QuestNpc.cs#L85) evaluates its `WalkBehavior` parameter on each map tick:

### 4.1 Static Anchors (`WalkBehavior == 1` or `IsStaticNpc()`)
* Entity position is hard-anchored to `(SpawnX, SpawnY)`.
* Roaming timer is delayed by 300 seconds to prevent unnecessary processing cycles.

### 4.2 Bounding-Box Wandering (`WalkBehavior == 3`)
Used for farm animals (pigs, ducks, chicks) and ambient villagers:
* `WalkSteps[0]` specifies `(minDx, minDy)` signed relative offsets.
* `WalkSteps[1]` specifies `(maxDx, maxDy)` signed relative offsets.
* Offsets are clamped to safe bounding bounds `[-300, +300]`:
```csharp
int targetX = this.SpawnX + NextRandom(minDx, maxDx + 1);
int targetY = this.SpawnY + NextRandom(minDy, maxDy + 1);
ushort finalX = (ushort)Math.Max(50, Math.Min(4000, targetX));
ushort finalY = (ushort)Math.Max(50, Math.Min(4000, targetY));

SendPacket pkt = Tools.FromFormat("bbwwb", 22, 2, this.CickID, finalX, finalY, (byte)2);
BroadcastNpcMove(map, pkt);
```

### 4.3 Scripted Waypoint Patrol (`WalkBehavior == 2 || WalkBehavior == 5`)
* Reads patrol routes and delay parameters defined in `eve.Emg`.
* If a single waypoint exists, the NPC oscillates between `(SpawnX, SpawnY)` and the target coordinate.
* If multiple waypoints exist, the NPC cycles through the coordinate ring sequentially.

### 4.4 Outdoor Leashed Roaming (`WalkBehavior == 4` or `IsWildMonster()`)
* Wanders randomly within outdoor field zones.
* Strictly leashed within 60 pixels of `(SpawnX, SpawnY)` to prevent monsters from escaping bounds or clipping into obstacles.

---

## 5. Ground Items Harvesting & Respawn Engine

Ground items represent naturally harvestable resources (wood, ore, flowers, quest props) resting directly on map terrain.

### 5.1 Asset Population
Upon map load, [`Map.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs#L286) imports native `ItemAreas` from `eve.Emg`. Across 77 official maps, exactly 209 active ground items are registered:
* **Slot Identifier:** Logical terrain slot index (`1..255`).
* **Coordinates:** `(X, Y)` world coordinates.
* **Respawn Duration:** Defaults to `120` seconds unless overridden by script bytecode.

### 5.2 Pickup Lifecycle
```
Client Click Ground Item -> Client Sends AC 23:2 (Slot)
  |
  +--> Server verifies distance <= 150 pixels & !IsPickedUp
  |
  +--> src.Inv.AddItem(gi.ItemID, 1)
  +--> gi.IsPickedUp = true; gi.RespawnTime = DateTime.Now.AddSeconds(gi.RespawnSeconds);
  |
  +--> Send to Collector: AC 23:2 (Slot, 1 = Success)
  +--> Broadcast to Peers: AC 23:2 (Slot, 0 = Despawn)
  +--> Send to Collector: AC 23:6 (Gold Item Banner - 28-byte padded packet)
  +--> Send to Collector: AC 23:57 (System prompt notification)
```

### 5.3 Asynchronous Respawn Loop
During each `Map.Process()` execution, items with `gi.IsPickedUp && now >= gi.RespawnTime` are restored:
* `gi.IsPickedUp = false;`
* Server broadcasts `AC 23:3` (`bbwwwdb`, 23, 3, ItemID, X, Y, 0, 0, Slot) to all players within the map.

---

## 6. Peer Entity Replication (`SendPeerCompanionAndVehicle`)

When a player enters a map or another player approaches, [`SendPeerCompanionAndVehicle`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs#L535) synchronizes all complex visual attachments:

1. **Active Vehicle (`AC 15:10`):**
   * Dispatches Vehicle ID, Owner Character ID, and vehicle classification flags.
2. **Active Mount (`AC 15:16`):**
   * Dispatches Mount ID, Owner Character ID, saddle status, and 26-byte mount attribute padding.
3. **Battle Companion Visual Entity (`AC 15:4`):**
   * Transmits Character ID, Active Pet ID, visual state, companion name string, and combat stance.
4. **Companion Combat Attributes (`AC 15:1`):**
   * Synchronizes pet HP, MaxHP, SP, MaxSP, Amity, Level, and primary attribute allocation to map peers.
