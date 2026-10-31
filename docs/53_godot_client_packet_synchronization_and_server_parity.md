# 53: Godot Client Network Synchronization & C# Server Parity

## 1. Executive Summary
This document specifies the architectural synchronization and packet telemetry adaptations implemented in the Wonderland Online C# Server (`Wonderland Private Server.sln` / `wlo.pserver.core` / `Src/Network/ActionCodes`) to enable seamless communication with both the native Delphi client (`aLogin.exe` / `Main.exe`) and the modernized Godot 4 client (`wonderland-online-client`).

---

## 2. Framing & Cryptography Protocol Compatibility

* **Magic Header**: `0x44F4` (Little-Endian: `0xF4, 0x44`).
* **Frame Structure**: `[Header: 2B (0xF4, 0x44)][PayloadLength: 2B (uint16 LE)][Payload: N bytes]`. Total frame length = $4 + N$.
* **Symmetric Keystream Cryptography**:
  * Static XOR key `0xAD` (decimal 173).
  * Entire stream encrypted on wire: `wire_byte = plain_byte ^ 0xAD`.
  * Initial two wire bytes: `0xF4 ^ 0xAD = 0x59`, `0x44 ^ 0xAD = 0xE9`.
  * C# Implementation: `IncomingPacket.InData` & `OutgoingPacket.GetNextSet` using `xor = 173`.
  * Godot Implementation: `NetworkManager.build_wire_frame` & `xor_transform(..., 0xAD)`.

---

## 3. Synchronized ActionCodes & Protocol Handlers

### 3.1 Character Creation (`AC 63:3` / Opcode `0x033F`)
* **Problem**: `AC63.Recv3` was previously commented out, leaving the server unable to process character creation requests from the Godot client.
* **Client Request Frame**:
  `[63, 3, slot:byte, name_len:byte, char_name:string, role_id:byte, element:byte, str:byte, con:byte, int:byte, wis:byte, agi:byte]`
* **Server Processing**:
  1. Validates slot ($1$ or $2$) and character name length ($3 \dots 16$).
  2. Binds starter attributes, equipment outfit via `SetBeginnerOutfit()`, base stats via `ApplyCharacterBaseStats()`, full HP/SP.
  3. Configures spawn coordinates on starter ship deck (`Map 10017`, `X: 1042`, `Y: 1075`), quest 1, and beginner item starter pack.
  4. Persists character in SQLite/MySQL database via `CharacterDataBase.WriteNewPlayer`.
  5. Updates user account linking via `UserDataBase.Update_Player_ID`.
  6. Sends success ACK `[1, 3, 0]` (`Action 1, Sub 3, Status 0`) to transition client.
  7. Enqueues player into world server via `cGlobal.gWorld.OnLogin(p)`.

### 3.2 High-Frequency Keepalive Ping (`AC 62:45` / Opcode `0x3E2D`)
* **Problem**: In `AC62.cs`, SubCode 45 (`0x2D`) was unhandled, causing the diagnostic console to log unhandled packet errors and preventing keepalive echo.
* **Payload Structure**: `[62, 45, ping_id:uint32 LE]`
* **Server Processing**:
  1. Extracts incoming `ping_id` timestamp (or `Environment.TickCount`).
  2. Updates `p.LastPacketTime = DateTime.Now`.
  3. Echoes back `[62, 45, ping_id:uint32]` to preserve client latency monitoring without disconnects.

### 3.3 Chat Broadcast & Command Routing (`AC 35:12` / Opcode `0x230C`)
* **Problem**: In `AC35.cs`, SubCode 12 was unhandled.
* **Client Request Frame**: `[35, 12, channel_id:byte, message:string]`
* **Server Processing**:
  1. Unpacks `channel_id` and `message`.
  2. Checks mute status (`p.IsMuted`).
  3. Detects GM administrative commands (`:` or `/`) and delegates to `AC02` command interpreter.
  4. If standard chat, constructs authentic 0x230C wire packet:
     `[35, 12, channel_id:byte, sender_id:uint32 LE, message:string]`
  5. Broadcasts to map via `p.CurMap.Broadcast(broadcast)`.

### 3.4 Map Warp & Cutscene Scene ACK (`AC 85:7` / Opcode `0x0755`)
* **Problem**: In `AC85.cs`, SubCode 7 was unhandled.
* **Server Processing**:
  Handles warp / cutscene acknowledgments and transmits `[20, 8]` mobility unfreeze to release player controls.

### 3.5 UserDataBase Persistence Fix
* **Problem**: In `UserDataBase.Update_Player_ID`, the database update statement was commented out.
* **Fix**: Added dynamic column check (`character1ID` / `character2ID`) executing `UPDATE users SET {col} = '{id}' WHERE userID = '{user}'` safely across both SQLite and MySQL backends.

---

## 4. Verification Evidence

### 4.1 Solution Compilation
```powershell
dotnet build "D:\GitHub\Wonderland-Private-Server\Wonderland Private Server.sln"
```
* **Result**: `wlo.pserver.core.dll` -> Build succeeded (0 Warnings, 0 Errors).
* **Result**: `Wonderland Private Server.exe` -> Build succeeded (0 Warnings, 0 Errors).

### 4.2 Godot Headless Test Execution
```powershell
godot --headless --path "D:\Godot\WonderlandOnline\wonderland-online-client" -s "res://tests/test_network.gd"
godot --headless --path "D:\Godot\WonderlandOnline\wonderland-online-client" -s "res://tests/test_event_sequences.gd"
```
* **Result**: 13/13 tests passed in `test_network.gd` (100%).
* **Result**: 7/7 event sequence tests passed in `test_event_sequences.gd` (100%).
