# Technical Documentation: Companion Pet Buff and Support Skill Mechanics Engine

## 1. Overview
In Wonderland Online PvE and turn-based combat, companion pets (such as Victoria, Kanako, Niss, Robinson, Magellan, etc.) play critical tactical roles by casting defensive shields, stat boosts, cloaking/vanish buffs, and HP/SP restoration skills on players and allies.

This engine standardizes companion pet support skill classification, trained skill grade resolution, target-side scoping, packet-accurate status overlays, evasion mechanics, and round-based buff expiration.

---

## 2. Architecture & Root Causes Addressed

### 2.1 Pet Trained Skill Grade Resolution (`GetFighterSkillGrade`)
- **Prior Defect:** In `PvEBattleManager.cs`, `GetFighterSkillGrade` exclusively verified `actor.PlayerRef.PlayerSkills`. For companion pets, `actor.PlayerRef` is null, causing the function to silently fall back to `1` for all pet skills. Consequently, pet skills with high skill grades (e.g. Grade 5 Fiery Attack / Hot-Blooded) operated as Grade 1 minimum potency.
- **Resolution:** Added pet skill tree resolution checking `actor.PetRef.Skills`:
  ```csharp
  if (actor?.PetRef?.Skills != null)
  {
      var ps = actor.PetRef.Skills.FirstOrDefault(s => s.SkillID == skillId);
      if (ps != null && ps.Grade > 0)
          return ps.Grade;
  }
  ```

### 2.2 Phase 2 Support / Buff Friendly Team Resolution
- **Prior Defect:** In `PvEBattleManager.ProcessTurnActions`, Phase 2 (Support / Buff skills) statically referenced `var friendlyFighters = battle.Attackers;`. If an enemy NPC or monster executed a healing or buff skill (e.g. Magellan's Shield or an enemy Reviver), `primaryAlly` resolved to player combatants, causing enemy casters to heal or shield players.
- **Resolution:** Dynamically scoped `allyTeam` based on `actor.Side`:
  ```csharp
  var allyTeam = actor.Side == BattleTeamSide.Attacker ? battle.Attackers : battle.Defenders;
  ```

### 2.3 Vanish Skill Execution & Monster Evasion
- **Prior Defect:** `SkillData.IsVanish` (e.g., Victoria's skill `15190`) was checked in action intake, but Phase 2 had no handler for `IsVanish`. The action fell through into generic support without setting combatant statuses.
- **Resolution:**
  - Added `else if (sk != null && sk.IsVanish)` in Phase 2, applying `FighterStatusType.Vanish` for `baseTurns` (default 2 turns).
  - Emitted official 19-byte `AC 50:1` packet with visual overlay byte `0xE6` (Vanish shadow aura) and `AC 114:1` battle info prompt.
  - Updated Phase 4 and Phase 5 monster target selection to filter candidates by `!f.HasStatus(FighterStatusType.Vanish)`. Concealed allies cannot be targeted by monsters while vanished.

### 2.4 SpeedUp Stat Accumulation & Restoration
- **Prior Defect:** When `IsSpeedUp` executed, `primaryAlly.Spd += 30;` was applied alongside `primaryAlly.SetStatus(FighterStatusType.SpeedUp, turns, 30)`. However, Phase 6 round-end cleanup had no handler for `SpeedUp` expiration, causing the +30 speed boost to permanently remain on the combatant for subsequent rounds and battles.
- **Resolution:** Phase 6 round-end check now monitors expired `SpeedUp` statuses, restores base speed by subtracting `30`, and prints combat status removal notifications.

### 2.5 Healing and Support Stat Scaling
- **Prior Defect:** `IsHeal` and `IsManaRecovery` scaled recovery amounts strictly by `actor.Atk`. Pure Wisdom/INT pets (e.g. Kanako, Niss) had minimal physical `Atk`, resulting in severely underpowered heals despite high magic attack stats.
- **Resolution:** Recovery scaling now uses `Math.Max(actor.Atk, actor.Matk)`:
  ```csharp
  int stat = Math.Max(actor.Atk, actor.Matk);
  int healAmount = (int)(stat * 1.5) + (skillGrade * 40) + 50;
  ```

### 2.6 Skill Classification Exceptions (Robinson's Unload Wall)
- **Prior Defect:** `SkillData.IsShield` previously required `Target != 0`. Robinson's native skill `12046: Unload Wall` (`卸力牆`) has `Effect = 101` (Shield) but `Target = 0` in official `Skill.dat`. Because `Target == 0`, it failed shield detection and was executed as an offensive attack dealing damage.
- **Resolution:** Expanded `SkillManager.cs` property accessors to evaluate known shield, hot-blooded, speed-up, vanish, and buff IDs/effects even if `Target == 0`.

---

## 3. Protocol & Packet Synchronization

### 3.1 Status Visual Overlay Packet (`AC 50:1`)
Combat status overlays are dispatched via Action Code 50, Sub 1:
```
F4 44 0F 00 32 01 02 [TargetPos] [OverlayType] 00 00 00 00 00 00 00 00 00 00
```
- `0xE1`: Hot-Blooded / Fiery Attack (`FighterStatusType.HotBlooded`)
- `0xE2`: Shield / Barrier (`FighterStatusType.Shield`)
- `0xE4`: Speed Up (`FighterStatusType.SpeedUp`)
- `0xE6`: Vanish / Concealment (`FighterStatusType.Vanish`)

### 3.2 Battle Status Text Overlay (`AC 114:1`)
Dispatched to notify clients of active tactical combat states:
```
F4 44 [Len] 00 72 01 [MessageString]
```

---

## 4. Verification & Testing

- Automated regression test `.codex-verify/Test-PetBuffSkills.ps1` verifies:
  1. Skill classification of Robinson `12046: Unload Wall` as pure support shield (`IsShield = true`, `IsPureSupport = true`).
  2. Companion pet trained skill grade resolution (`Grade = 5` for Hot-Blooded, `Grade = 3` for SpeedUp).
  3. Skill execution, SP deduction, overlay packet dispatch (`0xE1`, `0xE6`), and round-based expiry.
  4. Vanish status prevents monster AI target selection.
  5. SpeedUp cleanly restores base speed upon expiration.
