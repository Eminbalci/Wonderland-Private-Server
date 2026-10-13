# 60 - Carnival Tabletop: Eight-Twelve, Magnet, and EXP Box Engines Specification

## 1. Architectural Overview & Source Mapping

Building upon the carnival and training mini-games detailed in Document 38, Wonderland Online features specialized arcade attractions and gambling tabletop games implemented across `RE_SGame_BiDaXiao.pas`, `KMK_MiniGame.pas`, `CH_MiniGame.pas`, and `MZ_MiniGame.pas`:
1. **Eight-Twelve (八十二骰寶):** `Form_EightTwelve` ($525 \times 409$ px) dice wager game.
2. **Magnet Attraction Game (磁鐵尋寶):** `Form_Magnet_1` ($399 \times 228$ px) physics attractor.
3. **EXP Box Multiplier (經驗儲蓄盒):** `Form_ExpBox` ($195 \times 223$ px) experience accumulation vault.
4. **Memory Card Match 2 (翻牌記憶二代):** `Form_MemCard2` ($397 \times 371$ px) pair matching puzzle.

### Subroutine Inventory & Source Locations

| Virtual Address | Subroutine Symbol | File / Unit | Purpose |
| :--- | :--- | :--- | :--- |
| `0x00189593` | `FUN_001af200` | `RE_SGame_BiDaXiao.pas` | `Form_EightTwelve` constructor ($525 \times 409$ px) |
| `0x001af25c` | `FUN_001af25c` | `RE_SGame_BiDaXiao.pas` | Eight-Twelve dice roll arbitrator and winning sum calculator |
| `0x001af664` | `LAB_001af664` | `RE_SGame_BiDaXiao.pas` | Dice wager slot selection and ante token allocator |
| `0x001af764` | `FUN_001af764` | `RE_SGame_BiDaXiao.pas` | Wager submission and Action Code 74:0x12 packet transmitter |
| `0x00195162` | `FUN_00195162` | `KMK_MiniGame.pas` | `Form_Magnet_1` magnet treasure attraction constructor ($399 \times 228$ px) |
| `0x00197581` | `FUN_00197581` | `CH_MiniGame.pas` | `Form_ExpBox` experience storage vault constructor ($195 \times 223$ px) |
| `0x00193331` | `FUN_00193331` | `MZ_Doll.pas` | `Form_MemCard2` memory puzzle card flip evaluator ($397 \times 371$ px) |

---

## 2. Eight-Twelve Dice Engine (`Form_EightTwelve`)

The Eight-Twelve game pits the player against an automated banker using three 6-sided dice.

```
+-------------------------------------------------------------+
| [Dice Cup Icon] Eight-Twelve Dice Wager Arena [Btn_Close_S_1]|
+-------------------------------------------------------------+
|  Dice Total Rules:                                          |
|  - Small (Under 8): 1x Payout                               |
|  - Eight - Twelve (8 to 12): 2x Payout                      |
|  - Big (Over 12): 1x Payout                                 |
|  - Triple Match: 10x Grand Payout                           |
|                                                             |
|  Betting Grid (10 Wager Slots):                             |
|  +-------------------------------------------------------+  |
|  | [Slot 1: Small]  [Slot 2: 8-12]  [Slot 3: Big]        |  |
|  | [Slot 4: Triple 1] [Slot 5: Triple 2] ...             |  |
|  +-------------------------------------------------------+  |
|  Ante Tokens: [ 100 Tokens ]    Potential Payout: [ 200 ]   |
|                                                             |
|         [Btn_Buy_1 (Roll Dice)]        [Btn_Close_1 (Exit)] |
+-------------------------------------------------------------+
```

### Probability & Payout Engine (`FUN_001af25c`)

Three 6-sided dice yield $6^3 = 216$ outcomes:
- **Small ($\Sigma \le 7$):** 35 outcomes ($16.2\%$), $1:1$ payout.
- **Mid-Range Eight to Twelve ($8 \le \Sigma \le 12$):** 146 outcomes ($67.6\%$), $1:2$ payout or variable stake.
- **Big ($\Sigma \ge 13$):** 35 outcomes ($16.2\%$), $1:1$ payout.
- **Any Triple ($D_1 == D_2 == D_3$):** 6 outcomes ($2.78\%$), $10:1$ jackpot.

---

## 3. Magnet Treasure Attraction Game (`Form_Magnet_1`)

`Form_Magnet_1` ($399 \times 228$ px) is an interactive physics game where players control an electromagnetic crane arm to extract submerged iron chests:
- **Magnetic Flux Register:** `*(actor + 0x180)` accumulates pull force based on button mash frequency.
- **Depth Resistance:** Drag resistance increases with chest depth ($F_{\text{drag}} = k \cdot Y$).
- **Rewards:** Alchemy iron ores, ancient copper coins, or rusted repair hammers.

---

## 4. Experience Storage Box (`Form_ExpBox`)

`Form_ExpBox` ($195 \times 223$ px) allows players to bank active combat experience into a portable tradeable consumable capsule:
- **Storage Capacity:** Clamped at **1,000,000 EXP** per capsule.
- **Extraction Tariff:** The bank charges a flat **$10\%$ experience tax** upon bottling (e.g. depositing 100,000 EXP stores 90,000 EXP into the capsule item).
- **Consumable Item Injection:** Upon full extraction, the player receives item ID `EXP_Pill` in their inventory.

---

## 5. Wire Protocol & Network Framing: Action Code 74 (`0x4A`)

Mini-games communicate through **Action Code 74 (`0x4A`)**.

### Mini-Game Protocol Matrix

| Action Code | Sub-Opcode | Direction | Description | Payload Schema |
| :---: | :---: | :---: | :--- | :--- |
| `0x4A` | `0x10` | C $\to$ S | Mini-Game Start Request | `[GameTypeID (1B)] [AnteTokens (2B)]` |
| `0x4A` | `0x11` | S $\to$ C | Game Initialized State | `[GameSessionID (4B)] [InitialSeed (4B)]` |
| `0x4A` | `0x12` | C $\to$ S | Submit Wager / Roll | `[SessionID (4B)] [WagerSlot (1B)] [Stake (2B)]` |
| `0x4A` | `0x13` | S $\to$ C | Roll Result & Payout | `[Dice1 (1B)] [Dice2 (1B)] [Dice3 (1B)] [Payout (4B)]` |
| `0x4A` | `0x15` | C $\to$ S | Magnet Crane Release | `[ReleaseX (2B)] [FluxCharge (2B)]` |
| `0x4A` | `0x18` | C $\to$ S | Bottle EXP Box | `[ExpAmount (4B)]` |

---

## 6. Private Server Implementation Requirements

1. **Cryptographic Seed Randomization:** Roll outcomes must be generated entirely server-side using secure RNG (`RandomNumberGenerator`), ensuring client state cannot manipulate dice values or crane collisions.
2. **Token Economy Balance:** Validate token purse balances before decrementing stakes and crediting winnings.
3. **EXP Box Boundary Checks:** Ensure players cannot deposit negative EXP or drain character levels below the current level baseline.
