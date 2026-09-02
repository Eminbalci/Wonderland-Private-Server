# Server Branding and Fast Login Optimization

## 1. Overview
This update provides GUI controls for configuring the server name and welcome MOTD message dynamically, and optimizes the login packet sequence to eliminate lag when existing characters spawn into the game.

## 2. Server Branding, MOTD & System Prompts Configuration
- **Server Name (`AC 1:9`)**:
  - Displays `Welcome to [{ServerName}] Server` on the client screen during login.
  - Configurable via `cGlobal.SrvSettings.ServerName` and **Server Name** text box on the Status tab.
- **MOTD / Welcome Message (`AC 23:57`)**:
  - Sent on map spawn via `[23, 57, 0, WelcomeMessage]`.
  - Configurable via `cGlobal.SrvSettings.WelcomeMessage` and **MOTD** text box on the Status tab.
- **Live System Prompt Broadcast (`AC 23:57`)**:
  - Allows the server administrator to type any custom announcement in **Live Prompt** and click **📢 Broadcast** to instantly dispatch the system prompt to all online players in real time.
- **Legacy Event Prompts Toggle (`AC 70:1` & `AC 69:1`)**:
  - Controls whether default hardcoded event announcements (e.g. "Interserver PVP" and "Sunday's Cursed Palace") are broadcasted upon login.
  - Configurable via the **Enable Legacy Event Prompts** checkbox (disabled by default so clients only receive the configured MOTD).

## 3. Fast Login Stream Optimization
- Removed redundant calls in `WorldServer.CommenceLogin`:
  - Eliminated duplicate `QuestManager.SendQuestJournal` (which previously sent redundant 15+ quest structures).
  - Eliminated duplicate `ItemMallManager.SendCatalog` (which previously sent redundant 32 mall items).
- Removed side-effect packet broadcasting from `GameDataBase.LoadFinalData`, allowing memory-only initialization and clean, instantaneous map entry via `Map.Warp_In`.
