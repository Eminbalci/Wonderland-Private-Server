# Friend List & Online Status Synchronization Protocol

## Overview
Documents the network synchronization architecture for character friendships, friend list queries, and live online/offline presence updates across players.

---

## Packet Specifications

### 1. Friend List Response (`AC 14:5`)
- **Direction**: Server -> Client
- **Format**: `[14, 5, (Entry)*]`
- **Per-Friend Entry Layout**:
  - `CharID: uint32` (4 bytes)
  - `CharName: string` (1 byte length + ASCII bytes)
  - `Level: byte` (1 byte)
  - `Reborn: byte` (1 byte: `0` = normal, `1` = reborn)
  - `Job: byte` (1 byte)
  - `Element: byte` (1 byte: `0`=Earth, `1`=Water, `2`=Fire, `3`=Wind)
  - `Body: byte` (1 byte)
  - `Head: byte` (1 byte)
  - `HairColor: uint16` (2 bytes)
  - `SkinColor: uint16` (2 bytes)
  - `ClothingColor: uint16` (2 bytes)
  - `EyeColor: uint16` (2 bytes)
  - `NickName: string` (1 byte length + ASCII bytes)
  - `OnlineStatus: byte` (1 byte: `1` = Online / Green, `0` = Offline / Grey)

### 2. Friend Online / Offline Real-time Updates
- Whenever a player connects and completes login, the server queries the `Friends` table and dispatches updated friend list packets (`SendFriendList`) to all mutual friends currently online.
- When a player disconnects, the server dispatches updated friend list packets to all online mutual friends, updating their display status to Offline.

---

## Code References
- Handled in [`AC14.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC14.cs) via `SendFriendList`, `PackFriendEntry`, and `NotifyFriendsStatus`.
- Triggered on login in [`AC63.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC63.cs).
- Triggered on disconnect in [`WorldServer.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Server/WorldServer.cs).
