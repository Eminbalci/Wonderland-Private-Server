# NPC Vendor Procurement and Catalog Protocol Specification

## Overview

This specification details the architecture, packet structures, and inventory validation rules for NPC merchant interactions in Wonderland Online, specifically resolving the inability to purchase items from vendors while liquidating inventory.

---

## 1. Architectural Separation: Purchasing vs. Liquidation

### 1.1 Vendor UI Forms in `HL_Shop.pas`

The client manages NPC vendor transactions via four distinct forms:

| Form Name | Form ID | Purpose | Network Opcode |
|---|---|---|---|
| `Form_EquipStore_1` | 30 | Equipment purchasing catalog (weapons, armor) | `AC 35:12` (Open), `AC 27:1` (Order) |
| `Form_ThingStore_1` | 31 | General goods purchasing catalog (potions, scrolls) | `AC 35:12` (Open), `AC 27:1` (Order) |
| `Form_SaleEquip_1` | 32 | Equipment liquidation staging panel (selling) | `AC 27:5` (Open), `AC 27:2` (Liquidate) |
| `Form_SaleThing_1` | 33 | General items liquidation staging panel (selling) | `AC 27:4` (Open), `AC 27:2` (Liquidate) |

### 1.2 Defect Analysis

In previous versions:
1. When interacting with shopkeeper NPCs (e.g. Weapon Merchant, Grocery Merchant), the server dialogue handler in `QuestNpc.cs` routed both Option 1 (Buy) and Option 2 (Sell) to `src.OpenNpcSale()`. This opened the liquidation window (`Form_SaleThing_1`) instead of the store catalog (`Form_ThingStore_1`).
2. If the client did attempt to dispatch purchase requests (`AC 27:1`), `AC27.cs` parsed 3-byte packets via `data.Length % 3 == 0` before checking the count prefix. For single-item purchases, the authentic client packet `[0x01, 0x01, 0x01]` (count 1, slot 1, ammt 1) was parsed as `BitConverter.ToUInt16([0x01, 0x01]) = 257`, producing a fictitious item ID.
3. `NpcBuyResult` defined `ItemUnavailable = 3`. In the official client (`aLogin.exe`), result code `3` maps to the localized popup "Sorry, your inventory is full / Not enough space to fit in inventory". Because item `257` was not found in `itemDat.wpdat`, the server returned `3`, causing the client to pop up the false-positive "inventory is full" dialog.
4. The client transmits 1-based catalog slot indices (`slot = 1`, `slot = 2`), not raw item IDs. The server had no mapping between catalog slot indices and authentic item IDs.

---

## 2. Procurement Implementation

### 2.1 Catalog Resolution (`QuestNpc.cs`, `EveEventInterpreter.cs`, & `Inventory.ResolveCatalogItem`)

When players select Option 1 ("Buy"), the server sends `AC 35:12` containing the authentic shop catalog resource ID:
- Weapon Store: `0x0001FB84`
- Props / General Goods Store: `0x0001FB85`
- Armor Store: `0x0001FB83`

The player's active session records `src.CurrentShopCatalogId = shopCatalogId`.

In `Inventory.ResolveCatalogItem`:
1. If `slotOrItemId >= 1000`, the identifier is already an item ID (direct purchases or tests).
2. If `slotOrItemId < 1000`, the index is resolved against the current map's `InteractiveInfo` in `EveManager` (`unknownbyte1 == 2` for Weapon/Equipment, `unknownbyte1 == 3` for Props/Items). Each subentry's item ID is decoded as `sub.unknownbyte2 | (sub.unknownbyte3 << 8)`.
3. If `InteractiveInfo` is absent on the active map, canonical fallback catalog tables are used:
   - Weapon Store (`0x0001FB84`): `[17064, 14032, 21061, 18032, 15032, 24067]`
   - Props Store (`0x0001FB85`): `[32011, 32002, 45001, 45007, 42008, 45007]`
   - Armor Store (`0x0001FB83`): `[21061, 24067, 21001, 24001]`

### 2.2 Wire Protocol Framing (`AC 27:1` Buy Request)

The client transmits cart orders via `AC 27:1`. Deserialization in `AC27.cs` evaluates formats in strict priority order to prevent collisions:
1. **Count-prefixed 2-byte pairs (Official Client Format):** `[count: byte], [[slot: byte], [ammt: byte]] * count` when `(data.Length - 1) % 2 == 0 && data[0] == (data.Length - 1) / 2`.
2. **Count-prefixed 3-byte records:** `[count: byte], [[itemId: ushort], [ammt: byte]] * count` when `(data.Length - 1) % 3 == 0 && data[0] == (data.Length - 1) / 3`.
3. **Unprefixed 3-byte records:** `[[itemId: ushort], [ammt: byte]] * N` when `data.Length % 3 == 0`.
4. **Unprefixed 2-byte records:** `[[slot: byte], [ammt: byte]] * N` when `data.Length % 2 == 0`.

### 2.3 Server Validation & Processing (`Inventory.BuyFromNpc`)

The purchase transaction is processed atomically within `Inventory.cs`:
1. **Sanity & State Guards:** Validates that `cartItems` is non-null, length $\le 10$, player is on a valid map, and not in turn-based combat.
2. **Catalog Slot Resolution:** Resolves each `entry.slot` to an authentic item ID via `ResolveCatalogItem()`.
3. **Item & Price Lookup:** Resolves item definition via `PhxItemDat`. Authentic unit buy cost is resolved from `NpcBuyPrices` (decoded from `Data/Item.dat` at offset `+0x72` / compiled into `Data/npc_buy_prices.csv`). Offset `+0x76` stores liquidation/sell-back price. Deducts the exact gold cost displayed in the client shop interface (e.g. Bread = 10 Gold, Rice Wine = 50 Gold, Ordinary Wood = 30 Gold).
4. **Gold Verification:** Computes $\text{TotalCost} = \sum (\text{UnitPrice} \times \text{Ammt})$. Rejects with `NpcBuyResult.NotEnoughGold` (`2`) if player gold is insufficient.
5. **Capacity Verification:** Uses `CanAddItems(requestedItems)` to ensure bag space exists for all requested stacks. Rejects with `NpcBuyResult.InventoryFull` (`3`) only when bag actually cannot fit items.
6. **Execution:** Deducts gold via `Eqs.TakeGold()`, adds items via `AddItem()`, synchronizes HUD gold via `Eqs.SendGold()`, persists state, and returns `NpcBuyResult.Success` (`0`).

### 2.4 Dual Pricing Architecture (`Data/Item.dat` & `npc_buy_prices.csv`)

In Wonderland Online, `Item.dat` stores 451-byte records (`0x1c3` stride) with separate fields for procurement and liquidation:
- **`+0x10` (16 bytes):** 16-bit Item ID, decoded as `(raw ^ 0xEFC3) - 9`.
- **`+0x72` (114 bytes):** 32-bit NPC Vendor Buy Price, decoded as `(raw ^ 0x0B80F4B4) - 9`. Read by client subroutines `FUN_00321984` and `FUN_00382c10` to display store catalog prices and calculate shopping cart totals.
- **`+0x76` (118 bytes):** 32-bit NPC Vendor Liquidation/Sell Price, decoded as `(raw ^ 0x0B80F4B4) - 9`. Read by client subroutine `FUN_00386ec0` when players liquidate items to merchants (typically 5%–10% of base item value).

`Inventory.cs` maintains `NpcBuyPrices` loaded from embedded resource `NpcBuyPrices` (`Data/npc_buy_prices.csv`), with multi-tier fallbacks to local CSV paths and dynamic decoding from `Data/Item.dat`. This ensures purchases deduct the exact gold amount shown on the client shop interface.

### 2.5 Server Confirmation Response

```
Server -> Client: AC 27:1 (and AC 27:2)
Byte 0..3: Header (0xF4, 0x44, 0x03, 0x00)
Byte 4:    0x1B (ActionCode 27: NPC Shop)
Byte 5:    0x01 (SubCode 1: Purchase Result ACK) / 0x02 (Client SubCode 2 Handler)
Byte 6:    Result Code:
           0 = Success
           1 = Rejected / Generic Error / ItemUnavailable
           2 = NotEnoughGold
           3 = InventoryFull
```

---

## 3. Liquidation Implementation (Selling Items & Equipment to NPC Vendors)

### 3.1 Window Invocation & Opcode Dispatch (`Player.OpenNpcSale`)

When players choose the liquidation / selling option when interacting with an NPC merchant, the server sends an opcode to trigger the authentic Delphi modal UI in `HL_Shop.pas`:
- **Equipment Sale (`Form_SaleEquip_1`, Mode 0):** `AC 27:4`
  - In `aLogin.exe` at VA `0x002FE177` (Index 4 of the SubCode jump table at `0x002FDFF4`), the client invokes `FUN_00386b04(Form_Sale, 0)`, setting form mode to Equipment (`[Form_Sale + 0x1cc] = 0`) and opening `Form_SaleEquip_1.ShowModal()`.
- **General Goods / Commodities Sale (`Form_SaleThing_1`, Mode 1):** `AC 27:3`
  - In `aLogin.exe` at VA `0x002FE158` (Index 3 of the SubCode jump table at `0x002FDFF4`), the client invokes `FUN_00386b04(Form_Sale, 1)`, setting form mode to Commodities (`[Form_Sale + 0x1cc] = 1`) and opening `Form_SaleThing_1.ShowModal()`.

### 3.2 Wire Protocol Framing (`AC 27:2` Liquidation Order)

When the player selects one or more items and clicks "OK" (`Btn_Ok_1` at VA `0x00386788`):
1. The client iterates through selected staging slots (`1..50`).
2. Appends each 1-based inventory slot byte into the packet payload.
3. Appends the active sale mode byte at the very end (`0` = Equipment, `1` = Things).
4. Dispatches `SendPacket(ActionCode = 27, SubCode = 2, Buffer = [slot_1, ..., slot_N, sale_mode])`.

### 3.3 Server Liquidation Validation (`Inventory.SellToNpc`)

1. **Session & Map Validation:** Verifies the player has an active sale or shop session on the current map (`(owner.NpcSaleMode != null || owner.CurrentShopCatalogId != 0 || (owner.LastNpcClick != 0 && owner.LastNpcMap == owner.CurMap.MapID)) && (owner.NpcSaleMap == 0 || owner.NpcSaleMap == owner.CurMap.MapID)`), and is not in turn-based combat.
2. **Item & Category Integrity:** For each selected slot, validates that:
   - The slot contains a valid item with `Ammt > 0` and is not locked (`!item.isLocked`) or slotted into a container (`item.Parent == 0`).
   - Category matches the active mode: `(mode == 0) == (item.Data != null && item.Data.Equippos >= 1 && item.Data.Equippos <= 6)`.
   - The item has an entry in `NpcSalePrices` (or `Item.dat` fallback) and does not have the unsellable flag (`(price[1] & 16) == 0`).
3. **Gold Ceiling Check:** Computes total payout $\sum (\text{LiquidationPrice} \times \text{Ammt})$. Rejects with `NpcSaleResult.GoldLimit` (`1`) if total gold exceeds `999,999`.
4. **Execution & State Synchronization:**
   - Adds gold to character via `Eqs.AddGold()`.
   - Clears each liquidated stack via `RemoveItem(slot, Ammt)` (sending `AC 23:9` slot removal notifications).
   - Synchronizes HUD gold with `Eqs.SendGold()`.
   - Updates `owner.NpcSaleMode = mode` to track active tab transitions.
   - Persists character state and returns `NpcSaleResult.Success` (`0`).

### 3.4 Server Liquidation Confirmation Response

```
Server -> Client: AC 27:2 (Official Liquidation ACK at VA 0x002FE0B6)
Byte 0..3: Header (0xF4, 0x44, 0x03, 0x00)
Byte 4:    0x1B (ActionCode 27: NPC Shop)
Byte 5:    0x02 (SubCode 2: Liquidation Result Handler at VA 0x002FE0B6)
Byte 6:    Result Code:
           0 = Success (closes liquidation modal without error)
           1 = GoldLimit ("Your Gold is at maximum!")
           2 = ItemUnavailable ("No such item in your inventory!")
           3 = TooManyItems ("Too many items to sell!")
           4 = Rejected ("Trade failed")
```
*(The server also dispatches `AC 27:2, [result]` for compatibility with any client scripts checking SubCode 2).*

### 3.5 Native `Eve.emg` Dialogue Branching & Liquidation Interception

In official Wonderland Online `Eve.emg` event scripts, NPC merchants across maps (e.g. Map 12000 Event 31 for Props Shop and Event 34 for Weapon Shop) use two-tier question dialogs with no native bytecode branches for second-tier choices:

1. **Initial Dialogue Prompt (Question 5 for Props, Question 9 for Weapons, Question 1 on rural maps):**
   - **Choice 1 (`30` / `0x1E`):** Navigates to the secondary merchant prompt (Opcode 2 with Question 6 for Props, Question 8 for Weapons).
   - **Choice 2 (`31` / `0x1F` - "Sell goods"):** Executes Opcode 7 (`d1=2` for Props, `d1=1` for Weapons).
   - **Sell Choice Redirection in Opcode 7:** If Opcode 7 is triggered from branch `w2=31`, the server bypasses the Buy Catalog (`AC 35:12`) and invokes `player.OpenNpcSale(mode)` (`AC 27:3` for Props Shop Mode 1; `AC 27:4` for Weapon Shop Mode 0).
2. **Merchant Option Prompt (Question 6 for Props, Question 8 for Weapons, Question 2 on general stores):**
   - `Eve.emg` stores zero bytecode branches for `question == 6 || question == 8`.
   - `EveEventRuntime.TryHandleMerchantChoice` intercepts unbranched merchant choices when `selected == null`:
     - **Choice 1 (`30` / `0x1E` - "Buy goods"):** Dispatches `finish()` followed by `AC 35:12` with the appropriate shop catalog (`0x0001FB85` for Props, `0x0001FB84` for Weapons, `0x0001FB83` for Armor).
     - **Choice 2 (`31` / `0x1F` - "Sell goods"):** Dispatches `finish()` followed by `player.OpenNpcSale(mode)` opening `Form_SaleThing_1` (`AC 27:3`) or `Form_SaleEquip_1` (`AC 27:4`).
3. **Modal UI Execution Lifecycle & Dialogue Clearance:**
   - First, `finish()` invokes `player.CancelInteraction()` which dispatches `AC 20:8` (Close dialogue), `AC 5:4` (Restore scene HUD), and `AC 6:2 [0]` (Unlock movement).
   - Second, `player.OpenNpcSale(mode)` dispatches `AC 27:3` (`Form_SaleThing_1`) or `AC 27:4` (`Form_SaleEquip_1`) as the terminal packet.
   - This prevents dialogue lock collisions (`[Form_Select]` lingering over `[Form_Sale]`), allowing Delphi's `ShowModal()` to display `Form_SaleThing_1` or `Form_SaleEquip_1` instantly without obstruction.

---

## 4. Post-Shop Interaction Unlock & Client Lock Architecture (`[GameState + 0x7108]`)

### 4.1 Client Interaction Gates in Delphi Disassembly (`aLogin.exe`)

Analysis of `aLogin.exe` reveals the exact mechanics behind post-shopping exploration and portal blockages:

1. **Client Lock Flag `[GameState + 0x7108]`:**
   - The Delphi client allocates a central game state structure at `*PTR_DAT_004c9588`.
   - Byte offset `+0x7108` acts as an interaction lock gate.
2. **NPC Click Suppression (VA `0x0031DC17`):**
   ```x86asm
   cmp byte ptr [ebx + 0x7108], 1
   je  0x0031DDFC   ; Drops and ignores all incoming NPC click events immediately!
   ```
   If `0x7108 == 1`, the client silently discards all user clicks on NPCs, preventing `AC 20:1` from ever transmitting to the server.
3. **Map Portal Transition Block (VA `0x00321218`):**
   ```x86asm
   cmp byte ptr [ebx + 0x7108], 1
   jne 0x00321220
   ret              ; Aborts portal entry step evaluation!
   ```
   If `0x7108 == 1`, stepping onto map entry points / portals does not trigger warp events or send `AC 20:8`.
4. **Player Movement Exemption:**
   - Player movement (`AC 06:1`) is NOT gated by `0x7108`. Consequently, a locked player can walk freely around the map, but cannot enter portals or talk to NPCs.

### 4.2 Shop Modal Close Handshake & `AC 20:6` -> `AC 20:8` Requirement

1. **Window Closure:**
   - When the user closes `Form_Sale` (`Form_Sale.DoClose` at VA `0x00386764`) or `Form_ThingStore` (`0x00382B20`), the client sets `[GameState + 0x7108] = 1`.
   - The main frame update loop at VA `0x0032138F` / `0x00309733` checks `0x7108 == 1` and transmits `AC 20:6` to the server:
     ```c
     if (*(char *)(param_1 + 0x7108) == 1 && *(char *)(param_1 + 0x7109) == 0) {
         FUN_002d6ad8(..., 0x14, 6, 0); // Send AC 20:6
     }
     ```
2. **Reset via Server `AC 20:8` (VA `0x002F8425`):**
   - The client handles `AC 20:8` by explicitly clearing the lock flags:
     ```c
     *(undefined1 *)(param_1 + 0x7108) = 0;
     *(undefined1 *)(param_1 + 0x710b) = 0;
     *(undefined1 *)(param_1 + 0x70e8) = 0;
     ```
   - Crucially, `AC 20:8` targets dialogue forms (`Form_SelectMsg` at `0x004C9590`); it does NOT destroy `Form_Sale` (`0x004C86EC`) or `Form_ThingStore` (`0x004C88A8`).
3. **Defect Resolution in `AC20.Recv6`:**
   - Previous versions suppressed `AC 20:8` when `p.NpcSaleMode != null || p.CurrentShopCatalogId != 0`, believing it closed the shop. Because of this suppression, `[GameState + 0x7108]` remained `1` indefinitely after the user closed the vendor window.
   - The server now responds to `AC 20:6` by clearing `p.NpcSaleMode = null` and `p.CurrentShopCatalogId = 0`, followed by dispatching:
     - `AC 6:2 [0]` (Posture unlock)
     - `AC 20:8` (Interaction lock clearance `0x7108 -> 0`)
     - `AC 5:4` (HUD and scene gate restore)

### 4.3 Movement Cleanup Safeguard (`AC 06:1`)

In `AC06.cs` (`Recv1`), if the player moves (`movedDistance > 0`) while `p.NpcSaleMode != null || p.CurrentShopCatalogId != 0`:
- The server recognizes that the player has physically walked away from the vendor.
- Atomically clears `p.NpcSaleMode = null` and `p.CurrentShopCatalogId = 0`.
- Immediately dispatches `AC 6:2 [0]`, `AC 20:8`, and `AC 5:4` to ensure client `0x7108` is reset even if the client skipped sending `AC 20:6`.

### 4.4 Automated Regression Verification

The fix is validated in `.codex-verify/Test-GameplayDefectFixes.ps1` under `Test 7`:
- **Case A (Shop Modal Close via `AC 20:6`):** Verifies that active catalog (`0x0001FB85`) and liquidation mode (`1`) are cleared to `0` / `null`, and `AC 6:2 [0]`, `AC 20:8`, and `AC 5:4` are dispatched in sequence.
- **Case B (Walking Away via `AC 06:1`):** Verifies that movement while holding active shop state resets vendor variables and emits `AC 20:8` + `AC 5:4`.

