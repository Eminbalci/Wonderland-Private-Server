# Technical Specification: NPC Movement, Dialogue vs Door Warps, and Skill Book Synchronization

## 1. NPC Movement Subsystem
- **File**: [`wlo.pserver.core/Game/Maps/Code/QuestNpc.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/QuestNpc.cs)
- **Source of Truth**: `Eve.EMG` parsed map data (`WalkBehavior` and `WalkSteps`).
- **Behaviors**:
  - `WalkSteps.Count > 0` (or `WalkBehavior == 5`): Follows sequential patrol coordinates parsed from the `.dat` file with individual step delays.
  - `WalkBehavior == 4`: Random wander within a bounded origin box (220px threshold steering).
  - All other behavior codes (`WalkBehavior == 0` / default): Static NPCs. Movement loop skips broadcast and delays next evaluation by 60 seconds.

## 2. NPC Interaction & Portal Disambiguation
- **File**: [`Src/Network/ActionCodes/AC20.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC20.cs)
- **Resolution**:
  - `ProcessInteraction((byte)clickID, player)` is prioritized for all NPCs, routing to dialogue (`AC 52 Sub 1`), quests, battles, or merchant shops.
  - Door warping fallback is strictly executed only if the object's name explicitly contains `"door"`.

## 3. Stat Synchronization & Point Allocation
- **Files**:
  - [`Src/Network/ActionCodes/AC08.cs`](file:///d:/GitHub/Wonderland-Private-Server/Src/Network/ActionCodes/AC08.cs)
  - [`wlo.pserver.core/Game/PlayerRelated/Equip.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/Equip.cs)
- **Resolution**:
  - Supports iterative multiple stat allocations per request frame.
  - Invokes `CheckAndUnlockProgressionSkills(player)` on stat allocation to grant new skills dynamically when prerequisites are met.

## 4. Automatic Skill Tree Progression System
- **File**: [`wlo.pserver.core/Game/SkillRelated/SkillManager.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/SkillRelated/SkillManager.cs)
- **Progression Mapping**:
  - **Fire Element**:
    - `15101` (Sword Awn Attack): `STR >= 16`
    - `11114` (Turning Fire Attack): `STR >= 26`
    - `12039` (Fire Wave Attack): `STR >= 38`
    - `15015` (Five Star Hit): `STR >= 51`
    - `15044` (Hagendis Attack): `STR >= 66`
    - `11029` (Flame Hit / Fireball): `INT >= 16`
    - `11025` (Flame Beating): `INT >= 26`
    - `11034` (Fire Ball Attack): `INT >= 38`
    - `11056` (Slowdown): `WIS >= 16`
    - `11002` (Poison Spell): `WIS >= 26`
  - Similar full tiers mapped for Earth, Water, and Wind elements.
- **Triggers**:
  - Checked dynamically on stat allocation (`AC 08`) and level-up.
  - Checked on login (`InitializePlayerSkillsNoSend`) so existing characters retain progression skills.
