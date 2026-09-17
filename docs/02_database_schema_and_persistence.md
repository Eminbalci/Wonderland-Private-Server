# 02 - Database Schema and Persistence Specification

This technical specification details the database engine, relational schema catalog, boot-time verification and self-healing mechanisms, and transactional persistence lifecycles used by the **Wonderland Online Private Server**.

---

## 1. Database Architecture

The persistence layer is implemented over SQLite, utilizing a single consolidated database file located at:
```
<BaseDirectory>/Data/ServerDataBase.db
```

The database is managed through three primary service classes in [`wlo.pserver.core/DataBase/`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/):
1. **`GameDataBase`**: Oversees global tables (Item Mall, chests, GM accounts, server settings) and orchestrates initial schema self-healing verification.
2. **`UserDataBase`**: Manages account-level authentication, account passwords, access ciphers, and GM permission levels.
3. **`CharacterDataBase`**: Manages character profiles, inventory bag slots, equipped items, companion and pet collections, active and completed quest states, and friendship rosters.

---

## 2. Boot-Time Self-Healing & Migration Engine

Upon server boot, `GameDataBase.VerifySetup()` is invoked to guarantee schema integrity before any network listener binds to ports.

### Automated Schema Verification (`VerifySetup`)
1. **Table Creation**: Issues `CREATE TABLE IF NOT EXISTS` for all missing relational tables.
2. **Index Enforcement**: Verifies and constructs unique indexes (e.g., `idx_char_id`, `idx_acc_name`).
3. **Column Migration**: Queries `PRAGMA table_info(<table_name>)` in memory to inspect existing column sets. If an upgraded column is absent, an atomic `ALTER TABLE ADD COLUMN` is applied.
4. **Pet Table Migration Optimization**: Table `character_pets` verification is centralized in `VerifyCharacterPetsTable()` and executed strictly once during startup, eliminating repetitive per-query DDL overhead during runtime character saves and loads.
5. **Default Seeder**: Automatically creates baseline administrative accounts if absent:
   - `admin` (Password: `password`, GM Level: 10)
   - `developer` (Password: `password`, GM Level: 10)

---

## 3. Relational Schema Catalog

### 3.1. `users` Table
Stores master account credentials and character slot mappings.

| Column | Type | Constraints | Description |
| :--- | :--- | :--- | :--- |
| `id` | `INTEGER` | `PRIMARY KEY AUTOINCREMENT` | Internal unique account ID. |
| `username` | `TEXT` | `UNIQUE NOT NULL` | Login account name. |
| `password` | `TEXT` | `NOT NULL` | Account password string. |
| `cipher` | `TEXT` | `NULL` | Secondary security cipher string. |
| `banned` | `INTEGER` | `DEFAULT 0` | Account suspension flag (`0 = Active`, `1 = Banned`). |
| `gm_level` | `INTEGER` | `DEFAULT 0` | Administrative authorization level (`0 = Player`, `10 = SuperGM`). |
| `character1_id` | `INTEGER` | `DEFAULT 0` | ID of character assigned to Slot 1. |
| `character2_id` | `INTEGER` | `DEFAULT 0` | ID of character assigned to Slot 2. |
| `im_points` | `INTEGER` | `DEFAULT 0` | Item Mall currency balance. |
| `im_bonus` | `INTEGER` | `DEFAULT 0` | Promotional bonus Item Mall currency. |

### 3.2. `characters` Table
Stores player core stats, appearance, overworld position, and attributes.

| Column | Type | Constraints | Description |
| :--- | :--- | :--- | :--- |
| `id` | `INTEGER` | `PRIMARY KEY AUTOINCREMENT` | Canonical Character ID (`CharID`). |
| `user_id` | `INTEGER` | `NOT NULL` | Associated master account foreign key. |
| `name` | `TEXT` | `UNIQUE NOT NULL` | In-game character name. |
| `nickname` | `TEXT` | `DEFAULT ''` | In-game character title / nickname. |
| `level` | `INTEGER` | `DEFAULT 1` | Character level (1 - 200). |
| `exp` | `INTEGER` | `DEFAULT 0` | Total accumulated experience points. |
| `element` | `INTEGER` | `DEFAULT 0` | Affinity (`0 = Earth`, `1 = Water`, `2 = Fire`, `3 = Wind`). |
| `gold` | `INTEGER` | `DEFAULT 0` | Current carried gold coins. |
| `hp` / `max_hp` | `INTEGER` | `DEFAULT 100` | Current and maximum health points. |
| `sp` / `max_sp` | `INTEGER` | `DEFAULT 100` | Current and maximum skill/mana points. |
| `str`, `con`, `int`, `wis`, `agi` | `INTEGER` | `DEFAULT 0` | Base distributed primary attributes. |
| `map_id` | `INTEGER` | `DEFAULT 10017` | Current map identifier. |
| `x_axis`, `y_axis` | `INTEGER` | `DEFAULT 0` | Overworld tile coordinates. |
| `body`, `head` | `INTEGER` | `DEFAULT 0` | Avatar body type and head model IDs. |
| `hair_color`, `skin_color` | `INTEGER` | `DEFAULT 0` | Color palette identifiers. |
| `clothing_color`, `eye_color` | `INTEGER` | `DEFAULT 0` | Color palette identifiers. |
| `reborn` | `INTEGER` | `DEFAULT 0` | Rebirth status flag (`0 = Normal`, `1 = Reborn`). |
| `job` | `INTEGER` | `DEFAULT 0` | Class / Job specialization ID. |

### 3.3. `character_inventory` Table
Maintains character bag contents across all 50 slots (1x1 grid).

| Column | Type | Constraints | Description |
| :--- | :--- | :--- | :--- |
| `character_id` | `INTEGER` | `NOT NULL` | Owner Character ID. |
| `slot` | `INTEGER` | `NOT NULL` | Bag slot index (`1 - 50`). |
| `item_id` | `INTEGER` | `NOT NULL` | Canonical item ID from `Item.dat`. |
| `count` | `INTEGER` | `DEFAULT 1` | Stack count. |
| `durability` | `INTEGER` | `DEFAULT 100` | Current item durability points. |
| `max_durability` | `INTEGER` | `DEFAULT 100` | Maximum item durability points. |
| `damage` | `INTEGER` | `DEFAULT 0` | Weapon/gear attack bonus modifier. |

### 3.4. `character_equipment` Table
Tracks worn equipment slots on the character model.

| Column | Type | Constraints | Description |
| :--- | :--- | :--- | :--- |
| `character_id` | `INTEGER` | `NOT NULL` | Owner Character ID. |
| `slot` | `INTEGER` | `NOT NULL` | Equip Position (`1 = Head`, `2 = Body`, `3 = Arms`, `4 = Legs`, `5 = Weapon`, `6 = Acc`). |
| `item_id` | `INTEGER` | `NOT NULL` | Canonical item ID. |
| `durability` | `INTEGER` | `DEFAULT 100` | Remaining durability. |

### 3.5. `character_pets` Table
Stores companions and wild-captured pets.

| Column | Type | Constraints | Description |
| :--- | :--- | :--- | :--- |
| `character_id` | `INTEGER` | `NOT NULL` | Owner Character ID. |
| `slot` | `INTEGER` | `NOT NULL` | Pet roster index (`1 - 4`). |
| `pet_id` | `INTEGER` | `NOT NULL` | Canonical pet/companion NPC ID from `Npc.dat`. |
| `name` | `TEXT` | `NOT NULL` | Pet name string. |
| `level` | `INTEGER` | `DEFAULT 1` | Pet current level. |
| `exp` | `INTEGER` | `DEFAULT 0` | Pet accumulated experience. |
| `hp` / `sp` | `INTEGER` | `DEFAULT 100` | Pet current HP and SP. |
| `intimacy` | `INTEGER` | `DEFAULT 50` | Pet loyalty/intimacy score (`0 - 100`). |
| `in_battle` | `INTEGER` | `DEFAULT 0` | Active combat participation flag (`0 = Resting`, `1 = Active`). |
| `is_riding` | `INTEGER` | `DEFAULT 0` | Overworld mount flag. |

### 3.6. `character_quests` Table
Persists quest progress and completion locks.

| Column | Type | Constraints | Description |
| :--- | :--- | :--- | :--- |
| `character_id` | `INTEGER` | `NOT NULL` | Owner Character ID. |
| `quest_id` | `INTEGER` | `NOT NULL` | Canonical Quest ID from `QuestDefinition`. |
| `step` | `INTEGER` | `DEFAULT 0` | Current active stage/step index. |
| `is_completed` | `INTEGER` | `DEFAULT 0` | Completion status (`0 = In Progress`, `1 = Finished`). |
| `completed_at` | `TEXT` | `NULL` | Completion ISO timestamp. |

---

## 4. Transactional Persistence Lifecycle

Player state changes are committed using transactional guarantees:

1. **Periodic Auto-Save**: Background worker writes dirty player state every 180 seconds.
2. **Zone Transition**: Saving coordinates, HP/SP, and gold prior to executing map warp (`onTeleport`).
3. **Player Disconnect**: Synchronously writing full character profiles, items, pets, and quests when sockets terminate.
4. **Crash Protection**: Critical operations (e.g. Item Mall purchases, trade exchange, rebirth) execute within explicit SQLite database transactions (`BEGIN TRANSACTION ... COMMIT`), guaranteeing atomicity.
