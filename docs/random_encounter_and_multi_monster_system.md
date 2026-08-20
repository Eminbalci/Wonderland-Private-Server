# Random Encounter & Multi-Monster Battle System Specification

## 1. Random Encounter Mechanics
- **Step Accumulator:** In [`AC06.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC06.cs), walking packets (`AC 6 Sub 1`) increment `p.StepsSinceLastBattle`.
- **Encounter Trigger:** Upon reaching `p.NextBattleSteps` (randomized between 18 and 35 steps), `PvEBattleManager.CheckAndTriggerRandomEncounter(player)` checks if the player is in an active battle or safe map (Tent, Robinson starter tutorial beach, main cities).
- **Wild Monster Selection:** Gathers candidate wandering monsters from the current map's `NpcList` (or map-specific fallback tables) and randomly spawns 1 to 4 monsters in authentic grid formation.

## 2. Multi-Monster Battle Formations
- **Formation Grid Slots:**
  - Front row: `(2, 2)` Center, `(2, 3)` Right, `(2, 1)` Left, `(2, 4)` Far Right
  - Back row: `(1, 2)` Center, `(1, 3)` Right, `(1, 1)` Left, `(1, 4)` Far Right
- **Battle Initialization:** Spawns all enemy entities via sequential `AC 11 Sub 5` packets and synchronizes HP/SP gauges for every grid slot (`AC 51 Sub 1`).

## 3. Turn Progression & Rewards
- **Target Selection:** Player attacks and skills are dispatched to the specific target monster grid coordinates `(dstX, dstY)`.
- **Individual Despawn:** Defeated monsters are despawned from the grid (`AC 11 Sub 1`).
- **Sequential Counter-Attacks:** During the enemy turn phase, each surviving monster performs its attack animation and inflicts damage in sequence before granting the player their next round turn (`AC 52 Sub 1`).
- **Cumulative Victory Rewards:** EXP, Gold, and item drop rolls are aggregated across all defeated monsters in the encounter.
