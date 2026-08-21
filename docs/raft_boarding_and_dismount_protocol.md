# Raft Boarding, Sailing, and Shore Dismount Protocol (ActionCode 15 / 0x0F)

## Overview
Replicates official Wonderland Online Raft (Sal - Vehicle ID `48016` / `0xBB90`) interaction mechanics reverse-engineered from `denizetiklayarakraftabinmeveisinlandiktansonrasahiletiklayarakraftikiriprafttaninme.pcapng`.

---

## Complete Protocol Breakdown

### 1. Boarding the Raft from Shore
- **Client &rarr; Server (`AC 15 Sub 14` / `0F 0E`)**:
  - `0F 0E 10 90 BB` (Vehicle Type `0x10`, Vehicle ID `0xBB90` = 48016).
- **Server &rarr; Client (`AC 15 Sub 18` / `0F 12`)**:
  - `0F 12 10 [CharID (4B LE)] [VehicleID (2B LE)] [Durability (8B)]`.
- **Client &rarr; Server (`AC 15 Sub 7` / `0F 07`)**:
  - `0F 07 10 90 BB` (Confirm ride).
- **Server &rarr; Map Broadcast**:
  - `AC 15 Sub 10` (`0F 0A 10 [CharID] 90 BB` &rarr; Mount confirmation).
  - `AC 15 Sub 14` (`0F 0E 10 [CharID] 00 00 00 00 00 00` &rarr; Active sailing mode).

### 2. Sailing & Ocean Navigation
- As player navigates water maps, server synchronizes vehicle durability & state periodically via `AC 15 Sub 14`.

### 3. Reaching Shore, Breaking the Raft & Dismounting on Foot
- **Client &rarr; Server (`AC 15 Sub 10` / `0F 0A`)**:
  - `0F 0A 10 90 BB` (Client clicks dry land/shore to dismount).
- **Server Responses & Broadcasts**:
  1. **Final State**: `AC 15 Sub 14` (`0F 0E 10 [CharID] D6 01 00 00 00 00`).
  2. **Break Notice Banner**: `AC 23 Sub 9` (`17 09 10 01` &rarr; "The wooden raft broke apart upon landing on the shore!").
  3. **Vehicle Destroyed**: `AC 15 Sub 15` (`0F 0F [CharID] [VehicleID: 90 BB]`).
  4. **Walking Mode**: `AC 15 Sub 11` (`0F 0B 10 [CharID]`).
- **Client &rarr; Server (`AC 15 Sub 13` / `0F 0D`)**:
  - Acknowledges dismount completion.
