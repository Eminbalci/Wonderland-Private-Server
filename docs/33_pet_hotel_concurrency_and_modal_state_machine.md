# Pet Hotel Concurrency and Modal State Machine Specification

## Overview

This specification details the architecture, memory model, and network protocol for the Pet Hotel storage facility in Wonderland Online. The Pet Hotel allows players to store companions when their active party limit (4 pets) is reached, and retrieve them as team slots become available.

---

## 1. Client Memory Layout & Modal Lock Architecture

### 1.1 Memory Model (`Form_PetHotel`)

In `aLogin.exe` (and `Main.exe`), the Pet Hotel window is managed by the VCL form `Form_PetHotel` located in `aLogin_decompiled.c`:

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `void*` | `VTable` | DirectDraw form interface virtual table |
| `+0x129` | `byte` | `StorageLock1` | Storage manager busy flag (checked during transaction dispatch) |
| `+0x12c` | `byte` | `StorageLock2` | Storage manager queue lock |
| `+0x174` | `byte` | `ModalInputLock` | Form modal interaction guard byte (`0` = active/unlocked, `1` = waiting/locked) |

### 1.2 The Permanent Window Lock Defect

When a player clicks Deposit or Withdraw:
1. The client subroutine `FUN_0022390b` / `FUN_00223cb3` sets `[Form_PetHotel + 0x174] = 1`.
2. All buttons in `Form_PetHotel` (Deposit, Withdraw, Close, Select) inspect `[Form_PetHotel + 0x174]`. While non-zero, all mouse clicks are discarded.
3. The client expects two discrete unlock packets from the server upon transaction resolution:
   - `AC 31:8`: Resets storage manager locks (`+0x129`, `+0x12c`).
   - `AC 31:12`: Invokes subroutine `FUN_002ea4c5`, which sets `[Form_PetHotel + 0x174] = 0` and calls `RefreshForm` (`FUN_002242d0`), restoring UI button responsiveness.
4. Previously, the server emitted `AC 31:8` but omitted `AC 31:12` on transaction success, and omitted both on error/validation exits. Consequently, the Pet Hotel modal remained permanently frozen after a single transaction.

---

## 2. Companion Withdrawal Memory Sequencing

### 2.1 The Premature Slot Erasure Defect

Client companion withdrawal follows a strict data copy handshake:
- Client subroutine `FUN_003b337c` processes `AC 31:5`:
  `AC 31:5 [hotelSlot, teamSlot]`
  Copies the companion attributes from the hotel slot cache into the active party companion structure in client memory.
- Client subroutine `FUN_003b3320` processes `AC 31:4`:
  `AC 31:4 [hotelSlot]`
  Clears the hotel slot cache and zeroes the companion entry in hotel memory.

Previously, the server transmitted `AC 31:4` *before* `AC 31:5`:
1. `AC 31:4` zeroed the companion entry in client hotel memory.
2. `AC 31:5` attempted to copy from the hotel slot to the team slot, copying all-zero data.
3. The companion appeared corrupted or disappeared from the client interface until relogging.

### 2.2 Correct Handshake Order

```
Client                              Server
  |                                   |
  |--- AC 31:2 [hotelSlot] ---------->|  (Withdraw request)
  |                                   |
  |<-- AC 31:5 [hotelSlot, teamSlot] -|  1. Copy hotel record to team memory
  |<-- AC 31:4 [hotelSlot] -----------|  2. Zero hotel slot
  |<-- AC 15:1 / PetListPacket -------|  3. Synchronize full party roster
  |<-- AC 31:8 -----------------------|  4. Release storage locks
  |<-- AC 31:12 ----------------------|  5. Reset Form_PetHotel modal lock & refresh
  |                                   |
```

---

## 3. Implementation Details (`Src/Network/ActionCodes/AC31.cs`)

### 3.1 Unlocking on Validation and Error Exits

On all capacity or validation failure paths (insufficient slots, duplicate companions, battle checks), both `AC 31:8` and `AC 31:12` are dispatched alongside the error notice:

```csharp
if (team.Count + incoming.Length > 4 || hotel.Count + outgoing.Length > 10)
{
    p.SendSystemMessage(" Not enough free pet slots.");
    p.Send(Tools.FromFormat("bb", 31, 8));
    p.Send(Tools.FromFormat("bb", 31, 12));
    return;
}
```

### 3.2 Withdrawal Sequence Implementation

```csharp
for (int i = 0; i < withdrawals.Length; i++)
{
    byte hotelSlot = withdrawals[i];
    var pet = incoming[i];
    byte slot = 1;
    while (team.ContainsKey(slot)) slot++;
    pet.Slot = slot; pet.ClientSlot = 0; pet.IsBattle = false; pet.IsRide = false;
    team[slot] = pet;
    p.RegisterClientPet(pet);

    // 1. Native 31:5 copies hotel slot to team slot in client memory before 31:4 clears it.
    p.Send(Tools.FromFormat("bbbb", 31, 5, hotelSlot, pet.ClientSlot));

    // 2. Native 31:4 removes record from hotel slot.
    p.Send(Tools.FromFormat("bbb", 31, 4, hotelSlot));
}
```

---

## 4. Wire Protocol Summary

| ActionCode | SubCode | Direction | Parameters | Functional Description |
|---|---|---|---|---|
| `31` | `2` | Client $\to$ Server | `hotelSlot: byte` | Withdraw companion from hotel slot |
| `31` | `3` | Client $\to$ Server | `teamSlot: byte` | Deposit companion from party slot |
| `31` | `4` | Client $\to$ Server | `teamCount`, `teamSlots`, `hotelCount`, `hotelSlots` | Simultaneous exchange |
| `31` | `3` | Server $\to$ Client | `hotelSlot: byte`, `clientSlot: byte` | Confirm companion deposit |
| `31` | `4` | Server $\to$ Client | `hotelSlot: byte` | Clear hotel slot record |
| `31` | `5` | Server $\to$ Client | `hotelSlot: byte`, `clientSlot: byte` | Transfer hotel companion data to party slot |
| `31` | `8` | Server $\to$ Client | (None) | Reset storage manager lock flags |
| `31` | `12` | Server $\to$ Client | (None) | Clear `Form_PetHotel` modal input lock (`+0x174`) and refresh |
