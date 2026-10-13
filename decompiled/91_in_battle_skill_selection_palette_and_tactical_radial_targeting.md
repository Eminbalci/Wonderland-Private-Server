# Document 91: In-Battle Skill Selection Palette, SP Calculation, and Tactical Radial Targeting Engine

## 1. Architectural Overview

During turn-based combat encounters in *Wonderland Online*, players execute offensive, defensive, restorative, and status-sealing techniques via the **Battle Skill Selection Palette** (`Form_BattleSkill_1`) and tactical targeting overlay (`form_skillexpress`). When the client enters the player command phase, `HL_Battle.pas` initializes the skill dialog, populating available abilities from the actor's learned skill tree registers and currently equipped catalytic gear.

The in-battle skill engine encompasses three distinct responsibilities:
1. **Dynamic Skill Registry Enumeration**: Iterating over 500 potential skill IDs (`0x001 - 0x1F4`) stored at local actor memory offsets `*(actor + 0x30cf + id * 5)` and hotkey equipment slots (`15000 + id * 2`).
2. **Resource Consumption & Prerequisite Gating**: Querying `Skill.dat` (record stride 148 bytes, `0x94`) to determine SP consumption, current turn cooldowns, elemental affinity alignment, and weapon catalyst constraints.
3. **Tactical Targeting Geometries**: Computing target selection matrices (Single Target, 2-Target Column, 2-Target Row, 4-Target Cross, 9-Target Full Team) and passing target vectors to Action Code 17 (`0x11`) command packets.

```
+-----------------------------------------------------------------------------------+
|                        IN-BATTLE SKILL ARCHITECTURE                               |
+-----------------------------------------------------------------------------------+
                                          |
                +-------------------------+-------------------------+
                |                                                   |
                v                                                   v
    [Skill Palette: Form_BattleSkill_1]                   [Targeting Engine: form_skillexpress]
    - Dimensions: 353x274 px                              - Tactical Target Bounding Box
    - 500 Learned Skill Slots                             - Single / Column / Row / Cross / Team
    - SP Cost & Cooldown Validation                       - Radial Reticle & Sound Feedback
                |                                                   |
                +----------------- Action Dispatch -----------------+
                                          |
                                          v
                      [Action Code 17:1 Combat Command Packet]
```

---

## 2. VCL Form Hierarchy & UI Structure

The battle skill palette is defined in `se_BattleSkill.pas` and dynamically centered over the combat backbuffer using client resolution registers `PTR_DAT_004c96d0 + 0x2d0`.

### 2.1 Form Specifications

| Attribute | Specification |
| :--- | :--- |
| **Pascal Source Unit** | `se_BattleSkill.pas` / `HL_Battle.pas` |
| **Form Identifier** | `Form_BattleSkill_1` |
| **Dimensions** | Width: 353 px (`0x161`), Height: 274 px (`0x112`) |
| **Subroutine Addresses** | Constructor: `FUN_002b5b00` (RVA `0x002b5b00`), Populator: `FUN_002b5bd4` |
| **Parent Component** | `TBattleManager` (`PTR_DAT_004c94b0`) |
| **Targeting Overlay** | `form_skillexpress` (RVA `0x002b6828`) |

### 2.2 Control Hierarchy

```
Form_BattleSkill_1 (353 x 274 px)
 ├── Btn_ArrowUp_1 (X: 130, Y: 17, W: 17, H: 17, Handler: LAB_002b6270)
 ├── Btn_ArrowDn_1 (X: 130, Y: 292, W: 17, H: 17, Handler: LAB_002b6204)
 ├── Btn_Close_1 (X: 111, Y: 20, W: 56, H: 20, Handler: LAB_002b62dc)
 ├── TSkillListView_1 (Offset 0x3d, Rect: [X: 17, Y: 42, W: 81, H: 242], Stride: 25 px)
 └── TSkillListView_2 (Offset 0x3e, Rect: [X: 17, Y: 151, W: 137, H: 242], Stride: 25 px)
```

---

## 3. Disassembly & Algorithmic Analysis

### 3.1 Learned Skill Enumeration (`FUN_002b5bd4`)

When `Form_BattleSkill_1` opens, `FUN_002b5bd4` iterates through the actor's skill state:

```c
// Decompiled from aLogin.exe: FUN_002b5bd4 @ 0x002b5bd4
void FUN_002b5bd4(int param_1)
{
    int iVar8;
    char cVar3;
    undefined *puVar2 = PTR_DAT_004c9918;

    // Clear previous skill list items
    (**(code **)(**(int **)(param_1 + 0xf8) + 0x84))();

    // Check if character is currently in player turn (Offset 0xd00)
    cVar3 = *(char *)(*(int *)(*(int *)PTR_DAT_004c94b0 + 0x1a0 +
                      *(char *)(*(int *)PTR_DAT_004c94b0 + 0xd00) * 4) + 0xb1);
    if (cVar3 == '\x01') {
        // Iterate through 500 skill IDs (0x01 to 0x1F4)
        for (iVar8 = 1; iVar8 < 501; iVar8++) {
            // Verify if player has unlocked this skill level (non-zero byte)
            if (*(char *)(*(int *)puVar2 + 0x30cf + iVar8 * 5) != '\0') {
                // Query Skill Database entry from PTR_DAT_004c9514 (Skill.dat)
                uVar6 = FUN_0049b258(*(undefined4 *)PTR_DAT_004c9514, iVar8);
                if (FUN_0049b32c(*(undefined4 *)PTR_DAT_004c9514, uVar6) != '\0') {
                    uVar7 = FUN_0049a7c8(*(undefined4 *)PTR_DAT_004c9514, uVar6);
                    // Add skill entry to UI list view: Name, Icon, Current Level
                    FUN_002b4d00(param_1,
                        *(undefined2 *)(*(int *)PTR_DAT_004c9514 + 0x1a + (uVar7 & 0xffff) * 0x94),
                        *(undefined1 *)(*(int *)puVar2 + 0x30cf + iVar8 * 5),
                        0, 0xffffffff);
                }
            }
        }

        // Secondary Pass: Iterate equipment/accessory catalyst skills
        for (iVar8 = 1; iVar8 < 20; iVar8++) {
            short sVar5 = *(short *)(*(int *)puVar2 + 15000 + iVar8 * 2);
            if (sVar5 != 0) {
                // Append equipment-granted combat skill
                uVar7 = FUN_0049a7c8(*(undefined4 *)PTR_DAT_004c9514, sVar5);
                FUN_002b4d00(param_1, sVar5, 1, 1, 0xffffffff);
            }
        }
    }
}
```

### 3.2 Targeting Geometry Matrix

Skills in `Skill.dat` declare a 1-byte target pattern code (`TargetType` at offset `+0x24`):

| Target Code | Geometry Name | Description | Grid Representation ($2 \times 4$) |
| :---: | :--- | :--- | :---: |
| `0x01` | **Single** | 1 target anywhere on grid | `[ ] [X] [ ] [ ]` |
| `0x02` | **Column** | 2 vertical slots (Front + Back) | `[X] [ ] [ ] [ ]`<br>`[X] [ ] [ ] [ ]` |
| `0x03` | **Row** | 4 horizontal slots across front or back | `[X] [X] [X] [X]` |
| `0x04` | **Cross** | 4 targets in diamond pattern | `[ ] [X] [ ] [ ]`<br>`[X] [X] [X] [ ]` |
| `0x05` | **Full Team** | All 8 enemy or friendly grid positions | `[X] [X] [X] [X]`<br>`[X] [X] [X] [X]` |
| `0x06` | **Self Only** | Caster only (Heal, Barrier, Shield) | `[Caster]` |

---

## 4. Network Wire Protocol

### 4.1 Client -> Server: Action Code 17:1 Combat Skill Execution

Dispatched when the player selects a skill and confirms the target coordinate.

```
+---------------+---------------+---------------+---------------+---------------+
| Magic Header  | Length (LE)   | Action Code   | Sub-Opcode    | Caster GridID |
| 0xF4 0x44     | 0x08 0x00     | 0x11          | 0x01          | 0x02          |
+---------------+---------------+---------------+---------------+---------------+
| Skill ID (LE) | Target GridID | Target Pattern| Reserved      |
| 0x2A 0x00     | 0x05          | 0x02 (Column) | 0x00          |
+---------------+---------------+---------------+---------------+
```

### 4.2 Server -> Client: Action Code 18:1 Skill Animation & Damage Broadcast

Broadcast to all battle participants to animate the attack and apply HP/SP updates.

```
+---------------+---------------+---------------+---------------+---------------+
| Magic Header  | Length (LE)   | Action Code   | Sub-Opcode    | Caster GridID |
| 0xF4 0x44     | 0x10 0x00     | 0x12          | 0x01          | 0x02          |
+---------------+---------------+---------------+---------------+---------------+
| Skill ID (LE) | Hit Count     | Target GridID | Damage/Heal   | Status Applied|
| 0x2A 0x00     | 0x01          | 0x05          | 0x84 0x03     | 0x00 (None)   |
+---------------+---------------+---------------+---------------+---------------+
```

---

## 5. C# Server Implementation Blueprint

```csharp
// CombatSkillService.cs - Wonderland Private Server In-Battle Skill Engine
using System;
using System.Collections.Generic;

namespace Wonderland.Server.Combat
{
    public enum SkillTargetGeometry : byte
    {
        Single   = 1,
        Column   = 2,
        Row      = 3,
        Cross    = 4,
        FullTeam = 5,
        SelfOnly = 6
    }

    public sealed class CombatSkillService
    {
        public bool ValidateAndExecuteSkill(
            CombatActor caster,
            ushort skillId,
            byte targetGridId,
            out List<CombatHitResult> results)
        {
            results = new List<CombatHitResult>();

            // 1. Query Skill Template
            var skill = SkillDatabase.Get(skillId);
            if (skill == null) return false;

            // 2. Validate SP Resource
            if (caster.CurrentSp < skill.SpCost)
                return false;

            caster.CurrentSp -= skill.SpCost;

            // 3. Resolve Target Grid Positions based on TargetGeometry
            var targetPositions = ResolveTargets(skill.TargetGeometry, targetGridId);

            // 4. Compute Damage / Healing per Target
            foreach (var pos in targetPositions)
            {
                var target = caster.Battle.GetActorAt(pos);
                if (target == null || target.IsDead) continue;

                int damage = CalculateDamage(caster, target, skill);
                target.CurrentHp = Math.Max(0, target.CurrentHp - damage);

                results.Add(new CombatHitResult
                {
                    TargetGridId = pos,
                    Damage = damage,
                    IsCritical = caster.RollCritical()
                });
            }

            return true;
        }

        private List<byte> ResolveTargets(SkillTargetGeometry geom, byte primaryTarget)
        {
            var targets = new List<byte> { primaryTarget };
            if (geom == SkillTargetGeometry.Column)
            {
                byte opposite = (byte)(primaryTarget < 4 ? primaryTarget + 4 : primaryTarget - 4);
                targets.Add(opposite);
            }
            return targets;
        }

        private int CalculateDamage(CombatActor caster, CombatActor target, SkillTemplate skill)
        {
            // Wonderland Combat Formula: Base + (MATK/ATK * Ratio) - (Target MDEF/DEF)
            int power = skill.IsMagic ? caster.Matk : caster.Atk;
            int defense = skill.IsMagic ? target.Mdef : target.Def;
            int rawDmg = (int)((power * skill.Multiplier) - (defense * 0.75));
            return Math.Max(1, rawDmg);
        }
    }

    public sealed class CombatHitResult
    {
        public byte TargetGridId { get; set; }
        public int Damage { get; set; }
        public bool IsCritical { get; set; }
    }
}
```

---

## 6. Verification & Cross-References

- **Disassembly Offsets**: `aLogin.exe` RVA `0x002b5b00` (`Form_BattleSkill_1`), `0x002b5bd4` (Skill Enumerator), `0x002b6828` (`form_skillexpress`).
- **Memory Offsets**: Learned skills array `*(puVar2 + 0x30cf + id * 5)`, Equipment skills `*(puVar2 + 15000 + id * 2)`.
- **String References**: Line 284330 (`"Form_BattleSkill_1"`), Line 284335 (`"Btn_ArrowUp_1"`), Line 284344 (`"Btn_ArrowDn_1"`), Line 286828 (`"form_skillexpress"`).
- **Related Specs**:
  - [`11_combat_turn_based_battle_engine_and_pk_mechanics.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/11_combat_turn_based_battle_engine_and_pk_mechanics.md)
  - [`36_skill_tree_affinities_and_elemental_mastery_progression.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/36_skill_tree_affinities_and_elemental_mastery_progression.md)
  - [`90_milestone_capstone_3_master_opcode_protocol_and_packet_cryptography_handbook.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/90_milestone_capstone_3_master_opcode_protocol_and_packet_cryptography_handbook.md)
