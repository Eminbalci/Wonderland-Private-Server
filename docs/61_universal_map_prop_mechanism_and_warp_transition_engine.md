# Universal Map Prop Mechanism and Warp Transition Engine Specification

## 1. Executive Summary

In Wonderland Online, various maps feature interactable world props (TID 19000+), mechanisms, levers, secret passage locks, and cellar entrances. These objects follow a multi-stage lifecycle:
1. **Initial / Locked State (`propState == 0`):** An interaction triggers an EVE script (e.g. Map 12612 NPC 3 'Cellar Entrance' -> Event 4). Condition type 3 checks `propState == 0`, executing Opcode 2 (`d2 == 5`, prop break/open animation frame via `AC 22:1`) and setting the prop state to 1.
2. **Unlocked / Opened State (`propState == 1`):** The client visually reflects the open state. Upon interacting again (either via direct NPC click `AC 20:1` or entry/door transit `AC 20:8`), the subsequent event (e.g. Event 5) evaluates condition type 3 (`unknownbyte1 == 3, unknownword2 == 3, unknownword4 == 261` $\to$ `propState == 1`), executing Opcode 1 (`d1 == 3, d2 == transitionType`) to warp the player to the destination scene (e.g. Map 12617 Cellar).

Previously, runtime prop state caching (`player.NativePropStates`) and condition evaluation were gated behind a restrictive 7-map whitelist (`UsesNativeMechanisms`). On 270 other maps with prop mechanisms, the prop state was never recorded in memory, and subsequent door/warp events failed to match, sending `AC 20:8` without teleporting the player. Furthermore, when NPCs only had explicit links to the unlock event in `NpcList`, direct subsequent clicks on the open actor failed to look up external map events and overlapping entry points that check the open prop state.

## 2. Root Cause Analysis

### 2.1 Restriction in `UsesNativeMechanisms` Whitelist
- In [`EveEventInterpreter.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs#L625-L635), Opcode 2 (`d2 == 5`) only saved `player.NativePropStates[propClickId] = propState` if `UsesNativeMechanisms(map.MapID)` was `true` (restricted to maps 11157, 11158, 11159, 11166, 12380, 60014, 12523).
- In [`EveEventRuntime.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventRuntime.cs#L560-L570), evaluating condition type 3 (`condition.unknownword2 == 3`) only checked `player.NativePropStates` if `UsesNativeMechanisms(map.MapID)` returned `true`.
- On Map 12612 and 269 other maps, `propState` remained 0, causing `FindBranch` on Event 5 to evaluate `1 == 0` (false) and return `null`.

### 2.2 Entry Click Margin Discrepancy
- In [`EveEventRuntime.TryExecuteEntry`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventRuntime.cs#L140-L160), active door click requests (`AC 20:8`) enforced `margin = 0` for `unknownbyte3 == 1` entry points, dropping requests when the player was 1-2 tiles (20-40px) adjacent to the bounding box.

### 2.3 NPC Event Candidate Discovery
- In [`EveEventInterpreter.TryExecute`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs#L60-L140), `candidates` only gathered events listed in `npcEntry.Events`. In cases like Map 12612 NPC 3, `npcEntry.Events` only listed Event 4 (the unlock event). Event 5 (the warp event) was attached to Entry Point 2 and checked `propState == 1`. When clicked directly while opened, Event 4 had no eligible branch, and Event 5 was never evaluated.

## 3. Architecture & Engine Solution

### 3.1 Universal Prop State and Actor Visibility Recording
- In [`EveEventInterpreter.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs), Opcode 2 (`d2 == 5`) now records `player.NativePropStates[propClickId] = propState` universally on all maps whenever decoded prop state is non-negative.
- Opcode 2 (`d2 == 2 || d2 == 3`) universally records `player.NativeActorVisibility[targetClickId] = (d2 == 3)`.
- [`PreEventInterpreter.ShouldNpcBeVisible`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/QuestRelated/PreEventInterpreter.cs#L195-L210) queries `player.NativeActorVisibility` universally without map whitelist restrictions.

### 3.2 Universal Prop State Condition Matching
- In [`EveEventRuntime.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventRuntime.cs#L560-L575):
  1. If `condition.unknownword2 == 3`, checks `player.NativePropStates.TryGetValue(condition.unknownword1, out propState)`.
  2. Fallback to authored `PreEventInterpreter.TryGetNativePropState`.
  3. Fallback to durable chest quest completion flag `map.MapID * 1000 + ev.clickID`.
  4. Default state 0 for un-interacted props.
- `UsesNativeMechanisms(uint mapId)` returns `true` for all valid map IDs (`mapId > 0`).
- `TryExecuteEntry` margin is normalized to 2 cells (40px) across all entry points for reliable door/warp interaction.

### 3.3 Secondary Candidate Discovery in `TryExecute`
- When explicit NPC events produce no matching branch (`selectedSub == null`):
  1. Searches `mapData.Entry_Points` that overlap or neighbor the NPC position (within 2 cells) or link events conditioning on `clickId`.
  2. Searches map events where `condition.unknownbyte1 == 3 && condition.unknownword1 == clickId`.
- Evaluates eligible branches with `SelectMatchingBranch` and triggers the matching transition or speech event immediately.

## 4. Verification and Empirical Evidence

- **Map 12612 Cellar Entrance Sequence:**
  - `PropBefore = 0`
  - `Try1 (Click NPC 3)` $\to$ `True`, `PropAfter = 1`, AC 22:1 emitted.
  - `TryNpcAgain (Second Click on NPC 3)` $\to$ `True`, Event 5 executed, Player warped to Map 12617 at `(1655, 268)`.
  - `Reopen (Map 12612 reset)` $\to$ `True`, `TryEntry (AC 20:8 on Entry 2)` $\to$ `True`, Player warped to Map 12617 at `(1655, 268)`.
- **Regression Suite:**
  - 84 / 84 Quest Runtime checks passed (`Test-QuestRuntime.ps1`).
  - 7 / 7 Gameplay Defect checks passed (`Test-GameplayDefectFixes.ps1`).
  - 0 Errors, 0 Warnings on solution build (`Wonderland Private Server.sln`).
