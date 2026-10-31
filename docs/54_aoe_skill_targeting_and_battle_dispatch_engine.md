# AOE Skill Targeting & Multi-Target Battle Dispatch Engine

## 1. Overview & Architecture

In Wonderland Online (WLO), combat abilities encompass single-target attacks as well as multi-target Area of Effect (AOE) offensive, defensive, and support spells. The native game client renders multi-target animations and damage indicators using Action Code `50:1` (`AC 50:1`) packets carrying a variable target count and an array of target records.

Prior to this implementation, `PvEBattleManager.cs` only processed a single target entity per action (`Pack8(1)`), applying damage, status conditions, and healing strictly to `targetFighter` / `targetAlly`. Furthermore, `SkillManager.cs` only extracted base combat attributes from `Skill.dat`, completely omitting the pattern and target definitions required to resolve combat grid geometries.

This subsystem provides:
1. Full decryption and loading of `SkillPattern1..4`, `MaxSkillLevel`, `Target`, `Effect`, `AdditionalEffect`, `NumberOfTurns`, and `TypeOfInjury` from `Data/Skill.dat`.
2. Grade-based pattern selection (`GetPatternForGrade(byte grade)`) supporting skill progression (Grade 1..3 -> Pattern 1, Grade 4..6 -> Pattern 2, Grade 7..9 -> Pattern 3, Grade 10 -> Pattern 4).
3. `AoeTargetResolver` geometry engine implementing patterns 1 through 8 across the 2x4 battlefield grid system.
4. Multi-target packet encoding for `AC 50:1` in both Phase 4 (Offensive/Status) and Phase 2 (Support/Healing/Buff).
5. Comprehensive multi-target damage calculation, elemental multipliers, critical hits, status effect application, HP synchronization, death broadcast (`AC 53:3`), and sleeper awakening.

---

## 2. Battlefield Coordinate System & Geometry

The Wonderland Online battlefield is structured as two opposing 2x4 grids:

```
[ Defender / Monster Side ]        [ Attacker / Player Side ]
Back Row (X=1)   Front Row (X=2)   Front Row (X=3)   Back Row (X=4)
+---+---+---+---+ +---+---+---+---+ +---+---+---+---+ +---+---+---+---+
|1,1|1,2|1,3|1,4| |2,1|2,2|2,3|2,4| |3,1|3,2|3,3|3,4| |4,1|4,2|4,3|4,4|
+---+---+---+---+ +---+---+---+---+ +---+---+---+---+ +---+---+---+---+
```

### Opposite Row Mapping
- For Defenders (Monsters):
  - Row 2 (Front) <-> Row 1 (Back)
- For Attackers (Players/Pets):
  - Row 3 (Front/Pet) <-> Row 4 (Back/Player)

Implemented via `AoeTargetResolver.GetOppositeRow(byte gridX)`:
- `1 -> 2`, `2 -> 1`
- `3 -> 4`, `4 -> 3`

---

## 3. AOE Skill Patterns (1 to 8)

| Pattern ID | Geometry Description | Formula / Slot Resolution | Typical Skills |
|---|---|---|---|
| **1** | Single Target | `(X, Y)` | Basic attacks, single-target spells |
| **2** | Column (2 targets) | `(X, Y)` + `(OppositeX, Y)` | Turning Ice, Gallop, Rockfall Hit |
| **3** | Row (Horizontal line, up to 3 targets) | `(X, Y)` + `(X, Y - 1)` + `(X, Y + 1)` | Rock Elf Beating, Fire Ball Beating, Rockfall Beating |
| **4** | Cross / Sideways-T (4 targets) | `(X, Y)` + `(OppositeX, Y)` + `(X, Y - 1)` + `(X, Y + 1)` | Fire Stone Bead (G10), Mesmerism, Water Sleep |
| **5** | 5 Targets (Cross + nearby) | Cross slots + opposite row adjacent | Centaur Flameblast |
| **6** | 6 Targets (3 columns x 2 rows) | `Y in {Y-1, Y, Y+1}` on both `X` and `OppositeX` | Sky Thunder, Rage Thunder (G10), Super Water Arrow |
| **7 & 8** | Full Team / All living units | All living enemy or ally fighters | Arrow Rain, Blaze Beating, Dark Thunder, Gaea's Guard |

---

## 4. Binary Layout in `Data/Skill.dat`

The client's `Data/Skill.dat` format stores 148-byte fixed records. Values are encrypted with custom XOR/subtraction ciphers:
- **1-byte decryption:** `(b ^ 0xFD) - 4`
- **2-byte decryption:** `(u16 ^ 0x6EA0) - 4`

### Decoded Attribute Offsets
- **Offset 51 (`byte`):** `NumberOfTurns`
- **Offset 52 (`byte`):** `Effect` (Status effect category)
- **Offset 99 (`byte`):** `Target` (0 = Enemy, 1 = Ally Single/AOE, 5 = Dead Ally / Revive)
- **Offset 105 (`byte`):** `MaxSkillLevel`
- **Offset 106 (`byte`):** `AdditionalEffect`
- **Offset 109 (`byte`):** `SkillPattern1` (Grade 1-3 AOE pattern)
- **Offset 110 (`byte`):** `SkillPattern2` (Grade 4-6 AOE pattern)
- **Offset 111 (`byte`):** `SkillPattern3` (Grade 7-9 AOE pattern)
- **Offset 112 (`byte`):** `SkillPattern4` (Grade 10 AOE pattern)
- **Offset 113 (`byte`):** `TypeOfInjury`

---

## 5. Packet Architecture (`AC 50:1`)

Multi-target battle actions broadcast an `AC 50:1` packet with the following structure:

```
[Header]
  50, 1
  0x11, 0x00
  actor.GridX (byte)
  actor.GridY (byte)
  skillId (ushort, little-endian)
  0 (byte)
  targetCount (byte) = N

[Per-Target Record (11 bytes each, repeated N times)]
  target.GridX (byte)
  target.GridY (byte)
  1 (byte)
  isDefending (byte, 0 or 1)
  1 (byte)
  statType (byte, 0x19 for HP)
  statValue / damage (uint32, little-endian)
  hitType (byte, 1 = normal, 2 = enlarged critical digits)
```

Setting `targetCount > 1` signals the game client to execute the skill's multi-hit visual effects simultaneously across all target coordinates without desynchronizing combat pacing.

---

## 6. Technical Implementation Details

### `AoeTargetResolver.cs`
- `GetOppositeRow(byte gridX)`: Determines opposite front/back row coordinates.
- `ResolveTargets(BattleFighter primaryTarget, byte pattern, IEnumerable<BattleFighter> candidatePool)`: Collects and returns the ordered list of affected fighters. Guarantees that the primary selected target is always first, preserving target intent and animation focus.

### `PvEBattleManager.cs` Updates
1. **`GetFighterSkillGrade`:** Queries the attacker's `PlayerSkills` to retrieve their current proficiency grade (1..10) for the executed skill.
2. **Phase 4 (Offensive Actions):**
   - Resolves all hit targets via `AoeTargetResolver.ResolveTargets(targetFighter, pattern, livingEnemies)`.
   - Iterates through all targets, computing individual defense reductions, elemental advantages, and critical checks.
   - Encodes all target records into a single `AC 50:1` animation packet.
   - Updates `CurHP` for all targets, wakes up sleeping targets that took damage, and broadcasts `AC 53:3` for all defeated entities.
3. **Phase 2 (Support / Heal / Buff Actions):**
   - Resolves ally targets based on skill pattern (e.g., single target heal, group cure, or team buffs).
   - Packs multiple ally records into `AC 50:1` and broadcasts stat updates to all players in battle.
