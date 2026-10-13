# Character Rebirth Classes, Stat Layout & Party Memory Architecture

## 1. Executive Summary & Architectural Overview

The Wonderland Online character progression model culminates in the **Rebirth Ascension System** (轉生系統), where characters reaching Level 100+ undergo ritual ascension, resetting their base level while unlocking one of six specialized prestige classes, distinctive job badges, unique combat passives, and exclusive skill trees.

Through reverse engineering of client subroutine `FUN_0028f90c` (RVA `0x0028f90c`) and party management subsystem `FUN_0028d5c4` in [`aLogin_decompiled.c`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c), the exact memory layout of active party members, class enumeration IDs, and GUI icon bindings have been extracted.

---

## 2. Rebirth Prestige Job Matrix (轉生職業體系)

When a character ascends, their Rebirth Class ID (`*(actor + 0x22)`) determines their class title, UI icon, and attribute scaling archetype:

| Job ID (`0x22`) | Class Name (EN) | Class Name (ZH) | Client Resource Sprite | Specialization Archetype | Primary Attribute Focus | Unique Combat Passives |
|---|---|---|---|---|---|---|
| `0` | Adventurer | 冒險者 | `(None)` | Base Unborn Character | Unspecialized | None |
| `1` | **Killer** | 殺手 | `Icon_Asn` | Physical Burst Assassin | ATK, SPD | +Crit Chance, Fatal Assault, Double Damage Burst |
| `2` | **Warrior** | 武士 | `Icon_Samurai` | Physical Tank & Counter | DEF, ATK | Physical Counter-Strike, Shield Block Mitigation |
| `3` | **Knight** | 騎士 | `Icon_Knight` | Mounted Mobility & Guardian | SPD, CON | Team Agility Aura, Mounted Speed Bonus, Sacrificial Cover |
| `4` | **Sage** | 智者 | `Icon_Sage` | Magic Striker & Disrupter | MATK, INT | Magic Defense Pierce, Status Effect Seal Resistance |
| `5` | **Priest** | 牧師 | `Icon_Clergyman` | Divine Support & Recovery | MDEF, WIS | Healing Multiplier Boost, Cleanse, Resurrect Efficiency |
| `6` | **Prophet** | 先知 | `Icon_Prophet` | Turn Order & Crowd Control | SPD, WIS | Turn Speed Priority Modifier, Extended Seal Duration |

---

## 3. Party Member Memory Layout (`PTR_DAT_004c9754`)

In [`aLogin.exe`](file:///D:/GitHub/ghidradecodeyapma/WLRI/aLogin.exe), party members and surrounding player entities are maintained in a global pointer table `PTR_DAT_004c9754[index]`. Subroutine `FUN_0028f90c` decodes party member fields using the following byte offsets:

```
+------------------+---------------------------------------------------------------+
| Byte Offset      | Data Type & Architectural Purpose                             |
+------------------+---------------------------------------------------------------+
| +0x04            | UInt32: Entity Pointer / Instance Handle                      |
| +0x08            | AnsiString: Character Name Pointer                            |
| +0x1F            | Byte: Team Slot Index / Formation Order (0..3)                |
| +0x20            | Byte: Character Level (1..200)                                |
| +0x21            | Byte: Elemental Affinity (1=Earth, 2=Water, 3=Fire, 4=Wind)   |
| +0x22            | Byte: Rebirth Job Class ID (0..6)                             |
| +0x24            | Int32: Current HP (Hit Points)                                |
| +0x28            | UInt16: Maximum HP                                            |
| +0x2A            | UInt16: Current SP (Skill Points / Mana)                      |
| +0x2C            | UInt16: Maximum SP                                            |
| +0x30            | UInt32: Secondary Combat Parameter / Gauge Ratio              |
| +0x34            | UInt32: Secondary Combat Parameter / Stance Gauge             |
| +0x38            | UInt16: Combat Action Charge Low Word                         |
| +0x3A            | UInt16: Combat Action Charge High Word                        |
| +0x4C            | Pointer: Rebirth Job Sprite Handle (Icon_Asn, Icon_Knight...) |
| +0xC4            | UInt32: Total Experience Points (EXP)                         |
| +0xC8 (+200)     | UInt32: Unique Character ID (CharID)                          |
| +0x11E           | Byte: Combat Animation State Index                            |
| +0x121           | Byte: Stance / Motion Action Code (0=Idle, 8=Walk, 18=Battle) |
+------------------+---------------------------------------------------------------+
```

---

## 4. Decompiled Class Resolution Routine (`FUN_0028f90c`)

The client resolves and renders the rebirth badge and class title dynamically via switch logic in `FUN_0028f90c`:

```c
// Decompiled from aLogin_decompiled.c lines 267306-267336
switch(job_id) {
case 1: // Killer
  icon_sprite = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "Icon_Asn");
  *(undefined4 *)(party_slot + 0x4c) = icon_sprite;
  FUN_00480ae4(party_slot, "Killer");
  break;
case 2: // Warrior
  icon_sprite = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "Icon_Samurai");
  *(undefined4 *)(party_slot + 0x4c) = icon_sprite;
  FUN_00480ae4(party_slot, "Warrior");
  break;
case 3: // Knight
  icon_sprite = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "Icon_Knight");
  *(undefined4 *)(party_slot + 0x4c) = icon_sprite;
  FUN_00480ae4(party_slot, "Knight");
  break;
case 4: // Sage
  icon_sprite = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "Icon_Sage");
  *(undefined4 *)(party_slot + 0x4c) = icon_sprite;
  FUN_00480ae4(party_slot, "Sage");
  break;
case 5: // Priest
  icon_sprite = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "Icon_Clergyman");
  *(undefined4 *)(party_slot + 0x4c) = icon_sprite;
  FUN_00480ae4(party_slot, "Priest");
  break;
case 6: // Prophet
  icon_sprite = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "Icon_Prophet");
  *(undefined4 *)(party_slot + 0x4c) = icon_sprite;
  FUN_00480ae4(party_slot, "Prophet");
  break;
}
```

---

## 5. Network Protocol Integration (Action Codes 8 & 14)

### 5.1 Rebirth Status Replication in Action Code 8 Sub-Opcode 2 (`AC 8:2`)
When character attributes synchronize, the server transmits stat records with field identifiers:
- Stat `0x011D`: Character Level
- Stat `0x0119`: Current HP / Max HP
- Stat `0x011A`: Current SP / Max SP
- Stat `0x0124`: Combat EXP
- Stat `0x0170`: Rebirth Status (`0` for normal, `1..6` for Rebirth Class)

### 5.2 Party Roster Packet (`AC 14:11` & `AC 14:5`)
Party member packets serialize 28 bytes per member, embedding `Level` at Byte 26, `Element` at Byte 27, and `RebirthJob` at Byte 28. Client parser `FUN_0028f90c` consumes these fields directly to populate `PTR_DAT_004c9754`.
