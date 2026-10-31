# Pure Debuff and Status Skill Damage Isolation & Status Lifecycle Engine

## 1. Overview & Operational Objective

In Wonderland Online (WLO), combat abilities are divided between:
1. Offensive Physical/Magical Attacks (`EffectLayer.Physical = 1`, `EffectLayer.Magical = 2`): Intended to deal direct base HP damage, apply elemental counters, trigger critical strikes, benefit from Hot-Blooded, and optionally apply secondary crowd control effects (e.g. Freeze Attack #15215, Fire Stone Attack #11035, Confusion Attack #25021).
2. Pure Status / Seal / Debuff Abilities (`EffectLayer.Seals1 = 3`, `EffectLayer.Seals2 = 15`, `EffectLayer.Debuffs1 = 5`, or item-based effects with `Target == 0`): Intended exclusively for crowd control (CC), stat reduction, or turn incapacitation (e.g., Tree Bind #15070, Terrafication #15035, Blockage #15187, Freezing Spell #11051, Sleep #12041, Mess Spell #11003, Cord Spell #15032, Coma Spell #11026, Poison Spell #11002, Curse #15243).

Previously, `PvEBattleManager.cs` routed all offensive and status actions into Phase 4 damage evaluation, where every ability executed `CalculateBaseDamage()`. Because non-physical/magical skills bypassed standard scaling, `CalculateBaseDamage()` applied a fallback formula `Math.Max(15, (int)(actor.Atk * 2.8) - target.Def / 2)`, inflicting massive direct HP damage upon the target. Furthermore:
- Targets suffered instant direct damage on pure debuff casts.
- The `AC 50:1` packet packed `0x19` (HP stat) and deducted target HP.
- Sleeping targets woken up immediately on the turn they were put to sleep because direct damage `totalDmg > 0` instantly broke the `Sleep` status condition.
- Skill EXP for non-damaging status abilities evaluated to minimum or inconsistent gains due to damage-dependent calculations.

This update resolves these defects by introducing `SkillData.IsPureStatusDebuff`, isolating pure debuffs from direct damage calculations, packing zero-damage / `statType = 0` animation records in `AC 50:1`, awarding support EXP via `CalculateSupportSkillExp(Buff)`, and maintaining proper sleep and CC lifecycle states.

---

## 2. Architecture & Data Flow

```
[ User Casts Battle Action ]
             |
             v
[ PvEBattleManager.HandleBattleAction ]
             |
     skill.IsPureStatusDebuff?
             |
     +-------+-------+
     | YES           | NO
     v               v
ActionType="status"  ActionType="attack"
     |               |
     v               v
[ Phase 4 Combat Dispatch ]
     |
     +-------+-------+
     |               |
[ IsPureDebuff ]  [ Offensive Attack ]
     |               |
     |-- finalDmg=0  |-- CalculateBaseDamage()
     |-- 0 HP loss   |-- Elemental / Crit / Hot-Blooded
     |-- statType=0  |-- finalDmg added to hitTargetsTotalDamage
     |-- Support EXP |-- Damage-based Skill EXP
     |-- CC Applied  |-- statType=0x19 (HP)
     |               |-- Breaks Sleep if totalDmg > 0
     |               |
     +-------+-------+
             |
             v
[ Broadcast AC 50:1 Packet ]
[ Retain Sleep / Freeze / Seal Status ]
```

---

## 3. Skill Classification & Qualification

### `SkillData.IsPureStatusDebuff` Specification

Defined in `wlo.pserver.core/Game/SkillRelated/SkillManager.cs`:

```csharp
public bool IsPureStatusDebuff =>
    Target == 0 && (
        EffectLayer == (byte)global::Game.EffectLayer.Seals1 ||
        EffectLayer == (byte)global::Game.EffectLayer.Seals2 ||
        EffectLayer == (byte)global::Game.EffectLayer.Debuffs1 ||
        (EffectLayer != (byte)global::Game.EffectLayer.Physical && EffectLayer != (byte)global::Game.EffectLayer.Magical &&
         (Effect == 1 || Effect == 3 || Effect == 4 || Effect == 6 || Effect == 7 || Effect == 8 ||
          Effect == 171 || Effect == 172 || Effect == 173 || Effect == 176 || Effect == 177 || Effect == 178 || Effect == 179))
    );
```

#### Qualification Matrix:

| Category | Skill ID Examples | EffectLayer | Target | IsPureStatusDebuff | Damage Dealt |
| :--- | :--- | :--- | :--- | :--- | :--- |
| Pure Seals / Traps | Tree Bind (#15070), Terrafication (#15035), Freezing Spell (#11051), Sleep (#12041), Cord Spell (#15032), Coma (#11026) | 3 (`Seals1`) | 0 (Enemy) | `true` | 0 |
| Pure Debuffs / Mess / Curse | Blockage (#15187), Mess Spell (#11003), Poison Spell (#11002), Curse (#15243), Slowdown (#11056), Shrink (#15188) | 15 (`Seals2`) | 0 (Enemy) | `true` | 0 |
| Banana Slip Debuff | Banana Skin (#10021) | 17 | 0 (Enemy) | `true` | 0 |
| Physical Attack w/ Debuff | Confusion Attack (#25021), Collapse (#15222), Curdle (#15225) | 1 (`Physical`) | 0 (Enemy) | `false` | Normal Physical Damage |
| Magical Attack w/ Debuff | Freeze Attack (#15215), Fire Stone Attack (#11035), Poisonous Wave (#25113) | 2 (`Magical`) | 0 (Enemy) | `false` | Normal Magical Damage |
| Ally Cures / Support | Wake Spell (#12043), Ice-out (#12048), Detoxification (#15100), Free From Mess (#12042) | 5 (`Debuffs1`) | 1/5 (Ally) | `false` | Support (ActionType="heal"/"buff") |

---

## 4. Combat Phase 4 Damage Isolation

### `CalculateBaseDamage` Guard
In `PvEBattleManager.cs`:
```csharp
if (skill.IsPureStatusDebuff)
    return 0;
```

### Action Dispatch Isolation
In `PvEBattleManager.cs`:
```csharp
bool isPureDebuff = a.ActionType == "status" || (sk != null && sk.IsPureStatusDebuff);

foreach (var target in hitTargets)
{
    int finalDmg = 0;
    bool isCritical = false;

    if (!isPureDebuff)
    {
        int baseDmg = CalculateBaseDamage(actor, target, a.SkillId, QuestNpc.NextRandom(1, 6));
        baseDmg = Math.Max(1, (int)(baseDmg * GetElementalMultiplier(actor.Element, target.Element)));

        if (actor.HasStatus(FighterStatusType.HotBlooded))
        {
            baseDmg = (int)(baseDmg * 2.0);
        }

        finalDmg = isCombo ? (int)(baseDmg * 1.25) : baseDmg;
        finalDmg = CriticalHitManager.CalculateDamage(finalDmg, actor, a.ActionType, QuestNpc.NextRandom(0, 100), out isCritical);

        if (defendingActors.Contains((target.GridX << 8) | target.GridY) || target.HasStatus(FighterStatusType.Shielded))
        {
            finalDmg = Math.Max(1, finalDmg / 2);
        }

        if (!hitTargetsTotalDamage.ContainsKey(target)) hitTargetsTotalDamage[target] = 0;
        hitTargetsTotalDamage[target] += finalDmg;
    }

    if (sk != null)
    {
        int turns = sk.NumberOfTurns > 0 ? sk.NumberOfTurns : 2;
        if (sk.IsFreeze) target.AddStatus(FighterStatusType.Frozen, turns);
        else if (sk.IsSleep) target.AddStatus(FighterStatusType.Sleep, turns);
        else if (sk.IsSeal) target.AddStatus(FighterStatusType.Sealed, turns);
        else if (sk.IsConfuse) target.AddStatus(FighterStatusType.Confused, turns);
        else if (sk.IsPoison) target.AddStatus(FighterStatusType.Poisoned, turns + 1);
        else if (sk.IsParalyze) target.AddStatus(FighterStatusType.Paralyzed, turns);
    }
```

### Packet Protocol Encoding (`AC 50:1`)
For each target in the animation packet:
- Pure Debuff:
  - `pAttackAnim.Pack8(0);` (statType 0: No HP reduction)
  - `pAttackAnim.Pack32(0);` (amount 0: 0 damage)
  - `pAttackAnim.Pack8(1);` (normal hit flag)
- Offensive Attack:
  - `pAttackAnim.Pack8(0x19);` (statType 0x19: HP damage)
  - `pAttackAnim.Pack32((uint)finalDmg);` (damage value)
  - `pAttackAnim.Pack8(isCritical ? (byte)2 : (byte)1);` (hit / critical flag)

### Sleep Status Integrity
Sleeping targets are only woken up when direct combat damage is inflicted:
```csharp
if (totalDmg > 0 && target.HasStatus(FighterStatusType.Sleep))
{
    target.ActiveStatuses.RemoveAll(s => s.StatusType == FighterStatusType.Sleep);
}
```
Because pure status debuffs record `finalDmg = 0` and do not increment `hitTargetsTotalDamage`, `totalDmg` is 0. Targets put to sleep remain asleep until damaged by a subsequent physical or magical attack.

---

## 5. Experience Awarding

For pure status debuffs:
- EXP is calculated via `SkillManager.CalculateSupportSkillExp(SupportSkillKind.Buff, 0, 0, 0)` which returns the standard baseline support EXP (`SupportExpPerUse + BuffBonusExp = 40`).
- The EXP value is then multiplied by `ServerStatusManager.ScaleExperience(expGain)` respecting `Config.Instance.ExpRate`.
- For offensive attacks, EXP continues using `CalculateSkillExpForHit(finalDmg, targetHpBefore, targetMaxHp)`.

---

## 6. Verification & Test Evidence

1. **Classification Verification:**
   - 92 authentic debuff skills from `Data/Skill.dat` verified: `IsPureStatusDebuff == true`.
   - Offensive abilities with secondary debuffs (Freeze Attack #15215, Fire Stone Attack #11035, Confusion Attack #25021) verified: `IsPureStatusDebuff == false`.
2. **Damage Verification:**
   - `CalculateBaseDamage` for #11051, #15070, #15035, #15187, #12041, #15032, #11026, #11003, #11002 evaluated to exactly `0`.
   - `CalculateBaseDamage` for #10001, #15215, #11035, #25021, #15222, #15225 evaluated to authentic positive values (> 200).
3. **Packet Byte-Level Output:**
   - Debuff target record: `F4-44-0B-00-01-00-01-00-01-00-00-00-00-00-01` (statType `00`, damage `00-00-00-00`).
   - Attack target record: `F4-44-0B-00-01-00-01-00-01-19-5E-01-00-00-01` (statType `19`, damage `5E-01-00-00` = 350).
4. **Sleep Lifecycle:**
   - Sleep cast applies `FighterStatusType.Sleep`, `CanAct == false`.
   - Sleep cast does not wake target up.
   - Subsequent attack dealing 150 damage wakes target up (`HasStatus(Sleep) == false`).
