# 80 - MaxStat Chat Command and Attribute Synchronization Specification

## 1. Executive Summary & Purpose

The `:maxstat` command enables players and administrators to immediately configure a character's core attributes (STR, CON, INT, WIS, AGI) to the maximum cap of **602** while resetting unspent stat points (`SkillPoints`) to **0**. This allows rapid build configuration and verification of endgame character statistics, damage calculations, and maximum HP/SP ceilings without tedious manual distribution of thousands of stat points.

---

## 2. Architecture & Implementation Details

### 2.1 Attribute Bonus Calibration (`GmManager.cs`)

Wonderland Online calculates final player attributes by combining a base stat (`baseStr`, `baseCon`, `baseInt`, `baseWis`, `baseAgi`) with inherent avatar hair/gender bonuses (`BonusStr`, `BonusCon`, `BonusInt`, `BonusWis`, `BonusAgi`).

To ensure that the final displayed attribute in the client status window (and calculated combat stats) is exactly `602`, `MaxStatPlayer` calibrates each base stat by subtracting avatar bonuses:

```csharp
player.Eqs.baseStr = (ushort)Math.Max(0, 602 - player.Eqs.BonusStr);
player.Eqs.baseCon = (ushort)Math.Max(0, 602 - player.Eqs.BonusCon);
player.Eqs.baseInt = (ushort)Math.Max(0, 602 - player.Eqs.BonusInt);
player.Eqs.baseWis = (ushort)Math.Max(0, 602 - player.Eqs.BonusWis);
player.Eqs.baseAgi = (ushort)Math.Max(0, 602 - player.Eqs.BonusAgi);
```

### 2.2 Points Reset & Vitals Restoration

- `player.Eqs.SkillPoints = 0;`: Sets unspent stat points to 0.
- `player.Eqs.CurHP = player.Eqs.FullHP;`: Restores current HP to the newly elevated maximum.
- `player.Eqs.CurSP = player.Eqs.FullSP;`: Restores current SP to the newly elevated maximum.
- `player.Eqs.Send8_1(true);`: Broadcasts full attribute update frame (`AC 8:1`) to client, synchronizing:
  - `0x1C`: STR (`602`)
  - `0x1D`: CON (`602`)
  - `0x1B`: INT (`602`)
  - `0x21`: WIS (`602`)
  - `0x1E`: AGI (`602`)
  - `38`: SkillPoints (`0`)
  - Derived combat ratings (ATK, DEF, MAT, MDF, SPD, MaxHP, MaxSP).
- `DataBase.CharacterDataBase.GlobalInstance?.WritePlayer(player.CharID, player);`: Persists updated stats into the character SQLite database immediately.

### 2.3 Network Wire Handler & Permissions (`AC02.cs`)

The command is bound under `Action Code 2` (Chat / Command parser):
- Aliases: `:maxstat`, `/maxstat`, `:maxstats`, `/maxstats`.
- Access Permissions: Registered in `isPublicCommand` so standard players can invoke `:maxstat` for themselves, while GMs can additionally supply a target player name (`:maxstat <targetPlayer>`).
- Included in `:help` and `:cmds` player and GM command listings.

---

## 3. Verification & Test Evidence

Tested via PowerShell runtime reflection on `wlo.pserver.core.dll`:
- Regular character: `Str=602`, `Con=602`, `Int=602`, `Wis=602`, `Agi=602`, `SkillPoints=0`, `CurHP=FullHP=2589`, `CurSP=FullSP=3225`.
- Avatar with inherent bonuses (Big Male Daniel: +2 STR, +1 CON): `BaseStr=600`, `TotalStr=602`; `BaseCon=601`, `TotalCon=602`. Result: PASS.
