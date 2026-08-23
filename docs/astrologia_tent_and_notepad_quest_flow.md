# Astrologia Tent & Notepad Quest Flow Specification

## 1. Overview
In Wonderland Online, interacting with Astrologia in Welling Village (Map 12003, NPC #2) provides beginner players with two critical progression items:
1. **Space Capsule (Tent)**: Item ID `36002`
2. **Notebook (Notepad / Quest Journal)**: Item ID `34038`

## 2. Event Structure (Map 12003, Event #4 / Eve.emg Event #5)
The authentic `Eve.emg` event contains 8 subentries (`Sub 0` through `Sub 7`):

| SubEntry | Conditions | Dialogue / Action | Item Granted |
| :--- | :--- | :--- | :--- |
| **Sub 5** | `w1=13042, w2=2` | Dialogue #30614: *"Wait a moment. These things are left by the people..."* | **Space Capsule Tent (`36002`)** |
| **Sub 2** | `w1=13042, w2=2` | Dialogue #30589: *"You can go home after you finish this vital mission..."* | **Notebook (`34038`)** |
| **Sub 0** | `w1=13042, w2=2` | Dialogue #30073: *"Is there anything I can help you with?"* | *None (Default Interaction)* |

## 3. Dynamic Branch Prioritization Logic
Previously, `SelectMatchingBranch` iterated linearly from index 0, returning `Sub 0` immediately because all candidate branches shared `w1 = 13042, w2 = 2`.

### Fixed Progression Order:
1. **Unobtained Item Priority:**
   - The engine inspects each candidate branch's opcodes (`DialogPtr == 1 && op.dialog1 == 1 && op.dialog3 > 0`) or linked item requirement subentry.
   - If the player lacks `36002` (Tent), `Sub 5` is selected.
   - If the player has `36002` (Tent) but lacks `34038` (Notebook), `Sub 2` is selected.
2. **Completion / Fallback:**
   - Once the player possesses both items, the engine falls back to `Sub 0` (general dialogue & mini-game prompts).
