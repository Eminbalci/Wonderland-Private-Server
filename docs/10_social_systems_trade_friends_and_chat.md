# Social Systems, Player Trade, Friends, and Chat Specification

## 1. Architectural Overview

The social subsystem facilitates player-to-player trade, persistent bilateral friendships, multi-channel chat, and inventory transactions. The core components are implemented in [`Trade.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Trade.cs), [`AC14.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC14.cs), [`AC13.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC13.cs), and [`AC02.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC02.cs).

---

## 2. Inventory Architecture & 31-Byte Record Alignment (`AC 23`)

* **Bag Capacity:** 50 discrete inventory slots.
* **31-Byte Item Record Layout (`AC 23:5` / `AC 30:5`):**
  Ensures byte-for-byte alignment with the client unmanaged struct parser:
  - Byte `0`: Slot index (`1..50`).
  - Bytes `1..2`: Item ID (`UInt16 LE`).
  - Byte `3`: Item damage / wear condition.
  - Bytes `4..5`: Quantity count (`UInt16 LE`).
  - Bytes `6..30`: Socket IDs, forge levels, and crystal attributes.
* **21-Byte Equipment Record Layout (`AC 23:11`):** Serializes active worn gear across 6 equipment slots (Head, Body, Weapon, Wrist, Shoes, Special).

---

## 3. Bilateral Friend System Protocol (`AC 14` & `AC 10`)

```
+---------------+---------------+-------------------------------------------------------+
| Packet Code   | Stride / Size | Functional Purpose                                    |
+---------------+---------------+-------------------------------------------------------+
| AC 14:11      | 26 Bytes / Rec| Friend category tabs and group labels                 |
| AC 14:5       | 28 Bytes / Rec| Master friend roster payload                          |
| AC 14:2       | 5 Bytes       | Client invitation request: [14, 2, TargetCharID]      |
| AC 14:3       | 6 Bytes       | Peer acceptance handshake: [14, 3, TargetCharID, 1]   |
| AC 14:9       | Variable      | Commits new friend entry to client UI roster          |
| AC 14:7       | 5 Bytes       | Friend login/logout state change notification         |
| AC 10:3       | 6 Bytes       | Real-time presence beacon: [10, 3, CharID: UInt32, FF]|
| AC 14:4       | 5 Bytes       | Bilateral friend removal: [14, 4, TargetCharID]       |
+---------------+---------------+-------------------------------------------------------+
```

### 3.1 Bilateral Removal Handshake
When Player A removes Player B via `AC 14:4`:
1. Server deletes relational record `(CharID1, CharID2)` and `(CharID2, CharID1)` from the `Friends` table.
2. Server dispatches `AC 14:4 <PlayerB_ID>` to Player A.
3. If Player B is currently online, server dispatches `AC 14:4 <PlayerA_ID>` to Player B, cleanly removing the entry from both client interfaces.

---

## 4. Atomic Player-to-Player Trade Engine (`AC 13`)

Trade operations follow a strict 4-phase state machine ensuring zero item duplication or desynchronization:

```
[Phase 1: Invitation] ---> Player A sends AC 13:1 -> Player B accepts AC 13:1
       |
       v
[Phase 2: Staging]    ---> Players stage items and gold into virtual escrow slots
       |                   Server updates trade window UI via AC 13:2
       |
       v
[Phase 3: Lock]       ---> Both players click Lock (AC 13:3). Escrow slots become immutable.
       |
       v
[Phase 4: Confirm]    ---> Both players click Confirm (AC 13:4).
       |
       +---> [If Both Confirmed]:
       |        1. Verifies inventory space in both characters under lock (mlock)
       |        2. Transfers items and gold atomically
       |        3. Emits AC 13:9, AC 13:10, AC 13:238 commit packets
       |        4. Persists character saves to database
       |
       +---> [If Disconnect / Cancel Occurs]:
                1. Rollbacks all escrowed items to original bags
                2. Emits AC 13:5 cancellation packet
```

---

## 5. Multi-Channel Chat & GM Moderation (`AC 02`)

* **Channel Opcodes:**
  - `Sub 1`: Public local area chat (dispatched to all players on the current map).
  - `Sub 2`: Private direct whisper (`/w <PlayerName> <Message>`).
  - `Sub 3`: Party / Team chat.
  - `Sub 4`: Guild chat.
* **Administrative GM Chat Commands:**
  Handled directly in [`AC02.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC02.cs) when `player.Account.gm >= 1`:
  - `:broadcast <message>`: Dispatches server-wide banner and audio alert.
  - `:mute <char> [mins]` & `:unmute <char>`: Toggles public chat privileges.
  - `:kick <char>`: Forcibly drops client socket connection.
