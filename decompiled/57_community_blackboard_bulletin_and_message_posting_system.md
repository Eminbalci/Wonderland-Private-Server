# 57 - Community Blackboard Bulletin and Message Posting System Specification

## 1. Architectural Overview & Source Mapping

Wonderland Online features public message blackboards located in major town plazas (Welling, Kelan, Holy, Chang'an), allowing players to post community announcements, party recruitment notices, and trade advertisements. The system is implemented in Delphi unit `mWS_Operation.pas` via dialogs `Form_BlackBoard_1` ($328 \times 418$ px) and `Form_BlackBoard_2` ($252 \times 418$ px).

### Subroutine Inventory & Source Locations

| Virtual Address | Subroutine Symbol | File / Unit | Purpose |
| :--- | :--- | :--- | :--- |
| `0x0020c288` | `FUN_0020c288` | `mWS_Operation.pas` | `Form_BlackBoard_1` dialog constructor ($328 \times 418$ px) |
| `0x0020cae0` | `FUN_0020cae0` | `mWS_Operation.pas` | Author permission validation (`"You are not author"`, edit mutex) |
| `0x0020ccd8` | `FUN_0020ccd8` | `mWS_Operation.pas` | Hit-test raycaster for 5 message slots (`PtInRect`, 45 px stride) |
| `0x0020cda8` | `FUN_0020cda8` | `mWS_Operation.pas` | Incoming message board synchronization and slot deserializer |
| `0x0020ba78` | `FUN_0020ba78` | `mWS_Operation.pas` | Message cache reset and local memory scrubber |
| `0x00222667` | `FUN_00222667` | `mWS_Operation.pas` | Post message submission dispatcher (`Action Code 0x40:0x02`) |

---

## 2. Form Geometry & Blackboard Display Layout

The blackboard interface renders up to 5 public announcements per page:

```
+-------------------------------------------------------------+
| [Chalkboard Header] Town Community Notice    [Btn_Close_S_1]|
+-------------------------------------------------------------+
|  Public Announcements & Notices:                            |
|                                                             |
|  +-------------------------------------------------------+  |
|  | [1] Author: PlayerOne   | Date: 2026/10/13 14:30      |  |
|  | Msg: Selling +6 Moon Wand, whisper me in Welling!     |  |
|  +-------------------------------------------------------+  |
|  | [2] Author: LeaderX     | Date: 2026/10/13 15:10      |  |
|  | Msg: Recruiting Level 40+ players for Sky Castle run!|  |
|  +-------------------------------------------------------+  |
|  | [3] Author: Merchant7   | Date: 2026/10/13 16:00      |  |
|  | Msg: Buying all Titanium Ore at 500g each!            |  |
|  +-------------------------------------------------------+  |
|  | [4] (Empty Slot)                                      |  |
|  | [5] (Empty Slot)                                      |  |
|  +-------------------------------------------------------+  |
|  Posting Fee: 500 Gold / 24 Hours                           |
|                                                             |
|  [Btn_OK_2 (Post)]   [Btn_Delete_1 (Delete)]   [Btn_Close_2]|
+-------------------------------------------------------------+
```

### Form Specifications

- **Form Name:** `Form_BlackBoard_1`
- **Dimensions:** Width = `0x148` (328 pixels), Height = `0x1a2` (418 pixels)
- **Controls & Buttons:**
  - `Btn_Close_S_1`: Small close button at $(X=368, Y=18)$.
  - `Btn_Close_2`: Exit button at $(X=187, Y=20, W=43, H=293)$.
  - `Btn_OK_2`: Post new message button at $(X=93, Y=20, W=43, H=293)$.
  - `Btn_Delete_1`: Delete selected message button at $(X=289, Y=20, W=43, H=293)$.
  - `Btn_Cancel_3`: Cancel edit mode button at $(X=141, Y=20, W=43, H=293)$.

---

## 3. Message Record Structure (44-Byte Stride)

Blackboard messages are stored in client memory with an exact stride of **44 bytes (`0x2c`)** per entry (`param_1 + 0x100 + slot * 0x2c`):

| Offset | Type | Field Name | Description |
| :---: | :---: | :--- | :--- |
| `+0x00` | `char[14]`| `AuthorName` | Character name of the message poster (Max 14 chars) |
| `+0x0E` | `uint8` | `Padding` | 1-byte string terminator alignment |
| `+0x0F` | `double` | `PostDateTime`| IEEE-754 64-bit Delphi `TDateTime` timestamp |
| `+0x17` | `char[20]`| `MessageBody` | Public announcement text content (Max 20 chars) |
| `+0x2B` | `uint8` | `StatusFlag` | $0 =$ Free slot, $1 =$ Active post, $2 =$ Pending deletion |

---

## 4. Author Authentication & Editing Mutex (`FUN_0020cae0`)

When a player attempts to delete or edit a post on the blackboard, the client performs strict authorship verification:

```c
// Compare message author name with LocalPlayer name
if (strcmp((char *)(param_1 + 0x100 + slot * 0x2c), LocalPlayer->CharacterName) != 0) {
    ShowAlert("You are not author", 1000);
    return; // Block operation
}
```

### Concurrency Mutex

If another player or transaction is currently modifying the selected message slot (`param_1 + 0x218 != 0`), the client rejects the interaction with:
`"Message being edited"`.

---

## 5. Wire Protocol & Network Framing: Action Code 64 (`0x40`)

Blackboard interactions communicate via **Action Code 64 (`0x40`)**.

### Blackboard Protocol Matrix

| Action Code | Sub-Opcode | Direction | Description | Payload Schema |
| :---: | :---: | :---: | :--- | :--- |
| `0x40` | `0x01` | S $\to$ C | Blackboard Page Data | `[Page (1B)] { [Slot (1B)] [Author Len] [Author] [Date (8B)] [Msg Len] [Msg] }` |
| `0x40` | `0x02` | C $\to$ S | Submit New Message | `[Slot (1B)] [Msg Len (1B)] [Msg String]` |
| `0x40` | `0x03` | C $\to$ S | Delete Message | `[Slot (1B)]` |
| `0x40` | `0x04` | S $\to$ C | Post Result Alert | `[Result Code (1B)] (0=Success, 1=Occupied, 2=Insufficient Gold)` |

---

## 6. Private Server Implementation Requirements

1. **Authorship Enforcement:** Never trust the client alone for deletion; verify that the requesting session's `CharacterName` matches the database record's `author_name`.
2. **Posting Tariff:** Deduct 500 gold from the poster's inventory purse upon receiving `0x40:0x02`. If insufficient gold, return result code `0x02`.
3. **Automated Expiry Purge:** Execute a recurring database prune removing blackboard messages whose `PostDateTime` is older than 24 hours.
