# NPC Movement & AI Roaming System

## Overview
The NPC movement system replicates the authentic Wonderland Online NPC roaming mechanics ported from the Python server implementation (`gameserver.py` `npc_walk_loop`). NPCs dynamically traverse the map using either predefined waypoint step sequences or localized random roaming.

---

## Technical Specifications

### 1. Packet Protocol
- **Action Code**: `22`
- **Sub Action**: `2`
- **Payload Format**:
  - `clickId` (`ushort`): Click/entity ID of the moving NPC.
  - `targetX` (`ushort`): Target coordinate X on the map.
  - `targetY` (`ushort`): Target coordinate Y on the map.
  - `speed` (`byte` = `3`): Movement speed/animation flag.
- **Packet Structure**: `[22, 2, <clickId 2B>, <targetX 2B>, <targetY 2B>, <speed 1B>]`

### 2. Walk Behaviors & Independent Trajectory AI

| Behavior Code | Name | Description |
|---|---|---|
| Scripted Steps | Scripted Path Walking | Executes ordered step waypoints loaded from `eve.Emg` (`npcWalkStep`). Cycles continuously with per-step delays (`delay / 1000.0s`). |
| Random Roaming | Monster / NPC Wandering | Automatically moves non-static NPCs and monsters (like Grape Mons, Beetles, etc.) in independently randomized directions with relative coordinate offsets (`CurX + dx`, `CurY + dy`). Returns towards spawn point if exceeding a 220px radius. |
| Static NPC | Stationary Entities | Portals, Banks, ATMs, Guides, and fixed Captain NPCs remain anchored. |

### 3. Thread-Safe Randomization & Jitter
- **Thread-Safe Static RNG**: Eliminates timestamp collision issues where separate `new Random()` instances in tight loops shared identical seeds.
- **Initial Timer Staggering**: Staggers initial `NextWalkTime` on map load between 1.0s and 8.0s (`QuestNpc.NextRandom(1000, 8000)` ms) to prevent synchronized map-wide movements.
- **Continuous Jitter**: Subsequent walk cycles randomize delays between 3.5s and 9.0s.

---

## Source Files

- [`wlo.pserver.core/Game/Maps/Code/QuestNpc.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/QuestNpc.cs)
- [`wlo.pserver.core/Game/Maps/Map.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs)
- [`wlo.pserver.core/Game/Maps/MapManager.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/MapManager.cs)
- [`Src/Server/WorldServer.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Server/WorldServer.cs)
