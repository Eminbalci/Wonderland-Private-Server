# Event & Quest DB Studio

Comprehensive documentation for the unified **Event & Quest DB Studio** in the Wonderland Online server management interface (`MainForm1.cs`), integrating Eve map bytecode execution, Talk.dat text decoding, multi-stage storyline quest management, and live in-game testing.

---

## 1. Architectural Overview

The **Event & Quest DB Studio** consolidates the legacy *Quest DB Manager* and *7 Event Systems* into a centralized 3-mode visual workstation:

```
+---------------------------------------------------------------------------------------------------+
|  ⚡ Event & Quest DB Studio                                                                      |
+---------------------------------------------------------------------------------------------------+
|  MODE 1: 🗺️ Map Events & Dialogue Flow Studio                                                    |
|  - Map Search & Dropdown (1,119 official maps)                                                    |
|  - Entity Tabs: NPCs, PreEvents, Warps, Chests, Mining, Traps                                     |
|  - Dialogue & Bytecode Visualizer: Decodes Eve.Emg conditions & Opcodes with Talk.dat text        |
+---------------------------------------------------------------------------------------------------+
|  MODE 2: 📜 Storyline & Companion Quests                                                          |
|  - Filtered Master Quests (free of Visit Mark noise)                                              |
|  - Category filters: Storyline, Companion/Rebirth, Crafting, Dungeons, Minigames                  |
|  - Multi-Stage Step Progression Viewer & Editor                                                   |
|  - Acceptance, In-Progress, and Completion Dialogue Script Viewer                                 |
|  - Reward & Boss Battle configuration                                                             |
+---------------------------------------------------------------------------------------------------+
|  MODE 3: 💬 Talk.dat Dialogue Browser                                                             |
|  - Substring & Talk ID Search Engine across 40,000+ authentic game lines                          |
|  - RichTextBox live formatted preview                                                             |
+---------------------------------------------------------------------------------------------------+
|  LIVE TESTER TOOLBAR (Bottom Docked)                                                              |
|  [Online Players] [Event ID / ClickID] [⚡ Execute Event] [🔄 Sync Flags (AC 24)] [🚀 Warp Map]     |
+---------------------------------------------------------------------------------------------------+
```

---

## 2. Mode 1: Map Events & Dialogue Flow Studio

### Entity Categorization
Maps loaded from `eve.Emg` categorize interactive and static objects into dedicated DataGridView tabs:
1. **👤 NPCs & Monsters**: ClickID, NPC Name (resolved from `Npc.dat`), Template ID, Map Pixel Coordinates $(X, Y)$, and Script Interactivity status.
2. **🎭 PreEvents**: Conditional scene cutscenes and cinematic triggers.
3. **🚪 Doors & Warps**: Portal ClickID, source coordinates, destination map ID/name, destination $(X, Y)$, and pass prerequisites (`neededtopass`).
4. **📦 Chests & Drops**: Interactive map chests and pickups with resolved item names from `Item.dat`.
5. **⛏️ Mining / Gathering**: Resource gathering nodes with required tools (Pickaxe, Fishing Rod, Wood Axe).
6. **🪤 Traps & Steps**: Coordinate-triggered collision boxes with trigger dimensions $(W \times H\text{ px})$.

### Eve Bytecode Opcode Mapping & Flow Decoding
When any NPC, PreEvent, or Chest row is clicked, `FormatEventScriptFlow` recursively decodes the underlying event sub-branches and opcodes:

| Opcode / Condition | Bytecode Identifier | Decoded Meaning in Studio Visualizer |
| :--- | :--- | :--- |
| **Level Gate** | `unknownbyte1 = 1` | Requires `Player Level >= unknownword1` |
| **Item Gate** | `unknownbyte1 = 2` | Requires `Player Has Item #unknownword3 (Name) x unknownword2` |
| **Pet Gate** | `unknownbyte1 = 4` | Requires `Player Has Companion Pet #unknownword1` |
| **Gold Gate** | `unknownbyte1 = 5` | Requires `Player Gold >= unknownword1 G` |
| **Choice Gate** | `unknownbyte1 = 7` | Player chose Option `#unknownword2` in preceding choice prompt |
| **Inventory Gate** | `unknownbyte1 = 15` | Pre-checks for `unknownword1` free non-multi-cell inventory slots |
| **Quest Flag Gate**| `unknownword1 > 0` | Checks if Flag `#unknownword1` is Not Started, In-Progress, or Completed |
| **Opcode 1 (Speech/Flag)** | `DialogPtr = 1` | If `dialog3 >= 10000`: Sets Quest Flag `#dialog3` to Step `dialog2`.<br>Else: Fetches authentic speech string from `Talk.dat` using `dialog2` or `dialog3`. |
| **Opcode 2 (Choice/Anim)** | `DialogPtr = 2` | If `dialog2 = 6`: Player Choice prompt.<br>If `dialog2 = 5`: Prop break / chest opening animation (AC 22:1).<br>Else: Dialogue frame. |
| **Opcode 3 (Recruitment)** | `DialogPtr = 3` | Recruits companion pet `dialog2` into player team. |
| **Opcode 5 (Item Grant/Drop)**| `DialogPtr = 5` | If `dialog2 = 1`: Grants item `#dialog1` ($x \text{dialog3}$) with victory fanfare.<br>If `dialog2 = 2`: Consumes item `#dialog1` ($x \text{dialog3}$). |
| **Opcode 6 (Battle)** | `DialogPtr = 6` | Initiates turn-based PvE encounter against monster group `#dialog1`. |
| **Opcode 7 (Teleport)** | `DialogPtr = 7` | Teleports player to Map `#dialog1` at coordinates $(X=\text{dialog2}, Y=\text{dialog3})$. |
| **Opcode 8 (Fanfare/SFX)** | `DialogPtr = 8` | Plays sound effect / fanfare / cutscene ID `#dialog1`. |
| **Opcode 9 (Minigame)** | `DialogPtr = 9` | Launches interactive minigame `#dialog1`. |
| **Opcode 10 (Gold Award)**| `DialogPtr = 10` | Awards `dialog1` Gold coins to player. |
| **Opcode 11 (EXP Award)** | `DialogPtr = 11` | Awards `dialog1` EXP to player. |

---

## 3. Mode 2: Storyline & Companion Quests

### Visit Mark De-duplication
Previously, GPS and map exploration markers (`Visit Mark`, `Time Mark`, `Quest Mark`) polluted the quest list. The database engine filters these exploration points and presents only true Storyline, Companion, Crafting, and Instance quests in `MasterQuests`.

### Multi-Stage Step Progression
Quests with multi-stage progressions (e.g. Talk to NPC A $\rightarrow$ Collect items from NPC B $\rightarrow$ Return to NPC A) are displayed in the **Multi-Stage Steps & Objectives** tab:
- Step Index
- Step Type (`Dialogue`, `ItemCollection`, `PvEBattle`, `Minigame`)
- Target NPC Name / Pattern
- Stage Objective Description

---

## 4. Mode 3: Talk.dat Dialogue Browser

Provides instant search and inspection across 40,000+ authentic game lines:
- Fast query execution by numeric Talk ID or case-insensitive text match.
- Live formatted text preview in `RichTextBox`.

---

## 5. Live Tester Toolbar

Enables real-time testing on connected players without restarting the server:
- **⚡ Execute Event Now**: Dispatches Eve event click directly to the selected online player (`EveEventInterpreter.ProcessClick`).
- **🔄 Sync Quest Flags (AC 24)**: Resynchronizes the player's quest journal and completed flags packet.
- **🚀 Warp to Selected Map**: Teleports the online player directly to the map selected in the Map Event Studio.
