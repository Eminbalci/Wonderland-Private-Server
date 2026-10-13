# Sprite Heading Direction & Motion State Resolver

## 1. Executive Summary & Coordinate System

Wonderland Online uses an isometric, 2.5D tile-based graphics renderer driven by DirectDraw (`DXDraw1`). Character and NPC sprites consist of multi-directional frame strips packed inside `odd.dat` and `Eve.emg`.

To map high-level entity motion commands to exact graphic sprite sub-frames, [`aLogin.exe`](file:///D:/GitHub/ghidradecodeyapma/WLRI/aLogin.exe) implements a dual-stage decomposition pipeline:
1. `FUN_00440308` (RVA `0x00440308`): **Heading Direction Isolator**, which maps the composite state code `*(actor + 0x121)` to the 8-directional isometric angle ($0..7$).
2. `FUN_00440fec` (RVA `0x00440fec`): **Motion Base Normalizer**, which strips directional offsets to return the canonical animation action category ($0, 8, 16, 18\dots$).

---

## 2. 8-Directional Isometric Heading System

The client engine aligns the Cartesian screen coordinate plane to 8 discrete compass headings:

```
                  North [4]
          (-X, -Y)   ^   (+X, -Y)
                    / \
      NW [3]       /   \       NE [5]
                  /     \
    West [2] <---+   +   +---> East [6]
                  \     /
      SW [1]       \   /       SE [7]
                    \ /
          (-X, +Y)   v   (+X, +Y)
                  South [0]
```

| Heading Code | Compass Direction | Screen Pixel Delta Vector | Typical Isometric Usage |
|---|---|---|---|
| `0` | **South (S)** | $(\Delta X = 0, \Delta Y > 0)$ | Facing directly towards the camera |
| `1` | **Southwest (SW)** | $(\Delta X < 0, \Delta Y > 0)$ | Diagonal bottom-left movement |
| `2` | **West (W)** | $(\Delta X < 0, \Delta Y = 0)$ | Horizontal left movement |
| `3` | **Northwest (NW)** | $(\Delta X < 0, \Delta Y < 0)$ | Diagonal top-left movement |
| `4` | **North (N)** | $(\Delta X = 0, \Delta Y < 0)$ | Facing directly away from camera |
| `5` | **Northeast (NE)** | $(\Delta X > 0, \Delta Y < 0)$ | Diagonal top-right movement |
| `6` | **East (E)** | $(\Delta X > 0, \Delta Y = 0)$ | Horizontal right movement |
| `7` | **Southeast (SE)** | $(\Delta X > 0, \Delta Y > 0)$ | Diagonal bottom-right movement |

---

## 3. Motion Action State Machine (`*(actor + 0x121)`)

The composite state code stored at entity memory offset `+0x121` packages both the animation action type and the facing heading. The table below details how `FUN_00440308` and `FUN_00440fec` decouple these fields:

| Composite Range | Normalized Action (`FUN_00440fec`) | Heading Formula (`FUN_00440308`) | Stance / Animation Semantics | Directional Granularity |
|---|---|---|---|---|
| `0 .. 7` | `0` (Idle) | `val - 0` | Idle standing / Breathing loop | Full 8 Directions |
| `8 .. 15` | `8` (Walk) | `val - 8` | Isometric movement / Running cycle | Full 8 Directions |
| `16 .. 17` (`0x10`) | `16` (Sit) | `val - 16` | Sitting on floor, chair, or tent mat | 2 Directions (SW / SE) |
| `18 .. 25` (`0x12`) | `18` (Battle) | `val - 18` | Turn-based combat ready stance | Full 8 Directions |
| `26 .. 27` (`0x1A`) | `26` (Attack 1) | `val - 26` | Physical attack primary strike | 2 Directions (Battle Facing) |
| `28 .. 29` (`0x1C`) | `28` (Attack 2) | `val - 28` | Heavy strike / Dual-weapon attack | 2 Directions (Battle Facing) |
| `30 .. 31` (`0x1E`) | `30` (Magic) | `val - 30` | Skill chanting / Magic cast glow | 2 Directions (Battle Facing) |
| `32 .. 33` (`0x20`) | `32` (Defend) | `val - 32` | Defensive guard / Shield block | 2 Directions (Battle Facing) |
| `34 .. 35` (`0x22`) | `34` (Hurt) | `val - 34` | Hit recoil / Damage flinch | 2 Directions (Battle Facing) |
| `36 .. 37` (`0x24`) | `36` (Faint) | `val - 36` | Knockout / Incapacitated on ground | 2 Directions (Battle Facing) |
| `38 .. 39` (`0x26`) | `38` (Victory) | `val - 38` | Post-battle victory cheer / Emote | 2 Directions (Battle Facing) |
| `40 .. 41` (`0x28`) | `40` (Special) | `val - 40` | Custom emote / Crafting posture | 2 Directions (Battle Facing) |
| `42 .. 43` (`0x2A`) | `42` (Mounted) | `val - 42` | Riding companion pet or vehicle | 2 Directions (Battle Facing) |

---

## 4. Decompiled Subroutine Implementations

### 4.1 Direction Isolator (`FUN_00440308`)
```c
// Decompiled from aLogin_decompiled.c lines 453187-453255
uint FUN_00440308(undefined4 param_1, uint param_2)
{
    char c = (char)param_2;
    switch(param_2) {
    case 0: case 1: case 2: case 3: case 4: case 5: case 6: case 7:
        return param_2;                 // Heading 0..7
    case 8: case 9: case 10: case 11: case 12: case 13: case 14: case 15:
        return (uint)(byte)(c - 8);     // Heading 0..7
    case 16: case 17:
        return (uint)(byte)(c - 16);    // Heading 0..1
    case 18: case 19: case 20: case 21: case 22: case 23: case 24: case 25:
        return (uint)(byte)(c - 18);    // Heading 0..7
    case 26: case 27:
        return (uint)(byte)(c - 26);    // Heading 0..1
    case 28: case 29:
        return (uint)(byte)(c - 28);    // Heading 0..1
    case 30: case 31:
        return (uint)(byte)(c - 30);    // Heading 0..1
    case 32: case 33:
        return (uint)(byte)(c - 32);    // Heading 0..1
    case 34: case 35:
        return (uint)(byte)(c - 34);    // Heading 0..1
    case 36: case 37:
        return (uint)(byte)(c - 36);    // Heading 0..1
    case 38: case 39:
        return (uint)(byte)(c - 38);    // Heading 0..1
    case 40: case 41:
        return (uint)(byte)(c - 40);    // Heading 0..1
    case 42: case 43:
        return (uint)(byte)(c - 42);    // Heading 0..1
    default:
        return 0;
    }
}
```

### 4.2 Base Action Normalizer (`FUN_00440fec`)
```c
// Decompiled from aLogin_decompiled.c lines 453607-453665
undefined4 FUN_00440fec(undefined4 param_1, undefined4 param_2)
{
    switch(param_2) {
    case 0: case 1: case 2: case 3: case 4: case 5: case 6: case 7:
        return 0;      // Base Idle Action
    case 8: case 9: case 10: case 11: case 12: case 13: case 14: case 15:
        return 8;      // Base Walk Action
    case 16: case 17:
        return 16;     // Base Sit Action
    case 18: case 19: case 20: case 21: case 22: case 23: case 24: case 25:
        return 18;     // Base Combat Stance Action
    case 26: case 27:
        return 26;     // Base Attack 1 Action
    case 28: case 29:
        return 28;     // Base Attack 2 Action
    case 30: case 31:
        return 30;     // Base Magic Action
    case 32: case 33:
        return 32;     // Base Defend Action
    case 34: case 35:
        return 34;     // Base Hurt Action
    case 36: case 37:
        return 36;     // Base Faint Action
    case 38: case 39:
        return 38;     // Base Victory Action
    case 40: case 41:
        return 40;     // Base Special Action
    case 42: case 43:
        return 42;     // Base Mounted Action
    default:
        return 0;
    }
}
```

---

## 5. Movement Replication Integration (Action Code 6 & Action Code 22)

When an entity steps across the map grid:
1. The server serializes target $(X, Y)$ and directional heading in `AC 6:1` (Player Move) or `AC 22:2` (NPC Roam).
2. The client writes heading into `*(actor + 0x121) = 8 + heading`.
3. The renderer executes `FUN_00440fec` (evaluating to 8 $\to$ walk cycle sprite track) and `FUN_00440308` (evaluating to 0..7 $\to$ sprite column offset) for seamless 8-directional isometric path replication.
