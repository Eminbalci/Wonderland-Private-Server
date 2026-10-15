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
- **Wire Format (19-Byte Header + 39 Bytes per Item):**

| Field | Type | Size | Description |
|---|---|---|---|
| Action Code | uint8 | 1 byte | Constant `23` (`0x17`) |
| Sub-Action Code | uint8 | 1 byte | Constant `33` (`0x21`) |
| Vendor CharID | uint32 (LE) | 4 bytes | Character / Actor ID of the stall owner |
| PointsCardDay | uint32 (LE) | 4 bytes | Constant `0` for regular item stall |
| PointsCardPoints | uint32 (LE) | 4 bytes | Constant `0` for regular item stall |
| PointsCardItem | uint32 (LE) | 4 bytes | Constant `0` (Non-zero branches to PointsCard mode) |
| CardType | uint8 | 1 byte | Constant `0` (Client offset `0x11` / byte 17 of payload read prior to loop) |

> [!IMPORTANT]
> **Client Parsing Off-by-One Root Cause:** In `aLogin.exe` (`FUN_0033fa00`), the client reads `CardType` at byte offset `0x11` (`param_2 + 17`) before beginning the 39-byte item record loop starting at offset `0x12` (`param_2 + 18`, 1-based index 19). If `CardType` is omitted, byte 17 consumes `SlotIndex`, shifting all subsequent item fields by 1 byte.
> Consequently, `ItemId` unmarshals into an invalid ID. At `0x00341EF0`, the client evaluates `ItemDatManager.GetItemByID` (`0x003E77BC`). If the item pointer is `NULL` (invalid or nonexistent item ID), it executes `break;`, aborting the loop and leaving `Form_BuyItemStall_1` completely blank. All stall items MUST also exist in `itemDat.wpdat`.

#### Item Record Structure (39 Bytes per Record, up to 5 Active Slots):

| Offset | Type | Size | Description |
|---|---|---|---|
| `+0x00` | uint8 | 1 byte | SlotIndex in stall ($1..5$ or $0..4$) |
| `+0x01` | uint16 (LE) | 2 bytes | ItemId (Database index in `itemDat.wpdat`) |
| `+0x03` | uint8 | 1 byte | CurrentQuantity available for purchase (clamped to 50 max for client visual display) |
| `+0x04` | uint8 | 1 byte | MaxQuantity / slot capacity limit (clamped to 50 max for client visual display) |
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
- **Payload Schema:** Header 19 bytes + $N \times 39$ bytes per item.

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
  - Infinite stock auto-reset: Whenever an item is purchased from an infinite stall (`stall.IsInfiniteStock` or item-level infinite flag), its stock quantity is immediately restored to its maximum configured limit (`item.MaxQuantity > 0 ? item.MaxQuantity : 50`) rather than depleting, guaranteeing persistent stock availability.
  - Live vendor gold crediting and offline database persistence.
  - Automatic `Form_BuyItemStall_1` refresh via S -> C `AC 23:33` upon purchase with restored stock.

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

---

## 5. Visual Bazaar Stall Studio & In-Game Interfaces

### 5.1 Server GUI Bazaar Stall Studio (`MainForm1.ExperimentalTab.cs`)

A full-featured visual administration workspace replaces manual string entry:
- **Left Panel (Stall Studio & Catalog Builder):**
  - **Map Presets & Coordinates:** Dropdown with named towns (`12000 - Kelan Village`, `11003 - Holy Village`, `11016 - Wrecked Shore`, `10001 - Astrologer Cave`, etc.) and a one-click **"Use Player Pos"** button to automatically grab the online player's current map and (X, Y) coordinates.
  - **Dynamic Item Search & Autocomplete:** Search-as-you-type filter across all 4,818 items from `itemDat.wpdat` decoding Big5 names, with price and stack quantity editors.
  - **Interactive Draft Catalog DataGrid:** Up-to-5 slot merchandise designer with live slot indexing, item names, prices, and infinite stock modes.
  - **Spawn & Persist:** Generates synthetic vendor actor, attaches 3D stall table and floating signboard, and persists record to SQLite & MySQL (`custom_stalls`).
- **Right Panel (Active Overworld Stalls & Database Management):**
  - **Active Stalls Grid:** Displays all active in-memory and database-saved stalls.
  - **Load into Editor:** Reverse-populates any selected stall into the left-panel studio for instant editing, price rebalancing, or cloning.
  - **Teleport to Stall:** Instantly warps selected online player directly in front of the stall.
  - **Delete & Clear Controls:** Removes stall from memory, dismisses actor/table/signboard from all map peers, and purges database rows.

### 5.2 In-Game Structured Chat & Dual Purchase Channels

- **Structured Chat Banner:** Upon interaction (left-click or NPC talk), displays a clean merchant banner with stall title, vendor name, stock mode, map location, and formatted catalog slots with prices and infinite badges `[∞ Stock]`.
- **Dual Purchase Channels:** Players can purchase goods either via the native visual `Form_BuyItemStall_1` DataGrid or via instant chat command `:buystall <slot 0..4> [qty]`.

---

## 6. Client Stall Speech Bubble & AC 20:18 Reverse-Engineering

### 6.1 Root Cause of "I have no questions. Visit me in 2 hours"
- **Disassembly Location:** `aLogin.exe` at address `0x2F8161` (Action Code 20 jump table `0x2F81A3`).
- **Subcode 18 (`0x12`) Handler at `0x2F86BE`:**
  ```assembly
  0x2F86BE  push 0
  0x2F86C0  push 0
  0x2F86C2  mov eax, [0x4C8D40]
  0x2F86C7  mov eax, [eax]
  0x2F86C9  mov ecx, 2000          ; Duration ms
  0x2F86CE  mov edx, 0x304B5C      ; "I have no questions. Visit me in 2 hours"
  0x2F86D3  mov ebx, [eax]
  0x2F86D5  call [ebx + 0x9C]      ; Display head banner / bubble
  0x2F86DB  jmp 0x303CE1
  ```
- **Finding:** Sub-code `18` of Action Code `20` was previously labeled as "Purchase Confirmation" in legacy drafts. In reality, `AC 20:18` is the client-side Scholar / Quiz cooldown packet. Sending it upon purchase caused `aLogin.exe` to render the speech bubble `"I have no questions. Visit me in 2 hours"` right above `Form_BuyItemStall_1`.
- **Speech Bubble Header (`0x3428AB`):** The client autonomously formats the second line of the stall bubble as `"[" + VendorName + "]'s Shop"` based on actor name memory at `[ebx + 0x220]`.
- **Resolution:**
  1. Removed `AC 20:18` packet dispatch from `PlayerStallBazaarEngine.PurchaseItem`.
  2. Removed `AC 20:1` dialogue invocation from `OnStallInteracted`.
  3. Replaced with `buyer.SendHeadBanner($"Purchased {quantity}x {item.ItemName} ({totalCost:N0}G)");` (native `AC 2:16`) and `buyer.Send(Tools.FromFormat("bb", 20, 8))` to release movement cleanly.

