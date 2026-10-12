# Instance Dungeon, PVP Rankings, and Server Events Specification

## 1. Instance Dungeon Engine

The client provides dedicated session states for temporary instanced dungeons:

### 1.1 Entry & Cooldown Restrictions
* **Validation Checks:**
  * Must select instance prior to warping: `"Select instance first"`.
  * Map location must contain valid instance portal: `"Can't enter instance here"`.
  * Cannot queue for a new instance while inside one: `"Leave instance first"` / `"Leave dungeon first"`.
  * Cooldown ratelimit guard: `"Make instance later"`.
  * Concurrent server capacity cap: `"Max instance amount"`.

### 1.2 Lifecycle Timers & Purge Protocol
* **Time Expiration:** `"Instance time is out"`.
* **Reward Phase:** 10-minute reward claim window before instance closure:
  * Alert: `"Instance complete. Claim rewards within 10 mins"`.
* **Evacuation Warning:** 5-minute warning before player ejection and map purge:
  * Alert: `"Instance clears in 5 mins. Players please leave it"`.
* **Map Purge:** `"Instance have been purged"` / `"Instance map purged"`.

---

## 2. PVP Rankings & Tournament System

### 2.1 PVP Rank Tiers
The client renders 4 distinct graphical rank badges on player profile cards:
* `[PVP Rank 1]` (Highest tier)
* `[PVP Rank 2]`
* `[PVP Rank 3]`
* `[PVP Rank 4]`
* Fallback state: `"You're unranked"`.

### 2.2 Leaderboards & Tournaments
* **Leaderboard Format:** `"Top DMG: %u, Rank %d"`.
* **Ranking Computation:** Displays `"Arranging ranks.."` during sync.
* **Events:**
  * 4v4 Arena: `"4-PVP event has begun! Good luck in ranking!"`.
  * Open Arena: `"PVP arena has begun! Come and join at venue!"` / `"PVP arena has ended! Come again next time!"`.
  * Cross-Server Sync: `"Interserver PVP Ranks updated! You can check leaderboard"`.
