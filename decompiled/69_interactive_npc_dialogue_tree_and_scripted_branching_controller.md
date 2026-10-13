# Document 69: Interactive NPC Dialogue Tree and Scripted Branching Controller

## 1. Executive Summary & Engine Architecture

Narrative exposition, quest assignment, and transactional interactions in Wonderland Online are executed through the NPC dialogue controller subsystem. Reverse-engineered from client routines `FUN_00490820`, `FUN_00491980`, `FUN_00440280`, and `FUN_00440440`, this engine drives three specialized dialogue forms:
1. **`Form_Talk_1` ($768 \times 169$ px):** Primary full-width bottom narrative dock for standard story dialogues, quest dialogues, and NPC expositions.
2. **`Form_Talk_2` ($768 \times 169$ px):** Alternate high-contrast cutscene skin featuring cinematic letterboxing.
3. **`Form_Talk_3` ($332 \times 168$ px):** Branching choice decision box positioned above the primary dialogue dock, displaying 1 to 4 selectable branching responses.

The dialogue engine decodes server-pushed rich text containing formatting tags (`#R`, `#B`, `%s`), synchronizes animated NPC facial portraits with blinking and lip-sync pacing via `FUN_00491980`, renders a flashing page advance prompt (`"Icon_Nextone_1"`), and handles option selection dispatching via Action Code `0x06` (`6` decimal).

```
+------------------------------------------------------------------------------------+
|                         NPC DIALOGUE TREE ENGINE ARCHITECTURE                      |
+------------------------------------------------------------------------------------+
                                      |
         +----------------------------+----------------------------+
         | Narrative Window: Form_Talk_1                           | Choice Window: Form_Talk_3
         v                                                         v
  [Full-Width Bottom Dock] (768x169 px)                     [Branching Decision Box] (332x168 px)
  - Animated NPC Portrait (100x100 px)                      - Up to 4 Scripted Options
  - Rich Text Parser (#R, #B, %s)                           - Highlight Hover Selection
  - Paged Text & Blinking Advance Arrow                     - Decision Mutex Locks
         |                                                         |
         +----------------------------+----------------------------+
                                      |
                                      v
                 [Network Dispatcher: Action Code 0x06 (6)]
         Subcodes: 0x01 (Start), 0x02 (Select Option), 0x04 (Close)
```

---

## 2. Memory Layouts & Data Structures

### 2.1 Dialogue State Registers (`PTR_DAT_004c8850`)
The global dialogue manager instance at `PTR_DAT_004c8850` tracks conversation state:

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `void*` | `VTable` | Delphi component virtual method table |
| `+0x4C` | `uint` | `FormHandle` | DirectDraw surface bitmap handle |
| `+0x294`| `uint` | `PacingSpeed` | Text typewriter render rate in ms per character (default `0x2D` = 45 ms) |
| `+0x30C`| `int*` | `pPortraitContext`| Active NPC portrait animation descriptor |
| `+0x319`| `byte` | `HasChoices` | `1` = Branching choices active in `Form_Talk_3` |
| `+0x31A`| `byte` | `IsTextComplete`| `1` = Current typewriter text page fully displayed |
| `+0x31B`| `byte` | `ChoicePanelOpen`| Visibility state of `Form_Talk_3` |
| `+0x38C`| `byte` | `SkinMode` | `1` = `Form_Talk_1` (Standard), `2` = `Form_Talk_2` (Cinematic) |
| `+0x3A0`| `uint` | `ActiveNpcId` | Master NPC ID of the speaking entity |

### 2.2 Branching Choice Record (`0x44` = 68 Bytes)
When `Form_Talk_3` is populated, choices are structured as:

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `byte` | `OptionIndex` | 1-indexed branch identifier ($1..4$) |
| `+0x01` | `char[60]` | `OptionText` | Display string of the player's dialogue option |
| `+0x3D` | `uint` | `NextScriptNode`| Target script node jumped to upon selection |

---

## 3. UI Form Geometry, Coordinates & Controls

### 3.1 `Form_Talk_1` & `Form_Talk_2` (Primary Narrative Dock)
- **Dimensions:** Width `0x300` (768 pixels), Height `0xA9` (169 pixels).
- **Dock Coordinates:** $X = \text{0x23}$ (35 pixels), $Y = \text{ScreenHeight} - 175$ pixels.
- **Sub-Components:**
  - **NPC Portrait Box:** Anchored at $(X=45, Y=45)$ relative to dock, size $100 \times 100$ px.
  - **Speaker Nameplate Banner:** Positioned at $(X=160, Y=25)$ px.
  - **Narrative Text Body (`"panel22"`):** Positioned at $(X=160, Y=50)$ px, bounding box $550 \times 95$ px.
  - **Blinking Advance Arrow (`"Icon_Nextone_1"`):** Positioned at $(X=710, Y=130)$ px.

### 3.2 `Form_Talk_3` (Branching Decision Selector)
- **Dimensions:** Width `0x14C` (332 pixels), Height `0xA8` (168 pixels).
- **Dock Coordinates:** Anchored at $X = \text{0xFA}$ (250 pixels), positioned vertically immediately above the narrative dock.
- **4 Selectable Rows:** Spaced vertically at 28 px stride ($Y = 20 + (i - 1) \times 28$ px for $i = 1..4$).

```
+---------------------------------------------------------------------------------+
|                       Form_Talk_3: Branching Options (332 x 168 px)             |
|                       +-------------------------------------------------------+ |
|                       | [1] I accept the quest to slay the forest beast.       | |
|                       | [2] Tell me more about the mysterious relic first.    | |
|                       | [3] I must decline for now. Goodbye.                  | |
|                       +-------------------------------------------------------+ |
+---------------------------------------------------------------------------------+
| Form_Talk_1: Narrative Dock (768 x 169 px)                                      |
+---------------------------------------------------------------------------------+
| +-----------+  Speaker: Village Elder                                           |
| |  [NPC]    |  "Greetings, brave traveler! A dark omen has fallen upon our     |
| |  Portrait |  peaceful village. #RThe ancient seal has shattered/#R!         |
| | (100x100) |  Will you lend us your strength?"                                 |
| +-----------+                                             [Icon_Nextone_1: >>]  |
+---------------------------------------------------------------------------------+
```

---

## 4. Network Protocol, Wire Frames & Action Codes

### 4.1 Packet Framing
All NPC dialogue interactions use Action Code `0x06` (`6` decimal):
- **Opcode:** `0x06`
- **Subcodes:**
  - `0x01`: Dialogue Script Frame Push (S2C: NPC ID, Portrait ID, Name, Body Text, Choice Count)
  - `0x02`: Branching Choice Selected (C2S via `FUN_00440280`: Option Index $1..4$)
  - `0x03`: Advance to Next Dialogue Page (C2S)
  - `0x04`: Dialogue Closed / Finished (C2S / S2C)

### 4.2 C# Packet Structure Definitions

```csharp
namespace Wonderland.Network.Packets.Dialogue
{
    /// <summary>
    /// Server-to-client packet delivering a narrative dialogue frame with text and optional choices.
    /// Wire Header: Action Code 0x06, Subcode 0x01.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct S2C_DialogueFramePacket
    {
        public byte ActionCode;       // 0x06 (6)
        public byte SubCode;          // 0x01
        public ushort NpcId;          // Master NPC identifier
        public ushort PortraitId;     // Facial portrait asset ID
        public byte ChoiceCount;      // 0 = Linear narrative, 1..4 = Branching options
        // Trailing variable-length strings: SpeakerName\0, NarrativeText\0, Option1\0, Option2\0...
    }

    /// <summary>
    /// Client-to-server response transmitting the player's selected dialogue branch.
    /// Wire Header: Action Code 0x06, Subcode 0x02.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct C2S_DialogueChoicePacket
    {
        public byte ActionCode;       // 0x06 (6)
        public byte SubCode;          // 0x02
        public ushort NpcId;          // Active speaking NPC ID
        public byte SelectedChoice;   // 1-indexed choice index (1..4)
    }

    /// <summary>
    /// Client-to-server request to advance paged narrative or close the dialogue.
    /// Wire Header: Action Code 0x06, Subcode 0x03 or 0x04.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct C2S_DialogueAdvancePacket
    {
        public byte ActionCode;       // 0x06 (6)
        public byte SubCode;          // 0x03 (Page Next) or 0x04 (Close)
        public ushort NpcId;          // Active speaking NPC ID
    }
}
```

---

## 5. Rich Text Formatting & Portrait Animation Engine

### 5.1 Tag Parsing Pipeline
The narrative string renderer decodes the following control sequences:
- `#R` ... `#R/`: Sets foreground text color to crimson red (`RGB(220, 40, 40)`), used for quest objectives and warnings.
- `#B` ... `#B/`: Enables bold dropshadow rendering with double glyph offsetting.
- `%s`: Replaced with character context variables:
  - `%s[0]`: Player Character Name
  - `%s[1]`: Active Companion Pet Name
  - `%s[2]`: Guild Nameplate
- `\n`: Forced carriage return and line wrap.

### 5.2 Portrait Animation Kinematics (`FUN_00491980`)
Subroutine `FUN_00491980` drives the animated portrait box:
- Blinking: Every 3.5 to 5.0 seconds (randomized interval), eyes close for 120 ms before reopening.
- Talking: While text typewriter rendering is active (`IsTextComplete == 0`), mouth alternates between closed, half-open, and open frames at 100 ms intervals. When typewriter finishes, mouth returns to idle resting frame.

---

## 6. Private Server Implementation Guide & C# Production Code

The following service manages dialogue tree progression, script node jumps, choice handling, and rich text payload assembly.

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Wonderland.Network.Packets.Dialogue;

namespace Wonderland.Server.Services.Dialogue
{
    public sealed class DialogueNode
    {
        public int NodeId { get; init; }
        public ushort NpcId { get; init; }
        public ushort PortraitId { get; init; }
        public string SpeakerName { get; init; } = string.Empty;
        public string Text { get; init; } = string.Empty;
        public List<(string OptionText, int TargetNodeId, Action<PlayerCharacter>? OnSelect)> Options { get; } = new();
    }

    public interface IDialogueService
    {
        void StartDialogue(PlayerCharacter player, int startNodeId);
        void HandleChoice(PlayerCharacter player, C2S_DialogueChoicePacket packet);
        void AdvanceOrClose(PlayerCharacter player, bool isAdvance);
    }

    public sealed class DialogueService : IDialogueService
    {
        private readonly Dictionary<int, DialogueNode> _scriptNodes = new();
        private readonly ConcurrentDictionary<uint, int> _activeConversations = new();

        public void RegisterNode(DialogueNode node)
        {
            if (node != null) _scriptNodes[node.NodeId] = node;
        }

        public void StartDialogue(PlayerCharacter player, int startNodeId)
        {
            if (player == null || !_scriptNodes.TryGetValue(startNodeId, out var node)) return;

            _activeConversations[player.CharacterId] = startNodeId;
            SendDialogueFrame(player, node);
        }

        public void HandleChoice(PlayerCharacter player, C2S_DialogueChoicePacket packet)
        {
            if (player == null) return;

            if (!_activeConversations.TryGetValue(player.CharacterId, out int currentNodeId)) return;
            if (!_scriptNodes.TryGetValue(currentNodeId, out var node)) return;

            int choiceIndex = packet.SelectedChoice - 1;
            if (choiceIndex >= 0 && choiceIndex < node.Options.Count)
            {
                var option = node.Options[choiceIndex];
                
                // Execute optional callback (grant quest, give item, etc.)
                option.OnSelect?.Invoke(player);

                if (option.TargetNodeId > 0 && _scriptNodes.TryGetValue(option.TargetNodeId, out var nextNode))
                {
                    _activeConversations[player.CharacterId] = option.TargetNodeId;
                    SendDialogueFrame(player, nextNode);
                }
                else
                {
                    // End conversation
                    _activeConversations.TryRemove(player.CharacterId, out _);
                    player.Send(new S2C_DialogueClosePacket { ActionCode = 0x06, SubCode = 0x04 });
                }
            }
        }

        public void AdvanceOrClose(PlayerCharacter player, bool isAdvance)
        {
            if (player == null) return;

            if (!isAdvance)
            {
                _activeConversations.TryRemove(player.CharacterId, out _);
                player.Send(new S2C_DialogueClosePacket { ActionCode = 0x06, SubCode = 0x04 });
            }
        }

        private void SendDialogueFrame(PlayerCharacter player, DialogueNode node)
        {
            // Format dynamic tokens
            string formattedText = node.Text.Replace("%s", player.Name);

            // Assemble frame packet
            var packet = new S2C_DialogueFramePacket
            {
                ActionCode = 0x06,
                SubCode = 0x01,
                NpcId = node.NpcId,
                PortraitId = node.PortraitId,
                ChoiceCount = (byte)node.Options.Count
            };

            // Stream trailing strings
            player.SendDialogue(packet, node.SpeakerName, formattedText, node.Options);
        }
    }
}
```
