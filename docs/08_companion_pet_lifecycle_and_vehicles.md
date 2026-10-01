# Companion Pet Lifecycle and Vehicle Engine Specification

## 1. Architectural Overview

The companion and mount subsystem provides full lifecycle simulation for human companions (Robinson, Roca, S. Monkey, Niss, Clive, etc.) and player vehicles (Raft, Canoe, Sailboat, Airship). It manages companion amity loyalty decay, combat skill trees, rebirth ascension, overworld follower replication, and vehicle wrecking sequences.

---

## 2. Companion Synchronization Protocol (`AC 15`, `AC 19`, `AC 8:2`)

```
+-------------------+---------------+-------------------------------------------------------+
| Packet Identifier | Target Class  | Functional Purpose                                    |
+-------------------+---------------+-------------------------------------------------------+
| AC 15:1           | AC15.cs       | 54-byte companion roster entry serialization          |
| AC 15:4           | AC15.cs       | Spawns overworld follower sprite with equipment visual|
| AC 19:1 / 19:4    | AC19.cs       | Designates active combat battle companion             |
| AC 19:2           | AC19.cs       | Sets companion to standby / rest mode                 |
| AC 19:7           | AC19.cs       | Despawns overworld follower sprite [CharID: UInt32]   |
| AC 8:2            | AC08.cs       | Companion real-time stat & proficiency sync           |
+-------------------+---------------+-------------------------------------------------------+
```

### 2.1 Login Handshake
During character login in [`WorldServer.CommenceLogin`](file:///D:/GitHub/Wonderland-Private-Server/Src/Server/WorldServer.cs), the server:
1. Iterates `player.PlayerPets.Values`, transmitting a 54-byte `AC 15:1` record for each recruited companion.
2. Dispatches `AC 8:2` with `TargetType = 0x04` and stat `0x016F` to unlock companion skills in the client UI.
3. If `player.ActivePetID > 0`, broadcasts `AC 19:4` (active combat pet) and `AC 15:4` (spawns follower sprite to map peers).

### 2.2 Companion Stat Synchronization (`AC 8:2`)
Using `TargetType = 0x04` (Pet), the server synchronizes real-time companion statistics:
* `0x0119`: Current HP
* `0x011A`: Current SP
* `0x011D`: Current Level
* `0x011E`: Total Experience Points
* `0x0124`: Combat Battle EXP Gain (aligning with official PCAP Seq 1180)
* `0x016F`: Skill proficiency and unlocked skill IDs

---

## 3. Amity System & Desertion Safeguards

Companion loyalty is governed by [`PetAmityManager.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PetRelated/PetAmityManager.cs):
* **Combat Knockout Decay:** Whenever a companion is knocked out in battle, their Amity decreases by `1`.
* **Permanent Desertion Threshold:** If Amity falls below **20**, the companion deserts the player permanently. The companion is removed from the active party, its overworld sprite is despawned, and its record is purged from [`character_pets`](file:///D:/GitHub/Wonderland-Private-Server/docs/02_database_schema_and_persistence_lifecycle.md#34-character_pets).
* **Loyalty Recovery:** Feeding companions special amity food items or resting in hot springs (`AC 80`) restores Amity up to the maximum of `100`.

---

## 4. Companion Rebirth Ascension

Companion Rebirth allows companions to ascend past normal caps via [`GmManager.TriggerPetRebirth`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/PlayerRelated/GmManager.cs):
* Resets Level to `1` and Total EXP to `0`.
* Sets `Reborn = true`.
* Awards `+50` bonus Potential Skill Points.
* Resets Amity to `100`.
* Emits `AC 69:1`, updates `AC 15:1`, and syncs updated skills via `SendPetSkills()`.

---

## 5. Vehicle & Mount System

### 5.1 Mount & Dismount Protocol
* **Mounting (`AC 15:10`):** Client sends bag slot containing vehicle item. Server validates vehicle type, clears item from bag, sets `MountedVehicleSlot = slot`, and broadcasts `AC 15:10 [CharID, VehicleID, Slot]`.
* **Dismounting (`AC 15:11`):** Restores vehicle item to bag slot and broadcasts `AC 15:11 [Slot, CharID]`.
* **Pet Riding (`AC 15:16` / `AC 15:17`):** Mounts or dismounts riding pets (horses, wolves) with speed bonuses.

### 5.2 Raft Shore Shipwreck Protocol (7-Step Handshake)

When a player navigates a wooden raft to shore, the authentic 7-step wrecking sequence executes:

```
+-------+---------------+---------------------------------------------------------------+
| Step  | Packet / Code | Action Performed                                              |
+-------+---------------+---------------------------------------------------------------+
| 1     | AC 15:14      | Dispatches impact animation: [15, 14, slot, CharID, 0xD6, 1...] |
| 2     | AC 23:9       | Destroys raft item: player.Inv.RemoveItem(slot, 1, true)      |
| 3     | AC 15:15      | Confirms raft wreckage to client: [15, 15, CharID, vid]       |
| 4     | AC 15:11      | Ejects player from vehicle: [15, 11, slot, CharID]            |
| 5     | Internal      | Clears state: ActiveVehicleID = 0, MountedVehicleSlot = 0     |
| 6     | AC 5:4        | Recalibrates player movement speed to default (speed 2)       |
| 7     | AC 23:57      | Displays shipwreck notification banner and commits auto-save  |
+-------+---------------+---------------------------------------------------------------+
```

---

## 6. Robinson Companion Recruitment Pipeline (Map 10035 / 10039)

### 6.1 Native Event 19 State Machine

Robinson recruitment on Rhode Island Beach (`Map 10035` / `10039`) is orchestrated via native EVE script Event 19 (`老魯加入` - "Robinson joins"). It consists of 3 action-bearing subentry branches chained seamlessly across interactions:

1. **SubEntry 1 (`subIndex 1` - Chest Opening & Raft Delivery):**
   * Precondition: `Quest 12046` not started (`w2 = 2`).
   * Actions: Plays chest opening animation (`Opcode 2`), grants `Robinson's Raft` (`ItemID 48016`, `Opcode 1`), flags `Quest 12046` as `InProgress` Step 1 (`Opcode 5`).
   * Transition: Seamlessly transitions to `subIndex 3` via [`EveEventRuntime.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventRuntime.cs#L978).

2. **SubEntry 3 (`subIndex 3` - Story Dialogue Sequence):**
   * Precondition: `Quest 12046 == 1` (`w2 = 1, w4 = 0x0105`).
   * Actions: Executes authentic dialogue between player and Robinson (`TalkIDs 20355, 28422..28427`, animation `11070`). Marks `Quest 12046` as `Completed` (`w2 = 2`), advances `Quest 12047` to `InProgress` Step 1 (`Opcode 5`).
   * Transition: Seamlessly transitions to `subIndex 5` via [`EveEventRuntime.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventRuntime.cs#L978).

3. **SubEntry 5 (`subIndex 5` - Recruitment & Map Despawn):**
   * Precondition: `Quest 12047 == 1` (`w2 = 1, w4 = 0x0105`).
   * Actions: Marks `Quest 15282` as `Completed`, despawns Robinson NPC (`ClickID 1`, `Opcode 2`), recruits Robinson (`Companion TID 12178` / internal `PetID 12032`, `Opcode 3`) into the player's party via `QuestManager.SendCompanionReward`, and marks `Quest 15283` as `InProgress` Step 1.
   * Finalization: Commits player character persistence and synchronizes map actor visibility.

### 6.2 Existing Player Rescue Fallback
For characters who completed dialogue prior to recruitment finalization (`Quest 12047` in progress without Robinson in party):
* Interacting with chest (`NPC #7`) directly evaluates `subIndex 5`.
* Interacting with Robinson (`NPC #1`) on `Map 10035` or `10039` prioritizes Event 19 in [`EveEventInterpreter.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs#L93), triggering immediate recruitment and despawning the overworld beach actor.
