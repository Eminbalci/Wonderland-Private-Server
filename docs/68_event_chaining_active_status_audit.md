# Emulator Event Chaining Active Status & Capabilities Audit

## 1. Executive Summary
This document provides a component-by-component audit of Wonderland Online event chaining mechanisms in `wlo.pserver.core`, distinguishing fully operational production systems from partially gated or manual-trigger architectures.

---

## 2. Component-by-Component Status Matrix

| Chaining Mechanism | Operational Status | Server Implementation | Behavior Details |
| :--- | :--- | :--- | :--- |
| **1. PreEvent Show/Hide Reactivity** | **FULLY ACTIVE** | [`PreEventInterpreter.cs`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/QuestRelated/PreEventInterpreter.cs), [`QuestManager.SyncPerPlayerNpcVisibility`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/QuestRelated/QuestManager.cs) | Modifying quest marks via Opcode 5 or GM commands instantly recalculates PreEvents and dispatches `AC 22:4` show/hide packets. |
| **2. Post-Dialogue Combat Handoff** | **FULLY ACTIVE** | [`EveEventInterpreter.cs:752-805`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs#L752-L805) | Opcode 4/6 encounters seamlessly start upon final dialogue acknowledgement (`AC 20:6`). Victory/defeat routes to `RunOutcome`. |
| **3. Cutscene, SFX & NPC Paths** | **FULLY ACTIVE** | [`EveEventRuntime.cs:1005-1053`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventRuntime.cs#L1005-L1053) | Movie cutscenes (`AC 186:12`), fanfare/SFX (`AC 20:10`), and authored NPC movement paths (`AC 22:8`) pause and resume through `player.OnInteractionComplete`. |
| **4. Map Transition & Portals** | **FULLY ACTIVE** | [`EveEventInterpreter.cs:375-405`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs#L375-L405), [`EveEventRuntime.cs:227`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventRuntime.cs#L227) | Opcode 1/3 and Opcode 7 teleport players and automatically invoke `TryExecuteStoryArrival` and walk-on `TryExecuteRegion` (`unknownbyte1 = 2`). |
| **5. Dialogue Choices & Questions** | **FULLY ACTIVE** | [`EveEventRuntime.cs:961-997`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventRuntime.cs#L961-L997) | Opcode 2 dialog2=6 dispatches question frame `AC 20:1 Type 6`; responses trigger callback branch via `FindBranch(trigger=7)`. |
| **6. Seamless Branch Transition** | **PARTIALLY ACTIVE** | [`EveEventRuntime.cs:1075-1155`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventRuntime.cs#L1075-L1155) | **Gated by whitelist.** Active on Robinson intro, Holy Village mirrors, Well lever, and pure flag actions. On un-whitelisted multi-branch quests, `finish()` closes dialogue, requiring a player re-click. |
| **7. Multi-Event Auto-Chain (`npcEntry.Events`)** | **CLICK-ACTIVATED** | [`EveEventInterpreter.cs:70-80`](file:///D:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs#L70-L80) | The server resolves all linked events in priority order upon player click. However, completing Event A does not auto-start Event B without a subsequent interaction. |
