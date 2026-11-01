# Map 12600 (Bangkok Main Street) Bangkok Monk Visibility, Body Rendering & Hit-Test Fix

## 1. Executive Summary & Root Cause Analysis

When players warped to Map 12600 (Bangkok Main Street), the NPC **Bangkok Monk** (ClickID `3`, TemplateID `14729`, Body ID `1277`) was observed with only his floating overhead name visible. His body sprite was completely missing, and clicking anywhere near him failed to register any interaction.

Deep reverse engineering of the official game client binary (`aLogin.exe`) and `odd_d01.dat` revealed the exact mechanics behind this behavior:

### 1.1 Overhead Name vs Body Render Separation (`aLogin.exe:0x0042e694`)
- `0x0042e694` is `TActor.DrawActor` located at `[vmt + 8]` in `THuman`'s Delphi VMT.
- The routine checks visibility flags:
  - `*(actor + 0x1eff) != 2`
  - `*(actor + 0x1eec) == 1` (1 = visible; 2/4 = skip body)
  - `*(actor + 0x2147) != 0` (viewport visible)
  - `*(actor + 0x1efc) != 0` (render enabled)
  - If any condition fails, execution jumps directly to `0x00430357`.
- **At `0x00430357`:** The client calls `0x00433e80`, which renders the actor's floating overhead name text **unconditionally**! This explains why the name was clearly visible in mid-air while the body was completely missing.

### 1.2 Animation Frame Failure on Stance 46 (`0x2E`)
- At `0x0042e76b`: The client checks the stance/animation index stored at `*(actor + 0x121)`.
- If `anim > 15`, client invokes `0x00474898` and `0x0031840c` to load frame data from `odd_d01.dat`.
- In `aLogin.exe:0x0044a51c` and `0x0044a558`: Animations `0x2E` (46) through `0x35` (53) are **Mount / Vehicle riding poses**.
- Bangkok Monk (Body ID 1277) is an unmounted pedestrian NPC and has no mount riding frames in `odd_d01.dat`.
- Inside `0x0031b0a8` (sprite builder), `0x00317df4` checked the requested animation 46 against the maximum animation index for Body 1277 and failed, jumping to `0x0031bf2f` (no-op).
- This left the sprite frame pointers null/uninitialized, aborted body drawing, and jumped to `0x00430357` (name-only render).

### 1.3 Mouse Hit-Testing & Unclickable NPC (`aLogin.exe:0x0043b874`)
- Mouse cursor movement and left clicks are processed by `0x00162bb4` -> `0x0043b874`.
- For NPC/actor entities (`Case 3` at `0x0043c4d5`):
  - At `0x0043c52d`: Checks `*(actor + 0x1eec) == 1`.
  - At `0x0043c80f`..`0x0043c811`: Invokes `[vmt + 8]` (`0x0042e694`) to update the actor's screen coordinates and calculate the mouse bounding box.
  - At `0x0043c81c`: Evaluates `*(actor + 0x80)` (cursor hit flag).
  - Because body rendering was aborted due to invalid stance 46, the bounding box was empty, `*(actor + 0x80)` remained `0`, mouse clicks never registered, and the dialogue never triggered.

### 1.4 PreEvent ActionType 4 vs ActionType 7
- Map 12600 PreEvent 0 Rule 0 contains raw bytes:
  `02 03 00 04 00 2E 00 00 00 00 00 00 00 01 00 00 00 00 00 00 00`
- `actionType == 4` specifies facing direction (Byte 13 = `0x01` -> facing 1 / South-West) and was mistakenly parsed as an actor pose animation (`anim = 46`).
- In official WLO protocol, living pedestrian character NPCs (`QuestNpc.GetIdleAnimationFrame == 0x00FF`) must spawn with `Stance = 0` (Byte 13 = `0`) and `State = 0x01FF` (facing 1 in high byte, idle frame 0xFF in low byte).
- Only explicit `actionType == 7` represents an emote/pose animation, never mount stance 46 on pedestrian NPCs.

---

## 2. Technical Modifications

### 2.1 PreEvent Action Handling (`PreEventInterpreter.cs`)
- In `GetNpcInitialPose`:
  - `actionType == 7` extracts emote/pose animation and facing.
  - `actionType == 10 || actionType == 4` extracts facing direction without overriding `animation` (`animation` remains `0`).
- In `ExecuteActionBlock`:
  - `actionType == 7` calls `SendActorPose(player, clickId, anim, facing)`.
  - `actionType == 10 || actionType == 4` dispatches standard `AC 22:1` actor turn packet (`[22, 1, clickId, dir]`).
- In `SendActorShow`:
  - Living character NPCs spawn with `stance = 0` and `frame = (0x00FF | (facing << 8))`.
  - Removed forced `AC 22:9` dispatch when `stance == 0`.

### 2.2 Scene Table Synchronization (`AC22.cs` & `Map.cs`)
- In `BuildNpcTablePacket` and `Map.SendMapInfo`:
  - For living character NPCs (`!qn.IsStaticNpc()`), `state = (ushort)((idleFrame & 0xFF) | (initFacing << 8))` and `stance = (byte)initAnim`.
  - For Bangkok Monk, `state = 0x01FF` and `stance = 0`.
  - Byte 13 in `AC 22:4` packet is `0`.

---

## 3. Verification & Results

- **Unit Verification (`test_bangkok_monk_fix.ps1`):**
  - Confirmed `PreEventInterpreter.GetNpcInitialPose(player, 12600, 3)` returns `hasPose=True`, `initAnim=0`, `initFacing=1`.
  - Resulting state: `State = 0x01FF`, `Stance = 0`.
- **Compilation:**
  - `Wonderland Private Server.sln` compiled with **0 errors, 0 warnings**.
- **Client In-Game Outcome:**
  - Bangkok Monk's Body 1277 renders correctly facing direction 1.
  - Sprite bounding box is active at `(2525, 399)`.
  - Mouse clicks trigger conversation without issues.
