# Skill Experience Multiplier & Progression Scaling Engine

## 1. Overview & Operational Objective

In Wonderland Online (WLO), character and companion pet skills advance through 10 distinct proficiency grades (Grade 1 through Grade 10) by earning skill experience (`sk.Exp`) in combat:
- Offensive / Debuff skills earn base use EXP plus damage percentage scaled against target maximum HP via `CalculateSkillExpForHit`.
- Support / Healing / Revive / Buff skills earn base support EXP plus effective restoration percentages via `CalculateSupportSkillExp`.

Previously, server-wide EXP rate configurations controlled by `Server.ServerStatusManager.ExpRate` (configurable via GM Dashboard, SQLite database `server_settings`, and in-game controls) only scaled character combat EXP (`Equip.AddExp`) and companion pet combat EXP (`PetData.GainExp`). Skill experience was awarded with raw unscaled values regardless of the global EXP multiplier setting.

This update integrates `Server.ServerStatusManager.ScaleExperience()` directly into skill experience progression for both characters and companion pets in `SkillManager.cs`, ensuring skill grinding accelerates proportionally when higher server rates (e.g., 2.0x, 5.0x, 10.0x) are active.

---

## 2. Architecture & Data Flow

```
[ Combat Action Resolves (PvEBattleManager.cs) ]
                      |
        +-------------+-------------+
        |                           |
[ Offensive Hit ]           [ Support Cast ]
        |                           |
CalculateSkillExpForHit    CalculateSupportSkillExp
        |                           |
        +-------------+-------------+
                      |
             Raw Base expGain
                      |
                      v
   [ SkillManager.AddSkillExp / AddPetSkillExp ]
                      |
                      v
   applyServerRate ? ScaleExperience(expGain) : expGain
                      |
                      v
           Scaled EXP Applied to sk.Exp
                      |
                      v
        Grade Level-Up Evaluation (Grade * 100)
                      |
         +------------+------------+
         |                         |
   Grade Up Triggered        Grade Remains Same
         |                         |
   AC 8:1 Stat 110           AC 5:11 (Proficiency %)
   AC 5:12 (Grade sync)      AC 5:12 (Grade sync)
   Evolution Check           Database Persist
```

---

## 3. Implementation Details

### Method Signatures in `SkillManager.cs`

1. **Player Skill EXP Addition:**
```csharp
public static void AddSkillExp(Player player, uint skillId, uint expGain, bool applyServerRate = true)
{
    if (player == null || skillId == 0 || expGain == 0) return;

    if (applyServerRate) expGain = (uint)Server.ServerStatusManager.ScaleExperience(expGain);

    var sk = player.PlayerSkills.FirstOrDefault(s => s.SkillID == skillId || GetClientSkillId(player, s.SkillID) == skillId);
    if (sk == null || sk.Grade == 0) return;
    if (sk.Grade >= 10) return; // Max Grade

    sk.Exp += expGain;
    // ... Grade checks, database persistence, and AC 5:11 / AC 5:12 synchronization ...
}
```

2. **Companion Pet Skill EXP Addition:**
```csharp
public static void AddPetSkillExp(Player owner, Player.PlayerPetData pet, uint skillId, uint expGain, bool applyServerRate = true)
{
    if (owner == null || pet == null || expGain == 0) return;
    if (applyServerRate) expGain = (uint)Server.ServerStatusManager.ScaleExperience(expGain);

    pet.EnsureSkills();
    var skill = pet.Skills.FirstOrDefault(s => s.SkillID == skillId);
    if (skill == null || skill.Grade == 0 || skill.Grade >= 10) return;

    byte oldGrade = skill.Grade;
    ulong exp = (ulong)skill.Exp + expGain;
    while (skill.Grade < 10 && exp >= (uint)(skill.Grade * 100))
    {
        exp -= (uint)(skill.Grade * 100);
        skill.Grade++;
    }
    skill.Exp = skill.Grade >= 10 ? 0 : (uint)exp;
    // ... AC 8:2 Stat 110/111 pet sync to owner ...
}
```

---

## 4. Multiplier Calculation & Edge Case Handling

- **Formula:** `(long)Math.Min(int.MaxValue, Math.Max(1, Math.Round(amount * rate)))`
- **Fallback:** If `rate <= 0` or invalid (NaN/Infinity), automatically clamps to `1.0`.
- **Minimum Gain:** Guarantees a minimum scaled gain of `1` whenever base `expGain > 0`.
- **Cap Preservation:** Capped at Grade 10 threshold. Once a skill reaches Grade 10, no further EXP is accumulated, and evolution conditions are evaluated.
- **Client Synchronization:** Emits authentic client protocol packets (`AC 5:11` for 0-10000 proficiency basis points, `AC 5:12` for grade status, and `AC 8:1`/`AC 8:2` stat notifications).

---

## 5. Verification Matrix

| Test Scenario | ExpRate Setting | Base Gain | Scaled Gain | Result |
|---|---|---|---|---|
| **Player Standard Rate** | `1.0x` | 20 | 20 | Grade 1, 20 EXP |
| **Player Double Rate** | `2.0x` | 15 | 30 | Grade 1, 50 EXP |
| **Player High Rate + Level Up** | `5.0x` | 12 | 60 | Grade 2, 10 EXP (Promoted!) |
| **Pet Skill Rate** | `3.0x` | 25 | 75 | Grade 1, 75 EXP |
