# Client Combat, Inventory & Economy Decompiled Specifications

This document outlines client-side combat mechanics, inventory management, crafting, NPC interactions, teleportation, item mall economy, and status effects extracted from `aLogin.exe.1.c`.

---

## 1. Item Mall (IM) & Points Economy

### A. Points Currency System
The client manages **three currency types** for the Item Mall:
- **IM Points** — Premium purchased currency. Displayed as `"IM Points: "`.
- **Bonus Points** — Earned bonus currency. Displayed as `"Bonus Points: "`. Capped at **60,000** (`"Note: Bonus Points capped at 60,000"`).
- **Tokens** — Event/forge tokens. Used in token-to-forge conversions.

### B. Points Validation
- `"Not enough Points!"` / `"Not enough Points"` / `"Low Points"` — Insufficient currency alerts.
- `"Points used"` / `"Points spent: "` — Confirmation after purchase.
- `"Points remain "` — Remaining balance display.
- `"Player %s lacks Points"` — Multiplayer point check failure.
- `"Points Card"` — Redeemable points card item.
- `": insert in equip to increase "` — Points-purchasable gem insertion into equipment.

### C. Token/Points Fallback
```
"All tokens used up. Will consume Points. Confirm?"
```
When tokens are exhausted, the forge system falls back to IM Points with user confirmation.

### D. Fun Token System
```
"Playing with Fun Token, Play Token at event will not double rewards. Confirm?"
```
Fun Tokens are a non-premium alternative that bypass event multipliers.

### E. Payment Selection Dialog
`"Points\nToken\nFun Token"` — Dropdown/radio button options for currency selection in item mall and mini-games.

### F. Item Mall Events
- `"Double rewards in Item Mall mini-games has begun!"` / `"...has ended!"` — 2x mini-game reward events.

---

## 2. Crafting System

### A. Crafting Restrictions
- `"Can't craft"` / `"Not for crafting"` / `"Not a material"` — Invalid material/recipe validation.
- `"Already 5 semi-finished crafts"` — Maximum concurrent crafting jobs: **5**.
- `"Unable in craft"` — Action blocked while crafting is active.
- `"Can't remove when craft"` — Cannot remove items during active crafting.
- `"Crafting, can't recycle"` — Recycle blocked during crafting.
- `"Can't sell Crafting pet"` — Crafting-assigned pets are untradeable.
- `"Moving will pause crafting"` — Movement interrupts crafting with a confirmation dialog.

### B. NPC Store Forms
- `"NpcStoreSelectedItem"` — NPC shop item selection grid identifier.
- `"form_BuyQuant_Npc"` / `"form_BuyQ_Npc"` — NPC purchase quantity input forms.

---

## 3. Inventory System

### A. Inventory Overflow Alerts
- `"Inventory is full"` / `"Inventory is full!"` / `"Sorry, inventory is full!"` — Multiple overflow variants.
- `"No inventory space"` / `"Np inventory space"` — Shorthand overflow.
- `"Free 1 inventory slot to use"` — Minimum space requirement.
- `"Teammate inventory full"` / `"Player's inventory full"` — Party member overflow.
- `"Props inventory is full"` — Separate props/consumable inventory full.
- `"Inventory is full. Make more space before next maintenance"` — Pre-maintenance warning.

### B. Inventory UI
- `"Inventory Only"` — Filter toggle for inventory-only view.
- `"[ Equipped ]"` — Equipped item label in character panel.

---

## 4. Equipment System

### A. Equip/Unequip Validation
- `"LV is low to equip"` — Level requirement not met.
- `"Can't change equips"` — Equipment change blocked (combat/transform state).
- `"Pet can't equip"` — Pet equipment restriction.
- `"Remove Pet equip first"` — Pet equipment must be removed before certain actions.
- `"Modeled equips only"` — Only model-type equipment allowed.
- `"Job's cape must be in inventory"` — Class cape requirement for job change.
- `"Equip hoe first"` — Farming tool requirement.
- `"Remove all pet equips to reborn"` — Pet equipment must be cleared before rebirth.

### B. Durability
- `"] durability run out"` — Equipment durability has reached zero.
- `"Equipped [["` — Equipped item notification prefix.

### C. Equipment UI Forms
- `"Form_EquipStore_1"` — Equipment storage form.
- `"Form_SaleEquip_1"` — Equipment sale form.
- `"EquipItemImage"` — Equipped item image asset identifier.

---

## 5. NPC System

### A. NPC Data Loading
- `"Read Npc."` — NPC data file parsing log.
- `"Read Npc:: ParaNode"` — NPC parameter node parsing.
- `"npccount"` / `"npcpic"` — NPC count and picture resource identifiers.

### B. NPC Restrictions
- `"Can't store NPC"` — NPC cannot be placed in storage.
- `"Quest NPC can't use"` — Quest-bound NPCs have restricted actions.
- `"Remove hired Mercenary NPC?"` — Mercenary NPC dismissal confirmation.

### C. NPC Forms
- `"form_NPCUpgrade"` — NPC equipment upgrade panel.
- `"form_npcSkillTree"` — NPC skill tree display panel.

---

## 6. Teleportation System

- `"Can't teleport"` — Generic teleport denial.
- `"Can't teleport from here"` — Location-based teleport restriction.
- `"Failed to teleport"` — Server-side teleport failure.
- `"Teleport to the event?"` — Event teleport confirmation dialog with 5-second timeout.

---

## 7. Combat & Status Effects

### A. Class Stat Bonuses (Passives)
| Class | Primary Bonus | Secondary Bonus |
|-------|--------------|-----------------|
| Warrior | +10% ATK (no equips) | Decreased counter chance, slight magic boost |
| Guardian | +10% DEF (no equips) | Slight damage resistance |
| Knight | +10% SPD (no equips) | Can mount SPD animals without saddle |
| Mage | +10% MATK (no equips) | Slightly increases sealing rate |
| Priest | +10% MDEF (no equips) | Improves sealing resistance, any damage resistance |
| Thief | +10% SPD (no equips) | Reduces magic attack/seal fails |

### B. Status Effects
- `"Status effect expired"` — Generic buff/debuff expiry notification.
- `"EXP Boost"` — Experience boost buff label.
- `"Poison"` — Poison item/status category label.

### C. Pet Training & Capture
- `"Can't capture"` — Pet capture failure.
- `"Pet training complete, level up to continue"` — Pet must level to continue training.
- `"Can't use rebirth pill"` — Rebirth item restriction.

---

## 8. Recycle System

- `"Item will be recycled. Sure?"` — Recycle confirmation with styled text.
- `"Can't recycle, inventory full"` / `"Can't recycle, storage full"` — Overflow prevention.
- `"Crafting, can't recycle"` — State conflict.

---

## 9. Auto-Walk & Timer System

- `"Auto Walk: "` — Auto-walk toggle/status display in settings panel.
- `"Auto Unequip: "` — Auto-unequip toggle.
- `"Leave Timer: "` — AFK/leave countdown timer.
- `"Timer reseted"` — Timer reset confirmation.
- `"Icon_TimerBar"` — Timer bar UI asset.

---

## 10. Level Up & Daily Systems

- `" level up "` — Level up notification string.
- `"] daily draw "` — Daily draw event participation.
- `"Quest Item"` — Quest item category label.
- `"Quiz Event: Daily 2 PM to 2:30 PM"` — Scheduled daily quiz event.
- `"Allocate all points"` — Stat point allocation requirement.

---

## 11. Interserver PVP Events

- `"Interserver PVP has begun! Come to Interserver NPC!"` — Cross-server PVP event start.
- `"Interserver PVP will start at 3 PM, join by NPC at Capitol Building 2F!"` — Scheduled announcement.
- `"Christmas season double EXP event"` — Seasonal EXP multiplier with potion stacking.
