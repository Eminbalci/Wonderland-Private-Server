# 01 - Architecture and Core Engine

## 1. System Overview & Topology

The **Wonderland Private Server** is a modular, high-performance MMORPG server emulation platform for *Wonderland Online*, implemented in C# (.NET Framework 4.6.2). The server hosts multiple concurrent TCP network listeners to manage authentication, gameplay world instances, and cluster health monitoring.

```
                  +-----------------------------------+
                  |         Game Client (WLO)         |
                  |     (aLogin.exe / Main.exe)       |
                  +-----------------+-----------------+
                                    |
          +-------------------------+-------------------------+
          |                         |                         |
          v (TCP: 6414)             v (TCP: 6415)             v (TCP: 6416)
+-------------------+     +-------------------+     +-------------------+
|   Login Server    |     |   World Server    |     |   Status Server   |
| (Authentication,  |     | (Gameplay, Battle,|     | (Cluster Health,  |
|  Account & Roles) |     |  Map, Inventory)  |     |  Load & Rates)    |
+---------+---------+     +---------+---------+     +---------+---------+
          |                         |                         |
          +--------------------+----+-------------------------+
                               |
                               v
                  +-------------------------+
                  |  Data/ServerDataBase.db |
                  |    (SQLite Single-DB)   |
                  +-------------------------+
```

---

## 2. Multi-Server Port Architecture

| Component | Port | Transport | Protocol / Handler | Primary Responsibility |
| :--- | :--- | :--- | :--- | :--- |
| **Login Server** | `6414` | TCP | [`LoginServer`](file:///D:/GitHub/Wonderland-Private-Server/Src/Server/LoginServer.cs) / [`AC00_01`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC00_01.cs) | Account authentication, token dispatch, character selection, character creation/deletion, Item Mall catalog sync (`AC 75`). |
| **World Server** | `6415` | TCP | [`WorldServer`](file:///D:/GitHub/Wonderland-Private-Server/Src/Server/WorldServer.cs) / [`GameEngine`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/GameEngine.cs) | Overworld replication, tile pathfinding, turn-based battle engine, inventory, dialogue engine, party synchronization. |
| **Status Server** | `6416` | TCP | [`ServerStatusManager`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Server/ServerStatusManager.cs) | Heartbeat broadcasting, server population metrics, cluster load states (Green, Yellow, Red), dynamic rate configuration. |

---

## 3. Dynamic Path Resolution Architecture

To guarantee absolute portability across developer workstations, virtual machines, and arbitrary disk partitions (e.g. `C:`, `D:`, `E:`), all file access is channeled through [`RCLibrary.Core.PathHelper`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/RCLibrary.Core/PathHelper.cs).

### Path Resolution Sequence
1. **Application Root Discovery**: Inspects `AppDomain.CurrentDomain.BaseDirectory` upwards until finding either `Wonderland Private Server.sln` or an authoritative `Data` folder.
2. **Relative Resolution**: Resolves candidate files relative to detected root.
3. **Fallback Discovery**: Searches candidate directories:
   - `<Root>/Data/<FileName>`
   - `<Root>/bin/Debug/Data/<FileName>`
   - `<CurrentDirectory>/Data/<FileName>`

```csharp
public static class PathHelper
{
    public static string AppRootDirectory { get; }
    public static string GetDataFilePath(string filename);
    public static string GetDataDirectory();
}
```

- **Parameters**: `filename` (string) — Relative filename inside `Data/` (e.g., `"ServerDataBase.db"`).
- **Returns**: Absolute canonical file path verified to exist or normalized candidate path.
- **Exceptions**: Handles `UnauthorizedAccessException` and `DirectoryNotFoundException` with fallback to `BaseDirectory`.
- **Edge Cases**: Handles deep execution paths inside unit testing or nested subfolder builds.

---

## 4. Project Reference Hierarchy

The solution is decomposed into 8 clean, modular projects:

```
Wonderland Private Server.sln
│
├── 1. RCLibrary/                -> Base utilities, PathHelper, DataBase driver (SQLite/MySQL wrappers)
├── 2. Phoenix.Core/             -> Low-level byte buffers, cryptography helpers, bitwise packers
├── 3. PhoenixData/              -> Binary structure serializers, game record definitions
├── 4. Wlo.Core/                 -> Protocol enums, packet definitions, action code registries
├── 5. wlo.pserver.core/         -> Game mechanics, combat engine, NPC engine, Quest system, EVE parser
├── 6. wlo.pserver.maps/         -> Map coordinate systems, tile collision grids, portal graphs
├── 7. wlo.pserver.Bot/          -> Automated player bot routines, stress-testing harnesses
└── 8. Wonderland Private Server -> Windows Forms GUI management suite, server supervisor, launcher
```

---

## 5. Threading & Concurrency Model

- **Network I/O Polling**: Non-blocking `Socket.Select` loops run on background worker threads (`Thread.IsBackground = true`).
- **Concurrent Dispatch**: Incoming packets are framed, queued, and dispatched into the appropriate Action Code handler.
- **State Synchronization**:
  - `lock (_lock)` blocks guard entity collections in [`cGlobal`](file:///D:/GitHub/Wonderland-Private-Server/Src/cGlobal.cs).
  - Database access uses thread-safe serialized connection locking in [`DataBase`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/RCLibrary.Core/DataBase.cs).
  - Periodic tasks (NPC roaming, gathering node respawn, battle round timers) run on decoupled timer intervals.

---

## 6. Global Memory Model (`cGlobal`)

The [`cGlobal`](file:///D:/GitHub/Wonderland-Private-Server/Src/cGlobal.cs) singleton holds master references to the server runtime state:
- `gLoginServer`: Instance of [`LoginServer`](file:///D:/GitHub/Wonderland-Private-Server/Src/Server/LoginServer.cs).
- `gWorldServer`: Instance of [`WorldServer`](file:///D:/GitHub/Wonderland-Private-Server/Src/Server/WorldServer.cs).
- `gUserDataBase`: Instance of [`UserDataBase`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/UserDataBase.cs).
- `gCharacterDataBase`: Instance of [`CharacterDataBase`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/CharacterDataBase.cs).
- `gGameDataBase`: Instance of [`GameDataBase`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/GameDataBase.cs).
- `ItemDatManager`: Loaded memory cache of all item records from `Item.dat`.
- `NpcDatManager`: Loaded memory cache of 4,928 decoded NPC records from `Npc.dat`.
