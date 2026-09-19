# System Architecture and Server Topology

## 1. Architectural Overview

Wonderland Online Private Server is an event-driven, multi-threaded game server suite designed to simulate the official server topology of Wonderland Online (WLO). The system decouples network packet framing, world state management, client asset decoders, relational persistence, and administrative tooling into modular, decoupled libraries.

---

## 2. Solution Topology & Project Graph

The solution [`Wonderland Private Server.sln`](file:///D:/GitHub/Wonderland-Private-Server/Wonderland%20Private%20Server.sln) compiles into eight coordinated projects targeting .NET Framework 4.6.2 (`net462`):

```
+-----------------------------------------------------------------------------------+
|                           Wonderland Private Server                               |
|            (WinForms GUI Administration, GM Studio, Server Lifecycle)             |
+-----------------------------------------------------------------------------------+
       |                    |                    |                   |
       v                    v                    v                   v
+------------------+ +------------------+ +-----------------+ +-------------------+
| wlo.pserver.core | | wlo.pserver.maps | | PhoenixData     | | wlo.pserver.Bot   |
| (Game Engine)    | | (Spatial Engine) | | (Binary Assets) | | (Bot Simulation)  |
+------------------+ +------------------+ +-----------------+ +-------------------+
       |                    |                    |
       +--------------------+--------------------+
                            |
                            v
                   +------------------+
                   |   Phoenix.Core   |
                   +------------------+
                            |
                            v
                   +------------------+
                   |     Wlo.Core     |
                   |  (Data Models)   |
                   +------------------+
                            |
                            v
                   +------------------+
                   |    RCLibrary     |
                   | (Network/DB/IO)  |
                   +------------------+
```

### 2.1 Project Catalog

1. [`Wonderland Private Server`](file:///D:/GitHub/Wonderland-Private-Server/Wonderland%20Private%20Server.csproj): Root Windows Forms host executable. Integrates GUI administration dashboards, runtime telemetry, GM chat studio, configuration forms, and startup bootstrappers.
2. [`wlo.pserver.core`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/wlo.pserver.core.csproj): Core game domain engine. Contains player state machines, entity replication, turn-based combat, companion AI, vehicle logic, tent manufacturing, quest state machines, and Action Code handlers (`Src/Network/ActionCodes`).
3. [`wlo.pserver.maps`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.maps/wlo.pserver.maps.csproj): Spatial collision and map coordinate infrastructure.
4. [`PhoenixData`](file:///D:/GitHub/Wonderland-Private-Server/PhoenixData/PhoenixData.csproj): Dedicated binary file decoders for client assets (`Talk.dat`, `Item.dat`, `Skill.dat`, `Mark.dat`, `Formula.dat`, `Compound2.dat`).
5. [`Phoenix.Core`](file:///D:/GitHub/Wonderland-Private-Server/Phoenix.Core/Phoenix.Core.csproj): Data container abstractions and binary parsing primitives.
6. [`Wlo.Core`](file:///D:/GitHub/Wonderland-Private-Server/Wlo.Core/Wlo.Core.csproj): Shared data contracts, structs, and domain primitives.
7. [`RCLibrary`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/RCLibrary.csproj): Low-level network framing, TCP server abstractions, XOR ciphers, database persistence providers (SQLite and MySQL), and pathing utilities.
8. [`wlo.pserver.Bot`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.Bot/wlo.pserver.Bot.csproj): Automated client simulation framework for stress testing and bot integration.

---

## 3. Server Socket Topology

The server architecture utilizes an authentic multi-port distributed network model:

```
+-------------------+-----------------------+---------------+-----------------------------------------------+
| Server Service    | Implementation Class  | Default Port  | Primary Responsibility                        |
+-------------------+-----------------------+---------------+-----------------------------------------------+
| Login Server      | LoginServer           | 6414 TCP      | Client authentication, slot sync, AC 63       |
| World Server      | WorldServer           | 6415 TCP      | World simulation, combat, map entities, chat  |
| Item Mall Server  | ItemMallServer        | 6416 TCP      | In-game microtransaction catalog, AC 75       |
| Server Status     | ServerStatusManager   | 6416 TCP (Alt)| Status probe responder (0xC9 opcode)          |
| Web API & Reg     | RegistrationServer    | 8080 HTTP     | Web registration API, catalog endpoints, CORS |
| Embedded Web      | EmbeddedWebServer     | Dynamic HTTP  | Client web0.DAT live patcher                  |
+-------------------+-----------------------+---------------+-----------------------------------------------+
```

### 3.1 Login Server (Port 6414 TCP)
* **Implementation:** [`Src/Server/LoginServer.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Server/LoginServer.cs) inheriting from [`RCLibrary.Core.Networking.TcpServer`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/RCLibrary.Core.Networking/TcpServer.cs).
* **Connection Lifecycle:**
  1. Binds an asynchronous TCP listener on `0.0.0.0:6414` with backlog 40.
  2. The `ListenThread` processes incoming connections via `m_Socket.Accept()`.
  3. Deduplicates incoming IP connections against `ClientList`.
  4. Wraps socket in [`LoginClient`](file:///D:/GitHub/Wonderland-Private-Server/Src/Server/LoginClient.cs) and triggers `OnNewPlayer(this, Player p)`.
  5. Coordinates character slot serialization (`AC 63:2`) and character world handoffs (`AC 63:5`).
* **Exception Handling:** Explicitly filters expected non-fatal network exceptions: `SocketError.WouldBlock`, `SocketError.Interrupted`, `SocketError.OperationAborted`, and WinSock code 10004.

### 3.2 World Server (Port 6415 TCP)
* **Implementation:** [`Src/Server/WorldServer.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Server/WorldServer.cs).
* **Multi-Threaded Execution Model:** Operates four dedicated background synchronization loops:
  - `Mainthrd` (*World Manager Main Thread*, 2ms sleep): Dequeues authenticated players from `QueuedPlayerLogin`, validates connection vitality (`!src.isDisconnected()`), and initiates `CommenceLogin(Player src)`.
  - `MapTickThread` (*Map & NPC Tick Thread*, 500ms sleep): Iterates over [`MapManager.Instance.ActiveMaps`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/MapManager.cs), calling `map.Process()` to advance NPC roaming behaviors (`QuestNpc.Update`) and evaluate terrain item respawns.
  - `AutoSaveThread` (*Auto-Save Thread*, 1,000ms sleep): Periodically flushes online character dirty states, inventories, companion stats, and quest journals to the database.
  - `Eventthrd` (*World Manager Event Thread*, 120ms sleep): Dispatches scheduled world events and timers.

### 3.3 Item Mall Server (Port 6416 TCP)
* **Implementation:** [`Src/Server/ItemMallServer.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Server/ItemMallServer.cs).
* **Protocol:** Binds `TcpListener(IPAddress.Any, 6416)` with backlog 20. Accepts incoming connections via `ThreadPool.QueueUserWorkItem`. The client connects, receives the complete binary catalog stream via `BuildCatalogPayload()`, and the connection is immediately terminated cleanly using `SocketShutdown.Both` and `Close()`.

### 3.4 Web Registration & Management REST API (Port 8080 HTTP)
* **Implementation:** [`Src/Server/API/RegistrationServer.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Server/API/RegistrationServer.cs).
* **Endpoints:**
  - `POST /register`: Handles JSON and URL-encoded account creation, enforces username/password length invariants, and invokes [`UserDataBase.RegisterUser`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/UserDataBase.cs).
  - `GET /register`: Serves responsive HTML account registration interface.
  - `GET /api/catalog`: Returns JSON item mall catalog.
  - `GET /api/buy`: Handles web-initiated item deliveries via [`ItemMallManager.PurchaseItem`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/ItemMallManager.cs).
  - `OPTIONS *`: Injects Cross-Origin Resource Sharing (CORS) headers (`Access-Control-Allow-Origin: *`).

---

## 4. Low-Level Network Ping-Pong Loop

Client packet transmission and reception is implemented in [`RCLibrary/System.Net.Sockets/Client3.cs`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/System.Net.Sockets/Client3.cs) (`SClient` / `Client3`):

```
        [Client Socket]
               |
               v
     +-------------------+
     |     RecvProc      | <--- 3ms delay loop
     |   m_Socket.Recv   |
     +-------------------+
               |
               v
     +-------------------+
     | UnfinishedPacket  | <--- XOR 0xAD Decryption & 0x44F4 Frame Check
     |      InData       |
     +-------------------+
               | (When Complete Packet Assembled)
               v
     +-------------------+
     |  onPacketRecved   | ---> Player.ProcessSocket(packet)
     +-------------------+
               |
               v
     +-------------------+
     |     SendProc      | <--- Drains ConcurrentQueue<OutgoingPacket>
     |   m_Socket.Send   |
     +-------------------+
```

* **Disconnect Recovery:** When `m_Socket.Receive` returns 0 bytes or throws a fatal socket fault, `m_Disconnected` is set to `true`, the `onConnectionLost` event fires, and `Disconnect()` purges lingering socket allocations.

---

## 5. Dynamic Portability Engine

To ensure zero configuration requirements across developer environments, path resolution is unified through [`RCLibrary.Core.PathHelper`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/RCLibrary.Core/PathHelper.cs):
* **Automatic Discovery:** Resolves relative working directories, execution binaries, project roots, asset directories (`./Data`), and client executable paths (`aLogin.exe`, `WLO.exe`) dynamically.
* **Elimination of Hardcoded Paths:** All absolute paths have been eliminated from the code graph.

---

## 6. Graceful Shutdown & Diagnostic Countdown

Server termination initiates a non-destructive state flush in [`Src/Gui/MainForm1.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.cs):
1. **Network Ingress Lock:** Rejects new socket connections across Login, World, and API ports.
2. **Synchronous Persistence Flush:** Iterates all active players, executing `player.SaveCharacterData()` to commit inventories, equipment durability, companion stats, and quest progressions.
3. **Diagnostic Shutdown Countdown:** Executes an automated 10-second countdown in the console, outputs log directory locations, and invokes clean process termination.
