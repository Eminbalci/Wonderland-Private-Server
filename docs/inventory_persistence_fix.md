# Inventory & Equipment Data Persistence Specification

## 1. Overview
Investigation into inventory loss on logout revealed that [`GameDataBase.LoadFinalData`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/GameDataBase.cs) and [`CharacterDataBase.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/CharacterDataBase.cs) directly called `ItemDat.GetItemByID(id)`. When encountering items without complete metadata descriptors, `GetItemByID` returned `null`, causing an unhandled `NullReferenceException` that aborted the entire inventory restoration loop.

## 2. Fixes Applied
1. **Fallback Item Generation**:
   - Replaced fragile lookups with safe fallback initialization:
   ```csharp
   var baseItem = ItemDat?.GetItemByID(id) ?? new DataFiles.PhxItemInfo() { ItemID = id, ItemName = Encoding.ASCII.GetBytes("Item " + id) };
   ```
2. **Robust Error Handling**:
   - Wrapped the entire inventory loading loop in `try / catch` with per-row resilience, ensuring individual item anomalies never cancel the rest of the inventory.
3. **Database Synchronization**:
   - `CharacterDataBase.WritePlayer` continues to atomically save all non-empty bag slots (`storID = '0'`) and equipment slots (`storID = '1'`) on every 1-second auto-save tick and logout event.
