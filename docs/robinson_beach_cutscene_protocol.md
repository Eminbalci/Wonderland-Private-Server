# Robinson Beach Rescue Cutscene Protocol (Map 10035 / Quest 12040)

## Overview
Documents the authentic cutscene timeline, camera focus, character immobilization, and dialogue sequence on Map 10035 (Shipwreck Beach / Desert Island) when arriving from the starter ship prologue, verified against `ilkgorevinanimasyonlukisimlari.pcapng` and `Eve.emg` (Map 10035 Event 8).

---

## Authentic Official Server Packet Sequence (`ilkgorevinanimasyonlukisimlari.pcapng`)

| Frame | Direction | ActionCode | SubCode | Payload / Meaning |
|---|---|---|---|---|
| **2405** | S -> C | **AC 32** | **Sub 2** | `20 02 [CharID (4B)] 09` -> Set Player Emote 9 (Lying unconscious on beach sand) |
| **2405** | S -> C | **AC 5**  | **Sub 30**| `05 1E 01 [CharID (4B)] 00` -> Immobilize player for cutscene rendering |
| **2408** | S -> C | **AC 20** | **Sub 8** | `14 08` -> Screen / Event start lock |
| **2408** | S -> C | **AC 22** | **Sub 11**| `16 0B 06 00 FF FF` -> Camera focus / pan |
| **2408** | S -> C | **AC 6**  | **Sub 2** | `06 02 01` -> Cinema mode letterbox & movement lock |
| **2408** | S -> C | **AC 20** | **Sub 11**| `14 0B` -> Audio track sync |
| **2408** | S -> C | **AC 20** | **Sub 10**| `14 0A` -> Sound effect sync / step gate |
| **2414** | S -> C | **AC 22** | **Sub 12**| `16 0C 02 0B 00 05` -> Robinson approach & bending over player animation |
| **2414** | S -> C | **AC 20** | **Sub 10**| `14 0A` -> Sound effect sync / step gate |
| **2436** | S -> C | **AC 20** | **Sub 1** | `14 01 ...` -> Dialogue Step 1 (TalkID 20304 / Robinson rescue) |

---

## Implementation Architecture

1. **Map Entry (`AC 12:1`)**:
   - `PendingBeachCutscene` flag or fresh character check on Map 10035.
   - Dispatches player Emote 9 (lying down) and `AC 5:30` (immobilization).
   - Activates `BeachCutsceneActive = true`.
2. **Timeline Delays & Rendering**:
   - Asynchronous timed task dispatches Frame 2408 camera pan & cinema mode after 300ms.
   - Dispatches Frame 2414 Robinson walk and bend approach after 1200ms.
   - Dispatches Frame 2436 Robinson 9-step dialogue after 1500ms once animations complete visually.
3. **Packet Isolation (`AC 20:6`)**:
   - `AC20.Recv6` absorbs incoming client progression packets while `BeachCutsceneActive` is true to prevent premature skipping or early movement unlocking.
4. **Dialogue & Quest Completion**:
   - `EveEventInterpreter` streams authentic dialogue lines (`TalkIDs 20304..20312`).
   - Opcode 5 registers Quest #12040 to Step 1 (`InProgress`).
   - Final completion unlocks movement via `AC 20:8` and `AC 5:4`.

---

## Code References
- Handled in [`AC12.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC12.cs).
- Packet absorption & unlock gating in [`AC20.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC20.cs).
- Cutscene trigger and ship-to-beach transition in [`AC186.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC186.cs).
