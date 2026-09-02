# GUI Responsive Layout & Dynamic Scaling System

## Overview
Provides automatic dynamic resizing and scaling for all tabs, views, log consoles, and data tables when resizing or maximizing the server application window.

## Changes & Layout Structure

### 1. Form & Container Level
- **Form Size:** Default client size configured to `1200 x 780` with minimum size constraint of `1000 x 680`.
- **Form Border Style:** Set to `FormBorderStyle.Sizable` allowing window resizing and maximizing.
- **Root Containers:** `tabControl1` and `tabControl3` configured with `DockStyle.Fill` to eliminate static blank margins and fill the entire client area.

### 2. Tab Control Anchors & Dynamic Layouts
- **Monster Drops Tab (`SetupMonsterDropsTab`):**
  - Uses `SplitContainer` (`DockStyle.Fill`, `Orientation.Vertical`, `SplitterDistance = 350`).
  - **Left Panel (Panel1):** Search bar panel (`pnlSearch`, `Dock = Top`) and monster list DataGridView (`dgvMonsterList`, `Dock = Fill`).
  - **Right Panel (Panel2):** Status header (`pnlSelectedTop`, `Dock = Top`), drop editing action controls (`grpDropEdit`, `Dock = Bottom`, Height 200px), and monster drop items DataGridView (`dgvMonsterDrops`, `Dock = Fill`) with proper Z-order BringToFront rendering.
- **Item Mall Tab (`SetupItemMallTab`):**
  - Uses `SplitContainer` (`DockStyle.Fill`, `Orientation.Vertical`, `SplitterDistance = 640`).
  - **Left Panel (Panel1):** Catalog `dgvMallCatalog` (`DockStyle.Fill`), with top header panel and bottom action bar (`btnMoveUp`, `btnMoveDown`, `btnReload`).
  - **Right Panel (Panel2):** `grpEditItem` (Add/Edit Item Details) and `grpPoints` (Player IM Points Management) anchored cleanly (`Top | Left | Right`).
- **Cheat Tab (`tabPage1`):**
  - `panel_CheatTop` (`DockStyle.Top`, Height 36px): Houses `label_SelectedPlayer` ("🎯 Target Player:"), `comboBox_OnlinePlayers`, and `label_CheatHint`. Eliminates all visual clipping and overlap with list headers.
  - `tableLayoutPanel_Cheat` (`DockStyle.Fill`, 4 columns @ 25% equal width each):
    - Column 0: `groupBox_Maps` (`Dock = Fill`)
    - Column 1: `groupBox_Vehicles` (`Dock = Fill`)
    - Column 2: `groupBox_Items` (`Dock = Fill`)
    - Column 3: `groupBox_Npc` (`Dock = Fill`)
  - All inner search textboxes use `Anchor = Top | Left | Right`, listboxes use `Anchor = Top | Bottom | Left | Right`, and action controls stay anchored to bottom edges.
- **Status Tab (`tabPage7`):**
  - `MainOutput` (Server Log Box): `Anchor = Top | Bottom | Left | Right`
  - `btnSaveAllNow`, `btnSafeShutdown`: `Anchor = Top | Right`
- **Users Tab (`tabPageUsers`):**
  - `dataGridViewUsers`: `Anchor = Top | Bottom | Left | Right`
  - Action Buttons (`btnRefreshUsers`, `btnDeleteUser`, `btnChangePassword`): `Anchor = Top | Right`
- **Portals Tab (`tabPagePortals`):**
  - `dgvPortals`: `Anchor = Top | Left | Right`
  - `dgvDestinations`: `Anchor = Top | Bottom | Left | Right`
  - Action Buttons: `Anchor = Top | Right`
- **Characters Tab (`tabPageCharacters`):**
  - `dgvCharacters`: `Anchor = Top | Bottom | Left | Right`
  - Action Buttons: `Anchor = Top | Right`
- **Player Settings Tab (`tabPageSettings`):**
  - `dgvSettings`: `Anchor = Top | Bottom | Left | Right`
  - Action Buttons: `Anchor = Top | Right`
- **Friends Tab (`tabPageFriends`):**
  - `dgvFriends`: `Anchor = Top | Bottom | Left | Right`
  - Action Buttons: `Anchor = Top | Right`
- **Inventory Tab (`tabPageInventory`):**
  - `dgvInventory`: `Anchor = Top | Bottom | Left | Right`
  - Action Buttons: `Anchor = Top | Right`
- **Stats Tab (`tabPageStats`):**
  - `dgvStats`: `Anchor = Top | Bottom | Left | Right`
  - Action Buttons: `Anchor = Top | Right`
- **Chest Drops Tab (`tabPageChestDrops`):**
  - `dgvChestDrops`: `Anchor = Top | Bottom | Left | Right`
  - Action Buttons: `Anchor = Top | Right`
  - `grpAddDrop`: `Anchor = Bottom | Left | Right`
- **GM Management Tab (`SetupGmTab`):**
  - `lstGmList`: `Anchor = Top | Bottom | Left`
- **Quest DB Manager Tab (`SetupQuestManagerTab`):**
  - Uses `SplitContainer` with `Dock = DockStyle.Fill`.
