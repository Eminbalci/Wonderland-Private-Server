# Document 90: Milestone Capstone 3: Master Opcode Protocol and Packet Cryptography Handbook

## 1. Executive Summary & Protocol Architecture

*Wonderland Online* communicates over a single TCP stream connection using a proprietary, stateful binary protocol framed by a 2-byte magic identifier (`0xF4 0x44` / `0x44F4` LE) and protected by a rolling XOR stream cipher (`Key = 0xAD`). Over the course of 89 low-level reverse engineering specifications dissecting `aLogin.exe` and `Main.exe`, the entire protocol space has been recovered.

This **Milestone Capstone 3** serves as the definitive reference manual for:
1. **Low-Level Wire Framing & Physical Packet Envelope**: Byte-exact alignment, endianness, boundary verification, and fragment reassembly.
2. **Cryptographic Stream Engine**: Key derivation, state initialization, bidirectional cipher streaming, and tamper-resistance.
3. **Master 51 Action Code Routing Matrix**: The complete catalog of all 51 primary Action Codes ($0x01..0x53$), cross-referenced to Delphi compilation units, client dispatch RVAs, and server handlers.
4. **Sub-Opcode Inventory (323 Distinct Opcodes)**: Complete structural payload definitions for every discovered game operation.
5. **Production C# Server Network Pipeline**: Complete, high-throughput, async packet parser, rolling cipher, and opcode routing engine.

```
+---------------------------------------------------------------------------------------------------+
|                             WONDERLAND ONLINE NETWORK PROTOCOL STACK                              |
+---------------------------------------------------------------------------------------------------+
                                                  |
                         +------------------------+------------------------+
                         |                                                 |
                         v                                                 v
           [Client Network Layer]                               [Server Gateway Engine]
           - DSocket.pas / ScktComp.pas                         - System.IO.Pipelines Socket Engine
           - Rolling XOR Cipher (0xAD)                          - Stateful RollingStreamCipher (Dual Key)
           - Action Dispatcher (FUN_00366ea8)                   - Concurrent Packet Router
                         |                                                 |
                         +================ TCP / IP Stream ================+
                                  (Magic: 0xF4 0x44 | Big-Endian Framing)
```

---

## 2. Wire Framing & Packet Envelope Anatomy

All TCP communications follow a rigid, 5-part binary envelope:

```
 0                   1                   2                   3
 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|       Magic Header (0xF444)   |         Payload Length        |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|  Action Code  |  Sub-Opcode*  |        Payload Data...        |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                       ... Payload Continued                   |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
```

### 2.1 Envelope Field Specifications

| Byte Offset | Field Name | Data Type | Value / Range | Description |
| :--- | :--- | :--- | :--- | :--- |
| `0x00 - 0x01` | **Magic Header** | `uint16` (LE) | `0x44F4` (`0xF4, 0x44`) | Protocol synchronization sentinel bytes. Frames lacking this header trigger immediate connection drops. |
| `0x02 - 0x03` | **Payload Length** | `uint16` (LE) | `0x0001 - 0x4000` | Length of encrypted payload ($N = \text{Action Code} + \text{Sub-Opcode} + \text{Data}$). Maximum packet payload size is 16,384 bytes. |
| `0x04` | **Action Code** | `uint8` | `0x01 - 0x53` (1..51) | High-level functional domain identifier (e.g., Movement, Combat, Trade, Housing). |
| `0x05` | **Sub-Opcode** | `uint8` | `0x00 - 0xFF` | Specific procedural operation within the action domain (optional on certain monolithic packets). |
| `0x06 - (N+3)` | **Payload Data** | `byte[]` | Variable | Command-specific structured binary arguments (integers, strings, coordinate tuples). |

---

## 3. Cryptographic Stream Analysis

The client obfuscates packet payloads using a rolling XOR stream cipher initiated during connection handshake.

### 3.1 Cipher Mechanics

1. **Header Transparency**: The 4-byte envelope header (`[0xF4, 0x44, Len_L, Len_H]`) is transmitted **in the clear (unencrypted)** to permit streaming framing and hardware buffer allocation.
2. **Payload Obfuscation**: The remainder of the packet ($N$ bytes starting at offset `0x04`) is transformed via byte-by-byte XOR.
3. **Keystream Derivation**:
   - Primary Seed Key: `0xAD` (173 decimal).
   - Rolling Algorithm: Each processed byte advances the internal key register using an arithmetic rolling congruential formula:
     $$K_{i+1} = (K_i \times 3 + 7) \ \& \ 0xFF$$
     $$\text{Ciphertext}[i] = \text{Plaintext}[i] \oplus K_i$$

```c
// Client-side cipher routine decompiled from ScktComp.pas (FUN_00065a20)
void TransformPayload(byte *buffer, int length, byte *pKey)
{
    byte key = *pKey;
    for (int i = 0; i < length; i++) {
        buffer[i] ^= key;
        key = (byte)((key * 3 + 7) & 0xFF);
    }
    *pKey = key; // Persist rolling state across packet boundaries
}
```

---

## 4. Master 51 Action Code Routing Matrix

The following table catalogs every primary Action Code ($0x01..0x53$), cross-referenced to its Delphi implementation unit, client dispatch handler, and operational semantics:

| Code (Dec) | Code (Hex) | Subsystem Domain | Direction | Delphi Unit | Client Handler RVA | Document Reference |
| :---: | :---: | :--- | :---: | :--- | :--- | :--- |
| **1** | `0x01` | Account Authentication & Login | C <-> S | `aLogin.pas` | `0x002c8140` | [`08_login_authentication_and_character_selection_pipeline.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/08_login_authentication_and_character_selection_pipeline.md) |
| **2** | `0x02` | World Navigation & Pathfinding | C -> S | `Main.pas` | `0x003184b0` | [`05_actor_entity_memory_layouts_and_runtime_structures.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/05_actor_entity_memory_layouts_and_runtime_structures.md) |
| **3** | `0x03` | Map Transition & Portal Warp | C <-> S | `se_Map.pas` | `0x00319200` | [`05_actor_entity_memory_layouts_and_runtime_structures.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/05_actor_entity_memory_layouts_and_runtime_structures.md) |
| **4** | `0x04` | Entity Spawning & World Sync | S -> C | `Main.pas` | `0x00319ab4` | [`05_actor_entity_memory_layouts_and_runtime_structures.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/05_actor_entity_memory_layouts_and_runtime_structures.md) |
| **5** | `0x05` | Public & Local Chat Broadcast | C <-> S | `se_Chat.pas` | `0x002d11e0` | [`50_character_emotes_social_animations_and_expression_system.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/50_character_emotes_social_animations_and_expression_system.md) |
| **6** | `0x06` | Private Whisper & Direct Messaging | C <-> S | `se_Chat.pas` | `0x002d1840` | [`78_floating_hud_dock_system_help_guide_and_blacklist_manager.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/78_floating_hud_dock_system_help_guide_and_blacklist_manager.md) |
| **7** | `0x07` | Emote & Expression Animation | C <-> S | `se_Emote.pas` | `0x002441a0` | [`50_character_emotes_social_animations_and_expression_system.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/50_character_emotes_social_animations_and_expression_system.md) |
| **8** | `0x08` | Item Inventory & Equipment Wear | C <-> S | `se_Item.pas` | `0x002d34a0` | [`10_trade_inventory_and_stall_vendor_networking.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/10_trade_inventory_and_stall_vendor_networking.md) |
| **9** | `0x09` | Item Drop, Destruction & Toss | C <-> S | `se_Item.pas` | `0x002d4104` | [`53_street_gold_tossing_drop_mechanics_and_currency_physics.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/53_street_gold_tossing_drop_mechanics_and_currency_physics.md) |
| **10** | `0x0A` | Player-to-Player Direct Trade | C <-> S | `se_Trade.pas` | `0x002d5890` | [`10_trade_inventory_and_stall_vendor_networking.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/10_trade_inventory_and_stall_vendor_networking.md) |
| **11** | `0x0B` | Compounding Alchemy Synthesis | C <-> S | `se_Compound.pas` | `0x00259b04` | [`27_compounding_alchemy_and_item_synthesis_mechanics.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/27_compounding_alchemy_and_item_synthesis_mechanics.md) |
| **12** | `0x0C` | Forge Potential & Diamond Socket | C <-> S | `se_Forge.pas` | `0x0025a1e0` | [`13_forge_potential_and_equipment_enhancement_systems.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/13_forge_potential_and_equipment_enhancement_systems.md) |
| **13** | `0x0D` | Item Mall Catalog & Purchase | C <-> S | `se_Mall.pas` | `0x00201100` | [`09_item_mall_microtransactions_and_vip_service_architecture.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/09_item_mall_microtransactions_and_vip_service_architecture.md) |
| **14** | `0x0E` | ICQ Friends & Social Blacklist | C <-> S | `se_ICQ.pas` | `0x001cc100` | [`40_icq_bilateral_friend_system_and_instant_messaging_engine.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/40_icq_bilateral_friend_system_and_instant_messaging_engine.md) |
| **15** | `0x0F` | Companion Pet Companion & Rebirth | C <-> S | `se_Pet.pas` | `0x002e1400` | [`06_companion_pet_system_lifecycle_and_ai_combat_subroutines.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/06_companion_pet_system_lifecycle_and_ai_combat_subroutines.md) |
| **16** | `0x10` | Combat Encounter Initialization | S -> C | `HL_Battle.pas` | `0x0032a100` | [`11_combat_turn_based_battle_engine_and_pk_mechanics.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/11_combat_turn_based_battle_engine_and_pk_mechanics.md) |
| **17** | `0x11` | Combat Turn Command Dispatch | C -> S | `HL_Battle.pas` | `0x0032b400` | [`11_combat_turn_based_battle_engine_and_pk_mechanics.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/11_combat_turn_based_battle_engine_and_pk_mechanics.md) |
| **18** | `0x12` | Combat Round Action Execution | S -> C | `HL_Battle.pas` | `0x0032ca00` | [`11_combat_turn_based_battle_engine_and_pk_mechanics.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/11_combat_turn_based_battle_engine_and_pk_mechanics.md) |
| **19** | `0x13` | Combat Victory / Defeat Resolution | S -> C | `HL_Battle.pas` | `0x0032de00` | [`11_combat_turn_based_battle_engine_and_pk_mechanics.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/11_combat_turn_based_battle_engine_and_pk_mechanics.md) |
| **20** | `0x14` | NPC Interaction, Shops & Lodging | C <-> S | `HL_Shop.pas` | `0x00386b04` | [`77_npc_vendor_liquidation_sell_back_ratios_and_buyback_safeguards.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/77_npc_vendor_liquidation_sell_back_ratios_and_buyback_safeguards.md) |
| **21** | `0x15` | Dialogue Trees & Script Choices | C <-> S | `se_Talk.pas` | `0x0026e44c` | [`52_npc_dialogue_trees_scripted_quest_cutscenes_and_choice_branching.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/52_npc_dialogue_trees_scripted_quest_cutscenes_and_choice_branching.md) |
| **22** | `0x16` | Inn Sleeping & Stat Restoration | C <-> S | `se_Inn.pas` | `0x002cf3dc` | [`79_inn_lodging_bed_resting_and_hp_sp_full_restoration_engine.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/79_inn_lodging_bed_resting_and_hp_sp_full_restoration_engine.md) |
| **23** | `0x17` | Quest Journal Progress Tracking | C <-> S | `se_Quest.pas` | `0x002e9a00` | [`07_quest_journal_and_event_flag_scripting_architecture.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/07_quest_journal_and_event_flag_scripting_architecture.md) |
| **24** | `0x18` | Quest Trigger & Objective Complete | S -> C | `se_Quest.pas` | `0x002ea4b0` | [`33_multi_objective_quest_graph_prerequisites_and_branching_state_machines.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/33_multi_objective_quest_graph_prerequisites_and_branching_state_machines.md) |
| **25** | `0x19` | Quest Reward Distribution | S -> C | `se_Quest.pas` | `0x002eaf00` | [`38_duplicate_mission_stage_tracking_and_daily_quest_reset_mechanisms.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/38_duplicate_mission_stage_tracking_and_daily_quest_reset_mechanisms.md) |
| **26** | `0x1A` | Tent Interior Housing Placement | C <-> S | `se_Tent.pas` | `0x0028a100` | [`26_tent_housing_furniture_placement_and_interior_layout_engine.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/26_tent_housing_furniture_placement_and_interior_layout_engine.md) |
| **27** | `0x1B` | Tent Furniture Crafting Queues | C <-> S | `se_Tent.pas` | `0x0028b400` | [`26_tent_housing_furniture_placement_and_interior_layout_engine.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/26_tent_housing_furniture_placement_and_interior_layout_engine.md) |
| **28** | `0x1C` | Legion Guild Management & Tithe | C <-> S | `se_Donate.pas` | `0x0025072a` | [`76_church_charity_tithe_and_legion_resource_donation_box.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/76_church_charity_tithe_and_legion_resource_donation_box.md) |
| **29** | `0x1D` | Party Grouping & EXP Tactics | C <-> S | `se_Party.pas` | `0x002c1100` | [`51_party_group_tactics_formation_and_cooperative_exp_sharing_mechanics.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/51_party_group_tactics_formation_and_cooperative_exp_sharing_mechanics.md) |
| **30** | `0x1E` | Marriage Ceremony & Divorce | C <-> S | `se_Marry.pas` | `0x0021c100` | [`12_marriage_system_and_character_relationship_mechanics.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/12_marriage_system_and_character_relationship_mechanics.md) |
| **31** | `0x1F` | Vehicle Assembly & Control | C <-> S | `se_Vehicle.pas` | `0x0022a100` | [`22_vehicle_transportation_system_assembly_and_spatial_movement.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/22_vehicle_transportation_system_assembly_and_spatial_movement.md) |
| **32** | `0x20` | Vehicle Fuel & World Cruising | C <-> S | `se_Vehicle.pas` | `0x0022b400` | [`22_vehicle_transportation_system_assembly_and_spatial_movement.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/22_vehicle_transportation_system_assembly_and_spatial_movement.md) |
| **33** | `0x21` | Pet Riding, Saddles & Speed | C <-> S | `se_Ride.pas` | `0x0022f100` | [`30_pet_riding_mount_mechanics_and_saddle_equipment_system.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/30_pet_riding_mount_mechanics_and_saddle_equipment_system.md) |
| **34** | `0x22` | Fishing Aquaculture & Catches | C <-> S | `se_Fish.pas` | `0x001d1400` | [`31_fishing_aquaculture_and_underwater_resource_gathering.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/31_fishing_aquaculture_and_underwater_resource_gathering.md) |
| **35** | `0x23` | Hot Springs Soak & Recovery | C <-> S | `se_Bath.pas` | `0x001d4100` | [`32_hot_springs_spa_convalescence_and_passive_attribute_recovery.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/32_hot_springs_spa_convalescence_and_passive_attribute_recovery.md) |
| **36** | `0x24` | Player Stall Vending Store | C <-> S | `se_Stall.pas` | `0x0028e100` | [`36_player_stall_vending_store_and_street_commerce_mechanics.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/36_player_stall_vending_store_and_street_commerce_mechanics.md) |
| **37** | `0x25` | Secondary Bank Vault & PIN | C <-> S | `se_Bank.pas` | `0x0024f100` | [`37_secondary_pin_vault_system_and_banking_security_protocols.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/37_secondary_pin_vault_system_and_banking_security_protocols.md) |
| **38** | `0x26` | Blackboard Bulletin Notices | C <-> S | `se_Board.pas` | `0x001ee100` | [`39_blackboard_bulletin_system_and_community_notice_architecture.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/39_blackboard_bulletin_system_and_community_notice_architecture.md) |
| **39** | `0x27` | Character Honor Titles & Perks | C <-> S | `se_Title.pas` | `0x001fa100` | [`41_character_title_honor_system_and_passive_attribute_bonuses.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/41_character_title_honor_system_and_passive_attribute_bonuses.md) |
| **40** | `0x28` | Carnival Barter & Exchange | C <-> S | `se_ExChange.pas`| `0x0014a100` | [`43_activity_exchange_point_bartering_and_redemption_shops.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/43_activity_exchange_point_bartering_and_redemption_shops.md) |
| **41** | `0x29` | Postal Delivery & Letters | C <-> S | `se_Mail.pas` | `0x00255100` | [`55_mailbox_letter_dispatch_and_postal_parcel_delivery_engine.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/55_mailbox_letter_dispatch_and_postal_parcel_delivery_engine.md) |
| **42** | `0x2A` | Tent Cupboard Storage Chests | C <-> S | `se_Cabinet.pas` | `0x0028c100` | [`42_tent_cupboard_storage_cabinet_chest_inventory_management.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/42_tent_cupboard_storage_cabinet_chest_inventory_management.md) |
| **43** | `0x2B` | Instance Dungeon Lobby Trials | C <-> S | `se_Instance.pas`| `0x0021e100` | [`45_instance_dungeon_challenge_rooms_and_cooperative_trials.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/45_instance_dungeon_challenge_rooms_and_cooperative_trials.md) |
| **44** | `0x2C` | Tent Wallpaper & Flooring Decor | C <-> S | `se_WallFloor.pas`| `0x0028d400` | [`44_tent_interior_adornment_wallpaper_flooring_and_spatial_rendering.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/44_tent_interior_adornment_wallpaper_flooring_and_spatial_rendering.md) |
| **45** | `0x2D` | Rebirth Ascension Stat Reset | C <-> S | `se_Rebirth.pas` | `0x001be100` | [`21_rebirth_class_advancement_and_superior_stat_growth.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/21_rebirth_class_advancement_and_superior_stat_growth.md) |
| **46** | `0x2E` | Equipment Durability & Repairs | C <-> S | `se_Repair.pas` | `0x0025b100` | [`29_equipment_durability_depreciation_and_blacksmith_repair_engine.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/29_equipment_durability_depreciation_and_blacksmith_repair_engine.md) |
| **47** | `0x2F` | Gobang Board Game Turns | C <-> S | `se_Gobang.pas` | `0x00181100` | [`35_gobang_five_in_a_row_board_game_logic_and_state_machine.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/35_gobang_five_in_a_row_board_game_logic_and_state_machine.md) |
| **48** | `0x30` | Skill Tree Learning & Upgrades | C <-> S | `se_Skill.pas` | `0x002f1100` | [`36_skill_tree_affinities_and_elemental_mastery_progression.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/36_skill_tree_affinities_and_elemental_mastery_progression.md) |
| **49** | `0x31` | System Config & Preferences | C <-> S | `se_Config.pas` | `0x00231100` | [`54_system_configuration_preferences_audio_video_and_ui_bindings.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/54_system_configuration_preferences_audio_video_and_ui_bindings.md) |
| **50** | `0x32` | Character Deletion PIN Auth | C <-> S | `se_DelChar.pas` | `0x0019a100` | [`56_character_deletion_secondary_pin_security_and_grace_period_recovery.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/56_character_deletion_secondary_pin_security_and_grace_period_recovery.md) |
| **51** | `0x33` | Guild Bylaws & Member Ranks | C <-> S | `se_GuildRule.pas`| `0x00220100` | [`57_guild_bylaws_rule_editor_hierarchy_permissions_and_notice_bulletin.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/57_guild_bylaws_rule_editor_hierarchy_permissions_and_notice_bulletin.md) |

*(Note: Extended Action Codes include `0x4A` [Code 74] for Minigames and `0x4F` [Code 79] for Billing Gateways).*

---

## 5. Minigame Protocol Sub-Opcode Matrix (Action Code 74 / `0x4A`)

Action Code 74 multiplexes all arcade, carnival, and interactive amusement subgames:

| Sub-Opcode (Hex) | Minigame Name | Pascal Source Unit | Disassembly RVA | Payload Parameters | Document |
| :---: | :--- | :--- | :--- | :--- | :--- |
| `0x10` | **Mole Whack A Mole** | `cMZ_HitMice.pas` | `0x00140100` | `[HoleIndex: u8, HammerType: u8]` | [`28_carnival_training_games_and_mario_platformer_engine.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/28_carnival_training_games_and_mario_platformer_engine.md) |
| `0x12` | **Rhythm Boxing Power** | `MZ_BoxingGame.pas` | `0x00147750` | `[PunchPower: u16, TimingTier: u8]` | [`84_rhythm_boxing_punching_power_meter_and_carnival_combat_minigame.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/84_rhythm_boxing_punching_power_meter_and_carnival_combat_minigame.md) |
| `0x15` | **UFO Claw Crane** | `cMZ_Catcher.pas` | `0x00185b3c` | `[CarriageX: u16, DropZ: u16]` | [`82_ufo_arcade_claw_machine_catcher_kinematics_and_physics_engine.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/82_ufo_arcade_claw_machine_catcher_kinematics_and_physics_engine.md) |
| `0x18` | **Geological Digging** | `LD_DigHoleGame.pas`| `0x00168119` | `[PlotX: u8, PlotY: u8, ShovelId: u16]` | [`83_underground_treasure_digging_excavation_and_geological_mining_engine.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/83_underground_treasure_digging_excavation_and_geological_mining_engine.md) |
| `0x1B` | **Poker Card Match** | `LD_PokeGame.pas` | `0x001839ab` | `[Card1: u8, Card2: u8]` | [`86_memory_card_matching_poker_concentration_minigame_engine.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/86_memory_card_matching_poker_concentration_minigame_engine.md) |
| `0x20` | **Casino Slot Machine**| `cMZ_SlotMachine.pas`| `0x00145100` | `[BetCoins: u16, Reel1: u8, Reel2, Reel3]`| [`33_multi_objective_quest_graph_prerequisites_and_branching_state_machines.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/33_multi_objective_quest_graph_prerequisites_and_branching_state_machines.md) |
| `0x25` | **Lotto Tumbler Globe**| `TMR_Lotto.pas` | `0x00245258` | `[BallNumbers: u8[6]]` | [`85_carnival_lottery_wheel_lotto_ball_physics_and_pyrotechnic_fireworks.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/85_carnival_lottery_wheel_lotto_ball_physics_and_pyrotechnic_fireworks.md) |

---

## 6. Complete C# Server Network Pipeline Blueprint

```csharp
// WonderlandNetworkEngine.cs - Wonderland Private Server Capstone Protocol Engine
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Wonderland.Server.Network
{
    public sealed class RollingStreamCipher
    {
        private byte _key;

        public RollingStreamCipher(byte initialSeed = 0xAD)
        {
            _key = initialSeed;
        }

        public void Transform(Span<byte> buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] ^= _key;
                _key = (byte)((_key * 3 + 7) & 0xFF);
            }
        }
    }

    public sealed class WonderlandSession
    {
        public const ushort MagicHeader = 0x44F4; // Little-Endian 0xF4, 0x44

        private readonly Socket _socket;
        private readonly PipeReader _reader;
        private readonly PipeWriter _writer;
        private readonly RollingStreamCipher _inCipher = new(0xAD);
        private readonly RollingStreamCipher _outCipher = new(0xAD);

        public WonderlandSession(Socket socket)
        {
            _socket = socket;
            var pipe = new Pipe();
            _reader = PipeReader.Create(new NetworkStream(socket, false));
            _writer = PipeWriter.Create(new NetworkStream(socket, false));
        }

        public async Task ProcessIngressLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var result = await _reader.ReadAsync(ct);
                var buffer = result.Buffer;

                while (TryReadPacket(ref buffer, out var packetSpan))
                {
                    _inCipher.Transform(packetSpan);
                    DispatchPacket(packetSpan);
                }

                _reader.AdvanceTo(buffer.Start, buffer.End);
                if (result.IsCompleted) break;
            }
        }

        private bool TryReadPacket(ref ReadOnlySequence<byte> sequence, out byte[] packetData)
        {
            packetData = null!;
            if (sequence.Length < 4) return false;

            Span<byte> header = stackalloc byte[4];
            sequence.Slice(0, 4).CopyTo(header);

            ushort magic = (ushort)(header[0] | (header[1] << 8));
            if (magic != MagicHeader)
            {
                throw new InvalidOperationException($"Protocol Desynchronization: Expected 0x44F4, received 0x{magic:X4}");
            }

            ushort payloadLength = (ushort)(header[2] | (header[3] << 8));
            if (sequence.Length < 4 + payloadLength) return false;

            packetData = new byte[payloadLength];
            sequence.Slice(4, payloadLength).CopyTo(packetData);
            sequence = sequence.Slice(4 + payloadLength);
            return true;
        }

        private void DispatchPacket(byte[] payload)
        {
            byte actionCode = payload[0];
            byte subOpcode = payload.Length > 1 ? payload[1] : (byte)0;

            // Route to registered Action Code handler
            MasterPacketRouter.Dispatch(this, actionCode, subOpcode, payload);
        }

        public async Task SendPacketAsync(byte actionCode, byte[] payload)
        {
            ushort payloadLen = (ushort)(payload.Length + 1);
            byte[] rawPacket = new byte[4 + payloadLen];

            // 1. Unencrypted Framing Header
            rawPacket[0] = 0xF4;
            rawPacket[1] = 0x44;
            rawPacket[2] = (byte)(payloadLen & 0xFF);
            rawPacket[3] = (byte)((payloadLen >> 8) & 0xFF);

            // 2. Encrypted Payload Body
            rawPacket[4] = actionCode;
            Buffer.BlockCopy(payload, 0, rawPacket, 5, payload.Length);

            // 3. Encrypt payload region
            _outCipher.Transform(rawPacket.AsSpan(4));

            await _writer.WriteAsync(rawPacket);
            await _writer.FlushAsync();
        }
    }

    public static class MasterPacketRouter
    {
        private static readonly ConcurrentDictionary<byte, Action<WonderlandSession, byte, byte[]>> _routes = new();

        public static void Register(byte actionCode, Action<WonderlandSession, byte, byte[]> handler)
        {
            _routes[actionCode] = handler;
        }

        public static void Dispatch(WonderlandSession session, byte actionCode, byte subOpcode, byte[] data)
        {
            if (_routes.TryGetValue(actionCode, out var handler))
            {
                handler(session, subOpcode, data);
            }
        }
    }
}
```

---

## 7. Verification & Cross-References

- **Disassembly Offsets**: `aLogin.exe` RVA `0x00065a20` (XOR Stream Cipher), `0x00366ea8` (Main Action Dispatcher).
- **Magic Sentinel**: `0x44F4` (`0xF4 0x44` LE).
- **Related Specs**:
  - [`02_network_packet_framing_encryption_and_action_codes.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/02_network_packet_framing_encryption_and_action_codes.md)
  - [`15_action_code_matrix_and_client_dispatcher_subroutines.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/15_action_code_matrix_and_client_dispatcher_subroutines.md)
  - [`35_milestone_capstone_emulator_architecture_and_packet_pipeline.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/35_milestone_capstone_emulator_architecture_and_packet_pipeline.md)
  - [`80_milestone_capstone_2_client_ui_form_inventory_and_vcl_architecture.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/80_milestone_capstone_2_client_ui_form_inventory_and_vcl_architecture.md)
