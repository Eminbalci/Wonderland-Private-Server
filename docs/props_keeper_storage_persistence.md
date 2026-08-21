# Character Props Keeper Storage & Database Persistence

## Overview
This document specifies the architecture, network protocol, and database persistence mechanisms for character-specific Props Keeper (Storage Bank / Vault) items across all game maps in `ServerDataBase.db`.

---

## 1. Storage Architecture

Each player character possesses a dedicated, 50-slot storage vault identified in the database by `storID = 2`:

| Component | Storage ID (`storID`) | Capacity | Description |
|---|---|---|---|
| **Inventory (Bag)** | `0` | 50 slots | Main player carrying bag |
| **Equipment (Equips)** | `1` | 6 slots | Currently equipped gear |
| **Props Keeper (Vault)** | `2` | 50 slots | Character-bound storage vault accessible at any Props Keeper NPC |

---

## 2. Database Schema & Persistence

Stored items are saved in the `inventory` table:
```sql
CREATE TABLE inventory (
    pri_key INTEGER PRIMARY KEY AUTOINCREMENT,
    invIdx INTEGER NOT NULL,
    charID INTEGER NOT NULL,
    storID INTEGER NOT NULL, -- 0: Bag, 1: Equip, 2: Props Keeper Storage
    itemID INTEGER NOT NULL,
    dmg INTEGER DEFAULT 0,
    qty INTEGER DEFAULT 1,
    pos INTEGER NOT NULL,
    socketID INTEGER DEFAULT 0,
    bombID INTEGER DEFAULT 0,
    sewID INTEGER DEFAULT 0,
    forge INTEGER DEFAULT 0
);
```

### 2.1 Loading (`CharacterDataBase.GetCharacterData`)
- Queries `SELECT * FROM inventory WHERE charID = '{charID}' AND storID = 2`.
- Populates `Player.Storage` (50 slots) upon character initialization.

### 2.2 Saving (`CharacterDataBase.WritePlayer`)
- Cleans and atomically batch-inserts current storage items:
  ```sql
  DELETE FROM inventory WHERE charID = '{charID}' AND storID = '2';
  INSERT INTO inventory (invIdx, charID, storID, itemID, dmg, qty, pos, socketID, bombID, sewID, forge) VALUES ...;
  ```

---

## 3. Network Protocol (`AC 30`, `AC 29` & `AC 23`)

### 3.1 Opening Props Keeper (`Player.OpenPropsKeeper`)
- Client GUI window `bank` is opened via `AC 29:6` and `AC 30:6`.
- **Right Pane ("items held") Sync**: Dispatches `p.Inv.GetAC23_5()` (ActionCode 23:5) with the full 29-byte inventory slot layout so the player's held bag items render immediately.
- **Left Pane ("store items") Sync**: Dispatches `p.Storage.GetAC30_5()` (ActionCodes 30:5, 30:1, 29:1, 29:5) and per-slot `AC 30:2` / `AC 29:1` packets with authentic 29-byte item data (`slot`, `itemID`, `ammt`, `damage`, and 24 attribute bytes).

### 3.2 Deposit Item (`AC 30 Sub 2` / `AC 29 Sub 1`)
- **Client Request**: `F4 44 03 00 1E 02 [bagSlot]` (Len=7)
  - `bagSlot` (1 byte, 1..50): The inventory bag slot to store.
- **Server Execution**:
  1. Finds the first available slot in `Player.Storage` (`targetSlot`).
  2. Copies item data and quantity (`Math.Min(bagItem.Ammt, requestedAmmt)`).
  3. Removes item from `Player.Inv` (`p.Inv.RemoveItem(bagSlot, ammt, true)`).
  4. Dispatches real-time UI synchronization:
     - `p.Inv.GetAC23_5()` to refresh the "items held" right pane.
     - `AC 30:2` (29-byte + short) and `AC 29:1` to update the "store items" left pane.
     - Full storage sync `p.Storage.GetAC30_5()`.
  5. Automatically persists state via `CharacterDataBase.WritePlayer(p.CharID, p)`.
  6. Sends confirmation system message: `📥 Stored '{ItemName}' x{qty} into Storage Slot {slot}.`

### 3.3 Withdraw Item (`AC 30 Sub 3` / `AC 29 Sub 2`)
- **Client Request**: `[30, 3, storSlot]` or `[29, 2, storSlot]`
  - `storSlot` (1 byte, 1..50): The vault slot to withdraw from.
- **Server Execution**:
  1. Transfers item from `Player.Storage[storSlot]` to `Player.Inv` via `p.Inv.AddItem`.
  2. Clears storage slot via `p.Storage.RemoveItem(storSlot, ammt, false)`.
  3. Dispatches UI updates (`p.Inv.GetAC23_5()`, `AC 30:3`, `AC 29:2`, `AC 30:1` zero-clear, `p.Storage.GetAC30_5()`).
  4. Automatically persists state via `CharacterDataBase.WritePlayer(p.CharID, p)`.
  5. Sends confirmation system message: `📤 Withdrew '{ItemName}' x{qty} from Storage Slot {slot}.`

### 3.4 Move Item within Storage (`AC 30 Sub 4` / `AC 29 Sub 3`)
- **Client Request**: `[30, 4, srcSlot, dstSlot]`
- **Server Execution**:
  1. Relocates / swaps slots in `Player.Storage.MoveItem(srcSlot, dstSlot, ammt)`.
  2. Dispatches `p.Storage.GetAC30_5()`.
  3. Immediately persists state to SQLite database.
