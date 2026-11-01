# Rebirth Level Reduction, Stat Points Policy, Battle Reborn Sync & Portal Priority Fix

## 1. Overview
This technical document details the root causes and resolutions for three interrelated issues reported by the user:
1. **Rebirth Level Calculation & Stat Point Policy:** When characters reborn, they were resetting to Level 1 and potentially recalculating stat point grants. In authentic Wonderland Online rebirth, a character's level is reduced by exactly 100 levels from their pre-rebirth level (e.g. Lv 100 $\rightarrow$ Lv 1, Lv 120 $\rightarrow$ Lv 20, Lv 199 $\rightarrow$ Lv 99) and no stat points should be granted upon rebirth (`ve point vermemesi lazım`).
2. **Battle Reborn & Job Sync:** During battle initiation (`AC 11:250` for self player and `AC 11:5` for allies/pets/enemies), the server was hardcoding `reborn = 0` and `job = 0`. This caused characters to look not reborn in battle despite being reborn in the overworld, leading to inconsistent client level and appearance rendering between battle and map views.
3. **Portal Destination Misdirection (Map 12600 Portal 11 $\rightarrow$ Bangkok):** On Map 12600 (Passage), stepping on portal 11 (which in `Eve.emg` connects to Bangkok Map 12613) mistakenly warped the player to Map 12612. This occurred because `LookupPortal` prioritized geometric reverse matching over direct `clickID == portalID` matching. Because Map 12612 had a reverse warp near the player's stepping position, the geometric heuristic hijacked the portal transition.

---

## 2. Technical Root Causes

### 2.1 Reborn Level Deduction & Stat Points
- In `RebornManager.cs:PerformReborn`, the server called `player.SetLevel(1)` unconditionally.
- In `Equip.cs:SetLevel(byte targetLvl)`, if `targetLvl > oldLvl`, it granted `(targetLvl - oldLvl) * 3` stat points to `SkillPoints`.
- When reborn was processed, the client was only sent `SendStat64(36, TotalExp)` and `Send8_1(true)` (which resets vitals and flags level up), without full login packets `Send_5_3()` or proper `SendExp()`, causing the client HUD and status window to become out of sync.

### 2.2 Battle Reborn Status Desync
- In `PvEBattleManager.cs:InitializeAndStartBattle`, `BattleFighter` records did not track `Reborn` or `Job`.
- The combat spawn packets (`AC 11:250` and `AC 11:5`) hardcoded:
  ```csharp
  p250.Pack8(0); // reborn
  p250.Pack8(0); // job
  ```
- As a result, the client received `reborn = 0` and `job = 0` upon entering any battle, causing the player to render as non-reborn in battle.

### 2.3 Portal Reverse-Match Hijacking
- In `Map.cs:LookupPortal`, the priority order had Geometric Reverse Matching (Priority A) running **before** the authentic `clickID == portalID` check (Priority B):
  ```csharp
  // Priority A (OLD): Geometric reverse matching based on player stepping position (px, py)
  // Priority B (OLD): Direct match on clickID == portalID
  ```
- When stepping on portal 11 at `pos(442, 1455)`, Map 12612 had a reverse portal to 12600 at `pos(532, 1336)` (distance ~149.2), matching `< 600` distance threshold and hijacking the warp to Map 12612 instead of Bangkok (Map 12613).

---

## 3. Implemented Modifications

### 3.1 Equip.cs
- Updated `SetLevel`:
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
      }
  }
  ```
- Changed `SendExp()` from private to `public` to allow clean packet dispatch on level/exp recomputations.

### 3.2 RebornManager.cs
- In `PerformReborn`:
  ```csharp
  byte newLevel = (byte)Math.Max(1, player.Level - 100);
  player.Job = (Game.RebornJob)(byte)job;
  player.RebornJob = (byte)job;
  player.SetLevel(newLevel, grantPoints: false);
  player.FillHP();
  player.FillSP();

  // Full state synchronization to client
  player.Send_5_3();
  player.SendExp();
  player.Send8_1(false);
  player.CurMap?.Broadcast(Tools.FromFormat("bbdw", 5, 5, player.CharID, (ushort)0));
  SendPacket pVisual = new SendPacket();
  pVisual.Pack8(23);
  pVisual.Pack8(122);
  pVisual.Pack32(player.CharID);
  player.CurMap?.Broadcast(pVisual);

  player.SaveCharacterData();
  ```

### 3.3 PvEBattleManager.cs
- Added `Reborn` and `Job` fields to `BattleFighter`.
- Populated `Reborn` and `Job` for attacker players, defender players, and pets.
- Synchronized authentic `Reborn` and `Job` values in `AC 11:250` and `AC 11:5` packets.

### 3.4 Map.cs
- In `LookupPortal`, moved `clickID == portalID` to Priority A ahead of geometric matching:
  ```csharp
  // Priority A: Direct match on clickID == portalID (Authentic Eve.emg Portal Click ID)
  foreach (var w in currentWarpLoc)
  {
      if (w.clickID == portalID && w.mapID > 0)
      {
          dstMap = w.mapID;
          dstX = (ushort)w.x;
          dstY = (ushort)w.y;
          DebugSystem.Write($"[Portal] Eve.emg 'clickID' match: Map {MapID} portal {portalID} -> Map {dstMap} ({dstX},{dstY})");
          return true;
      }
  }

  // Priority B: Geometric reverse matching based on player stepping position (px, py)
  ...
  ```

---

## 4. Verification & Testing
- Executed `.codex-verify/test_reborn_and_portal.ps1`:
  - **Test 1:** Stepping on portal 11 on Map 12600 at `pos(442, 1455)` resolved to `dstMap = 12613 (1746, 282)` (Bangkok). **PASSED**.
  - **Test 2:** Reborn at Level 120 resulted in Level 20, preserved existing `SkillPoints` (357 points), and set `Reborn = true, Job = Killer`. **PASSED**.
  - **Test 3:** Reborn at Level 100 resulted in Level 1, preserved existing `SkillPoints` (297 points), and set `Reborn = true, Job = Wit`. **PASSED**.
- Solution built successfully with `0 Warnings, 0 Errors`.
- Verified SQLite database file `Data/ServerDataBase.db` remained strictly untouched.
