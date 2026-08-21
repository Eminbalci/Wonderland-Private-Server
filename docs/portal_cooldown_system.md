# Portal Teleport Cooldown & Debounce Protection (1 Second)

## 1. Problem Statement
When a player stepped onto a portal or spawned directly on a destination portal trigger upon map transition, the server would immediately process another portal teleport packet. This caused rapid oscillation, double-teleporting, or desynchronization between client and server.

## 2. Technical Implementation
- **Timestamp Tracking (`Player.LastTeleportTime`):** Added `public DateTime LastTeleportTime` property to `Player.cs` initialized to `DateTime.MinValue`.
- **Warp Entry Update (`Map.Warp_In`):** When any player completes a map warp (`Warp_In`), `LastTeleportTime` is stamped with `DateTime.UtcNow`.
- **Portal Debounce Guard (`Map.Teleport`):** In `Map.Teleport(TeleportType.Regular, ...)`:
  - If `(DateTime.UtcNow - sender.LastTeleportTime).TotalMilliseconds < 1000`, the server ignores the portal request and sends the end-of-event packet (`20, 8`).
- **Packet-Level Guard (`AC20.Recv8`):** Packet handler `AC 20:8` checks `(DateTime.UtcNow - p.LastTeleportTime).TotalMilliseconds < 1000` to drop rapid consecutive portal packets before invoking pathfinding or map lookups.
