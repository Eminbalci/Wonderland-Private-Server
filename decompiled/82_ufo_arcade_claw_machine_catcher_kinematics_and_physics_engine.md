# Document 82: UFO Arcade Claw Machine Catcher Kinematics, Collision Physics, and Prize Dispenser Engine

## 1. Executive Summary & Engine Architecture

In Wonderland Online, arcade claw machine attractions (located in the Carnie Mini-Game Park and municipal game centers) simulate physical plush doll grabbing through the crane kinematics state machine implemented in `cMZ_Catcher.pas`. Dissected from client subroutines at RVA `0x00185b3c` (`CatcherBasic`), RVA `0x00185bd4` (`CatcherMoving`), RVA `0x00185a90` (`CatcherDroping`), RVA `0x00185c8c` (`Catching`), animation controller `FUN_00185d30`, and master coordinator `FUN_00185e7c`, this engine governs electromechanical arcade simulations.

The claw machine operates across a 4-state deterministic finite state machine (FSM):
1. **`CatcherBasic`**: Rest stance ($154 \times 126$ px) parked at upper-left coordinates $(0, 0)$. Awaits coin insert.
2. **`CatcherMoving`**: Dual-phase lateral translation ($154 \times 235$ px). Button 1 drives the X-axis rail eastward; releasing locks the X position. Button 2 drives the Y-axis carriage southward; releasing triggers descent.
3. **`CatcherDroping`**: Vertical cable unspooling ($154 \times 128$ px) descending down the Z-axis toward the prize bed.
4. **`Catching`**: 3-prong mechanical jaw closure ($154 \times 242$ px) executing AABB bounding box collision checks against prize entities, followed by a vertical hoist retraction and horizontal return to the collection chute.

```
+------------------------------------------------------------------------------------+
|                UFO ARCADE CLAW MACHINE KINEMATICS STATE MACHINE                    |
+------------------------------------------------------------------------------------+
                                       |
                                       v
                    [State 0: CatcherBasic (154x126 px)]
             Parked at Rest Origin (0, 0) | Coin Handshake (AC 0x4A:0x15)
                                       |
                                       v  Button 1 Pressed & Released (X-Axis)
                    [State 1: CatcherMoving (154x235 px)]
             Lateral Carriage Translation | Button 2 Pressed (Y-Axis)
                                       |
                                       v  Button 2 Released
                    [State 2: CatcherDroping (154x128 px)]
             Z-Axis Cable Unspooling Descent to Target Coordinates
                                       |
                                       v  Target Z Depth Attained
                    [State 3: Catching (154x242 px)]
             Jaw Clamping Closure | AABB Hit-Test | Tension Slip Roll
                                       |
         +-----------------------------+-----------------------------+
         | Success: Retain Grip                                      | Failure: Slip Fallback
         v                                                           v
  [Hoist & Return to Drop Chute]                              [Doll Drops Back to Pit Bed]
  - Item Dispensed to Player Bag                              - No Item Dispensed
  - AC 0x4A:0x15 Prize Protocol                               - Reset to CatcherBasic
```

---

## 2. Memory Layouts & Data Structures

### 2.1 Claw Crane Component Instance (`cMZ_Catcher.pas`, `param_1`)

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `void*` | `VTable` | Delphi component virtual method table |
| `+0x08` | `int` | `ActiveState` | State indicator (-1: Inactive, 0: Basic, 1: Move, 2: Drop, 3: Catch) |
| `+0x0C` | `int` | `CurrentFrame`| Active animation frame index (0..5) |
| `+0x10` | `int` | `FrameCount` | Total animation frames allocated for current state |
| `+0x14` | `int` | `TickCounter` | Millisecond tick sub-accumulator |
| `+0x18` | `int` | `TickDelay` | Frame duration pacing (default 10 ms, drop 18 ms) |
| `+0x1C` | `int` | `SurfaceHeight`| Height dimension stride (0x7e, 0xeb, 0x80, 0xf2) |
| `+0x20` | `int` | `SurfaceWidth` | Width dimension stride ($154$ px / `0x9a`) |
| `+0x24..0x30`| `RECT` | `ClipBounds` | Screen bounding rectangle for DirectDraw clipping |
| `+0x38` | `int*` | `pAnimBasic` | Pointer to `CatcherBasic` sprite renderer |
| `+0x3C` | `int*` | `pAnimMoving` | Pointer to `CatcherMoving` sprite renderer |
| `+0x40` | `int*` | `pAnimDroping`| Pointer to `CatcherDroping` sprite renderer |
| `+0x44` | `int*` | `pAnimCatching`| Pointer to `Catching` sprite renderer |

### 2.2 Prize Entity Hitbox Structure (Bed Dimensions: $480 \times 320$ px)

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `ushort`| `PrizeItemId` | Reward item ID from `Item.dat` |
| `+0x04` | `int` | `CenterPosX` | 2D horizontal center on prize bed (0..480 px) |
| `+0x08` | `int` | `CenterPosY` | 2D vertical center on prize bed (0..320 px) |
| `+0x0C` | `int` | `Radius` | Radial collision boundary (15..35 px depending on size) |
| `+0x10` | `byte` | `Weight` | Physical weight factor (determines slip probability) |

---

## 3. Disassembled Subroutine Analysis

### 3.1 Claw State Instantiation & Dimension Strides
Extracted from `aLogin_decompiled.c` lines 170095–170250:

```c
// 1. CatcherDroping (Z-Descent)
int FUN_00185a90(int param_1, char param_2) {
    FUN_00185548(param_1, 0);
    FUN_0018575c(param_1, "CatcherDroping");
    *(undefined4 *)(param_1 + 0x20) = 0x9a; // Width 154 px
    *(undefined4 *)(param_1 + 0x1c) = 0x80; // Height 128 px
    *(undefined4 *)(param_1 + 0x10) = 4;    // 4 frames
    *(undefined4 *)(param_1 + 0x18) = 10;   // 10 ms delay
    return param_1;
}

// 2. CatcherBasic (Rest Origin)
int FUN_00185b3c(int param_1, char param_2) {
    FUN_00185548(param_1, 0);
    FUN_0018575c(param_1, "CatcherBasic");
    *(undefined4 *)(param_1 + 0x20) = 0x9a; // Width 154 px
    *(undefined4 *)(param_1 + 0x1c) = 0x7e; // Height 126 px
    return param_1;
}

// 3. CatcherMoving (X/Y Translation)
int FUN_00185bd4(int param_1, char param_2) {
    FUN_00185548(param_1, 0);
    FUN_0018575c(param_1, "CatcherMoving");
    *(undefined4 *)(param_1 + 0x20) = 0x9a; // Width 154 px
    *(undefined4 *)(param_1 + 0x1c) = 0xeb; // Height 235 px
    *(undefined4 *)(param_1 + 0x10) = 4;    // 4 frames
    *(undefined4 *)(param_1 + 0x18) = 1;    // Fast movement cycle
    return param_1;
}

// 4. Catching (Jaw Closure & Hoist)
int FUN_00185c8c(int param_1, char param_2) {
    FUN_00185548(param_1, 0);
    FUN_0018575c(param_1, "Catching");
    *(undefined4 *)(param_1 + 0x20) = 0x9a; // Width 154 px
    *(undefined4 *)(param_1 + 0x1c) = 0xf2; // Height 242 px
    *(undefined4 *)(param_1 + 0x10) = 5;    // 5 animation frames
    return param_1;
}
```

### 3.2 Collision Hit-Test & Gripper Physics (`FUN_00185d30`)
When `Catching` frame reaches frame index 2 (fully closed jaws):
1. The claw's coordinates $(X_c, Y_c)$ are tested against all active prize centers:
   $$\text{Distance} = \sqrt{(X_c - X_p)^2 + (Y_c - Y_p)^2}$$
2. If $\text{Distance} \le \text{Radius}$, an AABB contact occurs.
3. A retention slip probability roll is calculated during the upward hoist:
   $$\text{SlipChance} = \max(0.10, \frac{\text{Weight}}{100} \times 0.75)$$
4. If slip occurs, the doll detaches at mid-height and falls back into the pit bed.

---

## 4. Network Wire Protocol: Action Code 74 (`0x4A`)

Claw machine interaction communicates under **Action Code 74** (`0x4A`):

### 4.1 Client Request: Start Play & Insert Token (`0x4A:0x15`)

```
Offset 0x00: [0x4A]            (Action Code 74: Minigame Protocol)
Offset 0x01: [0x15]            (Subcode: Claw Machine Play)
Offset 0x02: [MachineId: uint32] (Arcade Machine Entity ID)
```

### 4.2 Client Request: Report Drop Coordinates (`0x4A:0x16`)

```
Offset 0x00: [0x4A]            (Action Code 74)
Offset 0x01: [0x16]            (Subcode: Claw Drop Coordinates)
Offset 0x02: [TargetX: uint16] (Final horizontal X coordinate)
Offset 0x04: [TargetY: uint16] (Final vertical Y coordinate)
```

### 4.3 Server Response: Dispense Prize Result (`0x4A:0x15`)

```
Offset 0x00: [0x4A]            (Action Code 74)
Offset 0x01: [0x15]            (Subcode: Prize Dispense Result)
Offset 0x02: [Status: uint8]   (0x00: Success Prize Won, 0x01: Miss / Slip, 0x02: Insufficient Tokens)
Offset 0x03: [PrizeItemId: uint16] (Awarded Item ID if Status == 0x00)
Offset 0x05: [Quantity: uint8] (Quantity rewarded, typically 1)
```

---

## 5. C# Server Implementation Blueprint

```csharp
namespace Wonderland.Network.Handlers
{
    public static class ClawMachineHandler
    {
        private const byte ACTION_CODE_MINIGAME = 0x4A;
        private const ushort CARNIVAL_TOKEN_ID = 27001;

        public static void Register(PacketDispatcher dispatcher)
        {
            dispatcher.RegisterHandler(ACTION_CODE_MINIGAME, HandleMinigamePacket);
        }

        public static void HandleMinigamePacket(GameSession session, ReadOnlySpan<byte> packet)
        {
            if (packet.Length < 2) return;
            byte subcode = packet[1];

            if (subcode == 0x16) // Claw Drop Target
            {
                HandleClawDrop(session, packet[2..]);
            }
        }

        private static void HandleClawDrop(GameSession session, ReadOnlySpan<byte> data)
        {
            if (data.Length < 4) return;
            ushort targetX = BinaryPrimitives.ReadUInt16LittleEndian(data[0..2]);
            ushort targetY = BinaryPrimitives.ReadUInt16LittleEndian(data[2..4]);

            var character = session.Character;
            if (!character.Inventory.HasItem(CARNIVAL_TOKEN_ID, 1))
            {
                SendPrizeResult(session, 0x02, 0); // No tokens
                return;
            }

            character.Inventory.RemoveItemById(CARNIVAL_TOKEN_ID, 1);

            // Evaluate AABB collision against prize layout
            var prize = ClawMachineManager.EvaluateDrop(targetX, targetY);
            if (prize == null)
            {
                SendPrizeResult(session, 0x01, 0); // Complete miss
                return;
            }

            // Tension slip roll based on prize weight
            double slipChance = Math.Max(0.10, (prize.Weight / 100.0) * 0.75);
            if (Random.Shared.NextDouble() < slipChance)
            {
                SendPrizeResult(session, 0x01, 0); // Slipped during hoist
                return;
            }

            // Dispense prize
            character.Inventory.AddItem(prize.ItemId, 1);
            SendPrizeResult(session, 0x00, prize.ItemId);
            session.SendSystemNotice($"Congratulations! You won a {prize.Name}!");
        }

        private static void SendPrizeResult(GameSession session, byte status, ushort itemId)
        {
            byte[] response = new byte[6];
            response[0] = ACTION_CODE_MINIGAME;
            response[1] = 0x15;
            response[2] = status;
            BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(3..5), itemId);
            response[5] = 1;
            session.Send(response);
        }
    }
}
```
