# Guild System Architecture & Implementation

## 1. Overview
The Guild System allows players to establish, join, manage, and coordinate guilds with authentic WLO packet protocols and data persistence.

---

## 2. Protocol Actions ([AC39.cs](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Network/ActionCodes/AC39.cs))

| SubAction | Direction | Payload & Action |
| :--- | :--- | :--- |
| `39:1` | Client $\rightarrow$ Server | **Create Guild:** Unpacks `GuildName` & `Icon`. Deducts 20,000 gold. Registers guild. |
| `39:2` | Client $\rightarrow$ Server | **Invite Player:** Leader/Vice sends invite to `TargetCharID`. Server sends prompt `[39, 2, InviterID, GuildID, GuildName]` to target. |
| `39:3` | Client $\rightarrow$ Server | **Respond to Invite:** Target sends `1` (Accept) or `0` (Decline) with `GuildID`. |
| `39:4` | Client $\rightarrow$ Server | **Leave Guild:** Member leaves guild or Leader triggers disbanding. |
| `39:5` | Client $\rightarrow$ Server | **Kick Member:** Leader/Vice removes `TargetCharID`. |
| `39:6` | Client $\rightarrow$ Server | **Disband Guild:** Leader disbands guild, clears members and deletes from database. |
| `39:7` | Client $\rightarrow$ Server | **Update Notice/Rules:** Leader updates guild notice broadcasted to members. |
| `39:8` | Client $\rightarrow$ Server | **Request Info:** Synchronizes full guild member list, ranks, levels, elements, and online status. |
| `39:30` | Server $\rightarrow$ Client | **Insignia Badge:** Broadcasts `[39, 30, CharID, IconID, GuildName]` on map and login. |

---

## 3. Data Architecture ([Guild.cs](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Guild.cs))
- **`GuildManager`**: Manages all active guilds in memory (`Dictionary<ushort, Guild>`).
- **`Guild`**: Contains member lists, leader reference, icon ID, rules, and broadcast utilities (`BroadCastGuild`).
- **`Data/guilds.txt`**: Persists guild registrations, leadership, emblems, and notices across server restarts.
