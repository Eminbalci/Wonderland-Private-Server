# Arcade Minigame Machine Integration Protocol (Map 10027)

## Overview
Replaces generic event dialogue scripts on Map 10027 (Ship Cabin / Arcade Room) with authentic direct minigame launches for Arcade Machines.

---

## Machine Protocol Mapping
| Click ID | Target Name | Minigame Type | Seed (Hex) | ActionCode Packet |
|---|---|---|---|---|
| **6** | Game Machine a | Type 3 (Whack-a-Mole) | `0x012AF8` | `AC 57 Sub 1` (`39 01 03 F8 2A 01 00`) + `AC 20 Sub 9` |
| **5** | Game Machine b | Type 4 (Woodcutting / Archery) | `0x012710` | `AC 57 Sub 1` (`39 01 04 10 27 01 00`) + `AC 20 Sub 9` |
