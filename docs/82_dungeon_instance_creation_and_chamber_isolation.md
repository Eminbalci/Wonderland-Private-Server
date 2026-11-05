# 82 - Dungeon Instance Creation and Chamber Isolation Specification

## 1. Executive Summary & Root Cause Analysis

The Wonderland Online duplicate mission (副本任務) instanced dungeon subsystem enables parties to enter isolated challenge chambers (Maps 30000+, such as the 20-Round Challenge Map 30001) with multi-wave mob encounters, boss encounters, and reward phases.

Previously, players attempting to create or enter a dungeon chamber encountered the failure symptom:
`"yok hala dungeon instance oluşturmuyor"` (dungeon instance is still not creating/working in the client).

Reverse engineering analysis of the native client binary (`decompiled/aLogin_decompiled.c`, lines 180470–183150 and `decompiled/56_instance_dungeon_duplicate_mission_stage_controller.md`) revealed four fundamental root causes:

1. **Packet Reading Pointer Offset in `AC85.cs`:**
   In `RecievePacket`, `SetPtr(4)` is the default pointer pointing to ActionCode (byte 4). When handlers such as `Recv3` called `r.Unpack16()` without explicitly setting `r.SetPtr(6)`, the unpacker read bytes 4 and 5 (Action Code 85 and SubCode 3) rather than the actual incoming payload (`MapID` and `MaxMembers`), resulting in corrupt parameters.

2. **Opcode Inversion & Client Error Code Collision (`FUN_001a10c4`):**
   In Delphi unit `MZ_DupMis.pas`, `FUN_001a10c4` processes incoming server responses for Action Code 85 (`0x55`):
   ```c
   bVar1 = *(byte *)(local_8 + 1); // SubCode / Status byte
   switch(bVar1) {
     case 0: // SUCCESS! Client confirms entry and sends 0x55:0x01
     case 1: "Max instance amount" (Error dialog)
     case 2: "LV too low"
     case 3: "Finished today"
     case 8: "Can't enter instance here"
     case 10: "Instance map purged"
     case 11: "Unavailable yet"
     case 12: "Make instance later"
   }
   ```
   `InstanceDungeonEngine.cs` was previously responding with `Pack8(85); Pack8(1);` (SubCode 1). The client evaluated `bVar1 == 1` and immediately displayed the modal error popup `"Max instance amount"`, aborting instance creation.

3. **Client Roster Layout Mismatch (`FUN_0019da10`):**
   Delphi subroutine `FUN_0019da10` expects the server response to carry SubCode 0 with a strict payload schema:
   `[MapId: uint16] [MemberCount: uint8] { [NameLength: uint8] [CharName: ASCII] [CharId: uint32] }...`
   Without this layout, the client could not populate the room roster and refused to open the stage chamber window.

4. **Missing Party Warp & Map Singleton Collision:**
   - In `StartInstance`, the engine previously logged a placeholder comment (`// In live server, player lookup dispatches startPkt`) and never warped players to the dungeon map.
   - `MapManager.cs` only maintained a singleton cache (`_mapCache`), meaning any two parties entering Map 30001 would share the same map instance, colliding on monsters, player lists, and NPC stages.

---

## 2. Architecture & Implementation Details

### 2.1 Chamber Isolation & Map Lifecycle (`MapManager.cs`, `Map.cs`)

To isolate dungeon chambers, `MapManager` was extended with dynamic instance map lifecycle management:
- `CreateInstanceMap(ushort baseMapId)`: Instantiates a dedicated `GameMap` object with its own spawn table and NPC list, completely independent of the global `_mapCache`.
- `RegisterInstanceMap(uint instanceId, GameMap map)`: Stores the isolated map in a concurrent registry.
- `UnregisterInstanceMap(uint instanceId)`: Cleans up when the instance room is disbanded.
- `ActiveMaps`: Aggregates cached overworld maps and active instance chambers for seamless server-wide ticks and player lookups.

To safely transition players into isolated maps without recursion or cache lookup, `GameMap.TeleportToInstance` was implemented:
```csharp
public bool TeleportToInstance(GameMap targetInstanceMap, Player sender, WarpData warp)
{
    sender.Send(Tools.FromFormat("bb", 20, 7)); // Freeze
    // Coordinate initialization frames (AC 23:32, 23:112, 23:132)
    Warp_Out(0, sender, warp, false);
    targetInstanceMap.Warp_In(TeleportType.CmD, sender, warp, 0);
    return true;
}
```

### 2.2 Wire Protocol Alignment & Roster Serialization (`InstanceDungeonEngine.cs`)

The engine now serializes member rosters strictly according to `FUN_0019da10` requirements:
```csharp
SendPacket p = new SendPacket();
p.Pack8(85);
p.Pack8(0); // SubCode 0: Success & Roster Sync
p.Pack16(room.MapId);
p.Pack8((byte)room.Members.Count);
foreach (var mem in room.Members)
{
    p.PackStringN(mem.CharName ?? "Member"); // 1-byte length prefix + string bytes
    p.Pack32(mem.CharId);                   // 4-byte uint32
}
```

When a player launches an instance (`StartInstance`):
1. Pre-warp coordinates are preserved in `memPlayer.PrevMap` for safe evacuation or forfeiture return.
2. Every party member is warped to `room.InstanceMap` at spawn coordinates $(1000, 1000)$.
3. Action Code 85 SubCode 5 (`0x55:0x05`) status frame is dispatched to notify clients of active stage progression.

When a player leaves or forfeits (`LeaveRoom`):
1. If the player is inside `room.InstanceMap`, they are warped back to their preserved `PrevMap` (or default safe map 12000).
2. Action Code 85 SubCode 2 (`0x55:0x02`) leave confirmation is dispatched.
3. If no members remain, `room.InstanceMap` is unregistered and disposed.

### 2.3 Wire Handler Calibration (`AC85.cs`)

Every sub-opcode handler in `AC85.cs` now invokes `r.SetPtr(6)` before unpacking arguments:

| Sub-Opcode | Direction | Description | Parameters |
| :---: | :---: | :--- | :--- |
| `0x55:0x01` | C $\to$ S | Create / Enter Instance | `[MapID: uint16]` (optional), launches if already in pending room |
| `0x55:0x02` | C $\to$ S | Exit / Leave Instance | Forfeits and teleports back to overworld |
| `0x55:0x03` | C $\to$ S | Create Room | `[MapID: uint16] [MaxMembers: uint8]` |
| `0x55:0x04` | C $\to$ S | Query / Pre-join | `[RoomID: uint32/uint16]` |
| `0x55:0x05` | C $\to$ S | Join Room | `[RoomID: uint32/uint16]` |
| `0x55:0x06` | C $\to$ S | Exit Room | Leaves current room |
| `0x55:0x07` | C $\to$ S | Map Warp ACK | Returns mobility unfreeze `Tools.FromFormat("bb", 20, 8)` |
| `0x55:0x0A` | C $\to$ S | Wave Signal / Roster | Advances wave if in progress, else sends roster |
| `0x55:0x0B` | S $\to$ C | Victory / Reward | `[ClearTimeSec: uint16] [RewardID: uint16] [EXP: uint32]` |
| `0x55:0x0C` | S $\to$ C | Evacuation Purge Timer | `[RemainingSec: uint16]` |
| `0x55:0x0C` | C $\to$ S | Launch Instance | Initiates dungeon session and warps party |
| `0x55:0x0D` | C $\to$ S | Dismiss Member | `[TargetCharID: uint32]` |

---

## 3. Verification & Test Evidence

Verified using automated test harness `tests/Run-DungeonInstanceTests.ps1`:
- **Chamber Isolation:** Two calls to `CreateInstanceMap(30001)` generated distinct instances (`HashCode: 50776467 != 13915302`). PASS.
- **Map Registration:** Dynamic maps successfully registered in `MapManager` and surfaced in `ActiveMaps`. PASS.
- **Roster Serialization:** Room creation emitted wire packet `F4 44 14 00 55 00 31 75 01 ...` with SubCode 0, MapID 30001, count 1, and length-prefixed character identity. PASS.
- **Multiplayer Room Sync:** Participant `SideKick` joined room, receiving SubCode 0 roster with count 2 (`F4 44 20 00 55 00 31 75 02 ...`). PASS.
- **Party Launch & Warping:** `StartInstance` successfully transitioned `IsInProgress = true`, dispatched SubCode 5 status packets, and initiated chamber teleportation. PASS.
- **Disband & Evacuation Cleanup:** Exiting players properly warped out and empty room unregistered and disposed cleanly. PASS.
- **Compilation:** 0 errors, 0 warnings across solution.
