# 55 - Character Role Status Sheets and Attribute Recalculation Specification

## 1. Architectural Overview & Source Mapping

The character status, pet companion attributes, vehicle statistics, and stat point progression in Wonderland Online are managed through a unified 3-panel notebook interface represented by `Form_RoleStatus_1`, `Form_RoleStatus_2`, and `Form_RoleStatus_3`. The underlying engine logic resides in `DZ_Human.pas`, `DZ_Calculate.pas`, and associated UI controllers, orchestrating real-time attribute recalculation (HP, SP, ATK, DEF, MATK, MDEF, SPD) from base stats, gear bonuses, rebirth multipliers, and elemental passives.

### Subroutine Inventory & Source Locations

| Virtual Address | Subroutine Symbol | File / Unit | Purpose |
| :--- | :--- | :--- | :--- |
| `0x0036df10` | `FUN_0036df10` | `DZ_Human.pas` | Stat point allocation buffer reset and visual button lock |
| `0x0036e0fe` | `FUN_0036e0fe` | `DZ_Human.pas` | Stat distribution commit handler and Action Code 8:1 packet assembly |
| `0x0036e130` | `FUN_0036e130` | `DZ_Human.pas` | Toggle allocation '+' button visibility for STR, CON, INT, WIS, AGI |
| `0x0036e154` | `FUN_0036e154` | `DZ_Human.pas` | Discard pending attribute allocations and restore unallocated pool |
| `0x003700b0` | `FUN_003700b0` | `DZ_Human.pas` | RoleStatus 3-tab switcher (`Form_RoleStatus_1..3`) and geometry resizing |
| `0x0037d778` | `FUN_0037d778` | `DZ_Calculate.pas` | Combat rating formula recalculator (ATK, DEF, MATK, MDEF, SPD) |

---

## 2. Form Geometry & 3-Tab Architecture

The character status browser uses a dynamically resizing container managed by `FUN_003700b0`:

```
+-------------------------------------------------------------+
| [icon] Character Status & Inventory          [Btn_Close_s_1]|
+-------------------------------------------------------------+
|  [Tab 0: Character]     [Tab 1: Companion Pet]   [Tab 2: Mount] |
|  +-------------------------------------------------------+  |
|  | Name: [CharName]            Element: [Earth/Water/Fire/Wind] |
|  | Job:  [Base / Rebirth Job]  Level:   [1..199]         |
|  | HP:   [2450 / 2450]         SP:      [1120 / 1120]    |
|  | EXP:  [45,820 / 100,000]    Gold:    [1,240,500g]     |
|  +-------------------------------------------------------+  |
|  Base Attributes:            Combat Derived Ratings:        |
|  [+] STR: 120 (+15)           ATK:  420 (+45)                |
|  [+] CON: 85  (+10)           DEF:  310 (+80)                |
|  [+] INT: 10  (+0)            MATK: 25  (+0)                 |
|  [+] WIS: 10  (+0)            MDEF: 60  (+20)                |
|  [+] AGI: 95  (+25)           SPD:  245 (+50)                |
|  Unallocated Pts: [ 14 ]                                    |
|                                                             |
|  Equipment Slots (6):                                       |
|  [Head] [Body] [Weapon] [Wrist] [Foot] [Special]            |
|                                                             |
|  [Status Only]      [Inventory Only]      [Confirm Stats]   |
+-------------------------------------------------------------+
```

### Tab Configuration Matrix (`*(byte *)(param_1 + 1000)`)

| Tab Index | Form Asset Name | Target Entity | Unique Controls & Displays |
| :---: | :--- | :--- | :--- |
| `0` | `Form_RoleStatus_1` | Local Player | 6 Equipment slots, 5 Stat allocation buttons, Rebirth crest |
| `1` | `Form_RoleStatus_2` | Active Pet | Pet Amity bar ($0..100$), Pet Rebirth badge, Pet Equip slot |
| `2` | `Form_RoleStatus_3` | Active Vehicle | Fuel / Oil level, Vehicle Durability, Passenger list |

---

## 3. Stat Allocation Wire Protocol & Codes (`FUN_0036e0fe`)

When the player clicks the `[Confirm Stats]` button (`FUN_0036e0fe`), the client inspects combat mutex `*(actor + 0x1f16)`. If in combat (`0x01` or `0x04`), the client rejects allocation with:
`"Can't be set in battle"`.

If permitted, the client formats an **Action Code 8 (`0x08`)** packet with Sub-Opcode `0x01`:

### Wire Attribute Code Identifiers

| Stat Name | Loop Index ($i$) | Client Wire Code (Hex) | Client Wire Code (Dec) | Target Register |
| :--- | :---: | :---: | :---: | :--- |
| **STR** (Strength) | `1` | `0x1C` | `28` | Physical Attack Power (`ATK`) |
| **CON** (Constitution) | `2` | `0x1D` | `29` | Maximum HP & Physical Defense (`DEF`) |
| **INT** (Intelligence) | `3` | `0x1B` | `27` | Magic Attack Power (`MATK`) |
| **WIS** (Wisdom) | `4` | `0x21` | `33` | Maximum SP & Magic Defense (`MDEF`) |
| **AGI** (Agility) | `5` | `0x1E` | `30` | Turn-Order Combat Speed (`SPD`) |

### Stat Allocation Wire Frame (`0x08:0x01`)

```
+---------------+---------------+--------------------+---------------+---------------+
| Action (0x08) | Sub-Op (0x01) | Modified Stats (N) | Stat Code (B) | Added Pts (2B)|
+---------------+---------------+--------------------+---------------+---------------+
| Stat Code (B) | Added Pts (2B)| ...                | ...           | ...           |
+---------------+---------------+--------------------+---------------+---------------+
```

---

## 4. Derived Attribute Calculation Formulas (`FUN_0037d778`)

Derived ratings are calculated from base attributes, equipment bonuses, and rebirth job coefficients:

### 1. Maximum Health & Mana

$$\text{Max HP} = \text{BaseHP} + (\text{CON} \times 2.0) + \text{GearHP}$$
$$\text{Max SP} = \text{BaseSP} + (\text{WIS} \times 2.0) + \text{GearSP}$$

### 2. Physical & Magical Ratings

$$\text{ATK} = (\text{STR} \times 2.0) + \text{WeaponATK} + \text{GearATK}$$
$$\text{DEF} = (\text{CON} \times 2.0) + \text{ArmorDEF} + \text{GearDEF}$$
$$\text{MATK} = (\text{INT} \times 2.0) + \text{WeaponMATK} + \text{GearMATK}$$
$$\text{MDEF} = (\text{WIS} \times 2.0) + \text{ArmorMDEF} + \text{GearMDEF}$$
$$\text{SPD} = (\text{AGI} \times 2.0) + \text{GearSPD} + \text{MountSPDBonus}$$

### 3. Rebirth Class Job Modifiers

Upon Rebirth Ascension (Document 23), the base stats receive job-specific passive scalings:
- **Killer (殺手):** ATK $+10\%$, SPD $+5\%$
- **Warrior (武士):** DEF $+10\%$, Counter-attack probability $+15\%$
- **Knight (騎士):** Mount SPD bonus increased to $1/3$ (standard is $1/5$), Innate Saddle exemption
- **Sage (智者):** MDEF $+10\%$, Skill SP consumption reduced by $20\%$
- **Priest (牧師):** Healing skill effectiveness $+30\%$, MDEF $+15\%$
- **Prophet (先知):** Magic debuff hit rate $+20\%$, Turn priority speed $+10\%$

---

## 5. Companion Pet Status Synchronization (`Form_RoleStatus_2`)

When viewing Tab 1 (`Form_RoleStatus_2`):
- **Amity Rating (`LocalPet + 0x1fa`):** Displayed on the affection heart meter ($0..100$). If amity falls below $20$, the companion refuses battle commands. If amity drops to $0$, the pet permanently abandons the player.
- **Pet Equipment:** Pets possess a dedicated single equipment slot (`LocalPet + 0x27f8`) for pet armaments or amity-preservation necklaces.
- **Pet EXP Curve:** Pets gain $100\%$ character combat EXP provided their level is within $\pm 19$ levels of the highest monster encountered.

---

## 6. Private Server Implementation Requirements

1. **Strict Stat Point Ledger:** Verify that the sum of requested points $\sum \Delta \text{Pts} \le \text{UnallocatedPoints}$ in the character database record.
2. **Atomic Rollback on Error:** If any single stat code is invalid, reject the entire transaction and re-synchronize the client stat values via `Action Code 23`.
3. **Turn-Order Priority Engine:** When speed changes occur (equipping/unequipping gear or allocating AGI), immediately recalculate party and combat turn-order queues in active battle instances.
