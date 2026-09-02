# Decompiled Client Engine: Memory Map, Assets & Subsystems

## 1. Primary Global Memory Map (`PTR_DAT_...`)

| Pointer Address | Access Count | Internal Subsystem & Structure Description |
| :--- | :--- | :--- |
| `PTR_DAT_004c9918` | **5,402** | **Main Player Context (`PlayerObject`):** Stores coordinates (`+0x20`, `+0x24`), active inventory (`+0x27e8`), companion pointers (`+0x21e8`), and stat flags. |
| `PTR_DAT_004c8d40` | **3,521** | **Client Toast & Alert Dispatcher (`UIManager`):** `ShowSystemToast(msg, duration_ms, ...)` at vtable offset `+0x9c`. |
| `PTR_DAT_004c8a94` | **2,550** | **Asset Icon & Sprite Resource Pool:** Caches UI sprites, rail icons, and status glyphs. |
| `PTR_DAT_004c94f8` | **2,114** | **Item & Equipment Master Table (`ItemTable`):** Deserialized item metadata, weapon classes, and equipment attributes. |
| `PTR_DAT_004c87fc` | **1,585** | **String Table & Localization Engine:** Text formatting and dialogue string lookup. |
| `PTR_DAT_004c94b0` | **1,135** | **Combat Scene Manager (`BattleManager`):** Active battle slots, round timers, and combatant indices. |
| `PTR_DAT_004c96d0` | **947** | **Main VCL Application Root (`AppRoot`):** Root window instance, active skin, and modal dialog stacks. |
| `PTR_DAT_004c9134` | **739** | **Active Pet Array (`PetArray`):** Memory array tracking up to 4 companion objects. |
| `PTR_DAT_004c864c` | **480** | **Map Scene & Collision Grid (`MapGrid`):** 2D collision matrix, walkable tile flags, and warp portals. |

---

## 2. Client Asset & Data Format Architecture

### 2.1 File Formats & Archives
* **Map & World Layouts:** `Data\Ground.MMG`, `Data\Wem.MMG`, `\Pic\Grid.bmp`.
* **Sprite & Model Archives:** `Data\odd.dat`, `Data\odd_d01.dat`, `dat\cuts.dat`, `Lbd.dat`, `\Data\Gec.Dat`.
* **Client User State:** `user\AccountList.dat`, `user\save.dat`, `User\ActionInfo.dat`, `\MailData.dat`, `\AllOrgEnsign.dat`, `UnReadMsg.dat`.
* **Binary Executables:** `aLogin.exe` (Launcher), `Main.exe` (Game Engine), `Update.EXE` (Patcher), `Mainupdate.exe`.

### 2.2 Audio & DirectSound Integration
* **Sound Engine:** DirectSound (`DSound.dll`).
* **Background Music (BGM):** 20+ WAV/MP3 tracks (`Sound\BGM0003.wav` to `BGM0019.wav`).
* **Combat Sound Effects (SFX):** 30+ weapon and magic effects (`Sound\SEB0008.wav` to `SEB0300.wav`).
* **UI Feedback Audio:** 25+ interface notification cues (`Sound\WAV0149.wav` to `WAV9916.wav`).

---

## 3. UI Framework & VCL Forms (450+ Controls)
* **Skin Engine:** `Menu\Skins\White\Form_CreateRole_1.jpg` – `Form_CreateRole_4.jpg`, `Form_IdPassword_1.jpg`.
* **Guild & Military Controls:** `Btn_ArmyList2`, `Btn_ArmyMail`, `Btn_ArmyMain`.
* **Mounts & Transport:** `Btn_AssignRide_1`.
* **Economy & Trade:** `Btn_Buy_1`, `Btn_BlockMsg`.
