# Technical Specification: Reborn Job Skills Award, Authentic Level Deduction & Level Synchronization Fix

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

### 1.2 Overworld vs Combat Synchronization Bug with `:level` Command
When players leveled up normally through combat or quest EXP (`Equip.cs:AddExp`):
- `AddExp` recalculates `TotalExp`, adjusts `SkillPoints`, and sends:
  1. `SendExp()`: `AC 8:1` stat 36 (`TotalExp` 64-bit unsigned integer).
  2. `Send8_1(true)`: `AC 8:1` derived combat stats (ATK, DEF, MAT, MDF, SPD), base stats (STR, CON, INT, WIS, AGI), available stat points (stat 38), potential (stat 37), equipment maxima (stats 207, 208), and vitals (HP stat 25, SP stat 26).
- Under normal gameplay, the client updates the EXP bar and immediately triggers its client-side level up calculation upon receiving `TotalExp` via `AC 8:1` stat 36.

However, in an earlier commit, `:level` and `Equip.cs:SetLevel` erroneously introduced:
- `player.Send_5_3()`: In *Wonderland Online*, `AC 5:3` (`FUN_004381c4` @ `004381c4` in `aLogin.exe`) is strictly a character initialization packet intended exclusively for login sequence. It initializes character state, resets hotbars, and updates actor models. When dispatched mid-session in an active overworld map, it corrupts the client state machine, desynchronizing overworld packet streams and breaking visual rendering.
- `Tools.FromFormat("bbdw", 5, 5, player.CharID, (ushort)0)`: In client protocol `FUN_002043cc`, `AC 5:5` with subcode 0 is part of the character morph/shapeshifting subsystem. Emitting this packet during level up corrupted actor stance and visual rendering.

### 1.3 Rebirth Level Reduction Formula Alignment
Official Wonderland Online mechanics specify that upon rebirth, the character's level is lowered by 99 (`NewLevel = Math.Max(1, CurrentLevel - 99)`):
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

### 2.2 Level Synchronization Architecture & Removal of Destructive Mid-Session Packets
1. **Clean Protocol in `EquipManager.SetLevel`:**
   Matches the exact working pattern of `AddExp`:
   ```csharp
   public void SetLevel(byte targetLvl, bool grantPoints = true)
   {
       lock (m_Lock)
       {
           byte oldLvl = Level;
           long exp = 0;
           for (int l = 1; l < targetLvl; l++)
           {
               exp += (uint)CalcMaxExp(BitConverter.GetBytes(Reborn)[0], l);
           }
           m_totalexp = exp;
           if (grantPoints && targetLvl > oldLvl)
           {
               SkillPoints += (ushort)((targetLvl - oldLvl) * 3);
           }
           CurHP = FullHP;
           CurSP = FullSP;

           SendExp();
           Send8_1(grantPoints);
       }

       if (this is Player player)
       {
           player.SaveCharacterData();
       }
   }
   ```
2. **Complete Removal of Destructive Mid-Session `Send_5_3()` and `AC 5:5` Packets:**
   - `Src/Network/ActionCodes/AC02.cs`:
     - `:level` / `:lvl`: Invokes `p.Eqs.SetLevel(targetLvl);` exclusively without any `p.Send_5_3()` or `5:5` morph broadcast.
     - `:heal` / `:full` / `:hp`: Removed `p.Send_5_3();`.
     - `:godmode` / `:god`: Removed `p.Send_5_3();`.
   - `Src/Gui/MainForm1.cs`:
     - Removed `targetPlayer.Send_5_3();` and `p.Send_5_3();` from GM tools.
   - `wlo.pserver.core/Game/PlayerRelated/RebornManager.cs`:
     - Removed `player.Send_5_3();` and `5:5` broadcast from `PerformReborn`.
   - `wlo.pserver.core/Game/PlayerRelated/GmManager.cs`:
     - Removed `player.Send_5_3();` from `RefundStats`.
   - `Send_5_3()` is now exclusively called during initial client login in `WorldServer.cs:489`.

---

## 3. Verification & Empirical Proof

1. **Reborn Job Skill Mapping:** All 6 classes verified to map directly to authentic `Skill.dat` entries.
2. **Rebirth Level Formula:** Lv 100 -> Lv 1, Lv 120 -> Lv 21, Lv 130 -> Lv 31, Lv 180 -> Lv 81, Lv 199 -> Lv 100.
3. **PerformReborn Skill Award:** Verified that `PerformReborn` adds Murder (15241) to `PlayerSkills`, leaves `SkillPoints` unchanged without adding bonus points, and sets level to 21.
4. **Packet Dispatch Verification:** Verified that `SetLevel` generates clean `AC 8:1` stat 36 (TotalExp) and `AC 8:1` stats without emitting any `AC 5:3` or `AC 5:5` packets.
5. **Database Non-Mutation:** `Data/ServerDataBase.db` verified completely unmodified.
6. **Compilation:** Solution built cleanly with 0 errors and 0 warnings.
