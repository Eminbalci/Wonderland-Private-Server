# Starter Ship and Scene Transition Lifecycle Specification

## 1. Architectural Overview

The scene transition subsystem coordinates authored event transitions, inter-map teleports, and cinematic cutscene triggers across Wonderland Online maps via [`EveEventInterpreter.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs) and [`EveEventRuntime.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventRuntime.cs).

---

## 2. Eve Opcode 1: Scene / Chapter Transition Protocol

In the authentic binary script structure (`Eve.EMG`), Opcode 1 (`DialogPtr = 1`) with parameter `dialog1 = 3` designates a scene/chapter transition.

### 2.1 Binary Frame Schema

* `DialogPtr`: `1` (Scene Transition Command)
* `dialog1`: `3` (Sub-action: Warp to Scene Location)
* `dialog2`: `transitionType` (Unsigned 32-bit Integer matching `WarpLoc.clickID`)
* `dialog3`: `0`
* `dialog4`: `0`

### 2.2 WarpLoc Resolution Rules

1. **Target Lookup:** `transitionType` (`op.dialog2`) is matched against `map.WarpLoc[].clickID` for the active map.
2. **Generic Transitions:** When `warpEntry != null && warpEntry.mapID > 0`, the player is teleported to `(warpEntry.mapID, warpEntry.x, warpEntry.y)` using `TeleportType.CmD`.
3. **Starter Ship Prologue Exception:** On starter ships (`MapID` 10014..10018), `transitionType == 1` specifically represents the storm shipwreck sequence initiated by dialogue with Captain (`TID 14003`, Event 11). This routes to Rhode Island Beach (`Map 10035`) and flags `player.PendingBeachCutscene = true`.
4. **Cabin Traversal:** On starter ships (`MapID` 10014..10018), `transitionType == 2` represents the cabin door leading to the player's personal ship quarters (`MapID` 10024..10028). From within cabins (`MapID` 10024..10028), `transitionType == 1` leads back to the main ship deck (`MapID` 10014..10018). These traversals do not flag cutscenes and route strictly to the target coordinates.

---

## 3. Map Correlation Matrix

| Starter Ship Deck | Deck Map ID | Cabin Door WarpLoc | Cabin Map ID | Cabin Exit WarpLoc | Shipwreck Beach Map ID |
|-------------------|-------------|-------------------|--------------|-------------------|------------------------|
| Ship Variant 1    | 10014       | clickID: 2 -> 10024| 10024        | clickID: 1 -> 10014| 10034 (10035 unified)  |
| Ship Variant 2    | 10015       | clickID: 2 -> 10025| 10025        | clickID: 1 -> 10015| 10035                  |
| Ship Variant 3    | 10016       | clickID: 2 -> 10026| 10026        | clickID: 1 -> 10016| 10036 (10035 unified)  |
| Ship Variant 4    | 10017       | clickID: 2 -> 10027| 10027        | clickID: 1 -> 10017| 10037 (10035 unified)  |
| Ship Variant 5    | 10018       | clickID: 2 -> 10028| 10028        | clickID: 1 -> 10018| 10038 (10035 unified)  |

---

## 4. Storm Cutscene & Shipwreck Pipeline

Dialogue with Captain (`TID 14003`, Event 11) concludes with a two-stage cinematic shipwreck:
1. **Dialogue Completion & Opcode 8 (`dialog4 == 31488 / 0x7B00`):**
   - The player completes all dialogue lines with the Captain (TalkIDs 30124 and 30125).
   - Upon concluding the dialogue, Opcode 8 executes:
     - Sets `player.PlayingStormCutscene = true` and records `player.StormCutsceneStartTime = DateTime.UtcNow`.
     - Emits `AC 186:12` (CG Movie Trigger Cutscene #1) and `AC 20:1` (Step 3 Cinematic Event Trigger, mode 2).
     - Starts an asynchronous 4,500ms safety watchdog timer (`EveEventInterpreter.cs`).
2. **Cutscene Acknowledgment (`AC 186:9`):**
   - Client returns `AC 186:9 [01, 00]`.
   - Server responds with playback status acknowledgment `AC 186:9 [186, 9, 1, 0, 1, 0, 0, 0, 0]`.
   - Premature timers during playback are avoided; cutscene playback is allowed to run without mid-dialogue interruptions.
3. **Warp Execution:**
   - When the cutscene completes or the user clicks to skip (`AC 20:6`), the server verifies `elapsed >= 1.0s` to filter out premature frame closure ACKs.
   - Upon verified completion or 4.5s watchdog expiry:
     - Clears `PlayingStormCutscene`, sets `PendingBeachCutscene = true`, and invokes `ClearInteraction()`.
     - Emits `AC 20:7` Warp Out frame.
     - Teleports character to Rhode Island Beach (`Map 10035`, `1038, 2235`).
4. **Beach Arrival (`Map 10035`):**
   - Handled via [`AC12.Recv1`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC12.cs#L47):
     - Sets `BeachCutsceneActive = true`, `Emote = 9` (lying on sand), and sends immobilize (`AC 5:30`).
     - At 800ms, transmits `AC 20:1` Type 5 Cutscene 12008 (Beach Wake Up animation).
     - At 1,800ms, wakeup animation concludes: executes `AdvanceBeachCutscene(p, forceComplete: true)`.
     - Unfreezes mobility (`AC 20:8`, `AC 5:4`), clears emote (`Emote = 0`), and immediately initiates first-contact dialogue with Robinson (`Event 8`).

---

## 5. Edge Cases and Error Handling

* **Missing WarpLoc Definition:** If `transitionType` does not match any entry in `map.WarpLoc`, a diagnostic warning is logged (`[EveEventInterpreter] Warning: No WarpLoc found for transitionType {transitionType} on Map #{map.MapID}`), and the server dispatches `AC 20:8` to release player movement lock.
* **Debounce and Spawn Guard:** Handled in [`AC20.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC20.cs#L35-L53) with a 2,500ms cooldown and 120px spawn perimeter guard against rapid re-triggering.
