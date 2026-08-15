# Technical Documentation: NPC Data Resolution & Decoding

## Overview
This document details the root cause analysis and resolution of garbled NPC names (`?u詺`, `榆3圴圪?`) and incorrect dictionary mappings (`Persian Cat` for ID 21000).

---

## Root Cause & Resolution
1. **Exact Dictionary Mapping Only:**
   - Removed loose ID division/offset heuristics (`id / 2`, `id - 10000`) in `ImportNpcDat` which incorrectly mapped arbitrary IDs to `Persian Cat` (`11000`).
   - `npc.json` lookups now require exact ID matches (`npcJsonNames.TryGetValue(id)`).
2. **Exact 138-Byte Fixed Record Architecture:**
   - File Size: `680,202` bytes / 138 = exact 4,929 fixed-size records.
   - Record Layout within each 138-byte block:
     - `offset + 1 .. offset + 10`: Name buffer (10 bytes, reversed ASCII/Big5)
     - `offset + 11`: `Type` (`(val ^ 0xC8) - 1`)
     - `offset + 12 .. offset + 13`: `NpcID` (`(val ^ 0x5209) - 1`)
     - `offset + 14 .. offset + 15`: `ImageNum` (`(val ^ 0x5209) - 1`)
     - `offset + 37`: `Level` (`(val ^ 0xC8) - 1`)
     - `offset + 38 .. offset + 41`: `HP` (`(val ^ 0x0BAEB716) - 1`)
     - `offset + 42 .. offset + 45`: `SP` (`(val ^ 0x0BAEB716) - 1`)
     - `offset + 57`: `Element` (`(val ^ 0xC8) - 1`)
3. **Database Population & Sample Output:**
   - Sample verified imports:
     - `ID: 10000` -> `Breillat` (Level: 1, HP: 36, Element: 1)
     - `ID: 11000` -> `Persian Cat` (Level: 1, HP: 36, Element: 1)
     - `ID: 11001` -> `Tabby Cat` (Level: 1, HP: 40, Element: 2)
     - `ID: 11002` -> `Lala` (Level: 1, HP: 40, Element: 3)
     - `ID: 11003` -> `Shiba inu` (Level: 1, HP: 40, Element: 4)
     - `ID: 12000` -> `Commercial Fleet` (Level: 136, HP: 3963, Element: 4)
     - `ID: 12008` -> `Marco Polo` (Level: 86, HP: 850, Element: 1)
   - Populated 4,928 clean, fully resolved NPC records into `ServerDataBase.db`.





