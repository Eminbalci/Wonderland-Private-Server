# Friend System & Social Management

## 1. Overview
The Friend System enables players to add, manage, view online status, and remove friends with full persistent database synchronization across sessions.

---

## 2. Packet Architecture

| Action Code | Sub-Action | Direction | Description |
| :--- | :--- | :--- | :--- |
| `AC 10` | `1` | Client $\rightarrow$ Server | Add Friend Request (Target CharID) |
| `AC 10` | `3` | Client $\rightarrow$ Server | Request Friend List |
| `AC 10` | `4` | Client $\rightarrow$ Server | Delete Friend (Target CharID) |
| `AC 14` | `4` | Server $\rightarrow$ Client | Remove Friend from UI |
| `AC 14` | `5` | Server $\rightarrow$ Client | Synchronize Full Friend List with Visuals & Statuses |
| `AC 14` | `7` | Server $\rightarrow$ Client | Friend Online Notification & Name Broadcast |
| `AC 14` | `9` | Server $\rightarrow$ Client | Friend Request / Add Success Notification |

---

## 3. Database Persistence ([CharacterDataBase.cs](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/CharacterDataBase.cs) & [GameDataBase.cs](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/GameDataBase.cs))
- Friend IDs are serialized into `charactersExtData.Friends` using standard delimiter format (`CharID1&CharID2&...`).
- When a player logs in, `GameDataBase.VerifyCharacter` reads `charactersExtData.Friends` and initializes `player.MyFriends.LoadFriends(...)`.
- `CharacterDataBase.WritePlayer` updates the database column `Friends` automatically on character save.
