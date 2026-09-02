# Item Mall GUI Management Specification

## 1. Visual Server GUI Management Interface
The server application GUI includes a dedicated **🛍️ Item Mall** management tab:
- **Live Catalog Table (`dgvMallCatalog`)**: Displays all items with `ItemID`, `ItemName`, `Category`, `PointCost`, and `Count`.
- **Interactive Row Selection**: Clicking any row auto-populates the editor controls for instant modification.
- **Auto Name Lookup**: Typing an `ItemID` into the text box automatically fetches the item's authentic name from `ItemDat`.
- **Reordering Controls**:
  - `⬆️ Move Up`: Moves the selected item one position up in the catalog.
  - `⬇️ Move Down`: Moves the selected item one position down in the catalog.
- **Add / Update (`➕`)**: Saves modifications or adds new entries to the active catalog and writes to `Data/item_mall.txt`.
- **Delete (`🗑️`)**: Removes selected items from the catalog.
- **Reload (`🔄`)**: Reloads catalog configuration from disk.
- **Player Points Management**: Allows adding or setting IM points for any online player or database account.
