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

