# Turn-Based Combat Engine and Battlefield Specification

## 1. Architectural Overview

Wonderland Online features an authentic turn-based 4v4 combat system on a 2D isometric grid. The combat engine manages fighter positioning, elemental wheel affinities, 30-second round countdowns, dual combos, real-time HP/SP commitments, and drop generation. The implementation spans [`PvEBattleManager.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Battle/PvEBattleManager.cs), [`MonsterDropManager.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Battle/MonsterDropManager.cs), and Action Codes `AC 11`, `AC 50`, `AC 51`, `AC 52`, and `AC 53`.

---

## 2. Battlefield Grid & Entity Serialization

Combat occurs across friendly (side `0x05`) and enemy (side `0x01`) grid positions:

```
        [Enemy Grid: Side 0x01]
       (0,3)   (1,3)   (2,3)   (3,3)
       (0,2)   (1,2)   (2,2)   (3,2)
       (0,1)   (1,1)   (2,1)   (3,1)
       (0,0)   (1,0)   (2,0)   (3,0)
  ---------------------------------------
       (0,0)   (1,0)   (2,0)   (3,0)
       (0,1)   (1,1)   (2,1)   (3,1)
       (0,2)   (1,2)   (2,2)   (3,2)
       (0,3)   (1,3)   (2,3)   (3,3)
       [Friendly Grid: Side 0x05]
```

### 2.1 Fighter Initialization (`AC 11:250` & `AC 11:5`)

Each combatant is serialized as a 34-byte record:

```
+---------------+---------------+-----------------------------------------------+
| Byte Offset   | Field Type    | Description                                   |
+---------------+---------------+-----------------------------------------------+
| 0x00..0x01    | UInt16 (LE)   | Action SubCode: 11, 5                         |
| 0x02          | Byte          | Team Side (0x05 = Friendly, 0x01 = Enemy)     |
| 0x03          | Byte          | Grid Row Index (0..3)                         |
| 0x04          | Byte          | Grid Column Index (0..3)                      |
| 0x05..0x08    | UInt32 (LE)   | Entity ID (Character ID or Monster ID)        |
| 0x09..0x1A    | Bytes[18]     | Sprite Template, Weapon Visual, Palette Bytes |
| 0x1B          | Byte          | Current Fighter Level (1..200)                |
| 0x1C          | Byte          | Element (0=Earth, 1=Water, 2=Fire, 3=Wind)    |
| 0x1D..0x20    | UInt32 (LE)   | Current HP                                    |
| 0x21..0x22    | UInt16 (LE)   | Current SP                                    |
+---------------+---------------+-----------------------------------------------+
```

> [!CAUTION]
> **Level and Element Byte Ordering:** Offset 28 represents `Level` and Offset 29 represents `Element`. Inverting these two bytes causes the client UI to interpret the Element byte as Level, resulting in the infamous **"Lv. 0 Monster"** display defect.

---

## 3. Turn Loop & Round State Machine

```
[Server Emits AC 52:1] ---> Enables player action inputs & starts 30s timer
       |
       v
[Player Submits Action] ---> Client sends AC 50:1 (Skill/Attack/Item/Catch)
       |
       v
[Server Emits AC 53:5] ---> Acknowledges player move & advances focus to Pet
       |
       v
[Pet Action Submitted] ---> Player submits pet turn action
       |
       v
[Turn Execution Pipeline]:
       Phase 0:   Poison Status Damage Tick
       Phase 0.5: Flee Evaluation
       Phase 1:   Defend Actions (50% physical damage reduction)
       Phase 2:   Heal / Buff / Revive Skills
       Phase 3:   Monster Catch Attempts
       Phase 4:   Player/Pet Attacks & Dual Combos (1.25x Damage Boost)
       Phase 5:   Monster AI Retaliation
       Phase 6:   Knockout Resolution & Win/Loss Condition Checks
       |
       +---> [If Knockout Occurs]: Server Emits AC 53:3 [GridX, GridY]
       |     (Dead sprites collapse on grid; AC 11:1 despawns are withheld)
       |
       v
[Real-Time Stat Commit] ---> Server Emits AC 51:1 (Syncs HP/SP)
       |
       v
[Loop Next Round] ---> Re-prompts AC 52:1 (If battle ongoing)
```

---

## 4. Combat Damage Formulas & Elemental Wheel

### 4.1 Elemental Advantage Multipliers

```
+---------------+---------------+-----------------------+
| Attacking Elm | Defending Elm | Damage Multiplier     |
+---------------+---------------+-----------------------+
| Earth         | Water         | 1.7x (Advantage)      |
| Earth         | Wind          | 0.6x (Disadvantage)   |
| Water         | Fire          | 1.7x (Advantage)      |
| Water         | Earth         | 0.6x (Disadvantage)   |
| Fire          | Wind          | 1.5x (Advantage)      |
| Fire          | Water         | 0.6x (Disadvantage)   |
| Wind          | Earth         | 1.7x (Advantage)      |
| Wind          | Fire          | 0.6x (Disadvantage)   |
| Any           | Same / Neutral| 1.0x (Neutral)        |
+---------------+---------------+-----------------------+
```

### 4.2 Base Physical & Magical Damage
$$\text{BaseDamage} = (\text{ATK} \times 2.0 - \text{DEF} \times 1.2) \times \text{SkillFactor} \times \text{ElementMult}$$
Dual combos trigger when multiple friendly fighters target the same grid slot with matching or compatible agility scores, applying a flat **$1.25\times$ combo damage multiplier**.

---

## 5. Monster Drops & Inventory Protection

Managed by [`MonsterDropManager.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Battle/MonsterDropManager.cs):
* **Drop Resolution:** Evaluates against Template ID specific drops from `Npc.dat`, falling back to level bracket tables.
* **Fair-Play Drop Cap:** Drops are strictly capped at a maximum of **1 item drop per monster kill**.
* **Inventory Full Alert:** If the player bag is full, drops are rejected and a notification is sent to prevent item loss.

---

## 6. Battle Exit Sequence (7-Step Handshake)

To prevent visual desynchronization and client state locks, combat exit follows a strict 7-step teardown:

1. **Step 1:** Server emits `AC 11:12` victory fanfare.
2. **Step 2:** Server emits 4-byte `AC 11:1` pet battle despawn frame.
3. **Step 3:** Server emits 6-byte `AC 11:0` combat window close frame.
4. **Step 4:** Server emits 5-byte `AC 11:1` player battle despawn frame.
5. **Step 5:** Server emits `AC 11:4` to clear the overworld crossed-swords combat presence indicator.
6. **Step 6:** Server sets a 5-second combat immunity cooldown on the player.
7. **Step 7:** Server awards EXP, gold, and rolls monster drops into the player's bag.
