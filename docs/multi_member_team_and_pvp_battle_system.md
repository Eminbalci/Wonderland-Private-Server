# Multi-Member Team & PvP Battle System Specification

## Overview
Wonderland Online uses a 4x4 turn-based combat grid where each side can contain up to 4 players and 4 companion battle pets. This document specifies the multi-member party team battle and Player-versus-Player (PvP) mechanics, grid slot arrangements, packet sequences, action resolution, and rewards.

---

## 1. Grid Formation Architecture

### Attacking Side / Friendly Team (Right Side)
- **Slot 0 (Party Leader)**:
  - Player: `Grid (4, 2)` (Back Row)
  - Companion Pet: `Grid (3, 2)` (Front Row)
- **Slot 1 (Member 1)**:
  - Player: `Grid (4, 3)` (Back Row)
  - Companion Pet: `Grid (3, 3)` (Front Row)
- **Slot 2 (Member 2)**:
  - Player: `Grid (4, 1)` (Back Row)
  - Companion Pet: `Grid (3, 1)` (Front Row)
- **Slot 3 (Member 3)**:
  - Player: `Grid (4, 4)` (Back Row)
  - Companion Pet: `Grid (3, 4)` (Front Row)

### Defending Side / Opposing PvP Team (Left Side)
- **Slot 0 (Enemy Leader)**:
  - Player: `Grid (1, 2)` (Back Row)
  - Companion Pet: `Grid (2, 2)` (Front Row)
- **Slot 1 (Enemy Member 1)**:
  - Player: `Grid (1, 3)` (Back Row)
  - Companion Pet: `Grid (2, 3)` (Front Row)
- **Slot 2 (Enemy Member 2)**:
  - Player: `Grid (1, 1)` (Back Row)
  - Companion Pet: `Grid (2, 1)` (Front Row)
- **Slot 3 (Enemy Member 3)**:
  - Player: `Grid (1, 4)` (Back Row)
  - Companion Pet: `Grid (2, 4)` (Front Row)

### PvE Monster Formation (Left Side)
Monsters use designated 8 formation slots:
`{(2, 2), (2, 3), (2, 1), (2, 4), (1, 2), (1, 3), (1, 1), (1, 4)}`

---

## 2. Packet Flow

### Initiation Phase
1. **Enter Battle Mode**: `AC 20:12`
2. **Mode Change**: `AC 6:2 [01]`
3. **Prepare Battle Grid (`AC 11:250`)**:
   - Background Map ID (`ushort`)
   - For each friendly player on that player's side:
     `[Role (1B: 1=Attacker, 2=Defender)] [FType: 2] [CharID (4B)] [ClickID: 0] [OwnerID: 0] [GridX] [GridY] [MaxHP (4B)] [MaxSP (2B)] [CurHP (4B)] [CurSP (2B)] [Level (1B)] [Element (1B)] [Reborn (1B)] [Job (1B)] [Pad: 0]`
4. **Combat Start**: `AC 11:10 [01]`
5. **Entity Spawn (`AC 11:5`)**:
   - Friendly Companion Pets (`FType: 4`, `OwnerID = member.CharID`)
   - Opposing Fighters (Monsters or Opposing Players `FType: 2` & Pets `FType: 4`)
6. **Real-time Stat Synchronization (`AC 51:1`)**:
   - `[GridX (1B)] [GridY (1B)] [StatID (1B: 0x19=HP, 0x1A=SP)] [Value (4B LE)]`
7. **Action UI Activation (`AC 50:6` & `AC 52:1`)**:
   - Opens wheel UI for all living human combatants.

### Turn Resolution Phase
- **Action Broadcast**: `AC 53:5 [GridX] [GridY]`
- **Combat Animation**: `AC 50:1` payload specifying source, skill ID, target, and damage.
- **Damage Sync**: `AC 51:1` updates target HP in real time.
- **Despawn Fallen Combatant**: `AC 11:1 [GridX, GridY, 0]` when HP reaches 0.
- **Companion Strikes**: Active pets of living team members execute follow-up attacks.
- **Retaliation**: Opposing monsters or defending PvP players retaliate against living targets.

### Victory & Conclusion Phase
1. **Combat End**: `AC 11:12 [01]`
2. **Result Screen**: `AC 22:6 [11, 0, Result]` (2 = Victory, 0 = Defeat, 1 = Flee)
3. **Reward Popup**: `AC 22:5 [11, Exp, Gold]`
4. **Close Battle Window**: `AC 11:0 [CharID (4B), 0 (2B)]`
5. **Despawn Fighters**: `AC 11:1 [GridX, GridY, 0]` for all battle grid slots.
6. **Mode Reset**: `AC 6:2 [00]`
7. **Unlock Movement**: `AC 20:8`

---

## 3. Implementation Components
- [`wlo.pserver.core/Game/Battle/PvEBattleManager.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Battle/PvEBattleManager.cs): Core battle orchestration engine (`BattleFighter`, `ActiveBattle`, `StartPvPBattle`, `BuildFighters`, `ProcessTurn`, `EndBattleVictory`, `EndBattleDefeat`).
- [`Src/Network/ActionCodes/AC11.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC11.cs): Action Code 11 router (`HandlePlayerPK` initiates PvP encounters).
- [`Src/Network/ActionCodes/AC50.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC50.cs): Action Code 50 turns and action dispatching.
