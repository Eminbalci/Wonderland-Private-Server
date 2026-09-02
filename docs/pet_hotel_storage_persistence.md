# Pet Hotel System & Database Persistence

## Overview
This document specifies the Pet Hotel (Companion Storage) protocol (`AC 31`), data model, and SQLite database persistence across all game maps.

---

## 1. Data Model & Architecture

- **Active Pet Team (`Player.PlayerPets`):** Up to 4 active companions in party/battle.
- **Pet Hotel Storage (`Player.HotelPets`):** Up to 20 stored companions per character.

### Database Schema (`character_pets` table)
```sql
CREATE TABLE character_pets (
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
    isRide TINYINT DEFAULT 0,
    isHotel TINYINT DEFAULT 0 -- 0: Active Pet Team, 1: Pet Hotel Storage
);
```

---

## 2. Network Protocol (`AC 31` / `0x1F`)

1. **Open Pet Hotel (`Player.OpenPetHotel`):**
   - Dispatches `AC 31:1`, `AC 20:9`, `AC 20:8`, `AC 5:4`.
   - Iterates through `Player.HotelPets` and sends `AC 31:3` with full companion stats to populate the Pet Hotel GUI.

2. **Deposit Pet (`AC 31 Sub 3`):**
   - Client sends: `F4 44 03 00 1F 03 [petSlot]` (Deposit Pet from Team Slot 1..4).
   - If pet was following/battling, unsummons and resets `ActivePetID = 0` (`AC 19:5`).
   - Transfers pet from `Player.PlayerPets[petSlot]` to `Player.HotelPets[freeSlot]`.
   - Server dispatches:
     - `AC 19:2 [petSlot]` (Removes pet from player active team roster).
     - `AC 31:3` with companion stats (Adds pet to Pet Hotel UI list).
   - Immediately commits state to SQLite database via `CharacterDataBase.WritePlayer`.

3. **Withdraw Pet (`AC 31 Sub 4` / `AC 31 Sub 2`):**
   - Client sends: `[31, 4, hotelSlot]` (Withdraw Pet from Hotel Slot 1..20).
   - Transfers pet from `Player.HotelPets[hotelSlot]` to first free `Player.PlayerPets[freeTeamSlot]`.
   - Server dispatches:
     - `AC 31:4 [hotelSlot]` (Removes pet from Hotel UI list).
     - `QuestManager.SendCompanionReward` (Adds pet to active team).
   - Immediately commits state to SQLite database.

4. **Close Pet Hotel (`AC 31 Sub 7`):**
   - Dispatches `AC 31:7`, `AC 20:8`, `AC 5:4`.
