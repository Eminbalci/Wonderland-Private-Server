# NPC & Chest Blinking Prevention System

## 1. Problem Diagnosis
In the official Wonderland Online client engine, `AC 22 Sub 2` (`[22, 2, clickId, X, Y, speed]`) commands a ClickID to execute the dynamic movement animation. For static props (such as wooden chests, crates, trees, coconut nodes, ores), the client's "walking" animation frame is an open or moving state. When movement packets are repeatedly broadcast to static ClickIDs, the client toggles between its walking animation and idle frame, resulting in rapid blinking / flickering.

### Root Causes
1. **Misclassified Native Spawns:** In `EveLoader.cs`, non-NPC entries or offset shifts produced phantom NPC entries with `WalkBehavior = 4` and non-zero `WalkSteps` assigned to ClickIDs 6 and 7 (beach chests/crates).
2. **Permissive `IsStaticNpc()` Check:** Empty, unrecognized, or template-ID-only props (`TemplateID: 0` or in range `16000–19200`) returned `false`, allowing `QuestNpc.Update()` to broadcast `[22, 2]` movement packets.

---

## 2. Permanent Solution

### 2.1 Spawn Filtering ([Map.cs](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Map.cs))
* Added strict bounds and sanity checking when loading native `mapData.Npclist` entries:
```csharp
if (entry.clickId == 0 || entry.x > 4000 || entry.y > 4000 || (entry.x == 0 && entry.y == 0))
    continue;
```

### 2.2 Strict Static Entity & Prop Filter ([QuestNpc.cs](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/QuestNpc.cs))
* **Template Range Check:** Any entity with `TemplateID == 0` or within `16000..19200` (official prop/furniture/chest range) is classified as strictly static (`IsStaticNpc() == true`).
* **Name & ID Filtering:** ClickIDs 6, 7, 10, empty names, and `npc_0*` strings are forced to `true`.
* **Movement Loop Guard:**
```csharp
if (IsStaticNpc() || WalkBehavior == 0 || TemplateID == 0 || (TemplateID >= 16000 && TemplateID <= 19200))
{
    NextWalkTime = now.AddSeconds(300);
    return;
}
```
* Ensures `[22, 2]` packets are never generated or broadcast for chests, crates, or gathering objects.
