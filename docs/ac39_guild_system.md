# AC39 (Guild & Quest Management) Technical Specification

## 1. Overview
[AC39.cs](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC39.cs) handles binary packets for Guild operations, member permissions, insignia updates, and Quest Journal synchronization.

---

## 2. Packet Dispatch Table

| Sub-Command | Direction | Handler | Action Description |
| :--- | :--- | :--- | :--- |
| `AC 39:1` | S $\rightarrow$ C | `QuestManager.SendQuestJournal(p)` | Sends player's quest journal list. |
| `AC 39:2` | C $\rightarrow$ S | `Recv2(p, r)` | Sends Guild invitation request to target player in map. |
| `AC 39:3` | C $\rightarrow$ S | `Recv3(p, r)` | Accepts Guild invitation, adds player to `Guild`. |
| `AC 39:6` | C $\rightarrow$ S | `Recv6(p)` | Player leaves current Guild. |
| `AC 39:7` | C $\rightarrow$ S | `Recv7(p, r)` | Guild Leader dismisses a guild member. |
| `AC 39:8` | C $\rightarrow$ S | `Recv8(p, r)` | Broadcasts guild chat / tab message to online members. |
| `AC 39:9` | C $\rightarrow$ S | `Recv9(p, r)` | Guild Leader updates the guild rules/notice. |
| `AC 39:11` | C $\rightarrow$ S | `Recv11(p, r)` | Removes Vice-Leader role from a member. |
| `AC 39:12` | S $\rightarrow$ C | Direct Packet `[39, 12, 0]` | Clear/empty guild member list acknowledge. |
| `AC 39:14` | C $\rightarrow$ S | `Recv14(p, r)` | Assigns Vice-Leader role to a member. |
| `AC 39:16` | C $\rightarrow$ S | `Recv16(p, r)` | Updates guild member permissions / rank. |
| `AC 39:18` | C $\rightarrow$ S | `Recv18(p, r)` | Changes the guild insignia icon badge. |

---

## 3. Type Safety & Implementation
- All guild operations are strongly-typed via [`Game.PlayerRelated.Guild`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Guild.cs).
- Synchronized across online guild members using thread-safe broadcast methods (`BroadCastGuild` and `BroadcastMemberList`).
