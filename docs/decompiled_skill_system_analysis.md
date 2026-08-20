# Decompiled Analysis: Skill Systems in `aLogin_decompiled.c`

## 1. Overview
Reverse engineering analysis of skill-related subroutines in `decompiled/aLogin_decompiled.c` covers player skill trees, NPC/companion skill progression, combat casting validation, skill sealing mechanics, alchemy/compound skills, and VFX animation engines.

---

## 2. Identified Skill Subsystems & Functions

### 2.1 Skill UI Forms & Tree Renderers
* **NPC Skill Tree Form:** [`FUN_0025933c`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L25933c) (`@ 0x0025933c`)
  * Initializes `form_npcSkillTree` and `icon_levelUp`.
  * Handles leveling up companion-specific elemental and stunt skills.
* **Player Skill Express & Requirement Inspector:** [`FUN_002bb608`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L2bb608) (`@ 0x002bb608`)
  * Displays `form_skillexpress` and `Icon_SkillNeed`.
  * Computes pre-requisite skill levels and attribute thresholds (STR/INT/WIS/AGI).
* **Battle Skill Selection Window:** [`FUN_002b5774`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L2b5774) (`@ 0x002b5774`)
  * Initializes `Form_BattleSkill_1` during turn-based combat rounds.

### 2.2 Skill Execution & State Guards
* **Skill Cast & Seal Verification:** [`FUN_002b5e58`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L2b5e58) (`@ 0x002b5e58`)
  * Validates debuffs preventing skill execution: `"Skill is sealed"`.
* **General Skill Execution Guard:** [`FUN_002ab518`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L2ab518) (`@ 0x002ab518`)
  * Checks SP availability, target viability, and combat status: `"Can't use skill"`.
* **Auto-Player / AI Skill Slot Configuration:** [`FUN_001d7310`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L1d7310) (`@ 0x001d7310`)
  * Enforces offensive skill restriction for auto-battle slots: `"Can't set non-Atk skill"`.
* **Pet / Companion Skill Assignment:** [`FUN_001d8998`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L1d8998) (`@ 0x001d8998`)
  * Enforces companion skill verification: `"Select skill pet knows"`.

### 2.3 Compound & Manufacture Skills
* **Subroutine:** [`FUN_00235340`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L235340) (`@ 0x00235340`)
  * Handles `Icon_UseCompoundSkill` for Alchemy, Cooking, and Crafting skill execution.

### 2.4 Skill Visual Effects (VFX) & Lighting
* **Subroutines:** [`FUN_00322a90`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L322a90) (`@ 0x00322a90`) & [`FUN_003e32bc`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L3e32bc) (`@ 0x003e32bc`)
  * Loads skill light sprite animations and particle assets (`SkillLight\\`).
  * Calculates trajectory, hit orientation (`param_8` to `param_16`), and visual layering.

### 2.5 Rebirth Superior Class Skill Descriptions
* **Subroutine:** [`FUN_001a40ac`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L1a40ac) (`@ 0x001a40ac`)
  * Handles class-specific skill tooltips for Killer, Warrior, Knight, Mage, Priest, and Wit classes.
