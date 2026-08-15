# Technical Documentation: NPC Resolution & Dialogue Pipeline

## 1. Architecture Overview
Ported dynamic NPC template resolution and dialogue interaction from the authentic Python server (`handle_20_interaction.py`).

### Key Components:
1. **Dynamic NPC Resolution (`GameDataBase.ResolveNpcInfo`):**
   - **Step 1: Map/Click Overrides:** Checks known map-specific native click overrides (e.g. Map 10017 click IDs 3, 9, 10, 11).
   - **Step 2: Client ID Remapping:** Remaps client-side encoded IDs (`0x908e` -> `0x5209`, etc.).
   - **Step 3: Multi-Candidate Decoding:** Tests `[templateId, decNoOffset, decWithOffset, decNoOffset + 27000, decWithOffset + 27000, decNoOffset + 10000, decWithOffset + 10000, templateId * 2, templateId + 16000]`.
   - **Step 4: Database Query:** Fetches matching record from `npc_data` table (which contains all English names including `11000: Persian Cat`).

2. **Native Map Loading (`Map.cs`):**
   - On map initialization, all native NPCs are resolved against `ResolveNpcInfo`.
   - Resolves `TID 11000` directly to `Name = "Persian Cat"`.

3. **Dialogue & Interaction (`QuestNpc.cs` & `AC20.cs`):**
   - Responds to clicks with AC 52 Sub 1 (`bbws`) dialogue packets and AC 20 Sub 8 unlock packets.
   - Includes custom dialogues for Persian Cat ("Meow~"), Dogs, Captain, ATM/Bank, Grandma quest, Mary Lou quest, and Niss quest.
