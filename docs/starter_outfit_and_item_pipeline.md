# Starter Outfit and Item Resolution Pipeline

## Overview
This document specifies the server initialization pipeline for item data resolution, beginner outfit allocation, and starter item pack delivery during new character creation (`AC09`) and first-time login fallback.

## 1. Item Database Resolution
The server's global item manager (`cGlobal.ItemDatManager`) is an instance of [`PhxItemDat`](file:///D:/GitHub/Wonderland-Private-Server/PhoenixData/DataFiles/PhxItemDat.cs).
* **Target File:** [`Data/itemDat.wpdat`](file:///D:/GitHub/Wonderland-Private-Server/Data/itemDat.wpdat)
* **Record Structure:** 47-byte fixed binary struct (`Marshal.SizeOf(typeof(PhxItemInfo)) == 47`).
* **Record Count:** Exactly 4,818 authenticated server records.
* **Resolution Priority:**
  1. `RCLibrary.Core.PathHelper.GetDataFilePath("itemDat.wpdat")` (Primary authoritative server catalog).
  2. `RCLibrary.Core.PathHelper.GetDataFilePath("Item.dat")` (Fallback).

### Rationale
Raw client `Item.dat` (3.2 MB) uses encrypted/client-specific packing incompatible with direct 47-byte struct streaming in `PhxItemDat`. Attempting to load raw `Item.dat` causes offset desynchronization where every `GetItemByID(ushort id)` query returns `null`. Prioritizing `itemDat.wpdat` restores valid item definitions for all 4,818 IDs across equips, consumables, and quest rewards.

## 2. Character Creation Outfit Allocation
When a player creates a character via [`AC09.Process`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC09.cs), [`EquipManager.SetBeginnerOutfit()`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Equip.cs) evaluates the character's `Body` (`BodyStyle`) and `Head` (`HairStyle` enum).

### Big Male Matrix (`HairStyle_BigM`)
* `Head = 0 (Daniel)`:
  - Body (Slot 2): Item `21004` (Dust Coat)
  - Shoes (Slot 5): Item `24004` (Gentleman's Shoes)
* `Head = 1 (Sid)`:
  - Body (Slot 2): Item `21005` (Jeans)
  - Shoes (Slot 5): Item `24005` (Leisure Shoes)
* `Head = 2 (More)`:
  - Body (Slot 2): Item `21012`
  - Shoes (Slot 5): Item `24012`
* `Head = 3 (Kurogane)`:
  - Head (Slot 1): Item `22009`
  - Body (Slot 2): Item `21014`
  - Weapon (Slot 3): Item `18002`
  - Shoes (Slot 5): Item `24014`

### Big Female Matrix (`HairStyle_BigF`)
* `Iris`: Items `22005`, `21006`, `23001`, `24006`
* `Lique`: Items `21007`, `23002`, `24007`
* `Maria`: Items `22006`, `21011`, `10004`, `24011`
* `Vanessa`: Items `21008`, `24008`
* `Breillat`: Items `22007`, `21009`, `10002`, `24009`
* `Karin`: Items `21015`, `22008`, `24015`
* `Konnotsuroko`: Items `24013`, `21013`
* `Jessica`: Items `22002`, `21010`, `10003`, `24010`

### Persistence
`WriteNewPlayer` persists equipped items into the `inventory` table with `storID = 1`, referencing slots 1-6.

## 3. Starter Pack Delivery
[`StarterPackManager.DeliverToPlayer(Player p, bool sendData)`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/StarterPackManager.cs) seeds or queries the `starter_items` SQLite table and places the 8 authentic starter items into inventory slots (`storID = 0`, positions 1-8):
1. `34038` - Notepad (Qty: 1)
2. `34058` - Remote Control (Qty: 1)
3. `32176` - Fugu Hot Pot (Qty: 50)
4. `34014` - Tao Rice Ball (Qty: 10)
5. `34026` - Protective EXP Pill (Qty: 5)
6. `34169` - Bamboo Dragonfly (Qty: 1)
7. `34190` - 10X Holy EXP Potion (Qty: 3)
8. `34253` - Training Ticket (Qty: 5)

## 4. Fallback Delivery on Login
In [`WorldServer.CommenceLogin`](file:///D:/GitHub/Wonderland-Private-Server/Src/Server/WorldServer.cs#L459-L464), if a Level 1 character has an empty inventory upon login, `StarterPackManager.HasAnyStarterItem(src)` evaluates to `false` and triggers automatic starter pack delivery, persisting changes back to SQLite.
