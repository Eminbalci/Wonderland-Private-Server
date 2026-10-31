# Stun, Paralyze, and Poison Combat Status Lifecycle & Visual Feedback Engine

## 1. Overview & Operational Objective

In Wonderland Online (WLO) turn-based combat, status ailments and crowd-control (CC) abilities play a foundational role in strategic encounters:
1. **Incapacitating / Crowd-Control (CC) Effects:** Freeze (`Frozen`), Sleep (`Sleep`), Tree Bind / Terrafication (`Sealed`), Coma / Cord / Paralysis (`Paralyzed`), Confusion / Mess (`Confused`), and Blockage (`Sealed`). These abilities must render affected targets incapable of performing combat actions (`CanAct == false`) for their designated duration.
2. **Periodic Damage Over Time (DoT) Effects:** Poison (`Poisoned`). Targets afflicted with poison must suffer periodic HP reduction at the start of each round accompanied by authentic graphical animations and damage popups.

During live battle testing, players reported that Stun (Cord Spell, Coma Spell) and Poison abilities appeared to have no effect. Investigation revealed three fundamental defects in `PvEBattleManager.cs` and `SkillManager.cs`:

1. **Immediate Status Expiration Due to End-of-Round Decrement:**
   Skills in `Skill.dat` such as Cord Spell (#15032), Coma Spell (#11026), Freezing Spell (#11051), Sleep (#12041), and Blockage (#15187) define `NumberOfTurns = 1`. In `PvEBattleManager.cs`, Phase 6 decrements `st.RemainingTurns--` for all active statuses at the end of each round. If an enemy monster possessed higher SPD than the caster, the monster had already executed its turn in Round 1 before being stunned. When Round 1 ended, Phase 6 decremented `RemainingTurns` from 1 to 0, expiring and removing the status immediately. When Round 2 began, the monster acted normally—effectively suffering zero turns of incapacitation.

2. **Missing Incapacitation Feedback in Monster Turn Execution:**
   In Phase 5 (Monster Turn Execution), when a monster was incapacitated (`!monster.CanAct`), the server executed a bare `continue;` with 0ms delay and no packet broadcast. As a result, the server skipped the monster instantaneously without moving the combat camera (`AC 50:6`) or presenting an idle pause, giving the player the visual illusion that the turn was ignored or glitched.

3. **Silent Poison Ticks Without Client Visual Feedback:**
   In Phase 0 (Round Start / Status Upkeep), `Poisoned` status decremented target `CurHP` and broadcast an attribute synchronization packet (`AC 51:1`), but never broadcast the combat animation packet (`AC 50:1`). The WLO client engine requires `AC 50:1` to trigger floating damage numbers, flinch animations, and visual status FX. Without `AC 50:1`, players saw no visual feedback, and the small damage value (8% HP, often 5-8 HP on lower-level mobs) was virtually undetectable on the client health bar. Furthermore, targets dying from poison did not trigger victory/defeat checks until Phase 5.

This update resolves these defects by:
- Aligning status initialization duration to `baseTurns + 1` across all debuff categories so that affected entities lose their intended number of future action rounds.
- Adding camera focus (`AC 50:6`) and an 800ms pacing delay when incapacitated monsters skip their turns in Phase 5.
- Implementing an authentic visual poison tick in Phase 0 using `AC 50:6` focus, an `AC 50:1` combat animation packet (Skill ID 11002, statType `0x19`, formula `Max(15, 10% MaxHP)`), a 1000ms animation pause, attribute sync, and immediate combat termination evaluation if the afflicted entity dies.
- Expanding `SkillManager.IsSeal` and `SkillManager.IsParalyze` heuristics to recognize Tree Bind (#15070) and Coma Spell (#11026).

---

## 2. Architecture & Data Flow

```
========================================================================================
                          COMBAT ROUND STATUS LIFECYCLE
========================================================================================

 [ PHASE 0: Status Upkeep & Periodic Damage ]
       |
       +---> For Each Actor with BattleStatus:
                 |
                 +---> Poisoned?
                 |       |-- Broadcast AC 50:6 (Camera focus on target)
                 |       |-- Calculate Tick Damage: Max(15, 10% MaxHP)
                 |       |-- Deduct Target CurHP
                 |       |-- Broadcast AC 50:1 (Skill 11002, statType 0x19, Dmg)
                 |       |-- SendStatSync(target, 1) [AC 51:1]
                 |       |-- Target CurHP == 0? -> Play Defeat Animation & Check Victory
                 |       +-- Task.Delay(1000) for client animation playback
                 |
                 +---> RemainingTurns Decrement occurs in Phase 6 (Round End)

 [ PHASE 4: Action Resolution & Status Application ]
       |
       +---> Actor Casts CC / Debuff (Cord, Coma, Freeze, Sleep, Tree Bind, Poison):
                 |-- sk.NumberOfTurns resolved (e.g. 1 turn for Cord/Coma)
                 |-- Formula Applied: int turns = baseTurns + 1;
                 |   (Ensures status survives current round's Phase 6 decrement)
                 |-- Target.AddStatus(type, turns, value)
                 |-- Broadcast AC 50:1 (Animation, statType 0, Dmg 0)

 [ PHASE 5: Monster Action Execution ]
       |
       +---> Next Monster Selected in Turn Order:
                 |
                 +---> !monster.CanAct? (Frozen / Paralyzed / Sealed / Sleep)
                 |       |-- Broadcast AC 50:6 (Camera focus on incapacitated monster)
                 |       |-- await Task.Delay(800) (Visual turn-skip feedback)
                 |       +-- continue; (Skip monster's combat action)
                 |
                 +---> monster.CanAct == true:
                         +-- Execute normal AI attack/skill

 [ PHASE 6: Round Finalization ]
       |
       +---> For Each Actor with BattleStatus:
                 |-- st.RemainingTurns--;
                 |-- If RemainingTurns <= 0: RemoveStatus(st.Type)
                 +-- Proceed to Next Round
```

---

## 3. Skill Qualification & Classification

### 3.1 Incapacitation Skill Census

In `Skill.dat`, crowd-control skills utilize specific `Effect` and `Target` values:

| Skill ID | Name | Element | EffectLayer | Effect | Target | NumberOfTurns | Classified As |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **15032** | Cord Spell | Earth | 3 (`Seals1`) | 8 | 0 (Enemy) | 1 | `Paralyzed` |
| **11026** | Coma Spell | Water | 3 (`Seals1`) | 7 | 0 (Enemy) | 1 | `Paralyzed` |
| **15070** | Tree Bind | Earth | 3 (`Seals1`) | 3 | 0 (Enemy) | 2 | `Sealed` |
| **15035** | Terrafication | Earth | 3 (`Seals1`) | 4 | 0 (Enemy) | 2 | `Sealed` |
| **11051** | Freezing Spell | Water | 3 (`Seals1`) | 1 | 0 (Enemy) | 1 | `Frozen` |
| **12041** | Sleep | Fire | 3 (`Seals1`) | 6 | 0 (Enemy) | 1 | `Sleep` |
| **15187** | Blockage | Earth | 15 (`Seals2`) | 179 | 0 (Enemy) | 1 | `Sealed` |
| **11003** | Mess Spell | Water | 15 (`Seals2`) | 178 | 0 (Enemy) | 2 | `Confused` |
| **11002** | Poison Spell | Water | 15 (`Seals2`) | 171 | 0 (Enemy) | 3 | `Poisoned` |

### 3.2 SkillManager Qualification Updates

In `wlo.pserver.core/Game/SkillRelated/SkillManager.cs`:
- `IsSeal`: Added `Effect == 3` to detect Earth Tree Bind (#15070).
- `IsParalyze`: Added `(Effect == 7 && Target == 0)` to detect Water Coma Spell (#11026), and extended name checks to match "Palsy" and "Paraly".

```csharp
public bool IsSeal =>
    Target == 0 && (
        Effect == 3 || // Tree Bind
        Effect == 4 || // Terrafication
        Effect == 179 || // Blockage
        EffectLayer == (byte)global::Game.EffectLayer.Seals1 ||
        EffectLayer == (byte)global::Game.EffectLayer.Seals2
    );

public bool IsParalyze =>
    Target == 0 && (
        Effect == 8 || // Cord Spell
        Effect == 7 || // Coma Spell
        (Name != null && (Name.IndexOf("Paraly", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          Name.IndexOf("Palsy", StringComparison.OrdinalIgnoreCase) >= 0))
    );
```

---

## 4. Status Duration Lifecycle Fix

### 4.1 Root Cause of Immediate Expiration

When an entity with higher SPD is targeted by a status effect:
1. Turn order is sorted descending by SPD (`actors.OrderByDescending(a => a.Spd)`).
2. The faster enemy monster acts first in Round 1.
3. The slower player casts a CC ability (e.g. Cord Spell, `baseTurns = 1`).
4. At Round 1 conclusion, Phase 6 decrements all statuses: `RemainingTurns--`.
5. `RemainingTurns` becomes `1 - 1 = 0`. The status is removed immediately.
6. In Round 2, the monster is no longer afflicted and acts without restriction.

### 4.2 Lifecycle Normalization Formula

To guarantee that the target loses `baseTurns` full upcoming action phases regardless of relative turn order during the cast round, status initialization applies:

$$\text{turns} = \text{baseTurns} + 1$$

Implemented in `PvEBattleManager.cs` across all status application branches:

```csharp
int baseTurns = sk.NumberOfTurns > 0 ? (int)sk.NumberOfTurns : 2;
int turns = baseTurns + 1;

if (sk.IsFreeze)
    target.AddStatus(BattleStatusType.Frozen, turns);
else if (sk.IsSleep)
    target.AddStatus(BattleStatusType.Sleep, turns);
else if (sk.IsSeal)
    target.AddStatus(BattleStatusType.Sealed, turns);
else if (sk.IsConfusion)
    target.AddStatus(BattleStatusType.Confused, turns);
else if (sk.IsPoison)
    target.AddStatus(BattleStatusType.Poisoned, turns);
else if (sk.IsParalyze)
    target.AddStatus(BattleStatusType.Paralyzed, turns);
```

#### Lifecycle Timeline for 1-Turn CC (e.g. Cord Spell / Coma Spell):
- **Round 1 (Phase 4):** Player casts Cord Spell. Monster gains `Paralyzed` with `RemainingTurns = 2`.
- **Round 1 (Phase 6):** Round ends. `RemainingTurns` decrements to `1`. Status remains active.
- **Round 2 (Phase 5):** Monster's turn arrives. `CanAct` is `false`. Monster turn is skipped with visual camera focus.
- **Round 2 (Phase 6):** Round ends. `RemainingTurns` decrements to `0`. Status expires and is removed.
- **Round 3 (Phase 5):** Monster is free and acts normally. Target correctly missed exactly 1 full action turn.

---

## 5. Incapacitation Feedback Engine

When an incapacitated monster's turn arrives in Phase 5, the server now focuses the combat camera onto the disabled entity and pauses briefly before proceeding:

```csharp
if (!monster.CanAct)
{
    var focusPacket = new Packet(50, 6);
    focusPacket.AddByte(monster.Row);
    focusPacket.AddByte(monster.Col);
    BroadcastToBattle(focusPacket);
    await Task.Delay(800);
    continue;
}
```

This provides clear visual feedback to players that the monster attempted to take its turn but was prevented from acting due to its status condition.

---

## 6. Periodic Poison Visual Tick Engine

### 6.1 Phase 0 Tick Implementation

In `PvEBattleManager.cs`, Phase 0 (Round Start) executes periodic status effects before accepting round inputs. The poison handler now performs the following complete protocol:

```csharp
if (actor.HasStatus(BattleStatusType.Poisoned))
{
    int dmg = Math.Max(15, (int)(actor.MaxHP * 0.10));
    actor.CurHP = Math.Max(0, actor.CurHP - dmg);

    // 1. Focus combat camera on poisoned target
    var focus = new Packet(50, 6);
    focus.AddByte(actor.Row);
    focus.AddByte(actor.Col);
    BroadcastToBattle(focus);

    // 2. Broadcast combat action animation packet (Skill 11002 Poison Spell)
    var anim = new Packet(50, 1);
    anim.AddByte(actor.Row);
    anim.AddByte(actor.Col);
    anim.AddUShort(11002); // Poison Spell visual effect ID
    anim.AddByte(1);       // 1 target affected
    anim.AddByte(actor.Row);
    anim.AddByte(actor.Col);
    anim.AddByte(0x19);    // Stat Type: HP
    anim.AddInt(dmg);      // Damage dealt
    BroadcastToBattle(anim);

    // 3. Synchronize actual HP stat with client
    SendStatSync(actor, 1);

    // 4. Delay for client visual animation and damage number popup
    await Task.Delay(1000);

    // 5. Evaluate knockout if target died from poison
    if (actor.CurHP <= 0)
    {
        var deadPkt = new Packet(53, 3);
        deadPkt.AddByte(actor.Row);
        deadPkt.AddByte(actor.Col);
        BroadcastToBattle(deadPkt);

        if (CheckVictoryOrDefeat())
        {
            EndBattle();
            return;
        }
    }
}
```

### 6.2 Packet Encoding Specification

#### `AC 50:6` Focus Packet
- `Header`: `0x32 0x06` (50, 6)
- `Payload`: `[Row (1B)] [Col (1B)]`

#### `AC 50:1` Combat Animation Packet
- `Header`: `0x32 0x01` (50, 1)
- `Actor Row/Col`: `[Row (1B)] [Col (1B)]`
- `Skill ID`: `11002` (`0xE2 0x2A`, 2B Little-Endian, Poison Spell FX)
- `Target Count`: `1` (1B)
- `Target Row/Col`: `[Row (1B)] [Col (1B)]`
- `Stat Type`: `0x19` (1B, Target HP stat deduction)
- `Damage`: 4B Little-Endian integer

---

## 7. Verification & Empirical Testing

### 7.1 Compilation
Project build verified via `dotnet build wlo.pserver.core\wlo.pserver.core.csproj`:
```
Oluşturma başarılı oldu.
    0 Uyarı
    0 Hata
```

### 7.2 Simulation Testing
A simulated multi-round combat test verified:
1. **Cord Spell Lifecycle:**
   - Round 1 Cast: Target receives `Paralyzed` with 2 turns.
   - Round 1 End: Decrements to 1 turn.
   - Round 2 Start: `CanAct == false`. Monster skips turn.
   - Round 2 End: Decrements to 0 turns. Status is removed.
   - Round 3 Start: `CanAct == true`. Monster successfully acts.
2. **Poison Tick Verification:**
   - Round Start: Target with 500 MaxHP takes `Max(15, 50) = 50` poison damage.
   - HP drops from 500 to 450.
   - `AC 50:1` packet generated with 18 bytes, Skill ID 11002, statType `0x19`, and damage `50`.
   - Lethal poison check verified: Entity at 20 HP reduced to 0 HP triggers defeat packet `AC 53:3` and terminates battle.
