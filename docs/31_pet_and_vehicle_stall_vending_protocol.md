# Technical Specification: Companion Pet and Vehicle/Utensil Stall Vending Protocol

## 1. Architectural Overview

In Wonderland Online (WLO), vending stalls exist as three architecturally separate subtypes differentiated by their wire protocol, client forms, memory offsets, and purchase execution semantics:

1. **Item Stall (`StallType = 1`)**: General merchandise (consumables, equipment, materials). Handled by `Form_BuyItemStall_1` using `AC 23:33` browse and `AC 23:33` purchase.
2. **Pet Stall (`StallType = 2`)**: Companion pet roster. Handled by `Form_BuyPet1Stall_1`, `Form_BuyPet2Stall_1`, or `Form_BuyPet3Stall_1` unpacked by `FUN_0032def0` via `AC 23:113` (`0x71`) browse and `AC 23:63` (`0x3F`) purchase.
3. **Vehicle & Household Utensil Stall (`StallType = 3`)**: Transports and manufacturing appliances. Handled by `Form_Buy1Utensil_1` or `Form_Buy3Utensil_1` unpacked by `FUN_003325bc` via `AC 23:133` (`0x85`) browse and `AC 23:68` (`0x44`) purchase.

---

## 2. Reverse-Engineered Client Binary Mechanics (`aLogin.exe`)

### 2.1 Overworld Stall Discriminator (`AC 23:31`)
When an actor opens a vending stall or when map actors are synchronized (`BuildStallMapStatePacket`), the server sends:
```
[Header: AC 23, Sub 31]
[VendorCharId: 4 Bytes LE]
[StallTitleLength: 1 Byte]
[StallTitleBytes: N Bytes ASCII]
[StallType: 1 Byte]
```
The client inspects the trailing byte:
- `1`: Renders the traditional item merchant stall kiosk and maps click interaction to `Form_BuyItemStall_1`.
- `2`: Renders the companion pet trading kiosk and maps click interaction to `Form_BuyPet1..3Stall_1`.
- `3`: Renders the vehicle/furniture platform and maps click interaction to `Form_Buy1..3Utensil_1`.

---

### 2.2 Companion Pet Stall Browse Protocol (`AC 23:113` / `0x71`)
Unpacked by `FUN_0032def0` in `aLogin.exe`:
- **Header**:
  - `ActionCode`: 1 byte (23)
  - `SubCode`: 1 byte (113)
  - `VendorCharId`: 4 bytes (uint LE)
- **Pet Record Array**: $N \times 225$ bytes (0xE1 bytes per pet slot):
  - `+0x00` (1B): Slot Index (0..14)
  - `+0x01` (2B): Pet NPC Template ID (ushort LE)
  - `+0x03` (4B): Price Gold (uint LE)
  - `+0x07` (1B): Pet Level
  - `+0x08` (4B): Max/Current HP (uint LE)
  - `+0x0C` (4B): Max/Current SP (uint LE)
  - `+0x10` (2B): Element / Reborn Flag (ushort LE)
  - `+0x12` (126B): 6 Worn Equipment Slots ($6 \times 21$ bytes zeroed)
  - `+0x90` (81B): Stats and Name Block:
    - Offset +0..15 (16B): Pet Name string (ASCII null-padded)
    - Offset +16 (1B): Strength (Str)
    - Offset +17 (1B): Constitution (Con)
    - Offset +18 (1B): Intelligence (Int)
    - Offset +19 (1B): Wisdom (Wis)
    - Offset +20 (1B): Agility (Agi)
    - Offset +21 (1B): Amity / Loyalty (default 60)
    - Offset +22..80 (59B): Reserved padding

Depending on pet count ($N \le 5$, $N \le 10$, $N \le 15$), the client dynamically instantiates `Form_BuyPet1Stall_1`, `Form_BuyPet2Stall_1`, or `Form_BuyPet3Stall_1`.

---

### 2.3 Companion Pet Purchase Protocol (`AC 23:63` / `0x3F`)
Triggered when the buyer clicks "Purchase" in `Form_BuyPet1..3Stall_1`:
```
C->S: [AC: 23, Sub: 63] [VendorCharId: 4B] { [SelectedSlot: 1B] }
```
**Server Validation & Joining Workflow**:
1. Retrieve `PlayerStall` by `VendorCharId`.
2. Locate `StallItem` by `slotIndex`.
3. Verify buyer gold balance (`buyer.Gold >= price`).
4. Validate buyer pet party roster:
   - Ensure buyer has an active `PlayerPets` roster.
   - Enforce 4-pet party maximum (`roster.Values.Count >= 4` -> rejection).
   - Enforce companion duplicate prevention (`Player.IsSamePetOrCompanion` -> rejection if already joined).
5. Deduct gold balance from buyer and credit vendor if online.
6. Deduct stock or auto-replenish if `IsInfinite` is enabled.
7. Construct new `PlayerPetData` with base stats, level 1, and amity 60.
8. Register pet in `buyer.PlayerPets` and client via `SendPetProgression`, `CreatePetPacket`, `CreatePetNamePacket`.
9. Send audio ACK `Tools.FromFormat("bbbb", 23, 15, 0, 0)` and Gold HUD refresh (`AC 26:4` and `AC 53:2`).
10. Send updated pet stall catalog `AC 23:113` to buyer.

---

### 2.4 Vehicle & Utensil Browse Protocol (`AC 23:133` / `0x85`)
Unpacked by `FUN_003325bc` in `aLogin.exe`:
- **Header**:
  - `ActionCode`: 1 byte (23)
  - `SubCode`: 1 byte (133)
  - `VendorCharId`: 4 bytes (uint LE)
  - `Padding`: 11 bytes (ensures 17-byte header offset before record parsing)
- **Utensil Record Array**: $N \times 15$ bytes (0x0F bytes per entry):
  - `+0x00` (1B): Slot Index (1-indexed: 1..3)
  - `+0x01` (2B): Item ID (ushort LE, e.g. 48010 Raft, 48009 Sailboat, 26002 Alchemy Pot)
  - `+0x03` (1B): Current Durability (e.g. 100)
  - `+0x04` (1B): Max Durability (e.g. 100)
  - `+0x05` (4B): Price Gold (uint LE)
  - `+0x09` (4B): Potential / Attribute data (uint LE)
  - `+0x0D` (2B): Record padding

The client presents `Form_Buy1Utensil_1` for 1 item or `Form_Buy3Utensil_1` for 2-3 items.

---

### 2.5 Vehicle & Utensil Purchase Protocol (`AC 23:68` / `0x44`)
Triggered when the buyer clicks "Purchase" in `Form_Buy1..3Utensil_1`:
```
C->S: [AC: 23, Sub: 68] [VendorCharId: 4B] { [SlotIndex: 1B] [Quantity: 1B] }
```
**Server Validation & Delivery Workflow**:
1. Locate stall and target vehicle item by slot index.
2. Verify buyer inventory capacity.
3. Validate gold (`totalCost = price * qty`).
4. Deduct gold and grant item directly to buyer bag (`buyer.Inv.AddItem(itemId, qty)`).
5. Refresh Gold HUD (`AC 26:4`, `AC 53:2`).
6. Send refreshed `AC 23:133` browse packet to buyer viewport.

---

## 3. Database Persistence and Schema Migration

Both SQLite (`Data/ServerDataBase.db`) and MySQL schemas are updated non-destructively:

### SQL Schema Definition
```sql
CREATE TABLE IF NOT EXISTS custom_stalls (
    vendor_id INT PRIMARY KEY,
    vendor_name VARCHAR(64),
    stall_name VARCHAR(64),
    map_id INT,
    x INT,
    y INT,
    is_infinite INT DEFAULT 0,
    stall_type INT DEFAULT 1,
    items_json TEXT,
    created_at DATETIME
);
```

### Auto-Migration
During `EnsureDatabaseTable()`, the engine runs:
```sql
ALTER TABLE custom_stalls ADD COLUMN stall_type INT DEFAULT 1;
```
If the column already exists, the migration safely ignores duplicate column errors and proceeds.

---

## 4. Chat Commands & Shortcuts

| Command | Arguments | Description |
| :--- | :--- | :--- |
| `:stall` | `[title] [infinite\|limited] [id:price ...]` | Spawns a standard merchandise item stall. |
| `:stall pet` | `[title] [infinite\|limited] [npcId:price ...]` | Spawns a companion pet stall. |
| `:stall vehicle` | `[title] [infinite\|limited] [itemId:price ...]` | Spawns a vehicle / utensil appliance stall. |
| `:petstall` | `[title] [infinite\|limited] [npcId:price ...]` | Dedicated direct shortcut for pet stalls. |
| `:vehiclestall` | `[title] [infinite\|limited] [itemId:price ...]` | Dedicated direct shortcut for vehicle stalls. |
| `:buystall <slot> [qty]` | `<slot> [qty]` | Overworld text purchase command from closest stall. |
| `:removestall [vendorId]` | `[vendorId]` | Removes custom stall from map and database. |
| `:clearstalls` | None | Purges all custom stalls from memory and database. |

---

## 5. GUI Studio Integration (`MainForm1.ExperimentalTab.cs`)

The Bazaar Stall Studio subtab provides:
- **Stall Type Selector**: DropDown choosing between `Item Stall (Merchandise)`, `Pet Stall (Companion Roster)`, and `Vehicle & Utensil Stall`.
- **Automatic Type Presets**:
  - Item Preset: Rainbow Drop (30167), Safety Bell (30162), Magic Potion (30253), Magical Cure Potion (30323), Orchid Candy (27001).
  - Pet Preset: Roca (10001), Niss (10002), Fred (10003), Cliff (10004), Xaolan (10007).
  - Vehicle Preset: Raft (48010), Sailboat (48009), Alchemy Pot (26002).
- **Slot Capacity Gating**: Enforces 3 slots maximum for vehicles/appliances, 5 slots for merchandise and companion pets.
- **Active Stalls DataGridView**: Includes `colStallType` column displaying `Item`, `Pet`, or `Vehicle`.
- **Editor Synchronization**: "Load into Editor" automatically sets the appropriate stall type, titles, and catalog slots.
