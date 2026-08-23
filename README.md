WLO Private Server CheatEngine
=========================

Developing Tools: Visual Studio 2022

Database: sqlite bypass

Private Server + CheatEngine for Wonderland Online

Running steps:
1. Client Location: `D:\garipgudubetseyler\WLRI`
2. Ensure `SERVER.INI` in the client directory is set to `127.0.0.1`
3. Server DAT files in `./Data` are synchronized with the client data files (`Npc.dat`, `Item.dat`, `Skill.dat`, `Talk.dat`, `Eve.emg`, `Ground.MMG`, `SkillData.MBTM`, etc.).
   - Note: The large 1.42 GB sprite archive `odd.dat` can be downloaded directly from [Releases v1.0.0](https://github.com/Eminbalci/Wonderland-Private-Server/releases/tag/v1.0.0).
4. Run `Wonderland Private Server.exe` in `bin/Debug` & wait until log shows "Now listening for clients..."
5. Run `aLogin.exe`, select server and login with `gmone` / `gmone`

Tips:
In the private server exe, click "Cheat" tab:
- Double click the item in list:
    > Maps : Teleports you to that ID
    
    > Vehicle : Ride the vehicle
    
    > Items : Adds the item to your inventory
    
    > Npc : Battle/ride the NPC or Pet
- Each lists have a search textbox on top of it:
    > Type in your search query and then hit Enter
    
    > To reload all, blank the search then hit Enter
- Press `F5` key anywhere in the GUI window to automatically launch `aLogin.exe`.
- Click the in-game PK button (sword icon) and click any monster/NPC to engage in turn-based combat. Supports attack, skills, defending, fleeing, XP/Gold rewards, and automatic battle exit.
- Real-time NPC movement and roaming (`AC 22 Sub 2`) ported from Python server with scripted waypoints and random wandering.
- Character skill unlocking system (`AC 5 Sub 11`, `AC 8 Sub 1`) with character-specific stunt skills, element skills, and `:skill <id> [grade]` chat command.
- Interactive Quest & Journal System (`AC 39`, `AC 52`, `charquest` DB table) supporting multi-stage NPC dialogues, item delivery verification, automatic reward distribution (Gold, EXP, Items, Companions), and quest battle encounters.
- In-Game GM Chat Commands:
    > `:heal [hp] [sp]` : Fully restores character HP and SP (or specified values).
    > `:level <1-200>` : Sets character level and recalculates stats.
    > `:gold <amount>` : Sets character gold.
    > `:stat <str> <con> <int> <wis> <agi>` : Sets base character stats.
    > `:item <id> [amount]` : Adds item(s) to inventory.
    > `:skill <id> [grade]` : Unlocks or upgrades a skill.
    > `:warp <map_id> <x> <y>` : Teleports player to map coordinates.
    > `:help` : Shows command help in chat.
- `Src/`: Server architecture, networking handlers, action codes (AC00-AC92), database access, and GUI management.
- `dev_scripts/`: Development and reverse-engineering diagnostic scripts and tools.
- `docs/`: Technical specifications, reverse engineering documentation, and protocol guides.
- Technical documentation and reverse engineering analysis of the client launcher available in [docs/alogin_decompiled_analysis.md](file:///d:/GitHub/Wonderland-Private-Server/docs/alogin_decompiled_analysis.md).
- Detailed technical overview of the NPC and Quest systems available in [docs/npc_and_quest_systems_overview.md](file:///d:/GitHub/Wonderland-Private-Server/docs/npc_and_quest_systems_overview.md).
- Reverse engineering analysis of decompiled NPC and quest routines in [docs/decompiled_npc_and_quest_analysis.md](file:///d:/GitHub/Wonderland-Private-Server/docs/decompiled_npc_and_quest_analysis.md).
- Reverse engineering analysis of decompiled skill systems in [docs/decompiled_skill_system_analysis.md](file:///d:/GitHub/Wonderland-Private-Server/docs/decompiled_skill_system_analysis.md).
- Comprehensive game mechanics and mathematical formulas reference in [docs/game_systems_and_formulas_reference.md](file:///d:/GitHub/Wonderland-Private-Server/docs/game_systems_and_formulas_reference.md).
- Extended reverse engineering analysis of gameplay subsystems in [docs/decompiled_extended_systems_analysis.md](file:///d:/GitHub/Wonderland-Private-Server/docs/decompiled_extended_systems_analysis.md).
- Master binary packet Action Code protocol specification in [docs/master_action_codes_protocol_reference.md](file:///d:/GitHub/Wonderland-Private-Server/docs/master_action_codes_protocol_reference.md).
- Reverse engineering memory map, asset formats, and engine internals in [docs/decompiled_engine_internals_and_memory_map.md](file:///d:/GitHub/Wonderland-Private-Server/docs/decompiled_engine_internals_and_memory_map.md).
- Technical diagnosis and solution for NPC/chest blinking and state toggling in [docs/npc_blinking_and_chest_state_fix.md](file:///d:/GitHub/Wonderland-Private-Server/docs/npc_blinking_and_chest_state_fix.md).
- Authentic map chest, prop gathering drop tables and timed respawn system in [docs/map_chest_and_gathering_drop_system.md](file:///d:/GitHub/Wonderland-Private-Server/docs/map_chest_and_gathering_drop_system.md).
- Chest & Gathering Drop GUI Editor documentation in [docs/chest_drop_editor_gui.md](file:///d:/GitHub/Wonderland-Private-Server/docs/chest_drop_editor_gui.md).
- Player inventory and equipment persistence on save/shutdown in [docs/player_inventory_persistence_fix.md](file:///d:/GitHub/Wonderland-Private-Server/docs/player_inventory_persistence_fix.md).
- Safe server shutdown and instant data save GUI controls in [docs/safe_shutdown_system.md](file:///d:/GitHub/Wonderland-Private-Server/docs/safe_shutdown_system.md).
- Authentic Item Mall & Catalog System (Port 6416 & AC 23/35/54) in [docs/item_mall_system_status.md](file:///d:/GitHub/Wonderland-Private-Server/docs/item_mall_system_status.md).
- Responsive GUI Layout & Dynamic Scaling in [docs/gui_responsive_layout.md](file:///d:/GitHub/Wonderland-Private-Server/docs/gui_responsive_layout.md).
- Portal Teleport Cooldown System in [docs/portal_cooldown_system.md](file:///d:/GitHub/Wonderland-Private-Server/docs/portal_cooldown_system.md).
- NPC Scripted Path Movement & Farm Leashing in [docs/npc_movement_system.md](file:///d:/GitHub/Wonderland-Private-Server/docs/npc_movement_system.md).
- DAT Files and Authentic Talk/Mark/Eve System in [docs/dat_file_system.md](file:///d:/GitHub/Wonderland-Private-Server/docs/dat_file_system.md).
- Authentic Minigame System & Voucher Reward Protocol (AC 57 / AC 23:6) in [docs/minigame_system_protocol.md](file:///d:/GitHub/Wonderland-Private-Server/docs/minigame_system_protocol.md).
- Gathering Nodes & Timed Respawn System (Coconuts, Wood, Ore) in [docs/gathering_nodes_and_respawn_system.md](file:///d:/GitHub/Wonderland-Private-Server/docs/gathering_nodes_and_respawn_system.md).
- Authentic Redeem Voucher NPC Exchange Protocol in [docs/redeem_voucher_exchange_protocol.md](file:///d:/GitHub/Wonderland-Private-Server/docs/redeem_voucher_exchange_protocol.md).
- Breillat 10-Talks Character Swap & Model Transformation in [docs/breillat_character_swap_protocol.md](file:///d:/GitHub/Wonderland-Private-Server/docs/breillat_character_swap_protocol.md).
- Ship Captain Storm Cutscene & Shipwreck Protocol (AC 186 / AC 20 / AC 12) in [docs/captain_storm_cutscene_protocol.md](file:///d:/GitHub/Wonderland-Private-Server/docs/captain_storm_cutscene_protocol.md).
- Companion Recruitment and Map NPC Despawn Synchronization (AC 22:10 / AC 15:1) in [docs/companion_recruitment_and_npc_despawn.md](file:///d:/GitHub/Wonderland-Private-Server/docs/companion_recruitment_and_npc_despawn.md).
- Character Deletion and Relational Data Cleanup Protocol in [docs/character_deletion_and_cleanup_protocol.md](file:///d:/GitHub/Wonderland-Private-Server/docs/character_deletion_and_cleanup_protocol.md).
- Character Relational Data & Map NPC Visibility GUI Editor in [docs/character_data_editor_gui.md](file:///d:/GitHub/Wonderland-Private-Server/docs/character_data_editor_gui.md).
- Robust Safe Server Shutdown & Thread Synchronization in [docs/safe_server_shutdown_system.md](file:///d:/GitHub/Wonderland-Private-Server/docs/safe_server_shutdown_system.md).
- Robinson Beach Rescue Cutscene Protocol & StepQueue Architecture in [docs/robinson_beach_cutscene_protocol.md](file:///d:/GitHub/Wonderland-Private-Server/docs/robinson_beach_cutscene_protocol.md).
- Raft Shore Landing & Vehicle Wrecking Protocol in [docs/raft_shore_landing_protocol.md](file:///d:/GitHub/Wonderland-Private-Server/docs/raft_shore_landing_protocol.md).
- Holy Village & NPC System Actions Protocol (Choice Resolution & System Action Opcodes) in [docs/holy_village_npc_system_actions.md](file:///d:/GitHub/Wonderland-Private-Server/docs/holy_village_npc_system_actions.md).
- Native Eve.emg Portal Resolution Protocol (Hierarchical Multi-Priority Warp Engine) in [docs/native_eve_portal_resolution_protocol.md](file:///d:/GitHub/Wonderland-Private-Server/docs/native_eve_portal_resolution_protocol.md).
- Character Props Keeper Storage & Database Persistence (AC 30 / AC 29 / storID 2) in [docs/props_keeper_storage_persistence.md](file:///d:/GitHub/Wonderland-Private-Server/docs/props_keeper_storage_persistence.md).
- Pet Hotel Storage System & Companion Database Persistence (AC 31 / isHotel 1) in [docs/pet_hotel_storage_persistence.md](file:///d:/GitHub/Wonderland-Private-Server/docs/pet_hotel_storage_persistence.md).
- Overworld Monsters, Aggro, and Dynamic Encounter Combat Protocol in [docs/overworld_monsters_and_encounter_combat.md](file:///d:/GitHub/Wonderland-Private-Server/docs/overworld_monsters_and_encounter_combat.md).
- Character & Companion Stat Point Allocation Protocol (AC 8 / Stat 38) in [docs/stat_point_allocation_protocol.md](file:///d:/GitHub/Wonderland-Private-Server/docs/stat_point_allocation_protocol.md).
- Companion & Pet Combat System (AC 11:5, AC 50:1, AC 51:1) in [docs/companion_battle_system.md](file:///d:/GitHub/Wonderland-Private-Server/docs/companion_battle_system.md).
- Multi-Member Team & PvP Battle System in [docs/multi_member_team_and_pvp_battle_system.md](file:///d:/GitHub/Wonderland-Private-Server/docs/multi_member_team_and_pvp_battle_system.md).
- Party Member Portal Following Protocol in [docs/party_portal_teleport_protocol.md](file:///d:/GitHub/Wonderland-Private-Server/docs/party_portal_teleport_protocol.md).
- Party HP/SP Synchronization and AC 13 Protocol in [docs/party_stat_sync_protocol.md](file:///d:/GitHub/Wonderland-Private-Server/docs/party_stat_sync_protocol.md).
- Friend List Online Status, Party Formation & Companion Following Synchronization Fix in [docs/friend_list_status_and_party_following_fix.md](file:///d:/GitHub/Wonderland-Private-Server/docs/friend_list_status_and_party_following_fix.md).
- PvE Battle Monster Spawning Fix in [docs/pve_battle_monster_spawning_fix.md](file:///d:/GitHub/Wonderland-Private-Server/docs/pve_battle_monster_spawning_fix.md).
- Player Disconnect, Logout & World Despawn Protocol in [docs/player_logout_and_despawn_handling.md](file:///d:/GitHub/Wonderland-Private-Server/docs/player_logout_and_despawn_handling.md).
- Player Map Spawn & Visual Appearance Protocol (AC 3 vs AC 4) in [docs/player_spawn_and_visual_sync.md](file:///d:/GitHub/Wonderland-Private-Server/docs/player_spawn_and_visual_sync.md).
- Linux Compatibility, Porting Roadmap & Docker Guide in [docs/linux_compatibility_and_porting_guide.md](file:///d:/GitHub/Wonderland-Private-Server/docs/linux_compatibility_and_porting_guide.md).
- Event & Quest DB Studio (Map Bytecode Flow, Talk.dat Visualizer & Master Quests) in [docs/event_and_quest_db_manager_studio.md](file:///d:/GitHub/Wonderland-Private-Server/docs/event_and_quest_db_manager_studio.md).
- Companion & Pet Network Synchronization Protocol (AC 15:4, AC 15:1, AC 19:4, AC 15:16/17, AC 13:5, AC 5:8) in [docs/companion_following_synchronization_protocol.md](file:///d:/GitHub/Wonderland-Private-Server/docs/companion_following_synchronization_protocol.md).
- Eve.emg Binary String Encoding & Big5 Localization Architecture in [docs/eve_binary_encoding_and_localization.md](file:///d:/GitHub/Wonderland-Private-Server/docs/eve_binary_encoding_and_localization.md).
- Universal Quest & Event Runtime Engine (7-Pillar Architecture) in [docs/quest_and_event_runtime_engine.md](file:///d:/GitHub/Wonderland-Private-Server/docs/quest_and_event_runtime_engine.md).
- Talk.dat Dialogue Flow Resolution & Eve Opcode Decoding in [docs/talk_dat_dialogue_flow_resolution.md](file:///d:/GitHub/Wonderland-Private-Server/docs/talk_dat_dialogue_flow_resolution.md).
- Modular Talk ID Resolver Engine Architecture & Dynamic Token Parser in [docs/talk_id_resolver_architecture.md](file:///d:/GitHub/Wonderland-Private-Server/docs/talk_id_resolver_architecture.md).
- Map NPCs & Event Sequence Studio Architecture in [docs/map_npc_and_event_sequence_studio.md](file:///d:/GitHub/Wonderland-Private-Server/docs/map_npc_and_event_sequence_studio.md).
- Dynamic PreEvent Runtime Engine & Actor Scene Isolation in [docs/preevent_runtime_engine.md](file:///d:/GitHub/Wonderland-Private-Server/docs/preevent_runtime_engine.md).
- 7 Independent Event Flow Systems Architecture in [docs/all_7_event_flow_systems.md](file:///d:/GitHub/Wonderland-Private-Server/docs/all_7_event_flow_systems.md).
- Advanced Features Implementation Roadmap in [docs/remaining_features_roadmap.md](file:///d:/GitHub/Wonderland-Private-Server/docs/remaining_features_roadmap.md).
- Native Quiz & Multi-Question Choice Event Flow Engine in [docs/native_quiz_and_multiquestion_event_flow.md](file:///d:/GitHub/Wonderland-Private-Server/docs/native_quiz_and_multiquestion_event_flow.md).
- Immediate Player Persistence System across critical packet triggers in [docs/immediate_player_persistence_system.md](file:///d:/GitHub/Wonderland-Private-Server/docs/immediate_player_persistence_system.md).
- PvE Battle Pet & Turn-Based Flee System in [docs/pve_battle_pet_and_flee_turn_system.md](file:///d:/GitHub/Wonderland-Private-Server/docs/pve_battle_pet_and_flee_turn_system.md).
- Pure Binary Npc.dat Decoding Engine in [docs/pure_binary_npc_dat_resolver.md](file:///d:/GitHub/Wonderland-Private-Server/docs/pure_binary_npc_dat_resolver.md).
- NPC Name Resolver & Template Directory GUI Studio in [docs/npc_name_resolver_gui.md](file:///d:/GitHub/Wonderland-Private-Server/docs/npc_name_resolver_gui.md).
- Companion Pet Dismissal & Release System (AC 15:2) in [docs/pet_dismissal_system.md](file:///d:/GitHub/Wonderland-Private-Server/docs/pet_dismissal_system.md).
- NPC & Event Mapping Integrity and Teleportation Isolation in [docs/npc_event_mapping_and_teleport_isolation.md](file:///d:/GitHub/Wonderland-Private-Server/docs/npc_event_mapping_and_teleport_isolation.md).
