# Wonderland Online Private Server & CheatEngine

A high-performance, modular private server emulator and cheat/administration engine for **Wonderland Online (WLO)** written in C# (.NET Framework 4.8 / .NET runtime). Engineered for **100% dynamic portability**, allowing zero-configuration cloning, building, and running across any Windows machine or drive.

---

## Key Features

- **100% Dynamic Portability**: Zero hardcoded absolute paths. Seamless path resolution across all workstations via `RCLibrary.Core.PathHelper`.
- **Dual Database Engine (SQLite & MySQL / MariaDB)**: Full GUI-configurable persistence supporting both embedded SQLite (`Data/ServerDataBase.db`) and remote/local MySQL servers. Includes non-blocking connection testing, live runtime switching across all active database instances, automatic database & schema auto-creation (`VerifySetup()`), transparent dialect translation (`TranslateSqlForMySql`), and a built-in 1-click SQLite-to-MySQL data migration engine with real-time progress reporting.
- **Tri-Server Socket Architecture**:
  - **Login Server (Port 6414)**: Authentication, account management, character selection/creation, Item Mall catalog sync (`AC 75`).
  - **World Server (Port 6415)**: Overworld replication, tile pathfinding, turn-based combat engine, quests, dialogue engine, housing.
  - **Item Mall Server (Port 6416)**: Microtransaction catalog and promotional point currency transactions.
  - **Registration API (Port 8080)**: REST API endpoint for account creation.
- **Complete Action Code Protocol Catalog (AC 0 - AC 226)**: Full support for turn-based battles (`AC 11`/`AC 50`/`AC 51`/`AC 52`/`AC 53`), lucky draw wheel (`AC 104`), vehicle/mount system (`AC 15`), tent housing (`AC 12`), quests (`AC 39`/`AC 52`), scene entities (`AC 22`), inventory bag (`AC 23`), friend system (`AC 14`), companion pets (`AC 15`/`AC 19`), and cinematic cutscenes (`AC 20`/`AC 186`).
- **Authentic Pet & Companion Lifecycle Protocol Engine (`AC 15`, `AC 19`, `AC 8:2`)**: Official packet-level implementation of character login companion roster synchronization (`AC 15:1` per companion + authentic skill unlock via `AC 8:2` Stat `0x016F` with TargetType `0x04` and roster slot), dynamic companion skill catalog resolution spanning curated human abilities and `Npc.dat` `SkillID1..3` fallback, in-battle real-time SP consumption sync (`AC 8:2` Stat `0x011A`) and skill proficiency EXP (`AC 8:2` Stat `0x016F`), real-time HP synchronization upon taking damage, healing, or revival (`AC 8:2` Stat `0x0119`), post-battle level-up progression sync (`AC 8:2` Level `0x011D`, HP `0x0119`, SP `0x011A`), combat battle EXP gain sync (`AC 8:2` Stat `0x0124` matching PCAP Seq 1180), companion battle auto-revival for knocked out pets entering combat, active battle companion designation (`AC 19:4` / `AC 19:1`), authentic overworld visual follower spawning (`AC 15:4` with 8-byte trailing equipment padding) broadcast to map peers without leaking private pet roster packets to foreign clients, standby/rest despawning (`AC 19:7 [CharID: UInt32]` followed by `AC 19:2`), mount/dismount replication (`AC 15:11` -> `AC 15:16`, `AC 15:12` -> `AC 15:17`), and combat battle spawn alignment in `AC 11:5` (friendly team side `0x05`, enemy side `0x01`, pet slot indexing, and corrected Level/Element byte ordering).
- **Authentic Turn-Based Combat Protocol Engine**: Byte-for-byte alignment with official network captures (`session_20260911_150803`). Resolves monster stats and templates via an O(1) in-memory cache loaded from `npc_data` table (4,928 authentic entries), serializes fighter entities in `AC 11:5` & `AC 11:250` with exact byte offsets (Byte 26 = Level, Byte 27 = Element, preventing the "Lv. 0" display defect), prompts turn input via `AC 52:1` without premature `AC 50:6`, acknowledges player moves via `AC 53:5` to advance focus automatically to pet, broadcasts active-turn replay focus via `AC 50:6`, serializes continuous 19-byte `AC 50:1` action animation records, triggers in-combat knockouts via `AC 53:3` without premature entity despawns, synchronizes real-time stats via `AC 51:1`, broadcasts `AC 11:4` crossed-swords combat presence (`[0x02, CharID: UInt32, 0x00, 0x00, State: Byte]`) to map peers via strongly-typed packet serialization eliminating `System.OverflowException` on 32-bit Character IDs, calibrates combat drop engine to authentic MMO rates (capping at 1 item drop per monster kill with full inventory detection and chat alerts, plus `:clearinv` bag cleanup), and executes canonical exit despawn sequencing (`AC 11:12` -> 4-byte pet `AC 11:1` -> 6-byte player `AC 11:0` -> 5-byte player `AC 11:1`).
- **Authentic Social & Friend Protocol Engine (`AC 14` & `AC 10`)**: Official packet-level implementation of friend tabs (`AC 14:11`, 26 bytes/friend), main roster (`AC 14:5`, 28 bytes/friend), mutual invitation/acceptance handshake (`AC 14:2` -> `AC 14:3` -> `AC 14:9`), dual live online presence notifications (`AC 14:7` and `AC 10:3 [CharID, 0xFF]`), and bilateral friend removal with UI sprite/row de-allocation (`AC 14:4 <TargetCharID>`).
- **Authentic Asset Parsing**: In-memory decryption and caching of `Npc.dat` (4,928 NPCs via XOR `0x5209`), `Item.dat`, `Skill.dat`, `Talk.dat` (17,494 records), `Mark.dat`, `SceneData.dat`, and `Eve.emg` event scripts (1,119 maps, 10,644 event scripts, 1,412 PreEvents, 8,181 native NPCs, and 2,791 warps).
- **Comprehensive GUI Management Suite**: Live player monitoring, map inspector, NPC/Mob editor, quest manager, Item Mall studio, chest drop editor, and firewall security center.
- **Dynamic Ground Items Lifecycle (`AC 23`)**: Automatic map loading of native terrain resources from `Eve.emg` `ItemAreas` (209 items across 77 maps). Real-time authentic batched spawning (`AC 23:4`), terrain slot pickups (`AC 23:2`), gold item banner acquisition popups (`AC 23:6`), and asynchronous heartbeat respawning.
- **Robust NPC Spatial AI & Wander Boundaries**: Authentic signed bounding-box roaming (`WalkBehavior == 3`), waypoint patrol oscillation (`WalkBehavior == 2 / 5`), and static anchors (`WalkBehavior == 1`) preventing map boundary drift or corner teleports.
- **State-Verified Dialogue & Quest Safeguards**: Multi-event candidate evaluation prioritizing post-quest resolution over completed stages, multi-candidate event aggregation (direct clickID + linked NPC events) with strict quest/companion condition qualification without premature forced defaults, zero-op condition gate priority pass supporting dual item (`w1=1`) and companion (`w1=2`, active team, capacity, recruitment) condition decoding, Quest State Condition decoding, paired quest completion flags (`questId + 1`), persistent SQLite quest `step` tracking, and irreversible completion locks preventing quest demotion loops.
- **Authentic Starter Pack, 31-Byte Record Alignment, Chest Safeguards & Raft Wreck Protocol**: Complete 31-byte fixed record item serialization (`AC 23:5` / `AC 30:5`) and 21-byte equipment serialization (`AC 23:11`) preventing client parser desynchronization and guaranteeing full multi-item visibility across all 50 bag slots. Deduplicated single `AC 23:6` acquisition banner and `player.Quests` persistence check preventing chest double-granting. Client bag slot tracking (`MountedVehicleSlot`) with single `AC 23:9` slot clearing and authentic 7-step shore wrecking sequence (`AC 15:14 -> AC 23:9 -> AC 15:15 -> AC 15:11 -> AC 5:4`) preventing raft phantom icons.
- **Pre-Event NPC Visibility, Staged Quest Lifecycle & Universal Cutscene Dummy Suppression**: Comprehensive bidirectional multi-target PreEvent evaluation and per-player entity lifecycle tracking (`player.HiddenNpcClickIDs`). Authentic 14-byte `AC 22:4` entity table serialization utilizing State `0xFFFF`, EntityType Byte 8 (`2 = Hidden`), and Despawn Code `0x03E7FC18` (65,535,000 ms) invoking client `FUN_00432674` to set `*(actor + 0x1eec) = 2` and clear map collision grid via `FUN_0043d390`. Suppresses client actor rendering (`FUN_0043d58c`) and disables mouse cursor hit-testing (client lines 308016 & 308092). Dynamic runtime concealment (despawn) and reveal (spawn) transmits single-record `AC 22:4` frames (`[ClickID, 0xFFFF, X, Y, Type=2, Duration=0x03E7FC18, 0]` for despawn, `[ClickID, 0x00FF, X, Y, Type=1, Duration=0, 0]` for spawn). Evaluates `Eve.emg` 21-byte PreEvent condition bytecodes (Opcodes `0x01` unconditional, `0x02` companion recruitment mode 1 [recruited] / mode 2 [not recruited], `0x03` quest step/item check, `0x05` quest marks/steps) with multi-subentry compound rule grouping (AND logic across subentries with trailing zero-action conditions) and Action Opcode `0x02` (ActionType `2` conceal vs ActionType `3` reveal). Universally detects and eliminates 230 duplicate cutscene dummy actors across 108 maps by concealing zero-event puppets sharing Template IDs with primary dialogue NPCs on map entry. Enforces default concealment for all staged actors targeted by `ActionType 3` (reveal) rules until their matching quest conditions evaluate to true. Integrates declarative `SpawnNpcClickIDs` and `DespawnNpcClickIDs` into `QuestDefinition` and `QuestStep`, automatically synchronizes visibility in real time across `QuestManager` lifecycle operations (`AcceptQuest`, `AdvanceQuestStep`, `SetPlayerQuestState`, `CompleteQuest`, `ResetQuest`) and `EveEventInterpreter` opcodes (2, 3, 5, battle victory) without requiring map re-entry, and enforces server-side interaction guards in `AC20.Recv1` and `Map.ProcessInteraction`. Fully isolates staged quest NPCs and props on Map 12000, Map 12001 (Chief's House: staged cutscene Roca ClickID 3 concealed, static Roca ClickID 2 hidden upon recruitment), Map 11003 (Holy Village: Niss ClickID 9 visible, dummy puppets 38/39 concealed; Match Girl ClickID 11 visible, dummy puppet 18 concealed), and Map 11016 (S. Monkey visible to new players, concealed upon recruitment; Robinson re-issue and seasonal NPCs suppressed until active).
- **Interactive Map Props, Chests & Entity State Synchronization (`AC 22:4`, `AC 22:1`, `AC 22:10`)**: Authentic 14-byte entity table synchronization with proper intact frame (`0x00FF`) vs opened/broken frame (`0x0001`), eliminating the bug where barrels/chests spawned broken by default due to inverted `0x0000` state. Per-player quest container isolation preventing one-time quest props from leaking shared broken state across players, unicast break animations for quest containers, and automatic intact frame restoration (`AC 22:1` Action 0) upon renewable gathering node respawn.
- **Companion Actor Despawning & Overworld Visibility Isolation**: Full dynamic despawning of recruited companion NPCs (Robinson, Roca, S. Monkey, Clive, Niss, etc.) using authentic `AC 22:4` concealment frames (`State = 0xFFFF`, `EntityType = 2`, `Duration = 0x03E7FC18`). Multi-tier resolution through `Player.IsSamePetOrCompanion`, `Npc.dat` canonical name resolution for generic Eve names (`" Npc"`), active pet verification, and quest flag safeguards (including Quest 12002 for S. Monkey) completely eliminating overworld companion duplicates (e.g. Robinson on Starter Beach Map 11016).
- **Cinematic Cutscene Timeline & Animation Pre-Dialogue Sequencing**: Authentic dispatch of cutscene animation opcodes (`AC 20:1 SubCode 1 Type 5 Cutscene ID 12008` for Rhode Island Beach arrival) with client ACK (`AC 20:6`) step synchronization, mobility restoration (`AC 20:8`, `AC 5:4`), and fail-safe timeouts ensuring cutscene animations play to completion before dialogue trees begin.
- **Graceful Shutdown & Diagnostic Countdown**: Non-immediate shutdown saves all player, inventory, and server data, displays the exact canonical log file location on the console, and executes a 10-second countdown before process exit.
- **Codebase Deduplication & Dead-Code Optimization**: System-wide cleanup purging 13 uncompiled legacy files, eliminating 1,850+ lines of dead commented code across combat, player, and action code subsystems, centralizing database table DDL checks to boot-time execution, extracting unified peer entity replication helpers in Map engine, and enforcing 0-error 0-warning compilation.

---

## Technical Documentation (`docs/`)

The server technical documentation is organized into a cohesive 11-document master specification suite:

1. [**01 - System Architecture and Server Topology**](file:///D:/GitHub/Wonderland-Private-Server/docs/01_system_architecture_and_server_topology.md): Multi-server socket topology, dynamic portability engine (`PathHelper`), threading model, project graph, and graceful shutdown.
2. [**02 - Database Schema and Persistence Lifecycle**](file:///D:/GitHub/Wonderland-Private-Server/docs/02_database_schema_and_persistence.md): Centralized SQLite relational tables (`users`, `characters`, `character_inventory`, `character_equipment`, `character_pets`, `character_quests`), startup schema verification, and atomic transaction lifecycles.
3. [**03 - Network Protocol and Action Codes**](file:///D:/GitHub/Wonderland-Private-Server/docs/03_network_protocol_and_action_codes.md): Binary wire framing, `0x44F4` magic header, little-endian serialization (`Tools.FromFormat`), and exhaustive Action Code reference catalog.
4. [**04 - Data File Pipeline and Asset Parsing**](file:///D:/GitHub/Wonderland-Private-Server/docs/04_data_file_pipeline_and_asset_parsing.md): In-memory client asset decoders (`Npc.dat` XOR `0x5209` cipher, `Item.dat` 45-byte structs, `Talk.dat` 292-byte dialogues, `SceneData.dat`), `eve.Emg` bytecode engine, and `Formula.dat` coefficients.
5. [**05 - Map Engine and Entity Lifecycle**](file:///D:/GitHub/Wonderland-Private-Server/docs/05_map_engine_and_entity_lifecycle.md): Isometric coordinate systems, NPC roaming AI, 209-item ground harvesting loop, 14-byte `AC 22:4` scene table, concealment frame sequencing, and peer replication.
6. [**06 - Dialogue, Quest State Machine, and Cutscene Engine**](file:///D:/GitHub/Wonderland-Private-Server/docs/06_dialogue_quest_and_cutscene_engine.md): Modal dialogue boxes, 24-bit talk IDs, choice callbacks, quest state monotonic progression, PreEvent condition evaluation, and cinematic timelines.
7. [**07 - Gameplay Subsystems and Mechanics**](file:///D:/GitHub/Wonderland-Private-Server/docs/07_gameplay_subsystems_and_mechanics.md): 50-slot inventory, companion recruitment and amity, vehicle systems with 7-step raft shore shipwreck sequence, tent housing, and turn-based combat math with elemental wheel.
8. [**08 - GUI Administration Suite and GM Engine**](file:///D:/GitHub/Wonderland-Private-Server/docs/08_gui_administration_and_gm_engine.md): Windows Forms dashboard, 13 operational management tabs, deep character editor, and in-game GM chat command directory (`:heal`, `:level`, `:item`, `:warp`, etc.).
9. [**09 - Developer Tooling and Reverse Engineering**](file:///D:/GitHub/Wonderland-Private-Server/docs/09_developer_tooling_and_reverse_engineering.md): Asynchronous rotating diagnostic logs (`DebugSystem`), PCAP packet capture analysis, and Ghidra MCP reverse engineering bridge.
10. [**10 - Deployment Operations and Codebase Integrity**](file:///D:/GitHub/Wonderland-Private-Server/docs/10_deployment_operations_and_codebase_integrity.md): Prerequisites, zero-error build command, client configuration (`SERVER.INI`), and codebase deduplication audit.
11. [**11 - Formula.dat Specification and EXP Engine**](file:///D:/GitHub/Wonderland-Private-Server/docs/11_formula_dat_and_exp_engine.md): Complete 407-byte binary schema, IEEE-754 double precision coefficients, client `TCalculator` disassembly, normal/reborn/pet EXP formulas, combat monster EXP distribution, real-time GUI multiplier control ($0.1\times$ - $1000\times$), and SQLite persistence.

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
| `:petexp` | `:petexp <amount>` | Awards experience points to the active companion and synchronizes level/stats. |
| `:petlvl` / `:petlevel` | `:petlvl <1-199>` | Sets the active companion's level, recalculates stats, and persists. |
| `:unride` | `:unride` | Dismounts active vehicle or riding pet. |
| `:help` | `:help` | Displays command list and syntax help. |
