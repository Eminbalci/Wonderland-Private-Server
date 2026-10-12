# Login Authentication, Session Security, and Channels Specification

## 1. Authentication Session Structures

The client manages user login authentication and cluster discovery using the following session registers:

| Offset / Global | Type | Field Name | Description & Functional Meaning |
|---|---|---|---|
| `DAT_0071ef58` | `pointer` | `PlayerSessionPtr` | Master session object containing network state |
| `+0x264` | `int` | `LoginSocketID` | Active socket descriptor for authentication |
| `+0x268` | `int` | `SessionGUID1` | Primary session token received on authentication success |
| `+0x26c` | `int` | `SessionGUID2` | Secondary security verification token |
| `+0x270` | `byte` | `CharacterSlot` | Active character slot index (`0` or `1`) |
| `+0x3c22` | `char` | `LoginState` | `2` = Authenticated, `1` = Connecting, `0` = Disconnected / Failed |

---

## 2. Key Decompiled Subroutines

### 2.1 Login Response Handler (`FUN_0033c310`)
* **Signature:** `void FUN_0033c310(int *param_1, int param_2)`
* **Logic:**
  1. Inspects the status byte from the incoming packet: `cVar1 = *(char *)(param_2 + 1)`.
  2. **If `cVar1 == '\x01'` (Success):**
     * Sets `LoginState (+0x3c22) = 2`.
     * Extracts `SessionGUID1` (offset 3, 4 bytes) $\to$ writes to `+0x268`.
     * Extracts `SessionGUID2` (offset 7, 4 bytes) $\to$ writes to `+0x26c`.
     * Extracts `CharacterSlot` (offset 10, 1 byte) $\to$ writes to `+0x270`.
     * Invokes GUI transition subroutine `FUN_0033d100` to display character select screen.
  3. **If `cVar1 == '\x02'` (Password Error):**
     * Sets `LoginState (+0x3c22) = 0`.
     * Displays modal warning: `"Login/Pwd error"`.
  4. **If `cVar1 == '\x03'` / `'\x04'` (Account Locked / Banned):**
     * Displays security alert and aborts connection.

### 2.2 `SERVER.INI` Parser (`FUN_0032f674`)
* **Signature:** `void FUN_0032f674(undefined4 param_1, int param_2)`
* **Logic:**
  1. Reads `"SERVER.INI"` via string loader `PTR_DAT_004c8c60`.
  2. Extracts server clusters using formula:
     $$\text{Cluster Group} = \lfloor \text{ServerID} / 100 \rfloor, \quad \text{Subchannel} = \text{ServerID} \pmod{100}$$
  3. Formats channel selector labels with `"Channel: %d"`.
  4. If status port (6416) does not respond within timeout, displays `"Player offline"`.

### 2.3 Channel Classifier (`FUN_0014c114`)
* **Signature:** `void FUN_0014c114(int param_1, int param_2)`
* **Logic:**
  Supports up to **21 concurrent subchannels** (`0x15`), classifying them into:
  * **Normal Channels (`0x02`):** Standard PvE rule set.
  * **PVP Channels (`0x01`):** Open combat enabled.
  * **Event / Special Channels (`0x03`):** Tournament and quest instance branch (Branch 1).
  * Computes player load percentage, updating occupancy bars (green, yellow, red).

---

## 3. Network Protocol Packet Format (`Opcode 63` / `0x3F`)

### Login Response Packet (Server $\to$ Client)
* **Opcode:** `63` (`0x3F`)
* **Payload Structure:**
  * `status_code` (1 byte): `1` (Success), `2` (Invalid Password), `3`/`4` (Account Locked/Banned).
  * `session_guid_1` (4 bytes): Primary session token.
  * `session_guid_2` (4 bytes): Secondary session checksum token.
  * `character_slot` (1 byte): Initial character slot index.
