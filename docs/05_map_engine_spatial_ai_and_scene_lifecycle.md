# Map Engine, Spatial AI, and Scene Entity Lifecycle

## 1. Architectural Overview

The overworld simulation coordinates player movement, pathfinding, NPC autonomous wandering, harvestable terrain resources, and dynamic entity concealment across 1,119 maps. The subsystem is driven by [`wlo.pserver.core/Game/Maps/Map.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs), [`QuestNpc.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/QuestNpc.cs), and [`PreEventInterpreter.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/QuestRelated/PreEventInterpreter.cs).

---

## 2. Coordinate System, Terrain Geometry, and Collision Grids

* **Isometric Coordinate Space:** Coordinates $X$ and $Y$ are unsigned 16-bit integers (`0..65535`) representing isometric grid coordinates.
* **Standard Walk Speeds:** Base unmounted character walking speed = 2; mount speed = 3; vehicle speed = 4..5.
* **Terrain Asset:** `Data/Ground.MMG` (21,921,689 bytes).
* **Client Collision Disassembly (`aLogin.exe`):**
  - Terrain dimensions and heights are initialized in `FUN_004121a8`.
  - Dynamic collision masks are updated via `FUN_0043d390(actor)`, manipulating grid occupancy bits using bitwise mask `*pbVar & 0xfb`.
  - When an entity is concealed, collision bits are cleared, allowing characters to walk through the tile without obstruction.

---

## 3. Scene Entity Lifecycle Protocol (`AC 22:4`)

Entity visibility and interaction states are synchronized via the 14-byte fixed-size entity table frame in `Map.cs`:

```
+---------------+---------------+-----------------------------------------------+
| Byte Offset   | Field Type    | Description                                   |
+---------------+---------------+-----------------------------------------------+
| 0x00..0x01    | UInt16 (LE)   | Target Entity ClickID                         |
| 0x02..0x03    | UInt16 (LE)   | Entity State Frame (0x00FF Intact, 0x0001 Brk)|
| 0x04..0x05    | UInt16 (LE)   | Isometric Coordinate X                        |
| 0x06..0x07    | UInt16 (LE)   | Isometric Coordinate Y                        |
| 0x08          | Byte          | Entity Type (1 = Active / Visible, 2 = Hidden)|
| 0x09..0x0C    | UInt32 (LE)   | Despawn Duration (0 = None, 0x03E7FC18 Conceal|
| 0x0D          | Byte          | State Flag (Default 0x00)                     |
+---------------+---------------+-----------------------------------------------+
```

### 3.1 Lifecycle States
1. **Visible Active Entity:** `State = 0x00FF`, `EntityType = 1`, `Duration = 0`.
2. **Concealed Entity (Despawn):** `State = 0xFFFF`, `EntityType = 2`, `Duration = 0x03E7FC18` (65,535,000 ms).
3. **Broken Prop / Opened Chest:** `State = 0x0001`, `EntityType = 1`, `Duration = 0`.

> [!CAUTION]
> **Client Animation Table Index Trap:** Transmitting `State = 0x0000` is invalid in official WLO client sprite tables; index `0` maps to the broken/cracked animation frame. To render an entity intact and unbroken, the server must transmit `0x00FF` (255).

### 3.2 Client Reverse Engineering Verification (`aLogin.exe`)
* The packet is parsed into actor memory at `PTR_DAT_004c9790 + ClickID * 4`.
* When `EntityType == 2`, client sets `*(actor + 0x1eec) = 2`.
* When `Duration == 0x03E7FC18`, client invokes `FUN_00432674`, which calls `FUN_0043d390(actor)` to clear collision.
* The rendering loop `FUN_0043d58c` skips actors with visibility state `2` or `4`.
* Mouse hit-testing subroutines (client lines 308016 & 308092) skip actors with visibility `2` or `4`, preventing phantom clicks on hidden NPCs.

### 3.3 Universal Cutscene Dummy Suppression
Across 108 maps, official WLO scene files contain 230 duplicate cutscene dummy actors that share Template IDs with active dialogue NPCs but have zero linked events (`Events.Count == 0`). [`PreEventInterpreter.ShouldNpcBeVisible`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/QuestRelated/PreEventInterpreter.cs) automatically detects and suppresses these clones on map entry by emitting `AC 22:4` concealment frames.

---

## 4. NPC Spatial Roaming AI (`WalkBehavior`)

NPC movements are evaluated on every tick of the 500ms `MapTickThread` in [`QuestNpc.Update`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/QuestNpc.cs):

```
+---------------+-----------------------+-------------------------------------------------------+
| Behavior Mode | Target Entity Types   | Movement Math & Packet Emission                       |
+---------------+-----------------------+-------------------------------------------------------+
| Behavior 1    | Static Guards / Props | Anchored at Spawn(X,Y). Zero packet emissions.        |
| Behavior 2/5  | Patrol Waypoint Paths | Follows Eve.emg WalkSteps. Advances sequentially or   |
|               |                       | oscillates. Emits AC 22:2 ("bbwwb", 22, 2, ClickID...).|
| Behavior 3    | Farm Animals / Pets   | Signed relative bounding-box roaming (pigs, chicks).   |
|               |                       | Clamped to [-300, 300] and map boundaries [50, 4000].  |
|               |                       | Randomized interval 4.0 - 9.0 seconds. Emits AC 22:2.  |
| Behavior 4    | Leashed Wild Monsters | Small random drift [-40, 40], max 60 px from spawn.   |
|               |                       | Randomized interval 5.0 - 10.0 seconds. Emits AC 22:2. |
+---------------+-----------------------+-------------------------------------------------------+
```

---

## 5. Ground Items Harvesting & Respawning Loop

Terrain items are loaded into [`MapGroundItem`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs) from `Eve.emg` `ItemAreas` (209 items across 77 maps):

```
[Player Map Entry] ---> Emits AC 23:4 (Spawns all active terrain items)
       |
       v
[Player AC 23:2 Pickup Request]
       |
       v
[Server Proximity Check] ---> (dx^2 + dy^2 <= 180^2) & Inventory Space Check
       |
       +---> [If Valid]:
       |        1. Inv.AddItem(ItemID, 1)
       |        2. Emits AC 23:6 (Gold Acquisition Banner)
       |        3. Emits AC 23:5 (31-Byte Inventory Bag Update)
       |        4. Unicast AC 23:2 [Slot, 1] to collector
       |        5. Broadcast AC 23:2 [Slot, 0] (Despawn) to peers
       |        6. Sets RespawnTime = DateTime.Now.AddSeconds(RespawnSeconds)
       |        7. Persists character save
       |
       v
[Map.Process() Background Loop] ---> When now >= RespawnTime:
                                        1. Resets IsPickedUp = false
                                        2. Broadcasts AC 23:3 (Terrain Item Respawn Frame)
```
