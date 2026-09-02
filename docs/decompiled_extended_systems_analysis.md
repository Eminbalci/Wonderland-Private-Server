# Extended Decompiled Analysis: Game Subsystems & Operational Rules

## 1. Overview
In-depth reverse engineering analysis of `decompiled/aLogin_decompiled.c` reveals the complete client state machines, constraint checks, and subsystem mechanics across Equipment, Tent/Crafting, Social/Trading, Marriage, PVP/Events, and Pet/Mount systems.

---

## 2. Equipment, Forging & Durability Subsystems

### 2.1 Durability Degradation Engine
* **Subroutine:** [`FUN_003f45ec`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L3f45ec) (`@ 0x003f45ec`)
* **Triggers:**
  * Displays `"Equipped [[Item]] durability run out"` when item durability reaches 0.
  * Disables attribute contributions until repaired with repair wrenches or corresponding tier repair tools.

### 2.2 Gem Socketing & Enhancement
* **Subroutine:** [`FUN_0049f998`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L49f998) (`@ 0x0049f998`)
* **Mechanics:**
  * Handles `"Diamond"` (ATK/MATK enhancement) and `"Crystal"` (DEF/MDEF enhancement) socket insertion.
  * Escalating failure risk per socket level.

---

## 3. Tent, Furniture & Resource Gathering Subsystems

### 3.1 State Exclusivity & Deployment Guards
* **Subroutine:** [`FUN_0042b194`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L42b194) (`@ 0x0042b194`)
  * Restricts tent deployment: `"Fishing, can't use tent"`, `"Collecting, can't use tent"`.
* **Subroutine:** [`FUN_0034a24c`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L34a24c) (`@ 0x0034a24c`)
  * Enforces stall exclusivity: `"Can't set more than 1 Stall"`, `"Fishing, can't use"`.

### 3.2 Furniture Placement Grid & Storage
* **Subroutines:** [`FUN_003fccc8`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L3fccc8) & [`FUN_003762a8`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L3762a8)
  * Validates placement collisions: `"No space for furniture"`, `"Placed in tent claim area"`.
  * Enforces storage capacity limits across Storeroom, Furniture props, and Cabinet inventories: `"Full, can't store more"`.

---

## 4. Social, Trading & Marriage Mechanics

### 4.1 Trade & Personal Stall Security
* **Subroutine:** [`FUN_004768ac`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L4768ac) & [`FUN_00385358`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L385358)
  * Enforces PIN security locks: `"Trade is locked"`, `"Unlock to trade items"`, `"Unlock to set a Stall"`.
  * Mutual state synchronization: `"Target uses Stall"`, `"Close Stall first"`, `"Trade complete"`.

### 4.2 Marriage System
* **Subroutine:** [`FUN_0021e4cc`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L21e4cc) (`@ 0x0021e4cc`)
* **Requirements:**
  1. Both players must be in the same 2-player party: `"Have to apply in party"`, `"Wrong party member"`.
  2. Initiates wedding sequence: `"Marriage ceremony has begun"`.

### 4.3 Chat & Team Communication
* **Subroutines:** [`FUN_0027e2c4`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L27e2c4) & [`FUN_0042209c`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L42209c)
  * Toggles and enforces channel isolation for Team, Guild, and World chat.
  * Map-level team formation restrictions: `"Can't team here"`, `"Teaming is turned off"`.

---

## 5. PVP, Matchmaking & Event Schedules

### 5.1 Team PVP Tournament
* **Subroutine:** [`FUN_00447bb8`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L447bb8) (`@ 0x00447bb8`)
* **Schedule & Constraints:**
  * Venue: Capitol Building 2F in Welling Village.
  * Level threshold: Player level $\ge 10$ (`"Below LV10 can't participate"`, `"A team member is below LV10"`).
  * Automated broadcast countdowns (5-minute start notice, active round notifications, 5-minute end notice).

### 5.2 Matchmaking Queues
* **Subroutine:** [`FUN_004431b0`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L4431b0) (`@ 0x004431b0`)
  * Implements `"Solo PVP Matchmaking..."` and `"Team PVP Matchmaking..."` matchmaking queues.

---

## 6. Pet, Mount & Amity Operational Rules

### 6.1 Saddle Mounting Constraints
* **Subroutine:** [`FUN_00291828`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L291828) (`@ 0x00291828`)
* **Requirements:**
  * Minimum Amity threshold: $\text{Amity} \ge 40$ (`"Can't use, Amity below 40"`).
  * Environment restrictions: `"Can't mount in bath"`, `"Can't mount when in transport"`.

### 6.2 Pet Trade & Cage Requirements
* **Subroutines:** [`FUN_00344ac8`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L344ac8) & [`FUN_003e193c`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L3e193c)
* **Rules:**
  * Companion trade requires Pet Cage: `"Use Pet Cage to trade"`.
  * Prohibits trading if $\text{Amity} = 0$ or target already owns identical companion: `"Can't trade, Amity is 0"`, `"Can't trade when possess the same pets"`.
  * Crafting / production pets cannot be liquidated: `"Can't sell Crafting pet"`.
