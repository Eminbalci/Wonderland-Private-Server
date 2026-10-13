# Document 65: Hotkey Macro Dock and Action Shortcut Runtime Engine

## 1. Executive Summary & Engine Architecture

In fast-paced MMORPG gameplay, rapid item consumption, skill invocation, and vehicle mounting rely on an optimized hotkey shortcut bar. The Wonderland Online client features a multi-orientation, draggable shortcut docking system managed across Delphi units `se_HotKey.pas` and `Kse_HotKey.pas`. Dissected from client subroutines `FUN_002a9124`, `FUN_002a965c`, `FUN_002a9b88`, and `FUN_002aa544`, this engine provides 8 primary action slots (bound to keyboard keys F1 through F8) distributed across multi-page hotkey registries.

The hotkey engine supports three dynamic presentation modes:
1. **Vertical Dock (`Form_HotKey_V`):** $56 \times 274$ px (`0x38` $\times$ `0x112` px), optimal for sidebar positioning.
2. **Horizontal Dock (`Form_HotKey_H`):** $274 \times 56$ px (`0x112` $\times$ `0x38` px), optimal for screen-bottom anchoring.
3. **Minimized Dock (`Form_HotKey_Mini`):** Collapsed compact button docked beneath the minimap or screen edge.

The hotkey runtime tracks item slot bindings, inventory quantity tallies, skill cooldown timers, and combat state context registers (`*(LocalPlayer + 0x1F16)`). When an F-key is pressed, the client evaluates whether the character is roaming ($0$) or engaged in turn-based combat ($4$), appropriately dispatching either an overworld usage packet (`Action Code 0x17`) or a battle action command (`Action Code 0x08`).

```
+------------------------------------------------------------------------------------+
|                         HOTKEY RUNTIME ENGINE ARCHITECTURE                         |
+------------------------------------------------------------------------------------+
                                      |
         +----------------------------+----------------------------+
         | Mode 1: Form_HotKey_V                                   | Mode 2: Form_HotKey_H
         v                                                         v
  [Vertical Shortcut Dock] (56x274 px)                      [Horizontal Shortcut Dock] (274x56 px)
  - 8 Slots (F1..F8) at Y-Stride 27 px                      - 8 Slots (F1..F8) at X-Stride 27 px
  - Minimize Button: Y = 37 px                              - Minimize Button: X = 255 px
  - Pagination: Btn_ArrowL_3 / Btn_ArrowR_3                 - Pagination: Btn_ArrowUp_2 / Btn_ArrowDn_2
         |                                                         |
         +----------------------------+----------------------------+
                                      | Mode 3: Mode Toggle (FUN_002a965c)
                                      v
                        [Form_HotKey_Mini] (Docked)
                                      |
                                      v
                      [Key Event Dispatcher: FUN_002aa544]
                     Interception of Keys VK_F1..VK_F8
                                      |
                  +-------------------+-------------------+
                  |                                       |
                  v (Roaming: State == 0)                 v (Combat: State == 4)
          [Action Code 0x17]                     [Action Code 0x08]
       Consumable / Vehicle Mount              Turn-Based Skill / Potion
```

---

## 2. Memory Layouts & Data Structures

### 2.1 Hotkey Slot Binding Record (`0x10` = 16 Bytes)
Each of the 8 hotkey slots stores a 16-byte binding structure:

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `byte` | `BindingType` | `0` = Empty, `1` = Inventory Item, `2` = Skill/Spell, `3` = Vehicle, `4` = Pet Command |
| `+0x01` | `byte` | `SourceInventorySlot`| For items: Index of item in character inventory ($1..50$) |
| `+0x02` | `ushort` | `TargetId` | Master Item ID or Skill/Spell ID |
| `+0x04` | `ushort` | `CachedQuantity` | Current possessed count in character inventory |
| `+0x06` | `byte` | `CooldownRemaining`| Ticks / turns remaining before slot can be triggered again |
| `+0x07` | `byte` | `PageNumber` | Hotkey page index ($1..4$) |
| `+0x08` | `uint` | `IconSurfaceHandle` | DirectDraw cached surface handle for fast blitting |
| `+0x0C` | `uint` | `Flags` | Attribute flags (Bit 0 = Usable in battle, Bit 1 = Usable roaming) |

### 2.2 `Form_HotKey` Manager State (`param_1`)

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `void*` | `VTable` | Delphi component virtual method table |
| `+0x04` | `uint` | `FormHandle` | DirectDraw surface bitmap handle |
| `+0x18` | `int` | `PosX` | Screen horizontal anchor coordinate |
| `+0x1C` | `int` | `PosY` | Screen vertical anchor coordinate |
| `+0x55` | `int*[8]` | `SlotControls` | Array of 8 slot button controls (F1 through F8) |
| `+0x5E` | `uint` | `BtnPagePrev` | Page backward arrow button |
| `+0x5F` | `uint` | `BtnPageNext` | Page forward arrow button |
| `+0x60` | `byte` | `PreviousOrientation`| Cached layout orientation before minimization |
| `+0x67` | `uint` | `BtnMinimize` | Minimization / dock collapse toggle button |
| `+0x181`| `byte` | `CurrentOrientation` | `1` = Vertical, `2` = Horizontal, `3` = Minimized |

---

## 3. UI Form Geometry, Coordinates & Grid Mechanics

```
+---------------------------------------------------------------------------------+
| Form_HotKey_H: Horizontal Dock (274 x 56 px)                                    |
+---------------------------------------------------------------------------------+
| [<][>] [F1]   [F2]   [F3]   [F4]   [F5]   [F6]   [F7]   [F8]   [_] [Minimize] |
| P 1/4  [HP]   [SP]   [Fire] [Tele] [Tent] [Boat] [Res]  [Food]                  |
+---------------------------------------------------------------------------------+

+-------------------------------+
| Form_HotKey_V: (56 x 274 px)  |
+-------------------------------+
| [Minimize: _]  [<] P 1/4 [>]  |
+-------------------------------+
| [F1: HP Potion (x45)]         |
| [F2: SP Water (x12)]          |
| [F3: Fire Arrow (LV 10)]      |
| [F4: Teleport Scroll (x5)]    |
| [F5: Portable Tent]           |
| [F6: Steam Boat Mount]        |
| [F7: Revive Water (x2)]       |
| [F8: Roast Meat (x99)]        |
+-------------------------------+
```

### 3.1 Spatial Positioning Calculations (`FUN_002a965c`)
When toggling orientation modes, slot coordinates are recalculated dynamically:
1. **Vertical Orientation (`Form_HotKey_V`):**
   - Dimensions: Width $56$ px (`0x38`), Height $274$ px (`0x112`).
   - Slot coordinates for slot $i$ ($1 \le i \le 8$):
     $$\text{Slot X} = 23\text{ px } (\text{0x17})$$
     $$\text{Slot Y} = (i - 1) \times 27 + 29\text{ px} = (i - 1) \times \text{0x1B} + \text{0x1D}$$
   - Minimization button: $X = 37$ px (`0x25`).
   - Page arrows: `"Btn_ArrowL_3"` ($X=12$), `"Btn_ArrowR_3"` ($X=36$).
2. **Horizontal Orientation (`Form_HotKey_H`):**
   - Dimensions: Width $274$ px (`0x112`), Height $56$ px (`0x38`).
   - Slot coordinates for slot $i$ ($1 \le i \le 8$):
     $$\text{Slot X} = (i - 1) \times 27 + 32\text{ px} = (i - 1) \times \text{0x1B} + \text{0x20}$$
     $$\text{Slot Y} = 23\text{ px } (\text{0x17})$$
   - Minimization button: $X = 255$ px (`0xFF`), $Y = 38$ px (`0x26`).
   - Page arrows: `"Btn_ArrowUp_2"`, `"Btn_ArrowDn_2"`.

---

## 4. Keypress Interception & Context-Aware Dispatch

When the client processes Windows keyboard messages (`WM_KEYDOWN`), subroutine `FUN_002aa544` intercepts virtual key codes `VK_F1` (`0x70`) through `VK_F8` (`0x77`):

```c
void ProcessHotKeyInput(int slotIndex)
{
    char combatState = *(char *)(*(int *)PTR_DAT_004c9918 + 0x1f16);
    var binding = GetSlotBinding(slotIndex);

    if (binding.Type == BINDING_ITEM)
    {
        if (combatState == 4) // In Turn-Based Combat
        {
            // Send Action Code 0x17 Subcode 0x02 (In-Battle Potion/Item)
            FUN_002d6ad8(SocketPtr, 0x17, 0x02, binding.SourceSlot, TargetEntityId);
        }
        else // Roaming Overworld
        {
            // Send Action Code 0x17 Subcode 0x04 (Overworld Consumable Ingestion)
            FUN_002d6ad8(SocketPtr, 0x17, 0x04, binding.SourceSlot, 0);
        }
    }
    else if (binding.Type == BINDING_SKILL)
    {
        if (combatState == 4)
        {
            // Send Action Code 0x08 (Combat Skill Command)
            FUN_002d6ad8(SocketPtr, 0x08, 0x01, binding.TargetId, SelectedTargetCell);
        }
    }
}
```

---

## 5. Tooltip Rendering Engine (`FUN_002a9b88`)

When the mouse cursor hovers over an active hotkey slot, subroutine `FUN_002a9b88` executes:
1. Performs hit-testing using `PtInRect(&slotRect, mousePt)`.
2. Formats item information string:
   - Name, remaining inventory count, durability status.
   - HP/SP recovery values (`MaxHp`, `MaxSp`).
   - Stat bonuses (`STR`, `CON`, `INT`, `WIS`, `AGI`, `Shoot RES`).
3. Renders background frame via DirectDraw surface blitter `FUN_00073804` with color key `0xF98B3D`.
4. Renders font text using high-contrast dropshadow rendering via `FUN_004A33A8`.

---

## 6. Private Server Implementation Guide & C# Production Code

The following C# manager provides server-side persistence, slot validation, inventory quantity synchronization, and execution for the Hotkey Macro system.

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Wonderland.Server.Services.Hotkeys
{
    public enum HotkeyBindingType : byte
    {
        Empty = 0,
        InventoryItem = 1,
        Skill = 2,
        Vehicle = 3,
        PetCommand = 4
    }

    public sealed class HotkeySlot
    {
        public byte SlotIndex { get; set; } // 1..8 (F1..F8)
        public byte Page { get; set; }      // 1..4
        public HotkeyBindingType BindingType { get; set; }
        public ushort TargetId { get; set; }
        public byte InventorySlot { get; set; }
    }

    public sealed class PlayerHotkeyProfile
    {
        public uint CharacterId { get; init; }
        public byte ActivePage { get; set; } = 1;
        public byte Orientation { get; set; } = 2; // Default to Horizontal (Form_HotKey_H)
        public Dictionary<(byte Page, byte Slot), HotkeySlot> Slots { get; } = new();
    }

    public interface IHotkeyService
    {
        PlayerHotkeyProfile GetProfile(uint characterId);
        bool SetBinding(PlayerCharacter character, byte page, byte slot, HotkeyBindingType type, ushort targetId, byte invSlot);
        bool ClearBinding(PlayerCharacter character, byte page, byte slot);
        void SyncInventoryQuantities(PlayerCharacter character);
    }

    public sealed class HotkeyService : IHotkeyService
    {
        private readonly ConcurrentDictionary<uint, PlayerHotkeyProfile> _profiles = new();

        public PlayerHotkeyProfile GetProfile(uint characterId)
        {
            return _profiles.GetOrAdd(characterId, id => new PlayerHotkeyProfile
            {
                CharacterId = id
            });
        }

        public bool SetBinding(PlayerCharacter character, byte page, byte slot, HotkeyBindingType type, ushort targetId, byte invSlot)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (page < 1 || page > 4 || slot < 1 || slot > 8) return false;

            var profile = GetProfile(character.CharacterId);
            lock (profile)
            {
                profile.Slots[(page, slot)] = new HotkeySlot
                {
                    SlotIndex = slot,
                    Page = page,
                    BindingType = type,
                    TargetId = targetId,
                    InventorySlot = invSlot
                };
            }

            return true;
        }

        public bool ClearBinding(PlayerCharacter character, byte page, byte slot)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));

            var profile = GetProfile(character.CharacterId);
            lock (profile)
            {
                return profile.Slots.Remove((page, slot));
            }
        }

        public void SyncInventoryQuantities(PlayerCharacter character)
        {
            if (character == null) return;

            var profile = GetProfile(character.CharacterId);
            lock (profile)
            {
                foreach (var kvp in profile.Slots)
                {
                    var slot = kvp.Value;
                    if (slot.BindingType == HotkeyBindingType.InventoryItem)
                    {
                        // Count remaining items in character inventory
                        int count = character.Inventory.GetTotalQuantity(slot.TargetId);
                        // If completely depleted, slot can remain bound with count 0 or auto-clear
                    }
                }
            }
        }
    }
}
```
