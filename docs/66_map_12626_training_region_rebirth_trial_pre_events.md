# Map 12626 (1F, Training Region) NPC PreEvent & Job Rebirth Trial Quest Specification

## 1. Overview
Map `12626` ("1F, Training Region") contains ClickID `1` (`ID = 15316`, "The Immortal Xu" / 徐福) at coordinates `(1069, 694)`. By default, this NPC is defined with `unknownbyte1 = 4` (`4 & 1 == 0`), classifying it as an initially concealed/staged event actor. The NPC's appearance is controlled exclusively through Map 12626 PreEvents evaluated against quest flags `15090` (`入職試煉` - Job Rebirth Trial Quest) and `15091` (`Rebirth Completion Flag`).

## 2. PreEvent Rule Matrix (Map 12626)

| Subentry | Quest Conditions | Action Opcode | Description |
| :--- | :--- | :--- | :--- |
| **0 & 1** | `Quest[15090] < 1` AND `Quest[15091] == 0` | `02-01-00-02` | Hide NPC ClickID 1 ("The Immortal Xu") |
| **2 & 3** | `Quest[15090] == 1` AND `Quest[15091] == 0` | `02-01-00-03` | Reveal NPC ClickID 1 with Frame 1 (Trial Uninitiated Stance) |
| **4 & 5** | `Quest[15090] >= 2` AND `Quest[15091] == 0` | `02-01-00-03` | Reveal NPC ClickID 1 with Frame 0 (Trial Completed Stance) |
| **6+** | `Quest[15091] == 1` | `02-01-00-02` | Hide NPC ClickID 1 (Rebirth Complete) |

## 3. Job Rebirth Quest Lifecycle (Maps 12625 - 12630)

1. **Map 12625 (`Job Palace`):**
   - NPC 1 (Chief Priest, `ID = 15305`): Player interacts at Level 100+ after completing Quest 13189 (`Death of Nicole`).
   - Acceptance grants `Quest[15090] = 1`.
2. **Map 12626 (`1F, Training Region`):**
   - Requires `Quest[15090] == 1`.
   - Interaction with The Immortal Xu (`ID = 15316`) initiates the trial combat.
   - Victory advances the quest to `Quest[15090] = 2` and unlocks the warp to 2F.
3. **Map 12627 (`2F, Training Region`):**
   - Requires `Quest[15090] == 2`.
   - Completion advances quest to `Quest[15090] = 3`.
4. **Map 12628 (`3F, Training Region`):**
   - Requires `Quest[15090] == 3`.
   - Completion advances quest to `Quest[15090] = 4`.
5. **Map 12629 (`4F, Training Region`):**
   - Requires `Quest[15090] == 5`.
   - Completion advances quest to `Quest[15090] = 6`.
6. **Map 12630 (`5F, Training Region`):**
   - Final trial stage (`Quest[15090]` steps 7 through 9).
   - Defeating the guardian sets `Quest[15090] = 10`.
7. **Return to Map 12625:**
   - Chief Priest acknowledges completion: sets `Quest[15090] = 11` (or marks complete) and sets `Quest[15091] = 1`, applying character Rebirth class promotion.

## 4. Root Cause for Invisible NPC

When a player teleports directly into Map 12626 using administrative warps or developer commands without talking to the Chief Priest in Map 12625:
- `Quest[15090]` is unset (`0`).
- PreEvent Subentry 0/1 executes upon map entry (`Quest 15090 < 1`).
- Action `02-01-00-02` executes, suppressing the spawn of NPC ClickID 1.

## 5. GM Commands for Quest Flag Management

Administrative GM commands were introduced in `wlo.pserver.core/Game/Player.cs`:

### In-Game Chat Syntax:
```text
/quest <quest_id> [step]
:quest <quest_id> [step]
/setquest <quest_id> [step]
Q><quest_id>>[step]
```

### Examples:
- `/quest 15090 1` : Sets Quest 15090 to Step 1 (reveals The Immortal Xu on Map 12626 immediately).
- `/quest 15090 0` : Clears Quest 15090.
- `Q>15090>1` : Packet shortcut version.

### Behavior:
1. Updates in-memory quest dictionary `Player.Quests`.
2. Persists changes to SQLite table `charquest` (`INSERT OR REPLACE` / `DELETE`).
3. Sends packet AC 23, Sub 25 (`[F4 44 04 00 17 19 <QuestID_LE> <Step>]`) to update client quest log.
4. Invokes `QuestManager.SyncPerPlayerNpcVisibility(player, mapId)` to re-evaluate all PreEvents and emit NPC reveal/hide packets in real time without requiring map reload.
