# Wonderland Online Private Server & CheatEngine

A high-performance, modular private server emulator and cheat/administration engine for **Wonderland Online (WLO)** written in C# (.NET Framework 4.8 / .NET runtime). Engineered for **100% dynamic portability**, allowing zero-configuration cloning, building, and running across any Windows machine or drive.

---

## Key Features

- **100% Dynamic Portability**: Zero hardcoded absolute paths. Seamless path resolution across all workstations via `RCLibrary.Core.PathHelper`.
- **Self-Healing SQLite Database**: Consolidated single database in `Data/ServerDataBase.db` with relational persistence tables. Automatically verifies schemas, applies unique indexes, and seeds missing default data upon every boot (`GameDataBase.VerifySetup()`).
- **Tri-Server Socket Architecture**:
  - **Login Server (Port 6414)**: Authentication, account management, character selection/creation, Item Mall catalog sync (`AC 75`).
  - **World Server (Port 6415)**: Overworld replication, tile pathfinding, turn-based combat engine, quests, dialogue engine, housing.
  - **Item Mall Server (Port 6416)**: Microtransaction catalog and promotional point currency transactions.
  - **Registration API (Port 8080)**: REST API endpoint for account creation.
- **Complete Action Code Protocol Catalog (AC 0 - AC 226)**: Full support for turn-based battles (`AC 11`/`AC 50`/`AC 51`), vehicle/mount system (`AC 15`), tent housing (`AC 12`), quests (`AC 39`/`AC 52`), scene entities (`AC 22`), inventory bag (`AC 23`), and cinematic cutscenes (`AC 20`/`AC 186`).
- **Authentic Asset Parsing**: In-memory decryption and caching of `Npc.dat` (4,928 NPCs via XOR `0x5209`), `Item.dat`, `Skill.dat`, `Talk.dat` (17,494 records), `Mark.dat`, `SceneData.dat`, and `Eve.emg` event scripts.
- **Comprehensive GUI Management Suite**: Live player monitoring, map inspector, NPC/Mob editor, quest manager, Item Mall studio, chest drop editor, and firewall security center.
- **Dynamic Ground Items Lifecycle (`AC 23`)**: Automatic map loading of native terrain resources from `Eve.emg` `ItemAreas` (209 items across 77 maps). Real-time authentic batched spawning (`AC 23:4`), terrain slot pickups (`AC 23:2`), gold item banner acquisition popups (`AC 23:6`), and asynchronous heartbeat respawning.
- **Robust NPC Spatial AI & Wander Boundaries**: Authentic signed bounding-box roaming (`WalkBehavior == 3`), waypoint patrol oscillation (`WalkBehavior == 2 / 5`), and static anchors (`WalkBehavior == 1`) preventing map boundary drift or corner teleports.
- **State-Verified Dialogue & Quest Safeguards**: Multi-event evaluation prioritizing post-quest resolution over completed stages, Quest State Condition decoding, paired quest completion flags (`questId + 1`), persistent SQLite quest `step` tracking, and irreversible completion locks preventing quest demotion loops.
- **Authentic Starter Pack, 31-Byte Record Alignment, Chest Safeguards & Raft Wreck Protocol**: Complete 31-byte fixed record item serialization (`AC 23:5` / `AC 30:5`) and 21-byte equipment serialization (`AC 23:11`) preventing client parser desynchronization and guaranteeing full multi-item visibility across all 50 bag slots. Deduplicated single `AC 23:6` acquisition banner and `player.Quests` persistence check preventing chest double-granting. Client bag slot tracking (`MountedVehicleSlot`) with single `AC 23:9` slot clearing and authentic 7-step shore wrecking sequence (`AC 15:14 -> AC 23:9 -> AC 15:15 -> AC 15:11 -> AC 5:4`) preventing raft phantom icons.
- **Pre-Event NPC Visibility & Dynamic Entity Isolation**: Comprehensive multi-target subentry PreEvent evaluation (`handledNpcs`), authentic 14-byte `AC 22:4` entity table serialization utilizing Byte 8 (`entityType = 2` to completely suppress rendering, mouse interaction, and proximity collisions), followed by authentic `AC 22:10` + `AC 22:11` dynamic concealment frames dispatched between `AC 23:102` map loading finished and `AC 20:8` input unlock. Fully isolates staged quest NPCs and props on Map 12000 (Roca ClickID 32 at village center, grave Rocas ClickID 34 & 36, Father's Statue ClickID 33, Iron Sword ClickID 35, permanent dog ClickID 29 always visible, missing dog ClickID 28 concealed until found, and staged pigs ClickID 14 & 15 concealed prior to step 2).
- **Companion Actor Despawning & Overworld Visibility Isolation**: Full dynamic despawning of recruited companion NPCs (Robinson, Roca, S. Monkey, Clive, Niss, etc.) using dual `AC 22:10` (actor unbind/despawn frame) and `AC 22:11` (stage isolation frame). Multi-tier resolution through `Player.IsSamePetOrCompanion`, `Npc.dat` canonical name resolution for generic Eve names (`" Npc"`), active pet verification, and quest flag safeguards completely eliminating overworld companion duplicates (e.g. Robinson on Starter Beach Map 11016).
- **Cinematic Cutscene Timeline & Animation Pre-Dialogue Sequencing**: Authentic dispatch of cutscene animation opcodes (`AC 20:1 SubCode 1 Type 5 Cutscene ID 12008` for Rhode Island Beach arrival) with client ACK (`AC 20:6`) step synchronization, mobility restoration (`AC 20:8`, `AC 5:4`), and fail-safe timeouts ensuring cutscene animations play to completion before dialogue trees begin.
- **Graceful Shutdown & Diagnostic Countdown**: Non-immediate shutdown saves all player, inventory, and server data, displays the exact canonical log file location on the console, and executes a 10-second countdown before process exit.
- **Codebase Deduplication & Dead-Code Optimization**: System-wide cleanup purging 13 uncompiled legacy files, eliminating 1,850+ lines of dead commented code across combat, player, and action code subsystems, centralizing database table DDL checks to boot-time execution, extracting unified peer entity replication helpers in Map engine, and enforcing 0-error 0-warning compilation.

---

## Technical Documentation (`docs/`)

The server technical documentation is organized into a cohesive 10-document master specification suite:

1. [**01 - System Architecture and Server Topology**](file:///D:/GitHub/Wonderland-Private-Server/docs/01_system_architecture_and_server_topology.md): Multi-server socket topology, dynamic portability engine (`PathHelper`), threading model, project graph, and graceful shutdown.
2. [**02 - Database Schema and Persistence Lifecycle**](file:///D:/GitHub/Wonderland-Private-Server/docs/02_database_schema_and_persistence.md): Centralized SQLite relational tables (`users`, `characters`, `character_inventory`, `character_equipment`, `character_pets`, `character_quests`), startup schema verification, and atomic transaction lifecycles.
3. [**03 - Network Protocol and Action Codes**](file:///D:/GitHub/Wonderland-Private-Server/docs/03_network_protocol_and_action_codes.md): Binary wire framing, `0x44F4` magic header, little-endian serialization (`Tools.FromFormat`), and exhaustive Action Code reference catalog.
4. [**04 - Data File Pipeline and Asset Parsing**](file:///D:/GitHub/Wonderland-Private-Server/docs/04_data_file_pipeline_and_asset_parsing.md): In-memory client asset decoders (`Npc.dat` XOR `0x5209` cipher, `Item.dat` 45-byte structs, `Talk.dat` 292-byte dialogues, `SceneData.dat`), and `eve.Emg` bytecode engine.
5. [**05 - Map Engine and Entity Lifecycle**](file:///D:/GitHub/Wonderland-Private-Server/docs/05_map_engine_and_entity_lifecycle.md): Isometric coordinate systems, NPC roaming AI, 209-item ground harvesting loop, 14-byte `AC 22:4` scene table, concealment frame sequencing, and peer replication.
6. [**06 - Dialogue, Quest State Machine, and Cutscene Engine**](file:///D:/GitHub/Wonderland-Private-Server/docs/06_dialogue_quest_and_cutscene_engine.md): Modal dialogue boxes, 24-bit talk IDs, choice callbacks, quest state monotonic progression, PreEvent condition evaluation, and cinematic timelines.
7. [**07 - Gameplay Subsystems and Mechanics**](file:///D:/GitHub/Wonderland-Private-Server/docs/07_gameplay_subsystems_and_mechanics.md): 50-slot inventory, companion recruitment and amity, vehicle systems with 7-step raft shore shipwreck sequence, tent housing, and turn-based combat math with elemental wheel.
8. [**08 - GUI Administration Suite and GM Engine**](file:///D:/GitHub/Wonderland-Private-Server/docs/08_gui_administration_and_gm_engine.md): Windows Forms dashboard, 13 operational management tabs, deep character editor, and in-game GM chat command directory (`:heal`, `:level`, `:item`, `:warp`, etc.).
9. [**09 - Developer Tooling and Reverse Engineering**](file:///D:/GitHub/Wonderland-Private-Server/docs/09_developer_tooling_and_reverse_engineering.md): Asynchronous rotating diagnostic logs (`DebugSystem`), PCAP packet capture analysis, and Ghidra MCP reverse engineering bridge.
10. [**10 - Deployment Operations and Codebase Integrity**](file:///D:/GitHub/Wonderland-Private-Server/docs/10_deployment_operations_and_codebase_integrity.md): Prerequisites, zero-error build command, client configuration (`SERVER.INI`), and codebase deduplication audit.

---

## Quick Start Guide

### 1. Prerequisites
- Windows 10 / 11 (64-bit)
- .NET Framework 4.8 or later
- Visual Studio 2022 or .NET SDK (`dotnet build`)
- Wonderland Online Client (e.g., [Rhode Island Client](https://drive.google.com/file/d/18z5H1w5G9GujMJywRHL-uOac4fFyOTSY))
  - Note: Large 1.42 GB client sprite archive `odd.dat` is available on [Releases v1.0.0](https://github.com/Eminbalci/Wonderland-Private-Server/releases/tag/v1.0.0).

### 2. Build the Server
```powershell
dotnet build "Wonderland Private Server.sln" --configuration Debug
```

### 3. Launch Server & Client
1. Ensure client `SERVER.INI` points to `127.0.0.1`.
2. Run `bin/Debug/Wonderland Private Server.exe`.
3. Wait until the dashboard displays operational status.
4. Press `F5` in the server window (or run `aLogin.exe` in the client directory).
5. Log in with default GM accounts:
   - User: `admin` / Password: `password` (GM Level 10)
   - User: `developer` / Password: `password` (GM Level 10)

---

## In-Game GM Chat Commands

Type commands into standard in-game chat to execute administrative operations:

| Command | Usage | Description |
| :--- | :--- | :--- |
| `:heal` / `:full` | `:heal [hp] [sp]` | Restores character HP and SP to maximum (or specified values). |
| `:level` / `:lvl` | `:level <1-200>` | Sets character level and recalculates derived base stats. |
| `:points` / `:sp` | `:points <amount>` | Grants unallocated attribute stat points. |
| `:gold` / `:money` | `:gold <amount>` | Updates character wallet gold balance. |
| `:exp` | `:exp <amount>` | Sets total character experience points. |
| `:stats` / `:stat` | `:stat <str> <con> <int> <wis> <agi>` | Distributes character base attribute points. |
| `:item` | `:item [add] <id> [count]` | Injects item(s) directly into character inventory. |
| `:skill` | `:skill <id> [grade]` | Unlocks or levels up a specific skill ID. |
| `:warp` / `:goto` | `:warp <map_id> <x> <y>` | Teleports player and followers to map coordinates. |
| `:buy` | `:buy <query/id> [quantity]` | Buys or spawns item from Item Mall or Item.dat. |
| `:pet` | `:pet <pet_id>` | Spawns and recruits specified companion into party. |
| `:unride` | `:unride` | Dismounts active vehicle or riding pet. |
| `:help` | `:help` | Displays command list and syntax help. |
