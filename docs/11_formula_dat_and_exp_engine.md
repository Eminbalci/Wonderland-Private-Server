# Formula.dat Specification & Experience Point (EXP) Engine

## 1. Overview

Wonderland Online defines its core mathematical balancing, level progression curves, and stat calculation multipliers inside an authentic binary asset named `Data/Formula.dat` (407 bytes). This file is read at client/server startup by the `TFormula` data class and evaluated by the `TCalculator` mathematical engine.

This document details the binary schema of `Formula.dat`, reverse-engineered disassembly from the official game client (`aLogin.exe` / `Main.exe`), and the exact mathematical formulas for character EXP, reborn EXP, pet EXP, and combat monster EXP distribution.

---

## 2. Binary Layout of `Formula.dat`

The official `Formula.dat` file is exactly 407 bytes (`0x197` bytes):

```
+---------------+---------------+--------+-------------------------------------------------------------+
| Offset (Hex)  | Offset (Dec)  | Type   | Description                                                 |
+---------------+---------------+--------+-------------------------------------------------------------+
| 0x0000        | 0             | Byte   | File Format Version (0x02)                                  |
| 0x0001..0x0168| 1..360        | Double | 45 Little-Endian IEEE-754 64-bit Floating Point Values      |
| 0x0169..0x016C| 361..364      | UInt32 | Configuration Count / Flag (0x00000005)                     |
| 0x016D..0x0196| 365..406      | UInt16 | 21 Little-Endian 16-bit Unsigned Integers (Level thresholds)|
+---------------+---------------+--------+-------------------------------------------------------------+
```

### 2.1 Double-Precision Floating Point Table (Offsets 1..360)

| Index | File Offset | Hex Value (LE) | Decoded Double | Game Engine Meaning |
| :--- | :--- | :--- | :--- | :--- |
| **#0** | `0x0001` | `00 00 00 00 00 00 00 40` | `2.0` | STR to ATK primary coefficient |
| **#1** | `0x0009` | `66 66 66 66 66 66 F6 3F` | `1.4` | STR secondary scaling |
| **#2** | `0x0011` | `00 00 00 00 00 00 F0 3F` | `1.0` | Primary stat base |
| **#3** | `0x0019` | `33 33 33 33 33 33 E3 3F` | `0.6` | Stat penalty coefficient |
| **#4** | `0x0021` | `00 00 00 00 00 00 FC 3F` | `1.75`| CON to DEF coefficient |
| **#5** | `0x0029` | `00 00 00 00 00 00 00 40` | `2.0` | CON to MaxHP scaling |
| **#8** | `0x0041` | `00 00 00 00 00 00 00 40` | `2.0` | INT to MATK primary coefficient |
| **#9** | `0x0049` | `66 66 66 66 66 66 F6 3F` | `1.4` | INT secondary scaling |
| **#12** | `0x0061` | `00 00 00 00 00 00 F0 3F` | `1.0` | WIS to MDEF coefficient |
| **#13** | `0x0069` | `9A 99 99 99 99 99 F9 3F` | `1.6` | WIS to MaxSP scaling |
| **#16** | `0x0081` | `00 00 00 00 00 00 F0 3F` | `1.0` | AGI to SPD coefficient |
| **#17** | `0x0089` | `9A 99 99 99 99 99 F9 3F` | `1.6` | AGI secondary scaling |
| **#30** | `0x00F1` (241) | `CD CC CC CC CC CC 08 40` | **`3.1`** | **Level-Up Required EXP Exponent** |

### 2.2 Integer Constants (Offsets 361..406)
* **Offset `0x0169` (361):** `UInt32 = 5`. The base flat experience added to all character levels.
* **Offset `0x016D` (365):** `UInt16 = 180` (Standard level cap threshold).
* **Offset `0x016F` (367):** `UInt16 = 94` (Reborn progression gate).
* **Offset `0x0171` (369):** `UInt16 = 100` (Rebirth initial starting level).

---

## 3. Reverse-Engineered Client Engine (`TCalculator`)

In `aLogin.exe` / `Main.exe`, the mathematical engine resides in `TCalculator`. The function at VA `0x00778628` computes the exact experience points required to advance from Level $L$ to $L + 1$:

```assembly
0x00778628: push   ebx
0x00778629: add    esp, -0x1c
0x0077862c: mov    byte ptr [esp], cl        ; cl = Reborn flag (0 = normal, 1 = reborn)
0x0077862f: mov    ebx, edx                  ; edx = Level L
0x00778631: mov    eax, dword ptr [0x4c973c] ; global pointer to TFormula
0x00778636: mov    eax, dword ptr [eax]
0x00778638: add    eax, 4                    ; skip 4-byte header -> Formula.dat payload
0x0077863b: cmp    byte ptr [esp], 0         ; if (Reborn == 0)
0x0077863f: jne    0x778662
0x00778641: mov    edx, dword ptr [eax + 0xf1] ; load Double 3.1 from offset 0xf1
0x00778647: mov    dword ptr [esp + 8], edx
0x0077864b: mov    edx, dword ptr [eax + 0xf5]
0x00778651: mov    dword ptr [esp + 0xc], edx
0x00778655: fild   dword ptr [eax + 0x169]     ; load Int 5 from offset 0x169
0x0077865b: fstp   qword ptr [esp + 0x10]
0x0077865f: wait   
0x00778660: jmp    0x7786c8                  ; compute: Round(Power(L, 3.1)) + 5
```

---

## 4. Canonical EXP Formulas

### 4.1 Normal Character Level EXP Formula
For non-reborn characters (`Reborn == 0`) across levels $1 \le L \le 199$:

$$\text{ExpRequired}(L) = \text{Round}\left( (L + 1)^{3.1} \right) + 5$$

Cumulative Total EXP required to reach Target Level $N$:
$$\text{TotalExp}(N) = \sum_{L=1}^{N-1} \left( \text{Round}\left( (L + 1)^{3.1} \right) + 5 \right)$$

#### Sample Level Progression Table (Normal)
| Level | EXP Required for Next Level | Cumulative Total EXP |
| :--- | :--- | :--- |
| **Lv. 1** | 14 EXP | 0 EXP |
| **Lv. 2** | 35 EXP | 14 EXP |
| **Lv. 5** | 303 EXP | 609 EXP |
| **Lv. 10** | 2,752 EXP | 8,247 EXP |
| **Lv. 20** | 26,112 EXP | 108,924 EXP |
| **Lv. 50** | 506,009 EXP | 3,680,240 EXP |
| **Lv. 100** | 4,960,340 EXP | 63,770,050 EXP |

---

### 4.2 Reborn (Rebirth) Character EXP Formula
When a character undergoes Rebirth (`Reborn == 1`), the level progression adopts a higher polynomial curve:

#### Stage 1: Levels $1 \le L < 150$
$$\text{ExpRequired}_{\text{reborn}}(L) = \text{Round}\left( (L + 1)^{3.3} \right) + 50$$

#### Stage 2: Levels $150 \le L \le 199$
$$\text{ExpRequired}_{\text{reborn}}(L) = \text{Round}\left( (L + 1)^{3.3} \right) + \text{Round}\left( (L + 1 - 150)^{4.9} \right)$$

---

### 4.3 Companion Pet EXP Formula
In `TCalculator.GetPetExp(L)` (VA `0x0077880d`), companion pets scale directly on the exponent from `Formula.dat` offset `0x00F1` (`3.1`) without the flat $+5$ character constant:

$$\text{PetExpRequired}(L) = \text{Round}\left( (L + 1)^{3.1} \right)$$

#### Pet Progression Curve Table
| Level | EXP Required for Next Level | Cumulative Total EXP |
| :--- | :--- | :--- |
| **Lv. 1** | 9 EXP | 0 EXP |
| **Lv. 2** | 30 EXP | 9 EXP |
| **Lv. 3** | 74 EXP | 39 EXP |
| **Lv. 5** | 298 EXP | 338 EXP |
| **Lv. 10** | 2,747 EXP | 7,952 EXP |
| **Lv. 20** | 26,107 EXP | 108,359 EXP |
| **Lv. 50** | 506,004 EXP | 3,678,920 EXP |
| **Lv. 100** | 4,960,335 EXP | 63,767,500 EXP |

---

### 4.4 Combat Battle Monster EXP Distribution (PvE)

In official Wonderland Online protocol:
* `Npc.dat` specifies base stats (`Level`, `HP`, `SP`, `STR`, `CON`, `INT`, `WIS`, `AGI`, `SPD`) but **does not contain a static EXP reward column**.
* The server calculates experience rewarded dynamically upon monster defeat according to monster level and level difference:
  $$\text{BaseExp} = \max(10, \text{MonsterLevel} \times 15)$$
* When level difference is within $\pm 19$ levels, full EXP is awarded.
* Beyond $\pm 20$ levels, an anti-leeching / level-penalty dampener applies:
  $$\text{ExpMultiplier} = \max\left(0.1, 1.0 - \frac{|\text{PlayerLevel} - \text{MonsterLevel}| - 19}{10}\right)$$

---

## 5. Server Code Implementation

The C# server implementation in [`wlo.pserver.core/Game/PlayerRelated/Equip.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Equip.cs#L1447) mirrors the authentic `Formula.dat` and `TCalculator` binary logic:

```csharp
int CalcMaxExp(byte rebirth, int Level)
{
    if (rebirth == 0)
    {
        return (int)Math.Round(Math.Pow((Level + 1), 3.1) + 5);
    }
    else
    {
        if (Level < 150)
        {
            return (int)Math.Pow((double)(Level + 1), 3.3) + 50;
        }
        else
        {
            return (int)Math.Pow((Level + 1), 3.3) + (int)Math.Pow((Level + 1 - 150), 4.9);
        }
    }
}
```

Companion pet required EXP and award engine in [`wlo.pserver.core/Game/Player.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Player.cs#L1725):

```csharp
public static uint CalcPetMaxExp(int level)
{
    return (uint)Math.Max(1, Math.Round(Math.Pow(level + 1, 3.1)));
}

public bool AddPetExp(PlayerPetData pet, uint expAmount, bool applyServerRate = true)
{
    if (pet == null || expAmount <= 0) return false;

    double multiplier = (applyServerRate && Server.ServerStatusManager.ExpRate > 0)
        ? Server.ServerStatusManager.ExpRate
        : 1.0;

    uint finalExp = (uint)Math.Max(1, Math.Round(expAmount * multiplier));
    pet.Exp += finalExp;

    uint reqExp = CalcPetMaxExp(pet.Level);
    bool petLeveledUp = false;

    while (pet.Exp >= reqExp && pet.Level < 199)
    {
        pet.Exp -= reqExp;
        pet.Level++;
        pet.MaxHP += 30;
        pet.HP = pet.MaxHP;
        pet.MaxSP += 15;
        pet.SP = pet.MaxSP;
        pet.SkillPoints += 3;
        petLeveledUp = true;
        reqExp = CalcPetMaxExp(pet.Level);
    }

    // Synchronize Pet Combat Exp Gain (Stat 0x0124) and Pet Current Exp (Stat 0x011E)
    SendPetStat(pet.Slot, 0x0124, finalExp);
    SendPetStat(pet.Slot, 0x011E, pet.Exp);

    if (petLeveledUp)
    {
        SendPetStat(pet.Slot, 0x011D, (uint)pet.Level);
        SendPetStat(pet.Slot, 0x0119, (uint)pet.HP);
        SendPetStat(pet.Slot, 0x011A, (uint)pet.SP);
        Send(Tools.FromFormat("bbbs", 23, 57, 0, $"{pet.PetName} gained {finalExp} EXP and leveled up to Lv.{pet.Level}!"));
    }
    else
    {
        SendPetStat(pet.Slot, 0x0119, (uint)pet.HP);
        SendPetStat(pet.Slot, 0x011A, (uint)pet.SP);
        Send(Tools.FromFormat("bbbs", 23, 57, 0, $"{pet.PetName} gained {finalExp} EXP!"));
    }

    return petLeveledUp;
}
```

---

## 6. Dynamic Server EXP Rate Multiplier & GUI Control

### 6.1 Server Architecture & Persistence
The server supports a dynamic, real-time EXP Rate Multiplier (`ServerStatusManager.ExpRate`) that is persisted in the SQLite `server_settings` table (`key = 'EXP_RATE'`) and evaluated during combat victory processing:

* **Engine Configuration (`ServerStatusManager.cs`):**
  * `ServerStatusManager.SetExpRate(double rate)`: Validates `rate > 0`, rounds to 2 decimal places, updates in-memory `ExpRate`, commits to `server_settings` via `SaveConfig()`, and triggers `OnStatusChanged` event.
  * Defaults to `1.0x` upon first initialization.
* **Player Experience Application ([`Equip.cs:AddExp`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Equip.cs#L396)):**
  * Evaluates `applyServerRate` flag. When active and `ExpRate != 1.0`, scales incoming experience:
    $$\text{AwardedExp} = \max(1, \text{Round}(\text{BaseExp} \times \text{ExpRate}))$$
  * Progresses `TotalExp`, allocates $+3$ stat points per level gained, and issues `AC 8:1` stat synchronization packets.
* **Pet Experience Application ([`PvEBattleManager.cs:EndBattleVictory`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Battle/PvEBattleManager.cs#L2154)):**
  * Active battle companions scale on the identical server EXP multiplier:
    $$\text{PetExpGain} = \max(1, \text{Round}(\text{BaseExp} \times \text{ExpRate}))$$
  * Evaluates against the canonical pet level-up polynomial curve:
    $$\text{PetReqExp}(L) = \max(1, \text{Round}((L + 1)^{3.1}))$$
  * Accumulates remainder EXP in `PlayerPetData.Exp` (persisted to `character_pets.exp`), advances `Level`, updates `MaxHP` / `MaxSP`, and dispatches `AC 8:2` stats (`0x011E` EXP, `0x011D` Level, `0x0119` HP, `0x011A` SP) plus level-up notifications to the owner.

### 6.2 Main Console Management Interface (`MainForm1.cs`)
The primary server dashboard (`tabPage7` / Status) hosts a dedicated real-time EXP rate management control inside the Status group box:
* **Numeric Stepper (`numExpMultiplier`):** Allows fine-grained decimal selection ($0.1\times$ to $1000.0\times$) with $0.5\times$ increments. Pressing Enter or changing the value updates the active server rate immediately.
* **Quick Presets:** Instant buttons for `1x Normal` ($1.0\times$), `2x Double` ($2.0\times$), `5x` ($5.0\times$), and `10x` ($10.0\times$).
* **Apply & Save Button (`btnApplyExp`):** Explicitly commits the chosen rate to disk/database and updates in-memory multiplier.
* **Live Status Indicator (`lblExpMultiplierStatus`):** Displays current active rate in real-time, synchronizing automatically across GUI thread events.

### 6.3 GM In-Game Chat Commands for EXP & Pets
Administrators can test and adjust player and companion experience directly through chat commands:
* `:exp <amount>`: Sets total character experience points.
* `:level <1-200>` / `:lvl <1-200>`: Sets character level and recalculates derived base stats.
* `:petexp <amount>`: Grants experience points to the currently active battle companion, automatically applying server multiplier, compound level-ups, and live wire synchronization.
* `:petlvl <1-199>` / `:petlevel <1-199>`: Sets active companion level, recalculates HP/SP, and commits to the database.

