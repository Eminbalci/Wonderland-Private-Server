# Technical Documentation: Client & DAT File Synchronization

## Overview
This document details the configuration and data synchronization between the server repository (`d:\GitHub\Wonderland-Private-Server`) and the target client located at `D:\garipgudubetseyler\WLRI`.

---

## Client Integration Details

1. **Client Directory Location:** `D:\garipgudubetseyler\WLRI`
2. **Server IP Configuration:**
   - File: `D:\garipgudubetseyler\WLRI\SERVER.INI`
   - Content:
     ```ini
     01[Local Server]1
     Local*127.0.0.1
     ```
3. **Data Files Synchronized (`./Data`):**
   - `AdjustRidePetPos.txt` (10,919 bytes)
   - `Compound.dat` (10,920 bytes)
   - `Compound2.Dat` (51,285 bytes)
   - `Eve.emg` (5,164,409 bytes)
   - `Formula.dat` (407 bytes)
   - `Ground.MMG` (21,921,689 bytes)
   - `Item.dat` (3,267,044 bytes)
   - `Mark.dat` (1,191,715 bytes)
   - `Npc.dat` (680,202 bytes)
   - `PlayerTitleData.txt` (121 bytes)
   - `SceneData.dat` (153,532 bytes)
   - `Skill.dat` (141,044 bytes)
   - `SkillData.MBTM` (1,234,468 bytes)
   - `Talk.dat` (5,108,248 bytes)
   - `TrafficSetting.txt` (28,068 bytes)
   - `Wem.MMG` (293,725 bytes)
   - `odd.dat` (1,428,116,336 bytes)
   - `odd_d01.dat` (1,284,556 bytes)

---

## Code Base Modifications

1. **`Src/DataFiles/GroundMMg.cs`:**
   - Replaced hardcoded `C:\Program Files\Wonderland Online\data\Ground.MMG` path with dynamic resolution (`Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Ground.MMG")`).
2. **`Src/Gui/MainForm1.cs`:**
   - Updated `ItemDatManager.Load` to fallback to `Item.dat` in `Data/` if `itemDat.wpdat` is absent.
