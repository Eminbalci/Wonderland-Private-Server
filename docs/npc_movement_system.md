# NPC Movement, Waypoints & Pen Wandering Safeguards

## 1. Root Cause Analysis
- **Unconstrained Random Roaming:** Previously, NPCs with Monster Template IDs (17000–19500) that lacked explicit `WalkSteps` were treated as wild wandering monsters. They executed random Cartesian step deltas (`±100px`) with a large `180px` spawn leash, allowing domestic animals (such as pigs in village pens) to walk right through fences and walls.
- **Fast Step Execution:** Scripted `WalkSteps` (from `eve.Emg`) with `delay = 0` were ticking every 1.0 second without natural idle pauses, causing path-walking NPCs to cycle through waypoints too quickly.

## 2. Implemented Fixes
1. **Scripted Waypoint Adherence ([`QuestNpc.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/QuestNpc.cs)):**
   - NPCs with official `WalkSteps` follow their exact predetermined waypoints inside designated boundaries.
   - Natural step pacing (`3.5s – 7.5s` idle intervals) is enforced between path movements.
2. **Village & Town Map Safeguards:**
   - Added `IsVillageOrTownMap()` check (covering Kelan Village, Welling, Holy Village, Kyoto, Chang'an, etc.).
   - Domestic farm animals (`pig`, `cow`, `sheep`, `chicken`, `duck`, `goat`, etc.) are excluded from `IsWildMonster()`.
   - In towns and villages, NPCs without scripted `WalkSteps` stay static at their designated spawn positions and do not randomly wander out of pens.
3. **Tight Roaming Leash for Field Monsters:**
   - On wild field maps, roaming step deltas were reduced from `±100px` to `±40px`, and max spawn leash was clamped to `60px` to avoid glitching through terrain barriers.
