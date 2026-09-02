# Map NPCs & Event Sequence Studio Architecture & Specification

## 1. Overview
The **Map NPCs & Event Sequence Studio** (`🗺️ Map NPCs & Events`) in [`MainForm1.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Gui/MainForm1.cs) provides visual inspection, sequence tracing, and live debugging for all entities, NPCs, and interactive event bytecodes loaded from `Eve.emg` across all game maps.

---

## 2. Architecture & UI Layout

```
+---------------------------------------------------------------------------------------------------+
| 🗺️ Map Selector: [10035 - Rhode Island (Beach) v]  ID: [10035]  🔍 Filter: [...]  [🔄 Reload]     |
+-------------------------------------------------+-------------------------------------------------+
| 👥 NPCs ON SELECTED MAP                         | 📜 CHRONOLOGICAL EVENT & ACTION SEQUENCE FLOW   |
| 🔍 Search: [Robinson              ] 8 NPCs      | ⚡ Selected: Burke (ClickID: 8, TID: 11019)     |
+-------------------------------------------------+-------------------------------------------------+
| ClickID | NPC Name     | TID   | Pos   | Script | 📜 Event Entry #7: 'Action Trigger #7'          |
| 1       | Robinson     | 12032 | ...   | ⚡ 1 Ev|   📌 BRANCH #1 -> Condition: Level >= 0         |
| 8       | Burke        | 11019 | ...   | ⚡ 1 Ev|      💬 [NPC SPEECH] TalkID #11070:             |
| 2       | Beach Chest  | 19038 | ...   | ⚡ 1 Ev|         "#swav1538/#sRoaring..."                |
| 4       | Drift Bottle | 11019 | ...   | ⚡ 1 Ev|                                                 |
| 6       | Beach Rock   | 19033 | ...   | ⚡ 1 Ev| [Live Player: Emin v] [▶️ Trigger on Player]    |
+-------------------------------------------------+-------------------------------------------------+
```

---

## 3. Core Features

### 1. Map Navigation & Filtering
- **Dynamic Map Dropdown (`cmbMapSelect`)**: Lists all loaded maps with numeric IDs and canonical English names.
- **Direct ID Jump (`numMapSelect`)**: Instant jump to any map ID (e.g. `10001`, `10035`, `10036`, `12001`).
- **Live Search Filter (`txtMapFilter`)**: Real-time filtering of map list by name or ID.

### 2. Map Entity & NPC Grid (`dgvMapNpcs`)
- **Click ID**: Map object interaction index.
- **NPC Name**: Authentic name resolved via `npc.json` and `SceneDataManager`.
- **Template ID**: Base visual sprite / monster template.
- **Coordinates**: Spawn position `(X, Y)`.
- **Script Status**: Interactive event trigger count (`⚡ X Event Triggers`) or static ambient status.

### 3. Chronological Event Sequence Inspector (`rtbEventSequenceFlow`)
- When any entity is selected, its linked `Eve.emg` event bytecode branches and sequential opcodes are decoded in chronological order:
  - 💬 **Dialogues**: Talk ID, speaker identification (Player vs NPC ClickID), sound effects, and resolved English text via [`TalkResolver.ResolveDetailed()`](file:///d:/GitHub/Wonderland-Private-Server/PhoenixData/DataFiles/TalkResolver.cs).
  - 🎁 **Item Grants**: Opcode 1 (`dptr=1, d1=1`) and Opcode 5 awards with item name and quantity.
  - 🔻 **Item Consumptions**: Opcode 5 items consumed from player inventory.
  - 🚩 **Quest Flags**: Flag step transitions and completion status.
  - ⚔️ **PvE Battles**: Boss / Monster encounter triggers.
  - 🚪 **Warps & Teleports**: Map ID, Map Name, and destination coordinates `(X, Y)`.
  - 👥 **Companion Recruits**: Pet ID and resolved Companion Name.
  - ❓ **Dialogue Choice Prompts**: Interactive branch decision triggers.
  - 💥 **Animations & Despawns**: Visual break frames and gathering respawn cycles.

### 4. Live Server Event Triggering (`SimulateEventForSelectedPlayer`)
- Enables GM to select any online player from `cmbLivePlayerForNpc` and trigger the selected NPC's event chain in real-time via `EveEventInterpreter.TryExecute()`.
