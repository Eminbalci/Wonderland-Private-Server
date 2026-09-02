# Monster Loot Drop System & Startup Population

## Overview
The Monster Loot Drop system handles authentic NPC and monster item drops across battles in Wonderland Online.

## Key Features & Initialization Lifecycle

1. **Auto-Load on Server Boot:**
   - On server startup (`MainThreadWork`), `MonsterDropManager.LoadFromNpcDat("Data/Npc.dat")` parses all authentic monster drop slots (up to 5 drops per monster).
   - If user-defined drop configurations exist in `Data/monster_drops.txt`, `LoadFromFile()` integrates them.
   - Triggers `OnLootTablesChanged` to notify GUI components.

2. **Real Item Name Lookup (`ItemNameResolver`):**
   - Decoupled `Func<ushort, string> ItemNameResolver` allows `MonsterDropManager` in `wlo.pserver.core` to resolve accurate item names from `itemDat.wpdat` managed in `Src/cGlobal.ItemDatManager`.

3. **GUI Auto-Population & Reactive Tab Switching:**
   - Background initialization task runs on `Form1_Load` to refresh `dgvMonsterList` and `dgvMallCatalog` once server data finishes loading.
   - `tabControl3.SelectedIndexChanged` automatically refreshes the monster list when the user navigates to the `🐲 Monster Drops` tab.
   - Auto-selects the first monster in the list upon load so the right panel's drop items table is immediately populated with data.
