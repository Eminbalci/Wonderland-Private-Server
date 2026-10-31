# Wonderland Online Private Server & Emulation Framework















A high-performance, reverse-engineered Wonderland Online (WLO) server emulator and administration suite built in C# targeting .NET Framework 4.6.2 (`net462`). Features multi-threaded socket servers, byte-for-byte protocol synchronization with official client binaries (`aLogin.exe` and `Main.exe`), dual-database persistence (SQLite and MySQL/MariaDB), native `Eve.emg` event script interpretation, turn-based isometric combat, and an extensive operator administration dashboard.















Discord invite link : http://discord.gg/J79ezkpzrT























---















## 1. System Architecture & Core Topology















The server architecture is structured into decoupled network, data, and gameplay subsystems:















* **Tri-Server Socket Architecture:**







  * **Login Server (Port 6414):** Handles account authentication, security validation, character enumeration, creation, deletion, and Item Mall catalog synchronization ([`LoginServer.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Server/LoginServer.cs)).







  * **World Server (Port 6415):** Drives overworld entity replication, tile pathfinding, turn-based combat encounters, interactive dialogues, quest state progression, companion pet lifecycles, and tent housing ([`WorldServer.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Server/WorldServer.cs)).







  * **Item Mall Server (Port 6416):** Manages microtransaction catalogs, point balances, bonus points, and promotional currency transactions ([`ItemMallManager.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/ItemMallManager.cs)).







  * **Registration REST API (Port 8080):** Lightweight HTTP service for account provisioning and web registrations.







* **100% Dynamic Portability:** Zero hardcoded paths. All asset lookups, database locations, and client binaries resolve dynamically at runtime via [`PathHelper.cs`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/Core/PathHelper.cs).







* **Dual Database Engine (SQLite & MySQL / MariaDB):**







  * Embedded SQLite persistence via [`ServerDataBase.db`](file:///D:/GitHub/Wonderland-Private-Server/Data/ServerDataBase.db) requiring zero external dependencies.







  * Native MySQL/MariaDB integration with live runtime switching, non-blocking connection verification, dialect translation via [`TranslateSqlForMySql`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/DataBase.cs), self-healing column migrations (`AddColumnIfNotExists`), and a built-in 1-click SQLite-to-MySQL data migration engine with progress tracking ([`DatabaseMigrationEngine.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/DatabaseMigrationEngine.cs)).







* **Binary Asset Pipeline & Decoders:** Direct in-memory parsing and caching of official binary assets:







  * `Npc.dat` (4,928 authentic NPC records decoded via XOR `0x5209` in [`PhxNpcDat.cs`](file:///D:/GitHub/Wonderland-Private-Server/PhoenixData/DataFiles/PhxNpcDat.cs)).







  * `itemDat.wpdat` (4,818 authentic 47-byte item records decoded in [`PhxItemDat.cs`](file:///D:/GitHub/Wonderland-Private-Server/PhoenixData/DataFiles/PhxItemDat.cs)).







  * `Skill.dat` and `SkillData.MBTM` (character and companion abilities in [`PhxSkillDat.cs`](file:///D:/GitHub/Wonderland-Private-Server/PhoenixData/DataFiles/PhxSkillDat.cs)).







  * `Talk.dat` (17,494 dialogue text entries in [`PhxTalkDat.cs`](file:///D:/GitHub/Wonderland-Private-Server/PhoenixData/DataFiles/PhxTalkDat.cs)).







  * `Mark.dat` (quest journal and task tracking definitions in [`PhxMarkDat.cs`](file:///D:/GitHub/Wonderland-Private-Server/PhoenixData/DataFiles/PhxMarkDat.cs)).







  * `Eve.emg` (1,119 maps, 10,644 event scripts, 1,412 PreEvents, 8,181 native NPCs, and 2,791 warps parsed in [`EveLoader.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataFiles/EveLoader.cs)).







  * `Formula.dat` (407-byte IEEE-754 double precision EXP progression curves in [`FormulaDat.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataFiles/FormulaDat.cs)).







  * `Compound2.dat` (tent manufacturing recipes and synthesis formulas).















---















## 2. Core Gameplay Engines & Protocol Subsystems















### 2.1 Action Code Protocol Engine (`AC 0` - `AC 226`)







Authentic packet-level framing with magic header `0x44F4`, XOR `0xAD` encryption, and little-endian wire serialization via [`Tools.FromFormat`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/Core/Tools.cs) and [`SendPacket.cs`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/Network/SendPacket.cs). Covers all official opcode channels across [`Src/Network/ActionCodes/`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/).







* **Comprehensive Packet & Animation Logging:** Real-time capture of all incoming (`RECV`) and outgoing (`SEND`) packets with hex dumps, length metrics, and descriptive classification for cutscenes (`AC 20:1`), emoticons (`AC 20:3`), animations (`AC 20:7`, `AC 23:2`), UI restore (`AC 20:8`), and CG movies (`AC 186:12`). Toggleable live via the Server Diagnostic Console button (`Packet Debug: ON/OFF`) and in-game chat commands (`:packetdebug on/off`). See technical details in [`packet_debugging_and_animation_logging.md`](file:///D:/GitHub/Wonderland-Private-Server/docs/packet_debugging_and_animation_logging.md).















### 2.2 Turn-Based Combat Engine (`AC 11`, `AC 50`, `AC 51`, `AC 52`, `AC 53`)







* **Battlefield Grid:** 4v4 isometric combat arena managed by [`PvEBattleManager.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Battle/PvEBattleManager.cs) and [`Battle.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Battle/Battle.cs).







* **Fighter Serialization:** Byte-for-byte alignment with official network captures in `AC 11:5` and `AC 11:250` (Byte 26 = Level, Byte 27 = Element, preventing the "Lv. 0" rendering defect).







* **Turn Lifecycle:** 30-second turn timer prompting player input via `AC 52:1`, move acknowledgments via `AC 53:5`, active turn replay focus via `AC 50:6`, 19-byte `AC 50:1` animation records, knockouts via `AC 53:3`, and real-time attribute updates via `AC 51:1`.

* **AOE Skill Targeting & Multi-Target Dispatch Engine:** Full geometry resolution for skill patterns 1 through 8 (single, column, row, cross/T-shape, 5-target, 6-target, full team) across 2x4 combat grids with grade-based pattern scaling (Grade 1..10), simultaneous multi-target packet dispatch via AC 50:1, elemental/critical damage calculation, multi-target status effects, and HP/death state synchronization. Detailed in [54_aoe_skill_targeting_and_battle_dispatch_engine.md](file:///D:/GitHub/Wonderland-Private-Server/docs/54_aoe_skill_targeting_and_battle_dispatch_engine.md).







* **Exit Protocol:** Authentic 7-step exit handshake (`AC 11:12` -> 4-byte pet `AC 11:1` -> 6-byte player `AC 11:0` -> 5-byte player `AC 11:1`).







* **Loot & Drop Engine:** MMO-accurate drop calculation capped at 1 item drop per monster kill with inventory capacity validation and bag cleanup commands.















### 2.3 Companion Pet & Mount Lifecycle (`AC 15`, `AC 19`, `AC 8:2`)







* **Roster Synchronization:** Official packet-level companion roster synchronization on login (`AC 15:1`) and skill unlocks (`AC 8:2` Stat `0x016F` with TargetType `0x04`).







* **In-Battle Synchronization:** Real-time SP consumption (`AC 8:2` Stat `0x011A`), skill proficiency EXP (`AC 8:2` Stat `0x016F`), real-time HP updates on damage/heal/revive (`AC 8:2` Stat `0x0119`), post-battle level-up progression, and combat EXP distribution (`AC 8:2` Stat `0x0124`).







* **Overworld Follower Replication:** Authentic follower spawning (`AC 15:4` with 8-byte equipment padding) broadcast to map peers without leaking private roster packets, standby/rest despawning (`AC 19:7` followed by `AC 19:2`), active combat designation (`AC 19:4` / `AC 19:1`), and mount/dismount replication (`AC 15:11` -> `AC 15:16`, `AC 15:12` -> `AC 15:17`).







* **Amity & Rebirth:** Amity decay on defeat, desertion below 20 amity, and instant Rebirth Ascension mechanics.















### 2.4 Narrative Cutscene, Dialogue & Quest State Machine (`AC 20`, `AC 186`, `AC 15:20`)







* **Bytecode Interpretation:** Direct execution of `Eve.emg` event opcodes via [`EveEventInterpreter.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs) and [`EveEventRuntime.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventRuntime.cs).







* **Story Album CG Cutscenes & Staging:** Orchestrated in-engine staging scripts (`.sty`), CG photo illustrations (`pic/jRole.JMg`), and Story Album CG unlocks (`AC 15:20` Star ID registration) for major milestones and companion recruitment (e.g. S.Monkey Cutscene 12000 CPR revival followed by Cutscene 11011 `JRole_5_3.jpg` full-screen illustration and Star 29 unlock). Detailed in [`quest_animations_and_cutscene_engine_architecture.md`](file:///D:/GitHub/Wonderland-Private-Server/docs/quest_animations_and_cutscene_engine_architecture.md).







* **PreEvent Evaluation & Dummy Suppression:** Bidirectional PreEvent condition evaluation in [`PreEventInterpreter.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/QuestRelated/PreEventInterpreter.cs) using 14-byte `AC 22:4` entity frames (`State = 0xFFFF`, `Type = 2`, `Duration = 0x03E7FC18`), universally concealing 230 duplicate cutscene dummy puppets across 108 maps on map load.







* **Cinematic Timeline Synchronization:** Dual playback handshakes (`AC 186:9` / `AC 186:12` and `AC 20:1 Type 5`), scene gate unlocks (`AC 5:4`), and mobile posture restoration (`AC 20:8`, `AC 6:2 [0]`).







* **Quest State Monotonicity:** Acyclic state tracking in `character_quests` (`NotStarted = 2`, `InProgress = 1`, `Completed = 3`) preventing quest demotion loops, paired completion flags (`questId + 1`), and multi-subentry chained state transitions.















### 2.5 Tent Housing, Virtual Maps & Manufacturing (`AC 12`, `AC 64`, `AC 65`, `AC 20:8`)







* **Virtual Map Routing:** Instanced personal tents keyed by `MapID = 100000 + CharID` with dual dictionary indexing in [`Map.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs) and [`Tent.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Tent/Tent.cs).







* **Stream Pointer Alignment:** Explicit stream pointer alignment (`p.SetPtr(6)`) in `AC 65:1` to resolve 32-bit owner CharIDs.







* **Security & Evacuation:** Locking guards preventing unauthorized visitors, overworld coordinate preservation (`TentReturnMap`), safe disconnect recovery, and occupant evacuation on tent closure.







* **Exit Lifecycle & Opcode Alignment:** Dual exit dispatching supporting both doorway clicks (`AC 65:11` / `AC 65:3`) and portal stepping (`AC 20:8` portal 1) restoring overworld entry position (`TentReturnMap`), spawn proximity guard bypass on interior rugs, and complete isolation from world map `EveDat` script collisions.







* **Interior Manufacturing & Housing Furniture Lifecycle:** Provisioning of default starter furniture: Resource Recycling / Trash Can (`38055`) at `(34, 42)` and Work Platform (`38049`) at `(37, 42)` inside client Floor 0 boundary rectangle `[Left: 29, Top: 38, Right: 44, Bottom: 50]` (`FUN_003e5ccc` / `FUN_003e6b78`) in [`Tent.InitializeDefaultItems()`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Tent/Tent.cs). Native Housing Manager (`*(DAT_004c9900 + 0x51b4)`) protocol alignment via `AC 62:4` (`FUN_003fd644`), 57-byte entity serialization (`FUN_003fb18c`), 1-based slot indexing ($1 \le \text{Index} \le 130$), dynamic furniture placement (`AC 62:1`), atomic furniture retrieval / pickup into bag (`AC 62:2`), furniture repositioning and rotation (`AC 62:3`), interior floor/wall texture streaming (`AC 62:14` / `AC 62:15`), dual-phase idempotent synchronization in `SendMapInfo` and `AC 12:1`, and SQLite persistence in `chartent_items` backed by [`CharacterDataBase.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/CharacterDataBase.cs) and [`AC62.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC62.cs).















### 2.6 NPC Vendors & Trade Systems (`AC 27`, `AC 35:12`, `AC 56`)







* **Catalog Resolution:** Dynamic NPC merchant catalog resolution in [`ShopKeeper.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/ShopKeeper.cs) dispatching `AC 35:12` with authentic catalog IDs (`0x0001FB83`..`0x0001FB85`).







* **Procurement Flow:** Clean separation of procurement (`Form_EquipStore` / `Form_ThingStore`) from liquidation (`Form_Sale`), shopping cart validation via `AC 27:1` in [`ShoppingCart.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/ShoppingCart.cs), authentic BuyPrice lookup from `Data/Item.dat` (offset `+0x72` compiled to `Data/npc_buy_prices.csv`) ensuring exact client-displayed gold deduction (e.g. Bread = 10 Gold, Rice Wine = 50 Gold), gold balance verification, bag capacity checks, and atomic item delivery in [`Inventory.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Inventory.cs).







* **Liquidation Protocol:** Support for authentic client liquidation modal panels (`Form_SaleEquip_1` via `AC 27:4` for mode 0; `Form_SaleThing_1` via `AC 27:3` for mode 1), multi-item batch liquidation parsing from `AC 27:2` payload `[slot_1, ..., slot_N, mode]`, category validation (`Equippos` 1..6 vs commodities), authentic liquidation price lookup (`NpcSalePrices`), gold payout up to 999,999 gold cap, stack removal, and server ACK response (`AC 27:2`).







* **Dialogue Interception & Liquidation Routing:** Two-tier dialogue intercept in [`EveEventRuntime.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventRuntime.cs) (`TryHandleMerchantChoice`) handling secondary prompts (Questions 6, 8, 2) when unauthored in `Eve.emg`, routing Choice 30 (`0x1E`, Buy) to catalog `AC 35:12` and Choice 31 (`0x1F`, Sell) to `player.OpenNpcSale()`. Opcode 7 liquidation routing in [`EveEventInterpreter.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs) intercepting `w2=31` single-merchant branches to bypass buy catalog generation. Clean modal termination sequencing emitting `finish()` dialogue clearance (`AC 20:8`, `AC 5:4`, `AC 6:2 [0]`) before `AC 27:3`/`AC 27:4`, and responding to shop closure `AC 20:6` with `AC 20:8`, `AC 5:4`, and `AC 6:2 [0]` to reset client-side interaction gate `[GameState + 0x7108]` to `0`, ensuring unhindered exploration, NPC interaction, and portal transitions post-shopping, backed by movement cleanup safeguards in `AC 06:1`.















### 2.7 Pet Hotel & Companion Storage (`AC 31`)







* **Storage Concurrency:** Storage manager lock flags in `AC 31:8` preventing double-click race conditions.







* **Modal Unlock:** Modal input lock clearance via `AC 31:12` resetting `[Form_PetHotel + 0x174]` in client memory.







* **Withdrawal Memory Sequencing:** Safe entity transfer copying companion data via `AC 31:5` prior to emitting `AC 31:4` slot clearance.















### 2.8 Inventory Item Double-Click Dispatcher, Delta Synchronization & 2D Grid Engine (`AC 23:15`, `AC 23:5`, `AC 23:1`)







* **Universal Dispatcher:** Centralized item usage router in [`AC23.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC23.cs) handling equipment (`WearEQ`), vehicles/mounts and Bamboo Dragonfly (`34169`), Quest Notebook (`32000`), combat auto-battle Remote Control (`34058` / `AC 29:6`), Return Scrolls (`30001`, `34163`), and EXP Potion consumption.







* **Additive Synchronization Safeguard:** Elimination of redundant full-bag `GetAC23_5()` resends during vehicle movement wear (`ApplyVehicleWear`) and alchemy storage transfers, preventing additive quantity inflation on native clients.







* **2D Multi-Cell Inventory Grid Engine:** Authentic 5-column $\times$ 10-row bag spatial topology in [`Inventory.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Inventory.cs) driven by binary `Item.dat` 451-byte record alignment in [`PhxItemDat.cs`](file:///D:/GitHub/Wonderland-Private-Server/PhoenixData/DataFiles/PhxItemDat.cs) (`cellwidth` at offset 406, `cellheight` at offset 407). Multi-cell spatial collision detection, boundary clipping protection (blocking row/column wrap), master/slave slot locking (`Parent` assignment and `isLocked = true`), slave unlock and cleanup upon drag or removal, login grid reconstruction (`RebuildOccupancyGrid`), and exact 2D bag capacity evaluation (`CanAddItems`, `FilledCount`, and `unFilledCount`).















### 2.9 Experimental Systems Suite (23 Dedicated Gameplay Engines)







Modular feature suite located in [`Game.Experimental`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Experimental/) with real-time controls in the GUI cockpit:







* **Wave 1:** Instance Dungeons (`AC 85`), Ranked PvP Elo Arena (`AC 183`/`AC 186`), Guild Siege Warfare (`AC 184`/`AC 92`), Carnival Minigames (`AC 57`), Seasonal Festival Races (`AC 226`), Dungeon Mechanism Levers, Companion Talent Trees (`AC 199`).







* **Wave 2:** Community Bulletin (`AC 82`), Church Charity & Karma (`AC 76`), Equipment Durability & Forge (`AC 40`), Gobang Gomoku Chess (`AC 41`), Fishing & Aquaculture (`AC 90`), Hot Spring Bathing (`AC 44`), Secondary PIN Vault (`AC 24`), Player Stall Bazaar (`AC 53`/`AC 94`), Dynamic Weather (`AC 88`), Vehicle Fleet Transport (`AC 84`/`AC 86`).







* **Wave 3:** ATM Banking & Wires (`AC 26`), Player Titles & Achievements (`AC 40`), Geological Treasure Excavation (`AC 90`), Carnival Fortune Wheel & Capsule Machines (`AC 76`), Tent Housekeeper Maid & Butler (`AC 62`), Tactical Combat Status Conditions & CC (`AC 114`).















---















## 3. Quick Start & Installation Guide















### 3.1 Prerequisites







* Windows 10 / 11 or Windows Server 2016+







* Microsoft Visual Studio 2022 (Community, Professional, or Enterprise) with `.NET desktop development` workload, or `.NET Framework 4.6.2 Developer Pack`







* .NET SDK (`dotnet` CLI)







* Wonderland Online client distribution (Rhode Island / English / Traditional Chinese client assets)















### 3.2 Setup Steps







1. **Clone the Repository:**







   ```bash







   git clone https://github.com/Eminbalci/Wonderland-Private-Server.git







   cd Wonderland-Private-Server







   ```







2. **Synchronize Client Assets:**







   * Place client data archives in `./Data`: `Npc.dat`, `itemDat.wpdat`, `Skill.dat`, `SkillData.MBTM`, `Talk.dat`, `Mark.dat`, `SceneData.dat`, `eve.Emg`, `Ground.MMG`, `Formula.dat`, `Compound2.dat`.







   * The 1.42 GB sprite archive `odd.dat` can be downloaded from [Releases v1.0.0](https://github.com/Eminbalci/Wonderland-Private-Server/releases/tag/v1.0.0).







3. **Configure Client Server Connection:**







   * Open `SERVER.INI` in the client directory and configure local connection:







     ```ini







     [Server]







     IP=127.0.0.1







     Port=6414







     ```







4. **Compile the Server:**







   ```powershell







   dotnet build "Wonderland Private Server.csproj" -c Debug







   ```







5. **Launch the Server:**







   * Run `bin/Debug/Wonderland Private Server.exe`.







   * Wait until the console outputs: `Now listening for clients...`







6. **Launch the Client & Connect:**







   * Press `F5` in the server GUI window (or launch `aLogin.exe` manually).







   * Select the configured server and authenticate using the default administrator credentials:







     * **Username:** `gmone`







     * **Password:** `gmone`















---















## 4. In-Game GM Chat Commands















Type commands into standard in-game chat to execute administrative and debugging operations:















| Command | Syntax | Description |







| :--- | :--- | :--- |







| `:b` / `:broadcast` | `:broadcast <message>` | Broadcasts a server-wide announcement with HUD notification banner. |







| `:kick` | `:kick <character> [reason]` | Forcibly terminates the target player's socket session. |







| `:summon` / `:bring` | `:summon <character>` | Warps target player to the GM's current map coordinates. |







| `:goto` / `:tp` | `:goto <character>` | Warps the GM directly to the target player's position. |







| `:warp` | `:warp <map_id> [x] [y]` | Teleports player to destination map and coordinates. |







| `:town` | `:town <name>` | Warps player to one of 16 canonical town locations. |







| `:amity` / `:petamity` | `:amity [1-100]` | Sets active companion's loyalty/amity value (default: 100). |







| `:rebirth` / `:petrebirth`| `:rebirth` | Triggers instant companion Rebirth Ascension. |







| `:allskills` | `:allskills [grade]` | Unlocks all element and stunt skills up to specified grade. |







| `:god` / `:godmode` | `:god` | Boosts base stats to 999 and fully restores player and pet HP/SP. |







| `:heal` / `:full` | `:heal [hp] [sp]` | Restores character and active pet HP/SP to maximum. |







| `:winbattle` / `:killall` | `:winbattle` | Instantly completes active combat encounter in victory. |







| `:battle` / `:fight` | `:battle <mob_tid>` | Spawns a test turn-based combat encounter against the specified monster ID. |







| `:mute` / `:unmute` | `:mute <char> [mins]` | Suppresses or restores player public chat privileges. |







| `:online` / `:who` | `:online` | Displays online player census, levels, and map locations. |







| `:level` / `:lvl` | `:level <1-200>` | Sets character level and recalculates derived base attributes. |







| `:points` / `:sp` | `:points <amount>` | Grants unallocated attribute stat points. |







| `:gold` / `:money` | `:gold <amount>` | Sets character wallet gold balance. |







| `:exp` | `:exp <amount>` | Sets total character experience points. |







| `:stats` / `:stat` | `:stat <str> <con> <int> <wis> <agi>` | Distributes character base attribute points. |







| `:restat` | `:restat [char]` | Resets attributes to 10 and refunds all invested stat points. |







| `:item` | `:item [add] <id> [count]` | Injects item(s) directly into character inventory. |







| `:clearinv` | `:clearinv` | Clears all items from the 50-slot inventory bag. |







| `:tent` | `:tent` | Injects authentic Personal Tent item (`36002`) into inventory. |







| `:repair` / `:fixall` | `:repair [char]` | Restores durability (Damage = 0) on all equipped gear and bag items. |







| `:skill` | `:skill <id> [grade]` | Unlocks or upgrades a specific skill ID. |







| `:clearskills` | `:clearskills [char]` | Purges learned progression skills from memory and database. |







| `:buy` | `:buy <query/id> [quantity]`| Buys or spawns item from Item Mall or Item.dat. |







| `:im` / `:mallpoints` | `:im <amount> [char]` | Grants Item Mall currency points to account balance. |







| `:pet` | `:pet <npc_id>` | Spawns and recruits specified companion into active party. |







| `:petexp` | `:petexp <amount>` | Awards experience points to active companion and updates stats. |







| `:petlvl` / `:petlevel` | `:petlvl <1-199>` | Sets active companion's level, recalculating attributes and persisting. |







| `:carnie` | `:carnie` | Warps character to Carnie mini-game amusement park (Map 11094). |







| `:jail` / `:unjail` | `:jail <char> [mins]` | Incarcerates player in Jail cell or releases them. |







| `:unride` | `:unride` | Dismounts active vehicle or riding pet. |







| `:invis` / `:ghost` | `:invis` | Toggles GM Ghost Mode (invisibility from map broadcasts). |







| `:droprate` | `:droprate <rate>` | Sets global monster loot drop multiplier (0.1x to 100.0x). |







| `:reload` | `:reload [all\|quests\|mall\|drops]` | Hot-reloads server game tables without restarting the process. |







| `:stall` / `:spawnstall`| `:stall [pet\|vehicle\|item] [map x y] [title] [infinite\|limited] [id:price[:qty] ...]` | Spawns persistent synthetic vendor stall entity; player remains mobile. |







| `:petstall` | `:petstall [map x y] [title] [infinite\|limited] [npcId:price ...]` | Spawns dedicated Companion Pet stall entity with `Form_BuyPet1..3Stall_1`. |







| `:vehiclestall` | `:vehiclestall [map x y] [title] [infinite\|limited] [itemId:price ...]` | Spawns dedicated Vehicle & Appliance stall entity with `Form_Buy1..3Utensil_1`. |







| `:buystall` | `:buystall <slot> [qty]` | Purchases item, companion, or vehicle from active stall catalog. |







| `:removestall` | `:removestall [vendorId]` | Removes synthetic stall by ID (or nearest within 300 units) and purges from DB. |







| `:clearstalls` | `:clearstalls` | Clears all active stalls from memory and purges `custom_stalls` table. |







| `:summonall` | `:summonall` | Warps all online players to GM's current position (Event). |







| `:kickall` | `:kickall [reason]` | Mass-disconnects all non-GM players for maintenance. |







| `:info` / `:whois` | `:info [char]` | Formats comprehensive character and companion diagnostic summary. |







| `:help` | `:help` | Displays categorized command list and syntax help in chat. |















---















## 5. GUI Operator Administration Console















The server GUI ([`MainForm1.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.cs)) provides an enterprise-grade operator dashboard:















* **Executive Header:** Real-time telemetry displaying online player count, server uptime, CPU/RAM working set, and listening socket states.







* **Persistent Settings:** Server name and MOTD stored relationally in `server_settings` with automatic boot-time loading and debounced auto-save.







* **5-Panel GM Studio (`tabGm`):**







  1. *Authorization & Permissions:* Database-backed GM account management (`gm_accounts`) with instant promotion.







  2. *Live Player Management:* Live player inspector with one-click full heal, gold/stat point grant, level adjustments, amity boost, rebirth, skill unlocking, chat muting, and socket kicking.







  3. *Server Operations:* Real-time announcement broadcaster with 4 color channels, global EXP and drop multipliers, table hot-reloading, 13-companion instant spawner, and graceful 10-second countdown shutdown.







  4. *Spatial Teleportation:* 16 town presets, custom coordinate warps, mass event summons, jail controls, and turn-based combat encounter simulator.







  5. *Inventory & Gear Inspector:* 50-slot inventory DataGridView and 6-slot equipped gear inspector with direct item injection, deletion, and full equipment repair.







* **Bazaar Stall Studio:** Search-as-you-type item catalog autocompletion, map coordinate capture, vendor customization, and persistent database deployment for item, companion, and vehicle kiosks.







* **Experimental Systems Cockpit (`tabExperimental`):** 22 sub-tab operational interface for live simulation and parameter tuning of all 23 experimental engines.















---















## 6. Technical Documentation Index (Master Specification Suite)















The repository includes a comprehensive 43-document master specification suite documenting every system:















1. [**01 - System Architecture & Server Topology**](file:///D:/GitHub/Wonderland-Private-Server/docs/01_system_architecture_and_server_topology.md): Multi-server socket topology, dynamic portability engine ([`PathHelper.cs`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/Core/PathHelper.cs)), threading model, project dependency graph, and graceful shutdown.







2. [**02 - Database Schema & Persistence Lifecycle**](file:///D:/GitHub/Wonderland-Private-Server/docs/02_database_schema_and_persistence_lifecycle.md): Centralized SQLite and MySQL relational persistence, self-healing migrations (`AddColumnIfNotExists`), schema verification (`VerifySetup`), and 1-click migration engine.







3. [**03 - Network Protocol & Action Codes**](file:///D:/GitHub/Wonderland-Private-Server/docs/03_network_protocol_and_action_codes.md): Binary wire framing, `0x44F4` magic header, XOR `0xAD` encryption, little-endian serialization ([`Tools.FromFormat`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/Core/Tools.cs)), and complete Action Code reference catalog (`AC 0` to `AC 226`).







4. [**04 - Binary Asset Pipeline & Decoders**](file:///D:/GitHub/Wonderland-Private-Server/docs/04_binary_asset_pipeline_and_decoders.md): In-memory client asset decoders (`Npc.dat`, `itemDat.wpdat`, `Talk.dat`, `Mark.dat`, `SceneData.dat`, `Eve.emg`, `Formula.dat`, `Compound2.dat`) and [Starter Outfit Pipeline](file:///D:/GitHub/Wonderland-Private-Server/docs/starter_outfit_and_item_pipeline.md).







5. [**05 - Map Engine, Spatial AI & Scene Entity Lifecycle**](file:///D:/GitHub/Wonderland-Private-Server/docs/05_map_engine_spatial_ai_and_scene_lifecycle.md): Isometric coordinate systems, NPC roaming AI, 209-item ground harvesting loop, 14-byte `AC 22:4` scene table, concealment frame sequencing, and puppet suppression.







6. [**06 - Dialogue, Quest State Machine & Cutscenes**](file:///D:/GitHub/Wonderland-Private-Server/docs/06_dialogue_quest_state_machine_and_cutscenes.md): Modal dialogue boxes, 24-bit talk IDs, choice callbacks, quest state monotonic progression, PreEvent condition evaluation, and cinematic timelines.







7. [**07 - Turn-Based Combat Engine & Battlefield**](file:///D:/GitHub/Wonderland-Private-Server/docs/07_turn_based_combat_engine_and_battlefield.md): 4v4 isometric battlefield grid, byte-for-byte `AC 11:5` fighter serialization, 30s turn loop, elemental wheel, knockouts, fair-play drops, and 7-step exit handshake.







8. [**08 - Companion Pet Lifecycle & Vehicles**](file:///D:/GitHub/Wonderland-Private-Server/docs/08_companion_pet_lifecycle_and_vehicles.md): Companion login sync (`AC 15:1`), skill unlocks, amity decay and desertion (<20), rebirth ascension, overworld follower spawning, and 7-step raft shore shipwreck protocol.







9. [**09 - Tent Housing & Manufacturing System**](file:///D:/GitHub/Wonderland-Private-Server/docs/09_tent_housing_and_manufacturing_system.md): Instanced personal tents keyed by `CharID`, multi-tenant instance isolation, overworld multi-tent replication (`SendOpenTents`), tent locking, visitor guards, overworld coordinate preservation (`TentReturnMap`), furniture placement (`chartent_items`), 2nd floor expansions, and authentic `Compound2.dat` synthesis (`AC 64`).







10. [**10 - Social Systems, Player Trade, Friends & Chat**](file:///D:/GitHub/Wonderland-Private-Server/docs/10_social_systems_trade_friends_and_chat.md): 50-slot bag with 31-byte records, bilateral friend roster (`AC 14:5`) and removal (`AC 14:4`), 4-phase atomic player trade, and multi-channel chat moderation.







11. [**11 - GUI Administration Suite & GM Engine**](file:///D:/GitHub/Wonderland-Private-Server/docs/11_gui_administration_suite_and_gm_engine.md): Modernized Windows Forms dashboard with two-tier horizontal navigation, 24 operational modules, executive telemetry header, database-backed MOTD persistence, and GM chat commands.







12. [**12 - Formula.dat Specification & Experience Engine**](file:///D:/GitHub/Wonderland-Private-Server/docs/12_formula_dat_specification_and_exp_engine.md): Complete 407-byte binary schema, IEEE-754 double precision coefficients, client `TCalculator` disassembly, normal/reborn/pet EXP formulas, combat monster EXP distribution, and real-time multiplier control.







13. [**13 - Quest Ecosystem, Census & Flag Mapping**](file:///D:/GitHub/Wonderland-Private-Server/docs/13_quest_ecosystem_census_and_flag_mapping.md): Comprehensive census across `Mark.dat` and `Eve.emg`, explanation of community ~502 canonical journal quest count vs internal 1,027 master quests and 1,507 flag scripts, regional breakdowns, and pairing mechanics.







14. [**14 - Developer Tooling, Reverse Engineering & Deployment**](file:///D:/GitHub/Wonderland-Private-Server/docs/14_developer_tooling_reverse_engineering_and_deployment.md): Prerequisites, zero-error build command, client configuration (`SERVER.INI`), asynchronous diagnostic logger (`DebugSystem`), PCAP analysis, Ghidra disassembly addresses, and bot testing framework.







15. [**15 - Gap Analysis, Missing Features & Feature Roadmap**](file:///D:/GitHub/Wonderland-Private-Server/docs/15_gap_analysis_and_feature_roadmap.md): Protocol gap analysis, unhandled Eve script opcodes (15, 16, 17), anti-exploit security checklist, and phased 5-tier implementation roadmap.







16. [**16 - REA Reverse Engineering Agent Tooling**](file:///D:/GitHub/Wonderland-Private-Server/docs/16_rea_reverse_engineering_agent_tooling.md): REA (`morluto/rea`) CLI and MCP toolchain configuration, Ghidra 12.1.2 decompiler integration, PE/CLI and native binary inspection.







17. [**17 - aLogin.exe Reverse Engineering & Protocol Analysis**](file:///D:/GitHub/Wonderland-Private-Server/docs/17_alogin_reverse_engineering_and_protocol_analysis.md): Client binary architecture (PE32 x86, Delphi VCL forms, `TGmForm`), socket topology (ports 6414 and 6416), `SERVER.INI` parsing, and process dispatching.







18. [**18 - Complete Client Engine & GM Suite Reverse Engineering Specification**](file:///D:/GitHub/Wonderland-Private-Server/docs/18_complete_client_engine_and_gm_suite_analysis.md): Monolithic client engine topology (`aLogin.exe` vs `Main.exe`), 81 in-game forms (AutoPlayerPlus botting, Capsulem, Compound alchemy, ATM banking, instance dungeons), exhaustive 56-button GM administration suite (`TGmForm` / `CY_GmForm`), chat bitmasks, and built-in server event state machines.







19. [**19 - Experimental Features Lab & Gap Implementation**](file:///D:/GitHub/Wonderland-Private-Server/docs/19_experimental_features_and_gap_implementation.md): Complete specifications for the experimental gameplay suite, AC 85/183/184/57/226/199 wire handlers, Opcode 16/17 script bridges, and operator administration cockpit.







20. [**20 - Extended Experimental Features & Decompiled Gap Census**](file:///D:/GitHub/Wonderland-Private-Server/docs/20_extended_experimental_features_and_decompiled_gap_census.md): Exhaustive specifications for the 10 extended experimental gameplay engines (AC 82, 76, 40, 41, 90, 44, 24, 53, 88, 84/86), parameter specifications, data contracts, and expanded administrative cockpit integration.







21. [**21 - Experimental Stall & Player Nullsafety Hotfix**](file:///D:/GitHub/Wonderland-Private-Server/docs/21_experimental_stall_and_player_nullsafety_hotfix.md): Architectural fix resolving null reference crash during test stall spawning with synthetic vendors.







22. [**22 - Wave 3 Decompiled Experimental Systems Specification**](file:///D:/GitHub/Wonderland-Private-Server/docs/22_wave_3_decompiled_experimental_systems_specification.md): Specifications for Wave 3 experimental engines: ATM banking, player prestige titles, geological excavation, carnival lottery wheel, tent butler, and combat status condition rules.







23. [**23 - Experimental Engines Verification & Custom Stall Spawning**](file:///D:/GitHub/Wonderland-Private-Server/docs/23_experimental_engines_verification_and_custom_stall_spawning.md): Verification matrix for all 23 experimental engines, dual-entity stall architecture (Map NPC + Visual Actor), coordinate spawning, wire protocol packets, and in-game purchase commands.







24. [**24 - Personal Player Stall & Custom Kiosk Uncoupling**](file:///D:/GitHub/Wonderland-Private-Server/docs/24_personal_player_stall_and_custom_kiosk_uncoupling.md): Architectural separation of player personal vending kiosks and custom map NPC stalls.







25. [**25 - Native Client Private Shop Protocol & GM Guard Relaxation**](file:///D:/GitHub/Wonderland-Private-Server/docs/25_native_client_private_shop_protocol_and_gm_guard_relaxation.md): S->C AC 23:30 native confirmation packet, sign board banner sync (`AC 56:30`), sitting stance transition, and GM chat stall creation for non-GM accounts.







26. [**26 - Native Stall Closure Opcode Alignment & Posture Recovery**](file:///D:/GitHub/Wonderland-Private-Server/docs/26_stall_close_opcode_alignment_and_posture_recovery.md): Jump table `0x002E5A1F` alignment, opcode 32 routing to `FUN_00454cb8`, clearing player stall mutex `0x2231 = 0`, pet stall flag `0x2232 = 0`, and posture acknowledgement via `AC 32:2`.







27. [**27 - Custom Stall Actor Rendering & Map Synchronization**](file:///D:/GitHub/Wonderland-Private-Server/docs/27_custom_stall_actor_rendering_and_map_synchronization.md): Synthetic vendor actor instantiation via AC 4, online presence AC 10:3, coordinates AC 7, sitting emote AC 32:2, kiosk AC 5:16, signboard AC 56:30, and 15-byte AC 12 despawning.







28. [**28 - Persistent Custom Stalls & Infinite Bazaar Catalog**](file:///D:/GitHub/Wonderland-Private-Server/docs/28_persistent_custom_stalls_and_infinite_bazaar_catalog.md): Dual-database persistence (SQLite & MySQL), synthetic vendor allocation (`8900001+`), infinite vs limited stock semantics, and bootloader restoration.







29. [**29 - Native Stall Appearance, Table/Banner Attachment & Form Closure Protocol**](file:///D:/GitHub/Wonderland-Private-Server/docs/29_native_stall_appearance_and_form_closure_protocol.md): Authentic 3D stall table and floating signboard banner attachment (`AC 23:31`), despawn dismissal (`AC 23:32`), native Form_PrivateShop closure (`AC 23:165 1`), elimination of "Stall is in use" lockup, posture ping-pong suppression in `AC 32:2`, and fully clothed synthetic vendor avatar.







30. [**30 - Native Stall Browse & Purchase Protocol**](file:///D:/GitHub/Wonderland-Private-Server/docs/30_native_stall_browse_and_purchase_protocol.md): Authentic client interaction flow for clicking stalls in-game, rendering Form_BuyItemStall_1 via S -> C AC 23:33 (19-byte header + 39-byte records), infinite stock auto-reset mechanics, elimination of bogus quiz cooldown messages (AC 20:18), visual Bazaar Stall Studio administration GUI with search-as-you-type item catalog autocompletion, map presets, position capture, and dual in-game purchase channels.







31. [**31 - Pet & Vehicle Stall Vending Protocol**](file:///D:/GitHub/Wonderland-Private-Server/docs/31_pet_and_vehicle_stall_vending_protocol.md): Companion pet and vehicle stall vending protocol, trade lock safeguards, and price serialization.







32. [**32 - Narrative Cutscene & Scene Gate Lifecycle**](file:///D:/GitHub/Wonderland-Private-Server/docs/32_narrative_cutscene_and_scene_gate_lifecycle.md): Early-game narrative and cutscene engine synchronization, scene gate and HUD restoration (`AC 5:4`), Kran Island S. Monkey Cutscene 12000 (`12000.sty`) execution with client ACK synchronization (`AC 20:6`) prior to fanfare (`AC 20:10`) and party recruitment, Robinson beach arrival sequence with cutscene timeline progression, player mobilization without forced dialogue, manual interaction trigger for Robinson Event 8, and duplicate dialogue prevention with trailing click debouncing.







33. [**33 - Pet Hotel Concurrency & Modal State Machine**](file:///D:/GitHub/Wonderland-Private-Server/docs/33_pet_hotel_concurrency_and_modal_state_machine.md): Storage manager lock flags (`AC 31:8`), `Form_PetHotel` modal input lock clearance (`AC 31:12` resetting `[Form_PetHotel + 0x174]` via `0x002ea4c5`), companion withdrawal memory sequencing (`AC 31:5` copy before `AC 31:4` slot clear), and capacity validation on deposits and exchanges.







34. [**34 - NPC Vendor Procurement & Catalog Protocol**](file:///D:/GitHub/Wonderland-Private-Server/docs/34_npc_vendor_procurement_and_catalog_protocol.md): NPC merchant store catalog resolution (`AC 35:12` with catalog IDs `0x0001FB83`..`0x0001FB85`), decoupling purchasing from liquidation (`Form_EquipStore` / `Form_ThingStore` vs `Form_Sale`), shopping cart order validation (`AC 27:1`), resolution of 1-based catalog slot indices to authentic item IDs via `EveData` map `InteractiveInfo` and fallback catalog tables, resolution of packet parsing precedence for client `[count, slot, ammt]` framing, alignment of `NpcBuyResult` wire codes (`0 = Success, 1 = Rejected, 2 = NotEnoughGold, 3 = InventoryFull`) to eliminate false-positive inventory full dialogs, dual-price decoding from `Data/Item.dat` (`+0x72` BuyPrice vs `+0x76` SellPrice compiled to `npc_buy_prices.csv`) ensuring exact gold deduction matching the client store UI (Bread = 10 Gold, Rice Wine = 50 Gold), bag capacity verification, and atomic item delivery in `Inventory.BuyFromNpc`. Full liquidation / sell support via `AC 27:4` (`Form_SaleEquip_1`, Mode 0) and `AC 27:3` (`Form_SaleThing_1`, Mode 1), batch stack sales via `AC 27:2`, session and category verification in `Inventory.SellToNpc`, official liquidation ACK response (`AC 27:2`), two-tier Eve dialogue branching interception (`TryHandleMerchantChoice`), and terminal modal packet sequencing.







35. [**35 - Tent Housing Virtual Map & Entry Lifecycle**](file:///D:/GitHub/Wonderland-Private-Server/docs/35_tent_housing_virtual_map_and_entry_lifecycle.md): Instanced personal tent virtual map routing (`MapID = 100000 + CharID`), dual-keying in map dictionary, stream pointer realignment (`p.SetPtr(6)`) for 32-bit owner CharID extraction in `AC 65:1`, map initialization and load completion packet transmission (`AC 23:102` / `AC 20:8`), and authentic reward item injection (`36002` Personal Tent / `34058` Remote Control).







36. [**36 - Inventory Item Dispatching & Mount Lifecycle**](file:///D:/GitHub/Wonderland-Private-Server/docs/36_inventory_item_dispatching_and_mount_lifecycle.md): Universal inventory double-click dispatcher (`AC 23:15`), equipment wearing (`WearEQ`), mount/dismount lifecycle for vehicles and Bamboo Dragonfly (`34169`), Quest Notebook rendering (`32000`), combat auto-battle Remote Control (`34058` / `AC 29:6`), Return Scrolls (`30001`, `34163`), EXP Potion consumption, and `AC 23:54` fishing/gathering mutex state clearance.







37. [**37 - Tent Exit Lifecycle & Inventory Additive Sync Hotfix**](file:///D:/GitHub/Wonderland-Private-Server/docs/37_tent_exit_lifecycle_and_inventory_additive_sync_hotfix.md): Dual exit dispatching (`AC 65:11`/`AC 65:3` door clicks and `AC 20:8` portal 1), entry coordinate recording (`TentReturnMap`, `_mapx`, `_mapy`), spawn proximity guard bypass, and inventory additive synchronization prevention in `AC 23:1`.







38. [**38 - Tent Furniture Default Spawning and Manipulation Lifecycle**](file:///D:/GitHub/Wonderland-Private-Server/docs/38_tent_furniture_default_spawning_and_manipulation_lifecycle.md): Reverse-engineered client housing manager (`FUN_003fd644`), batch scene synchronization via `AC 62:4`, 57-byte entity serialization (`FUN_003fb18c`), 1-based indexing ($1 \le \text{Index} \le 130$), default furniture spawning (Resource Recycling `38055` and Work Platform `38049`), pickup (`AC 62:2`), move/rotation (`AC 62:3`), and SQLite persistence.







39. [**39 - Tent Floor Boundaries & 2D Inventory Grid Engine**](file:///D:/GitHub/Wonderland-Private-Server/docs/39_tent_floor_boundaries_and_2d_inventory_grid_engine.md): Reverse-engineered client tent floor 0 bounding rectangle `[Left: 29, Top: 38, Right: 44, Bottom: 50]` (`FUN_003e5ccc` / `FUN_003e6b78`) resolving client-side item rejection, correction of default tent coordinates to `(34, 42)` and `(37, 42)`, binary deserialization alignment for `Data/Item.dat` 451-byte records in `PhxItemDat` with offset 406 (`cellwidth`) and 407 (`cellheight`) decoding, and implementation of the complete 50-slot (5x10) 2D inventory grid engine with multi-cell slot footprints, boundary wrapping prevention, master/slave locking, and capacity simulation.







40. [**40 - Dual-Format ItemDat Deserialization & Starter Pack Delivery Lifecycle**](file:///D:/GitHub/Wonderland-Private-Server/docs/40_dual_format_item_dat_deserialization_and_starter_pack_delivery.md): Dual-format adaptive deserializer in `PhxItemDat` seamlessly supporting both 47-byte `itemDat.wpdat` cache and 451-byte client `Item.dat` assets, resolving critical ItemID/Equippos lookup failures, restoring beginner equipment creation in `SetBeginnerOutfit`, expanding starter items to 10 entries including Personal Tent (`36002`) and Raft (`48010`), and providing login self-healing fallbacks for Level 1 characters.







41. [**41 - Tent Housing Interior Floor Bounds Protocol & Furniture Lifecycle**](file:///D:/GitHub/Wonderland-Private-Server/docs/41_tent_furniture_lifecycle_and_ac65_floor_bounds_protocol.md): Reverse-engineered `aLogin.exe` Housing Manager dispatchers (`FUN_003fd644`, `FUN_00404e4c`, `FUN_003e5ccc`, `FUN_003e6b78`) discovering mandatory `AC 65:12` protocol requirement for establishing internal floor bounds `Rect(29, 38, 44, 50)` and sprite `10005`, resolving client-side rejection of furniture placed inside personal tent, fixing `Tent._tenttype` default from 0 to 1, ensuring default Trash Can (`38055`) and Work Platform (`38049`) spawn upon entering tent, and wiring exact packet ordering (`AC 65:12` -> `AC 65:7` -> `AC 62:14` -> `AC 62:15` -> `AC 62:4`).



42. [**42 - Inventory Grid Footprint Standards & Transport Item Dimensions**](file:///D:/GitHub/Wonderland-Private-Server/docs/42_inventory_grid_footprint_and_transport_cell_standards.md): Disassembled client bag inventory semantics (`aLogin.exe` `FUN_002a2490`, `FUN_002d6ad8` AC 23) vs. housing placement footprints (`FUN_003e6b78`), establishing uniform 1x1 bag slot occupation for water transport items (Raft `48010`/`48016`, Sailboat `48009`) while retaining authentic `cellwidth`/`cellheight` collision footprints in tent housing.



43. [**43 - Tent Packet Dispatch Timing & Sprite Generation Architecture**](file:///D:/GitHub/Wonderland-Private-Server/docs/43_tent_packet_dispatch_timing_and_sprite_generation.md): Reverse-engineered `aLogin.exe` tent map validation guard (`0x42dd08`..`0x42dd1d`) and sprite generation caller (`0x4358c4`), eliminating premature `AC 62:4` dispatch in `Tent.SendMapInfo` during `Warp_In` when client `MapID` was not yet `63507`, enforcing authentic post-`AC 12:1` map acknowledgement transmission sequence (`AC 65:12` -> `AC 65:7` -> `AC 62:14` -> `AC 62:15` -> `AC 62:4` -> `AC 5:4`), and ensuring reliable default rendering of Trash Can (`38055`) and Work Platform (`38049`).

44. [**44 - Crafting Compound Recipe Deserialization & Inventory Grid Multi-Cell Footprint**](file:///D:/GitHub/Wonderland-Private-Server/docs/44_crafting_compound_recipe_deserialization_and_inventory_grid_footprint.md): Reverse-engineered `aLogin.exe` recipe loader (`0x2BC80C`), client packet assembler (`0x145882`), and compound lookup methods (`0x2BC674`), aligning `AC 64:1` packet wire protocol with 16-bit `benchIndex` (`p.Unpack16()`) and 16-bit `formulaIndex` (`p.Unpack16()`), eliminating pointer desynchronization that bit-scrambled formula IDs into 3584, sanitizing record 0 header in `Compound2.dat` (789 records), resolving "Yetersiz malzeme!" and "Bilinmeyen tarif!" crafting failures, restoring authentic multi-cell footprint dimensions in `Inventory.GetItemWidth`/`GetItemHeight` for Raft (`48010` / `48016` -> 4x3 cells), locking slave cells, and separating tent appliance floor placement from direct bag delivery for vehicles, tools, and consumables.

45. [**45 - Character Appearance, Model Encoding & Role Matrix Specification**](file:///D:/GitHub/Wonderland-Private-Server/docs/45_character_appearance_and_model_encoding_matrix.md): Reverse-engineered client character matrix `FUN_0049eb74` (`0x0049eb74`), sprite generator `FUN_00201de4`, creation wizard `FUN_00270028`, and packet dispatcher `FUN_002d6ad8` (`case 9:1`), documenting the 15-role character architecture across all four body styles, fixing stat unpacking wire order in `AC09.cs` (`Str -> Con -> Int -> Wis -> Agi`), resolving the Daniel / More avatar desynchronization caused by mismatched database head style (`head 0` for Daniel vs `head 1` for Sid vs `head 2` for More), re-aligning beginner outfit class locks (`21004` / `24004` for Daniel), and correcting character palette hair and clothing color codes.

46. [**46 - Tent Furniture Movement Offset & Crafting Morph Resolution**](file:///D:/GitHub/Wonderland-Private-Server/docs/46_tent_furniture_movement_offset_and_crafting_morph_resolution.md): Reverse-engineered `AC 62` packet handling in `AC62.cs`, adding missing `r.SetPtr(6)` to eliminate a 1-byte pointer desynchronization that bit-shifted incoming `rawIndex` 2 into 515 and coordinates (42, 39) into (10752, 9984), preserving tent floor index in `Tent.MoveItem` without corruption from Z layer elevation, eliminating erroneous `AC 5:5` RiceBall shapeshifting broadcasts with dummy NPC IDs (`60018`, `60020`, `60015`, `60025`, `60010`, `60050`) across crafting, alchemy, repair, forging, marriage, and rebirth systems, and establishing authentic `AC 23:122` synthesis visual sparkles and proactive login morph resetting.

47. [**47 - Tent Furniture Tool Accessory Attachment Protocol & Spatial Occupancy**](file:///D:/GitHub/Wonderland-Private-Server/docs/47_tent_furniture_tool_accessory_attachment_protocol.md): Reverse-engineered `aLogin.exe` tool collector (`0x00439b56`–`0x00439c25`), tool attachment handler (`0x0042ddcf`), and tent unpacker (`0x0042aff4`). Aligned the 57-byte tent object packet payload to exact client memory offsets (`parent` at 15..16, reserved padding at 17..18, accessory slots 1..10 at 19..38 matching `ebx * 2 + 0x2b`, padding at 39..47, floor at 48, trailing padding at 49..56), unlocking tool-dependent recipes (e.g. Stone Knife `38058` unlocking Raft `48010` and Lauuan Wood `45001`..`45004`, Wooden Saw `38036` unlocking 12 woodcrafting formulas). Implemented incremental `AC 62:5` broadcast without disruptive full layout wipes, resolved unstacked item material consumption in `Inventory.RemoveItem` (`Ammt == 0` fallback), loaded authentic `PhxItemInfo` in `Tent.PlaceItem` ensuring multi-cell bounding box dimensions (Raft 4x3, Workbench 3x2, Trash Can 2x2), enforced spatial collision via `Tent.IsAreaOccupied`, and removed stray `AC 5:5` disguise polymorphing in `AC64.cs`.

48. [**48 - Tent Item Movement Persistence & Cache Bypass Resolution**](file:///D:/GitHub/Wonderland-Private-Server/docs/48_tent_item_movement_persistence_and_cache_bypass_fix.md): Resolved tent furniture coordinate reversion where moved items (`AC 62:3`) reset back to initial constructor positions (`34, 42` and `37, 42`) upon client reconnect, re-login, or server restart. Root-caused `Cache.TryGetValue` early-return in `CharacterDataBase.GetCharacterData` that skipped `LoadTentData`, causing newly instantiated `Player.Tent` objects to retain default constructor furniture and overwrite `chartent_items` with defaults on subsequent saves. Integrated `LoadTentData` and character settings into both cached and uncached login paths, ensured clean `IsDirty = false` state after database load and constructor initialization, broadcast movement confirmation (`AC 62:3`) to all occupants via `p.Tent.Broadcast`, enforced immediate database commit via `SaveTentData(p, force: true)`, and implemented `Tent.IsInFloorBounds` to reject coordinates outside client bounds (`29 <= X <= 44`, `38 <= Y <= 50`), preventing furniture sprites from being discarded into the void.

49. [**49 - Tent Crafting Manufacturing Timer & AC 64 Wire Protocol**](file:///D:/GitHub/Wonderland-Private-Server/docs/49_tent_crafting_manufacturing_timer_and_ac64_protocol.md): Resolved instant craft completion by reverse-engineering `aLogin.exe` crafting handler `FUN_00401808` and timer renderer `FUN_00479102`. Identified that `cBuildElement.buildTime` in `Compound2.dat` represents minutes (`buildTime * 60 * requestedBatchCount` in seconds). Replaced instantaneous item delivery with an asynchronous `ActiveCraftSession` lifecycle: `AC 64:1` transmits authentic active slot duration (`[benchIndex (1B)] [itemId (2B)] [durationSeconds (4B)] [batchCount (1B)]`), `AC 64:2` triggers client hammering/sawing animation and `Panel_makeThingTimeForm_1` countdown progress bar, asynchronous timer dispatches `AC 64:4` on completion, `AC 64:3` supports craft cancellation with material refund, and `Tent.OnTentItemsSent` synchronizes ongoing craft countdowns upon tent re-entry.

50. [**50 - Tent Furniture Tool Accessory Persistence & Client Formula Unlock Protocol**](file:///D:/GitHub/Wonderland-Private-Server/docs/50_tent_furniture_tool_accessory_attachment_and_recipe_unlock.md): Reverse-engineered `aLogin.exe` deserialization preconditions at `0x416ae1` (requires `X == 0 && Y == 0 && Z == 0` to skip floor polygon bounds check `0x485db8`) and `0x413496` (requires `Offset 48 (Floor) != 0`, set to `1`, to skip 2D ground grid tile collision check `0x4167c4`). Resolved recipe disappearance where tool attachments (Stone Knife `#38058`, Wooden Saw `#38036`) vanished from Work Platform (`#38049`) after crafting or tent re-entry by setting `Z = 0` and `Floor = 1` in `SendTentItemsToPlayer` (`AC 62:4`). Updated `AC62.Recv4` to broadcast the synchronized tree following incremental `AC 62:5`, and updated `AC62.Recv2` to automatically refund all attached tools to the player's inventory when an appliance is picked up.

51. [**51 - Tent Furniture Placement Packet Structure & Slot Offset Alignment**](file:///D:/GitHub/Wonderland-Private-Server/docs/51_tent_furniture_placement_packet_structure_and_slot_alignment.md): Disassembled `aLogin.exe` at `0x2d610c` (`AC 62:1` packet builder) to isolate the wire structure for deploying inventory furniture onto the tent floor: `[SlotIndex (1B)] [X (4B LE)] [Y (4B LE)] [Z (4B LE)] [Floor (1B)]`. Resolved the placement defect where dragging furniture from bag threw `Invalid slot 40` with corrupted coordinates `(637534208, 16777216)` by removing an erroneous phantom `bagIndex` byte in `AC62.Recv1` that shifted all coordinate fields. Correctly reads `slotIndex`, validates `IsInFloorBounds` and `IsAreaOccupied`, deducts item from inventory, deploys furniture into `_tentObjects`, and broadcasts `AC 62:1` confirmation and synchronized tree.

52. [**52 - Tent Wallpaper & Flooring Application Protocol**](file:///D:/GitHub/Wonderland-Private-Server/docs/52_tent_wallpaper_and_flooring_application_protocol.md): Reverse-engineered `aLogin.exe` Subcode 15 (`0x42ea88`) -> `0x433904` (`SetWallpaper`) and Subcode 14 (`0x42ea50`) -> `0x433864` (`SetFloorColor`), establishing that the client loads texture mappings directly from `Item.dat` via the item ID payload (`AC 62:15` for walls, `AC 62:14` for floors) and renders textured meshes when `[ebx + 0x1a68] > 0`. Fixed wallpaper crafting (e.g. Item `#39041` Wallpaper) from being mistakenly treated as 2-cell floor furniture via `PlaceFurnitureInTent`, added item classification heuristics (`Tent.IsWallpaperItem` and `Tent.IsFloorItem`), implemented `Tent.SetWallpaper` and `Tent.SetFloorColor` broadcasting to all occupants in real time, integrated automatic wallpaper/flooring application upon craft completion in `AC64.cs` and upon bag drag/use in `AC62.cs`, and persisted texture state permanently in SQLite `chartent` (`floor1wallpaper`, `floor1Color`).

53. [**53 - Godot Client Network Synchronization & C# Server Parity**](file:///D:/GitHub/Wonderland-Private-Server/docs/53_godot_client_packet_synchronization_and_server_parity.md): Synchronized the C# private server wire protocol with the Godot 4 client (`wonderland-online-client`) across core gameplay opcodes. Implemented `AC 63:3` (`CREATE_CHAR` / `0x033F`) to receive character creation requests (slot, name, role, element, stats), validate and persist new players in `CharacterDataBase.WriteNewPlayer`, and dispatch success ACK `[1, 3, 0]`. Implemented `AC 62:45` (`PING` / `0x3E2D`) keepalive heartbeat echo to eliminate connection watchdog timeouts. Implemented `AC 35:12` (`CHAT_BROADCAST` / `0x230C`) for public/channel chat and GM command forwarding. Added `AC 85:7` (`MAP_WARP` ACK / `0x0755`) mobility unfreeze response. Activated `UserDataBase.Update_Player_ID` to maintain account-character slot bindings across SQLite and MySQL.















---















## 7. Decompiled Client Reverse Engineering Compendium















The complete reverse engineering of the monolithic client binaries (`aLogin.exe` and `Main.exe`), asset pipelines, memory structures, and protocol dispatchers is cataloged in [`decompiled/README.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/README.md):















* **Specifications 01-20:** Client architecture, GM studio (`CY_GmForm`), form automation, wire framing, actor memory layout, asset parsers, companion lifecycle, quests, authentication, Item Mall, chat constraints, alchemy, combat validation, minigames, marriage, instance dungeons, forge potential, DirectX audio, and the complete 9,451 function inventory catalog.







* **Specifications 21-40:** Binary asset formats, patcher/updater engines, rebirth stat layout, server broadcasts, alchemy chemistry matrix, motion state resolver, vehicle GPS navigation, weather particle systems, arcade state machines, guild emblems, lodging, character creation wizard, courier dispatch, co-op battles, tent camping, alchemy grades, Mario platformer, and church charity tithes.







* **Specifications 41-60:** Durability wear, Gobang chess board, companion riding, aquaculture catch tables, hot springs, casino gaming, ATM banking, cursor state machines, quest trackers, DirectDraw blitting, skill trees, guild authority, player stalls, PIN vaults, character sheets, duplicate mission controllers, bulletin boards, instant messenger ICQ, and title achievements.







* **Specifications 61-80:** Token redemption, express freight, furniture storage, challenge chambers, hotkey macro docks, wallpapers, emotes, party tactics, dialogue trees, ante stakes, audio preferences, offline mailbox, character deletion challenge, guild bylaws, point card recharge, church tithe donation boxes, vendor liquidation safeguards, HUD docks, and inn resting.







* **Specifications 81-100:** Pet rebirth ascension, UFO claw machines, underground treasure mining, rhythm boxing power meters, lottery ball physics, memory concentration poker, tent butler automation, dynamic scene lighting, DirectDraw clippers, combat radial targeting, status condition buffs/debuffs, vendor bulk procurement, consignment bazaars, password rotation, character creation wizards, furniture stall vending, gacha capsule machines, guild emblem canvas, and the grand master implementation manual.















---















## 8. License & Disclaimer















Wonderland Online is a registered trademark of Chinese Gamer International Corp. / IGG Inc. This project is an independent clean-room reverse-engineering and educational research emulator intended solely for archival, research, and private development purposes. All original assets and trademarks belong to their respective copyright holders.







