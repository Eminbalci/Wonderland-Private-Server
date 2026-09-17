# GUI Administration Suite and GM Engine

## 1. Architectural Overview

The Wonderland Online private server provides an integrated desktop operator console implemented in Windows Forms ([`MainForm1.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.cs) and [`MainForm1.ExtendedTabs.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.ExtendedTabs.cs)), coupled with a deep character editor ([`CharacterDataEditorForm.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Gui/CharacterDataEditorForm.cs)) and an in-game chat command engine ([`AC02.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC02.cs)).

---

## 2. Windows Forms Operator Dashboard

The administration GUI organizes server management across 13 specialized operational tabs:

### 2.1 Tab Index & Functional Matrix

| Tab Name | Method Target | Operational Capability |
|:---|:---|:---|
| **Online Sessions** | `SetupOnlineSessionsTab()` | Real-time player session grid, IP inspection, latency tracking, forced disconnection, whisper dispatch. |
| **Developer & GM Tools** | `SetupGmTab()` | One-click coordinates warp, instant vehicle mounting, custom inventory injection, live NPC battle triggers. |
| **Character Data Editor**| [`CharacterDataEditorForm`](file:///D:/GitHub/Wonderland-Private-Server/Src/Gui/CharacterDataEditorForm.cs) | Granular modification of character stats (STR, CON, INT, WIS, AGI), 50 bag slots, equipped gear, pet amity, and quests. |
| **Item Mall Catalog** | `SetupItemMallTab()` | In-game microtransaction store editor, point price adjustment, promotional bundles, catalog hot-reloading. |
| **Monster Drops** | `SetupMonsterDropsTab()` | Drop rate modifier, item drop slot editor, direct synchronization and reload from `Npc.dat`. |
| **Dialogue & Talk Resolver**| `SetupTalkResolverTab()` | Searchable 17,494-entry `Talk.dat` browser, reverse ASCII text preview, live dialogue frame packet injection. |
| **NPC Studio & Scene Inspector**| `SetupMapNpcStudioTab()` | Overworld map NPC visualizer, scene coordinates editor, roam behavior toggling. |
| **NPC Binary Resolver** | `SetupNpcResolverTab()` | Low-level `Npc.dat` inspector, deciphering XOR `0x5209` IDs, monster combat stats, and animations. |
| **Guild Management** | `SetupGuildsTab()` | Guild roster view, guild level adjustment, leadership transfer, guild bank inspection. |
| **Mail Dispatcher** | `SetupMailTab()` | Mailbox audit log, item attachment inspector, server-wide broadcast mail composer. |
| **Security & Ban Center**| `SetupSecurityTab()` | IP/Subnet ban matrix, hardware/account lockout controls, anti-flood packet thresholds. |
| **Live Battle Monitor** | `SetupLiveBattlesTab()` | Active battle arena inspector, turn clock monitoring, combat participant health gauges. |
| **Marriage Registry** | `SetupMarriagesTab()` | Active marriage records, couple intimacy stats, divorce administration. |
| **Starter Items Config** | `SetupStarterItemsTab()` | Visual configuration tool for `starter_items.json` default equipment packages. |

### 2.2 Client Quick-Launch Integration
* **F5 Shortcut:** Automatically verifies `SERVER.INI`, binds the local socket listener, and launches the game client (`aLogin.exe`).
* **Shift + F5 Shortcut:** Opens a file browser to re-bind or locate the official game client root directory.

---

## 3. In-Game GM Chat Command Engine

Game Masters and authorized players can execute administrative commands directly in the in-game chat interface via [`AC 2`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC02.cs#L22). Commands accept both `:` and `/` prefixes.

### 3.1 Command Reference

```
+-------------------+---------------------------+-------------------------------------------------------+
| Command           | Parameters                | Functional Description                                |
+-------------------+---------------------------+-------------------------------------------------------+
| :heal / :full     | [hp] [sp]                 | Restores HP and SP to maximum (or custom value).      |
| :level / :lvl     | <1..200>                  | Sets character level, recalculates stat points.       |
| :points / :sp     | <amount>                  | Grants unallocated attribute stat points.             |
| :gold / :money    | <amount>                  | Sets character wallet gold balance.                   |
| :exp              | <amount>                  | Sets total character experience points.               |
| :stats / :stat    | <str> <con> <int> <wis>   | Directly sets primary attributes and recalculates     |
|                   | <agi>                     | maximum HP and SP limits.                             |
| :item             | [add] <id> [count]        | Injects item into character bag. Prevents duplicate   |
|                   |                           | radio set (ID 34076) exploit.                         |
| :warp / :goto     | <map_id> <x> <y>          | Instantly teleports character to target coordinates.  |
| :skill            | <skill_id> [grade]        | Unlocks or upgrades skill in player skill book.       |
| :buy              | <query/id> [quantity]     | Purchases or spawns Item Mall items by name or ID.   |
| :pet              | <pet_id>                  | Spawns and recruits specified companion into roster.  |
| :unride           | None                      | Dismounts active ride pet or vehicle.                 |
| :help             | None                      | Prints player-accessible command directory.           |
+-------------------+---------------------------+-------------------------------------------------------+
```

### 3.2 Security Validation & Anti-Exploit Guards
* **Radio Set Guard:** Prevents duplicate acquisition of the Communication Radio (`ItemID = 34076`), preventing inventory clutter and script corruption.
* **Coordinate Clamping:** Teleport coordinates are clamped to valid map boundaries (`50 <= X, Y <= 4000`) to prevent map edge clipping.
* **Bounded Level Ranges:** Player level modifications via `:level` are strictly clamped between `1` and `200`.
