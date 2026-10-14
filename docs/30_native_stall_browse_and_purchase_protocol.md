# 30. Native Stall Browse and Purchase Protocol Specification

## 1. System Overview

This specification details the reverse-engineered network protocol and client interaction flow for browsing and purchasing from player-owned and custom synthetic merchant stalls in Wonderland Online.

Based on decompilation and disassembly of `aLogin.exe`:
- Actor click handler: offset `0x3d03d6` (hit-testing `*(actor + 0x2231) != 0` and sending C -> S `AC 23:32`)
- Sub-AC dispatch table: offset `0x2F9F83` (indexed by Sub-AC after Action Code 23)
- Sub-AC 32 browse packet transmitter: offset `0x3d03d6`
- Sub-AC 33 stall browse receiver: offset `0x2FA7E3` -> `0x33fa00` (`TRe_BuyItemStallForm.ProcessPacket`)
- `Form_BuyItemStall_1` Purchase Button handler: offset `0x33F3B8` -> `0x33F672` (sending C -> S `AC 23:33`)

---

## 2. Reverse-Engineered Wire Protocols

### 2.1 Native Stall Browse Request (`AC 23:32`, C -> S)

- **Client Trigger:** When a player left-clicks on an actor marked as a stall (`*(actor + 0x2231) != 0`), the client invokes `SendStallBrowsePacket` at `0x3d03d6`.
- **Purpose:** Informs the server that the player wishes to view the catalog of the target vendor.
- **Wire Format:**

| Field | Type | Size | Description |
|---|---|---|---|
| Action Code | uint8 | 1 byte | Constant `23` (`0x17`) |
| Sub-Action Code | uint8 | 1 byte | Constant `32` (`0x20`) |
| Vendor CharID | uint32 (LE) | 4 bytes | Character / Actor ID of the stall owner |

---

### 2.2 Native Stall Browse Data & Form Opener (`AC 23:33`, S -> C)

- **Client Handler:** `0x33fa00` (`FUN_0033fa00`)
- **Purpose:** Instantiates and renders `Form_BuyItemStall_1` ($525 \times 409$ px dialog), populating its 3-column DataGrid with available items, stock levels, and prices.
- **Wire Format (18-Byte Header + 39 Bytes per Item):**

| Field | Type | Size | Description |
|---|---|---|---|
| Action Code | uint8 | 1 byte | Constant `23` (`0x17`) |
| Sub-Action Code | uint8 | 1 byte | Constant `33` (`0x21`) |
| Vendor CharID | uint32 (LE) | 4 bytes | Character / Actor ID of the stall owner |
| PointsCardDay | uint32 (LE) | 4 bytes | Constant `0` for regular item stall |
| PointsCardPoints | uint32 (LE) | 4 bytes | Constant `0` for regular item stall |
| PointsCardItem | uint32 (LE) | 4 bytes | Constant `0` (Non-zero branches to PointsCard mode) |

#### Item Record Structure (39 Bytes per Record, up to 5 Active Slots):

| Offset | Type | Size | Description |
|---|---|---|---|
| `+0x00` | uint8 | 1 byte | SlotIndex in stall ($1..5$ or $0..4$) |
| `+0x01` | uint16 (LE) | 2 bytes | ItemId (Database index in `itemDat.wpdat`) |
| `+0x03` | uint8 | 1 byte | CurrentQuantity available for purchase |
| `+0x04` | uint8 | 1 byte | MaxQuantity / slot capacity limit |
| `+0x05` | uint32 (LE) | 4 bytes | UnitPrice in Gold |
| `+0x09` | uint8 | 1 byte | Durability rating (Default: 100) |
| `+0x0A` | uint8 | 1 byte | MaxDurability rating (Default: 100) |
| `+0x0B` | uint16 (LE) | 2 bytes | ForgeLevel / Potential value |
| `+0x0D` | byte[8] | 8 bytes | Reserved / Double timestamp |
| `+0x15` | uint8 | 1 byte | Pet Amity bonus |
| `+0x16` | uint8 | 1 byte | Damage counter wear |
| `+0x17` | uint16 (LE) | 2 bytes | Inset Jewel ID |
| `+0x19` | uint8 | 1 byte | Skill Bonus ID |
| `+0x1A` | uint8 | 1 byte | Reserved metadata flag |
| `+0x1B` | uint32 (LE) | 4 bytes | Reserved attribute stat |
| `+0x1F` | uint16 (LE) | 2 bytes | Struct padding 1 |
| `+0x21` | uint32 (LE) | 4 bytes | Struct padding 2 |
| `+0x25` | uint16 (LE) | 2 bytes | Struct padding 3 |

---

### 2.3 Native Stall Item Purchase (`AC 23:33`, C -> S)

- **Client Trigger:** Dispatched when the player clicks `Btn_Buy_1` inside `Form_BuyItemStall_1` (routine at `0x33F3B8` -> `0x33F672`).
- **Wire Format:**

| Field | Type | Size | Description |
|---|---|---|---|
| Action Code | uint8 | 1 byte | Constant `23` (`0x17`) |
| Sub-Action Code | uint8 | 1 byte | Constant `33` (`0x21`) |
| Vendor CharID | uint32 (LE) | 4 bytes | Target Vendor ID |
| Item Array | Array | $8 \times N$ bytes | Selected merchandise records |

#### Purchase Item Record (8 Bytes each):

| Field | Type | Size | Description |
|---|---|---|---|
| SlotIndex | uint8 | 1 byte | Target slot index selected in `Form_BuyItemStall_1` |
| BuyQty | uint8 | 1 byte | Quantity requested for purchase |
| ItemId | uint16 (LE) | 2 bytes | Item ID expected |
| UnitPrice | uint32 (LE) | 4 bytes | Unit price in gold |

---

## 3. Server-Side Method Specifications

### `PlayerStallBazaarEngine.BuildStallBrowse33Packet(PlayerStall stall)`

- **Namespace:** `Game.Experimental`
- **Parameters:** `PlayerStall stall` (Must not be null).
- **Return Type:** `Network.SendPacket`
- **Exceptions:** Returns `null` if `stall` is null.
- **Payload Schema:** Header 18 bytes + $N \times 39$ bytes per item.

### `PlayerStallBazaarEngine.PurchaseItem(Player buyer, uint vendorCharId, byte slotIndex, int quantity)`

- **Namespace:** `Game.Experimental`
- **Parameters:**
  - `Player buyer`: The character attempting the purchase.
  - `uint vendorCharId`: Unique vendor identifier.
  - `byte slotIndex`: Stall item slot index ($0..4$ or $1..5$).
  - `int quantity`: Units to purchase (default 1).
- **Return Type:** `bool` (True if transaction succeeds, false otherwise).
- **Edge Cases Handled:**
  - Robust slot fallback: Resolves 0-based and 1-based indexing (`stall.Items[slotIndex]`, `stall.Items.FirstOrDefault(i => i.SlotIndex == slotIndex)`, or `stall.Items[slotIndex - 1]`).
  - Pre-transaction inventory capacity validation: Prevents taking gold if bag is full.
  - Infinite stock protection: Infinite stalls do not deplete catalog stock.
  - Live vendor gold crediting and offline database persistence.
  - Automatic `Form_BuyItemStall_1` refresh via S -> C `AC 23:33` upon purchase.

---

## 4. Action Code 23 Handler Alignment

- **File:** `Src/Network/ActionCodes/AC23.cs`
- **`case 32:`** Invokes `Recv32(Player p, RecievePacket r)`:
  - Unpacks `VendorCharId: 4B`.
  - Dispatches `BuildStallBrowse33Packet(stall)` to buyer, launching `Form_BuyItemStall_1`.
  - Sends chat catalog and redundant `AC 20:15` / `AC 56:3` frames.
- **`case 33:`** Invokes `Recv33(Player p, RecievePacket r)`:
  - Unpacks `VendorCharId: 4B`.
  - Loops over 8-byte item chunks, calling `PurchaseItem`.
  - Refreshes open `Form_BuyItemStall_1` with updated stock.
