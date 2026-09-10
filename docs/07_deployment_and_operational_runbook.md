# 07 - Deployment and Operational Runbook

## 1. Zero-Configuration Portable Deployment

The server is engineered for **100% dynamic portability**. It can be cloned or extracted to any folder on any drive (`C:`, `D:`, `E:`, external drives) on any Windows workstation without requiring hardcoded paths or registry modifications.

### Path Resolution Principles
- **No Hardcoded Absolute Paths**: All paths are resolved dynamically relative to the application directory using [`RCLibrary.Core.PathHelper`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/RCLibrary.Core/PathHelper.cs).
- **Embedded Database**: Uses self-contained SQLite [`Data/ServerDataBase.db`](file:///D:/GitHub/Wonderland-Private-Server/Data/ServerDataBase.db) without needing an external database daemon like MySQL or SQL Server.
- **Auto-Provisioning**: On startup, [`GameDataBase.VerifySetup()`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/GameDataBase.cs) automatically verifies all 32 tables, applies indexes, and seeds initial data if missing.

---

## 2. System Prerequisites

| Requirement | Minimum Specification | Recommended Specification |
| :--- | :--- | :--- |
| **Operating System** | Windows 10 (64-bit) / Windows Server 2016 | Windows 11 / Windows Server 2022 |
| **.NET Runtime** | .NET Framework 4.6.2 | .NET Framework 4.8+ |
| **Development IDE** | Visual Studio 2022 (Community / Pro) | Visual Studio 2022 with C# / .NET Desktop Workload |
| **Build Tools** | MSBuild 17+ or `dotnet` CLI | Included with VS2022 |
| **Client Assets** | Wonderland Online Game Client (Rhode Island or v1.0-v3.0) | Client synchronized with `./Data` resource files |

---

## 3. Quick Start & Execution Workflow

### Step 1: Build the Solution
Open PowerShell in the project root and compile using the .NET CLI:
```powershell
dotnet build "Wonderland Private Server.sln"
```
Ensure build terminates with `0 Error(s)`.

### Step 2: Configure Client `SERVER.INI`
Ensure the client points to the local or remote server IP:
```ini
[SERVER]
COUNT=1
SERVER1=127.0.0.1
NAME1=Wonderland Private Server
```

### Step 3: Run the Server Supervisor
Execute the primary server binary:
```powershell
.\bin\Debug\"Wonderland Private Server.exe"
```
The server will:
1. Initialize dynamic paths via `PathHelper`.
2. Inspect `Data/ServerDataBase.db` and execute `VerifySetup()`.
3. Decrypt and cache `Npc.dat`, `Item.dat`, `Skill.dat`, and `Talk.dat`.
4. Open TCP listeners on Ports `6414` (Login), `6415` (World), and `6416` (Status).
5. Display the green operational indicator on the Dashboard.

### Step 4: Launch Client
- Press `F5` in the server GUI window (or manually run `aLogin.exe` in the client directory).
- Select the server and log in with default credentials:
  - Account: `admin` / Password: `password` (GM Level 10)
  - Account: `developer` / Password: `password` (GM Level 10)

---

## 4. Port Forwarding & Networking Guide

If hosting for external friends or an online private server community, forward the following TCP ports on your router / firewall:

| Port | Protocol | Purpose | External Access Required |
| :--- | :--- | :--- | :--- |
| `6414` | TCP | Login Server & Mall Sync | **Yes** (WAN) |
| `6415` | TCP | World Server & Battles | **Yes** (WAN) |
| `6416` | TCP | Cluster Status & Ping | **Yes** (WAN) |

---

## 5. Troubleshooting & Diagnostics

### Issue 1: "Address already in use" Socket Exception
- **Cause**: Another instance of `Wonderland Private Server.exe` or another service is listening on port 6414/6415/6416.
- **Solution**: Terminate zombie processes via PowerShell:
  ```powershell
  Get-Process "Wonderland Private Server" | Stop-Process -Force
  ```

### Issue 2: "Database is locked" SQLite Exception
- **Cause**: Multiple threads or external SQLite GUI viewers (e.g., DB Browser) holding exclusive transaction locks on `ServerDataBase.db`.
- **Solution**: Close external database editors. The server uses internal thread-safe synchronization locks.

### Issue 3: Missing `odd.dat` Sprite Asset
- **Cause**: `odd.dat` is a ~1.42 GB sprite archive required by the game client for visuals.
- **Solution**: Download `odd.dat` from [Releases v1.0.0](https://github.com/Eminbalci/Wonderland-Private-Server/releases/tag/v1.0.0) and place it into the client folder.

### Issue 4: Safe Server Shutdown & Data Integrity
- When clicking the **"Shutdown"** button (`btnSafeShutdown`) in the GUI Dashboard or closing the application, the server will not exit abruptly. It flushes all player data, drop tables, and configs, outputs the exact canonical log file path to the console, performs a 10-second countdown with second-by-second updates, and then terminates cleanly.
