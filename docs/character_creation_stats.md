# Technical Documentation: Character Creation Base Stats & Starting EXP

## Overview
This document specifies the character creation attribute initialization and starting experience (EXP) architecture for Wonderland Online.

---

## 1. Starting EXP Architecture
- **Root Cause:** Level 1 base experience threshold in WLO is 6. When `TotalExp` was initialized to 0, the client formula (`CurrentExp = TotalExp - LevelBaseExp = 0 - 6`) resulted in `-6/14 (-42%)`.
- **Resolution:** Initialized `TotalExp = 6` on new character creation in [`AC09.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC09.cs#L78). The client now displays `0/8` (0%) baseline level 1 progress cleanly.

---

## 2. Character Model Base Stat Bonus Matrix
Each character avatar in Wonderland Online provides inherent starting stat bonuses (+3 total points) on top of the 5 player-allocated points:

| Body Style | Head / Character | Inherent Bonus Stats |
| :--- | :--- | :--- |
| **Big Male** | `Daniel` | STR +2, CON +1 |
| **Big Male** | `Sid` | STR +2, CON +1 |
| **Big Male** | `More` | WIS +2, AGI +1 |
| **Big Male** | `Kurogane` | STR +1, CON +2 |
| **Small Male** | `Rocco` | INT +2, AGI +1 |
| **Small Female** | `Nina` | INT +1, WIS +1, AGI +1 |
| **Small Female** | `Betty` | INT +1, AGI +2 |
| **Big Female** | `Iris` | CON +2, STR +1 |
| **Big Female** | `Lique` | STR +2, AGI +1 |
| **Big Female** | `Vanessa` | INT +3 |
| **Big Female** | `Breillat` | CON +2, STR +1 |
| **Big Female** | `Jessica` | WIS +2, INT +1 |
| **Big Female** | `Konnotsuroko` | INT +2, WIS +1 |
| **Big Female** | `Maria` | WIS +2, AGI +1 |
| **Big Female** | `Karin` | WIS +2, CON +1 |

---

## 3. Implementation Details
- Added [`EquipManager.ApplyCharacterBaseStats()`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Equip.cs#L1448-L1535).
- Integrated automatic bonus application in [`AC09.Recv1`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC09.cs#L72) prior to calculating full HP/SP and saving to `CharacterDataBase`.
