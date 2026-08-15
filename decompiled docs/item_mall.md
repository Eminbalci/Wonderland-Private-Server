# Item Mall Decompiled Specifications

This document outlines the client-side Item Mall (Nesne Market) interface initialization, point check triggers, and transaction handlers extracted from `alogin_analyzed/item_mall.c`.

---

## 1. Memory Offsets & GUI Form Properties

During Nesne Market transactions and layout rendering, the client maps balance and selection properties using the following offsets:

| Offset | Data Type | Field / Purpose | Description & Actions |
|---|---|---|---|
| `+0x134` | `int` | Current Point Balance | Located inside player mall session. Tracks player's available points |
| `+0x138` | `int` | Selected Item Price | Price of the chosen item in points |
| `+0x127` | `char` | Selection Flag | `1` if an item slot is actively selected, `0` if empty |
| `+0x52` | `pointer` | Mall Template Data | Reference pointer to item data tables |
| `+0x3c` | `pointer[10]` | Selection Slot Array | Pointers for the 10 item selection slots displayed in UI |
| `+0x3a` | `pointer` | Main Shop Panel | Pointer reference to Form_EightTwelve |
| `+0x4a` | `pointer` | Buy Button Reference | Binds the click trigger for `Btn_Buy_1` |
| `+0x4b` | `pointer` | Close Button Reference | Binds the click trigger for `Btn_Close_1` |

---

## 2. Key Decompiled Functions

### `FUN_001aec08` (Item Mall Form Loader)
- **C Signature**: `int* FUN_001aec08(int *param_1, char param_2)`
- **Logic**:
  1. Resolves Item Mall template data offsets (`+0x52`). Registers item parameters like template ID `0x37c1`.
  2. Resolves viewport boundaries and loads the interface overlay window named **`Form_EightTwelve`**.
  3. Instantiates the 10 item selection slots (`+0x3c` index array `1` to `10`) using layout name `"NpcStoreSelectedItem"`.
  4. Binds buttons:
     - **`Btn_Buy_1`**: Binds the click handler task function `FUN_001af620`.
     - **`Btn_Close_1`**: Binds the close window sequence.
     - **`Btn_Close_S_1`**: Binds secondary escape buttons.

### `FUN_001af620` (Buy Button Handler & Point Validator)
- **C Signature**: `void FUN_001af620(undefined1 *param_1)`
- **Logic**:
  1. Compares Current Point Balance (`+0x134`) against the Selected Item Price (`+0x138`):
     $$\text{Points}_{\text{Current}} < \text{Price}_{\text{Item}}$$
  2. **If Balance is Insufficient**:
     - Aborts the buy request.
     - Triggers system alert message: `"Not enough Points"`.
  3. **If Balance is Sufficient**:
     - Verifies selection flag `+0x127` is active.
     - Dispatches purchase confirmation packet containing Selected Item Price value to the server using `FUN_00366ebc`.

---

## 3. Network Protocol Packet Format

Item Mall purchases use Opcode **`34`** (`0x22`).

### Item Mall Purchase Packet (Client -> Server)
- **Opcode**: `34` (Item Mall Action)
- **Sub-opcode**: `1` (Purchase Request)
- **Payload Structure**:
  - `item_mall_id` (4 bytes): Unique ID of item in mall.
  - `quantity` (2 bytes): Number of items to buy.
  - `expected_points` (4 bytes): Expected price subtraction value to prevent desync.
