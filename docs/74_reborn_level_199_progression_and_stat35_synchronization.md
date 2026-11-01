# Technical Specification: Reborn Level 199 Progression, Client Stat 35 Synchronization & AC 5:4 Visual Feedback

## 1. Problem Statement & Root Cause Diagnosis

### 1.1 Root Cause of Overworld Level Desync
Users reported that after using the `:level` command or grinding past Level 100 on a reborn character, their character level was correctly reflected during battle, but failed to synchronize or update in the overworld outside of battle:
- In battle (`PvEBattleManager.cs:1266`), the server dispatches `AC 11:250`, which packs `selfFighter.Level` into the battle initialization stream. The battle engine reads this byte directly and displays the accurate level.
- In the overworld, character level display relies on the client's internal actor structure `[actor + 0x1f98]`:
  - `0x0027b830`: Stringifies `[actor + 0x1f98]` for the nameplate and character overhead level display.
  - `0x0027bf7c`: Reads `[actor + 0x1f98]` and `[actor + 0x1fa0]` (TotalExp) to compute EXP bar progress and tooltips.
- The server's `Send8_1()` previously never dispatched **Stat 35** (`0x23`). Without Stat 35, the client was forced to deduce its level from `TotalExp`.

### 1.2 Delphi Reverse Engineering of `aLogin.exe`
Decompilation and disassembly of `aLogin.exe` revealed the exact client mechanics:
1. **Opcode 8:1 Handler (`0x00430d58` -> `0x004311c4`):**
   ```x86asm
   0x4311c4: mov eax, esi
   0x4311c6: mov byte ptr [ebx + 0x1f98], al   ; actor.Level = Level
   0x4311cc: cmp byte ptr [ebx + 0xb1], 1      ; is local player?
   0x4311d3: jne 0x4311f0
   0x4311d5: mov eax, dword ptr [0x4c89e8]
   0x4311da: mov eax, dword ptr [eax]
   0x4311dc: cmp byte ptr [eax + 0x14], 0
   0x4311e0: jne 0x4311f0
   0x4311e2: mov eax, dword ptr [0x4c94e4]
   0x4311e7: mov eax, dword ptr [eax]
   0x4311e9: mov byte ptr [eax + 0x159], 1      ; Mark UI dirty for immediate redraw
   0x4311f0: mov eax, dword ptr [0x4c9170]
   0x43120d: call 0x12d10                      ; Fire actor level change event
   ```
   **Stat 35 (`0x23`) is the client's native Level stat.** Sending `SendStat(35, Level)` directly populates `[actor + 0x1f98]`, triggers event `0x43120d`, sets UI dirty flag `0x159 = 1`, and recalculates derived limits.

2. **Visual & Audio Feedback (`0x002fd090` -> `0x0022b69c`):**
   - Sub-opcode 4 of Opcode 5 (`[5, 4, CharID:UInt32]`) dispatches to `0x0022b69c`.
   - Function `0x0022b69c` reads `CharID` at offset 2, finds the actor on the map (`0x112f58`), plays `Sound\Wav0153.wav`, and animates the floating `LevelUP` visual effect.

3. **Client-Side EXP Overflow (`0x0049f0ec` & `0x0049f100`):**
   - Reborn characters have client EXP caps at level 190, maxing at `0x76F1D017` (1,995,558,935).
   - Server `TotalExp` at Level 191+ reaches 3,491,147,452, exceeding `0x7FFFFFFF` (2,147,483,647) and causing signed 32-bit integer overflow in client arithmetic (`FUN_0036e860`).
   - Clamping wire EXP in `SendExp()` to `Math.Min((long)TotalExp, 0x76F1D017L)` prevents signed overflow on the client while preserving full 64-bit precision on the server.

---

## 2. Technical Implementation Details

### 2.1 EquipManager Overworld Synchronization (`Equip.cs`)
1. **Stat 35 Added to `Send8_1`:**
   ```csharp
   // Base Stats (Right column in Status Window):
   SendStat(0x1C, Str); // Row 1: STR (0x1f8c)
   SendStat(0x1D, Con); // Row 2: CON (0x1f8e)
   SendStat(0x1B, Int); // Row 3: INT (0x1f8a)
   SendStat(0x21, Wis); // Row 4: WIS (0x1f92)
   SendStat(0x1E, Agi); // Row 5: AGI (0x1f90)

   // Level Stat 35 (0x23): Native client actor.Level (0x1f98)
   SendStat(35, Level);
   ```

2. **LevelUP Visual & Sound Broadcasting:**
   ```csharp
   if (this is Player player)
   {
       if (levelup)
       {
           var fxPkt = Tools.FromFormat("bbd", 5, 4, player.CharID);
           player.Send(fxPkt);
           player.CurMap?.Broadcast(fxPkt, "Ex", player.CharID);
       }

       if (player.hasParty)
       {
           player.BroadcastPartyUpdate();
       }
   }
   ```

3. **Wire EXP Overflow Prevention:**
   ```csharp
   public void SendExp()
   {
       SendPacket p = new SendPacket();
       p.PackArray(new byte[] { 8, 1 });
       p.Pack8(36);
       p.Pack8(1);
       long wireExp = Math.Min((long)TotalExp, 0x76F1D017L);
       p.Pack64((ulong)wireExp);
       Send?.Invoke(p);
   }
   ```

4. **`SetLevel` Progression Support up to 199:**
   ```csharp
   public void SetLevel(byte targetLvl, bool grantPoints = true)
   {
       targetLvl = Math.Max((byte)1, Math.Min((byte)199, targetLvl));
       bool leveledUp = false;
       lock (m_Lock)
       {
           byte oldLvl = Level;
           leveledUp = targetLvl > oldLvl;
           long exp = 0;
           for (int l = 1; l < targetLvl; l++)
           {
               exp += (uint)CalcMaxExp(BitConverter.GetBytes(Reborn)[0], l);
           }
           m_totalexp = exp;
           if (grantPoints && leveledUp)
           {
               SkillPoints += (ushort)((targetLvl - oldLvl) * 3);
           }
           CurHP = FullHP;
           CurSP = FullSP;

           SendExp();
           Send8_1(leveledUp);
       }

       if (this is Player player)
       {
           player.SaveCharacterData();
       }
   }
   ```

### 2.2 GM Command Boundary Update (`AC02.cs`)
Updated `:level` / `:lvl` GM command to support levels 1 through 199 without redundant packets:
```csharp
case ":level":
case ":lvl": {
    try {
        if (words.Length >= 2 && byte.TryParse(words[1], out byte newLvl)) {
            byte targetLvl = Math.Max((byte)1, Math.Min((byte)199, newLvl));
            p.Eqs.SetLevel(targetLvl);
            DataBase.CharacterDataBase.GlobalInstance?.WritePlayer(p.CharID, p);
            p.SendSystemMessage($"[GM] Level updated to {p.Eqs.Level}! Available Stat Points: {p.Eqs.SkillPoints}");
        }
    } catch { }
}
break;
```

---

## 3. Verification & Validation Results

- **Automated Reborn Progression Test Suite:**
  - Verified setting level to 100, 101, 120, 150, 180, 190, and 199 on a Reborn character.
  - Confirmed packet `AC 8:1` Stat 35 (`Tools.FromFormat("bbbbdd", 8, 1, 35, 1, Level, 0)`) is dispatched for all levels.
  - Confirmed wire EXP does not exceed `0x76F1D017L` (1,995,558,935), preventing client signed 32-bit overflow.
  - Confirmed level cap clamping at 199 when passing 200+.
- **Build Status:** `dotnet build "Wonderland Private Server.sln"` passed with 0 Warnings and 0 Errors.
- **Database Safety:** `Data/ServerDataBase.db` checked and clean (unmodified).
