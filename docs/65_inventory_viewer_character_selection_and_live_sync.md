# Inventory Viewer Character Selection and Live Inventory Sync Architecture

## 1. Overview and Problem Analysis

### 1.1 Root Causes
1. **Unselected Default Index**: Previously, `LoadCharacterFilters()` populated `cmbCharacterFilter` and `cmbCharacterFilterStats` but never assigned `SelectedIndex = 0`. Because ComboBoxes default to `SelectedIndex = -1` (`SelectedItem == null`), attempting to click "Refresh" triggered a blocking modal dialog (`MessageBox.Show("Please select a character first.")`), preventing users from loading characters.
2. **Missing Live Online Player Synchronization**: `LoadCharacterFilters()` previously only executed a static query against `cGlobal.gCharacterDataBase.GetAllCharacters()` at server boot. Active online players who logged in, created characters, or modified inventories in memory during runtime were not added to the dropdown and lacked online status indicators.
3. **Restricted Storage Query (`storID = 1`)**: The previous SQL query in `btnRefreshInventory_Click` strictly filtered `WHERE i.charID = {charID} AND i.storID = 1`. In the Wonderland database schema, `storID = 0` represents Bag Inventory, `storID = 1` represents Equipped Gear, and `storID = 2` represents Tent Storage. As a result, standard bag inventory items were completely omitted from the view.
4. **Stale In-Memory Disconnect**: When players are online, item acquisitions (crafting, quests, drops, NPC purchases) reside in memory (`player.Inv`) prior to database writes. Directly reading the database returned stale or empty records for active sessions.
5. **Raw IDs Without Item Names**: The data grid displayed raw numeric IDs without resolving human-readable item names from `cGlobal.ItemDatManager`.
6. **No Navigation Hook**: Navigating to the "Inventory Viewer" tab in Modern UI or classic tab mode never triggered a character refresh, locking users out if no character was pre-selected.

---

## 2. Technical Implementation

### 2.1 Character Filter Item & ID Parsing ([`MainForm1.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.cs))
- Introduced `Form1.CharacterFilterItem`:
  - `CharID` (`uint`): Unique character identifier.
  - `Name` (`string`): Character name.
  - `IsOnline` (`bool`): Active session status.
  - `ToString()`: Formats active sessions as `"{CharID} - {Name} [Online]"` and offline characters as `"{CharID} - {Name}"`.
- Implemented `GetSelectedCharId(ComboBox cmb)`:
  - Extracts `CharID` directly if `SelectedItem` is `CharacterFilterItem`.
  - Fallback regex extractor isolates the leading integer from formatted strings even when prefixed or postfixed with tags, ensuring non-breaking parsing across legacy strings.

### 2.2 Dynamic Filter Population & Selection Persistence (`LoadCharacterFilters`)
- Merges database records (`cGlobal.gCharacterDataBase.GetAllCharacters()`) and active online player instances from `cGlobal.gCharacterDataBase.GetOnlinePlayers()` and `cGlobal.gLoginServer.GetAllPlayers()`.
- Sorts online players to the top of the dropdown list (`OrderByDescending(c => c.IsOnline).ThenBy(c => c.CharID)`).
- Preserves the user's previously selected character ID across refreshes.
- Automatically selects the first character (`SelectedIndex = 0`) if no prior selection existed, completely eliminating false-positive "Please select a character first" blocks.
- Configures `DropDownWidth = 280` on both `cmbCharacterFilter` and `cmbCharacterFilterStats` to prevent truncation of long names.

### 2.3 Live & Offline Inventory Loader (`btnRefreshInventory_Click`)
- Auto-reloads `LoadCharacterFilters()` if the combobox is unselected or empty upon clicking "Refresh".
- Detects if the selected character is currently active in memory:
  - **Online**:
    - Bag items: Iterates `livePlayer.Inv[1..50]`, retrieving `ItemID`, `Ammt`, and `Damage`.
    - Equipped gear: Iterates `livePlayer.Eqs[1..6]`, retrieving `ItemID`, `Ammt`, and `Damage`.
    - Tent storage: Queries `inventory WHERE charID = {charID} AND storID = 2`.
  - **Offline**:
    - Queries all storage tiers (`storID IN (0, 1, 2)`) with `ORDER BY i.storID, i.pos`.
- Resolves human-readable item names via `GetItemNameById(ushort itemId)` backed by `cGlobal.ItemDatManager.GetItemByID(itemId)`.
- Displays structured `DataTable` columns:
  - `charID` (`uint`)
  - `CharName` (`string`)
  - `Storage` (`string`: "Bag", "Equipped", "Tent")
  - `storID` (`byte`: hidden column for deletions)
  - `Slot` (`int`)
  - `itemID` (`ushort`)
  - `ItemName` (`string`)
  - `Quantity` (`ushort`)
  - `Damage` (`byte`)

### 2.4 Synchronized Item Deletion (`btnDeleteItem_Click`)
- Reads exact `storID` and `Slot` from the selected row.
- If the player is online:
  - If `storID == 0` (Bag): calls `livePlayer.Inv.RemoveItem((byte)slot, livePlayer.Inv[(byte)slot].Ammt, senddata: true)` to dispatch the deletion packet (`AC 23:5`) directly to the connected client.
  - If `storID == 1` (Equipped): calls `livePlayer.Eqs[(byte)slot].Clear()`.
- Executes parameterized deletion on `cGlobal.gCharacterDataBase`:
  `DELETE FROM inventory WHERE charID = {charID} AND pos = {slot} AND storID = {storID}`.
- Re-queries and updates the inventory view immediately.

### 2.5 Tab Navigation Hooks ([`MainForm1.ModernUi.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.ModernUi.cs))
- Subscribed to `tabPageInventory.Enter` and `tabPageStats.Enter` in the main constructor.
- Integrated `SwitchToTab(TabPage tab, Button subBtn)` in `MainForm1.ModernUi.cs`: whenever the user clicks "Inventory Viewer" or "Base Stats & Attributes", `LoadCharacterFilters()` and grid data refreshes execute automatically.

---

## 3. Automated Verification

An automated verification suite (`scratch/test_inventory_viewer.cs`) was compiled and executed with the following results:
- **Total Assertions**: 25 Passed, 0 Failed.
- **Coverage**:
  1. `CharacterFilterItem` string serialization (`[Online]` flag formatting).
  2. `GetSelectedCharId` extraction from objects and legacy strings.
  3. ComboBox instantiation and initialization.
  4. Online player insertion and ordering priority.
  5. Auto-selection (`SelectedIndex = 0`) on refresh.
  6. Live memory bag reflection (`livePlayer.Inv`).
  7. Human-readable item name resolution from `PhxItemDat`.
  8. Data grid schema validation (`Storage`, `Slot`, `itemID`, `ItemName`, `Quantity`, `Damage`).
