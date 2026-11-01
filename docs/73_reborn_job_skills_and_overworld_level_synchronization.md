# Technical Specification: Reborn Job Skills Award and Overworld Level Synchronization Fix

## 1. Problem Statement & Root Cause Diagnosis

### 1.1 Missing Job Skills Award Upon Rebirth
In *Wonderland Online*, completing character rebirth grants an exclusive innate class skill according to the chosen reborn profession:
- **Killer (1):** *Murder* (Skill ID 15241, 132 SP)
- **Warrior (2):** *Holy Shield* (Skill ID 15240, 142 SP)
- **Knight (3):** *Knight's Spirit* (Skill ID 15242, 146 SP)
- **Wit (4):** *Curse / Cuss* (Skill ID 15243, 152 SP)
- **Priest (5):** *Rest* (Skill ID 15244, 188 SP)
- **Seer (6):** *Foretell* (Skill ID 15245, 147 SP)

Prior to this fix, `RebornManager.PerformReborn` only set the `player.Job` and `player.RebornJob` enum values, without granting or unlocking the corresponding job skill in `player.PlayerSkills` or sending the client skill packets (`AC 8:1` stat 110, `AC 5:12`, `AC 5:11`, and `AC 5:4`). Additionally, `SkillManager.GetQualifiedProgressionSkills` only checked elemental affinities and basic attributes, omitting reborn job skills entirely.

### 1.2 Overworld Level Inconsistency with `:level` Command
When players leveled up using `:level <lvl>` or `:lvl <lvl>` in chat (`Network.ActionCodes.AC02.cs`):
- `p.Eqs.SetLevel(targetLvl)` updated the total experience in memory (`m_totalexp`) and adjusted available stat points (`SkillPoints`).
- However, `:level` only invoked `p.Eqs.Send8_1(true)` (which synchronizes combat stats, HP/SP, and stat points) without calling `p.Eqs.SendExp()` (which sends `AC 8:1` stat 36 `TotalExp`) or `p.Send_5_3()` (which sends the login/status packet containing the client-side `Level` at offset 19 and `TotalExp` at offset 20..23).
- As a consequence, the game client retained its previous total experience and continued displaying the old level on the overworld HUD and character status window.
- When entering combat, `PvEBattleManager.cs` built `AC 11:250` containing `selfFighter.Level` (which pulled directly from `p.Eqs.Level`), causing the combat HUD to show the new high level while the overworld remained out of sync.

### 1.3 Rebirth Level Reduction Formula Alignment
Official Wonderland Online mechanics specify that upon rebirth, the character's level is lowered by 99 (`NewLevel = Math.Max(1, CurrentLevel - 99)`), meaning:
- Lv 100 -> Lv 1
- Lv 120 -> Lv 21
- Lv 130 -> Lv 31
- Lv 180 -> Lv 81
- Lv 199 -> Lv 100

The formula was aligned to `(byte)Math.Max(1, player.Level - 99)`.

---

## 2. Technical Implementation Details

### 2.1 Reborn Job Skills Award & Persistence (`SkillManager.cs` & `RebornManager.cs`)
1. **Job Skill Resolution (`SkillManager.GetJobSkillId`):**
   ```csharp
   public static ushort GetJobSkillId(RebornJob job)
   {
       switch (job)
       {
           case RebornJob.Killer: return 15241;  // Murder
           case RebornJob.Warrior: return 15240; // Holy Shield
           case RebornJob.Knight: return 15242;  // Knight's Spirit
           case RebornJob.Wit: return 15243;     // Curse / Cuss
           case RebornJob.Priest: return 15244;  // Rest
           case RebornJob.Seer: return 15245;    // Foretell
           default: return 0;
       }
   }
   ```
2. **Instant Skill Granting (`SkillManager.LearnSkill`):**
   - Adds the skill to `player.PlayerSkills` (or updates existing grade).
   - Dispatches `AC 8:1` stat 110 (unlock signal).
   - Dispatches `AC 5:12` (skill book entry and grade).
   - Dispatches `AC 5:11` (proficiency gauge).
   - Dispatches `AC 5:4` (skill palette refresh).
   - Saves character data via `player.SaveCharacterData()`.
3. **Automatic Progression Inclusion:**
   In `SkillManager.GetQualifiedProgressionSkills`:
   ```csharp
   if (player.Job != RebornJob.none)
   {
       ushort jobSkillId = GetJobSkillId(player.Job);
       if (jobSkillId > 0 && !toUnlock.Contains(jobSkillId))
       {
           toUnlock.Add(jobSkillId);
       }
   }
   ```
4. **Rebirth Execution (`RebornManager.PerformReborn` & `ChangeJob`):**
   - Applies `newLevel = (byte)Math.Max(1, player.Level - 99)`.
   - Directly calls `SkillManager.LearnSkill(player, jobSkillId, 1, 0)`.

### 2.2 Overworld & In-Battle Level Synchronization (`Equip.cs` & `AC02.cs`)
1. **Core Synchronization in `EquipManager.SetLevel`:**
   Whenever `SetLevel(targetLvl, grantPoints)` is called on a `Player` instance:
   ```csharp
   if (this is Player player)
   {
       SendExp();
       player.Send_5_3();
       Send8_1(grantPoints);
       player.CurMap?.Broadcast(Tools.FromFormat("bbdw", 5, 5, player.CharID, (ushort)0));
       player.SaveCharacterData();
   }
   ```
2. **Direct Packet Dispatch in `AC02.cs` (`:level` / `:lvl`):**
   Invokes `p.Eqs.SetLevel(targetLvl)`, `p.Eqs.SendExp()`, `p.Send_5_3()`, `p.Eqs.Send8_1(true)`, and `p.CurMap?.Broadcast(Tools.FromFormat("bbdw", 5, 5, p.CharID, (ushort)0))` ensuring immediate overworld visual, HUD, and EXP bar updates.
3. **Null-Safety Hardening:**
   Updated `EquipManager.SendStat`, `SendStat64`, and `SendExp` to invoke `Send?.Invoke(p)` instead of calling the delegate unsafely.

---

## 3. Verification & Empirical Proof

Automated test `.codex-verify/test_reborn_skills_and_level_sync.ps1` verified:
1. **Reborn Job Skill Mapping:** All 6 classes verified to map directly to authentic `Skill.dat` entries.
2. **Rebirth Level Formula:** Lv 100 -> Lv 1, Lv 120 -> Lv 21, Lv 130 -> Lv 31, Lv 180 -> Lv 81, Lv 199 -> Lv 100.
3. **PerformReborn Skill Award:** Verified that `PerformReborn` adds Murder (15241) to `PlayerSkills`, leaves `SkillPoints` unchanged without adding bonus points, and sets level to 21.
4. **Database Non-Mutation:** `Data/ServerDataBase.db` checked with git status; 0 modifications.
5. **Compilation:** Solution built cleanly with 0 errors and 0 warnings.
