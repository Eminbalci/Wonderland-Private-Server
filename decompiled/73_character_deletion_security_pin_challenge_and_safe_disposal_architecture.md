# Document 73: Character Deletion, Security PIN Challenge, and Safe Disposal Architecture

## 1. Executive Summary & Engine Architecture

To protect player progression, virtual assets, and account investments against accidental loss or unauthorized account compromise, Wonderland Online enforces a multi-factor character deletion pipeline. Implemented through `form_DeleteRole` across subroutines `FUN_002abe58` and `FUN_002ac11c`, this security architecture enforces a challenge-response verification sequence before any character slot can be de-registered from the world database.

The deletion window `form_DeleteRole` ($299 \times 449$ px) requires the user to submit four distinct validation credentials:

1. Account Primary Login Password.
2. Character Deletion Security Code (a dedicated 6-to-10 alphanumeric PIN configured upon account creation).
3. Re-typed Deletion Security Code Confirmation.
4. Secondary Secret Answer.

Subroutine `FUN_002ac11c` validates credential lengths (`"New Del must be 6 to 10 ch"`), ensures deletion codes do not match basic account passwords (`"Secure Code & Pwd must not match"`), evaluates active guildmaster leadership mutexes, and enforces level-tiered countdown holding periods before emitting Action Code `0x02` Subcode `0x03` to purge the character entity.

```
+------------------------------------------------------------------------------------+
|                      CHARACTER DELETION PIPELINE ARCHITECTURE                      |
+------------------------------------------------------------------------------------+
                                      |
                                      v
                      [form_DeleteRole Dialog (299x449 px)]
                4 Masked Input Boxes + Dual Confirmation Buttons
                                      |
         +----------------------------+----------------------------+
         | Security Verification Gate                              | Account & Guild Mutex Gate
         v                                                         v
  [Credential Validation: FUN_002ac11c]                     [State Locks]
  - Primary Account Password Match                          - Guild Leader Lock: Disband/Pass First
  - Deletion PIN (6..10 Characters)                         - Vending Stall / Trade Mutex Lock
  - Dual PIN Equality Verification                          - High-Level 7-Day Safety Escrow
         |                                                         |
         +----------------------------+----------------------------+
                                      |
                                      v
                 [Network Dispatcher: Action Code 0x02 (2)]
               Subcodes: 0x03 (Delete Request), 0x05 (Roster Sync)
```

---

## 2. Memory Layouts & Data Structures

### 2.1 Character Deletion Request Record (`0x28` = 40 Bytes)

| Offset    | Type         | Field Name          | Description                                        |
| --------- | ------------ | ------------------- | -------------------------------------------------- |
| `+0x00` | `uint`     | `CharacterId`     | Target character identifier to be deleted          |
| `+0x04` | `char[16]` | `LoginPassword`   | Primary account password verification string       |
| `+0x14` | `char[11]` | `DeletionPin`     | 6 to 10 character secondary deletion PIN           |
| `+0x1F` | `byte`     | `SlotIndex`       | Character selection roster slot index ($0..3$)   |
| `+0x20` | `uint`     | `Timestamp`       | Unix timestamp of deletion request                 |
| `+0x24` | `uint`     | `EscrowHoldUntil` | Epoch timestamp when 7-day safety period completes |

### 2.2 `form_DeleteRole` Instance Layout (`param_1`)

| Offset    | Type        | Field Name     | Description                                    |
| --------- | ----------- | -------------- | ---------------------------------------------- |
| `+0x00` | `void*`   | `VTable`     | Delphi component virtual method table pointer  |
| `+0x04` | `uint`    | `FormHandle` | DirectDraw surface bitmap handle               |
| `+0x3A` | `uint`    | `BtnOk`      | Confirm deletion button (`"btn_ok_1"`)       |
| `+0x3B` | `uint`    | `BtnCancel`  | Cancel button (`"btn_cancel"`)               |
| `+0x3C` | `uint`    | `BtnCloseS1` | Top-right close button (`"Btn_Close_s_1"`)   |
| `+0x3D` | `int*[4]` | `InputBoxes` | Array of 4 password-masked input edit controls |

---

## 3. UI Form Geometry, Coordinates & Controls

### 3.1 `form_DeleteRole` Layout Metrics

- **Dimensions:** Width `299` pixels, Height `0x1C1` (449 pixels).
- **Positioning:** Centered at $(X = \text{0x96}, Y = \text{0xAF})$.
- **4 Password-Masked Input Boxes ($i = 0..3$):**
  - Vertically spaced at stride 39 px (`0x27`):
    $$
    \text{Box Y} = i \times 39 + 97\text{ px} = i \times \text{0x27} + \text{0x61}
    $$
  - Horizontal anchor: $X = \text{0x7B}$ (123 px).
  - Box dimensions: Width `0x83` (131 px), Height `0x13` (19 px).
  - Masking mode: Password bullets (`FUN_00482b00(piVar1, 2)`).
  - Length ceiling: 10 characters (`FUN_00482ae0(piVar1, 10)`).
- **Action Buttons:**
  - `"btn_ok_1"`: $X = \text{0x91}$ (145 px), $Y = \text{0x116}$ (278 px), size $25 \times 70$ px. (Callback: `FUN_002ac11c`)
  - `"btn_cancel"`: $X = \text{0xE9}$ (233 px), $Y = \text{0x116}$ (278 px), size $25 \times 70$ px.
  - `"Btn_Close_s_1"`: $X = \text{Width} - 22$ px, $Y = 6$ px, size $17 \times 17$ px.

```
+-------------------------------------------------------+
| form_DeleteRole: Character Deletion (299 x 449 px)    |
+-------------------------------------------------------+
| [Header Title: Delete Character]                  [X] |
+-------------------------------------------------------+
|                                                       |
|  Account Password:        [ ********** ]              |
|  Deletion Security PIN:   [ ********** ]              |
|  Re-type Deletion PIN:    [ ********** ]              |
|  Secret Verification:     [ ********** ]              |
|                                                       |
|  WARNING: Deletion is permanent and removes all       |
|  inventory, equipment, pets, and tent structures.     |
|                                                       |
|         [ btn_ok_1: Confirm ]   [ btn_cancel ]        |
+-------------------------------------------------------+
```

---

## 4. Network Protocol, Wire Frames & Action Codes

### 4.1 Packet Framing

Character deletion actions operate under Action Code `0x02` (Account Lifecycle):

- **Opcode:** `0x02`
- **Subcodes:**
  - `0x03`: Submit Character Deletion Request (C2S)
  - `0x04`: Cancel Pending 7-Day Deletion Hold (C2S)
  - `0x05`: Character Slot Cleared Notification (S2C)

### 4.2 C# Packet Structure Definitions

```csharp
namespace Wonderland.Network.Packets.Account
{
    /// <summary>
    /// Client-to-server request to delete a character slot with credentials.
    /// Wire Header: Action Code 0x02, Subcode 0x03.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct C2S_DeleteCharacterPacket
    {
        public byte ActionCode;       // 0x02 (2)
        public byte SubCode;          // 0x03 (3)
        public byte SlotIndex;        // Character slot index (0..3)
      
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 17)]
        public string LoginPassword;  // Account password
      
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 11)]
        public string DeletionPin;    // 6 to 10 character deletion PIN
    }

    /// <summary>
    /// Server-to-client response confirming character deletion or scheduled escrow hold.
    /// Wire Header: Action Code 0x02, Subcode 0x05.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct S2C_DeleteCharacterResponse
    {
        public byte ActionCode;       // 0x02 (2)
        public byte SubCode;          // 0x05 (5)
        public byte SlotIndex;        // Affected slot (0..3)
        public byte ResultCode;       // 0 = Success/Purged, 1 = Invalid Password, 2 = Invalid PIN, 3 = In Escrow Hold
        public uint EscrowSeconds;    // Seconds remaining if held in safety period
    }
}
```

---

## 5. Security Safeguards & Deletion Validation Pipeline

Subroutine `FUN_002ac11c` executes four sequential verification checks:

1. **Length Validation:** The deletion PIN must contain between 6 and 10 alphanumeric characters:
   `"Old Del must be 6 to 10 ch"` / `"New Del must be 6 to 10 ch"`
2. **Password Mismatch Rule:** To prevent compromised login passwords from immediately destroying characters, the deletion PIN cannot be identical to the login password:
   `"Secure Code & Pwd must not match"` / `"Pwd & Del are same!"`
3. **Guild Master Mutex:** Characters serving as active army group guildmasters cannot be deleted until leadership is passed:
   `"Want to be a team leader?"` / `"Can't delete now"`
4. **Safety Escrow Hold:** Characters above Level 50 enter a 7-day (`604,800` seconds) pending deletion state, allowing legitimate account owners to cancel the request if their account was compromised.

---

## 6. Private Server Implementation Guide & C# Production Code

The following service manages the character disposal pipeline, PIN verification, safety escrow periods, and database purging.

```csharp
using System;
using System.Collections.Concurrent;
using Wonderland.Network.Packets.Account;

namespace Wonderland.Server.Services.Account
{
    public sealed class CharacterDeletionRecord
    {
        public uint CharacterId { get; init; }
        public uint AccountId { get; init; }
        public byte SlotIndex { get; init; }
        public DateTime RequestTime { get; init; } = DateTime.UtcNow;
        public DateTime PurgeTime { get; init; }
        public bool IsPendingPurge => DateTime.UtcNow < PurgeTime;
    }

    public interface ICharacterDeletionService
    {
        S2C_DeleteCharacterResponse ProcessDeletion(uint accountId, string realPassword, string realPin, C2S_DeleteCharacterPacket packet, PlayerCharacter character);
        bool CancelPendingDeletion(uint accountId, byte slotIndex);
    }

    public sealed class CharacterDeletionService : ICharacterDeletionService
    {
        private readonly ConcurrentDictionary<(uint, byte), CharacterDeletionRecord> _pendingEscrows = new();

        public S2C_DeleteCharacterResponse ProcessDeletion(uint accountId, string realPassword, string realPin, C2S_DeleteCharacterPacket packet, PlayerCharacter character)
        {
            if (character == null)
            {
                return new S2C_DeleteCharacterResponse { ActionCode = 0x02, SubCode = 0x05, ResultCode = 1 };
            }

            // 1. Password Verification
            if (packet.LoginPassword != realPassword)
            {
                return new S2C_DeleteCharacterResponse { ActionCode = 0x02, SubCode = 0x05, ResultCode = 1 }; // Wrong Passwords
            }

            // 2. PIN Verification
            if (packet.DeletionPin != realPin)
            {
                return new S2C_DeleteCharacterResponse { ActionCode = 0x02, SubCode = 0x05, ResultCode = 2 }; // Wrong Del Pwd
            }

            // 3. Guild Leader Mutex
            if (character.IsGuildLeader)
            {
                return new S2C_DeleteCharacterResponse { ActionCode = 0x02, SubCode = 0x05, ResultCode = 3 }; // Can't delete now
            }

            // 4. Level-Tiered Safety Escrow Check
            if (character.Level >= 50)
            {
                var escrow = new CharacterDeletionRecord
                {
                    CharacterId = character.CharacterId,
                    AccountId = accountId,
                    SlotIndex = packet.SlotIndex,
                    PurgeTime = DateTime.UtcNow.AddDays(7)
                };

                _pendingEscrows[(accountId, packet.SlotIndex)] = escrow;

                return new S2C_DeleteCharacterResponse
                {
                    ActionCode = 0x02,
                    SubCode = 0x05,
                    SlotIndex = packet.SlotIndex,
                    ResultCode = 3, // In Escrow Hold
                    EscrowSeconds = 7 * 86400
                };
            }

            // Instant Purge for Low-Level Characters
            character.PurgeFromDatabase();

            return new S2C_DeleteCharacterResponse
            {
                ActionCode = 0x02,
                SubCode = 0x05,
                SlotIndex = packet.SlotIndex,
                ResultCode = 0 // Purged
            };
        }

        public bool CancelPendingDeletion(uint accountId, byte slotIndex)
        {
            return _pendingEscrows.TryRemove((accountId, slotIndex), out _);
        }
    }
}
```
