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
| `Form_SaleEquip_1` | 32 | Equipment liquidation staging panel (selling) | `AC 27:3` (Open), `AC 27:2` (Liquidate) |
| `Form_SaleThing_1` | 33 | General items liquidation staging panel (selling) | `AC 27:4` (Open), `AC 27:2` (Liquidate) |

### 1.2 Defect Analysis

In previous versions:
1. When interacting with shopkeeper NPCs (e.g. Weapon Merchant, Grocery Merchant), the server dialogue handler in `QuestNpc.cs` routed both Option 1 (Buy) and Option 2 (Sell) to `src.OpenNpcSale()`. This opened the liquidation window (`Form_SaleThing_1`) instead of the store catalog (`Form_ThingStore_1`).
2. If the client did attempt to dispatch purchase requests (`AC 27:1`), `AC27.cs` lacked a handler for `subCode == 1`, silently dropping customer orders.
3. `Inventory.cs` had a liquidation method `SellToNpc()`, but lacked a server-side procurement method `BuyFromNpc()` to validate gold, check bag capacity, deduct funds, and grant items.

---

## 2. Procurement Implementation

### 2.1 Catalog Resolution (`QuestNpc.cs` & `EveEventInterpreter.cs`)

When players select Option 1 ("Buy"), the server sends `AC 35:12` containing the authentic shop catalog resource ID:
- Weapon Store: `0x0001FB84`
- Props / General Goods Store: `0x0001FB85`
- Armor Store: `0x0001FB83`

The player's active session records `src.CurrentShopCatalogId = shopCatalogId`.

### 2.2 Wire Protocol Framing (`AC 27:1` Buy Request)

The client transmits cart orders via `AC 27:1`. The payload supports both 3-byte records `[ushort ItemID, byte Ammt]` and 2-byte records `[ushort/byte Slot, byte Ammt]`, with optional item count prefix byte:

```
Client -> Server: AC 27:1
Byte 0..3: Header (0xF4, 0x44, LengthL, LengthH)
Byte 4:    0x1B (ActionCode 27: NPC Shop)
Byte 5:    0x01 (SubCode 1: Purchase Items)
Byte 6..N: Cart Records:
           Option A: [count: byte], [[itemId: ushort], [ammt: byte]] * count
           Option B: [[itemId: ushort], [ammt: byte]] * N
           Option C: [[slot: byte], [ammt: byte]] * N
```

### 2.3 Server Validation & Processing (`Inventory.BuyFromNpc`)

The purchase transaction is processed atomically within `Inventory.cs`:
1. **Sanity & State Guards:** Validates that `cartItems` is non-null, length $\le 10$, player is on a valid map, and not in turn-based combat.
2. **Item & Price Lookup:** Resolves item definition via `PhxItemDat`. Base cost is determined from `NpcSalePrices` (authentic 200% buy/sell ratio: $\text{BuyPrice} = \text{SellPrice} \times 2$).
3. **Gold Verification:** Computes $\text{TotalCost} = \sum (\text{UnitPrice} \times \text{Ammt})$. Rejects with `NpcBuyResult.NotEnoughGold` if player gold is insufficient.
4. **Capacity Verification:** Uses `CanAddItems(requestedItems)` to ensure bag space exists for all requested stacks.
5. **Execution:** Deducts gold via `Eqs.TakeGold()`, adds items via `AddItem()`, synchronizes HUD gold via `Eqs.SendGold()`, persists state, and returns `NpcBuyResult.Success`.

### 2.4 Server Confirmation Response

```
Server -> Client: AC 27:1
Byte 0..3: Header (0xF4, 0x44, 0x03, 0x00)
Byte 4:    0x1B (ActionCode 27: NPC Shop)
Byte 5:    0x01 (SubCode 1: Purchase Result)
Byte 6:    Result Code:
           0 = Success
           1 = Rejected / Generic Error
           2 = NotEnoughGold
           3 = InventoryFull
           4 = ItemUnavailable
           5 = TooManyItems
```
