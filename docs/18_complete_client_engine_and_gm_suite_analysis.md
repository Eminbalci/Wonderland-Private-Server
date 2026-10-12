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

