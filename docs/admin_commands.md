# In-Game GM & Admin Commands

## Overview
In-game commands are executed via normal or local chat. All commands are prefixed with `:` and handled in [`Src/Network/ActionCodes/AC02.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC02.cs).

---

## Command Reference

| Command | Arguments | Description | Example |
|---|---|---|---|
| `:heal` / `:hp` / `:full` | `[hp] [sp]` | Restores full HP and SP (or custom specified values) and syncs stats directly to the client. | `:heal`<br>`:heal 500 200` |
| `:level` / `:lvl` | `<level>` | Sets character level (1–200), recalculates required EXP, restores full HP/SP, and syncs full stats via `AC 8:1`. | `:level 50` |
| `:gold` / `:money` | `<amount>` | Sets character gold and syncs with client (`SendStat(39)`). | `:gold 999999` |
| `:stat` / `:stats` | `<str> <con> <int> <wis> <agi>` | Customizes core player attributes and updates combat values. | `:stat 100 80 20 20 50` |
| `:item` | `<id> [count]` or `add <id> [count]` | Adds items directly to the player's inventory. | `:item 34076`<br>`:item 10001 10` |
| `:skill` | `<id> [grade]` | Unlocks/updates a specific character skill and broadcasts `AC 5:11` / `AC 8:1 (110)`. | `:skill 15041 3` |
| `:warp` / `:goto` | `<map_id> <x> <y>` | Teleports the player to specified map coordinates. | `:warp 10017 1400 600` |
| `:help` / `:cmds` | None | Lists available GM commands in system chat. | `:help` |

---

## Source Files
- [`Src/Network/ActionCodes/AC02.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC02.cs)
- [`wlo.pserver.core/Game/PlayerRelated/Equip.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Equip.cs)
