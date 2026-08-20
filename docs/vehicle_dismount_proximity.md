# Vehicle Dismount & Shore Proximity Protocol Reference

## 1. Overview
When players ride marine/land vehicles (e.g. Robinson Raft `#48016`, Jalor `#48005`, Cabriolet `#48011`, Robot `#48014`, Mecha Dragon `#48050`, Submarine `#36007`), attempting to dismount while in water zones triggers client-side terrain checks (`"Can't exit here"`).

## 2. Proximity & Shore Teleportation Implementation
In [`AC15.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC15.cs) and [`AC02.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC02.cs):

1. **Inventory Double-Click / Dismount (`AC 15 Sub 14`)**:
   - Checks if `player.ActiveVehicleID > 0`.
   - Computes distance to nearest walkable shore coordinates:
     - On Map 10036 (Newbie Island): Beach anchor at `(X: 1038, Y: 2235)`.
     - Valid shore proximity radius: $\le 800$ pixels or $X \in [600, 1600], Y \in [1800, 2850]$.
   - **Within Proximity**:
     - Sends `player.RideVehicle("")` dismount packet (`[15, 11, 0, CharID]`).
     - Safely teleports player onto the sand at `(1038, 2235)`.
     - Displays confirmation message: `"🚶 Dismounted from vehicle."`
   - **Outside Proximity (Open Sea)**:
     - Rejects dismount: `"⚠️ Can't exit here: You are too far from shore to dismount."`

2. **Chat Command (`:unride` / `/unride`)**:
   - Immediate dismount with safe beach fallback for Map 10036.
