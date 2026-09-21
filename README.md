WLO Private Server CheatEngine

Discord invite link : http://discord.gg/J79ezkpzrT

Developing Tools: Visual Studio 2022

Database: sqlite bypass

- **100% Dynamic Portability**: Zero hardcoded absolute paths. Seamless path resolution across all workstations via `RCLibrary.Core.PathHelper`.
- **Dual Database Engine (SQLite & MySQL / MariaDB)**: Full GUI-configurable persistence supporting both embedded SQLite (`Data/ServerDataBase.db`) and remote/local MySQL servers. Includes non-blocking connection testing, live runtime switching across all active database instances, automatic database & schema auto-creation (`VerifySetup()`), self-healing incremental column migration (`GetColumnNames` / `AddColumnIfNotExists`) eliminating boot-time duplicate column errors, transparent dialect translation (`TranslateSqlForMySql`), and a built-in 1-click SQLite-to-MySQL data migration engine with real-time progress reporting.
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
- **Modernized User-Friendly GUI Administration Suite**: Ergonomic operator interface with two-tier horizontal navigation (Level 1: 5 primary categories; Level 2: dynamic sub-module pill strip preserving 100% full screen width for grids and maps), persistent executive header with live telemetry (player count, uptime, RAM, ports), quick jump search (`Ctrl+K`), one-click client launcher (`F5`), database-backed MOTD and server name persistence (`server_settings` relational synchronization across reboots and migrations with automatic boot-time loading and auto-save on blur, Enter key, or manual save), safe shutdown with synchronized countdown, diagnostic console toolbar (clear, copy, auto-scroll toggle, search, export), debounced cheat list search, and modular `ModernTheme` design system with universal Segoe UI / double-buffered DataGrid theming.
- **Dynamic Ground Items Lifecycle (`AC 23`)**: Automatic map loading of native terrain resources from `Eve.emg` `ItemAreas` (209 items across 77 maps). Real-time authentic batched spawning (`AC 23:4`), terrain slot pickups (`AC 23:2`), gold item banner acquisition popups (`AC 23:6`), and asynchronous heartbeat respawning.
- **Robust NPC Spatial AI & Wander Boundaries**: Authentic signed bounding-box roaming (`WalkBehavior == 3`), waypoint patrol oscillation (`WalkBehavior == 2 / 5`), and static anchors (`WalkBehavior == 1`) preventing map boundary drift or corner teleports.
- **State-Verified Dialogue & Quest Safeguards**: Multi-event candidate evaluation prioritizing post-quest resolution over completed stages, multi-candidate event aggregation (direct clickID + linked NPC events) with strict quest/companion condition qualification without premature forced defaults, zero-op condition gate priority pass supporting dual item (`w1=1`) and companion (`w1=2`, active team, capacity, recruitment) condition decoding, Quest State Condition decoding, paired quest completion flags (`questId + 1`), persistent SQLite quest `step` tracking, and irreversible completion locks preventing quest demotion loops.
- **Authentic Starter Pack, 31-Byte Record Alignment, Chest Safeguards & Raft Wreck Protocol**: Complete 31-byte fixed record item serialization (`AC 23:5` / `AC 30:5`) and 21-byte equipment serialization (`AC 23:11`) preventing client parser desynchronization and guaranteeing full multi-item visibility across all 50 bag slots. Deduplicated single `AC 23:6` acquisition banner and `player.Quests` persistence check preventing chest double-granting. Client bag slot tracking (`MountedVehicleSlot`) with single `AC 23:9` slot clearing and authentic 7-step shore wrecking sequence (`AC 15:14 -> AC 23:9 -> AC 15:15 -> AC 15:11 -> AC 5:4`) preventing raft phantom icons.
- **Pre-Event NPC Visibility, Staged Quest Lifecycle & Universal Cutscene Dummy Suppression**: Comprehensive bidirectional multi-target PreEvent evaluation and per-player entity lifecycle tracking (`player.HiddenNpcClickIDs`). Authentic 14-byte `AC 22:4` entity table serialization utilizing State `0xFFFF`, EntityType Byte 8 (`2 = Hidden`), and Despawn Code `0x03E7FC18` (65,535,000 ms) invoking client `FUN_00432674` to set `*(actor + 0x1eec) = 2` and clear map collision grid via `FUN_0043d390`. Suppresses client actor rendering (`FUN_0043d58c`) and disables mouse cursor hit-testing (client lines 308016 & 308092). Dynamic runtime concealment (despawn) and reveal (spawn) transmits single-record `AC 22:4` frames (`[ClickID, 0xFFFF, X, Y, Type=2, Duration=0x03E7FC18, 0]` for despawn, `[ClickID, 0x00FF, X, Y, Type=1, Duration=0, 0]` for spawn). Evaluates `Eve.emg` 21-byte PreEvent condition bytecodes (Opcodes `0x01` unconditional, `0x02` companion recruitment mode 1 [recruited] / mode 2 [not recruited], `0x03` quest step/item check, `0x05` quest marks/steps) with multi-subentry compound rule grouping (AND logic across subentries with trailing zero-action conditions) and Action Opcode `0x02` (ActionType `2` conceal vs ActionType `3` reveal). Universally detects and eliminates 230 duplicate cutscene dummy actors across 108 maps by concealing zero-event puppets sharing Template IDs with primary dialogue NPCs on map entry. Enforces default concealment for all staged actors targeted by `ActionType 3` (reveal) rules until their matching quest conditions evaluate to true. Integrates declarative `SpawnNpcClickIDs` and `DespawnNpcClickIDs` into `QuestDefinition` and `QuestStep`, automatically synchronizes visibility in real time across `QuestManager` lifecycle operations (`AcceptQuest`, `AdvanceQuestStep`, `SetPlayerQuestState`, `CompleteQuest`, `ResetQuest`) and `EveEventInterpreter` opcodes (2, 3, 5, battle victory) without requiring map re-entry, and enforces server-side interaction guards in `AC20.Recv1` and `Map.ProcessInteraction`. Fully isolates staged quest NPCs and props on Map 12000, Map 12001 (Chief's House: staged cutscene Roca ClickID 3 concealed, static Roca ClickID 2 hidden upon recruitment), Map 11003 (Holy Village: Niss ClickID 9 visible, dummy puppets 38/39 concealed; Match Girl ClickID 11 visible, dummy puppet 18 concealed), and Map 11016 (S. Monkey visible to new players, concealed upon recruitment; Robinson re-issue and seasonal NPCs suppressed until active).
- **Interactive Map Props, Chests & Anti-Blinking State Synchronization (`AC 22:4`, `AC 22:1`, `AC 22:10`)**: Authentic 14-byte entity table synchronization with verified intact frame (`0x0000`) vs opened/broken frame (`0x0001`) for static props/chests and `0x00FF` for living actors, completely eliminating sprite frame cycling and visual blinking/flickering defects. Features per-player quest container isolation preventing one-time quest props from leaking shared broken state across players, unicast break animations for quest containers, and automatic intact frame restoration (`AC 22:1` Action 0) upon renewable gathering node respawn.
- **Companion Actor Despawning & Overworld Visibility Isolation**: Full dynamic despawning of recruited companion NPCs (Robinson, Roca, S. Monkey, Clive, Niss, etc.) using authentic `AC 22:4` concealment frames (`State = 0xFFFF`, `EntityType = 2`, `Duration = 0x03E7FC18`). Multi-tier resolution through `Player.IsSamePetOrCompanion`, `Npc.dat` canonical name resolution for generic Eve names (`" Npc"`), active pet verification, and quest flag safeguards (including Quest 12002 for S. Monkey) completely eliminating overworld companion duplicates (e.g. Robinson on Starter Beach Map 11016).
- **Cinematic Cutscene Timeline & Animation Pre-Dialogue Sequencing**: Authentic dispatch of cutscene animation opcodes (`AC 20:1 SubCode 1 Type 5 Cutscene ID 12008` for Rhode Island Beach arrival) with client ACK (`AC 20:6`) step synchronization, mobility restoration (`AC 20:8`, `AC 5:4`), and fail-safe timeouts ensuring cutscene animations play to completion before dialogue trees begin.
- **Graceful Shutdown & Diagnostic Countdown**: Non-immediate shutdown saves all player, inventory, and server data, displays the exact canonical log file location on the console, and executes a 10-second countdown before process exit.
- **Codebase Deduplication & Dead-Code Optimization**: System-wide cleanup purging 13 uncompiled legacy files, eliminating 1,850+ lines of dead commented code across combat, player, and action code subsystems, centralizing database table DDL checks to boot-time execution, extracting unified peer entity replication helpers in Map engine, and enforcing 0-error 0-warning compilation.
- **Zero-Error Login & Equipment Lifecycle (`AC 63`, `EquipManager`)**: Resilient character loading sequence (`CharacterDataBase.GetCharacterData`) featuring multi-tier `ItemDat` resolution (`CharacterDataBase.GlobalInstance`, `GameDataBase.GlobalInstance`), null-safe socket event dispatch (`Send?.Invoke(...)`), dynamic beginner outfit fallback (`EquipManager.SetBeginnerOutfit`) with automatic slot resolution preventing `NullReferenceException` on unequipped accounts, and culture-invariant numeric parsing (`CultureInfo.InvariantCulture`) guaranteeing valid drop rate serialization and clamping in administrative and battle subsystems.
- **Resilient Item Database & Starter Pack Stacking (`itemDat.wpdat`, `Inventory.AddItem`)**: Prioritized canonical decrypted `itemDat.wpdat` (4,776 validated items) over raw client `Item.dat` archives, ensuring authentic item types and stackability flags. Configured `Item.Stackable` to recognize stacked quantities (`Ammt > 1`) and consumables, eliminating inventory flooding where starter foods (e.g. Fugu Hot Pot / Spicy Hot Pot #32176 x50) erroneously occupied all 50 slots instead of consolidating into a single slot.

Running steps:

1. rhode island install : [drive.google.com/file/d/18z5H1w5G9GujMJywRHL-uOac4fFyOTSY](https://drive.google.com/file/d/18z5H1w5G9GujMJywRHL-uOac4fFyOTSY)
2. Ensure `SERVER.INI` in the client directory is set to `127.0.0.1`
3. Server DAT files in `./Data` are synchronized with the client data files (`Npc.dat`, `Item.dat`, `Skill.dat`, `Talk.dat`, `Eve.emg`, `Ground.MMG`, `SkillData.MBTM`, etc.).
   - Note: The large 1.42 GB sprite archive `odd.dat` can be downloaded directly from [Releases v1.0.0](https://github.com/Eminbalci/Wonderland-Private-Server/releases/tag/v1.0.0).
4. Run `Wonderland Private Server.exe` in `bin/Debug` & wait until log shows "Now listening for clients..."
5. Run `aLogin.exe`, select server and login with `gmone` / `gmone`

The server technical documentation is organized into a cohesive 16-document master specification suite:

1. [**01 - System Architecture and Server Topology**](file:///D:/GitHub/Wonderland-Private-Server/docs/01_system_architecture_and_server_topology.md): Multi-server socket topology, dynamic portability engine (`PathHelper`), threading model, project graph, and graceful shutdown.
2. [**02 - Database Schema and Persistence Lifecycle**](file:///D:/GitHub/Wonderland-Private-Server/docs/02_database_schema_and_persistence_lifecycle.md): Centralized SQLite and MySQL relational persistence, self-healing migration (`AddColumnIfNotExists`), automated schema verification (`VerifySetup`), and 1-click migration engine.
3. [**03 - Network Protocol and Action Codes**](file:///D:/GitHub/Wonderland-Private-Server/docs/03_network_protocol_and_action_codes.md): Binary wire framing, `0x44F4` magic header, XOR `0xAD` encryption, little-endian serialization (`Tools.FromFormat`), and exhaustive Action Code reference catalog (AC 0 to AC 226).
4. [**04 - Binary Asset Pipeline and Decoders**](file:///D:/GitHub/Wonderland-Private-Server/docs/04_binary_asset_pipeline_and_decoders.md): In-memory client asset decoders (`Npc.dat` XOR `0x5209`, `Item.dat` 45B, `Talk.dat` 292B, `Mark.dat` 526B, `SceneData.dat`, `Eve.emg`, `Formula.dat`, `Compound2.dat`).
5. [**05 - Map Engine, Spatial AI, and Scene Entity Lifecycle**](file:///D:/GitHub/Wonderland-Private-Server/docs/05_map_engine_spatial_ai_and_scene_lifecycle.md): Isometric coordinate systems, NPC roaming AI, 209-item ground harvesting loop, 14-byte `AC 22:4` scene table, concealment frame sequencing, anti-blinking prop state synchronization, and puppet suppression.
6. [**06 - Dialogue, Quest State Machine, and Cutscenes**](file:///D:/GitHub/Wonderland-Private-Server/docs/06_dialogue_quest_state_machine_and_cutscenes.md): Modal dialogue boxes, 24-bit talk IDs, choice callbacks, quest state monotonic progression, PreEvent condition evaluation, and cinematic timelines.
7. [**07 - Turn-Based Combat Engine and Battlefield**](file:///D:/GitHub/Wonderland-Private-Server/docs/07_turn_based_combat_engine_and_battlefield.md): 4v4 isometric battlefield grid, byte-for-byte `AC 11:5` fighter serialization, 30s turn loop, elemental wheel, knockouts, fair-play drops, and 7-step exit handshake.
8. [**08 - Companion Pet Lifecycle and Vehicles**](file:///D:/GitHub/Wonderland-Private-Server/docs/08_companion_pet_lifecycle_and_vehicles.md): Companion login sync (`AC 15:1`), skill unlocks, amity decay and permanent desertion (<20), rebirth ascension, overworld follower spawning, and 7-step raft shore shipwreck protocol.
9. [**09 - Tent Housing and Manufacturing System**](file:///D:/GitHub/Wonderland-Private-Server/docs/09_tent_housing_and_manufacturing_system.md): Instanced personal tents keyed by `CharID`, multi-tenant instance isolation, overworld multi-tent replication (`SendOpenTents`), tent security locking, visitor permission guards, overworld coordinate preservation (`TentReturnMap`), crash/disconnect safe DB persistence, non-destructive doorway exit (`AC 65:3`), portal 1 exit, occupant evacuation on tent close, interior furniture placement (`chartent_items`), 2nd floor expansions, and authentic `Compound2.dat` tent machine manufacturing (`AC 64`).
10. [**10 - Social Systems, Player Trade, Friends, and Chat**](file:///D:/GitHub/Wonderland-Private-Server/docs/10_social_systems_trade_friends_and_chat.md): 50-slot bag with 31-byte records, bilateral friend roster (`AC 14:5`) and removal (`AC 14:4`), 4-phase atomic player trade, and multi-channel chat moderation.
11. [**11 - GUI Administration Suite and GM Engine**](file:///D:/GitHub/Wonderland-Private-Server/docs/11_gui_administration_suite_and_gm_engine.md): Modernized Windows Forms dashboard with two-tier horizontal categorized navigation, 24 operational modules, executive telemetry header, database-backed MOTD persistence, and GM chat commands.
12. [**12 - Formula.dat Specification and Experience Engine**](file:///D:/GitHub/Wonderland-Private-Server/docs/12_formula_dat_specification_and_exp_engine.md): Complete 407-byte binary schema, IEEE-754 double precision coefficients, client `TCalculator` disassembly, normal/reborn/pet EXP formulas, combat monster EXP distribution, and real-time multiplier control.
13. [**13 - Quest Ecosystem, Census, and Flag Mapping**](file:///D:/GitHub/Wonderland-Private-Server/docs/13_quest_ecosystem_census_and_flag_mapping.md): Comprehensive census across `Mark.dat` and `Eve.emg`, explanation of community ~502 canonical journal quest count vs internal 1,027 master quests and 1,507 flag scripts, regional breakdowns, and pairing mechanics.
14. [**14 - Developer Tooling, Reverse Engineering, and Deployment**](file:///D:/GitHub/Wonderland-Private-Server/docs/14_developer_tooling_reverse_engineering_and_deployment.md): Prerequisites, zero-error build command, client configuration (`SERVER.INI`), asynchronous diagnostic logger (`DebugSystem`), PCAP analysis, Ghidra disassembly addresses, and bot testing framework.
15. [**15 - Gap Analysis, Missing Features, and Feature Roadmap**](file:///D:/GitHub/Wonderland-Private-Server/docs/15_gap_analysis_and_feature_roadmap.md): Protocol gap analysis, unhandled Eve script opcodes (15, 16, 17), anti-exploit security checklist, and phased 5-tier implementation roadmap.
16. [**16 - Interactive Props and Chest State Machine**](file:///D:/GitHub/Wonderland-Private-Server/docs/16_interactive_props_and_chest_state_machine.md): Prop sprite animation mechanics, elimination of prop blinking/flickering artifacts, PCAP wire capture verification, per-player chest completion persistence, and PreEvent/Eve interpreter visibility synchronization.

  > Maps : Teleports you to that ID
  >

  > Vehicle : Ride the vehicle
  >

  > Items : Adds the item to your inventory
  >

  > Npc : Battle/ride the NPC or Pet
  >
- Each lists have a search textbox on top of it:

  > Type in your search query and then hit Enter
  >

  > To reload all, blank the search then hit Enter
  >
- Press `F5` key anywhere in the GUI window to automatically launch `aLogin.exe`.
- Click the in-game PK button (sword icon) and click any monster/NPC to engage in turn-based combat. Supports attack, skills, defending, fleeing, XP/Gold rewards, and automatic battle exit.
- Real-time NPC movement and roaming (`AC 22 Sub 2`) ported from Python server with scripted waypoints and random wandering.
- Character skill unlocking system (`AC 5 Sub 11`, `AC 8 Sub 1`) with character-specific stunt skills, element skills, and `:skill <id> [grade]` chat command.
- Interactive Quest & Journal System (`AC 39`, `AC 52`, `charquest` DB table) supporting multi-stage NPC dialogues, item delivery verification, automatic reward distribution (Gold, EXP, Items, Companions), and quest battle encounters.
- In-Game GM Chat Commands:

## In-Game GM Chat Commands

Type commands into standard in-game chat to execute administrative operations:

| Command | Usage | Description |
| :--- | :--- | :--- |
| `:b` / `:broadcast` / `:notice` | `:broadcast <message>` | Broadcasts server-wide announcement with HUD notification. |
| `:kick` | `:kick <character> [reason]` | Disconnects player socket session from server. |
| `:summon` / `:bring` | `:summon <character>` | Warps target player to GM's current coordinates. |
| `:goto` / `:tp` | `:goto <character>` | Warps GM directly to target player's position. |
| `:warp` | `:warp <map_id> [x] [y]` | Teleports player to destination map and coordinates. |
| `:amity` / `:petamity` | `:amity [1-100]` | Sets active companion's loyalty/amity (default: 100). |
| `:rebirth` / `:petrebirth` | `:rebirth` | Triggers instant companion Rebirth Ascension. |
| `:allskills` / `:maxskills` | `:allskills [grade]` | Unlocks all element & stunt skills up to specified grade. |
| `:god` / `:godmode` | `:god` | Boosts base stats to 999 and full restores player and pet HP/SP. |
| `:winbattle` / `:killall` | `:winbattle` | Instantly completes active combat encounter in victory. |
| `:mute` | `:mute <char> [mins]` | Mutes target player's public chat messages. |
| `:unmute` | `:unmute <char>` | Restores player's public chat privileges. |
| `:online` / `:who` | `:online` | Lists online players, levels, and current map locations. |
| `:tent` | `:tent` | Injects authentic Tent item (ID 34001) into inventory. |
| `:reload` | `:reload [all\|quests\|mall\|drops]` | Hot-reloads server game tables without rebooting. |
| `:heal` / `:full` | `:heal [hp] [sp]` | Restores character HP and SP to maximum (or specified values). |
| `:level` / `:lvl` | `:level <1-200>` | Sets character level and recalculates derived base stats. |
| `:points` / `:sp` | `:points <amount>` | Grants unallocated attribute stat points. |
| `:gold` / `:money` | `:gold <amount>` | Updates character wallet gold balance. |
| `:exp` | `:exp <amount>` | Sets total character experience points. |
| `:stats` / `:stat` | `:stat <str> <con> <int> <wis> <agi>` | Distributes character base attribute points. |
| `:item` | `:item [add] <id> [count]` | Injects item(s) directly into character inventory. |
| `:skill` | `:skill <id> [grade]` | Unlocks or levels up a specific skill ID. |
| `:buy` | `:buy <query/id> [quantity]` | Buys or spawns item from Item Mall or Item.dat. |
| `:pet` | `:pet <pet_id>` | Spawns and recruits specified companion into party. |
| `:petexp` | `:petexp <amount>` | Awards experience points to the active companion and synchronizes level/stats. |
| `:petlvl` / `:petlevel` | `:petlvl <1-199>` | Sets the active companion's level, recalculates stats, and persists. |
| `:carnie` | `:carnie` | Warps character to Carnie mini-game park (Map 11094). |
| `:unride` | `:unride` | Dismounts active vehicle or riding pet. |
| `:invis` / `:ghost` | `:invis` | Toggles GM Ghost Mode (invisibility; hides from map broadcasts). |
| `:restat` / `:resetstats` | `:restat [char]` | Resets attributes to 10 and refunds all invested stat points. |
| `:clearskills` | `:clearskills [char]` | Clears learned progression skills in memory and database. |
| `:repair` / `:fixall` | `:repair [char]` | Restores durability (Damage = 0) on all gear and bag items. |
| `:im` / `:mallpoints` | `:im <amount> [char]` | Grants Item Mall currency points to account balance. |
| `:town` | `:town <name>` | Warps player to 16 canonical town locations. |
| `:jail` / `:unjail` | `:jail <char> [mins]` | Incarcerates or releases players from Jail cell. |
| `:summonall` | `:summonall` | Warps all online players to GM's current position (Event). |
| `:kickall` | `:kickall [reason]` | Mass-disconnects all non-GM players for maintenance. |
| `:battle` / `:fight` | `:battle <mob_tid>` | Spawns a test combat encounter against the specified mob ID. |
| `:info` / `:whois` | `:info [char]` | Formats comprehensive character diagnostic summary. |
| `:droprate` | `:droprate <rate>` | Sets global monster loot drop multiplier (0.1x to 100.0x). |
| `:shutdown` | `:shutdown [secs]` | Initiates graceful countdown shutdown with server alert. |
| `:help` | `:help` | Displays categorized command list and syntax help. |

## Desktop GM Studio (`tabGm`)

The server console includes an integrated 5-panel **GM Studio**:
1. **GM Authorization & Access Control:** Database-backed GM permissions (`gm_accounts`) with instant promotion of online players and live counter badges.
2. **Online Player Live Control & Extended Cheats:** Real-time player selector, Full Heal, Gold (+1M), Stat Points (+100), Level adjustment, Max Companion Amity, Companion Rebirth Ascension, Full Element Skill Tree Unlocking, Teleport to Player, Summon to GM, Socket Kick, Chat Muting, Restat Point Refund, Gear Repair, +1,000 IM Points, Clear Skills, and Character Diagnostic Summary.
3. **Server Operations & Maintenance:** Server-wide broadcast notice dispatcher with 4 color channels, Live Global EXP Multiplier, Hot-Reload of Quests, Item Mall, Monster Drops, and GM accounts, one-click Companion Spawner for 13 iconic companions, Global Monster Drop Multiplier (0.1x - 100.0x), Mass Non-GM Kick, Graceful 10s Countdown Shutdown, and Map Ground Item Cleanup.
4. **Spatial Teleportation & World Warps Studio:** 16 Town Presets (Welling, Kelan, Holy, Kyoto, Chang'an, Rome, Maya, Inca, Bangkok, South Pole, Ghost Isle, Carnie Park, Pirate Base, Kaohsiung, Jail), Custom Coordinate Warp, Mass Event Summon All, Jail/Unjail, Ghost Mode Invisibility Toggle, and Combat Encounter Simulator with instant kill/win button.
5. **Live Player Inventory & Equipment Inspector:** 50-Slot Player Bag DataGridView, 6-Slot Equipped Gear DataGridView with slot names, Item Injector with ID and Count, Delete Selected Bag Item, Clear Bag, and Repair All Gear.

