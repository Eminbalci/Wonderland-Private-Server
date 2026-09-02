# Immediate Player Persistence System

## 1. Overview
The **Immediate Player Persistence System** guarantees that critical player state modifications are atomically and immediately committed to the SQLite database without relying solely on periodic background save intervals or server shutdown routines.

## 2. Persisted State Scope
Every persistence trigger executes `CharacterDataBase.WritePlayer(charID, player)` and updates:
1. **Character Base Data (`characters` table)**:
   - `location_map`, `location_x`, `location_y` (or Tent coordinate preservation)
   - `gold` (Gold currency)
   - `haircolor`, `skincolor`, `clothingcolor`, `eyecolor`
   - `head`, `body`, `element`, `job`, `rebirth`
2. **Character Stats (`stats` table)**:
   - `CurHP` (Stat 25), `CurSP` (Stat 26)
   - `SkillPoints` (Stat 38), `Potential`
   - `TotalExp` (Stat 36 / Level progression)
   - Base attributes: `baseStr` (28), `baseCon` (29), `baseAgi` (30), `baseInt` (27), `baseWis` (33)
3. **Inventory & Equipment (`inventory` table)**:
   - Bag items (`storID = 0`)
   - Equipped items (`storID = 1`)
   - Props Keeper vault storage (`storID = 2`)
4. **Skills (`character_skills` table)**:
   - All learned player skills with current `grade` (1-10) and `exp` proficiency
5. **Pets & Companions (`character_pets` table)**:
   - Active party pets (`isHotel = 0`) and Pet Hotel storage (`isHotel = 1`)
   - Levels, HP, SP, MaxHP, MaxSP, Amity, Battle toggle, Ride toggle
6. **Quests & Flags (`charquest` table)**:
   - All accepted, in-progress, and completed quests/flags with authentic states

## 3. Real-Time Trigger Matrix

| Category | Action / Packet | Trigger Point |
| :--- | :--- | :--- |
| **Map & Movement** | `AC 5:7` (Map Handshake Ready) | Full sync completion on entering any map |
| **Map & Movement** | `AC 20:8` (Map Portal / Teleport) | Stepping onto map warps, doors, teleporters |
| **Dialogue & Quests** | `AC 20:6` (Interaction Finish) | Completing NPC dialogues, event choices |
| **Dialogue & Quests** | `EveEventInterpreter` Opcode 1 | Receiving or consuming quest items |
| **Dialogue & Quests** | `EveEventInterpreter` Opcode 5 | Quest acceptance, step progression, and completion |
| **Inventory & Items** | `AC 23:96` (Item Use) | Consuming food, potions, vouchers, scrolls |
| **Inventory & Items** | `AC 23:2` / `AC 23:3` | Picking up items from ground or dropping items |
| **Inventory & Items** | `AC 23:10` | Moving / swapping inventory slots |
| **Inventory & Items** | `AC 23:11` / `AC 23:12` | Equipping or unequipping items |
| **Inventory & Items** | `AC 23:124` | Discarding / destroying items |
| **Item Mall** | `AC 23:26` | Purchasing items from Item Mall |
| **Stats & Allocation**| `AC 8:1` (Stat Point Up) | Allocating STR, CON, INT, WIS, AGI to character or pet |
| **Skills & Evolution**| `SkillManager.AddSkillExp` | Gaining skill EXP in combat turns |
| **Skills & Evolution**| `SkillManager.CheckAndUnlockProgressionSkills` | Unlocking element/level progression skills |
| **Battle System** | `PvEBattleManager.EndBattleVictory` | Winning combat, receiving EXP, Gold, and Monster Drops |
| **Battle System** | `PvEBattleManager.EndBattleDefeat` | Attacking/Defending team defeated |
| **Battle System** | `PvEBattleManager.HandleFlee` | Fleeing from combat |
