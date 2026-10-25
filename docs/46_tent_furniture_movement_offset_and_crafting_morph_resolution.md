# Technical Specification: Tent Furniture Movement Offset & Crafting Morph Resolution

## 1. Overview & Operational Objective

This specification details the root causes, network packet structures, and architectural remediation for two critical issues observed during runtime gameplay:
1. **Tent Furniture Movement Failure (`AC 62:3`):** Workbench (`38049`) inside the player tent failed to move with the error `[Tent] Move failed: Index 515 out of range (Count: 2)`.
2. **Crafting Shapeshift Morphing (`AC 5:5`):** Crafting any item or triggering specific system managers caused the player character (`dasdas`, CharID `10001`) to morph into an NPC model (`60018`).

---

## 2. Root Cause Analysis

### A. AC 62 Payload Pointer Desynchronization

In the core network pipeline [`Player.ProcessSocket`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Player.cs#L1192):
```csharp
p.SetPtr();          // Initializes pointer to index 4
var b = p.Unpack8(); // Reads ActionCode (62) at index 4; pointer advances to index 5
```
When `ac.ProcessPkt(c, p)` was dispatched to [`AC62.ProcessPkt`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC62.cs#L15), the receive packet pointer remained at byte 5. Property `r.B` read `this[5]` (the SubCode `0x03`) without altering `ptr`.

In `Recv3`:
```csharp
ushort rawIndex = r.Unpack16(); // Unpacked byte 5 (SubCode 0x03) and byte 6 (Index 0x02) -> 515!
uint x = r.Unpack32();          // Read bytes 7..10 instead of 8..11 -> 10752 instead of 42
uint y = r.Unpack32();          // Read bytes 11..14 instead of 12..15 -> 9984 instead of 39
uint z = r.Unpack32();          // Read bytes 15..18 instead of 16..19 -> 256 instead of 1
byte rotation = r.Unpack8();    // Read byte 19 instead of 20
```
Because `rawIndex = 515` exceeded `TentObjects.Count` (2), index resolution resolved to `-1`, throwing `Move failed: Index 515 out of range`.

Additionally, the Z elevation parameter (1) in `AC 62:3` was erroneously treated as `floor`, causing `item.floor` to be overwritten to 1 (2nd floor) instead of remaining 0 (ground floor).

### B. Action Code 5:5 Shapeshift Protocol Collision

In Wonderland Online, `AC 5:5` is the dedicated **RiceBall shapeshifting packet**:
$$\text{Packet Format: } [5, 5, \text{CharID (4B UInt32 LE)}, \text{NpcID (2B UInt16 LE)}]$$
- $\text{NpcID} > 0$: The game client replaces the player's 3D skeleton and mesh with the model corresponding to $\text{NpcID}$.
- $\text{NpcID} == 0$: The game client cancels any active morph and restores the original human character model (`Daniel`, head `0`).

In multiple crafting and event managers, developers mistakenly used `AC 5:5` with placeholder IDs (`60018`, `60020`, `60015`, `60025`, `60010`, `60050`) assuming it was a particle effect or sound cue. Consequently:
- Manufacturing an item broadcast `[5, 5, CharID, 60018]`, instantly shapeshifting the character into NPC `60018`.
- Synthesizing items via Alchemy broadcast `60020`.
- Blacksmith repairs broadcast `60015`.
- Equipment forging broadcast `60025`.
- Marriage ceremonies broadcast `60010`.
- Rebirth / Ascension broadcast `60050`.

The authentic synthesis/crafting sparkle particle effect in Wonderland Online is **`AC 23:122`** (`[23, 122, CharID (4B)]`), as documented in [`AC23.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC23.cs#L653).

---

## 3. Implemented Corrections

### A. AC 62 Dispatch Alignment ([`AC62.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC62.cs))
- Explicitly called `r.SetPtr(6)` at the entry point of `ProcessPkt` to skip Header (4B), ActionCode (1B), and SubCode (1B), ensuring all subcode handlers (`Recv1`, `Recv2`, `Recv3`, `Recv4`) unpack from payload offset 6.
- Updated `Recv3` to correctly parse:
  - `rawIndex` (2B UInt16)
  - `x` (4B UInt32)
  - `y` (4B UInt32)
  - `z` (4B UInt32, fallback to 1 if 0)
  - `rotation` (1B Byte)
- Updated `AC 62:3` confirmation packet transmission:
  $$\text{Payload: } [62, 3, \text{rawIndex:2}, x:4, y:4, z:4, \text{rot:1}]$$

### B. Tent Elevation Preservation ([`Tent.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Tent/Tent.cs))
- Updated `MoveItem(ushort index, int x, int y, int z, byte rotation)`:
  - Sets `item.tentX = (ushort)x;`
  - Sets `item.tentY = (ushort)y;`
  - Sets `item.tentZ = (ushort)(z > 0 ? z : 1);`
  - Sets `item.rotate = rotation;`
  - Preserves `item.floor` without corruption.
- In `PlaceItem`, ensured `newItem.tentZ = 1;`.

### C. Visual Effect & Morph Elimination
Replaced all erroneous `AC 5:5` dummy NPC morph broadcasts with authentic `AC 23:122` synthesis visual sparkles and proactive morph reset `[5, 5, CharID, 0]`:
1. [`AC64.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC64.cs): Broadcasts `[23, 122, CharID]` and `[5, 5, CharID, 0]`.
2. [`TentManufactureManager.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Crafting/TentManufactureManager.cs): Broadcasts `[23, 122, CharID]` and `[5, 5, CharID, 0]`.
3. [`AlchemyManager.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Crafting/AlchemyManager.cs): Broadcasts `[23, 122, CharID]` and `[5, 5, CharID, 0]`.
4. [`EquipmentRepairManager.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Crafting/EquipmentRepairManager.cs): Broadcasts `[23, 122, CharID]` and `[5, 5, CharID, 0]`.
5. [`ForgingManager.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Crafting/ForgingManager.cs): Broadcasts `[23, 122, CharID]` and `[5, 5, CharID, 0]`.
6. [`MarriageManager.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/MarriageManager.cs): Broadcasts `[23, 122, ProposerCharID]` and `[5, 5, ProposerCharID, 0]`.
7. [`RebornManager.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/RebornManager.cs): Broadcasts `[23, 122, CharID]` and `[5, 5, CharID, 0]`.
8. [`WorldServer.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Server/WorldServer.cs): Dispatches `[5, 5, CharID, 0]` on login immediately after `SendCharacterData()` to clear any client-cached shapeshift state.

---

## 4. Empirical Verification

- Solution built clean via `dotnet build "Wonderland Private Server.sln"` (0 Errors, 0 Warnings).
- Executed automated PowerShell integration test `Test-TentAndMorphFixes.ps1`:
  - Verified default tent items: Trash Can (`38055`) at `(34, 42)` and Workbench (`38049`) at `(37, 42)`.
  - Injected exact client packet `F4 44 11 00 3E 03 02 00 2A 00 00 00 27 00 00 00 01 00 00 00 00` (`AC 62:3`).
  - Verified Workbench position moved to `(42, 39)`, `tentZ = 1`, `floor = 0`, `rotate = 0`.
  - Verified `AC 62:3` confirmation packet sent with matching parameters: `Index=2, X=42, Y=39, Z=1, Rot=0`.
  - Verified shapeshift reset packet format: `[5, 5, CharID=10001, NpcID=0]`.
  - Verified crafting sparkle visual packet format: `[23, 122, CharID=10001]`.
