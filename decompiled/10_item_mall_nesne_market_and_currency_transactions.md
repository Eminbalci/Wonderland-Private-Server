# Item Mall (Nesne Market) and Currency Transactions Specification

## 1. Item Mall Memory Offsets & Form Properties

During Item Mall transactions and point management, the client maps structures using the following offsets:

| Offset | Type | Field Name | Description & Functional Meaning |
|---|---|---|---|
| `+0x134` | `int` | `PointBalance` | Available player Item Mall point balance |
| `+0x138` | `int` | `SelectedItemPrice` | Point price of the currently selected item |
| `+0x127` | `char` | `SelectionFlag` | `1` if an item slot is selected, `0` if empty |
| `+0x52` | `pointer` | `MallTemplatePtr` | Pointer to loaded item catalog database |
| `+0x3c` | `pointer[10]` | `SelectionSlots` | Array of 10 item selection slots in the UI |
| `+0x4a` | `pointer` | `BuyButtonPtr` | Binds click handler for `Btn_Buy_1` |
| `+0x4b` | `pointer` | `CloseButtonPtr` | Binds click handler for `Btn_Close_1` |

---

## 2. Key Decompiled Subroutines

### 2.1 Item Mall Form Loader (`FUN_001aec08`)
* **Signature:** `int* FUN_001aec08(int *param_1, char param_2)`
* **Logic:**
  1. Resolves template database pointers (`+0x52`).
  2. Loads interface layout `"Form_EightTwelve"` / `"NpcStoreSelectedItem"`.
  3. Instantiates the 10 item selection slots (`+0x3c` index array `1..10`).
  4. Binds buttons:
     * `Btn_Buy_1`: Binds purchase validation subroutine `FUN_001af620`.
     * `Btn_Close_1` / `Btn_Close_S_1`: Binds window exit routines.

### 2.2 Point Validator & Purchase Handler (`FUN_001af620`)
* **Signature:** `void FUN_001af620(undefined1 *param_1)`
* **Logic:**
  1. Validates available balance against item cost:
     $$\text{PointBalance (+0x134)} < \text{SelectedItemPrice (+0x138)}$$
  2. **If Balance is Insufficient:**
     * Aborts purchase request.
     * Displays system warning: `"Not enough Points"`.
  3. **If Balance is Sufficient:**
     * Confirms selection flag `+0x127 == 1`.
     * Dispatches purchase confirmation packet containing price and quantity to server using `FUN_00366ebc`.

---

## 3. Network Protocol Packet Format (`Opcode 34` / `0x22`)

### Purchase Request Packet (Client $\to$ Server)
* **Opcode:** `34` (`0x22`)
* **Sub-Opcode:** `1`
* **Payload Structure:**
  * `item_mall_id` (4 bytes): Unique catalog identifier.
  * `quantity` (2 bytes): Number of items to purchase.
  * `expected_points` (4 bytes): Expected point cost to prevent client/server balance desynchronization.
