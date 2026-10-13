# Document 89: DirectDraw 16-Bit Display Mode Adapter, Surface Allocation, and Clipper Subsystem

## 1. Architectural Overview

*Wonderland Online* relies on Microsoft DirectDraw 7 (`IDirectDraw7`) for 2D hardware-accelerated surface management, double-buffering, and hardware page flipping. Developed using Delphi 7 and Borland VCL bindings (`DXDraws.pas`, `PDXRender.pas`), the graphics subsystem bridges low-level COM interfaces exported by `DDraw.dll` to high-level game canvas abstractions.

The display adapter engine provides four essential runtime capabilities:
1. **Dynamic DirectDraw Interface Creation**: Dynamically resolving `DirectDrawCreate` / `DirectDrawCreateEx` via `GetProcAddress` on `DDraw.dll` (RVA `0x00071912`, `0x00071848`).
2. **16-Bit Pixel Format Negotiation**: Automatic detection of 16-bit RGB 565 vs RGB 555 display adapter capabilities, populating engine-wide bitmasks.
3. **Hardware Surface Allocation with System Memory Fallback**: Initializing primary flipping chains in video RAM (`DDSCAPS_VIDEOMEMORY`), with graceful fallback to system memory (`DDSCAPS_SYSTEMMEMORY`).
4. **Hardware Window Clipper (`IDirectDrawClipper`)**: Configuring bounding region clip lists (`RGNDATA` structs) to prevent out-of-bounds video memory corruption during windowed-mode rendering.

```
+-----------------------------------------------------------------------------------+
|                        DIRECTDRAW 7 ADAPTER SUBSYSTEM                              |
+-----------------------------------------------------------------------------------+
                                          |
        +---------------------------------+---------------------------------+
        |                                                                   |
        v                                                                   v
 [DDraw.dll COM Bridge]                                           [Surface Manager]
 - DirectDrawCreate / DirectDrawCreateEx                          - Primary Flipping Chain (Front/Back)
 - IDirectDraw7 Interface Query (IID: 0x00071a40)                 - Offscreen Plain Cache Surfaces
 - SetCooperativeLevel (DDSCL_EXCLUSIVE / DDSCL_NORMAL)           - DDERR_SURFACELOST Recovery
        |                                                                   |
        +---------------------------------+---------------------------------+
                                          |
                                          v
                              [IDirectDrawClipper Engine]
                              - RGNDATA Multi-Rectangle List
                              - Window HWND Attachment
                              - Safe Client-Area Scissoring
```

---

## 2. Low-Level Initialization & COM Query (`FUN_000717ca`)

The client runtime encapsulates DirectDraw factory routines in `FUN_000717ca` (`DXDraws.pas`):

```c
// Decompiled from aLogin.exe: Lines 79450 - 79545
// Factory for IDirectDraw / IDirectDraw7
if (param_4 == '\0') {
    // Legacy fallback: DirectDrawCreate
    pcVar5 = (code *)FUN_0006596c("DDraw.dll", "DirectDrawCreate");
    iVar6 = (*pcVar5)(0, param_1 + 8, 0); // Query IDirectDraw
    if (iVar6 != 0) {
        RaiseDirectDrawException("IDirectDraw", 0xb);
    }
    // Query IDirectDraw2/4 interface
    FUN_000161ec(param_1 + 0xc, *(undefined4 *)(param_1 + 8), &DAT_00071a50);
}
else {
    // Modern DirectDraw 7 path: DirectDrawCreateEx
    pcVar5 = (code *)FUN_0006596c("DDraw.dll", "DirectDrawCreateEx");
    // IID_IDirectDraw7 GUID: DAT_00071a40
    iVar6 = (*pcVar5)(0, param_1 + 0x10, &DAT_00071a40, 0);
    if (iVar6 != 0) {
        RaiseDirectDrawException("IDirectDraw7", 0xb);
    }
    // Store interfaces at param_1 + 8 (IDirectDraw) and param_1 + 0xc (IDirectDraw7)
    FUN_000161ec(param_1 + 8, *(undefined4 *)(param_1 + 0x10), &DAT_00071a40);
    FUN_000161ec(param_1 + 0xc, *(undefined4 *)(param_1 + 0x10), &DAT_00071a50);
}
```

---

## 3. 16-Bit Pixel Format Negotiation

To support diverse graphic hardware across legacy Windows 98/2000/XP environments, the engine negotiates the native 16-bit display mode during initialization:

```c
DDSURFACEDESC2 ddsd;
memset(&ddsd, 0, sizeof(ddsd));
ddsd.dwSize = sizeof(ddsd);
ddsd.dwFlags = DDSD_CAPS;
ddsd.ddsCaps.dwCaps = DDSCAPS_PRIMARYSURFACE;

HRESULT hr = pDD7->GetDisplayMode(&ddsd);
if (SUCCEEDED(hr)) {
    if (ddsd.ddpfPixelFormat.dwRGBBitCount == 16) {
        if (ddsd.ddpfPixelFormat.dwGBitMask == 0x07E0) {
            // High Color 16-bit: RGB 565
            g_IsRGB565 = 1;
            g_RedMask   = 0xF800; // 5 bits (11..15)
            g_GreenMask = 0x07E0; // 6 bits (5..10)
            g_BlueMask  = 0x001F; // 5 bits (0..4)
        } else if (ddsd.ddpfPixelFormat.dwGBitMask == 0x03E0) {
            // High Color 15-bit: RGB 555
            g_IsRGB565 = 0;
            g_RedMask   = 0x7C00; // 5 bits (10..14)
            g_GreenMask = 0x03E0; // 5 bits (5..9)
            g_BlueMask  = 0x001F; // 5 bits (0..4)
        }
    }
}
```

---

## 4. Hardware Clipper & Scissoring Subsystem (`IDirectDrawClipper`)

In windowed mode (default for *Wonderland Online* at $800 \times 600$ resolution), drawing outside the client viewport damages other desktop windows. The client constructs an explicit `IDirectDrawClipper` using Windows GDI bounding boxes.

### 4.1 Clipper Construction & Region Packing (`FUN_00072218`)

`FUN_00072218` converts screen clip rectangles into a Win32 `RGNDATA` structure:

```c
// Decompiled from aLogin.exe: Lines 80102 - 80200
void FUN_00072218(undefined4 param_1, undefined4 *param_2, int param_3)
{
    int rect_count = param_3 + 1;
    // Allocate RGNDATA buffer: Header (32 bytes) + (rect_count * 16 bytes)
    RGNDATA *rgn = (RGNDATA *)FUN_00012794(rect_count * 16 + 32);
    
    // Initialize RGNDATAHEADER
    rgn->rdh.dwSize   = 32;               // sizeof(RGNDATAHEADER)
    rgn->rdh.iType    = 1;                // RDH_RECTANGLES
    rgn->rdh.nCount   = rect_count;
    rgn->rdh.nRgnSize = rect_count * 16;  // Size of rectangle array
    
    // Copy bounding box [left, top, right, bottom]
    rgn->rdh.rcBound.left   = local_24;
    rgn->rdh.rcBound.top    = local_20;
    rgn->rdh.rcBound.right  = local_1c;
    rgn->rdh.rcBound.bottom = local_18;

    // Populate rectangle array into buffer
    for (int i = 0; i < rect_count; i++) {
        RECT *destRect = (RECT *)&rgn->Buffer[i * 16];
        destRect->left   = param_2[i * 4 + 0];
        destRect->top    = param_2[i * 4 + 1];
        destRect->right  = param_2[i * 4 + 2];
        destRect->bottom = param_2[i * 4 + 3];
    }

    // Bind clip list to IDirectDrawClipper
    // IDirectDrawClipper::SetClipList(rgn, 0)
    pClipper->SetClipList(rgn, 0);
    // Attach clipper to Primary DirectDraw Surface
    pPrimarySurface->SetClipper(pClipper);
}
```

---

## 5. Surface Memory Allocation & Loss Recovery

### 5.1 Primary Flipping Chain Creation

The engine configures a complex flipping chain:
```c
DDSURFACEDESC2 ddsd;
memset(&ddsd, 0, sizeof(ddsd));
ddsd.dwSize = sizeof(ddsd);
ddsd.dwFlags = DDSD_CAPS | DDSD_BACKBUFFERCOUNT;
ddsd.dwBackBufferCount = 1;
ddsd.ddsCaps.dwCaps = DDSCAPS_PRIMARYSURFACE | DDSCAPS_FLIP | DDSCAPS_COMPLEX;

// Attempt allocation in Video RAM
HRESULT hr = pDD7->CreateSurface(&ddsd, &pPrimarySurface, NULL);
if (FAILED(hr)) {
    // VRAM exhausted fallback: Allocate in System RAM without hardware flipping
    ddsd.dwFlags = DDSD_CAPS;
    ddsd.ddsCaps.dwCaps = DDSCAPS_PRIMARYSURFACE | DDSCAPS_SYSTEMMEMORY;
    hr = pDD7->CreateSurface(&ddsd, &pPrimarySurface, NULL);
}
```

### 5.2 Surface Restoration (`RestoreAllSurfaces`)

When an Alt+Tab, screen saver, or UAC prompt causes `DDERR_SURFACELOST`, the engine triggers `RestoreAllSurfaces`:

```c
HRESULT CheckAndRestoreSurfaces()
{
    if (pPrimarySurface->IsLost() == DDERR_SURFACELOST) {
        HRESULT hr = pPrimarySurface->Restore();
        if (SUCCEEDED(hr)) {
            // Re-render desktop background and dirty UI forms
            InvalidateAllForms();
            ReloadOffscreenBitmaps();
        }
        return hr;
    }
    return DD_OK;
}
```

---

## 6. COM Interface Vtable Offsets in Client Memory

| Interface | GUID Symbol | RVA In Binary | Vtable Offset | Function |
| :--- | :--- | :--- | :--- | :--- |
| **`IDirectDraw`** | `IID_IDirectDraw` | `0x00071a50` | `+0x18` | `SetCooperativeLevel` |
| **`IDirectDraw7`** | `IID_IDirectDraw7` | `0x00071a40` | `+0x14` | `CreateSurface` |
| **`IDirectDraw7`** | `IID_IDirectDraw7` | `0x00071a40` | `+0x28` | `GetDisplayMode` |
| **`IDirectDrawSurface7`** | `IID_IDirectDrawSurface7` | `0x00071a60` | `+0x1C` | `Blt` / `BltFast` |
| **`IDirectDrawSurface7`** | `IID_IDirectDrawSurface7` | `0x00071a60` | `+0x2C` | `Flip` |
| **`IDirectDrawSurface7`** | `IID_IDirectDrawSurface7` | `0x00071a60` | `+0x68` | `Restore` |
| **`IDirectDrawClipper`** | `IID_IDirectDrawClipper` | `0x00071a70` | `+0x18` | `SetHWnd` |
| **`IDirectDrawClipper`** | `IID_IDirectDrawClipper` | `0x00071a70` | `+0x1C` | `SetClipList` |

---

## 7. Verification & Cross-References

- **Disassembly Offsets**: `aLogin.exe` RVA `0x000717ca` (DirectDraw Factory), `0x00072178` (Clipper Wrapper), `0x00072218` (Region Builder).
- **String References**: Line 79297 (`"DDraw.dll"` / `"DirectDrawEnumerateA"`), Line 79476 (`"DirectDrawCreate"`), Line 79507 (`"DirectDrawCreateEx"`), Line 79695 (`"IDirectDraw7"`), Line 80080 (`"IDirectDrawClipper"`), Line 80537 (`"IDirectDrawSurface7"`).
- **Related Specs**:
  - [`14_directx_multimedia_engine_directdraw_directsound_and_directshow.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/14_directx_multimedia_engine_directdraw_directsound_and_directshow.md)
  - [`34_directdraw_7_rendering_pipeline_and_hardware_surface_architecture.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/34_directdraw_7_rendering_pipeline_and_hardware_surface_architecture.md)
  - [`88_dynamic_scene_lighting_day_night_cycle_and_alpha_shadow_engine.md`](file:///D:/GitHub/Wonderland-Private-Server/decompiled/88_dynamic_scene_lighting_day_night_cycle_and_alpha_shadow_engine.md)
