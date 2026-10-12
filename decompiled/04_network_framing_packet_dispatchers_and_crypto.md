# Network Framing, Packet Dispatchers, and Cryptography Specification

## 1. Network Topology & Socket Architecture

The client manages asynchronous Winsock TCP client connections across two primary sockets anchored under context object `PTR_DAT_004c96d0` / `DAT_006c5bcc`:

```
                             +-------------------+
                             |    aLogin.exe     |
                             |  (Master Context) |
                             +-------------------+
                                /               \
             Socket Index 1    /                 \   Socket Index 2
            (Context + 0x3dc)/                   \  (Context + 0x3e4)
                            v                     v
                +-----------------------+     +-----------------------+
                | Status / Mall Probe   |     | Primary Game Socket   |
                | Port 6416 (0x1910)    |     | Port 6414 TCP         |
                +-----------------------+     +-----------------------+
```

* **Socket Index 1 (`Context + 0x3dc`):** Status / Mall Probe socket. Connects to port 6416 (`0x1910`) to test server status and fetch promotional announcements.
* **Socket Index 2 (`Context + 0x3e4`):** Primary Game / Login socket. Connects to port 6414 for user authentication, character selection, and in-game packet transmission.

---

## 2. Packet Framing & Binary Format

Wonderland Online network packets follow a strict binary framing protocol:

```
+---------------+-------------------+-----------------------+-------------------+
| Field         | Size              | Format                | Description       |
+---------------+-------------------+-----------------------+-------------------+
| Magic Header  | 2 Bytes           | UInt16 Little-Endian  | 0x44F4 (17652)    |
| Length        | 2 Bytes           | UInt16 Little-Endian  | Payload byte count|
| Payload       | Length - 4 Bytes  | XOR-encrypted bytes   | Action Code + Data|
+---------------+-------------------+-----------------------+-------------------+
```

### TCP Framing Algorithm (`FUN_00124a1c`)
1. Reads 2 bytes from buffer stream.
2. Checks if bytes match header signature `0x44F4` (`0xF4, 0x44`).
3. If invalid, slides buffer by 1 byte and re-evaluates.
4. If valid, reads next 2 bytes for payload length.
5. Verifies if available buffer bytes $\ge$ length.
6. If complete frame is present, forwards payload to decryption and dispatch handlers.

---

## 3. Cryptography & Packet Decryption (`FUN_00153d6c`)

Both incoming and outgoing packet payloads are encrypted using symmetric single-byte XOR arithmetic.

### Decompiled Encryption / Decryption Routine
```c
// Function: FUN_00153d6c @ 00153d6c
void FUN_00153d6c(undefined4 param_1, int param_2, byte param_3, undefined4 param_4)
{
    int iVar2;
    int iVar3;
    int iVar4;
    int local_8;

    local_8 = param_2;
    if (param_3 == 0) {
        param_3 = 0xad; // Default symmetric XOR key = 0xAD (173 decimal)
    }
    iVar2 = FUN_0001412c(local_8); // Buffer byte length
    if (0 < iVar2) {
        iVar4 = 1;
        do {
            iVar3 = FUN_000142fc(&local_8);
            *(byte *)(iVar3 + -1 + iVar4) = *(byte *)(local_8 + -1 + iVar4) ^ param_3;
            iVar4 = iVar4 + 1;
            iVar2 = iVar2 + -1;
        } while (iVar2 != 0);
    }
    FUN_00013f00(param_4, local_8);
}
```

---

## 4. Outgoing Packet Transmission Lifecycle (`FUN_001630d0`)

The client buffers outbound action codes into an outgoing packet queue before transmitting them over the socket:

```c
// Function: FUN_001630d0 @ 001630d0
void FUN_001630d0(void)
{
    if (DAT_006c9864 != (int *)0x0) { // Outgoing Packet Queue
        iVar3 = (**(code **)(*DAT_006c9864 + 0x14))(); // Queue count check
        if (iVar3 != 0) {
            (**(code **)(*DAT_006c9864 + 0xc))(DAT_006c9864, 0, &local_1c); // Dequeue packet
            FUN_00153d6c(DAT_006c8e9c, local_1c, 0xad); // Encrypt payload with XOR 0xAD
            iVar4 = FUN_00079f24(*(undefined4 *)(*(int *)(DAT_006c5bcc + 0x3e4) + 0x80), local_c); // Send to socket
            ...
        }
    }
}
```

---

## 5. Primary Packet Dispatchers

* **`FUN_00115a38` (Main Keyboard & Input Scancode Dispatcher):**
  * Maps DirectInput hardware scan codes to virtual key codes (`1` $\to$ `0x1B` Escape, `2..10` $\to$ `'1'..'9'`, `0x10..0x19` $\to$ `'Q'..'P'`, `0x1E..0x26` $\to$ `'A'..'L'`, `0x2C..0x32` $\to$ `'Z'..'M'`).
* **`FUN_0010e218` (Interface State Dispatcher):**
  * Evaluates packet category codes and routes UI refresh signals to active in-game forms.
