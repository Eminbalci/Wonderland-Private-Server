# Document 88: Dynamic Scene Lighting, Day/Night Cycle, and Alpha Shadow Blitter Engine

## 1. Architectural Overview

*Wonderland Online* employs a specialized, low-overhead software lighting and rasterization pipeline optimized for 16-bit DirectDraw 7 surfaces. The engine seamlessly blends dynamic ambient environmental illumination (Day/Night cycle) with localized, additive radial light sources (lanterns, doorway illumination, magical skill auras, and campfires).

The lighting architecture is split into two collaborative subsystems:
1. **Scene Light Source Manager (`DZ_Light.pas`)**: Tracks up to 30 concurrent active localized light sources (`FUN_0041d8e4`, `FUN_0041da28`), viewport frustum culling against the screen camera (`*(PTR_DAT_004c864c + 0xc)`), animated illumination sprites, and map entity anchors.
2. **Software Rasterizer & Blitter Kernel (`Qro_draw.pas`)**: High-performance assembly/C rasterization routines implementing 16-bit color-keyed, alpha-blended, and additive light-saturated pixel transfers (`ro_Clipper_LightAlpha_ColorKey_Blt` at RVA `0x00104b54`, `ro_Clipper_Alpha_Fill` at RVA `0x00104b5c`, and kernel `FUN_00104b84`).

```
+-----------------------------------------------------------------------------------+
|                        SCENE LIGHTING & RASTERIZATION PIPELINE                    |
+-----------------------------------------------------------------------------------+
                                          |
                +-------------------------+-------------------------+
                |                                                   |
                v                                                   v
    [Light Manager: DZ_Light.pas]                         [Rasterizer: Qro_draw.pas]
    - Max 30 Light Nodes (Offset 0x80)                    - 16-bit Dual Format (RGB565 / RGB555)
    - Viewport Culling & Bounds Test                      - 5 Blend Modes (Opaque, Key, Alpha, Light, Tint)
    - Animated Light Frames (DoorLight)                   - Channel Saturation Clamping (0xF800, 0x07E0)
                |                                                   |
                +----------------- Render Target -------------------+
                                          |
                                          v
                         [DirectDraw 7 Backbuffer Surface]
```

---

## 2. Low-Level Blitter Kernel Specification (`FUN_00104b84`)

The foundation of all lighting and transparency in the client is the blitter kernel `FUN_00104b84` in `Qro_draw.pas`. It handles source-to-destination sub-rectangle transfers with arbitrary scaling, viewport clipping, and mathematical blending across 16-bit pixel buffers.

### 2.1 Function Prototype & Parameters

```c
// Decompiled from aLogin.exe: FUN_00104b84 @ 0x00104b84
void FUN_00104b84(
    int dest_width,          // param_1: Destination surface width (e.g., 800)
    int dest_height,         // param_2: Destination surface height (e.g., 600)
    int dest_pitch_bytes,    // param_3: Memory offset to destination buffer
    char is_rgb565,          // param_4: 0 = RGB 555, 1 = RGB 565
    uint blend_params,       // param_5: Packed alpha level & color tint
    undefined1 blend_mode,   // param_6: 0 = Opaque, 1 = ColorKey, 2 = Alpha, 3 = LightAlpha, 4 = Tint
    int *src_rect,           // param_7: Source rectangle [left, top, right, bottom]
    int *dest_rect,          // param_8: Destination rectangle [left, top, right, bottom]
    int src_stride_bytes,    // param_9: Source pitch stride
    uint dest_pitch_pixels,  // param_10: Destination pitch in pixels
    int color_table,         // param_11: 16-bit color LUT pointer
    int src_buffer           // param_12: Source memory pointer
);
```

### 2.2 Dual Color-Space Mask Negotiation

The kernel determines color-channel bitmasks dynamically based on `param_4`:

| Format Flag (`param_4`) | Color Space | Red Mask (`local_6c`) | Green Mask (`local_70`) | Blue Mask | Red Shift | Green Shift | Blue Shift |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `0` | **RGB 555** | `0x7C00` (5 bits) | `0x03E0` (5 bits) | `0x001F` (5 bits) | `>> 10` | `>> 5` | `& 0x1F` |
| `1` | **RGB 565** | `0xF800` (5 bits) | `0x07E0` (6 bits) | `0x001F` (5 bits) | `>> 11` | `>> 5` | `& 0x1F` |

The alpha factor $uVar8$ (range $0..31$) is extracted from bits $27..31$ of `param_5`:
$$\alpha = (\text{param\_5} \gg 27) \ \& \ 0x1F$$

---

## 3. Mathematical Formulation of Blending Modes

`FUN_00104b84` executes one of five inner loops based on `param_6`:

### 3.1 Mode 0: Direct Opaque Blit
Direct memory copy from indexed color palette / source buffer into destination:
$$\text{Dest}(x, y) = \text{LUT}[\text{Src}(x, y)]$$

### 3.2 Mode 1: Color-Keyed Blit (Transparency)
Skips pixels where source color equals transparent color key (`0x0000`):
$$\text{Dest}(x, y) = \begin{cases} \text{LUT}[\text{Src}(x, y)] & \text{if } \text{Src}(x, y) \neq 0 \\ \text{Dest}(x, y) & \text{otherwise} \end{cases}$$

### 3.3 Mode 2: Standard Alpha Blending (Shadows & Translucent Windows)
Linear interpolation between destination pixel and source pixel:
$$R_{\text{out}} = \left( \frac{R_{\text{dest}} \times (31 - \alpha)}{32} \right) + \left( \frac{R_{\text{src}} \times \alpha}{32} \right)$$
$$G_{\text{out}} = \left( \frac{G_{\text{dest}} \times (31 - \alpha)}{32} \right) + \left( \frac{G_{\text{src}} \times \alpha}{32} \right)$$
$$B_{\text{out}} = \left( \frac{B_{\text{dest}} \times (31 - \alpha)}{32} \right) + \left( \frac{B_{\text{src}} \times \alpha}{32} \right)$$
Each channel is clamped to its respective bitmask ceiling:
$$R = \min(R_{\text{out}}, \text{Mask}_R), \quad G = \min(G_{\text{out}}, \text{Mask}_G), \quad B = \min(B_{\text{out}}, 0x1F)$$

### 3.4 Mode 3: Additive Light Alpha (`ro_Clipper_LightAlpha_ColorKey_Blt`)
Additive saturation used for dynamic illumination, torchlight cones, and magic glows:
$$R_{\text{out}} = R_{\text{dest}} + \left( \frac{R_{\text{src}} \times \alpha}{32} \right)$$
$$G_{\text{out}} = G_{\text{dest}} + \left( \frac{G_{\text{src}} \times \alpha}{32} \right)$$
$$B_{\text{out}} = B_{\text{dest}} + \left( \frac{B_{\text{src}} \times \alpha}{32} \right)$$
Pixels saturate up to the maximum allowable brightness without rolling over:
$$\text{Pixel}_{\text{final}} = \min(R_{\text{out}}, \text{Mask}_R) \mid \min(G_{\text{out}}, \text{Mask}_G) \mid \min(B_{\text{out}}, 0x1F)$$

### 3.5 Mode 4: Environmental Constant Tint (Day/Night Ambient Filter)
Blends the scene with a uniform atmospheric color filter (e.g., deep midnight blue, golden sunset orange, dark night purple):
$$\text{Pixel}_{\text{out}} = \text{Pixel}_{\text{src}} \times (31 - \alpha) + \text{Tint}_{\text{ambient}} \times \alpha$$

---

## 4. Scene Light Source Manager (`DZ_Light.pas`)

The scene light registry is allocated at runtime pointer `PTR_DAT_004c8d9c` and maintains an array of up to 30 light emitter nodes.

### 4.1 Light Node Memory Layout (64-byte Struct)

| Offset | Type | Description |
| :--- | :--- | :--- |
| `+0x00` | `void*` | Vtable Pointer |
| `+0x04` | `int` | World Space X Coordinate |
| `+0x08` | `int` | World Space Y Coordinate |
| `+0x10` | `double` | Animation Frame Timestamp |
| `+0x18` | `int` | Sprite Texture Handle / Resource Pointer |
| `+0x1C` | `byte` | Light Blend Mode (`3` = Additive Light) |
| `+0x20` | `int` | Frame Delay Interval (ms) |
| `+0x24` | `byte` | Total Keyframe Count (e.g., `13` for DoorLight) |
| `+0x25` | `byte` | Current Active Keyframe Index (`0..Total-1`) |
| `+0x28` | `int` | Sprite Width (px) |
| `+0x2C` | `int` | Keyframe Height Stride (px) |

### 4.2 Light Lifecycle Methods

1. **Light System Reset (`FUN_0041d830`)**:
   Clears active count byte at `param_1 + 0x80 = 0`, iterates indices `1..30`, frees loaded texture resources via `FUN_00013144`, and zeroes slot pointers.
2. **Register Light Emitter (`FUN_0041d8e4`)**:
   Checks capacity (`*(byte*)(param_1 + 0x80) < 30`). Increments count, allocates node, resolves sprite path from resource manager (`PTR_DAT_004c8a94`), computes frame stride (`SpriteHeight / TotalFrames`), and anchors coordinates.
3. **Viewport Frustum Culling (`FUN_0041da28`)**:
   Culls light nodes outside screen boundaries:
   $$\text{Camera}_X \le \text{Light}_X \le \text{Camera}_X + \text{ScreenWidth}$$
   $$\text{Camera}_Y \le \text{Light}_Y \le \text{Camera}_Y + \text{ScreenHeight}$$
   Nodes passing the test invoke `FUN_0041db1c` to blit the light cone.

---

## 5. Light Source Asset Catalog

| Resource Name | Frames | Base Size | Anchor Point | Usage Scenario |
| :--- | :--- | :--- | :--- | :--- |
| **`DoorLight`** | 13 | $128 \times 128$ px | Center ($X - W/2, Y - H + H/4$) | Standard doorway portals, house entrances |
| **`SmallDoorLight`** | 4 | $64 \times 64$ px | Center ($X - W/2, Y - H + H/4$) | Tent entrance flap, small cave passages |
| **`SkillLight\S10061`** | Variable | $256 \times 256$ px | Actor Origin | Fire/Light element spell charge aura |
| **`CampFireLight`** | 8 | $192 \times 192$ px | Ground Fireplace | Tent cooking fires, outdoor bivouac |

---

## 6. Day/Night Cycle Environmental States

The client server clock updates environmental ambient tinting across 4 distinct global diurnal phases:

```
[Dawn: 05:00 - 08:00] ----> [Day: 08:00 - 18:00] ----> [Dusk: 18:00 - 21:00] ----> [Night: 21:00 - 05:00]
 (Rose/Gold Ambient)         (100% Brightness)          (Orange/Purple)              (Midnight Blue Overlay)
 (Alpha: 8/31)               (Alpha: 0/31, Opaque)      (Alpha: 14/31)               (Alpha: 24/31 + Lights)
```

During **Night Phase**, the ambient overlay diminishes visibility, causing localized light emitters (`DoorLight`, tent windows, lanterns) to produce vivid high-contrast light halos on the DirectDraw backbuffer.

---

## 7. C# Server Implementation Blueprint

```csharp
// SceneLightingService.cs - Wonderland Private Server Ambient & Light Engine
using System;
using System.Collections.Generic;

namespace Wonderland.Server.Environment
{
    public enum DiurnalPhase : byte
    {
        Dawn  = 0,
        Day   = 1,
        Dusk  = 2,
        Night = 3
    }

    public sealed class SceneLightingService
    {
        public DiurnalPhase GetCurrentPhase(DateTime serverTime)
        {
            int hour = serverTime.Hour;
            if (hour >= 5 && hour < 8)   return DiurnalPhase.Dawn;
            if (hour >= 8 && hour < 18)  return DiurnalPhase.Day;
            if (hour >= 18 && hour < 21) return DiurnalPhase.Dusk;
            return DiurnalPhase.Night;
        }

        public byte[] BuildAmbientSyncPacket(ushort mapId, DateTime serverTime)
        {
            var phase = GetCurrentPhase(serverTime);
            byte alpha = phase switch
            {
                DiurnalPhase.Dawn  => 8,
                DiurnalPhase.Day   => 0,
                DiurnalPhase.Dusk  => 14,
                DiurnalPhase.Night => 24,
                _ => 0
            };

            ushort tintColor565 = phase switch
            {
                DiurnalPhase.Dawn  => 0xFBA4, // Soft Rose
                DiurnalPhase.Day   => 0xFFFF, // Full White
                DiurnalPhase.Dusk  => 0xFA88, // Warm Amber
                DiurnalPhase.Night => 0x18E8, // Deep Indigo
                _ => 0xFFFF
            };

            // Action Code 0x14:0x18 Environmental Ambient Sync
            return new byte[]
            {
                0xF4, 0x44, // Magic
                0x07, 0x00, // Length
                0x14, 0x18, // Ambient Sync
                (byte)(mapId & 0xFF),
                (byte)((mapId >> 8) & 0xFF),
                (byte)phase,
                alpha,
                (byte)(tintColor565 & 0xFF),
                (byte)((tintColor565 >> 8) & 0xFF)
            };
        }
    }
}
```

---

## 8. Verification & Cross-References

- **Disassembly Offsets**: `aLogin.exe` RVA `0x00104b54` (`ro_Clipper_LightAlpha_ColorKey_Blt`), `0x00104b5c` (`ro_Clipper_Alpha_Fill`), `0x00104b84` (Blitter Kernel), `0x0041d8e4` (Add Light), `0x0041da28` (Cull & Render).
- **Mask Offsets**: Red `0x7C00`/`0xF800`, Green `0x03E0`/`0x07E0`, Blue `0x001F`.
- **String References**: Line 307567 (`"DoorLight"`), Line 307572 (`"SmallDoorLight"`), Line 310507 (`"SkillLight\\"`).
- **Related Specs**:
  - [`14_directx_multimedia_engine_directdraw_directsound_and_directshow.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/14_directx_multimedia_engine_directdraw_directsound_and_directshow.md)
  - [`23_weather_particle_engine_precipitation_and_ambient_rendering.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/23_weather_particle_engine_precipitation_and_ambient_rendering.md)
  - [`34_directdraw_7_rendering_pipeline_and_hardware_surface_architecture.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/34_directdraw_7_rendering_pipeline_and_hardware_surface_architecture.md)
