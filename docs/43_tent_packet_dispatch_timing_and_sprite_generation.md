# 43 - Tent Packet Dispatch Timing & Sprite Generation Architecture

## 1. Context and Problem Statement
When players entered their personal tent interior (Map ID `63507`), starter furniture—namely the Resource Recycling / Trash Can (`38055`) and Work Platform / Low Workbench (`38049`)—failed to render on the tent floor despite being properly seeded in SQLite and transmitted over the network.

## 2. Root Cause Analysis in Client Binary (`aLogin.exe`)
Disassembly of `C:\Games\WLRI\aLogin.exe` across the ActionCode 62:4 handler (`0x42d8ea`), map transition dispatcher (`0x2e1613`), and sprite generation engine (`0x4358c4`) revealed a strict timing and state precondition:

1. **Tent Map Validation Guard (`0x42dd08` - `0x42dd1d`):**
   ```assembly
   0x42dd08: mov eax, dword ptr [0x4caa28]
   0x42dd0d: mov eax, dword ptr [eax]
   0x42dd0f: mov ax, word ptr [eax + 0x235c] ; Current MapID
   0x42dd16: call 0x485a08                   ; Range check: 0xF80D <= MapID <= 0xFFDC (63501..65500)
   0x42dd1b: test al, al
   0x42dd1d: je 0x430b58                     ; Aborts if current map is not tent!
   0x42dd23: mov eax, dword ptr [0x4caa28]
   0x42dd28: mov eax, dword ptr [eax]
   0x42dd2a: movzx eax, byte ptr [eax + 0x22a0] ; Floor level (0 = ground, 1 = upstairs)
   0x42dd31: call 0x485a1c                   ; Floor index check (floor <= 1)
   0x42dd36: test al, al
   0x42dd38: je 0x430b58                     ; Aborts if floor is invalid!
   0x42dd3e: mov eax, ebx                    ; Tent Manager
   0x42dd40: call 0x4358c4                   ; CREATE TENT SPRITES ON SCREEN!
   ```

2. **Premature Dispatch Flaw:**
   - In `Tent.SendMapInfo(Player t)` (called during `Map.Warp_In` inside `Map.Teleport`), the server transmitted `AC 62:4` immediately following `SendAc12`.
   - At that moment, the client had only just received the warp instruction. Its active in-memory `MapID` at `[0x4caa28] + 0x235c` was still the overworld map (`10001` or similar).
   - Because `MapID < 63501`, `0x485a08` returned `0`, triggering `je 0x430b58`. The client aborted packet processing and completely skipped `call 0x4358c4` (sprite generation).

3. **Collision / Slot Tainting Side Effect:**
   - Prior to checking `MapID` at `0x42dd08`, `0x42d8ea` populated the client's internal `map_struct` at `[ebx + eax*57 + 0x1a]`.
   - When a redundant `AC 62:4` was subsequently sent after map load, the collision check in `0x4169e0` (`cmp word ptr [ebx + eax + 0x1a], 0`) detected an existing occupancy and rejected every item with error code `0xFF`, leaving the room blank.

4. **Scene Curtain Release Precedence (`AC 5:4`):**
   - In `AC12.Recv1`, `p.Send(Tools.FromFormat("bb", 5, 4))` was previously dispatched before sending tent furniture, dropping the loading veil before the scene objects had registered.

## 3. Implementation and Packet Sequencing Fix

### Exact Packet Lifecycle Sequence
Upon entering a tent:
1. `Map.Warp_In`:
   - Dispatches `AC 12` (destination MapID `63507`).
   - Dispatches `AC 7` (coordinates), `AC 5:0` (equipment), and `AC 5:8` (sprite refresh).
   - `Tent.SendMapInfo` defers furniture synchronization, avoiding premature dispatch.
2. Client loads assets, sets its internal MapID to `63507`, and responds with `AC 12:1`.
3. `AC12.Recv1`:
   - Transmits `AC 65:12` (Floor bounds polygon `0x2e58` for ground floor).
   - Transmits `AC 65:12` (Floor 1 bounds if `Enlarged`).
   - Transmits `AC 65:7` (Lock state).
   - Transmits `AC 62:14` (Floor 1 color/texture).
   - Transmits `AC 62:15` (Floor 1 wallpaper).
   - Executes `tentMap.EnsureDefaultItemsIfEmpty()`.
   - Transmits `tentMap.SendTentItemsToPlayer(p)` (`AC 62:4`).
     - Client verifies `MapID == 63507` and `PtInPolygon` passes.
     - Client calls `0x4358c4`, instantiating visible furniture sprites.
   - Transmits `AC 5:4` to fade in and present the fully populated room.

## 4. Modified Source Files
- [`Src/Network/ActionCodes/AC12.cs`](file:///D:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC12.cs): Adjusted dispatch order in both `Warping` and non-`Warping` branches so `SendTentItemsToPlayer` precedes `AC 5:4`.
- [`wlo.pserver.core/Game/PlayerRelated/Tent/Tent.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Tent/Tent.cs): Removed premature `SendTentItemsToPlayer(t)` from `SendMapInfo`.
