# Client Advanced Systems Decompiled Specifications

This document outlines additional client-side game systems extracted from deep scanning of `aLogin.exe.1.c`: instance dungeons, PVP rankings, mini-games (dig hole / lucky draw), treasure hunts, title/badge management, voyage maps, and chaos crystals.

---

## 1. Instance Dungeon System

The client manages instanced dungeon entry, exit, timers, and reward claim phases:

### A. Entry & Exit
- `"Select instance first"` — No instance selected when attempting entry.
- `"Can't enter instance here"` — Map location is not an instance portal.
- `"Leave instance first"` / `"Leave dungeon first"` — Must exit before certain actions.
- `"Has Left Instance"` — Confirmation upon leaving.

### B. Timers & Purging
- `"Instance time is out"` — Duration expiry broadcast (system chat + alert).
- `"Instance complete. Claim rewards within 10 mins"` — Reward phase with 10-minute countdown.
- `"Instance clears in 5 mins. Players please leave it"` — 5-minute purge warning.
- `"Instance have been purged"` / `"Instance map purged"` — Cleanup confirmation.
- `"Make instance later"` — Cooldown/ratelimit active.

### C. Limits
- `"Max instance amount"` — Maximum concurrent instances reached.

---

## 2. PVP Rankings & Arena System

### A. PVP Rank Tiers
The client displays rank badges from 1 (highest) to 4:
- `"[PVP Rank 1]"` / `"[PVP Rank 2]"` / `"[PVP Rank 3]"` / `"[PVP Rank 4]"`

### B. Leaderboard
- `"Rank: "` — Prefix label used in multiple leaderboard UI panels.
- `"Arranging ranks.."` / `"Arranging ranks..."` — Loading animation labels during rank computation.
- `"You're unranked"` — Displayed when a player has no placement.
- `"Top DMG: %u, Rank %d"` — Damage leaderboard format string.
- `"Interserver PVP Ranks updated! You can check leaderboard"` — Cross-server rank sync notification.

### C. PVP Events
- `"4-PVP event has begun! Good luck in ranking!"` — 4v4 PVP event start.
- `"PVP arena has begun! Come and join at venue!"` — Arena open broadcast.
- `"PVP arena has ended! Come again next time!"` — Arena close broadcast.

---

## 3. Mini-Games

### A. Dig Hole Game
- UI Form: `"MiniGame_DigHole_Form"` — Main dig hole mini-game panel.
- Animation assets: `"DigHole"`, `"DigHole_Hole"`, `"DigHole_Finger"`, `"icon_dig_1"`.

### B. Lucky Draw Game
- UI Form: `"MiniGame_Lucky_Form"` — Lucky number draw mini-game panel.

### C. Quiz Event
- `"[Quiz Event] Rankings: "` / `"[Quiz Zone] Rankings: "` — Quiz competition leaderboard displays.

---

## 4. Treasure System

- `"icon_SelectTreasure"` — Treasure selection icon asset.
- `"Special treasure drop event has ended!"` — Seasonal treasure event broadcast.

---

## 5. Title & Badge System

### A. Titles
- `"Title_"` — Dynamic title prefix template (constructed with appended index values).
- `"Unknown Title"` — Fallback for unrecognized title IDs.

### B. Badges
- `"Badge updated"` — Badge change confirmation.
- `"Can't change badge"` — Badge modification denied.
- `"Badge force changed"` — Admin/system-forced badge override.

---

## 6. Voyage / Sailing Map System

- `"Can only be used on Voyage Map"` — Item restricted to sailing/voyage maps only.
- `"Sailing has begun! Double EXP, mini-games, and Forges 50% Off!"` — Seasonal sailing event.
- `"Sailing event has ended!"` — Sailing event close.
- `"Dragon Boat double EXP event has begun! Don't miss it!"` / `"Dragon Boat double EXP event has ended!"` — Dragon Boat festival events.

---

## 7. Chaos Crystal System

- `"Reached max Chaos Crystals"` — Crystal collection cap reached.
- `"Crystal"` — Crystal item category label.

---

## 8. Socket System (Network Internals)

Low-level socket management error strings:
- `"Socket send aborted"` / `"Socket readln aborted"` / `"Socket capture aborted"` — Connection failure handlers.
- `"Max Sockets= "` — Maximum socket pool limit display.
- `"Request Close Socket"` — Graceful socket teardown request.
