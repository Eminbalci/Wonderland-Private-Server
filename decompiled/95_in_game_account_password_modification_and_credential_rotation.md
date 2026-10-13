# Document 95: In-Game Account Password Modification and Credential Rotation Subsystem

## 1. Architectural Overview

To ensure user account security without requiring out-of-game web portals, *Wonderland Online* provides an in-engine credential management interface: **`Form_ChangePassword_1`**. Embedded directly into the client's system settings and security modules, this form permits players to alter their primary game account password and secondary character deletion security PIN concurrently.

The credential modification engine enforces strict multi-tier verification:
1. **Synchronous Dual-Credential Rotation**: Allowing simultaneous updates to both the primary login password and the character deletion PIN.
2. **Strict Length & Character Bounds**: Mandating that all credentials satisfy a 6-to-10 character length policy (`5 < Length && Length < 11`).
3. **Collision Prevention Safeguard**: Enforcing an anti-collision security rule preventing the account login password from matching the secondary deletion PIN (`"Pwd & Del are same!"`).
4. **Wire Obfuscation**: Packaging credential updates into encrypted Action Code 1:8 (`0x01:0x08`) authentication frames.

```
+-----------------------------------------------------------------------------------+
|                        CREDENTIAL ROTATION ARCHITECTURE                           |
+-----------------------------------------------------------------------------------+
                                          |
                +-------------------------+-------------------------+
                |                                                   |
                v                                                   v
    [Password Form: Form_ChangePassword_1]                [Security Validation Kernel]
    - Dimensions: 307x440 px                              - 6 Masked Input Fields
    - 6-to-10 Character Constraint                        - "Two pwds not match"
    - OK / Cancel Actions                                 - "Pwd & Del are same!" Mutual Exclusion
                |                                                   |
                +----------------- Network Dispatch ----------------+
                                          |
                                          v
                      [Action Code 1:8 Credential Update Packet]
```

---

## 2. VCL Form Hierarchy & UI Structure

The password rotation form is defined in `se_ChangePassword.pas` and instantiated at RVA `0x002ac8c0`:

### 2.1 Form Specifications

| Attribute | Specification |
| :--- | :--- |
| **Pascal Source Unit** | `se_ChangePassword.pas` / `se_System.pas` |
| **Form Identifier** | `Form_ChangePassword_1` |
| **Dimensions** | Width: 307 px (`0x133`), Height: 440 px (`0x1B8`) |
| **Subroutine Addresses** | Constructor: `FUN_002ac85c`, Verification: `FUN_002ac8c0` |
| **Input Fields** | 6 Password-Masked Textboxes (Max length: 10 chars, stride: 29 px) |

### 2.2 Control Hierarchy

```
Form_ChangePassword_1 (307 x 440 px)
 ├── Btn_Close_s_1 (X: 387, Y: 18, W: 18, H: 18)
 ├── btn_ok_1 (X: 138, Y: 20, W: 56, H: 20, Handler: FUN_002ac8c0)
 ├── btn_cancel_1 (X: 248, Y: 20, W: 56, H: 20, Handler: LAB_002ac854)
 ├── Field[1]: Current Account Password (Y: 77)
 ├── Field[2]: New Account Password (Y: 106)
 ├── Field[3]: Confirm New Account Password (Y: 135)
 ├── Field[4]: Current Deletion PIN (Y: 164)
 ├── Field[5]: New Deletion PIN (Y: 193)
 └── Field[6]: Confirm New Deletion PIN (Y: 222)
```

---

## 3. Disassembly & Algorithmic Analysis

### 3.1 Verification & Safeguard Logic (`FUN_002ac8c0`)

```c
// Decompiled from aLogin.exe: FUN_002ac8c0 @ 0x002ac8c0
void FUN_002ac8c0(int param_1)
{
    char cVar1 = '\0';
    byte bVar3 = 0;

    // 1. Check if all 6 edit boxes are empty
    do {
        if (*(int *)(*(int *)(param_1 + 0xf4 + (uint)bVar3 * 4) + 0x1a0) == 0) {
            cVar1 = cVar1 + '\x01';
        }
        bVar3 = bVar3 + 1;
    } while (bVar3 != 6);

    if (cVar1 == '\x06') return; // All blank, do nothing

    // 2. Validate New Password Length (6 to 10 characters)
    int lenNewPwd = FUN_0001412c(*(undefined4 *)(*(int *)(param_1 + 0xf8) + 0x1a0));
    if (lenNewPwd < 6 || lenNewPwd > 10) {
        ShowAlert("Password must be 6-10 characters!");
        return;
    }

    // 3. Validate Password Confirmation Match
    bool pwdMatch = FUN_0001423c(
        *(undefined4 *)(*(int *)(param_1 + 0xfc) + 0x1a0),
        *(undefined4 *)(*(int *)(param_1 + 0x100) + 0x1a0));
    if (!pwdMatch) {
        // Display toast error: "Two pwds not match"
        (**(code **)(**(int **)PTR_DAT_004c8d40 + 0x9c))
            (*(int **)PTR_DAT_004c8d40, "Two pwds not match", 2000, 0, 0);
        return;
    }

    // 4. Validate Deletion PIN Confirmation Match
    bool pinMatch = FUN_0001423c(
        *(undefined4 *)(*(int *)(param_1 + 0x108) + 0x1a0),
        *(undefined4 *)(*(int *)(param_1 + 0x10c) + 0x1a0));
    if (!pinMatch) {
        ShowAlert("Two PINs not match!");
        return;
    }

    // 5. Anti-Collision Safeguard: Password cannot match Deletion PIN
    bool areSame = FUN_0001423c(
        *(undefined4 *)(*(int *)(param_1 + 0xfc) + 0x1a0),   // New Account Password
        *(undefined4 *)(*(int *)(param_1 + 0x108) + 0x1a0)); // New Deletion PIN
    if (areSame) {
        // Display error: "Pwd & Del are same!"
        (**(code **)(**(int **)PTR_DAT_004c8d40 + 0x9c))
            (*(int **)PTR_DAT_004c8d40, "Pwd & Del are same!", 2000, 0, 0);
        return;
    }

    // 6. Submit Credential Rotation to Auth Server via Action Code 1:8
    SendCredentialRotationPacket(param_1);
}
```

---

## 4. Security Policy & Validation Rules

| Rule Name | Severity | Condition | Client Reaction |
| :--- | :--- | :--- | :--- |
| **Length Check** | Fatal | $\text{Length} < 6 \lor \text{Length} > 10$ | Rejects input; prompts 6..10 limit |
| **Password Match** | Fatal | $\text{NewPwd} \neq \text{ConfirmPwd}$ | Prompts `"Two pwds not match"` |
| **PIN Match** | Fatal | $\text{NewPIN} \neq \text{ConfirmPIN}$ | Prompts `"Two PINs not match"` |
| **Collision Mutex** | Fatal | $\text{NewPwd} == \text{NewPIN}$ | Prompts `"Pwd & Del are same!"` |
| **Whitespace Ban** | Warning | Contains space (`0x20`) or control chars | Automatically stripped by editor |

---

## 5. Network Wire Protocol

### 5.1 Client -> Server: Action Code 1:8 Credential Rotation Request

```
+---------------+---------------+---------------+---------------+---------------+
| Magic Header  | Length (LE)   | Action Code   | Sub-Opcode    | Old Pwd Len   |
| 0xF4 0x44     | 0x2A 0x00     | 0x01          | 0x08          | 0x08          |
+---------------+---------------+---------------+---------------+---------------+
| Old Password (8B)             | New Pwd Len   | New Password (8B)             |
+-------------------------------+---------------+-------------------------------+
| Old PIN Len   | Old PIN (6B)  | New PIN Len   | New PIN (6B)                  |
+---------------+---------------+---------------+-------------------------------+
```

### 5.2 Server -> Client: Action Code 1:9 Credential Rotation Result

```
+---------------+---------------+---------------+---------------+---------------+
| Magic Header  | Length (LE)   | Action Code   | Sub-Opcode    | Result Code   |
| 0xF4 0x44     | 0x03 0x00     | 0x01          | 0x09          | 0x00 (Success)|
+---------------+---------------+---------------+---------------+---------------+
```

---

## 6. C# Server Implementation Blueprint

```csharp
// CredentialRotationService.cs - Wonderland Private Server Account Security Manager
using System;
using System.Security.Cryptography;
using System.Text;

namespace Wonderland.Server.Security
{
    public sealed class CredentialRotationService
    {
        public bool TryRotateCredentials(
            AccountEntity account,
            string oldPassword,
            string newPassword,
            string oldPin,
            string newPin,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            // 1. Verify Existing Password
            if (!VerifyHash(oldPassword, account.PasswordHash))
            {
                errorMessage = "Incorrect current password.";
                return false;
            }

            // 2. Verify Existing PIN (if set)
            if (!string.IsNullOrEmpty(account.DeletionPinHash))
            {
                if (!VerifyHash(oldPin, account.DeletionPinHash))
                {
                    errorMessage = "Incorrect current deletion PIN.";
                    return false;
                }
            }

            // 3. Length Constraints (6..10 chars)
            if (newPassword.Length < 6 || newPassword.Length > 10)
            {
                errorMessage = "New password must be 6-10 characters.";
                return false;
            }

            if (newPin.Length < 6 || newPin.Length > 10)
            {
                errorMessage = "New PIN must be 6-10 characters.";
                return false;
            }

            // 4. Collision Mutex: Password cannot equal PIN
            if (string.Equals(newPassword, newPin, StringComparison.Ordinal))
            {
                errorMessage = "Password and deletion PIN cannot be identical.";
                return false;
            }

            // 5. Update Password & PIN Hashes
            account.PasswordHash = HashCredential(newPassword);
            account.DeletionPinHash = HashCredential(newPin);
            account.LastCredentialUpdate = DateTime.UtcNow;

            return true;
        }

        private bool VerifyHash(string input, string storedHash) =>
            string.Equals(HashCredential(input), storedHash, StringComparison.OrdinalIgnoreCase);

        private string HashCredential(string input)
        {
            using var sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes);
        }
    }
}
```

---

## 7. Verification & Cross-References

- **Disassembly Offsets**: `aLogin.exe` RVA `0x002ac85c` (Constructor), `0x002ac8c0` (Validator).
- **String References**: Line 280533 (`"Form_ChangePassword_1"`), Line 280617 (`"Two pwds not match"`), Line 280648 (`"Pwd & Del are same!"`).
- **Related Specs**:
  - [`08_login_authentication_and_character_selection_pipeline.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/08_login_authentication_and_character_selection_pipeline.md)
  - [`54_secondary_password_security_and_account_pin_vault.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/54_secondary_password_security_and_account_pin_vault.md)
  - [`73_character_deletion_security_pin_challenge_and_safe_disposal_architecture.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/73_character_deletion_security_pin_challenge_and_safe_disposal_architecture.md)
