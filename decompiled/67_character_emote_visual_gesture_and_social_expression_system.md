# Document 67: Character Emote, Visual Gesture, and Social Expression System

## 1. Executive Summary & Engine Architecture

Social expression in Wonderland Online relies on a visual character emote and gesture engine managed through `Form_Emotion_1`. Reverse-engineered from client routines at RVAs `0x00296949`, `0x00296754`, `0x002992e4`, and `FUN_002a1ba4`, this subsystem provides 24 animated overhead emotion bubbles and character body animations.

When triggered, an emote plays a multi-frame animated visual effect in an overhead speech balloon above the player's character sprite (`Icon_Bubble_1`, `Icon_Bubble_2`), synchronizes matching audio chirps (`Sound\wav0151.wav`, `Sound\wav0152.wav`), and broadcasts the gesture to all entities within the current map quadrant via Action Code `0x05` Subcode `0x08`. The system integrates with both overworld roaming and turn-based combat phases, allowing players to express tactical reactions without breaking turn timers.

```
+------------------------------------------------------------------------------------+
|                         EMOTE SYSTEM ARCHITECTURE                                  |
+------------------------------------------------------------------------------------+
                                      |
                                      v
                      [Form_Emotion_1 Dialog (172x240 px)]
                   24 Emote Slots Arranged in a 4x6 Matrix
                                      |
         +----------------------------+----------------------------+
         | Client Visual Blitter                                   | Network Broadcast Dispatcher
         v                                                         v
  [Overhead Floating Balloon]                               [Action Code 0x05:0x08 Packet]
  - Entity Coordinate Binding                               - Actor Character GUID
  - 3-Second Lifespan (3000 ms)                             - Emote ID (101..124)
  - 3-Frame Looping Sprite Pulse                            - World Quadrant Replication
```

---

## 2. Memory Layouts & Data Structures

### 2.1 Emote Definition Table (IDs 101 through 124)
The client indexes 24 standardized emotion sprites offset by $+100$ from slot indices $1..24$:

| Emote ID | Internal Name | Description | Visual FX |
|---|---|---|---|
| `101` | `EMOTE_SMILE` | Friendly smile / grin | Yellow smiley with open mouth |
| `102` | `EMOTE_CRY` | Crying tears / sobbing | Water droplet cascading |
| `103` | `EMOTE_ANGRY` | Furious rage | Red puffing steam icons |
| `104` | `EMOTE_SWEAT` | Embarrassed / awkward | Blue sweatdrop over brow |
| `105` | `EMOTE_LAUGH` | Roaring laughter | Vibrating giggling face |
| `106` | `EMOTE_BLUSH` | Shy / blushing | Rosy cheeks with bashful gaze |
| `107` | `EMOTE_HEART` | Love / affection | Pulsing pink heart bubble |
| `108` | `EMOTE_SLEEP` | Sleeping / bored | Floating floating 'Zzz' text |
| `109` | `EMOTE_QUESTION` | Confused / inquiring | Floating animated question mark |
| `110` | `EMOTE_IDEA` | Eureka / realization | Shining electric lightbulb |
| `111` | `EMOTE_THUMBSUP` | Agreement / praise | Thumbs-up golden hand |
| `112` | `EMOTE_THUMBSDOWN`| Disapproval / rejection | Thumbs-down hand gesture |
| `113` | `EMOTE_CLAP` | Applause / cheering | Two clapping hands |
| `114` | `EMOTE_BOW` | Respectful bow / greeting | Respectful bowing silhouette |
| `115` | `EMOTE_DANCE` | Joyful dance / twirl | Musical notes hovering |
| `116` | `EMOTE_WAVE` | Friendly wave / farewell | Waving hand animation |
| `117` | `EMOTE_SIT` | Rest / sitting down | Sitting character glyph |
| `118` | `EMOTE_FAINT` | Shock / dizzy knockout | Swirling stars around head |
| `119` | `EMOTE_VICTORY` | Victory peace sign | Two-finger V hand sign |
| `120` | `EMOTE_SURRENDER` | Defeat / surrender | Waving white flag |
| `121` | `EMOTE_CHEER` | Pumped up / energetic | Flexing bicep / fist pump |
| `122` | `EMOTE_TAUNT` | Playful tease / poke | Sticking tongue out |
| `123` | `EMOTE_DESPAIR` | Total disappointment | Drooping blue ghost swirl |
| `124` | `EMOTE_SPARKLE` | Magic / sparkles | Twinkling 4-point star cluster |

### 2.2 Active Overhead Bubble State (`LocalActor + 0x2210`)

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `byte` | `IsActive` | `1` = Emote bubble currently rendering |
| `+0x01` | `byte` | `EmoteId` | Active emote identifier ($101..124$) |
| `+0x02` | `ushort` | `CurrentFrame` | Current animation sub-frame ($0..2$) |
| `+0x04` | `uint` | `SpawnTimestamp`| Tick count when emote was triggered |
| `+0x08` | `uint` | `DurationMs` | Total display duration (default `3000` ms) |

---

## 3. UI Form Geometry, Coordinates & Grid Mechanics

```
+-------------------------------------------------------+
| Form_Emotion_1: Emote Matrix (172 x 240 px)           |
+-------------------------------------------------------+
| [Header Title: Social Gestures]                   [X] |
+-------------------------------------------------------+
|  [101: Smile]  [102: Cry]    [103: Anger]  [104: Sweat] |
|  [105: Laugh]  [106: Blush]  [107: Heart]  [108: Sleep] |
|  [109: Query]  [110: Idea]   [111: Up]     [112: Down]  |
|  [113: Clap]   [114: Bow]    [115: Dance]  [116: Wave]  |
|  [117: Sit]    [118: Faint]  [119: Peace]  [120: Flag]  |
|  [121: Cheer]  [122: Taunt]  [123: Despair][124: Star]  |
+-------------------------------------------------------+
| [Close Button: Dismiss Window]                        |
+-------------------------------------------------------+
```

### 3.1 24-Slot Spatial Coordinates (`0x00296949`)
The 24 button controls ($i = 1..24$) are arranged in 4 columns and 6 rows:
- **Column Index:** $col = (i - 1) \bmod 4$
- **Row Index:** $row = \lfloor (i - 1) / 4 \rfloor$
- **Spatial Positioning:**
  $$\text{Slot X} = col \times 34 + 37\text{ px} = ((i - 1) \bmod 4) \times \text{0x22} + \text{0x25}$$
  $$\text{Slot Y} = row \times 34 + 180\text{ px} = \lfloor (i - 1) / 4 \rfloor \times \text{0x22} + \text{0xB4}$$
- **Slot Dimensions:** $32 \times 32$ pixels (`0x20` $\times$ `0x20` px).
- **Control Styling:** Sub-control `"btn_blank4"` with DirectDraw surface blitting.

---

## 4. Network Protocol, Wire Frames & Action Codes

### 4.1 Packet Framing
Emotes are transmitted using Action Code `0x05` (Player Social & Chat Engine), Subcode `0x08`:
- **Client Request (C2S):** `[0x05, 0x08, EmoteId: byte]`
- **Server Broadcast (S2C):** `[0x05, 0x08, ActorGuid: uint, EmoteId: byte]`

### 4.2 C# Packet Structure Definitions

```csharp
namespace Wonderland.Network.Packets.Social
{
    /// <summary>
    /// Client-to-server request to trigger an overhead emote.
    /// Wire Header: Action Code 0x05, Subcode 0x08.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct C2S_TriggerEmotePacket
    {
        public byte ActionCode;       // 0x05 (5)
        public byte SubCode;          // 0x08 (8)
        public byte EmoteId;          // Emote ID (101..124)
    }

    /// <summary>
    /// Server-to-client broadcast replicating character emote to nearby entities.
    /// Wire Header: Action Code 0x05, Subcode 0x08.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct S2C_BroadcastEmotePacket
    {
        public byte ActionCode;       // 0x05 (5)
        public byte SubCode;          // 0x08 (8)
        public uint ActorGuid;        // Character ID of the emitting player
        public byte EmoteId;          // Emote ID (101..124)
    }
}
```

---

## 5. Overhead Bubble Animation & Rendering Engine

When a packet is received, subroutine `FUN_002a1ba4` mounts an animated bubble above the target entity:
1. **Anchor Coordinates:**
   $$\text{Bubble X} = \text{Actor.WorldX} - 16\text{ px}$$
   $$\text{Bubble Y} = \text{Actor.WorldY} - \text{Actor.Height} - 45\text{ px}$$
2. **Animation Loop:**
   - 3 frames rendered sequentially at 200 ms intervals (0 ms, 200 ms, 400 ms), looping until the 3000 ms expiration timer fires.
   - Frame 1: Standard size ($32 \times 32$ px).
   - Frame 2: Expanded pulse ($36 \times 36$ px, centered with 2 px offset).
   - Frame 3: Settle frame ($34 \times 34$ px).
3. **Sound Effect Binding:**
   - Emotes `101..112` trigger `Sound\wav0151.wav` (High chirp).
   - Emotes `113..124` trigger `Sound\wav0152.wav` (Chime effect).

---

## 6. Private Server Implementation Guide & C# Production Code

The following service manages emote validation, cooldown throttling, and quadrant spatial broadcasting.

```csharp
using System;
using System.Collections.Concurrent;
using Wonderland.Network.Packets.Social;

namespace Wonderland.Server.Services.Social
{
    public interface IEmoteService
    {
        bool TriggerEmote(PlayerCharacter player, byte emoteId, out string failureReason);
    }

    public sealed class EmoteService : IEmoteService
    {
        private const byte MinEmoteId = 101;
        private const byte MaxEmoteId = 124;
        private const int EmoteCooldownMs = 1500; // Anti-spam throttling

        private readonly ConcurrentDictionary<uint, DateTime> _lastEmoteTime = new();

        public bool TriggerEmote(PlayerCharacter player, byte emoteId, out string failureReason)
        {
            failureReason = string.Empty;

            if (player == null) throw new ArgumentNullException(nameof(player));

            // 1. Range Validation
            if (emoteId < MinEmoteId || emoteId > MaxEmoteId)
            {
                failureReason = "Invalid emote identifier";
                return false;
            }

            // 2. Cooldown Throttling
            var now = DateTime.UtcNow;
            if (_lastEmoteTime.TryGetValue(player.CharacterId, out var lastTime))
            {
                if ((now - lastTime).TotalMilliseconds < EmoteCooldownMs)
                {
                    failureReason = "Emote rate limit exceeded";
                    return false;
                }
            }

            _lastEmoteTime[player.CharacterId] = now;

            // 3. Assemble Broadcast Packet
            var broadcastPacket = new S2C_BroadcastEmotePacket
            {
                ActionCode = 0x05,
                SubCode = 0x08,
                ActorGuid = player.CharacterId,
                EmoteId = emoteId
            };

            // 4. Replicate to all players in map quadrant / visibility range
            player.CurrentMap?.BroadcastAround(player, broadcastPacket);

            return true;
        }
    }
}
```
