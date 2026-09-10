# Wonderland Online Private Server & CheatEngine

A high-performance, modular private server emulator and cheat/administration engine for **Wonderland Online (WLO)** written in C# (.NET Framework 4.6.2). Engineered for **100% dynamic portability**, allowing zero-configuration cloning, building, and running across any Windows machine or drive.

---

## Key Features

- **100% Dynamic Portability**: Zero hardcoded absolute paths. Seamless path resolution across all workstations via `RCLibrary.Core.PathHelper`.
- **Self-Healing SQLite Database**: Consolidated single database in `Data/ServerDataBase.db` with 32 relational tables. Automatically verifies schemas, applies unique indexes, and seeds missing default data upon every boot (`GameDataBase.VerifySetup()`).
- **Tri-Server Socket Architecture**:
  - **Login Server (Port 6414)**: Authentication, account management, character selection/creation, Item Mall catalog sync (`AC 75`).
  - **World Server (Port 6415)**: Overworld replication, tile pathfinding, turn-based combat engine, quests, dialogue engine, housing.
  - **Status Service (Port 6416)**: Server cluster load indicator (Green/Yellow/Red/Auto), rates, player count broadcasting.
- **Complete 88 Action Code Protocol (AC 1 - AC 186)**: Full support for turn-based battles (`AC 11`/`AC 50`/`AC 51`), vehicle/mount system (`AC 15`), tent housing (`AC 12`), quests (`AC 39`/`AC 52`), and cinematic cutscenes (`AC 186`).
- **Authentic Asset Parsing**: In-memory decryption and caching of `Npc.dat` (4,928 NPCs via XOR `0x5209`), `Item.dat`, `Skill.dat`, `Talk.dat`, `Mark.dat`, and `Eve.emg` event scripts.
- **Comprehensive GUI Management Suite**: Live player monitoring, map inspector, NPC/Mob editor, quest manager, Item Mall studio, chest drop editor, and firewall security center.
- **Graceful Shutdown & Diagnostic Countdown**: Non-immediate shutdown saves all player, inventory, and server data, displays the exact canonical log file location on the console, and executes a 10-second countdown with second-by-second updates before exit.

---

## Technical Documentation (`docs/`)

The documentation is organized into 10 authoritative master technical specifications:

1. [**01 - Architecture and Core Engine**](file:///D:/GitHub/Wonderland-Private-Server/docs/01_architecture_and_core_engine.md): Multi-server socket topology, dynamic pathing, threading model, and project hierarchy.
2. [**02 - Database Persistence and Schema Specification**](file:///D:/GitHub/Wonderland-Private-Server/docs/02_database_persistence_and_schema.md): Complete 32-table SQLite schema catalog, automated startup verification, seeders, and character persistence lifecycle.
3. [**03 - Network Protocol and Action Codes**](file:///D:/GitHub/Wonderland-Private-Server/docs/03_network_protocol_and_action_codes.md): Binary wire framing, little-endian packing, and exhaustive AC 1 through AC 186 opcode catalog with packet layouts.
4. [**04 - Data File Pipeline and EVE Scripting Engine**](file:///D:/GitHub/Wonderland-Private-Server/docs/04_data_files_and_eve_engine.md): Client binary decoders (`Npc.dat` XOR cipher, `Item.dat`, `Skill.dat`, `Talk.dat`), `Eve.emg` bytecode interpreter, and spawn CSV pipelines.
5. [**05 - Gameplay Mechanics and Subsystems**](file:///D:/GitHub/Wonderland-Private-Server/docs/05_gameplay_mechanics_and_subsystems.md): Turn-based combat math, elemental wheel, companion/pet intimacy, tent crafting, guilds, marriage, mail, and item mall.
6. [**06 - GUI Administration Suite and GM Engine**](file:///D:/GitHub/Wonderland-Private-Server/docs/06_gui_administration_and_gm_engine.md): Windows Forms dashboard, management tabs, in-game GM chat command catalog, and IP/account security center.
7. [**07 - Deployment and Operational Runbook**](file:///D:/GitHub/Wonderland-Private-Server/docs/07_deployment_and_operational_runbook.md): Portable setup guide, client synchronization, port mapping, compilation instructions, and troubleshooting.
8. [**08 - English Localization and Internationalization**](file:///D:/GitHub/Wonderland-Private-Server/docs/08_localization_and_internationalization.md): Complete audit and dictionary of English GUI controls, chat broadcast channels, and network action messages.
9. [**09 - Logging Subsystem and Diagnostic Timestamps**](file:///D:/GitHub/Wonderland-Private-Server/docs/09_logging_subsystem_and_diagnostics.md): Canonical `[yyyy-MM-dd HH:mm:ss]` timestamping, thread-safe UI async dispatch, and persistent disk logging.
10. [**10 - Dialogue and Quest Engine**](file:///D:/GitHub/Wonderland-Private-Server/docs/10_dialogue_and_quest_engine.md): Wire framing, 24-bit talk IDs, dialogue queues, choice callback delegates, and quest state progression.

---

## Quick Start Guide

### 1. Prerequisites
- Windows 10 / 11 (64-bit)
- .NET Framework 4.6.2 or later
- Visual Studio 2022 or .NET SDK (`dotnet build`)
- Wonderland Online Client (e.g., [Rhode Island Client](https://drive.google.com/file/d/18z5H1w5G9GujMJywRHL-uOac4fFyOTSY))
  - Note: Large 1.42 GB client sprite archive `odd.dat` is available on [Releases v1.0.0](https://github.com/Eminbalci/Wonderland-Private-Server/releases/tag/v1.0.0).

### 2. Build the Server
```powershell
dotnet build "Wonderland Private Server.sln"
```

### 3. Launch Server & Client
1. Ensure client `SERVER.INI` points to `127.0.0.1`.
2. Run `bin/Debug/Wonderland Private Server.exe`.
3. Wait until the dashboard displays green operational status.
4. Press `F5` in the server window (or run `aLogin.exe` in the client directory).
5. Log in with default GM accounts:
   - User: `admin` / Password: `password` (GM Level 10)
   - User: `developer` / Password: `password` (GM Level 10)

---

## Administration & GM Chat Commands

Type commands into standard in-game chat to execute administrative operations:

| Command | Usage | Description |
| :--- | :--- | :--- |
| `:heal` | `:heal [hp] [sp]` | Restores character HP and SP to maximum (or specified values). |
| `:level` | `:level <1-200>` | Sets character level and recalculates derived base stats. |
| `:gold` | `:gold <amount>` | Updates character gold balance. |
| `:stat` | `:stat <str> <con> <int> <wis> <agi>` | Distributes character base attribute points. |
| `:item` | `:item <id> [count]` | Injects item(s) directly into character inventory. |
| `:skill` | `:skill <id> [grade]` | Unlocks or levels up a specific skill ID. |
| `:warp` | `:warp <map_id> <x> <y>` | Teleports player and followers to map coordinates. |
| `:speed` | `:speed <multiplier>` | Sets player movement speed multiplier. |
| `:im` | `:im <points> [bonus]` | Grants Item Mall Points and Bonus Points. |
| `:help` | `:help` | Displays command list and syntax help. |

---

## Cheat & Developer Tab Features

In the server GUI window, switch to the **"Cheat"** tab for developer debugging:
- **Double-Click Actions**:
  - **Maps**: Instantly teleports your online player to that map ID.
  - **Vehicle**: Spawns and mounts the selected vehicle/mount.
  - **Items**: Injects the selected item into your inventory.
  - **Npc**: Initiates instant combat with or mounts the selected NPC/Pet.
- **Search Filtering**:
  - Type search query and press Enter to filter items/NPCs.
  - Clear the search box and press Enter to reset and reload the full catalog.
