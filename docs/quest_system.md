# Personal (Per-Player) NPC Visibility & Phasing

## 1. Overview
In Wonderland Online, world NPC despawns, phase transitions, and recruitments are strictly **personal (per-player)**:
- When **Player A** rescues or recruits an NPC, that NPC vanishes (`AC 19:2`) **only for Player A**.
- **Player B** standing next to Player A on the same map who has not yet done the quest continues to see and interact with the NPC normally.
- When Player A warps between maps or logs in, the server synchronizes Player A's personal phase state so the completed NPC remains hidden only for them.

---

## 2. Implementation Pipeline

### 2.1 Direct Dispatch vs. Map Broadcast ([QuestManager.cs](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/QuestRelated/QuestManager.cs))
- During quest completion (`GrantRewards`), `AC 19:2` packets are sent directly via `player.Send(...)` to the completing player.
- Packets are intentionally **never** broadcasted to `map.Broadcast(...)`.

### 2.2 Map Entrance Phase Synchronization ([Map.cs](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs))
When a player loads into a map (`Map.SendMapInfo`):
```csharp
t.Send(new SendPacket(tmp.End()));

// Personal Client-Side NPC Visibility Sync (Only hides completed/recruited NPCs for this specific player)
QuestRelated.QuestManager.SyncPerPlayerNpcVisibility(t, (ushort)this.MapID);
```

`SyncPerPlayerNpcVisibility` checks `player.Quests` and dispatches `AC 19:2` for all completed quests belonging to that player only.
