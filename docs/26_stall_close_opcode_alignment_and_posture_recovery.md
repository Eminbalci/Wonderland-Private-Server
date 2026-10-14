# Technical Specification: Native Stall Closure Opcode Alignment and Posture Recovery

## 1. Overview
Investigation into client stall termination revealed that clicking "End Stall" left the player character immobilized in a sitting vending posture with the client displaying the toast error `"Stall is in use"`. Analysis of the decompiled binary `aLogin.exe` identified the opcode routing table and player stall mutex flags responsible for this behavior.

## 2. Binary Disassembly & Root Cause Analysis

### 2.1 Action Code 23 Jump Table (aLogin.exe)
In `aLogin.exe`, network packets with Action Code 23 (`0x17`) are processed through a virtual jump table at VA `0x002E5A1F` (File offset `0x2D4E1F`):

* **Sub-code 30 (`0x1E`) at VA `0x2E62C7`:** Calls `0x0043AA94` (`FUN_004546e8`). Confirms private shop creation and sets player stall mutex flag:
  ```c
  *(actor + 0x2231) = 1;
  ```
* **Sub-code 31 (`0x1F`) at VA `0x2E62DB`:** Calls `0x0043ADC4` (`FUN_00454a18`). Updates item/appearance data for remote stalls observed on the map.
* **Sub-code 32 (`0x20`) at VA `0x2E62EF`:** Calls `0x0043B064` (`FUN_00454cb8`). Handles native stall closure for the local player:
  ```c
  FUN_00014334(param_2, 2, 4, &local_8);
  iVar2 = FUN_00112f58(DataPool, local_8);
  if (iVar2 == local_player->CharID) {
      local_player->vtable[+0x28](); // Calls 0x0043B260
  }
  ```
  `0x0043B260` clears shop state registers and sets:
  ```c
  *(actor + 0x2231) = 0;
  ```
* **Sub-code 33 (`0x21`) at VA `0x2E6303`:** Calls `FUN_00454db0`. Handles pet stall closure and clears:
  ```c
  *(actor + 0x2232) = 0;
  ```

### 2.2 Movement Gate & Mutex Check
Subroutine `FUN_004558c4` checks player stall flags before processing user movement input:
```c
if ((*(actor + 0x2231) != 0) || (*(actor + 0x2232) != 0) || (*(actor + 0x2233) != 0)) {
    ShowToast("Stall is in use");
    return;
}
```
Because the server previously responded to stall closure with sub-code 31 instead of sub-code 32, `*(actor + 0x2231)` remained non-zero, trapping the player in the stall state.

### 2.3 Posture Acknowledgement
In `Src/Network/ActionCodes/AC32.cs`, emote/posture updates (`AC 32:2`) were dispatched via `p.CurMap?.Broadcast(s, "Ex", p.CharID)`, deliberately excluding the sending client. Consequently, the vendor client never received server confirmation to revert from emote 1 (sitting) to emote 0 (standing).

## 3. Implementation Details

### 3.1 PlayerStallBazaarEngine.cs
* **Method:** `BuildStallNativeClosePacket(uint vendorCharId, byte subCode = 32)`
  * **Parameters:**
    * `vendorCharId` (`uint`): Unique character identifier of the vendor.
    * `subCode` (`byte`, default `32`): Protocol sub-code (`32` for player shop, `33` for pet stall, `31` for legacy fallback).
  * **Returns:** `SendPacket` containing `[AC 23, SubCode, vendorCharId (32-bit)]`.
* **Method:** `CloseStall(Player vendor)`
  * **Parameters:**
    * `vendor` (`Player`): Player closing their stall.
  * **Returns:** `void`.
  * **Behavior:**
    1. Removes stall from `m_stalls` concurrent dictionary.
    2. Resets `vendor.Emote = 0` and dispatches `BuildSittingPosePacket(vendor.CharID, 0)` directly to vendor (`vendor.Send`) and map peers (`"Ex"`).
    3. Dispatches `AC 53:3` ACK.
    4. Clears kiosk appearance (`AC 5:16 0`) to vendor and map peers.
    5. Clears signboard text (`AC 56:30 ""`) to vendor and map peers.
    6. Dispatches `AC 23:32` to clear local stall flag `*(actor + 0x2231) = 0`.
    7. Dispatches `AC 23:33` to clear pet stall flag `*(actor + 0x2232) = 0`.
    8. Dispatches `AC 23:31` for backward compatibility.
    9. Calls legacy `StallManager.CloseStall(vendor)`.

### 3.2 AC32.cs (Action Code 32 Handlers)
* **Method:** `Recv1(Player p, RecievePacket cp)` / `Recv2(Player p, RecievePacket cp)`
  * **Parameters:**
    * `p` (`Player`): Client session instance.
    * `cp` (`RecievePacket`): Incoming posture packet.
  * **Behavior:**
    * Unpacks posture action byte.
    * Updates `p.Emote`.
    * Dispatches `s` packet to `p.Send(s)` in addition to `p.CurMap?.Broadcast(s, "Ex", p.CharID)`.

## 4. Exceptions and Edge Cases Handled

* **Null Vendor Check:** Gracefully returns if `vendor` or `vendor.CurMap` is null.
* **Concurrent Stall Access:** `m_stalls.TryRemove` prevents double-close race conditions.
* **Legacy Manager Synchronization:** Handled within try-catch block to prevent uncaught exceptions from aborting packet dispatch.
* **Multi-Packet Redundancy:** Dispatches opcodes 32, 33, and 31 sequentially to support varied client modifications and vanilla binary routines.

## 5. Verification

* **Automated Unit Verification:** `.codex-verify/Test-StallPacket.ps1` verifies:
  * Full registration on `AC 23:30`.
  * Proper closure on `AC 23:31` input.
  * Dispatch of `AC 23:32` (clearing `0x2231`).
  * Dispatch of `AC 23:33` (clearing `0x2232`).
  * Dispatch of `AC 53:3` and `AC 5:16` visual resets.
  * Reversion of `player.Emote` to 0.
