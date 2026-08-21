# Item Mall & Bonus / Lucky Draw System Protocol

## Overview
Replicates official Wonderland Online Item Mall category navigation, point balances, and the Bonus / Lucky Wheel / Mystery Draw lottery system based on `itemmallvebonuskismi.pcapng`.

---

## Protocol Breakdown

### 1. Item Mall Open & Point Synchronization
- **Client &rarr; Server (`AC 34 Sub 1` / `0x22:01`):**
  - `F4 44 03 00 22 01 00` (Queries balance upon opening Mall)
- **Server &rarr; Client:**
  - `AC 34 Sub 1`: Sends current IM Points (`22 01 [Points_2B LE]`)
  - `AC 75 Sub 3`: Updates in-game GUI Points counter (`4B 03 [Points_2B LE]`)
  - `AC 35 Sub 4`: Confirms shopping state (`23 04 [16 Zero Bytes]`)

### 2. Category & Tab Switching (`AC 75 Sub 4` / `0x4B:04`)
- **Client &rarr; Server:**
  - `F4 44 03 00 4B 04 [CategoryID (1B)]` (e.g. `0x15` = 21, `0x07` = 7, `0x0A` = 10)
- **Server &rarr; Client:**
  - `F4 44 06 00 39 01 [CategoryID (1B)] 00 00 00` (`AC 57 Sub 1`)
  - `AC 75 Sub 1`: Sends item entries corresponding to the category.

### 3. Bonus / Lucky Wheel / Roulette Draw (`AC 91` / `0x5B`)
- **Client &rarr; Server (`AC 91 Sub 1`):**
  - `F4 44 05 00 5B 01 [DrawID (2B LE)] [Type (1B)]`
  - In capture: `5B 01 B0 DE 00` (Draw #57008 / `0xDEB0`)
- **Server &rarr; Client (`AC 91 Sub 2`):**
  - `F4 44 26 00 5B 02 [DrawID (2B)] [01 (1B)] [List of Rewards: (ItemID_2B + Count_1B)...]`
  - Authentic prize pool items: `35135`, `35136`, `34029`, `34181`, `33031`, `34116`, `34011`, `34105`, `34167`, `30556`, `22884`.
  - Server rewards the player and notifies them in chat (`🎁 Bonus Draw: You won Item #...`).

---

## Handler References
- [`AC34.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC34.cs) (`ID = 34`): Shopping Cart & Balance Query.
- [`AC75.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC75.cs) (`ID = 75`): Mall Catalog, Category Switching, and Purchases.
- [`AC91.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC91.cs) (`ID = 91`): Bonus & Lucky Wheel Draw System.
