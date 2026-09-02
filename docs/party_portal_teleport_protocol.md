# Party Portal Teleport & Follower Formation Protocol

## 1. Overview
When players in a party transition across map portals together, follower party members must automatically attach to the party leader and synchronize walking pathing without causing duplicate chat spam to the leader.

---

## 2. Synchronization Mechanism (`AC 13:5`)
- **Follower Attachment**:
  - In `Map.Warp_In`, when any team member loads a new map:
    1. If `src == leader`, the server sends `AC 13:5` (`[13, 5, leader.CharID, member.CharID]`) directly to each member on the new map and broadcasts it to map peers (`"Ex", leader.CharID`).
    2. If `src != leader`, the server sends `AC 13:5` directly to `src` and broadcasts it to map peers (`"Ex", leader.CharID`).
- **Elimination of Chat Spam**:
  - The leader never receives `AC 13:5` upon portal warp (since `AC 13:5` triggers the client's `Player [name] joined team` chat prompt on the leader's client).
  - Followers receive `AC 13:5` to rebind their pathing coordinates to the leader.
  - Map peers receive `AC 13:5` to render the formation.

---

## 3. Verification
- Solution compiles cleanly via `dotnet build "Wonderland Private Server.sln"` (0 Errors).
