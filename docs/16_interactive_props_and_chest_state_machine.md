# Interactive Props, Chest State Machine, and Client Synchronization

## 1. Subsystem Purpose

The Interactive Props and Chest State Machine handles overworld static chests, crates, gathering nodes, mechanisms, and harvestable props across all 1,119 maps in Wonderland Online. It coordinates wire protocol serialization (`AC 22:4`), client sprite animation frame mapping, per-player quest completion persistence, and prevents client-side sprite flickering/blinking artifacts.

---

## 2. Protocol Wire Invariants (`AC 22:4`)

The 14-byte per-record entity synchronization frame is structured as follows:

```
[ClickID: 2 bytes LE] [State: 2 bytes LE] [X: 2 bytes LE] [Y: 2 bytes LE] [EntityType: 1 byte] [Duration: 4 bytes LE] [Flag: 1 byte]
```

### 2.1 State Word Invariants

* **Living NPC Actors:** `State = 0x00FF` (`255` LE: `FF 00`), `EntityType = 1`, `Duration = 0`.
* **Interactive Props / Chests (Intact):** `State = 0x0000` (`0` LE: `00 00`), `EntityType = 1`, `Duration = 0`.
* **Opened Chests / Broken Gathering Nodes:** `State = 0x0001` (`1` LE: `01 00`), `EntityType = 1`, `Duration = 0`.
* **Despawned / Concealed Entities:** `State = 0xFFFF` (`65535` LE: `FF FF`), `EntityType = 2`, `Duration = 0x03E7FC18` (65,535,000 ms).

### 2.2 Client Animation Frame Invariant (Root Cause of Blinking Bug)

In the official client sprite engine (`aLogin.exe`), static props and chests possess only two discrete frame states:
* `Frame 0`: Intact / Closed / Unbroken.
* `Frame 1`: Opened / Broken / Empty.

Transmitting living actor state `0x00FF` (`255`) to an interactive prop or chest causes the client animation engine to cycle out-of-range frames between 0 and 1 every render tick, creating rapid continuous visual flickering and blinking. Official network packet captures (Sessions `session_20260911_150803` Seq 635 and Seq 971) confirm that props always initialize with `State = 0x0000` and transition to `State = 0x0001` when looted.

---

## 3. Core Methods & API Reference

### 3.1 `PreEventInterpreter.IsStaticPropOrChest`

```csharp
public static bool IsStaticPropOrChest(Player player, ushort mapId, ushort clickId, out bool isOpened)
```

* **Parameters:**
  * `player` (`Player`): Online player instance receiving map entity synchronization.
  * `mapId` (`ushort`): Isometric map identifier (`1..65535`).
  * `clickId` (`ushort`): Unique entity click identifier on the target map.
  * `isOpened` (`out bool`): Evaluates to `true` if the chest/prop has been looted or broken by the player.
* **Return Type:**
  * `bool`: `true` if the entity is an interactive prop, gathering node, chest, crate, or mechanism; `false` if it is a living NPC actor or wandering mob.
* **Exceptions:**
  * Fail-safe design: Internal exceptions are trapped and logged to `DebugSystem.Write`, returning `false`.
* **Edge Cases:**
  * Checks NPC template ranges (`19000..19999` and `25000..35000`).
  * Explicitly excludes human companion models (`12000..12999`), preventing companions like Robinson Crusoe (`TID 12032`) from being misclassified as static props.
  * Cross-references `mapData.Events` using `npcDef.Events`, direct `clickID`, and `DialogPtr == 2 && dialog2 == 5`.

### 3.2 `PreEventInterpreter.SendPropShow`

```csharp
public static void SendPropShow(Player player, ushort clickId)
```

* **Parameters:**
  * `player` (`Player`): The destination player session.
  * `clickId` (`ushort`): Entity click ID to spawn/reveal.
* **Return Type:**
  * `void`.
* **Wire Emission:**
  * Dispatches `AC 22:4` with `State = isOpened ? 0x0001 : 0x0000`, `EntityType = 1`, and `Duration = 0`.
* **Edge Cases:**
  * Removes `clickId` from `player.HiddenNpcClickIDs`.
  * Never transmits `0x00FF` for props.

### 3.3 `AC22.BuildPropSpawnPacket`

```csharp
public static SendPacket BuildPropSpawnPacket(ushort clickId, ushort x = 0, ushort y = 0, bool isOpened = false)
```

* **Parameters:**
  * `clickId` (`ushort`): Target entity click identifier.
  * `x` (`ushort`): X coordinate in isometric space.
  * `y` (`ushort`): Y coordinate in isometric space.
  * `isOpened` (`bool`): Intact (`false` -> `0x0000`) or opened (`true` -> `0x0001`).
* **Return Type:**
  * `SendPacket`: Serialized 14-byte WLO wire frame with header.

---

## 4. Per-Player Chest Persistence & Anti-Respawn Guard

1. **One-Time Chest Keys:** Stored in `player.Quests` under key `mapId * 1000 + clickId` (or `mapId * 1000 + eventEntry.clickID`) with state `QuestState.Completed`.
2. **Persistence:** Persisted into character database via `QuestManager.SavePlayerQuest`.
3. **Guard Against Frame Reset:** [`QuestNpc.Interact`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/QuestNpc.cs) distinguishes renewable gathering nodes (coconuts, wood, iron ore) from one-time quest containers (chests, boxes). One-time containers do not set shared `IsBroken = true` with a respawn timer, preventing [`QuestNpc.Update`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/QuestNpc.cs) from reverting opened frames back to frame 0 for other players.
4. **Despawn Loop Prevention:** [`PreEventInterpreter.ShouldNpcBeVisible`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/QuestRelated/PreEventInterpreter.cs) checks for completed one-time events with Opcode 2 despawn (`dialog2 == 2`). If completed, `ShouldNpcBeVisible` returns `false`, preventing `EvaluateMapPreEvents` from re-showing the entity and eliminating rapid show/hide blinking loops.
