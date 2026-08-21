# Companion Following Synchronization Protocol

## Overview
Documents the network synchronization architecture for companion pets following player characters in the game world across map instances and player connections.

---

## Protocol Specification

### 1. Peer Pet Registration (`AC 15:1`)
- **Direction**: Server -> All Map Peers / New Arrivals
- **Format**: `[15, 1, CharID: uint32, PetID: uint32, Slot: byte, ...]` (54 bytes)
- **Purpose**: Creates the remote character's pet entity inside the peer client's internal pet table for `CharID`. Without this packet, peer clients have no object representation for the remote player's pet and cannot render it in the world.

### 2. Owner Personal Battle Stance (`AC 19:1`)
- **Direction**: Server -> Owner Client
- **Format**: `[19, 1, PetID: uint32]` (6 bytes)
- **Purpose**: Activates the player's personal active battle pet UI and companion status bars.

### 3. Peer Map Following Broadcast (`AC 19:4`)
- **Direction**: Server -> Other Players on Map
- **Format**: `[19, 4, CharID: uint32, PetID: uint32]` (10 bytes)
- **Purpose**: Attaches the instantiated pet to `CharID`'s position and enables walking/following movement behavior.

---

## Synchronization Pipeline

1. **Map Entry (`Map.Warp_In`)**:
   - For every existing player `r` on the map with `r.ActivePetID > 0`:
     - Dispatches `AC 15:1` (`CreatePetPacket(r, r.ActivePetID)`) to the new arrival `src`.
     - Dispatches `AC 19:4` (`[19, 4, r.CharID, r.ActivePetID]`) to the new arrival `src`.
   - For the new arrival `src` with active companions:
     - Dispatches `AC 15:1` to `src` and broadcasts `AC 15:1` to all existing players `r`.
     - Dispatches `AC 19:1` to `src` and broadcasts `AC 19:4` to all existing players `r`.

2. **Companion Recruitment (`QuestManager.SendCompanionReward`)**:
   - Sends `AC 15:1` + `AC 19:1` to the owner.
   - Broadcasts `AC 15:1` + `AC 19:4` to all map peers (`CurMap.Broadcast`).

3. **Active Companion Toggle (`AC19.RecvSetBattlePet`)**:
   - Sends `AC 19:1` to the owner.
   - Broadcasts `AC 15:1` + `AC 19:4` to all map peers (`CurMap.Broadcast`).

---

## Code References
- Handled in [`Map.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs) under `Warp_In`.
- Handled in [`AC19.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC19.cs) under `RecvSetBattlePet`.
- Handled in [`QuestManager.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/QuestRelated/QuestManager.cs) under `SendCompanionReward`.
