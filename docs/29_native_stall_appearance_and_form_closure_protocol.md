# 29. Native Stall Appearance, Table/Banner Attachment, and Form Closure Protocol

## 1. System Overview

This specification details the reverse-engineered Wonderland Online client network packets for player and custom stall visual rendering, table attachment, floating signboard display, form dismissal, and posture synchronization.

Based on decompilation and disassembly of `aLogin.exe` at:
- Sub-AC dispatch table: offset `0x2d4e1f` (indexed by Sub-AC after Action Code 23)
- Sub-AC 31 handler: offset `0x2d56db` -> `0x42a1c4`
- Sub-AC 32 handler: offset `0x2d56ef` -> `0x42a464`
- Sub-AC 165 handler: offset `0x2d79f8`

---

## 2. Reverse-Engineered Wire Protocols

### 2.1 Authentic Stall 3D Table and Signboard Banner Attachment (`AC 23:31`, S -> C)

- **Client Handler:** `0x42a1c4`
- **Purpose:** Attaches the 3D vendor kiosk table to the character model and creates the floating blue title signboard banner above the character in the world scene.
- **Trigger:** Sent upon opening personal private shop (`OpenStall`), spawning synthetic custom stalls (`SpawnCustomStall`), or replicating existing map stalls to entering players (`SyncMapStallsToPlayer`).
- **Wire Format:**

| Field | Type | Size | Description |
|---|---|---|---|
| Action Code | uint8 | 1 byte | Constant `23` (`0x17`) |
| Sub-Action Code | uint8 | 1 byte | Constant `31` (`0x1F`) |
| Vendor CharID | uint32 (LE) | 4 bytes | Character / Actor ID of the stall owner |
| Title Length | uint8 | 1 byte | Length of ASCII stall name string |
| Title String | ASCII | N bytes | Name of stall |
| Stall Type | uint8 | 1 byte | `1` = Item Private Shop, `2` = Pet Stall |

- **Multi-Record Capability:** The client loop at `0x42a1c4` supports packing multiple stalls sequentially within a single `AC 23:31` frame by advancing `esi += 6 + TitleLength`.

---

### 2.2 Authentic Stall Table and Signboard Dismissal (`AC 23:32`, S -> C)

- **Client Handler:** `0x42a464`
- **Purpose:** Destroys the 3D vendor table model and floating signboard attached to the actor.
- **Trigger:** Sent when closing personal stalls (`CloseStall`), removing custom synthetic stalls (`RemoveCustomStall`), or clearing all stalls (`ClearAllStalls`).
- **Wire Format:**

| Field | Type | Size | Description |
|---|---|---|---|
| Action Code | uint8 | 1 byte | Constant `23` (`0x17`) |
| Sub-Action Code | uint8 | 1 byte | Constant `32` (`0x20`) |
| Vendor CharID | uint32 (LE) | 4 bytes | Character / Actor ID of the stall owner |

---

### 2.3 Native Stall Form Closure (`AC 23:165`, S -> C)

- **Client Handler:** `0x2d79f8`
- **Purpose:** Closes and hides the client-side stall GUI (`Form_PrivateShop` or `Form_PetStall`). Prevents the modal form from remaining open in client state, which otherwise triggers "Stall is in use" popup errors and traps the player in vending state.
- **Trigger:** Sent to the vending player upon closing personal stall in `CloseStall`.
- **Wire Format:**

| Field | Type | Size | Description |
|---|---|---|---|
| Action Code | uint8 | 1 byte | Constant `23` (`0x17`) |
| Sub-Action Code | uint8 | 1 byte | Constant `165` (`0xA5`) |
| Target Form | uint8 | 1 byte | `1` = `Form_PrivateShop` (singleton at `0x004c9ef4`), `2` = `Form_PetStall` (singleton at `0x004c9a60`) |

---

### 2.4 Action Code 32:2 Posture Broadcast Alignment (`AC 32:2`, S -> C)

- **Handler:** `Network.ActionCodes.AC32.Recv2`
- **Issue Diagnosed:** `Recv2` was executing `p.Send(s)` which reflected posture changes back to the initiating client. When the client was stuck in vending stance without `AC 23:165 1`, any movement click triggered `AC 32:2 00` ("cancel sit"), creating an infinite echo ping-pong loop between server and client.
- **Resolution:** Removed `p.Send(s)` from `Recv2`. Posture changes are strictly broadcast to map peers via `p.CurMap.Broadcast(s, "Ex", p.CharID)`.

---

### 2.5 Clothed Synthetic Vendor Appearance (`AC 4` & `AC 5:0`, S -> C)

- **Issue Diagnosed:** Previously `BuildActorSpawnPacket` set `BodyStyle = 1` (Small_Male) with `Head = 1` and `WornCount = 0` with an empty equips array. This caused the synthetic vendor to render bald and naked.
- **Resolution:**
  - `Body`: `3` (`BodyStyle.Big_Male` - Daniel)
  - `Head`: `0` (`HairStyle_BigM.Daniel`)
  - `WornCount`: `2`
  - Worn Equips: `21004` (Daniel's Outfit) and `24004` (Daniel's Boots)
  - Dispatched via `BuildActorSpawnPacket` (`AC 4`) and `BuildActorEquipPacket` (`AC 5:0`).

---

## 3. Method Specifications

### `PlayerStallBazaarEngine.BuildStallMapStatePacket(PlayerStall stall)`
- **Parameters:** `PlayerStall stall` (Must not be null).
- **Return Type:** `SendPacket`
- **Exceptions:** Returns `null` if `stall` is null.
- **Wire Payload:** `[23, 31, VendorCharId (4B), TitleLen (1B), Title (ASCII), 1]`

### `PlayerStallBazaarEngine.BuildStallDespawnPacket(uint vendorCharId)`
- **Parameters:** `uint vendorCharId`
- **Return Type:** `SendPacket`
- **Wire Payload:** `[23, 32, VendorCharId (4B)]`

### `PlayerStallBazaarEngine.BuildStallFormClosePacket(byte formType = 1)`
- **Parameters:** `byte formType` (Default: `1` for `Form_PrivateShop`).
- **Return Type:** `SendPacket`
- **Wire Payload:** `[23, 165, formType]`

### `PlayerStallBazaarEngine.BuildActorSpawnPacket(PlayerStall stall)`
- **Parameters:** `PlayerStall stall`
- **Return Type:** `SendPacket`
- **Wire Payload:** `AC 4` actor spawn frame configured for Daniel (`Body = 3`, `Head = 0`, `WornCount = 2`, equips `21004` and `24004`).

---

## 4. Verification Evidence

Automated regression suite `.codex-verify/Test-StallPacket.ps1` runs 7 test phases:
1. Part 1: Native AC 23:30 / 23:31 personal stall flow.
2. Part 2: Synthetic vendor chat command (`:stall`) and player mobility verification.
3. Part 3: Infinite stock purchase semantics.
4. Part 4: Limited stock purchase semantics and slot depletion.
5. Part 5: Database persistence (Save, Query, Reload, Remove across SQLite & MySQL).
6. Part 6: `:removestall` and `:clearstalls` commands.
7. Part 7: Authentic wire packet tests for AC 23:31, AC 23:32, AC 23:165, AC 4 (Body 3, Head 0, Equips 21004/24004), and AC 5:0.
All 7 phases pass with zero errors.
