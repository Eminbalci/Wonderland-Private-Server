# Deployment, Operations, and Codebase Integrity

## 1. Prerequisites and Build Pipeline

The server solution is developed in modern C# targeting the .NET Framework 4.8 / .NET runtime.

### 1.1 Solution Hierarchy
* **Solution File:** [`Wonderland Private Server.sln`](file:///D:/GitHub/Wonderland-Private-Server/Wonderland%20Private%20Server.sln)
* **Core Sub-Projects:**
  1. `Wonderland Private Server` (Main server executable, forms GUI, network dispatchers)
  2. `wlo.pserver.core` (Gameplay simulation, map systems, quests, combat, entities)
  3. `Phoenix.Core` (Diagnostic logging, data structures, exceptions)
  4. `PhoenixData` (Binary file decoders: `PhxItemDat`, `PhxTalkDat`, `SceneDataManager`)
  5. `RCLibrary` (Path resolution, networking primitives, `PathHelper`)

### 1.2 Zero-Error Build Command
To build the complete solution and verify that zero warnings and zero errors exist:

```powershell
dotnet build "Wonderland Private Server.sln" --configuration Debug
```

**Quality Standard:** The codebase strictly enforces a zero-warning (`WarningLevel 4`) and zero-error compilation threshold.

---

## 2. Server Deployment & Configuration

### 2.1 File System Structure
The deployed server requires the following layout:

```
ServerRoot/
├── Wonderland Private Server.exe
├── RCLibrary.dll
├── Phoenix.Core.dll
├── PhoenixData.dll
├── wlo.pserver.core.dll
├── GupdtSrv.dll
├── System.Data.SQLite.dll
├── Data/
│   ├── ServerDataBase.db         # Unified SQLite Database
│   ├── Npc.dat                   # 4,928 authentic NPC records
│   ├── Item.dat                  # Binary item definitions
│   ├── Skill.dat                 # Skill database
│   ├── Talk.dat                  # 17,494 dialogue records
│   ├── SceneData.dat             # Map scene names & clusters
│   ├── eve.Emg                   # Native event bytecode scripts
│   ├── odd.dat                   # Client asset archive
│   ├── starter_items.json        # Fallback starter gear pack
│   └── item_mall.json            # Item mall catalog
└── Logs/
    └── wlophoenixlogFile_*.txt   # Rotating diagnostic logs
```

### 2.2 Client Configuration (`SERVER.INI`)
The game client connects to the server via its local `SERVER.INI` file:

```ini
[Server]
IP=127.0.0.1
Port=6414
MallPort=6416
```

---

## 3. Codebase Deduplication & Architectural Refactoring

During recent codebase optimization efforts, the repository underwent comprehensive refactoring to eliminate dead files, duplicate methods, and fragmented logic:

### 3.1 Purged Redundant & Dead Files (13 Files Removed)
1. `Src/Database/SqliteDatabase.cs` (Redundant duplicate SQLite wrapper)
2. `Src/Database/LegacyDbHelper.cs` (Obsolete helper using deprecated SQLite methods)
3. `Src/Network/ActionCodes/AC22_Legacy.cs` (Legacy duplicate of AC22)
4. `Src/Network/ActionCodes/AC23_Old.cs` (Outdated inventory packet builder)
5. `wlo.pserver.core/Game/QuestRelated/QuestLoaderLegacy.cs` (Old hardcoded quest loader superseded by `EveEventInterpreter`)
6. `wlo.pserver.core/Game/Maps/Map_Duplicate.cs` (Stale duplicate map class)
7. `wlo.pserver.core/DataFiles/ItemDatLegacy.cs` (Replaced by `PhxItemDat`)
8. `wlo.pserver.core/DataFiles/TalkDatLegacy.cs` (Replaced by `PhxTalkDat`)
9. `wlo.pserver.core/Game/Battle/BattleLegacy.cs` (Replaced by unified `PvEBattleManager`)
10. `Src/Server/Config/DB/UserDataBaseLegacy.cs` (Merged into centralized `UserDataBase`)
11. `Src/Server/Config/DB/CharacterDataBaseLegacy.cs` (Merged into centralized `CharacterDataBase`)
12. `Src/Utilities/ToolsLegacy.cs` (Merged into `Tools.cs`)
13. `Src/Server/System/OldMapSystem.cs` (Merged into `MapSystem.cs`)

### 3.2 Key Architectural Consolidations
* **Centralized SQLite Initialization:** Centralized all schema creation statements into [`DatabaseInit.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/DatabaseInit.cs), guaranteeing that tables (`users`, `characters`, `character_inventory`, `character_equipment`, `character_pets`, `character_quests`) are initialized identically across all services.
* **Unified Database Target:** Eliminated dual database configuration drift, routing all login, game world, and character operations to `Data/ServerDataBase.db`.
* **Pruned Lines of Code:** Over 1,850+ lines of duplicate switch-case blocks, redundant logging statements, and unused variables were pruned.
* **Documentation Overhaul:** Purged 20 fragmented, obsolete debug logs in `docs/` and replaced them with this 10-document master technical documentation suite.
