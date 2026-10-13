# Document 64: Cooperation Battle Assembly and Challenge Chamber Controller

## 1. Executive Summary & Engine Architecture

Wonderland Online features dedicated group challenge arenas and cooperative instance rooms managed through the Cooperation Battle Assembly subsystem. Implemented in Delphi units `IN_CooperateFight.pas` and `MZ_Cooperation.pas` (decompiled across subroutines `FUN_002c6000`, `FUN_002c75a4`, `FUN_002c75f0`, `FUN_002c7624`, and `FUN_002c7ede`), this engine orchestrates room creation, ante wagers, team lobby formation, player readiness verification, and synchronous arena transitions.

The engine operates through two specialized dialog windows:
1. **`Form_ChooseCooperation` ($463 \times 375$ px):** The cooperation lobby browser, displaying 4 active challenge rooms per page with real-time occupancy counts, challenge stage identifiers, room leader names, and join/quit triggers.
2. **`Form_CreateCooperation` ($463 \times 377$ px):** The room assembly orchestrator, enabling leaders to configure stage difficulty, ante stakes (Points, Gold, Coupons), participant slots, entry requirements, and private access codes.

The subsystem communicates with the game server via Action Code `0xBA` (`186` decimal). It enforces strict validation rules, including a 4-player team cap (`"Please apply in team of 4 people"`), floor progression matching (`"Can't team, player haven't reached this floor yet"`), and stake balance checks (`"Lack Pts, can't team up"`).

```
+------------------------------------------------------------------------------------+
|                   COOPERATION BATTLE ASSEMBLY SUBSYSTEM                            |
+------------------------------------------------------------------------------------+
                                      |
         +----------------------------+----------------------------+
         | Lobby Browser: Form_ChooseCooperation                   | Room Creator: Form_CreateCooperation
         v                                                         v
  [Form_ChooseCooperation] (463x375 px)                     [Form_CreateCooperation] (463x377 px)
  - 4 Challenge Rooms per Page                              - Challenge Stage Difficulty Selector
  - Occupancy Counter: Players %d/%d                        - Stake / Ante Wagering Setup
  - Navigation: Btn_ArrowL_5 / Btn_ArrowR_5                 - Team Leader Mutex Validation
  - Room Inspection: Btn_CooperationInfo                    - Password / Private Pin Protection
         |                                                         |
         +----------------------------+----------------------------+
                                      |
                                      v
                 [Network Dispatcher: Action Code 0xBA (186)]
        Subcodes: 0x01 (Create), 0x02 (Join), 0x08 (Leave), 0x09 (Browse)
```

---

## 2. Memory Layouts & Data Structures

### 2.1 Cooperation Room Record (`0x28` = 40 Bytes)
Within the client lobby manager, active cooperation rooms received from the server are stored as 40-byte structures:

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `ushort` | `RoomId` | Unique sequential room identifier |
| `+0x02` | `ushort` | `StageId` | Target challenge arena / instance stage ID |
| `+0x04` | `byte` | `MaxPlayers` | Maximum allowed participants (typically `4`) |
| `+0x05` | `byte` | `CurrentPlayers`| Number of currently joined participants ($1..4$) |
| `+0x06` | `byte` | `StateFlags` | Room status (0 = Open, 1 = Full, 2 = In Battle, 3 = Locked) |
| `+0x07` | `byte` | `HasPassword` | `0` = Public room, `1` = Password protected |
| `+0x08` | `uint` | `RequiredFloor`| Minimum progression floor required of all members |
| `+0x0C` | `uint` | `AntePoints` | Required point stakes or entry tariff |
| `+0x10` | `char[16]` | `LeaderName` | Character nameplate string of the room host |
| `+0x20` | `char[8]` | `RoomPass` | Stored MD5/hash of room PIN (if protected) |

### 2.2 `Form_ChooseCooperation` Instance Layout (`param_1`)

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `void*` | `VTable` | Delphi component virtual method table |
| `+0x04` | `uint` | `FormHandle` | DirectDraw surface bitmap handle |
| `+0x3A` | `uint` | `BtnClose1` | Bottom close button control handle |
| `+0x3B` | `uint` | `BtnCloseS1` | Top-right small close button control handle |
| `+0x3C` | `uint` | `BtnChoose` | Room selection button |
| `+0x3D` | `uint` | `BtnCreateRoom`| Launch button for `Form_CreateCooperation` |
| `+0x3E` | `uint` | `BtnArrowLeft` | Previous page navigation arrow |
| `+0x3F` | `uint` | `BtnArrowRight`| Next page navigation arrow |
| `+0x40` | `int` | `BaseHalfWidth`| Screen center horizontal offset |
| `+0x4D` | `int*[4]` | `BtnInfo` | Array of 4 room info buttons |
| `+0x51` | `int*[4]` | `BtnPlayers` | Array of 4 roster inspect buttons |
| `+0x55` | `int*[4]` | `BtnJoin` | Array of 4 room join buttons |
| `+0x59` | `int*[4]` | `BtnQuit` | Array of 4 room leave buttons |
| `+0x10C`| `int` | `CurrentPage` | Active lobby page index ($1..N$) |
| `+0x110`| `int` | `TotalPages` | Maximum available page count |

---

## 3. UI Form Geometry, Coordinates & Controls

### 3.1 `Form_ChooseCooperation` (Lobby Browser)
- **Dimensions:** Width `0x1CF` (463 pixels), Height `0x177` (375 pixels).
- **4 Room Slots:**
  - 4 challenge room rows ($i = 1..4$) spaced vertically at stride `0x46` (70 pixels):
    $$\text{Row Y} = (i - 1) \times 70 + 130 = (i - 1) \times \text{0x46} + \text{0x82}$$
  - Row button horizontal alignment:
    - `"Btn_CooperationInfo"`: $X = \text{0x95}$ (149 pixels), dimensions $20 \times 60$ px.
    - `"Btn_CooperationPlayer"`: $X = \text{0xD2}$ (210 pixels), dimensions $20 \times 60$ px.
    - `"Btn_Join"`: $X = \text{0x10F}$ (271 pixels), dimensions $20 \times 60$ px.
    - `"Btn_CooperationQuit"`: $X = \text{0x10F}$ (271 pixels), dimensions $20 \times 60$ px (replaces `"Btn_Join"` when user is joined).
- **Navigation Controls:**
  - `"Btn_ArrowL_5"`: $X = \text{0x92}$ (146 px), $Y = \text{0x170}$ (368 px), size $17 \times 17$ px.
  - `"Btn_ArrowR_5"`: $X = \text{0xCE}$ (206 px), $Y = \text{0x170}$ (368 px), size $17 \times 17$ px.
- **Action Buttons:**
  - `"Btn_CreateCooperation"`: $X = \text{0x95}$ (149 px), $Y = \text{0x186}$ (390 px), size $20 \times 72$ px.
  - `"Btn_Close_1"`: $X = \text{0x9B}$ (155 px), $Y = \text{0x1A9}$ (425 px), size $20 \times 56$ px.

### 3.2 `Form_CreateCooperation` (Room Creation Dialog)
- **Dimensions:** Width `0x1CF` (463 pixels), Height `0x179` (377 pixels).
- **Controls:**
  - Title & PIN input fields with 14-character length restrictions.
  - Stage difficulty slider: 5 preset challenge tiers.
  - Action buttons:
    - `"Btn_Create"`: $X = \text{0x82}$ (130 px), $Y = \text{0x1A4}$ (420 px), size $20 \times 56$ px.
    - `"Btn_Cancel_1"`: $X = 200$ px, $Y = \text{0x1A4}$ (420 px), size $20 \times 56$ px.

```
+---------------------------------------------------------------------------------+
| Form_ChooseCooperation: Challenge Lobby Browser (463 x 375 px)                  |
+---------------------------------------------------------------------------------+
| [Header Title: Cooperation Challenge Chambers]                              [X] |
+---------------------------------------------------------------------------------+
| Room 1: [Dragon Cave]  Host: HeroX   [4/4 Players] [Info] [Roster] [Quit]       |
| Room 2: [Abyss Tower]  Host: Mage01  [2/4 Players] [Info] [Roster] [Join]       |
| Room 3: [Sky Arena]    Host: KnightA [1/4 Players] [Info] [Roster] [Join]       |
| Room 4: [Pirate Bay]   Host: Thief99 [3/4 Players] [Info] [Roster] [Join]       |
+---------------------------------------------------------------------------------+
| Page: [<] 1 / 5 [>]                                                             |
|                                       [Create Room]               [Close]       |
+---------------------------------------------------------------------------------+
```

---

## 4. Network Protocol, Wire Frames & Action Codes

### 4.1 Packet Action Codes
The Cooperation Battle Assembly subsystem uses Action Code `0xBA` (`186` decimal):
- **Opcode:** `0xBA`
- **Subcodes:**
  - `0x01`: Create Cooperation Room (C2S via `0x002c7ede`)
  - `0x02`: Join Selected Room (C2S via `LAB_002c640c`)
  - `0x04`: Query Room Info / Player Roster (C2S via `LAB_002c6394`)
  - `0x08`: Leave / Forfeit Cooperation Room (C2S via `FUN_002c75f0`)
  - `0x09`: Page Browse Query (C2S via `FUN_002c75a4`)
  - `0x0A`: Ready State Toggle / Match Launch (C2S)

### 4.2 C# Packet Structure Definitions

```csharp
namespace Wonderland.Network.Packets.Cooperation
{
    /// <summary>
    /// Client-to-server request to browse cooperation rooms on a specific page.
    /// Wire Header: Action Code 0xBA, Subcode 0x09.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct C2S_CooperationBrowsePacket
    {
        public byte ActionCode;       // 0xBA (186)
        public byte SubCode;          // 0x09
        public ushort RequestedPage;  // 1-indexed page number
    }

    /// <summary>
    /// Client-to-server request to create a new challenge cooperation room.
    /// Wire Header: Action Code 0xBA, Subcode 0x01.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct C2S_CooperationCreatePacket
    {
        public byte ActionCode;       // 0xBA (186)
        public byte SubCode;          // 0x01
        public ushort StageId;        // Target challenge stage
        public byte MaxPlayers;       // 4-person limit
        public uint AntePoints;       // Entry stake fee
        
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 17)]
        public string RoomTitle;      // Custom room label
        
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 9)]
        public string RoomPassword;   // Optional PIN (empty for public)
    }

    /// <summary>
    /// Client-to-server request to join an existing cooperation room.
    /// Wire Header: Action Code 0xBA, Subcode 0x02.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct C2S_CooperationJoinPacket
    {
        public byte ActionCode;       // 0xBA (186)
        public byte SubCode;          // 0x02
        public ushort RoomId;         // Target room ID
        
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 9)]
        public string EnteredPin;     // PIN attempt if room is protected
    }

    /// <summary>
    /// Client-to-server request to leave or forfeit a cooperation room.
    /// Wire Header: Action Code 0xBA, Subcode 0x08.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct C2S_CooperationLeavePacket
    {
        public byte ActionCode;       // 0xBA (186)
        public byte SubCode;          // 0x08
        public ushort RoomId;         // Current room ID
    }
}
```

---

## 5. Validation Safeguards & Pre-Match Logic

Before allowing a character to enter or create a cooperation challenge room, the server enforces five sequential validation gates:
1. **Team Requirement Gate:**
   - Evaluates whether the applicant is in a 4-member party or Solo. For competitive TGC chambers, four members are required:
     `"Please apply in team of 4 people"`
2. **Floor Level Gate:**
   - All applicants must have unlocked the required instance floor:
     `"Can't team, player haven't reached this floor yet"`
3. **Point Stake Gate:**
   - Ensures each party member holds sufficient arena tokens or points:
     `"Player %s lacks Points"` or `"Lack Pts, can't team up"`
4. **State Mutex Gate:**
   - Blocks players currently in trade, vending stall, pet training, or mini-games:
     `"Player busy"` / `"Can't do in battle!"`
5. **Capacity Cap Gate:**
   - Prevents joins if current players equal maximum capacity:
     `"Target team is full"`

---

## 6. Private Server Implementation Guide & C# Production Code

The following service provides full lifecycle management for cooperation battle chambers, handling page queries, participant joining, validation, and battle launch.

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Wonderland.Network.Packets.Cooperation;

namespace Wonderland.Server.Services.Cooperation
{
    public sealed class CooperationRoom
    {
        public ushort RoomId { get; init; }
        public ushort StageId { get; init; }
        public uint LeaderCharacterId { get; set; }
        public string LeaderName { get; set; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
        public byte MaxPlayers { get; init; } = 4;
        public uint AntePoints { get; init; }
        public uint RequiredFloor { get; init; }
        public List<uint> MemberCharacterIds { get; } = new();
        public bool InBattle { get; set; }

        public bool IsFull => MemberCharacterIds.Count >= MaxPlayers;
        public bool HasPassword => !string.IsNullOrEmpty(Password);
    }

    public interface ICooperationService
    {
        IReadOnlyList<CooperationRoom> GetRoomPage(int page, int pageSize);
        bool CreateRoom(PlayerCharacter leader, C2S_CooperationCreatePacket packet, out ushort roomId, out string error);
        bool JoinRoom(PlayerCharacter player, C2S_CooperationJoinPacket packet, out string error);
        bool LeaveRoom(PlayerCharacter player, ushort roomId, out string error);
    }

    public sealed class CooperationService : ICooperationService
    {
        private readonly ConcurrentDictionary<ushort, CooperationRoom> _rooms = new();
        private int _nextRoomId = 1;

        public IReadOnlyList<CooperationRoom> GetRoomPage(int page, int pageSize = 4)
        {
            if (page < 1) page = 1;
            return _rooms.Values
                .Where(r => !r.InBattle)
                .OrderBy(r => r.RoomId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }

        public bool CreateRoom(PlayerCharacter leader, C2S_CooperationCreatePacket packet, out ushort roomId, out string error)
        {
            roomId = 0;
            error = string.Empty;

            if (leader == null) throw new ArgumentNullException(nameof(leader));

            if (leader.ArenaPoints < packet.AntePoints)
            {
                error = "Lack Pts, can't team up";
                return false;
            }

            ushort newId = (ushort)System.Threading.Interlocked.Increment(ref _nextRoomId);
            var room = new CooperationRoom
            {
                RoomId = newId,
                StageId = packet.StageId,
                LeaderCharacterId = leader.CharacterId,
                LeaderName = leader.Name,
                Title = packet.RoomTitle,
                Password = packet.RoomPassword,
                MaxPlayers = Math.Min((byte)4, packet.MaxPlayers),
                AntePoints = packet.AntePoints
            };

            room.MemberCharacterIds.Add(leader.CharacterId);
            _rooms[newId] = room;
            roomId = newId;

            return true;
        }

        public bool JoinRoom(PlayerCharacter player, C2S_CooperationJoinPacket packet, out string error)
        {
            error = string.Empty;

            if (player == null) throw new ArgumentNullException(nameof(player));

            if (!_rooms.TryGetValue(packet.RoomId, out var room))
            {
                error = "Room does not exist";
                return false;
            }

            if (room.InBattle)
            {
                error = "Target is in battle";
                return false;
            }

            if (room.IsFull)
            {
                error = "Target team is full";
                return false;
            }

            if (room.HasPassword && room.Password != packet.EnteredPin)
            {
                error = "Code doesn't match, try again";
                return false;
            }

            if (player.ArenaPoints < room.AntePoints)
            {
                error = "Lack Pts, can't team up";
                return false;
            }

            lock (room)
            {
                if (room.IsFull)
                {
                    error = "Target team is full";
                    return false;
                }

                room.MemberCharacterIds.Add(player.CharacterId);
            }

            return true;
        }

        public bool LeaveRoom(PlayerCharacter player, ushort roomId, out string error)
        {
            error = string.Empty;

            if (player == null) throw new ArgumentNullException(nameof(player));

            if (!_rooms.TryGetValue(roomId, out var room))
            {
                error = "Room does not exist";
                return false;
            }

            lock (room)
            {
                room.MemberCharacterIds.Remove(player.CharacterId);
                
                // If room host leaves, migrate host or disband room
                if (room.LeaderCharacterId == player.CharacterId)
                {
                    if (room.MemberCharacterIds.Count > 0)
                    {
                        room.LeaderCharacterId = room.MemberCharacterIds[0];
                    }
                    else
                    {
                        _rooms.TryRemove(roomId, out _);
                    }
                }
            }

            return true;
        }
    }
}
```
