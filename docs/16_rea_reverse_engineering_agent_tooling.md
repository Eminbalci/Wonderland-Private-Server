# REA (Reverse Engineer Anything) Agent Tooling Specification

## 1. Architectural Overview

[REA](https://github.com/morluto/rea) (`rea-agents`) provides unified reverse-engineering capabilities across native binaries (x86/x64 PE, ELF, Mach-O), managed .NET assemblies (PE/CLI), web/Electron applications, and Android packages. It bridges autonomous AI agents directly to reverse engineering engines via Model Context Protocol (MCP) and terminal CLI utilities.

In this environment, REA integrates with the local Ghidra 12.1.2 decompiler installation to enable automated disassembly and static inspection of official Wonderland Online client binaries (`aLogin.exe`, `WLO.exe`) and managed server assemblies (`PhoenixData.dll`, `RCLibrary.dll`, `wlo.pserver.core.dll`).

---

## 2. Host Configuration & Environment Prerequisites

* **Package:** `rea-agents` v6.4.0 (Global npm installation).
* **CLI Executable:** `C:\Users\muham\AppData\Roaming\npm\rea.cmd` / `rea` (Node.js v24.14.1).
* **Decompiler Engine:** Ghidra 12.1.2 (`D:\GitHub\ghidradecodeyapma\ghidra_12.1.2_PUBLIC`).
* **Java Runtime:** OpenJDK 21 64-bit (`C:\Program Files\Java\jdk-21`).
* **Native Authority:** Windows Job Objects, private DACL, and NTFS reparse-safe handle controls.

---

## 3. MCP Client Registrations

REA is registered as an MCP server across the following agent configurations:

1. **Antigravity CLI:** [`C:\Users\muham\.gemini\config\mcp_config.json`](file:///C:/Users/muham/.gemini/config/mcp_config.json)
   ```json
   "rea": {
     "command": "C:\\Program Files\\nodejs\\node.exe",
     "args": [
       "C:\\Users\\muham\\AppData\\Roaming\\npm\\node_modules\\rea-agents\\scripts\\rea.mjs",
       "mcp"
     ],
     "env": {
       "GHIDRA_INSTALL_DIR": "D:\\GitHub\\ghidradecodeyapma\\ghidra_12.1.2_PUBLIC",
       "JAVA_HOME": "C:\\Program Files\\Java\\jdk-21"
     }
   }
   ```
2. **Claude Code:** [`C:\Users\muham\.claude.json`](file:///C:/Users/muham/.claude.json)
3. **Gemini CLI:** [`C:\Users\muham\.gemini\settings.json`](file:///C:/Users/muham/.gemini/settings.json)
4. **GitHub Copilot CLI:** [`C:\Users\muham\.copilot\mcp-config.json`](file:///C:/Users/muham/.copilot/mcp-config.json)
5. **VS Code:** [`C:\Users\muham\AppData\Roaming\Code\User\mcp.json`](file:///C:/Users/muham/AppData/Roaming/Code/User/mcp.json)

---

## 4. Installed Agent Skills

The matching investigation skill `reverse-engineer-anything` (version 37, 170 tools) is installed at:
* [`C:\Users\muham\.gemini\config\skills\reverse-engineer-anything\SKILL.md`](file:///C:/Users/muham/.gemini/config/skills/reverse-engineer-anything/SKILL.md)
* [`C:\Users\muham\.claude\skills\reverse-engineer-anything\SKILL.md`](file:///C:/Users/muham/.claude/skills/reverse-engineer-anything/SKILL.md)

---

## 5. Command Reference & CLI Workflows

### 5.1 Managed .NET Assembly Inspection
Inspects PE/CLI metadata, types, methods, P/Invoke boundaries, and CIL bytecode anchors without executing assembly code:
```powershell
rea.cmd inspect-managed-artifact "DLLS/RCLibrary.dll"
rea.cmd inspect-managed-members "DLLS/PhoenixData.dll"
rea.cmd inspect-managed-native-boundaries "bin/Debug/Wonderland Private Server.exe"
```

### 5.2 Native Binary Decompilation & Function Analysis (via Ghidra)
Performs headless decompilation, instruction disassembly, and cross-reference inspection:
```powershell
# Analyze client binary overview
rea.cmd analyze "decompiled/WLO.exe"

# Disassemble specific subroutines (e.g. movement, combat, scene loading)
rea.cmd instructions "decompiled/WLO.exe" --address 0x004121A8

# Cross-reference analysis
rea.cmd xrefs "decompiled/WLO.exe" --address 0x00432674
```

### 5.3 Diagnostic & Health Verification
```powershell
rea.cmd doctor
```

---

## 6. Edge Cases & Operational Caveats

1. **PowerShell Pipeline Stdin Blocking:**
   In PowerShell environments, executing the wrapper `rea.ps1` without an active stdin stream can block due to `$MyInvocation.ExpectingInput`. Always execute `rea.cmd` or `node "<npm-global-dir>\node_modules\rea-agents\scripts\rea.mjs"` directly.
2. **Ghidra Startup Deadline:**
   For very large binaries (such as full game executables), increase the headless analysis timeout using the environment variable:
   ```powershell
   $env:REA_GHIDRA_STARTUP_TIMEOUT_MS = "60000"
   ```
3. **Local NTFS Restriction:**
   On Windows, native memory isolation requires a local NTFS volume for Job Object DACL isolation. Network shares (SMB/UNC) are disallowed for binary target sandboxing.
