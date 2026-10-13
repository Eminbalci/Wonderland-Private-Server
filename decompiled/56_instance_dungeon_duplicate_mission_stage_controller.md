# 56 - Instance Dungeon (Duplicate Mission) Stage Controller Specification

## 1. Architectural Overview & Source Mapping

Wonderland Online implements multi-player instanced party dungeons known as **Duplicate Missions** (副本任務). These isolated chambers feature multi-stage wave battles, boss encounters, stage clear time trials, and reward phases. The subsystem is managed by Delphi unit `MZ_DupMis.pas` with client UI interfaces `Form_CreateDupMis` ($463 \times 377$ px) and `Form_DupMis_List`.

### Subroutine Inventory & Source Locations

| Virtual Address | Subroutine Symbol | File / Unit | Purpose |
| :--- | :--- | :--- | :--- |
| `0x00180471` | `FUN_0019bb9c` | `MZ_DupMis.pas` | Instance creation button handler with party/instance state validation |
| `0x00180400` | `FUN_0019ba74` | `MZ_DupMis.pas` | Instance page catalog populator (10 instances per page, 131-byte records) |
| `0x00180300` | `FUN_0019b484` | `MZ_DupMis.pas` | Instance launch packet formatter and stage parameter pack |
| `0x00180570` | `FUN_0019c250` | `MZ_DupMis.pas` | Instance query packet emitter (`Action Code 0x55:0x04`) |
| `0x00181453` | `FUN_0019f374` | `MZ_DupMis.pas` | Instance entry packet transmitter (`Action Code 0x55:0x01`) |
| `0x00182104` | `FUN_001a0a50` | `MZ_DupMis.pas` | Stage / wave progression transition dispatcher (`Action Code 0x55:0x0A`) |
| `0x00183058` | `FUN_001a1820` | `MZ_DupMis.pas` | Boss victory and reward phase activator (`Action Code 0x55:0x0B`) |
| `0x00184291` | `FUN_001a3cb0` | `MZ_DupMis.pas` | Instance dungeon forfeit / exit dispatcher (`Action Code 0x55:0x02`) |
| `0x00181331` | `FUN_0019f200` | `MZ_DupMis.pas` | `Form_CreateDupMis` modal dialog constructor ($463 \times 377$ px) |

---

## 2. Form Geometry & Instance Catalog Grid

The instance lobby creation dialog `Form_CreateDupMis` provides catalog browsing and difficulty tier selection.

```
+-------------------------------------------------------------+
| [icon] Duplicate Mission (Instance Dungeon)  [Btn_Close_s_1]|
+-------------------------------------------------------------+
|  Select an instance dungeon challenge for your team:       |
|                                                             |
|  Instance Catalog (10 per page, DataGrid):                  |
|  +-------------------------------------------------------+  |
|  | Instance Name      | Min LV | Party Req | Wave Count  |  |
|  +-------------------------------------------------------+  |
|  | [1] Kelan Trial    | LV 10+ | 1 - 4 P   | 5 Waves     |  |
|  | [2] Holy Catacombs | LV 25+ | 2 - 4 P   | 8 Waves     |  |
|  | [3] Dragon Lair    | LV 50+ | 4 Players | 10 Waves    |  |
|  | [4] Sky Castle     | LV 80+ | 4 Players | 12 Waves    |  |
|  +-------------------------------------------------------+  |
|  [Btn_ArrowL_5 (< Prev)]              [Btn_ArrowR_5 (Next >)]|
|                                                             |
|  Selected: [Instance Details & Objective Description]       |
|                                                             |
|         [Btn_Create (Launch)]         [Btn_Cancel_1 (Exit)] |
+-------------------------------------------------------------+
```

### Form Specifications

- **Form Name:** `Form_CreateDupMis`
- **Dimensions:** Width = `0x1cf` (463 pixels), Height = `0x179` (377 pixels)
- **Controls & Navigation:**
  - `param_1 + 0x4f`: 10-row instance selection table (`piVar3[0x52] = 10`).
  - `Btn_ArrowL_5`: Previous catalog page at $(X=45, Y=17)$.
  - `Btn_ArrowR_5`: Next catalog page at $(X=130, Y=17)$.
  - `Btn_Create`: Create and launch instance (`FUN_0019bb9c`).
  - `Btn_Cancel_1`: Close dialog (`LAB_0019b7b4`).

---

## 3. Instance Database Schema (131-Byte Records)

Instance definition templates are stored in an array indexed by `PTR_DAT_004c8a0c` with a stride of **131 bytes (`0x83`)**:

| Offset | Type | Field Name | Description |
| :---: | :---: | :--- | :--- |
| `+0x00` | `uint16` | `InstanceID` | Unique numeric identifier of duplicate mission |
| `+0x02` | `char[32]`| `InstanceName`| Display title of the instance dungeon |
| `+0x22` | `uint16` | `MapID` | Base map ID loaded for the instanced chamber |
| `+0x24` | `uint16` | `SpawnX` | Player entry spatial coordinate $X$ |
| `+0x26` | `uint16` | `SpawnY` | Player entry spatial coordinate $Y$ |
| `+0x28` | `uint8` | `MinLevel` | Minimum character level required to enter |
| `+0x29` | `uint8` | `MaxLevel` | Maximum level ceiling (if level-capped) |
| `+0x2A` | `uint8` | `TotalWaves` | Number of combat stages / mob wave encounters |
| `+0x2B` | `uint16` | `TimeLimitSec`| Overall instance time limit (e.g., 1800s = 30 mins) |
| `+0x2D` | `uint8[8]`| `WaveMobGroup`| Monster encounter group table IDs per wave |
| `+0x67` | `uint8` | `PartyRequired`| $1 =$ Full party strictly required, $0 =$ Solo permitted |
| `+0x68` | `uint16` | `RewardItemID`| Bonus treasure reward item spawned upon clear |
| `+0x6A` | `uint32` | `RewardExp` | Bonus experience granted on completion |

---

## 4. Entry Validation & State Machine (`FUN_0019bb9c`)

Before an instance is created, the client evaluates three mandatory preconditions:

1. **Selection Verification:**
   `if (*(int *)(param_1 + 0x144) == -1)` $\implies$ rejects with `"Select instance first"`.
2. **Existing Instance Mutex:**
   `if (*(char *)(LocalPlayer + 0x56f0) != 0)` $\implies$ rejects with `"Leave instance first"`.
   (Actor register `+0x56f0` is the active instance flag).
3. **Party Constraint Enforcement:**
   `if ((*(char *)(InstanceRecord + 0x67) == 1) && (*(int *)(PartyPtr + 8) == 0))`
   $\implies$ rejects entry if party is required but player is unaccompanied.

---

## 5. Wire Protocol & Network Framing: Action Code 85 (`0x55`)

Instanced dungeons operate on dedicated **Action Code 85 (`0x55`)**.

### Action Code 85 Sub-Opcode Matrix

| Action Code | Sub-Opcode | Direction | Description | Payload Schema |
| :---: | :---: | :---: | :--- | :--- |
| `0x55` | `0x01` | C $\to$ S | Create / Enter Instance | `[InstanceID (2B)] [Difficulty (1B)]` |
| `0x55` | `0x02` | C $\to$ S | Exit / Forfeit Instance | `[Confirm (1B)]` |
| `0x55` | `0x04` | C $\to$ S | Query Instance Records | `[InstanceID (2B)]` |
| `0x55` | `0x05` | S $\to$ C | Instance Status Sync | `[InstanceID (2B)] [CurWave (1B)] [TimeLeft (2B)]` |
| `0x55` | `0x0A` | C $\to$ S | Advance Next Wave | `[CompletedWave (1B)]` |
| `0x55` | `0x0B` | S $\to$ C | Boss Defeated / Victory | `[ClearTimeSec (2B)] [RewardID (2B)] [EXP (4B)]` |
| `0x55` | `0x0C` | S $\to$ C | Time Warning / Purge | `[RemainingSec (2B)]` |

---

## 6. Private Server Implementation Requirements

1. **Chamber Isolation:** Each instance session must be allocated a dynamic, isolated virtual map instance so that parties running the same instance simultaneously do not see or collide with each other.
2. **Wave Spawning Clocks:** When `0x55:0x0A` is received, initiate a 5-second countdown timer before spawning the next wave of combat encounters.
3. **5-Minute Evacuation Purge:** When the boss is defeated (`0x55:0x0B`), trigger a 300-second evacuation countdown clock, teleporting all players back to the entrance town when the timer expires.
