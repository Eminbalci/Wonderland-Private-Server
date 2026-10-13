# Vehicle Transport, Navigation GPS & Traffic Setting Mechanics

## 1. Executive Summary & Vehicle Architecture

Wonderland Online features an advanced multimodal transportation system enabling players to traverse land, sea, air, and deep space. The subsystem is driven by client Pascal source units extracted via REA:
- `CH_Traffic.pas`: Vehicle input controllers and fuel/durability consumption.
- `nNJ_TrafficData.pas`: Configuration parser for `WLRI\data\TrafficSetting.txt`.
- `CH_RidePet.pas`: Companion mount mechanics and coordinate anchoring (`AdjustRidePetPos.txt`).
- `se_GPS.pas`: Global Positioning System minimap radar and portal transit calculator.

---

## 2. Vehicle Configuration Schema (`TrafficSetting.txt`)

The client parses `data\TrafficSetting.txt` into in-memory table structures indexing all 29 primary vehicle types:

```
+-----------------------------------------------------------------------------------+
|                        TrafficSetting.txt CONFIGURATION SECTIONS                  |
+-----------------------------------------------------------------------------------+
| Section Name          | Data Type     | Architectural Meaning                     |
+-----------------------+---------------+-------------------------------------------+
| MaxTrafficTool        | Integer (29)  | Total count of extended vehicle classes   |
| TrafficID             | ItemID List   | Active vehicle entity Item IDs (48044..)  |
| TrafficCapsuleID      | ItemID List   | Inventory vehicle capsule Item IDs        |
| TrafficWayKind        | Byte (1..3)   | Traversal medium (1=Land, 2=Water, 3=Air) |
| TrafficSpeedAdd       | Float (0.04..) | Tile traversal velocity bonus modifier    |
| TrafficCapacity       | Byte (1..4)   | Passenger seating capacity (1, 2, or 4)   |
| TrafficSpecialKind    | Byte (0..2)   | Special abilities (Submersible, Space)    |
| FixTrafficCost        | Integer (1..) | Maintenance and repair material tokens    |
| SitOnTraffic          | Byte (0..2)   | Player riding stance (0=Sit, 1=Stand)     |
| TrafficShowOrder      | Byte (0..1)   | DirectDraw sprite layer rendering order   |
| TrafficShadow         | ResourceID    | Ground shadow projection sprite           |
| TrafficAdjust         | Point (X, Y)  | Visual pixel offset adjustments           |
+-----------------------+---------------+-------------------------------------------+
```

---

## 3. Comprehensive 29-Vehicle Matrix

| Vehicle Item ID | Capsule ID | Name (ZH) | Name (EN) | WayKind (Medium) | Capacity | Stance | Special Traversal Ability |
|---|---|---|---|---|---|---|---|
| `48044` | `48061` | 黃色小鴨 | Yellow Duck | 2 (Water) | 1 Seater | Sit | Coastal water floating |
| `48053` | `48065` | 紙飛機 | Paper Airplane | 3 (Air) | 1 Seater | Sit | Low-altitude obstacle glide |
| `48063` | `48064` | 羅德號 | Rhode Island Ship | 1 (Land/Amph) | 2 Seater | Stand | Shoreline cruise, dual rider |
| `48058` | `48066` | 上弦月 | Crescent Moon | 3 (Air) | 1 Seater | Sit | Sky flight, night glow aura |
| `48059` | `48067` | 上弦之月 | Upper Moon | 3 (Air) | 1 Seater | Sit | Sky flight, high agility |
| `48055` | `48068` | 南瓜車 | Pumpkin Carriage | 1 (Land) | 4 Seater | Sit | Full 4-player team transport |
| `48056` | `48069` | 狗拉雪橇 | Dog Sled | 1 (Land) | 2 Seater | Stand | Snow & ice terrain sprint |
| `48057` | `48070` | 重型機車 | Heavy Motorcycle | 1 (Land) | 2 Seater | Sit | Highway & land speed boost |
| `48048` | `48071` | 魔法掃把 | Magic Broom | 1 (Land/Hover)| 1 Seater | Sit | Low-ground levitation |
| `48049` | `48072` | 魔羽掃把 | Magic Feather Broom | 1 (Land/Hover)| 1 Seater | Sit | Low-ground levitation |
| `48062` | `48073` | 筋斗雲 | Somersault Cloud | 1 (Land/Hover)| 1 Seater | Stand | Celestial cloud surfing |
| `48075` | `48076` | 聖誕雪橇 | Christmas Sled | 1 (Land) | 2 Seater | Sit | Holiday dual passenger sled |
| `48077` | `48078` | 虎氣球 | Tiger Balloon | 3 (Air) | 1 Seater | Stand | Hot air balloon elevation |
| `48079` | `48080` | 兔氣球 | Rabbit Balloon | 3 (Air) | 1 Seater | Stand | Hot air balloon elevation |
| `48082` | `48083` | 聽雨眠 | Rain Sleeping Pavillion | 3 (Air) | 1 Seater | Sit | Floating aerial canopy |
| `48086` | `48095` | 神龍飛船 | Dragon Airship | 3 (Air) | 4 Seater | Stand | Multi-passenger aerial battleship |
| `48087` | `48088` | 紙船 | Paper Boat | 2 (Water) | 1 Seater | Sit | River and marine water cruise |
| `48089` | `48090` | 紋銀弦月 | Silver Moon | 3 (Air) | 1 Seater | Sit | Enhanced lunar aerial flight |
| `48091` | `48092` | 七香車 | 7-Incense Chariot| 1 (Land) | 2 Seater | Sit | Imperial carriage transport |
| `48081` | `48094` | UFO | UFO Flying Saucer| 3 (Air/Space) | 1 Seater | Sit | Space & atmosphere antigravity |
| `48097` | `48099` | 鯊魚 | Shark Mount | 2 (Water) | 1 Seater | Ride | High-speed marine predator |
| `48105` | `48106` | 飛劍˙銀 | Silver Flying Sword | 3 (Air) | 1 Seater | Stand | Immortal sword aerial surfing |
| `48100` | `48101` | 瑪雅火箭 | Mayan Rocket | 3 (Air/Space) | 2 Seater | Sit | High-velocity rocket propulsion |
| `48052` | `(None)`| 機械豹 | Mechanical Panther | 1 (Land) | 1 Seater | Ride | Robotic quadruped land sprint |
| `48108` | `48109` | 傘中憩 | Parasol Rest | 3 (Air) | 1 Seater | Sit | Aerial umbrella glider |
| `48050` | `(None)`| 機械龍 | Mechanical Dragon | 1 (Land/Air) | 1 Seater | Ride | Heavy robotic dragon mount |
| `48118` | `48119` | 玩具木馬 | Toy Wooden Horse | 1 (Land) | 1 Seater | Ride | Whimsical rocking land mount |
| `48120` | `48121` | 飛劍翡翠 | Jade Flying Sword | 3 (Air) | 1 Seater | Stand | Emerald sword aerial surfing |
| `48051` | `(None)`| 機械象 | Mechanical Elephant | 1 (Land) | 2 Seater | Ride | Heavy armored elephant mount |

---

## 4. Navigation GPS & Minimap Positioning (`se_GPS.pas`)

The `se_GPS.pas` unit calculates real-time world map positioning:
1. **World Coordinate Projection:** Maps local sub-zone coordinates $(x, y)$ onto the global world atlas matrix ($MapID \times 1000 + SubArea$).
2. **Waykind Portal Pathfinding:** Identifies traversable warp gates based on the player's active `TrafficWayKind`. Standard foot players cannot cross oceanic portals requiring `WayKind == 2` or mountain cliffs requiring `WayKind == 3`.
3. **Vehicle Durability & Fuel Consumption:** Every 100 steps on motorized vehicles consumes 1 fuel unit (Gas / Crude / Ref Oil from Material Category 59..61) or degrades durability by 1 point.
