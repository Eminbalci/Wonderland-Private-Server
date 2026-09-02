# Technical Specification: Skill.Dat Binary Parsing & Map Actor Spawn Protocol

## 1. Skill.Dat Binary Architecture
- **Record Size**: 148 bytes per skill entry (953 total skills across 141,044 bytes).
- **Encoding & Cipher**:
  - `SkillName` (Offset 1..20, 20 bytes): Raw ASCII byte sequence reversed in place (`SkillName[19 - i] <-> SkillName[i]`). No XOR mask.
  - `Description` (Offset 68..97, 30 bytes): Raw ASCII byte sequence reversed in place. No XOR mask.
  - `Type` (Offset 21, 1 byte): Decoded via `(byte)((Type ^ 0xFD) - 4)`.
  - `SkillID` (Offset 22..23, 2 bytes): Decoded via `(ushort)((SkillID ^ 0x6EA0) - 4)`.
  - `SP` (Offset 24..25, 2 bytes): Decoded via `(ushort)((SP ^ 0x6EA0) - 4)`.
  - `ElementType` (Offset 26, 1 byte): Decoded via `(byte)((ElementType ^ 0xFD) - 4)`. (1=Earth, 2=Water, 3=Fire, 4=Wind, 7=Undefined).
  - `Attack` (Offset 27..28, 2 bytes): Decoded via `(ushort)((Attack ^ 0x6EA0) - 4)`.

## 2. Character Skill Unlocking & Stat Serialization
- **AC 8:1 (Stat 367 / 0x016F)**:
  - **Packet Structure**: `Tools.FromFormat("bbwdd", 8, 1, 0x016F, (uint)SkillID, (uint)Grade)`
  - **Parameters**:
    - `Param 1` (DWord): `SkillID` (e.g., 11016 for Flame Attack, 11076 for Combo x3 Attack).
    - `Param 2` (DWord): `Grade` (e.g., 1 for starter skills).
- **AC 5:16 (Intro Tree Node Unlock)**:
  - **Packet Structure**: `[5, 16, 0, (ushort)SkillID, (byte)Grade]`

## 3. Map Spawn Lifecycle & Actor Synchronization
- **Lifecycle Sequence**:
  1. `Map.Warp_In`:
     - Dispatches `AC 3` (`ToAC3Packet`) directly to entering player.
     - Dispatches `AC 12` (Map ID & Warp coordinates).
     - Dispatches `AC 7` (Position synchronization).
     - Dispatches `AC 5:0` (Equipment visuals).
     - Dispatches `AC 5:8` (Sprite refresh).
  2. `AC 5:7` (Client Scene Ready Request):
     - Handler `AC05.Recv7` verifies player presence and immediately re-emits `AC 3`, `AC 5:0`, and `AC 5:8`.
