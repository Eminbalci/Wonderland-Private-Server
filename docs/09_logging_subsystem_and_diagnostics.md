# 09 - Logging Subsystem and Diagnostic Timestamp Specification

## 1. Overview and Architecture

The Wonderland Private Server diagnostic logging infrastructure is centralized in [`RCLibrary.System.DebugSystem`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/System/DebugSystem.cs), [`Phoenix.Core.System.DebugSystem`](file:///D:/GitHub/Wonderland-Private-Server/Phoenix.Core/System/DebugSystem.cs), and [`Wonderland_Private_Server.Utilities.LogServices`](file:///D:/GitHub/Wonderland-Private-Server/Src/Utilities/LogServices.cs).

All diagnostic log messages across the GUI dashboard, persistent disk log files, and standard console streams strictly follow an ISO-aligned canonical timestamp prefix format:
```text
[yyyy-MM-dd HH:mm:ss] <payload>
```

---

## 2. Standardized Formatting Specifications

### 2.1 Format Definition
Every log emission is intercepted and timestamped at arrival time:
- **Timestamp Token**: `[yyyy-MM-dd HH:mm:ss]` (e.g., `[2026-09-10 18:25:30]`).
- **GUI Console (`RichTextBox` / `TextBox`)**:
  `[yyyy-MM-dd HH:mm:ss] <message>`
- **Persistent Disk Log (`Logs/wlophoenixlogFile_YYYYMMDD.txt`)**:
  `[yyyy-MM-dd HH:mm:ss] | <DebugItemType> | <message>`
- **Exception Logging**:
  `[yyyy-MM-dd HH:mm:ss] [ERROR] <ExceptionMessage>`

### 2.2 Thread-Safe UI Dispatching
- When invoked from background threads (WorldServer, LoginServer, or Database worker tasks), `DebugSystem` dispatches updates to the GUI controls (`MainOutput`) asynchronously using `BeginInvoke` instead of blocking synchronous `Invoke`, eliminating UI thread deadlock risks.
- If no GUI controls are active (headless execution), output is redirected to `Console.WriteLine`.

---

## 3. Function & Parameter Tracking

### 3.1 `FormatWithTimestamp(DateTime when, string message)`
- **Namespace**: `System.DebugSystem`
- **Parameters**:
  - `when` (`DateTime`): Timestamp captured at message receipt.
  - `message` (`string`): Log content or payload.
- **Returns**: `string` - The formatted message string prefixed with `[yyyy-MM-dd HH:mm:ss]`.
- **Edge Cases**:
  - `null` or empty messages: Returns `[yyyy-MM-dd HH:mm:ss]`.
  - Multi-line strings (containing `\n` or `\r\n`): Splits into lines and prepends `[yyyy-MM-dd HH:mm:ss]` to every non-empty line.
  - Idempotency guard: If the message is already prefixed with an existing `[yyyy-MM-dd HH:mm:ss]` token, skips re-prefixing to avoid double timestamping.

### 3.2 `Write(string data, DebugItemType type = DebugItemType.Info_Light, bool newline = true)`
- **Parameters**:
  - `data` (`string`): The diagnostic log payload.
  - `type` (`DebugItemType`): Log category level (`Info_Light`, `Info_Heavy`, `Error`, `DataBase_Light`, `Network_Light`, etc.).
  - `newline` (`bool`): Determines trailing line break emission.
- **Side Effects**:
  - Appends formatted timestamped text with category color to GUI control.
  - Enqueues timestamped record into `msg_towrite` stack for asynchronous batch writing to daily log files.

---

## 4. Verification and Integrity

- **Clean Compilation**: Built with zero compilation errors (`0 Hata`).
- **Dual-Channel Synchronization**: GUI `RichTextBox` view and disk files in `Logs/` maintain 100% synchronized timestamping across all server events.
