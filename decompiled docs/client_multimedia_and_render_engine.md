# Client Multimedia & Render Engine Decompiled Specifications

This document outlines the client-side graphics render buffers (DirectDraw/Direct3D), sound wrapper hooks (DirectSound), and static wave audio assets (`sound\*.wav`) extracted from `aLogin.exe.1.c`.

---

## 1. Graphics Renderer (DirectDraw & Direct3D)

The client initializes hardware acceleration dynamically using standard Windows DirectX APIs:

- **Dynamic Linking**: Loads `"DDraw.dll"` using runtime API lookups:
  - `DirectDrawCreate` / `DirectDrawCreateEx` (allocates raw screen rendering contexts).
  - `DirectDrawEnumerateA` (probes compatible graphics devices).
- **Interface Allocations**:
  - `IDirectDraw7`: Core drawing controller context.
  - `IDirectDrawSurface7`: Primary and back surface buffers used for double-buffered 2D canvas drawing.
- **Palette Allocations (`IDirectDrawPalette`)**:
  - Manages color layouts for sprites and textures:
    - Palette structure signature: `palVersion = 0x300` (DirectX Palette specification).
    - Allocates structures containing 256 entries (`palNumEntries = 0x100`) mapped to RGBQUAD buffers.

---

## 2. Sound Wrapper Engine (DirectSound)

Sound effects and background music streaming are routed through DirectX audio buffers:

- **Dynamic Linking**: Loads `"DSound.dll"`:
  - `DirectSoundCreate`: Initializes secondary sound cards.
  - `IDirectSoundBuffer`: Configures play, loop, and pan offsets for audio channels.

---

## 3. Game Audio Wave File Assets (`sound\*.wav`)

The client references specific wave audio assets matching in-game interactions:

### A. Background Music Tracks (BGM)
- **`sound\BGM0019.wav`**: Played on the login and character select screens.
- **`Sound\BGM0003.wav` / `Sound\BGM0013.wav` / `Sound\BGM0014.wav`**: Environment music themes loaded on specific warp coordinates.

### B. User Interface Sound Effects (SE)
- **`sound\wav0152.wav`**: Generic UI click confirmation sound effect.
- **`sound\wav0150.wav`**: Dialogue and alert window popup sound effect.
- **`sound\wav0154.wav` / `sound\wav0153.wav`**: Menu transitions sound effect.

### C. Combat & Level Up Sound Effects
- **`Sound\SEB0010.wav`**: Triggers when a character gains a level (level-up bells sound effect).
- **`Sound\SEB0156.wav` / `Sound\SEB0173.wav` / `Sound\SEB0221.wav`**: Spell casting, sword slashes, and combat strike sounds.
