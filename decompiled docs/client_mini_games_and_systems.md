# Client Mini-Games, Media & Security Systems Specifications

This document outlines client-side mini-games (Sheep, Boxing, Mario Ground, Gobang, Poke, Turn Egg, Slots), MPEG audio parsing, HP/Status recovery, Security Lock, and odd/game show integrations extracted from `aLogin.exe.1.c`.

---

## 1. Mini-Game Subsystems

The client hosts various mini-games, tracking scores, timers, player lives, and UI states.

### A. Sheep Game (Dream Life)
- **UI & Graphics**:
  - `sheepDreamLife` — Main configuration/entry identifier.
  - `SheepGameBG_2` — Background layer asset.
  - `S10151` / `Sleepbabo` — Animation/state sprites.
  - `Hammer_1` — Hammer cursor/tool sprite.
- **HUD Components**:
  - `MiniGame_Score_1` — Score display panel.
  - `MiniGame_Life_1` — Life counter display.
  - `MiniGame_Time_1` — Time countdown panel.
  - `Num_White5_1` — White numeric font sheet asset (used across multiple games).

### B. Boxing Mini-Game
- **Assets & HUD**:
  - `BoxingBackground` — Background board asset.
  - `StrengthBarFrame` — Force/strength gauge frame.
- **Messages**:
  - `"Game Result: \nConsolation: "` — Game end results display showing consolation prizes.

### C. Mario Ground Mini-Game
- **Assets**:
  - `MarioGround` — Background/terrain layer.

### D. Circle-Cross (Tic-Tac-Toe variant)
- **Assets**:
  - `CJ_BG1` — Game board background.
  - `little_circle` — Highlight ring.
  - `Icon_X_1` / `Icon_O_1` — Marker symbols.
  - `icon_heart` / `icon_Heart` — Player life display icons.

### E. Gobang Board Game (Five in a Row)
- **State Prompts**:
  - `"Start The Game?"` — Invitation/readiness prompt.
  - `"Return to lobby?"` — Disconnection/exit confirmation.
  - `"Leave and lose chips!\nWant to Quit?"` — Chip penalty warning on mid-game exit.
  - `"You Win "` / `"You Lose"` — Outcome overlays.
- **Assets & Board UI**:
  - `GameHall` — Lobby UI manager.
  - `Gobang_BG2` — Playing board graphic.
  - `Gobang_W` / `Gobang_B` — White/Black stone assets.
  - `Gobang_W2` / `Gobang_B2` — Shadowed or preview stone variants.
  - `Gobang_S` / `Gobang_N` / `Gobang_C` — Control/selection buttons.
  - `icon_Gobang_y` — Active turn highlight indicator.
  - `Cursor4` — Game-specific cursor.

### F. Poke Game
- **Assets**:
  - `PokeGameBG` — Background.
  - `PokeCardBack` — Card back graphic.
  - `Icon_Currect_1` (Correct) / `Icon_Miss_1` — Success/failure feedback indicators.
  - `Poke_Text_Tc` — Text localization box.

### G. Turn Egg System (Gacha Capsule)
- **Assets**:
  - `TrunEggBG` / `TrunEggBG2` — Gacha machine background graphics.
  - `TrunEggItemDB` — Gacha item database grid.
  - `TrunEggButton` / `TrunEggButton2` — Interaction buttons.
  - `TrunEggA` — Spawn animation container.
- **Mailing/Prompts**:
  - `"Congrats! Can play free slots!"` — Multi-play/free bonus award message.

### H. Slot Machine Game
- **Assets**:
  - `SlotmachBG` — Cabinet background layout.
  - `SlotmachItemT` — Reel item display texture.
  - `SlotmachBtnBG` — Spin button.
  - `SlotmachRing` — Reel boundary frames.
  - `SlotmachText` — Payout/status text label.

---

## 2. Odd and Game Show Integration

- **File Paths**:
  - `"Data\\odd.dat"` / `"Data\\odd_d01.dat"` — Oddity/probability lookup database tables.
- **Remote Integration URL**:
  - `http://wlr.dragongamerhk.com/gameshow.php?id=18` — Remote endpoint for game show question/event configuration.

---

## 3. HP Recovery & Status Modifiers

### A. Health Restorers
- `"HP restored"` — Toast notification upon health replenishment.
- `"[Refresh] to increase %d HP"` — Refresh command UI tooltip.

### B. Status Buff Indicators
- `"ATK Up"` — Attack power increased.
- `"DEF Up"` — Defense increased.
- `"MATK Up  "` — Magic attack increased.
- `"MDEF Up  "` — Magic defense increased.
- `"SPD Up"` — Speed increased.

---

## 4. Security Lock System

The client implements an account protection system (Security Lock) to safeguard assets (preventing deletion, trade, or discarding).

- **UI Assets**:
  - `Icon_Savelock` — Locked state icon indicator.
  - `Icon_UnSavelock` — Unlocked state icon indicator.
- **Labels**:
  - `"Security Lock: Locked"` — System state message when restrictions are active.
  - `"Security Lock: Unlocked"` — System state message when restrictions are cleared.

---

## 5. Media & MPEG Audio Metadata Parser

The client binary includes a structured parser block for reading ID3/MPEG tags from media files.

| Attribute Name | Tag Mapping / Rationale |
|---|---|
| `FileName` / `FileNameShort` | Target music track filename (long/short 8.3 format). |
| `FilePath` / `FilePathName` | Absolute/relative directories of sound assets. |
| `Title` / `ExtractedTitle` / `GuessedTitle` | Song title metadata. |
| `Artist` / `ExtractedArtist` / `GuessedArtist` | Performer metadata. |
| `Album` | Album name. |
| `Comment` | ID3 comments block. |
| `Genre` / `GenreNr` | Music category text and genre numerical ID. |
| `Track` | Track number within album. |
| `Duration` / `DurationComma` / `DurationMinutes` | Played track length calculations. |
| `Length` / `LengthComma` / `LengthKB` / `LengthMB` | File size calculations. |
| `Version` | MPEG Audio revision (e.g. Layer 1, 2, 3). |
| `SampleRate` / `SampleRateKHz` | Sampling frequency (Hz / kHz). |
| `BitRate` | Bitrate value (kbps). |
| `Stereo` | Channel count layout (Stereo, Joint-Stereo, Dual-Channel, Mono). |
| `Copyright` / `Original` | Rights ownership tags. |

- **BGM Assets Mapped**:
  - `sound\\BGM0011.wav`
  - `sound\\BGM0019.wav`

---

## 6. Miscellaneous Assets

- `InitCommonControlsEx` — Windows shell UI helper.
- `FlatSB_GetScrollProp` / `FlatSB_SetScrollPos` — Scroll bar helpers.
- `ImmGetContext` / `ImmGetConversionStatus` / `ImmSetCompositionFontA` — Input Method Editor (IME) support for Chinese/Japanese/Korean text layout input.
- `MSH_SCROLL_LINES_MSG` — RegisterWindowMessage identifier.
