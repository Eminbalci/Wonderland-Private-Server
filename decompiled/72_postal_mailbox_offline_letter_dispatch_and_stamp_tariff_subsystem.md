# Document 72: Postal Mailbox, Offline Letter Dispatch, and Stamp Tariff Subsystem

## 1. Executive Summary & Engine Architecture

Asynchronous player communication in Wonderland Online is delivered through the in-game postal service, driven by dialog forms `form_Mail` and `form_Mailplus`. Reverse-engineered from client subroutines `FUN_003dc410`, `FUN_003dc614`, and `FUN_003df66c`, this subsystem enables players to read incoming letters, compose text correspondence to offline recipients, attach small currency remittances, and organize up to 50 archived messages.

The postal subsystem switches dynamically between two presentation windows:
1. **`form_Mail` ($414 \times 290$ px):** Standard mailbox client displaying 5 message headers per page with unread status indicators (`"icon_mail_1"`), sender nameplates, and reception timestamps.
2. **`form_Mailplus` ($444 \times 290$ px):** Expanded postal terminal featuring a built-in letter composition memo, recipient address lookup, and stamp consumption validation.

The postal network communicates via Action Code `0x17` (`23` decimal). It enforces strict transmission safeguards, verifying postage stamp inventory requirements, recipient character existence, and recipient mailbox capacity limits (`"Other player full"`, `"Inv. full, reissue tmrw"`).

```
+------------------------------------------------------------------------------------+
|                         POSTAL MAILBOX ENGINE ARCHITECTURE                         |
+------------------------------------------------------------------------------------+
                                      |
         +----------------------------+----------------------------+
         | Inbox Reader Mode: form_Mail                            | Outbox Composer: form_Mailplus
         v                                                         v
  [Standard Mailbox] (414x290 px)                           [Expanded Composer] (444x290 px)
  - 5 Letter Entries per Page (Stride: 58 px)               - Recipient Address Input Box
  - Unread vs Read Flags (icon_mail_1)                      - Multi-Line Letter Body Memo
  - Max Inbox Capacity: 50 Letters                          - Stamp Tariff / Gold Postage Fee
         |                                                         |
         +----------------------------+----------------------------+
                                      |
                                      v
                 [Network Dispatcher: Action Code 0x17 (23)]
        Subcodes: 0x1F (Headers), 0x20 (Read), 0x21 (Send), 0x22 (Delete)
```

---

## 2. Memory Layouts & Data Structures

### 2.1 Mail Header Record (`0x34` = 52 Bytes)
Each message in the player's inbox is stored as a 52-byte record:

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `uint` | `MailId` | Unique sequential mail identifier |
| `+0x04` | `char[16]` | `SenderName` | Nameplate string of the authoring player |
| `+0x14` | `char[20]` | `Subject` | Short letter title or subject line |
| `+0x28` | `double` | `Timestamp` | Delphi IEEE-754 `TDateTime` reception timestamp |
| `+0x30` | `byte` | `IsRead` | `0` = Unread (blinking icon), `1` = Opened/Read |
| `+0x31` | `byte` | `HasRemittance`| `1` = Contains enclosed gold coins |
| `+0x32` | `ushort` | `RemittanceGold`| Gold amount enclosed (if any) |

### 2.2 `form_Mail` Form Instance Layout (`param_1`)

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `void*` | `VTable` | Delphi component virtual method table pointer |
| `+0x04` | `uint` | `FormHandle` | DirectDraw surface bitmap handle |
| `+0x3C` | `int*` | `pBottomBar` | Container anchoring bottom navigation buttons |
| `+0x3E` | `int*` | `BtnInboxTab` | Tab button selecting Inbox mode |
| `+0x3F` | `int*` | `BtnOutboxTab` | Tab button selecting Outbox / Compose mode |
| `+0x40` | `int*[5]` | `RowSenderControls`| Display controls for the 5 visible sender names |
| `+0x45` | `int*[5]` | `RowSubjectControls`| Display controls for the 5 visible letter subjects |
| `+0x4D` | `int*[4]` | `ActionButtons`| Array of action buttons (Read, Compose, Delete, Close) |
| `+0x7F` | `byte` | `TerminalMode` | `0` = Standard `form_Mail`, `1` = Expanded `form_Mailplus` |
| `+0x1FD`| `byte` | `ActiveTab` | `0` = Inbox, `1` = Outbox Composer |

---

## 3. UI Form Geometry, Coordinates & Controls

### 3.1 `form_Mail` & `form_Mailplus` Layout Metrics
- **Dimensions:**
  - Mode 0 (`form_Mail`): Width `0x19E` (414 px), Height `0x122` (290 px).
  - Mode 1 (`form_Mailplus`): Width `0x1BC` (444 px), Height `0x122` (290 px).
- **5 Letter Rows ($i = 0..4$):**
  - Vertically spaced at stride 58 px (`0x3A`):
    - Row Sender Anchor: $Y = i \times 58 + 44$ px ($i \times \text{0x3A} + \text{0x2C}$).
    - Row Subject Anchor: $Y = i \times 58 + 69$ px ($i \times \text{0x3A} + \text{0x45}$).
- **Outbox Composer Controls:**
  - Recipient Edit Box: Width 140 px, max 14 characters.
  - Letter Body Memo: Bounding box $380 \times 150$ px, max 300 characters.
  - Action buttons: `"Btn_SendOut_1"`, `"Btn_Cancel_1"`.

```
+---------------------------------------------------------------------------------+
| form_Mail: Postal Mailbox (414 x 290 px)                                        |
+---------------------------------------------------------------------------------+
| [Tab: Inbox (5/50)] [Tab: Compose Letter]                                   [X] |
+---------------------------------------------------------------------------------+
| [icon_mail_1] Sender: GuildMaster  | Subject: Guild War Schedule   | 2026/09/20 |
| [icon_mail_1] Sender: MerchantX    | Subject: Trade Delivery Info  | 2026/09/19 |
| [icon_mail_1] Sender: Friend01     | Subject: Hello from Kyoto!    | 2026/09/18 |
| [Empty Slot]                                                                    |
| [Empty Slot]                                                                    |
+---------------------------------------------------------------------------------+
| Page: [<] 1 / 10 [>]       [Btn_Read: Open]   [Btn_Delete: Discard]  [Close]    |
+---------------------------------------------------------------------------------+
```

---

## 4. Network Protocol, Wire Frames & Action Codes

### 4.1 Packet Framing
Postal correspondence uses Action Code `0x17` (`23` decimal):
- **Opcode:** `0x17`
- **Subcodes:**
  - `0x1F`: Query Mailbox Headers (C2S)
  - `0x20`: Retrieve Full Letter Body (C2S)
  - `0x21`: Dispatch Outgoing Letter (C2S via `FUN_003df66c`)
  - `0x22`: Delete Stored Letter (C2S)
  - `0x25`: Inbound Mail Alert Notification (S2C)

### 4.2 C# Packet Structure Definitions

```csharp
namespace Wonderland.Network.Packets.Mail
{
    /// <summary>
    /// Client-to-server request to transmit an outbound letter to another player.
    /// Wire Header: Action Code 0x17, Subcode 0x21.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct C2S_SendMailPacket
    {
        public byte ActionCode;       // 0x17 (23)
        public byte SubCode;          // 0x21 (33)
        
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
        public string RecipientName;  // Max 14 characters + null
        
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 21)]
        public string Subject;        // Max 20 characters
        
        public ushort BodyLength;     // Length of message body
        // Trailing: BodyText string (max 300 chars)
    }

    /// <summary>
    /// Client-to-server request to read a letter or delete it.
    /// Wire Header: Action Code 0x17, Subcode 0x20 (Read) or 0x22 (Delete).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct C2S_MailActionPacket
    {
        public byte ActionCode;       // 0x17 (23)
        public byte SubCode;          // 0x20 (Read) or 0x22 (Delete)
        public uint MailId;           // Target letter ID
    }
}
```

---

## 5. Postal Validation Safeguards & Stamp Tariff

1. **Stamp Tariff Verification:** Sending a letter requires either:
   - Possession of consumable item `Postage Stamp` (`ItemId == 50041`) in inventory, OR
   - Payment of a base postal tariff of $50\text{ Gold}$ from the player's purse.
2. **Recipient Validation:** If the recipient character does not exist in the database, transmission aborts with `"Unable to find"`.
3. **Capacity Cap Gate:** If the recipient's inbox contains 50 messages, the letter is rejected with `"Other player full"`.

---

## 6. Private Server Implementation Guide & C# Production Code

The following service provides database persistence, recipient routing, unread tracking, and postal tariff management.

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Wonderland.Network.Packets.Mail;

namespace Wonderland.Server.Services.Mail
{
    public sealed class MailMessage
    {
        public uint MailId { get; init; }
        public uint SenderCharacterId { get; init; }
        public string SenderName { get; init; } = string.Empty;
        public uint RecipientCharacterId { get; init; }
        public string Subject { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
        public DateTime SentTime { get; init; } = DateTime.UtcNow;
        public bool IsRead { get; set; }
    }

    public interface IMailService
    {
        bool SendMail(PlayerCharacter sender, C2S_SendMailPacket packet, string body, out string error);
        IReadOnlyList<MailMessage> GetHeaders(uint characterId);
        string ReadBody(PlayerCharacter player, uint mailId);
        bool DeleteMail(PlayerCharacter player, uint mailId);
    }

    public sealed class MailService : IMailService
    {
        private readonly ConcurrentDictionary<uint, List<MailMessage>> _inboxes = new();
        private readonly IPlayerDirectory _directory;
        private int _nextMailId = 1000;

        public MailService(IPlayerDirectory directory)
        {
            _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        }

        public bool SendMail(PlayerCharacter sender, C2S_SendMailPacket packet, string body, out string error)
        {
            error = string.Empty;

            if (sender == null) throw new ArgumentNullException(nameof(sender));

            var recipient = _directory.FindByName(packet.RecipientName);
            if (recipient == null)
            {
                error = "Unable to find";
                return false;
            }

            var inbox = _inboxes.GetOrAdd(recipient.CharacterId, _ => new List<MailMessage>());
            lock (inbox)
            {
                if (inbox.Count >= 50)
                {
                    error = "Other player full";
                    return false;
                }

                // Deduct Postage Tariff (50 Gold)
                if (sender.Gold < 50)
                {
                    error = "Not enough Gold";
                    return false;
                }

                sender.DeductGold(50);

                var mail = new MailMessage
                {
                    MailId = (uint)System.Threading.Interlocked.Increment(ref _nextMailId),
                    SenderCharacterId = sender.CharacterId,
                    SenderName = sender.Name,
                    RecipientCharacterId = recipient.CharacterId,
                    Subject = packet.Subject,
                    Body = body
                };

                inbox.Add(mail);

                // If recipient is online, notify them
                recipient.Send(new S2C_NewMailAlertPacket { ActionCode = 0x17, SubCode = 0x25 });
            }

            return true;
        }

        public IReadOnlyList<MailMessage> GetHeaders(uint characterId)
        {
            if (_inboxes.TryGetValue(characterId, out var inbox))
            {
                lock (inbox) return inbox.ToList();
            }
            return Array.Empty<MailMessage>();
        }

        public string ReadBody(PlayerCharacter player, uint mailId)
        {
            if (player == null) return string.Empty;

            if (_inboxes.TryGetValue(player.CharacterId, out var inbox))
            {
                lock (inbox)
                {
                    var mail = inbox.FirstOrDefault(m => m.MailId == mailId);
                    if (mail != null)
                    {
                        mail.IsRead = true;
                        return mail.Body;
                    }
                }
            }
            return string.Empty;
        }

        public bool DeleteMail(PlayerCharacter player, uint mailId)
        {
            if (player == null) return false;

            if (_inboxes.TryGetValue(player.CharacterId, out var inbox))
            {
                lock (inbox)
                {
                    return inbox.RemoveAll(m => m.MailId == mailId) > 0;
                }
            }
            return false;
        }
    }
}
```
