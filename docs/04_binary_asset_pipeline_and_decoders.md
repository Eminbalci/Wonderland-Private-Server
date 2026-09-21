# Binary Asset Pipeline and Asset Decoders

## 1. Architectural Overview

Wonderland Online client assets utilize proprietary binary layouts, reversed string buffers, and layered XOR subtraction ciphers. The server pipeline loads, decrypts, and caches these assets into in-memory lookup tables during startup to achieve $O(1)$ random-access performance during real-time game loops.

---

## 2. In-Memory Asset Decoders

```
+-------------------+---------------+---------------+-----------------------------------------------+
| Asset File        | Record Size   | Total Records | Cryptographic Cipher / Encoding               |
+-------------------+---------------+---------------+-----------------------------------------------+
| Data/Npc.dat      | 138 Bytes     | 4,928 NPCs    | XOR 0x5209 Subtraction: ((raw ^ 0x5209) - 1)  |
| Data/Item.dat     | 45 Bytes      | 60,000+ Items | Multi-tier XOR Subtraction (0xB80F4B4, etc.)  |
| Data/Skill.dat    | 148 Bytes     | 800+ Skills   | Multi-tier XOR Subtraction (0x6EA0, 0xFD)     |
| Data/Talk.dat     | 292 Bytes     | 17,494 Dialogs| Reversed ASCII Byte Buffer                    |
| Data/Mark.dat     | 526/553 Bytes | 2,154 Marks   | Reversed ASCII Byte Buffer                    |
| Data/SceneData.dat| 131 Bytes     | 2,791 Warps   | Reversed ASCII String at Offset 14            |
| Data/Eve.emg      | Variable      | 1,119 Maps    | Compound Event Bytecode Structure             |
| Data/Formula.dat  | 407 Bytes     | 1 File        | IEEE-754 64-Bit Double-Precision Floating Pt  |
| Data/Compound2.dat| 65 Bytes      | 500+ Recipes  | Multi-tier XOR Subtraction (0xFBBC, 0xD3)     |
+-------------------+---------------+---------------+-----------------------------------------------+
```

---

## 3. Deep Asset Specifications

### 3.1 Monster & NPC Database (`Data/Npc.dat`)
* **Decoder:** [`SceneDataManager.LoadNpcNames`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataFiles/SceneDataManager.cs) and [`MonsterDropManager.LoadFromNpcDat`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Battle/MonsterDropManager.cs).
* **Record Layout (138 Bytes):**
  - Offset `0`: Header byte.
  - Offset `1..10`: NPC Name (Reversed ASCII bytes).
  - Offset `12..13`: Decrypted Template ID via `((rawId ^ 0x5209) - 1) & 0xFFFF`.
  - Offset `14..63`: Base combat attributes (MaxHP, MaxSP, STR, CON, INT, WIS, AGI, Element).
  - Offset `64..73`: 5 Drop Item IDs (Decrypted via `((raw ^ 0x5209) - 1)`).
  - Offset `74..137`: Model scale, sprite ID, walk speed, animation frames.

### 3.2 Item Database (`Data/itemDat.wpdat` & `Data/Item.dat`)
* **Decoder:** [`PhxItemDat.Load`](file:///D:/GitHub/Wonderland-Private-Server/PhoenixData/DataFiles/PhxItemDat.cs).
* **Decrypted Canonical Asset (`itemDat.wpdat`):** The server prioritizes `itemDat.wpdat` (4,776 pre-decrypted items, 47 bytes/record) over raw `Item.dat` to ensure instant zero-overhead deserialization and accurate item category/stackability attributes.
* **Record Layout (47 Bytes Fixed Sequential Struct, `Pack = 1`):**
  - Offset `0`: Name string length (`len`).
  - Offset `1..20`: Item name (ASCII bytes).
  - Offset `21`: Item Category / Type (23=Food, 25=Consumable/Material, 12=Body, 15=Shoes, etc.).
  - Offset `22..23`: Item ID (`UInt16 LE`).
  - Offset `24..25`: Icon Index (`UInt16 LE`).
  - Offset `26..27`: Large Icon Index (`UInt16 LE`).
  - Offset `28..29`: Equipment Slot (`Equippos`: 1=Head, 2=Body, 3=Weapon, 4=Wrist, 5=Shoes, 6=Special).
  - Offset `30..31`: Required Level.
  - Offset `32`: Item Rank / Grade.
  - Offset `33..34`: Inventory Grid Width & Height.
  - Offset `35..38`: Status Type 1 & 2 (ATK, DEF, MATK, MDEF, SPD, HP, SP).
  - Offset `39..46`: Status Up 1 & 2 values.

### 3.3 Skill Database (`Data/Skill.dat`)
* **Decoder:** [`SkillManager.LoadSkillDatabase`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/SkillRelated/SkillManager.cs).
* **Record Layout (148 Bytes):**
  - Offset `0`: Name length prefix.
  - Offset `1..20`: Skill Name (Reversed ASCII).
  - Offset `21`: Skill Target Type (`((b ^ 0xFD) - 4)`).
  - Offset `22..23`: Skill ID (`((w ^ 0x6EA0) - 4)`).
  - Offset `24..25`: Base SP Consumption.
  - Offset `26`: Element Affinity (0=Earth, 1=Water, 2=Fire, 3=Wind).
  - Offset `27..28`: Base Damage / Power Coefficient.
  - Offset `97..98`: Skill Notebook UI Order Index.

### 3.4 Dialogue Database (`Data/Talk.dat`)
* **Decoder:** [`PhxTalkDat`](file:///D:/GitHub/Wonderland-Private-Server/PhoenixData/DataFiles/PhxTalkDat.cs).
* **Record Layout (292 Bytes Fixed Struct):**
  - Exactly 17,494 entries.
  - Offset `0..1`: Dialogue ID (`UInt16`).
  - Offset `2`: String length byte (`len`, capped at 250).
  - Offset `292 - 35 - len`: Text string payload (Stored in reversed byte order).
  - Filtering: Strips leading dialogue control prefix `fffff`.
  - Dual Indexing: Supports both direct record index lookup (`_talkByIndex`) and binary file seek offset lookup (`_talkByOffset`).

### 3.5 Quest Journal Database (`Data/Mark.dat`)
* **Decoder:** [`PhxMarkDat`](file:///D:/GitHub/Wonderland-Private-Server/PhoenixData/DataFiles/PhxMarkDat.cs) and [`QuestManager.LoadQuests`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/QuestRelated/QuestManager.cs).
* **Record Layout (526 Bytes Payload, 553 Bytes Stride):**
  - Exactly 2,154 mark records.
  - Offset `200..265` (65 Bytes): Quest Title in reversed Big5/ASCII.
  - Offset `266..525` (260 Bytes): Quest Log / Narrative body in reversed ASCII.
  - Pattern Matching: Decodes `#(\d{2})([^#]*)` tags identifying in-progress steps (`#01`, `#02`) and completion summaries (`#99`).

### 3.6 Map & Warp Coordinates (`Data/SceneData.dat`)
* **Decoder:** [`SceneDataManager`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataFiles/SceneDataManager.cs).
* **Record Layout (131 Bytes):**
  - Contains 2,791 warp records.
  - Offset `0..3`: Source Map ID.
  - Offset `4..7`: Target Map ID.
  - Offset `8..11`: Target Isometric X Coordinate.
  - Offset `12..13`: Target Isometric Y Coordinate.
  - Offset `14..127`: Map Name in reversed ASCII text.

### 3.7 Game Event Bytecodes (`Data/Eve.emg`)
* **Decoder:** [`EveManager`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataFiles/EveLoader.cs).
* **Binary Sections per Map (1,119 Maps):**
  - Map Header & Isometric Bounds.
  - Warp Entry Gate Points.
  - Overworld NPC Placements (8,181 entities).
  - PreEvent Visibility Chunk Tables (1,412 condition blocks).
  - Event Script Bytecodes (10,644 event scripts).
  - Ground Item Harvesting Nodes (`ItemAreas`, 209 items across 77 maps).

### 3.8 Experience & Attribute Coefficients (`Data/Formula.dat`)
* **Decoder:** [`FormulaManager`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Battle/FormulaManager.cs).
* **Schema (407 Bytes):**
  - Byte `0`: Version (`0x02`).
  - Bytes `1..360`: 45 64-bit IEEE-754 double precision constants (STR/CON/INT/WIS/AGI conversions, Exp Exponent `3.1` at offset 241).
  - Bytes `361..364`: Base flat EXP constant UInt32 `5`.
  - Bytes `365..406`: 21 UInt16 level gates (Level 180 standard cap, Level 94 reborn unlock, Level 100 rebirth baseline).

### 3.9 Tent Machine Recipes (`Data/Compound2.dat`)
* **Decoder:** [`cCompound2Dat`](file:///D:/GitHub/Wonderland-Private-Server/Src/DataManagement/DataFiles/cCompound2Dat.cs).
* **Record Layout (65 Bytes):**
  - Ciphers: Byte `((b ^ 0xD3) - 3)`, UInt16 `((w ^ 0xFBBC) - 3)`, UInt32 `((d ^ 0x0A06F965) - 3)`.
  - Fields: `resultID` (UInt16), `planID` (UInt16), `toolID` (UInt16), `ammtRecv` (Byte), 5 Material Requirements (15 bytes: pairs of MaterialID and Count), `buildTime` (UInt16).
