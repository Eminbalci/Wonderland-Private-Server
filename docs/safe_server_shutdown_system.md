# Safe Server Shutdown System

## Overview
The Safe Server Shutdown system guarantees that no player data (inventories, equipment, stats, positions, quests, skills, or Item Mall points) or server configurations are lost or corrupted when shutting down the server.

---

## Key Improvements & Bug Fixes

1. **Deadlock & Thread Race Elimination**:
   - Previously, clicking `Safe Server Shutdown` or the window `[X]` button caused a race condition between UI thread `Application.Exit()` and the background worker loop calling `ShutDown()`.
   - Replaced multi-threaded collision with an atomic, re-entrancy safe method: `PerformSafeShutdown()`.

2. **Full Lifecycle Execution Sequence**:
   1. **In-Game Warning**: Dispatches a system message (`AC 23:57`) to all online players alerting them of the shutdown.
   2. **Atomic Player Persistence**: Saves all online characters (`cGlobal.gCharacterDataBase.WritePlayer`) and updates user account Item Mall points (`cGlobal.gUserDataBase.SetIMPoints`).
   3. **Config & Drop Persistence**: Flushes `ChestDropManager` JSON configs and global settings (`Config.settings.wlo`).
   4. **Clean Disconnections**: Gracefully disconnects all player socket sessions.
   5. **Listener Teardown**: Stops `gRegistrationServer` (HTTP 8080), `gLoginServer` (TCP 6414), `gItemMallServer` (TCP 6415), and `gWorld` threads.
   6. **Clean Process Termination**: Calls `Environment.Exit(0)` to prevent lingering zombie threads or port bindings.

---

## Source Files
- [`Src/Gui/MainForm1.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.cs)
- [`Src/cGlobal.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/cGlobal.cs)
- [`Src/Server/WorldServer.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Server/WorldServer.cs)
- [`Src/Server/API/RegistrationServer.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Server/API/RegistrationServer.cs)
