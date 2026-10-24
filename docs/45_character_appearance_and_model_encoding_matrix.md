# Character Appearance, Model Encoding & Role Matrix Specification

## 1. Overview & Reverse Engineering Summary

In Wonderland Online (WLO), character visual identity across the game client (`aLogin.exe`) is governed by an interrelated tripartite system:
1. **Network Wire Representations:** Packets for character creation (`AC 9:1`), slot selection (`AC 63:1`), login state broadcast (`AC 3`), and map presence broadcast (`AC 4`).
2. **Client Role Dispatcher (`FUN_0049eb74` at `0x0049eb74`):** Translates physical `(BodyStyle, HeadStyle)` coordinates into canonical `RoleID`s (1..15) used for portraits, voice lines, and character-locked equipment meshes.
3. **Sprite Asset Naming Convention (`FUN_00201de4`):** Dynamically loads character walker dolls and paperdoll sprites via the template `JRole_<Body>_<Head>` (or `BRole_` for Breillat).

---

## 2. Authentic Character Role Matrix (`FUN_0049eb74`)

The client resolves character profiles through `FUN_0049eb74(char Body, char Head)`:

```
+-----------+-------+---------+---------------+-----------------+-------------------------+-----------------------------------+
| Body Style| Head  | Role ID | Character     | Portrait Icon   | Sprite Asset Identifier | Beginner Equipment Items          |
+-----------+-------+---------+---------------+-----------------+-------------------------+-----------------------------------+
| 1 (SmallM)| 0     | 3       | Rocco         | 0x2b43 (11075)  | JRole_1_0               | 21001 (Shirt), 24001 (Shoes)      |
| 2 (SmallF)| 0     | 1       | Nina          | 0x3ac1 (15041)  | JRole_2_0               | 22003, 21002, 24002               |
| 2 (SmallF)| 1     | 2       | Betty         | 0x2f04 (12036)  | JRole_2_1               | 22001, 21003, 24003               |
| 3 (BigM)  | 0     | 6       | Daniel        | 0x3abe (15038)  | JRole_3_0               | 21004 (Coat), 24004 (Shoes)       |
| 3 (BigM)  | 1     | 7       | Sid           | 0x2b44 (11076)  | JRole_3_1               | 21005 (Jacket), 24005 (Shoes)     |
| 3 (BigM)  | 2     | 12      | More          | 0x3abf (15039)  | JRole_3_2               | 21012 (Leather), 24012 (Straw)    |
| 3 (BigM)  | 3     | 14      | Kurogane      | 0x2bae (11182)  | JRole_3_3               | 22009, 21014, 18002, 24014        |
| 4 (BigF)  | 0     | 10      | Iris          | 0x2b46 (11078)  | JRole_4_0               | 22005, 21006, 23001, 24006        |
| 4 (BigF)  | 1     | 5       | Lique         | 0x2f15 (12053)  | JRole_4_1               | 21007, 23002, 24007               |
| 4 (BigF)  | 2     | 4       | Vanessa       | 0x3ac0 (15040)  | JRole_4_2               | 21008, 24008                      |
| 4 (BigF)  | 3     | 13      | Breillat      | 0x3ad4 (15060)  | BRole_4_3               | 22007, 21009, 10002, 24009        |
| 4 (BigF)  | 4     | 8       | Jessica       | 0x2f13 (12051)  | JRole_4_4               | 22002, 21010, 10003, 24010        |
| 4 (BigF)  | 5     | 9       | Konnotsuroko  | 0x2f11 (12049)  | JRole_4_5               | 24013, 21013                      |
| 4 (BigF)  | 6     | 11      | Maria         | 0x2b45 (11077)  | JRole_4_6               | 22006, 21011, 10004, 24011        |
| 4 (BigF)  | 7     | 15      | Karin         | 0x2baf (11183)  | JRole_4_7               | 21015, 22008, 24015               |
+-----------+-------+---------+---------------+-----------------+-------------------------+-----------------------------------+
```

---

## 3. Network Protocol Structures

### 3.1 Character Creation (`AC 9, Sub 1`) Wire Protocol
Reconstructed from `FUN_002d6ad8` (`case '\t':` line `0x2d7e6f`):
* `Opcode`: `0x09` (byte)
* `Subcode`: `0x01` (byte)
* `Body`: `ushort` (little-endian: 1 byte body style, 1 byte padding `0x00`)
* `Head`: `ushort` (little-endian: 1 byte head style, 1 byte padding `0x00`)
* `ColorCode1`: `uint32` (little-endian: `HairColor:ushort` | `SkinColor:ushort`)
* `ColorCode2`: `uint32` (little-endian: `ClothingColor:ushort` | `EyeColor:ushort`)
* `Element`: `byte` (Affinity: 1=Water, 2=Earth, 3=Fire, 4=Wind)
* `Stats Allocation`:
  - `Str`: `byte` (offset `0x1e8`)
  - `Con`: `byte` (offset `0x1ec`)
  - `Int`: `byte` (offset `0x1eb`)
  - `Wis`: `byte` (offset `0x1ea`)
  - `Agi`: `byte` (offset `0x1e9`)
* `Cipher`: optional Pascal-style string (`length:byte` + `ascii:bytes`)

### 3.2 Server Login Broadcast (`AC 3`)
Reconstructed from `ToAC3Packet()`:
* `[0x03:b, CharID:d, Body:b, MapID:w, X:w, Y:w, 0x00:b, Head:b, 0x00:b, HairColor:w, SkinColor:w, ClothingColor:w, EyeColor:w, WornCount:b, Equips:w*, 0x00:d, CharName:s, NickName:s, 0x00:d]`

### 3.3 Map Arrival / Peer Spawn (`AC 4`)
Reconstructed from `ToAC4Packet()`:
* `[0x04:b, CharID:d, Body:b, Element:b, Level:b, MapID:w, X:w, Y:w, 0x00:b, Head:b, 0x00:b, HairColor:w, SkinColor:w, ClothingColor:w, EyeColor:w, WornCount:b, Equips:w*, 0x00:d, 0x00:b, Reborn:b, Job:b, CharName:s, NickName:s, 0xFF:b, GuildID:d, 0x01:b]`

---

## 4. Root Cause Analysis: Daniel vs More Mismatch

### 4.1 Symptom
The user reported that their character's appearance became corrupted:
* Top-left HUD circle portrait correctly showed **Daniel** (`RoleID 6`, orange spiky hair, headband, fire element).
* Walking doll on map and paperdoll in Equipment Window displayed **More** (white topknot hair, bare chest, loincloth).

### 4.2 Root Causes Identified
1. **Database `head` Value:** In `characters` table for `charID = 10001`, `head` was persisted as `1`. For Big Male (`Body 3`), `head 1` maps to **Sid**, whereas Daniel requires `head 0`.
2. **Beginner Outfit Locking:** In `Equip.cs`, `SetBeginnerOutfit()` equipped Sid's jacket (`21005`) and leisure shoes (`24005`) because `head == 1`. In WLO, beginner clothes are hard-locked to their specific character class. When Daniel wears Sid's clothes, the client cannot resolve the clothing sprite layer and falls back to rendering the character naked (loincloth).
3. **Hair & Clothing Color Desync:** In `characters` table, `haircolor` was saved as `44828` (`0xAF1C` = white/light-gray) and `clothingcolor = 44828`. White hair on a naked Big Male body visually mirrors **More** (`RoleID 12`), giving the user the impression that their character morphed into More.
4. **Stat Deserialization Bug in `AC09.cs`:** `AC09.Recv1` was unpacking stats out of wire order (`STR, AGI, WIS, INT, CON` instead of `STR, CON, INT, WIS, AGI`).

### 4.3 Applied Resolution
1. Corrected `Src/Network/ActionCodes/AC09.cs` to unpack stats in exact client wire order: `Str -> Con -> Int -> Wis -> Agi`.
2. Updated `characters` table for `charID = 10001`:
   - `head = 0` (Daniel)
   - `haircolor = 42168` (authentic Daniel orange hair)
   - `clothingcolor = 35964` (authentic Daniel coat color)
3. Updated `inventory` table for `charID = 10001, storID = 1`:
   - `pos 2` (body): replaced `21005` with `21004` (Daniel's Dust Coat)
   - `pos 5` (shoes): replaced `24005` with `24004` (Daniel's Gentleman's Shoes)
