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

---

## 7. Native Map Ground Items Lifecycle

Wonderland Online maps feature collectible resources (e.g. Clay, Sea Water, Iron Ore, Herbs) lying directly on the terrain. These are defined within binary `Eve.emg` event scripts under the `ItemArea` data category (209 items across 77 maps).

```
        Map Initialization (ReloadSpawns)
                       │
                       v
         Player Joins Map -> AC 23:4
           (Array of active ground items)
                       │
                       v
            Player Interaction (AC 23:2)
                       │
         ┌─────────────┴─────────────┐
         v                           v
  AddItem to Inv            Broadcast Removal
    (AC 23:2)                   (AC 23:1)
         │
         v
  Schedule Respawn
  (RespawnTime = UtcNow + RespawnSeconds)
         │
         v
    Map Heartbeat (Map.Process())
  Check: UtcNow >= RespawnTime
         │
         v
   Broadcast Respawn (AC 23:3)
```

### Data Structures & State Model

Each collectible item on a map is managed via [`MapGroundItem`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs):
- `Slot (byte)`: 1-based unique identifier within the map.
- `ClickID (ushort)`: Client click targeting handle.
- `ItemID (ushort)`: Item ID defined in `Item.dat`.
- `Name (string)`: Display name resolved from `Item.dat`.
- `X (ushort)`, `Y (ushort)`: Terrain spawn coordinates.
- `RespawnSeconds (uint)`: Authentic respawn interval loaded from `Eve.emg` (defaults to 180 seconds if 0).
- `IsPickedUp (bool)`: Runtime flag indicating availability.
- `RespawnTime (DateTime)`: Absolute UTC timestamp when the item reappears.

### Network Protocol Specification

| Packet | Direction | Opcode / Sub | Wire Layout | Description |
| :--- | :--- | :--- | :--- | :--- |
| Initial Items Sync | S -> C | `AC 23:4` | `[23, 4, count:byte, ...items]`<br>Item: `slot:b, item_id:w, x:w, y:w, respawn:d, 0:b` | Sent during [`Map.SendMapInfo`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs) upon map entry. |
| Pickup Request | C -> S | `AC 23:2` | `[23, 2, slot:byte]` | Triggered when player walks up to and clicks a ground item. |
| Pickup Response | S -> C | `AC 23:2` | `[23, 2, slot:byte, 0:byte]` | Acknowledges inventory grant to picking player. |
| Removal Broadcast | S -> C | `AC 23:1` | `[23, 1, slot:byte]` | Broadcast to all map players to despawn the visual sprite. |
| Respawn Broadcast | S -> C | `AC 23:3` | `[23, 3, slot:b, item_id:w, x:w, y:w, respawn:d, 0:b]` | Broadcast by [`Map.Process`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs) when respawn timer expires. |

