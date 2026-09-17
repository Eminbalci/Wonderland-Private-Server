# 01 - System Architecture and Server Topology

This technical specification documents the system architecture, component topology, network service boundaries, portability engine, and lifecycle management for the **Wonderland Online Private Server** emulator.

---

## 1. Architectural Overview

Wonderland Online Private Server is a modular game server emulator engineered in C# targeting the .NET Framework 4.6.2 runtime. The server reproduces the official Wonderland Online MMORPG server environment, implementing authentic binary protocols, game mechanics, event scripting bytecode execution, and spatial simulation.

### Solution Project Graph

```
Wonderland Private Server.sln
|-- Wonderland Private Server (Host Executable)
|   |-- WinForms Administrative Dashboard
|   |-- Network Service Dispatchers (AC 1 .. AC 186)
|   +-- Global Node Orchestrator (cGlobal)
|
|-- wlo.pserver.core (Engine Library)
|   |-- Game Entities (Player, Character, Pet, Companion, Vehicle, Tent)
|   |-- Map Engine (GameMap, Map, Tile Pathfinding, Ground Items)
|   |-- Database Engine (GameDataBase, CharacterDataBase, UserDataBase, SQLite)
|   |-- Scripting & Dialogue (EveEventInterpreter, PreEventInterpreter)
|   +-- Combat Engine (PvEBattleManager, Battle, BattleScene)
|
|-- wlo.pserver.maps (Spatial Library)
|   +-- Map Geometry and Island Object Data
|
|-- wlo.pserver.Bot (Automated Entities)
|   +-- GM Bot Scripts and NPC AI Routines
|
|-- Phoenix.Core / PhoenixData / Wlo.Core / RCLibrary (Support Infrastructure)
    |-- Binary Wire Packet Serialization (Packet, SendPacket, RecievePacket)
    |-- Dynamic Portability & File Path Resolver (PathHelper)
    +-- Database Abstraction Wrappers
```

---

## 2. Server Socket Topology

The server implements a tri-server socket architecture, running concurrent TCP listeners mapped to distinct game lifecycle phases:

| Server Service | Default Port | Protocol | Primary Responsibilities |
| :--- | :--- | :--- | :--- |
| **Login Server** | `6414` | TCP / Binary | Client connection handshake, account authentication, cipher verification, character slot selection, character creation (`AC 9`), Item Mall catalog delivery (`AC 75`). |
| **World Server** | `6415` | TCP / Binary | Overworld player replication, tile movement, NPC dialogues, quest state engine, ground item lifecycles (`AC 23`), turn-based combat (`AC 11`/`50`/`51`), tent housing (`AC 12`). |
| **Status Service** | `6416` | TCP / Binary | Server cluster heartbeat, operational state indicator (Green/Yellow/Red), experience/drop rate broadcast, online player count replication. |
| **Embedded Web API** | `8080` (Configurable) | HTTP / REST | Web-based account registration endpoint, server diagnostic queries, operational status reports. |

### Socket Lifecycle and Communication Flow

```
[Game Client]
      |
      |-- 1. Connects to Port 6414 (Login Server)
      |      --> Transmits credentials (AC 63)
      |      <-- Receives character list (AC 63:1 / AC 63:2)
      |      --> Selects slot / creates character (AC 9 / AC 63)
      |      <-- Receives World Server redirect credentials
      |
      |-- 2. Connects to Port 6415 (World Server)
      |      --> Transmits World authentication handshake
      |      <-- Receives player stats and attributes (AC 5:3)
      |      <-- Receives inventory (AC 23:5) and equipment (AC 23:11)
      |      <-- Receives map load directive (AC 23:102)
      |      <-- Receives Scene Table entity definition (AC 22:4)
      |      <-- Receives dynamic actor concealment frames (AC 22:10 / 22:11)
      |      <-- Receives input unlock directive (AC 20:8, AC 5:4)
      |
      +-- 3. Queries Port 6416 (Status Service)
             <-- Receives server load and player concurrency metrics
```

---

## 3. Dynamic Portability Engine

To eliminate hardcoded drive letters, absolute directory structures, and environment-specific file locations, the solution utilizes a centralized portability engine implemented in [`RCLibrary.Core.PathHelper`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/PathHelper.cs).

### Path Resolution Rules
1. **Base Directory**: Determined at runtime via `AppDomain.CurrentDomain.BaseDirectory`.
2. **Data Directory**: Resolved dynamically to `<BaseDirectory>/Data` or repository root relative fallback.
3. **Database Directory**: Maps directly to `<BaseDirectory>/Data/ServerDataBase.db`.
4. **Client Assets**: Resolved via user-configurable settings or relative project scan (`SERVER.INI`, `odd.dat`, `Npc.dat`, `Item.dat`, `Skill.dat`, `Talk.dat`, `Eve.emg`).
5. **Zero Machine Dependencies**: Any clone of the repository compiles and runs immediately on drive `C:`, `D:`, or external volumes without modifying source code or configuration files.

---

## 4. Threading, Synchronization, and Concurrency

The server coordinates high-throughput network packets, database disk operations, spatial navigation, and asynchronous timers:

### Concurrency Primitives
- **Network Dispatch Loops**: Asynchronous socket readers running on thread pool threads, isolating network socket reads from engine game tick loops.
- **Database Thread Safety**: Synchronized access via transaction blocks and thread-safe lock wrappers protecting SQLite read/write operations against concurrency collisions.
- **Map Broadcasters**: Spatial iteration over thread-safe collections (`ConcurrentDictionary` and locked member lists) to prevent collection modification exceptions during active player joining or departing.
- **Heartbeat Timers**:
  - Ground items respawn heartbeat: Evaluates item area respawn timestamps every 1,000 ms.
  - Map entity roaming heartbeat: Evaluates NPC signed bounding-box wandering and waypoint patrols every 500 ms.
  - Periodic auto-save loop: Flushes dirty character state to SQLite at configured intervals.

---

## 5. Graceful Shutdown & Diagnostic Sequence

To safeguard database consistency and prevent corrupted player state upon process termination, [`Src/Gui/MainForm1.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.cs) and [`Src/Gui/ShutDown Dialog.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Gui/ShutDown%20Dialog.cs) execute a structured shutdown lifecycle:

1. **Shutdown Initiation**: Triggered via GUI window close or console interrupt.
2. **Socket Interception**: Halts acceptance of new incoming TCP connections on ports 6414, 6415, and 6416.
3. **Player State Persistence**:
   - Iterates through all connected active players across all maps.
   - Synchronously writes current coordinates, experience, gold, stats, inventory, equipment, pet states, and quest flags to SQLite.
4. **Log File Reporting**:
   - Emits canonical final log entries to disk (`bin/Debug/Logs/wlophoenixlogFile_YYYYMMDD.txt`).
   - Displays exact path of active log file in server output.
5. **Diagnostic Countdown**:
   - Executes a second-by-second countdown (10 to 0) with UI progress indication.
   - Unloads native resources and exits cleanly with return code `0`.
