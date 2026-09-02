# Companion & Pet Combat System (`AC 11:5`, `AC 50:1`, `AC 51:1`)

This document describes the packet architecture, grid positioning, combat mechanics, and turn simulation for companion and pet entities participating in PvE battles.

---

## 1. Overview & Mechanics

When a player enters combat with an active battle companion or recruited pet (`player.PlayerPets` where `isBattle == 1`):
1. **Grid Positioning**: The active companion is placed on the player's front-row slot directly ahead of the player at **Grid (3, 2)** (while the Player occupies **Grid (4, 2)**).
2. **Entity Initialization**: The server dispatches an authentic `AC 11:5` fighter spawn packet (`ftype = 4` for Pet/Companion, `owner_id = player.CharID`, `ClickID = 0`, Level, MaxHP/HP, MaxSP/SP).
3. **Stat Synchronization**: Real-time HP and SP are synchronized via `AC 51:1` for both the player and the companion.
4. **Turn Execution**:
   - During the player's turn phase, the companion executes an offensive combat attack against the targeted enemy monster.
   - Monster enemies distribute attacks dynamically between the player and their frontline companion.
5. **Post-Battle & Persistence**:
   - Companion HP and SP updates are preserved in `player.PlayerPets`.
   - On victory, companion gains EXP and level increases.
   - On battle exit, companion entities are cleanly despawned from the grid (`AC 11:1`).

---

## 2. Packet Flow

### 2.1 Battle Entry Sequence
1. `AC 20:12` - Enter battle mode.
2. `AC 6:2 [01]` - Mode change signal.
3. `AC 11:250` - Spawn Player battle entity.
4. `AC 11:10 [01]` - Combat start signal.
5. `AC 11:5` - Spawn Active Companion entity (`ftype = 4`, `owner_id = player.CharID`, `GridX = 3`, `GridY = 2`).
6. `AC 11:5` - Spawn Monster entities in formation (`ftype = 7`, `GridX = 2..1`, `GridY = 1..4`).
7. `AC 51:1` - Synchronize initial HP/SP values for Player, Pet, and Monsters.
8. `AC 50:6` & `AC 52:1` - Start round and present action wheel UI.

### 2.2 Companion Turn Attack
- `AC 53:5 [PetGridX (1B), PetGridY (1B)]` - Companion action broadcast.
- `AC 50:1` - Combat animation packet:
  - Header: `50 01 11 00`
  - Source: `[PetGridX] [PetGridY]`
  - Skill: `10001` (Basic Strike / Combat Skill)
  - Target: `[TargetGridX] [TargetGridY]`
  - Damage & Stat: `[StatID = 25 (HP)] [Damage (4B LE)]`
- `AC 51:1` - Sync target monster's decremented HP.

---

## 3. Retaliation & Damage Mitigation

- **Target Selection**: Enemy monsters randomly target either the frontline companion or the player.
- **Companion Defense**: Calculated using `(pet.Level * 2 + pet.Con * 2)`.
- **Fallen Companion**: If companion HP reaches 0, the server sends `AC 11:1 [3, 2, 0]` to despawn the pet and issues a system notification.
