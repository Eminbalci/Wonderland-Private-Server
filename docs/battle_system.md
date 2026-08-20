# Battle System - Defeat & Respawn Mechanics

## 1. Player Defeat Protocol
When a player's `CurHP` reaches `0` during battle:
1. **Defeat Notification & UI Teardown:**
   - Sends combat finish signal `AC 11:12`.
   - Transmits battle defeat result code `AC 22:6 [11, 0, 0]` (0 = Defeat).
   - Closes battle grid window `AC 11:0` and despawns all combatants.
2. **Player Revival:**
   - Restores player HP to 50% (`player.Eqs.FullHP / 2`) and synchronizes character stats via `Send8_1()`.
3. **Respawn Destination Priority:**
   - **1st Priority:** `player.RecordMap` (Saved town / inn recording point).
   - **2nd Priority:** `player.ReturnSpawnMap` (Town spawn point).
   - **Fallback:** Starter Beach (Map `10036`, X: `1038`, Y: `2235`).
   - Teleports player smoothly to the destination with `CurMap.Teleport(TeleportType.CmD, player, 0, respawnWarp)`.
