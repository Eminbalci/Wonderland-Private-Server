# Dialogue, Quest State Machine, and Cinematic Cutscene Engine

## 1. Architectural Overview

Wonderland Online integrates a modal dialogue system, an acyclic quest state machine, dynamic scene prop visibility evaluation, and cinematic timeline sequences. These components are coordinated by [`PhxTalkDat.cs`](file:///D:/GitHub/Wonderland-Private-Server/PhoenixData/DataFiles/PhxTalkDat.cs), [`AC20.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC20.cs), [`EveEventInterpreter.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs), and [`PreEventInterpreter.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/QuestRelated/PreEventInterpreter.cs).

---

## 2. Dialogue Engine Architecture

```
[Client Click NPC] ---> Dispatches AC 20:1 (clickId)
       |
       v
[EveEventInterpreter.SelectMatchingBranch] ---> Evaluates quest flags & inventory
       |
       v
[Server Emits Dialogue Frame: AC 20:1] ---> Formats 24-bit TalkID
       |
       +---> Emits AC 20:8 (Freeze Player Movement)
       |
       v
[Client Advances Frame] ---> Dispatches AC 20:6 (Step ACK)
       |
       +---> [If Player Choice Present]: Client Dispatches AC 20:9 (Choice Index)
       |
       v
[On Dialogue Completion]:
       1. Emits AC 6:2 [0] (Restores Default Entity Stance)
       2. Emits AC 20:8 (Unfreezes Player Movement)
       3. Emits AC 5:4 (Restores Walking Velocity)
```

### 2.1 Wire Packet Formats
* **Dialogue Frame (`AC 20:1` Server -> Client):**
  Serialized as: `[20, 1, 0, 0, 0, stepNum, 1, portrait, speakerClickId, 0, 1, 0, 0, 0, 0, TalkID_LSB, TalkID_MID, TalkID_MSB]`.
  - Portrait byte: `3` = NPC portrait, `7` = Player portrait (sets `speakerClickId = 0`).
  - 24-Bit TalkID: Encoded across the trailing 3 bytes to support all 17,494 records in `Talk.dat`.
* **Step ACK (`AC 20:6` Client -> Server):** Fired when the player presses Enter or clicks the conversation bubble. Invokes `Player.ContinueInteraction()`.
* **Branch Choice (`AC 20:9` Client -> Server):** Unpacks the 1-byte choice selection (`r.Unpack8()`), invoking the `player.OnDialogueChoice` delegate callback.

---

## 3. Cinematic Cutscenes & Fail-Safe Watchdog

Cinematic cutscenes coordinate synchronized camera pans, animations, and audio fanfares:
* **Rhode Island Beach Arrival Sequence ([`AC20.AdvanceBeachCutscene`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC20.cs#L166)):**
  1. *Step 1:* Initiates cutscene animation frame `12008` (player lying unconscious on the beach sand).
  2. *Step 2:* Client returns `AC 20:6` -> Server starts Quest 12040 InProgress (`AC 24:1` + `AC 20:10`).
  3. *Step 3:* Client returns `AC 20:6` -> Server plays audio fanfare (`AC 20:10`).
  4. *Step 4:* Client returns `AC 20:6` -> Server frames camera on Robinson (`AC 24:5` + `AC 20:10`).
  5. *Step 5:* Client returns `AC 20:6` -> Server executes NPC stage directions (`AC 22:12` + `AC 20:10`).
  6. *Step 6:* Client returns `AC 20:6` -> Server restores mobility (`AC 20:8`, `AC 5:4`) and initiates first-contact dialogue with Robinson.
* **Fail-Safe Watchdog:** If client network packet loss drops an intermediate `AC 20:6` ACK during a cutscene, the background watchdog triggers `forceComplete = true`, immediately advancing to Step 6 and releasing movement locks.

---

## 4. PreEvent Interpreter & Visibility Engine

[`PreEventInterpreter.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/QuestRelated/PreEventInterpreter.cs) decodes the 21-byte condition buffers and 10-byte action buffers from `Eve.emg` to dynamically reveal or conceal actors per-player.

### 4.1 Condition Buffer Binary Schema (3x 7-Byte Chunks = 21 Bytes)

```
+---------------+---------------+-----------------------------------------------+
| Byte Offset   | Field Type    | Description                                   |
+---------------+---------------+-----------------------------------------------+
| 0             | Byte          | Opcode (0x01, 0x02, 0x03, 0x05)               |
| 1..2          | UInt16 (LE)   | Target Parameter 1 (FlagID, CompanionID, Item)|
| 3..4          | UInt16 (LE)   | Target Parameter 2 (Required State / Count)   |
| 5..6          | UInt16 (LE)   | Comparison Operator (1: ==, 2: >=, 3: <=, 4:!=)|
+---------------+---------------+-----------------------------------------------+
```

* **Opcode `0x01` (Unconditional):** Evaluates unconditionally to `true`.
* **Opcode `0x02` (Companion Recruitment):** Evaluates companion status:
  - `count == 1`: Target pet must be in active party or roster.
  - `count == 2`: Target pet must NOT be recruited yet.
* **Opcode `0x03` (Step & Inventory Check):** Offset 0 verifies inventory item count against required quantity; Offset 7 verifies active quest step.
* **Opcode `0x05` (Quest Flag & Step):** Matches `flagId` against `reqState` (`1: InProgress`, `2: NotStarted`, `3: Completed`).

### 4.2 Action Buffer Binary Schema (10 Bytes)

```
+---------------+---------------+-----------------------------------------------+
| Byte Offset   | Field Type    | Description                                   |
+---------------+---------------+-----------------------------------------------+
| 0             | Byte          | Action Opcode (0x02 = Actor/Prop Control)     |
| 1..2          | UInt16 (LE)   | Target ClickID                                |
| 3..4          | UInt16 (LE)   | Action Type (2 = Conceal, 3 = Reveal, 5 = Prop|
| 5..6          | UInt16 (LE)   | Subtype / State parameter                     |
| 7             | Byte          | Padding (0x00)                                |
| 8..9          | Byte[2]       | Frame Marker (0xFF, 0xFF)                     |
+---------------+---------------+-----------------------------------------------+
```

* **Compound Rule Grouping (AND Logic):** A rule begins with an action-bearing subentry (`subentry2.Count > 0`). Subsequent subentries with zero actions (`subentry2.Count == 0`) act as chained continuation conditions evaluated via logical AND.
* **Actor Classification Tiers:**
  - *Show-Only Actors (1,229 NPCs):* All PreEvent actions are ActionType 3 (Reveal). Concealed by default until conditions evaluate to true.
  - *Dynamic Actors (569 NPCs):* Contain both ActionType 3 (Reveal) and ActionType 2 (Hide) actions. Visible by default, conditionally hidden/revealed.
  - *Hide-Only Actors (1,023 NPCs):* Contain only ActionType 2 (Hide) actions. Visible by default, hidden when conditions match.

---

## 5. Quest State Machine & Anti-Demotion Invariants

Quest states are tracked in-memory within `player.Quests` and persisted in [`character_quests`](file:///D:/GitHub/Wonderland-Private-Server/docs/02_database_schema_and_persistence_lifecycle.md#35-charquest-alias-character_quests):

```csharp
public enum QuestState : ushort
{
    InProgress = 1,
    NotStarted = 2,
    Completed  = 3
}
```

### 5.1 Monotonic Progression Invariants
1. **State Monotonicity:** A quest marked as `Completed (3)` can never be overwritten by `InProgress (1)` or `NotStarted (2)`.
2. **Completion Flag Pairs:** When a quest finishes, official WLO sets the base `questId` to `Completed (3)` and frequently sets the adjacent flag `questId + 1` to `1` (locking starter dialogues).
3. **Multi-Event Resolution:** When an NPC has multiple event branches, [`EveEventInterpreter.SelectMatchingBranch`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs) prioritizes post-completion dialogues over initial starter dialogues.
