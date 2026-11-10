# Overkill Flight Packet (AC53:3) and Revive Gate Specification

## Overview

In the native Wonderland Online client, Action Code `53:3` (`Tools.FromFormat("bbbb", 53, 3, x, y)`) represents an overkill flight mechanic where a defeated entity is blown out of the battlefield frame. Standard combat incapacitation/death without exceeding the overkill threshold retains the entity's defeated battle sprite on the battlefield grid so it may be revived via skills or items.

This document outlines the behavior and implementation of round damage accumulation, overkill status gating for AC 53:3 packets, and revive prevention for overkilled entities during PvE turn execution.

## Architectural Changes

### 1. Round Damage Tracking & Per-Turn Reset

- Method: `ExecuteTurn(ActiveBattle battle)`
- Location: `wlo.pserver.core/Game/Battle/PvEBattleManager.cs`
- Lifecycle: At the start of each turn immediately after `battle.Turn++`, `RoundDamage` is reset to 0 for all fighters in `battle.Attackers` and `battle.Defenders`:
  ```csharp
  battle.Turn++;
  foreach (var f in battle.Attackers) f.RoundDamage = 0;
  foreach (var f in battle.Defenders) f.RoundDamage = 0;
  ```

### 2. Player and Companion Combo Damage Phase

- During `hitTargetsTotalDamage` calculation, `target.TakeRoundDamage(totalDmg)` is invoked prior to updating `target.CurHP`:
  ```csharp
  target.TakeRoundDamage(totalDmg);
  target.CurHP = Math.Max(0, target.CurHP - totalDmg);
  ```
- In the subsequent `fallenTargets` iteration, packet AC 53:3 is dispatched if and only if `fallen.IsOverkilled`:
  ```csharp
  foreach (var fallen in fallenTargets)
  {
      if (fallen.IsOverkilled)
          BroadcastToBattle(battle, Tools.FromFormat("bbbb", 53, 3, fallen.GridX, fallen.GridY));
      if (fallen.PetRef != null)
          PetRelated.PetAmityManager.OnPetDeath(battle.AllPlayers.FirstOrDefault(p => p.CharID == fallen.OwnerID), fallen.PetRef);
  }
  ```
- Note: Pet amity reduction (`PetAmityManager.OnPetDeath`) remains executed for all fallen companion pets regardless of overkill state.

### 3. Confused Monster Internecine Damage Phase

- In the confused monster attack branch, `confusedTarget.TakeRoundDamage(cDmg)` is called prior to updating `confusedTarget.CurHP`.
- AC 53:3 is broadcast only when `confusedTarget.IsOverkilled`:
  ```csharp
  confusedTarget.TakeRoundDamage(cDmg);
  confusedTarget.CurHP = Math.Max(0, confusedTarget.CurHP - cDmg);
  if (confusedTarget.MonsterRef != null) confusedTarget.MonsterRef.MonsterHP = confusedTarget.CurHP;
  await Task.Delay(GetAnimationDelayMs(10001));
  if (confusedTarget.IsOverkilled)
      BroadcastToBattle(battle, Tools.FromFormat("bbbb", 53, 3, confusedTarget.GridX, confusedTarget.GridY));
  continue;
  ```

### 4. Monster Retaliation Phase

- Monster normal attacks invoke `target.TakeRoundDamage(rawDmg)` prior to clamping `target.CurHP`:
  ```csharp
  target.TakeRoundDamage(rawDmg);
  target.CurHP = Math.Max(0, target.CurHP - rawDmg);
  ```
- When `targetDied` is true, AC 53:3 is sent only if `target.IsOverkilled`. All companion amity loss notifications and defeat chat messages remain preserved for standard deaths:
  ```csharp
  if (targetDied)
  {
      if (target.IsOverkilled)
          BroadcastToBattle(battle, Tools.FromFormat("bbbb", 53, 3, target.GridX, target.GridY));
      if (target.PetRef != null)
          PetRelated.PetAmityManager.OnPetDeath(battle.AllPlayers.FirstOrDefault(p => p.CharID == target.OwnerID), target.PetRef);
      if (target.FighterType == BattleFighterType.Pet)
      {
          target.PlayerRef?.Send(Tools.FromFormat("bbbs", 23, 57, 0, $"{target.Name} has fallen in battle!"));
      }
  }
  ```

### 5. Revive Skill Gating

- In the revive support branch (`sk.IsRevive`), revival checks gate on `!ally.IsOverkilled`:
  ```csharp
  if (ally.IsDead && !ally.IsOverkilled)
  {
      ally.CurHP = reviveHp;
      if (ally.PlayerRef?.Eqs != null) ally.PlayerRef.Eqs.CurHP = reviveHp;
      if (ally.PetRef != null) ally.PetRef.HP = reviveHp;
  }
  ```
- Overkilled targets who have left the battlefield frame cannot be revived by active combat revive skills.

## Overkilled Player Return

Source of the rule: `Wonderland-Gonline` (`internal/battle/overkill.go`, `internal/server/combat_overkill.go`,
`docs/BATTLE_CLIENT_PARITY.md`).

- Overkill needs round damage **strictly above 150% of max HP** (`OverkillRule.Exceeds`); exactly 150% is an
  ordinary knockout. The counter (`BattleFighter.RoundDamage`) resets at the start of every round.
- 300 ms after the AC53:3 flight of a player fighter, `ReturnOverkilledPlayer` removes only that player from the
  battle. Team mates keep playing the round.
- The player's living pet is withdrawn (`IsWithdrawn`, counted as dead for targeting and actions, HP unchanged) and
  leaves the field with `11, 1, x, y, 0`.
- The player gets 1 HP, the battle close packet (`11, 0, charId, 0`), the movement unlock, and is teleported to the
  record point (`ReturnSpawnMap`), or to Starter Beach (map 11016, 1181, 243) when no record point is set.
- A removed player receives no later battle actions or rewards.

Not ported from the Go server: poison damage at the start of a round is not added to `RoundDamage`, and the
overkill path is not wired into PvP-specific result handling beyond the shared combo loop.

Tests: `tests/Run-OverkillTests.ps1` (rule thresholds, per-round reset, withdrawn fighters).
