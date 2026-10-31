# Event Dialogue Completion Chaining Architecture Specification

## 1. Overview
In Wonderland Online's `eve.Emg` script engine, dialogue completion does not merely terminate player interaction. Instead, dialogue termination frequently serves as a state transition trigger that immediately invokes chained branches, sequential events, combat engagements, cutscenes, or spatial warps.

Across all 1,119 compiled maps in `Data/eve.Emg`:
- **1,684 events** contain branches where dialogue text updates a quest flag (`Opcode 5`) and immediately satisfies conditions for a subsequent branch within the same event.
- **484 NPCs** feature multi-event chains (`npcEntry.Events.Count > 1`), transitioning from intro/acceptance events to progress/reward events.
- **3,775 event actions** execute scene transitions (`Opcode 1, dialog1 = 3`) warping players upon dialogue completion.

## 2. Event Chaining Categories

### A. Seamless Branch Transition (Same Event)
When an NPC branch completes speech and modifies a quest flag via Opcode 5:
- **Authored Behavior:** The dialogue prompt immediately transitions to the subsequent branch (e.g. asking a Choice Question or delivering follow-up instructions) without closing the dialogue window or requiring a manual re-click.
- **Implementation in `EveEventRuntime.cs`:**
  - Evaluated at line 1075–1155 when `index >= branch.SubEntry.Count`.
  - Exemplified by Robinson on Rhode Island Beach (`Map 10035/10039, Event 8`): Branch 1 sets `Quest 12040 = 1`, and immediately transitions into Branch 3 (Beginner's Guide choice prompt).
  - Also active in Holy Village mirrors (`Map 11157`), Well lever (`Map 11159`), and Holy Village cellar (`Map 11015`).

### B. Immediate Combat Engagement (Opcode 4 / 6)
- The dialogue culminates in an encounter challenge.
- Upon acknowledging the final speech frame (`AC 20:6`), the server transitions to combat:
  - Invokes `PvEBattleManager.StartPvEBattle`.
  - Dispatches battle formation (`AC 11:1` / `AC 11:5`).
  - Registers `RunOutcome` token to trigger Victory or Defeat branches upon battle termination.

### C. Cutscene, SFX & NPC Movement Execution
- Actions immediately following dialogue:
  - `Opcode 2 (dialog2 = 4, 8, 10)`: NPC walking path (`AC 22:8`) or emotional expression (`AC 22:7`).
  - `Opcode 8`: Full-screen CG cutscene video (`AC 186:12`) or audio fanfare (`AC 20:10`).
  - `Opcode 15`: Background music change (`AC 20:1 Type 15`).

### D. Map Transition & Portal Warps (Opcode 1 / 7)
- Dialogue ends with Opcode 1 (`dialog1 = 3`) or Opcode 7 (`dialog1 >= 10000`).
- The player is immediately warped to destination map and coordinates.
- Landing on destination map automatically evaluates `TryExecuteStoryArrival` and tile-based `TryExecuteRegion` (`unknownbyte1 = 2`).

### E. NPC Multi-Event List (`npcEntry.Events`)
- A single overworld actor references a list of event IDs in sequence (e.g., Map 12625 NPC 1: `Events = [1, 3]`).
- Event 1 executes the quest trial initiation (`轉職任務`).
- Once trial criteria are met, subsequent interaction targets Event 3 (`拿到結晶`).
