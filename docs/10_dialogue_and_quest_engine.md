# 10 - Dialogue and Quest Engine Specification

## 1. Architectural Overview

The Wonderland Online private server dialogue and quest engine orchestrates interactive NPC dialogues, multi-step branching choices, cutscene cues, and quest state progression. It bridges client-side binary asset tables ([`Talk.dat`](file:///D:/GitHub/Wonderland-Private-Server/Data/Talk.dat), [`Eve.emg`](file:///D:/GitHub/Wonderland-Private-Server/Data/Eve.emg)) with runtime player state ([`Character`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Character.cs)) and relational database persistence ([`charquest`](file:///D:/GitHub/Wonderland-Private-Server/docs/02_database_persistence_and_schema.md)).

```mermaid
flowchart TD
    Client["WLO Client"] -->|AC 20 Sub 1 (Click NPC)| AC20Handler["AC20.Recv1 Handler"]
    AC20Handler -->|QueueData.Count > 0| QueueContinuation["player.ContinueInteraction()"]
    AC20Handler -->|No Active Queue| EveInterp["EveEventInterpreter.ProcessEvent()"]
    EveInterp --> TalkLookup["TalkDatManager.GetTalk(talkId)"]
    EveInterp --> QuestCheck["Character.GetQuestState(questId)"]
    EveInterp --> WirePacket["EveEventInterpreter.BuildDialoguePacket()"]
    WirePacket -->|AC 14 / Dialogue Wire Frame| Client
    Client -->|AC 20 Sub 2 (Option Choice)| ChoiceDelegate["player.OnDialogueChoice(choice)"]
    ChoiceDelegate --> QuestUpdate["Character.SetQuestState(questId, step)"]
    QuestUpdate --> DB["SQLite: charquest Table"]
```

### Core Subsystems

| Subsystem | Source Component | Responsibilities |
| :--- | :--- | :--- |
| **Event Interpreter** | [`EveEventInterpreter`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs) | Decodes `.emg` sub-opcodes, evaluates condition blocks, executes actions, and constructs network response buffers. |
| **Dialogue String Provider** | [`TalkDatManager`](file:///D:/GitHub/Wonderland-Private-Server/Src/cGlobal.cs) | Indexes authentic `Talk.dat` binary records; resolves 24-bit talk IDs to localized ASCII/Big5 text strings. |
| **Action Code Dispatcher** | [`AC20`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC20.cs) | Decodes client interaction signals (`AC 20:1` initiate/continue, `AC 20:2` choice reply) and dispatches to player interaction queues. |
| **Quest State Repository** | [`Character.QuestList`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Character.cs) | In-memory cache of quest progress synchronized with SQLite `charquest` table (`character_id`, `quest_id`, `step`, `state`). |

---

## 2. Network Wire Protocol and Packet Framing

### 2.1 24-Bit Little-Endian TalkID Layout
Wonderland Online uses 24-bit unsigned integers for dialogue references in client packets. Previous builds suffered from byte corruption where the MSB (Byte 17) was overwritten by `subIndex`, causing dialogue text lookup failures in client memory.

#### Wire Frame Layout (Dialogue Speech Packet)
```
Byte 0..1  : Packet Header [0x44, 0x54]
Byte 2..3  : Payload Length (ushort, Little-Endian)
Byte 4..5  : Checksum / Cipher Flag
Byte 6..7  : Action Code (ushort, e.g. 0x000E for AC 14 or 0x0014 for AC 20)
...
Byte 15    : TalkID Low Byte       `talkId & 0xFF`
Byte 16    : TalkID Middle Byte    `(talkId >> 8) & 0xFF`
Byte 17    : TalkID High Byte      `(talkId >> 16) & 0xFF`
Byte 18    : Dialogue Type Flag    (0 = Bubble, 1 = Modal Dialog Window)
Byte 19    : Speaker Emotion Index (0 = Normal, 1 = Angry, 2 = Sad, 3 = Happy)
```

```csharp
// Corrected 24-bit Little-Endian Serialization
uint talkId24 = (uint)talkId;
packet[15] = (byte)(talkId24 & 0xFF);
packet[16] = (byte)((talkId24 >> 8) & 0xFF);
packet[17] = (byte)((talkId24 >> 16) & 0xFF);
```

### 2.2 Client Interaction Opcodes

| Action Code | Direction | Purpose | Description |
| :--- | :--- | :--- | :--- |
| **`AC 20 Sub 1`** | Client -> Server | Interact / Next Step | Sent when player clicks an NPC or clicks the dialog box to advance text. |
| **`AC 20 Sub 2`** | Client -> Server | Dialogue Choice | Contains `byte choice` (1-based index) when player selects an option. |
| **`AC 20 Sub 8`** | Server -> Client | Close Dialogue Window | Unlocks client camera and closes floating dialogue frames. |
| **`AC 6 Sub 2 Sub 0`** | Server -> Client | Restore UI / HUD | Re-enables minimap, inventory, and action shortcuts. |
| **`AC 5 Sub 4`** | Server -> Client | Movement Unlock | Unfreezes character movement vectors on overworld map. |

---

## 3. Dialogue Queue and Multi-Step Progression

### 3.1 Interaction Queue Mechanism
When an NPC has multiple sequential dialogue lines or conditional choices:
1. `EveEventInterpreter.ProcessEvent()` loads the matching event script from `Eve.emg`.
2. All dialogue steps up to a branching choice are staged into `player.QueueData`.
3. Step 1 is transmitted immediately via network socket.
4. When the player clicks through, the client sends `AC 20 Sub 1`.
5. [`AC20.Recv1`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC20.cs) inspects `player.QueueData`:
   - If `QueueData.Count > 0`, it calls `player.ContinueInteraction()`, dequeuing and transmitting Step 2.
   - If `QueueData` is empty, it evaluates new event triggers or clears active interaction state.

```csharp
// AC20.cs Recv1 Queue Check
if (player.QueueData.Count > 0)
{
    player.ContinueInteraction();
    return;
}
```

### 3.2 Premature Dialog Close Defect Resolution
A critical defect existed in `EveEventInterpreter.cs`:
- Dialogue sequences containing choice prompts registered `player.OnDialogueChoice = (choice) => ...`.
- At the conclusion of `RunSubOpcodes()`, the teardown check evaluated:
  ```csharp
  // DEFECTIVE IMPLEMENTATION:
  if (firstDialogSent && player.OnDialogueChoice == null)
  {
      // Keep open
  }
  else if (!interactiveSessionStarted)
  {
      // Sent AC 20:8, AC 6:2:0, AC 5:4 IMMEDIATELY on Step 1!
  }
  ```
- Because `player.OnDialogueChoice` was non-null, the check evaluated to `false`, instantly closing the dialogue window on Step 1 before the player could read text or make choices.
- **Resolution**: Teardown logic was corrected to `if (firstDialogSent)`, ensuring the dialogue session remains active whenever dialogue packets have been dispatched.

---

## 4. Case Study: South Island Event 38 (Lina & Missing Dog)

### 4.1 NPC Specification
- **NPC ID**: `27` (Lina)
- **Map ID**: `12000` (South Island / Welling Outskirts)
- **Event ID**: `38`
- **Associated Quest**: Quest Flag `13046`

### 4.2 Sequence State Machine

```
[Start Event 38]
       |
[Check Quest Flag 13046]
   |                  |
   |== Unstarted (0)  |== In-Progress (1) / Completed (2)
   |                  |
   v                  v
Talk 30601         Talk 30236 ("Did you find my puppy?")
("What shall I do?    |
 My little dog is     +--> Check Pet/Item ID (Puppy)
 missing.")                  |
   |                         |== Found: Reward EXP + Gold -> Flag 13046 = 2
Talk 30234                   |== Not Found: Keep Flag 13046 = 1
("Will you help me?")
   |
Talk 30602
("I lost him near the lake.")
   |
Question 7 Choice Prompt
   +--> Option 1 ("Yes, I will help you!"):
   |      Set Quest Flag 13046 = 1 (In-Progress)
   |      Send confirmation speech (Talk 30235)
   |      Unlock HUD and player movement
   +--> Option 2 ("Sorry, I'm busy right now."):
          Dismiss dialogue without flag update
```

---

## 5. Quest Progression and Completion Safeguards

### 5.1 Multi-Event Candidate Resolution
NPCs in Wonderland Online often register multiple event scripts in [`MapObjectEntries.Events`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/DataFiles/EveExt.cs) (e.g. NPC #12 Doll on Map 12000 has events `[14, 52, 53]`).
- **Previous Defect**: The engine prematurely broke on the first event defined in `npcEntry.Events`, ignoring whether the player had already finished the quest.
- **Resolution**: `EveEventInterpreter.ProcessEvent()` iterates through all registered events, calling `SelectMatchingBranch(player, map, clickId, candidateEvent)`. The engine activates the first event that yields an eligible branch for the player's active quest states, gracefully advancing to post-quest dialogue (e.g., Event 53 "Thank you for the Hill Pepper!") once the quest is done.

### 5.2 Completion Demotion and Turn-In Safeguards
1. **Item Condition Evaluation (`unknownbyte1 == 2`)**: When verifying item requirements for completion turn-ins, the interpreter verifies whether the completion branch target quest (`target.unknownword1` or Opcode 5 target) is already in `QuestState.Completed`. If already completed, the branch is skipped to prevent repeated reward distribution.
2. **Quest State Condition (`unknownbyte1 == 5`)**: Previously misinterpreted as a player gold check (`player.Gold >= sub.unknownword1`), `unknownbyte1 == 5` is actually the authentic binary Quest State Condition (`unknownword1` = Quest ID, `unknownword2` = Required State [1: InProgress, 2: NotStarted, 3: Completed], `unknownword3` = Required Step). Corrected to check the player's actual quest records.
3. **Opcode 5 Safeguard**: In `ExecuteOpcode` `case 5:`, incoming state updates are checked against existing quest state. An already `Completed` quest is strictly protected from being demoted back to `InProgress` (`if (player.Quests[questId].State == QuestState.Completed && state == QuestState.InProgress) return true;`).

---

## 6. NPC Spatial Movement and Wandering AI

### 6.1 Roaming Behaviors (`WalkBehavior`)
NPC roaming is governed by `entry.unknownbyte4` from `Eve.emg`:
- **Behavior 1 (Static)**: Shopkeepers, quest givers, and standing villagers strictly anchor to `(SpawnX, SpawnY)` and do not wander.
- **Behavior 2 & 5 (Scripted Waypoints)**: NPCs follow explicit coordinate waypoints (`WalkSteps`). If `WalkSteps.Count == 1`, the NPC oscillates between `(SpawnX, SpawnY)` and `WalkSteps[0]`.
- **Behavior 3 (Bounding Box Roaming)**: Farm animals (pigs, ducks, chicks) wander inside a local signed bounding box defined by `WalkSteps[0]` (`minDx, minDy`) and `WalkSteps[1]` (`maxDx, maxDy`). Coordinates are parsed as signed integers, clamped to `[-300, 300]`, and added to `(SpawnX, SpawnY)` to prevent drifting off-map or jumping to `(123, 93)`.
- **Behavior 4 / Wild Monsters**: Roam randomly within a 60px leash of `(SpawnX, SpawnY)` on outdoor wilderness maps.

---

## 7. Technical Specifications: Parameters, Returns, and Exceptions

### `EveEventInterpreter.ProcessEvent`
- **Parameters**:
  - `Player player`: Active player initiating event interaction.
  - `GameMap map`: Current map instance hosting the interaction.
  - `ushort clickId`: Map click target or NPC trigger instance.
  - `MapObjectEntries npcEntry`: Optional NPC object metadata containing template ID and registered events.
- **Returns**: `bool` indicating whether event opcodes executed successfully.
- **Exceptions Handled**:
  - `KeyNotFoundException`: Handled when event ID is not defined in map EMG; logs warning and unlocks player.
  - `IndexOutOfRangeException`: Handled when binary script pointer exceeds opcode array; safely halts event.
  - `NullReferenceException`: Handled when target NPC data is null; aborts event and restores player movement.

### `Character.SetQuestState` / `QuestManager.SavePlayerQuest`
- **Parameters**:
  - `Player player`: Character entity.
  - `uint questId`: 32-bit quest unique identifier.
  - `QuestState state`: Quest status (`NotStarted`, `InProgress`, `Completed`).
  - `byte step`: Quest step counter.
- **Returns**: `void`. Synchronously persists state to SQLite `charquest`.
- **Edge Cases**:
  - Disconnect during interaction: `QueueData` and `OnDialogueChoice` are cleaned up on connection loss to avoid dangling delegates.
