# GUI Administration Suite and GM Engine Specification

## 1. Architectural Overview

The server administration suite provides an ergonomic, high-density desktop control panel built with Windows Forms, paired with an in-game chat-based GM engine. The subsystem is implemented across [`MainForm1.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.cs), [`MainForm1.ModernUi.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.ModernUi.cs), [`ModernTheme.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Gui/ModernTheme.cs), and [`GmManager.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/GmManager.cs).

---

## 2. GUI Navigation Architecture & Theming

```
+-----------------------------------------------------------------------------------+
|  Executive Telemetry Header: [Players: 12] [Uptime: 04:15:22] [RAM: 418 MB] [Ports: 6414, 6415, 6416, 8080] |
+-----------------------------------------------------------------------------------+
| Tier 1 Navigation: [Server & Ops] [Accounts & Players] [Combat & GM] [World Studio] [Economy] |
+-----------------------------------------------------------------------------------+
| Tier 2 Sub-Tabs:  ( Server Info ) ( MOTD & Rates ) ( Live Logs ) ( Socket Monitor )|
+-----------------------------------------------------------------------------------+
|                                                                                   |
|                   100% Full-Width Working Workspace / DataGrid                    |
|                                                                                   |
+-----------------------------------------------------------------------------------+
```

### 2.1 Two-Tier Navigation System
* **Tier 1 Categories:** Divides 24 operational modules into 5 ergonomic categories (Server & Operations, Accounts & Players, Combat & GM Tools, World & NPC Studio, Economy & Rewards).
* **Tier 2 Sub-Tab Pill Strip:** Renders active modules as horizontal pill buttons, preserving 100% of horizontal screen real estate for deep DataGrids and map coordinates.

### 2.2 `ModernTheme.cs` & Performance Optimization
* **Dark Palette Design:** Custom dark slate background palette (`#1E1E2E`, `#252538`, `#313244`).
* **Double-Buffered Reflection:** Injects `DoubleBuffered = true` into all `TabPage`, `Panel`, and `DataGridView` instances via reflection, completely eliminating render flicker during live socket packet updates.
* **Global Shortcuts:**
  - `F5`: Launches client `aLogin.exe` instantly via [`PathHelper.GetClientExecutablePath()`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/RCLibrary.Core/PathHelper.cs).
  - `Ctrl+K`: Opens quick jump search dialog.

### 2.3 Relational Persistence of Server Settings
* The server Message of the Day (MOTD) and Server Name are persisted relationally in SQLite / MySQL table [`server_settings`](file:///D:/GitHub/Wonderland-Private-Server/docs/02_database_schema_and_persistence_lifecycle.md#37-server_settings).
* Changes auto-commit to the database on textbox `Leave` (blur) and `Enter` key events via [`ServerStatusManager.SaveMotd`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Server/ServerStatusManager.cs), persisting across restarts and SQLite-to-MySQL migrations.

---

## 3. In-Game GM Chat Command Directory

In-game commands are parsed in [`AC02.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC02.cs) and executed in [`GmManager.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/GmManager.cs), requiring `player.Account.gm >= 1`:

```
+---------------------------+-----------------------------------+-----------------------------------------------+
| Command                   | Syntax                            | Functional Description                        |
+---------------------------+-----------------------------------+-----------------------------------------------+
| :b / :broadcast / :notice | :broadcast <message>              | Broadcasts server-wide banner and alert SFX   |
| :kick                     | :kick <character> [reason]        | Disconnects player socket session immediately |
| :summon / :bring          | :summon <character>               | Warps target player to GM coordinates         |
| :goto / :tp               | :goto <character>                 | Warps GM directly to target player position   |
| :warp                     | :warp <map_id> [x] [y]            | Teleports player to destination map & coords  |
| :amity / :petamity        | :amity [1-100]                    | Sets active companion loyalty (default 100)   |
| :rebirth / :petrebirth    | :rebirth                          | Triggers instant companion Rebirth Ascension  |
| :allskills / :maxskills   | :allskills [grade]                | Unlocks all element & stunt skills            |
| :god / :godmode           | :god                              | Boosts stats to 999 and full heals HP/SP      |
| :winbattle / :killall     | :winbattle                        | Instantly completes active combat encounter   |
| :heal                     | :heal                             | Full restores HP and SP for player and pet    |
| :level                    | :level <level>                    | Sets player level (1..200) and updates stats  |
| :item                     | :item <id> [count]                | Spawns item into player inventory bag         |
| :clearinv                 | :clearinv                         | Clears all 50 slots of inventory bag          |
| :mute                     | :mute <char> [mins]               | Mutes target player's public chat             |
| :unmute                   | :unmute <char>                    | Restores player's public chat privileges      |
+---------------------------+-----------------------------------+-----------------------------------------------+
```
