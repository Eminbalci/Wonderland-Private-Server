# Walk-On Region Auto-Dialogue & Running Subcode Architecture

## 1. Overview
Wonderland Online features automatic dialogue mechanisms that activate without explicit player mouse clicks. These systems trigger when moving across specific coordinates, entering maps, or reaching proximity thresholds.

## 2. Automatic Dialogue Systems

| System | Trigger Mechanism | Emulator Implementation |
| :--- | :--- | :--- |
| **Walk-On Region Dialogue** | Stepping onto map coordinates defined in `MapData.Entry_Points` with `unknownbyte3 = 1` | [`EveEventRuntime.cs:TryExecuteRegion`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventRuntime.cs#L200), [`EveEventRuntime.cs:ExecuteRegionRequest`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventRuntime.cs#L164) |
| **Story Arrival Dialogue** | Entering a map via warp or initial login | [`EveEventRuntime.cs:TryExecuteStoryArrival`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventRuntime.cs#L227) |
| **Client Proximity Auto-Click** | Entering interaction radius of quest/aggro actors | Handled natively by official client `Main.exe` (sends interaction packet upon arrival) |
| **Ambient Overhead Speech** | NPC roaming speech bubbles | Packet AC 22, Sub 2 / Sub 5 (`Broadcast`) |

## 3. Running Movement (`AC 6:2`) Region Trigger Synchronization

### Problem
Previously, [`Src/Network/ActionCodes/AC06.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC06.cs) commented out `case 2` (running packet), leaving `Recv2` as an empty stub. While walking (`AC 6:1`) correctly evaluated `TryExecuteRegion`, running across trigger zones skipped `TryExecuteRegion` entirely.

### Resolution
Enabled `case 2: Recv2(r, p); break;` in `AC06.ProcessPkt` and implemented `Recv2` delegating to `Recv1(p, r)`. Running movement now accurately synchronizes player map coordinates and evaluates walk-on region auto-dialogue triggers without requiring manual NPC clicks.
