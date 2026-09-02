# Mob Roaming & NPC Visibility Specification

## 1. Mob Roaming & AI Walking
- Monsters (TID 16000-19200) and wandering NPCs (`WalkBehavior == 4` or with `WalkSteps`) roam naturally across their spawn areas.
- Static props (chests, trees, portals, furniture, rocks, etc.) are strictly kept stationary.

## 2. Per-Player NPC Visibility
- Quest completion NPC despawn packets use official `AC 22 Sub 1` (`[22, 1, clickId, 0, 1]`) instead of pet stance packets (`AC 19:2`).
- `SyncPerPlayerNpcVisibility` validates `quest.MapID == mapId` so that completing a quest on one map does not unintentionally hide unrelated NPCs sharing the same ClickID on other maps.
