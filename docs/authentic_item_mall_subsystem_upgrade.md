# Authentic Item Mall & Bonus Mall Subsystem Upgrade Specification

## 1. Overview & Architecture Parity
Based on extensive reverse engineering of authentic live server packet captures (`itemmalldatalari.pcapng`, `itemmallvebonuskismi.pcapng`), client decompilation (`aLogin.exe` `FUN_0025a684`, `FUN_0025b5ec`, `FUN_001af620`), and architectural parity with the high-fidelity implementation in `D:\GitHub\Wonderland Online`, the Item Mall subsystem in `Wonderland-Private-Server` has been upgraded to a production-grade dual-tier architecture:

```
[ WLO Client: aLogin.exe ]
       |
       +---> Port 6416 (TCP): Dedicated Item Mall Catalog Service (ItemMallServer.cs)
       |        <--- Raw binary catalog stream: [0xC9, 0x00, 0x01, ... (ItemID 2B LE, HotFlag 1B) ...]
       |        <--- Dispatches all 152 Points Mall catalog entries and terminates socket
       |
       +---> Port 6414 (TCP): Game Server Action Code Protocol
       |        <--- AC 75 Sub 1 (Points Mall Catalog: 152 authentic items, 10B each)
       |        <--- AC 75 Sub 10 (Bonus Mall Catalog: 71 authentic items, 10B each)
       |        <--- AC 75 Sub 8 (Mall System Settings: [75, 8, 0, 0])
       |        <--- AC 75 Sub 7 (Mall Status Indicator: [75, 7, 1])
       |        <--- AC 75 Sub 3 (Authentic 13-byte Dual Balance: IM Points 4B + Bonus Points 4B)
       |        <--- AC 75 Sub 4 (Category switch ACK via AC 57:1 or Points Mall purchase)
       |        <--- AC 75 Sub 5 (Bonus Mall purchase)
       |        <--- AC 34 Sub 1 & Sub 2 (Shopping Cart checkout and points inquiry)
       |        <--- AC 91 Sub 1, 2, 3 (Item Mall Bonus rewards catalog and item claims)
       |        <--- AC 13 Sub 238 -> Sub 42 (Top-Right HUD Mall button synchronization)
       |        <--- AC 23 Sub 54 & Sub 25/26 (Inventory/Map mall synchronization)
       |
       +---> SQLite / MySQL Database Persistence
                <---> accounts.im_points / accounts.im_bonus
                <---> Data/item_mall.json (224 authentic catalog items)
```

---

## 2. Catalog Dataset & 11-Page Grocery Architecture

The server imports all 224 authentic items from [`Data/item_mall.json`](file:///d:/GitHub/Wonderland-Private-Server/Data/item_mall.json):
- **Points Mall (`IsBonus = 0`)**: 152 items.
- **Bonus Mall (`IsBonus = 1`)**: 71 items.
- **11-Page Grocery Layout**:
  - The authentic client renders 12 items per page.
  - Category 3 (Grocery Singles): 102 items (Bottles, potions, reset scrolls, sparrows, pills).
  - Category 4 (Grocery Bundles): 21 items (5x, 8x, 20x, 50x bundle packs).
  - Total Grocery items: $102 + 21 = 123$ items.
  - Total pages: $\lceil 123 / 12 \rceil = 11$ pages!

### Descriptor Layout (`MallItemEntry`)
Each catalog entry contains complete metadata:
- `ItemID`: In-game Item ID mapping to `Item.dat`.
- `ItemName`: Localized item name.
- `Category`: Text category name (`Hot`, `Armory`, `Weaponry`, `Grocery`, `Furniture`, `Slot Machine`, `Forging Room`).
- `PointCost`: Actual purchase price in IM Points or Bonus Points.
- `OriginalPrice`: Base / list price displayed at `POINTS`.
- `GoldCost`: Optional gold price.
- `Count`: Delivered quantity per pack (1 for single, 5/8/20/50 for bundles).
- `IsHot`, `IsNew`, `IsLimited`, `OnSale`: Promotional attribute flags.
- `Discount`: Discount percentage (`100` = full price / no sale, `<100` = sale discount percentage).
- `Badge`: Badge overlay tag (`0` = Normal, `1` = `NEW`, `2` = `HOT`, `3` = `LIMITED`).
- `CategoryID`: UI tab selector (1=Hot, 2=Armory, 3=Grocery single, 4=Grocery pack, 5=Furniture).
- `OrderIndex`: UI display sort index.
- `IsBonus`: `0` for Points Mall, `1` for Bonus Mall.
- `SubCategoryID`: Subcategory grouping index.

---

## 3. Network Protocol Implementation

### A. Dedicated TCP Port 6416 Service ([ItemMallServer.cs](file:///d:/GitHub/Wonderland-Private-Server/Src/Server/ItemMallServer.cs))
- **Packet Structure**:
  - `[0]`: Opcode `0xC9` (201)
  - `[1-2]`: Header `0x00 0x01`
  - Per item (3 bytes each): `[ItemID (uint16_LE), HotFlag (uint8)]` (`2` = Hot/Special, `3` = Normal).
- Flushes all 152 Points Mall items to client stream and cleanly closes the socket.

### B. In-Game Action Codes (Port 6414)

#### 1. Map Entry & Login Mall Handshake Sequence (`ItemMallManager.SendInitialMallSync`)
Called on character spawn in [`WorldServer.CommenceLogin`](file:///d:/GitHub/Wonderland-Private-Server/Src/Server/WorldServer.cs):
1. **`AC 75 Sub 1`**: Points Mall catalog (152 items).
2. **`AC 75 Sub 10`**: Bonus Mall catalog (71 items).
3. **`AC 75 Sub 8`**: System settings (`[75, 8, 0, 0]`).
4. **`AC 75 Sub 7`**: Status indicator (`[75, 7, 1]`).
5. **`AC 75 Sub 3`**: Dual balance sync.

#### 2. Catalog Packet Layout (`AC 75 Sub 1` & `AC 75 Sub 10`)
- **Header**: `[75, sub_code, ItemCount (uint16_LE)]` (`sub_code = 1` for Points Mall, `10` for Bonus Mall).
- **Per Item (Exactly 10 Bytes)**:
  - `[0-1]` `item_id (uint16_LE)`: Authentic item ID.
  - `[2]` `count (uint8)`: Quantity per pack.
  - `[3-4]` `base_price (uint16_LE)`: List price.
  - `[5]` `discount (uint8)`: Sale discount percentage.
  - `[6]` `badge (uint8)`: Badge tag (1=NEW, 2=HOT, 3=LIMITED, 0=Normal).
  - `[7]` `category_id (uint8)`: Category byte (1..5).
  - `[8-9]` `order_idx (uint16_LE)`: Sort order index.

#### 3. Dual Balance Synchronization (`AC 75 Sub 3`)
- **Payload**: `[75, 3, IMPoints (uint32_LE), BonusPoints (uint32_LE), 0 (uint16_LE), 0 (uint8)]`
- Total length: 13 bytes.
- Populates client memory offsets `+0x5660` (IM Points) and `+0x5664` (Bonus Points) to enable purchase buttons.

#### 4. Category Switch & Purchase Protocol ([AC75.cs](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC75.cs))
- **Category Switch**: Client sends `[75, 4, category_id (1B)]`.
  - Server sends `AC 57:1` ACK `[57, 1, category_id, 0, 0, 0]`.
  - Server sends `AC 34:1` balance and `AC 75:3` balance.
  - If `category_id > 0`, re-dispatches catalog. If `category_id == 0` (minigame exit), catalog is withheld to prevent recursive UI reopenings.
- **Purchase Execution**:
  - Points Mall: Client sends `[75, 4, item_id (2B), quantity (1B)]`.
  - Bonus Mall: Client sends `[75, 5, item_id (2B), quantity (1B)]`.
  - Server deducts points, grants items to inventory, broadcasts character un-disguise (`AC 5:5`), syncs dual balance (`AC 75:3`), and responds with authentic confirmation:
    `S->C [75, sub, RemPoints (4B), SpentPoints (4B), ItemID (2B), Quantity (1B)]`.

#### 5. Shopping Cart & Minigame Checkout ([AC34.cs](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC34.cs))
- **Mode 0 (Points Query)**: Responds with `[34, 1, points (4B)]`, `AC 75:3`, and catalog.
- **Mode >= 1 (Cart Checkout)**: Buys slot item, responds with `[34, 1, rem_points (4B)]`, `AC 75:3`, `AC 35:4` (16 zero bytes cart confirmation), and catalog.
- **Subcode 2 (Direct Purchase)**: Buys item, syncs points and cart clearance ACK.

#### 6. Bonus Rewards Catalog & Claims ([AC91.cs](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC91.cs))
- **Sub 1 (Query Catalog)**: Client sends `[91, 1, category_id (2B), page (1B)]`. Server responds with `AC 91 Sub 2` containing 11 authentic bonus reward items (3 bytes each: `item_id (2B) + count (1B)`).
- **Sub 3 (Claim Item)**: Client sends `[91, 3, item_id (2B)]`. Server verifies Bonus Points $\ge 100$, deducts 100 Bonus Points, grants item with `AC 23 Sub 6` acquisition popup, saves database, syncs `AC 75:3` balance, and prints confirmation in chat.

#### 7. Top-Right HUD Mall Button ([AC13.cs](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC13.cs))
- When client sends `AC 13 Sub 238`:
  - Server responds `AC 13 Sub 42` (`[13, 42, char_id (4B)]`).
  - Dispatches `AC 75 Sub 3` dual balance.
  - Dispatches `AC 75 Sub 1` Points Mall catalog.
  - Dispatches `AC 75 Sub 10` Bonus Mall catalog.

---

## 4. Persistence & Database Management

- **[`Game.Code.User`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/User.cs)**: Added `IMBonus` thread-safe property alongside `IM`.
- **[`DataBase.UserDataBase`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/UserDataBase.cs)**: Added `SetIMBonusPoints(userId, points)` and `GetIMBonusPoints(userId)` with dynamic schema migration.
- **[`MainForm1.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.cs)**:
  - Registered `ItemMallManager.OnBonusPointsChanged`.
  - Updated `RefreshMallGrid()` to display `ItemID`, `ItemName`, `Category`, `PointCost`, `OriginalPrice`, `Discount`, `Badge`, `Count`, and `MallType` (`Points` vs `Bonus`) across all 224 catalog entries.
