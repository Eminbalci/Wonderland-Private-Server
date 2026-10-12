# Trade, Shop, Inventory, and Alchemy Materials Specification

## 1. Trade & Item Memory Layout

During player trading and inventory management, the client maps structures using the following offsets:

| Offset | Type | Field Name | Description & Functional Meaning |
|---|---|---|---|
| `+0x1d0` | `pointer[6]` | `MyTradeSlots` | Array of 6 pointers for items placed in trade by the local player |
| `+0x1c0` | `pointer[6]` | `TargetTradeSlots` | Array of 6 pointers for items placed in trade by the trading partner |
| `+0xdc` | `int` | `ItemID` | Located inside item struct: Template ID |
| `+0xe0` | `int` | `ItemCount` | Located inside item struct: Stack quantity |
| `+0xe4` | `short` | `Durability` | Located inside item struct: Current equipment durability (0-100) |
| `+0xfa` | `short` | `AmityBoost` | Located inside item struct: Companion amity gain value |
| `+0xfe` | `short` | `BoundSkillID` | Located inside item struct: Skill ID bound to weapon/armor |
| `+0x1ea` | `byte` | `LevelRequirement` | Located inside item struct: Level required to equip |
| `+0x1f9c` | `byte` | `ElementType` | Located inside item struct: Elemental affinity (Earth, Water, Fire, Wind) |
| `+0x295c` | `pointer` | `BankStorageArray` | Base memory array for bank vault item storage |

---

## 2. Key Decompiled Subroutines

### 2.1 Trade Slot Validator & Tooltip Builder (`FUN_002a1f14`)
* **Signature:** `void FUN_002a1f14(int *param_1, int param_2)`
* **Logic:**
  1. Inspects item tradeability flag via `FUN_002a6ba8` (`<Non-tradeable>`). If matched, blocks trade insertion.
  2. Binds container items (`CupBoardItem`, `Express`, `BankItem`, `TradeLeftItem`, `OtherSafeTradeItem`, `MySafeTradeItem`, `VenderItemImage`).
  3. Verifies player level against requirement `+0x1ea`. If lower, renders warning label `"LV Req: %d"` or `"Below %d"`.

---

## 3. Network Protocol Packet Format (`Opcode 20` / `AC 20`)

Player trading handshakes are managed under Opcode **`20`**:

| Sub-Opcode | Action Name | Direction | Payload Structure |
|---|---|---|---|
| `20:10` | Open Trade Request | Client $\to$ Server | `PartnerID: UInt32` |
| `20:11` | Insert Item into Trade | Client $\to$ Server | `InvSlot: Byte`, `TradeSlot: Byte`, `Count: UInt16` |
| `20:12` | Remove Item from Trade | Client $\to$ Server | `TradeSlot: Byte` |
| `20:13` | Lock Trade | Client $\to$ Server | `LockFlag: Byte` |
| `20:14` | Confirm Trade | Client $\to$ Server | `ConfirmFlag: Byte` |
| `20:15` | Cancel / Abort Trade | Client $\to$ Server | `CancelFlag: Byte` |
