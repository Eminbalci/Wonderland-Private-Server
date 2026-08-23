# Tent Crafting Station, Pack-Up, and Database Persistence Protocol

## 1. Overview
In Wonderland Online, player tents serve as personal bases and crafting hubs. Each character owns an isolated tent instance (`MapID = 100000 + CharID`), private interior state, and persistent furniture arrangement.

## 2. Database Schema & Persistence
1. **`chartent` Table**:
   - `charID` (INT PK): Character identifier.
   - `locked` (INT): Whether visitor entry is locked.
   - `enlarged` (INT): Tent expansion tier.
   - `tenttype` (INT): Exterior visual style.
   - `floor1Color`, `floor1wallpaper`, `floor2Color`, `floor2wallpaperr` (INT): Customized floor/wallpaper aesthetics.

2. **`chartent_items` Table**:
   - `pri_key` (INT PK AI): Unique row ID.
   - `charID` (INT): Owner character ID.
   - `itemID` (INT): Item ID of the furniture/crafting station (e.g. 38027 Coconut Basin, 38049 Work Platform).
   - `posX` (INT): Grid/pixel coordinate X.
   - `posY` (INT): Grid/pixel coordinate Y.
   - `floor` (INT): Tent floor index (0 = 1st floor, 1 = 2nd floor).
   - `rotate` (INT): Object orientation (0-3).

3. **Lifecycle Synchronization**:
   - **Login (`LoadFinalData`)**: Queries `chartent` and `chartent_items` via `LoadTentData(Player)`. If no existing furniture records exist, initializes default starter stations (Coconut Basin `38027` and Work Platform `38049`) and writes them to the DB.
   - **World Save / Disconnect (`WritePlayer`)**: Invokes `SaveTentData(Player)`, serializing current `TentObjects` and decor styles.
   - **Interactive Actions (`AC 62:1` Place, `AC 62:3` Move)**: Triggers instant `SaveTentData(Player)` calls.
   - **Deletion (`DeleteCharacter`, `WriteNewPlayer`)**: Cascades cleanup across `chartent` and `chartent_items`.
