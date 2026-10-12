# Actor Memory Layout and Spatial Systems Specification

## 1. Character & Actor Memory Layout

The client maintains active scene actors (the local player `DAT_006c7368`, remote players, companion pets, and NPCs) within structured heap blocks:

| Offset | Type | Field Name | Functional Meaning & Behavioral Enforcement |
|---|---|---|---|
| `+0x20` | `int` | `PositionX` | Current pixel X coordinate |
| `+0x24` | `int` | `PositionY` | Current pixel Y coordinate |
| `+0xe0` | `int` | `VisualX` | Sprite rendering visual X coordinate (mount aligned) |
| `+0xe4` | `int` | `VisualY` | Sprite rendering visual Y coordinate (mount aligned) |
| `+0x1eec` | `int` | `VisibilityState` | Entity visibility (`2 = Hidden / Suppressed`, `1 = Normal`) |
| `+0x1efc` | `byte` | `MotionState` | Movement / animation state index |
| `+0x1eff` | `byte` | `TeamMode` | Team / Party status (`0x02` indicates player is in party) |
| `+0x1f16` | `byte` | `SpecialState` | Special effect state (`0x04` = Cupid state; blocks PK) |
| `+0x2091` | `byte` | `BusyFlag` | Interaction busy flag (trading, dialogue, cutscene) |
| `+0x20b5` | `byte` | `FishingFlag` | If `0x01`, character is currently fishing |
| `+0x20eb` | `byte` | `BathingFlag` | If `0x01`, character is bathing in hot spring |
| `+0x20f6` | `byte` | `CollectingFlag` | If `0x01`, character is actively gathering resources |
| `+0x21bc` | `int` | `PartyLeaderID` | Character ID of active party leader |
| `+0x2231` | `byte` | `StallState1` | Active item vending stall flag |
| `+0x2232` | `byte` | `StallState2` | Active pet vending stall flag |
| `+0x2233` | `byte` | `StallState3` | Auxiliary stall configuration flag |
| `+0x235c` | `short` | `MapPKStatus` | Map PK enablement / target identifier |
| `+0x27e8` | `pointer` | `MountCoords` | Base pointer array for mounted companion coordinates |
| `+0x295c` | `pointer` | `BankStorage` | Base pointer array for personal banking vault storage |

---

## 2. Spatial Thresholds & Proximity Radii

The client strictly checks geometric distances before executing interactions:

### 2.1 Player Proximity Circle (`FUN_0016359c`)
* **Detection Radius:** 200 Pixels.
* **Mechanism:**
  ```c
  hrgn = CreateEllipticRgn(PlayerX - 200, PlayerY - 200, PlayerX + 200, PlayerY + 200);
  PtInRegion(hrgn, TargetX, TargetY);
  ```
* **Purpose:** Detects entities entering the player's immediate interactive proximity sphere.

### 2.2 NPC Dialogue Range (`FUN_0031d874`)
* **Maximum Distance Delta:** $\le 169$ Pixels (`0xA9`).
* **Check Logic:**
  $$\Delta X = |X_{\text{player}} - X_{\text{npc}}| \le 169 \quad \text{and} \quad \Delta Y = |Y_{\text{player}} - Y_{\text{npc}}| \le 169$$
* **Action:** If either delta exceeds 169 pixels, click interaction is aborted without sending packet to server.

### 2.3 Player-Killing (PK) Challenge Distance (`FUN_003a7154`)
* **Maximum Combat Delta:** $< 271$ Pixels (`0x10F`).
* **Check Logic:**
  $$\Delta X = |X_{\text{player}} - X_{\text{target}}| < 271 \quad \text{and} \quad \Delta Y = |Y_{\text{player}} - Y_{\text{target}}| < 271$$
* **Action:** Prevents duel requests against targets outside visual combat range.

---

## 3. MapData File Loading & XOR Cryptography (`FUN_0032cd88`)

Terrain tiles, collision blocks, and obstacle flags are loaded from individual map files located in `user\Map\`:

* **File Path Pattern:** `user\Map\<XOR_MapID>.MapData`
* **XOR Decryption Key:** `0x190C` (6412 decimal).
* **Record Structure:** 36-byte records (`0x24`) per grid block containing tile sprite indices, walkability flags, and elevation markers.
* **Block Scaling:** Standard block dimension = 20 pixels (`BlockSize` at `+0x38`).
