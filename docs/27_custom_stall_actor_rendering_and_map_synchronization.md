# Technical Specification: Custom Stall Actor Rendering and Map Synchronization

## 1. Overview
Investigation into invisible custom stalls revealed that synthetic bazaar kiosks spawned via GUI controls or the `:stall` chat command failed to appear on the client screen. While the stall entity was registered server-side in `PlayerStallBazaarEngine` with ClickID 200, the client dropped the spawn event because `AC 22:4` only updates entities already registered in the client's static `Eve.emg` binary, and the synthetic merchant actor was never instantiated via `AC 4`.

## 2. Root Cause Analysis

### 2.1 Entity Model Discrepancy
In the Wonderland Online client (`aLogin.exe` / `Main.exe`):
1. **Interactive Scene NPCs (`AC 22:4`):**
   * Indexed by `ClickID` against the map's pre-compiled `Eve.emg` definitions.
   * Sending `AC 22:4` for an unmapped `ClickID` (such as dynamically assigned `200..250`) results in `FindNpcByClickId` returning `NULL`, discarding the packet.
2. **Dynamic Actors & Player Avatars (`AC 4`):**
   * Spawns a dynamic visual entity in the client's global `DataPool` / actor roster.
   * Requires sequential packet framing:
     * `AC 4`: Entity constructor (`CharID`, `Body`, `Element`, `Level`, `MapID`, `X`, `Y`, `Head`, `Colors`, `Name`).
     * `AC 5:0`: Equipment list.
     * `AC 10:3`: Online presence state (`255 = Active`).
     * `AC 7`: Fixed coordinate placement.
3. **Stall Visual Components:**
   * `AC 32:2`: Sitting posture (`Emote = 1`).
   * `AC 5:16`: Vending kiosk table object (`1 = Display`).
   * `AC 56:30`: Floating wooden signboard banner displaying the shop name.
   * All three sub-protocols look up the vendor by `CharID`. Because `AC 4` was omitted for synthetic vendors, all visual attachments failed to attach to any actor.

### 2.2 Despawn Protocol Framing (`AC 12`)
The client's scene exit handler `FUN_004558c4` parses `AC 12` using a 15-byte fixed record:
```
[AC 12: Byte] [CharID: UInt32] [DstMap: UInt16] [DstX: UInt16] [DstY: UInt16] [PortalID: UInt16] [Flag: Byte]
```
Sending an abbreviated 5-byte packet caused deserialization failure and prevented the client from removing despawned stall models upon `/clearstalls`.

## 3. Implementation Details

### 3.1 PlayerStallBazaarEngine.cs
* **`SpawnCustomStall(uint vendorId, string vendorName, string stallTitle, ushort mapId, int x, int y, List<StallItem> items = null)`:**
  * Broadcasts the full 7-packet suite to `targetMap`:
    1. `BuildActorSpawnPacket(stall)` (`AC 4`)
    2. `BuildActorEquipPacket(stall)` (`AC 5:0`)
    3. `Tools.FromFormat("bbdb", 10, 3, stall.VendorCharId, (byte)255)` (`AC 10:3`)
    4. `BuildActorPosPacket(stall)` (`AC 7`)
    5. `BuildSittingPosePacket(stall.VendorCharId, 1)` (`AC 32:2`)
    6. `BuildKioskStatePacket(stall.VendorCharId, 1)` (`AC 5:16`)
    7. `BuildStallSignPacket(stall)` (`AC 56:30`)
* **`SyncMapStallsToPlayer(Player player, ushort mapId)`:**
  * Checks if `stall.VendorCharId` is a synthetic custom vendor not currently connected as an active player in `(player.CurMap as GameMap)?.PlayersList`.
  * Transmits `AC 4`, `AC 5:0`, `AC 10:3`, and `AC 7` before sending `AC 32:2`, `AC 5:16`, and `AC 56:30`, ensuring players entering the map after stall creation see the vendor immediately.
* **`BuildActorSpawnPacket(PlayerStall stall)`:**
  * Updated with authentic character color palettes:
    * `HairColor`: `44828`
    * `SkinColor`: `6781`
    * `ClothingColor`: `44828`
    * `EyeColor`: `6781`
* **`BuildActorDespawnPacket(uint vendorCharId)`:**
  * Updated to full 15-byte framing with zeroed trailing destination bytes to ensure clean client actor destruction.
* **`ClearAllStalls()`:**
  * Broadcasts `BuildActorDespawnPacket` to cleanly purge all synthetic vendor actors from the client renderer.

## 4. Exceptions and Edge Cases Handled

* **Connected Player vs Synthetic Stall Isolation:** Connected players opening personal stalls (`OpenStall`) do not receive redundant `AC 4` spawn packets, avoiding duplicate actor models.
* **Map Presence Null-Safety:** `(player.CurMap as GameMap)?.PlayersList` safely resolves `IMap` interface abstractions without throwing `CS1061` or `NullReferenceException`.
* **Dynamic Coordinate Positioning:** Supports arbitrary map IDs and coordinates via both GUI (`MainForm1.ExperimentalTab.cs`) and chat commands (`:stall <mapId> <x> <y> [title]`).

## 5. Verification

* **Automated Unit Verification:** `.codex-verify/Test-StallPacket.ps1` asserts:
  * Synthetic stall creation via `SpawnCustomStall`.
  * Dispatch of `AC 4`, `AC 10:3`, `AC 7`, `AC 32:2`, `AC 5:16`, and `AC 56:30` on map synchronization.
  * Clean actor removal on `ClearAllStalls`.
