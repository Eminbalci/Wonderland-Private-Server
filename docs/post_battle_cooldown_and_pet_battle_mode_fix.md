# Post-Battle Encounter Cooldown & Pet Battle Mode Architecture Specification

## 1. Overview & System Scope

This technical document details the engineering specifications, protocol flow, and architectural fixes implemented for two critical gameplay subsystems:
1. **Post-Battle Encounter Cooldown Grace Period (2.0 - 4.0 seconds)**: Prevents players from immediately re-entering combat encounters (wild monster proximity agro or step-based random encounters) upon exiting turn-based battles.
2. **Pet Battle Mode Entry & Companion Template Normalization**: Resolves the bug where pets/companions set to battle mode in the client interface failed to participate in combat encounters (`ExpectedActionCount: [1/1]`).

---

## 2. Post-Battle Encounter Cooldown System (Grace Period)

### 2.1 Problem Analysis
In previous revisions:
- When a turn-based battle concluded (via Victory, Defeat, Fleeing, or Disconnect), the server immediately restored map movement via packets `AC 6 Sub 2 [00]` and `AC 20 Sub 8`.
- If an overworld roaming wild monster was positioned within the proximity radius (180 distance units) where the battle occurred, the player taking even a single step immediately re-triggered `PvEBattleManager.StartProximityEncounter()` in `AC06.cs`.
- Players were frequently trapped in unavoidable consecutive combat encounters without time to move away, heal, or adjust party configuration.

### 2.2 Architectural Design & Implementation

#### Data Structure (`wlo.pserver.core/Game/Player.cs`)
```csharp
public DateTime LastBattleEndTime { get; set; } = DateTime.MinValue;
public double BattleCooldownSeconds { get; set; } = 3.0;
private static readonly Random _cooldownRng = new Random();

public bool IsInBattleCooldown()
{
    if (LastBattleEndTime == DateTime.MinValue) return false;
    double elapsed = (DateTime.UtcNow - LastBattleEndTime).TotalSeconds;
    return elapsed >= 0 && elapsed < BattleCooldownSeconds;
}

public void SetBattleCooldown()
{
    LastBattleEndTime = DateTime.UtcNow;
    lock (_cooldownRng)
    {
        // Random grace period between 2.0 and 4.0 seconds
        BattleCooldownSeconds = 2.0 + (_cooldownRng.NextDouble() * 2.0);
    }
    StepsSinceLastBattle = 0;
}
```

#### Cooldown Trigger Points (`wlo.pserver.core/Game/Battle/PvEBattleManager.cs`)
The cooldown is automatically engaged (`p.SetBattleCooldown()`) at both session cleanup and client movement release across all battle termination pathways:
- **`EndBattleVictory`**: Attacking winners and defending players (PvP).
- **`EndBattleFlee`**: Fleeing party members.
- **`EndBattleDefeat`**: Defeated party members.
- **`OnPlayerDisconnect`**: Players disconnecting mid-battle.

#### Suppressed Encounter Handlers
1. **`Src/Network/ActionCodes/AC06.cs` (Movement Packet `AC 6:1`)**:
   - Skips both proximity monster scans and step accumulator progression while `p.IsInBattleCooldown()` returns `true`.
2. **`PvEBattleManager.StartProximityEncounter`**:
   - Direct guard clause: Returns immediately and logs suppression if `player.IsInBattleCooldown()` is active.
3. **`PvEBattleManager.CheckAndTriggerRandomEncounter`**:
   - Rejects random encounter roll if `player.IsInBattleCooldown()` is active.
4. **`PvEBattleManager.StartRandomEncounterFromPool`**:
   - Rejects encounter initialization if `player.IsInBattleCooldown()` is active.

---

## 3. Pet Battle Mode Entry & Companion Normalization

### 3.1 Problem Analysis
- **Companion ID Divergence**: In the client binary and packet protocol, companions have dedicated Pet Template IDs distinct from overworld NPC IDs. For instance, Robinson's NPC Template ID in `Npc.dat` is `12032`, while his Pet Template ID is `12178` (`0x2F92`).
- **Database/Storage Storage**: The database and quest manager store Robinson as `12032` within `player.PlayerPets`.
- **Packet Mismatch in `AC19.cs`**:
  - Client sends `AC 19 Sub 1` with `petId = 12178`.
  - `AC19.cs` previously checked `kvp.Value.PetID == petId`. Since `12032 == 12178` evaluated to `false`, every pet's `IsBattle` flag was set to `false`.
- **Selector Failure in `PvEBattleManager.cs`**:
  - `GetActivePet()` checked `x.PetID == p.ActivePetID && x.IsBattle && x.HP > 0`.
  - Since `x.PetID` (`12032`) != `p.ActivePetID` (`12178`) and `IsBattle` was `false`, `GetActivePet()` returned `null`.
  - As a result, no `BattleFighter` for the companion was added to `battle.Attackers`.
  - `ExpectedActionCount` remained `1` (Player only), and the companion never spawned on the combat grid.

### 3.2 Architectural Design & Implementation

#### 1. Universal Companion Equivalence (`wlo.pserver.core/Game/Player.cs`)
A unified helper method standardizes companion equivalence across the server:
```csharp
public static bool IsSamePetOrCompanion(uint id1, uint id2)
{
    if (id1 == id2) return true;
    if (id1 == 0 || id2 == 0) return false;

    // Robinson: 12032 (NPC TID) <-> 12178 (Pet TID)
    if ((id1 == 12032 || id1 == 12178) && (id2 == 12032 || id2 == 12178)) return true;
    // S.Monkey: 17162 (NPC TID) <-> 10727 (Pet TID)
    if ((id1 == 17162 || id1 == 10727) && (id2 == 17162 || id2 == 10727)) return true;
    // Roca: 14161 <-> 14001
    if ((id1 == 14161 || id1 == 14001) && (id2 == 14161 || id2 == 14001)) return true;
    // Niss: 14162 <-> 14002
    if ((id1 == 14162 || id1 == 14002) && (id2 == 14162 || id2 == 14002)) return true;
    // Clive: 14163 <-> 14003
    if ((id1 == 14163 || id1 == 14003) && (id2 == 14163 || id2 == 14003)) return true;
    // Fred: 14164 <-> 14004
    if ((id1 == 14164 || id1 == 14004) && (id2 == 14164 || id2 == 14004)) return true;
    // Elin: 14165 <-> 14005
    if ((id1 == 14165 || id1 == 14005) && (id2 == 14165 || id2 == 14005)) return true;
    // Sam: 14166 <-> 14006
    if ((id1 == 14166 || id1 == 14006) && (id2 == 14166 || id2 == 14006)) return true;
    // Shizune: 14167 <-> 14007
    if ((id1 == 14167 || id1 == 14007) && (id2 == 14167 || id2 == 14007)) return true;
    // Suzan: 14168 <-> 14008
    if ((id1 == 14168 || id1 == 14008) && (id2 == 14168 || id2 == 14008)) return true;

    return false;
}
```

#### 2. Robust Pet Battle Selection (`Src/Network/ActionCodes/AC19.cs`)
`RecvSetBattlePet` now resolves pets using 4 cascaded tiers:
1. Slot number lookup (`1..4`).
2. Companion equivalence lookup (`Player.IsSamePetOrCompanion(pet.PetID, petId)`).
3. Dictionary key lookup (`player.PlayerPets[(byte)petId]`).
4. Fallback to first pet in bag.

Upon selection:
- Sets `targetPet.IsBattle = true;` (all other pets set to `false`).
- Sets `player.ActivePetID = broadcastPetId` (normalized to `12178` for Robinson).
- Emits `AC 19:1` and broadcasts companion appearance (`AC 15:4`, `AC 15:1`, `AC 19:4`, `AC 13:5`, `AC 5:8`).
- Emits stat synchronization packets (`AC 8:2` and `AC 8:1`) for Level, HP/SP, and potential.

#### 3. Combat Fighter Construction (`wlo.pserver.core/Game/Battle/PvEBattleManager.cs`)
- **`GetActivePet`**:
  - Matches active combat pet using `Player.IsSamePetOrCompanion(x.PetID, p.ActivePetID) || x.Slot == p.ActivePetID`.
  - Fallback checks any pet with `IsBattle == true` and positive HP.
  - Secondary fallback matches `ActivePetID` if `IsBattle` flag was omitted.
- **`BuildFighters`**:
  - Normalizes Robinson's sprite ID to `12178` for the combat packet `AC 11:5`.
  - Sets element to `1` (Water) for Robinson.
  - Deploys pet to designated grid coordinate (Slot 0 Attacker pet: `(3, 2)`).
- **Turn Flow & Expected Action Count**:
  - `ExpectedActionCount` dynamically accounts for living friendly pets (`ExpectedActionCount = 2`).
  - Sends pet skills (`QuestManager.SendPetSkills`) so action menus populate with authentic spells ("Fury Strike", "Freeze Strike").
  - Collects pet actions (`PendingActions`) and auto-defends upon 30s turn timeout.

---

## 4. Verification & Status

- **Compilation**: Built with .NET CLI (`dotnet build "Wonderland Private Server.sln"`), achieving **0 errors**.
- **Regressions**: Verified zero interference with existing pet mount system, player inventory, or safe town maps.
