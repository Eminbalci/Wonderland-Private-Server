# Marriage & Wedding System Architecture

## 1. Overview
The Marriage System enables players to propose, perform wedding ceremonies with animations and fireworks, exchange wedding rings, warp directly to their spouse, and divorce when needed.

---

## 2. Marriage Mechanics ([MarriageManager.cs](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/MarriageManager.cs))

### 2.1 Proposal & Requirements
- **Level Requirement:** Level 30+.
- **Cost:** 60,000 gold fee deducted upon wedding acceptance.
- **Wedding Rings:** Grants Wedding Ring #49001 (Husband) and #49002 (Wife) directly into inventories.
- **Ceremony Visuals:** Dispatches `AC 5:5` wedding fireworks and global congratulatory announcement `AC 23:57`.

### 2.2 In-Game Commands & Teleportation
- `/propose <CharID>`: Sends formal proposal prompt to target.
- `/acceptmarry`: Accepts pending marriage proposal and triggers wedding.
- `/declinemarry`: Declines pending proposal.
- `/warptospouse`: Instantly teleports player to their spouse's exact map coordinates.
- `/divorce`: Dissolves marriage bond and updates persistent database.

---

## 3. Data Architecture
- **`MarriageRecord`**: Tracks Husband/Wife CharIDs, names, and wedding date.
- **`Data/marriages.txt`**: Persists all marriage records across server restarts.
