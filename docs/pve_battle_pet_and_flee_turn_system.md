# PvE Battle Turn Flow & Pet Action Synchronization

## 1. Overview
In Wonderland Online (WLO), battle is turn-based where each human player commands both their **character** (Back row, Grid `[4, 2]`) and their active **companion pet** (Front row, Grid `[3, 2]`).

## 2. Packet Flow & Protocol Architecture

### 2.1 Round Start & Initial Turn Prompt
1. `AC 50:6` (`[50, 6, gridX, gridY, 0]`): Dispatched to open the action wheel UI for the initial actor (Character `(4, 2)` or Pet `(3, 2)` if character is incapacitated).
2. `AC 52:1` (`[52, 1]`): Dispatched to start the 30-second round countdown timer.

### 2.2 Turn Action Submission & Pet Prompting
1. Player submits character action via `AC 50:1` (Attack/Skill/Defend/Flee).
2. Server registers action in `battle.PendingActions[(gridX << 8) | gridY]` and broadcasts `AC 53:5` acknowledge.
3. If the player controls other living friendly fighters (e.g. Companion Pet) who haven't acted yet:
   - Server immediately dispatches `AC 50:6 [petGridX, petGridY, 0]` and `AC 52:1` to open the command wheel for the Pet.
4. When all actions are collected (`ExpectedActionCount` reached), `TryExecuteTurn()` processes the round.

### 2.3 Turn-Based Flee (Escape) Mechanics
- Selecting "Flee" (`AC 50:5` / `Skill 60041`) does not abruptly abort the match; it is queued as an authentic turn action (`actionType = "flee"`).
- The player can still issue commands for the Pet (e.g., Pet attacks, defends, or flees).
- In `ExecuteTurn` (Phase 0.5):
  - Flee animations (`Skill 60041`) are played for fleeing actors.
  - If a team player/leader fled, `EndBattleFlee()` cleanly concludes combat, despawns battle entities, restores normal map movement, and commits player data.

### 2.4 Disconnect Handling
- `Player.OnConnectionLost()` invokes `PvEBattleManager.OnPlayerDisconnect(player)`.
- Sockets dropping or closing during active combat cleanly terminates the battle session, disposes the 30s turn timer, and removes the session from `_activeBattles`.
