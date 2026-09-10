# 06 - GUI Administration Suite and GM Command Engine

## 1. GUI Administration Suite (`MainForm1`)

The server supervisor is built with a responsive Windows Forms administration panel in [`Src/Gui/MainForm1.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.cs), providing comprehensive real-time monitoring and server management.

```
+---------------------------------------------------------------------------------------+
|  Wonderland Private Server - Server Supervisor                                        |
+---------------------------------------------------------------------------------------+
| [Dashboard] [Players] [Maps] [NPCs] [Quests] [Item Mall] [Chests] [Settings] [Security]|
+---------------------------------------------------------------------------------------+
| Cluster: 1  | Port: 6414/6415/6416 | Status: [ Green ] [ Yellow ] [ Red ] [ Auto ]     |
| Online: 12 Players | RAM: 142 MB   | Safe Save: [ Save All ] [ Safe Shutdown ]        |
+---------------------------------------------------------------------------------------+
```

---

## 2. Tab Specifications and Controls

| Tab Name | Data Sources | Key Controls & Functionality |
| :--- | :--- | :--- |
| **Server Dashboard** | Memory / Network | Start/Stop server listeners, view live connection console, trigger immediate DB save, toggle load status color (Green/Yellow/Red/Auto). |
| **Live Players** | `cGlobal.gLoginServer`, `characters` | Real-time list of connected players, CharID, UserID, Level, Current Map, Ping. Controls: Kick, Ban IP, Teleport to GM, Send Private Message. |
| **Map Inspector** | `wlo.pserver.maps` | Visual map viewer, coordinate locator, active player count per map instance, portal links. |
| **NPC & Mob Manager**| `npcs`, `npc_data` | Live inspection of active spawns, edit NPC coordinates `(x, y)`, swap template IDs, reload spawns from `spawns.csv`. |
| **Quest Studio** | `game_quests`, `charquest` | Browse 1,078 registered quests, inspect prerequisite dialogue IDs, edit EXP/Gold rewards, review player progress. |
| **Item Mall Studio** | `item_mall` | Catalog editor for Points Mall and Bonus Mall. Adjust prices, discounts, Hot/New badges, and save directly to SQLite. |
| **Chest Drops** | `chest_drops` | Configure chest container rewards, drop rates, and item counts. |
| **Player Settings** | `player_settings` | Global and per-character gameplay toggles: PK Mode (Allow/Disallow), Join Team Mode, Trade Mode. |
| **Security Center** | `banned_ips`, `banned_users` | Blacklist management: Add/Remove IP addresses, Ban user accounts, view ban audit logs. |

---

## 3. In-Game GM Chat Command Engine

Players authenticated with GM rights in SQLite table `gm_accounts` (or `is_gm = 1` in `users`) can execute real-time administrative commands via standard chat:

```
:command <arguments>
```

### Complete GM Command Reference

| Command | Arguments | Example | Description |
| :--- | :--- | :--- | :--- |
| `:heal` | `[hp] [sp]` | `:heal` or `:heal 5000 5000` | Fully restores character HP and SP (or sets specified amounts). |
| `:level` | `<1-200>` | `:level 100` | Sets character level, recalculates base stats, and updates client HP/SP bars. |
| `:gold` | `<amount>` | `:gold 999999` | Sets current character gold inventory balance. |
| `:stat` | `<str> <con> <int> <wis> <agi>` | `:stat 50 50 50 50 50` | Sets base character attribute distribution. |
| `:item` | `<item_id> [amount]` | `:item 23050 50` | Injects items directly into player inventory slots. |
| `:skill`| `<skill_id> [grade]` | `:skill 1001 5` | Unlocks skill and sets proficiency/grade. |
| `:warp` | `<map_id> <x> <y>` | `:warp 1001 500 600` | Instantly teleports character to target map coordinates. |
| `:speed`| `<speed_multiplier>` | `:speed 2` | Increases movement velocity for development navigation. |
| `:im` | `<points> [bonus]` | `:im 1000 200` | Grants Item Mall Points and Bonus Points. |
| `:help` | None | `:help` | Lists available commands in player chat box. |

---

## 4. Security & Ban Architecture

1. **Firewall Blacklist (`banned_ips`)**:
   - Checked during initial TCP handshake on Port 6414 and Port 6415.
   - If incoming remote endpoint IP is blacklisted, connection is immediately aborted prior to buffer allocation.
2. **Account Blacklist (`banned_users`)**:
   - Evaluated during authentication in [`LoginServer`](file:///D:/GitHub/Wonderland-Private-Server/Src/Server/LoginServer.cs).
   - Returns authentication failure packet with custom ban reason string.
3. **Audit Trail**:
   - Every ban record captures timestamp (`banned_at`), ban reason (`reason`), and executing administrator (`banned_by`).

---

## 5. Graceful Server Shutdown & Countdown Lifecycle

When the administrator triggers the server shutdown button (`btnSafeShutdown`) or closes the supervisor application:

1. **Non-Immediate Interception**: The application prevents immediate termination, disabling the shutdown buttons and keeping the GUI/console responsive.
2. **Data & State Flushing**:
   - Broadcasts immediate shutdown warning to all active players (`AC 23 Sub 57`).
   - Atomically saves character inventories, equipment, attributes, gold, and companion pets to SQLite.
   - Saves world map drop configurations (`chest_drops`) and server settings (`server_settings`).
   - Gracefully disconnects all connected sockets and stops TCP listeners on Ports 6414, 6415, 6416, and 8080.
3. **Log File Location Reporting**:
   - Flushes all pending log buffers to disk via [`DebugSystem.Flush()`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/System/DebugSystem.cs).
   - Writes the absolute canonical log file location (e.g., `Logs\wlophoenixlogFile_YYYYMMDD.txt`) to both the GUI dashboard console and CLI output stream.
4. **10-Second Countdown Sequence**:
   - Executes a 10-second countdown loop (`10` down to `1`).
   - Emits real-time countdown progress ticks every second to the console and GUI window title/buttons (`Closing (Xs)...`).
5. **Clean Exit**:
   - Finalizes logging subsystems via [`DebugSystem.EndIntialize()`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/System/DebugSystem.cs).
   - Gracefully terminates the process via `Environment.Exit(0)`.
