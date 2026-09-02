# Chest & Gathering Drop GUI Editor

## 1. Overview
The server management interface (`MainForm1`) now includes a dedicated **Chest Drops** tab (`tabPageChestDrops`). This interface allows administrators to view, modify, add, and delete map-specific and category-based drop tables and configure respawn cooldown timers dynamically during runtime without restarting the server.

---

## 2. Key Features

### 2.1 Map & Category Loot Pool Selector
* Switch between map pools (`Map 10036`, `Map 10001`, `Map 10010`, `Map 10020`) and category pools (`Category: coconut`, `Category: medicine`, `Category: headband`, `Category: ore`, `Category: default_chest`).
* Populates the interactive `DataGridView` with all drop items belonging to the selected pool.

### 2.2 Interactive Drop Table Editor (`dgvChestDrops`)
* Columns:
  * **ItemID:** Numerical item identifier (e.g., 41066 for Coconut, 30259 for Black Medicine).
  * **ItemName:** Human-readable item title displayed in-game (`AC 23 Sub 57`).
  * **Count:** Quantity awarded per chest interaction.
  * **Weight:** Drop weight for weighted random selection (`_rng.Next(0, totalWeight)`).
* Allows direct in-grid row selection and deletion (`Delete Item` button).

### 2.3 Add New Drop Item (`grpAddDrop`)
* **Item ID Input:** Typing an Item ID automatically looks up and autocompletes the official item name from `cGlobal.ItemDatManager` (`Item.dat`).
* **Item Name Input:** Customizable item display name.
* **Count:** Numeric spinner (1–255).
* **Weight:** Numeric spinner (1–10,000).
* **Add Drop to Pool Button:** Appends the configured entry directly to the active drop table.

### 2.4 Respawn Cooldown & Persistence
* **Respawn Cooldown (s):** Numeric spinner configuring how many seconds broken chests remain inactive before automatically restoring (`ChestDropManager.DefaultRespawnSeconds`).
* **Save Changes Button:** Commits all in-memory table modifications and persists them to `Data/chest_drops.txt`.
* **Refresh Button:** Reloads data from active memory and storage.

---

## 3. Storage Format (`Data/chest_drops.txt`)

```ini
# RespawnSeconds=60
[MAP:10036]
41066|1 pcs Coconut|1|40
28014|1 pcs Fresh Fruit|1|30
28001|1 pcs Sea Water|1|15
27001|1 pcs Ordinary Wood|1|15
[MAP:10001]
28006|1 pcs Red Apple|1|35
28012|1 pcs Mushroom|1|25
30001|1 pcs Herb Potion|1|20
27002|1 pcs Pine Wood|1|20
[CAT:coconut]
41066|1 pcs Coconut|1|80
28014|1 pcs Fresh Fruit|1|20
```
