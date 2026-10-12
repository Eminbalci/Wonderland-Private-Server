# Binary Asset Formats and Database Structures Specification

## 1. Overview of Client Binary Asset Pipeline

All game content consumed by [`aLogin.exe`](file:///D:/GitHub/ghidradecodeyapma/WLRI/aLogin.exe) is stored in binary formatted files located in `WLRI\data\` and `WLRI\user\`. This document specifies the exact binary layouts, encryption keys, record lengths, and header structures for each asset file:

```
WLRI\
  ├── data\
  │     ├── odd.dat / odd_d01.dat   (1.42 GB Master Resource Package)
  │     ├── Ground.MMG              (21.9 MB Map Tile Textures)
  │     ├── Eve.emg                 (5.16 MB Event Scripts & NPC Entities)
  │     ├── Talk.dat                (5.10 MB Dialogues & Quest Text)
  │     ├── Item.dat                (3.28 MB Item Database)
  │     ├── SkillData.MBTM          (1.24 MB Skill Combat Animations)
  │     ├── Mark.dat                (1.19 MB Guild Emblem Bitmaps)
  │     ├── Npc.dat                 (680 KB NPC Entity Definitions)
  │     ├── SceneData.dat           (153 KB Scene Geometry & Portals)
  │     ├── Skill.dat               (141 KB Skill Mechanics Database)
  │     ├── Compound2.Dat           (51.2 KB Expanded Alchemy Recipes)
  │     ├── Compound.dat            (10.9 KB Standard Alchemy Synthesis)
  │     ├── Formula.dat             (407 B Experience & Stat Coefficients)
  │     ├── TrafficSetting.txt      (28.8 KB Vehicle Speed & Fuel Config)
  │     └── AdjustRidePetPos.txt    (10.9 KB Mount Visual Offsets)
  └── user\
        ├── save.dat                (Local Profile, Hotkeys, Display Options)
        └── Map\<ID>.MapData        (Terrain Collision Grids & Obstacles)
```

---

## 2. Asset Specifications by File

### 2.1 `Formula.dat` (407 Bytes)
* **File Encryption:** None (Raw IEEE-754 Little-Endian binary data).
* **Structure:**
  * 4-byte header: Magic count / version.
  * Array of 64-bit IEEE-754 double precision floating-point multipliers (`double`).
  * Normal character EXP curve coefficients, Rebirth EXP multipliers, Companion pet EXP leveling curves, and combat monster EXP distribution weights.

### 2.2 `Npc.dat` (680,340 Bytes)
* **File Encryption:** Symmetric XOR with 16-bit key **`0x5209`** (21001 decimal).
* **Record Structure:**
  * Header: 4-byte NPC entry count (4,928 authentic entries).
  * 138-byte fixed record per NPC template:
    * `TemplateID: UInt32`
    * `Name: Char[32]` (Big5 encoded string)
    * `Level: Byte`
    * `Element: Byte` (1 = Earth, 2 = Water, 3 = Fire, 4 = Wind)
    * `MaxHP: UInt32`, `MaxSP: UInt32`
    * `ATK: UInt16`, `DEF: UInt16`, `MATK: UInt16`, `MDEF: UInt16`, `SPD: UInt16`
    * `SkillID1..3: UInt16[3]` (Combat abilities)
    * `DropItemIDs: UInt16[4]`, `DropRates: UInt16[4]`

### 2.3 `Item.dat` / `itemDat.wpdat` (3,289,143 Bytes)
* **File Encryption:** None / standard XOR stream depending on patch level.
* **Record Structure:**
  * 47-byte fixed record per item (4,818 authentic items):
    * `ItemID: UInt32` (Unique template ID)
    * `Name: Char[20]` (Item display name)
    * `ItemType: Byte` (Weapon, Armor, Consumable, Material, Quest, Machine)
    * `EquipSlot: Byte` (Head, Body, Weapon, Shield, Shoes, Ring, Accessory)
    * `LevelReq: Byte` (Level threshold required to equip)
    * `Stats: Int16[6]` (ATK, DEF, MATK, MDEF, SPD, HP/SP bonus)
    * `Durability: UInt16` (Default maximum durability)
    * `MaterialType: Byte` (Material class `0x01` Flower to `0x3C` Ref Oil for compounding)
    * `MaterialGrade: Byte` (Alchemy synthesis level)

### 2.4 `Talk.dat` (5,108,248 Bytes)
* **File Encryption:** Variable record length table.
* **Record Structure:**
  * Header: 4-byte record count (17,494 authentic dialogue records).
  * 292-byte fixed index header per talk record:
    * `TalkID: UInt32` (24-bit dialogue key)
    * `SpeakerName: Char[24]`
    * `TextPayloadLength: UInt16`
    * `TextPayload: Char[Length]` (Big5 dialogue text supporting formatting tokens `#C`, `#P`, `#R`)

### 2.5 `Eve.emg` (5,164,521 Bytes)
* **File Structure:**
  * 1,119 Maps, 10,644 event scripts, 1,412 PreEvents, 8,181 native NPCs, and 2,791 warps.
  * Binary sections parsed sequentially by `FUN_00498208`:
    * Section 1: `Read Npc.` & `Read Npc:: ParaNode`
    * Section 2: `Read Door.`
    * Section 3: `Read Mine.`
    * Section 4: `Read Goods.`
    * Section 5: `Read Eve.`
    * Section 6: `Read Eve->Cdn.` (21-byte PreEvent condition bytecodes)
    * Section 7: `Read Eve->Cdn->Result.` (Action opcodes)
    * Section 8: `Read Battle.` (Left and Right member arrays)
    * Section 9: `Read Convey.`
    * Section 10: `Read Group.` & `Read LeadGroup.`

### 2.6 `SceneData.dat` (153,532 Bytes)
* **Master Scene Loader:** `FUN_00497c2c`
* **Format Version Signature:** `0x70` (112 decimal).
* **Record Structure:**
  * Scene ID, pixel dimensions (Width $\times$ Height), background music ID (`BGMxxxx`), default weather flags, and doorway portal trigger boundaries.

### 2.7 `user\save.dat`
* **File Encryption:** Obfuscated using symmetric XOR key **`121`** (`0x79`).
* **Record Contents:**
  * Screen resolution, fullscreen mode flag, BGM volume (0-100), SFX volume (0-100), hotbar key mappings (1-9, F1-F12), auto-decline filters.

### 2.8 `user\Map\<XOR_MapID>.MapData`
* **File Encryption:** Filename XOR decrypted with key **`0x190C`** (6412 decimal).
* **Record Structure:**
  * 36-byte records (`0x24`) per grid block representing collision flags, elevation, water tiles, and walkable terrain.
