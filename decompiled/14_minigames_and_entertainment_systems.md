# Mini-Games and Entertainment Systems Specification

## 1. Overview of Embedded Client Mini-Games

The client engine includes native mini-game interactive subsystems designed for leisure, gambling chips, player rankings, and event competitions:

---

## 2. Mini-Game Subsystems

### 2.1 Sheep Game (Dream Life)
* **Configuration Key:** `sheepDreamLife`
* **Assets & Sprites:** `SheepGameBG_2` (background), `Hammer_1` (hammer cursor tool), `S10151` / `Sleepbabo` (sleep animation sprites).
* **HUD Panels:**
  * `MiniGame_Score_1`: Active point score display.
  * `MiniGame_Life_1`: Player remaining lives counter.
  * `MiniGame_Time_1`: Stage countdown timer.
  * `Num_White5_1`: Numeric font texture sheet.

### 2.2 Gobang Board Game (Five in a Row)
* **Rules & Logic:** Turn-based board game between two players with casino chip wagers.
* **UI Prompts:**
  * `"Start The Game?"` (Readiness prompt).
  * `"Return to lobby?"` (Exit confirmation).
  * `"Leave and lose chips!\nWant to Quit?"` (Mid-game surrender chip forfeiture warning).
  * `"You Win "` / `"You Lose"` (Match outcome displays).
* **Assets:** `GameHall` (lobby manager), `Gobang_BG2` (board layout), `Gobang_W` / `Gobang_B` (white/black stone assets), `icon_Gobang_y` (active turn marker).

### 2.3 Boxing Mini-Game
* **Assets:** `BoxingBackground`, `StrengthBarFrame` (force/power meter).
* **Scoring:** Displays score and consolation prizes: `"Game Result: \nConsolation: "`.

### 2.4 Dig Hole Mini-Game
* **UI Form:** `"MiniGame_DigHole_Form"`.
* **Assets:** `"DigHole"`, `"DigHole_Hole"`, `"DigHole_Finger"`, `"icon_dig_1"`.

### 2.5 Lucky Draw & Slots
* **Lucky Draw:** Form `"MiniGame_Lucky_Form"`.
* **Slot Machine:**
  * Assets: `SlotmachBG` (cabinet background), `SlotmachItemT` (reel items), `SlotmachBtnBG` (spin button), `SlotmachRing` (reels).
  * Bonus Toast: `"Congrats! Can play free slots!"`.

### 2.6 Turn Egg (Gacha Capsule) System
* **Assets:** `TrunEggBG`, `TrunEggBG2`, `TrunEggItemDB`, `TrunEggButton`, `TrunEggA`.
* **Database:** Connects to `odd.dat` and `odd_d01.dat` probability matrix tables.
