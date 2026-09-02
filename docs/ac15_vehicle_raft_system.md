# AC15 (Vehicle, Mount & Companion System) Technical Specification

## 1. Overview
[AC15.cs](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC15.cs) handles all Companion, Riding/Mount, Raft, and Vehicle operations.

---

## 2. Packet Dispatch Table

| Action / Sub-Command | Direction | Payload & Protocol | Description |
| :--- | :--- | :--- | :--- |
| `AC 15:14` | Client $\rightarrow$ Server | `[15, 14, 0x15, ItemID (uint16)]` | Player uses Raft / Vehicle item from inventory. |
| `AC 15:18` | Server $\rightarrow$ Client | `[15, 18, 0x15, CharID (uint32), ItemID (uint16), CurX (uint16), CurY (uint16)]` | Broadcasts boarding vehicle at exact current coordinates. |
| `AC 15:7` | Client $\rightarrow$ Server | `[15, 7, 0x15, ItemID (uint16)]` | Starts water navigation / sailing mode on map. |
| `AC 15:10` | Server $\rightarrow$ Client | `[15, 10, 0x15, CharID (uint32), ItemID (uint16)]` | Confirms sailing state. |
| `AC 15:10` | Client $\rightarrow$ Server | `[15, 10, 0x15, ItemID (uint16)]` | Raft wreck/break/dismount on ocean arrival or landing. |
| `AC 15:15` | Server $\rightarrow$ Client | `[15, 15, CharID (uint32), ItemID (uint16)]` | Plays raft shattering / destruction animation. |
| `AC 15:11` | Server $\rightarrow$ Client | `[15, 11, 0x15, CharID (uint32)]` | Resets player to standard walking/swimming state. |

---

## 3. Coordinate Encoding Rules
> [!IMPORTANT]
> In all WLO map and vehicle packets, map coordinates `CurX` and `CurY` must strictly be packed as **16-bit unsigned integers (`uint16`)**. Packing as 32-bit (`uint32`) causes byte-alignment offset errors in the client, warping the entity to (X, 0).
