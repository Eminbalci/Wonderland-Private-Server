# Gap Analysis, Missing Features, and Feature Roadmap

## 1. Architectural Overview

This document presents a technical gap analysis of Wonderland Online Private Server, auditing missing Action Codes, unhandled `eve.Emg` opcodes, anti-exploit requirements, and a phased 5-tier implementation roadmap.

---

## 2. Protocol & Action Code Gap Analysis

```
+---------------+-----------------------+---------------+-----------------------------------------------+
| Action Code   | Feature Subsystem     | Current State | Engineering Required for Full Fidelity        |
+---------------+-----------------------+---------------+-----------------------------------------------+
| AC 85         | Instance Dungeons     | Stubbed       | Party round progression inside Maps 30000+    |
| AC 183        | Ranked PvP Matchmaking| Partial       | Elo rating, arena queue, matchmaking lobby    |
| AC 184        | Guild Siege Warfare   | Stubbed       | Territory crystal health, siege timers, buffs |
| AC 57         | Arcade Minigames      | Partial       | Coin-op minigame state machines & scoreboards |
| AC 226        | Festival Races        | Unhandled     | Seasonal event track racing and checkpoints   |
+---------------+-----------------------+---------------+-----------------------------------------------+
```

---

## 3. Eve.emg Script Opcode Coverage Gaps

Across all 10,644 event scripts in `Data/Eve.emg`, three opcodes require further implementation:

1. **Opcode 17 (Instance Dungeon Wave Signal - 934 Occurrences):**
   * Manages round advancement inside challenge instances (e.g., 20-Round Challenge, 12 Constellation Palaces).
   * *Status:* Individual combat encounters execute, but automated floor-to-floor party warping via `AC 85` requires activation.
2. **Opcode 16 (Map Mechanism & Lever Switches - 38 Occurrences):**
   * Controls puzzle mechanisms (such as statues and floor switches in dungeon maps like Map 10065) that toggle hidden doors.
   * *Status:* Currently unfreezes players without toggling spatial obstruction masks.
3. **Opcode 15 (Secondary Battle / Dungeon Triggers - 42 Occurrences):**
   * Multi-stage boss transitions that branch into secondary combat encounters upon phase shifts.

---

## 4. Anti-Exploit & Security Audit

```
+---------------------------+-----------------------------------+-----------------------------------------------+
| Vulnerability Category    | Exploit Vector                    | Server Enforcement Mechanism                  |
+---------------------------+-----------------------------------+-----------------------------------------------+
| Speedhacking              | Manipulated client movement speed | Validate distance <= speed * dt + tolerance   |
| Packet Spoofing           | Client forged ClickID in AC 20:1  | Verify entity distance <= 350 px before event |
| Trade Duplication         | Simultaneous cancel & accept lag  | Atomic lock (mlock) & dual-inventory staging  |
| Storage Overflow          | Malformed slot indices > 50       | Strictly enforce slot index range 1..50       |
| Rate Limiting             | Packet flooding on Action Codes   | Token-bucket flood control per socket session |
+---------------------------+-----------------------------------+-----------------------------------------------+
```

---

## 5. Phased 5-Tier Implementation Roadmap

```
  Tier 1: Core Combat Integrity & Protocol Alignment
  ----------------------------------------------------
  [x] Byte-for-byte AC 11:5 fighter serialization (Level/Element order)
  [x] Deduplicated starter items & 31-byte inventory bag records
  [x] Authentic 7-step raft shore shipwreck sequence
  [x] PreEvent compound rule grouping & 230 duplicate puppet suppression

  Tier 2: Instance Dungeons & Wave Trials (Maps 30000+)
  ----------------------------------------------------
  [ ] Implement AC 85 party floor advancement
  [ ] Wire Opcode 17 wave completion signals to dungeon portal warp engine
  [ ] Add team trial leaderboards and reward disbursement

  Tier 3: High-Tier Companion Sagas & Rebirth Quests
  ----------------------------------------------------
  [ ] Multi-continent death and rebirth storylines (holy water altars)
  [ ] Companion specialization trees and skill book unlocks (AC 199)
  [ ] Deep companion dialogue branch qualification

  Tier 4: Competitive PvP & Guild Siege Warfare
  ----------------------------------------------------
  [ ] Ranked arena matchmaking engine (AC 183)
  [ ] Guild territory control and crystal destruction engine (AC 184)
  [ ] Weekly territory tax distribution and guild research buffs (AC 92)

  Tier 5: Minigames, Arcade, and Seasonal Content
  ----------------------------------------------------
  [ ] Arcade mini-games (AC 57:1)
  [ ] Seasonal festival races and holiday event state machines (AC 226)
  [ ] Auction house real-time bidding engine (AC 40)
```
