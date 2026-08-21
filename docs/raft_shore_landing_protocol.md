# Raft Shore Landing & Vehicle Wrecking Protocol

## Overview
When players sail on the World Ocean (`Map 11016`) using the wooden Raft (or other watercraft) and arrive at the shoreline of Kelan Island / South Beach (coordinates `X >= 280, Y >= 950`), the server automatically handles the disembarkation and raft destruction sequence.

---

## Protocol & Action Code Dispatches

1. **Movement Interception (`AC 6:1`)**:
   - As the player moves across the water towards the sandy beach (`X >= 280, Y >= 950` on `Map 11016`), the movement handler detects shore arrival while `ActiveVehicleID > 0`.

2. **Vehicle Destruction Packet (`AC 15:15`)**:
   - Dispatches `[15, 15, CharID (4B), VehicleID (2B)]` to the player and broadcasts to nearby players.
   - Plays the authentic wooden raft breaking animation and sound effect.

3. **Vehicle Unmount / Reset Packet (`AC 15:11`)**:
   - Dispatches `[15, 11, CharID (4B)]` to the player and broadcasts to nearby players.
   - Clears the sailing state on both client and server (`player.ActiveVehicleID = 0`).

4. **Inventory Cleanup & Movement Release**:
   - Removes 1 unit of the wrecked Raft from the player's inventory.
   - Dispatches `AC 5:4` (movement refresh) to allow the player to freely walk across the island's sandy terrain with their companions.

---

## Source Files
- [`wlo.pserver.core/Game/PlayerRelated/Vehicle.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Vehicle.cs)
- [`Src/Network/ActionCodes/AC06.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC06.cs)
- [`Src/Network/ActionCodes/AC20.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC20.cs)
