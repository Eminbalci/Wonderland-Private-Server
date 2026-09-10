# 02 - Database Persistence and Schema Specification

## 1. Storage Architecture Overview

All persistent server state is consolidated into a single, high-performance SQLite database:
- Canonical Path: [`Data/ServerDataBase.db`](file:///D:/GitHub/Wonderland-Private-Server/Data/ServerDataBase.db)
- Runtime Mirror: [`bin/Debug/ServerDataBase.db`](file:///D:/GitHub/Wonderland-Private-Server/bin/Debug/ServerDataBase.db)

Access is managed through [`RCLibrary.Core.DataBase`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/RCLibrary.Core/DataBase.cs) using thread-safe synchronization locks and parameterized SQL commands.

---

## 2. Complete 32-Table Schema Catalog

### Core Account & Character Tables
| Table | Description | Primary Key / Indexes | Key Columns |
| :--- | :--- | :--- | :--- |
| `users` | User accounts and credentials | `userID PRIMARY KEY` | `username`, `password`, `im_points`, `im_bonus`, `ip`, `is_gm`, `created_at` |
| `characters` | Character identities | `charID PRIMARY KEY` | `userID`, `char_name`, `slot`, `gender`, `element`, `mapID`, `x`, `y`, `level`, `gold` |
| `charactersextdata` | Extended attributes & appearance | `charID PRIMARY KEY` | `hair_color`, `skin_color`, `head_style`, `rebirth_type`, `trans_flag` |
| `stats` | Character attributes & point pool | `charID PRIMARY KEY` | `str`, `con`, `int_stat`, `wis`, `agi`, `hp`, `sp`, `unallocated_points` |
| `inventory` | Equipped items & bags | `id PRIMARY KEY` | `charID`, `slot`, `item_id`, `count`, `damage`, `created_at` |
| `character_skills` | Unlocked skills & proficiency | `(charID, skill_id) PK` | `skill_id`, `skill_grade`, `proficiency` |
| `character_pets` | Active pets & companions | `(charID, slot) PK` | `pet_id`, `level`, `hp`, `sp`, `amity`, `is_battle`, `is_hotel` |
| `chartent` | Player housing tents | `charID PRIMARY KEY` | `tent_id`, `floor_style`, `wall_style`, `is_open`, `mapID`, `x`, `y` |
| `chartent_items` | Furniture & appliances in tent | `id PRIMARY KEY` | `charID`, `item_id`, `x`, `y`, `rotation`, `data` |
| `charquest` | Quest progress & step tracking | `(charID, quest_started) UNIQUE` | `charID`, `quest_started`, `quest_step`, `completed` |
| `charunlocks` | Unlocked milestones / cutscenes | `(charID, unlock_id) PK` | `unlock_id`, `unlocked_at` |
| `Friends` | Friend relationship graph | `(CharID1, CharID2) PK` | `CharID1`, `CharID2`, `AddedDate` |
| `player_settings` | Per-character interaction toggles | `char_id PRIMARY KEY` | `pk_mode` (0/1), `join_mode` (0/1), `trade_mode` (0/1) |

### World & Static Content Tables
| Table | Description | Primary Key / Indexes | Key Columns |
| :--- | :--- | :--- | :--- |
| `game_quests` | Static quest definitions | `quest_id PRIMARY KEY` | `quest_name`, `npc_pattern`, `npc_template_id`, `map_id`, `reward_exp`, `reward_gold` |
| `npc_data` | Decoded NPC templates (Npc.dat) | `template_id PRIMARY KEY` | `name`, `level`, `hp`, `sp`, `element`, `atk`, `def`, `matk`, `mdef`, `spd` |
| `npcs` | Dynamic overworld spawns | `npc_id PRIMARY KEY` | `map_id`, `click_id`, `template_id`, `npc_type`, `npc_name`, `x`, `y` |
| `portals` | Map warp triggers | `portal_id PRIMARY KEY` | `map_id`, `x`, `y`, `dest_map_id`, `dest_x`, `dest_y`, `req_level` |
| `warp_destinations` | Teleportation points | `dest_id PRIMARY KEY` | `label`, `map_id`, `x`, `y` |

### Server Subsystem & Economy Tables
| Table | Description | Primary Key / Indexes | Key Columns |
| :--- | :--- | :--- | :--- |
| `server_settings` | Dynamic server configuration | `key PRIMARY KEY` | `CLUSTER`, `SERVER_ID`, `MODE`, `EXP_RATE`, `DROP_RATE`, `GOLD_RATE`, `MAX_PLAYERS` |
| `gm_accounts` | GM authorized accounts | `username UNIQUE` | `id PK`, `username`, `gm_level` (1-10), `notes` |
| `item_mall` | Authentic Item Mall catalog | `id PK` | `item_id`, `item_name`, `category`, `category_id`, `point_cost`, `original_price`, `gold_cost`, `count`, `is_hot`, `is_new`, `is_limited`, `on_sale`, `discount`, `badge`, `order_idx`, `is_bonus`, `subcategory_id` |
| `starter_items` | New player welcome package | `id PK` | `order_idx`, `item_id`, `item_name`, `count`, `description` |
| `guilds` | Player guilds | `guild_id PRIMARY KEY` | `guild_name`, `leader_char_id`, `leader_name`, `notice`, `created_date` |
| `guild_members` | Guild membership rosters | `(guild_id, char_id) PK` | `guild_id`, `char_id`, `char_name`, `rank` (1-4), `join_date` |
| `mails` | In-game postal mail system | `id PK` | `recipient_char_id`, `sender_name`, `title`, `content`, `item_id`, `item_count`, `gold`, `is_read`, `sent_time` |
| `marriages` | Character marriage bonds | `(char_id1, char_id2) PK` | `char_id1`, `char_id2`, `marriage_date`, `rings_exchanged` |
| `monster_drops` | Monster combat drop overrides | `id PK` | `monster_id`, `item_id`, `drop_rate` (0.0-100.0), `count` |
| `chest_drops` | Map chest loot configurations | `id PK` | `chest_id`, `item_id`, `count`, `rate` |
| `alchemy_recipes` | Compound crafting recipes | `id PK` | `item_id1`, `item_id2`, `result_item_id`, `rate` |
| `banned_ips` | Firewall network IP blacklist | `ip PRIMARY KEY` | `reason`, `banned_at`, `banned_by` |
| `banned_users` | Account blacklist | `userID PRIMARY KEY` | `username`, `reason`, `banned_at`, `banned_by` |

---

## 3. Automated Startup Verification & Migration (`VerifySetup`)

On every server launch, [`GameDataBase.VerifySetup()`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/GameDataBase.cs#L53-L138) runs automatically to inspect schema integrity and auto-seed missing records.

```csharp
public void VerifySetup()
{
    // 1. Core tables (Friends, NPCs, NPC Templates, Quests)
    VerifyFriendsTable();
    VerifyNpcDataSetup();
    QuestDataBase.Initialize(this);

    // 2. Subsystem tables & seeders
    Server.ServerStatusManager.LoadConfig();
    Game.PlayerRelated.GmManager.LoadFromDatabase();
    Game.PlayerRelated.ItemMallManager.LoadFromDatabase();
    Game.PlayerRelated.StarterPackManager.LoadFromDatabase();
    Game.PlayerRelated.GuildManager.LoadFromDatabase();
    Game.PlayerRelated.MailSystem.LoadFromDatabase();
    Game.PlayerRelated.MarriageManager.LoadFromDatabase();
    Game.Battle.MonsterDropManager.LoadFromDatabase();
    Game.Maps.ChestDropManager.LoadFromDatabase();
    Game.Crafting.AlchemyManager.LoadFromDatabase();

    // 3. Security, settings & unique indexes
    ExecuteNonQuery("CREATE TABLE IF NOT EXISTS player_settings (char_id INTEGER PRIMARY KEY, pk_mode INT DEFAULT 0, join_mode INT DEFAULT 1, trade_mode INT DEFAULT 1);");
    ExecuteNonQuery("CREATE TABLE IF NOT EXISTS banned_ips (ip TEXT PRIMARY KEY, reason TEXT, banned_at TEXT, banned_by TEXT);");
    ExecuteNonQuery("CREATE TABLE IF NOT EXISTS banned_users (userID INT PRIMARY KEY, username TEXT, reason TEXT, banned_at TEXT, banned_by TEXT);");
    ExecuteNonQuery("CREATE UNIQUE INDEX IF NOT EXISTS idx_charquest_char_quest ON charquest(charID, quest_started);");
}
```

### Self-Healing Characteristics
- If `server_settings` is empty, it seeds: `CLUSTER=1`, `SERVER_ID=1`, `MODE=1`, `EXP_RATE=1.0`, `DROP_RATE=1.0`, `GOLD_RATE=1.0`, `MAX_PLAYERS=500`.
- If `gm_accounts` is empty, it seeds default `admin` and `developer` accounts with GM Level 10.
- If `item_mall` is empty, it migrates all 224 authentic items from `Data/item_mall.json`.
- If `starter_items` is empty, it seeds 5 novice starter supplies (Recovery Potions, Mana Water, Repair Wrench, Rice Balls, Adventurer Badge).

---

## 4. Character Data Hydration & Persistence Flow

```
[Client Login] ---> AC 2:4 (Select Character)
                          │
                          v
         [Hydrate Character from SQLite]
         - Character Identity (characters)
         - Visual Appearance (charactersextdata)
         - Combat Attributes (stats)
         - Inventory & Equipment (inventory)
         - Companions & Pets (character_pets)
         - Quest Step Journal (charquest)
         - Unlocked Skills (character_skills)
         - Interaction Flags (player_settings)
                          │
                          v
                 [Live Gameplay (RAM)]
                          │
          +---------------+---------------+
          |                               |
          v                               v
[Event: Level/Item/Quest]      [Periodic Flush / Logout / Shutdown]
Immediate DB Update            Atomic Transaction Sync
```

### Methods & Parameters
- `UserDataBase.GetCharacters(int userId)`: Returns array of populated [`Character`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Character.cs) models.
- `GameDataBase.SaveCharacter(Character c)`: Serializes HP, SP, Gold, MapID, X, Y coordinates back to `characters` and `stats`.
- `GameDataBase.SaveInventory(Player p)`: Executes transaction replacing inventory rows for target `charID`.
- `GameDataBase.SavePets(Player p)`: Updates levels, amity, battle statuses for all companions.

### Exceptions & Edge Cases
- **Duplicate Quest ID Collision**: Guarded by `idx_charquest_char_quest` unique index and `INSERT OR REPLACE` query semantics.
- **Concurrent DB Locks**: Thread synchronization inside [`RCLibrary.Core.DataBase.Execute`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/RCLibrary.Core/DataBase.cs#L100-L125) prevents `database is locked` exceptions under high packet frequency.
