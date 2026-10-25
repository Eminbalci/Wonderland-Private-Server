# 48. Tent Item Movement Persistence and Cache Bypass Fix

## 1. Executive Summary
This document specifies the architecture, packet flow, and SQLite database persistence mechanisms for moving placed objects inside player tents (`AC 62:3`) in Wonderland Online. It resolves the issue where moved tent items reverted to their initial spawn positions (`34, 42` and `37, 42`) upon reconnecting, logging in, or restarting the server.

---

## 2. Root Cause Analysis

### 2.1 Cache Hit Bypass in `CharacterDataBase.GetCharacterData`
During character login (`AC 63:4` character list retrieval followed by `AC 63:2` character selection), `CharacterDataBase.GetCharacter(uint charID)` populated an in-memory cache:
```csharp
Cache[(int)charID] = t;
```
When `CharacterDataBase.GetCharacterData(uint charID, ref Player t)` executed upon character selection:
```csharp
Character c;
if (Cache.TryGetValue((int)charID, out c))
{
    // Copied stats, equips, etc.
    return true; // Bypassed LoadTentData(t)!
}
```
Because this cache branch returned `true` immediately, the downstream execution at line 1276 (`LoadTentData(t)`) was never reached.

### 2.2 Default Constructor In-Memory Re-Initialization
When a new `Player` object was constructed (`new Player(socket, itemdat)`), its constructor created `t.Tent = new Tent(t)`. The `Tent` constructor invoked `InitializeDefaultItems()`, inserting:
- Resource Recycling / Trash Can (`38055`) at `(34, 42)`
- Work Platform / Low Workbench (`38049`) at `(37, 42)`
- Marking `IsDirty = true`

Because `LoadTentData(t)` was bypassed on login:
1. `t.Tent` retained the constructor defaults at `(34, 42)` and `(37, 42)`.
2. Any subsequent save (`SaveCharacterData()`, map warp, or logout) evaluated `t.Tent.IsDirty == true` and overwrote `chartent_items` with the default constructor coordinates.

---

## 3. Protocol & Persistence Implementation

### 3.1 Packet Specification: AC 62 SubCode 3 (Item Move)
- **Client Request:** `[Header 4B] [AC 62: 1B] [Sub 3: 1B] [rawIndex: 2B LE] [x: 4B LE] [y: 4B LE] [z: 4B LE] [rot: 1B]`
- **Server Response & Broadcast:** `[Header 4B] [AC 62: 1B] [Sub 3: 1B] [rawIndex: 2B LE] [x: 4B LE] [y: 4B LE] [z: 4B LE] [rot: 1B]`
  - Confirmation is broadcast to all occupants currently inside the tent via `p.Tent.Broadcast(confirmation)`.
  - Full state is synchronized via `p.Tent.SendTentItemsToAll()`.
  - Database persistence is triggered immediately via `(cGlobal.gCharacterDataBase ?? CharacterDataBase.GlobalInstance)?.SaveTentData(p, force: true)`.

### 3.2 Cache-Hit Tent Data Loading
In `CharacterDataBase.cs`, `LoadTentData(t)` and character settings loading were integrated into the `Cache.TryGetValue` block:
```csharp
if (Cache.TryGetValue((int)charID, out c))
{
    // ... copy basic attributes ...
    #region load settings from ExtData
    try
    {
        var srcExt = GetDataTable(string.Format("SELECT Settings FROM charactersExtData where charID = '{0}'", charID));
        if (srcExt != null && srcExt.Rows.Count > 0 && srcExt.Rows[0]["Settings"] != DBNull.Value)
        {
            string settingsStr = srcExt.Rows[0]["Settings"].ToString();
            if (!string.IsNullOrEmpty(settingsStr)) t.Settings.Load(settingsStr);
        }
    }
    catch (Exception ex) { DebugSystem.Write($"[DEBUG] Error loading settings: {ex.Message}"); }
    #endregion

    #region load tent data
    try
    {
        LoadTentData(t);
    }
    catch (Exception ex) { DebugSystem.Write($"[DEBUG] Error loading tent data: {ex.Message}"); }
    #endregion

    return true;
}
```

### 3.3 WorldServer LoadFinalData Fallback
In `GameDataBase.LoadFinalData(Player c)`:
```csharp
#region Tent
try
{
    CharacterDataBase.GlobalInstance?.LoadTentData(c);
}
catch (Exception ex)
{
    DebugSystem.Write($"[GameDataBase] Error loading tent data for {c.CharName}: {ex.Message}");
}
#endregion
```

### 3.4 In-Memory Clean Initialization
In `Tent.cs`:
```csharp
private void InitializeDefaultItems()
{
    if (_tentObjects == null) _tentObjects = new List<TentItem>();
    if (_tentObjects.Count == 0)
    {
        PlaceItem(38055, 34, 42, 0, 0);
        PlaceItem(38049, 37, 42, 0, 0);
        IsDirty = false;
    }
}
```
In `CharacterDataBase.LoadTentData`:
After clearing and loading rows from `chartent_items`, `player.Tent.IsDirty = false;` is set so loaded items do not cause dirty overwrites.

### 3.5 Tent Floor Bounds Enforcement (`IsInFloorBounds`)
Client disassembly `FUN_003e67c0` strictly requires `0 < (PosX - 28) < 17` (`29 <= PosX <= 44`) and `0 < (PosY - 26) < 25` (`38 <= PosY <= 50` for standard tent). Coordinates outside this polygon cause the client to reject and drop the item sprite.
Implemented `Tent.IsInFloorBounds(int x, int y, int w, int h, int floor)` to reject out-of-bounds placements and movements (`AC 62:1` and `AC 62:3`) with `"Gecersiz konum!"`, protecting items from ever being displaced into the void.

---

## 4. Verification & Validation Evidence
1. Restored Workbench (`38049`) to canonical in-bounds coordinates `(37, 42)` in SQLite `chartent_items`.
2. Verified `IsInFloorBounds` boundary rejection for illegal tiles (e.g. `X=48` beyond right wall).
3. Verified `LoadTentData` restores both Trash Can (`38055`) at `(34, 42)` and Workbench (`38049`) at `(37, 42)`.
4. Automated test verified `MoveItem` updates and immediate persistence via `SaveTentData(force: true)`.
3. `SaveTentData(force: true)` wrote changes directly to SQLite `chartent_items` and reset `IsDirty = false`.
4. Fresh player instance re-authenticated and verified `LoadTentData` retrieved `(48, 44, 2)` without data loss or default fallback.
