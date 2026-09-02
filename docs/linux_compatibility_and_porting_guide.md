# Linux Compatibility & Porting Guide

## Overview
This document evaluates and outlines the technical roadmap for running and building the Wonderland Online Private Server on Linux (Ubuntu, Debian, Alpine, Docker containers, and headless VPS environments).

---

## 1. Current State & Platform Blockers

| Component | Current State | Linux Compatibility Assessment |
| :--- | :--- | :--- |
| **Target Framework** | `.NET Framework 4.6.2` | ❌ Windows-only runtime. Requires Mono, Wine, or modern .NET 8/9 migration. |
| **Application Type** | `WinExe` (Windows Forms) | ❌ `System.Windows.Forms` is not natively supported on Linux headless environments. |
| **GUI Dependencies** | `MainForm1.cs`, `System.Drawing` | ❌ Server loop and packet dispatcher hooks are partially coupled to UI event handlers. |
| **Database Providers** | `MySql.Data` (9.1.0) / `System.Data.SQLite` | ⚠️ `MySql.Data` is cross-platform; bundled Windows `System.Data.SQLite.dll` needs `Microsoft.Data.Sqlite` or Linux SQLite3 native runtime. |
| **File System Paths** | Windows backslashes (`\`), mixed casing | ⚠️ Linux ext4 is case-sensitive (`Data/Item.dat` vs `data/item.dat`). |

---

## 2. Deployment Pathways

### Pathway A: Headless Console Port to .NET 8 / .NET 9 (Recommended for Production)
Transform the game server engine into a cross-platform daemon/CLI application that runs on Linux natively via `dotnet run` / `dotnet publish -r linux-x64`.

#### Architecture Decoupling:
1. **Target Framework Migration**: Update project SDKs to `<TargetFramework>net8.0</TargetFramework>`.
2. **Decouple GUI from Server Engine**:
   - Extract server initialization and network listener from `MainForm1.cs` into `ServerHost.cs` / `GameServerDaemon.cs`.
   - Direct `DebugSystem.Write` to standard console output (`Console.WriteLine`) and file logger (`NLog` / `Serilog`).
3. **Headless Console Entry Point**:
   ```csharp
   // Program.cs (Linux / Cross-Platform Entry Point)
   namespace Wonderland_Private_Server
   {
       internal class Program
       {
           static async Task Main(string[] args)
           {
               Console.WriteLine("Starting Wonderland Online Private Server (Headless/Linux)...");
               var server = new GameServerHost();
               await server.StartAsync();
               Console.WriteLine("Server listening. Press Ctrl+C to stop.");
               await Task.Delay(Timeout.Infinite);
           }
       }
   }
   ```
4. **Database Library**: Swap native Windows SQLite DLL with cross-platform NuGet `Microsoft.Data.Sqlite`.
5. **Path Normalization**: Wrap file loading paths with `Path.Combine` and standardize `./Data/` file names to lowercase or exact-case matching.

---

### Pathway B: Run via Mono Runtime (Zero Code Rewrite, Emulated WinForms)
Run the existing `.NET Framework 4.6.2` binary directly on Linux using the Mono runtime.

```bash
# Ubuntu / Debian setup
sudo apt update
sudo apt install mono-complete libgdiplus

# Running headless with virtual framebuffer (Xvfb) for WinForms compatibility
sudo apt install xvfb
xvfb-run mono "Wonderland Private Server.exe"
```

*Note: Mono's Windows Forms implementation has known thread-safety and rendering quirks, suitable for testing but not recommended for long-term production.*

---

### Pathway C: Run via Wine / Proton (Zero Code Rewrite)
Execute the precompiled Windows binaries inside a Wine compatibility layer on Linux:

```bash
sudo apt install wine
wine "Wonderland Private Server.exe"
```

---

## 3. Recommended Step-by-Step Modernization Plan (.NET 8 Headless)

1. **Project Separation**:
   - `wlo.pserver.core` -> Multi-target `netstandard2.0` / `net8.0`.
   - `wlo.pserver.cli` -> Dedicated headless console runner for Linux (`net8.0`).
   - `wlo.pserver.gui` -> Windows Forms management tool for desktop admin (`net8.0-windows`).
2. **Linux Docker Support**:
   ```dockerfile
   FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
   WORKDIR /app
   COPY ./bin/Release/net8.0/linux-x64/publish/ .
   COPY ./Data/ ./Data/
   EXPOSE 6414 6415 6416
   ENTRYPOINT ["./wlo.pserver.cli"]
   ```
3. **Path Case Normalizer**: Add an automated file lookup helper that resolves case discrepancies on ext4 Linux filesystems.
