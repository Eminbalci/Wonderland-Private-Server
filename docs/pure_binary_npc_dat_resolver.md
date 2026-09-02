# Pure Binary Npc.dat Decoding Engine

## 1. Overview
The server resolves all NPC names, monster identities, and map interactive entity metadata 100% dynamically and directly from the binary `Data/Npc.dat` file at boot time, eliminating JSON and hardcoded dictionary mappings.

## 2. Npc.dat Binary Layout & Decryption
Each record in `Npc.dat` is a fixed 138-byte struct:
* **Record Size**: 138 bytes (`Pack = 1`).
* **Name Buffer**: Offset `[off + 1 .. off + 10]`, stored right-aligned and reversed.
* **Template ID**: Offset `[off + 12 .. off + 13]` (ushort). Decrypted via XOR `0x5209`:
  $$\text{NpcID} = \text{rawId} \oplus \text{0x5209}$$

## 3. Runtime Resolution
* Loaded during server initialization in `SceneDataManager.Initialize()`.
* Successfully decodes all 4,928 authentic NPC templates across all maps and instances directly into memory (`_npcNames`).
* Seamlessly supports all standard NPC clicks, dialogue speaker resolution, cutscene actors, and battle encounters.
