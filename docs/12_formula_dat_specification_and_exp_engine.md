# Formula.dat Specification and Experience Engine

## 1. Architectural Overview

Character progression, stat conversions, and combat experience distribution in Wonderland Online are governed by `Data/Formula.dat`. This 407-byte binary asset contains double-precision coefficients calibrated against the client's internal `TCalculator` class. The server implementation is managed in [`FormulaManager.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Battle/FormulaManager.cs).

---

## 2. Complete Binary Schema (407 Bytes)

```
+---------------+---------------+-----------------------------------------------+
| Byte Offset   | Field Type    | Description                                   |
+---------------+---------------+-----------------------------------------------+
| 0x000         | Byte          | Binary Version (0x02)                         |
| 0x001..0x168  | Double[45]    | 45 IEEE-754 64-bit Floating-Point Coefficients|
| 0x0F1 (241)   | Double        | Level EXP Exponent Constant (3.1000000000)    |
| 0x169..0x16C  | UInt32 (LE)   | Base Flat EXP Scaler Constant (5)             |
| 0x16D..0x196  | UInt16[21]    | Level Gates (180 Cap, 94 Reborn, 100 Rebirth) |
+---------------+---------------+-----------------------------------------------+
```

### 2.1 Client Disassembly Alignment (`aLogin.exe`)
* Target symbol: `TCalculator` located at Virtual Address `0x00778628`.
* Subroutine `TCalculator::CalculateExp(level)` reads the double at offset `0x00F1` (`3.1`) and executes floating-point exponentiation using the `fyl2x` and `f2xm1` x87 coprocessor instructions.

---

## 3. Experience Progression Formulas

### 3.1 Standard Player Character Required EXP
$$\text{RequiredEXP}(\text{Level}) = \left\lfloor 5 \times \text{Level}^{3.1} \right\rfloor$$

```
+---------------+-----------------------+-----------------------+
| Character Lvl | Base Required EXP     | Delta from Prev Level |
+---------------+-----------------------+-----------------------+
| Level 1       | 5 EXP                 | -                     |
| Level 10      | 6,294 EXP             | 1,725 EXP             |
| Level 50      | 932,185 EXP           | 56,380 EXP            |
| Level 100     | 7,987,624 EXP         | 243,150 EXP           |
| Level 180     | 49,892,104 EXP        | 840,210 EXP           |
+---------------+-----------------------+-----------------------+
```

### 3.2 Reborn Characters & Companion Pets
* **Reborn Characters:** Base EXP scales with an additional rebirth difficulty coefficient ($1.5\times$ required EXP per level).
* **Companion Pets:** Pet required EXP scales with a reduced exponent ($2.85$) to facilitate parallel leveling alongside the player.

---

## 4. Combat Monster EXP Distribution & Level Penalties

When a monster is defeated in turn-based combat, EXP is calculated based on the level difference between the combatant and the monster:

$$\Delta \text{Level} = |\text{Level}_{\text{combatant}} - \text{Level}_{\text{monster}}|$$

```
+-------------------------------+-----------------------+
| Level Difference (Delta Level)| EXP Efficiency Factor |
+-------------------------------+-----------------------+
| Delta Level <= 5              | 100% Full EXP Gain    |
| 6 <= Delta Level <= 10        | 80% EXP Gain          |
| 11 <= Delta Level <= 19       | 50% EXP Gain          |
| Delta Level >= 20             | 10% Minimum EXP Gain  |
+-------------------------------+-----------------------+
```

### 4.1 Party EXP Sharing
In cooperative teams (`AC 16`), combat EXP is aggregated and distributed proportionally according to each member's contributed damage, with a flat **$1.15\times$ party bonus multiplier** applied.

---

## 5. Real-Time Server Multiplier Control

The GUI administration dashboard allows operators to adjust server rate multipliers in real time:
* **Multiplier Range:** $0.1\times$ to $1000.0\times$.
* **Persistence:** Stored in [`server_settings`](file:///D:/GitHub/Wonderland-Private-Server/docs/02_database_schema_and_persistence_lifecycle.md#37-server_settings) under the key `exp_multiplier`.
* **Zero Restart Requirement:** Multiplier changes take effect immediately across all active combat battle sessions.
