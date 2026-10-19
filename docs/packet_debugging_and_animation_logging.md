# Packet Debugging and Animation Logging Architecture

## Overview
This subsystem provides comprehensive real-time capture and logging for all incoming (`RECV`) and outgoing (`SEND`) network packets in Wonderland Private Server, with special descriptive labeling for animation, cutscene, stage direction, and emotion packets. It includes an interactive toggle in the GUI console and in-game chat commands to enable or disable packet debugging without server restarts.

## Architectural Components

### 1. Packet Interception and Logging Engine
* **File:** [`Player.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Player.cs)
* **Outgoing Packets (`Send`):**
  * Intercepts all packets dispatched through `Player.Send(SendPacket p, PacketFlags pFlags)`.
  * Extracts ActionCode (`AC`) and SubCode (`Sub`) from either the framed packet header (`0xF4 0x44` at offsets 4-5) or raw payloads.
  * Resolves descriptive labels with deep payload decoding:
    * `AC 20:1 Type 1`: Dialogue / Speech (`[DIALOGUE Step={step} Speaker={speaker} TalkID={talkId}: "{text}"]`) with live string resolution from `Talk.dat` via `TalkResolver`.
    * `AC 20:1 Type 5`: Stage Direction / Cutscene (`[CUTSCENE / STAGE DIR CutsceneID={id} Mode={mode} Step={step}]`).
    * `AC 20:2`: Dialogue / step advance.
    * `AC 20:3`: Emoticon bubble animation.
    * `AC 20:7`: Stage action / motion animation.
    * `AC 20:8`: UI restoration / dialogue termination.
    * `AC 20:10`: BGM fanfare / SFX playback.
    * `AC 186:12`: Full-screen CG movie playback with 16-bit CutsceneID (`[CG MOVIE START CutsceneID={id}]`).
    * `AC 186:9`: CG movie playback status.
    * `AC 22:10`: NPC despawn with ClickID.
    * `AC 22:11`: NPC pose / emote state.
    * `AC 22:12`: NPC movement / walk path.
    * `AC 23:2`: Player motion / sit / stand animation.
    * `AC 23:7`: Player emoticon animation.
    * `AC 23:57`: System notification text.
    * `AC 5:4`: Scene gate unfreeze and HUD visibility restoration.
    * `AC 5:30`: Player immobilization lock / unlock.
    * `AC 6:2`: Player stance / movement lock.
    * `AC 32:2`: Player action posture / emote.
  * Emits formatted output:
    `[SEND PKT] [PlayerName] AC={acId}, Sub={subId} [LABEL] Len={len} Hex: {hexData}`
* **Incoming Packets (`ProcessSocket`):**
  * Intercepts all received client packets through `Player.ProcessSocket(IPacket g)`.
  * Resolves client acknowledgments:
    * `AC 20:6`: Event / Cutscene step acknowledgment (`ACK`).
    * `AC 20:2`: Dialogue next step acknowledgment (`Step={step}`).
    * `AC 20:9`: Dialogue interactive choice selection (`Choice=0x{choice:X}`).
    * `AC 20:1`: NPC click interaction (`ClickID={clickId}`).
    * `AC 186:9`: CG movie handshake acknowledgment (`Scene={scene}`).
    * `AC 20:4`: Region trigger interaction.
    * `AC 32:2`: Player emote / posture update.
  * Emits formatted output:
    `[RECV PKT] [PlayerName] AC={p.A}, Sub={p.B} [LABEL] (Handler: {handlerName}) Len={len} Hex: {hexData}`

### 2. Global State and Persistence
* **State Field:** `DebugSystem.PacketDebugEnabled` ([`DebugSystem.cs`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/System/DebugSystem.cs))
* **Configuration:** `Settings.EnablePacketDebugLogging` ([`Settings.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Server/Config/Settings.cs))
* **Initialization:** Loaded and synchronized at startup in [`MainForm1.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.cs).

### 3. User Controls and Interfaces
* **GUI Diagnostic Bar:**
  * Located directly above the server console log in `tabPage7` ([`MainForm1.ModernUi.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.ModernUi.cs)).
  * Interactive button `Packet Debug: ON / OFF` toggles logging live, updates styling, and saves the configuration state.
* **In-Game Chat Command:**
  * Handled in [`AC02.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC02.cs).
  * Commands: `:packetdebug`, `/packetdebug`, `:debugpkt`, `/debugpkt`.
  * Optional arguments: `on`, `off`, `enable`, `disable`.
  * Responds with system broadcast to player and console logging.
