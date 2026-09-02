# NPC Name Resolver & Template Directory GUI Studio

## 1. Overview
The **🧙 NPC Name Resolver** GUI tab in the Wonderland Private Server control panel provides real-time binary template lookup, full search filtering across all 4,928 authentic NPC records decoded directly from `Data/Npc.dat`, and cross-referencing against the world maps database (`eve.Emg`).

## 2. Features
1. **Live NPC Template ID Resolver**:
   - Numeric input for Template ID (`0 - 65535`).
   - Resolves authentic official name, Hex ID, categorical classification (Companions, Humanoids, Monsters, Props), and decoding source.
2. **Interactive Search & Category Filtering**:
   - Instant search by Name, Template ID, or Hex ID.
   - Category filtering: `All Categories`, `Companions (10000-12999)`, `Humanoids & NPCs (13000-15999)`, `Monsters & Animals (16000-18999)`, `Props & Gathering (19000+)`.
   - Double-clicking any row instantly loads and inspects the NPC in the Resolver and World Spawn Inspector.
3. **World Map Spawn Inspector**:
   - Cross-analyzes `eve.Emg` to display all maps, Click IDs, X/Y coordinates, and event triggers associated with the selected NPC template across the entire game world.
4. **Live Binary Reload**:
   - `🔄 Reload Npc.dat` re-executes the XOR cipher parser live without requiring a server reboot.
