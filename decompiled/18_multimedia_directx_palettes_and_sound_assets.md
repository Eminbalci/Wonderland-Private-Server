# Multimedia DirectX Palettes and Sound Assets Specification

## 1. DirectDraw 2D Palette Engine (`IDirectDrawPalette`)

The client manages 8-bit palettized sprites and color tables via DirectDraw hardware palettes:

* **Palette Version Signature:** `palVersion = 0x300` (DirectX Palette specification).
* **Entry Count:** Allocates 256 color palette entries (`palNumEntries = 0x100`) mapped to Win32 `RGBQUAD` structures.
* **Double Buffering:** Implemented via `IDirectDrawSurface7` primary and back buffer flippings.

---

## 2. DirectSound Audio Buffers (`IDirectSoundBuffer`)

Audio playback is divided into background music streaming and discrete sound effect channels:

* **Sound Device Initialization:** `DirectSoundCreate` allocates hardware or emulated audio buffers.
* **Volume, Pan & Looping:** Configures pan offsets and volume levels via `IDirectSoundBuffer::SetVolume` and `SetPan`.

---

## 3. Client Audio Asset Catalog (`sound\*.wav`)

The client references specific wave audio assets matching in-game interactions:

### 3.1 Background Music Tracks (BGM)
* `sound\BGM0019.wav`: Played on the login and character selection screens.
* `sound\BGM0003.wav`: Welling Village environment theme.
* `sound\BGM0013.wav` / `sound\BGM0014.wav`: Kelan Village and ocean voyage themes.

### 3.2 User Interface Sound Effects (SE)
* `sound\wav0150.wav`: Dialogue bubble and alert window popup sound.
* `sound\wav0152.wav`: Generic UI button click confirmation sound.
* `sound\wav0153.wav` / `sound\wav0154.wav`: Menu open, tab switch, and bag transition sounds.

### 3.3 Combat & Progression Sound Effects
* `sound\SEB0010.wav`: Character / companion level-up bells sound effect.
* `sound\SEB0156.wav`: Melee sword slash sound effect.
* `sound\SEB0173.wav`: Critical hit / physical blow impact sound effect.
* `sound\SEB0221.wav`: Elemental magic spell cast release sound effect.
