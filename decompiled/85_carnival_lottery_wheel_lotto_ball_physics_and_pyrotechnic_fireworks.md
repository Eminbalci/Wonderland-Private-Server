# Document 85: Carnival Lottery Wheel, Lotto Ball Physics, and Pyrotechnic Fireworks System

## 1. Executive Summary & Engine Architecture

In Wonderland Online, municipal lottery draws, weekly jackpot pools, and celebration pyrotechnics are driven by the mechanical lottery sphere and particle explosion systems implemented in `TMR_Lotto.pas` and `HL_NewFireworks.pas`. Dissected from client subroutines at RVA `0x00245258` (`FUN_003e2f0c`, lotto ball physics instantiator), draw step sequencer `FUN_0024d2c8` (RVA `0x0024d2c8`), ball coordinate projection `FUN_0024d3e0`, and fireworks particle spawner `FUN_003e2e64`, this subsystem models mechanical gambling and celebratory effects.

The lottery apparatus features a rotating wire-mesh lottery globe populated with numbered ping-pong ball entities (`Lottosball`). The engine simulates multi-body sphere collisions within a 3D-projected bounding volume, applying angular velocity decay before guiding 6 distinct winning balls down an extraction chute. Upon a winning jackpot draw, the client triggers the celebratory fireworks system (`HL_NewFireworks.pas`), launching multi-stage expanding particle bursts (`0x280b`) synchronized with pyrotechnic audio cues (`wav0150.wav`).

```
+------------------------------------------------------------------------------------+
|               LOTTERY WHEEL, BALL PHYSICS & FIREWORKS ARCHITECTURE                 |
+------------------------------------------------------------------------------------+
                                       |
                                       v
                    [TMR_Lotto Simulation: 6-Ball Extraction]
                                       |
         +-----------------------------+-----------------------------+
         | Rotational Tumbling Physics                               | Prize Verification & Fireworks
         v                                                           v
  [Lottosball Multi-Body Entities]                            [HL_NewFireworks Particle Bursts]
  - Numbers: 1 to 49 (6 Balls Drawn)                          - 1st Prize: 6 Matches (Jackpot 1M Gold)
  - Collision Bounds: FUN_003e2f0c Sphere                     - 2nd Prize: 5 Matches (200k Gold)
  - Chute Extractor: FUN_0024d2c8                             - 3rd Prize: 4 Matches (50k Gold)
  - CPU Rolling: "CPU rolled!" Prompt                         - Pyrotechnic Bursts: FUN_003e2e64 (wav0150)
         |                                                           |
         +-----------------------------+-----------------------------+
                                       |
                                       v
                 [Network Dispatcher: Action Code 0x4A (74)]
               Subcode 0x25: Lottery Ticket Registration & Draw
```

---

## 2. Memory Layouts & Data Structures

### 2.1 Lottery Ball Physics Entity (`Lottosball`, `param_1 + 0x17c`)

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `void*` | `VTable` | Physics entity virtual method table |
| `+0x10` | `int` | `BallNumber` | Face value of lottery ball (1..49) |
| `+0x14` | `float` | `PositionX` | Current 3D X coordinate inside sphere |
| `+0x18` | `float` | `PositionY` | Current 3D Y coordinate inside sphere |
| `+0x1C` | `float` | `PositionZ` | Current 3D Z depth coordinate inside sphere |
| `+0x20` | `float` | `VelocityX` | Horizontal tumbling momentum |
| `+0x24` | `float` | `VelocityY` | Vertical tumbling momentum |
| `+0x28` | `float` | `AngularSpin` | 3D spin angle for surface number rendering |
| `+0x34..0x40`| `int*[3]` | `pDrawnBalls` | Pointers to active extracted balls in chute |
| `+0x84..0x90`| `uint[3]` | `ChuteTargetX`| Screen X destination coordinates for extracted balls |
| `+0x90..0x9C`| `uint[3]` | `ChuteTargetY`| Screen Y destination coordinates for extracted balls |

### 2.2 Pyrotechnic Firework Particle Instance (`HL_NewFireworks.pas`)

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `ushort`| `ParticleType` | Visual burst asset ID (`0x280b`) |
| `+0x04` | `int` | `OriginX` | Burst center X pixel coordinate |
| `+0x08` | `int` | `OriginY` | Burst center Y pixel coordinate |
| `+0x0C` | `int` | `ParticleCount`| Number of expanding embers (typically 30..60) |
| `+0x10` | `float` | `ExpansionRate`| Radial velocity step multiplier (`0x3fd70a3d`) |
| `+0x14` | `byte` | `ColorPalette` | Particle color tint (0: Gold, 1: Crimson, 2: Azure, 3: Emerald) |

---

## 3. Disassembled Subroutine Analysis

### 3.1 Lottery Ball Physics Instantiation (`FUN_003e2f0c`)
Extracted from `aLogin_decompiled.c` lines 245250–245265:

```c
// Initialize 3D tumbling sphere and bind Lottosball entities
void FUN_003e2f0c(int lotto_container, char *entity_name, int radius, ...) {
    // Instantiate 3D wire-mesh bounding volume
    // Param: "Lottosball", Sphere Radius: 2000 units, Decay: 0x3fc99999 (0.80 friction)
    *(undefined4 *)(param_1 + 0x170) = 100; // 100 ms physics stepping tick
    *(undefined1 *)(param_1 + 0x180) = 1;   // Tumbling motor active
    *(undefined1 *)(param_1 + 0x181) = 1;   // Chute gate open
}
```

### 3.2 Draw Sequence & Ball Chute Extraction (`FUN_0024d2c8`)
Extracted from `aLogin_decompiled.c` lines 245310–245345:

```c
// Step sequence for extracting drawn lottery balls into the result chute
void FUN_0024d2c8(int param_1) {
    int iVar3;
    undefined4 uVar1, uVar2;

    if (*(char *)(param_1 + 0x85) == '\x01') {
        // Display floating status banner: "CPU rolled!"
        (**(code **)(**(int **)PTR_DAT_004c8d40 + 0x9c))(*(int **)PTR_DAT_004c8d40, "CPU rolled!", 0x4b0, 0, 0);
    }

    // Step physics and extract remaining balls
    FUN_0024d430(param_1);
    FUN_0024d220(param_1);

    iVar3 = 1;
    do {
        if (*(int *)(param_1 + 0x34 + iVar3 * 4) == 0) {
            uVar1 = FUN_003e2408(PTR_DAT_003e23a0, 1);
            *(undefined4 *)(param_1 + 0x34 + iVar3 * 4) = uVar1;

            // Project 3D chute destination into 2D screen coordinates
            uVar2 = FUN_0024d3e0(param_1, *(undefined4 *)(param_1 + 0x84 + iVar3 * 4), *(undefined1 *)(param_1 + 0xb0));
            uVar1 = FUN_0024d3e0(param_1, *(undefined4 *)(param_1 + 0x90 + iVar3 * 4), *(undefined1 *)(param_1 + 0xb0));

            // Trigger celebratory burst if jackpot ball lands
            FUN_003e2e64(*(undefined4 *)(param_1 + 0x34 + iVar3 * 4), 0x280b, 0x28, 0x70a3d70a, 0x3fd70a3d, 0, 0, 0x1e, 1, 0xff, uVar1, uVar2, 2, 0x14, 4, 1);
        }
        iVar3 = iVar3 + 1;
    } while (iVar3 != 4);
}
```

---

## 4. Network Wire Protocol: Action Code 74 (`0x4A`)

Lottery ticket registration and draw synchronization communicate under **Action Code 74** (`0x4A`):

### 4.1 Client Request: Purchase Lottery Ticket (`0x4A:0x25`)

```
Offset 0x00: [0x4A]            (Action Code 74: Minigame Protocol)
Offset 0x01: [0x25]            (Subcode: Lottery Ticket Purchase)
Offset 0x02: [Ball1: uint8]    (Chosen Number 1..49)
Offset 0x03: [Ball2: uint8]    (Chosen Number 1..49)
Offset 0x04: [Ball3: uint8]    (Chosen Number 1..49)
Offset 0x05: [Ball4: uint8]    (Chosen Number 1..49)
Offset 0x06: [Ball5: uint8]    (Chosen Number 1..49)
Offset 0x07: [Ball6: uint8]    (Chosen Number 1..49)
```

### 4.2 Server Response: Winning Draw & Prize Payout (`0x4A:0x25`)

```
Offset 0x00: [0x4A]            (Action Code 74)
Offset 0x01: [0x25]            (Subcode: Draw Result ACK)
Offset 0x02: [WinningBall1: uint8]
Offset 0x03: [WinningBall2: uint8]
Offset 0x04: [WinningBall3: uint8]
Offset 0x05: [WinningBall4: uint8]
Offset 0x06: [WinningBall5: uint8]
Offset 0x07: [WinningBall6: uint8]
Offset 0x08: [MatchCount: uint8]  (Number of matching picks 0..6)
Offset 0x09: [PrizeGold: uint32]   (Gold currency awarded)
```

---

## 5. C# Server Implementation Blueprint

```csharp
namespace Wonderland.Network.Handlers
{
    public static class LotteryGameHandler
    {
        private const byte ACTION_CODE_MINIGAME = 0x4A;
        private const uint TICKET_PRICE = 1000; // 1,000 Gold per ticket

        public static void Register(PacketDispatcher dispatcher)
        {
            dispatcher.RegisterHandler(ACTION_CODE_MINIGAME, HandleMinigamePacket);
        }

        public static void HandleMinigamePacket(GameSession session, ReadOnlySpan<byte> packet)
        {
            if (packet.Length < 2) return;
            byte subcode = packet[1];

            if (subcode == 0x25) // Lottery Ticket Submission
            {
                HandleLotteryPurchase(session, packet[2..]);
            }
        }

        private static void HandleLotteryPurchase(GameSession session, ReadOnlySpan<byte> data)
        {
            if (data.Length < 6) return;
            byte[] chosenPicks = data[..6].ToArray();

            var character = session.Character;
            if (character.Gold < TICKET_PRICE)
            {
                session.SendSystemNotice("Insufficient gold to buy a lottery ticket.");
                return;
            }

            character.Gold -= TICKET_PRICE;

            // Generate 6 unique winning balls (1..49)
            var winningBalls = GenerateWinningDraw();
            int matches = chosenPicks.Intersect(winningBalls).Count();

            uint prize = matches switch
            {
                6 => 1_000_000, // Jackpot!
                5 => 200_000,
                4 => 50_000,
                3 => 5_000,
                _ => 0
            };

            if (prize > 0)
            {
                character.Gold += prize;
                if (matches >= 5)
                {
                    WorldManager.BroadcastSystemNotice($"{character.Name} matched {matches} balls and won {prize:N0} Gold in the Lottery!");
                }
            }

            SendLotteryResult(session, winningBalls, (byte)matches, prize);
        }

        private static byte[] GenerateWinningDraw()
        {
            return Enumerable.Range(1, 49)
                .OrderBy(_ => Random.Shared.Next())
                .Take(6)
                .Select(b => (byte)b)
                .ToArray();
        }

        private static void SendLotteryResult(GameSession session, byte[] balls, byte matches, uint prize)
        {
            byte[] response = new byte[13];
            response[0] = ACTION_CODE_MINIGAME;
            response[1] = 0x25;
            balls.CopyTo(response.AsSpan(2..8));
            response[8] = matches;
            BinaryPrimitives.WriteUInt32LittleEndian(response.AsSpan(9..13), prize);
            session.Send(response);
        }
    }
}
```
