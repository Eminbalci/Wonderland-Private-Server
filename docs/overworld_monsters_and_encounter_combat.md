# Overworld Monsters, Aggro, and Dynamic Encounter Combat Protocol

This document details the architecture, bug diagnosis, and resolution for overworld map monster interaction, proximity aggro, step encounters, and turn-based PvE combat initiation across all maps (specifically outdoor field maps such as Map 60000 North Island).

---

## 1. Problem Diagnosis & Root Cause Analysis

### 1.1 Premature Despawn / Opcode 4 Hijack
When players clicked on wild creatures or monsters (e.g. Jelly ClickID 9 on Map 60000), `QuestNpc.Interact` invoked `EveEventInterpreter.TryExecute` prior to evaluating monster combat triggers. `eve.emg` binds map NPCs to events whose default sub-opcodes contain `Opcode 4` (despawn / visibility). As a result:
- The server sent an `AC 22:10` despawn packet for the monster.
- `EveEventInterpreter.ExecuteOpcode` returned `true`, terminating the interaction flow without launching battle.

### 1.2 `IsSafeTownMap` Map 60000 Misclassification
In `PvEBattleManager.IsSafeTownMap`, Map 60000 (North Island Overworld) was mistakenly registered as a safe town map alongside Kelan Village (Map 10000). This caused:
- Proximity aggro checks in `AC06.cs` to be completely bypassed.
- Step-based random encounters (`NextBattleSteps`) on Map 60000 to be disabled.

### 1.3 `IsStaticNpc()` Monster Template ID Range Collision
In `QuestNpc.IsStaticNpc()`, the condition `(this.TemplateID >= 16000 && this.TemplateID <= 19999)` classified all Wonderland Online monster templates (Jellies, Wolves, Beetles, Snails, Boars spanning `17000..19500`) as static props/objects:
- `IsWildMonster()` evaluated to `false` for every outdoor monster in the game.
- Autonomous wandering/roaming in `QuestNpc.Update()` was permanently suppressed.

---

## 2. Technical Implementation & Architectural Fixes

### 2.1 Immediate PvE Combat Trigger in `QuestNpc.Interact`
Moved monster detection to the top-priority slot (Step 0.0) in [`QuestNpc.Interact(Player src)`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/QuestNpc.cs):
```csharp
if (this.IsWildMonster() || (this.TemplateID >= 17000 && this.TemplateID <= 19500))
{
    src.Send(Tools.FromFormat("bb", 20, 8));
    string mobName = this.Name;
    if (string.IsNullOrEmpty(mobName) || mobName.Equals("Npc", StringComparison.OrdinalIgnoreCase) || mobName.StartsWith("unknown", StringComparison.OrdinalIgnoreCase))
    {
        mobName = Game.Battle.PvEBattleManager.ResolveMonsterName(this.TemplateID);
    }
    Battle.PvEBattleManager.StartPvEBattle(src, (ushort)this.CickID, mobName, Math.Max(1, (int)this.Level), Math.Max(50, (int)this.HP), this.TemplateID);
    DebugSystem.Write($"[QuestNpc] Started PvE battle for monster '{mobName}' (ClickID {this.CickID}, TID {this.TemplateID}, Lv.{this.Level}) with {src.CharName}");
    return;
}
```

### 2.2 Template Range Refactoring in `IsWildMonster` & `IsStaticNpc`
- `IsWildMonster()` explicitly detects WLO mob template ranges `17000..17999` and `19000..19500`, while filtering out friendly human NPCs, domestic animals, and static map objects.
- `IsStaticNpc()` explicitly yields `false` whenever `IsWildMonster()` or `IsHumanNpc()` is `true`, and only matches authentic prop ranges (`12000..12999`, `25000..35000`).

### 2.3 Monster Name & Template Resolver (`ResolveMonsterName`)
Added [`PvEBattleManager.ResolveMonsterName(uint templateId)`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Battle/PvEBattleManager.cs) mapping template IDs to authentic names and attributes:
- `17000`: Water Jelly
- `17001`: Earth Jelly
- `17002`: Wind Jelly
- `17003`: Fire Jelly
- `17004`: Horned Beetle
- `17005`: Forest Snail
- `17100..17101`: Wild Boar / Fire Boar
- `17106..17107`: Wood Beetle / Stag Beetle
- `17410..17412`: Island Wolf / Wolf Leader / Dire Wolf
- `17413..17418`: Wild Boar, Snail, Giant Beetle, Forest Treant, Dark Bat, Cave Spider

### 2.4 Safe Map Definitions Correction
Corrected [`PvEBattleManager.IsSafeTownMap`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Battle/PvEBattleManager.cs) to exclude Map 60000, enabling both proximity aggro and step-based random encounters on the North Island overworld.

### 2.5 Safeguard in `EveEventInterpreter.TryExecute`
Added an upstream intercept in [`EveEventInterpreter.TryExecute`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs) ensuring any wild monster clicked through native eve script dispatch launches a PvE battle immediately without executing despawn opcodes.

---

## 3. Verification & Validation

1. **Compilation**: Built solution with 0 errors (`dotnet build "Wonderland Private Server.sln"`).
2. **Click Encounter**: Clicking any roaming monster on Map 60000 immediately starts turn-based combat with authentic monster name, level, HP, and stats.
3. **Movement & Aggro**: Walking near roaming monsters or accumulating steps in outdoor maps correctly triggers proximity and random battle encounters.
