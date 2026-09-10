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

## 5. Technical Specifications: Parameters, Returns, and Exceptions

### `EveEventInterpreter.ProcessEvent`
- **Parameters**:
  - `Character player`: Active player initiating event interaction.
  - `ushort eventId`: Authentic `Eve.emg` event identifier.
  - `int clickId`: Map click target or NPC trigger instance.
- **Returns**: `bool` indicating whether event opcodes executed successfully.
- **Exceptions Handled**:
  - `KeyNotFoundException`: Handled when event ID is not defined in map EMG; logs warning and unlocks player.
  - `IndexOutOfRangeException`: Handled when binary script pointer exceeds opcode array; safely halts event.
  - `NullReferenceException`: Handled when target NPC data is null; aborts event and restores player movement.

### `Character.SetQuestState`
- **Parameters**:
  - `ushort questId`: 16-bit quest unique identifier.
  - `byte state`: Quest status (0 = Not Started, 1 = In Progress, 2 = Completed).
  - `ushort step`: Quest step pointer for multi-stage objectives.
- **Returns**: `void`. Synchronously persists state to SQLite `charquest` via parameterized `INSERT OR REPLACE INTO charquest (character_id, quest_id, step, state) VALUES (@cid, @qid, @step, @state)`.
- **Edge Cases**:
  - Character disconnect during interaction: `QueueData` and `OnDialogueChoice` are cleaned up on `ClientDisconnected` to prevent memory leaks and dangling callback delegates.
