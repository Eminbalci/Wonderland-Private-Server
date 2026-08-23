# Companion Pet Dismissal & Release System (AC 15:2)

## 1. Overview
This document specifies the authentic Wonderland Online packet protocol and server behavior for dismissing (releasing) a companion or pet from the player's pet list.

---

## 2. Packet Flow Specifications

### 2.1 Client Request (`AC 15, Sub 2`)
When the player clicks the **Dismiss** (Serbest Bırak) button in the Pet Status Window:
```
C -> S: [Header: 4B][AC: 0x0F][Sub: 0x02][Slot: 1B]
Example: F4 44 03 00 0F 02 01 (Dismiss pet at Slot 1)
```

| Byte Offset | Type | Description |
|---|---|---|
| 0 | Byte (15) | ActionCode 15 (Companion/Vehicle actions) |
| 1 | Byte (2) | Subcode 2 (Dismiss / Release Pet) |
| 2 | Byte | Pet Slot Index (1..4) |

---

### 2.2 Server Handling & Cleanup
1. **Active/Battle State Check**:
   - If the pet in the given slot is currently in Battle Mode or following on the map (`player.ActivePetID == pet.PetID` or `pet.IsBattle`):
     - Clears `player.ActivePetID = 0`.
     - Sends `AC 19:5` (Battle Mode Rest) to owner and peers.
     - Sends `AC 5:8` (Despawn Follower NPC) to owner and peers.
2. **Collection & DB Removal**:
   - Removes pet entry from `player.PlayerPets[slot]`.
   - Executes `DELETE FROM character_pets WHERE charID = ... AND slot = ...`.
   - Persists state via `player.SaveCharacterData()`.
3. **Client Notification (`AC 15, Sub 2`)**:
   - Dispatches confirmation packet to the client:
   ```
   S -> C: [15, 2, CharID (4B), Slot (1B)]
   ```
   - Broadcasts the dismissal confirmation packet to map peers.
