# 03 - Network Protocol and Action Codes Specification

## 1. Binary Wire Protocol Framing

Every packet transmitted between the game client and server adheres to a strict binary frame:

```
+----------------+----------------+----------------+----------------+-------------------------+
| Length [0]     | Length [1]     | ActionCode [2] | SubCode [3]    | Payload [4..N]          |
| (High Byte)    | (Low Byte)     | (1 Byte)       | (1 Byte)       | (Variable Length Data)  |
+----------------+----------------+----------------+----------------+-------------------------+
```

- **Header Size**: 2 Bytes representing total packet length excluding the header itself.
- **Action Code (AC)**: 1 Byte primary subsystem opcode (e.g., `0x01` to `0xBA`).
- **SubCode**: 1 Byte message variant or transaction step.
- **Endianness**: Little-Endian for multi-byte numeric primitives (`uint16`, `uint32`), UTF-8 for string buffers.

---

## 2. Master Action Code Catalog

| AC | Name | Direction | SubCodes | Description |
| :--- | :--- | :--- | :--- | :--- |
| **AC 1** | Ping / Heartbeat | Both | `0` | Connection liveness verification; client sends ping, server replies pong. |
| **AC 2** | Character Selection | Both | `1, 2, 4, 7` | Character creation (`Sub 1`), deletion (`Sub 2`), world entry selection (`Sub 4`), character stats (`Sub 7`). |
| **AC 3** | Movement & Pathing | Both | `1, 2, 3, 5, 7` | Coordinate walking (`Sub 1`), stop position (`Sub 2`), direction face (`Sub 3`), jump (`Sub 5`), instant warp (`Sub 7`). |
| **AC 4** | Map Entity Replication | S->C | `1, 2, 3, 4` | Player visual spawn (`Sub 1`), entity despawn (`Sub 2`), mob/NPC spawn (`Sub 3`), map load acknowledgment (`Sub 4`). |
| **AC 5** | Chat & Skill Unlocks | Both | `1, 2, 3, 4, 5, 11` | Public chat (`Sub 1`), Whisper (`Sub 2`), Party chat (`Sub 3`), Guild chat (`Sub 4`), World shout (`Sub 5`), Stunt/skill unlock (`Sub 11`). |
| **AC 6** | Trade System | Both | `1, 2, 3, 4` | Trade request, item lock, gold wager, trade confirmation. |
| **AC 7** | Party & Team Engine | Both | `1, 2, 3, 4, 5` | Team invitation (`Sub 1`), accept (`Sub 2`), leave team (`Sub 3`), kick member (`Sub 4`), request to join (`Sub 5`). |
| **AC 8** | Inventory & Attributes | Both | `1, 2, 3, 4, 5, 38` | Use consumable (`Sub 1`), equip item (`Sub 2`), unequip item (`Sub 3`), drop item (`Sub 4`), move slot (`Sub 5`), stat point allocation (`Sub 38`). |
| **AC 9** | Friend List System | Both | `1, 2, 3` | Send friend request (`Sub 1`), delete friend (`Sub 2`), online/offline status update (`Sub 3`). |
| **AC 11** | Battle State Machine | Both | `1, 2, 3, 5` | Enter combat arena (`Sub 1`), round start timer (`Sub 2`), round ready ack (`Sub 3`), companion battle mode toggle (`Sub 5`). |
| **AC 12** | Player Housing (Tent) | Both | `1, 2, 3, 4` | Enter tent interior (`Sub 1`), exit tent (`Sub 2`), place/rotate furniture (`Sub 3`), pack up tent (`Sub 4`). |
| **AC 13** | Team Stat Sync | S->C | `1, 2` | Real-time broadcast of teammates' current HP, MaxHP, SP, MaxSP, and Level. |
| **AC 14** | Dialogue & EVE Script | Both | `1, 2, 3` | Dialogue bubble display (`Sub 1`), question prompt selection (`Sub 2`), event script continuation (`Sub 3`). |
| **AC 15** | Vehicles & Mounts | Both | `1, 2, 3, 10` | Ride/dismount pet (`Sub 1`), board ocean raft (`Sub 2`), airship navigation (`Sub 3`), despawn vehicle (`Sub 10`). |
| **AC 20** | NPC Interaction | C->S | `1` | Player clicks an overworld NPC or clickable chest prop. |
| **AC 22** | Dynamic NPC Movement | S->C | `2, 10` | NPC roaming waypoints (`Sub 2`), NPC despawn after recruitment (`Sub 10`). |
| **AC 23** | System Messaging | S->C | `6, 57` | Floating announcement / marquee banner (`Sub 57`), voucher redemption acknowledgement (`Sub 6`). |
| **AC 29** | Bank Storage | Both | `1, 2, 3` | Open vault, deposit item, withdraw item. |
| **AC 30** | Props Keeper Storage | Both | `1, 2` | Open specialized props keeper vault, transfer special items. |
| **AC 31** | Pet Hotel Storage | Both | `1, 2` | Store active companion in hotel (`Sub 1`), retrieve companion from hotel (`Sub 2`). |
| **AC 39** | Guild Management | Both | `1, 2, 3, 4` | Guild creation, invite member, kick member, update guild billboard notice. |
| **AC 50** | Combat Action Command | C->S | `1` | Dispatches player or pet turn decision: Attack, Skill, Guard, Item, Escape, Catch. |
| **AC 51** | Combat Action Animation | S->C | `1` | Replicates damage numbers, critical hits, status buffs, animations, and death flags. |
| **AC 52** | Quest Journal Sync | S->C | `1, 2` | Updates client quest journal entries with active or completed state. |
| **AC 57** | Minigame Engine | Both | `1, 2, 3` | Start minigame, transmit score, award prize vouchers. |
| **AC 75** | Item Mall Subsystem | Both | `1, 2, 3, 4` | Points Mall catalog (`Sub 1`), buy item (`Sub 2`), balance sync (`Sub 3`), Bonus Mall catalog (`Sub 4`). |
| **AC 186** | Cinematic Cutscenes | S->C | `1, 2` | Scripted camera pans, actor walk cues, letterboxing, shipwreck intro sequences. |

---

## 3. High-Frequency Packet Payload Specifications

### AC 75 Sub 3: Item Mall Balance Sync (S->C)
```csharp
SendPacket p = new SendPacket();
p.Pack8(75);                  // Action Code
p.Pack8(3);                   // SubCode: Balance
p.Pack32((uint)points);       // Mall Points balance
p.Pack32((uint)bonusPoints);  // Bonus Mall balance
p.Pack16(0);                  // Reserved / Alignment
p.Pack8(0);                   // Flags
```

### AC 8 Sub 38: Stat Point Allocation (C->S)
```csharp
byte statType = reader.Unpack8(); // 1=STR, 2=CON, 3=INT, 4=WIS, 5=AGI
byte count = reader.Unpack8();    // Points to allocate
// Server validates unallocated_points >= count, decrements pool, adds to target attribute
```

### AC 50 Sub 1: Battle Action Command (C->S)
```csharp
byte actionType = reader.Unpack8(); // 1=Attack, 2=Skill, 3=Guard, 4=Item, 5=Flee, 6=Catch
byte actorPos   = reader.Unpack8(); // Position (0-3 player row, 4-7 pet row)
byte targetPos  = reader.Unpack8(); // Target battle slot (0-7 enemies)
ushort skillOrItemId = reader.Unpack16(); // Skill ID or Inventory Item ID
```

---

## 4. Connection State Machine

```
[TCP Connected] 
       │
       v
[Handshake / Auth] (AC 1)
       │
       v
[Character Selection] (AC 2)
       │
       v
[World Map Hydration] (AC 4:4 -> AC 4:1 -> AC 8:1)
       │
       +-----------------------+
       |                       |
       v                       v
[Overworld State]       [Battle Arena] (AC 11)
(AC 3, AC 14, AC 20)    (AC 50, AC 51)
       │                       │
       +-----------+-----------+
                   │
                   v
            [Disconnection]
          (Flush DB & Despawn)
```
