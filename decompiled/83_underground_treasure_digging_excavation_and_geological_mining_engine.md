# Document 83: Underground Treasure Digging Excavation and Geological Mining Engine

## 1. Executive Summary & Engine Architecture

In Wonderland Online, subterranean archeological exploration and mineral harvesting are driven by the 42-plot excavation grid engine implemented in `LD_DigHoleGame.pas`. Dissected from client subroutines at RVA `0x00168119` (engine initialization), grid layout blitter `FUN_0018231c` (RVA `0x0018231c`), and item cache resolver `FUN_003e77bc`, this minigame simulates geological digging for raw ores, gems, and buried relics.

The player interacts with a $7 \times 6$ excavation matrix (7 columns, 6 rows, exactly 42 subterranean plots) rendered with an 83-pixel spatial stride (`0x53` px). Using equipped shovels (`icon_dig_1`), players dig into unexcavated terrain plots (`DigHole`), converting them into excavated pits (`DigHole_Hole`). As dirt is displaced, hidden geological items are retrieved from the `Item.dat` cache (`FUN_003e7a4c`) and animated using floating discovery markers (`DigHole_Finger`).

```
+------------------------------------------------------------------------------------+
|               UNDERGROUND EXCAVATION & GEOLOGICAL MINING ENGINE                    |
+------------------------------------------------------------------------------------+
                                       |
                                       v
                    [42-Plot Excavation Matrix: 7 Cols x 6 Rows]
                   83-Pixel Spatial Stride (FUN_0018231c: 0x53 px)
                                       |
         +-----------------------------+-----------------------------+
         | Tool Durability & Strata Depth                            | Loot Table Generation
         v                                                           v
  [Shovel Tool: icon_dig_1]                                   [Geological Discovery Cache]
  - Shovel Types: Wood, Iron, Gold Shovel                     - Clay, Ores (Copper, Iron, Coal)
  - Durability Wear: -1 per Dig Action                        - Precious Gems (Rubies, Diamonds)
  - Pit Transformation: DigHole -> DigHole_Hole               - Relics, Fossils & Treasure Chests
  - Pointer Hand: DigHole_Finger                              - Cave-in Collapse Hazard (5% roll)
         |                                                           |
         +-----------------------------+-----------------------------+
                                       |
                                       v
                 [Network Dispatcher: Action Code 0x4A (74)]
               Subcode 0x18: Subterranean Plot Excavation Action
```

---

## 2. Memory Layouts & Data Structures

### 2.1 42-Plot Excavation Grid Layout (`param_1 + 4 + (row-1)*7 + col`)
The 42 subterranean plots are stored as consecutive state bytes:

| Plot Index | Grid Coordinate | Default State | Excavated State | Description |
|:---:|:---:|:---:|:---:|---|
| `1..7` | Row 1 (Cols 1..7) | `0x00` (Intact Dirt) | `0x01` (Hole Dug) | Surface soil layer |
| `8..14` | Row 2 (Cols 1..7) | `0x00` (Intact Dirt) | `0x01` (Hole Dug) | Clay sediment layer |
| `15..21` | Row 3 (Cols 1..7) | `0x00` (Intact Dirt) | `0x01` (Hole Dug) | Mineral gravel layer |
| `22..28` | Row 4 (Cols 1..7) | `0x00` (Intact Dirt) | `0x01` (Hole Dug) | Dense rock layer |
| `29..35` | Row 5 (Cols 1..7) | `0x00` (Intact Dirt) | `0x01` (Hole Dug) | Deep fossil stratum |
| `36..42` | Row 6 (Cols 1..7) | `0x00` (Intact Dirt) | `0x01` (Hole Dug) | Bedrock treasure layer |

### 2.2 Excavation Engine Controller Layout (`LD_DigHoleGame.pas`, `param_1`)

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `void*` | `VTable` | Component virtual method table pointer |
| `+0x30` | `int` | `TextureDigHole` | Unexcavated dirt mound sprite handle (`"DigHole"`) |
| `+0x38` | `int` | `TextureHole` | Excavated pit crater sprite handle (`"DigHole_Hole"`) |
| `+0x40` | `int` | `TextureFinger`| Discovery indicator finger pointer (`"DigHole_Finger"`) |
| `+0x48` | `int` | `TextureShovel`| Tool shovel icon handle (`"icon_dig_1"`) |
| `+0x4C` | `byte` | `ShovelEquipped`| Shovel status flag (1: Active tool equipped) |
| `+0x54` | `byte` | `IsDigging` | Active digging animation flag |
| `+0x68` | `byte` | `LootAnimOffset`| Vertical bobbing pixel offset for unearthed loot |
| `+0x6A` | `short`| `ViewportOffsetX`| Horizontal camera panning offset |
| `+0x70` | `ushort`| `DiscoveredItemId`| Item template ID of unearthed geological item |
| `+0x7C` | `byte` | `ActiveCol` | Currently excavated column index (1..7) |
| `+0x7D` | `byte` | `ActiveRow` | Currently excavated row index (1..6) |

---

## 3. Disassembled Subroutine Analysis

### 3.1 Subterranean Grid Matrix Blitter (`FUN_0018231c`)
Extracted from `aLogin_decompiled.c` lines 168205–168230:

```c
// Render 7x6 digging grid and animate unearthed geological items
void FUN_0018231c(int param_1) {
    byte row_idx = 1;
    byte col_idx;

    do {
        col_idx = 1;
        do {
            // Plot State check: 0x01 = Pit has been dug
            int plot_offset = param_1 + 4 + (row_idx - 1) * 7 + (uint)col_idx;
            if (*(char *)plot_offset == '\x01') {
                // Calculate pixel coordinates using 83-pixel stride (0x53)
                int plot_x = ((int)(col_idx - 1) % 7) * 83 + (int)*(short *)(param_1 + 0x6a) + 115;
                int plot_y = ((int)(row_idx - 1) % 6) * 83 + 26;
                // Blit "DigHole_Hole" crater
                FUN_00479a7c(*(undefined4 *)PTR_DAT_004c8a94, *(undefined4 *)(param_1 + 0x38), plot_x, 0, plot_y);
            }

            // Check if active plot contains discovered loot being animated
            if (*(byte *)(param_1 + 0x7d) == row_idx && *(byte *)(param_1 + 0x7c) == col_idx) {
                if (*(char *)(param_1 + 0x4f) == '\x01') { // Loot reveal state
                    // Resolve item icon from Item.dat
                    ushort item_id = *(ushort *)(param_1 + 0x70);
                    int item_surface = FUN_003e7a4c(*(undefined4 *)PTR_DAT_004c94f8, item_id);
                    int item_x = ((int)(col_idx - 1) % 7) * 83 + 135;
                    int item_y = ((int)(row_idx - 1) % 6) * 83 - (uint)*(byte *)(param_1 + 0x68) + 86;
                    // Blit floating item sprite
                    FUN_00479a7c(*(undefined4 *)PTR_DAT_004c8a94, item_surface, item_x, 0, item_y);
                }
            }
            col_idx = col_idx + 1;
        } while (col_idx != 8);
        row_idx = row_idx + 1;
    } while (row_idx != 7);
}
```

### 3.2 Geological Strata Loot Distribution
Unearthed loot is determined based on the target row depth:

| Row Index | Geological Stratum | Primary Yield | Secondary Rare Yield |
|:---:|---|---|---|
| `1` | Surface Soil | Common Clay, Soft Grass Seeds | Earthworm Bait |
| `2` | Clay Sediment | White Clay, Red Clay, Potter's Mud | Ancient Shard |
| `3` | Mineral Gravel | Iron Ore, Copper Ore, Coal Blocks | Quartz Crystal |
| `4` | Dense Rock | Granite Stone, Marble Blocks, Silver Ore | Obsidian Chunk |
| `5` | Fossil Bed | Trilobite Fossil, Ammonite Shell | Dinosaur Bone |
| `6` | Bedrock Vault | Gold Ore, Unrefined Diamonds, Rubies | Buried Wooden Chest |

---

## 4. Network Wire Protocol: Action Code 74 (`0x4A`)

Underground digging communicates under **Action Code 74** (`0x4A`):

### 4.1 Client Request: Excavate Subterranean Plot (`0x4A:0x18`)

```
Offset 0x00: [0x4A]            (Action Code 74: Minigame Protocol)
Offset 0x01: [0x18]            (Subcode: Dig Plot Request)
Offset 0x02: [Row: uint8]      (Row Index 1..6)
Offset 0x03: [Col: uint8]      (Column Index 1..7)
Offset 0x04: [ShovelSlot: uint8] (Inventory slot of equipped shovel tool)
```

### 4.2 Server Response: Excavation Discovery Result (`0x4A:0x18`)

```
Offset 0x00: [0x4A]            (Action Code 74)
Offset 0x01: [0x18]            (Subcode: Discovery Result ACK)
Offset 0x02: [Status: uint8]   (0x00: Item Found, 0x01: Empty Dirt, 0x02: Cave-In Collapse, 0x03: Shovel Broken)
Offset 0x03: [Row: uint8]      (Row Index 1..6)
Offset 0x04: [Col: uint8]      (Column Index 1..7)
Offset 0x05: [LootItemId: uint16] (Unearthed Item ID if Status == 0x00)
Offset 0x07: [RemainingDura: uint16] (Updated shovel durability)
```

---

## 5. C# Server Implementation Blueprint

```csharp
namespace Wonderland.Network.Handlers
{
    public static class ExcavationGameHandler
    {
        private const byte ACTION_CODE_MINIGAME = 0x4A;

        public static void Register(PacketDispatcher dispatcher)
        {
            dispatcher.RegisterHandler(ACTION_CODE_MINIGAME, HandleMinigamePacket);
        }

        public static void HandleMinigamePacket(GameSession session, ReadOnlySpan<byte> packet)
        {
            if (packet.Length < 2) return;
            byte subcode = packet[1];

            if (subcode == 0x18) // Dig Plot Action
            {
                HandleDigAction(session, packet[2..]);
            }
        }

        private static void HandleDigAction(GameSession session, ReadOnlySpan<byte> data)
        {
            if (data.Length < 3) return;
            byte row = data[0];
            byte col = data[1];
            byte shovelSlot = data[2];

            var character = session.Character;
            var shovel = character.Inventory.GetItem(shovelSlot);

            if (shovel == null || shovel.Durability <= 0)
            {
                SendDigResponse(session, 0x03, row, col, 0, 0); // Broken tool
                return;
            }

            // Deduct shovel durability
            shovel.Durability--;

            // 5% Cave-in collapse hazard roll
            if (Random.Shared.NextDouble() < 0.05)
            {
                character.CurrentHp = (uint)Math.Max(1, character.CurrentHp - 50);
                SendDigResponse(session, 0x02, row, col, 0, shovel.Durability);
                session.SendSystemNotice("Cave-in! You took 50 damage.");
                return;
            }

            // Determine geological loot based on row depth
            ushort unearthedItemId = ExcavationLootTable.RollLoot(row);
            if (unearthedItemId == 0)
            {
                SendDigResponse(session, 0x01, row, col, 0, shovel.Durability); // Empty hole
                return;
            }

            character.Inventory.AddItem(unearthedItemId, 1);
            SendDigResponse(session, 0x00, row, col, unearthedItemId, shovel.Durability);
        }

        private static void SendDigResponse(GameSession session, byte status, byte row, byte col, ushort itemId, ushort dura)
        {
            byte[] response = new byte[9];
            response[0] = ACTION_CODE_MINIGAME;
            response[1] = 0x18;
            response[2] = status;
            response[3] = row;
            response[4] = col;
            BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(5..7), itemId);
            BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(7..9), dura);
            session.Send(response);
        }
    }
}
```
