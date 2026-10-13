# Complete Client Engine and GM Suite Reverse Engineering Specification

## 1. Executive Summary & Architectural Reality

Through deep reverse engineering using the REA framework, Ghidra decompilation analysis (`aLogin_decompiled.c`, 518,522 lines), and Delphi binary resource inspection (`RT_RCDATA` DFM parsing), the operational topology of the Wonderland Online: Rhodes Island client binaries has been resolved:

```
+-----------------------------------------------------------------------------------+
|                                 CLIENT TOPOLOGY                                   |
+-----------------------------------------------------------------------------------+
|                                                                                   |
|  [ Main.exe ] (1.49 MB)                                                           |
|    - Patcher & Launcher (WinHttp HTTP client, TPackageUpdater)                     |
|    - Updates asset packages via PackagesVersion_z.dat and web0.DAT                |
|    - Configures graphics resolution and color depth (TSetScreenFM)                |
|    - Dispatches aLogin.exe via WinExec / ShellExecute                             |
|                                                                                   |
|  [ aLogin.exe ] (5.38 MB, 9,451 Subroutines)                                      |
|    - Monolithic Client Engine (DirectDraw DXDraw1, DirectSound DXSound1, D3DRM)   |
|    - Built-in Server Selector, Account Login, and Character Management            |
|    - 81 In-Game Functional Forms (AutoPlayerPlus botting, Alchemy, Banking, etc.) |
|    - Complete Hidden Developer & GM Administration Suite (TGmForm / CY_GmForm)    |
|    - Automated In-Game Event State Machines (Trojan War, Battle Royale, PVP)     |
|                                                                                   |
+-----------------------------------------------------------------------------------+
```

---

## 2. In-Game Form Architecture (81 Discovered Forms)

The client contains 81 in-game forms dynamically invoked via the engine's form dispatch method `FUN_001d6294` / `ShowForm`:

### 2.1 Automation & Quality of Life
| Form Identifier | Engine Handler | Functionality |
|---|---|---|
| `Form_AutoPlayerPlus_1` | `FUN_001d6294` | Built-in Auto-Combat & Botting (HP/SP item thresholds, auto-targeting) |
| `Form_AutoPlayerPlus_2` | `FUN_001d6294` | Auto-Player secondary skill and companion pet combat automation |
| `Form_ExpBox` | `FUN_001d6294` | Experience container and booster multiplier controller |
| `Form_Magnet_1` | `FUN_001f62d4` | Item collection magnet tool |

### 2.2 Economy, Trade & Vending
| Form Identifier | Engine Handler | Functionality |
|---|---|---|
| `Form_ATM_List` | `FUN_0021b57c` | In-game banking system: gold deposit, withdrawal, and balance audit |
| `Form_BuyItemStall_1` | Engine Dispatch | Player item vending stall |
| `Form_BuyPet1Stall_1` .. `_3` | Engine Dispatch | Player companion pet vending stalls (3 concurrent slots) |
| `Form_ThingStore_1` / `Form_EquipStore_1` | Engine Dispatch | NPC item vault and equipment storage |
| `Form_ThrowMoney` | Engine Dispatch | Ground gold coin drop / throw interface |
| `Form_ExchangeLockChang` | Engine Dispatch | Secondary secure trading lock PIN modification |

### 2.3 Crafting, Housing & Tent Engine
| Form Identifier | Engine Handler | Functionality |
|---|---|---|
| `Form_Compound_1` | `FUN_00235340` | Alchemy item compounding and grade calculation |
| `Form_MakeThing_1` | `FUN_001f62d4` | Tent manufacturing machine and tool production |
| `Form_blueprint_1` | `FUN_001f62d4` | Architectural tent blueprint crafting recipes |
| `Form_HomeAdorn_1` | Engine Dispatch | Tent furniture layout, interior decorating, and positioning |
| `Form_Cupboard_1` / `Form_Cupboard_2` | Engine Dispatch | Tent storage cabinets and furniture item slots |

### 2.4 Social, Guild & Communication
| Form Identifier | Engine Handler | Functionality |
|---|---|---|
| `Form_ArmyGrounp_1` | Engine Dispatch | Guild / Army organization roster and hierarchy |
| `Form_ArmyGrounpInfo_1` | Engine Dispatch | Organization profile, emblem, and level metrics |
| `Form_OrgNotice` | Engine Dispatch | Guild Bulletin board and message of the day (MOTD) |
| `Form_ChooseCooperation` / `Form_CreateCooperation` | Engine Dispatch | Multi-guild alliance management |
| `Form_BlackBoard_1` / `Form_BlackBoard_2` | Engine Dispatch | Public town blackboard message posting system |
| `form_Mail` / `form_Mailplus` | Engine Dispatch | In-game postal system supporting text and item attachments |

### 2.5 Combat, Progression & Minigames
| Form Identifier | Engine Handler | Functionality |
|---|---|---|
| `Form_BattleSkill_1` | Engine Dispatch | Turn-based combat skill selection and target grid |
| `Form_SkillTree` / `form_npcSkillTree` | Engine Dispatch | Player and Pet skill progression tree |
| `form_NPCUpgrade` | Engine Dispatch | Pet rebirth, stat transcendence, and companion upgrade |
| `Form_CreateDupMis` | `FUN_0019de44` | Duplicate Mission / Instance dungeon generation |
| `Form_EightTwelve` | `FUN_001aed4c` | 8-12 Dice gambling minigame |
| `Form_Ante` | Engine Dispatch | Arena betting and wager placement |
| `Form_Capsulem` | `FUN_0022bb00` | Gacha capsule toy vending system |
| `Form_setauthority_1` | Engine Dispatch | Runtime GM permission and privilege modifier |

---

## 3. Comprehensive GM Administration Suite (`TGmForm` / `CY_GmForm`)

Stored in `RT_RCDATA` at offset `5361508` (12,781 B) with published method table at offset `1206000`, `TGmForm` contains 56 distinct operational tools:

### 3.1 Player Moderation & Punishment
| Button / Handler | Method RVA | Caption (Big5) | English Translation | Action |
|---|---|---|---|---|
| `Button5Click` | `0x00137a64` | `查玩家` | Inspect Player | Queries player character stats, level, inventory, and location |
| `Button6Click` | `0x00137a68` | `改名` | Rename Player | Forces a character name change on target account |
| `Button8Click` | `0x00137b2c` | `關人` | Imprison Player | Transports offending player to GM Jail (`Scene 10001`) |
| `Button13Click` | `0x00137b64` | `放人` | Release Player | Releases player from GM Jail to default respawn coordinates |
| `Button9Click` | `0x00137b30` | `抓玩家` | Summon Player | Pulls target player directly to GM's current spatial coordinates |
| `Button38Click` | `0x00137c14` | `到玩家` | Teleport to Player | Teleports GM directly to target player's position |
| `Button10Click` | `0x00137b58` | `斷線` | Kick Player | Sends disconnect notice and terminates client TCP connection |
| `Button21Click` | `0x00137b84` | `強斷線` | Force Kick | Hard socket termination without teardown handshake |
| `Button33Click` | `0x00137bd8` | `禁言` | Mute Player | Suppresses player message broadcasting across all channels |
| `Button34Click` | `0x00137bdc` | `解禁言` | Unmute Player | Restores chat permissions for target player |
| `Button35Click` | `0x00137be0` | `禁名單` | Mute List | Displays table of currently muted player IDs |

### 3.2 Security, Anti-Bot & Account Management
| Button / Handler | Method RVA | Caption (Big5) | English Translation | Action |
|---|---|---|---|---|
| `Button15Click` | `0x00137b6c` | `鎖單一角色` | Lock Character | Freezes target character slot without locking entire account |
| `Button18Click` | `0x00137b78` | `解單一角色` | Unlock Character | Unfreezes target character slot |
| `Button30Click` | `0x00137bcc` | `鎖帳號` | Ban Account | Sets account ban flag in persistence layer |
| `Button31Click` | `0x00137bd0` | `解帳號` | Unban Account | Removes ban flag from account |
| `Button19Click` | `0x00137b7c` | `鎖單一名單` | Banlist | Manages active account and character ban registries |
| `Button20Click` | `0x00137b80` | `外掛場` | Botting Arena | Teleports suspected automated bots to verification arena |
| `Button37Click` | `0x00137bec` | `問問題` | Anti-Bot Quiz | Dispatches interactive verification prompt to player |
| `Ans1` .. `Ans4` | `0x00137c00` | `答1:` .. `答4:` | Quiz Answers | Displays correct anti-bot answer keys for validation |
| `GMGetSaveLockPwdClick`| `0x00138190` | `查交易鎖密碼` | Get Storage PIN | Decrypts and displays user's secondary storage lock PIN |
| `Button56Click` | `0x0013817c` | `Login Drifter` | Login Drifter | Developer authentication bypass for unregistered testing |
| `Button45Click` | `0x00138174` | `查玩家IP` | Check Player IP | Displays remote client IPv4 address and ISP routing info |

### 3.3 Spatial Navigation & Battle Observation
| Button / Handler | Method RVA | Caption (Big5) | English Translation | Action |
|---|---|---|---|---|
| `Button47Click` | `0x00137f60` | `瞬移` | Coordinate Teleport | Teleports GM to explicit `SceneNum`, `X`, and `Y` coordinates |
| `TreeView1DblClick` | `0x00137b28` | Scene Tree | Scene Explorer | Hierarchical map treeview for instant zone warping |
| `Button17Click` | `0x00137b74` | `觀戰` | Spectate Battle | Infiltrates active turn-based combat instance as spectator |
| `CheckBox1` | - | `連續觀戰` | Continuous Spectate | Automatically follows target player into every battle instance |
| `CheckBox2` | - | `連續跟蹤` | Continuous Stalk | Automatically tracks player across map scene transitions |
| `Button23Click` | `0x00137b8c` | `Photo` / `拍照目錄` | Remote Screenshot | Captures client screen buffer and writes to `Photo\` directory |

### 3.4 Chat & Broadcast Channel Bitmasks
The GM console exposes bitmask toggles configuring transmission scope:
* `Bit 0 (1)`: Local (`全`)
* `Bit 1 (2)`: Whisper (`輕`)
* `Bit 2 (4)`: Private / Secret (`密`)
* `Bit 3 (8)`: Party / Team (`隊`)
* `Bit 4 (16)`: Organization / Guild (`組`)
* `Bit 5 (32)`: Alliance (`盟`)
* `Bit 6 (64)`: GM Privilege (`GM(64)`)

---

## 4. Built-in Server Events Discovered in Decompiled Source

String cross-referencing in [`aLogin_decompiled.c`](file:///D:/GitHub/ghidradecodeyapma/aLogin_decompiled.c) confirms four automated server events natively supported by client dialogs:

1. **Interserver PVP:**
   * Announcement: `"Interserver PVP begins in 5 minutes. Players LV30+ can go to..."`
   * Logic: Disables party formation (`"No teams in Interserver PVP"`), handles cross-server routing.
2. **Battle Royale:**
   * Announcement: `"Battle Royale has begun. Players LV10+ can go to Capitol Building 4F in Welling Village"`
   * Warning: `"Battle Royale will end in 10 minutes!"`
3. **Trojan War:**
   * Announcement: `"Trojan War has begun. Go to the Castle and join the battle!"`
   * Logic: Organization territory siege battle.
4. **Welling Village Quiz Event:**
   * Announcement: `"Quiz Event has begun! Come to Capitol Building 4F in Welling Village at server branch 1 to join!"`
   * Requirement: Branch 1 channel routing (`"Quiz is at server branch 1"`).

---

## 5. Client File System and Data Asset Reference

```
D:\GitHub\ghidradecodeyapma\WLRI\
  ├── aLogin.exe             (5.38 MB, Main game engine & GM suite)
  ├── Main.exe               (1.49 MB, WinHttp patcher & display settings)
  ├── SERVER.INI             (Channel cluster definitions: ID / 100)
  ├── Sinfo.txt              (Display configuration: WindowScreen, 60 FPS)
  ├── Finfo.txt              (Fullscreen flag: ON / OFF)
  ├── web0.DAT               (HTTP update endpoints: Hong Kong & Taiwan CDN)
  ├── ISP0.DAT               (FTP update mirrors: port 21 anonymous)
  ├── PackagesVersion_z.dat  (Asset package manifest)
  └── data\
      ├── odd.dat            (1.42 GB master asset package)
      ├── Ground.MMG         (21.9 MB map tile textures)
      ├── Eve.emg            (5.16 MB cutscene and event animations)
      ├── Talk.dat           (5.10 MB quest dialogues and NPC scripts)
      ├── Item.dat           (3.28 MB item database)
      ├── SkillData.MBTM     (1.24 MB skill combat effects)
      ├── Mark.dat           (1.19 MB guild emblem textures)
      ├── Npc.dat            (680 KB NPC entity definitions)
      ├── SceneData.dat      (153 KB scene geometry & collision bounds)
      ├── Skill.dat          (141 KB skill mechanics database)
      ├── Compound.dat       (10.9 KB alchemy compounding table)
      ├── Compound2.Dat      (51.2 KB expanded alchemy formulas)
      └── Formula.dat        (Formula coefficients)
```

---

## 6. Implementation Guidance for Wonderland-Private-Server

1. **AutoPlayer Compatibility:** Server must recognize auto-combat action codes emitted when `Form_AutoPlayerPlus_1` is active.
2. **GM Action Codes:** The private server's GM packet handler (`ActionCode 0x14` / `0x32`) must support all 56 actions exposed in `TGmForm` (jail, summon, inspect, unban, question push).
3. **Storage Lock PIN:** Ensure secure hashing for secondary bank passwords so GM secondary PIN retrieval (`GMGetSaveLockPwd`) returns expected plaintext or decryptable token.
4. **Channel Branches:** Support sub-branches (channel % 100) as expected by `SERVER.INI` and Quiz event routing.

---

## 7. Deep Decompiled Technical Specifications Catalog

For granular source-level subroutines, memory offsets, and protocol packets, refer to the full specification suite in [`decompiled/`](file:///D:/GitHub/Wonderland-Private-Server/decompiled):

1. [`01 - Client Architecture and Monolithic Engine`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/01_client_architecture_and_monolithic_engine.md)
2. [`02 - Complete GM Studio and CY_GmForm`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/02_complete_gm_studio_and_cy_gmform.md)
3. [`03 - In-Game Form Systems and Automation`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/03_in_game_form_systems_and_automation.md)
4. [`04 - Network Framing, Packet Dispatchers, and Cryptography`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/04_network_framing_packet_dispatchers_and_crypto.md)
5. [`05 - Actor Memory Layout and Spatial Systems`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/05_actor_memory_layout_and_spatial_systems.md)
6. [`06 - Binary Asset Pipeline and Scene Parsers`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/06_binary_asset_pipeline_and_scene_parsers.md)
7. [`07 - Pet Companion Lifecycle and AI Modes`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/07_pet_companion_lifecycle_and_ai_modes.md)
8. [`08 - Quest Journal, Dialogue Engine, and Talk Forms`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/08_quest_journal_dialogue_engine_and_talk_forms.md)
9. [`09 - Login Authentication, Session Security, and Channels`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/09_login_authentication_session_security_and_channels.md)
10. [`10 - Item Mall (Nesne Market) and Currency Transactions`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/10_item_mall_nesne_market_and_currency_transactions.md)
11. [`11 - Client Save Configuration and Chat Channel Constraints`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/11_client_save_configuration_and_chat_channel_constraints.md)
12. [`12 - Trade, Shop, Inventory, and Alchemy Materials`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/12_trade_shop_inventory_and_alchemy_materials.md)
13. [`13 - Combat, PK Battlefield, and Encounter Validation`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/13_combat_pk_battlefield_and_encounter_validation.md)
14. [`14 - Mini-Games and Entertainment Systems`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/14_minigames_and_entertainment_systems.md)
15. [`15 - Marriage, Divorce, and Transform Systems`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/15_marriage_divorce_and_transform_systems.md)
16. [`16 - Instance Dungeon, PVP Rankings, and Server Events`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/16_instance_dungeon_pvp_rankings_and_events.md)
17. [`17 - Forge Enhancement and Equipment Potential`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/17_forge_enhancement_and_equipment_potential.md)
18. [`18 - Multimedia DirectX Palettes and Sound Assets`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/18_multimedia_directx_palettes_and_sound_assets.md)
19. [`19 - Exhaustive Client Action Code Matrix & Protocol Dispatchers`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/19_exhaustive_client_action_code_matrix.md)
20. [`20 - Complete 9,451 Function Inventory and Subsystem Catalog`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/20_complete_9451_function_inventory_catalog.md)
21. [`21 - Binary Asset Formats and Database Structures Specification`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/21_binary_asset_formats_and_database_structures.md)
22. [`22 - Main.exe Patcher, HTTP/FTP Update Engine & Archive Extractor`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/22_main_exe_patcher_http_updater_and_archive_extractor.md)
23. [`23 - Character Rebirth Classes, Stat Layout & Party Memory Architecture`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/23_character_rebirth_classes_stat_layout_and_party_memory.md)
24. [`24 - Server Announcement Broadcast and Festival Event Engine`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/24_server_announcement_broadcast_and_festival_event_engine.md)
25. [`25 - Item Material Chemistry and Alchemy Classification Matrix`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/25_item_material_chemistry_and_alchemy_classification_matrix.md)
26. [`26 - Sprite Heading Direction & Motion State Resolver`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/26_sprite_heading_direction_and_motion_state_resolver.md)
27. [`27 - Delphi Unit Architecture, Package Manifest & Modular Source Hierarchy`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/27_delphi_unit_architecture_and_modular_source_hierarchy.md)
28. [`28 - Vehicle Transport, Navigation GPS & Traffic Setting Mechanics`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/28_vehicle_transport_gps_and_traffic_setting_mechanics.md)
29. [`29 - Weather Particle Engine and Ambient Scene Effects`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/29_weather_particle_engine_and_ambient_scene_effects.md)
30. [`30 - Mini-Games State Machines and Arcade Systems Specification`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/30_minigames_state_machines_and_arcade_systems.md)
31. [`31 - Guild Organization Hierarchy and Emblem System`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/31_guild_organization_hierarchy_and_emblem_system.md)
32. [`32 - Inn Lodging, Rest and Recovery Mechanics`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/32_inn_lodging_rest_and_recovery_mechanics.md)
33. [`33 - Character Creation, Appearance Engine, and Palette Customization`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/33_character_creation_appearance_and_palette_customization.md)
34. [`34 - Postal Mail, Express Courier, and Parcel Delivery System`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/34_postal_mail_express_courier_and_parcel_delivery_system.md)
35. [`35 - Turn-Based Co-op Battle Assembly and Chamber Lobby`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/35_turn_based_coop_battle_assembly_and_chamber_lobby.md)
36. [`36 - Tent Housing, Camping Deployment, and Furniture Layout`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/36_tent_housing_camping_deployment_and_furniture_layout.md)
37. [`37 - Item Compound, Alchemy Synthesis, and Grade Formulas`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/37_item_compound_alchemy_synthesis_and_grade_formulas.md)
38. [`38 - Carnival Minigame Training Matrix and Mario Platformer Engine`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/38_carnival_minigame_training_matrix_and_mario_platformer.md)
39. [`39 - Church Charity Donation, Divine Blessings, and Karma Engine`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/39_church_charity_donation_divine_blessings_and_karma.md)
40. [`40 - Equipment Durability, Combat Wear, and Repair Mechanics`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/40_equipment_durability_combat_wear_and_repair_hammers.md)
41. [`41 - Gobang (Gomoku) Chess Board Game and Wager Engine`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/41_gobang_gomoku_chess_board_game_and_wager_engine.md)
42. [`42 - Companion Pet Riding, Mount Anchors, and Saddle Mechanics`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/42_companion_pet_riding_mount_anchors_and_saddle_mechanics.md)
43. [`43 - Fishing, Aquaculture, Bait Consumption, and Catch Tables`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/43_fishing_aquaculture_bait_consumption_and_catch_tables.md)
44. [`44 - Hot Spring Bathing, Vitality Recovery, and Towel Mechanics`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/44_hot_spring_bathing_vitality_recovery_and_towel_mechanics.md)
45. [`45 - Casino Gaming Hall, Slot Machines, and Token Exchange`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/45_casino_gambling_slot_machines_and_token_exchange.md)
46. [`46 - ATM Banking, Vault Deposits, and Currency Remittance`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/46_atm_banking_vault_deposit_and_currency_remittance.md)
47. [`47 - Interactive Cursor State Machine and Context Targeting`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/47_interactive_cursor_state_machine_and_context_targeting.md)
48. [`48 - Quest Journal, Task Tracker, and Multi-Objective Engine`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/48_quest_journal_task_tracker_and_multi_objective_engine.md)
49. [`49 - DirectDraw Multimedia Rendering Loop and Surface Pipelines`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/49_directdraw_multimedia_rendering_loop_and_surface_pipelines.md)
50. [`50 - Master Reverse Engineering Summary and Emulator Architecture`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/50_master_reverse_engineering_summary_and_emulator_architecture.md)
51. [`51 - Skill Tree, Elemental Affinity, and Learning Mechanics`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/51_skill_tree_elemental_affinity_and_learning_mechanics.md)
52. [`52 - Army Group (Guild Organization) and Authority Hierarchy`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/52_army_group_guild_organization_and_authority_hierarchy.md)
53. [`53 - Player Stall Vending and Pet Consignment Bazaar`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/53_player_stall_vending_and_pet_consignment_bazaar.md)
54. [`54 - Secondary Password Security and Account PIN Vault`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/54_secondary_password_security_and_account_pin_vault.md)
55. [`55 - Character Role Status Sheets and Attribute Recalculation`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/55_character_role_status_sheets_and_attribute_recalculation.md)
56. [`56 - Instance Dungeon (Duplicate Mission) Stage Controller`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/56_instance_dungeon_duplicate_mission_stage_controller.md)
57. [`57 - Community Blackboard Bulletin and Message Posting System`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/57_community_blackboard_bulletin_and_message_posting_system.md)
58. [`58 - Instant Messenger (ICQ) and Bilateral Friend Roster`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/58_instant_messenger_icq_and_bilateral_friend_roster.md)
59. [`59 - Player Title Management and Achievement Honor Engine`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/59_player_title_management_and_achievement_honor_engine.md)
60. [`60 - Carnival Tabletop: Eight-Twelve, Magnet, and EXP Box Engines`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/60_carnival_tabletop_eight_twelve_magnet_and_exp_box_engines.md)
61. [`61 - Activity Exchange, Token Redemption, and Merchant Broker System`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/61_activity_exchange_token_redemption_and_merchant_broker_system.md)
62. [`62 - Express Courier, Parcel Delivery, and Consignment Freight System`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/62_express_courier_parcel_delivery_and_consignment_freight_system.md)
63. [`63 - Tent Cupboard, Furniture Storage, and Housing Vault Engine`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/63_tent_cupboard_furniture_storage_and_housing_vault_engine.md)
64. [`64 - Cooperation Battle Assembly and Challenge Chamber Controller`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/64_cooperation_battle_assembly_and_challenge_chamber_controller.md)
65. [`65 - Hotkey Macro Dock and Action Shortcut Runtime Engine`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/65_hotkey_macro_dock_and_action_shortcut_runtime_engine.md)
66. [`66 - Tent Interior Wallpaper, Flooring, and Aesthetic Adornment Engine`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/66_tent_interior_wallpaper_flooring_and_aesthetic_adornment_engine.md)
67. [`67 - Character Emote, Visual Gesture, and Social Expression System`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/67_character_emote_visual_gesture_and_social_expression_system.md)
68. [`68 - Party Group Assembly, Formation Tactics, and EXP Sharing Engine`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/68_party_group_assembly_formation_tactics_and_exp_sharing_engine.md)
69. [`69 - Interactive NPC Dialogue Tree and Scripted Branching Controller`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/69_interactive_npc_dialogue_tree_and_scripted_branching_controller.md)
70. [`70 - Wager Ante Stake and Street Gold Tossing Subsystem`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/70_wager_ante_stake_and_street_gold_tossing_subsystem.md)
71. [`71 - Client System Configuration, Audio-Visual, and Preferences Engine`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/71_client_system_configuration_audio_visual_and_preferences_engine.md)
72. [`72 - Postal Mailbox, Offline Letter Dispatch, and Stamp Tariff Subsystem`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/72_postal_mailbox_offline_letter_dispatch_and_stamp_tariff_subsystem.md)
73. [`73 - Character Deletion, Security PIN Challenge, and Safe Disposal Architecture`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/73_character_deletion_security_pin_challenge_and_safe_disposal_architecture.md)
74. [`74 - Guild Code of Conduct, Bylaws, and Organization Rule Editor`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/74_guild_code_of_conduct_bylaws_and_organization_rule_editor.md)
75. [`75 - Member Point Card Recharge, Serial Key Verification, and Billing Gateway Bridge`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/75_member_point_card_recharge_and_billing_gateway_bridge.md)
76. [`76 - Church Charity Tithe, Donation Box, and Legion Resource Storage System`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/76_church_charity_tithe_and_legion_resource_donation_box.md)
77. [`77 - NPC Vendor Liquidation, Sell-Back Ratios, and Buyback Safeguards`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/77_npc_vendor_liquidation_sell_back_ratios_and_buyback_safeguards.md)
78. [`78 - Floating HUD Dock, System Help Guide, and Blacklist Privacy Manager`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/78_floating_hud_dock_system_help_guide_and_blacklist_manager.md)
79. [`79 - Inn Lodging, Bed Resting, and HP/SP Full Restoration Engine`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/79_inn_lodging_bed_resting_and_hp_sp_full_restoration_engine.md)
80. [`80 - Milestone Capstone 2: Complete Client UI Form Inventory, Component Hierarchy, and Delphi Package Architecture`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/80_milestone_capstone_2_client_ui_form_inventory_and_vcl_architecture.md)
81. [`81 - Companion Pet Rebirth Ascension, Reincarnation Stats, and NPC Upgrade System`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/81_companion_pet_rebirth_ascension_and_npc_upgrade_system.md)
82. [`82 - UFO Arcade Claw Machine Catcher Kinematics, Collision Physics, and Prize Dispenser Engine`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/82_ufo_arcade_claw_machine_catcher_kinematics_and_physics_engine.md)
83. [`83 - Underground Treasure Digging Excavation and Geological Mining Engine`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/83_underground_treasure_digging_excavation_and_geological_mining_engine.md)
84. [`84 - Rhythm Boxing Punching Power Meter and Carnival Combat Minigame`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/84_rhythm_boxing_punching_power_meter_and_carnival_combat_minigame.md)
85. [`85 - Carnival Lottery Wheel, Lotto Ball Physics, and Pyrotechnic Fireworks System`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/85_carnival_lottery_wheel_lotto_ball_physics_and_pyrotechnic_fireworks.md)
86. [`86 - Memory Card Matching Poker Concentration Minigame Engine`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/86_memory_card_matching_poker_concentration_minigame_engine.md)
87. [`87 - Tent Maid and Butler Customization, Wardrobe, and Housing Automation Engine`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/87_tent_maid_and_butler_customization_and_housing_automation.md)
88. [`88 - Dynamic Scene Lighting, Day/Night Cycle, and Alpha Shadow Blitter Engine`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/88_dynamic_scene_lighting_day_night_cycle_and_alpha_shadow_engine.md)
89. [`89 - DirectDraw 16-Bit Display Mode Adapter, Surface Allocation, and Clipper Subsystem`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/89_directdraw_16bit_display_mode_adapter_and_surface_clipper.md)
90. [`90 - Milestone Capstone 3: Master Opcode Protocol and Packet Cryptography Handbook`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/90_milestone_capstone_3_master_opcode_protocol_and_packet_cryptography_handbook.md)
91. [`91 - In-Battle Skill Selection Palette, SP Calculation, and Tactical Radial Targeting Engine`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/91_in_battle_skill_selection_palette_and_tactical_radial_targeting.md)
92. [`92 - Combat Abnormal Status Condition, Buff/Debuff System, and Turn Decay Engine`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/92_combat_abnormal_status_condition_buff_debuff_and_turn_decay_engine.md)
93. [`93 - NPC Vendor Bulk Procurement, Stack Limits, and Purchase Quantity Selector`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/93_npc_vendor_bulk_procurement_and_purchase_quantity_selector.md)
94. [`94 - Pet Consignment Bazaar, Multi-Tier Stall Showcase, and Purchase Engine`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/94_pet_consignment_bazaar_stall_showcase_and_purchase_engine.md)
95. [`95 - In-Game Account Password Modification and Credential Rotation Subsystem`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/95_in_game_account_password_modification_and_credential_rotation.md)
96. [`96 - Multi-Stage Character Creation Wizard, Palette Tinting, and Attribute Allocation Engine`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/96_character_creation_wizard_palette_tinting_and_attribute_allocation.md)
97. [`97 - Player Furniture and Utensil Stall Consignment Vending Engine`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/97_player_furniture_and_utensil_stall_consignment_system.md)
98. [`98 - Gacha Capsule Vending Machine, Crank Kinematics, and Prize Drop Chute Engine`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/98_gacha_capsule_vending_machine_and_mystery_gift_box_engine.md)
99. [`99 - Guild Headquarters Notebook, Member Roster, 10x10 Pixel Emblem Canvas, and Notice Dispatcher`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/99_guild_headquarters_roster_10x10_emblem_canvas_and_notice_board.md)
100. [`100 - Milestone Capstone 4: The Complete 100-Specification Grand Master Compendium and Autonomous Private Server Implementation Manual`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/100_milestone_capstone_4_grand_master_compendium_and_implementation_manual.md)
