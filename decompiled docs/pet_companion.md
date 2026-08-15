# Pet & Companion Decompiled Specifications

This document outlines the client-side pet summon registers, bmounted configurations, and AI behavior transition mappers extracted from `alogin_analyzed/pet_companion.c`.

---

## 1. Memory Offsets & Summon Slots

The client tracks pet companion states within the local player session structure using the following offsets:

| Offset | Data Type | Representation / Purpose | Values & Operations |
|---|---|---|---|
| `+0x21e8` | `int[4]` | Companion Array | Base pointers to the 4 pet companion slot structures |
| `+0x2230` | `byte` | Active Pet Index | Points to the active companion index (0-3) currently selected |
| `+0x1efc` | `byte` | Summon Status | Located inside the pet structure. `1` when summoned, `0` when dismissed |
| `+0x1eff` | `byte` | Riding State | Located inside player structure. `1` when mounted, `0` when walking |
| `+0x1f16` | `char` | Conflict State | Status conflict validation byte. Prevents toggle if `0x04` |
| `+0x121` | `byte` | AI Behavior Mode | Located inside pet structure. Controls current role (Combat/Rest/Stall/Wandering) |

---

## 2. Key Decompiled Functions

### `FUN_003de310` (Active Pet Summoner)
- **C Signature**: `void FUN_003de310(int param_1)`
- **Logic**:
  1. Iterates through all 4 pet slots from index `1` to `4` (slots `1` through `4` at `param_1 + 0x21e8 + slot * 4`).
  2. Sets the summon status byte `+0x1efc` of each inactive slot's pet structure to `0`.
  3. Verifies conflict state: if player state `+0x1f16` is `0` or `0x04` and riding state `+0x1eff` is `0`, retrieves the pet structure matching index `+0x2230`.
  4. Sets that pet's summon status `+0x1efc` to `1`.
  5. Sends summon packet Opcode `15` Sub-opcode `4`.

### `FUN_003e9898` (Pet Mode Transition Mapper)
- **C Signature**: `undefined1 FUN_003e9898(int param_1, int param_2)`
- **Logic**:
  1. Resolves temporary interface/command codes into server-side AI state parameters (`0-7`).
  2. Reads the index code `param_2`. If it is `0`, immediately returns `0` (fails/default).
  3. Converts interface state hashes depending on the active pointer index `param_1 + 8`:
     - **Combat Mode (Returns `0`)**: Default state when summoned.
     - **Rest Mode (Returns `1`)**: Triggered on hash matches: `0x8b8`, `0x8d4`, `0xcbc`, `0x10a4`, `0x148c`.
     - **Stall Mode (Returns `2`)**: Triggered on hash matches: `0x91b`, `0xd03`, `0x10eb`, `0x14d3`.
     - **Wandering Mode (Returns `6`)**: Triggered on hash matches: `0xafe`, `0x91c`-`0x92b`, `0x95e`, `0xaf4`-`0xafb`, `0xb1a`, `0xb0a`-`0xb0b`, `0xd46`, `0x112e`, `0x12ea`, `0x12f2`-`0x1301`, `0x1516`, `0x16d2`, `0x16da`-`0x16e9`.
  4. Modifies `active_pet + 0x121` to store the mapped AI status.

---

## 3. Network Protocol Packet Format

### Companion Action Packet (Client -> Server)
- **Opcode**: `15` (Companion Action)
- **Sub-opcode**:
  - `2` (Dismiss Pet): Payload = Owner ID (4 bytes) + Slot Index (1 byte).
  - `4` (Spawn/Summon Pet): Payload = Owner ID (4 bytes) + Pet Template ID (4 bytes) + Level (1 byte) + Name String + Extra Binds.
  - `11` (Mount/Ride Pet): Payload = Slot Index (1 byte) + Pet Template ID (4 bytes).
  - `12` (Dismount/Rest Riding): Payload = Slot Index (1 byte) + Pet Template ID (4 bytes).
  - `15` (Reborn Request): Payload = Slot Index (1 byte).
