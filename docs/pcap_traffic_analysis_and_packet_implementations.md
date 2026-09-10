# Official Network Traffic Analysis & Packet Implementations

## 1. Overview
This specification details the comprehensive analysis and reverse engineering of network traffic captured from official Wonderland Online client-server sessions (38 `.pcapng` files located at `C:\Users\muham\OneDrive\Masaüstü\paketler`), and the resulting implementations across the server's network action code pipeline in [`Src/Network/ActionCodes/`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/).

Each capture was dissected, decrypted (XOR key `0xAD`), and mapped into a comprehensive request/response matrix encompassing **44 distinct client requests (C->S)** and **644 server responses (S->C)**.

---

## 2. Packet Framing & Transport Architecture

All packets communicate over TCP with the following binary framing:
```
+-------------------+--------------------+-------------------+--------------------+------------------------+
| Header (2 Bytes)  | Length (2 Bytes LE)| Action Code (1 B) | Sub-Code (1 B)     | Payload (N Bytes)      |
| 0x44 0xF4         | uint16             | byte (0..255)     | byte (optional)    | Variable typed data    |
+-------------------+--------------------+-------------------+--------------------+------------------------+
```

### Unpack Pointer Semantics
When a [`RecievePacket`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Network/Packet.cs) is instantiated, `SetPtr(4)` initializes the reading pointer to index 4 (Action Code byte). Handlers that switch on `p.B` (byte index 5) must set the pointer to index 6 via `p.SetPtr(6)` before invoking sequential unpack methods (`Unpack8()`, `Unpack16()`, `Unpack32()`, `UnpackString()`), ensuring payload arguments are decoded without index shifting.

---

## 3. Dissected Packet Workflows & Implementations

### 3.1 AC 23: Inventory, Crafting & Consumables
Located in [`Src/Network/ActionCodes/AC23.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC23.cs).

#### 1. Pointer Offset & Direct Unpacking Standardization
- **Bug Fixed**: In `Recv10` (Move Item), `Recv11` (Equip Item), and `Recv12` (Unequip Item), code previously indexed `r[2]`, `r[3]`, and `r[4]`, which incorrectly accessed packet length header bytes instead of payload data.
- **Resolution**: Initialized `p.SetPtr(6)` in `ProcessPkt` and converted all handlers to sequential unpack calls:
  - `Recv10`: `byte src = r.Unpack8(); byte ammt = r.Unpack8(); byte dst = r.Unpack8();`
  - `Recv11`: `byte loc = r.Unpack8();`
  - `Recv12`: `byte loc = r.Unpack8(); byte dst = r.Unpack8();`

#### 2. AC 23:14 - Compound Crafting (Simya / Item Synthesis)
Confirmed in `yerdenitemalipcompounddaikiitemikaristirdim.pcapng` (Frame 68–76, 256 packets):
- **Client Request (`C->S`)**:
  - Format: `17 0e <count(1B)> <slot1(1B)> <slot2(1B)>`
  - Example: `17 0e 02 13 12` (Compound 2 items located at slots 19 and 18).
- **Server Response Pipeline (`S->C`)**:
  1. `AC 23:9` (x2): `17 09 <slot> <amount>` - Deducts 1 unit from both ingredient slots.
  2. Recipe Lookup: Queries [`Game.Crafting.AlchemyManager.FindRecipe(id1, id2)`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Crafting/AlchemyManager.cs). If no recipe matches, generates charcoal / ash (`#27008`) or ranks material.
  3. Item Placement: Places output item into `targetSlot = Math.Min(slot1, slot2)` via `p.Inv.AddItem(resultItem, targetSlot, false)`.
  4. `AC 23:8`: `17 08 <targetSlot(1B)> <resultItemId(2B LE)> <count(1B)> <28B zeros>` - Confirms item addition in client UI.
  5. `AC 23:13`: `17 0d <resultItemId(2B LE)> <count(1B)> <targetSlot(1B)>` - Displays the official compound success dialog and result item popup.
  6. `AC 23:122`: `17 7a <charId(4B LE)>` - Broadcasts synthesis light effect to all nearby players on map.

#### 3. AC 23:15 - Quick HP/MP Consumable Refill & Tent
Confirmed in `hpmpdoldurmabutonu.pcapng` (Frame 268–281, 104 packets):
- **Client Request (`C->S`)**:
  - Format: `17 0f <slot(1B)> <count(1B)> <target(2B LE)>`
  - Target Parameter:
    - `0x0000` = Player Character
    - `> 0` = Companion / Pet Slot index (1..4 in party)
- **Server Response Pipeline (`S->C`)**:
  1. Tent Check: If item is Tent (`#36002`), calls `p.Tent.Open()`.
  2. Health & Mana Gain: Computes status gains from `ItemDat` attributes (types 207 for HP, 208 for SP) with fallback mappings for food/potions/syrups.
  3. Target Application:
     - Player: Updates `p.Eqs.CurHP` and `p.Eqs.CurSP`, syncs via `p.Eqs.Send8_1()`, sends `AC 5:1 [05 01 <charId> <hp>]`.
     - Pet: Updates `pet.HP` and `pet.SP`, syncs via `AC 8:2` (`statId 25` for CurHP, `statId 26` for CurSP), sends `AC 5:1 [05 01 <petId> <hp>]`.
  4. Remaining Stack Update (`AC 23:208`):
     - If stack remains: `17 d0 01 <slot> <remainingCount> 00 00 00`.
     - If stack depleted: `17 09 <slot> <count>` (`AC 23:9`).
  5. Persistence: Triggers `p.SaveCharacterData()`.

---

### 3.2 AC 14: Social, Friends & In-Game Mail
Located in [`Src/Network/ActionCodes/AC14.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC14.cs).

#### 1. AC 14:1 - In-Game Mail & Direct Delivery
Confirmed in `mailyolladimreplyyaptim.pcapng`:
- **Client Request (`C->S`)**:
  - Format: `0e 01 <mailType(1B)> <targetCharId(4B LE)> <mailText>`
  - Example: `0e 01 01 a1 e5 03 00 6d 61 69 6c 79 6f 6c 6c 61 64 69 6d` ("mailyolladim" to CharID 255393).
- **Server Responses (`S->C`)**:
  1. `AC 14:9`: `0e 09 <targetCharId(4B LE)> 00` - Mail Sent ACK sent to the sender.
  2. `AC 14:1`: `0e 01 <senderCharId(4B LE)> <timestamp(8B OLE Date)> <mailText>` - Live delivery to recipient if online.
  3. Database Storage: Persists to `Mail` SQLite table.

#### 2. AC 14:2 - Add Friend Request Send / List Request
Confirmed in `arkadaseklemeveonlinegozukme.pcapng`:
- **Client Request (`C->S`)**: `0e 02 <targetCharId(4B LE)>`
- **Bug Fixed**: Removed spurious `Unpack8()` flag read that shifted CharID by 1 byte.
- **Server Response (`S->C`)**:
  - If `targetCharId == 0`: Dispatches complete friend roster via `SendFriendList(p)`.
  - If `targetCharId > 0`: Forwards request to target player via `0e 02 <requesterCharId(4B LE)>`, triggering the client friend confirmation modal.

#### 3. AC 14:3 - Friend Request Acceptance
Confirmed in `arkadaseklemeveonlinegozukme.pcapng`:
- **Client Request (`C->S`)**: `0e 03 <requesterCharId(4B LE)> <group(1B)>`
- **Server Response (`S->C`)**:
  1. Database: Inserts into `Friends` table with symmetric ordering (`Math.Min`, `Math.Max`).
  2. Notifications: Emits `0e 03 <otherCharId(4B LE)> <group(1B)>` to both requester and accepter.
  3. Refresh: Dispatches `SendFriendList` to both participants.

#### 4. AC 14:4 - Friend Removal
Confirmed in `arkadassilme.pcapng`:
- **Client Request (`C->S`)**: `0e 04 <friendCharId(4B LE)>`
- **Server Response (`S->C`)**: Deletes entry from `Friends` database, auto-refreshes friend list for requester, and notifies ex-friend if online.

---

### 3.3 AC 27: Ground Item Discard & Trash Bin
Located in [`Src/Network/ActionCodes/AC27.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC27.cs).

Confirmed in `shoplarincalismamantigi.pcapng`:
- **Client Request (`C->S`)**:
  - Format: `1b 02 <slot(1B)> <count(1B)>`
  - Example: `1b 02 18 01` (Discard 1 item from inventory slot 24).
- **Server Responses (`S->C`)**:
  1. `AC 23:9`: `17 09 <slot> <count>` - Item removed from inventory.
  2. `AC 27:2`: `1b 02 00` - Discard ACK.
  3. `AC 27:3`: `1b 03` - Status update.
  4. `AC 27:4`: `1b 04` - Final completion signal.

---

### 3.4 AC 183: Heartbeat & Client Liveness Sync
Located in [`Src/Network/ActionCodes/AC183.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC183.cs).

Confirmed across official captures (e.g., Frame 1443, Frame 661, Frame 879):
- **Client Request (`C->S`)**: `b7 11 00` (`AC 183:17`)
- **Server Responses (`S->C`)**:
  1. `AC 183:17`: `b7 11 00` (`[183, 17, 0]`) - Ping ACK.
  2. `AC 183:11`: `b7 0b 09 02` (`[183, 11, 9, 2]`) - Periodic system status synchronization token.

---

---

### 3.5 AC 32: Visual Emote/Action & Window Close Reset
Located in [`Src/Network/ActionCodes/AC32.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC32.cs).

Confirmed across 10 official captures (`brelliatlayerdegistirdim`, `digersandiklaritoplama`, `shoplarincalismamantigi`, `robinsonlakonusma`, etc.):
- **Client Request (`C->S`)**:
  - `20 01 <emote>`: Plays visual emotion bubble.
  - `20 02 <action>`: Enters player action state (sit down `0x0f`, wave `0x08`, sleep, etc.).
  - `20 03`: Stop Action / Dialogue Close / Return to Idle stance.
- **Implementation**:
  - Initialized `p.SetPtr(6)` in `ProcessPkt`.
  - Added `Recv3` for Subcode 3 (`20 03`): Resets `p.Emote = 0`, broadcasts `[32, 2, charId, 0]` to map observers so the player returns to idle standing posture, and dismisses active interaction state via `p.ClearInteraction()`.

---

### 3.6 AC 33: Settings, Privacy & Team Auto-Follow Toggle
Located in [`Src/Network/ActionCodes/AC33.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC33.cs).

Confirmed in `partyegiripharitadegistirdim.pcapng` (Frames 228 & 369):
- **Client Request (`C->S`)**:
  - `21 01 <settingType>`: Toggles PK, Joinable, Tradable, or Channel settings.
  - `21 02`: Current settings request.
  - `21 05 <val>`: Team Follow / Walk-along setting toggle (`21 05 01`).
- **Implementation**:
  - Initialized `r.SetPtr(6)` in `ProcessPkt` to fix setting type unpacking.
  - Added `case 5` and `HandleTeamFollow`: reads toggle value, acknowledges status with `[33, 5, followVal]`.

---

### 3.7 AC 13: Team Requests, Joining, & Leadership Synchronization
Located in [`Src/Network/ActionCodes/AC13.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC13.cs).

Confirmed in `partyegiripharitadegistirdim.pcapng`:
- **Client Requests (`C->S`)**:
  - `0d 01 <targetCharId(4B LE)>`: Invite / Join Request.
  - `0d 03 <reply(1B)> <requesterCharId(4B LE)>`: Invite Response / Accept.
  - `0d 04`: Leave Team.
  - `0d 09 <targetCharId(4B LE)>`: Kick Member.
  - `0d 0a <newLeaderId(4B LE)>`: Transfer Leadership.
- **Implementation**:
  - Set `r.SetPtr(6)` in `ProcessPkt` to align with the standard packet layout.
  - Normalized ID extraction across `Recv1`, `Recv3`, `Recv9`, and `Recv10`, supporting both raw 32-bit `CharID` and legacy composite encodings seamlessly with robust fallback matching.

---

### 3.8 AC 186: Cutscene & CG Animation Handshake
Located in [`Src/Network/ActionCodes/AC186.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC186.cs).

Confirmed across 22 official captures (141 instances of `ba 09 01 00`):
- **Client Request (`C->S`)**:
  - `ba 09 <cutsceneId(2B LE)>`: Cutscene playback acknowledgment.
- **Server Responses (`S->C`)**:
  1. `AC 186:9`: `ba 09 <cutsceneId(2B LE)> 01 00 00 00 00` (Playback confirmation & active state).
  2. `AC 186:12`: `ba 0c <cutsceneId(2B LE)> 00 00 00` (Frame 469/470/492: Cutscene completion and camera control release).

---

## 4. 44-Request Completeness Matrix

All 44 distinct client requests identified from the 38 official captures are fully handled:

| Action Code | Subcode | Description | Handling Status |
|:---:|:---:|:---|:---|
| **0** | `-1` | TCP Keep-Alive / Heartbeat Ping | Handled in [`Player.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Player.cs) |
| **5** | `7` | Character Attribute / Status Query | Handled in [`AC05.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC05.cs) |
| **6** | `1` | Map Navigation / Player Walk Path | Handled in [`AC06.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC06.cs) |
| **9** | `1` | Character Creation Request | Handled in [`AC09.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC09.cs) |
| **9** | `2` | Character Name Availability Check | Handled in [`AC09.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC09.cs) |
| **12** | `1` | Map Warping / Ready Acknowledgment | Handled in [`AC12.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC12.cs) |
| **13** | `1` | Team Invite / Join Request | Handled in [`AC13.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC13.cs) |
| **13** | `3` | Team Accept / Join Response | Handled in [`AC13.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC13.cs) |
| **14** | `1` | Mail Delivery Send | Handled in [`AC14.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC14.cs) |
| **14** | `2` | Friend Request / Roster Query | Handled in [`AC14.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC14.cs) |
| **14** | `3` | Friend Request Accept | Handled in [`AC14.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC14.cs) |
| **14** | `4` | Friend Removal | Handled in [`AC14.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC14.cs) |
| **15** | `7` | Vehicle Board Confirmation | Handled in [`AC15.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC15.cs) |
| **15** | `10` | Vehicle Dismount & Shore Landing | Handled in [`AC15.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC15.cs) |
| **15** | `13` | Dismount Acknowledgment | Handled in [`AC15.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC15.cs) |
| **15** | `14` | Board Raft / Vehicle on Water | Handled in [`AC15.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC15.cs) |
| **20** | `1` | NPC Dialogue Initiation | Handled in [`AC20.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC20.cs) |
| **20** | `6` | NPC Dialogue Advance / Next | Handled in [`AC20.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC20.cs) |
| **20** | `8` | NPC Option Selection / Portal Step | Handled in [`AC20.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC20.cs) |
| **20** | `9` | NPC Menu / Service Select | Handled in [`AC20.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC20.cs) |
| **23** | `2` | Ground Item Pickup | Handled in [`AC23.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC23.cs) |
| **23** | `10` | Move / Split Inventory Item | Handled in [`AC23.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC23.cs) |
| **23** | `14` | Compound Item Crafting / Alchemy | Handled in [`AC23.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC23.cs) |
| **23** | `15` | Quick HP/SP Recovery & Tent | Handled in [`AC23.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC23.cs) |
| **23** | `54` | Tent / Furniture Status Query | Handled in [`AC23.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC23.cs) |
| **23** | `77` | Vehicle / Mount Status Query | Handled in [`AC23.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC23.cs) |
| **27** | `2` | Item Discard / Trash Disposal | Handled in [`AC27.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC27.cs) |
| **32** | `2` | Action / Emote Performance | Handled in [`AC32.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC32.cs) |
| **32** | `3` | Emote Stop / Dialogue Window Close | Handled in [`AC32.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC32.cs) |
| **33** | `5` | Team Auto-Follow Setting Toggle | Handled in [`AC33.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC33.cs) |
| **34** | `1` | Cart Balance Query & Checkout | Handled in [`AC34.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC34.cs) |
| **35** | `2` | Character Deletion | Handled in [`AC35.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC35.cs) |
| **39** | `19` | Quest Tracker / Guild Status Query | Handled in [`AC39.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC39.cs) |
| **50** | `1` | Combat Turn Command / Skill / Attack | Handled in [`AC50.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC50.cs) |
| **57** | `1` | Minigame Outcome Report (Win/Loss) | Handled in [`AC57.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC57.cs) |
| **63** | `2` | Client Version Handshake | Handled in [`AC63.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC63.cs) |
| **63** | `4` | Account Authentication Login | Handled in [`AC63.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC63.cs) |
| **75** | `4` | Item Mall Category Switch & Buy | Handled in [`AC75.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC75.cs) |
| **89** | `0` | Client Scene Ready Keep-Alive | Handled in [`AC89.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC89.cs) |
| **91** | `1` | Lucky Draw / Roulette Spin | Handled in [`AC91.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC91.cs) |
| **92** | `1` | Map Scene Ready Ping | Handled in [`AC92.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC92.cs) |
| **104** | `1` | Daily Lucky Wheel Spin Request | Handled in [`AC104.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC104.cs) |
| **183** | `17` | Liveness Ping / Heartbeat | Handled in [`AC183.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC183.cs) |
| **186** | `9` | Cutscene / CG Animation Sync | Handled in [`AC186.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC186.cs) |

---

## 5. Verification & Build Integrity
The complete codebase builds cleanly with zero compilation errors:
- **Build Target**: `Wonderland Private Server.sln`
- **Output Assembly**: `bin/Debug/Wonderland Private Server.exe`
- **Result**: `0 Hata` (0 Errors).

