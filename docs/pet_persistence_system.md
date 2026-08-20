# Pet & Companion Persistence System Specification

## 1. Overview
Companions and recruited pets (e.g., Robinson `#12178`, Monkey `#10727`, Roca `#14161`, Niss, etc.) are now fully persisted in the MySQL database under table `character_pets`. Whenever a pet is recruited, leveled up, mounted, or when the player logs off / auto-saves, the pet's full state is synchronized and restored seamlessly upon the next login.

## 2. Database Schema (`character_pets`)
```sql
CREATE TABLE IF NOT EXISTS character_pets (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    charID INT NOT NULL,
    slot TINYINT NOT NULL,
    petID INT NOT NULL,
    petName TEXT,
    level TINYINT DEFAULT 1,
    hp INT DEFAULT 250,
    maxHp INT DEFAULT 250,
    sp INT DEFAULT 100,
    maxSp INT DEFAULT 100,
    amity TINYINT DEFAULT 60,
    isBattle TINYINT DEFAULT 1,
    isRide TINYINT DEFAULT 0
);
```

## 3. Implementation Workflow
1. **Recruitment / Addition**:
   - `QuestManager.SendCompanionReward`: Registers companion into `player.PlayerPets[slot]` and dispatches authentic 54-byte `AC 15:1` packet + `AC 19:1` battle state.
   - Command `:pet <id> [name]`: Instantly awards, spawns, and persists companion to database.
2. **Auto-Save & Logout Persistence**:
   - [`CharacterDataBase.WritePlayer`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/CharacterDataBase.cs): Saves all pets from `player.PlayerPets` into `character_pets` atomically.
3. **Login Restoration**:
   - [`GameDataBase.LoadFinalData`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/GameDataBase.cs): Reads all pets from `character_pets`, rebuilds `player.PlayerPets`, and dispatches the companion recruit and battle/mount packets upon spawn.
