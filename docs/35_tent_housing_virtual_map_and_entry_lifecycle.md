# Tent Housing Virtual Map and Entry Lifecycle Specification

## Overview

This specification details the virtual map routing, packet pointer parsing, and housing lifecycle for player personal tents in Wonderland Online. Tents operate as isolated instanced virtual maps dynamically mapped to player character IDs.

---

## 1. Virtual Map Routing Architecture

### 1.1 Virtual Map ID Scheme

In Wonderland Online, tent interiors are instanced maps with IDs computed as:
$$\text{TentMapID} = 100000 + \text{OwnerCharID}$$

For example, a character with `CharID = 5001` has a tent map ID of `105001`.

### 1.2 Dual-Keying in Map Management (`wlo.pserver.core/Game/Maps/Map.cs`)

Because client interactions and warp tables interchangeably reference the base `OwnerCharID` (`5001`) and the expanded `TentMapID` (`105001`), the server map dictionary `Tents` is dual-keyed:
```csharp
Tents[ownerId] = targetTent;
Tents[100000 + ownerId] = targetTent;
```

Resolution in `Map.onEnterTent` tests both representations with automatic fallback:
```csharp
Tent targetTent = null;
if (!Tents.TryGetValue(ID, out targetTent) &&
    !(ID > 100000 && Tents.TryGetValue(ID - 100000, out targetTent)) &&
    !Tents.TryGetValue(100000 + ID, out targetTent))
{
    uint ownerId = ID > 100000 ? (ID - 100000) : ID;
    if (p.CharID == ownerId && p.Tent != null)
    {
        targetTent = p.Tent;
        Tents[ownerId] = targetTent;
        Tents[100000 + ownerId] = targetTent;
    }
}
```

---

## 2. Root Cause Analysis of Entry Failures

### 2.1 Packet Stream Pointer Corruption (`Src/Network/ActionCodes/AC65.cs`)

When a player clicks an erected tent on the overworld map:
- The client transmits `AC 65:1` with the tent owner's 32-bit `CharID`:
  ```
  Byte 0..3: 0xF4, 0x44, 0x06, 0x00 (Header & Length)
  Byte 4:    65 (ActionCode 65: Tent)
  Byte 5:    1  (SubCode 1: Enter Tent)
  Byte 6..9: Owner CharID (UInt32, Little-Endian)
  ```
- In `AC65.cs`, `Recv1` previously called `p.Unpack32()` without setting the stream pointer to offset 6.
- In `RecievePacket`, constructor initialization sets pointer to offset 4. Accessing property `p.B` examines offset 5 via direct indexer `this[5]` without advancing `m_ptr`.
- Consequently, `p.Unpack32()` unpacked bytes 4..7 (`[65, 1, CharID_L, CharID_H]`) instead of bytes 6..9. For CharID `5001` (`0x00001389`), this read `1280257` (`0x00138901`).
- The lookup for tent `1280257` failed, and tent entry was rejected.

**Fix:** Added explicit `p.SetPtr(6);` before unpacking the 32-bit owner ID in `AC65.cs`.

### 2.2 Unsent Map Initialization Packets (`wlo.pserver.core/Game/PlayerRelated/Tent/Tent.cs`)

Inside `Tent.SendMapInfo(Player t)`:
- The method constructed all tent furniture, wall colors, floor colors, and map load termination packets (`AC 23:102` and `AC 20:8`) into a temporary `PacketBuilder tmp`.
- However, it omitted `t.Send(new SendPacket(tmp.End()))` at the conclusion of the method.
- Even if a player entered the tent, the client never received the "Map Load Finished" frame and froze with a black loading screen.

**Fix:** Appended `t.Send(new SendPacket(tmp.End()));` to transmit the accumulated scene payload.

### 2.3 Tent Item Restoration in Starting Dialogue (`wlo.pserver.core/Game/Maps/Map.cs`)

In the Astrologer Laura shipwreck scene on Map 10014:
- The server granted items `32000` (an invalid ID) and `32075` (an empty placeholder).
- Authentic WLO grants `36002` (Personal Tent) and `34058` (Remote Control).
- Without item `36002`, players could never deploy a tent in the first place.

**Fix:** Replaced reward IDs with authentic Tent `36002` and Remote Control `34058`.

---

## 3. Wire Protocol & Teleport Cycle

```
Overworld Map (e.g. Map 10001)
  |
  |--- AC 65:1 [OwnerCharID: UInt32] ---------> (Client clicks tent)
  |                                            1. Server validates tent existence
  |                                            2. Records return coordinates (Map, X, Y)
  |                                            3. Teleports to Virtual Tent Map
  |
Virtual Tent Map (Map 100000 + OwnerCharID)
  |<-- AC 23:102 (Map Load Finished) ---------- 4. Client renders tent interior
  |<-- AC 20:8   (Unfreeze Player) ------------ 5. Enables interior movement
  |
  |--- AC 65:3 (Exit Tent) -------------------> (Player clicks door rug)
  |                                            6. Server warps back to TentReturnMap
```
