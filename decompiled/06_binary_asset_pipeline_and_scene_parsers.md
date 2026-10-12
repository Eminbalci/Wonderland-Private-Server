# Binary Asset Pipeline and Scene Parsers Specification

## 1. Master Scene Parser (`FUN_00497c2c` & `FUN_00498208`)

The client's spatial world engine loads map entities, triggers, conditions, and combat encounters through master binary parser `FUN_00497c2c` which dispatches sub-parsers in `FUN_00498208`.

```
                       +-----------------------+
                       |    FUN_00497c2c       |
                       |  (Read SceneHead)     |
                       | Version Check: 0x70   |
                       +-----------------------+
                                   |
         +-------------------------+-------------------------+
         |                                                   |
         v                                                   v
+------------------+                               +--------------------+
|  Map Topologies  |                               |  Entity Descriptors|
|  Read Door       |                               |  Read Npc          |
|  Read Mine       |                               |  Read Npc::ParaNode|
|  Read Goods      |                               |  Read Convey       |
+------------------+                               +--------------------+
         |                                                   |
         +-------------------------+-------------------------+
                                   |
                                   v
                   +-------------------------------+
                   |       Combat & Scripting      |
                   |  Read Eve / Cdn / Result      |
                   |  Read Interact                |
                   |  Read Battle                  |
                   |  Read Battle.MemberAry[Left]  |
                   |  Read Battle.MemberAry[Right] |
                   |  Read Group / LeadGroup       |
                   |  Read LeadEve / Cdn / Result  |
                   +-------------------------------+
```

---

## 2. The 19 Binary Record Readers

`FUN_00498208` manages sequential record parsing with dedicated error logging strings:

| Reader Identifier | Data Source | Record Representation |
|---|---|---|
| `Read SceneHead.` | `SceneData.dat` | Master map header, dimension bounds, and format version (`0x70`) |
| `Read Npc.` | `Eve.emg` | Native static and roaming map NPC definitions |
| `Read Npc:: ParaNode`| `Eve.emg` | Parameter nodes controlling NPC behavior patterns and dialog scripts |
| `Read Door.` | `SceneData.dat` | Warp portals, doorway entrance bounds, and target scene coordinates |
| `Read Mine.` | `Eve.emg` | Renewable resource harvesting nodes (wood, ore, fish, herb) |
| `Read Goods.` | `Eve.emg` | Map merchant item catalog indices and currency prices |
| `Read Eve.` | `Eve.emg` | Interactive event scripts keyed by unique event IDs |
| `Read Eve->Cdn.` | `Eve.emg` | PreEvent condition gates (quest step, item, companion amity) |
| `Read Eve->Cdn->Result.`| `Eve.emg` | Condition outcome opcodes (spawn, conceal, transition, award) |
| `Read Interact.` | `Eve.emg` | Click trigger areas and prop interaction bounding boxes |
| `Read Battle.` | `Eve.emg` | Static scripted battle encounters and boss triggers |
| `Read Battle.MemberAry[Left]`| `Eve.emg` | Friendly combat team layout, party formation coordinates |
| `Read Battle.MemberAry[Right]`| `Eve.emg` | Enemy combat team formation, monster IDs, and placement grid |
| `Read Convey.` | `Eve.emg` | Conveyance vehicles, rafts, boats, and airships |
| `Read Group.` | `Eve.emg` | Random monster spawn groupings and encounter weightings |
| `Read LeadEve.` | `Eve.emg` | Quest leader / companion follower event triggers |
| `Read LeadEve->Cdn.` | `Eve.emg` | Follower condition criteria |
| `Read LeadEve->Cdn->Result.`| `Eve.emg` | Follower condition results |
| `Read LeadGroup.` | `Eve.emg` | Leader mob formations for elite and boss battles |

---

## 3. Item Mall Dynamic Catalog Synchronization (`FUN_0025bd44`)

When the client connects to Item Mall server port 6416 (`0x1910`), catalog updates trigger `FUN_0025bd44`:

* **Log Message:** `"Mall updated"`
* **Subroutines Invoked:**
  * `FUN_0025a684`: Clears local catalog caches.
  * `FUN_00264cbc`: Reconstructs category treeview (`DAT_006dbf3c`).
  * `FUN_00264fa8`: Binds promotional item banners and pricing models.
* **Point Currency Format:** Uses 32-bit unsigned integers for point balances displayed across `Form_Buy` and `Form_ActivityExchange`.

---

## 4. Material Classifications for Alchemy (`FUN_0049f5e8`)

The alchemy engine categorizes item ingredients into 23 material classes:

| ID (Hex) | Class Name | Usage & Synthesis Result |
|---|---|---|
| `0x01` | `Flower` | Floral synthesis, perfumes, basic consumables |
| `0x02` | `Grass` | Herbal medicine, SP restoring concoctions |
| `0x07` | `Veggie` | Cooking items, food buffs |
| `0x08` | `Fruit` | Juices, light HP recovery items |
| `0x0D` | `Seafood` | Seafood dishes, advanced HP/SP recovery |
| `0x1E` | `Diamond` | Master weapon crafting, gem socketing |
| `0x1F` | `Crystal` | Magical armor compounding, resistance equipment |
| `0x20` | `Mercury` | Metallurgy catalyst for rare metallic alloys |
| `0x21` | `Silver` | Medium-tier silver jewelry, weapons, and accessories |
| `0x22` | `Stone` | Structural stonework, primitive tools |
| `0x23` | `Magnet` | Electronics, engine manufacturing |
| `0x2B` | `Red Clay` | Basic pottery, clay stoves, bricks |
| `0x2D` | `Black Clay` | Advanced metallurgy furnaces, heat-resistant crucibles |
| `0x2E` | `White Clay` | Fine porcelain, decorative tent furniture |
| `0x2F` | `Grey Clay` | Standard roofing tiles, structural bricks |
| `0x30` | `Dry Clay` | Basic construction bricks |
| `0x32` | `Feather` | Lightweight clothing, speed boots, wings |
| `0x37` | `Feces` | Agricultural fertilizer for tent gardens |
| `0x38` | `Secrets` | Unique quest ingredients and legendary catalysts |
| `0x39` | `Alcohol` | Buff beverages, chemical solvents |
| `0x3A` | `Nylon` | Synthetic textiles, ropes, sails |
| `0x3B` | `Crude` | Heavy petroleum fuel, crude oil processing |
| `0x3C` | `Ref Oil` | High-efficiency refined fuel, plastic compounding |
