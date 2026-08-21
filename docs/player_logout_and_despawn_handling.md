# Player Disconnect, Logout & World Despawn Protocol

## 1. Overview
When a client logs out or disconnects from the game server, the server must properly:
1. Automatically remove the player from their current party (`LeaveParty()`).
2. Broadcast despawn packets (`AC 12` and companion `AC 13:4`) to map peers so the character and pets vanish immediately from other players' screens.
3. Remove the player instance from the map entity registry (`IMap.RemovePlayer()`).
4. Save the character and inventory state to the database and notify friends of offline status via `Disconnected` event.

---

## 2. Root Cause Analysis
- Previously, when `SocketClient.onConnectionLost` fired, `LoginClient.RemSock()` removed the client socket from the dictionary without invoking cleanup logic on the `Player` instance.
- Consequently, the disconnected character remained stranded in `m_playerlist` on the map, remained in the party team HUD, and left other players seeing a static ghost entity.

---

## 3. Implementation Details
1. **`Player.OnConnectionLost()`**:
   - Executes `LeaveParty()`, removing the player from their party and updating remaining member HUDs.
   - Broadcasts `AC 12` despawn signal to map peers (`CurMap.Broadcast(despawnPkt, "Ex", CharID)`).
   - Broadcasts companion detach/despawn (`AC 13:4`) if an active companion is present.
   - Removes the player from `CurMap` (`CurMap.RemovePlayer(this)`).
   - Invokes `Disconnected?.Invoke(this)`, triggering DB auto-save (`OnCharacterLeave`) and Friend List offline state broadcasting (`AC 14:8`).
2. **`IMap.RemovePlayer()`**:
   - Added `RemovePlayer(Player p)` to `IMap` interface and `GameMap` class to purge disconnected players from memory.
3. **`LoginClient.RemSock()`**:
   - Connected `RemSock` to call `tmp.OnConnectionLost()` immediately on socket drop.

---

## 4. Verification
- Solution builds cleanly via `dotnet build "Wonderland Private Server.sln"` (0 Errors).
