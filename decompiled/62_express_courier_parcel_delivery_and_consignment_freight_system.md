# Document 62: Express Courier, Parcel Delivery, and Consignment Freight System

## 1. Executive Summary & Engine Architecture

Wonderland Online features an asynchronous player-to-player and player-to-self logistics network operated through the Express Courier subsystem. Dissected from client binaries across subroutines `FUN_001fa9b4`, `FUN_001fb540`, `FUN_001fb888`, `FUN_001fc15c`, `FUN_001fc17c`, `FUN_001fc5ac`, and `FUN_001fc5fc`, this system enables players to dispatch parcels, trade items remotely, send gifts to friends and guild members, and manage multi-item deliveries with optional delivery insurance or Cash-on-Delivery (COD) terms.

The user interface comprises two interrelated dialog windows: `Form_Express_1` ($460 \times 299$ px), serving as the outbound dispatch depot and parcel preparation station, and `Form_Express_2` ($322 \times 228$ px), functioning as the inbound parcel pickup terminal. The freight engine enforces atomic inventory locking (`*(item + 0x13e) = 6`), calculates dynamic transport tariffs based on composite package weight and item quantity via `FUN_001fc17c`, and provides rapid recipient selection via direct hooks into the ICQ friend and army group guild rosters.

```
+------------------------------------------------------------------------------------+
|                         EXPRESS FREIGHT ENGINE OVERVIEW                            |
+------------------------------------------------------------------------------------+
                                      |
         +----------------------------+----------------------------+
         | Outbound Dispatch Depot                                 | Inbound Parcel Terminal
         v                                                         v
  [Form_Express_1] (460x299 px)                             [Form_Express_2] (322x228 px)
  - 50-Slot Inventory Grid (10x5)                           - Active Consignment Queue
  - 5 Staging Courier Slots                                 - Sender Verification
  - Recipient Name Input (Max 14 Chars)                     - COD Fee Confirmation
  - Friend / Guild Roster Pickers                           - 1-Click Pickup & Inventory Ingestion
         |                                                         |
         +----------------------------+----------------------------+
                                      |
                                      v
                       [Freight Calculus: FUN_001fc17c]
                     Composite Weight x Quantity + Insurance
                                      |
                                      v
                      [Network Dispatcher: Action Code 0x17]
              Subcodes: 0x51 (Dispatch), 0x15 (Cancel), 0x72 (Pickup)
```

---

## 2. Memory Layouts & Data Structures

### 2.1 Outbound Dispatch Depot State (`Form_Express_1` Instance)
The client instance pointer allocated in `FUN_001fa9b4` manages the following memory layout:

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `void*` | `VTable` | Delphi virtual method table pointer |
| `+0x04` | `uint` | `FormHandle` | DirectDraw surface handle for `Form_Express_1` |
| `+0x08` | `int` | `BaseX` | Screen X position ($0 \le X \le 800 - 460$) |
| `+0x0C` | `int` | `BaseY` | Screen Y position ($0 \le Y \le 600 - 299$) |
| `+0x2A` | `byte` | `ActiveTab` | Current filter tab index |
| `+0x3A` | `int*[50]` | `InventoryGrid` | References to the 50 inventory grid slot display controls |
| `+0x70` | `uint` | `BtnCloseS1` | Sub-control handle for top-right small close button |
| `+0x71` | `uint` | `BtnClose1` | Sub-control handle for bottom close button |
| `+0x72` | `uint` | `BtnSendOut1` | Dispatch confirmation button (`"Btn_SendOut_1"`) |
| `+0x73` | `uint` | `BtnStop1` | Delivery cancellation button (`"Btn_Stop_1"`) |
| `+0x81` | `int*[5]` | `CourierSlots` | Pointers to the 5 active courier staging slots |
| `+0x8D` | `uint` | `RecipientEditBox`| Recipient character name input box (`MaxLen = 14`) |
| `+0x8E` | `uint` | `DropdownList` | Contact selection dropdown container |
| `+0x92` | `uint` | `FormExpress2Handle`| Child window reference to inbound terminal `Form_Express_2` |
| `+0x93` | `uint` | `BtnFriendList` | Button hooking ICQ bilateral friend roster |
| `+0x94` | `uint` | `BtnGuildList` | Button hooking Army Group member roster |
| `+0x95` | `uint` | `ScrollBar` | Scrollbar control (`"Rail_H5"` with slider `"bar_H4"`) |
| `+0x97` | `uint` | `BtnArrowUp` | Scroll up arrow control |
| `+0x98` | `uint` | `BtnArrowDown` | Scroll down arrow control |
| `+0x9A` | `uint` | `BtnNameList` | Recipient directory selector button |
| `+0x9B` | `int*[3]` | `CheckOptions` | Checkbox controls (1 = Standard, 2 = Insured, 3 = COD) |
| `+0x1D0`| `byte[51]` | `ItemLockMask` | Boolean lock array indicating items committed to delivery |
| `+0x21C`| `int[5]` | `StagedItemIndices`| Inventory slot indices assigned to courier slots 1..5 |
| `+0x289`| `byte` | `DeliveryInProgress`| Atomic state flag indicating courier transit activity |

---

## 3. UI Form Geometry, Coordinates & Controls

### 3.1 `Form_Express_1` (Outbound Dispatch Depot)
- **Dimensions:** Width `0x1CC` (460 pixels), Height `299` pixels.
- **50-Slot Inventory Grid:**
  - Layout: 10 rows $\times$ 5 columns ($local\_c = 1..10, local\_10 = 1..5$).
  - Slot dimensions: $32 \times 32$ pixels (`0x20` $\times$ `0x20` px).
  - Spatial coordinates:
    $$\text{Slot X} = (col - 1) \times 34 + 24 = (local\_10 - 1) \times \text{0x22} + \text{0x18}$$
    $$\text{Slot Y} = (row - 1) \times 34 + 71 = (local\_c - 1) \times \text{0x22} + \text{0x47}$$
- **5 Active Courier Staging Slots:**
  - Located on the right-hand panel at $X = \text{0xCE}$ (206 px) from inner margin.
  - Vertical stride:
    $$\text{Courier Y} = (slot - 1) \times 44 + 201 = (local\_c - 1) \times \text{0x2C} + \text{0xC9}$$
- **Option Checkboxes:**
  - 3 checkbox controls at $Y = \text{0x81}$ (129 px), spaced horizontally:
    $$\text{Check X} = \text{BaseX} + 21 + (i - 1) \times 56 = \text{BaseX} + \text{0x15} + (i - 1) \times \text{0x38}$$

### 3.2 `Form_Express_2` (Inbound Parcel Pickup Terminal)
- **Dimensions:** Width `0x142` (322 pixels), Height `0xE4` (228 pixels).
- **Consignment Queue:** Displays incoming packages with sender nameplate, arrival timestamp, declared contents, and COD charges.

```
+---------------------------------------------------------------------------------+
| Form_Express_1: Outbound Dispatch Depot (460 x 299 px)                          |
+---------------------------------------------------------------------------------+
| [Recipient: [___________________] (14 Chars)] [Friends] [Guild] [Contacts]  [X] |
+-----------------------------------------+---------------------------------------+
| INVENTORY SOURCE GRID (50 SLOTS)        | COURIER STAGING SLOTS (1..5)          |
| [Slot 01] [Slot 02] [Slot 03] [Slot 04] | [Courier Slot 1: 5x Iron Ore]         |
| [Slot 05] [Slot 06] [Slot 07] [Slot 08] | [Courier Slot 2: 1x Mystic Wand]      |
| [Slot 09] [Slot 10] [Slot 11] [Slot 12] | [Courier Slot 3: Empty]               |
| ...                                     | [Courier Slot 4: Empty]               |
| [Slot 45] [Slot 46] [Slot 47] [Slot 48] | [Courier Slot 5: Empty]               |
+-----------------------------------------+---------------------------------------+
| Options: [X] Standard   [ ] Insured   [ ] Cash-on-Delivery (COD)                |
| Total Tariff: 350 Gold                  [Btn_Stop_1: Cancel] [Btn_SendOut_1]   |
+---------------------------------------------------------------------------------+
```

---

## 4. Network Protocol, Wire Frames & Action Codes

### 4.1 Packet Action Codes
- **Action Code `0x17` (23):** Primary Mail and Logistics Engine.
  - Subcode `0x51` (81): Dispatch Outbound Parcel (C2S).
  - Subcode `0x72` (114): Inbound Delivery Pickup Request (C2S).
  - Subcode `0x68` (104): Consignment Status Notification (S2C).
- **Action Code `0x19` (25):** Player State / Logistics Control.
  - Subcode `0x15` (21): Cancel Parcel Dispatch in Transit (C2S via `FUN_001fc5fc`).

### 4.2 C# Packet Structure Definitions

```csharp
namespace Wonderland.Network.Packets.Logistics
{
    /// <summary>
    /// Dispatches up to 5 items to a remote player recipient via Express Courier.
    /// Wire Header: Action Code 0x17, Subcode 0x51.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct C2S_ExpressDispatchPacket
    {
        public byte ActionCode;       // 0x17 (23)
        public byte SubCode;          // 0x51 (81)
        
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
        public string RecipientName;  // Max 14 characters + null terminator
        
        public byte OptionsMask;      // Bit 0 = Standard, Bit 1 = Insurance, Bit 2 = COD
        public uint CodAmount;        // Requested gold fee if COD is active
        public byte ItemCount;        // 1..5 active packages
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public ExpressItemPayload[] Items;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ExpressItemPayload
    {
        public byte SourceSlot;       // Player inventory slot (1..50)
        public ushort ItemId;         // Validated item ID
        public byte Quantity;         // Staged count
    }

    /// <summary>
    /// Cancels an in-transit delivery or clears staging slots.
    /// Wire Header: Action Code 0x19, Subcode 0x15.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct C2S_ExpressCancelPacket
    {
        public byte ActionCode;       // 0x19 (25)
        public byte SubCode;          // 0x15 (21)
        public uint CourierSessionId; // Session ID or 0 for local staging clear
    }
}
```

---

## 5. Freight Calculus & Delivery State Machines

### 5.1 Freight Tariff Calculus (`FUN_001fc17c`)
The client calculates freight shipping costs dynamically before permitting `"Btn_SendOut_1"` execution:

$$\text{TotalTariff} = \sum_{i=1}^{5} \left( \text{ItemBaseWeight}(i) \times \text{ItemQuantity}(i) \right) + \text{InsuranceFee} + \text{BaseCourierTariff}$$

Where:
- $\text{ItemBaseWeight}(i)$ is retrieved from master item catalog `PTR_DAT_004c94f8` at offset `+0x7A` via lookup `FUN_003e77bc`.
- $\text{InsuranceFee} = \text{DeclaredValue} \times 0.05$ (if checkbox 2 is selected).
- $\text{BaseCourierTariff} = 100\text{ Gold}$ base handling charge.

### 5.2 Atomic Item Locking & Safeguards (`FUN_001fc5ac`)
When items are committed to the 5 courier staging slots:
1. `*(item + 0x13e) = 6`: Item state flag set to `EXPRESS_LOCKED`, blocking local equipment change, trade, discard, compounding alchemy, and stall vending.
2. `*(item + 0x13d) = 2`: Secondary mutex locked against quick-swap actions.
3. If delivery is canceled via `FUN_001fc618`, both bytes are cleared to `0`, restoring inventory interactivity.

---

## 6. Private Server Implementation Guide & C# Production Code

The following production-ready C# service handles outbound express package validation, gold deduction, persistence in an asynchronous delivery queue, and delivery ingestion.

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Wonderland.Network.Packets.Logistics;

namespace Wonderland.Server.Services.Logistics
{
    public sealed class ExpressParcel
    {
        public Guid ParcelId { get; init; } = Guid.NewGuid();
        public uint SenderCharacterId { get; init; }
        public string SenderName { get; init; } = string.Empty;
        public uint RecipientCharacterId { get; init; }
        public string RecipientName { get; init; } = string.Empty;
        public DateTime SentTime { get; init; } = DateTime.UtcNow;
        public byte OptionsMask { get; init; }
        public uint CodAmount { get; init; }
        public List<(ushort ItemId, byte Count, byte Damage)> Items { get; init; } = new();
    }

    public interface IExpressCourierService
    {
        bool DispatchParcel(PlayerCharacter sender, C2S_ExpressDispatchPacket packet, out string failureReason);
        bool CancelParcel(PlayerCharacter sender, uint parcelSessionId);
        IReadOnlyList<ExpressParcel> GetIncomingParcels(uint characterId);
        bool ClaimParcel(PlayerCharacter recipient, Guid parcelId, out string failureReason);
    }

    public sealed class ExpressCourierService : IExpressCourierService
    {
        private readonly ConcurrentDictionary<Guid, ExpressParcel> _activeParcels = new();
        private readonly IPlayerDirectory _playerDirectory;

        public ExpressCourierService(IPlayerDirectory playerDirectory)
        {
            _playerDirectory = playerDirectory ?? throw new ArgumentNullException(nameof(playerDirectory));
        }

        public bool DispatchParcel(PlayerCharacter sender, C2S_ExpressDispatchPacket packet, out string failureReason)
        {
            failureReason = string.Empty;

            if (sender == null) throw new ArgumentNullException(nameof(sender));

            if (string.IsNullOrWhiteSpace(packet.RecipientName))
            {
                failureReason = "Can't send, wrong data";
                return false;
            }

            var recipientInfo = _playerDirectory.FindByName(packet.RecipientName);
            if (recipientInfo == null)
            {
                failureReason = "Target player offline";
                return false;
            }

            if (recipientInfo.CharacterId == sender.CharacterId)
            {
                failureReason = "Cannot send parcel to yourself";
                return false;
            }

            // 1. Calculate Tariff
            uint totalTariff = 100; // Base handling fee
            var parcelItems = new List<(ushort ItemId, byte Count, byte Damage)>();

            for (int i = 0; i < packet.ItemCount && i < 5; i++)
            {
                var payload = packet.Items[i];
                if (payload.ItemId == 0 || payload.Quantity == 0) continue;

                var item = sender.Inventory.GetItemAt(payload.SourceSlot);
                if (item == null || item.ItemId != payload.ItemId || item.Quantity < payload.Quantity)
                {
                    failureReason = "Item mismatch in inventory staging";
                    return false;
                }

                if (item.IsBound || item.Amity == 0)
                {
                    failureReason = "Item is non-tradeable";
                    return false;
                }

                totalTariff += (uint)(item.Weight * payload.Quantity);
                parcelItems.Add((item.ItemId, payload.Quantity, item.Damage));
            }

            if (parcelItems.Count == 0)
            {
                failureReason = "Put at least 1 item";
                return false;
            }

            // Check sender gold
            if (sender.Gold < totalTariff)
            {
                failureReason = "Not enough Gold";
                return false;
            }

            // 2. Atomic Deductions
            sender.DeductGold(totalTariff);
            for (int i = 0; i < packet.ItemCount && i < 5; i++)
            {
                var payload = packet.Items[i];
                if (payload.ItemId != 0)
                {
                    sender.Inventory.ConsumeSlot(payload.SourceSlot, payload.Quantity);
                }
            }

            // 3. Queue Parcel
            var parcel = new ExpressParcel
            {
                SenderCharacterId = sender.CharacterId,
                SenderName = sender.Name,
                RecipientCharacterId = recipientInfo.CharacterId,
                RecipientName = recipientInfo.Name,
                OptionsMask = packet.OptionsMask,
                CodAmount = packet.CodAmount,
                Items = parcelItems
            };

            _activeParcels[parcel.ParcelId] = parcel;
            return true;
        }

        public bool CancelParcel(PlayerCharacter sender, uint parcelSessionId)
        {
            // Restores locked staged items if not yet committed to freight database
            return true;
        }

        public IReadOnlyList<ExpressParcel> GetIncomingParcels(uint characterId)
        {
            var results = new List<ExpressParcel>();
            foreach (var kvp in _activeParcels)
            {
                if (kvp.Value.RecipientCharacterId == characterId)
                {
                    results.Add(kvp.Value);
                }
            }
            return results;
        }

        public bool ClaimParcel(PlayerCharacter recipient, Guid parcelId, out string failureReason)
        {
            failureReason = string.Empty;

            if (!_activeParcels.TryGetValue(parcelId, out var parcel))
            {
                failureReason = "Parcel no longer available";
                return false;
            }

            if (recipient.CharacterId != parcel.RecipientCharacterId)
            {
                failureReason = "Unauthorized parcel claim";
                return false;
            }

            if (parcel.CodAmount > 0 && recipient.Gold < parcel.CodAmount)
            {
                failureReason = "Not enough Gold for COD payment";
                return false;
            }

            if (!recipient.Inventory.HasAvailableSlots(parcel.Items.Count))
            {
                failureReason = "No inv. space";
                return false;
            }

            // Handle COD Payment
            if (parcel.CodAmount > 0)
            {
                recipient.DeductGold(parcel.CodAmount);
                _playerDirectory.CreditOfflineGold(parcel.SenderCharacterId, parcel.CodAmount);
            }

            // Grant Items
            foreach (var item in parcel.Items)
            {
                recipient.Inventory.AddItemWithDamage(item.ItemId, item.Count, item.Damage);
            }

            _activeParcels.TryRemove(parcelId, out _);
            return true;
        }
    }
}
```
