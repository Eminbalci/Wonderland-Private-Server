# 83. World Subsystem Modernization

## 1. Overview and Architectural Objective

The World Subsystem in the Wonderland Private Server coordinates the operational runtime for active players, scene maps, periodic persistence, and cluster routing. Historically, legacy implementations in `WorldServer` and `WloWorldNode` suffered from synchronization divergence between isolated local map lists and the singleton `MapManager.Instance`, unpopulated collections, non-thread-safe player queues (`Queue<Player>`, `List<Player>`), and incomplete or stubbed `WorldServerHost` methods.

This modernization establishes a unified, high-concurrency, null-safe architecture across four core modules:
1. `wlo.pserver.core\Game\Enums\WorldEnums.cs`
2. `wlo.pserver.core\Plugin\WorldAccess.cs`
3. `Src\Server\WloWorldNode.cs`
4. `Src\Server\WorldServer.cs`

---

## 2. Enums and State Modeling (`WorldEnums.cs`)

### 2.1 `WorldServerState`
Defines the lifecycle states of a world server or node:
- `Stopped (0)`: Node or server is uninitialized or fully stopped.
- `Starting (1)`: Initialization in progress; background worker threads starting.
- `Running (2)`: Fully operational; processing logins, ticks, and broadcasts.
- `Stopping (3)`: Graceful shutdown in progress; disconnecting clients and stopping threads.
- `Faulted (4)`: Unhandled critical failure state.

### 2.2 `WorldTickRate`
Centralized tick constants and cadence standards (in milliseconds):
- `Realtime = 2`: Main login and queue drain loop.
- `Fast = 50`: Node-level processing loop.
- `EventTick = 120`: Event monitoring and zombie player eviction loop.
- `MapTick = 500`: Overworld map and NPC spatial processing tick.
- `AutoSaveStep = 250`: Step slice for responsive autosave cancellation.
- `Heartbeat = 1000`: Default autosave interval and metrics observation period.

### 2.3 `WorldEventType`
Standardized world event dispatch identifiers:
- `PlayerLogon = 0`: Player authenticated and logging into map.
- `PlayerLogoff = 1`: Player disconnecting or logged out.
- `PlayerWarp = 2`: Scene transition or portal traversal.
- `PlayerKick = 3`: Administrative or timeout disconnect.
- `WorldBroadcast = 4`: Cluster-wide broadcast packet.
- `MapLoaded = 5`: Dynamic or static map loaded into cache.
- `MapUnloaded = 6`: Instance map disposed.

---

## 3. Host Interface Contract (`WorldAccess.cs`)

The `WorldServerHost` interface provides access for plugins, administrative consoles, and subsystems:

```csharp
public interface WorldServerHost
{
    Player GetPlayer(uint charId);
    bool isOnline(uint charId);
    void DisconnectPlayer(uint charId);
    void Broadcast(SendPacket pkt);
    void Broadcast(SendPacket pkt, uint? directTo = null, bool exclude = false);
    IEnumerable<Player> GetAllPlayers();
}
```

### Method Specifications

| Method | Parameters | Return Type | Description |
| :--- | :--- | :--- | :--- |
| `GetPlayer` | `uint charId` | `Player` | Looks up online player across database online cache and active maps. Returns `null` if offline or invalid. |
| `isOnline` | `uint charId` | `bool` | Verifies whether the specified character is online and connected (`!isDisconnected()`). |
| `DisconnectPlayer` | `uint charId` | `void` | Gracefully disconnects the player, removes them from their current map, and triggers logout persistence. |
| `Broadcast` | `SendPacket pkt` | `void` | Dispatches packet to all active maps in `MapManager.Instance`. |
| `Broadcast` | `SendPacket pkt, uint? directTo, bool exclude` | `void` | Routes packet to a specific player ID or broadcasts to all maps with an optional exclusion filter (`"Ex"`). |
| `GetAllPlayers` | None | `IEnumerable<Player>` | Returns a deduplicated snapshot of all online players across database tracking and active map collections. |

---

## 4. Cluster Node Modernization (`WloWorldNode.cs`)

### 4.1 Thread-Safe Player Collections
- Replaced unsynchronized `List<Player>` with `ConcurrentDictionary<uint, Player>`:
  - `m_localPlayers`: Stores players connected directly to the local node.
  - `m_externalPlayers`: Stores players routed from remote nodes.
- Replaced `ConcurrentStack<Player>` with `ConcurrentQueue<Player>` for FIFO connection and disconnection sequencing.
- Removed unpopulated `List<GameMap> Maplist`; all map iterations query `MapManager.Instance.ActiveMaps`.

### 4.2 Non-Blocking Lifecycle and Resource Management
- **Bounded Shutdown Timeout:** In `Close()`, a 3-second bounded countdown prevents infinite spinlocks during player disconnections.
- **Thread Join Safeguard:** Background thread joins with a 1-second timeout to prevent deadlocks upon shutdown.
- **Fault-Isolated Tick Loop:** `MainMapLoop` isolates each map's `Process()` invocation in a `try/catch` block, preventing a single corrupted map instance from halting overall node execution.

---

## 5. World Server Implementation (`WorldServer.cs`)

### 5.1 MapList and MapManager Synchronization
`WorldServer` maintains synchronization between its internal cache and `MapManager.Instance`:
- `SyncMapList()`: Automatically queries `MapManager.Instance.ActiveMaps` and populates `_mapList` via `AddOrUpdate`.
- `GetActiveMaps()`: Provides an aggregated enumerable of active maps, ensuring broadcasts reach every player regardless of how their map was instantiated.
- `GetMap(ushort ID)`: Cascades from `MapManager.Instance.GetMap(ID)` to local cache and fallback plugin map loaders.

### 5.2 Concurrent Player Collections & Loop Safety
- `QueuedPlayerLogin`: Modernized to `ConcurrentQueue<Player>`. `OnLogin` safely enqueues without lock contention, and `MainLoop` drains queued logins via `TryDequeue`.
- `Eventwrk`: Periodically scans `GetAllPlayers()` to identify disconnected or orphaned client sockets and invoke `DisconnectPlayer(charId)`.
- `AutoSaveLoop`: Iterates a thread-safe snapshot from `GetAllPlayers()`, recording metrics and persisting character data without blocking login or packet pipelines.
- `MapTickLoop`: Executes map processing at standard 500ms intervals using `MapManager.Instance.ActiveMaps`.

### 5.3 Dead Code Removal & Logic Preservation
- Removed obsolete commented-out blocks (`Mainwrk`, dummy `isOnline`, blank `DisconnectPlayer`).
- Preserved all authentic login sequences: AC packets (20:8, 24:5, 35:11, 35:12, 75:1-10, 90:1), starter packs delivery, beginner outfits, stats (5:3), skills (8:1), hotbar clearing, and quest constellation states.

---

## 6. Verification and Parity

- **Compilation Status:** 0 warnings, 0 errors across `wlo.pserver.core`, `wlo.pserver.maps`, `wlo.pserver.Bot`, and `Wonderland Private Server.exe`.
- **Thread Safety Invariants:** All collection modifications use thread-safe data structures (`ConcurrentDictionary`, `ConcurrentQueue`).
- **Memory & Resource Leak Prevention:** Background threads properly terminated via `killFlag` and graceful joins.
