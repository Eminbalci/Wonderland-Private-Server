# Trade, Stall & Alchemy Decompiled Specifications

This document outlines the client-side secure trade registers, stall boundaries, item compound validations, and hammadde (material) classes extracted from `alogin_analyzed/trade_shop.c`.

---

## 1. Memory Offsets & Trade Form Structures

During trade initialization and rendering, the client maps item structural data using the following offsets:

| Offset | Data Type | Field / Purpose | Description & Actions |
|---|---|---|---|
| `+0x1d0` | `pointer[6]` | My Trade Slots Array | Stores base pointers for the 6 items placed by the player in trade |
| `+0x1c0` | `pointer[6]` | Target Trade Slots Array | Stores base pointers for the 6 items placed by the trading partner |
| `+0xdc` | `int` | Item ID | Located inside the item structure. Unique template identifier |
| `+0xe0` | `int` | Item Count | Located inside the item structure. Quantity of the item |
| `+0xe4` | `short` | Durability | Located inside the item structure. Current item health |
| `+0xfa` | `short` | Amity Boost | Items providing amity bonuses when fed to pets |
| `+0xfe` | `short` | Skill ID Binds | Skills bound to specific weapons/armors |
| `+0x1ea` | `byte` | Level Restriction | Character level limit required to use or trade |
| `+0x1f9c` | `byte` | Character Element Type | Used to evaluate element-bound equipments |
| `+0x295c` | `pointer` | Bank Storage Array | Base address array for Bank Items |

---

## 2. Key Decompiled Functions

### `FUN_002a1f14` (Secure Trade Slots Renderer)
- **C Signature**: `void FUN_002a1f14(int *param_1, int param_2)`
- **Logic**:
  1. Validates trade slot state: checks if item is restricted using `FUN_002a6ba8` (`<Non-tradeable>`). If matched, blocks trade insertion.
  2. Resolves specific item forms (checks if item is registered as `TRe_CompoundForm`, `CompoundItem_Main`, or `CompoundItem_Sub`).
  3. Verifies attributes: retrieves Amity values (`+0xfa`), MaxHP (`+0xcf`), and MaxSP (`+0xd0`) to render tooltip overlay.
  4. Resolves level requirements: if `local_1ea != 0`, checks player level. If lower, prints `"LV Req: %d"` or `"Below %d"`.
  5. Binds container items (`CupBoardItem`, `Express`, `BankItem`, `ParkCase`, `FixTrafficSpace`, `TradeLeftItem`, `OtherSafeTradeItem`, `MySafeTradeItem`, `VenderItemImage`).

---

## 3. Eşya Hammadde Sınıfları (Material Types)

The client's internal parser `FUN_0049f5e8` resolves item material classifications for Alchemy/Compound recipes using the following ID mappings:

| ID (Hex) | Material Name | Turkish Translation | Usage Context |
|---|---|---|---|
| `0x01` | `Flower` | Çiçek | Alchemy recipes, gathering drops |
| `0x02` | `Grass` | Çimen / Ot | Consumables compounding |
| `0x07` | `Veggie` | Sebze | Cooking, health items |
| `0x08` | `Fruit` | Meyve | Cooking, juice compounding |
| `0x0d` | `Seafood` | Deniz Ürünü | Fish compounding |
| `0x1e` | `Diamond` | Elmas | High-tier weapon alchemy |
| `0x1f` | `Crystal` | Kristal | High-tier armor compounding |
| `0x20` | `Mercury` | Cıva | Catalyst for metallic alchemy |
| `0x21` | `Silver` | Gümüş | Medium-tier metal metallurgy |
| `0x22` | `Stone` | Taş | Construction crafting, low alchemy |
| `0x23` | `Magnet` | Mıknatıs | Electronic/engineering crafting |
| `0x2b` | `Red Clay` | Kırmızı Kil | Pottery, stove furniture |
| `0x2d` | `Black Clay` | Siyah Kil | Pottery, advanced furnace |
| `0x2e` | `White Clay` | Beyaz Kil | Pottery, porcelain |
| `0x2f` | `Grey Clay` | Gri Kil | Pottery, standard tiles |
| `0x30` | `Dry Clay` | Kuru Kil | Basic brick masonry |
| `0x32` | `Feather` | Tüy | Clothing crafting |
| `0x37` | `Feces` | Gübre / Dışkı | Farm fertilization |
| `0x38` | `Secrets` | Gizli Hammaddeler | Unique quest items compounding |
| `0x39` | `Alcohol` | Alkol | Cooking, buff items |
| `0x3a` | `Nylon` | Naylon | Synthetic textiles |
| `0x3b` | `Crude` | Ham Petrol | Chemical manufacturing, fuel |
| `0x3c` | `Ref Oil` | Rafine Petrol | High-efficiency fuel, plastic |

---

## 4. Network Protocol Packet Format

Secure trade transactions use Opcode **`20`** (Dialogue & Interaction).

### Trade Action Packets (Client -> Server)
- **Opcode**: `20` (Dialogue & Interaction)
- **Sub-opcodes**:
  - `10` (Open Trade Dialog): Payload = Partner ID (4 bytes).
  - `11` (Insert Item): Payload = Inventory Slot (1 byte) + Trade Slot (1 byte) + Count (2 bytes).
  - `12` (Remove Item): Payload = Trade Slot (1 byte).
  - `13` (Lock Deal): Payload = Lock Flag (1 byte).
  - `14` (Confirm Deal): Payload = Confirm Flag (1 byte).
  - `15` (Cancel Deal): Payload = Cancel Flag (1 byte).
