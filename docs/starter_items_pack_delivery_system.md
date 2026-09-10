# Starter Items Pack Delivery System & Character Creation Persistence

## Overview
This document details the architecture and implementation of the authentic starter items pack delivery system for newly created and existing characters. Previously, starter items defined in [`Data/starter_items.json`](file:///d:/GitHub/Wonderland-Private-Server/Data/starter_items.json) were loaded into [`StarterPackManager`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/StarterPackManager.cs) and displayed in the Admin GUI (Tab 7), but [`StarterPackManager.DeliverToPlayer`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/StarterPackManager.cs) was never invoked anywhere during character creation or login. Furthermore, [`Inventory.AddItem`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Inventory.cs) previously bundled item placement decrement logic inside client packet transmission blocks, causing memory slot corruption when adding items silently prior to map initialization.

---

## Technical Components & Architecture

### 1. `StarterPackManager` Dual-Path Resolution & Delivery Overload
- **File**: [`wlo.pserver.core/Game/PlayerRelated/StarterPackManager.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/StarterPackManager.cs)
- **Multi-Path Path Resolution**:
  - `GetConfigPath()` evaluates candidates sequentially:
    1. Primary execution directory: `Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "starter_items.json")`
    2. Working directory: `Path.Combine(Environment.CurrentDirectory, "Data", "starter_items.json")`
    3. Source project directory: `Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Data", "starter_items.json")`
- **Synchronization**:
  - Saving via Admin GUI writes to the active path and automatically syncs to both the root `Data/` and binary output directories.
- **Parameters**:
  - `DeliverToPlayer(Player p, bool sendData = true)`:
    - Iterates over items ordered by `OrderIdx`.
    - Bounds quantity via `(byte)Math.Min(entry.Count, 255)`.
    - Passes `sendData` down to [`Inventory.AddItem`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Inventory.cs).
  - `HasAnyStarterItem(Player p)`:
    - Scans player inventory for any configured starter item IDs to prevent duplicate deliveries.

### 2. Silent Inventory Addition (`Inventory.AddItem`)
- **File**: [`wlo.pserver.core/Game/Inventory.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Inventory.cs)
- **Method Signature**:
  - `public void AddItem(ushort ID, byte amt, bool sendData = true)`
  - `public int AddItem(Item item, byte at = 0, bool sendData = true)`
- **Loop Terminating Fix**:
  - In `AddItem(Item, byte, bool)`, `addammt -= ammt;` is decoupled from `if (sendData && owner != null)`.
  - When `sendData` is `false` (character creation or offline batch additions), memory slots `m_Items[slot]` are updated accurately and quantity arithmetic terminates properly without sending unhandled `AC 23:6` / `AC 23:5` packets before socket map attachment.

### 3. Character Creation Hook (`AC 9 Sub 1`)
- **File**: [`Src/Network/ActionCodes/AC09.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC09.cs)
- **Execution Order**:
  1. Base stats, beginner outfit, HP/SP, and gold set on `tp`.
  2. Starter Quest 1 added to `tp.Started_Quests`.
  3. `StarterPackManager.DeliverToPlayer(tp, sendData: false)` populates `tp.Inv`.
  4. `cGlobal.gCharacterDataBase.WriteNewPlayer(tp.CharID, tp)` executes:
     - Pre-deletes stale character rows across relational tables.
     - Inserts player record into `characters`.
     - Serializes `tp.Inv.InventoryDBData` directly into SQL:
       `INSERT INTO inventory (invIdx,charID,storID,itemID,dmg,qty,pos,socketID,bombID,sewID,forge) VALUES ...`
     - Starter pack is committed to MySQL before user map entrance.
  5. `cGlobal.gWorld.OnLogin(tp)` queues client for login.

### 4. Level 1 First Login Fallback (`CommenceLogin`)
- **File**: [`Src/Server/WorldServer.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Server/WorldServer.cs)
- **Mechanism**:
  - In `CommenceLogin(Player src)`, after `cGlobal.gGameDataBase.LoadFinalData(src)`:
    - Checks: `if (src.Inv.FilledCount == 0 && (src.Eqs?.Level ?? 1) <= 1 && !StarterPackManager.HasAnyStarterItem(src))`
    - Calls `StarterPackManager.DeliverToPlayer(src, sendData: false);`
    - Immediately persists to DB via `cGlobal.gCharacterDataBase.WritePlayer(src.CharID, src);`.
  - Dispatches full inventory snapshot `src.Send(new SendPacket(src.Inv.GetAC23_5()));` to client during the initial login packet sequence.

### 5. Admin GUI Live Delivery (`Tab 7 Starter Items`)
- **File**: [`Src/Gui/MainForm1.ExtendedTabs.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.ExtendedTabs.cs)
- **Feature**:
  - Added button `🎁 Give to All Online` (`btnGiveOnline`).
  - Calls `ActionGiveStarterPackToOnline()`, iterating through `cGlobal.gCharacterDataBase.GetOnlinePlayers()`, delivering the pack with `sendData: true` (dispatches live `AC 23:6` and `AC 23:5` packets directly to online clients) and saving state to database.

---

## Configured Authentic Starter Pack Items

| Order | Item ID | Name | Count | Description |
|---|---|---|---|---|
| 1 | 34038 | Starter Gift 1 | 1 | Beginner gift package |
| 2 | 34058 | Remote Control | 1 | Auto-combat and assistant remote control |
| 3 | 34332 | Mini Dragonfly | 5 | Starter flying mount vehicle |
| 4 | 32176 | Spicy Hot Pot | 50 | Full recovery food |
| 5 | 34026 | Protective Exp Pill | 10 | Prevents EXP loss upon death |
| 6 | 34542 | Substitute Doll | 1 | Prevents companion amity drop upon death |
| 7 | 21742 | Goddess Robe | 1 | Starter protective equipment |
| 8 | 34330 | Mini HP Potion | 1 | Starter HP healing potions |
| 9 | 34190 | 10x Holy EXP Potion | 5 | Boosts experience gain |
| 10 | 34258 | Training Ticket | 5 | Instant training island pass |

---

## Edge Cases & Error Handling

1. **Character Creation without Map Session**:
   - Items added with `sendData: false` prevent socket exceptions and premature packets before map handshake.
2. **Pre-existing Characters Created Before Fix**:
   - Detected upon login via `FilledCount == 0 && Level <= 1` and delivered safely.
3. **Database Write Failure Protection**:
   - `WritePlayer` and `WriteNewPlayer` transactions cleanly wrap inventory serialization; any failure logs via `DebugSystem.Write` without crashing the game thread.
4. **Missing or Corrupt JSON**:
   - If `starter_items.json` is missing or contains invalid syntax, `StarterPackManager.LoadFromFile` automatically instantiates the 10 authentic defaults and writes them out to disk.
