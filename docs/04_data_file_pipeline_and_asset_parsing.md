# Data File Pipeline and Client Asset Parsing Engine

## 1. Architectural Overview

The Wonderland Online private server utilizes direct in-memory binary asset decoders rather than static SQLite translation dumps for core gameplay tables. This guarantees 100% protocol fidelity with official client behavior, dynamic multi-language text alignment, and zero data drift between client and server runtimes.

Asset discovery is coordinated by [`PathHelper`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/RCLibrary.Core/PathHelper.cs#L22), scanning `Data/`, client root, or subdirectories specified in `SERVER.INI`.

---

## 2. In-Memory Asset Decoders

### 2.1 NPC Database (`Data/Npc.dat`)

The `Npc.dat` file contains canonical data for 4,928 NPCs and monsters. It is processed in [`SceneDataManager`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataFiles/SceneDataManager.cs#L102) and [`MonsterDropManager`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Battle/MonsterDropManager.cs#L612).

#### Binary Layout (138-Byte Fixed Struct)
```
+---------------+---------------+---------------------------------------+
| Offset (Byte) | Type          | Description                           |
+---------------+---------------+---------------------------------------+
| 0             | Byte          | Record Header / Delimiter             |
| 1..10         | Char[10]      | NPC Name (Reversed ASCII sequence)    |
| 11            | Byte          | Null padding / flags                  |
| 12..13        | UInt16        | Ciphered NPC ID: (Val ^ 0x5209) - 1   |
| 14..63        | Byte[50]      | Combat attributes (HP, MP, STR, etc.) |
| 64..65        | UInt16        | Drop Item 1: (Val ^ 0x5209) - 1       |
| 66..67        | UInt16        | Drop Item 2: (Val ^ 0x5209) - 1       |
| 68..69        | UInt16        | Drop Item 3: (Val ^ 0x5209) - 1       |
| 70..71        | UInt16        | Drop Item 4: (Val ^ 0x5209) - 1       |
| 72..73        | UInt16        | Drop Item 5: (Val ^ 0x5209) - 1       |
| 74..137       | Byte[64]      | Animation, scale, and sound metadata  |
+---------------+---------------+---------------------------------------+
```

#### Decryption Formula & Logic
```csharp
// Decrypt 16-bit NPC Identifier:
ushort rawId = BitConverter.ToUInt16(bytes, offset + 12);
uint npcId = (uint)(((rawId ^ 0x5209) - 1) & 0xFFFF);

// Reverse ASCII Name Parsing:
var chars = new List<char>();
for (int p = offset + 10; p >= offset + 1; p--)
{
    byte b = bytes[p];
    if (b >= 32 && b <= 126) chars.Add((char)b);
}
string npcName = new string(chars.ToArray()).Trim();
```

---

### 2.2 Item Database (`Data/Item.dat`)

The `Item.dat` database is parsed by [`PhxItemDat`](file:///D:/GitHub/Wonderland-Private-Server/PhoenixData/DataFiles/PhxItemDat.cs#L12). Each record is represented by the 45-byte unmanaged struct [`PhxItemInfo`](file:///D:/GitHub/Wonderland-Private-Server/PhoenixData/DataFiles/PhxItemInfo.cs#L6).

#### Binary Layout (45-Byte Sequential Struct, `Pack = 1`)
```
+---------------+---------------+---------------------------------------+
| Offset (Byte) | Type          | Description                           |
+---------------+---------------+---------------------------------------+
| 0             | Byte          | Item Name Length                      |
| 1..20         | Byte[20]      | Item Name Bytes (ASCII)               |
| 21            | Byte          | Item Type (Consumable, Weapon, etc.)  |
| 22..23        | UInt16        | Item ID                               |
| 24..25        | UInt16        | Inventory Icon ID                     |
| 26..27        | UInt16        | Large Preview Icon ID                 |
| 28..29        | UInt16        | Equipment Position Bitmask            |
| 30..31        | UInt16        | Item Required Level                   |
| 32            | Byte          | Item Grade / Rank                     |
| 33            | Byte          | Grid Height Dimension                 |
| 34            | Byte          | Grid Width Dimension                  |
| 35..38        | UInt16[2]     | Status Type Modifiers                 |
| 39..46        | Int32[2]      | Status Up Magnitudes                  |
+---------------+---------------+---------------------------------------+
```

#### Cipher Masks
```csharp
// 32-bit fields:
val = Convert.ToUInt32((val ^ 0xB80F4B4) - 9);

// 16-bit fields:
val = Convert.ToUInt16((val ^ 0xEFC3) - 9);

// 8-bit fields:
val = Convert.ToByte((val ^ 0x9A) - 9);
```

---

### 2.3 Dialogue Database (`Data/Talk.dat`)

`Talk.dat` houses all client-side dialogue strings (17,494 records). The parsing engine [`PhxTalkDat`](file:///D:/GitHub/Wonderland-Private-Server/PhoenixData/DataFiles/PhxTalkDat.cs#L13) supports dual lookup indexing:
1. **1-Based Record Index:** Maps logical talk IDs directly to the record row (`1..17,494`).
2. **Direct Byte Offset:** Resolves raw byte offsets encoded in legacy event scripts (e.g., `0x063ED2`).

#### Binary Layout (292-Byte Fixed Struct)
```
+---------------+---------------+---------------------------------------+
| Offset (Byte) | Type          | Description                           |
+---------------+---------------+---------------------------------------+
| 0..1          | UInt16        | Talk Identifier                       |
| 2             | Byte          | Text Length (N <= 250 bytes)          |
| 3..(291 - 35) | Byte[]        | Text Area (Reversed ASCII copy)       |
| 257..261      | Char[5]       | "fffff" prefix boundary tag           |
| 262..291      | Byte[30]      | Metadata and voice footer             |
+---------------+---------------+---------------------------------------+
```

Reversed string reconstruction strips internal formatting tags:
```csharp
int textStart = recOffset + recordSize - 35 - len;
byte[] textBytes = new byte[len];
for (int i = 0; i < len; i++)
{
    textBytes[i] = data[textStart + len - 1 - i]; // Reverse text
}
string dialogue = Encoding.Default.GetString(textBytes).Trim();
if (dialogue.StartsWith("fffff"))
    dialogue = dialogue.Substring(5).Trim();
```

---

### 2.4 Map Scene Metadata (`Data/SceneData.dat`)

`SceneData.dat` defines official map names and world cluster topologies without requiring hardcoded dictionary mappings.

* **Record Size:** 131 Bytes.
* **Text Offset:** Offset 14 containing reversed ASCII characters.
* **Cluster Mapping:** Links logical scene indices (`r = 1056..1158`) to canonical game map IDs (`10000`, `10035`, `12000`, `12001`, `12002`, `12010`, etc.).

---

### 2.5 Event Scripting Engine (`Data/eve.Emg`)

`eve.Emg` is the compiled binary event bytecode database powering all NPC interactions, quests, cutscenes, warp triggers, and chest rewards across the entire game world.

#### Data Hierarchy
1. **Map Entry (`EveMapData`):**
   * Indexed by Map ID (`ushort`).
   * Contains two primary collections: `Npclist` and `Events`.
2. **Npc Entry (`NpcListEntry`):**
   * Associates an in-world `clickId` with template `npcId`, default coordinates, and registered `Events` (list of event IDs).
3. **Event Entry (`EventsinMapEntries`):**
   * Contains unique `clickID` matching the NPC trigger.
   * Holds an array of `EventSubEntry` branches.
4. **Event SubEntry (`EventSubEntry`):**
   * Defines pre-conditions (quest completion flags, item ownership, equipped items) and an array of executable bytecode actions.

#### Core Bytecode Action Types
* **Dialogue Triggers:** Calls `AC 20 Sub 1` with a resolved `Talk.dat` ID.
* **Item Grants & Removals:** Grants reward items or deducts quest prerequisites.
* **Gold Grants & Deductions:** Modifies character currency balance.
* **Mobility & Stage Control:** Issues `AC 20:8` (mobility lock/unlock), camera shakes, and sound playback.
* **Warp Triggers:** Initiates transitions to target map coordinates (`WarpData`).
* **Companion Recruitment:** Recruits NPCs into active party slots and triggers companion overworld despawn isolation.
