# Skill Unlocking & Learning System

## Overview
The Wonderland Online Skill System manages character stunt skills, elemental trees, skill progression, in-combat skill usage, and skill grade upgrades.

---

## Technical Specifications

### 1. Packet Protocol

#### A. Initial Login Packet (`AC 5 Sub 3`)
- **Format**: `Send_5_3()` sends standard character base info with 0 skills placeholder, followed by reborn, job, and potential.

#### B. Skill Slot Mapping (`AC 5 Sub 13`)
- **Action Code**: `5`
- **Sub Action**: `13`
- **Payload Format**: `[5, 13, <slot 1B>, <skill_id 2B ushort>]`
- **Purpose**: Maps each unlocked skill into the client's skill window and battle quickbar slots.

#### C. Skill Unlock & EXP Sync (`AC 5 Sub 11`)
- **Action Code**: `5`
- **Sub Action**: `11`
- **Payload Format**: `[5, 11, <skill_id 4B uint>, <exp 4B uint>]`

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

## Source Files

- [`wlo.pserver.core/Game/SkillRelated/SkillManager.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/SkillRelated/SkillManager.cs)
- [`wlo.pserver.core/Game/PlayerRelated/Character.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Character.cs)
- [`Src/Server/WorldServer.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Server/WorldServer.cs)
- [`Src/Network/ActionCodes/AC02.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC02.cs)
