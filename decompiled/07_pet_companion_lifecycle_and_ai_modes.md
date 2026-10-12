# Pet & Companion Lifecycle and AI Modes Specification

## 1. Companion Memory Offsets & Summon Slots

The client tracks companion pets and mountable vehicles within the local player structure using the following memory offsets:

| Offset | Data Type | Field Name | Functional Meaning & State Operations |
|---|---|---|---|
| `+0x21e8` | `int[4]` | `CompanionArray` | Base memory pointers to the 4 companion pet slots |
| `+0x2230` | `byte` | `ActivePetIndex` | Index (`0..3`) of the currently active companion |
| `+0x1efc` | `byte` | `SummonStatus` | Located inside pet struct: `1` = summoned, `0` = dismissed / standby |
| `+0x1eff` | `byte` | `RidingState` | Located inside player struct: `1` = mounted, `0` = walking |
| `+0x1f16` | `char` | `ConflictState` | State conflict flag; prevents summon / mount toggle if `0x04` |
| `+0x121` | `byte` | `AIBehaviorMode` | Located inside pet struct: current AI behavior role |

---

## 2. Key Decompiled Subroutines

### 2.1 Active Pet Summoner (`FUN_003de310`)
* **Signature:** `void FUN_003de310(int param_1)`
* **Execution Logic:**
  1. Loops through all 4 pet slots (`param_1 + 0x21e8 + slot * 4`).
  2. Sets the summon status byte `+0x1efc` of each inactive slot's structure to `0`.
  3. Verifies conflict state: if player state `+0x1f16` is `0` or `0x04` and riding state `+0x1eff` is `0`, retrieves the pet structure matching active index `+0x2230`.
  4. Sets active pet's summon status `+0x1efc` to `1`.
  5. Sends summon packet: **Opcode 15, Sub-opcode 4** (`AC 15:4`).

### 2.2 Pet Mode Transition Mapper (`FUN_003e9898`)
* **Signature:** `undefined1 FUN_003e9898(int param_1, int param_2)`
* **Execution Logic:**
  Translates client interface actions into server-side AI companion behavior modes:
  * **Combat Mode (`0`):** Default active combat state when summoned.
  * **Rest Mode (`1`):** Triggered on command hashes `0x8b8`, `0x8d4`, `0xcbc`, `0x10a4`, `0x148c`. Companion recovers HP/SP and does not enter battle.
  * **Stall Mode (`2`):** Triggered on command hashes `0x91b`, `0xd03`, `0x10eb`, `0x14d3`. Pet holds commercial vending stall.
  * **Wandering Mode (`6`):** Triggered on hashes `0xafe`, `0x91c`-`0x92b`, `0x95e`, `0xaf4`-`0xafb`, `0xb1a`, `0xb0a`-`0xb0b`, `0xd46`, `0x112e`, `0x12ea`, `0x1516`, `0x16d2`. Companion wanders autonomously within leash radius.
  * Writes the resulting mode byte to `active_pet + 0x121`.

---

## 3. Network Protocol Packet Format (`Opcode 15`)

| Sub-Opcode | Action Name | Direction | Payload Structure |
|---|---|---|---|
| `15:2` | Dismiss Pet | Client $\to$ Server | `OwnerID: UInt32`, `SlotIndex: Byte` |
| `15:4` | Summon Pet | Client $\to$ Server | `OwnerID: UInt32`, `PetTID: UInt32`, `Level: Byte`, `Name: String` |
| `15:11` | Mount Vehicle / Pet | Client $\to$ Server | `SlotIndex: Byte`, `PetTID: UInt32` |
| `15:12` | Dismount | Client $\to$ Server | `SlotIndex: Byte`, `PetTID: UInt32` |
| `15:15` | Reborn Ascension | Client $\to$ Server | `SlotIndex: Byte` |
