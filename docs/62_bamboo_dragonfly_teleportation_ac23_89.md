# Bamboo Dragonfly Teleportation Protocol Specification (AC 23:89)

## 1. Overview
When a player uses a Bamboo Dragonfly (Item ID: 34169 or 34332), the client opens the Global Map (`GlobaMap`) UI form containing an array of 40 mainland and island destination buttons. Selecting a destination button and confirming dispatches client packet `AC 23:89` (`0x17, 0x59`).

Previously, this opcode was unhandled in `Src/Network/ActionCodes/AC23.cs`, emitting `AC 23,89 has not been coded`.

## 2. Client Packet Architecture (Reverse-Engineered from `aLogin.exe`)
- **Sender Routine**: `FUN_002d6ad8` at Virtual Address `0x2dfba4`
- **Item Trigger**: `0x3f8652` inspecting item IDs `0x8579` (34169) and `0x861c` (34332), opening form `0x221ce4`
- **Global Map UI**: `GlobaMap` constructor at `0x22193c`, loading button coordinates from `0x4bbf10` and display labels from `0x4bbe50`
- **Button Click Event**: `0x222028` recording `[ebx + 0x1a8] = DestinationIndex (1..40)`
- **Map Coordinate Table**: Static table at `0x4b5978` (16 bytes per entry, storing MapID at `+4`, X coordinate at `+8`, Y coordinate at `+12`)

### Packet Structure
- **Opcode**: `0x17` (23)
- **Subcode**: `0x59` (89)
- **Payload** (3 bytes):
  - Byte 0: `DestinationId` (`0x01` .. `0x28`, 1 to 40)
  - Byte 1: `InventorySlot` (`0x01` .. `0x32`, 1 to 50)
  - Byte 2: `ItemType` (`0x02` = Bamboo Dragonfly)

## 3. Destination Coordinate Matrix

| Index | Destination Name | Target Map ID | Coordinate X | Coordinate Y |
|:-----:|:-----------------|:-------------:|:------------:|:------------:|
| 1 | China | 12100 | 1800 | 1300 |
| 2 | Calcutta | 12300 | 2062 | 1735 |
| 3 | Oslya | 60006 | 5180 | 4675 |
| 4 | Mayan Forest | 60005 | 1002 | 1295 |
| 5 | Universal | 60007 | 1040 | 1335 |
| 6 | Revival | 60004 | 5750 | 1955 |
| 7 | Hawaii Isle | 60008 | 1750 | 3900 |
| 8 | S.Pole | 12355 | 690 | 1090 |
| 9 | Korea | 60013 | 3045 | 5235 |
| 10 | Iceberg | 12356 | 1580 | 1255 |
| 11 | Kyoto | 60011 | 5145 | 355 |
| 12 | Taiwan | 60012 | 2845 | 780 |
| 13 | S.Island | 60002 | 2862 | 4575 |
| 14 | N.Island | 60000 | 2900 | 3055 |
| 15 | Stoowa Isle | 60003 | 2142 | 5175 |
| 16 | Egypt | 12500 | 3122 | 1755 |
| 17 | Madagascar | 11127 | 3040 | 340 |
| 18 | Polo | 10069 | 1840 | 2075 |
| 19 | Hezrou | 10070 | 3460 | 1775 |
| 20 | SnowIsle | 12367 | 674 | 1352 |
| 21 | Inca | 60015 | 4040 | 355 |
| 22 | Ghostdom | 12286 | 3180 | 875 |
| 23 | Shayii | 12281 | 340 | 415 |
| 24 | Yazhou | 12105 | 2000 | 1555 |
| 25 | Bangkok | 12601 | 342 | 415 |
| 26 | LionIsle | 11145 | 3362 | 1795 |
| 27 | Centaur Isle | 11135 | 1900 | 1495 |
| 28 | Sky City | 11152 | 620 | 470 |
| 29 | Africa | 12700 | 2500 | 2110 |
| 30 | Yamatai | 12850 | 2240 | 1695 |
| 31 | Athens | 12800 | 1182 | 2575 |
| 32 | Small Hawaii | 60009 | 1442 | 1315 |
| 33 | Train Island | 13050 | 2632 | 1344 |
| 34 | Rome | 13200 | 2300 | 1330 |
| 35 | Persia | 13100 | 930 | 1870 |
| 36 | Filipino | 12952 | 1780 | 1295 |
| 37 | Lost Jungle | 13261 | 1900 | 235 |
| 38 | Fog Jungle | 13164 | 1765 | 390 |
| 39 | Chang'an | 12110 | 1842 | 1355 |
| 40 | Deep Jungle | 12905 | 2680 | 270 |

## 4. Server Execution Flow
1. **Combat Check**: Rejects teleportation during active PvE battle (`PvEBattleManager.IsInBattle`).
2. **Destination Validation**: Verifies destination ID is within `[1, 40]` and maps to a valid entry.
3. **Current Map Check**: Blocks teleportation if the player is already located in the target map.
4. **Item Verification & Consumption**: Validates that the slot holds a Bamboo Dragonfly item (or locates another slot containing one) and consumes 1 unit via `player.Inv.RemoveItem(slot, 1, true)`.
5. **Warp Execution**: Dispatches `player.CurMap.Teleport(TeleportType.Regular, player, 0, warp)` to transit the player sprite and load the new scene.
6. **Persistence**: Saves updated coordinates to database via `player.SaveCharacterData()`.
