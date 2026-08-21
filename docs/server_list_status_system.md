# Single Server Traffic Light & Status Color Service (Port 6416)

## Overview
Provides live server status / traffic light color indicators (🟢 Green, 🟡 Yellow, 🔴 Red, ⚫ Offline, ⚡ Auto) for single-server setups on the client's Server Selection screen (`aLogin.exe`).

## Configuration & Protocol (Port 6416)
The client connects on **Port 6416 (TCP)** to retrieve the server load status.
The server returns an authentic `0xC9` packet for the server and gracefully disconnects:
- **Packet Structure**: `C9 00 [ClusterID] [ServerID_LE] [StatusColor] [Alias101_LE] [StatusColor]`

### Color Status Codes
- `0x01`: 🟢 **Yeşil** (Boş / Akıcı / Smooth)
- `0x02`: 🟡 **Sarı** (Kalabalık / Crowded)
- `0x03`: 🔴 **Kırmızı** (Dolu / Full)
- `0x00` / `0x04`: ⚫ **Kapalı / Bakım** (Offline / Maintenance)
- `0xFF`: ⚡ **Otomatik** (Online oyuncu sayısına göre dinamik)

## GUI Interface (Status Tab)
- Located on the **Status** tab (`MainForm1.cs`).
- Simple single-server selection box (`🟢 Yeşil`, `🟡 Sarı`, `🔴 Kırmızı`, `⚫ Kapalı`, `⚡ Otomatik`).
- Quick-action buttons: `🟢 Yeşil`, `🟡 Sarı`, `🔴 Kırmızı`, `⚡ Otomatik`.
- Automatically saves and loads settings from `Data/server_status.txt`.
