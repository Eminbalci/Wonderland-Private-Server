# Document 61: Activity Exchange, Token Redemption, and Merchant Broker System

## 1. Executive Summary & Engine Architecture

The Wonderland Online client incorporates a specialized merchant exchange subsystem dedicated to non-currency bartering, quest token turn-ins, and promotional item redemptions. Implemented within client routines `FUN_00383c10`, `FUN_00383fa4`, `FUN_003840a4`, and `FUN_003820ec`, this engine bridges static equipment/consumable vendors (`Form_EquipStore_1`, `Form_ThingStore_1`) with dynamic recipe-driven trading posts (`Form_ActivityExchange`).

Unlike standard NPC shops that transact via Gold coins (`Action Code 0x14:0x02`), the Activity Exchange system requires players to deposit specific combinations and quantities of prerequisite items (such as event badges, star tokens, monster pelts, or alchemical materials) in exchange for finished equipment, rare consumables, or vanity items. The exchange logic enforces strict inventory slot checks, verifies that ingredients meet non-damage criteria, and calculates item attributes (including `MaxHp`, `MaxSp`, and `Shoot RES` modifiers) directly during client rendering.

```
+------------------------------------------------------------------------------------+
|                         CLIENT ENGINE MERCHANT SUBSYSTEM                          |
+------------------------------------------------------------------------------------+
                                      |
         +----------------------------+----------------------------+
         | Mode 1: Equip Store        | Mode 2: Consumable Store   | Mode 3: Activity Exchange
         v                            v                            v
  [Form_EquipStore_1]          [Form_ThingStore_1]         [Form_ActivityExchange]
  - 10 Discrete Slots          - Stack Aggregation         - 65-Byte Recipe Records
  - Max Stack: 1               - Max Stack: 50             - Up to 5 Prerequisite Items
  - Durability Tooltips        - Quantity Sliders          - Multi-Ingredient Validation
         |                            |                            |
         +----------------------------+----------------------------+
                                      |
                                      v
                       [Network Client Dispatcher]
                    Action Code 0x19:0x2A / 0x14 Wire Frame
```

---

## 2. Memory Layouts & Data Structures

### 2.1 Exchange Recipe Database Record (`0x41` = 65 Bytes)
The master activity recipe catalog is loaded into global memory at `PTR_DAT_004c87f8` via `FUN_000134dc + *(int *)PTR_DAT_004c87f8 + 4`. Each recipe occupies a contiguous 65-byte stride indexed by recipe index $i$:

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `ushort` | `RecipeId` | Unique sequential identifier of the exchange recipe |
| `+0x02` | `ushort` | `OutputItemId` | Target item ID generated upon successful redemption |
| `+0x04` | `byte` | `OutputCount` | Number of output items awarded (default `1`) |
| `+0x05` | `byte` | `MinLevelReq` | Minimum player character level required to redeem |
| `+0x06` | `byte` | `Category` | Category filter flag (1 = Weapon, 2 = Armor, 3 = Consumable, 4 = Misc) |
| `+0x07` | `byte[22]` | `Reserved` | Engine flags, promotional period masks, server channel masks |
| `+0x1D` | `ushort[5]` | `InputItemIds` | Array of up to 5 prerequisite item IDs required for redemption |
| `+0x27` | `byte[2]` | `Padding` | 2-byte boundary alignment |
| `+0x29` | `byte[5]` | `InputCounts` | Array of required quantities corresponding to each `InputItemId` |
| `+0x2E` | `byte` | `IngredientCount`| Number of active ingredient slots utilized ($1 \le N \le 5$) |
| `+0x2F` | `uint` | `GoldTariff` | Supplementary gold fee required alongside items (if any) |
| `+0x33` | `byte[14]` | `Description` | Truncated display label or promotional badge identifier |

### 2.2 Activity Exchange Form State (`param_1`)
The client instance managing `Form_ActivityExchange` tracks state across the following member variables:

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x4C` | `uint` | `FormHandle` | DirectDraw window handle returned by `FUN_0047885c` |
| `+0x94` | `uint` | `PTR_DAT_004c8a94` | DirectDraw surface bitmap pool manager |
| `+0xE8` | `byte` | `StoreMode` | `1` = Equipment Store, `2` = Consumable Store, `3` = Activity Exchange |
| `+0xF0` | `int*` | `pContainer` | Pointer to display container holding item display controls |
| `+0xF4` | `int*[10]` | `ItemSlotControls` | Pointers to the 10 displayed recipe/item slot UI controls |
| `+0x120` | `byte[10]` | `SelectedFlags` | Selection bitmask for each of the 10 visible slots |
| `+0x12B` | `byte` | `SelectedCount` | Total number of currently selected recipe slots |
| `+0x140` | `uint` | `SelectedSlotIdx` | Index of the active selected recipe ($1..10$) |
| `+0x144` | `ushort` | `SelectedRecipeId`| ID of the selected exchange recipe |
| `+0x146` | `byte` | `SelectedTableIdx`| Index within the `0x41` byte global recipe catalog |
| `+0x148` | `int*[5]` | `IngredientIcons` | UI control pointers for rendering the 5 prerequisite ingredients |
| `+0x1C0` | `uint` | `ScrollUpBtn` | Button handle for scrolling recipe listing upward |
| `+0x1C4` | `uint` | `ScrollDownBtn` | Button handle for scrolling recipe listing downward |
| `+0x1D0` | `int*[50]` | `InventoryGrid` | References to local character inventory items for ingredient matching |

---

## 3. UI Form Geometry, Coordinates, Sub-Components & Event Dispatchers

The merchant form is constructed dynamically in `FUN_00383c10`. Based on parameter `param_2`, the window switches its graphics asset and layout metrics:

```
+---------------------------------------------------------------------+
| Form_ActivityExchange (Width: 320 px, Height: 440 px)               |
+---------------------------------------------------------------------+
| [Header Title: Activity Exchange / Event Redemption]            [X] |
+---------------------------------------------------------------------+
| [Recipe Slot 1]  Target Item Name               [Cost: 5x Badges]   |
| [Recipe Slot 2]  Target Item Name               [Cost: 2x Stars]    |
| [Recipe Slot 3]  Target Item Name               [Cost: 10x Pelts]   |
| ...                                                                 |
| [Recipe Slot 10] Target Item Name               [Cost: 1x Core]     |
+---------------------------------------------------------------------+
| Ingredient Requirements Panel:                                      |
| +----------+ +----------+ +----------+ +----------+ +----------+   |
| | Req #1   | | Req #2   | | Req #3   | | Req #4   | | Req #5   |   |
| | Count: 5 | | Count: 2 | | Count: 0 | | Count: 0 | | Count: 0 |   |
| +----------+ +----------+ +----------+ +----------+ +----------+   |
+---------------------------------------------------------------------+
| Gold Fee: 0 Gold                                [Btn_Exchange_1]    |
+---------------------------------------------------------------------+
```

### 3.1 Sub-Component Specifications
1. **Window Background:**
   - Evaluates `StoreMode` at `param_1 + 0xe8`:
     - Mode 1: Calls `FUN_0047885c(..., "Form_EquipStore_1")`
     - Mode 2: Calls `FUN_0047885c(..., "Form_ThingStore_1")`
     - Mode 3: Calls `FUN_0047885c(..., "Form_ActivityExchange")`
2. **List Controls (10 Slots):**
   - 10 recipe slots (`iVar2 = 1..10`), spaced with vertical stride `0x22` (34 pixels):
     $$\text{Y-Coord} = (i - 1) \times 34 + 34 = (i - 1) \times \text{0x22} + \text{0x22}$$
3. **Action Button:**
   - Modes 1 & 2: Sub-control `"Btn_Buy_1"` positioned at $(X=136, Y=56)$, dimensions $20 \times 56$ px.
   - Mode 3: Sub-control `"Btn_Exchange_1"` positioned at $(X=136, Y=56)$, dimensions $20 \times 56$ px.
4. **Prerequisite Ingredient Matrix:**
   - 5 discrete ingredient icon blocks at `param_1 + 0x148 + i * 4` ($i = 1..5$):
     - Displays prerequisite item bitmap via `FUN_003e77bc(PTR_DAT_004c94f8, ItemId)`.
     - Renders required vs possessed quantity counters (`FUN_003840fc` and `FUN_003840a4`).
     - Items lacking required quantities are drawn with an alpha-blended red overlay (`0x800000`).

---

## 4. Network Protocol, Wire Frames & Action Codes

When the player confirms a transaction via `"Btn_Exchange_1"`, the client verifies local quantities before assembling the packet.

### 4.1 Packet Framing
- **Opcode Family:** Action Code `0x19` (`25`) with Subcode `0x2A` (`42`) for Event Token Exchanges, or Action Code `0x14` (`20`) for NPC vendor buy requests.
- **Client Request (C2S):** `[0x19, 0x2A, RecipeId: ushort, Quantity: byte, SlotIndices: byte[5]]`
- **Server Response (S2C):** `[0x19, 0x2A, Status: byte, OutputItemId: ushort, OutputCount: byte]`

### 4.2 C# Network Packet Structures

```csharp
namespace Wonderland.Network.Packets.Merchant
{
    /// <summary>
    /// Client-to-server request to exchange items or event tokens via Activity Exchange.
    /// Wire Header: Action Code 0x19, Subcode 0x2A.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct C2S_ActivityExchangeRequest
    {
        public byte ActionCode;       // 0x19 (25)
        public byte SubCode;          // 0x2A (42)
        public ushort RecipeId;       // Target Recipe ID from master catalog
        public byte Quantity;         // Number of redemption iterations (usually 1)
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public byte[] InventorySlots; // Exact inventory slot indices containing the consumed items
    }

    /// <summary>
    /// Server-to-client response communicating activity exchange redemption result.
    /// Wire Header: Action Code 0x19, Subcode 0x2A.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct S2C_ActivityExchangeResponse
    {
        public byte ActionCode;       // 0x19 (25)
        public byte SubCode;          // 0x2A (42)
        public byte ResultCode;       // 0 = Success, 1 = Insufficient Items, 2 = Inventory Full, 3 = Invalid Level
        public ushort OutputItemId;   // Item ID granted to the character
        public byte OutputCount;      // Quantity granted
        public uint RemainingGold;    // Character gold balance after deducting any tariff
    }
}
```

---

## 5. State Machines, Validation Safeguards & Algorithmic Calculus

```
+---------------------------------------------------------------------------------+
|                       EXCHANGE VERIFICATION STATE MACHINE                       |
+---------------------------------------------------------------------------------+
                                         |
                                         v
                         [Player Clicks "Btn_Exchange_1"]
                                         |
                                         v
                     +---------------------------------------+
                     | Level Check: Character LV >= MinLevel |
                     +---------------------------------------+
                                    |         |
                                [Pass]       [Fail] --> "Insufficient level"
                                    v
                     +---------------------------------------+
                     | Gold Check: Gold Purse >= GoldTariff  |
                     +---------------------------------------+
                                    |         |
                                [Pass]       [Fail] --> "Not enough Gold"
                                    v
                     +---------------------------------------+
                     | Ingredient Check: 5 Slots Possessed?  |
                     +---------------------------------------+
                                    |         |
                                [Pass]       [Fail] --> Red Highlight / Abort
                                    v
                     +---------------------------------------+
                     | Free Space Check: Slots >= ReqSlots   |
                     +---------------------------------------+
                                    |         |
                                [Pass]       [Fail] --> "No inv. space"
                                    v
                     +---------------------------------------+
                     | Transmit Action Code 0x19:0x2A Packet |
                     +---------------------------------------+
```

### 5.1 Attribute Recalculation Calculus (`FUN_003820ec`)
When browsing items in the exchange store, equipment stat bonuses are extracted dynamically:
- **Stat Modifier Codes:**
  - `0x19`: STR Bonus
  - `0x1A`: CON Bonus
  - `0x65`: INT Bonus
  - `0x66`: WIS Bonus
  - `0xCF`: Max HP Addition (`"MaxHp"`)
  - `0xD0`: Max SP Addition (`"MaxSp"`)
  - `100`: Ranged Resistance Percentage (`"Shoot RES"`)
- **Delta Calculus:**
  - Values $< 100$ represent negative adjustments: $\Delta = 100 - \text{val}$
  - Values $\ge 100$ represent positive enhancements: $\Delta = \text{val} - 100$

---

## 6. Private Server Implementation Guide & C# Production Code

The following modular C# service implements the complete Activity Exchange engine, executing recipe matching, atomic ingredient consumption, and reward granting.

```csharp
using System;
using System.Collections.Generic;
using Wonderland.Network.Packets.Merchant;

namespace Wonderland.Server.Services.Merchant
{
    public sealed class ExchangeRecipe
    {
        public ushort RecipeId { get; init; }
        public ushort OutputItemId { get; init; }
        public byte OutputCount { get; init; }
        public byte MinLevel { get; init; }
        public uint GoldTariff { get; init; }
        public (ushort ItemId, byte Count)[] Ingredients { get; init; } = Array.Empty<(ushort, byte)>();
    }

    public interface IActivityExchangeService
    {
        bool RegisterRecipe(ExchangeRecipe recipe);
        S2C_ActivityExchangeResponse ProcessExchange(PlayerCharacter character, C2S_ActivityExchangeRequest request);
    }

    public sealed class ActivityExchangeService : IActivityExchangeService
    {
        private readonly Dictionary<ushort, ExchangeRecipe> _recipes = new();

        public bool RegisterRecipe(ExchangeRecipe recipe)
        {
            if (recipe == null || _recipes.ContainsKey(recipe.RecipeId))
                return false;

            _recipes[recipe.RecipeId] = recipe;
            return true;
        }

        public S2C_ActivityExchangeResponse ProcessExchange(PlayerCharacter character, C2S_ActivityExchangeRequest request)
        {
            if (character == null)
                throw new ArgumentNullException(nameof(character));

            if (!_recipes.TryGetValue(request.RecipeId, out var recipe))
            {
                return new S2C_ActivityExchangeResponse
                {
                    ActionCode = 0x19,
                    SubCode = 0x2A,
                    ResultCode = 1 // Invalid recipe
                };
            }

            // 1. Level Verification
            if (character.Level < recipe.MinLevel)
            {
                return new S2C_ActivityExchangeResponse
                {
                    ActionCode = 0x19,
                    SubCode = 0x2A,
                    ResultCode = 3 // Insufficient Level
                };
            }

            // 2. Gold Tariff Verification
            if (character.Gold < recipe.GoldTariff)
            {
                return new S2C_ActivityExchangeResponse
                {
                    ActionCode = 0x19,
                    SubCode = 0x2A,
                    ResultCode = 1 // Not enough Gold
                };
            }

            // 3. Ingredient Verification
            foreach (var (itemId, requiredCount) in recipe.Ingredients)
            {
                if (itemId == 0 || requiredCount == 0) continue;
                
                int possessed = character.Inventory.GetTotalQuantity(itemId);
                if (possessed < requiredCount * request.Quantity)
                {
                    return new S2C_ActivityExchangeResponse
                    {
                        ActionCode = 0x19,
                        SubCode = 0x2A,
                        ResultCode = 1 // Missing required ingredients
                    };
                }
            }

            // 4. Free Inventory Space Verification
            if (!character.Inventory.HasAvailableSlots(1))
            {
                return new S2C_ActivityExchangeResponse
                {
                    ActionCode = 0x19,
                    SubCode = 0x2A,
                    ResultCode = 2 // Inventory full
                };
            }

            // 5. Execute Atomic Deduction
            if (recipe.GoldTariff > 0)
            {
                character.DeductGold(recipe.GoldTariff);
            }

            foreach (var (itemId, requiredCount) in recipe.Ingredients)
            {
                if (itemId == 0 || requiredCount == 0) continue;
                character.Inventory.ConsumeItem(itemId, (byte)(requiredCount * request.Quantity));
            }

            // 6. Grant Reward Item
            byte grantAmount = (byte)(recipe.OutputCount * (request.Quantity == 0 ? 1 : request.Quantity));
            character.Inventory.AddItem(recipe.OutputItemId, grantAmount);

            return new S2C_ActivityExchangeResponse
            {
                ActionCode = 0x19,
                SubCode = 0x2A,
                ResultCode = 0, // Success
                OutputItemId = recipe.OutputItemId,
                OutputCount = grantAmount,
                RemainingGold = character.Gold
            };
        }
    }
}
```
