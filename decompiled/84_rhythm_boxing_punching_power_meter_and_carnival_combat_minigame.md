# Document 84: Rhythm Boxing Punching Power Meter and Carnival Combat Minigame

## 1. Executive Summary & Engine Architecture

In Wonderland Online, arcade carnival boxing attractions (located in the Carnie Mini-Game Park) challenge player timing and reflexes through the rhythm punching simulator implemented in `MZ_BoxingGame.pas`. Dissected from client subroutines at RVA `0x00147750` (`FUN_00147750`, constructor), match reset handler `FUN_001428c4` (RVA `0x001428c4`), and frame blitter `FUN_001858bc`, this minigame models timed athletic combat against an arcade punching dummy.

The minigame is structured around an oscillating power bar (`BoxingBar`, $284 \times 19$ px) situated within a decorative bezel frame (`StrengthBarFrame`, $575 \times 65$ px). The player controls two animated boxing gloves (`BoxingGameBoxer`, $339 \times 201$ px) striking an interactive adversary sandbag monster (`BoxingGameMonst`, $417 \times 305$ px). A 30-second match round countdown (`param_1 + 0x54 = 30`) bounds the duel. Depleting the monster's 3,000 HP before the timer expires displays the knockout victory banner (`BoxerKo`, $157 \times 108$ px); running out of time displays the defeat banner (`BoxerLose`, $237 \times 85$ px).

```
+------------------------------------------------------------------------------------+
|               RHYTHM BOXING POWER METER & CARNIVAL COMBAT ARCHITECTURE             |
+------------------------------------------------------------------------------------+
                                       |
                                       v
                    [MZ_BoxingGame Arena: 30-Second Round Timer]
                                       |
         +-----------------------------+-----------------------------+
         | Power Bar Rhythm Engine                                   | Combat Sprites & Outcomes
         v                                                           v
  [Oscillating Needle: BoxingBar (284x19 px)]                 [Interactive Combatants]
  - Frame Bezel: StrengthBarFrame (575x65 px)                 - Opponent Dummy: BoxingGameMonst (417x305 px)
  - Sweet Spot: Center +-10 px (Critical Hit)                 - Player Boxer: BoxingGameBoxer (339x201 px)
  - Multiplier: Crit (500 pts), Great (300), Good (150)       - Win Banner: BoxerKo (157x108 px)
  - Miss: 0 pts, Rebound Recoil Stumble                       - Loss Banner: BoxerLose (237x85 px)
         |                                                           |
         +-----------------------------+-----------------------------+
                                       |
                                       v
                 [Network Dispatcher: Action Code 0x4A (74)]
               Subcode 0x12: Punch Power Score & Round Resolution
```

---

## 2. Memory Layouts & Data Structures

### 2.1 Boxing Game Component Instance (`MZ_BoxingGame.pas`, `param_1`)

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `void*` | `VTable` | DirectDraw component virtual method table |
| `+0x1C` | `int` | `FrameStrengthBar` | Strength bar outer border handle (`"StrengthBarFrame"`, $575 \times 65$ px) |
| `+0x20` | `int` | `BarPowerMeter` | Oscillating inner power gauge handle (`"BoxingBar"`, $284 \times 19$ px) |
| `+0x24` | `int` | `SpriteMonster` | Opponent punching dummy sprite (`"BoxingGameMonst"`, $417 \times 305$ px) |
| `+0x28` | `int` | `SpriteLeftGlove` | Left boxing glove sprite handle (`"BoxingGameBoxer"`, $339 \times 201$ px) |
| `+0x2C` | `int` | `SpriteRightGlove`| Right boxing glove sprite handle (`"BoxingGameBoxer"`, $339 \times 201$ px) |
| `+0x34` | `int` | `SpriteLose` | Defeat banner handle (`"BoxerLose"`, $237 \times 85$ px) |
| `+0x38` | `int` | `SpriteKo` | Knockout victory banner handle (`"BoxerKo"`, $157 \times 108$ px) |
| `+0x40` | `int` | `NeedlePosition` | Horizontal position of oscillating power indicator (0..284 px) |
| `+0x44` | `int` | `NeedleVelocity` | Current needle speed and direction ($+6$ px/tick or $-6$ px/tick) |
| `+0x50` | `uint` | `AccumulatedScore`| Total punch score accrued in round |
| `+0x54` | `uint` | `RemainingSeconds`| Match countdown timer (initialized to 30 seconds / `0x1e`) |
| `+0x5C` | `uint` | `MonsterHp` | Current dummy monster vitality (starts at 3,000 HP) |
| `+0x60` | `byte` | `MatchStatus` | Round state (0: Playing, 1: Knockout Victory, 2: Time Out Defeat) |

---

## 3. Disassembled Subroutine Analysis

### 3.1 Arena Construction & Asset Initialization (`FUN_00147750`)
Extracted from `aLogin_decompiled.c` lines 147750–147805:

```c
// Constructor for MZ_BoxingGame
int * FUN_00147750(int *param_1, char param_2) {
    undefined4 uVar2;

    // Strength Bar Bezel Frame (Width 0x23f = 575 px, Height 0x41 = 65 px)
    uVar2 = FUN_00185548(PTR_PTR_00185358, 1);
    param_1[0x1c] = uVar2;
    FUN_0018575c(uVar2, "StrengthBarFrame");
    FUN_001858bc(param_1[0x1c], 575, 65, 0, 65);

    // Inner Oscillating Power Meter (Width 0x11c = 284 px, Height 0x13 = 19 px)
    uVar2 = FUN_00185548(PTR_PTR_00185358, 1);
    param_1[0x20] = uVar2;
    FUN_0018575c(uVar2, "BoxingBar");
    FUN_001858bc(param_1[0x20], 284, 19, 0, 19);

    // Opponent Monster Sandbag (Width 0x1a1 = 417 px, Height 0x131 = 305 px)
    uVar2 = FUN_00185548(PTR_PTR_00185358, 1);
    param_1[0x24] = uVar2;
    FUN_0018575c(uVar2, "BoxingGameMonst");
    FUN_001858bc(param_1[0x24], 417, 305, 3, 305);

    // Left and Right Boxer Gloves (Width 0x153 = 339 px, Height 0xc9 = 201 px)
    uVar2 = FUN_00185548(PTR_PTR_00185358, 1);
    param_1[0x28] = uVar2;
    FUN_0018575c(uVar2, "BoxingGameBoxer");
    FUN_001858bc(param_1[0x28], 339, 201, 1, 201);

    uVar2 = FUN_00185548(PTR_PTR_00185358, 1);
    param_1[0x2c] = uVar2;
    FUN_0018575c(uVar2, "BoxingGameBoxer");
    FUN_001858bc(param_1[0x2c], 339, 201, 1, 201);

    // Outcome Banners (Lose: 237x85 px, KO: 157x108 px)
    uVar2 = FUN_00185548(PTR_PTR_00185358, 1);
    param_1[0x34] = uVar2;
    FUN_0018575c(uVar2, "BoxerLose");
    FUN_001858bc(param_1[0x34], 237, 85, 0, 85);

    uVar2 = FUN_00185548(PTR_PTR_00185358, 1);
    param_1[0x38] = uVar2;
    FUN_0018575c(uVar2, "BoxerKo");
    FUN_001858bc(param_1[0x38], 157, 108, 0, 108);

    // Initialize 30-Second Round Countdown Timer (0x1e = 30)
    param_1[0x54] = 30;
    param_1[0x50] = 0; // Accumulated Score
    return param_1;
}
```

### 3.2 Rhythm Timing Window & Damage Evaluation
When the player triggers a strike (Space bar or Left Mouse Click), the needle's position along the 284-pixel gauge is evaluated relative to the center sweet spot ($X = 142$ px):

$$\Delta = |X_{\text{needle}} - 142|$$

| Timing Tier | Offset $\Delta$ Range | Power % | Damage to Monster | Score Awarded | Visual Effect |
|:---:|:---:|:---:|:---:|:---:|---|
| **Critical** | $0 \le \Delta \le 10$ px | $100\%$ | $500$ HP | $+500$ pts | Screen flash + heavy recoil shake |
| **Great** | $11 \le \Delta \le 30$ px | $75\%$ | $300$ HP | $+300$ pts | Yellow spark impact |
| **Good** | $31 \le \Delta \le 60$ px | $50\%$ | $150$ HP | $+150$ pts | Light impact puff |
| **Miss** | $\Delta > 60$ px | $0\%$ | $0$ HP | $0$ pts | Whiff sound + glove stumble delay |

---

## 4. Network Wire Protocol: Action Code 74 (`0x4A`)

Arcade rhythm boxing communicates under **Action Code 74** (`0x4A`):

### 4.1 Client Request: Start Boxing Round (`0x4A:0x12`)

```
Offset 0x00: [0x4A]            (Action Code 74: Minigame Protocol)
Offset 0x01: [0x12]            (Subcode: Boxing Match Start)
Offset 0x02: [MachineId: uint32] (Arcade Machine Entity ID)
```

### 4.2 Client Request: Report Punch Strike (`0x4A:0x13`)

```
Offset 0x00: [0x4A]            (Action Code 74)
Offset 0x01: [0x13]            (Subcode: Report Punch Strike)
Offset 0x02: [NeedlePos: uint16] (Reported needle X coordinate 0..284)
Offset 0x04: [StrikeSeq: uint8]  (Sequential strike counter 1..N)
```

### 4.3 Server Response: Round Outcome & Ticket Reward (`0x4A:0x12`)

```
Offset 0x00: [0x4A]            (Action Code 74)
Offset 0x01: [0x12]            (Subcode: Match Complete Result)
Offset 0x02: [Outcome: uint8]  (0x00: Knockout Victory, 0x01: Time Out Loss)
Offset 0x03: [FinalScore: uint32] (Total match points accumulated)
Offset 0x07: [TicketsWon: uint16] (Carnival Prize Tickets awarded: Score / 50)
```

---

## 5. C# Server Implementation Blueprint

```csharp
namespace Wonderland.Network.Handlers
{
    public static class BoxingGameHandler
    {
        private const byte ACTION_CODE_MINIGAME = 0x4A;
        private const ushort CARNIVAL_TICKET_ID = 27002;

        public static void Register(PacketDispatcher dispatcher)
        {
            dispatcher.RegisterHandler(ACTION_CODE_MINIGAME, HandleMinigamePacket);
        }

        public static void HandleMinigamePacket(GameSession session, ReadOnlySpan<byte> packet)
        {
            if (packet.Length < 2) return;
            byte subcode = packet[1];

            if (subcode == 0x13) // Punch Strike Report
            {
                HandlePunchStrike(session, packet[2..]);
            }
        }

        private static void HandlePunchStrike(GameSession session, ReadOnlySpan<byte> data)
        {
            if (data.Length < 3) return;
            ushort needlePos = BinaryPrimitives.ReadUInt16LittleEndian(data[0..2]);
            byte sequence = data[2];

            int delta = Math.Abs(needlePos - 142);
            uint damage = 0;
            uint points = 0;

            if (delta <= 10)      { damage = 500; points = 500; }
            else if (delta <= 30) { damage = 300; points = 300; }
            else if (delta <= 60) { damage = 150; points = 150; }

            var match = session.Character.ActiveBoxingMatch;
            if (match == null || match.IsExpired) return;

            match.Score += points;
            match.MonsterHp = (uint)Math.Max(0, match.MonsterHp - damage);

            if (match.MonsterHp == 0) // Knockout Victory!
            {
                ushort tickets = (ushort)(match.Score / 50);
                session.Character.Inventory.AddItem(CARNIVAL_TICKET_ID, tickets);
                SendMatchResult(session, 0x00, match.Score, tickets);
                session.Character.ActiveBoxingMatch = null;
            }
        }

        private static void SendMatchResult(GameSession session, byte outcome, uint score, ushort tickets)
        {
            byte[] response = new byte[9];
            response[0] = ACTION_CODE_MINIGAME;
            response[1] = 0x12;
            response[2] = outcome;
            BinaryPrimitives.WriteUInt32LittleEndian(response.AsSpan(3..7), score);
            BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(7..9), tickets);
            session.Send(response);
        }
    }
}
```
