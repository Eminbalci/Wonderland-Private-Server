# Technical Specification: Personal Player Stall & Custom Kiosk Uncoupling

## 1. Overview
Investigation into reported stall behavior where typing `:stall selam` spawned an external character entity (Daniel body) next to the player rather than putting the player's own character into vending stance. 

## 2. Root Cause Analysis
1. **Command Alias Collision:**
   In `wlo.pserver.core/Game/Player.cs`, `:stall` and `/stall` were grouped into the same conditional block as `:spawnstall`. When the player issued `:stall selam`, the parser treated `"selam"` as the title parameter and routed execution to `PlayerStallBazaarEngine.SpawnCustomStall`.
2. **Dual-Entity Body Duplication in Standalone Kiosks:**
   `SpawnCustomStall` was generating a random vendor ID (`10000 + random`), attaching a Props Keep merchant NPC (`TemplateID = 14134`) via `AC 22:4`, and simultaneously broadcasting an `AC 4` (AddPlayer / SpawnActor) packet with `Body = 1` (Daniel). This caused a second character model to render beside the player.
3. **Missing Personal Vending Stance Workflow:**
   Opening a player's own stall does not require spawning a new actor entity because the character already exists on the map. It requires transitioning the calling player's character into sitting posture (`AC 32:2`), activating the kiosk state (`AC 5:16`), attaching the floating signboard banner (`AC 56:30`), and acknowledging via `AC 53:1`.

## 3. Architecture & Protocol Design

### 3.1 Personal Player Stall (`:stall <title>` / `:openstall <title>`)
* **Entity Mode:** Modifies the calling player character (`c.CharID`). No external actor or NPC is spawned.
* **Posture Transition:** Sets `c.Emote = 1` (sitting). Broadcasts `AC 32:2` (emote = 1) to map peers and echoes to vendor.
* **Kiosk State:** Broadcasts `AC 5:16` (active = 1) to map peers and vendor.
* **Signboard Banner:** Broadcasts `AC 56:30` containing `stallTitle` above `c.CharID`.
* **Inventory Population:** Extracts up to 10 active items from `c.Inv` with market pricing; falls back to default high-value trading goods if inventory is empty.
* **Engine Registration:** Registers stall under `c.CharID` in `PlayerStallBazaarEngine` and synchronizes with `StallManager`.
* **ACK Packet:** Sends `AC 53:1` response to the vendor with stall title and item count.
* **Chat Notification:** Sends system chat feedback: `[Stall] Your vending stall '<title>' is now OPEN! You are vending in sitting posture at (X, Y). Other players can click you or type ':buystall' to buy. Type ':closestall' to close.`

### 3.2 Personal Stall Closure (`:closestall` / `:stall close`)
* **Posture Reset:** Sets `c.Emote = 0` (standing). Broadcasts `AC 32:2` (emote = 0) to map peers and vendor.
* **Kiosk Deactivation:** Broadcasts `AC 5:16` (active = 0) to map peers and vendor.
* **Signboard Removal:** Broadcasts `AC 56:30` with empty string to remove the banner above `c.CharID`.
* **ACK Packet:** Sends `AC 53:3` closed notification to vendor.
* **Engine Unregistration:** Removes `c.CharID` from `PlayerStallBazaarEngine` and `StallManager`.

### 3.3 Movement Interrupt (`AC 06:1`)
* If a vending player moves (`movedDistance > 0`), the server automatically invokes `ClosePersonalStall(p)` and sends: `[Stall] You moved; your vending stall has been closed.`

### 3.4 Remote Custom Kiosk Spawning (`:spawnstall <map> <x> <y> [title]`)
* Reserved exclusively for GMs and Experimental Tab administrators.
* Spawns an authentic stationary `StallVendorNpc` (`QuestNpc`, `TemplateID = 14134`, `CickID = 200..250`) on the target map.
* Strictly broadcasts `AC 22:4` (NPC spawn) and `AC 56:30` (Signboard banner). No dummy `AC 4` player body is broadcast.
* Added to `Map.NPCs` for interaction via `AC 20:1` / `AC 20:14`.

## 4. Verification & Testing
* Automated test script executed against compiled assemblies:
  1. `OpenPersonalStall(p, "selam")`: Confirmed `p.Emote == 1`, title `"selam"`, 5 items listed, 0 duplicate actors spawned.
  2. `ClosePersonalStall(p)`: Confirmed `p.Emote == 0`, stall unregistered, signboard removed.
  3. `SpawnCustomStall(99999, "Merchant", "OverworldKiosk", 12000, 1200, 1500)`: Confirmed `StallVendorNpc` spawned with `TemplateID == 14134` at ClickID 200 without actor duplication.
  4. `ClearAllStalls()`: Confirmed 100% clean despawn of all active bazaar kiosks.
