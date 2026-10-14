# Technical Specification: Wave 3 Experimental Gameplay Engines & Decompiled Systems Census

## Executive Summary
This document specifies the Wave 3 expansion of the Wonderland Private Server Experimental Framework. By analyzing remaining unfulfilled subsystems from the 100-document decompiled compendium, Wave 3 introduces 6 additional production-grade gameplay engines, expanding the total experimental framework to 23 comprehensive subsystems.

---

## 1. Engine Inventory & Decompiled Census Mapping

| Engine Class | Decompiled Origin | Action Code Protocol | Core Functionality |
| :--- | :--- | :--- | :--- |
| `AtmBankingEngine` | Spec 46 (`Form_ATM`) | `AC 26` (Sub 1, 2, 3, 4) | High-capacity personal vault storage, gold deposit/withdraw, inter-player wire transfers, wire fees, interest accrual. |
| `PlayerTitleAchievementEngine` | Spec 59 (`Form_Title`) | `AC 40` (Sub 6, 7, 8) | Milestone achievement tracking, prestige title inventory, passive attribute buffs (+ATK, +DEF, +SPD, +HP), public title banner display. |
| `TreasureExcavationEngine` | Spec 83 (`Form_Dig`) | `AC 90` / `AC 23:180` | Map spatial mining grid, geological ore veins (Copper, Silver, Gold, Crystal, Meteoric), shovel wear, buried treasure chest extraction. |
| `LotteryGachaEngine` | Spec 85 & 98 (`Form_Lottery`) | `AC 76` (Sub 10, 20, 30) | Carnival spinning wheel physics, progressive jackpot pool, lucky capsule gacha tiers (Common, Rare, Epic, Legendary), carnival ticket redemption. |
| `TentMaidButlerEngine` | Spec 87 (`Form_HouseKeep`) | `AC 62` (Sub 80, 81, 82) | Housing automation, maid/butler custom visual models, auto-cook food on low energy, automated cupboard craft processing, visitor welcome greeting. |
| `CombatStatusConditionEngine` | Spec 92 (`Battle_Buffs`) | `AC 114` & `AC 115` | Turn-based combat crowd control & buff system (Freeze, Petrify, Sleep, Confusion, Seal, Poison, Hot-Wheel, Earth Shield, Goddess Blessing) with turn decay & dispel. |

---

## 2. Detailed Technical Specifications

### 2.1 `AtmBankingEngine` (Spec 46 / AC 26)
* **Storage Model:** `ConcurrentDictionary<uint, BankAccount>` storing deposited gold up to 2,000,000,000G (2 Billion Gold), exceeding standard character inventory limits.
* **Operations:**
  * `Deposit(Player player, uint amount)`: Transfers gold from player wallet to bank vault.
  * `Withdraw(Player player, uint amount)`: Returns gold from vault to player wallet.
  * `Transfer(Player sender, uint targetCharId, uint amount)`: Executes inter-player bank wire with 1% municipal tariff fee.
* **Network Packets:**
  * `S->C AC 26:1`: Vault balance synchronization (`Pack32(balance)`).
  * `S->C AC 26:2`: Deposit acknowledgement.
  * `S->C AC 26:3`: Withdrawal acknowledgement.
  * `S->C AC 26:4`: Transfer receipt.

### 2.2 `PlayerTitleAchievementEngine` (Spec 59 / AC 40)
* **Achievement Taxonomy:**
  * `Grand Adventurer`: Reached Level 100 (+15 SPD).
  * `Monster Scourge`: Slain 1,000 beasts (+25 ATK).
  * `Iron Wall`: Mastered defense and blocking (+30 DEF).
  * `Bazaar Tycoon`: Accumulate 5,000,000G in commerce (+500 Max HP).
  * `Master Angler`: Landed 50 rare aquatic catches (+10 ALL STATS).
* **Network Packets:**
  * `S->C AC 40:6`: Unlock Title notification.
  * `S->C AC 40:7`: Equip Title packet broadcasted to map peers (`Pack32(charId)`, `Pack16(titleId)`).

### 2.3 `TreasureExcavationEngine` (Spec 83 / AC 90)
* **Geological Mechanics:**
  * Players equip mining pickaxes or excavation shovels (`ItemId 26301`).
  * Maps contain dynamic underground deposit nodes with varying hardness and depth.
  * Yields raw ores (Iron, Copper, Gold, Titanium, Agate, Meteorite Fragments) and buried antiquity relics.
* **Network Packets:**
  * `S->C AC 90:10`: Excavation animation initiated.
  * `S->C AC 90:11`: Geological find extracted with quantity and rarity.

### 2.4 `LotteryGachaEngine` (Spec 85 & 98 / AC 76)
* **RNG Distribution:**
  * Wheel contains 12 slices: Gold Pouch (30%), Consumable Elixir (25%), Crafting Catalyst (20%), Rare Gem (15%), Mount Saddle (8%), Jackpot Ticket (2%).
  * Capsule Toy Gacha dispenses collectible mini-dolls, companion accessories, and fashion tints.
* **Network Packets:**
  * `S->C AC 76:10`: Wheel spin outcome slice index and awarded prize item.
  * `S->C AC 76:20`: Gacha capsule reveal animation and item payload.

### 2.5 `TentMaidButlerEngine` (Spec 87 / AC 62)
* **Automation Routine:**
  * `HouseholdAssistant` entity bound to tent map.
  * Auto-cooks meals using available ingredients in tent cupboard inventory when owner HP/SP drops below 30%.
  * Greets entering visitors with customizable dialogue string.
* **Network Packets:**
  * `S->C AC 62:80`: Housekeeper configuration synchronized.
  * `S->C AC 62:81`: Autonomous maintenance event triggered.

### 2.6 `CombatStatusConditionEngine` (Spec 92 / AC 114 & 115)
* **Condition Matrix:**
  * `Freeze`: Target cannot act for 2-3 turns; takes 1.5x Earth damage.
  * `Petrify`: Target turns to stone for 2-4 turns; defense +100%, cannot move or evade.
  * `Sleep`: Target disabled; awakens immediately upon receiving physical damage.
  * `Poison`: Target loses 10% max HP at start of each turn for 3 turns.
  * `EarthWall`: Absorbs next incoming physical/magical blow up to threshold.
* **Network Packets:**
  * `S->C AC 114:1`: Afflict status condition (`Pack32(fighterId)`, `Pack8(statusType)`, `Pack8(duration)`).
  * `S->C AC 114:2`: Status decay tick / Cleanse dispel.
