# Combat & PK Battle Decompiled Specifications

This document outlines the client-side combat systems, Player-Killing (PK) validations, step encounters, and coordinate range checks extracted from `alogin_analyzed/combat_battle.c`.

---

## 1. Memory Offsets & Player States

During PK validation, the client checks the target character structure using the following memory offsets relative to the player base pointer:

| Offset | Data Type | Game State represented | Action on PK Match / Error Triggered |
|---|---|---|---|
| `+0x20` | `int` | X Coordinate | Distance verification (difference must be `< 271` pixels) |
| `+0x24` | `int` | Y Coordinate | Distance verification (difference must be `< 271` pixels) |
| `+0x20b5` | `char` | Fishing Flag | If `\x01`, blocks PK. Alert: `"Fishing, can't act"` or `"In fishing"` |
| `+0x20eb` | `char` | Bathing (Hot Spring) | If `\x01`, blocks PK. Alert: `"Bathing, can't act"` |
| `+0x20f6` | `char` | Collecting (Gathering) | If `\x01`, blocks PK. Alert: `"Collecting, can't act"` |
| `+0x2091` | `char` | Busy (Trading/Dialog) | If `\x01`, blocks PK. Alert: `"Unable to apply, player is busy"` |
| `+0x1f16` | `char` | State Type Code | If `\x04`, player is in Cupid state. Alert: `"Can't PK Cupid"` |
| `+0x1eff` | `char` | Team Mode | If `\x02`, indicates player is in a party/team |
| `+0x21bc` | `int` | Party Leader / Member ID | Used during team PK checks. If equals current ID, blocks PK. Alert: `"Can't PK teammate"` |
| `+0x2231` | `char` | Stall State 1 | If non-zero, blocks PK. Alert: `"Can't PK Stall user"` |
| `+0x2232` | `char` | Stall State 2 | If non-zero, blocks PK. Alert: `"Can't PK Stall user"` |
| `+0x2233` | `char` | Stall State 3 | If non-zero, blocks PK. Alert: `"Can't PK Stall user"` |
| `+0x235c` | `short` | Target CharID / Map PK status | Target identification |

---

## 2. Key Decompiled Functions

### `FUN_003a6f18` (PK Challenge Checker)
- **C Signature**: `void FUN_003a6f18(int param_1)`
- **Logic**:
  1. Validates if the current map allows PK by testing target map ID (`+0x235c`) using `FUN_0049606c` on the map database pointer `PTR_DAT_004c89f0`.
  2. Checks if the player is busy fishing (`+0x20b5`) or bathing (`+0x20eb`). If so, prints alerts and aborts.
  3. Scans a matrix of 9 short pointers starting at `PTR_DAT_004c9044` to test if the map zone is blacklisted for PK. If match found, triggers: `"Can't PK here"`.
  4. Retrieves the target session index at `*(int *)(*(int *)PTR_DAT_004c87e0 + 0x7b)`. If target has PK flag enabled, prompts duel packet.
  5. Initiates packet dispatch with parameters: Opcode `11`, Sub-opcode `2`, PK Type `3`.

### `FUN_003a7154` (PK Validator & Distance Check)
- **C Signature**: `void FUN_003a7154(int param_1)`
- **Logic**:
  1. Verifies that the player is not currently in a battle (`*PTR_DAT_004c8810 == 0`).
  2. Compares coordinates between the player (`PTR_DAT_004c98f4`) and target (`PTR_DAT_004c9110 + target_index * 4`):
     $$\Delta X = |X_{target} - X_{player}| < 271 \text{ pixels (0x10f)}$$
     $$\Delta Y = |Y_{target} - Y_{player}| < 271 \text{ pixels (0x10f)}$$
     If either is exceeded, prints distance alert.
  3. Verifies if target is in Stall mode (`+0x2231`, `+0x2232`, `+0x2233`). Triggers: `"Can't PK Stall user"`.
  4. Verifies teammate relationship: if target `+0x1eff` is `0x02` and target party ID `+0x21bc` equals the player's ID, aborts with: `"Can't PK teammate"`.
  5. Verifies client's local PK setting: if `PTR_DAT_004c8d3c` is inactive and character setting `PTR_DAT_004c8b88 + 0x205` is `0x02`, blocks with: `"You PK is turned off"`.
  6. If all checks pass, calls `FUN_002d6994` to send combat start packet to the server.

---

## 3. Network Protocol Packet Format

### PK Combat Initiate Packet (Client -> Server)
- **Opcode**: `11` (Combat Action)
- **Sub-opcode**: `2` (Initiate Combat)
- **Structure**:
  - `pk_type` (1 byte): `3` for Challenge, `2` or `5` for direct PK battle.
  - `target_id` (4 bytes): Character ID / Unique target identifier.
  - `target_index` (2 bytes): Client-side map session index of target.
