# Companion & Pet Network Synchronization Protocol

## Overview
Documents the complete network synchronization architecture for companion pets, battle pets, mounts, and vehicles following player characters across map instances and player connections in Wonderland Online.

---

## Protocol Specification

### 1. Map Pet Entity Visual Spawn (`AC 15:4`)
- **Direction**: Server -> Owner & All Map Peers (`CurMap.Broadcast`)
- **Format**: `[15, 4, CharID: uint32, PetID: uint32, unk: byte(0), slot: byte(1), PetName: string, weapon: uint16(0)]`
- **Purpose**: Creates and renders the visible 3D/isometric sprite representation of the pet attached to player `CharID` in the client's map scene. Without this packet, peer clients do not instantiate the follower sprite on the map grid.

### 2. Peer Pet Registration & Stats (`AC 15:1`)
- **Direction**: Server -> All Map Peers / New Arrivals
- **Format**: `[15, 1, CharID: uint32, PetID: uint32, Slot: byte, HP: uint32, MaxHP: uint32, SP: uint32, MaxSP: uint32, Level: byte, Amity: byte, ...]` (54 bytes)
- **Purpose**: Instantiates the companion's attribute table inside the client's internal pet list for `CharID`.

### 3. Owner Personal Battle Stance (`AC 19:1`)
- **Direction**: Server -> Owner Client
- **Format**: `[19, 1, PetID: uint32]` (6 bytes)
- **Purpose**: Activates the player's personal active battle pet UI and companion status bars.

### 4. Peer Map Following Broadcast (`AC 19:4`)
- **Direction**: Server -> Other Players on Map
- **Format**: `[19, 4, CharID: uint32, PetID: uint32]` (10 bytes)
- **Purpose**: Attaches the instantiated pet to `CharID`'s position and enables walking/following movement behavior.

### 5. Follow Formation Alignment (`AC 13:5`)
- **Direction**: Server -> Owner & All Map Peers
- **Format**: `[13, 5, CharID: uint32, PetID: uint32]` (10 bytes)
- **Purpose**: Synchronizes follower placement offset relative to the character's facing direction.

### 6. Sprite Appearance Refresh (`AC 5:8`)
- **Direction**: Server -> Owner & All Map Peers
- **Format**: `[5, 8, CharID: uint32, 0: byte]` (7 bytes)
- **Purpose**: Forces client render loop to invalidate and redraw character and attached follower sprites.

### 7. Mount / Riding Pet (`AC 15:16`)
- **Direction**: Server -> Owner & All Map Peers
- **Format**: `[15, 16, Slot: byte(1), CharID: uint32, PetID: uint32, 26 bytes zero padding]` (37 bytes)
- **Purpose**: Renders the player riding on top of the companion mount.

### 8. Dismount / Unriding Pet (`AC 15:17`)
- **Direction**: Server -> Owner & All Map Peers
- **Format**: `[15, 17, CharID: uint32]` (6 bytes)
- **Purpose**: Dismounts the player and restores normal walking mode.

---

## Synchronization Pipeline

1. **Map Entry (`Map.Warp_In`)**:
   - **For existing player `r` receiving new arrival `src`**:
     - `src.ToAC4Packet()` + `src.Worn_Equips`
     - If `src.ActiveVehicleID > 0`: Dispatches `[15, 10, 0, src.CharID, src.ActiveVehicleID]`
     - If `src.ActiveMountID > 0`: Dispatches `[15, 16, 1, src.CharID, src.ActiveMountID, 26B 0]`
     - If `src.ActivePetID > 0`: Dispatches `AC 15:4` (Visual Spawn) + `AC 15:1` (Pet Info) + `AC 19:4` (Following) + `AC 13:5` (Formation) + `AC 5:8` (Sprite Refresh)
   - **For new arrival `src` receiving existing player `r`**:
     - `r.ToAC4Packet()` + `r.Worn_Equips`
     - If `r.ActiveVehicleID > 0`: Dispatches `[15, 10, 0, r.CharID, r.ActiveVehicleID]`
     - If `r.ActiveMountID > 0`: Dispatches `[15, 16, 1, r.CharID, r.ActiveMountID, 26B 0]`
     - If `r.ActivePetID > 0`: Dispatches `AC 15:4` (Visual Spawn) + `AC 15:1` (Pet Info) + `AC 19:4` (Following) + `AC 13:5` (Formation) + `AC 5:8` (Sprite Refresh)
   - **For `src`'s own companions**:
     - Dispatches `AC 15:1` to `src` and broadcasts to `CurMap`
     - If active: Dispatches `AC 19:1` to `src` and broadcasts `AC 15:4`, `AC 19:4`, `AC 13:5`, and `AC 5:8` to `CurMap`

2. **Active Companion Toggle (`AC19.RecvSetBattlePet` / `Player.PutPetToBattle`)**:
   - Sends `AC 19:1` to owner.
   - Calls `player.BroadcastPetAppearance(petId)` to broadcast `AC 15:4`, `AC 15:1`, `AC 19:4`, `AC 13:5`, and `AC 5:8` to map peers.

3. **Rest Companion (`AC19.RecvRestBattlePet` / `Player.UnridePet`)**:
   - Sends `AC 19:5` to owner and broadcasts to map peers.
   - Broadcasts `AC 15:2` (Dismiss) and `AC 5:8` (Sprite Refresh) to map peers.

4. **Mount / Ride Pet (`AC15.Recv11` / `Player.PutPetToRide`)**:
   - Sends `AC 15:16` to owner and broadcasts to map peers.
   - Broadcasts `AC 5:8` (Sprite Refresh) to map peers.

5. **Dismount Pet (`AC15.Recv12` / `Player.UnridePet`)**:
   - Sends `AC 15:17` to owner and broadcasts to map peers.
   - Broadcasts `AC 5:8` (Sprite Refresh) to map peers.

6. **Companion Recruitment (`QuestManager.SendCompanionReward` / Opcode 3)**:
   - Saves companion to player pet list.
   - Sends `AC 15:1` to owner and broadcasts to map peers.
   - If battle mode active: sends `AC 19:1` to owner and broadcasts `AC 15:4`, `AC 19:4`, `AC 13:5`, and `AC 5:8` to map peers.

---

## Code References
- Handled in [`Map.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs) under `Warp_In`.
- Handled in [`Player.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Player.cs) under `BroadcastPetAppearance`, `CreatePetMapPacket`, `PutPetToBattle`, `PutPetToRide`, and `UnridePet`.
- Handled in [`AC19.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC19.cs) under `RecvSetBattlePet` and `RecvRestBattlePet`.
- Handled in [`AC15.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC15.cs) under `Recv11` and `Recv12`.
- Handled in [`QuestManager.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/QuestRelated/QuestManager.cs) under `SendCompanionReward` and `GetNpcName`.
