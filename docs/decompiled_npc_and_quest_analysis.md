# Decompiled Analysis: NPC & Quest Structures in `aLogin_decompiled.c`

## 1. Overview
Direct reverse engineering scan of `decompiled/aLogin_decompiled.c` reveals client-side implementations for NPC rendering, map event decoding, companion/pet management, and quest item handling.

---

## 2. Identified Subroutines in `aLogin_decompiled.c`

### 2.1 Map Scene & NPC Data Loader
* **Subroutine:** [`FUN_00497c2c`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L497c2c) (`@ 0x00497c2c`) & [`FUN_00498208`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L498208) (`@ 0x00498208`)
* **Logic:**
  * Parses map header chunk (`"Read SceneHead."`).
  * Deserializes static map NPCs (`"Read Npc."`) and portal/door transitions (`"Read Door."`).
  * Sets up scene entity structures at `*param_2 + 0xd50`.

### 2.2 NPC / Companion Storage & Management
* **Subroutine:** [`FUN_0020885c`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L20885c) (`@ 0x0020885c`)
* **Logic:**
  * Checks NPC/Companion party flags and bank/tent storage eligibility.
  * Enforces constraints: `"Can't store NPC"`.
  * Handles companion slot indexing (up to 4 companion slots: `param_1 + 0x121 + iVar4`).

### 2.3 Pet / Companion Rebirth & Skills
* **Subroutine:** [`FUN_0022b994`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L22b994) (`@ 0x0022b994`)
  * Validates pet rebirth criteria: `"Remove all pet equips to reborn"`.
* **Subroutine:** [`FUN_001d8998`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L1d8998) (`@ 0x001d8998`)
  * Skill selection validator: `"Select skill pet knows"`.

### 2.4 Entity Differentiation & EXP Cap Verification
* **Subroutine:** [`FUN_003f7aec`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L3f7aec) (`@ 0x003f7aec`)
* **Logic:**
  * Distinguishes whether target entity is Player (`*(param_1 + 0xb) == 0`) or Pet/Companion (`*(param_1 + 0xb) != 0`).
  * Enforces player-only items/actions: `"Only for player"`.
  * Verifies entity EXP cap against table bounds: `"EXP is at maximum"`.

### 2.5 Item Classification & Quest Items
* **Subroutine:** [`FUN_0049f674`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L49f674) (`@ 0x0049f674`)
* **Logic:**
  * Switch table classifying inventory item categories:
    * `Case 0x15`: Returns `"Quest Item"` (identifying non-tradeable, task-specific collection items).
    * `Case 0x10`: `"Special Eq"`
    * `Case 0x19`: `"Spec Prop"`
    * `Case 0x06` - `0x0B`: Weapon classes (`"Claw"`, `"Knuckle"`, `"Axe"`, `"Club"`, `"Hammer"`).

### 2.6 Battle & Event Interactions
* **Subroutines:** [`FUN_001bf5c4`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L1bf5c4), [`FUN_001da04c`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L1da04c), [`FUN_00447bb8`](file:///d:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c#L447bb8)
  * Restricts dialogs/actions during combat: `"Can't in battle"`, `"Target is in battle"`.
  * Triggers event NPC dialogue alerts and level checks (`"Below LV10 can't participate"`).
