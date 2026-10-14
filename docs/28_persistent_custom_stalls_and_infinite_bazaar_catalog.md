# Persistent Custom Stalls and Infinite Bazaar Catalog Specification

## 1. Executive Summary

This document specifies the persistent synthetic vendor architecture, customizable merchandise catalog, infinite vs. limited stock options, and dual-database persistence engine for the Wonderland Online Private Server.

Prior implementations tied stall entities directly to the executing player's character ID and animation state, forcing the calling player into a stationary sitting posture. This release introduces fully isolated synthetic vendor entities, dynamic merchandise catalog parsing, infinite/limited stock management, and automatic database persistence (SQLite and MySQL) loaded at server initialization.

---

## 2. Synthetic Vendor Architecture & Player Independence

### 2.1 Entity Isolation
- **Calling Player Independence**: Executing the `:stall` or `:spawnstall` chat command no longer modifies the calling player's posture (`player.Emote` remains `0`) and does not assign the player's character ID to the stall. The player remains free, mobile, and unencumbered.
- **Reserved Synthetic ID Range**: Synthetic vendor characters are allocated from a reserved non-colliding block (`8900001` to `8999999`) generated via `PlayerStallBazaarEngine.GenerateSyntheticVendorId()`. This block avoids collision with connected players (`1` to `999999`) and standard NPCs (`1` to `65535`).

### 2.2 Dual-Layer Map Entity Model
Each spawned stall registers two coordinated representations on the target map:
1. **Interactive Quest NPC (`StallVendorNpc`)**:
   - Attached to `targetMap.NPCs` with dynamic ClickID allocation in the range `200..250`.
   - TemplateID: `14134` (Props Keep merchant model).
   - WalkBehavior: `1` (Stationary anchored).
   - Handles mouse-click interaction, opening AC 20:15 / AC 56:3 shop menus and interactive dialog menus.
2. **Visual Actor & Kiosk Stance**:
   - Broadcasted to peers via AC 4 (Spawn), AC 5:0 (Equipment), AC 10:3 255 (Online status), and AC 7 (Coordinates).
   - Visual posture: AC 32:2 `1` (Sitting pose) and AC 5:16 `1` (Kiosk counter table).
   - Signboard banner: AC 56:30 containing the stall title.

---

## 3. Customizable Merchandise & Stock Modes

### 3.1 Command Syntax
Players and Game Masters can spawn custom stalls anywhere with flexible parameters:

```text
:stall [title] [infinite|limited] [itemId:price[:qty] ...]
:stall <mapId> <x> <y> [title] [infinite|limited] [itemId:price[:qty] ...]
```

- **Stock Mode Tokens**:
  - `infinite`, `inf`, `sonsuz`, `unlimited`, `unlim`: Activates infinite stock mode.
  - `limited`, `lim`, `limitli`: Activates limited stock mode (default).
- **Item Specification Format**:
  - `<itemId>:<unitPrice>`: Default quantity is 50 for limited stock, or 99 for infinite stock.
  - `<itemId>:<unitPrice>:<quantity>`: Explicit initial stock quantity.
  - Example: `:stall Rare Potions infinite 10001:500:99 10002:150:10` spawns a vendor named "Rare Potions Vendor" selling item 10001 for 500 gold (infinite) and item 10002 for 150 gold (infinite).
- **Default Catalog Fallback**:
  - If no items are specified, the stall is initialized with a standard merchant catalog (Healing Draughts, Vitality Elixirs, Star Iron, Warp Stones, and Treasure Caskets).

### 3.2 Purchase Semantics
- **Infinite Stock (`IsInfinite = true`)**:
  - In `PlayerStallBazaarEngine.PurchaseItem`, quantity deduction is bypassed.
  - Item quantities never decrease, and slots never become depleted.
  - Displayed in catalog views and chat previews with the symbol `∞`.
  - The buyer's gold is deducted, and items are credited to the buyer's inventory.
- **Limited Stock (`IsInfinite = false`)**:
  - In `PlayerStallBazaarEngine.PurchaseItem`, the purchased quantity is subtracted from `item.Quantity`.
  - If `item.Quantity <= 0`, the item is permanently removed from `stall.Items`.
  - The updated inventory is automatically synchronized to the database.

---

## 4. Dual-Database Persistence (SQLite & MySQL)

### 4.1 Schema Definition
Custom stalls are stored in the `custom_stalls` table across both SQLite and MySQL:

```sql
CREATE TABLE IF NOT EXISTS custom_stalls (
    vendor_id INT NOT NULL PRIMARY KEY,
    vendor_name TEXT NOT NULL,
    stall_title TEXT NOT NULL,
    map_id INT NOT NULL,
    x INT NOT NULL,
    y INT NOT NULL,
    is_infinite INT DEFAULT 0,
    items_data TEXT NOT NULL,
    created_at TEXT
);
```

### 4.2 Serialization Format
Merchandise items are serialized into the `items_data` column using a delimited pipe-separated format:
`slotIndex:itemId:itemName:quantity:priceGold:invSlot:isInfinite|...`

### 4.3 Server Bootstrapping & Lifecycle
1. **Server Boot Initialization**:
   - `WorldServer.Initialize()` calls `PlayerStallBazaar.InitializeDatabase()`.
   - `EnsureDatabaseTable()` checks table existence and initializes schema if absent.
   - `LoadStallsFromDatabase()` queries all records and restores stalls onto their respective maps with full visual packets and NPC instances (`persistToDb: false` prevents redundant writes).
2. **On-Demand Mutations**:
   - `SaveStallToDatabase(stall)`: Executes `INSERT OR REPLACE INTO custom_stalls` with parameterized `DbParam` values.
   - `DeleteStallFromDatabase(vendorCharId)`: Executes `DELETE FROM custom_stalls WHERE vendor_id = @vendor_id`.
   - `ClearAllStallsFromDatabase()`: Truncates `custom_stalls`.

---

## 5. Management & Removal Controls

### 5.1 Chat Commands
- `:stall ...`: Spawns synthetic persistent stall.
- `:removestall <vendorId>`: Removes the stall matching `<vendorId>` from memory, clears all visual/NPC packets from players on the map, and deletes the record from the database.
- `:removestall`: If no vendor ID is specified, calculates distances to all stalls on the player's current map. If a stall is within 300 units, it is despawned and deleted; otherwise, lists all stalls on the map with their coordinates and vendor IDs.
- `:clearstalls`: Clears all active stalls from memory and purges the database table.

### 5.2 WinForms Experimental Tab GUI
- **Bazaar Sub-tab**:
  - Displays all active stalls in `dgvBazaarStalls` with columns: `Vendor ID`, `Vendor Name`, `Stall Name`, `Map ID`, `X`, `Y`, `Stock Mode`, and `Items`.
  - Checkbox: `chkInfinite` ("Infinite Stock").
  - Text input: `txtItems` ("Catalog Items (id:price[:qty])").
  - Button: `btnDeleteSelected` removes selected stall from map and database.
  - Button: `btnClearStalls` purges all stalls.
  - Button: `btnSpawnCustomStall` creates and saves stall to database.

---

## 6. Verification & Test Suite

The automated test script `.codex-verify/Test-StallPacket.ps1` executes six validation stages:

1. **Native Personal Stall Flow**:
   - Verifies AC 23:30 registration, sitting stance (`Emote = 1`), and AC 23:31 close opcode handling (`Emote = 0`).
2. **Synthetic Vendor Spawning**:
   - Executes `:stall Rare Alchemy infinite 10001:500:99 10002:150:5`.
   - Asserts caller `player.Emote == 0` (remains mobile and free).
   - Asserts synthetic vendor ID >= `8900001`.
   - Asserts infinite flag set to true and items parsed accurately.
3. **Infinite Stock Purchase**:
   - Simulates buyer purchase of 20 units.
   - Asserts buyer gold is deducted.
   - Asserts stall quantity remains unchanged at 99.
4. **Limited Stock Purchase**:
   - Spawns limited stall with 5 units.
   - Simulates purchase of 2 units (stock reduces to 3).
   - Simulates purchase of remaining 3 units (slot depleted and removed).
5. **Database Persistence**:
   - Verifies record insertion in `custom_stalls`.
   - Clears engine memory cache and executes `LoadStallsFromDatabase()`.
   - Verifies restored stall preserves map coordinates, title, and item definitions.
6. **Despawn & Cleanup**:
   - Executes `:removestall` and asserts record deletion from database and memory.
   - Executes `ClearAllStalls` and verifies clean teardown.
