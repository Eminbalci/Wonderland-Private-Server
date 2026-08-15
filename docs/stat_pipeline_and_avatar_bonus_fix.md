# Technical Documentation: Stat Pipeline, Avatar Bonus & Item StatusUp Architecture

## 1. Problem Analysis & Root Cause
1. **WLO Binary Item Data Offset (DEF 206 Root Cause):**
   - In Wonderland Online's `itemDat.wpdat` binary format, all `StatusUp` values are base-100 encoded:
     - `100` = +0 (no stat change)
     - `101` = +1 (e.g., Dust Coat Jeans `StatusUp0 = 101` -> +1 DEF, Gentleman's Shoes `StatusUp0 = 101` -> +1 DEF)
     - `105` = +5
     - `95` = -5
   - Previously, the server read `Data.StatusUp[0]` as raw integer `101` and added `101 + 101 = 202` directly to base defense `4`, yielding `4 + 202 = 206`.
   - **Solution:** Implemented `ParseItemDelta(int rawStatusUp)` which computes `rawStatusUp - 100`, properly converting `101` -> `+1`.

2. **Double Stat Bonus on Creation:**
   - Dynamic properties `BonusStr` (+2) and `BonusCon` (+1) were doubled when `ApplyCharacterBaseStats()` did `Str += 2` on top of raw input.
   - `ApplyCharacterBaseStats()` was made a no-op since backing properties evaluate avatar bonuses dynamically.

3. **Discrete AC 8:1 Broadcast Standard:**
   - Slots `210, 211, 214, 215, 216, 207, 208` are broadcast as `0` to prevent client packet ID offsets.
   - Core stats are sent via `FullAtk (41)`, `FullDef (42)`, `FullSpd (45)`, `FullMatk (43)`, `FullMdef (44)`.

---

## 2. Character Stats Verification (Daniel Level 1 + 5 STR)
- **Base Attributes:** STR = 5 + 2 = **7**, CON = 0 + 1 = **1**, INT = **0**, WIS = **0**, AGI = **0**
- **HP / SP:** HP = **185 / 185**, SP = **95 / 95**
- **Base DEF:** `int(round(1 * 2.0 + 1 * 2.0)) = 4`
- **Equipped Gear:**
  - Dust Coat Jeans (Body): `DEF +1` (`101 - 100`)
  - Gentleman's Shoes (Feet): `DEF +1` (`101 - 100`)
- **Total DEF:** `4 (Base) + 2 (Equipped) = 6` (with both items) or `5` (with Jeans only).
