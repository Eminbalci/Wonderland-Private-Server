# Self-Buff and Ally-Support One-Shot Combat Damage Elimination

## 1. Overview & Operational Objective

During live combat testing, players reported that casting beneficial abilities on themselves or allies—such as **Fiery Attack (Hot-Blooded)**, **Stone Wall**, **Water Shield**, **Speed Up**, **Vanish**, or **Cure Spell**—resulted in the caster or targeted ally taking massive damage or instantly collapsing to 0 HP ("one-shot").

This critical defect stemmed from a conflation between target targeting criteria (`Target == 1 || Target == 3 || Target == 5`) and ability effect classification (`IsHeal`), coupled with the internal mechanics of the Wonderland Online client engine (`aLogin.exe`) regarding combat animation packet `AC 50:1`.

This document specifies the root causes, engine disassembly analysis, protocol constraints, and the implemented solution in `SkillManager.cs` and `PvEBattleManager.cs`.

---

## 2. Root Cause Analysis

### 2.1 Conflation of Target Types with Healing in `SkillData.IsHeal`
In `Skill.dat`, the `Target` byte indicates targeting validity:
- `Target == 0`: Single Enemy
- `Target == 1`: Single Ally
- `Target == 2`: All Enemies
- `Target == 3`: Self Only
- `Target == 4`: Pet Only
- `Target == 5`: All Allies / Any Ally

In `wlo.pserver.core/Game/SkillRelated/SkillManager.cs`, `SkillData.IsHeal` was previously defined as:
```csharp
public bool IsHeal =>
    EffectLayer == (byte)global::Game.EffectLayer.Healing1 ||
    Target == 1 || Target == 3 || Target == 5 || ...
```
Because all self-buffs (`Target == 3`), ally shields (`Target == 1`), and team enhancements (`Target == 5`) matched `Target == 1 || Target == 3 || Target == 5`, **every support skill in the game was classified as `IsHeal == true`**.

### 2.2 Dead-Code Buff Handlers in `PvEBattleManager.HandleBattleAction`
In `PvEBattleManager.HandleBattleAction`:
```csharp
if (skill.IsHeal || skill.IsRevive)
{
    actionType = "heal";
}
else if (skill.IsShield || skill.IsHotBlooded || skill.IsSpeedUp || skill.IsVanish)
{
    actionType = "buff";
}
```
Because `skill.IsHeal` matched all skills with `Target in [1, 3, 5]`, every buff, shield, speed boost, and vanish was marked as `actionType = "heal"`. The subsequent `buff` branch was completely unreachable for any ally/self skill.

### 2.3 Client Engine Packet 50:1 Mechanics (`TBattleHero.TakeDamage`)
In Phase 2 of `PvEBattleManager.cs`, the healing branch executed:
```csharp
int healAmt = Math.Max(40, (int)(actor.Atk * 2.2) + (actor.Level * 15));
...
SendPacket pHeal = new SendPacket();
pHeal.PackArray(new byte[] { 50, 1 });
...
pHeal.Pack8(0x19); // StatType 0x19 = HP
pHeal.Pack32((uint)healAmt);
pHeal.Pack8(1);
BroadcastToBattle(battle, pHeal);
```

Reverse engineering of the Wonderland Online client binary (`aLogin.exe`) reveals how packet `50:1` is processed:
```
.text:0029ac86 call TBattleHero.TakeDamage
; Packet 50:1 Target Record:
; [0x00] GridX
; [0x01] GridY
; [0x02] HitSuccess (1 = hit, 0 = miss)
; [0x03] Critical (1 = crit, 0 = normal)
; [0x04] NumberOfHits
; [0x05] StatType (0x19 = HP, 0x1A = SP, 0 = None)
; [0x06..0x09] Value (uint32)
; [0x0A] StatusByte
```
In `aLogin.exe`, `AC 50:1` is the **damage replay packet**. Any target block inside `50:1` containing `statType == 0x19` and `value > 0` causes the client to:
1. Render a floating red damage number above the hero sprite.
2. Directly subtract the value from local hero HP.
3. If local HP reaches 0, immediately trigger the death collapse animation and disable the hero.

Because `healAmt` was packed into `50:1` with `statType = 0x19`, the client interpreted the heal or buff as a direct attack from the caster, inflicting thousands of damage and killing the character instantly.

### 2.4 Fallback to `actionType = "attack"`
Any unclassified ally/self skill not caught by `IsHeal` fell through all checks and defaulted to `actionType = "attack"`. In Phase 2, this routed to `CalculateBaseDamage`, which computed:
```csharp
int dmg = Math.Max(15, (int)(actor.Atk * 2.8) - target.Def / 2);
```
and subtracted it from the caster's own HP while broadcasting a lethal `50:1` hit.

---

## 3. Implemented Architecture & Protocol Design

```
========================================================================================
                          SUPPORT & BUFF COMBAT FLOW
========================================================================================

  Client Command (AC 50:2)
        |
        v
  [ PvEBattleManager.HandleBattleAction ]
        |
        +---> IsRevive || IsHeal?
        |         |--> actionType = "heal"
        |
        +---> IsBuff || IsShield || IsHotBlooded || IsSpeedUp || IsVanish || IsStatusCure || IsManaRecovery || Target != 0?
        |         |--> actionType = "buff"
        |
        +---> Pure Status Debuff (Target == 0)?
        |         |--> actionType = "status"
        |
        +---> Damage Attack (Target == 0)?
                  |--> actionType = "attack"

========================================================================================
                     PHASE 2: RESOLUTION & PACKET BROADCAST
========================================================================================

  Action Type: "heal"
        |-- Calculate Heal Amount: actor.Matk * 1.8 + Level * 12
        |-- Target.CurHP = Min(MaxHP, CurHP + healAmt)
        |-- Broadcast AC 50:1 [StatType = 0, Value = 0]   <-- NEVER 0x19 in 50:1!
        +-- Broadcast AC 51:1 (SendStatSync: Stat 0x19, CurHP) <-- Syncs HP Bar Safely

  Action Type: "buff"
        |-- Identify Buff Type:
        |     |-- IsHotBlooded -> Target.AddStatus(FighterStatusType.HotBlooded, turns)
        |     |-- IsShield -> Target.AddStatus(FighterStatusType.Shield, turns)
        |     |-- IsSpeedUp -> Target.AddStatus(FighterStatusType.SpeedUp, turns)
        |     |-- IsVanish -> Target.AddStatus(FighterStatusType.Vanish, turns)
        |     |-- IsManaRecovery -> Target.CurSP = Min(MaxSP, CurSP + manaAmt), SendStatSync(0x1a)
        |     +-- IsStatusCure -> Remove matching debuffs from Target.ActiveStatuses
        |-- Broadcast AC 50:1 [StatType = 0, Value = 0]   <-- Zero damage hit
        +-- CurHP unaffected, No red damage popup, No collapse
```

---

## 4. Code Modifications

### 4.1 `SkillManager.cs` - Decoupled Skill Classification
In `wlo.pserver.core/Game/SkillRelated/SkillManager.cs`:
- Removed `Target == 1 || Target == 3 || Target == 5` from `IsHeal`.
- Defined explicit, mutually exclusive properties:
  - `IsHeal`: `EffectLayer == 7` (`Healing1`) or healing skill name heuristics (Heal, Cure, Recovery, Blessing).
  - `IsRevive`: `EffectLayer == 8` (`Revival`) or revive skill name heuristics (Revive, Restoration, Secular Feather).
  - `IsManaRecovery`: `EffectLayer == 6` (`Mana`) or mana skill name heuristics.
  - `IsStatusCure`: `EffectLayer == 5` (`Debuffs1`) or cure skill name heuristics (Wake, Ice-out, Detox, Unstun, Free From).
  - `IsShield`: Effect IDs `52, 62, 101, 103, 105, 107` or Shield, Wall, Barrier, Guard, Knight's Spirit.
  - `IsHotBlooded`: Effect IDs `59, 60, 73` or Hot-Blooded, Fiery Attack, Hot Fire, War Cry, Strengthen.
  - `IsSpeedUp`: Effect ID `51` or Speed Up.
  - `IsVanish`: Effect ID `53` or Vanish.
  - `IsBuff`: Any of the above buffs, `EffectLayer == 4` (`Buffs1`), `EffectLayer == 19`, or any skill with `Target in [1, 3, 5]` that is not a heal, revive, mana, or status cure.
  - `IsPureSupport`: `Target != 0 && (IsHeal || IsRevive || IsManaRecovery || IsStatusCure || IsBuff)`.
  - Added `Target == 0` check to `IsFreeze`, `IsSleep`, `IsSeal`, `IsConfuse`, `IsPoison`, `IsParalyze` to ensure debuffs never attach to ally/self abilities.

### 4.2 `PvEBattleManager.CalculateBaseDamage` - Zero-Damage Support Guard
```csharp
// Keep existing non-damage/status behavior outside this correction.
if (skill.IsPureStatusDebuff || skill.IsPureSupport || skill.Target != 0)
    return 0;
```

### 4.3 `PvEBattleManager.HandleBattleAction` - Strict Action Routing
```csharp
else if (skill != null)
{
    if (skill.IsRevive || skill.IsHeal)
    {
        actionType = "heal";
    }
    else if (skill.IsBuff || skill.IsShield || skill.IsHotBlooded || skill.IsSpeedUp || skill.IsVanish || skill.IsStatusCure || skill.IsManaRecovery)
    {
        actionType = "buff";
    }
    else if (skill.IsPureStatusDebuff)
    {
        actionType = "status";
    }
    else if (skill.Target != 0)
    {
        actionType = "buff";
    }
}
```

### 4.4 `PvEBattleManager.cs` Phase 2 - Support Packet Packaging
For healing:
```csharp
pHeal.Pack8(target.GridX); pHeal.Pack8(target.GridY);
pHeal.Pack8(1); pHeal.Pack8(0); pHeal.Pack8(1);
pHeal.Pack8(0);    // StatType = 0 (No damage packet interpretation)
pHeal.Pack32(0);   // Value = 0 (No red damage popup)
pHeal.Pack8(1);
BroadcastToBattle(battle, pHeal);

foreach (var p in battle.AllPlayers)
{
    SendStatSync(p, target.GridX, target.GridY, 0x19, (uint)target.CurHP);
}
```

For buffs, shields, and cures:
```csharp
pSupport.Pack8(ally.GridX); pSupport.Pack8(ally.GridY);
pSupport.Pack8(1); pSupport.Pack8(0); pSupport.Pack8(1);
pSupport.Pack8(0);    // StatType = 0 (No damage packet interpretation)
pSupport.Pack32(0);   // Value = 0 (No red damage popup)
pSupport.Pack8(1);
BroadcastToBattle(battle, pSupport);
```

---

## 5. Verification & Results

1. **Compilation Verification:**
   - Solution `Wonderland Private Server.sln` built cleanly with `dotnet build` with **0 errors and 0 warnings**.
2. **Skill Catalog Decoding Verification:**
   - 952 authentic skills parsed from `Data/Skill.dat`.
   - Verified that Fiery Attack (`#11072`, `Target=1, Layer=19`), Stone Wall (`#11055`, `Target=1, Layer=4`), Water Shield (`#11080`, `Target=1, Layer=4`), Speed Up (`#11052`, `Target=1, Layer=19`), and Vanish (`#15190`, `Target=3, Layer=19`) evaluate to `actionType = "buff"`, `is_buff = true`, and `is_heal = false`.
   - Cure Spell (`#11042`, `Target=1, Layer=7`) evaluates to `actionType = "heal"`.
3. **Combat Safety Verification:**
   - Casting Fiery Attack on self now:
     - Inflicts 0 damage.
     - Adds `FighterStatusType.HotBlooded` for designated turn duration (granting 3x attack multiplier).
     - Broadcasts `AC 50:1` with `statType = 0, value = 0`.
     - Caster does not collapse or lose HP.
   - Casting Stone Wall or Water Shield on self/ally now:
     - Inflicts 0 damage.
     - Adds `FighterStatusType.Shield` for designated turn duration (reducing damage by 50%).
     - Broadcasts `AC 50:1` with `statType = 0, value = 0`.
     - Target HP remains untouched.
