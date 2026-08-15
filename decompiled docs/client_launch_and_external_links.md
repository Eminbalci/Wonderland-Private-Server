# Client Launch & External Links Decompiled Specifications

This document outlines the client-side startup command-line parsing, system warning modals, and external web redirects (official site registration and billing card gateway) extracted from `aLogin.exe.1.c`.

---

## 1. External Web Integrations (`ShellExecuteA`)

The client dispatches system commands to launch the user's default web browser when triggering specific buttons:

### A. Official Account Registration
- **URL**: `https://wlrac.chinesegamer.net/`
- **Trigger**: Clicked during the login page account registration button flow.
- **API Call**: `ShellExecuteA(hwnd, "open", "https://wlrac.chinesegamer.net/", ...)`

### B. MyCard Payment Gateway
- **URL**: `http://mycardingametest.chinesegamer.net/MyCardService.aspx?`
- **Trigger**: Selected when players request point additions inside the Nesne Market (Item Mall) GUI.
- **API Call**: `ShellExecuteA(0, 0, "http://mycardingametest.chinesegamer.net/MyCardService.aspx?...", ...)`

---

## 2. Command Line Options (`GetCommandLineA`)

During launch, the client checks startup options:

- **Command Line Parsing**:
  - Triggers `GetCommandLineA()` at initialization.
  - Verifies whether a valid security check token or launch flag was passed from the primary launcher (`aMain.exe` / `Wonderland.exe`). If missing or invalid, blocks standalone direct access to prevent login bypassing.

---

## 3. System Alerts & Warning Dialogs (`MessageBoxA`)

Windows confirmation panels are triggered using Winsock wrappers and GUI thread contexts:

- **Routines**:
  - `MessageBoxA(hWnd, lpText, lpCaption, uType)`
- **Uses**:
  - Runtime and execution errors (such as missing resource files or invalid DLL binds).
  - Purchase validations and transaction warnings.
