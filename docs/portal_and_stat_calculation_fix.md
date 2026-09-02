# Technical Documentation: Portal System & Stat Calculation Pipeline

## 1. Overview
This document specifies the architectural fixes for:
1. Player Derived Stat Calculations and AC 8:1 Status Broadcast.
2. Portal Resolution and Map Teleportation Pipeline matching `D:\GitHub\Wonderland Online`.

---

## 2. Stat Calculation & Broadcast Fix
- **Root Cause of Def 206 / Incorrect Stats:**
  - `Equip.Send8_1` previously called `PacketBuilder.Add(Tools.FromFormat(...))`. Since `Tools.FromFormat` produces a `SendPacket` object (which was not recognized by `PacketBuilder.Add(object)`), the builder discarded all stat items and sent an empty packet (`F4 44 00 00`).
  - Without AC 8:1 status updates, the client retained uninitialized / leftover UI slot values (e.g. constant `206` for FullSP in the DEF slot).
  - Formulas for `Def`, `Atk`, `Matk`, `Mdef`, and `Spd` had discrepancies (e.g. Earth DEF multiplier was `Level * 8.0` instead of `3.0`).
- **Resolution:**
  - Implemented direct, discrete stat broadcasting via `SendStat(byte statId, int val)` and `SendStat64(byte statId, long val)` matching authentic WLO protocol.
  - Aligned stat formulas:
    - **ATK:** `Level * 2.0 + STR * 2.0` (Fire) / `Level * 1.4 + STR * 2.0` (Other)
    - **DEF:** `Level * 3.0 + CON * 2.0` (Earth) / `Level * 2.0 + CON * 2.0` (Other)
    - **MATK:** `Level * 1.6 + INT * 2.0` (Fire) / `Level * 1.4 + INT * 2.0` (Other)
    - **MDEF:** `Level * 2.0 + WIS * 2.0`
    - **SPD:** `Level * 2.1 + AGI * 2.2` (Wind) / `Level * 1.6 + AGI * 2.2` (Other)

---

## 3. Portal System Architecture
- **Multi-Tier Portal Resolver ([`GameMap.LookupPortal`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs)):**
  1. **Local & Database Overrides:** Evaluates `Portals` dictionary and `portal_data` / `portal_overrides` database tables.
  2. **Geometric Reverse Matching:** Searches candidate warps in `EveDat.GetMapData(MapID).WarpLoc` and finds reverse warps on destination maps pointing back to the current map. Finds the nearest portal to the player coordinates `(px, py)` with a 400px Euclidean distance threshold:
     $$\text{dist} = \sqrt{(px - \text{revX})^2 + (py - \text{revY})^2} < 400$$
  3. **Direct `eve.Emg` Click ID Matching:** Matches `w.clickID == portalID`.
  4. **Gray Code Decoding:** Applies bitwise Gray code reversal to decode encoded portal IDs:
     ```csharp
     ushort mask = n;
     while (mask > 0) { mask >>= 1; n ^= mask; }
     ```
  5. **Single-Exit Fallback:** When a map contains only 1 portal in `WarpLoc`, routes player directly to that destination.
- **AC 20 Interactions ([`AC20.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC20.cs)):**
  - `Sub 8` (Portal Step): Unpacks full 16-bit `portalID` (replacing the faulty 8-bit truncation) and triggers map teleportation.
  - `Sub 1` (Door NPC Click): Checks if clicked NPC possesses `linked_portals` in `unknownbytearray2` and activates the connected portal warp automatically.
