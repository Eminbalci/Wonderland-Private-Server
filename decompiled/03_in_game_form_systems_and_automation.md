# In-Game Form Systems and Automation Specification

## 1. Overview of In-Game Form Architecture

The client engine manages 81 distinct in-game forms dynamically invoked through the master form dispatcher `FUN_001d6294` / `ShowForm`. Forms manage all interactive user interfaces including automated combat, item compounding, player stalls, banking, tent customization, and progression trees.

---

## 2. Categorized Form Reference

### 2.1 Automation & Botting Systems
* **`Form_AutoPlayerPlus_1` (Engine Handler: `FUN_001d6294`):**
  * Built-in client-side combat botting engine.
  * Allows players to configure automatic HP recovery thresholds (item drinking at $< X\%$ HP), SP recovery thresholds, and automatic skill activation priority.
* **`Form_AutoPlayerPlus_2`:**
  * Extended companion pet automation: pet HP/SP management and battle actions.
* **`Form_ExpBox`:**
  * Experience booster box interface; manages time-limited EXP multipliers.
* **`Form_Magnet_1`:**
  * Automated resource collection magnet gadget.

### 2.2 Economy, Trade & Vending
* **`Form_ATM_List` (Engine Handler: `FUN_0021b57c`):**
  * In-game banking terminal. Handles gold deposits, withdrawals, and balance inquiries.
* **`Form_BuyItemStall_1`:**
  * Player vending stall for listing items for sale in designated commercial zones.
* **`Form_BuyPet1Stall_1`, `Form_BuyPet2Stall_1`, `Form_BuyPet3Stall_1`:**
  * Companion pet vending stalls, supporting up to 3 concurrent pet sale listings.
* **`Form_ThrowMoney`:**
  * Interactive interface for dropping or throwing gold coins onto the ground.
* **`Form_ExchangeLockChang`, `Form_InputPw_1`, `Form_InputPw_2`:**
  * Secondary safe lock password setup and PIN change dialogs.
* **`Form_ThingStore_1` & `Form_EquipStore_1`:**
  * NPC storage vaults for normal inventory items and equipment.

### 2.3 Housing, Crafting & Tent Manufacturing
* **`Form_Compound_1` (Engine Handler: `FUN_00235340`):**
  * Alchemy compounding system. Evaluates item material tiers (Grass, Flower, Wood, Stone, Diamond, etc.) and calculates synthesis outcomes.
* **`Form_MakeThing_1` & `Form_blueprint_1` (Engine Handler: `FUN_001f62d4`):**
  * Tent manufacturing machines and architectural blueprint crafting recipes.
* **`Form_HomeAdorn_1`:**
  * Interior tent furniture layout, rotation, and placement coordinator.
* **`Form_Cupboard_1` & `Form_Cupboard_2`:**
  * Storage cabinet containers placed inside player tents.

### 2.4 Social, Guilds & Mail
* **`Form_ArmyGrounp_1` & `Form_ArmyGrounpInfo_1`:**
  * Guild / Army organization roster, member ranks, and emblem displays.
* **`Form_OrgNotice`:**
  * Organization message of the day (MOTD) and guild bulletin.
* **`Form_ChooseCooperation` & `Form_CreateCooperation`:**
  * Multi-guild alliance management and shared war pacts.
* **`form_Mail` & `form_Mailplus`:**
  * Postal delivery system supporting textual letters and parcel item attachments.
* **`Form_BlackBoard_1` & `Form_BlackBoard_2`:**
  * Town bulletin blackboard where players post public notices.

### 2.5 Combat, Minigames & Quest Progression
* **`Form_BattleSkill_1`:**
  * Turn-based combat skill selection and battle targeting grid.
* **`Form_SkillTree` & `form_npcSkillTree`:**
  * Player elemental progression tree and companion pet ability unlocks.
* **`form_NPCUpgrade`:**
  * Pet rebirth, stat transcendence, and companion upgrade interface.
* **`Form_CreateDupMis` (Engine Handler: `FUN_0019de44`):**
  * Duplicate Mission / Instance dungeon generation and party queuing.
* **`Form_EightTwelve` (Engine Handler: `FUN_001aed4c`):**
  * 8-12 Dice minigame for wagers and rewards.
* **`Form_Ante`:**
  * Combat arena betting and spectator wagering.
* **`Form_Capsulem` (Engine Handler: `FUN_0022bb00`):**
  * Gacha capsule toy vending machine.
* **`form_taskview_1`:**
  * Quest journal and task progress viewer.
* **`Form_setauthority_1`:**
  * Runtime GM permission and authority tier editor.
