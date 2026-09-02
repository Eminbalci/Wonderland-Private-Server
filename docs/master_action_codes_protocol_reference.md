# Master Protocol Reference: Network Action Codes (AC)

## 1. Overview
Comprehensive specification of the binary packet protocol between the Wonderland Online Client (`aLogin.exe` / `Main.exe`) and the Private Server (`Wonderland-Private-Server`).

---

## 2. Action Code (AC) Protocol Registry

| AC ID | Handler Class | Description & Primary Sub-Codes |
| :--- | :--- | :--- |
| **`AC 0`** | [`AC0.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC0.cs) | Connection heartbeat and keep-alive null packets. |
| **`AC 2`** | [`AC02.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC02.cs) | Client-server version handshake & ping synchronization (`Recv1`, `Recv2`). |
| **`AC 6`** | [`AC06.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC06.cs) | Character Stat & Attribute allocation (STR, CON, INT, WIS, AGI point spending). |
| **`AC 8`** | [`AC08.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC08.cs) | Skill learning, grading, and stunt skill unlocks (`Recv_1`, sub-codes 27–33). |
| **`AC 9`** | [`AC09.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC09.cs) | Character creation flow, stunt skill assignment, and initial world map spawn. |
| **`AC 11`** | [`AC11.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC11.cs) | Combat engagement triggers (PK player battles, wild monster encounters, NPC combat). |
| **`AC 12`** | [`AC12.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC12.cs) | Turn-based round command selection (Attack, Cast Skill, Defend, Use Item, Flee, Catch). |
| **`AC 13`** | [`AC13.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC13.cs) | Combat round execution engine: animation packets, damage numbers, HP/SP sync, victory/defeat. |
| **`AC 14`** | [`AC14.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC14.cs) | Social relations, Friend List synchronization, and online status notifications. |
| **`AC 15`** | [`AC15.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC15.cs) | Map entity visibility & state broadcasts (Spawn player, NPC, pet, vehicle, ocean raft, despawn). |
| **`AC 19`** | [`AC19.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC19.cs) | Player grid coordinate pathfinding, continuous walking steps, and direction orientation. |
| **`AC 20`** | [`AC20.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC20.cs) | NPC talk dialogue interaction (`Sub 1`), portal warp navigation (`Sub 8`), ocean sailing. |
| **`AC 23`** | [`AC23.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC23.cs) | Inventory operations: equip/unequip equipment, use consumables, swap slots, split stacks. |
| **`AC 32`** | [`AC32.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC32.cs) | Team / Party management (Invite, Accept, Leave, Kick, Transfer Party Leader). |
| **`AC 33`** | [`AC33.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC33.cs) | Player settings & privacy toggles (PK flag, trade lock, team request auto-reject). |
| **`AC 35`** | [`AC35.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC35.cs) | Lucky Draw carnival games and Item Mall redemption. |
| **`AC 39`** | [`AC39.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC39.cs) | Quest journal tracking, multi-stage dialogue step advancement, and quest requirements. |
| **`AC 50`** | [`AC50.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC50.cs) | Guild administration (Guild creation, ranks, member rosters, guild base access). |
| **`AC 62`** | [`AC62.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC62.cs) | Rebirth / Superior Class awakening (Killer, Warrior, Knight, Mage, Priest, Wit). |
| **`AC 63`** | [`AC63.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC63.cs) | Tent interior housing: deploy tent, furniture placement, grid rotations, crafting machines. |
| **`AC 64`** | [`AC64.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC64.cs) | Marriage chapel rituals, proposal ring exchange, and matrimonial bonds. |
| **`AC 65`** | [`AC65.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC65.cs) | Player personal market stall (Set sale items, pricing, purchase transactions). |
| **`AC 82`** | [`AC82.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC82.cs) | Companion & Pet Amity management, feeding, and bank/tent storage. |
| **`AC 85`** | [`AC85.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC85.cs) | Event notifications, Team PVP tournament scheduling, and arena matchmaking. |
| **`AC 104`** | [`AC104.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC104.cs) | In-game mailbox system (Send/receive mail messages and attached items). |
| **`AC 186`** | [`AC186.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC186.cs) | System gift redemption and promotional reward distributions. |
