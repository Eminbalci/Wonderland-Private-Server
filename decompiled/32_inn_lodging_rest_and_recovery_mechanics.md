# INN Lodging, Rest & Health Recovery Mechanics Specification

## 1. Executive Summary & Hospitality Subsystem

In Wonderland Online, towns and settlements feature traditional Hotels and Inns (客棧 / 旅社) where adventurers rest to restore depleted Hit Points (HP) and Skill Points (SP) without consuming expensive alchemical food or potions.

The INN system is driven by client unit `BK_INN.pas` and in-game form `Form_Inn_1` (dimensions $476 \times 472$ pixels), integrating with spatial panel `panel_restrainINN_2`.

---

## 2. INN Lodging Workflow & Lifecycle

```
+-----------------------------------------------------------------------------------+
|                            INN LODGING STATE MACHINE                              |
+-----------------------------------------------------------------------------------+
|                                                                                   |
|  [ 1. NPC Interaction ]                                                           |
|    - Player talks to Town Innkeeper NPC (Eve script invokes INN dialog)           |
|    - Client opens Form_Inn_1 (0x1DC width, 0x1D8 height)                          |
|                                                                                   |
|  [ 2. Room Tier & Fee Calculation ]                                               |
|    - Standard Room: Recovers Player only                                          |
|    - Deluxe Suite: Recovers Full 4-Player Party + Active Companion Pets           |
|    - Base Fee: Flat 100 Gold + (PlayerLV * 10)                                     |
|                                                                                   |
|  [ 3. Lodging Check-In & State Locking ]                                          |
|    - Server validates player wallet balance in inventory                          |
|    - Deducts gold fee and triggers sleeping transition animation                   |
|    - Activates panel_restrainINN_2 spatial constraint                             |
|    - Entity stance code *(actor + 0x121) sets to 16 (Sitting/Resting Pose)         |
|                                                                                   |
|  [ 4. Full Vitality Regeneration ]                                                |
|    - Screen fades to black (DirectDraw palette transition to 0x000000)            |
|    - Plays healing jingle SEB0010.wav or BGM snippet                              |
|    - Current HP restores to Max HP; Current SP restores to Max SP                 |
|    - All party companions also restore to full vitality                           |
|                                                                                   |
|  [ 5. Wake-Up & Movement Restoration ]                                            |
|    - Palette fades back to daytime scene lighting                                 |
|    - Restores normal walk stance *(actor + 0x121) = 0 (Idle)                       |
|    - Form_Inn_1 closes; control returned to player                                 |
|                                                                                   |
+-----------------------------------------------------------------------------------+
```

---

## 3. Spatial Restraint Panel (`panel_restrainINN_2`)

During the lodging sequence, the client instantiates `panel_restrainINN_2`:
- **Collision Lock:** Disables standard mouse click-to-move pathfinding while the sleep animation sequence executes.
- **Combat Immunity:** Prevents hostile PK challenges (`FUN_003a6f18`) or duel requests while resting inside an INN.
- **Trade Isolation:** Mutes incoming bilateral player trade invitations (`Form_BuyItemStall_1`).

---

## 4. Network Protocol Integration (Action Code 25)

The lodging transaction is coordinated via **Action Code 25 (`0x19`)**:
- `AC 25:1`: Check-In Lodging Request (`[RoomType: Byte, PartyFlag: Byte]`)
- `AC 25:2`: Check-In Confirmation & Fee Acknowledgment (`[Success: Byte, GoldCost: UInt32]`)
- `AC 25:3`: Vitality Restoration Notification (Triggers HUD HP/SP gauge sweep to 100%)
- `AC 25:4`: Wake-Up & Room Vacate Handshake
