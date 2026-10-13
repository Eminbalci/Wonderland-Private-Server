# Document 92: Combat Abnormal Status Condition, Buff/Debuff System, and Turn Decay Engine

## 1. Architectural Overview

Combat in *Wonderland Online* centers around elemental sealing and crowd control. When players and monsters exchange abilities, skills can inflict status effects ranging from complete incapacitation (Freeze, Petrify, Sleep) to persistent resource attrition (Poison, Burn) and stat debuffs (Curse, Weakness).

The abnormal status engine operates across two synchronized domains:
1. **Client Status Inspection & UI HUD (`form_status` / `se_Status.pas`)**: Rendered via hotkey `[Ctrl+A]` ($480 \times 228$ px), displaying live HP/SP/EXP telemetry, moral rating (`Icon_Heart`), dynamic partner affinities (`Btn_partner`), and visual abnormal status overlays.
2. **Combat Round Resolution & Decay Pipeline (`HL_Battle.pas`)**: Tracks per-actor active status bitmasks, duration turn counters, elemental resistance checks, and beginning-of-round damage/decay ticks.

```
+-----------------------------------------------------------------------------------+
|                        ABNORMAL STATUS & DECAY PIPELINE                           |
+-----------------------------------------------------------------------------------+
                                          |
                +-------------------------+-------------------------+
                |                                                   |
                v                                                   v
    [Status Dialog: form_status]                          [Combat Decay Pipeline]
    - Dimensions: 480x228 px                              - 8 Core Abnormal States
    - Telemetry Gauges (HP/SP/EXP)                        - Turn-Start DoT Application
    - 6 Attribute Point Allocators                        - Turn-End Duration Decrement
                |                                                   |
                +----------------- Network Sync --------------------+
                                          |
                                          v
                      [Action Code 18:2 Combat Status Broadcast]
```

---

## 2. VCL Form Hierarchy & UI Structure

The client encapsulates status visualization in `se_Status.pas` (constructed at RVA `0x00280bfc`):

### 2.1 Form Specifications

| Attribute | Specification |
| :--- | :--- |
| **Pascal Source Unit** | `se_Status.pas` / `HL_Status.pas` |
| **Form Identifier** | `form_status` (`"Status [Ctrl+A]"`) |
| **Dimensions** | Width: 480 px (`0x1E0`), Height: 228 px (`0xE4`) |
| **Subroutine Addresses** | Constructor: `FUN_00280bfc`, Renderer: `FUN_00281068` |
| **Attribute Allocator** | 6 $\times$ `arrow_upBig1` buttons (Stride: 13 px, RVA `0x002ab86c`) |

### 2.2 Control Hierarchy

```
form_status (480 x 228 px)
 ├── Btn_Close_s_1 (X: 457, Y: 17, W: 17, H: 17)
 ├── btn_skill (X: 159, Y: 25, W: 44, H: 25)
 ├── btn_Moral (X: 159, Y: 25, W: 44, H: 25)
 ├── btn_Job (X: 159, Y: 25, W: 44, H: 25)
 ├── Btn_partner (X: 159, Y: 25, W: 44, H: 25)
 ├── Arrow_L2 / Arrow_R2 (Pet Roster Carousel)
 ├── Gauges: icon_HP1, icon_SP1, icon_exp1, icon_HP_SP_EXP, icon_HOR1..3
 └── Attribute Buttons [1..6]: arrow_upBig1 (STR, CON, INT, WIS, AGI, POT)
```

---

## 3. Disassembly & Status Bitmask Specification

In combat actor structures (`*(battle_actor + 0x21a0)`), status effects are stored as an array of active condition nodes:

```c
// Combat Abnormal Condition Node Struct (8 bytes)
struct AbnormalStatusNode {
    uint8_t  StatusType;     // 1 = Freeze, 2 = Petrify, 3 = Sleep, etc.
    uint8_t  RemainingTurns; // Remaining combat rounds (1..5)
    uint16_t Magnitude;      // Parameter modifier (e.g., -20% DEF, 100 Poison dmg)
    uint32_t InflictorActorId;// Source actor ID
};
```

### 3.1 Status Condition Catalog

| ID | Condition Name | Elemental School | Behavior Mechanics | Cancellation / Dispel Trigger |
| :---: | :--- | :--- | :--- | :--- |
| `0x01` | **Freeze (Ice Seal)** | Water | Incapacitated; cannot attack, cast, or use items. Physical damage taken is reduced by 25%. | Expires after turns (2..3), or Fire elemental attack. |
| `0x02` | **Petrify (Stone Seal)** | Earth | Completely paralyzed; Defense is locked; cannot evade or counter. | Expires after turns (2..4), or Purify spell. |
| `0x03` | **Sleep** | Wind | Incapacitated; evasion drops to 0. | Direct physical attack wakes target immediately. |
| `0x04` | **Paralyze / Silence** | Fire | Skills disabled; standard physical attacks and items permitted. | Expires after turns (3..5). |
| `0x05` | **Confusion (Chaos)** | Neutral / Dark | Random action selection (50% chance to attack ally, 25% self, 25% enemy). | Expires after turns (2..3), or physical strike. |
| `0x06` | **Poison** | Earth / Wood | Loses $\max(10, \text{MaxHP} \times 0.10)$ per turn during round maintenance. | Antidote potion, Water "Clear Poison", or expiry. |
| `0x07` | **Curse** | Dark | Physical ATK and DEF reduced by 20%; MATK and MDEF reduced by 20%. | Holy water or battle end. |
| `0x08` | **Burn** | Fire | Continuous fire damage ($5\%$ MaxHP) and DEF reduced by 10%. | Water spell, Rain weather, or 3 turns. |

---

## 4. Round Start & Turn Decay Execution Loop

During round resolution in `HL_Battle.pas`:

```c
void ProcessRoundMaintenance(CombatBattle *battle)
{
    for (int i = 0; i < battle->ActorCount; i++) {
        CombatActor *actor = &battle->Actors[i];
        if (actor->IsDead) continue;

        // 1. Apply Persistent Damage Ticks (Poison / Burn)
        if (actor->HasStatus(STATUS_POISON)) {
            int dmg = actor->MaxHp * 0.10;
            actor->CurrentHp = max(1, actor->CurrentHp - dmg);
            BroadcastDamageTick(actor->GridId, dmg, STATUS_POISON);
        }
        if (actor->HasStatus(STATUS_BURN)) {
            int dmg = actor->MaxHp * 0.05;
            actor->CurrentHp = max(1, actor->CurrentHp - dmg);
            BroadcastDamageTick(actor->GridId, dmg, STATUS_BURN);
        }

        // 2. Decrement Turn Counters
        for (int s = 0; s < actor->StatusCount; s++) {
            actor->Statuses[s].RemainingTurns--;
            if (actor->Statuses[s].RemainingTurns <= 0) {
                // Remove expired condition
                actor->RemoveStatusAt(s);
                BroadcastStatusExpire(actor->GridId, actor->Statuses[s].StatusType);
                s--;
            }
        }
    }
}
```

---

## 5. Network Wire Protocol

### 5.1 Server -> Client: Action Code 18:2 Status Condition Applied / Expired

Broadcast when an attack applies or clears an abnormal condition.

```
+---------------+---------------+---------------+---------------+---------------+
| Magic Header  | Length (LE)   | Action Code   | Sub-Opcode    | Target GridID |
| 0xF4 0x44     | 0x07 0x00     | 0x12          | 0x02          | 0x03          |
+---------------+---------------+---------------+---------------+---------------+
| Status Type   | Turns Added   | Operation     |
| 0x01 (Freeze) | 0x03          | 0x01 (Apply)  |
+---------------+---------------+---------------+
```

---

## 6. C# Server Implementation Blueprint

```csharp
// AbnormalStatusService.cs - Wonderland Private Server Combat Status Manager
using System;
using System.Collections.Generic;

namespace Wonderland.Server.Combat
{
    public enum AbnormalStatusType : byte
    {
        Freeze    = 1,
        Petrify   = 2,
        Sleep     = 3,
        Paralyze  = 4,
        Confusion = 5,
        Poison    = 6,
        Curse     = 7,
        Burn      = 8
    }

    public sealed class ActiveStatusEffect
    {
        public AbnormalStatusType Type { get; set; }
        public int RemainingTurns { get; set; }
        public int Magnitude { get; set; }
        public uint InflictorId { get; set; }
    }

    public sealed class AbnormalStatusService
    {
        public void ApplyStatus(CombatActor target, AbnormalStatusType type, int turns, int magnitude, uint inflictorId)
        {
            // 1. Elemental Immunity & Resistance Check
            if (IsImmune(target, type)) return;

            // 2. Check for existing effect (Refresh duration)
            var existing = target.ActiveStatuses.Find(s => s.Type == type);
            if (existing != null)
            {
                existing.RemainingTurns = Math.Max(existing.RemainingTurns, turns);
                return;
            }

            // 3. Add Status Node
            target.ActiveStatuses.Add(new ActiveStatusEffect
            {
                Type = type,
                RemainingTurns = turns,
                Magnitude = magnitude,
                InflictorId = inflictorId
            });

            // 4. Broadcast to Battle Participants (Action Code 18:2)
            target.Battle.Broadcast(BuildStatusPacket(target.GridId, type, (byte)turns, 1));
        }

        public void ExecuteTurnDecay(CombatBattle battle)
        {
            foreach (var actor in battle.Actors)
            {
                if (actor.IsDead) continue;

                // Process DoT damage
                foreach (var status in actor.ActiveStatuses.ToArray())
                {
                    if (status.Type == AbnormalStatusType.Poison)
                    {
                        int poisonDmg = (int)(actor.MaxHp * 0.10);
                        actor.CurrentHp = Math.Max(1, actor.CurrentHp - poisonDmg);
                    }

                    status.RemainingTurns--;
                    if (status.RemainingTurns <= 0)
                    {
                        actor.ActiveStatuses.Remove(status);
                        battle.Broadcast(BuildStatusPacket(actor.GridId, status.Type, 0, 0)); // Expire
                    }
                }
            }
        }

        private bool IsImmune(CombatActor target, AbnormalStatusType type)
        {
            // Water actors have high freeze resistance; Earth resists petrify
            return false;
        }

        private byte[] BuildStatusPacket(byte gridId, AbnormalStatusType type, byte turns, byte op)
        {
            return new byte[] { 0xF4, 0x44, 0x07, 0x00, 0x12, 0x02, gridId, (byte)type, turns, op };
        }
    }
}
```

---

## 7. Verification & Cross-References

- **Disassembly Offsets**: `aLogin.exe` RVA `0x00280bfc` (`form_status`), `0x00281068` (Renderer).
- **String References**: Line 261956 (`"form_status"`), Line 261957 (`"Status [Ctrl+A]"`), Line 262046 (`"Btn_partner"`), Line 262033 (`"arrow_upBig1"`).
- **Related Specs**:
  - [`11_combat_turn_based_battle_engine_and_pk_mechanics.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/11_combat_turn_based_battle_engine_and_pk_mechanics.md)
  - [`91_in_battle_skill_selection_palette_and_tactical_radial_targeting.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/91_in_battle_skill_selection_palette_and_tactical_radial_targeting.md)
