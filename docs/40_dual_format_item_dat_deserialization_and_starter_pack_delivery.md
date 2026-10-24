# Technical Specification 40: Dual-Format ItemDat Deserialization & Starter Pack Delivery Lifecycle

## 1. Context & Architectural Root-Cause Diagnosis

During in-game testing of newly created Level 1 characters (e.g. `gmone` on the Ship Deck), the character spawned without any equipment (rendered naked in default underwear) and with an empty 50-slot inventory bag.

### 1.1 Binary Asset Discrepancy & Format Collision
The server loader in [`MainForm1.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.cs) searches for item database files in priority order:
1. `Data/itemDat.wpdat` (226,446 bytes)
2. `Data/Item.dat` (3,267,044 bytes)

Because `Data/itemDat.wpdat` exists, it is loaded by default.
- `itemDat.wpdat` is a compact binary cache containing 4,818 records of exactly 47 bytes (`Marshal.SizeOf(typeof(PhxItemInfo)) = 47`).
- `Item.dat` is the native client asset containing 7,244 records of exactly 451 bytes (`0x1C3`), obfuscated with stream XOR keys (`0xEFC3` for ItemID at `+16`, `0x9A` offset `9` for type at `+15`, equippos at `+46`, and cell dimensions at `+406`/`+407`).

When `RECORD_SIZE = 451` was previously hardcoded in [`PhxItemDat.cs`](file:///D:/GitHub/Wonderland-Private-Server/PhoenixData/DataFiles/PhxItemDat.cs), the deserializer read 451 bytes from the 47-byte-record `itemDat.wpdat`. This created an off-by-404-byte stream desync on every read:
1. `GetItemByID` failed to locate item IDs, returning `null` for virtually all queries.
2. In `AC09.Recv1`, [`EquipManager.SetBeginnerOutfit()`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Equip.cs) failed to look up clothing item templates (`21004`, `24004`, `22005`, `21006`, etc.), leaving all equipment slots empty.
3. In `AC09.Recv1`, [`StarterPackManager.DeliverToPlayer()`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/StarterPackManager.cs) called [`Inventory.AddItem()`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Inventory.cs), which rejected all items because `ItemDat?.GetItemByID(itemId)` returned `null`.
4. The empty character was saved to SQLite via [`CharacterDataBase.WriteNewPlayer()`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/CharacterDataBase.cs), producing zero rows in `inventory` (`storID = 0`) and zero rows in `inventory` (`storID = 1`).

---

## 2. Implementation Details

### 2.1 Dual-Format Adaptive Deserializer
[`PhxItemDat.cs`](file:///D:/GitHub/Wonderland-Private-Server/PhoenixData/DataFiles/PhxItemDat.cs) dynamically detects the binary file format upon opening the stream:

```csharp
bool isClientItemDat = (fileStream.Length % 451 == 0 && fileStream.Length > 1000000);
int recordSize = isClientItemDat ? 451 : Marshal.SizeOf(typeof(PhxItemInfo)); // 47
byte[] recBuf = new byte[recordSize];
```

1. **451-Byte Format (`Item.dat`):**
   - Item Name: byte length at offset 0, ASCII characters at offsets 1..20.
   - ItemID: `(BitConverter.ToUInt16(recBuf, 16) ^ 0xEFC3) - 9`
   - ItemType: `(recBuf[15] ^ 0x9A) - 9`
   - Equippos: `(recBuf[46] ^ 0x9A) - 9`
   - Cellwidth: `(recBuf[406] ^ 0x9A) - 9`
   - Cellheight: `(recBuf[407] ^ 0x9A) - 9`

2. **47-Byte Format (`itemDat.wpdat`):**
   - Directly unmarshaled into [`PhxItemInfo`](file:///D:/GitHub/Wonderland-Private-Server/PhoenixData/DataFiles/PhxItemInfo.cs) via `Generics.ConvertTo<PhxItemInfo>`.
   - Cellwidth (offset 34) and Cellheight (offset 33) defaulted to 1 if 0.
   - Raft (`48010`) natively parses as $4 \times 3$.
   - Work Platform (`38049`) natively parses as $3 \times 2$.
   - Resource Recycling (`38055`) natively parses as $2 \times 2$.

Both pathways index into a fast $O(1)$ dictionary lookup `Dictionary<ushort, PhxItemInfo> m_Dict` in `GetItemByID`.

### 2.2 Starter Item Catalog Expansion
Expanded the starter pack in [`StarterPackManager.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/StarterPackManager.cs) and SQLite table `starter_items` to 10 essential items:

| Slot Order | ItemID | Name | Count | Footprint | Category |
|:---|:---|:---|:---|:---|:---|
| 1 | 34038 | Notepad | 1 | $1 \times 1$ | Guide |
| 2 | 34058 | Remote Control | 1 | $1 \times 1$ | Auto-Battle |
| 3 | 36002 | Personal Tent | 1 | $1 \times 1$ | Housing / Camping |
| 4 | 48010 | Raft | 1 | $4 \times 3$ | Water Transport |
| 5 | 32176 | Fugu Hot Pot | 50 | $1 \times 1$ | Full Recovery |
| 6 | 34014 | Tao Rice Ball | 10 | $1 \times 1$ | Companion Food |
| 7 | 34026 | Protective EXP Pill | 5 | $1 \times 1$ | Death EXP Protection |
| 8 | 34169 | Bamboo Dragonfly | 1 | $1 \times 1$ | Flying Mount |
| 9 | 34190 | 10X Holy EXP Potion | 3 | $1 \times 1$ | EXP Multiplier |
| 10 | 34253 | Training Ticket | 5 | $1 \times 1$ | Training Island Pass |

### 2.3 Login Self-Healing Fallbacks
In [`WorldServer.CommenceLogin()`](file:///D:/GitHub/Wonderland-Private-Server/Src/Server/WorldServer.cs), added dual checks executing prior to transmitting `AC 23:5` (inventory) and `AC 23:11` (equipment):

1. **Inventory Check:** If a Level 1 character has no starter items (`!StarterPackManager.HasAnyStarterItem(src)`), `StarterPackManager.DeliverToPlayer(src, sendData: false)` places all 10 starter items into free slots and persists them to SQLite.
2. **Equipment Check:** If a Level 1 character has no equipment worn (`src.WornCount == 0`), `src.SetBeginnerOutfit()` re-equips the character according to their `Body` and `Head` style and persists the equipment to SQLite.

---

## 3. Empirical Verification

1. **`itemDat.wpdat` Deserialization:** Loaded 4,817 items. Verified:
   - Item 34038 (Notepad): $1 \times 1$, `Equippos = 7`, `Type = 25`
   - Item 36002 (Personal Tent): $1 \times 1$, `Equippos = 7`, `Type = 27`
   - Item 48010 (Raft): $4 \times 3$, `Equippos = 7`, `Type = 39`
   - Item 21004 (Dust Coat): $1 \times 1$, `Equippos = 2` (Body), `Type = 12`
   - Item 24004 (Shoes): $1 \times 1$, `Equippos = 5` (Feet), `Type = 15`
2. **`Item.dat` Deserialization:** Loaded 7,244 items. Verified identical dimensions and slot metadata across all test items.
3. **End-to-End Build:** Solution built with zero warnings and zero errors.
