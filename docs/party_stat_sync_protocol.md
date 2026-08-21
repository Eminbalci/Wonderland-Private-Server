# Party HP/SP Synchronization and AC 13 Protocol Technical Specification

## Overview
This document specifies the reverse-engineered Wonderland Online (WLO) party synchronization protocol, including `AC 13:6` (`_13_6Data`), party member stat synchronization, and client-side memory structures for party HUD status display (`FUN_004245c8`, `FUN_00430d58`).

---

## 1. Root Cause Analysis
- **Symptom**: Party members saw teammates with empty HP bars and tooltip `Hp: 0 / MaxHP` despite having full health.
- **Decompiled Client Trace (`aLogin_decompiled.c:437977`)**:
  `FUN_004245c8` handles `AC 13:6` packet stream. It iterates over party members and expects a packed struct for each member:
  1. `LeaderID` (4 Bytes uint32)
  2. `MemberID` (4 Bytes uint32)
  3. `IsLeader` (1 Byte: 1 if leader, 0 otherwise)
  4. `Job` (1 Byte)
  5. `NameLength` (1 Byte) + `Name` (Bytes)
  6. `CurHP` (2 Bytes ushort)
  7. `MaxHP` (2 Bytes ushort)
  8. `Element` (1 Byte)
  9. `Level` (1 Byte)
  10. `Body` (2 Bytes ushort)
- **Previous Implementation**:
  Server previously sent `[13, 6, LeaderID, MemberCount, MemberID1, MemberID2...]`, which resulted in offset misalignments, unpopulated CurHP/MaxHP (`+0x1f84`), and 0 HP rendering in the party status window.

---

## 2. Protocol Specification

### `AC 13:6` (Party Roster & Status Sync)
- **Direction**: Server -> Client
- **Trigger**: Party creation, member join, member leave, leader transfer, map warp-in, and stat changes (`Send8_1`).
- **Packet Structure**:
```
[Header: 13, 6]
For each member in party:
  LeaderID   : uint32 (4 bytes)
  MemberID   : uint32 (4 bytes)
  IsLeader   : uint8  (1 byte, 1 or 0)
  Job        : uint8  (1 byte)
  NameLength : uint8  (1 byte)
  Name       : byte[] (NameLength bytes)
  CurHP      : uint16 (2 bytes)
  FullHP     : uint16 (2 bytes)
  Element    : uint8  (1 byte)
  Level      : uint8  (1 byte)
  Body       : uint16 (2 bytes)
```

### `AC 13` Client SubCodes
- `AC 13:1`: Invite to Party / Join Request (`[13, 1, TargetCharID(4B)]`)
- `AC 13:2`: Accept Party Request (`[13, 2, TargetCharID(4B)]`)
- `AC 13:3`: Decline Party Request (`[13, 3, TargetCharID(4B)]`)
- `AC 13:4`: Leave Party (`[13, 4]`) -> Clears client party state
- `AC 13:9`: Kick Member (`[13, 9, MemberCharID(4B)]`)
- `AC 13:10`: Transfer Leader (`[13, 10, NewLeaderCharID(4B)]`)

---

## 3. Server Implementation Details

### Files Modified
- [`wlo.pserver.core/Game/Player.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Player.cs):
  - Updated `_13_6Data` property to serialize complete member stat blocks (`LeaderID`, `MemberID`, `IsLeader`, `Job`, `Name`, `CurHP`, `FullHP`, `Element`, `Level`, `Body`).
  - Updated `LeaveParty()` to update remaining members' references and broadcast updated roster, while dispatching `AC 13:4` to the exiting player.
  - Updated `JoinParty()` and `CreateParty()` to maintain synchronized party roster.
- [`wlo.pserver.core/Game/PlayerRelated/Equip.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Equip.cs):
  - In `CurHP` and `CurSP` accessors: Auto-fallback `m_curhp = FullHP` and `m_cursp = FullSP` if uninitialized / non-positive.
  - In `Send8_1(bool levelup)`: Initialize `CurHP` and `CurSP` to full if non-positive, and cascade `player.BroadcastPartyUpdate()` to party members on stat changes.
- [`wlo.pserver.core/Network/ActionCodes/AC13.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Network/ActionCodes/AC13.cs):
  - Implemented client action code 13 dispatcher supporting subactions 1, 2, 3, 4, 9, 10.
- [`wlo.pserver.core/wlo.pserver.core.csproj`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/wlo.pserver.core.csproj):
  - Registered `Network\ActionCodes\AC13.cs` in the project compile items.

---

## 4. Edge Cases Handled
1. **Zero / Negative HP on Login**: `CurHP` and `CurSP` properties check `m_curhp <= 0 && FullHP > 0` and fallback to `FullHP`, preventing zero-HP party member HUD sync.
2. **Solo Player Status**: `_13_6Data` gracefully handles non-party state by falling back to packing `this` player.
3. **Leaving Party**: Sending `AC 13:4` with `CharID` clears the leaving player's UI, while remaining party members receive the revised roster with correct leader ID.
