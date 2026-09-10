# 05 - Gameplay Mechanics and Subsystems

## 1. Turn-Based Battle Engine

Combat in Wonderland Online is turn-based, supporting up to 4 players and 4 active companions against up to 8 enemy combatants.

```
       Round Start (AC 11:2)
                 │
                 v
   Player & Pet Input Phase (20s)
   [Attack / Skill / Guard / Item / Flee / Catch]
                 │
                 v
       Turn Order Calculation
     (Sorted descending by SPD)
                 │
                 v
    Action Resolution & Damage Calculation
  (Elemental Multipliers, Crits, Combos)
                 │
                 v
  Replication to Clients (AC 51:1)
                 │
                 +-----> Check Win / Loss Condition
                         - All Enemies Defeated -> Victory Rewards (EXP, Gold, Drops)
                         - All Players Defeated -> Defeat Warp (Revival Sanctuary)
```

---

## 2. Elemental Affinity & Combat Formulae

### Elemental Advantage Wheel
The 4 elements interact with strict damage bonuses:
- **Water** counters **Fire** (+50% Damage)
- **Fire** counters **Wind** (+50% Damage)
- **Wind** counters **Earth** (+50% Damage)
- **Earth** counters **Water** (+50% Damage)
- Attacking an element you are weak against inflicts a -25% damage penalty.

### Damage Calculations
- **Physical Damage**:
  $$\text{Damage} = \max(1, (\text{ATK} \times 2 - \text{DEF}) \times \text{ElemMultiplier} \times \text{SkillMultiplier})$$
- **Magical Damage**:
  $$\text{Damage} = \max(1, (\text{MATK} \times 2 - \text{MDEF}) \times \text{ElemMultiplier} \times \text{SkillMultiplier})$$
- **Turn Order (Speed Priority)**:
  $$\text{Initiative} = \text{SPD} + \text{Random}(-2, 2)$$

---

## 3. Companion & Pet System

- **Recruitment**: Recruited via quest storylines (e.g., Robinson, Breillat, Clive) or wild capture in battle (`AC 50 Sub 1 Action 6`).
- **Storage**:
  - Active Companions: Stored in SQLite table `character_pets` (`is_hotel = 0`). Maximum 4 active slots per player.
  - Pet Hotel: Stored in SQLite table `character_pets` with `is_hotel = 1` for archival.
- **Intimacy / Amity**:
  - Starts at 60. Increases by +1 per battle victory.
  - Decreases by -5 upon pet death in combat.
  - If amity drops below 20, the companion refuses to fight; if it hits 0, the companion leaves the player.

---

## 4. Housing & Tent Subsystem

- **Activation**: Triggered via `AC 12 Sub 1`. Instantiates a personal private instance map for the player.
- **Placement**: Players can drop crafting tools (Workbenches, Stoves, Furnaces) and furniture.
- **Persistence**: Serialized inside `chartent` (exterior colors and tent skin) and `chartent_items` (interior item positions `x, y, rotation`).

---

## 5. Economy, Mall & Crafting

### Item Mall (`AC 75`)
- Dual-Currency System:
  - **IM Points**: Acquired via store donations or GM grant. Used in Points Mall (`AC 75 Sub 1`).
  - **IM Bonus Points**: Reward points from spending IM. Used in Bonus Mall (`AC 75 Sub 4`).
- All 224 items, prices, discounts, and category IDs are dynamically loaded from SQLite table `item_mall`.

### Starter Pack Welcome Package
- When any newly created character completes intro ship dialogue, [`StarterPackManager.DeliverStarterPack(player)`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/StarterPackManager.cs) auto-injects essential consumables from SQLite `starter_items`:
  - 50x Novice Recovery Potions (ID 23050)
  - 50x Novice Mana Water (ID 23051)
  - 1x Magic Repair Wrench (ID 48050)
  - 10x Rice Balls (ID 30025)
  - 1x Novice Adventurer Badge (ID 57001)

### Alchemy & Synthesis
- Players combine two raw materials using the Alchemy skill to produce higher-tier items.
- Configured in SQLite table `alchemy_recipes` with success probability rates (`rate`).

### Gathering Nodes & Map Chests
- Map gathering nodes (Ore veins, wood trunks, coconut palms) drop authentic materials.
- Gathering cooldowns and timed node respawns are managed asynchronously without blocking the main game loop.
- Map chests loot tables are configured via SQLite `chest_drops`.

---

## 6. Social Subsystems

- **Guilds**: Guild creation (`AC 39`), member promotions (Leader, Vice-Leader, Member), and broadcast billboard notices persisted in `guilds` and `guild_members`.
- **Friend List**: Mutual friend tracking and real-time online/offline presence alerts (`AC 9 Sub 3`) persisted in `Friends`.
- **Postal Mail**: In-game letters with attached gold or inventory items persisted in `mails`.
- **Marriage**: In-game wedding ceremony bonding two characters persisted in `marriages`.
