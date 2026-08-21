# Gathering Nodes & Respawn System Protocol

## Overview
Handles harvestable interactive resources across Wonderland Online maps (e.g., Coconuts, Wooden Crates, Herbs, Ores). Implements the official despawn and respawn synchronization protocol.

## Protocol Structure
1. **Node Interaction & Harvest (`EveEventInterpreter.cs`):**
   - Harvest detection opcode: `DialogPtr == 2 && dialog2 == 2`
   - Node Despawn packet: `S->C AC 22:10 [ClickID (ushort), 0xFF, 0xFF]` broadcast to all players in current map.
   - Resource Grant opcode: `DialogPtr == 1 && op.dialog3 >= 10000 && op.dialog1 == 1`
   - Awards Item (e.g., Coconut Item `#41066`) to `player.Inv`.
   - Sends notification `AC 23:57` and fanfare SFX `AC 20:10`.

2. **Respawn Cycle (`QuestNpc.cs`):**
   - Node marked `IsBroken = true` with `RespawnTime = DateTime.Now.AddSeconds(60)`.
   - Upon timer expiry (`now >= RespawnTime` in `QuestNpc.Update()`):
     - Sets `IsBroken = false`.
     - Broadcasts un-hide packet: `S->C AC 22:10 [ClickID (ushort), 0x00, 0x00]`.
     - Re-enables gathering interaction in `EveEventInterpreter.SelectMatchingBranch()` for all players on the map.
