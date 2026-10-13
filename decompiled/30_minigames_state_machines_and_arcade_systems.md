# Mini-Games State Machines and Arcade Systems Specification

## 1. Executive Summary & Arcade Architecture

Wonderland Online incorporates an extensive suite of embedded mini-games and gambling mechanics used for Item Mall token consumption, event rewards, and player entertainment. The underlying systems are compiled across dedicated units identified in `PACKAGEINFO`:
- `MZ_BoxingGame.pas`: Rhythm boxing strength machine
- `LD_DigHoleGame.pas`: Underground digging grid game (`MiniGame_DigHole_Form`)
- `cMZ_Catcher.pas`: UFO arcade claw prize machine
- `SMZ_MarioGame.pas`: Side-scrolling obstacle avoidance platformer
- `MR_ShiBaDou.pas` / `RE_SGame_BiDaXiao.pas`: 18 Beans & Big/Small dice gambling
- `yRE_Chess.pas` / `RE_ChessTools.pas`: 5-in-a-Row Gobang board engine (`Gobang_BG2`)
- `TMR_Lotto.pas`: Turn-egg lottery gacha machine

---

## 2. UFO Claw Machine Arcade State Machine (`cMZ_Catcher.pas`)

Subroutines `FUN_001859c8` through `FUN_00185c00` in [`aLogin_decompiled.c`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/aLogin_decompiled.c) (lines 170080 to 170250) implement the UFO Claw prize retrieval state machine:

```
+-----------------------------------------------------------------------------------+
|                        UFO CLAW MACHINE STATE TRANSITIONS                         |
+-----------------------------------------------------------------------------------+
|                                                                                   |
|  [ STATE 1: CatcherBasic ]                                                        |
|    - Texture: "CatcherBasic" (Width: 0x9A = 154px, Height: 0x7E = 126px)          |
|    - Initial stationary position on horizontal gantry rail                        |
|    - Awaits player Direction Key or Button Press                                  |
|                                                                                   |
|  [ STATE 2: CatcherMoving ]                                                       |
|    - Texture: "CatcherMoving" (Horizontal traversing animation)                   |
|    - Velocity: 4 px/tick across X-axis until button release                       |
|                                                                                   |
|  [ STATE 3: CatcherDroping ]                                                      |
|    - Texture: "CatcherDroping" (Width: 0x9A = 154px, Height: 0x80 = 128px)        |
|    - 4-phase claw opening sequence (10 frame step animation)                      |
|    - Descends along vertical cable towards prize floor                            |
|                                                                                   |
|  [ STATE 4: Prize Hit-Testing & Grip Clamping ]                                   |
|    - Evaluates AABB intersection between claw tips and prize doll bounding boxes  |
|    - Server verifies Item Mall Play Token balance                                 |
|    - Determines grip retention roll: Success -> Prize Capsule, Fail -> Slip Drop  |
|                                                                                   |
|  [ STATE 5: Return & Chute Delivery ]                                             |
|    - Claw retracts vertically and glides to delivery chute at (0, 0)              |
|    - Item injected into player bag or compensation tokens awarded                 |
|                                                                                   |
+-----------------------------------------------------------------------------------+
```

---

## 3. Rhythm Boxing Machine Engine (`MZ_BoxingGame.pas`)

Subroutine `FUN_0018575c` (lines 147760 to 147820) coordinates the Boxing strength machine:
- **Spriting:** Alternates between `"BoxingGameMonst"` (Target monster dummy) and `"BoxingGameBoxer"` (Player avatar punch stance).
- **Oscillating Power Bar:** `StrengthBarFrame` cycles an indicator needle between 0% and 100% force.
- **Timing Gate:** Pressing punch evaluates needle proximity to the target sweet spot (90%..100%).
- **Score Calculation:**
  $$\text{PunchScore} = \text{ForceValue} \times \text{TimingMultiplier} + \text{PlayerSTR}$$
- **Reward Tiers:** High scores dispense commemorative tokens, consumables, or leaderboard placement.

---

## 4. Underground Digging Minigame (`LD_DigHoleGame.pas`)

Hosted inside in-game form `MiniGame_DigHole_Form` (RVA `0x00204424`):
- **Grid Geometry:** $5 \times 5$ underground grid represented by `DigHole_Hole` tile sprites.
- **Cursor Indicator:** `DigHole_Finger` tracks player mouse selection.
- **Dig Sequence:** Consumes shovel tool durability or energy points to excavate tiles.
- **Node Drop Tables:** Uncovers buried ore, fossil bones, ancient clay, or detonates booby traps.

---

## 5. Dice Gambling & Board Games

### 5.1 18 Beans Dice Engine (`MR_ShiBaDou.pas`)
- Traditional East Asian 4-dice gambling game (十八豆仔).
- Evaluates roll pairs: two matching dice establish the base point from the remaining two dice.
- Four-of-a-kind (一色) represents the supreme winning jackpot hand.

### 5.2 Gobang 5-in-a-Row (`yRE_Chess.pas` / `RE_ChessTools.pas`)
- $15 \times 15$ board grid rendered on `Gobang_BG2`.
- Turn timer enforcement (30 seconds per stone placement).
- Automatic win-state detection evaluating 5 consecutive stones horizontally, vertically, or diagonally.
- Forfeit timeout automatically awards chip wager to the active opponent.
