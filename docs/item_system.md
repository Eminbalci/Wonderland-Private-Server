# Item Usage System Specification (AC 23:96)

## 1. Protocol Handler
- **Client Request:** `AC 23, Sub 96 [Slot (1B)]`
- Triggered when the player double-clicks or selects "Use" on an inventory item.

## 2. Supported Item Types
1. **Tent (ID 36002):** Unpacks and opens the player's personal tent on map.
2. **Equipable Items:** Equips the item to the designated gear slot (`p.WearEQ(slot)`).
3. **Star Currency (ID 30025):**
   - Star is an authentic quest token currency used in skill learning, stat reset quests, and rebirth events.
   - It is kept safe in the inventory and not consumed for IM points.
4. **Food / Potions / Healing Items:**
   - Reads HP/SP recovery values from `ItemDat` (`StatusType 207/208`) or category fallbacks.
   - Refreshes player HP/SP stats via `Send8_1` and gives feedback message `AC 23:57`.
5. **Pet Cards / Vouchers:** Consumes voucher item and validates pet summon.
