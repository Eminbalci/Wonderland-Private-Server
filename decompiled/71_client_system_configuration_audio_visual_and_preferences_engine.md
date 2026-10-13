# Document 71: Client System Configuration, Audio/Visual, and Preferences Engine

## 1. Executive Summary & Engine Architecture

In Wonderland Online, client audio-visual settings, performance tuning, and runtime preferences are managed through the system options engine designated as `Form_System_2`. Reverse-engineered from client routines at RVA `0x002A1000` (constructor and dispatcher `FUN_002a1120`, callback handlers `LAB_002a13d4`, `LAB_002a13c8`, and `LAB_002a144c`), this subsystem provides in-game controls for DirectSound volume mixing, graphics detail scaling, resolution switching, keyboard shortcuts, and chat filtering.

The dialog window `Form_System_2` ($451 \times 388$ px) acts as the bridge between the Windows runtime environment and the game client's internal media pipelines. All user adjustments are validated against DirectDraw surface capabilities and DirectSound buffer limits, immediately applied to the active rendering loop, and serialized to disk in the binary preference configuration file `user\save.dat`.

```
+------------------------------------------------------------------------------------+
|                         CLIENT SYSTEM CONFIGURATION ENGINE                         |
+------------------------------------------------------------------------------------+
                                      |
         +----------------------------+----------------------------+
         | Audio Subsystem                                         | Video & Display Subsystem
         v                                                         v
  [DirectSound Mixer]                                       [DirectDraw Surface Pipeline]
  - BGM Volume: Btn_setup_1 (Slider 0..100)                 - Resolution Mode: 800x600 Windowed / Full
  - SFX Volume: Btn_setup_1 (Slider 0..100)                 - Frame Limiter: 60 FPS VSync Toggle
  - Mute Toggles: Btn_On_1 / Btn_Off_1                      - Animation Quality: Low / Med / High
         |                                                         |
         +----------------------------+----------------------------+
                                      |
                                      v
                      [Form_System_2 Dialog (451x388 px)]
                      Header: icon_System | Dismiss: [ESC]
                                      |
                                      v
                      [Preferences Storage: user\save.dat]
                     Binary Serialization of Client Profile
```

---

## 2. Memory Layouts & Data Structures

### 2.1 System Settings Profile (`0x20` = 32 Bytes)
User preferences are maintained in a contiguous 32-byte memory buffer within global client context `PTR_DAT_004c96d0`:

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `byte` | `BgmVolume` | Background music volume ($0..100$, 0 = Muted) |
| `+0x01` | `byte` | `SfxVolume` | Sound effects volume ($0..100$, 0 = Muted) |
| `+0x02` | `byte` | `BgmEnabled` | Boolean toggle for background music playback |
| `+0x03` | `byte` | `SfxEnabled` | Boolean toggle for sound effect playback |
| `+0x04` | `byte` | `DisplayMode` | `0` = Windowed 800x600, `1` = Fullscreen Exclusive Mode |
| `+0x05` | `byte` | `GraphicsQuality`| Particle and weather density ($1$ = Low, $2$ = Med, $3$ = High) |
| `+0x06` | `byte` | `ShowPlayerNames`| Overhead character nameplate rendering toggle |
| `+0x07` | `byte` | `ShowHpSpBars` | Persistent party/pet HP & SP gauge display |
| `+0x08` | `byte` | `LockWhispers` | Privacy toggle rejecting incoming private messages |
| `+0x09` | `byte` | `LockTrade` | Automatic rejection of incoming player trade requests |
| `+0x0A` | `byte` | `LockParty` | Automatic rejection of party team invitations |
| `+0x0B` | `byte` | `LockDuel` | Automatic rejection of PvP combat challenge requests |
| `+0x0C` | `uint` | `FrameRateCap` | Target display refresh rate (default 60 FPS) |
| `+0x10` | `byte[16]`| `Reserved` | Reserved engine metadata |

### 2.2 `Form_System_2` Form Instance Layout (`param_1`)

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `void*` | `VTable` | Delphi component virtual method table pointer |
| `+0x04` | `uint` | `FormHandle` | DirectDraw surface bitmap handle |
| `+0x4C` | `uint` | `BtnBgmSetup` | BGM configuration button (`"Btn_setup_1"`) |
| `+0x4D` | `uint` | `BtnSfxSetup` | SFX configuration button (`"Btn_setup_1"`) |
| `+0x4E` | `uint` | `BtnDisplaySetup`| Video display mode button (`"Btn_setup_1"`) |
| `+0x4F` | `uint` | `BtnClose1` | Bottom dismiss button (`"Btn_Close_1"`, caption `"[ESC]"`) |
| `+0x50` | `uint` | `BtnCloseS1` | Top-right small close button (`"Btn_Close_s_1"`) |
| `+0x51` | `uint` | `IconSystem` | Window title icon surface (`"icon_System"`) |
| `+0x52` | `int*[4]` | `ExternalLinks`| Array of external launch buttons (Website, Readme, Chat Settings) |
| `+0x89` | `uint` | `BtnOn1` | State toggle button ("ON" state bitmap) |
| `+0x8A` | `uint` | `BtnOff1` | State toggle button ("OFF" state bitmap) |
| `+0x8D` | `uint` | `IconVolumeOff`| Muted audio slider track indicator (`"icon_VolumeBarOff"`) |

---

## 3. UI Form Geometry, Coordinates & Controls

### 3.1 `Form_System_2` Layout Metrics
- **Dimensions:** Width `0x1C3` (451 pixels), Height `0x184` (388 pixels).
- **Positioning:** Centered dynamically upon viewport:
  $$\text{Form X} = \frac{\text{ViewportWidth} - 451}{2}, \quad \text{Form Y} = \frac{\text{ViewportHeight} - 388}{2}$$
- **Controls & Buttons:**
  - `"Btn_Close_1"`: $X = \text{0xA6}$ (166 px), $Y = \text{0x19C}$ (412 px), size $20 \times 56$ px, caption `"[ESC]"`.
  - `"Btn_Close_s_1"`: $X = \text{0x152}$ (338 px), $Y = \text{0x18}$ (24 px), size $18 \times 18$ px.
  - `"Btn_setup_1"` (BGM): $X = \text{0x8F}$ (143 px), $Y = \text{0xA0}$ (160 px).
  - `"Btn_setup_1"` (SFX): $X = \text{0x8F}$ (143 px), $Y = \text{0x86}$ (134 px).
  - `"Btn_setup_1"` (Display): $X = \text{0x8F}$ (143 px), $Y = \text{0xED}$ (237 px).
  - `"Btn_OfficialWeb_1"`: $X = \text{0xD5}$ (213 px), $Y = \text{0x107}$ (263 px).
  - `"Btn_Readme_1"`: $X = \text{0xD5}$ (213 px), $Y = \text{0x120}$ (288 px).
  - `"Btn_SetMsg"`: $X = \text{0xD5}$ (213 px), $Y = \text{0x139}$ (313 px).

```
+---------------------------------------------------------------------------------+
| Form_System_2: Client System Configuration (451 x 388 px)                       |
+---------------------------------------------------------------------------------+
| [icon_System] System Preferences                                            [X] |
+---------------------------------------------------------------------------------+
| AUDIO CONFIGURATION:                                                            |
| Background Music (BGM):  [ON / OFF]  Volume: [||||||||||||||||----] [Setup]     |
| Sound Effects (SFX):     [ON / OFF]  Volume: [||||||||||||||||||||] [Setup]     |
|                                                                                 |
| DISPLAY & GRAPHICS:                                                             |
| Screen Mode:             [Windowed 800x600] / [Fullscreen]          [Setup]     |
| Graphics Particle Detail: [High Quality]                                        |
|                                                                                 |
| PRIVACY & SECURITY:                                                             |
| [X] Reject Unsolicited Trade Requests      [X] Reject PvP Arena Challenges      |
| [ ] Reject Party Group Invitations         [ ] Reject Whisper Messages          |
|                                                                                 |
| DOCUMENTATION & SUPPORT:                                                        |
| [Btn_OfficialWeb_1: Website]  [Btn_Readme_1: Manual]  [Btn_SetMsg: Chat Filter] |
+---------------------------------------------------------------------------------+
|                                                   [Btn_Close_1: Dismiss [ESC]]  |
+---------------------------------------------------------------------------------+
```

---

## 4. Audio Mixing & Video Mode Hardware Integration

### 4.1 DirectSound Mixer Pipeline
Audio sliders map directly to logarithmic millibel attenuation values required by `IDirectSoundBuffer::SetVolume`:

$$\text{Volume}_{\text{mB}} = \begin{cases} 
-10000\text{ mB (Silence)}, & \text{if } \text{Slider} = 0 \\
2000 \times \log_{10}\left(\frac{\text{Slider}}{100}\right)\text{ mB}, & \text{if } \text{Slider} > 0
\end{cases}$$

- Value `100` maps to $0\text{ mB}$ (Maximum hardware gain).
- Value `50` maps to $-602\text{ mB}$ (Half linear acoustic power).
- Toggle `"Btn_Off_1"` instantly calls `IDirectSoundBuffer::Stop` and zeroes the hardware stream.

### 4.2 Preferences Persistence (`user\save.dat`)
When closing the window via `[ESC]`, the client writes modified registers to disk:
- Target path: `user\save.dat`
- Encoding: Little-endian binary stream.
- Integrity verification: 16-bit additive checksum appended to the final 2 bytes.

---

## 5. Private Server Implementation Guide & C# Production Code

The following service provides server-side persistence for player system preferences, privacy flags, and audio-visual configuration profiles.

```csharp
using System;
using System.Collections.Concurrent;
using System.IO;

namespace Wonderland.Server.Services.Configuration
{
    public sealed class PlayerSystemPreferences
    {
        public uint CharacterId { get; init; }
        public byte BgmVolume { get; set; } = 80;
        public byte SfxVolume { get; set; } = 90;
        public bool BgmEnabled { get; set; } = true;
        public bool SfxEnabled { get; set; } = true;
        public byte DisplayMode { get; set; } = 0; // 0 = Windowed, 1 = Fullscreen
        public byte GraphicsQuality { get; set; } = 3; // High
        public bool RejectTrade { get; set; } = false;
        public bool RejectParty { get; set; } = false;
        public bool RejectDuel { get; set; } = false;
        public bool RejectWhispers { get; set; } = false;

        public byte[] Serialize()
        {
            using var ms = new MemoryStream(32);
            using var bw = new BinaryWriter(ms);
            bw.Write(BgmVolume);
            bw.Write(SfxVolume);
            bw.Write(BgmEnabled);
            bw.Write(SfxEnabled);
            bw.Write(DisplayMode);
            bw.Write(GraphicsQuality);
            bw.Write(RejectTrade);
            bw.Write(RejectParty);
            bw.Write(RejectDuel);
            bw.Write(RejectWhispers);
            bw.Write(new byte[22]); // Reserved
            return ms.ToArray();
        }

        public static PlayerSystemPreferences Deserialize(uint characterId, byte[] data)
        {
            if (data == null || data.Length < 10) return new PlayerSystemPreferences { CharacterId = characterId };

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);
            return new PlayerSystemPreferences
            {
                CharacterId = characterId,
                BgmVolume = br.ReadByte(),
                SfxVolume = br.ReadByte(),
                BgmEnabled = br.ReadBoolean(),
                SfxEnabled = br.ReadBoolean(),
                DisplayMode = br.ReadByte(),
                GraphicsQuality = br.ReadByte(),
                RejectTrade = br.ReadBoolean(),
                RejectParty = br.ReadBoolean(),
                RejectDuel = br.ReadBoolean(),
                RejectWhispers = br.ReadBoolean()
            };
        }
    }

    public interface ISystemConfigurationService
    {
        PlayerSystemPreferences GetPreferences(uint characterId);
        void UpdatePreferences(uint characterId, PlayerSystemPreferences preferences);
    }

    public sealed class SystemConfigurationService : ISystemConfigurationService
    {
        private readonly ConcurrentDictionary<uint, PlayerSystemPreferences> _cache = new();

        public PlayerSystemPreferences GetPreferences(uint characterId)
        {
            return _cache.GetOrAdd(characterId, id => new PlayerSystemPreferences { CharacterId = id });
        }

        public void UpdatePreferences(uint characterId, PlayerSystemPreferences preferences)
        {
            if (preferences == null) throw new ArgumentNullException(nameof(preferences));
            _cache[characterId] = preferences;
        }
    }
}
```
