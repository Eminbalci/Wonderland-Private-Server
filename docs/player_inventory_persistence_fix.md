# Player Inventory & Item Persistence Fix

## 1. Problem Diagnosis
Previously, items picked up in-game (from opening chests, gathering nodes, drops, shops, or quests) were lost upon logging out or shutting down the server.

### Root Cause
1. **Ineffective `UPDATE` Queries in `WritePlayer`:** In [`CharacterDataBase.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/CharacterDataBase.cs), inventory saving executed `UPDATE inventory SET ... WHERE charID='...' AND storID='0' AND invIdx='...'`. Because new items occupied slots without pre-existing rows in the database, the `UPDATE` query affected 0 rows and silently failed to insert them.
2. **Missing Shutdown Flush:** During server shutdown in [`ShutDown Dialog.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Gui/ShutDown%20Dialog.cs), active online characters were not explicitly saved before terminating the world server.

---

## 2. Solution Implemented

### 2.1 Full Inventory & Equipment Synchronization ([CharacterDataBase.cs](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataBase/CharacterDataBase.cs))
* Refactored `WritePlayer` to atomically clean and batch-insert all non-empty bag slots (`storID = '0'`) and equipment slots (`storID = '1'`):
```csharp
ExecuteNonQuery("DELETE FROM inventory WHERE charID = '" + charID + "' AND storID = '0';");
if (invRows.Count > 0)
{
    ExecuteNonQuery(string.Format("INSERT INTO inventory (invIdx,charID,storID,itemID,dmg,qty,pos,socketID,bombID,sewID,forge) VALUES {0};", string.Join(",", invRows)));
}
```

### 2.2 Server Shutdown Player Flush ([ShutDown Dialog.cs](file:///d:/GitHub/Wonderland-Private-Server/Src/Gui/ShutDown%20Dialog.cs))
* Added a pre-shutdown routine iterating over all active players in `cGlobal.gCharacterDataBase.GetOnlinePlayers()` and calling `WritePlayer` before shutting down the socket listener and world server.
