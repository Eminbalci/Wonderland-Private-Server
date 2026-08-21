# Player Map Spawn & Visual Appearance Protocol (`AC 3` vs `AC 4`)

## 1. Problem Description & Root Cause
* **Symptom**: Character head/hair missing in UI/inventory preview and map (blank/white circle rendered where the head should be).
* **Root Cause**:
  - `Character.ToAC3Packet()` erroneously inserted `Element` (1 byte) and `Level` (1 byte) before `MapID`.
  - In the official WLO protocol, **`AC 3`** (Self Spawn) contains only `Body`, `MapID`, `CurX`, `CurY`, `0`, `Head`, `0`, `ColorCode1`, `ColorCode2`, `WornCount`, `Worn_Equips`, `00 00 00 00`, and `CharName`.
  - Conversely, **`AC 4`** (Other Player Map Spawn) contains `Body`, `Element`, `Level`, `MapID`, `CurX`, `CurY`, `0`, `Head`, `0`, `ColorCode1`, `ColorCode2`, `WornCount`, `Worn_Equips`, padding, `Reborn`, `Job`, `CharName`, `NickName`, `0xFF`.
  - The 2-byte insertion into `AC 3` caused the client to read `Head` and color parameters with a 2-byte offset, leading to a head index of `0` and a missing head sprite.

---

## 2. Correct `AC 3` (Self Map Entity Spawn) Packet Structure
```
[Header]      : F4 44 [Length_Lo] [Length_Hi]
Byte 0        : 0x03 (ActionCode 3: Map Entity Spawn)
Bytes 1..4    : CharID (uint32, 4B)
Byte 5        : Body ID (uint8, 1B - e.g. 1=Small Male, 2=Small Female, 3=Big Male, 4=Big Female)
Bytes 6..7    : MapID (uint16, 2B)
Bytes 8..9    : Current X coordinate (uint16, 2B)
Bytes 10..11  : Current Y coordinate (uint16, 2B)
Byte 12       : 0 (uint8, 1B padding)
Byte 13       : Head ID (uint8, 1B - 0..7)
Byte 14       : 0 (uint8, 1B padding)
Bytes 15..16  : HairColor (uint16, 2B)
Bytes 17..18  : SkinColor (uint16, 2B)
Bytes 19..20  : ClothingColor (uint16, 2B)
Bytes 21..22  : EyeColor (uint16, 2B)
Byte 23       : WornCount (uint8, 1B)
Bytes 24..    : Worn_Equips (WornCount * 2B uint16 item IDs)
Next 4 Bytes  : 0x00, 0x00, 0x00, 0x00 (uint32, 4B)
Dynamic       : CharName (length-prefixed ASCII string)
```

---

## 3. Correct `AC 4` (Other Player Map Entity Spawn) Packet Structure
```
[Header]      : F4 44 [Length_Lo] [Length_Hi]
Byte 0        : 0x04 (ActionCode 4: Other Player Map Entity Spawn)
Bytes 1..4    : CharID (uint32, 4B)
Byte 5        : Body ID (uint8, 1B)
Byte 6        : Element (uint8, 1B - 1=Earth, 2=Water, 3=Fire, 4=Wind)
Byte 7        : Level (uint8, 1B)
Bytes 8..9    : MapID (uint16, 2B)
Bytes 10..11  : Current X coordinate (uint16, 2B)
Bytes 12..13  : Current Y coordinate (uint16, 2B)
Byte 14       : 0 (uint8, 1B padding)
Byte 15       : Head ID (uint8, 1B)
Byte 16       : 0 (uint8, 1B padding)
Bytes 17..18  : HairColor (uint16, 2B)
Bytes 19..20  : SkinColor (uint16, 2B)
Bytes 21..22  : ClothingColor (uint16, 2B)
Bytes 23..24  : EyeColor (uint16, 2B)
Byte 25       : WornCount (uint8, 1B)
Bytes 26..    : Worn_Equips (WornCount * 2B uint16 item IDs)
Bytes 32..35  : 0x00, 0x00, 0x00, 0x00 (uint32, 4B)
Byte 36       : 0 (uint8, 1B)
Byte 37       : Reborn (bool, 1B)
Byte 38       : Job (uint8, 1B)
Dynamic       : CharName (length-prefixed ASCII string)
Dynamic       : NickName (length-prefixed ASCII string)
Trailer       : 0xFF (uint8, 1B)
```
