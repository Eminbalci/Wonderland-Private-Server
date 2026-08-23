# Portal Teleport Cooldown & Debounce Protection (1 Second)
# Portal Teleport Cooldown & Debounce Protection (2.5 Seconds)

## 1. Problem Statement
When a player stepped onto a portal or spawned directly on a destination portal trigger upon map transition, the server would immediately process another portal teleport packet. This caused rapid oscillation, double-teleporting, or desynchronization between client and server.

## 2. Technical Implementation
- **Timestamp Tracking (`Player.LastTeleportTime`):** Added `public DateTime LastTeleportTime` property to `Player.cs` initialized to `DateTime.MinValue`.
- **Spawn Proximity Guard (`Player.LastSpawnX`, `Player.LastSpawnY`):** Stamped in `Map.Warp_In` with player's entry position.
- **Warp Entry Update (`Map.Warp_In` / `AC89` / `AC92`):** When any player completes a map warp (`Warp_In`) and confirms scene readiness (`AC89:0` / `AC92:1`), `LastTeleportTime` is refreshed with `DateTime.UtcNow`.
- **Cooldown Window (2500ms):**
  - If `(DateTime.UtcNow - sender.LastTeleportTime).TotalMilliseconds < 2500`, the server ignores the portal request and sends the end-of-event packet (`20, 8`).
  - If `elapsedMs < 4000` and player is within 120 pixels of `(LastSpawnX, LastSpawnY)`, immediate return portal re-triggering is blocked.
- **Packet-Level Guard (`AC20.Recv8`):** Packet handler `AC 20:8` checks both cooldown and spawn proximity before invoking pathfinding or map lookups.
