# Minigame System & Protocol Reference (ActionCode 57 / 0x39)

## Overview
Replicates official Wonderland Online Minigame interactions reverse-engineered from live traffic captures across multiple game variants:
- **Type 3 (Whack-a-Mole)**: `minigameyikaybettim.pcapng` & `minigameyikazandim.pcapng`
- **Type 4 (Woodcutting / Archery / Target Puzzle)**: `baskabirminigamekaybettim.pcapng` & `baskabirminigamekazandim.pcapng`

---

## Minigame Types & Launch Parameters
- **`AC 57 Sub 1`**: `F4 44 06 00 39 01 [GameType (1B)] [Seed (3B LE)]`
  - `GameType = 0x03` (Seed: `0x012AF8` / 76536) &rarr; Whack-a-Mole (Köstebek Vurmaca)
  - `GameType = 0x04` (Seed: `0x012710` / 75536) &rarr; Woodcutting / Target Archery (Ağaç Kesme / Hedef Vurma)
- **`AC 20 Sub 9`**: `F4 44 02 00 14 09` (Locks player into minigame state)

---

## Result Handling Flow

### 1. Client Submission (`AC 57 Sub 1`)
- Client sends `39 01 [ResultByte]` (repeats during final UI transition):
  - `0x00`: ❌ **Lost / Failed**
  - `0x01`: 🏆 **Won / Passed**

### 2. Server Response & Quest Advancement
1. **Server ACK:** Sends `39 02` (`AC 57 Sub 2`) to conclude the minigame runtime.
2. **If Won (`Result == 1`):**
   - **Reward Delivery (`AC 23 Sub 6`):** `F4 44 21 00 17 06 32 75 01 [28x 0x00]` (Grants Item `#30002` Voucher x1 directly into player inventory).
   - **Quest Progression (`AC 24 Sub 5`):** `18 05 [QuestID_2B] [Step_1B]` advances linked quest (e.g. `44 00 01` for Whack-a-Mole / Quest 68, `43 00 01` for Target Shoot / Quest 67).
   - **Dialogue Lock (`AC 6 Sub 2`):** `06 02 01`.
   - **Victory Fanfare (`AC 20 Sub 10`):** `14 0A`.
3. **If Lost (`Result == 0`):**
   - Server acknowledges with `39 02` and sends retry message.
4. **Dialogue Close:**
   - Client sends `14 06` (`AC 20 Sub 6`).
   - Server unlocks player movement via `14 08` (`AC 20 Sub 8`) + `05 04` (`AC 5 Sub 4`).

---

## Code API
- [`AC57.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC57.cs):
  `AC57.LaunchMinigame(Player p, byte gameType, uint seed, uint questId, Action onWon, Action onLost);`
- [`EveEventInterpreter.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs):
  Handles Opcode 9 (`Minigame`), hooks `player.OnMinigameWon` to award `Item #30002` (Voucher) via `player.Inv.AddItem`.
