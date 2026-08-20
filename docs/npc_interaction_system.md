# NPC Interaction & Classification System Technical Specification

## 1. Overview
Previously, NPC click handling in [`QuestNpc.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/QuestNpc.cs) applied an overly broad template range (`11000 <= TemplateID <= 15000`) that mistook peaceful town NPCs (shops, healers, villagers, guards, pets) for wild monsters, erroneously triggering PvE battles.

## 2. Classification Architecture
NPCs are now strictly categorized into distinct functional handlers:

1. **Shopkeepers (`TemplateID 13000-13999` / `shop`, `sho`, `vendor`, `merchant`, `trader`, `blacksmith`)**:
   - Opens shop greetings and unlocks player movement (`AC 20 Sub 8`, `AC 5 Sub 4`).
2. **Witch Doctor & Healers (`TemplateID 14151` / `doctor`, `witch`, `nurse`, `healer`, `clinic`)**:
   - Fully restores player `CurHP` and `CurSP` to `FullHP` and `FullSP`.
   - Sends stat sync packet (`Send8_1`) and prompt.
3. **Storage & Vault Keepers (`TemplateID 14134, 14181, 14157` / `keep`, `storage`, `bank`, `exchanger`, `stock`)**:
   - Opens storage/exchange dialogue and unlocks movement.
4. **Friendly Town NPCs & Companions (Roca, Lina, Mary Lou, Jack, Emilie, Villagers, Guards, Shiba Inu, Cats, Statues)**:
   - Displays character-specific dialogues and narrative cues.
5. **Wild Monsters & Aggressive Creatures (`TemplateID 17000-17999`, slimes, wolves, spiders, pigs, bees)**:
   - Triggers authentic turn-based PvE battle.

## 3. Communication Protocol
- All NPC messages, health restores, and storage prompts are delivered using authentic `AC 23 Sub 57` (`[23, 57, 0, string]`) system prompt packets.
- Fixed the previous bug where sending `AC 2 Sub 2` with character ID `0` caused the client to display uninitialized garbage prefixes (`(Local):Y??`, `(Local):n?`, `(Local):M??`). Messages are now clean and accurately rendered in the client chat log.
