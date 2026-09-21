# Network Protocol and Action Code Specification

## 1. Architectural Overview

Wonderland Online uses a proprietary binary packet framing protocol running over TCP. Packets feature a fixed 4-byte header, symmetric XOR encryption, and a decoupled action code dispatch system mapping Action Codes (AC 0 to AC 226) to dedicated handlers.

---

## 2. Wire Framing & Packet Structure

Every network frame conforms to the following binary specification:

```
+---------------+---------------+-----------------------------------------------+
| Byte Offset   | Field Type    | Description                                   |
+---------------+---------------+-----------------------------------------------+
| 0x00..0x01    | UInt16 (LE)   | Magic Header: 0x44F4 (0xF4, 0x44; Dec: 17652) |
| 0x02..0x03    | UInt16 (LE)   | Payload Length (Total Wire Bytes - 4)         |
| 0x04          | Byte          | Action Code (AC) Identifier                   |
| 0x05          | Byte          | Sub-Action Code (SubCode / SubAction)         |
| 0x06..End     | Bytes         | Variable Payload Data                         |
+---------------+---------------+-----------------------------------------------+
```

### 2.1 Low-Level Packet Processing ([`IncomingPacket.cs`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/RCLibrary.Core.Networking/IncomingPacket.cs))
1. **Header Validation:** Bytes are accumulated until the stream length reaches at least 4 bytes. The header is decrypted using symmetric key `0xAD` (decimal 173). If `Unpack16() != 17652 (0x44F4)`, the frame is deemed corrupt and the connection is dropped.
2. **Payload Sizing:** The length field at offset 2 defines expected bytes: `m_nExpecting = Unpack16() + 4`.
3. **Decryption:** Once all expecting bytes are received, the remaining payload (offset 4 to end) is decrypted using XOR `0xAD`, and `m_bReady` is set to `true`.
4. **Header Protection:** The 4-byte wire header is sent in plaintext on the wire; only the packet payload is encrypted via `SendPacket.ApplyXorEncryption(0xAD)`.

---

## 3. Serialization Engine ([`Tools.FromFormat`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Generics/Tools.cs))

The server utilizes a compact format token string parser to serialize little-endian binary buffers:

```
+-------+---------------+-----------------------+-----------------------------------------------+
| Token | Wire Type     | Byte Size             | Description / Behavior                        |
+-------+---------------+-----------------------+-----------------------------------------------+
| 'B','b'| Byte         | 1 Byte                | Unsigned 8-bit integer (0..255)               |
| 'W','w'| UInt16       | 2 Bytes (LE)          | Unsigned 16-bit integer (0..65535)            |
| 'D','d'| UInt32 / Int32| 4 Bytes (LE)         | 32-bit integer                                |
| 'L','l'| UInt64 / Int64| 8 Bytes (LE)         | 64-bit integer                                |
| 's','S'| String       | 1 + N Bytes           | 1-byte length prefix followed by ASCII string |
+-------+---------------+-----------------------+-----------------------------------------------+
```

> [!WARNING]
> **Overflow Safety:** Supplying values exceeding 255 (such as 32-bit `CharID`) to format token `'b'` or `'B'` causes a fatal `System.OverflowException`. In high-ID fields (e.g., `AC 11:4` crossed-swords and `AC 14:5` friend rosters), strongly-typed serialization via `SendPacket.Pack32(uint)` is strictly enforced.

---

## 4. Action Code Catalog (AC 0 to AC 226)

All inbound client packets are dispatched by [`Network.ActionCodes.AC`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Network/ActionCodes/AC.cs) to specialized handlers in [`Src/Network/ActionCodes`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes):

```
+---------------+-----------------------+-------------------------------------------------------+
| Action Code   | Implementation File   | Functional Responsibility                             |
+---------------+-----------------------+-------------------------------------------------------+
| AC 0          | AC00.cs               | Keep-alive ping and latency measurement               |
| AC 1          | AC01.cs               | Client session authentication handshake (Sub 1)       |
| AC 2          | AC02.cs               | Public chat, whispers, and GM command parser          |
| AC 3          | AC03.cs               | Initial character appearance broadcast                |
| AC 4          | AC04.cs               | Overworld peer player entity replication              |
| AC 5          | AC05.cs               | Character attributes, walking speed (5:4), hotbars    |
| AC 6          | AC06.cs               | 8-compass entity orientation and sitting stance       |
| AC 7          | AC07.cs               | Social animations (wave, cry, laugh, bow)             |
| AC 8          | AC08.cs               | Equipment system, durability decay, pet stat sync (8:2)|
| AC 9          | AC09.cs               | Character creation intake                             |
| AC 10         | AC10.cs               | Social relations and online presence (10:3)           |
| AC 11         | AC11.cs               | Combat battlefield engine (11:0, 11:1, 11:4, 11:5)    |
| AC 12         | AC12.cs               | Tent housing deployment and interior instances        |
| AC 13         | AC13.cs               | 1-on-1 player trade transactions                      |
| AC 14         | AC14.cs               | Friend system rosters (14:5) and status tabs (14:11)  |
| AC 15         | AC15.cs               | Vehicles, mounts, raft wreck (15:14), followers (15:4)|
| AC 16         | AC16.cs               | Player party / team formation and invitation          |
| AC 18         | AC18.cs               | Player Killing (PK) and open-world duel challenges    |
| AC 19         | AC19.cs               | Companion pets active battle (19:4) and standby (19:2)|
| AC 20         | AC20.cs               | Dialogue engine (20:1), cutscenes, mobility locks     |
| AC 21         | AC21.cs               | Map portals and warp triggers                         |
| AC 22         | AC22.cs               | Scene entities, 14-byte scene table, roaming (22:2)   |
| AC 23         | AC23.cs               | 31-byte inventory bag, ground pickups (23:2), banners |
| AC 24         | AC24.cs               | Quest notebook UI synchronization and client settings |
| AC 25         | AC25.cs               | System broadcast announcements and modal dialogues    |
| AC 26         | AC26.cs               | Currency wallet and gold balance                      |
| AC 27         | AC27.cs               | Player overhead achievement titles                    |
| AC 28         | AC28.cs               | Production crafting tables (workbench, sewing machine)|
| AC 29         | AC29.cs               | Alchemy synthesis and item compounding                |
| AC 30         | AC30.cs               | Tent storage container chests                         |
| AC 31         | AC31.cs               | Pet hotel and companion paddock storage               |
| AC 32         | AC32.cs               | Vehicle garage docking and repair                     |
| AC 33         | AC33.cs               | Resource gathering nodes and fishing rod timers       |
| AC 34         | AC34.cs               | Player merchant kiosk stalls                          |
| AC 35         | AC35.cs               | Item Mall currency and token balance                  |
| AC 36         | AC36.cs               | Blacksmith weapon/armor refinement                    |
| AC 37         | AC37.cs               | Gem socketing, punching, and extraction               |
| AC 39         | AC39.cs               | Interactive quest tracker and step notifications      |
| AC 40         | AC40.cs               | Public auction house listings and bidding             |
| AC 41         | AC41.cs               | Marriage system proposal and wedding ceremonies       |
| AC 43         | AC43.cs               | Master-apprentice mentorship pairings                  |
| AC 44         | AC44.cs               | Milestone achievement unlocks                         |
| AC 45         | AC45.cs               | Daily attendance login streak rewards                  |
| AC 50         | AC50.cs               | Combat actions, 19-byte animation replay records       |
| AC 51         | AC51.cs               | Combat real-time HP/SP synchronization (51:1)          |
| AC 52         | AC52.cs               | Combat turn prompt enabling player input (52:1)        |
| AC 53         | AC53.cs               | Combat knockouts (53:3) and turn move ACK (53:5)       |
| AC 54         | AC54.cs               | Combat arena ladder rankings                           |
| AC 56         | AC56.cs               | Ferry and public transit scheduling                   |
| AC 57         | AC57.cs               | Arcade minigames and game room vouchers                |
| AC 59         | AC59.cs               | Remote player profile inspection                       |
| AC 60 / 61    | AC60.cs / AC61.cs     | Guild alliances and war declarations                   |
| AC 62         | AC62.cs               | Tent furniture placement and rotation                  |
| AC 63         | AC63.cs               | Login character selection and slot data                |
| AC 64         | AC64.cs               | Tent manufacturing via Compound2.dat recipes           |
| AC 65         | AC65.cs               | Tent deployment (65:1) and packing (65:4)              |
| AC 66 / 67    | AC66.cs / AC67.cs     | Tent furniture styling, flooring, and wallpaper        |
| AC 68         | AC68.cs               | Tent second-floor expansion and staircases             |
| AC 69         | AC69.cs               | Tent guest permissions and security locks              |
| AC 70 / 71    | AC70.cs / AC71.cs     | Friend categorization and user ignore blacklists       |
| AC 72         | AC72.cs               | In-game mailbox messaging                              |
| AC 74 / 75    | AC74.cs / AC75.cs     | Item Mall catalog browsing and point purchasing        |
| AC 76         | AC76.cs               | VIP membership tiers and privileges                    |
| AC 77 / 78    | AC77.cs / AC78.cs     | Companion equipment slots and amity food items         |
| AC 79         | AC79.cs               | Companion rebirth ascension checks                     |
| AC 80         | AC80.cs               | Hot spring HP/SP recovery pools                        |
| AC 81         | AC81.cs               | Stunt skill unlocks                                    |
| AC 82         | AC82.cs               | World clock and day/night lighting cycles              |
| AC 84         | AC84.cs               | Character Rebirth class specialization                 |
| AC 85         | AC85.cs               | Seasonal festival challenges and instance trials       |
| AC 86 / 87    | AC86.cs / AC87.cs     | Treasure maps and server lottery drawings              |
| AC 88         | AC88.cs               | Monthly attendance reward matrix                       |
| AC 89         | AC89.cs               | Anti-cheat security validation handshake               |
| AC 90         | AC90.cs               | Client-server synchronization timing                   |
| AC 91 / 92    | AC91.cs / AC92.cs     | Guild management, insignia upload, research buffs      |
| AC 104 / 105  | AC104.cs / AC105.cs   | Lucky draw prize wheel and voucher redemption          |
| AC 183 / 184  | AC183.cs / AC184.cs   | Ranked PvP matchmaking and guild territory siege       |
| AC 186        | AC186.cs              | Cutscene camera playback and stage direction           |
| AC 191 / 199  | AC191.cs / AC199.cs   | Companion rebirth stat redistribution and skill books  |
| AC 226        | AC226.cs              | Special seasonal festival races and minigames          |
+---------------+-----------------------+-------------------------------------------------------+
```

---

## 5. Packet Dispatch & Routing Pipeline

1. **Ingress:** Packet bytes arrive at [`Client3.RecvProc`](file:///D:/GitHub/Wonderland-Private-Server/RCLibrary/System.Net.Sockets/Client3.cs) and are decrypted.
2. **Event Notification:** `Client3.onPacketRecved` fires, routing the buffer to [`Player.ProcessSocket(IPacket g)`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Player.cs).
3. **Dispatch:** `Player.ProcessSocket` inspects byte 4 (`ActionCode`), queries [`Network.ActionCodes.AC.GetAction(int id)`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Network/ActionCodes/AC.cs), and executes `action.ProcessPkt(player, packet)`.
4. **Unhandled Fallback:** If an Action Code has no active handler registered, the packet is logged to `DebugSystem` with hex dumps without interrupting the socket session.

---

## 6. AC 63: Character Selection and Login Pipeline

### 6.1 SubCode 4: Account Credentials & Character List (`AC 63:4`)

1. Client sends user credentials and password hash to the Login Server.
2. Server validates user credentials via `UserDataBase.GetUserData(username, password)`.
3. Server responds with user account identifier:
   - Header: `bb` (ActionCode 63, SubCode 4)
   - Payload: Database ID, User ID, and encrypted Session Key.
4. Server compiles the character roster packet (`AC 63:1`):
   - Retrieves `Character1ID` and `Character2ID` via `CharacterDataBase.GetCharacterData`.
   - Serializes existing characters to byte arrays via `Character.ToArray()`.
   - Skips empty slots without corrupting packet byte offsets.
   - Dispatches packed character list to the client to render the character selection screen.

### 6.2 SubCode 1: Character Selection & World Entry (`AC 63:1`)

1. User selects character slot (`charNum = 1` or `2`).
2. Server loads full player state via `CharacterDataBase.GetCharacterData(charID, ref player)`.
3. If valid, server invokes `WorldServer.OnLogin(player)`:
   - Registers player with target map.
   - Synchronizes inventory, stats, skills, friends, and active companions.
   - Broadcasts visual appearance to surrounding map peers.

