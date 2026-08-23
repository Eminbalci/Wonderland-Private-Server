# Dynamic Dialogue & Scene Resolution Engine

## Overview
1. **`Talk.dat` Chapter Base Offset Decoding (`PhxTalkDat.cs`)**:
   * Wonderland Online `Eve.emg` bytecode encodes chapter-specific dialogue IDs with region base offsets (e.g. `30000+` for Chapter 1 / South Island / Welling Village, `20000+`, `60000+`).
   * `PhxTalkDat` resolves:
     * Direct index (`1400..1426` for Robinson)
     * Region offsets (`30657 -> Rec 657: 'Xiaomo how are you feeling?'`, `30650 -> Rec 650: 'But what about you? You are also badly hurt.'`)
     * Direct byte offsets and explicit record headers.
2. **Container & Prop Action Code Differentiation (`MainForm1.cs`)**:
   * Opcode 1 (`DialogPtr = 1`) represents Item Rewards, Gold, EXP, and Quest Flag updates rather than NPC speech.
   * Opcode 2 (`DialogPtr = 2`) represents Character Dialogue frames, Choice Prompts, and Prop/Chest opening animations.
3. **Dynamic Scene and NPC Metadata (`SceneDataManager.cs`)**:
   * Decodes `SceneData.dat` for authentic map titles.
   * Decodes `Npc.dat` with XOR cipher `0x520E` for authentic NPC names.
