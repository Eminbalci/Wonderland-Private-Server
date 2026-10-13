# Document 86: Memory Card Matching, Poker Concentration Minigame, and Deck Shuffling Engine

## 1. Executive Summary & Engine Architecture

In Wonderland Online, carnival memory card matching and concentration challenges are driven by the card flip state machine implemented in `LD_PokeGame.pas`. Dissected from client subroutines at RVA `0x001839ab` (engine constructor), board cleaner `FUN_00183aa0` (RVA `0x00183aa0`), card texture binding loop (RVA `0x00168958`), and card pair comparison handler `FUN_00183ae8`, this minigame tests player visual memory against randomized card decks.

The minigame renders an 8-card tabletop array ($4 \times 2$ grid) blitted over the custom background surface `PokeGameBG`. The card deck comprises 15 distinct card face values (`PokeCard_1` through `PokeCard_15`) masked by an opaque card back texture (`PokeCardBack`). Players click two face-down cards to reveal their faces. If matching, the engine flashes the success emblem (`Icon_Currect_1`) and locks the cards face-up; if mismatched, the failure emblem (`Icon_Miss_1`) displays for 1,000 ms before the cards flip back face-down. Completing all pairs awards Carnival Prize Tickets via Action Code `0x4A` (`74` decimal) Subcode `0x1B`.

```
+------------------------------------------------------------------------------------+
|               MEMORY CARD MATCHING & CONCENTRATION GAME ARCHITECTURE               |
+------------------------------------------------------------------------------------+
                                       |
                                       v
                    [LD_PokeGame Tabletop Canvas: PokeGameBG]
                     8-Card Grid Layout (4 Columns x 2 Rows)
                                       |
         +-----------------------------+-----------------------------+
         | Card Deck Shuffling & Textures                            | Pair Match Evaluation & Feedback
         v                                                           v
  [Card Deck: PokeCardBack + PokeCard_1..15]                  [Pair Verification Logic]
  - 15 Card Faces (PokeCard_1..PokeCard_15)                   - Click 1: Reveal First Card
  - Deck Size: 4 Matching Pairs (8 Cards Total)               - Click 2: Reveal Second Card & Compare
  - Mask: PokeCardBack                                        - Match: Icon_Currect_1 (Lock Face-Up)
  - Banner: Poke_Text_Tc                                      - Miss: Icon_Miss_1 (1000 ms Flip Back)
         |                                                           |
         +-----------------------------+-----------------------------+
                                       |
                                       v
                 [Network Dispatcher: Action Code 0x4A (74)]
               Subcode 0x1B: Card Flip Action & Round Complete Result
```

---

## 2. Memory Layouts & Data Structures

### 2.1 8-Card Playing Board Layout (`DAT_006dbbd4 + 0x134 + slot * 4`)

| Slot Index | Grid Position | Screen X Coordinate | Screen Y Coordinate | Card Value State |
|:---:|:---:|:---:|:---:|---|
| `1` | Row 1, Col 1 | $120$ px | $140$ px | Card ID (1..15) or 0 (Face-down) |
| `2` | Row 1, Col 2 | $210$ px | $140$ px | Card ID (1..15) or 0 (Face-down) |
| `3` | Row 1, Col 3 | $300$ px | $140$ px | Card ID (1..15) or 0 (Face-down) |
| `4` | Row 1, Col 4 | $390$ px | $140$ px | Card ID (1..15) or 0 (Face-down) |
| `5` | Row 2, Col 1 | $120$ px | $260$ px | Card ID (1..15) or 0 (Face-down) |
| `6` | Row 2, Col 2 | $210$ px | $260$ px | Card ID (1..15) or 0 (Face-down) |
| `7` | Row 2, Col 3 | $300$ px | $260$ px | Card ID (1..15) or 0 (Face-down) |
| `8` | Row 2, Col 4 | $390$ px | $260$ px | Card ID (1..15) or 0 (Face-down) |

### 2.2 Card Game Component Instance (`LD_PokeGame.pas`, `param_1`)

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `void*` | `VTable` | DirectDraw component virtual method table |
| `+0x04` | `uint` | `TextureCardBack`| Card back texture handle (`"PokeCardBack"`) |
| `+0x08` | `uint` | `TextureTableBg` | Green felt table background handle (`"PokeGameBG"`) |
| `+0x0C..0x48`| `uint[15]`| `TextureCardFaces`| Array of 15 card face textures (`"PokeCard_1"`..`"PokeCard_15"`) |
| `+0x4C` | `uint` | `IconCorrect` | Green checkmark match sprite handle (`"Icon_Currect_1"`) |
| `+0x50` | `uint` | `IconMiss` | Red cross mismatch sprite handle (`"Icon_Miss_1"`) |
| `+0x54` | `uint` | `TextureBanner` | Traditional Chinese game title banner (`"Poke_Text_Tc"`) |
| `+0x6A` | `byte` | `FirstCardSlot` | Currently selected first card index (1..8) |
| `+0x6B` | `byte` | `SecondCardSlot`| Currently selected second card index (1..8) |
| `+0x70` | `byte` | `MatchedPairs` | Count of successfully resolved pairs (0..4) |
| `+0x74` | `uint` | `FlipBackTimer` | Millisecond timer for delayed card flip-back on mismatch |
| `+0x78` | `byte` | `IsInputLocked` | Input freeze mutex during card comparison animation |

---

## 3. Disassembled Subroutine Analysis

### 3.1 Card Game Construction & Asset Registration (`FUN_001839ab`)
Extracted from `aLogin_decompiled.c` lines 168935–169002:

```c
// Constructor for LD_PokeGame
int * FUN_001839ab(int *param_1, char param_2) {
    uint uVar1;
    byte bVar4;

    FUN_003d846c(param_1, 0);
    FUN_0018366c(DAT_006dbbd4, 0);

    // Register Background & Texture Surfaces
    FUN_0047c044(*(undefined4 *)PTR_DAT_004c8a94, "PokeGameBG", 1, 1, 0);
    param_1[2] = FUN_00479a24(*(undefined4 *)PTR_DAT_004c8a94, "PokeGameBG");

    FUN_0047c044(*(undefined4 *)PTR_DAT_004c8a94, "PokeCardBack", 0, 1, 0);
    param_1[1] = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "PokeCardBack");

    // Success and Miss Indicator Sprites
    FUN_0047c044(*(undefined4 *)PTR_DAT_004c8a94, "Icon_Currect_1", 0, 1, 0);
    param_1[0x13] = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "Icon_Currect_1");

    FUN_0047c044(*(undefined4 *)PTR_DAT_004c8a94, "Icon_Miss_1", 0, 1, 0);
    param_1[0x14] = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "Icon_Miss_1");

    // Title banner
    param_1[0x15] = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, "Poke_Text_Tc");

    // Loop 1..15: Preload 15 card face sprites (PokeCard_1..PokeCard_15)
    bVar4 = 1;
    do {
        FUN_0001953c((uint)bVar4, &local_14);
        FUN_00014178(&local_10, "PokeCard_", local_14);
        FUN_0047c044(*(undefined4 *)PTR_DAT_004c8a94, local_10, 0);
        param_1[3 + (uint)bVar4] = FUN_0047885c(*(undefined4 *)PTR_DAT_004c8a94, local_10);
        bVar4 = bVar4 + 1;
    } while (bVar4 != 16);

    DAT_006dbbd8 = 8; // 8 active card positions
    return param_1;
}
```

### 3.2 Board State Reset (`FUN_00183aa0`)
Extracted from `aLogin_decompiled.c` lines 169024–169040:

```c
// Reset 8 board slots to face-down state
void FUN_00183aa0(int param_1) {
    byte bVar1 = 1;
    *(undefined4 *)(param_1 + 0x74) = 0; // Reset flip timer
    *(undefined1 *)(param_1 + 0x6a) = 0; // Reset slot 1
    *(undefined1 *)(param_1 + 0x72) = 0; // Reset slot 2

    do {
        // Zero card face indicator across all 8 active positions
        *(undefined4 *)(*(int *)(DAT_006dbbd4 + 0x134 + (uint)bVar1 * 4) + 0xc0) = 0;
        bVar1 = bVar1 + 1;
    } while (bVar1 != 9);

    *(undefined1 *)(DAT_006dbbd4 + 0x158) = 0;
    DAT_006dbbd8 = 0;
}
```

---

## 4. Network Wire Protocol: Action Code 74 (`0x4A`)

Concentration card game play communicates under **Action Code 74** (`0x4A`):

### 4.1 Client Request: Flip Card (`0x4A:0x1B`)

```
Offset 0x00: [0x4A]            (Action Code 74: Minigame Protocol)
Offset 0x01: [0x1B]            (Subcode: Flip Card Request)
Offset 0x02: [SlotIndex: uint8] (Target Card Position 1..8)
```

### 4.2 Server Response: Reveal Card Face & Match Evaluation (`0x4A:0x1B`)

```
Offset 0x00: [0x4A]            (Action Code 74)
Offset 0x01: [0x1B]            (Subcode: Card Flip Result)
Offset 0x02: [SlotIndex: uint8] (Flipped Slot 1..8)
Offset 0x03: [CardFaceId: uint8](Revealed Card Face ID 1..15)
Offset 0x04: [ResultCode: uint8](0x00: First Card Picked, 0x01: Match Success, 0x02: Mismatch, 0x03: Board Cleared)
Offset 0x05: [TicketsWon: uint16](Carnival Tickets awarded if ResultCode == 0x03)
```

---

## 5. C# Server Implementation Blueprint

```csharp
namespace Wonderland.Network.Handlers
{
    public static class PokerConcentrationHandler
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

            if (subcode == 0x1B) // Card Flip Action
            {
                HandleCardFlip(session, packet[2..]);
            }
        }

        private static void HandleCardFlip(GameSession session, ReadOnlySpan<byte> data)
        {
            if (data.Length < 1) return;
            byte slotIndex = data[0]; // 1..8

            var match = session.Character.ActiveConcentrationMatch;
            if (match == null || slotIndex < 1 || slotIndex > 8) return;

            byte cardFace = match.GetCardFace(slotIndex);

            if (match.FirstCardSlot == 0)
            {
                // First card chosen
                match.FirstCardSlot = slotIndex;
                SendFlipResponse(session, slotIndex, cardFace, 0x00, 0);
            }
            else if (match.FirstCardSlot != slotIndex)
            {
                // Second card chosen -> evaluate pair
                byte firstSlot = match.FirstCardSlot;
                byte firstCard = match.GetCardFace(firstSlot);
                match.FirstCardSlot = 0;

                if (firstCard == cardFace)
                {
                    match.ResolvedPairs++;
                    if (match.ResolvedPairs == 4) // All 4 pairs cleared!
                    {
                        ushort tickets = 50;
                        session.Character.Inventory.AddItem(CARNIVAL_TICKET_ID, tickets);
                        SendFlipResponse(session, slotIndex, cardFace, 0x03, tickets);
                        session.Character.ActiveConcentrationMatch = null;
                    }
                    else
                    {
                        SendFlipResponse(session, slotIndex, cardFace, 0x01, 0); // Pair match
                    }
                }
                else
                {
                    SendFlipResponse(session, slotIndex, cardFace, 0x02, 0); // Mismatch
                }
            }
        }

        private static void SendFlipResponse(GameSession session, byte slot, byte face, byte result, ushort tickets)
        {
            byte[] response = new byte[7];
            response[0] = ACTION_CODE_MINIGAME;
            response[1] = 0x1B;
            response[2] = slot;
            response[3] = face;
            response[4] = result;
            BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(5..7), tickets);
            session.Send(response);
        }
    }
}
```
