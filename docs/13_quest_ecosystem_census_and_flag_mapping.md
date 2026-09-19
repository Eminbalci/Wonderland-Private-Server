# Quest Ecosystem, Census, and Flag Mapping Specification

## 1. Architectural Overview

Wonderland Online tracks narrative progression through three synchronized layers: binary quest marks in `Data/Mark.dat`, bytecode flag evaluations in `Data/Eve.emg`, and relational state tracking in the database table [`character_quests`](file:///D:/GitHub/Wonderland-Private-Server/docs/02_database_schema_and_persistence_lifecycle.md#35-charquest-alias-character_quests). This document provides a complete census of the quest ecosystem, clarifying the relationship between community documentation (~502 quests) and internal engine structures.

---

## 2. Quest Census & Forensic Audit

```
+-------------------------------------------------------+---------------+
| Category / Metric                                     | Quantity      |
+-------------------------------------------------------+---------------+
| Raw Binary Marks in Data/Mark.dat                     | 2,154 marks   |
| Loaded Quest Records in QuestManager.AllQuests        | 1,681 records |
| Unified Master Quests in QuestManager (MasterQuests)  | 1,027 quests  |
| Distinct Quest Flag IDs in Data/Eve.emg scripts       | 1,507 flags   |
| Canonical Player Journal Quests (Wiki / AC 24 UI)    | ~502 quests   |
+-------------------------------------------------------+---------------+
```

### 2.1 The "~502 Quests" Figure Explained

Players and community wikis (Wonderland Online Wikia, IGG Official Quest Guides) cite **approximately 502 quests** (498–505 depending on whether companion rebirth sagas and holiday event quests are counted).

The internal engine numbers (1,027 Master Quests, 1,507 Flag Scripts) are higher due to three architectural factors:

1. **Dual-State Mark Pairing (In-Progress vs. Completed):**
   * Major storyline quests maintain two distinct records in `Data/Mark.dat`: an *InProgress* record (e.g., Mark `12001`, containing `#01`, `#02` step logs) and a *Completed* record (e.g., Mark `12002` or `#99` completion summary).
   * [`QuestManager.BuildMasterQuests`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/QuestRelated/QuestManager.cs) pairs these records, reducing 1,681 individual records into 1,027 structured Master Quests.

2. **Unlisted Delivery Tasks & Mini-Quests:**
   * Many minor trades (e.g., trading coconut shells, gathering clay, firewood exchanges, basic weapon craft recipes) are standalone quest flags in `Eve.emg` but do not create a dedicated star/badge page in the player's Quest Notebook UI (`AC 24`).

3. **Persistent Container & Gathering Flags:**
   * One-time chests and unique resource nodes use synthetic quest flags (`MapID * 1000 + ClickID` or IDs in ranges 40000–50000) to permanently record opened containers.

---

## 3. Regional Distribution of Quest Flags (`Data/Eve.emg`)

Across all 10,644 event scripts on 1,119 world maps, 1,507 distinct Quest Flag IDs are organized into regional blocks:

```
+---------------+---------------+-----------------------------------------------+
| Quest ID Range| Distinct Flags| Region / Narrative Arc                        |
+---------------+---------------+-----------------------------------------------+
| 10000 - 10999 | 27 flags      | Passenger Ship Prologue & Shipwreck Intro     |
| 11000 - 11999 | 41 flags      | Kelan Shore, Robinson's Beach & Tent Camp    |
| 12000 - 12999 | 115 flags     | North Island, South Island, Kelan & Welling   |
| 13000 - 13999 | 208 flags     | Holy Village, Kyoto & Japanese Islands        |
| 14000 - 14999 | 169 flags     | China, Chang'an, Fishing Village & Great Wall |
| 15000 - 15999 | 266 flags     | Maya, Inca, Amazon Forest & South America     |
| 16000 - 16999 | 105 flags     | Rome, Athens & Mediterranean Caverns          |
| 17000 - 17999 | 28 flags      | Bangkok, Thailand & Floating Market           |
| 18000 - 18999 | 122 flags     | Persia, India, Egypt & Desert Pyramids        |
| 20000 - 20999 | 49 flags      | South Pole, Ice Caves & Subsea Caverns        |
| 21000 - 21999 | 104 flags     | Cornwell, Australia, Ghost Isle & Pirate Cave |
| 40000 - 40999 | 75 flags      | Secondary Dungeons & Spatial Event Triggers   |
| 50000 - 50999 | 196 flags     | Special Trials, Festival Events & Rebirth Arc |
| 53000 - 53999 | 2 flags       | High-Level Endgame Specialty Flags            |
+---------------+---------------+-----------------------------------------------+
| Total         | 1,507 flags   | Across all 1,119 World Maps                   |
+---------------+---------------+-----------------------------------------------+
```

### 3.1 Master Quest Categorization (1,027 Quests)
* **Storyline & Area Quests:** 864 Quests
* **Dungeons & Instance Challenges:** 62 Quests
* **Companion Recruitment & Rebirth Sagas:** 46 Quests
* **Minigames & Arcade Challenges:** 33 Quests
* **Crafting, Housing & Vehicle Construction:** 22 Quests

---

## 4. Binary Mark Parser Architecture (`Data/Mark.dat`)

`Data/Mark.dat` is parsed by [`PhxMarkDat.cs`](file:///D:/GitHub/Wonderland-Private-Server/PhoenixData/DataFiles/PhxMarkDat.cs) and [`QuestManager.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/QuestRelated/QuestManager.cs):
* **Record Dimensions:** Stride of 553 bytes, 526-byte payload per record. Exactly 2,154 raw mark entries.
* **Text Offsets:**
  - Title at `+200..265` (65 bytes, reversed Big5/ASCII).
  - Body at `+266..525` (260 bytes, reversed ASCII).
* **Regex Step Extraction:** Evaluates `#(\d{2})([^#]*)` to isolate intermediate dialogue instructions (`#01`, `#02`) and the final completion summary (`#99`).
* **Pairing Mechanics (`BuildMasterQuests`):** Adjacent records sharing matching titles are unified into a single `QuestDefinition` containing both `InProgressMarkID` and `CompletedMarkID`.

---

## 5. Quest State Machine & Anti-Demotion Rules

Quest lifecycles are persisted in table [`character_quests`](file:///D:/GitHub/Wonderland-Private-Server/docs/02_database_schema_and_persistence_lifecycle.md#35-charquest-alias-character_quests):
* **Monotonicity:** Once a quest transitions to `Completed (3)`, it can never be regressed to `InProgress (1)` or `NotStarted (2)`.
* **Step Progression:** Multi-stage quests update `step` within `InProgress` state; client dialogue branches check matching `step` via Eve condition opcode `0x03` or `0x05`.
