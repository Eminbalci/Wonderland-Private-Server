# Master Protocol Reference: Network Action Codes (AC)

## 1. Overview
Comprehensive specification of the binary packet protocol between the Wonderland Online Client (`aLogin.exe` / `Main.exe`) and the Private Server (`Wonderland-Private-Server`).

The network layer operates across TCP sockets utilizing a 2-byte header length, 1-byte Action Code (`AC`), optional 1-byte Sub-Code, and variable typed payloads. 100% of all **86 client-side Action Codes** found in decompiled client binary (`FUN_002d6ad8` @ `002d6ad8`) plus server extensions are fully implemented across **88 handler classes** in [`Src/Network/ActionCodes/`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/).

For the exhaustive low-level C disassembly mappings and packet formats, refer to [docs/all_decompiled_network_action_codes.md](file:///d:/GitHub/Wonderland-Private-Server/docs/all_decompiled_network_action_codes.md).

---

## 2. Master Action Code (AC) Protocol Registry (88 Codes)

| AC ID | Handler Class | Description & Primary Sub-Codes |
| :--- | :--- | :--- |
| **`AC 0`** | [`AC0.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC0.cs) | Connection heartbeat and keep-alive null packets. |
| **`AC 1`** | [`AC01.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC01.cs) | Session Handshake & Protocol Initialization. |
| **`AC 2`** | [`AC02.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC02.cs) | Client-server version handshake & ping synchronization (`Recv1`, `Recv2`). |
| **`AC 3`** | [`AC03.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC03.cs) | Client Scene Loaded & Map Enter Ready Confirmation ACK. |
| **`AC 4`** | [`AC04.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC04.cs) | Character Selection & Session Initiation. |
| **`AC 5`** | [`AC05.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC05.cs) | Character Vitals, Stunt Skills & Stat Sync (`Sub 1`, `Sub 4`, `Sub 11`). |
| **`AC 6`** | [`AC06.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC06.cs) | Character Stat & Attribute allocation (STR, CON, INT, WIS, AGI point spending). |
| **`AC 7`** | [`AC07.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC07.cs) | Movement Verification & Rubberband Position Correction. |
| **`AC 8`** | [`AC08.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC08.cs) | Skill learning, grading, and stunt skill unlocks (`Recv_1`, sub-codes 27–33). |
| **`AC 9`** | [`AC09.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC09.cs) | Character creation flow, stunt skill assignment, and initial world map spawn. |
| **`AC 10`** | [`AC10.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC10.cs) | Game World Clock & Server Time Synchronization. |
| **`AC 11`** | [`AC11.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC11.cs) | Combat engagement triggers (PK player battles, wild monster encounters, NPC combat). |
| **`AC 12`** | [`AC12.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC12.cs) | Turn-based round command selection (Attack, Cast Skill, Defend, Use Item, Flee, Catch). |
| **`AC 13`** | [`AC13.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC13.cs) | Combat round execution engine: animation packets, damage numbers, HP/SP sync, victory/defeat. |
| **`AC 14`** | [`AC14.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC14.cs) | Social relations, In-Game Mail (`Sub 1`), Friend Request Send (`Sub 2`), Friend Accept (`Sub 3`), Friend Remove (`Sub 4`), Friend List (`Sub 5`), Status Sync (`Sub 7`, `Sub 8`). |
| **`AC 15`** | [`AC15.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC15.cs) | Map entity visibility & state broadcasts (Spawn player, NPC, pet, vehicle, ocean raft, despawn). |
| **`AC 16`** | [`AC16.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC16.cs) | Client Privacy, PK Allowed, Trade Lock & Walk Mode Settings. |
| **`AC 17`** | [`AC17.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC17.cs) | Chat communication (Local, Whisper, Team, Guild, World). |
| **`AC 18`** | [`AC18.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC18.cs) | Visual Emotes & Gesture Animations (Sit, wave, bow, cheer, dance). |
| **`AC 19`** | [`AC19.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC19.cs) | Player grid coordinate pathfinding, continuous walking steps, and direction orientation. |
| **`AC 20`** | [`AC20.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC20.cs) | NPC talk dialogue interaction (`Sub 1`), portal warp navigation (`Sub 8`), ocean sailing. |
| **`AC 21`** | [`AC21.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC21.cs) | NPC Dialogue Choice Selection & Branching Navigation. |
| **`AC 22`** | [`AC22.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC22.cs) | Dynamic Entity State, NPC Roaming Path & Companion Movement. |
| **`AC 23`** | [`AC23.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC23.cs) | Inventory operations: move (`Sub 10`), equip (`Sub 11`), unequip (`Sub 12`), compound synthesis (`Sub 14`), quick refill / tent (`Sub 15`), use item (`Sub 96`), stack sync (`Sub 208`). |
| **`AC 24`** | [`AC24.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC24.cs) | NPC Merchant Shop Buying & Selling Transactions. |
| **`AC 25`** | [`AC25.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC25.cs) | Secure Player-to-Player Trading Engine (Offer, Gold, Lock, Confirm). |
| **`AC 26`** | [`AC26.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC26.cs) | In-Game Currency & Gold Wallet Management. |
| **`AC 27`** | [`AC27.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC27.cs) | Item Discard / Trash disposal (`Sub 2`), Bulletin queries & announcements. |
| **`AC 28`** | [`AC28.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC28.cs) | Character Titles, Badges & Achievement System. |
| **`AC 29`** | [`AC29.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC29.cs) | Props Keeper Safe Deposit & Item Bank Storage (`Sub 1`, `Sub 2`, `Sub 6`). |
| **`AC 30`** | [`AC30.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC30.cs) | Storage Window Multi-Page Navigation & Bulk Sync (`Sub 1`, `Sub 5`). |
| **`AC 31`** | [`AC31.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC31.cs) | Pet Hotel Companion Storage & Management. |
| **`AC 32`** | [`AC32.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC32.cs) | Visual Emotion Bubble (`Sub 1`), Action/Sit/Rest Pose (`Sub 2`), Action Stop / Dialogue Close Reset (`Sub 3`). |
| **`AC 33`** | [`AC33.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC33.cs) | Player settings, chat channel visibility, PK/Trade toggles (`Sub 1`), and Team Auto-Follow Walk-Along (`Sub 5`). |
| **`AC 34`** | [`AC34.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC34.cs) | Inspect Target Character (Attributes, Gear, Element). |
| **`AC 35`** | [`AC35.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC35.cs) | Lucky Draw carnival games and Item Mall redemption. |
| **`AC 36`** | [`AC36.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC36.cs) | Quest Log Query & Objective Detail Inspection. |
| **`AC 37`** | [`AC37.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC37.cs) | Quest Objective Step Progression & Dialog Advance. |
| **`AC 38`** | [`AC38.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC38.cs) | Pet AI Combat Stance & Loyalty Mode Settings. |
| **`AC 39`** | [`AC39.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC39.cs) | Quest journal tracking, multi-stage dialogue step advancement, and quest requirements. |
| **`AC 40`** | [`AC40.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC40.cs) | Alchemy & Compound Crafting (Item synthesis formulas). |
| **`AC 41`** | [`AC41.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC41.cs) | Equipment Repair & Maintenance (Single and Bulk repair). |
| **`AC 42`** | [`AC42.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC42.cs) | Player Tent Interior Entry & Exit Navigation. |
| **`AC 43`** | [`AC43.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC43.cs) | World Map Tent Deployment & Pack Down. |
| **`AC 44`** | [`AC44.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC44.cs) | Active Visual Title Badge Broadcast. |
| **`AC 45`** | [`AC45.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC45.cs) | ATM & Bank Currency Management (Deposit, Withdraw, Balance). |
| **`AC 46`** | [`AC46.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC46.cs) | Hotbar Shortcut Key Assignments (F1–F8 binds). |
| **`AC 47`** | [`AC47.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC47.cs) | Pet Riding & Mount Activation. |
| **`AC 48`** | [`AC48.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC48.cs) | Ocean Vehicle Piloting (Raft, Canoe, Steamer). |
| **`AC 49`** | [`AC49.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC49.cs) | Vehicle Engine Fuel & Durability System. |
| **`AC 50`** | [`AC50.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC50.cs) | Guild administration (Guild creation, ranks, member rosters, guild base access). |
| **`AC 51`** | [`AC51.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC51.cs) | Guild Rank & Administrative Authority Assignment. |
| **`AC 52`** | [`AC52.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC52.cs) | Guild Notice & MOTD Announcement Board. |
| **`AC 53`** | [`AC53.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC53.cs) | Guild Headquarters Furniture & Architecture Layout. |
| **`AC 54`** | [`AC54.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC54.cs) | Item Mall Catalog Query & Point Balance. |
| **`AC 55`** | [`AC55.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC55.cs) | Item Mall Instant Purchase & Delivery. |
| **`AC 56`** | [`AC56.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC56.cs) | Item Mall Player Gifting Protocol. |
| **`AC 57`** | [`AC57.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC57.cs) | Carnival Minigame Session & Voucher Generation. |
| **`AC 58`** | [`AC58.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC58.cs) | Carnival Ticket Voucher Exchange & Prize Redemption. |
| **`AC 59`** | [`AC59.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC59.cs) | World Boss Dynamic Event Broadcast & Timers. |
| **`AC 60`** | [`AC60.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC60.cs) | World Boss Raid Arena Entrance. |
| **`AC 61`** | [`AC61.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC61.cs) | Rebirth Awakening Quest Stages & Token Redemption. |
| **`AC 62`** | [`AC62.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC62.cs) | Rebirth / Superior Class awakening (Killer, Warrior, Knight, Mage, Priest, Wit). |
| **`AC 63`** | [`AC63.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC63.cs) | Tent interior housing: deploy tent, furniture placement, grid rotations, crafting machines. |
| **`AC 64`** | [`AC64.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC64.cs) | Marriage chapel rituals, proposal ring exchange, and matrimonial bonds. |
| **`AC 65`** | [`AC65.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC65.cs) | Player personal market stall (Set sale items, pricing, purchase transactions). |
| **`AC 66`** | [`AC66.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC66.cs) | Superior Class Reborn Skill Tree & Points Allocation. |
| **`AC 67`** | [`AC67.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC67.cs) | Equipment Socket Gem Inlay (Diamond stat boost). |
| **`AC 68`** | [`AC68.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC68.cs) | Equipment Socket Extraction & Gem Reclamation. |
| **`AC 69`** | [`AC69.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC69.cs) | Pet Rebirth & Evolution System. |
| **`AC 70`** | [`AC70.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC70.cs) | Companion Specialized Equipment Socket Inlay. |
| **`AC 71`** | [`AC71.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC71.cs) | Reborn Pet Awakening Ability & Skill Unlock. |
| **`AC 72`** | [`AC72.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC72.cs) | Guild Castle Siege War Registration. |
| **`AC 73`** | [`AC73.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC73.cs) | Guild War Battlefield Scoring & Territory Control. |
| **`AC 74`** | [`AC74.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC74.cs) | Castle Siege Territory Tax Gold & Chest Distribution. |
| **`AC 75`** | [`AC75.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC75.cs) | Daily Quest Board & Bounty Contracts. |
| **`AC 76`** | [`AC76.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC76.cs) | Daily Check-in & Consecutive Login Calendar Rewards. |
| **`AC 77`** | [`AC77.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC77.cs) | 12 Zodiac Palaces Challenge Trial Stages. |
| **`AC 78`** | [`AC78.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC78.cs) | 20-Round Combat Arena Gauntlet Trial. |
| **`AC 79`** | [`AC79.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC79.cs) | Sky Garden Instance Raid Floors. |
| **`AC 80`** | [`AC80.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC80.cs) | Underworld / Hell Instance Trial Floors. |
| **`AC 81`** | [`AC81.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC81.cs) | Leaderboards & Top Player Rankings. |
| **`AC 82`** | [`AC82.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC82.cs) | Companion & Pet Amity management, feeding, and bank/tent storage. |
| **`AC 83`** | [`AC83.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC83.cs) | Pet Training & Attribute Redistribution Pills. |
| **`AC 84`** | [`AC84.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC84.cs) | Companion Skill Upgrade & Evolution. |
| **`AC 85`** | [`AC85.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC85.cs) | Event notifications, Team PVP tournament scheduling, and arena matchmaking. |
| **`AC 86`** | [`AC86.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC86.cs) | Tournament Ladder Match Results & Standings. |
| **`AC 87`** | [`AC87.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC87.cs) | Arena Spectator Viewing Mode. |
| **`AC 88`** | [`AC88.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC88.cs) | Tent Crafting Machinery Automation (Furnace, Loom, Anvil). |
| **`AC 89`** | [`AC89.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC89.cs) | Automated Crafting Queue Collect & Harvest. |
| **`AC 90`** | [`AC90.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC90.cs) | Resource Gathering (Fishing, Mining, Woodcutting nodes). |
| **`AC 91`** | [`AC91.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC91.cs) | Farming & Agricultural Crop Management. |
| **`AC 92`** | [`AC92.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC92.cs) | Animal Husbandry & Livestock Breeding. |
| **`AC 104`** | [`AC104.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC104.cs) | In-game mailbox system (Send/receive mail messages and attached items). |
| **`AC 105`** | [`AC105.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC105.cs) | Mailbox Parcel Delivery & COD Mail. |
| **`AC 183`** | [`AC183.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC183.cs) | Heartbeat & Client Liveness Synchronization (`Sub 17` ping ACK + `Sub 11` sync). |
| **`AC 184`** | [`AC184.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC184.cs) | Extended System Configuration Flags. |
| **`AC 186`** | [`AC186.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC186.cs) | Cutscene / CG Animation Sync Handshake (`Sub 9` Playback ACK + `Sub 12` Camera Release). |
| **`AC 191`** | [`AC191.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC191.cs) | Anti-Bot Macro Verification & Security Challenge. |
| **`AC 199`** | [`AC199.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC199.cs) | Extended Event & Promotional Reward Distributions. |

---

## 3. Official Traffic Captures & Detailed Packet Implementations
For the deep dive into the 38 official `.pcapng` gameplay captures and full implementation details of compound synthesis, quick recovery, in-game mail, and trash discarding, refer to:
- [docs/pcap_traffic_analysis_and_packet_implementations.md](file:///d:/GitHub/Wonderland-Private-Server/docs/pcap_traffic_analysis_and_packet_implementations.md)


