# Skill Unlocking & Learning System

## Overview
The Wonderland Online Skill System manages character stunt skills, elemental trees, skill progression, in-combat skill usage, and skill grade upgrades.

---

## Technical Specifications

### 1. Packet Protocol

#### A. Initial Login Packet (`AC 5 Sub 3`)
- **Format**: `Send_5_3()` sends standard character base stats and embeds the player's learned skill collection (`PlayerSkills` count followed by `[skillId 2B, grade 2B, exp 2B, 0 1B]` per skill), followed by reborn, job, and potential.
- **Silent Loading**: WLO client silently populates the skill book and active slots upon receiving `AC 5:3` without triggering "You have learned [Skill]" popups or animations.

#### B. Skill Unlock & EXP Sync (`AC 5 Sub 11`)
- **Action Code**: `5`
- **Sub Action**: `11`
- **Payload Format**: `[5, 11, <skill_id 4B uint>, <exp 4B uint>]`
- **Purpose**: Dispatched ONLY when a player dynamically unlocks a new skill during active gameplay (e.g. stat requirement reached, Grade 10 evolution, quest reward, GM command). Triggers the client's "You learned [Skill Name]" announcement.

#### C. Skill Slot Mapping (`AC 5 Sub 13`)
- **Action Code**: `5`
- **Sub Action**: `13`
- **Payload Format**: `[5, 13, <slot 1B>, <skill_id 2B ushort>]`
- **Purpose**: Maps each unlocked skill into the client's skill window and battle quickbar slots.

#### D. Skill Grade Stat Broadcast (`AC 8 Sub 1`)
- **Action Code**: `8`
- **Sub Action**: `1`
- **Stat ID**: `110` (Skill Grade)
- **Sub Index**: `1`
- **Payload Format**: `[8, 1, 110, 1, <grade 4B uint>, <skill_id 4B uint>]`

#### E. Skill Finalize & Refresh Signal (`AC 5 Sub 4`)
- **Payload**: `[5, 4]`
- **Purpose**: Informs the client to refresh and render the updated skill menu in both normal mode and in-turn battle window.

---

### 2. Starter Stunt & Elemental Skill Matrix

| Character / Head | Body | Starter Stunt Skill ID & Name |
| :--- | :--- | :--- |
| **Iris** (Head 0) | Big Female (4) | `15041` (Love Wish) |
| **Lique** (Head 1) | Big Female (4) | `12053` (Gallop) |
| **Vanessa** (Head 2) | Big Female (4) | `15003` (Newbie's Stunt) |
| **Breillat** (Head 3) | Big Female (4) | `15060` (Throw Dish) |
| **Jessica** (Head 4) | Big Female (4) | `12051` (Note) |
| **Konno Tsuruko** (Head 5) | Big Female (4) | `12049` (Fire Dance) |
| **Maria** (Head 6) | Big Female (4) | `11077` (Cure 2 Players) |
| **Karin** (Head 7) | Big Female (4) | `15040` (Palm) |
| **Daniel** (Head 0) | Big Male (3) | `11076` (Combo x3 Attack) |
| **Sid** (Head 1) | Big Male (3) | `11076` (Combo x3 Attack) |
| **More** (Head 2) | Big Male (3) | `11183` (Deacon Attack) |
| **Kurogane** (Head 3) | Big Male (3) | `11182` (Ghost Hammer) |
| **Nina** (Head 0) | Small Female (2) | `15039` (Wine Flame) |
| **Betty** (Head 1) | Small Female (2) | `12036` (Leap) |
| **Rocco** (Head 0) | Small Male (1) | `11075` (Summon Dogs Groups) |

| Element | Starter Physical Skill | Starter Magical Skill | Starter Assistant Skill |
| :--- | :--- | :--- | :--- |
| **Fire** (`3`) | `11166` (Blast Attack) | `11016` (Flame Attack) | `11056` (Slowdown) |
| **Earth** (`1`) | `11017` (Earth Attack) | `15085` (Rock Attack) | `11057` (Shield Defence) |
| **Water** (`2`) | `11001` (Icicle Attack) | `15091` (Ice Attack) | `15100` (Detoxification) |
| **Wind** (`4`) | `15079` (Air Attack) | `11007` (Wind Attack) | `11052` (Speed Up) |
| **Dark / Undefined** (`5` / `7`) | `25116` (Deadly Wind / Dark Strike) | `25115` (Fiery Wave / Dark Wave) | `25110` (Poisonous Chill / Chaos) |

---

### 3. Database Persistence (`character_skills`)
* **Schema**:
  ```sql
  CREATE TABLE IF NOT EXISTS character_skills (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      charID INT NOT NULL,
      skillID INT NOT NULL,
      grade TINYINT DEFAULT 1,
      exp INT DEFAULT 0,
      UNIQUE(charID, skillID)
  );
  ```
* **Persistence Lifecycle**:
  1. **Character Login (`SkillManager.InitializePlayerSkillsNoSend`)**: Loads saved `grade` and `exp` for all learned skills from `character_skills` and applies them to `PlayerSkills`.
  2. **Skill Unlock / Level Up (`SkillManager.UnlockSkill`)**: Atomically writes `INSERT INTO character_skills ... ON CONFLICT(charID, skillID) DO UPDATE SET grade=..., exp=...;`.
  3. **Character Save / Logout (`CharacterDataBase.WritePlayer`)**: Atomically flushes all entries in `player.PlayerSkills` into `character_skills`.
### 4. Skill Evolution Tree & Grade Leveling (`Attack` -> `Hit` -> `Beating`)
* **Grade Leveling (1–10)**:
  * Her yetenek savaşta kullanıldıkça EXP kazanır (`SkillManager.AddSkillExp`). `Grade * 100` EXP barajı aşıldığında yeteneğin `Grade` seviyesi artar (`AC 5:11`, `AC 5:12`, `AC 8:2 Stat 110` paketleri ile istemciye bildirilir).
* **Stat Kilitleri vs. Grade 10 Evrim Kuralı**:
  1. **Temel Ağaç Kök Yetenekleri (Stat Şartları)**: Stat puanları (STR, INT, WIS, CON, AGI) yeterli seviyeye ulaştığında yalnızca 1. Aşama (`Attack` varyantı, örn: `Five Star Attack`, `Fire Wave Attack`, `Sword Awn Attack`) açılır.
  2. **İleri Aşama Evrimler (`Hit` ve `Beating` - Grade 10 Şartı)**:
     * `Five Star Attack` (Grade 10) ➔ `Five Star Hit` açılır.
     * `Five Star Hit` (Grade 10) ➔ `Five Star Beating` açılır.
     * `Fire Wave Attack` (Grade 10) ➔ `Fire Wave Hit` ➔ `Fire Wave Beating` açılır.
     * `Sword Awn Attack` (Grade 10) ➔ `Sword Awn Hit` ➔ `Sword Awn Beating` açılır.
     * `Blast Attack` (Grade 10) ➔ `Blast Hit` ➔ `Blast Beating` açılır.
     * `Flame Attack` (Grade 10) ➔ `Flame Hit` ➔ `Flame Beating` açılır.
     * `Fire Ball Attack` (Grade 10) ➔ `Fire Ball Hit` ➔ `Fire Ball Beating` açılır.
     * `Blaze Attack` (Grade 10) ➔ `Blaze Hit` ➔ `Blaze Beating` açılır.
     * `Fire Combo Attack` (Grade 10) ➔ `Fire Combo Hit` ➔ `Fire Combo Beating` açılır.
     * `Fire Stone Attack` (Grade 10) ➔ `Fire Stone Hit` ➔ `Fire Stone Bead` açılır.
     * `Fire Attack` (Grade 10) ➔ `Magma Attack` ➔ `Fire Dance` açılır.
  * Tüm elementlerde (Toprak, Su, Ateş, Rüzgar) aynı 3 aşamalı hiyerarşik yapı eksiksiz çalışmaktadır.
      * `Ice Attack` (#15091, Grade 10) ➔ `Ice Hit` (#15092) ➔ `Ice Beating` (#15093) ➔ `Ice Ball Attack` (#11110)
      * `Icicle Attack` (#11001, Grade 10) ➔ `Icicle Hit` (#15062) ➔ `Turning Ice Attack` (#15019)
      * `Detoxification` (#15100, Grade 10) ➔ `Cure Spell` (#11042) ➔ `Ice-out` (#12048)
    * **Rüzgar (Wind):**
      * `Wind Attack` (#11007, Grade 10) ➔ `Wind Hit` (#11014) ➔ `Wind Bead` (#15113) ➔ `Whirlwind Attack` (#15123)
      * `Air Attack` (#15079, Grade 10) ➔ `Wind Cut Hit` (#15009) ➔ `Instant Attack` (#15002) ➔ `Dead Wind Attack` (#15114)
      * `Speed Up` (#11052, Grade 10) ➔ `Shield Smash` (#11073) ➔ `Unload Wall` (#12046) ➔ `Cord Spell` (#15032)
    * **Karanlık (Dark / Undefined):**
      * `Poisonous Chill` (#25110, Grade 10) ➔ `Poisonous Wave` (#25113)
      * `Fiery Wave / Dark Wave` (#25115, Grade 10) ➔ `Hellfire` (#25246) ➔ `Polar Demonitis` (#25248)
      * `Deadly Wind / Dark Strike` (#25116, Grade 10) ➔ `Crack Beating` (#25165) ➔ `Super Crack Beating` (#25169) ➔ `Furious Cyclone` (#25175) ➔ `Entangled Wind` (#25185)
      * `Chaos Curse` (#25167, Grade 10) ➔ `Entangled Curse` (#25168) ➔ `Summon Death` (#25470)

---

## Source Files

- [`wlo.pserver.core/Game/SkillRelated/SkillManager.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/SkillRelated/SkillManager.cs)
- [`wlo.pserver.core/Game/PlayerRelated/Character.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Character.cs)
- [`wlo.pserver.core/Game/Player.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Player.cs)
- [`wlo.pserver.core/DataBase/CharacterDataBase.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/CharacterDataBase.cs)
- [`Src/Server/WorldServer.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Server/WorldServer.cs)
- [`Src/Gui/CharacterDataEditorForm.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Gui/CharacterDataEditorForm.cs)
