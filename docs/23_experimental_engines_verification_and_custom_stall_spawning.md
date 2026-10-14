# 23 - Experimental Engines Verification and Custom Coordinate Stall Spawning Specification

## 1. Overview

This document specifies the comprehensive verification of all 23 experimental gameplay systems across Waves 1, 2, and 3, and documents the custom map and coordinate stall spawning architecture introduced in the `PlayerStallBazaarEngine` and GUI Experimental Tab.

---

## 2. All 23 Experimental Engines Verification Matrix

All 23 engines were tested end-to-end via automated reflection and runtime assertions with a 100% pass rate (23/23 passed, 0 failed):

| # | System Name | Action Code / Spec | Core Engine Class | Test Operation Executed | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **1** | Instance Dungeons | AC 85 / Spec 56 | `InstanceDungeonEngine` | `CreateRoom`, `JoinRoom`, `LeaveRoom` | **PASS** |
| **2** | Ranked PvP Arena | AC 88 / Spec 16 | `PvPArenaEngine` | `EnqueuePlayer`, Match Found, `EloRating` | **PASS** |
| **3** | Guild Siege Wars | AC 84 / Spec 31 | `GuildSiegeEngine` | `StartSiege`, `DamageCrystal`, `EndSiege` | **PASS** |
| **4** | Arcade Minigames | AC 76 / Spec 38 | `ArcadeMinigameEngine` | `StartSession` (WhackAMole), `SubmitScore` | **PASS** |
| **5** | Festival Races | AC 82 / Spec 24 | `FestivalRaceEngine` | `StartRaceForPlayer`, `PassCheckpoint` | **PASS** |
| **6** | Map Mechanisms | Eve Op 16 / Spec 47 | `MapMechanismEngine` | `GetOrRegister`, `ToggleMechanism` | **PASS** |
| **7** | Companion Talents | Spec 07 / Spec 55 | `CompanionTalentsEngine` | `AllocateTalentPoint` (Combat branch) | **PASS** |
| **8** | Community Bulletin | AC 40 / Spec 57 | `CommunityBulletinEngine` | `PostMessage`, `GetPosts` (Global notice) | **PASS** |
| **9** | Church Charity Tithes | AC 76 / Spec 39 | `ChurchCharityEngine` | `Donate` (15,000G), `GrantDivineBlessing` | **PASS** |
| **10** | Equipment Forge & Repair | AC 24 / Spec 40 | `EquipmentForgeEngine` | `ForgeUpgrade`, `ApplyCombatWear`, `RepairEquipment` | **PASS** |
| **11** | Gobang (Gomoku) Wager | AC 88 / Spec 41 | `GobangWagerEngine` | `CreateMatch` (2,000G wager), `PlaceStone` (7, 7) | **PASS** |
| **12** | Aquaculture Fishing | AC 90 / Spec 43 | `AquacultureFishingEngine` | `StartFishing`, `CatchFish` (Carp yield), `StopFishing` | **PASS** |
| **13** | Hot Spring Bathing | AC 44 / Spec 44 | `HotSpringBathingEngine` | `EnterHotSpring`, `PulseRestTick`, `ExitHotSpring` | **PASS** |
| **14** | Secondary PIN Vault | AC 73 / Spec 54 | `SecondaryPinVaultEngine` | `SetPin` ("8421"), `VerifyPin` authentication | **PASS** |
| **15** | Player Stall Bazaar | AC 53 / Spec 53 | `PlayerStallBazaarEngine` | `SpawnCustomStall` (Map 12005 @ 1450, 2200), `PurchaseItem` | **PASS** |
| **16** | Dynamic Weather & Light | AC 05:22 / Spec 29 | `DynamicWeatherLightingEngine`| `SetWeather` (Thunderstorm), `AdvanceTimeOfDay` (Dusk) | **PASS** |
| **17** | Vehicle Transport | AC 15 / Spec 28 | `VehicleTransportEngine` | `MountVehicle`, `ConsumeFuel`, `DismountVehicle` | **PASS** |
| **18** | ATM Municipal Banking | AC 26 / Spec 46 | `AtmBankingEngine` | `Deposit`, `Transfer` (1% fee wire), `Withdraw` | **PASS** |
| **19** | Player Prestige Titles | AC 40 / Spec 59 | `PlayerTitleAchievementEngine`| `UnlockTitle` (#3 Iron Bastion), `EquipTitle` | **PASS** |
| **20** | Treasure Excavation | AC 90 / Spec 83 | `TreasureExcavationEngine` | `PerformExcavation` (2.3m geological depth yield) | **PASS** |
| **21** | Carnival Lottery Wheel | AC 76 / Spec 85 & 98| `LotteryGachaEngine` | `SpinWheel` (12-slice wheel, progressive jackpot) | **PASS** |
| **22** | Tent Housekeeper Assistant| AC 62 / Spec 87 | `TentMaidButlerEngine` | `ConfigureAssistant` (Model 18), `TriggerAutoCook` | **PASS** |
| **23** | Combat Status Conditions | AC 114 / Spec 92 | `CombatStatusConditionEngine`| `AfflictStatus` (Sleep/Seal), `CanAct`, `Dispel` | **PASS** |

---

## 3. Custom Map & Coordinate Stall Spawning Architecture

### 3.1 Core Engine Enhancements (`PlayerStallBazaarEngine`)

1. **`SpawnCustomStall` Method:**
   ```csharp
   public PlayerStall SpawnCustomStall(uint vendorId, string vendorName, string stallTitle, ushort mapId, int x, int y, List<StallItem> items = null)
   ```
   - **Parameters:**
     - `vendorId`: Unique character ID for the kiosk vendor.
     - `vendorName`: Display name of the vendor displayed to clients.
     - `stallTitle`: Market kiosk title visible on hover or overworld click.
     - `mapId`: Target overworld map index (e.g. 10001 Kelan, 12005 Holy City).
     - `x`, `y`: Exact coordinate destination in the scene.
     - `items`: Optional catalog of `StallItem` objects. If null, automatically populates with authentic default merchandise (Healing Draughts, Vitality Elixirs, Meteoric Star Iron, Spacetime Warp Stones, Ancient Treasure Caskets).
   - **Return:** `PlayerStall` instance registered in the active stall dictionary.
   - **Thread Safety:** Synchronized with `lock (m_stallLock)`.

2. **`ClearAllStalls` Method:**
   ```csharp
   public void ClearAllStalls()
   ```
   - Clears all active overworld stalls immediately.

3. **`GetStall` Method:**
   ```csharp
   public PlayerStall GetStall(uint vendorCharId)
   ```
   - Lookup active stall by vendor character ID.

### 3.2 GUI Experimental Tab Sub-Tab 14 Controls

The Overworld Bazaar sub-tab in `Src/Gui/MainForm1.ExperimentalTab.cs` includes dedicated coordinate controls:
- **Map ID Input:** NumericUpDown (1 - 65,535, default: 10001).
- **Coord X Input:** NumericUpDown (0 - 10,000, default: 1200).
- **Coord Y Input:** NumericUpDown (0 - 10,000, default: 1500).
- **Title Input:** TextBox (default: "Rare Alchemy Supplies").
- **Vendor Input:** TextBox (default: "Bazaar Merchant").
- **Spawn Stall at Coords Button:** Instantiates and broadcasts kiosk to the selected map and coordinate position.
- **Clear All Stalls Button:** Resets all overworld vendor stalls.
- **DataGridView Table:** Real-time columns for `Vendor ID`, `Vendor`, `Stall Title`, `Map ID`, `Coord X`, `Coord Y`, `Catalog Size`, and `Opened At`.

### 3.3 In-Game Chat & Purchase Commands

- `:spawnstall <map_id> <x> <y> [title]` or `/spawnstall <map_id> <x> <y> [title]`: Spawns stall at specified map coordinates (GM only).
- `:stall [title]` or `/stall [title]`: Spawns stall at GM's current map and coordinates (GM only).
- `:clearstalls` or `/clearstalls`: Cleans up all active stalls on the server and removes their map NPCs (GM only).
- `:buystall <slot> [qty]` or `/buystall <slot> [qty]`: Purchases items from the closest vending stall on the current map (available to all players). If slot is omitted, lists available items and prices.

---

## 4. Dual-Entity Visual Stall Architecture & Client Rendering Fix

### 4.1 Invisibility Root Cause Analysis

When custom stalls were instantiated, they originally existed solely as in-memory state entries in `PlayerStallBazaarEngine.m_stalls`. Consequently:
1. `Map.NPCs` was unaware of the kiosk, omitting it from `Map.SendMapInfo` NPC broadcasts (`AC 22:4`).
2. The game client never received character avatar appearances (`AC 4`), equipment overlays (`AC 5:0`), or coordinate positioning (`AC 7`).
3. Floating vendor shop signboards (`AC 56:30`) and kiosk sitting stances (`AC 32:2`, `AC 5:16`) were never emitted.

### 4.2 Dual-Entity Architecture Implementation

To achieve 100% authentic visual rendering and physical click interaction across all client builds:
1. **Physical Map NPC Entity (`StallVendorNpc : QuestNpc`):**
   - Injected directly into target `GameMap.NPCs` with dynamic click ID ($\ge 200$) avoiding native map collisions.
   - Uses authentic Merchant NPC model (`TemplateID = 14134` Props Keep).
   - Set to stationary anchored AI (`WalkBehavior = 1`).
   - Dispatches `AC 22:4` map entity records during initial map load (`Map.SendMapInfo`) and live spawn broadcasts.
2. **Visual Actor & Signboard Entity:**
   - Emits `AC 4` avatar appearance with Earth Element, level 50 merchant model.
   - Emits `AC 5:0` worn gear state and `AC 7` world coordinate orientation.
   - Emits `AC 32:2` sitting stance and `AC 5:16` kiosk state flag.
   - Emits `AC 56:30` floating text signboard banner above the actor's head.
3. **Map Transition & Spawn Reload Resilience:**
   - `Map.SendMapInfo` calls `PlayerStallBazaarEngine.SyncMapStallsToPlayer` to replicate visual kiosks to players warping or logging in.
   - `GameMap.ReloadSpawns` invokes `PlayerStallBazaarEngine.ReattachStallsToMap` to restore custom stall NPCs if native maps reload.

### 4.3 Wire Protocol Packets

| Action Code | Sub-Opcode | Direction | Description | Payload Schema |
| :--- | :--- | :--- | :--- | :--- |
| `AC 4` | - | S $\to$ C | Actor Avatar Spawn | `[4] [CharID (4B)] [Body (1B)] [Element (1B)] [Level (1B)] [MapID (2B)] [X (2B)] [Y (2B)] ... [VendorName]` |
| `AC 5` | `0` | S $\to$ C | Actor Equipment | `[5, 0, CharID (4B), Equips]` |
| `AC 5` | `16` | S $\to$ C | Kiosk Mode Toggle | `[5, 16, CharID (4B), Active (1B)]` |
| `AC 7` | - | S $\to$ C | World Position | `[7, CharID (4B), MapID (2B), X (2B), Y (2B)]` |
| `AC 12` | - | S $\to$ C | Actor Despawn | `[12, CharID (4B)]` |
| `AC 20` | `14` (`0x0E`) | C $\to$ S | Browse Stall Request | `[20, 14, TargetMerchantCharID (4B)]` |
| `AC 20` | `15` (`0x0F`) | S $\to$ C | Stall Inventory Dialog | `[20, 15, VendorCharID (4B), SlotCount (1B), { 39-Byte Records }]` (Opens `Form_BuyItemStall_1`) |
| `AC 20` | `17` (`0x11`) | C $\to$ S | Purchase Item Request | `[20, 17, VendorCharID (4B), SlotIndex (1B), Quantity (1B)]` |
| `AC 20` | `18` (`0x12`) | S $\to$ C | Purchase Confirmation | `[20, 18, SuccessFlag (1B), SlotIndex (1B), RemainingQty (1B)]` |
| `AC 22` | `4` | S $\to$ C | Map NPC Spawn / State | `[22, 4, ClickID (2B), State (2B), X (2B), Y (2B), EntityType (1B), Duration (4B), Stance (1B)]` |
| `AC 32` | `2` | S $\to$ C | Sitting Emote Stance | `[32, 2, CharID (4B), 1 (1B)]` |
| `AC 56` | `3` | S $\to$ C | Alternative Stall Catalog | `[56, 3, VendorCharID (4B), StallName (string), Count (1B), {Slot, ItemID, Price, Count}]` |
| `AC 56` | `30` | S $\to$ C | Floating Stall Signboard | `[56, 30, CharID (4B), BannerString (string)]` (Empty string clears banner) |

