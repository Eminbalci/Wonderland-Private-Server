# Combat, PK Battlefield, and Encounter Validation Specification

## 1. Combat & PK Memory Structures

The client validates Player-Killing (PK) combat and turn-based battle encounters using the following player fields:

| Offset | Type | Field Name | Validation & Error Trigger |
|---|---|---|---|
| `+0x20` | `int` | `PositionX` | Distance verification: $|X_{\text{target}} - X_{\text{player}}| < 271$ pixels |
| `+0x24` | `int` | `PositionY` | Distance verification: $|Y_{\text{target}} - Y_{\text{player}}| < 271$ pixels |
| `+0x20b5` | `char` | `FishingFlag` | If `0x01`, blocks PK. Alert: `"Fishing, can't act"` |
| `+0x20eb` | `char` | `BathingFlag` | If `0x01`, blocks PK. Alert: `"Bathing, can't act"` |
| `+0x20f6` | `char` | `CollectingFlag` | If `0x01`, blocks PK. Alert: `"Collecting, can't act"` |
| `+0x2091` | `char` | `BusyFlag` | If `0x01`, blocks PK. Alert: `"Unable to apply, player is busy"` |
| `+0x1f16` | `char` | `CupidState` | If `0x04`, blocks PK. Alert: `"Can't PK Cupid"` |
| `+0x1eff` | `char` | `TeamMode` | If `0x02`, indicates player is in party |
| `+0x21bc` | `int` | `PartyLeaderID` | If target ID matches player's party ID, blocks PK: `"Can't PK teammate"` |
| `+0x2231..3` | `char` | `StallModes` | Non-zero values block PK. Alert: `"Can't PK Stall user"` |
| `+0x235c` | `short` | `MapPKStatus` | Map PK policy check |

---

## 2. Key Decompiled Subroutines

### 2.1 PK Challenge Checker (`FUN_003a6f18`)
* **Signature:** `void FUN_003a6f18(int param_1)`
* **Logic:**
  1. Validates map PK permission (`+0x235c`) using `FUN_0049606c` against map database `PTR_DAT_004c89f0`.
  2. Blocks PK if character is fishing (`+0x20b5`) or bathing (`+0x20eb`).
  3. Scans matrix of 9 short pointers at `PTR_DAT_004c9044` for PK blacklist zones; displays `"Can't PK here"` on match.
  4. Initiates combat challenge packet: **Opcode 11, Sub-opcode 2, PK Type 3**.

### 2.2 PK Distance Validator (`FUN_003a7154`)
* **Signature:** `void FUN_003a7154(int param_1)`
* **Logic:**
  1. Confirms local player is not currently engaged in combat (`*PTR_DAT_004c8810 == 0`).
  2. Calculates spatial distance between local player and target:
     $$\Delta X = |X_{\text{target}} - X_{\text{player}}| < 271 \text{ pixels (0x10f)}$$
     $$\Delta Y = |Y_{\text{target}} - Y_{\text{player}}| < 271 \text{ pixels (0x10f)}$$
     If either coordinate delta $\ge 271$, cancels encounter initiation.
  3. Verifies target stall mode (`+0x2231..3`) and teammate identity (`+0x21bc`).
  4. Dispatches combat initiation packet via `FUN_002d6994`.

---

## 3. Network Protocol Packet Format (`Opcode 11` / `AC 11`)

### Combat Initiate Packet (Client $\to$ Server)
* **Opcode:** `11`
* **Sub-Opcode:** `2`
* **Payload Structure:**
  * `pk_type` (1 byte): `3` (Challenge Duel), `2` or `5` (Direct PK encounter).
  * `target_id` (4 bytes): Unique Character ID.
  * `target_index` (2 bytes): Client-side map session index of target.
