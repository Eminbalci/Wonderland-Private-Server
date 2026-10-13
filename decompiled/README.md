# Wonderland Online: Rhodes Island Decompiled Specifications Catalog

## Overview

This directory contains reverse engineering findings, decompiled subroutine analyses, binary structures, memory layouts, and protocol specifications extracted from [`aLogin.exe`](file:///D:/GitHub/ghidradecodeyapma/WLRI/aLogin.exe) (5.38 MB, 9,451 subroutines) and [`Main.exe`](file:///D:/GitHub/ghidradecodeyapma/WLRI/Main.exe) (1.49 MB) using the REA (`morluto/rea`) framework and Ghidra 12.1.2 decompiler analysis.

---

## Documentation Index

1. [**01 - Client Architecture and Monolithic Engine**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/01_client_architecture_and_monolithic_engine.md)
   * Monolithic client topology (`aLogin.exe` as primary game client vs `Main.exe` as HTTP patcher and graphics configurator).
   * PE32 section headers, Delphi VCL form architecture, DirectDraw renderer (`DXDraw1`), DirectSound (`DXSound1`), Direct3D Retained Mode (`D3DRM.dll`), and third-party media runtimes.

2. [**02 - Complete GM Studio and CY_GmForm**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/02_complete_gm_studio_and_cy_gmform.md)
   * Complete reverse engineering of the 56-button hidden GM administration suite (`TGmForm` / `CY_GmForm` at offset `5361508` in `RT_RCDATA`).
   * Published method RVAs, original Big5 Chinese captions, English translations, and operational mechanics (jail, summon, inspect, coordinate warp, remote screenshot, secondary safe lock PIN retrieval, and developer login bypass).
   * Chat and broadcast privilege bitmask flags (1 to 64).

3. [**03 - In-Game Form Systems and Automation**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/03_in_game_form_systems_and_automation.md)
   * Complete catalog of the 81 in-game forms discovered in engine dispatchers (`FUN_001d6294`, `FUN_00235340`, `FUN_0022bb00`, `FUN_0019de44`).
   * Built-in auto-combat botting (`Form_AutoPlayerPlus_1/2`), alchemy compounding (`Form_Compound_1`), gacha capsule system (`Form_Capsulem`), banking (`Form_ATM_List`), instance dungeons (`Form_CreateDupMis`), 8-12 dice gambling (`Form_EightTwelve`), tent manufacturing (`Form_MakeThing_1`, `Form_blueprint_1`), companion rebirth (`form_NPCUpgrade`), and player vending stalls.

4. [**04 - Network Framing, Packet Dispatchers, and Cryptography**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/04_network_framing_packet_dispatchers_and_crypto.md)
   * Winsock asynchronous TCP communication, Socket Index 1 (Port 6416) and Socket Index 2 (Port 6414).
   * Outgoing packet queue (`DAT_006c9864`), symmetric XOR `0xAD` encryption subroutine (`FUN_00153d6c`), TCP framer (`0x44F4` magic header), and primary dispatcher tables (`FUN_00115a38` DirectInput scancodes, `FUN_0010e218`).

5. [**05 - Actor Memory Layout and Spatial Systems**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/05_actor_memory_layout_and_spatial_systems.md)
   * Memory offsets of the local player (`DAT_006c7368`) and scene actor structures (`0x20` X, `0x24` Y, `0x1eec` visibility, `0x1eff` party mode, `0x20b5` fishing, `0x20eb` bathing, `0x20f6` collecting, `0x2231..3` stalls, `0x295c` bank storage).
   * Spatial collision radii and interaction thresholds (200px proximity circle, $\le 169$px NPC dialogue, $< 271$px PK challenge).
   * MapData file loading and XOR decryption key `0x190c` (6412).

6. [**06 - Binary Asset Pipeline and Scene Parsers**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/06_binary_asset_pipeline_and_scene_parsers.md)
   * Master binary scene loader (`FUN_00497c2c` / `FUN_00498208`) decoding `Eve.emg` and `SceneData.dat`.
   * The 19 binary record reader sub-parsers (`Read SceneHead.`, `Read Npc.`, `Read Door.`, `Read Mine.`, `Read Goods.`, `Read Eve.`, `Read Battle.MemberAry[Left/Right]`, `Read Convey.`, `Read Group.`, `Read LeadEve.`).
   * Item Mall dynamic update and synchronization handler (`FUN_0025bd44`).

7. [**07 - Pet Companion Lifecycle and AI Modes**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/07_pet_companion_lifecycle_and_ai_modes.md)
   * Companion memory offsets (`+0x21e8` 4-slot array, `+0x2230` active index, `+0x1efc` summon status, `+0x1eff` riding state, `+0x121` AI behavior mode).
   * Active summoner routine (`FUN_003de310`), AI mode transition mapper (`FUN_003e9898`), and Opcode 15 packet definitions.

8. [**08 - Quest Journal, Dialogue Engine, and Talk Forms**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/08_quest_journal_dialogue_engine_and_talk_forms.md)
   * Dialogue memory registers (`+0x31a` active flag, `+0x31b/c` option sequences, `+0x38c` skin code, `+0x2130/4` balloon coordinates).
   * Form loaders (`FUN_0030859c` for `form_taskview_1`, `FUN_004896f4` for `Form_Talk_1/2/3`, `FUN_0040d550` for `TalkForm1`), and Opcode 39 (0x27) packet frames.

9. [**09 - Login Authentication, Session Security, and Channels**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/09_login_authentication_session_security_and_channels.md)
   * Session structures (`DAT_0071ef58`, `+0x264` socket, `+0x268` GUID 1, `+0x26c` GUID 2, `+0x270` slot, `+0x3c22` login state).
   * Login response handler (`FUN_0033c310`), `SERVER.INI` cluster parser (`FUN_0032f674`), and channel classifier (`FUN_0014c114`).

10. [**10 - Item Mall (Nesne Market) and Currency Transactions**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/10_item_mall_nesne_market_and_currency_transactions.md)
    * Point balance structures (`+0x134` points, `+0x138` selected price, `+0x127` selection flag, `+0x3c` 10 selection slots).
    * Purchase validator subroutine (`FUN_001af620`) and Opcode 34 purchase packet framing.

11. [**11 - Client Save Configuration and Chat Channel Constraints**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/11_client_save_configuration_and_chat_channel_constraints.md)
    * User profile save file `user\save.dat` obfuscated with XOR key `121` (`0x79`).
    * Chat channel RGB color mapping (`ChannelColor1..5`), Radio Set inventory constraint for World channel, and subchannel isolation checks.

12. [**12 - Trade, Shop, Inventory, and Alchemy Materials**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/12_trade_shop_inventory_and_alchemy_materials.md)
    * Trade slots array (`+0x1d0` my 6 slots, `+0x1c0` target 6 slots), item struct fields (`+0xdc` ID, `+0xe0` count, `+0xe4` durability, `+0xfa` amity, `+0xfe` skill ID), and Opcode 20 trade packets.

13. [**13 - Combat, PK Battlefield, and Encounter Validation**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/13_combat_pk_battlefield_and_encounter_validation.md)
    * PK distance delta validation ($< 271$ pixels), state conflict flags (fishing `+0x20b5`, bathing `+0x20eb`, collecting `+0x20f6`, cupid `+0x1f16`, stall `+0x2231..3`, teammate `+0x21bc`), and Opcode 11 combat challenge packets.

14. [**14 - Mini-Games and Entertainment Systems**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/14_minigames_and_entertainment_systems.md)
    * Mini-game engines: Sheep Game (`sheepDreamLife`), Gobang (`Gobang_BG2`), Boxing (`StrengthBarFrame`), Dig Hole (`MiniGame_DigHole_Form`), Lucky Draw, Slot Machine, and Turn Egg Gacha.

15. [**15 - Marriage, Divorce, and Transform Systems**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/15_marriage_divorce_and_transform_systems.md)
    * Marriage ceremony lifecycle (dress requirements, party check, 3-day booking timeout, 7-day divorce remarriage cooldown), and character transformation morph states.

16. [**16 - Instance Dungeon, PVP Rankings, and Server Events**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/16_instance_dungeon_pvp_rankings_and_events.md)
    * Instance dungeon lifecycle (entry gates, 10-minute reward phase, 5-minute evacuation warning, map purging), and PVP Rank 1..4 badges.

17. [**17 - Forge Enhancement and Equipment Potential**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/17_forge_enhancement_and_equipment_potential.md)
    * Equipment forging and enhancement UI (`form_NPCUpgrade`), forge success/failure states, token consumption, and companion pet potential progression limits.

18. [**18 - Multimedia DirectX Palettes and Sound Assets**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/18_multimedia_directx_palettes_and_sound_assets.md)
    * DirectDraw 256-color palette structures (`palVersion = 0x300`), DirectSound audio buffers, and game sound effect wave catalog (`sound\*.wav`).

19. [**19 - Exhaustive Client Action Code Matrix & Protocol Dispatchers**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/19_exhaustive_client_action_code_matrix.md)
    * Comprehensive mapping of all 51 primary Action Codes, 194 distinct opcode:sub-opcode signatures, and 323 client dispatch call sites (`FUN_002d6ad8`).
    * Full opcode matrix cross-referenced with server handlers, payload byte structures, and encryption pipelines.

20. [**20 - Complete 9,451 Function Inventory and Subsystem Catalog**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/20_complete_9451_function_inventory_catalog.md)
    * Complete architectural classification of all 9,451 subroutines (518,522 lines in `aLogin_decompiled.c`) across 15 operational domains.
    * Virtual addresses, line counts, string references, and critical function call graphs for engine subsystems.

21. [**21 - Binary Asset Formats and Database Structures Specification**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/21_binary_asset_formats_and_database_structures.md)
    * Low-level binary layouts, record sizes, header formats, and encryption algorithms for all client data files in `data\` and `user\`.
    * Coverage of `Item.dat`, `Npc.dat` (XOR `0x5209`), `Talk.dat`, `Skill.dat`, `SceneData.dat`, `Eve.emg`, `Ground.MMG`, `Mark.dat`, `SkillData.MBTM`, `Compound.dat`, `Compound2.Dat`, `Formula.dat`, and `save.dat` (XOR `121`).

22. [**22 - Main.exe Patcher, HTTP/FTP Update Engine & Archive Extractor**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/22_main_exe_patcher_http_updater_and_archive_extractor.md)
    * Complete PE32 architecture and VCL forms (`TMainFM`, `TSetScreenFM`, `TLinkToOfficeFM`, `Form2`, `TNMShow`) of `Main.exe` (1.49 MB).
    * Dual-protocol updater (WinHttp HTTP client & NetMasters FTP), manifest synchronization (`PackagesVersion_z.dat`), embedded Gilles Vollant `unzip 0.15` extraction, and process handoff via `WinExec("alogin.exe", SW_SHOWNORMAL)`.

23. [**23 - Character Rebirth Classes, Stat Layout & Party Memory Architecture**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/23_character_rebirth_classes_stat_layout_and_party_memory.md)
    * Complete reverse engineering of the Rebirth Ascension Job System (轉生系統) from `FUN_0028f90c`: Killer, Warrior, Knight, Sage, Priest, Prophet.
    * Memory layout of party member structure in `PTR_DAT_004c9754` (`+0x08` Name, `+0x20` Level, `+0x21` Element, `+0x22` Rebirth Job, `+0x24` HP, `+0x28` Max HP, `+0x2A` SP, `+0x2C` Max SP, `+0xC8` CharID) and Action Code 8:2 replication.

24. [**24 - Server Announcement Broadcast and Festival Event Engine**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/24_server_announcement_broadcast_and_festival_event_engine.md)
    * Complete specification of `FUN_004447b4` and client HUD system announcement dispatcher (`FUN_004a487c`).
    * Full 70+ event sub-opcode matrix spanning Double EXP, New Year's Eve, WLO Anniversary, White Day, Happy May, Jade Rabbit, National Day, Halloween, Trivia Quiz, Team PVP, Valentine's Day, and seasonal festivals.

25. [**25 - Item Material Chemistry and Alchemy Classification Matrix**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/25_item_material_chemistry_and_alchemy_classification_matrix.md)
    * Complete 70-category material taxonomy extracted from subroutine `FUN_0049f998`: Flowers, Grasses, Woods, Grains, Meats, Seafoods, Metals, Precious Gems, Clays, Polymers, Chemicals, and Potability flags.
    * Synthesis rank calculations, cross-category compounding rules, and failure byproduct degradation mechanics.

26. [**26 - Sprite Heading Direction & Motion State Resolver**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/26_sprite_heading_direction_and_motion_state_resolver.md)
    * Complete 8-directional isometric heading system (0..7: S, SW, W, NW, N, NE, E, SE) isolated by `FUN_00440308`.
    * Motion base normalizer `FUN_00440fec` mapping entity composite states `*(actor + 0x121)` into canonical animations: Idle (0), Walk (8), Sit (16), Battle (18), Attacks (26, 28), Magic (30), Defend (32), Hurt (34), Knockout (36), Victory (38), Mounted (42).

27. [**27 - Delphi Unit Architecture, Package Manifest & Modular Source Hierarchy**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/27_delphi_unit_architecture_and_modular_source_hierarchy.md)
    * Comprehensive 280-unit internal source architecture of the engine extracted from `PACKAGEINFO` (RVA `0x887098`) via REA `inspect_pe_resources`.
    * Complete architectural mapping across Binary Asset Units (`AC_*`), Network & Framing (`DZ_*`), Game Systems (`CY_*`, `DNJ_*`, `se_*`), Housing (`RE_*`), Mini-Games (`MZ_*`, `LD_*`), and Multimedia/DirectX drivers (`PDXRender`, `DXDraws`).

28. [**28 - Vehicle Transport, Navigation GPS & Traffic Setting Mechanics**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/28_vehicle_transport_gps_and_traffic_setting_mechanics.md)
    * Complete 29-vehicle taxonomy parsed from `data\TrafficSetting.txt` and driven by units `CH_Traffic`, `nNJ_TrafficData`, `CH_RidePet`, and `se_GPS`.
    * Traversal mediums (1 Land, 2 Water, 3 Air), seating capacity (1, 2, 4-player party), speed bonuses, repair token costs, and global minimap navigation.

29. [**29 - Weather Particle Engine and Ambient Scene Effects**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/29_weather_particle_engine_and_ambient_scene_effects.md)
    * DirectDraw particle simulation systems in `pFT_SceneEffect` and `se_CloudFloat`.
    * Snow particle engine (`FUN_0034f5f8`: 200..500 flakes, 3 sizes `icon_Snow1..3`, lateral drift) and Rain engine (`FUN_003503f4`: 1,200px span, 45-degree slant, 150 streams `icon_Rains`).

30. [**30 - Mini-Games State Machines and Arcade Systems Specification**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/30_minigames_state_machines_and_arcade_systems.md)
    * Complete state machines for arcade and gambling systems in `PACKAGEINFO`: UFO Claw Machine `cMZ_Catcher` (`CatcherBasic` -> `CatcherMoving` -> `CatcherDroping` -> AABB doll hit-test), Rhythm Boxing `MZ_BoxingGame`, Underground Digging `LD_DigHoleGame`, 18 Beans Dice `MR_ShiBaDou`, and Gobang `yRE_Chess`.

31. [**31 - Guild Organization Hierarchy and Emblem System**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/31_guild_organization_hierarchy_and_emblem_system.md)
    * Complete guild systems in `CY_Organ.pas`, `se_ArmyGroup.pas`, and `kAC_Mark.pas`: Form layouts (`Form_ArmyGrounp_1`, `Form_ArmyGrounpInfo_1`), 16x16 pixel emblem editor (`Form_Emblem_1`), leadership rules (`Form_EditRule_1`), authority bitmasks (0x01..0x80), and Action Code 40 (0x28) wire protocol.

32. [**32 - Inn Lodging, Rest and Recovery Mechanics**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/32_inn_lodging_rest_and_recovery_mechanics.md)
    * Complete lodging and vitality recovery engine in `BK_INN.pas`: Form layout (`Form_Inn_1`, 476x472 px), check-in fee calculation formulas, sleeping animation sequence, and Action Code 25 (0x19) wire framing.

33. [**33 - Character Creation, Appearance Engine, and Palette Customization**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/33_character_creation_appearance_and_palette_customization.md)
    * Complete 5-stage character generator in `ft_role_rgb_tool_Type.pas` and `CH_NewRole.pas`: Forms `form_NewRole_1` through `form_NewRole_5`, archetype selection, 6-point bonus stat allocation, elemental affinity, zoom-in/out toggles (`btn_ZoomIn/Out`), RGB palette tint encoding (Hair, Eyes, Skin, Clothes, Boots), name/deletion PIN validation (<= 10 chars), and Action Code 2 (0x02) creation packets.

34. [**34 - Postal Mail, Express Courier, and Parcel Delivery System**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/34_postal_mail_express_courier_and_parcel_delivery_system.md)
    * Complete postal engine in `AC_ICQ.pas` and `BK_Mail.pas`: Form canvas morphing between standard mail `form_Mail` (414x290 px) and parcel express `form_Mailplus` (444x290 px), Cash-on-Delivery (COD) gold escrow, 6-second spam cooldown throttle, attachment claiming, and Action Code 26 (0x1A) wire protocol.

35. [**35 - Turn-Based Co-op Battle Assembly and Chamber Lobby**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/35_turn_based_coop_battle_assembly_and_chamber_lobby.md)
    * Complete cooperative matchmaking and raid staging in `IN_CooperateFight.pas` and `mCH_Fight.pas`: Forms `Form_ChooseCooperation` (463x375 px) and `Form_CreateCooperation` (463x377 px), 4-room lobby browser, player ratio indicators (`Players: %d/%d`), readiness locks, ante stakes (`Form_Ante`), and Action Code 186 (0xBA) & Action Code 20 (0x14) challenge arbitration.

36. [**36 - Tent Housing, Camping Deployment, and Furniture Layout**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/36_tent_housing_camping_deployment_and_furniture_layout.md)
    * Complete private housing engine in `RE_Home.pas`, `RE_Draw.pas`, and `lRE_EveStruct.pas`: Tent pitching mechanics (`FUN_003074b0`), 12x12 isometric diamond grid, multi-floor attic/basement extensions, furniture classes (Storage chests, crafting workstations, rest bathtubs), public/private visitation permissions, and Action Code 12 (0x0C) wire framing.

37. [**37 - Item Compound, Alchemy Synthesis, and Grade Formulas**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/37_item_compound_alchemy_synthesis_and_grade_formulas.md)
    * Complete alchemy synthesis workbench in `jo_MixItem.pas`: `Form_Compound_1` (467x299 px), Junior/Intermediate/Senior Alchemy tiers, catalyst books I..IV, save profile INI state (`MiddleCompoundUse`, `HighCompoundUse`), material chemistry compatibility, item grade jump formulas, and Action Code 24 / 64 network protocol.

38. [**38 - Carnival Minigame Training Matrix and Mario Platformer Engine**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/38_carnival_minigame_training_matrix_and_mario_platformer.md)
    * Complete 19-game carnival attraction and companion training facility matrix dispatched by `FUN_001cf840`: Darts (`Throw_Exp_Form`), Whack-a-Mole (`HitMouse/Rabbit/Turtle`), Hunting (`Hunt_Exp`), Animal Racing (`Pig/Sheep_Exp`), Gashapon (`TrunEgg`), Claw Crane (`Doll_Exp`), Rhythm Boxing (`Boxing_Exp`), Memory Cards (`MemGame_Exp`), Balloon Pop (`Poke`), Pumpkin Catch, Gobang Chess, Slot Machines, and the hidden 2D physics platformer `SMZ_MarioGame.pas` (`MarioGround`, `Mario_Up/Mid/Buttom`, `Mario_Bingo`, 1000ms gravity ticks, Action Code 34 / 74).

39. [**39 - Church Charity Donation, Divine Blessings, and Karma Engine**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/39_church_charity_donation_divine_blessings_and_karma.md)
    * Complete temple offering and charity system in `dSe_donate.pas` and `BK_Donate.pas`: Form layout `form_Donate` (325x422 px), state validation in `FUN_0024f534` (combat, marriage, vendor stalls, forbidden maps), 50-slot bag filtering algorithm `FUN_002504b0`, Karma alignment index calculations, Angelic Blessing buffs, and Action Code 45 (0x2D) Sub-Opcode 4 wire protocol.

40. [**40 - Equipment Durability, Combat Wear, and Repair Mechanics**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/40_equipment_durability_combat_wear_and_repair_hammers.md)
    * Complete durability lifecycle and degradation mechanics in `oCY_Equip.pas` and `BK_Equip.pas`: Gear array offsets (+0x27e8..+0x27fc), combat round wear probabilities (10% weapon, 15% armor), zero-durability penalty alerts (`"Item's [X] durability run out"`), Repair Mallet tool hierarchies (Wood, Bronze, Silver, Gold), vehicle dockyard maintenance costs (`"Repairs cost: "` / `"Vehicle repaired"`), and Action Code 23 (0x17) packets.

41. [**41 - Gobang (Gomoku) Chess Board Game and Wager Engine**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/41_gobang_gomoku_chess_board_game_and_wager_engine.md)
    * Complete turn-based tabletop engine in `yRE_Chess.pas` and `BK_Chess.pas`: 15x15 board texture (`Gobang_BG2`), black/white stone placement (`Gobang_B`, `Gobang_W`), 4-direction raycast victory arbitrator, 30-second turn clocks (`btn_Gobang_T`), 5-tier gold ante betting (`btn_Gobang_A0`..`A4`), match locking (`" in chess game"`, `"Chess suspended"`), and Action Code 74 (0x4A) protocol.

42. [**42 - Companion Pet Riding, Mount Anchors, and Saddle Mechanics**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/42_companion_pet_riding_mount_anchors_and_saddle_mechanics.md)
    * Complete pet mount riding system in `CH_RidePet.pas` and `nNJ_TrafficData.pas`: Configuration parser for `\Data\AdjustRidePetPos.txt` (516 bytes per species entry), directional rider $(X, Y)$ offset coordinates, Knight class passive mount perk (1/5 mount SPD bonus without saddle), amity threshold (>= 60), saddle item requirements, stamina exhaustion limits, and Action Code 15 (0x0F) Sub-Opcode 9 wire protocol.

43. [**43 - Fishing, Aquaculture, Bait Consumption, and Catch Tables**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/43_fishing_aquaculture_bait_consumption_and_catch_tables.md)
    * Complete automatic fishing and gathering engine in `CY_Fish.pas` and `mWS_Operation.pas`: Shoreline merchant dialogs `FormBuyFish` (252x243 px) and `FormDelFish`, water tile hit-testing, client state mutex locking (`"Currently fishing"`, `"Fishing, can't act"`), tackle tiers (Bamboo, Wood, Steel, Auto), bait consumption, 15..30 second bite intervals, catch roll tables (Freshwater, Marine, Algae, Junk), and Action Code 23 (0x17) packets.

44. [**44 - Hot Spring Bathing, Vitality Recovery, and Towel Mechanics**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/44_hot_spring_bathing_vitality_recovery_and_towel_mechanics.md)
    * Complete hot spring bathhouse engine in `se_HotSpring.pas` and `BK_HotSpring.pas`: Geothermal water tile collision triggers, Bath Towel / Yukata item requirements, bathing sitting stance (`actor + 0x121 = 16`), client mutex locks (`"Bathing, unable to make"`, `"Bathing, can't act"`), steaming vapor particle emitters, accelerated +5% Max HP/SP regeneration per 5 seconds, negative status debuff purging (poison, exhaustion), and Action Code 23 (0x17) protocol.

45. [**45 - Casino Gaming Hall, Slot Machines, and Token Exchange**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/45_casino_gambling_slot_machines_and_token_exchange.md)
    * Complete casino gaming hall and gambling systems in `CY_Casino.pas` and `BK_Casino.pas`: `Slotmach_Exp_Form` (525x409 px), 3 slot machine tiers (Bronze, Silver, Gold), 7 reel symbols (Cherry, Lemon, Orange, Watermelon, Bell, BAR, Lucky Seven), 100x Grand Jackpot payouts, gold-to-token cashier exchange (1 Token = 10 Gold), and Action Code 74 (0x4A) protocol.

46. [**46 - ATM Banking, Vault Deposits, and Currency Remittance**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/46_atm_banking_vault_deposit_and_currency_remittance.md)
    * Complete commercial banking and gold storage system in `mWS_Operation.pas` and `BK_ATM.pas`: `Form_ATM_List` (321x228 px), `TransferList` transaction ledger, 400M gold account deposit ceiling (`"ATM deposit limit 400M"`), 1,000 gold flat surcharge fee (`"Pay ATM 1000g fee"`), overflow handling (`"Gold limit overflow, 1000g fee transferred to ATM"`), and Action Code 20 (0x14) protocol.

47. [**47 - Interactive Cursor State Machine and Context Targeting**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/47_interactive_cursor_state_machine_and_context_targeting.md)
    * Complete interactive cursor engine in `jo_Cursor.pas`: Windows Win32 API cursor integration (`SetCursor`, `GetCursorPos`, `ShowCursor`), DirectDraw software cursor rendering, 10 contextual interaction states (Talk balloon, Attack crossed swords, Pickup hand, Portal gear, Inspect lens, Repair anvil, Fishing bobber, Trade scales, Mining pick), and entity raycast priority picking.

48. [**48 - Quest Journal, Task Tracker, and Multi-Objective Engine**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/48_quest_journal_task_tracker_and_multi_objective_engine.md)
    * Complete quest tracking and multi-objective journal in `jo_TaskView.pas` and `BK_TaskView.pas`: `form_taskview_1` (367x432 px), page turning controls (`Arrow_L3`/`Arrow_R3`), multi-objective progress tracking (kills, items, visits, dialogues), quest forfeiture (`Btn_Delete_1`), desktop HUD docked mini-tracker (`btn_Model_1`), and Action Code 39 (0x27) wire protocol.

49. [**49 - DirectDraw Multimedia Rendering Loop and Surface Pipelines**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/49_directdraw_multimedia_rendering_loop_and_surface_pipelines.md)
    * Complete low-level 2D graphics hardware acceleration in `rDXETable.pas`, `PDXRender.pas`, and `DXDraws.pas`: `DDraw.dll` COM interface hooks (`IDirectDraw7`, `IDirectDrawSurface7`, `IDirectDrawPalette`, `IDirectDrawClipper`), 16-bit RGB 565/555 color pipelines, software alpha-blending lookup tables, double buffering page flips, 60 FPS vertical blank sync, and `DSound.dll` DirectSound audio buffers.

50. [**50 - Master Reverse Engineering Summary and Emulator Architecture**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/50_master_reverse_engineering_summary_and_emulator_architecture.md)
    * Comprehensive capstone synthesis of the entire 50-specification reverse engineering campaign: complete architectural mapping across 7 modular tiers, binary asset pipelines, rolling XOR 0xAD framing, 51 Action Codes, and the implementation blueprint for the `Wonderland-Private-Server` C# .NET emulator codebase.

51. [**51 - Skill Tree, Elemental Affinity, and Learning Mechanics**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/51_skill_tree_elemental_affinity_and_learning_mechanics.md)
    * Complete skill tree engine in `se_SkillTree.pas` and `Form_SkillTree` / `form_npcSkillTree`: Elemental opposition cycle restrictions (Earth/Fire, Water/Wind), affinity cost penalty ($2\times$ base cost for non-affinity elements), dual prerequisite node validators, 10-level skill cap, unallocated skill point registers, and Action Code 29 / 45 wire protocol.

52. [**52 - Army Group (Guild Organization) and Authority Hierarchy**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/52_army_group_guild_organization_and_authority_hierarchy.md)
    * Complete guild and legion management in `se_ArmyGroup.pas` and `se_EnsignType.pas`: Multi-page tab dispatcher `FUN_0028815c` (Info, Roster, Emblem, Notice), granular 5-bit authority permissions in `Form_setauthority_1`, 8-color $10\times 10$ block emblem raster engine (`FUN_002888d8`), and Action Code 31 wire protocol.

53. [**53 - Player Stall Vending and Pet Consignment Bazaar**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/53_player_stall_vending_and_pet_consignment_bazaar.md)
    * Complete street vending and companion consignment marketplace in `HL_Shop.pas` and `CY_TradeMenu.pas`: `Form_BuyItemStall_1` (525x409 px), contiguous 39-byte item stride records, 5 active stall slots, 50-stack quantity caps, gold purse limit checks, companion amity rules, and Action Code 20 protocol.

54. [**54 - Secondary Password Security and Account PIN Vault**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/54_secondary_password_security_and_account_pin_vault.md)
    * Complete secondary account protection vault in `Form_ExchangeLockChang` (300x440 px), `Form_InputPw_1`, and `Form_InputPw_2`: 3-stage PIN setup, 10-character ceiling, login password match prevention rule (`"Secure Code & Pwd must not match"`), actor security registers (`+0x5779` active, `+0x577a` locked), and Action Code 20 protocol.

55. [**55 - Character Role Status Sheets and Attribute Recalculation**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/55_character_role_status_sheets_and_attribute_recalculation.md)
    * Complete character sheet, pet sheet, and vehicle status notebook in `DZ_Human.pas` and `DZ_Calculate.pas`: `Form_RoleStatus_1..3`, stat allocation wire codes (`0x1C` STR, `0x1D` CON, `0x1B` INT, `0x21` WIS, `0x1E` AGI), combat rating formulas (HP, SP, ATK, DEF, MATK, MDEF, SPD), Rebirth passive multipliers, and Action Code 8:1 packets.

56. [**56 - Instance Dungeon (Duplicate Mission) Stage Controller**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/56_instance_dungeon_duplicate_mission_stage_controller.md)
    * Complete duplicate mission party instance engine in `MZ_DupMis.pas` and `Form_CreateDupMis` (463x377 px): 131-byte instance record catalog (`PTR_DAT_004c8a0c`), 10-instance page grids, party/instance entry validation, multi-wave mob progression, 300-second victory evacuation timers, and Action Code 85 (0x55) wire protocol.

57. [**57 - Community Blackboard Bulletin and Message Posting System**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/57_community_blackboard_bulletin_and_message_posting_system.md)
    * Complete public announcement and bulletin board system in `mWS_Operation.pas`: `Form_BlackBoard_1` (328x418 px), 44-byte message record strides (Author, Delphi TDateTime double timestamp, 20-char body), 5-slot page hit-testing (`PtInRect`), authorship verification (`"You are not author"`), and Action Code 64 (0x40) protocol.

58. [**58 - Instant Messenger (ICQ) and Bilateral Friend Roster**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/58_instant_messenger_icq_and_bilateral_friend_roster.md)
    * Complete peer-to-peer ICQ messaging and friend management in `AC_ICQ.pas`, `HL_BlackNameList.pas`, and `Kse_BlockMsg.pas`: `Form_Friend_2`, 24-contact friend limit, 24-entry blacklist array, bilateral invitation handshakes, silent spam dropping, and Action Code 14 (0x0E) / Action Code 32 (0x20) protocol.

59. [**59 - Player Title Management and Achievement Honor Engine**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/59_player_title_management_and_achievement_honor_engine.md)
    * Complete honor titles and achievement system in `NJ_PlayerTitleManager.pas`: 256-title database buffer (28 bytes per record), text versus graphic badge classification (`FUN_0016f460`), `"Title_"` bitmap binding (`FUN_0047885c`), multi-actor replication (local actor `+0x21a4`, party member `+0xca`, world actor `+0x21a4`), and Action Code 8 replication.

60. [**60 - Carnival Tabletop: Eight-Twelve, Magnet, and EXP Box Engines**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/60_carnival_tabletop_eight_twelve_magnet_and_exp_box_engines.md)
    * Complete tabletop and arcade attractions in `RE_SGame_BiDaXiao.pas`, `KMK_MiniGame.pas`, and `CH_MiniGame.pas`: Eight-Twelve dice wager engine `Form_EightTwelve` (525x409 px, 10 betting slots, 3-dice probability calculus), Magnet crane treasure hunting `Form_Magnet_1` (399x228 px), experience storage capsule vault `Form_ExpBox` (195x223 px, 1M cap, 10% tariff), and Action Code 74 (0x4A) protocol.

61. [**61 - Activity Exchange, Token Redemption, and Merchant Broker System**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/61_activity_exchange_token_redemption_and_merchant_broker_system.md)
    * Complete bartering and promotional redemption engine in `FUN_00383c10`, `FUN_00383fa4`, `FUN_003840a4`: `Form_ActivityExchange` (320x440 px), `Form_EquipStore_1`, `Form_ThingStore_1`, 65-byte recipe catalog records (`0x41`), up to 5 prerequisite ingredient items, real-time item attribute calculus (`MaxHp`, `MaxSp`, `Shoot RES`), and Action Code 25:42 (`0x19:0x2A`) wire protocol.

62. [**62 - Express Courier, Parcel Delivery, and Consignment Freight System**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/62_express_courier_parcel_delivery_and_consignment_freight_system.md)
    * Complete asynchronous logistics network in `se_Express.pas`: `Form_Express_1` (460x299 px) outbound dispatch depot, `Form_Express_2` (322x228 px) inbound pickup terminal, 50-slot inventory parcel grid (10x5), 5 active courier delivery slots, dynamic weight-based tariff calculus (`FUN_001fc17c`), Cash-on-Delivery (COD) and insurance options, recipient address picker (Friends/Guild), and Action Code 23:81 (`0x17:0x51`) / Action Code 25:21 (`0x19:0x15`) protocol.

63. [**63 - Tent Cupboard, Furniture Storage, and Housing Vault Engine**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/63_tent_cupboard_furniture_storage_and_housing_vault_engine.md)
    * Complete tent furniture container system in `MZ_Cupboard.pas` and `MZ_House.pas`: `Form_Cupboard_1` (155x209 px, 5 slots) and `Form_Cupboard_2` (179x209 px, 10 slots), 53-byte (`0x35`) stored furniture record strides in `PTR_DAT_004c9900` preserving durability, potential grades, and socket gems, capacity scanner `FUN_00209ea8`, and Action Code 62:41 (`0x3E:0x29`) protocol.

64. [**64 - Cooperation Battle Assembly and Challenge Chamber Controller**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/64_cooperation_battle_assembly_and_challenge_chamber_controller.md)
    * Complete challenge lobby browser and room orchestrator in `IN_CooperateFight.pas`: `Form_ChooseCooperation` (463x375 px) 4-room lobby browser with page navigation, `Form_CreateCooperation` (463x377 px) room configuration with challenge tiers and point stakes, 4-person team enforcement (`"Please apply in team of 4 people"`), and Action Code 186 (`0xBA`) wire protocol.

65. [**65 - Hotkey Macro Dock and Action Shortcut Runtime Engine**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/65_hotkey_macro_dock_and_action_shortcut_runtime_engine.md)
    * Complete action shortcut runtime in `se_HotKey.pas` and `Kse_HotKey.pas`: 8-slot action bar (F1..F8) supporting items, skills, mounts, and pet commands, dynamic 3-mode orientation switching (`Form_HotKey_V` 56x274 px, `Form_HotKey_H` 274x56 px, `Form_HotKey_Mini` collapsed dock), context-aware combat/roaming keypress dispatching (`FUN_002aa544`), DirectDraw dropshadow tooltip blitter (`FUN_002a9b88`), and Action Code 23/8 dispatchers.

66. [**66 - Tent Interior Wallpaper, Flooring, and Aesthetic Adornment Engine**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/66_tent_interior_wallpaper_flooring_and_aesthetic_adornment_engine.md)
    * Complete tent surface customizer in `se_HomeAdorn.pas`: `Form_HomeAdorn_1` (223x223 px), 2-floor surface skinning (`Floor 1`, `Floor 2`), 24-byte adornment records in `PTR_DAT_004c9900`, consumable decor scrolls (`Btn_AddNew_1`), tile reorientation (`Btn_Move_1`), canvas strip/revert (`Btn_Remove_1`), and Action Code 61 (`0x3D`) wire protocol.

67. [**67 - Character Emote, Visual Gesture, and Social Expression System**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/67_character_emote_visual_gesture_and_social_expression_system.md)
    * Complete visual gesture and overhead bubble engine in `se_Emotion.pas`: `Form_Emotion_1` (172x240 px) 24-emote matrix (4x6 layout, IDs 101..124), 3000 ms looping overhead animation bubbles (`Icon_Bubble_1`, `Icon_Bubble_2`), audio chirp sync, and Action Code 5:8 (`0x05:0x08`) quadrant broadcast protocol.

68. [**68 - Party Group Assembly, Formation Tactics, and EXP Sharing Engine**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/68_party_group_assembly_formation_tactics_and_exp_sharing_engine.md)
    * Complete 4-player party framework in `se_Team.pas`: `Form_Team_2` (345x240 px) member telemetry roster, follow formation kinematics (`*(actor + 0x207d) = 2`), level-weighted EXP pool formula with 20% group bonus and 20-level discrepancy safeguards, bilateral invitations, leadership delegation, and Action Code 21 (`0x15`) wire protocol.

69. [**69 - Interactive NPC Dialogue Tree and Scripted Branching Controller**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/69_interactive_npc_dialogue_tree_and_scripted_branching_controller.md)
    * Complete dialogue engine in `se_Talk.pas`: `Form_Talk_1` (768x169 px) primary narrative dock, `Form_Talk_2` cutscene skin, `Form_Talk_3` (332x168 px) 4-option branching decision box, animated 100x100 px NPC portraits with blinking/mouth sync (`FUN_00491980`), rich text tag parsing (`#R`, `#B`, `%s`), advance prompt (`Icon_Nextone_1`), and Action Code 6 wire protocol.

70. [**70 - Wager Ante Stake and Street Gold Tossing Subsystem**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/70_wager_ante_stake_and_street_gold_tossing_subsystem.md)
    * Complete currency mobility dual subsystem in `se_Ante.pas` and `se_ThrowMoney.pas`: `Form_Ante` (283x173 px) wager negotiation dialog, `Form_ThrowMoney` (209x87 px) street gold tossing modal, 5-tier physical ground entities (`GdMoney1` through `GdMoney5`), restricted zone checks (tent/hot springs), and Action Code 25:3 (`0x19:0x03`) drop / Action Code 74:2 (`0x4A:0x02`) ante protocols.

71. [**71 - Client System Configuration, Audio-Visual, and Preferences Engine**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/71_client_system_configuration_audio_visual_and_preferences_engine.md)
    * Complete preference engine in `se_System.pas` and `Form_System_2` (451x388 px): 32-byte preferences record in `PTR_DAT_004c96d0`, DirectSound logarithmic millibel attenuation formula (`2000 * log10(Slider/100)`), display toggles, privacy switches, and binary serialization to `user\save.dat`.

72. [**72 - Postal Mailbox, Offline Letter Dispatch, and Stamp Tariff Subsystem**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/72_postal_mailbox_offline_letter_dispatch_and_stamp_tariff_subsystem.md)
    * Complete asynchronous mailbox system in `se_Mail.pas`: `form_Mail` (414x290 px) and `form_Mailplus` (444x290 px), 52-byte mail headers, 50-letter mailbox capacity, 5-row page navigation, 50-gold stamp tariff, Cash-on-Delivery (COD) parcel attachments, and Action Code 23 (`0x17`) wire protocol.

73. [**73 - Character Deletion, Security PIN Challenge, and Safe Disposal Architecture**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/73_character_deletion_security_pin_challenge_and_safe_disposal_architecture.md)
    * Complete character deletion security suite in `se_DeleteRole.pas` and `form_DeleteRole` (299x449 px): 4 password-masked input boxes (Account Password, Deletion PIN, Re-type PIN, Secret Verification), 6-to-10 character constraints, password mismatch safeguard, guildmaster deletion mutex, 7-day safety escrow for LV50+ characters, and Action Code 2 (`0x02`) wire protocol.

74. [**74 - Guild Code of Conduct, Bylaws, and Organization Rule Editor**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/74_guild_code_of_conduct_bylaws_and_organization_rule_editor.md)
    * Complete guild governance text editor in `se_EditRule.pas` and `Form_EditRule_1` (415x347 px): 208-byte guild bylaws records in `PTR_DAT_004c9620`, 200-character multi-line memo editor with `"inputCursor"` caret and `"bar_H4"` vertical scrollbar, officer authority bitmask validation (Bit 0x04: `CAN_EDIT_NOTICE_AND_RULES`), profanity blacklist filtering, and Action Code 31:7 (`0x1F:0x07`) protocol.

75. [**75 - Member Point Card Recharge, Serial Key Verification, and Billing Gateway Bridge**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/75_member_point_card_recharge_and_billing_gateway_bridge.md)
    * Complete billing and point card engine in `se_MemCard.pas`: `Form_MemCard2` (397x371 px) 3-slot character roster, time gauge ratio calculus (`((Tier + 1) * 500,000) / 108`), sealed/unsealed character state flags (`*(actor + 0xb1) = 2`), character unseal button (`Btn_UnCachet_2`), time balance check (`icon_Time_1`), and Action Code 79 (`0x4F`) billing gateway wire protocol.

76. [**76 - Church Charity Tithe, Donation Box, and Legion Resource Storage System**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/76_church_charity_tithe_and_legion_resource_donation_box.md)
    * Complete municipal and legion tithing framework in `se_Donate.pas` and `form_Donate` (422x325 px): 50-slot inventory donation grid (5x10, 36 px stride), Item Type '5' material verification gate (`local_1b9 == '5'`), 7 donation currency edit boxes (`editorBG`), gold adjuster buttons (`btn_Bank_Gold_L/R`), 7 resource attributes (`0x9060`..`0x9067`), legion withdrawal gate (`icon_withDraw`), and Action Code 28:5 (`0x1C:0x05`) protocol.

77. [**77 - NPC Vendor Liquidation, Sell-Back Ratios, and Buyback Safeguards**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/77_npc_vendor_liquidation_sell_back_ratios_and_buyback_safeguards.md)
    * Complete NPC vendor sales engine in `HL_Shop.pas`: Dual-mode sales tabs (`Form_SaleEquip_1` and `Form_SaleThing_1`, 470x310 px), 10-slot batch sales staging queue (`param_1 + 0xf4 + slot * 4`), 50% depreciation price formula modulated by remaining item durability, high-value item safeguard confirmation dialogs, and Action Code 20:6 (`0x14:0x06`) liquidation protocol.

78. [**78 - Floating HUD Dock, System Help Guide, and Blacklist Privacy Manager**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/78_floating_hud_dock_system_help_guide_and_blacklist_manager.md)
    * Complete player guidance and privacy subsystem in `se_Module.pas` and `se_ModulePlus.pas`: Right-docked multimedia tutorial HUD `Form_Module_1` (223x327 px, 4 major categories `0x3E6F`..`0x3E7F`), 10-slot social blacklist manager `form_modulePlus` (350x228 px, `btn_unBlock`, `btn_Block`, `bar_H3`), bilateral whisper/trade suppression, and Action Code 14:4 (`0x0E:0x04`) synchronization protocol.

79. [**79 - Inn Lodging, Bed Resting, and HP/SP Full Restoration Engine**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/79_inn_lodging_bed_resting_and_hp_sp_full_restoration_engine.md)
    * Complete municipal inn lodging engine in `se_Inn.pas` and `form_module` (228x154 px): Dynamic tariff formula (`Fee = Level * 10` Gold), rich text prompt formatting (`FUN_00487118`), screen fade-to-black animation sequence (`*(0x4c9588 + 0x7108) = 1`), sleeping sound effect (`wav0144.wav`), 100% HP & SP restoration for player and active companion pet, and Action Code 20:9 (`0x14:0x09`) resting protocol.

80. [**80 - Milestone Capstone 2: Complete Client UI Form Inventory, Component Hierarchy, and Delphi Package Architecture**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/80_milestone_capstone_2_client_ui_form_inventory_and_vcl_architecture.md)
    * Milestone Capstone 2 providing the definitive catalog and structural blueprint of all 81 Delphi forms across `aLogin.exe` and `Main.exe`.
    * Master 81-form inventory table with exact pixel dimensions (W x H), Pascal source units, DirectDraw 7 surface bindings, control hierarchies, coordinate systems, vtable method signatures, 51-opcode Action Code matrix, and 100% C# emulator subsystem implementation matrix.

81. [**81 - Companion Pet Rebirth Ascension, Reincarnation Stats, and NPC Upgrade System**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/81_companion_pet_rebirth_ascension_and_npc_upgrade_system.md)
    * Complete companion pet rebirth engine in `Se_NpcUpgrade.pas`: `form_NPCUpgrade` (360x422 px), prerequisite validation `FUN_0024e808` (Level 100+, Amity 100, catalyst category '%'), stat recalculation with +20% bonus points, celestial skill unlocking, and Action Code 15:10 (`0x0F:0x0A`) protocol.

82. [**82 - UFO Arcade Claw Machine Catcher Kinematics, Collision Physics, and Prize Dispenser Engine**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/82_ufo_arcade_claw_machine_catcher_kinematics_and_physics_engine.md)
    * Complete electromechanical claw crane state machine in `cMZ_Catcher.pas`: `CatcherBasic` (154x126 px), `CatcherMoving` (154x235 px), `CatcherDroping` (154x128 px), `Catching` (154x242 px), AABB prize bounding box hit-test, prize weight retention slip physics, and Action Code 74:21 (`0x4A:0x15`) protocol.

83. [**83 - Underground Treasure Digging Excavation and Geological Mining Engine**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/83_underground_treasure_digging_excavation_and_geological_mining_engine.md)
    * Complete subterranean archeological excavation grid in `LD_DigHoleGame.pas`: 42-plot matrix (7x6 layout, 83 px stride `0x53`), shovel tool durability wear (`icon_dig_1`), dirt mound to pit transformation (`DigHole` to `DigHole_Hole`), 6-strata loot tables, cave-in hazard rolls, and Action Code 74:24 (`0x4A:0x18`) protocol.

84. [**84 - Rhythm Boxing Punching Power Meter and Carnival Combat Minigame**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/84_rhythm_boxing_punching_power_meter_and_carnival_combat_minigame.md)
    * Complete arcade rhythm boxing simulator in `MZ_BoxingGame.pas`: 284 px oscillating power gauge `BoxingBar` within `StrengthBarFrame` (575x65 px), 30-second round countdown timer, opponent monster dummy `BoxingGameMonst` (417x305 px, 3,000 HP), glove animations `BoxingGameBoxer`, Critical/Great/Good timing windows, and Action Code 74:18 (`0x4A:0x12`) protocol.

85. [**85 - Carnival Lottery Wheel, Lotto Ball Physics, and Pyrotechnic Fireworks System**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/85_carnival_lottery_wheel_lotto_ball_physics_and_pyrotechnic_fireworks.md)
    * Complete lottery tumbling sphere and celebratory fireworks engine in `TMR_Lotto.pas` and `HL_NewFireworks.pas`: 3D multi-body `Lottosball` collision physics, 6-ball draw extraction down mechanical chute, 4-tier prize pool, "CPU rolled!" status prompt, multi-stage pyrotechnic particle bursts (`0x280b`), and Action Code 74:37 (`0x4A:0x25`) protocol.

86. [**86 - Memory Card Matching Poker Concentration Minigame Engine**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/86_memory_card_matching_poker_concentration_minigame_engine.md)
    * Complete card matching concentration challenge in `LD_PokeGame.pas`: 8-card tabletop grid (4x2 layout, `PokeGameBG`), 15 card face textures (`PokeCard_1`..`PokeCard_15`), card back mask `PokeCardBack`, match evaluation (`Icon_Currect_1` vs `Icon_Miss_1`), 1000 ms flip-back delay, and Action Code 74:27 (`0x4A:0x1B`) protocol.

87. [**87 - Tent Maid and Butler Customization, Wardrobe, and Housing Automation Engine**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/87_tent_maid_and_butler_customization_and_housing_automation.md)
    * Complete domestic servant system in `HL_MaidMenu.pas`: RVA `0x0026e44c`, 4 costume categories (Winter/Summer, Rabbit, Cat, Halloween, Devil, Qi Pao, Easter Maid/Butler, Bath Butler, Xmas, Wear nothing), storage and pet repository integration ("Props Keeper", "Pet Keeper"), procedural audio playback (`voice\A_robo_011..013`), outfit transformation sound (`Sound\Wav0152.wav`), and Action Code 20:12 (`0x14:0x0C`) protocol.

88. [**88 - Dynamic Scene Lighting, Day/Night Cycle, and Alpha Shadow Blitter Engine**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/88_dynamic_scene_lighting_day_night_cycle_and_alpha_shadow_engine.md)
    * Complete software lighting and alpha rasterization engine in `DZ_Light.pas` and `Qro_draw.pas`: 16-bit blitter kernel `FUN_00104b84` (`ro_Clipper_LightAlpha_ColorKey_Blt` at RVA `0x00104b54`, `ro_Clipper_Alpha_Fill` at RVA `0x00104b5c`), 5 blend modes (Opaque, ColorKey, Alpha, LightAlpha additive saturation, Tint), dual color space masks (RGB 565 `0xF800/0x07E0/0x001F`, RGB 555 `0x7C00/0x03E0/0x001F`), 30-emitter scene light manager (`FUN_0041d8e4`), radial light cones (`DoorLight`, `SmallDoorLight`, `SkillLight\`), and 4-phase diurnal Day/Night ambient tinting.

89. [**89 - DirectDraw 16-Bit Display Mode Adapter, Surface Allocation, and Clipper Subsystem**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/89_directdraw_16bit_display_mode_adapter_and_surface_clipper.md)
    * Complete DirectDraw 7 hardware acceleration subsystem in `DXDraws.pas` and `PDXRender.pas`: Dynamic COM factory hooks (`DirectDrawCreate`, `DirectDrawCreateEx` via `DDraw.dll`), 16-bit RGB 565/555 display mode negotiation via `DDSURFACEDESC2`, hardware surface allocation with system memory fallback, `RGNDATA` multi-rectangle clipper creation (`FUN_00072218` on `IDirectDrawClipper`), and surface loss recovery (`RestoreAllSurfaces` on `DDERR_SURFACELOST`).

90. [**90 - Milestone Capstone 3: Master Opcode Protocol and Packet Cryptography Handbook**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/90_milestone_capstone_3_master_opcode_protocol_and_packet_cryptography_handbook.md)
    * Milestone Capstone 3 providing the definitive wire protocol specification and cryptographic handbook across the entire Wonderland Online engine.
    * Physical envelope framing (`0x44F4` magic header, 2-byte length), rolling XOR 0xAD keystream algorithm, complete routing matrix of all 51 Action Codes (Codes 1..51 / `0x01..0x53`), 323 sub-opcodes across all game subsystems, Action Code 74 minigame matrix, and production C# async pipeline network stack.

91. [**91 - In-Battle Skill Selection Palette, SP Calculation, and Tactical Radial Targeting Engine**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/91_in_battle_skill_selection_palette_and_tactical_radial_targeting.md)
    * Complete combat ability interface in `se_BattleSkill.pas`: `Form_BattleSkill_1` (353x274 px), `form_skillexpress`, 500 learned skill slot iterator (`*(actor + 0x30cf + id * 5)`), equipment catalyst skills (`15000 + id * 2`), 6 targeting geometries (Single, Column, Row, Cross, Full Team, Self), SP resource validation, and Action Code 17:1 (`0x11:0x01`) combat skill dispatch protocol.

92. [**92 - Combat Abnormal Status Condition, Buff/Debuff System, and Turn Decay Engine**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/92_combat_abnormal_status_condition_buff_debuff_and_turn_decay_engine.md)
    * Complete crowd control and condition decay system in `se_Status.pas` and `HL_Battle.pas`: `form_status` (480x228 px, "Status [Ctrl+A]"), 8 core abnormal conditions (Freeze, Petrify, Sleep, Paralyze/Silence, Confusion, Poison, Curse, Burn), 6 attribute point allocators (`arrow_upBig1`), turn-start DoT damage ticks, turn-end duration counters, and Action Code 18:2 (`0x12:0x02`) status broadcast protocol.

93. [**93 - NPC Vendor Bulk Procurement, Stack Limits, and Purchase Quantity Selector**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/93_npc_vendor_bulk_procurement_and_purchase_quantity_selector.md)
    * Complete commercial volume purchasing dialog in `se_Shop.pas`: `form_BuyQuant_Npc` (75x200 px) and `form_BuyQ_Npc` (250x452 px), 1,250 unit hard stack ceiling (`0x4E2`), numeric editor with `arrow_up1` and `arrow_dw1` steppers, multi-constraint validation (gold purse, inventory slots, weight allowance), and Action Code 20:1 (`0x14:0x01`) bulk purchase protocol.

94. [**94 - Pet Consignment Bazaar, Multi-Tier Stall Showcase, and Purchase Engine**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/94_pet_consignment_bazaar_stall_showcase_and_purchase_engine.md)
    * Complete companion pet street commerce system in `HL_Shop.pas` and `CY_TradeMenu.pas`: Responsive multi-tier stall showcases (`Form_BuyPet1Stall_1` 441x243 px [5 pets], `Form_BuyPet2Stall_1` 441x442 px [10 pets], `Form_BuyPet3Stall_1` 441x644 px [15 pets]), Amity threshold safety gate ($\ge 20$), active companion roster cap (max 4), and Action Code 20:5/20:7 pet purchase protocols.

95. [**95 - In-Game Account Password Modification and Credential Rotation Subsystem**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/95_in_game_account_password_modification_and_credential_rotation.md)
    * Complete in-game credential management suite in `se_ChangePassword.pas`: `Form_ChangePassword_1` (307x440 px), 6 password-masked input fields, 6-to-10 character constraints, confirmation mismatch verification (`"Two pwds not match"`), password versus deletion PIN anti-collision mutex (`"Pwd & Del are same!"`), and Action Code 1:8 (`0x01:0x08`) credential rotation wire protocol.

96. [**96 - Multi-Stage Character Creation Wizard, Palette Tinting, and Attribute Allocation Engine**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/96_character_creation_wizard_palette_tinting_and_attribute_allocation.md)
    * Complete 4-stage character creation wizard in `se_CreateRole.pas`: `form_NewRole_1..5`, 14 canonical playable archetypes, elemental affinity selection, 24 base attribute points, real-time hair/skin/clothing RGB palette tinting, and Action Code 1:2 (`0x01:0x02`) character generation wire protocol.

97. [**97 - Player Furniture and Utensil Stall Consignment Vending Engine**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/97_player_furniture_and_utensil_stall_consignment_system.md)
    * Complete appliance and furniture street commerce system in `HL_Shop.pas`: `Form_Buy1Utensil_1` and `Form_Buy3Utensil_1` (489x655 px, "BeStallUtenSil"), non-zero durability verification, container vacancy rule, and Action Code 20:8 (`0x14:0x08`) utensil purchase wire protocol.

98. [**98 - Gacha Capsule Vending Machine, Crank Kinematics, and Prize Drop Chute Engine**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/98_gacha_capsule_vending_machine_and_mystery_gift_box_engine.md)
    * Complete municipal gacha capsule machine in `se_Capsule.pas`: `Form_Capsulem` (343x243 px), dual configurations ("Capsule" item and "FurnitureCapsule" housing), coin slot token sensor `icon_rail2`, 360-degree rotational crank physics, tumbler globe animation, and Action Code 74:11 (`0x4A:0x0B`) gacha spin protocol.

99. [**99 - Guild Headquarters Notebook, Member Roster, 10x10 Pixel Emblem Canvas, and Notice Dispatcher**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/99_guild_headquarters_roster_10x10_emblem_canvas_and_notice_board.md)
    * Complete 4-tab guild notebook in `se_ArmyGroup.pas`: `Form_ArmyGrounpInfo_1` (statistics), `Form_ArmyGrounp_1` (roster), `Form_Emblem_1` ($10 \times 10$ 8-color pixel emblem canvas painter, `FUN_002888d8`), `Form_OrgNotice` (daily bulletin notice dispatcher, `FUN_00292994`), and Action Code 31 (`0x1F`) legion governance wire protocol.

100. [**100 - Milestone Capstone 4: The Complete 100-Specification Grand Master Compendium and Autonomous Private Server Implementation Manual**](file:///D:/GitHub/Wonderland-Private-Server/decompiled/100_milestone_capstone_4_grand_master_compendium_and_implementation_manual.md)
    * Milestone Capstone 4 delivering the definitive centennial synthesis across all 100 reverse engineering specifications.
    * 10-domain functional breakdown, master cross-index of all 100 specifications, unified 51 Action Code matrix with 323 sub-opcodes, master 81 Delphi VCL form catalog, and complete autonomous C# .NET server emulator implementation manual.
