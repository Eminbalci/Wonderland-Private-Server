# Document 63: Tent Cupboard, Furniture Storage, and Housing Vault Engine

## 1. Executive Summary & Engine Architecture

Wonderland Online features a personalized player housing system centered on the player's portable Tent (`Action Code 0x3E`). Within the tent interior, furniture pieces are deployed onto a 2-floor isometric tile grid (`Floor 1`, `Floor 2`). Among these decorative and functional items, storage containers—specifically cabinets and cupboards—provide essential off-inventory vault capacity. Reverse-engineered from client subroutines `FUN_002099e0`, `FUN_00209ea8`, `FUN_00209ef0`, `FUN_0020a088`, and `FUN_0020a594`, this engine manages container opening, visual grid layouts, multi-attribute item serialization, and client-server synchronization.

The client distinguishes between two container classes:
1. **`Form_Cupboard_1` (Small Cabinet):** Dimensions $155 \times 209$ px (`0x9B` $\times$ `0xD1` px), offering 5 storage slots.
2. **`Form_Cupboard_2` (Large Wardrobe):** Dimensions $179 \times 209$ px (`0xB3` $\times$ `0xD1` px), offering 10 storage slots arranged in two 5-slot rows.

Storage state is maintained within the tent housing memory structure at `PTR_DAT_004c9900`. Individual items stored within cupboard furniture are serialized as 53-byte (`0x35`) binary records that preserve item durability, socketed gems, potential enhancement tiers, and elemental chemistry affinities.

```
+------------------------------------------------------------------------------------+
|                       TENT FURNITURE STORAGE ENGINE ARCHITECTURE                   |
+------------------------------------------------------------------------------------+
                                      |
         +----------------------------+----------------------------+
         | Tier 1: Small Cupboard (5 Slots)                        | Tier 2: Large Cupboard (10 Slots)
         v                                                         v
  [Form_Cupboard_1] (155x209 px)                            [Form_Cupboard_2] (179x209 px)
  - 1 Row x 5 Slots                                         - 2 Rows x 5 Slots
  - Slots 6..10 Disabled                                    - Slots 1..10 Active
  - Position: Y = 73 px                                     - Position: Y = 64 px
         |                                                         |
         +----------------------------+----------------------------+
                                      |
                                      v
                 [Furniture Record Heap: PTR_DAT_004c9900]
                  53-Byte (0x35) Record Stride per Stored Item
                                      |
                                      v
               [Network Wire Frame: Action Code 0x3E (62)]
          Subcodes: 0x1F (Query), 0x29 (Deposit), 0x2B (Withdraw)
```

---

## 2. Memory Layouts & Data Structures

### 2.1 Stored Item Record (`0x35` = 53 Bytes)
Each item stored within a cupboard occupies a contiguous 53-byte block starting at base offset `0xC63` in `PTR_DAT_004c9900`:
$$\text{Memory Address} = \text{PTR\_DAT\_004c9900} + \text{0xC63} + (\text{SlotIndex} \times \text{0x35})$$

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `byte` | `OccupiedFlag` | `0` = Empty slot, `1` = Occupied slot |
| `+0x01` | `ushort` | `ItemId` | 16-bit Master Item Identifier |
| `+0x03` | `byte` | `Quantity` | Stored item count (1..50 for stackables) |
| `+0x04` | `byte` | `DamageValue` | Durability loss indicator (0 = Mint, 255 = Broken) |
| `+0x05` | `byte` | `GradeTier` | Refinement / Potential grade ($+1..+10$) |
| `+0x06` | `byte` | `ElementAffinity` | Elemental enchantment (Earth, Water, Fire, Wind) |
| `+0x07` | `short` | `AttrMod1` | Primary attribute modifier (STR, CON, INT, WIS, AGI) |
| `+0x09` | `short` | `AttrMod2` | Secondary attribute modifier |
| `+0x0B` | `uint` | `SocketGems` | Bitfield representing socketed jewels / diamonds |
| `+0x0F` | `uint` | `Timestamp` | Unix timestamp of when item was deposited |
| `+0x13` | `byte[34]`| `Reserved` | Reserved engine metadata, creator signatures |

### 2.2 Cupboard Form Control Layout (`param_1`)
The UI instance managing `Form_Cupboard_1` / `Form_Cupboard_2` contains the following fields:

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `void*` | `VTable` | Delphi component virtual method table pointer |
| `+0x04` | `uint` | `FormHandle` | DirectDraw surface bitmap handle |
| `+0x3A` | `byte` | `CupboardTier` | `1` = 5-Slot Cupboard, `2` = 10-Slot Cupboard |
| `+0x3C` | `int*` | `pTitleLabel` | Title caption control (`"Cupboard"`) |
| `+0x3D` | `int*[10]` | `SlotControls` | Pointers to the 10 item slot display buttons |
| `+0xEA` | `ushort` | `FurnitureGuid` | Unique entity ID of the target tent furniture piece |
| `+0xF4` | `int*[10]` | `ItemRenderers`| DirectDraw rendering interfaces for item icons |
| `+0x120`| `uint` | `LockMutex` | Thread synchronization primitive for slot drag-and-drop |

---

## 3. UI Form Geometry, Coordinates & Grid Mechanics

```
+-------------------------------------------------------+
| Form_Cupboard_2: 10-Slot Large Wardrobe (179 x 209 px)|
+-------------------------------------------------------+
| [Header Title: Cupboard Vault]                    [X] |
+-------------------------------------------------------+
| Row 1: Y = 64 px                                      |
| +----+   +----+   +----+   +----+   +----+            |
| |S 01|   |S 02|   |S 03|   |S 04|   |S 05|            |
| +----+   +----+   +----+   +----+   +----+            |
|                                                       |
| Row 2: Y = 98 px (Disabled in Form_Cupboard_1)        |
| +----+   +----+   +----+   +----+   +----+            |
| |S 06|   |S 07|   |S 08|   |S 09|   |S 10|            |
| +----+   +----+   +----+   +----+   +----+            |
+-------------------------------------------------------+
| [Status: Stored 7/10 Items]       [Close Button: 0x4B]|
+-------------------------------------------------------+
```

### 3.1 Spatial Grid Calculus
The 10 item slots are positioned according to index $i$ ($1 \le i \le 10$):
- **Horizontal (X) Coordinate:**
  $$\text{Slot X} = ((i - 1) \bmod 5) \times 34 + 18 = ((i - 1) \bmod 5) \times \text{0x22} + \text{0x12}$$
- **Vertical (Y) Coordinate:**
  - For `Form_Cupboard_1` (5 Slots):
    $$\text{Slot Y} = \lfloor (i - 1) / 5 \rfloor \times 34 + 73 = \lfloor (i - 1) / 5 \rfloor \times \text{0x22} + \text{0x49}$$
    *(Slots 6..10 are hidden by invoking `FUN_00480670(control, 0)`)*
  - For `Form_Cupboard_2` (10 Slots):
    $$\text{Slot Y} = \lfloor (i - 1) / 5 \rfloor \times 34 + 64 = \lfloor (i - 1) / 5 \rfloor \times \text{0x22} + \text{0x40}$$
    *(All 10 slots visible via `FUN_00480670(control, 1)`)*

---

## 4. Network Protocol, Wire Frames & Action Codes

### 4.1 Packet Framing
All furniture container interactions use Action Code `0x3E` (62 decimal):
- **Opcode:** `0x3E`
- **Subcodes:**
  - `0x1F` (31): Query Container Contents (C2S)
  - `0x29` (41): Deposit Item into Cupboard (C2S via `FUN_0020a088`)
  - `0x2B` (43): Withdraw Item from Cupboard (C2S)
  - `0x30` (48): Container Sync Payload (S2C)

### 4.2 C# Packet Structure Definitions

```csharp
namespace Wonderland.Network.Packets.Housing
{
    /// <summary>
    /// Client-to-server request to deposit an inventory item into a tent cupboard slot.
    /// Wire Header: Action Code 0x3E, Subcode 0x29.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct C2S_CupboardDepositPacket
    {
        public byte ActionCode;       // 0x3E (62)
        public byte SubCode;          // 0x29 (41)
        public byte Padding;          // 0x00
        public byte CupboardSlot;     // Target slot in cupboard (1..10)
        public byte InventorySlot;    // Source slot in inventory (1..50)
        public ushort FurnitureGuid;  // Unique ID of the cupboard furniture piece in the tent
    }

    /// <summary>
    /// Client-to-server request to withdraw an item from a cupboard back into player inventory.
    /// Wire Header: Action Code 0x3E, Subcode 0x2B.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct C2S_CupboardWithdrawPacket
    {
        public byte ActionCode;       // 0x3E (62)
        public byte SubCode;          // 0x2B (43)
        public byte Padding;          // 0x00
        public byte CupboardSlot;     // Source slot in cupboard (1..10)
        public byte TargetInvSlot;    // Target inventory slot (1..50)
        public ushort FurnitureGuid;  // Unique ID of the cupboard furniture piece
    }

    /// <summary>
    /// Server-to-client response transmitting full contents of a cupboard upon opening.
    /// Wire Header: Action Code 0x3E, Subcode 0x30.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct S2C_CupboardSyncPacket
    {
        public byte ActionCode;       // 0x3E (62)
        public byte SubCode;          // 0x30 (48)
        public ushort FurnitureGuid;  // Cupboard furniture entity ID
        public byte MaxSlots;         // 5 or 10
        public byte ItemCount;        // Number of non-empty slots
        // Trailing: StoredItemWireRecord[ItemCount]
    }
}
```

---

## 5. Free Slot Scanner & Capacity Validation Safeguards

In subroutine `FUN_00209ea8`, the engine scans stored items within the tent instance:
```c
int FUN_00209ea8(int param_1)
{
  uint uVar1;
  int iVar2;
  
  uVar1 = FUN_003fc338(*(undefined4 *)PTR_DAT_004c9900, *(undefined2 *)(param_1 + 0xea));
  iVar2 = 1;
  do {
    if (*(char *)(*(int *)(*(int *)PTR_DAT_004c9900 + 8 + (uVar1 & 0xffff) * 4) + 0x106 + iVar2) == '\0') {
      return iVar2; // First available free slot
    }
    iVar2 = iVar2 + 1;
  } while (iVar2 != 0xb);
  return 0; // Container completely full
}
```

If `FUN_00209ea8` returns `0`, `FUN_0020a088` aborts transmission and triggers the localized error modal:
```c
(**(code **)(**(int **)PTR_DAT_004c8d40 + 0x9c))
    (*(int **)PTR_DAT_004c8d40, "Full, can\'t store more", 2000, 0, 0);
```

---

## 6. Private Server Implementation Guide & C# Production Code

The following service handles cupboard vault operations, capacity management, item durability retention, and synchronization for Wonderland Online private servers.

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Wonderland.Network.Packets.Housing;

namespace Wonderland.Server.Services.Housing
{
    public sealed class StoredFurnitureItem
    {
        public byte SlotIndex { get; set; }
        public ushort ItemId { get; set; }
        public byte Quantity { get; set; }
        public byte Damage { get; set; }
        public byte Grade { get; set; }
        public byte Element { get; set; }
        public uint Sockets { get; set; }
    }

    public sealed class TentCupboardInstance
    {
        public ushort FurnitureGuid { get; init; }
        public byte MaxCapacity { get; init; } // 5 or 10
        public ConcurrentDictionary<byte, StoredFurnitureItem> Items { get; } = new();

        public bool IsFull => Items.Count >= MaxCapacity;

        public byte GetFirstAvailableSlot()
        {
            for (byte slot = 1; slot <= MaxCapacity; slot++)
            {
                if (!Items.ContainsKey(slot))
                    return slot;
            }
            return 0;
        }
    }

    public interface ITentStorageService
    {
        bool DepositItem(PlayerCharacter character, C2S_CupboardDepositPacket packet, out string errorMessage);
        bool WithdrawItem(PlayerCharacter character, C2S_CupboardWithdrawPacket packet, out string errorMessage);
        TentCupboardInstance GetCupboard(uint characterId, ushort furnitureGuid);
    }

    public sealed class TentStorageService : ITentStorageService
    {
        private readonly ConcurrentDictionary<(uint, ushort), TentCupboardInstance> _cupboards = new();

        public TentCupboardInstance GetCupboard(uint characterId, ushort furnitureGuid)
        {
            return _cupboards.GetOrAdd((characterId, furnitureGuid), key => new TentCupboardInstance
            {
                FurnitureGuid = furnitureGuid,
                MaxCapacity = 10 // Default to large wardrobe; configured via item templates
            });
        }

        public bool DepositItem(PlayerCharacter character, C2S_CupboardDepositPacket packet, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (character == null) throw new ArgumentNullException(nameof(character));

            var cupboard = GetCupboard(character.CharacterId, packet.FurnitureGuid);
            if (cupboard.IsFull)
            {
                errorMessage = "Full, can't store more";
                return false;
            }

            var inventoryItem = character.Inventory.GetItemAt(packet.InventorySlot);
            if (inventoryItem == null)
            {
                errorMessage = "Invalid inventory item";
                return false;
            }

            byte targetSlot = packet.CupboardSlot;
            if (targetSlot == 0 || targetSlot > cupboard.MaxCapacity || cupboard.Items.ContainsKey(targetSlot))
            {
                targetSlot = cupboard.GetFirstAvailableSlot();
                if (targetSlot == 0)
                {
                    errorMessage = "Full, can't store more";
                    return false;
                }
            }

            // Transfer from inventory into cupboard
            var storedItem = new StoredFurnitureItem
            {
                SlotIndex = targetSlot,
                ItemId = inventoryItem.ItemId,
                Quantity = inventoryItem.Quantity,
                Damage = inventoryItem.Damage,
                Grade = inventoryItem.Grade,
                Element = inventoryItem.Element,
                Sockets = inventoryItem.Sockets
            };

            character.Inventory.RemoveItemAt(packet.InventorySlot);
            cupboard.Items[targetSlot] = storedItem;

            return true;
        }

        public bool WithdrawItem(PlayerCharacter character, C2S_CupboardWithdrawPacket packet, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (character == null) throw new ArgumentNullException(nameof(character));

            var cupboard = GetCupboard(character.CharacterId, packet.FurnitureGuid);
            if (!cupboard.Items.TryGetValue(packet.CupboardSlot, out var storedItem))
            {
                errorMessage = "Slot is empty";
                return false;
            }

            if (!character.Inventory.HasAvailableSlots(1))
            {
                errorMessage = "No inv. space";
                return false;
            }

            // Ingest back into inventory
            character.Inventory.AddItemWithProperties(
                storedItem.ItemId,
                storedItem.Quantity,
                storedItem.Damage,
                storedItem.Grade,
                storedItem.Element,
                storedItem.Sockets
            );

            cupboard.Items.TryRemove(packet.CupboardSlot, out _);
            return true;
        }
    }
}
```
