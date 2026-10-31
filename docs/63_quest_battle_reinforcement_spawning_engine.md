# Quest Battle Dynamic Reinforcement Spawning Engine Specification

## 1. Executive Summary

In Wonderland Online, numerous storyline and quest battles do not conclude after the initial wave of enemies is defeated. Instead, authored reinforcements sequentially enter the battlefield at specific grid slots as existing monsters perish, up to the authored formation limits.

An empirical analysis of all 1,119 maps in `Eve.EMG` revealed:
- **3,655 total battle formations** defined within `ExtBattleInfo`.
- **1,208 formations contain reinforcement entries** (totaling **3,336 reinforcement monsters** across the game, with individual encounters having up to 16 reinforcements across multiple waves).
- Previously, the server discarded all entries beyond the initial 8 grid slots in `PvEBattleManager.cs` and stripped slot coordinates in `EveEventInterpreter.cs`. As a result, 3,336 reinforcement monsters across the game were completely dropped, causing quest battles to terminate prematurely after the initial wave and breaking quest progression sequences that required defeating full enemy contingents.

This document details the reverse-engineered data format of `Eve.EMG`'s `ExtBattleInfo`, the slot-to-grid mapping protocol, and the dynamic mid-battle replacement spawning engine implemented in `PvEBattleManager.cs`.

---

## 2. Reverse-Engineered Formation Architecture (`Eve.EMG`)

### 2.1 `ExtBattleInfo.BattleInfoEntries` Structure

Each battle encounter in `Eve.EMG` is represented by an `ExtBattleInfo` structure referenced by EVE script Opcodes 4 and 6 (e.g. `dialog1 = battleFormationIndex`). Within each formation entry:

```
ExtBattleInfo
  +-- BattleInfoEntries
        +-- subentry1: Enemy Spawns & Reinforcements (List<EveBattleEntry>)
        +-- subentry2: Allied Guest Combatants (e.g., Joan d'Arc TID 12005 at slot 16)
        +-- subentry3: Field / Environment / Background parameters
```

Each record in `subentry1` is a 4-byte packed element:
- `unknownbyte1 | (unknownbyte2 << 8)`: **Monster Template ID (TID)** (e.g. 20016 for Wild Wolf).
- `unknownbyte3`: **Battlefield Grid Slot Index (1 to 8)**.
- `unknownbyte4`: Authored spawn flags / variant parameters.

### 2.2 Battlefield Grid Slot Matrix

The Wonderland Online turn-based combat field is a 4x4 coordinate grid. The defender (enemy) side occupies X-coordinates 1 (back row) and 2 (front row), with Y-coordinates 1 to 4:

| Slot Index | Row Position | Column Position | Grid Coordinates $(X, Y)$ | Description |
|:----------:|:------------:|:---------------:|:-------------------------:|:------------|
| **1** | Front | Left | `(2, 1)` | Front row, leftmost |
| **2** | Front | Center-Left | `(2, 2)` | Front row, center-left |
| **3** | Front | Center-Right | `(2, 3)` | Front row, center-right |
| **4** | Front | Right | `(2, 4)` | Front row, rightmost |
| **5** | Back | Left | `(1, 1)` | Back row, leftmost |
| **6** | Back | Center-Left | `(1, 2)` | Back row, center-left |
| **7** | Back | Center-Right | `(1, 3)` | Back row, center-right |
| **8** | Back | Right | `(1, 4)` | Back row, rightmost |

### 2.3 Reinforcement Queue Mechanics

When multiple entries in `subentry1` specify the **same slot index**, they represent a sequential queue for that exact slot:
1. The **first** entry targeting Slot $S$ spawns immediately at combat initialization.
2. Any **subsequent** entries targeting Slot $S$ are enqueued in FIFO order into `ReinforcementQueues[S]`.
3. When the occupant of Slot $S$ is defeated (`f.IsDead == true`) during combat, the round conclusion phase dequeues the next monster for Slot $S$ and instantiates it into the active battle.

---

## 3. Server Architecture & Implementation

### 3.1 Data Structures in `PvEBattleManager.cs`

- **`BattleSpawnEntry`**:
  ```csharp
  public class BattleSpawnEntry
  {
      public uint MonsterTid { get; set; }
      public byte Slot { get; set; }
  }
  ```

- **`ActiveBattle` Enhancements**:
  ```csharp
  public Dictionary<byte, Queue<uint>> ReinforcementQueues { get; set; } = new Dictionary<byte, Queue<uint>>();
  public bool HasPendingReinforcements => ReinforcementQueues.Any(kvp => kvp.Value.Count > 0);
  ```

- **Grid Coordinate Mapping**:
  ```csharp
  public static void GetEnemySlotCoordinates(byte slot, out byte gridX, out byte gridY)
  {
      switch (slot)
      {
          case 1: gridX = 2; gridY = 1; break;
          case 2: gridX = 2; gridY = 2; break;
          case 3: gridX = 2; gridY = 3; break;
          case 4: gridX = 2; gridY = 4; break;
          case 5: gridX = 1; gridY = 1; break;
          case 6: gridX = 1; gridY = 2; break;
          case 7: gridX = 1; gridY = 3; break;
          case 8: gridX = 1; gridY = 4; break;
          default: gridX = 2; gridY = 2; break;
      }
  }

  public static byte GetSlotFromEnemyCoordinates(byte gridX, byte gridY)
  {
      if (gridX == 2)
      {
          switch (gridY)
          {
              case 1: return 1;
              case 2: return 2;
              case 3: return 3;
              case 4: return 4;
          }
      }
      else if (gridX == 1)
      {
          switch (gridY)
          {
              case 1: return 5;
              case 2: return 6;
              case 3: return 7;
              case 4: return 8;
          }
      }
      return 0;
  }
  ```

### 3.2 Battle Initialization & Slot Organization

`StartPvEBattle` now accepts an `IEnumerable<BattleSpawnEntry> spawnEntries`:
```csharp
public static void StartPvEBattle(Player player, ushort clickId, string monsterName, int npcLv = 10,
    int npcHp = 250, uint monsterTid = 11066, QuestBattleContext questContext = null,
    uint[] enemyTemplates = null, IEnumerable<BattleSpawnEntry> spawnEntries = null)
```

1. Each incoming `BattleSpawnEntry` is grouped by its authored `Slot`.
2. The first monster for each slot is dequeued and placed into `battle.Defenders` at its exact grid coordinates via `GetEnemySlotCoordinates`.
3. All remaining monsters for each slot are retained in `battle.ReinforcementQueues[slot]`.

### 3.3 Battle Victory Guarding

Previously, if all currently spawned defenders died during round processing, the battle immediately ended with victory:
```csharp
if (battle.Defenders.All(f => f.IsDead))
{
    EndBattleVictory(battle);
    return;
}
```

This is now guarded by checking `!battle.HasPendingReinforcements`:
- In **Phase 0** (Poison damage ticks).
- In **Phase 4** (Attack / Skill execution).
- If defenders are dead but reinforcements remain in the queue, victory is deferred until all reinforcement waves are depleted and defeated.

### 3.4 Phase 6.5 Reinforcement Spawning Engine

At the conclusion of each combat round (after actions, counter-attacks, and death checks, but before opening turn timers for the next round):

1. Scans `battle.Defenders.Where(f => f.IsDead).ToList()`.
2. For each dead fighter:
   - Resolves its slot via `GetSlotFromEnemyCoordinates(deadFighter.GridX, deadFighter.GridY)`.
   - Checks if `battle.ReinforcementQueues.TryGetValue(slot, out var queue) && queue.Count > 0`.
   - Dequeues `uint nextTemplateId = queue.Dequeue()`.
   - Instantiates a new `BattleMonster` and `BattleFighter` with template stats (Level, Element, HP, SP, Atk, Def, Matk, Mdef, Spd).
   - Replaces the dead fighter in `battle.Defenders` with the new reinforcement fighter.
   - Appends the reinforcement monster into `battle.Monsters` so victory EXP, item drops, and quest completion handlers receive full credit for all defeated waves.
3. Broadcasts client synchronization packets:
   - **`AC 11:5` (Monster Entry)**: Spawns the monster visual sprite at `(gridX, gridY)` with MaxHP, MaxSP, CurHP, CurSP, Level, and Element.
   - **`AC 51:1` (Stat Sync)**: Synchronizes combatant HP (attribute `0x19`) and SP (attribute `0x1A`).
   - **`AC 23:57` (System Announcement)**: Broadcasts `[Battle] {Name} (Lv.{Level}) enters as reinforcements!` to combat log.
4. If any reinforcements spawned, inserts a non-blocking `400ms` pacing delay to allow the game client's spawn animation to settle before proceeding to Phase 7 (Round Turn Prompt).

### 3.5 Integration in `EveEventInterpreter.cs`

In Opcode 4 and Opcode 6 execution:
```csharp
var spawnEntries = formation.subentry1?.Select(e => new BattleSpawnEntry
{
    MonsterTid = (uint)(e.unknownbyte1 | (e.unknownbyte2 << 8)),
    Slot = e.unknownbyte3
}).Where(e => e.MonsterTid >= 10000).ToList();

PvEBattleManager.StartPvEBattle(player, clickId, firstNpcName, (int)firstNpcLv, (int)firstNpcHp,
    (uint)firstMonsterTid, questBattleContext, enemyTemplates, spawnEntries);
```

---

## 4. Verification and Empirical Evidence

### 4.1 Automated Test Suite (`Test-BattleReinforcements.ps1`)

An automated integration harness (`.codex-verify/Test-BattleReinforcements.ps1`) was authored to verify the end-to-end reinforcement pipeline:
1. Configured an encounter with 3 monsters across 2 slots:
   - Slot 2: Monster TID 20014 (Initial) + Monster TID 20016 (Reinforcement).
   - Slot 5: Monster TID 20015 (Initial).
2. **Round 1**:
   - Player targets and defeats Slot 2 Monster (`20014`).
   - Slot 5 Monster remains alive.
   - Victory check evaluates `battle.Defenders.All(f => f.IsDead) == false` and `battle.HasPendingReinforcements == true`.
   - At Round 1 conclusion (Phase 6.5), the engine detects dead combatant at Slot 2, dequeues reinforcement `20016`, and emits:
     - `AC 11:5` monster frame packet.
     - `AC 51:1` HP/SP stat synchronization.
     - `AC 23:57` reinforcement notification.
3. **Round 2**:
   - Player executes attack defeating both Slot 2 (`20016`) and Slot 5 (`20015`).
   - All defenders are dead and `battle.HasPendingReinforcements == false`.
   - Battle terminates with `EndBattleVictory`, awarding total EXP and drops for all 3 monsters and invoking `QuestBattleContext.OnVictory`.
4. **Harness Result**:
   ```
   [PvEBattle] Initialized 2 monsters with 1 pending reinforcements across 1 slots for TestWarrior
   [PvEBattle] Spawned reinforcement Wild Wolf (TID 20016, Lv.30) at Slot 2 (2, 2) in battle TestWarrior
   [PvEBattle] Attacking team won battle! (Attackers: 1, Defenders: 2)
   [PvEBattle] Executed QuestBattleContext.OnVictory for TestWarrior
   SUCCESS: Battle reinforcements spawned correctly and battle concluded with victory.
   ```

### 4.2 Regression Verification

All existing verification suites were run against the updated binary:
- **`Test-Map12612Cellar.ps1`**: Passed.
- **`Test-GameplayDefectFixes.ps1`**: All 7 tests passed (Pet Hotel, Gathering Loops, Boat Disembark, Alchemy, Trade Cancellation, Merchant Interception, Post-Shop Unlock).
- **Compile Status**: `dotnet build "wlo.pserver.core/wlo.pserver.core.csproj"` builds with **0 Warnings, 0 Errors**.
