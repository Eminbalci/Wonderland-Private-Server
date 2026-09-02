# Character Relational Data & Map NPC Visibility GUI Editor

## Overview
The **Character Data & Map NPC Visibility Editor** is an integrated management interface within the Wonderland Server GUI. It allows administrators to inspect and modify all connected databases for any character in real time, including quest progression, pet/companion rosters, map NPC/chest visibility and despawn states, inventory, and skills.

## Features & Subsystems

### 1. Quests Tab (`charquest`)
* **Live Database Grid:** Displays all registered quests for the character with resolved Quest Titles from `Mark.dat` / `QuestManager`.
* **State Controls:**
  * `+ Add / Set Quest`: Assigns any Quest ID with status `NotStarted (0)`, `InProgress (1)`, or `Completed (2)`.
  * `✓ Mark Completed`: Instantly completes the selected quest.
  * `➔ Step +1`: Increments the quest progression step.
  * `🗑 Delete`: Purges the quest record.
  * `⚡ Sync Quests to Live Client`: Dispatches `AC 24:4` / `AC 24:5` packets to an online player without requiring a relog.

### 2. Companions & Pets Tab (`character_pets`)
* **Companion Rosters:** Displays all recruited companions across slots 1 to 4 with real-time stats (Level, HP/MaxHP, SP/MaxSP, Amity, isBattle, isRide).
* **Presets & Controls:**
  * Quick presets for authentic story companions (Robinson, Monkey, Niss, Xaolan, Elin, Shizune, Cliff, Clive, Sam).
  * `💖 Max Amity & Full HP`: Instantly restores the selected companion to 100 Amity and full HP.
  * `💾 Save Grid`: Writes in-place edits from the DataGridView directly into `character_pets`.
  * `⚡ Sync Pets to Live Client`: Dispatches `AC 15:1` (Pet Info), `AC 19:1` (Battle State), and `AC 19:4` (Following State) packets to the active client.

### 3. Map NPC & Prop Visibility Manager
* **Map Selector:** Select any map (or automatically load the character's current location map).
* **Eve.emg Integration:** Inspects all interactive NPCs, gathering nodes, chests, and story companions on the map.
* **Visibility & State Toggles:**
  * `📦 Break / Open`: Marks a chest as broken/opened and updates the linked quest flag.
  * `🔄 Reset / Unbroken`: Restores a broken chest to its closed state.
  * `🚫 Hide / Despawn`: Sends authentic `AC 22:10 [clickID, 0xFF, 0xFF]` packet to despawn stationary NPCs (such as recruited Robinson on map 10035 beach).
  * `👁 Show / Visible`: Sends `AC 22:10 [clickID, 0x00, 0x00]` packet to restore NPC visibility.

### 4. Inventory Tab (`inventory`)
* **Backpack & Equipment Grid:** Displays Item ID, authentic Item Name from `Item.dat`, Quantity, Durability / Damage, and Slot Position.
* **Item Management:**
  * `+ Add Item`: Adds items to the player's live inventory or database.
  * `🛠 Repair 100%`: Sets `dmg = 0` to repair equipment.
  * `🗑 Delete`: Removes the item from inventory.

### 5. Event & NPC Unlocks (`charunlocks`)
* **Event Unlock Grid:** Displays all completed/unlocked world events and NPC interactions (`pri_key`, `maploc`, `clickID`) with target descriptions.
* **Controls:** Add event unlock flags or revoke existing flags with live NPC visibility synchronization (`AC 22:10`).

### 6. Learned Skills & Grades (`character_skills`)
* **Learned Skills Grid:** Displays character's learned stunt, elemental, and progression skills along with their current `Grade` (1–10) and `EXP`.
* **Skill Controls:** 
  * `+ Learn / Update Skill`: Assigns or updates a skill, grade, and EXP, dispatching live client packets (`AC 5:11`, `AC 8:1 stat 110`, `AC 5:4`) and saving to `character_skills` database table.
  * `🗑 Forget Skill`: Deletes the skill from `character_skills` database and updates live player skill slots.
  * `🔄 Refresh`: Reloads skills from database or active player session.

## How to Access
1. Open the Wonderland Server GUI.
2. Navigate to the **Characters** tab.
3. Select a character row and click **"Edit Data ⚙"** (or double-click the character row).
