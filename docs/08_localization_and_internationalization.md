# 08 - English Localization and Internationalization Specification

## 1. Overview and Architecture

The Wonderland Private Server administration supervisor and network action code handler have been fully standardized to 100% English. This ensures cross-border team collaboration, consistent diagnostic log parsing, and unified client-server messaging.

---

## 2. Localized Subsystems and Components

### 2.1 GUI Administration Interface ([`Src/Gui/MainForm1.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.cs) & [`Src/Gui/MainForm1.Designer.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.Designer.cs))

| Component / Element | Previous (Turkish) | Current (English) | Usage Context |
| :--- | :--- | :--- | :--- |
| Client Selector Dialog | `WLO Client Klasörünü Seçin` | `Select Wonderland Online Client Directory` | FolderBrowserDialog title during initial setup |
| Client Validation Success | `Client klasörü başarıyla ayarlandı.` | `Client directory set successfully.` | Informational modal upon locating game assets |
| Client Validation Warning | `Lütfen geçerli bir WLO Client klasörü seçin!` | `Please select a valid WLO Client folder containing game data files!` | Error dialog when missing `data` or `ini` files |
| Broadcast Red Option | `Kırmızı (GM Duyurusu)` | `Red (GM Announcement)` | Broadcast dropdown `cmbBroadcastColor` (AC 2:4) |
| Broadcast Yellow Option | `Sarı (Dünya Sohbeti)` | `Yellow (World Chat)` | Broadcast dropdown `cmbBroadcastColor` (AC 2:1) |
| Broadcast Blue Option | `Mavi (Lonca Sohbeti)` | `Blue (Guild Chat)` | Broadcast dropdown `cmbBroadcastColor` (AC 2:6) |
| Broadcast Pink Option | `Pembe (Fısıltı)` | `Pink (Whisper)` | Broadcast dropdown `cmbBroadcastColor` (AC 2:3) |
| Status Traffic Panel | `Server Listesi Trafik Işığı (Port 6416)` | `Server List Traffic Indicator / Cluster Load (Port 6416)` | Cluster status control panel |
| Status ComboBox Items | `Yeşil (Boş / Akıcı)`, `Sarı (Kalabalık)`, `Kırmızı (Dolu)`, `Kapalı (Bakım)`, `Otomatik (Canlı Oyuncu)` | `Green (Smooth / Empty)`, `Yellow (Crowded)`, `Red (Full)`, `Offline / Maintenance`, `Auto (Live Population)` | Manual/Automatic cluster load setting |
| Safe Shutdown Initial | `Kapatılıyor...` | `Shutting down...` | Button state immediately after clicking shutdown |
| Countdown Ticks | `Kapanıyor ({i}s)...` | `Closing ({i}s)...` | Real-time button text during 10-second countdown |

### 2.2 Server Diagnostics and Graceful Shutdown Messaging ([`Src/Gui/MainForm1.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.cs))

| Message Key / Event | Localized Output Format | Channel |
| :--- | :--- | :--- |
| Shutdown Banner Start | `=== SAFE SERVER SHUTDOWN INITIATED ===` | Console & DebugSystem |
| State Preservation Progress | `Saving all active player states, inventories, and server settings...` | Console & DebugSystem |
| Data Persistence Complete | `All player data, maps, and server settings have been safely saved to SQLite!` | Console & DebugSystem |
| Canonical Log File Output | `LOG FILE LOCATION: {canonicalLogPath}` | Console & DebugSystem |
| Second-by-Second Countdown | `Closing server in {i} second(s)... (Logs saved at {canonicalLogPath})` | Console & DebugSystem |
| Shutdown Completed Notice | `=== SERVER SHUTDOWN COMPLETED SAFELY === Exiting application now.` | Console & DebugSystem |

### 2.3 Network Action Codes & Gameplay Messages ([`Src/Network/ActionCodes/AC11.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC11.cs))

| Action Code | Previous Message | Localized English Message | Condition |
| :--- | :--- | :--- | :--- |
| `AC 11` (PK Initiation) | `PK modunuz kapalı!` | `Your PK mode is disabled!` | Attacker has `Settings.PKABLE == false` |
| `AC 11` (PK Initiation) | `{target.CharName} PK modunu kapatmış!` | `{target.CharName} has PK mode disabled!` | Target has `Settings.PKABLE == false` |

### 2.4 Dungeon and Trial Names ([`wlo.pserver.core/Game/Battle/PalaceTrialManager.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Battle/PalaceTrialManager.cs))

All 12 Zodiac Palace stages have been stripped of localized parenthetical suffixes:
- Stage 1: `Aries Palace`
- Stage 2: `Taurus Palace`
- Stage 3: `Gemini Palace`
- Stage 4: `Cancer Palace`
- Stage 5: `Leo Palace`
- Stage 6: `Virgo Palace`
- Stage 7: `Libra Palace`
- Stage 8: `Scorpio Palace`
- Stage 9: `Sagittarius Palace`
- Stage 10: `Capricorn Palace`
- Stage 11: `Aquarius Palace`
- Stage 12: `Pisces Palace`

---

## 3. Verification and Zero-Error Compliance

- **String Audit Execution**: Automated recursive regex scan across all solution projects and source files confirmed **0 remaining Turkish character or keyword instances**.
- **Compilation**: Full solution build (`dotnet build "Wonderland Private Server.sln"`) completed with **0 Errors**.
