# Document 96: Multi-Stage Character Creation Wizard, Palette Tinting, and Attribute Allocation Engine

## 1. Architectural Overview

When players connect to a *Wonderland Online* game server and select an unoccupied character slot, the client launches the **Character Creation Wizard**. Built across five cascading Delphi sub-forms (`form_NewRole_1` through `form_NewRole_5`) in `se_CreateRole.pas`, the creation pipeline guides new players through character identity, elemental alignment, attribute point distribution, and visual sprite customization.

The character generation pipeline enforces four sequential phases:
1. **Stage 0: Character Avatar & Gender Selection (`form_NewRole_1`)**: Players pick from 14 distinct canonical protagonist archetypes (e.g., Daniel, Rok, Sid, Betty, Nina, Jessica, Maria), dictating baseline sprite models, voice profiles, and default animation sets.
2. **Stage 1: Elemental Affinity & Base Attribute Allocation (`form_NewRole_2` / `form_NewRole_3`)**: Players commit to an Elemental Affinity (Earth, Water, Fire, Wind) and distribute 24 unallocated attribute points across STR, CON, INT, WIS, and AGI.
3. **Stage 2: Palette Customization & Tint Shading (`form_NewRole_4`)**: Real-time HSV/RGB color modulation allowing players to personalize hair color, skin tone, and clothing accents without modifying on-disk sprite sheets.
4. **Stage 3: Identity Verification & Creation Submission (`form_NewRole_5`)**: Name validation, profanity blacklist screening, and serialization into Action Code 1:2 (`0x01:0x02`) character generation frames.

```
+-----------------------------------------------------------------------------------+
|                        CHARACTER CREATION PIPELINE (STAGE 0 - 3)                  |
+-----------------------------------------------------------------------------------+
                                          |
        +---------------------------------+---------------------------------+
        |                                                                   |
        v                                                                   v
 [Stage 0: Avatar Selection]                                      [Stage 1: Affinity & Stats]
 - form_NewRole_1 (RVA: 0x00271e8c)                               - form_NewRole_2 (Male) / 3 (Female)
 - 14 Archetype Characters                                        - Earth, Water, Fire, Wind
 - Male / Female Gender Gate                                      - 24 Attribute Points Pool
        |                                                                   |
        +---------------------------------+---------------------------------+
                                          |
        +---------------------------------+---------------------------------+
        |                                                                   |
        v                                                                   v
 [Stage 2: Palette Tinting]                                       [Stage 3: Name & Finalize]
 - form_NewRole_4                                                 - form_NewRole_5
 - Hair, Skin, Clothing Color Matrix                             - Blacklist String Filter
 - DirectDraw 16-bit Palette Modulator                            - Action Code 1:2 Packet Dispatch
```

---

## 2. VCL Form Hierarchy & UI Structure

The wizard is coordinated by `TCreateRoleController` in `se_CreateRole.pas` (state machine managed at RVA `0x00271e8c`):

### 2.1 Form Specifications

| Attribute | Specification |
| :--- | :--- |
| **Pascal Source Unit** | `se_CreateRole.pas` / `aLogin.pas` |
| **Form Identifiers** | `form_NewRole_1` (Avatar), `form_NewRole_2` (Male Stats), `form_NewRole_3` (Female Stats), `form_NewRole_4` (Palette), `form_NewRole_5` (Confirmation) |
| **Dimensions** | Width: 800 px, Height: 600 px (Full-Screen Modal Canvas) |
| **Subroutine Addresses** | State Dispatcher: `FUN_00271e8c`, Reset: `FUN_00271d50` |
| **Avatar Count** | 14 Protagonists (7 Male, 7 Female) |
| **Attribute Points** | 24 Distributable Points |

### 2.2 Stage Control Flow (`FUN_00271e8c`)

```c
// Decompiled from aLogin.exe: FUN_00271e8c @ 0x00271e8c
void FUN_00271e8c(int param_1)
{
    int stage = *(int *)(param_1 + 0x1ec);

    if (stage == 0) {
        // Stage 0: Avatar & Archetype Picker
        *(undefined4 *)(param_1 + 0x4c) = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "form_NewRole_1");
        SetVisible(param_1 + 0xf0, 1);  // Show character carousel
        SetVisible(param_1 + 0x110, 1); // Show gender toggle
        HideAllStatControls(param_1);
    }
    else if (stage == 1) {
        // Stage 1: Element & Stat Distribution
        if (*(char *)(param_1 + 0x1f0) == '\0') {
            // Male Stat Template
            *(undefined4 *)(param_1 + 0x4c) = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "form_NewRole_2");
        } else {
            // Female Stat Template
            *(undefined4 *)(param_1 + 0x4c) = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "form_NewRole_3");
        }
        SetVisible(param_1 + 0x150, 1); // Enable STR allocator
        SetVisible(param_1 + 0x154, 1); // Enable CON allocator
        SetVisible(param_1 + 0x158, 1); // Enable INT allocator
        SetVisible(param_1 + 0x15c, 1); // Enable WIS allocator
        SetVisible(param_1 + 0x160, 1); // Enable AGI allocator
    }
    else if (stage == 2) {
        // Stage 2: Hair & Skin Palette Tinting
        *(undefined4 *)(param_1 + 0x4c) = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "form_NewRole_4");
        ShowPaletteSliders(param_1);
    }
    else if (stage == 3) {
        // Stage 3: Confirmation & Naming
        *(undefined4 *)(param_1 + 0x4c) = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "form_NewRole_5");
        ShowNameInputBox(param_1);
    }
}
```

---

## 3. Canonical Protagonist Archetypes

The 14 selectable base avatars map directly to unique body sprite IDs:

| Archetype Name | Gender | Default Element | Base STR | Base CON | Base INT | Base WIS | Base AGI | Sprite Asset |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :--- |
| **Daniel** | Male | Earth | 6 | 6 | 4 | 4 | 4 | `Role_01.spr` |
| **Rok** | Male | Fire | 8 | 5 | 2 | 3 | 6 | `Role_02.spr` |
| **Sid** | Male | Wind | 4 | 4 | 5 | 3 | 8 | `Role_03.spr` |
| **Betty** | Female | Water | 3 | 5 | 8 | 5 | 3 | `Role_04.spr` |
| **Nina** | Female | Wind | 4 | 4 | 4 | 4 | 8 | `Role_05.spr` |
| **Jessica** | Female | Earth | 5 | 8 | 4 | 4 | 3 | `Role_06.spr` |
| **Maria** | Female | Water | 2 | 4 | 8 | 7 | 3 | `Role_07.spr` |
| **Kuro** | Male | Fire | 7 | 6 | 3 | 3 | 5 | `Role_08.spr` |
| **Vanessa** | Female | Fire | 6 | 5 | 5 | 3 | 5 | `Role_09.spr` |
| **Sam** | Male | Earth | 7 | 7 | 2 | 4 | 4 | `Role_10.spr` |
| **Alice** | Female | Water | 3 | 5 | 7 | 6 | 3 | `Role_11.spr` |
| **Konno** | Male | Wind | 5 | 4 | 4 | 3 | 8 | `Role_12.spr` |
| **Ayane** | Female | Earth | 4 | 7 | 5 | 5 | 3 | `Role_13.spr` |
| **Max** | Male | Fire | 8 | 6 | 3 | 2 | 5 | `Role_14.spr` |

---

## 4. Palette Tinting & Color Modulation Math

In Stage 2 (`form_NewRole_4`), players modify hair, skin, and clothing color via slider offsets ($R_{\text{mod}}, G_{\text{mod}}, B_{\text{mod}} \in [-31, +31]$).

During blit rasterization, `ro_Clipper_LightAlpha_ColorKey_Blt` transforms each indexed 16-bit pixel:
$$R_{\text{final}} = \text{Clamp}\left(R_{\text{orig}} + \frac{R_{\text{mod}} \times 0xF800}{31}, 0, 0xF800\right)$$
$$G_{\text{final}} = \text{Clamp}\left(G_{\text{orig}} + \frac{G_{\text{mod}} \times 0x07E0}{31}, 0, 0x07E0\right)$$
$$B_{\text{final}} = \text{Clamp}\left(B_{\text{orig}} + \frac{B_{\text{mod}} \times 0x001F}{31}, 0, 0x001F\right)$$

The resulting packed palette vector is stored in a 4-byte serialized integer:
$$\text{ColorVector} = (\text{HairIndex} \ll 24) \mid (\text{SkinIndex} \ll 16) \mid (\text{ClothingTint} \ll 8) \mid \text{AccessoryTint}$$

---

## 5. Network Wire Protocol

### 5.1 Client -> Server: Action Code 1:2 Create Character Request

```
+---------------+---------------+---------------+---------------+---------------+
| Magic Header  | Length (LE)   | Action Code   | Sub-Opcode    | Slot Index    |
| 0xF4 0x44     | 0x22 0x00     | 0x01          | 0x02          | 0x00 (Slot 1) |
+---------------+---------------+---------------+---------------+---------------+
| Archetype ID  | Element (1..4)| STR           | CON           | INT           |
| 0x03 (Sid)    | 0x04 (Wind)   | 0x04          | 0x04          | 0x05          |
+---------------+---------------+---------------+---------------+---------------+
| WIS           | AGI           | Color Vector (4 bytes uint32 LE)              |
| 0x03          | 0x08          | 0x14 0x08 0x02 0x00                           |
+---------------+---------------+-----------------------------------------------+
| Character Name (16 bytes, null-padded)                                        |
| "A e r o s \0 \0 \0 \0 \0 \0 \0 \0 \0 \0 \0"                                  |
+-------------------------------------------------------------------------------+
```

### 5.2 Server -> Client: Action Code 1:3 Create Character Response

```
+---------------+---------------+---------------+---------------+---------------+
| Magic Header  | Length (LE)   | Action Code   | Sub-Opcode    | Result Code   |
| 0xF4 0x44     | 0x04 0x00     | 0x01          | 0x03          | 0x00 (Success)|
+---------------+---------------+---------------+---------------+---------------+
| Created Slot  |
| 0x00          |
+---------------+
```

---

## 6. C# Server Implementation Blueprint

```csharp
// CharacterCreationService.cs - Wonderland Private Server Creation Pipeline
using System;
using System.Text;

namespace Wonderland.Server.Character
{
    public sealed class CharacterCreationService
    {
        public bool TryCreateCharacter(
            uint accountId,
            byte slotIndex,
            byte archetypeId,
            byte element,
            byte str, byte con, byte intStat, byte wis, byte agi,
            uint colorVector,
            string name,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            // 1. Verify Slot Availability (Max 3 characters)
            if (slotIndex > 2 || Database.CharacterExistsAtSlot(accountId, slotIndex))
            {
                errorMessage = "Character slot occupied or invalid.";
                return false;
            }

            // 2. Validate Stat Point Allocation (Sum must equal 24)
            int totalStats = str + con + intStat + wis + agi;
            if (totalStats != 24)
            {
                errorMessage = $"Invalid attribute sum: {totalStats} (expected 24).";
                return false;
            }

            // 3. Validate Elemental Affinity (1=Earth, 2=Water, 3=Fire, 4=Wind)
            if (element < 1 || element > 4)
            {
                errorMessage = "Invalid elemental affinity.";
                return false;
            }

            // 4. Validate Character Name (Length & Profanity Blacklist)
            if (string.IsNullOrWhiteSpace(name) || name.Length < 2 || name.Length > 16)
            {
                errorMessage = "Name must be 2-16 characters.";
                return false;
            }

            if (ProfanityFilter.ContainsProhibitedWords(name))
            {
                errorMessage = "Name contains forbidden words.";
                return false;
            }

            // 5. Persist Character Record
            Database.CreateCharacter(new CharacterEntity
            {
                AccountId = accountId,
                SlotIndex = slotIndex,
                Name = name,
                ArchetypeId = archetypeId,
                Element = element,
                Level = 1,
                CurrentHp = 200 + (con * 10),
                MaxHp = 200 + (con * 10),
                CurrentSp = 100 + (wis * 10),
                MaxSp = 100 + (wis * 10),
                Strength = str,
                Constitution = con,
                Intelligence = intStat,
                Wisdom = wis,
                Agility = agi,
                ColorVector = colorVector,
                CreatedDate = DateTime.UtcNow
            });

            return true;
        }
    }
}
```

---

## 7. Verification & Cross-References

- **Disassembly Offsets**: `aLogin.exe` RVA `0x00271e8c` (`TCreateRoleController`), `0x00271d50` (Reset).
- **String References**: Line 258033 (`"form_NewRole_1"`), Line 258087 (`"form_NewRole_2"`), Line 258092 (`"form_NewRole_3"`), Line 258135 (`"form_NewRole_4"`), Line 258170 (`"form_NewRole_5"`).
- **Related Specs**:
  - [`08_login_authentication_and_character_selection_pipeline.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/08_login_authentication_and_character_selection_pipeline.md)
  - [`55_character_role_status_sheets_and_attribute_recalculation.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/55_character_role_status_sheets_and_attribute_recalculation.md)
  - [`88_dynamic_scene_lighting_day_night_cycle_and_alpha_shadow_engine.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/88_dynamic_scene_lighting_day_night_cycle_and_alpha_shadow_engine.md)
