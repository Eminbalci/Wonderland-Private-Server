# Document 66: Tent Interior Wallpaper, Flooring, and Aesthetic Adornment Engine

## 1. Executive Summary & Engine Architecture

In addition to furniture positioning and container storage, the portable tent housing system in Wonderland Online incorporates an interior surface customization engine designated as `Form_HomeAdorn_1`. Dissected from client subroutines `FUN_0023f564`, `FUN_0024bac0`, and `FUN_0024bb14`, this subsystem allows players to alter the architectural appearance of their tent interior across multiple floors (`Floor 1`, `Floor 2`), applying customized wallpaper patterns, floor tile textures, and ambient lighting trims.

The dialog window `Form_HomeAdorn_1` ($223 \times 223$ px) provides interactive controls for previewing decoration skins, committing consumable interior design scrolls, repositioning wall trims, and stripping existing aesthetic modifications back to base canvas. Network communication is conducted via Action Code `0x3D` (`61` decimal), which coordinates atomic inventory consumption, real-time tile skinning, and spatial replication across visiting party members.

```
+------------------------------------------------------------------------------------+
|                   TENT INTERIOR ADORNMENT ENGINE ARCHITECTURE                      |
+------------------------------------------------------------------------------------+
                                      |
         +----------------------------+----------------------------+
         | Surface Target 1: Flooring Tiles                        | Surface Target 2: Wallpaper Trim
         v                                                         v
  [Floor Tile Surface Blitter]                              [Wallpaper Canvas Resolver]
  - 2-Floor Isometric Tile Map                              - Perimeter Wall Segment Masks
  - Consumable Flooring Item ID                             - Consumable Wallpaper Item ID
  - Texture Stride: 64x32 Diamond                           - Texture Stride: 128x128 Quad
         |                                                         |
         +----------------------------+----------------------------+
                                      |
                                      v
                      [Form_HomeAdorn_1 Dialog (223x223 px)]
                    Btn_AddNew_1 (Apply) | Btn_Move_1 | Btn_Remove_1
                                      |
                                      v
                 [Network Dispatcher: Action Code 0x3D (61)]
               Subcodes: 0x01 (Apply), 0x02 (Remove), 0x03 (Sync)
```

---

## 2. Memory Layouts & Data Structures

### 2.1 Adornment Surface Record (`0x18` = 24 Bytes)
Within the tent interior memory heap at `PTR_DAT_004c9900`, the active skinning configuration for each floor is stored as a 24-byte structure:

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `byte` | `FloorIndex` | `1` = Ground Floor, `2` = Second Floor |
| `+0x01` | `byte` | `AdornmentType`| `1` = Floor Tiles, `2` = Wallpaper, `3` = Ceiling Trim |
| `+0x02` | `ushort` | `SourceItemId` | Consumable decor item ID consumed to produce this skin |
| `+0x04` | `uint` | `TextureId` | Master graphics asset ID from `Data\Ground.MMG` or `Data\Wem.MMG` |
| `+0x08` | `uint` | `PaletteId` | 16-bit color lookup table index |
| `+0x0C` | `byte` | `Orientation` | Rotation / heading angle of tile alignment ($0..3$) |
| `+0x0D` | `byte` | `AlphaBlend` | Transparency / lighting modulation flag |
| `+0x0E` | `byte[10]`| `Reserved` | Reserved engine metadata |

### 2.2 `Form_HomeAdorn_1` Form Instance Layout (`param_1`)

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `void*` | `VTable` | Delphi component virtual method table pointer |
| `+0x04` | `uint` | `FormHandle` | DirectDraw surface bitmap handle |
| `+0x3A` | `uint` | `BtnClose1` | Bottom close button (`"Btn_Close_1"`) |
| `+0x3B` | `uint` | `BtnCloseS1` | Top-right small close button (`"Btn_Close_s_1"`) |
| `+0x3C` | `uint` | `BtnAddNew` | Apply new adornment button (`"Btn_AddNew_1"`) |
| `+0x3D` | `uint` | `BtnRemove` | Strip / revert adornment button (`"Btn_Remove_1"`) |
| `+0x3E` | `uint` | `BtnMove` | Reorient / shift adornment button (`"Btn_Move_1"`) |
| `+0x6F` | `byte` | `DirtyFlag` | State flag tracking uncommitted visual adjustments |
| `+0x16C`| `uint` | `SelectedItemId`| Staged consumable item ID for application |

---

## 3. UI Form Geometry, Coordinates & Controls

### 3.1 `Form_HomeAdorn_1` Layout Metrics
- **Dimensions:** Width `0xDF` (223 pixels), Height `0xDF` (223 pixels).
- **Control Layout:**
  - `"Btn_AddNew_1"`: $X = \text{0x54}$ (84 px), $Y = \text{0x46}$ (70 px), size $20 \times 56$ px. (Callback: `LAB_0023f864`)
  - `"Btn_Move_1"`: $X = \text{0x54}$ (84 px), $Y = 100$ px, size $20 \times 56$ px. (Callback: `LAB_0023f87c`)
  - `"Btn_Remove_1"`: $X = \text{0x54}$ (84 px), $Y = \text{0x82}$ (130 px), size $20 \times 56$ px. (Callback: `LAB_0023f88c`)
  - `"Btn_Close_1"`: $X = \text{0x54}$ (84 px), $Y = \text{0xA0}$ (160 px), size $20 \times 56$ px.
  - `"Btn_Close_s_1"`: $X = \text{0xAC}$ (172 px), $Y = \text{0x19}$ (25 px), size $18 \times 18$ px.

```
+-------------------------------------------------------+
| Form_HomeAdorn_1: Tent Adornment (223 x 223 px)       |
+-------------------------------------------------------+
| [Header Title: Interior Decoration]               [X] |
+-------------------------------------------------------+
|                                                       |
|              [ Btn_AddNew_1: Apply Skin ]             |
|                                                       |
|              [ Btn_Move_1: Reorient Tile ]            |
|                                                       |
|              [ Btn_Remove_1: Revert Base ]            |
|                                                       |
|              [ Btn_Close_1: Dismiss ]                 |
|                                                       |
+-------------------------------------------------------+
```

---

## 4. Network Protocol, Wire Frames & Action Codes

### 4.1 Packet Framing
Adornment modifications are transmitted via Action Code `0x3D` (61 decimal):
- **Opcode:** `0x3D`
- **Subcodes:**
  - `0x01`: Apply Adornment Skin (C2S via `FUN_0024bac0`)
  - `0x02`: Remove / Strip Adornment Skin (C2S)
  - `0x03`: Synchronize Tent Interior Appearance (S2C Broadcast to Tent Visitors)

### 4.2 C# Packet Structure Definitions

```csharp
namespace Wonderland.Network.Packets.Housing
{
    /// <summary>
    /// Client-to-server request to apply wallpaper or flooring skin to tent interior.
    /// Wire Header: Action Code 0x3D, Subcode 0x01.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct C2S_HomeAdornApplyPacket
    {
        public byte ActionCode;       // 0x3D (61)
        public byte SubCode;          // 0x01
        public byte FloorIndex;       // 1 = Ground Floor, 2 = Second Floor
        public byte AdornmentType;    // 1 = Floor Tiles, 2 = Wallpaper
        public byte InventorySlot;    // Slot index of the consumed decorative item
        public ushort ItemId;         // Validated item ID of the decoration scroll
    }

    /// <summary>
    /// Client-to-server request to remove current wallpaper or flooring and revert to default.
    /// Wire Header: Action Code 0x3D, Subcode 0x02.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct C2S_HomeAdornRemovePacket
    {
        public byte ActionCode;       // 0x3D (61)
        public byte SubCode;          // 0x02
        public byte FloorIndex;       // 1 or 2
        public byte AdornmentType;    // 1 or 2
    }

    /// <summary>
    /// Server-to-client broadcast notifying all players inside the tent of visual skin changes.
    /// Wire Header: Action Code 0x3D, Subcode 0x03.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct S2C_HomeAdornSyncPacket
    {
        public byte ActionCode;       // 0x3D (61)
        public byte SubCode;          // 0x03
        public uint TentOwnerId;      // Character ID of tent owner
        public byte FloorIndex;       // 1 or 2
        public ushort FlooringItemId; // 0 if default
        public ushort WallpaperItemId;// 0 if default
    }
}
```

---

## 5. Tile Skinning Algorithms & Validation Safeguards

### 5.1 Validation Safeguards
1. **Tent Ownership Gate:** Only the owner of the active tent instance may trigger `Form_HomeAdorn_1`. Visitors interacting with interior surfaces receive error notifications:
   `"Not the owner, can't set sale"` / `"House is #Rforbidden to enter/#R!"`
2. **Item Type Verification:** The client cross-references the item template category in `PTR_DAT_004c94f8`:
   - Items with attribute `Category == DECOR_FLOOR` can only apply to `AdornmentType == 1`.
   - Items with attribute `Category == DECOR_WALL` can only apply to `AdornmentType == 2`.
3. **Floor Index Validation:** Ground floor (`FloorIndex == 1`) is always unlocked; second floor (`FloorIndex == 2`) requires the presence of an assembled stairway / ladder furniture piece (`Elevator_LB`, `Elevator_RB`).

---

## 6. Private Server Implementation Guide & C# Production Code

The following service provides backend persistence and replication for tent interior wallpapers and flooring.

```csharp
using System;
using System.Collections.Concurrent;
using Wonderland.Network.Packets.Housing;

namespace Wonderland.Server.Services.Housing
{
    public sealed class TentFloorAesthetic
    {
        public ushort FlooringItemId { get; set; }
        public ushort WallpaperItemId { get; set; }
    }

    public sealed class TentAestheticProfile
    {
        public uint CharacterId { get; init; }
        public TentFloorAesthetic Floor1 { get; } = new();
        public TentFloorAesthetic Floor2 { get; } = new();

        public TentFloorAesthetic GetFloor(byte floor) => floor == 2 ? Floor2 : Floor1;
    }

    public interface ITentAestheticService
    {
        bool ApplyAdornment(PlayerCharacter owner, C2S_HomeAdornApplyPacket packet, out string error);
        bool RemoveAdornment(PlayerCharacter owner, C2S_HomeAdornRemovePacket packet, out string error);
        TentAestheticProfile GetProfile(uint characterId);
    }

    public sealed class TentAestheticService : ITentAestheticService
    {
        private readonly ConcurrentDictionary<uint, TentAestheticProfile> _profiles = new();

        public TentAestheticProfile GetProfile(uint characterId)
        {
            return _profiles.GetOrAdd(characterId, id => new TentAestheticProfile
            {
                CharacterId = id
            });
        }

        public bool ApplyAdornment(PlayerCharacter owner, C2S_HomeAdornApplyPacket packet, out string error)
        {
            error = string.Empty;

            if (owner == null) throw new ArgumentNullException(nameof(owner));

            if (!owner.IsInOwnTent)
            {
                error = "Can only use inside own tent";
                return false;
            }

            var item = owner.Inventory.GetItemAt(packet.InventorySlot);
            if (item == null || item.ItemId != packet.ItemId)
            {
                error = "Decoration item not found";
                return false;
            }

            var profile = GetProfile(owner.CharacterId);
            var floor = profile.GetFloor(packet.FloorIndex);

            // Consume decorative item
            owner.Inventory.ConsumeSlot(packet.InventorySlot, 1);

            if (packet.AdornmentType == 1)
            {
                floor.FlooringItemId = packet.ItemId;
            }
            else if (packet.AdornmentType == 2)
            {
                floor.WallpaperItemId = packet.ItemId;
            }

            // Broadcast visual update to all players currently inside this tent
            owner.CurrentTentInstance?.BroadcastAdornSync(packet.FloorIndex, floor.FlooringItemId, floor.WallpaperItemId);

            return true;
        }

        public bool RemoveAdornment(PlayerCharacter owner, C2S_HomeAdornRemovePacket packet, out string error)
        {
            error = string.Empty;

            if (owner == null) throw new ArgumentNullException(nameof(owner));

            if (!owner.IsInOwnTent)
            {
                error = "Can only use inside own tent";
                return false;
            }

            var profile = GetProfile(owner.CharacterId);
            var floor = profile.GetFloor(packet.FloorIndex);

            if (packet.AdornmentType == 1)
            {
                floor.FlooringItemId = 0; // Revert to canvas base
            }
            else if (packet.AdornmentType == 2)
            {
                floor.WallpaperItemId = 0;
            }

            owner.CurrentTentInstance?.BroadcastAdornSync(packet.FloorIndex, floor.FlooringItemId, floor.WallpaperItemId);

            return true;
        }
    }
}
```
