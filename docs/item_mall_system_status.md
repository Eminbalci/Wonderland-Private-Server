# Authentic Item Mall & Catalog System Protocol Reference

## 1. Overview & Architecture
Reverse engineering of the official Wonderland Online network capture (`itemmall.pcapng`) and client decompilation (`aLogin_decompiled.c`) identifies the dual-tier Item Mall architecture:

```
[ WLO Client: aLogin.exe ]
       |
       +---> Port 6416 (TCP): Item Mall Catalog Service
       |        <--- Dynamic binary catalog: [0xC9, 0x00, Count(2B), 10B per item: ItemID, 0, Price, 0, SubCat, MainCat, Index]
       |        <--- 15s keepalive pings: [0x00, 0x00, 0x00, 0x00, 0x00, 0x00]
       |
       +---> Port 6414 (TCP): Game Server (Action Code Protocol)
       |        <--- AC 54:201 (Category Catalog Matrix)
       |        <--- AC 35:4 (Mall State Confirmation, 16 zeros)
       |        <--- AC 35:11 (Mall Subcode ACK)
       |        <--- AC 35:12 (Mall User Bind: CharID + 00)
       |        <--- AC 5:8 (Character Status Sync)
       |        <--- AC 5:17 (Starter Beach / Record / Carnie Return Warp)
       |        <--- AC 90:1 (Mall / Map Service Descriptor)
       |        <--- AC 183:17 (Client UI State Acknowledgments)
       |        <--- AC 13:42 (Item Mall Confirmation)
       |        <--- AC 23:122 (Item Mall Claim & ACK)
       |        <--- AC 238:183 & AC 225:252 (Item Mall Matrix & Claim Sync)
       |        <--- AC 23:82 (Native URL Redirection via ShellExecuteA)
       |
       +---> Port 8080 (HTTP): Interactive Web Item Mall Portal
                <---> /api/buy?item=<id>&user=<name> (Instant in-game item delivery & point balance sync)
```

---

## 2. Port 6416: Item Mall Catalog TCP Service ([ItemMallServer.cs](file:///d:/GitHub/Wonderland-Private-Server/Src/Server/ItemMallServer.cs))
- **Port**: 6416 (Standard WLO Game Port + 2)
- **Lifecycle**: Initiated on server startup alongside `LoginServer` (Port 6414). Maintains a persistent TCP connection with 15-second keepalive pings (`00 00 00 00 00 00`).
- **Protocol Structure (`FUN_0025a684`)**:
  - `0xC9`: Opcode 201 (`byte`, 1 byte)
  - `ItemCount`: Total items in catalog (`ushort`, 2 bytes)
  - `10-byte Item Descriptor` (for each item):
    - `ushort ItemID`: Authentic WLO Item ID (offset 4, mapped to `Item.dat`)
    - `byte Flag1`: 0 (offset 6)
    - `ushort Price`: IM Points cost (offset 7)
    - `byte Flag2`: 0 (offset 9)
    - `byte SubCategory`: 1 (offset 10)
    - `byte MainCategory`: 1 (Hot), 2 (Consumables), 3 (Equipment), 4 (Vehicles/Tents), 5 (Special) (offset 11)
    - `ushort Index`: 1-indexed item number (offset 12)

---

## 3. Game Server Action Codes Protocol Sequence

### A. Initial Connection & Character Login
1. **Version & Catalog Handshake (`AC 0`)**:
   - Server returns Version (`AC 1:9`) and Mall Category Catalog Matrix (`AC 54:201`).
2. **Character Spawn Flow (`WorldServer.CommenceLogin`)**:
   - `AC 54:201`: `[54, 201, 0, 1, 101, 0, 3, 103, 0, 2, 104, 0, 3, 102, 0, 3]`
   - `AC 35:4`: `[35, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]` (16 zeros)
   - `AC 35:11`: `[35, 11]`
   - `AC 35:12`: `[35, 12, CharID(4B), 0]`

### B. Client World Entry UI Handshakes
1. **`AC 5 Sub 7` -> `AC 5 Sub 8` ([AC05.cs](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC05.cs))**:
   - Client sends `[5, 7, 0]`.
   - Server responds `[5, 8, CharID(4B), 0]`.
2. **`AC 5 Sub 17` (Rescue / Return Warp)** ([AC05.cs](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC05.cs)):
   - Client sends `[5, 17, DestChoice(1B)]`.
   - **DestChoice 1**: Starter Beach (`Map 11016, X: 1181, Y: 243`).
   - **DestChoice 2**: Record Point (`c.ReturnSpawnMap` or default Starter Beach).
   - **DestChoice 3**: Carnie Island / Carnival (`Map 10901, X: 482, Y: 395`).
   - Server executes `Teleport(TeleportType.CmD, c, 0, warp)`.
3. **`AC 89 Sub 0` -> `AC 90 Sub 1` ([AC89.cs](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC89.cs))**:
   - Client sends `[89, 0, MapID(2B), 0, 0]`.
   - Server responds `[90, 1, 0, 1, 1, 3, 2, 3]`.
4. **`AC 92 Sub 1` ([AC92.cs](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC92.cs))**:
   - Client sends `[92, 1]` state sync.
5. **`AC 183 Sub 17` ([AC183.cs](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC183.cs))**:
   - Client sends `[183, 17, 0]`.
   - Server responds with `[183, 17, 0]`.

### B. In-Game Item Mall Protocol Flow (`aLogin.exe` & `ItemMallManager.cs`)
1. **Catalog Handshake (`AC 75 Sub 1` & Port 6416)**:
   - Client fetches binary item list on Port 6416 (`ItemMallServer.cs`) and `AC 75 Sub 1`.
   - Sets client memory flag `[eax + 0x5698] = 1`.
2. **Point Balance Synchronization (`AC 75 Sub 3` & `AC 34 Sub 1`)**:
   - `AC 75 Sub 3`: `[75, 3, Points(uint16)]` sets `[eax + 0x5660]` (Bonus/IM Points) so purchase buttons are enabled.
   - `AC 34 Sub 1`: `[34, 1, Points(uint16)]` responds to client points query (`"WLO Point Remain: <points>"`).
3. **In-Game Direct Purchase Protocol (`AC 75 Sub 5` / `AC 75 Sub 4`)**:
   - When a player selects an item (e.g. Jalor `#48005`, Mecha Dragon `#48050`, Robot `#48014`) and confirms in the in-game UI dialog (`"Buy [ItemName]?"`):
   - Client sends: `C->S [75, 5, ItemID(2B), Quantity(1B), Type(2B)]`.
   - Server verifies points (`player.UserAccount.IM >= totalCost`).
   - Server deducts points, immediately saves new balance to `ServerDataBase.db` via `OnPointsChanged`.
   - Server adds the exact ItemID to player inventory (`player.Inv.AddItem`).
   - Server responds with authentic confirmation packet (`FUN_0025b5ec`): `S->C [75, 5, RemPoints(4B), SpentPoints(4B), ItemID(2B), Quantity(1B)]`.
   - Client plays sound `sound/wav0152.wav` and prints `"[ItemName] success. Spent: [SpentPoints]"`.
4. **Database Persistence & Auto-Save**:
   - `UserDataBase.SetIMPoints` persists points directly using parameterized SQL queries across SQLite and MySQL.
   - `WorldServer.AutoSaveLoop` also verifies and syncs `UserAccount.IM` periodically.

4. **`AC 23 Sub 54` -> `AC 23 Sub 122`**:
   - When player clicks Item Mall UI button, client sends `AC 23:54`.
   - Server dispatches:
     - `AC 75:1` (Catalog)
     - `AC 75:3` (Balance)
     - `AC 23:122` (`[23, 122, CharID(4B)]`)

