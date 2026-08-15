# Technical Documentation: F5 Shortcut Execution

## Overview
This document details the implementation of the `F5` hotkey in `Form1` (`Src/Gui/MainForm1.cs`), which enables running/launching the client application (`aLogin.exe`) directly from the GUI.

---

## Technical Specifications

### Handler Implementation
- **Location:** `Src/Gui/MainForm1.cs` (`Form1.ProcessCmdKey`, `Form1.RunClientProgram`)
- **Key Binding:** `Keys.F5`
- **Form Property:** `this.KeyPreview = true;` (enables hotkey execution regardless of active focused control)

### Parameters & Search Order
When `F5` is pressed, `RunClientProgram` checks candidate client executable paths in order:
1. `D:\garipgudubetseyler\WLRI\aLogin.exe`
2. `AppDomain.CurrentDomain.BaseDirectory\aLogin.exe`
3. `AppDomain.CurrentDomain.BaseDirectory\WLRI\aLogin.exe`
4. `AppDomain.CurrentDomain.BaseDirectory\..\WLRI\aLogin.exe`

### Exceptions & Error Handling
- **Missing Executable:** Writes error to `DebugSystem` log (`[System] F5 pressed: Client executable (aLogin.exe) not found.`) without crashing the application.
- **Process Launch Exception:** Catches and logs process start exceptions to `DebugSystem`.

---

## Verification
- Built via `dotnet build "Wonderland Private Server.sln"`: **0 Errors**.
