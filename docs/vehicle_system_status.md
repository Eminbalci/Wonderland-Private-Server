# Vehicle & Transportation System Architecture

## 1. Overview
The Vehicle & Transportation System enables players to ride land vehicles (Bicycle, Motorbike, Beetle Car), sail water vehicles (Raft, Canoe, Sailboat, Steamboat, Submarine), and pilot air vehicles (Hot Air Balloon, Airship, UFO), with fuel tracking, dismounting, and visual synchronization.

---

## 2. ActionCode 15 Protocol ([AC15.cs](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Network/ActionCodes/AC15.cs))

| SubAction | Direction | Payload & Action |
| :--- | :--- | :--- |
| `15:10` | Client $\leftrightarrow$ Server | **Mount Vehicle:** `[15, 10, CharID, VehicleID]`. Broadcasts vehicle model across map. |
| `15:11` | Client $\leftrightarrow$ Server | **Dismount Vehicle:** `[15, 11, CharID]`. Resets vehicle model to standard walking. |
| `15:14` | Server $\rightarrow$ Client | **Fuel Gauge:** `[15, 14, CharID, FuelLeft, MaxFuel]`. Synchronizes vehicle UI fuel bar. |

---

## 3. Vehicle Templates ([Vehicle.cs](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Vehicle.cs))

- **Water Craft:** Raft (`#36001`), Canoe (`#36002`), Sailboat (`#36003`), Steamboat (`#36004`), Submarine (`#36005`).
- **Air Craft:** Hot Air Balloon (`#36006`), Airship (`#36007`), UFO (`#36008`).
- **Land Vehicles:** Bicycle (`#36010`), Motorcycle (`#36011`), Beetle Car (`#36012`).
