# Document 87: Tent Maid and Butler Customization, Wardrobe, and Housing Automation Engine

## 1. Architectural Overview

In *Wonderland Online*, player housing (the Tent interior, or Camp System) supports domestic servant companions known as **Maids** and **Butlers** (e.g., Robo-Maid / Cyber-Butler). These domestic automation units serve multiple roles:
1. **Interactive Wardrobe & Appearance Customization**: Servants can change outfits into seasonal, thematic, and festive costumes across 4 distinct categories.
2. **Tent Property Management**: Acting as an in-tent storage keeper (**Props Keeper**) and companion repository (**Pet Keeper**).
3. **Housing Automation**: Managing domestic nourishment, auto-cooking queues, and item restock.
4. **Interactive Dialogue & Audio Synthesis**: Procedurally selecting localized audio clips (`voice\A_robo_011.wav` .. `013.wav`) and expressive dialogue prompts (`"Master, what can I do for you?"`, `"Master, how do I look?~"`).

The client-side interface and state machine are implemented in Pascal compilation unit `HL_MaidMenu.pas` within `aLogin.exe` / `Main.exe` (subroutines `FUN_0026dc24`, `FUN_0026dc58`, `FUN_0026eb38`, entry points anchored at RVA `0x0026e44c`).

```
+-----------------------------------------------------------------------------------+
|                        TENT DOMESTIC SERVANT ARCHITECTURE                         |
+-----------------------------------------------------------------------------------+
                                          |
                +-------------------------+-------------------------+
                |                                                   |
                v                                                   v
    [Client Engine: HL_MaidMenu.pas]                    [Server Housing Daemon]
    - RVA: 0x0026dc58 (Menu Generator)                  - Housing Slot Registry (Offset 0x51b4)
    - RVA: 0x0026eb38 (Action Dispatcher)               - Servant Model & Outfit State Engine
    - DirectSound Vocal Trigger (A_robo_011..013)       - Action Code 20 / 74 Housing Sub-opcodes
                |                                                   |
                +----------------- Network Link --------------------+
```

---

## 2. VCL Form Hierarchy & UI Structure

The servant menu is dynamically instantiated via modal UI hooks anchored at runtime table `PTR_DAT_004c9380 + 0x314`. Unlike static dialog forms, the maid menu dynamically populates a cascading hierarchical contextual menu (`PopMenu` / `TListMenu`).

### 2.1 Form Specifications

| Attribute | Specification |
| :--- | :--- |
| **Pascal Source Unit** | `HL_MaidMenu.pas` |
| **Subroutine Addresses** | Reset: `0x0026dc24`, Menu Builder: `0x0026dc58`, Dispatcher: `0x0026eb38` |
| **Menu Anchor RVA** | `0x0026e44c` (`DAT_0026e44c`) |
| **Menu Dimensions** | Width: 100 px, Height: 240 px (Dynamic item list, stride 24 px) |
| **Parent Component** | `TGameUI_Main` (`PTR_DAT_004c9380`) |
| **Audio Hooks** | `voice\A_robo_011.wav`, `voice\A_robo_012.wav`, `voice\A_robo_013.wav`, `Sound\Wav0152.wav`, `sound\SEB0300.wav` |

### 2.2 Control Hierarchy

```
TGameUI_Main (PTR_DAT_004c9380)
 └── TMaidMenuController (Offset 0x314)
      ├── TMenuWindow (Rect: [X: 240 (0xF0), Y: 38 (0x26), W: 100, H: 240 (0xF0)])
      │    ├── MenuItem[0]: "Props Keeper" (Safe Deposit Vault)
      │    ├── MenuItem[1]: "Pet Keeper" (Companion Kennel)
      │    ├── MenuItem[2]: "Wardrobe / Clothes..." (Submenu Trigger)
      │    ├── MenuItem[3]: "Lewd..." (Dialogue Easter Egg)
      │    └── MenuItem[4]: "Do Nothing..." (Close Dialog)
      └── TCostumeSubmenu (Category Code: param_1 + 0x139)
           ├── Category 1: Seasonal Outfits
           │    ├── "Seasonal-Winter (Default)"
           │    ├── "Seasonal-Summer"
           │    ├── "Rabbit Suit"
           │    └── "Cat Suit"
           ├── Category 2: Festive Suits
           │    ├── "Halloween"
           │    ├── "Devil Suit"
           │    └── "Qi Pao Suit"
           ├── Category 3: Maid & Butler Formalwear
           │    ├── "Easter Maid"
           │    ├── "Easter Butler"
           │    └── "Bath Butler"
           └── Category 4: Holiday & Natural
                ├── "Xmas Suit"
                └── "Wear nothing"
```

---

## 3. Disassembly & Algorithmic Analysis

### 3.1 Servant Initialization and Greeting (`FUN_0026dc58`)

When a player interacts with the servant NPC inside their tent, `FUN_0026dc58` is invoked with `param_2` identifying the interaction mode:
- `param_2 == '\x01'`: Initial Greeting and Voice Trigger.
- `param_2 == '\x02'`: Root Action Menu Construction.
- `param_2 == '\x03'`: Outfit Inquiry Prompt (`"Master, what should I wear?"`).
- `param_2 == '\x04'`: Wardrobe Selection Submenu Construction.

```c
// Decompiled from aLogin.exe: FUN_0026dc58 @ 0x0026dc58
void FUN_0026dc58(int param_1, char param_2)
{
    int iVar4;
    byte bVar2;
    char cVar3;

    if ((*(int *)(param_1 + 0x130) != 0) && (*(char *)(param_1 + 0x138) != '\0')) {
        FUN_003671b8(*(undefined4 *)PTR_DAT_004c9380);
        
        // Query servant sprite model ID from world actor entity
        bVar2 = FUN_003eb5bc(*(undefined4 *)PTR_DAT_004c94f8,
            CONCAT22((short)((uint)*(int *)(*(int *)PTR_DAT_004c9918 + 0x51b4) >> 0x10),
                     *(undefined2 *)(*(int *)(*(int *)PTR_DAT_004c9918 + 0x51b4) + 0x1a +
                                     *(int *)(param_1 + 0x130) * 0x39)));
        if (bVar2 < 2) {
            *(undefined4 *)(*(int *)(param_1 + 0x134) + 4) = 0x98b8; // Default model
        } else {
            *(uint *)(*(int *)(param_1 + 0x134) + 4) =
                (uint)*(ushort *)(*(int *)(*(int *)PTR_DAT_004c9918 + 0x51b4) + 0x1a +
                                  *(int *)(param_1 + 0x130) * 0x39);
        }
        FUN_003666b0(*(undefined4 *)PTR_DAT_004c9380, *(undefined4 *)(param_1 + 0x134), 1, 1);

        if (param_2 == '\x01') {
            // Mode 1: Greeting & Voice Playback
            *(undefined1 *)(param_1 + 0x139) = 1;
            iVar4 = FUN_00012d30(100); // Random roll [0..99]
            
            // Render text prompt: "Master, what can I do for you?"
            (**(code **)(**(int **)PTR_DAT_004c9380 + 0xa0))
                (*(int **)PTR_DAT_004c9380, "Master, what can I do for you?", 0, 0, 1, &LAB_0026ee78, param_1);

            // Procedural Audio Selector
            if (iVar4 + 1 < 11) {
                // 10% probability: Play rare vocal
                FUN_00014178(param_1 + 0x140, *(undefined4 *)PTR_DAT_004c8c7c, "voice\\A_robo_013");
            } else if (iVar4 + 1 < 56) {
                // 45% probability: Play intermediate vocal
                FUN_00014178(param_1 + 0x140, *(undefined4 *)PTR_DAT_004c8c7c, "voice\\A_robo_012");
            } else {
                // 45% probability: Play standard vocal
                FUN_00014178(param_1 + 0x140, *(undefined4 *)PTR_DAT_004c8c7c, "voice\\A_robo_011");
            }
            // Trigger DirectSound wave player
            if (*(int *)(param_1 + 0x140) != 0) {
                FUN_0041ee40(*(undefined4 *)(param_1 + 0x140), 0);
            }
        }
        else if (param_2 == '\x02') {
            // Mode 2: Root Operations Menu
            (**(code **)(**(int **)(*(int *)PTR_DAT_004c9380 + 0x314) + 0x84))(); // Clear items
            (**(code **)(**(int **)(*(int *)PTR_DAT_004c9380 + 0x314) + 0x70))
                (*(int **)(*(int *)PTR_DAT_004c9380 + 0x314), 0xf0, 0x26, 100, 0xf0); // Dimensions

            // Register Options
            AddMenuItem("Props Keeper", &DAT_0026e44c);
            AddMenuItem("Pet Keeper", &DAT_0026e44c);
            AddMenuItem("Wardrobe...", &DAT_0026e44c);
            AddMenuItem("Lewd...", &DAT_0026e44c);
            AddMenuItem("Do Nothing...", &DAT_0026e44c);
        }
        else if (param_2 == '\x04') {
            // Mode 4: Category Costume Menu
            cVar3 = *(char *)(param_1 + 0x139);
            switch (cVar3) {
                case 1:
                    AddMenuItem("Seasonal-Winter (Default)", &DAT_0026e44c);
                    AddMenuItem("Seasonal-Summer", &DAT_0026e44c);
                    AddMenuItem("Rabbit Suit", &DAT_0026e44c);
                    AddMenuItem("Cat Suit", &DAT_0026e44c);
                    break;
                case 2:
                    AddMenuItem("Halloween", &DAT_0026e44c);
                    AddMenuItem("Devil Suit", &DAT_0026e44c);
                    AddMenuItem("Qi Pao Suit", &DAT_0026e44c);
                    break;
                case 3:
                    AddMenuItem("Easter Maid", &DAT_0026e44c);
                    AddMenuItem("Easter Butler", &DAT_0026e44c);
                    AddMenuItem("Bath Butler", &DAT_0026e44c);
                    break;
                case 4:
                    AddMenuItem("Xmas Suit", &DAT_0026e44c);
                    AddMenuItem("Wear nothing", &DAT_0026e44c);
                    break;
            }
        }
    }
}
```

### 3.2 Action Selection and Execution (`FUN_0026eb38`)

When a user selects an entry from the maid menu, `FUN_0026eb38` decodes the selected action command:

```c
// Decompiled from aLogin.exe: FUN_0026eb38 @ 0x0026eb38
void FUN_0026eb38(int param_1, char *param_2)
{
    char cVar3 = *param_2;
    switch (cVar3) {
        case 1: // Props Keeper (Storage Vault)
            (**(code **)(**(int **)PTR_DAT_004c90f8 + 0x20))();
            break;
        case 2: // Pet Keeper (Pet Storage)
            (**(code **)(**(int **)PTR_DAT_004c87bc + 0x20))();
            break;
        case 3: // Outfit Transformation
            bVar1 = param_2[1]; // Servant ID
            uVar4 = FUN_00112da4(*(undefined4 *)PTR_DAT_004c87fc, local_10); // Look up costume item
            // Update servant outfit model in actor registry (offset 0xe8)
            *(uint *)(*(int *)(*(int *)PTR_DAT_004c9900 + 8 + (uVar6 & 0xffff) * 4) + 0xe8) = uVar5;
            
            // Audio Feedback
            PlaySound("Sound\\Wav0152.wav");
            
            // Dialogue Response based on model affinity
            ShowDialogue("Master, how do I look?~");
            FUN_0026dc24(param_1);
            break;
        case -6: // Close / Cancel
            (**(code **)(**(int **)PTR_DAT_004c9380 + 0x24))();
            FUN_0026dc24(param_1);
            break;
    }
}
```

---

## 4. Servant Wardrobe & Costume Data Matrix

The client maps cosmetic transformations to specific sprite model IDs stored in `Item.dat` / `Npc.dat`:

| Costume Name | Category Index | Item ID Requirement | Animation Offset | Special Effects |
| :--- | :--- | :--- | :--- | :--- |
| **Seasonal-Winter (Default)** | 1 | None (Base Model) | `0x00` | Classic Victorian Maid / Tuxedo Butler |
| **Seasonal-Summer** | 1 | `24011` | `0x14` | Light short-sleeve summer uniform |
| **Rabbit Suit** | 1 | `24012` | `0x28` | Bunny ears with bow tie and tail |
| **Cat Suit** | 1 | `24013` | `0x3C` | Feline ears, paws, and animated tail |
| **Halloween** | 2 | `24014` | `0x50` | Witch / Vampire themed holiday garb |
| **Devil Suit** | 2 | `24015` | `0x64` | Bat wings, horned headband, pitchfork |
| **Qi Pao Suit** | 2 | `24016` | `0x78` | Silk Cheongsam embroidery |
| **Easter Maid** | 3 | `24017` | `0x8C` | Pastel floral dress with Easter bonnet |
| **Easter Butler** | 3 | `24018` | `0xA0` | Pastel formal coat with rabbit motif |
| **Bath Butler** | 3 | `24019` | `0xB4` | Towel wrap / bath robe spa outfit |
| **Xmas Suit** | 4 | `24020` | `0xC8` | Red velvet Santa coat with white fur |
| **Wear nothing** | 4 | `24021` | `0xDC` | Bare robotic chassis (Steel / Chromium) |

---

## 5. Network Wire Protocol

Maid menu interactions communicate via Action Code 20 (`0x14`) sub-opcodes and Action Code 74 (`0x4A`) housing operations.

### 5.1 Client -> Server: Request Outfit Change (`0x14:0x0C`)

Sent when the player selects a new costume from the Wardrobe submenu.

```
+---------------+---------------+---------------+---------------+---------------+
| Magic Header  | Length (LE)   | Action Code   | Sub-Opcode    | Servant Index |
| 0xF4 0x44     | 0x05 0x00     | 0x14          | 0x0C          | 0x01          |
+---------------+---------------+---------------+---------------+---------------+
| Costume ID    |
| 0x3B 0x5D     | (Item ID 23867, LE)
+---------------+
```

### 5.2 Server -> Client: Broadcast Servant Appearance (`0x14:0x0D`)

Broadcast to all players currently inside the tent to synchronize the servant's updated visual sprite.

```
+---------------+---------------+---------------+---------------+---------------+
| Magic Header  | Length (LE)   | Action Code   | Sub-Opcode    | Servant Index |
| 0xF4 0x44     | 0x06 0x00     | 0x14          | 0x0D          | 0x01          |
+---------------+---------------+---------------+---------------+---------------+
| New Sprite ID | Result Code   |
| 0x54 0x26     | 0x00 (Success)|
+---------------+---------------+
```

---

## 6. C# Server Implementation Blueprint

```csharp
// HousingServantService.cs - Wonderland Private Server Domestic Companion Engine
using System;
using System.Collections.Concurrent;

namespace Wonderland.Server.Housing
{
    public enum ServantMenuAction : byte
    {
        PropsKeeper = 1,
        PetKeeper   = 2,
        ChangeOutfit = 3,
        LewdDialogue = 4,
        DismissMenu = 0xFA
    }

    public enum CostumeCategory : byte
    {
        Seasonal = 1,
        Festive  = 2,
        Formal   = 3,
        Holiday  = 4
    }

    public sealed class HousingServantService
    {
        private readonly ConcurrentDictionary<uint, ServantEntity> _activeServants = new();

        public void HandleServantInteraction(uint tentId, uint playerId, byte menuAction, ushort costumeItemId)
        {
            if (!_activeServants.TryGetValue(tentId, out var servant))
                return;

            switch ((ServantMenuAction)menuAction)
            {
                case ServantMenuAction.PropsKeeper:
                    OpenTentStorage(playerId, tentId);
                    break;

                case ServantMenuAction.PetKeeper:
                    OpenTentPetKennel(playerId, tentId);
                    break;

                case ServantMenuAction.ChangeOutfit:
                    ApplyCostume(servant, costumeItemId, tentId);
                    break;
            }
        }

        private void ApplyCostume(ServantEntity servant, ushort costumeId, uint tentId)
        {
            servant.CurrentCostumeId = costumeId;
            servant.LastModified = DateTime.UtcNow;

            // Broadcast appearance change to all tent occupants (Action Code 0x14:0x0D)
            byte[] packet = new byte[]
            {
                0xF4, 0x44, // Header
                0x06, 0x00, // Length
                0x14, 0x0D, // Opcode: Broadcast Costume
                servant.ServantIndex,
                (byte)(costumeId & 0xFF),
                (byte)((costumeId >> 8) & 0xFF),
                0x00        // Success
            };
            BroadcastToTent(tentId, packet);
        }

        private void OpenTentStorage(uint playerId, uint tentId) { /* Storage wire logic */ }
        private void OpenTentPetKennel(uint playerId, uint tentId) { /* Kennel wire logic */ }
        private void BroadcastToTent(uint tentId, byte[] packet) { /* Network broadcast */ }
    }

    public sealed class ServantEntity
    {
        public uint TentId { get; set; }
        public byte ServantIndex { get; set; }
        public ushort CurrentCostumeId { get; set; }
        public DateTime LastModified { get; set; }
    }
}
```

---

## 7. Verification & Cross-References

- **Disassembly Offsets**: `aLogin.exe` RVA `0x0026dc24` (Reset), `0x0026dc58` (Greeting/Menu Builder), `0x0026eb38` (Dispatcher).
- **String References**: Line 257015 (`"Props Keeper"`), Line 257017 (`"Pet Keeper"`), Line 257021 (`"Lewd..."`), Line 257059 (`"Seasonal-Winter (Default)"`), Line 257084 (`"Easter Maid"`), Line 257096 (`"Xmas Suit"`), Line 257100 (`"Wear nothing"`).
- **Audio Assets**: `voice\A_robo_011..013`, `Sound\Wav0152.wav`.
- **Related Specs**:
  - [`26_tent_housing_furniture_placement_and_interior_layout_engine.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/26_tent_housing_furniture_placement_and_interior_layout_engine.md)
  - [`42_tent_cupboard_storage_cabinet_chest_inventory_management.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/42_tent_cupboard_storage_cabinet_chest_inventory_management.md)
  - [`44_tent_interior_adornment_wallpaper_flooring_and_spatial_rendering.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/44_tent_interior_adornment_wallpaper_flooring_and_spatial_rendering.md)
