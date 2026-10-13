# 53 - Player Stall Vending and Pet Consignment Bazaar Specification

## 1. Architectural Overview & Source Mapping

Wonderland Online features an autonomous player marketplace where characters deploy temporary merchant stalls in designated trade sectors (e.g., Welling Village, Kelan Village, Holy Village). The vending infrastructure is managed by Delphi units `HL_Shop.pas` and `CY_TradeMenu.pas`, supporting both physical inventory merchandise (`Form_BuyItemStall_1`) and companion pet consignment contracts (`Form_BuyPet1Stall_1`, `Form_BuyPet2Stall_1`, `Form_BuyPet3Stall_1`).

### Subroutine Inventory & Source Locations

| Virtual Address | Subroutine Symbol | File / Unit | Purpose |
| :--- | :--- | :--- | :--- |
| `0x0033f2f4` | `FUN_0033f2f4` | `HL_Shop.pas` | First empty stall slot scanner (returns slot $1..5$ or 0) |
| `0x0033f34c` | `FUN_0033f34c` | `HL_Shop.pas` | Existing merchandise inventory slot index lookup |
| `0x0033f7b0` | `FUN_0033f7b0` | `HL_Shop.pas` | Remove merchandise from stall display and return to bag |
| `0x0033f83c` | `FUN_0033f83c` | `HL_Shop.pas` | Add item to stall display slot with quantity and price validation |
| `0x0033f960` | `FUN_0033f960` | `HL_Shop.pas` | Stall shutdown and reset all 5 display slot registers |
| `0x0033f9d4` | `FUN_0033f9d4` | `HL_Shop.pas` | Stacking quantity multiplier and nibble pack calculator |
| `0x0033fa00` | `FUN_0033fa00` | `HL_Shop.pas` | Primary stall packet receiver, binary field decoder, and UI binder |
| `0x00321625` | `FUN_00321625` | `HL_Shop.pas` | `Form_BuyItemStall_1` dialog constructor ($525 \times 409$ px) |

---

## 2. Form Geometry & UI Layout

The player stall browser is rendered via `Form_BuyItemStall_1` ($525 \times 409$ pixels).

```
+-------------------------------------------------------------+
| [Stall Name / Signboard Banner]              [Btn_Close_s_1]|
+-------------------------------------------------------------+
|  Merchant: [Owner Character Name]                           |
|  Merchant Gold Capacity: [Max 999,999,999g]                 |
|                                                             |
|  Available Merchandise (3-Column DataGrid):                 |
|  +-------------------------------------------------------+  |
|  | Item Name      | Quantity Available | Unit Price (g)  |  |
|  +-------------------------------------------------------+  |
|  | [Slot 1 Item]  | 50 / 50            | 1,500g          |  |
|  | [Slot 2 Item]  | 12 / 50            | 45,000g         |  |
|  | [Slot 3 Item]  | 01 / 01            | 2,000,000g      |  |
|  | [Slot 4 Item]  | --                 | --              |  |
|  | [Slot 5 Item]  | --                 | --              |  |
|  +-------------------------------------------------------+  |
|  Selected: [ItemStallSelectedItem 1..5]                     |
|  Purchase Quantity: [ 1 ]  Total Cost: [ 1,500g ]           |
|                                                             |
|         [Btn_Buy_1 (Purchase)]         [Btn_Close_1 (Exit)] |
+-------------------------------------------------------------+
```

### Form Specifications

- **Form Name:** `Form_BuyItemStall_1`
- **Dimensions:** Width = `0x20d` (525 pixels), Height = `0x199` (409 pixels)
- **Controls & Data Grids:**
  - `param_1 + 0x3b`: 3-Column DataGrid table (`piVar3[0x51] = 3`).
  - `param_1 + 0x3c + (1..5)*4`: 5 selected item slot buttons (`"ItemStallSelectedItem"`).
  - `Btn_Buy_1`: Purchase confirmation button at $(X=136, Y=20, W=56, H=489)$.
  - `Btn_Close_1`: Cancel / Exit button at $(X=214, Y=20, W=56, H=489)$.
  - `Btn_Close_s_1`: Small close button at $(X=358, Y=18, W=18, H=25)$.

---

## 3. Stall Memory Layout & 39-Byte Item Record Stride

Merchandise in the stall buffer is stored in a contiguous array with an exact stride of **39 bytes (`0x27`)** per entry (`FUN_0033f83c: (param_4 & 0xff) * 0x27`):

### Stall Item Binary Structure (`0x27` Bytes)

| Offset | Type | Field Name | Description |
| :---: | :---: | :--- | :--- |
| `+0x00` | `uint16` | `ItemID` | 16-bit item identifier (`Item.dat` database index) |
| `+0x02` | `uint8` | `CurrentQuantity` | Available item count currently in stall (Max 50) |
| `+0x03` | `uint8` | `MaxQuantity` | Initial quantity stocked at stall opening |
| `+0x04` | `uint32` | `UnitPrice` | Gold cost per single item unit |
| `+0x08` | `uint16` | `Durability` | Current equipment wear / durability value |
| `+0x0A` | `uint16` | `MaxDurability` | Maximum item durability limit |
| `+0x0C` | `uint8` | `ForgeLevel` | Enhancement forge level ($+0..+12$) |
| `+0x0D` | `uint8` | `PotentialValue`| Diamond / potential stat boost grade |
| `+0x0E` | `uint16` | `SocketJewelID` | Inset gem / crystalline enhancement ID |
| `+0x10` | `uint32` | `ReservedStat` | Specialized attribute modifiers / color tints |
| `+0x14` | `uint16` | `DamageCounter` | Damage wear accumulation tick |
| `+0x16` | `uint16` | `SkillBonusID` | Special skill unlocked by equipment |
| `+0x18` | `uint8` | `AmityBonus` | Pet amity modifier when applicable |
| `+0x19` | `uint8[14]`| `CustomMetadata`| Signature / creator name or trade flags |

---

## 4. Stacking Limits, Currency Limits, and Mutex Rules

1. **Stack Limit:**
   - Standard consumable merchandise is clamped at **50 units (`0x32`)** per slot (`FUN_0033f83c`: `bVar7 = 0x32 - CurrentQuantity`).
2. **5-Slot Ceiling:**
   - A single stall can host at most 5 unique item stacks simultaneously (`FUN_0033f2f4`: slots $1..5$).
3. **Purse Overflow Protection:**
   - If a purchase would cause the stall owner's purse to exceed **999,999,999 gold**, the transaction fails with `"Merchant gold limit reached"`.
4. **State Mutex Conflicts:**
   - Opening a stall sets `*(actor + 0x2231) = 1` and `*(actor + 0x2232) = 1`.
   - While vending is active, the character is strictly prohibited from:
     - Moving (`"Vending, unable to move"`)
     - Initiating trade (`"In stall, can't trade"`)
     - Entering combat (`"Currently vending stall"`)
     - Boarding vehicles or pets (`"Vending, can't ride"`)

---

## 5. Pet Consignment Bazaar (`Form_BuyPet1Stall_1`)

Wonderland Online allows players to consign recruited companions for sale.

- **Pet Slots:** Companion vending uses forms `Form_BuyPet1Stall_1` through `Form_BuyPet3Stall_1` (up to 3 pets per stall).
- **Amity Constraint:** A pet can only be placed on a stall if its amity rating is strictly **$\ge 20$** and it does not possess quest lock flags (`QuestLocked == 0`).
- **Transfer of Contract:** Upon successful transaction, the companion's recruitment record is severed from the seller and transferred to the buyer's active party register.

---

## 6. Wire Protocol & Network Framing

Stall transactions are routed via **Action Code 20 (`0x14`)**.

### Player Stall Protocol Matrix

| Action Code | Sub-Opcode | Direction | Description | Payload Schema |
| :---: | :---: | :---: | :--- | :--- |
| `0x14` | `0x0A` | C $\to$ S | Open Stall | `[Signboard Name Len] [Signboard String]` |
| `0x14` | `0x0B` | C $\to$ S | Add Item to Stall | `[Bag Index (1B)] [Slot (1B)] [Qty (1B)] [Price (4B)]` |
| `0x14` | `0x0C` | C $\to$ S | Remove Item from Stall | `[Stall Slot (1B)]` |
| `0x14` | `0x0D` | C $\to$ S | Close Stall | `None` |
| `0x14` | `0x0E` | C $\to$ S | Browse Player Stall | `[Target Merchant CharID (4B)]` |
| `0x14` | `0x0F` | S $\to$ C | Stall Inventory Data | `[Merchant ID (4B)] [Slot Count] { 39-Byte Item Records }` |
| `0x14` | `0x11` | C $\to$ S | Purchase Item | `[Merchant ID (4B)] [Stall Slot (1B)] [Qty (1B)]` |
| `0x14` | `0x12` | S $\to$ C | Purchase Confirmation | `[Success Flag (1B)] [Slot (1B)] [Remaining Qty (1B)]` |

---

## 7. Private Server Implementation Requirements

1. **Atomic Transaction Lock:** The server must execute purchases inside an ACID-compliant transaction lock to prevent duplicate spending or item cloning exploits when two buyers purchase the same slot simultaneously.
2. **Stall Signboard Broadcast:** Broadcast the vendor sign text (`0x14:0x0A`) to all surrounding players within render range ($< 271$ px spatial grid).
3. **Inventory & Purse Validation:** Ensure the buyer has sufficient gold and empty bag slots, and credit the seller's account even if the seller is AFK.
