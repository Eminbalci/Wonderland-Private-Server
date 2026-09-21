# Database Schema and Persistence Lifecycle

## 1. Architectural Overview

The persistence tier in Wonderland Online Private Server is managed by an enterprise dual-database engine supporting both embedded SQLite (`Data/ServerDataBase.db`) and production MySQL / MariaDB instances. Database interactions are encapsulated within [`RCLibrary.Core.DataBase`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/RCLibrary.Core/DataBase.cs) and specialized subsystem databases in [`wlo.pserver.core/DataBase`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase).

---

## 2. Dual Database Engine & Dialect Translation

The engine provides transparent SQL dialect translation via `TranslateSqlForMySql` in [`DataBase.cs`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/RCLibrary.Core/DataBase.cs):

```
+-------------------------------------------+-------------------------------------------------------+
| SQLite Expression                         | MySQL / MariaDB Translated Translation                |
+-------------------------------------------+-------------------------------------------------------+
| INTEGER PRIMARY KEY AUTOINCREMENT         | INT NOT NULL AUTO_INCREMENT PRIMARY KEY               |
| \bAUTOINCREMENT\b                         | AUTO_INCREMENT                                        |
| INTEGER PRIMARY KEY                       | INT PRIMARY KEY                                       |
| TEXT PRIMARY KEY                          | VARCHAR(255) PRIMARY KEY (Avoids MySQL Error 1170)    |
| INSERT OR REPLACE INTO                    | REPLACE INTO                                          |
| ON CONFLICT (...) DO UPDATE SET           | ON DUPLICATE KEY UPDATE                               |
| BEGIN TRANSACTION                         | START TRANSACTION;                                    |
| SELECT * FROM sqlite_master               | SELECT table_name AS name FROM information_schema...  |
| PRAGMA table_info('table')                | SELECT COLUMN_NAME FROM information_schema.COLUMNS... |
+-------------------------------------------+-------------------------------------------------------+
```

### 2.1 Self-Healing Schema Migration

To prevent startup crashes caused by altered table structures across versions, the engine implements automated incremental migration:
* `GetColumnNames(string tableName)`: Queries `PRAGMA table_info` (SQLite) or `information_schema.COLUMNS` (MySQL), returning a `HashSet<string>` with case-insensitive ordinal comparison (`StringComparer.OrdinalIgnoreCase`).
* `AddColumnIfNotExists(string tableName, string columnName, string columnDefinition)`: Verifies column existence before executing `ALTER TABLE ADD COLUMN`, completely preventing duplicate column exceptions.
* `VerifySetup()`: Executes boot-time DDL validation across all subsystem database classes before client network listeners open.

### 2.2 SQLite-to-MySQL Data Migration Engine

A built-in migration pipeline facilitates one-click data migration from embedded SQLite to remote MySQL with real-time UI progress updates:
1. Reads table DDL schemas from SQLite and translates syntax via `TranslateSqlForMySql`.
2. Creates destination tables on MySQL if missing.
3. Batches records using parameterized `DbParam` collections to prevent SQL injection.
4. Preserves primary key sequences and relational integrity.

---

## 3. Relational Table Schemas

### 3.1 `users`
Persists master account credentials and security roles:
```sql
CREATE TABLE users (
  userID INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  username VARCHAR(64) NOT NULL UNIQUE,
  password VARCHAR(128) NOT NULL,
  email VARCHAR(128) DEFAULT '',
  banned TINYINT DEFAULT 0,
  im_bonus INT DEFAULT 0,
  im_points INT DEFAULT 0,
  gm_level TINYINT DEFAULT 0,
  character1_id INT DEFAULT 0,
  character2_id INT DEFAULT 0
);
```

### 3.2 `characters`
Persists player entity overworld positions, visual customizations, and core statistics:
```sql
CREATE TABLE characters (
  charID INT NOT NULL PRIMARY KEY,
  slot TINYINT NOT NULL DEFAULT 1,
  head SMALLINT NOT NULL DEFAULT 1,
  body SMALLINT NOT NULL DEFAULT 1,
  name VARCHAR(32) NOT NULL,
  name_clean VARCHAR(32) NOT NULL,
  nickname VARCHAR(32) DEFAULT '',
  location_map INT NOT NULL DEFAULT 10035,
  location_x SMALLINT NOT NULL DEFAULT 1038,
  location_y SMALLINT NOT NULL DEFAULT 2235,
  haircolor INT DEFAULT 0,
  skincolor INT DEFAULT 0,
  clothingcolor INT DEFAULT 0,
  eyecolor INT DEFAULT 0,
  gold BIGINT NOT NULL DEFAULT 0,
  element TINYINT NOT NULL DEFAULT 0,
  rebirth TINYINT NOT NULL DEFAULT 0,
  job TINYINT NOT NULL DEFAULT 0,
  online TINYINT DEFAULT 0,
  cipher VARCHAR(64) DEFAULT ''
);
```

### 3.3 `inventory`
Persists player bags, equipped gear, and tent storage containers:
```sql
CREATE TABLE inventory (
  pri_key INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  invIdx INT NOT NULL,
  charID INT NOT NULL,
  storID TINYINT NOT NULL DEFAULT 0, -- 0=Bag, 1=Equipped, 2=Tent Storage
  itemID INT NOT NULL,
  dmg SMALLINT NOT NULL DEFAULT 0,
  qty SMALLINT NOT NULL DEFAULT 1,
  pos SMALLINT NOT NULL DEFAULT 0,
  socketID INT DEFAULT 0,
  bombID INT DEFAULT 0,
  sewID INT DEFAULT 0,
  forge SMALLINT DEFAULT 0,
  INDEX idx_char_stor (charID, storID)
);
```

### 3.4 `character_pets`
Persists companion rosters, active battle state, skills, amity, and equipment:
```sql
CREATE TABLE character_pets (
  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  charID INT NOT NULL,
  slot TINYINT NOT NULL, -- 1..4
  petID INT NOT NULL,
  petName VARCHAR(32) NOT NULL,
  level SMALLINT NOT NULL DEFAULT 1,
  exp BIGINT NOT NULL DEFAULT 0,
  hp INT NOT NULL DEFAULT 100,
  maxHp INT NOT NULL DEFAULT 100,
  sp INT NOT NULL DEFAULT 50,
  maxSp INT NOT NULL DEFAULT 50,
  str SMALLINT NOT NULL DEFAULT 0,
  con SMALLINT NOT NULL DEFAULT 0,
  int_ SMALLINT NOT NULL DEFAULT 0,
  wis SMALLINT NOT NULL DEFAULT 0,
  agi SMALLINT NOT NULL DEFAULT 0,
  potential SMALLINT NOT NULL DEFAULT 0,
  skillPoints SMALLINT NOT NULL DEFAULT 0,
  amity SMALLINT NOT NULL DEFAULT 100,
  isBattle TINYINT NOT NULL DEFAULT 0,
  isRide TINYINT NOT NULL DEFAULT 0,
  isHotel TINYINT NOT NULL DEFAULT 0,
  reborn TINYINT NOT NULL DEFAULT 0,
  job TINYINT NOT NULL DEFAULT 0,
  eq_head INT DEFAULT 0,
  eq_body INT DEFAULT 0,
  eq_weapon INT DEFAULT 0,
  eq_wrist INT DEFAULT 0,
  eq_shoes INT DEFAULT 0,
  eq_special INT DEFAULT 0,
  INDEX idx_char_pet (charID, slot)
);
```

### 3.5 `charquest` (Alias: `character_quests`)
Persists player quest states and progression steps:
```sql
CREATE TABLE charquest (
  pri_key INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  charID INT NOT NULL,
  quest_started INT NOT NULL, -- Quest ID
  quest_pos TINYINT NOT NULL DEFAULT 1, -- 1=InProgress, 2=NotStarted, 3=Completed
  step SMALLINT NOT NULL DEFAULT 0,
  kill_count SMALLINT NOT NULL DEFAULT 0,
  completed_at DATETIME NULL,
  INDEX idx_quest_lookup (charID, quest_started)
);
```

### 3.6 `game_quests`
Metadata and dialogue scripts for server quests, populated from `Data/Mark.dat`:
```sql
CREATE TABLE game_quests (
  quest_id INT NOT NULL PRIMARY KEY,
  name VARCHAR(128) NOT NULL,
  npc_name_pattern VARCHAR(64) DEFAULT '',
  npc_template_id INT DEFAULT 0,
  map_id INT NOT NULL,
  type VARCHAR(32) DEFAULT 'Normal',
  description TEXT,
  intro_dialogue TEXT,
  in_progress_dialogue TEXT,
  complete_dialogue TEXT,
  already_completed_dialogue TEXT,
  battle_monster_id INT DEFAULT 0,
  battle_monster_name VARCHAR(64) DEFAULT '',
  reward_gold INT DEFAULT 0,
  reward_exp INT DEFAULT 0,
  reward_companion_id INT DEFAULT 0,
  reward_companion_name VARCHAR(64) DEFAULT '',
  reward_items TEXT,
  required_items TEXT,
  prerequisite_quests TEXT,
  steps_json TEXT
);
```

### 3.7 `server_settings`
Key-value store for global configurations, MOTD, and rate multipliers:
```sql
CREATE TABLE server_settings (
  key VARCHAR(64) NOT NULL PRIMARY KEY,
  value TEXT NOT NULL
);
```

### 3.8 `npc_data`
In-memory cache template mirror for 4,928 monsters and NPCs loaded from `Data/Npc.dat`:
```sql
CREATE TABLE npc_data (
  id INT NOT NULL PRIMARY KEY,
  name VARCHAR(64) NOT NULL,
  level SMALLINT NOT NULL,
  hp INT NOT NULL,
  element TINYINT NOT NULL
);
```

### 3.9 Additional Subsystem Tables
* `npcs`: Map placement entries (`npc_id`, `map_id`, `click_id`, `template_id`, `npc_type`, `npc_name`, `x`, `y`).
* `charactersextdata`: Serialized JSON blobs for client settings, friends, guilds, and offline mailbox.
* `chartent`: Tent configurations (`charID`, `locked`, `enlarged`, `tenttype`, `floor1Color`, `floor1wallpaper`, `floor2Color`, `floor2wallpaper`).
* `chartent_items`: Furniture coordinates and rotation inside tents (`pri_key`, `charID`, `itemID`, `posX`, `posY`, `floor`, `rotate`).
* `charunlocks`: Recorded landmark and chest discoveries (`charID`, `maploc`, `clickID`).
* `stats`: Character allocated status attributes and potential points.
* `portals`: Portal boundaries and target map linkages (`mapID`, `portalID`, `destID`).
* `warp_destinations`: Portal warp target coordinates (`mapID`, `destID`, `dstMap`, `dstX`, `dstY`).
* `Friends`: Bilateral friendship pairings (`CharID1`, `CharID2`, `AddedDate`).
* `gm_accounts`: Authorized GM credentials.
* `player_settings`: Player UI toggles (`pk_mode`, `join_mode`, `trade_mode`).
* `banned_ips` & `banned_users`: IP and account disciplinary blacklists.
* `item_mall`: Catalog microtransaction listings (`item_id`, `item_name`, `category`, `point_cost`, `count`, `is_hot`, `badge`).

---

## 4. Transaction Lifecycle & Persistence Threading

1. **Auto-Save Loop:** The `AutoSaveThread` in [`WorldServer.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Server/WorldServer.cs) runs continuously on a 1,000ms tick, invoking `player.SaveCharacterData()` on dirty entities.
2. **Atomic Character Save (`SaveCharacterData`):**
   - Commits current position (`location_map`, `location_x`, `location_y`) and current gold balance.
   - Saves inventory items across storID 0 (bag) and storID 1 (gear).
   - Flushes companion HP/SP, experience points, and amity levels.
   - Flushes quest state transitions.
3. **Thread Safety:** Database connection instances utilize synchronization locks (`lock (mlock)`) to guarantee query isolation during concurrent socket executions.

---

## 5. Character Loading & Equipment Lifecycle

### 5.1 Character Retrieval (`CharacterDataBase.GetCharacterData`)

When a user logs in via `AC 63`, the server retrieves character data using `GetCharacterData(uint charID)`:
1. **Cache Verification:** Checks the in-memory `ConcurrentDictionary<int, Character> Cache`. If present, returns the cached entity immediately.
2. **Entity Instantiation:** Instantiates `Character` with defensive item manager resolution:
   - Primary: `CharacterDataBase.ItemDat`
   - Secondary: `CharacterDataBase.GlobalInstance?.ItemDat`
   - Fallback: `GameDataBase.GlobalInstance?.ItemDat`
3. **Core Attributes:** Queries `characters` table for slot, body style, head style, map location, coordinates, colors, and gold.
4. **Stat Allocation:** Queries `stats` table for HP, SP, base attributes (Str, Con, Agi, Int, Wis), Total EXP, and skill points.
5. **Equipment & Beginner Outfit Fallback:**
   - Queries `inventory` table for `storID = 1` (equipped items).
   - If equipped items are present in rows 1..6, resolves `PhxItemInfo` from `ItemDat` and copies attributes.
   - If no equipped rows exist (e.g., brand-new character), calls `t.SetBeginnerOutfit()`.

### 5.2 Beginner Outfit Resolution (`EquipManager.SetBeginnerOutfit`)

`SetBeginnerOutfit()` equips default starter gear tailored to the character's `BodyStyle` and `Head` hairstyle:
* **Item Resolution:** Invokes `WearBeginnerItem(ushort itemId)`. If `ItemManager` or `Item.dat` lookup fails, synthesizes fallback item metadata with appropriate `Equippos` slot boundaries (Head = 1, Body = 2, Feet = 5, Weapon = 6).
* **Socket Safety:** All socket notifications inside `EquipManager` (`SendStat`, `SendExp`, `SendGold`, `SetBreillatOutfit`) use safe invocations (`Send?.Invoke(...)`), preventing `NullReferenceException` when characters are loaded offline or during pre-connection states.

