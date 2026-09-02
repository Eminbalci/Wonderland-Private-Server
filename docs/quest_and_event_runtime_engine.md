# Universal Quest & Event Runtime Engine Specification

## 1. Architecture Overview
The quest and event execution system operates as a **unified rule interpreter / virtual machine**, executing data-driven opcodes directly from `Eve.emg`, `Mark.dat`, and `Talk.dat` across all 1,119 game maps.

```mermaid
sequenceDiagram
    autonumber
    actor Player as 🎮 Player Client
    participant Net as 📡 Network (AC 20 / AC 23)
    participant Core as ⚙️ EveEventInterpreter
    participant Battle as ⚔️ PvEBattleManager
    participant Actor as 🗺️ ActorVisibility & Map
    participant DB as 💾 QuestDataBase (charquest)

    Player->>Net: Click NPC / Object (AC 23:1)
    Net->>Core: ProcessInteraction(clickId, player)
    Core->>DB: Query Player Quest Flags & Step
    Core->>Core: Strict Order Branch Matching (Level/Item/Pet/Gold/Step)
    Core->>Player: Send AC 20:1 Intro Dialogue / Choice Menu
    opt Multi-Choice Branch
        Player->>Net: Send Choice Selection (AC 20:9)
        Net->>Core: OnDialogueChoice(choice)
        Core->>Core: Execute Selected Choice Branch (Accept / Deny)
    end
    opt Quest Battle Encounter
        Core->>Battle: StartPvEBattle with QuestBattleContext
        Battle->>Player: AC 11 Battle Turn Sequence
        alt Victory
            Battle->>Core: OnVictory() Callback
            Core->>Core: Execute Victory Branch Opcodes
            Core->>DB: Atomic Commit (Marks, Status 2, Rewards)
            Core->>Actor: ReplayActorVisibility (Despawn NPC)
        else Defeat / Flee
            Battle->>Core: OnDefeat() Callback (Execute Defeat Branch, No Rollback)
        end
    end
    Core->>Player: Close Dialogue & Unlock Controls (AC 20:8 / AC 5:4)
```

---

## 2. The 7 Pillars of the Runtime Engine

### 1. Unified Command Catalog (`Eve.emg`, `Mark.dat`, `Talk.dat`)
- **`Eve.emg`**: 145,254 opcodes across 1,119 maps representing all dialogue flows, battles, item transactions, and triggers.
- **Dynamic Opcodes & Multi-Branch Engine**:
  - 100% generic, data-driven execution with **zero hardcoding**.
  - All dialogue texts, choice options, item handovers, pet recruitments, and PvE battles are resolved dynamically at runtime from `Eve.emg`, `Talk.dat`, `item.json`, and `npc.json`.
- **Talk.dat Dual Lookup Engine**:
  - `Talk.dat` binary records are indexed by authentic `UInt16 TalkID` from bytes 0-1, as well as 0-based and 1-based record indices and direct byte offsets.
  - Opcodes with `DialogPtr = 1` or `DialogPtr = 2` dynamically look up exact speech strings for both Player and NPCs.
- **Rich Stage & Script Inspector in Studio GUI**:
  - Selecting any map and NPC/Object in the Event & Quest DB Studio dynamically parses and renders the entire `Eve.emg` bytecode tree, conditions, choice menus, item transactions, and spoken dialogue strings in real-time.

### 2. Robinson Crusoe (Rhodes Island — Harita #10035 / #10036) Başlangıç Görevi
- **Karakter:** Robinson Crusoe (`TemplateID: 12082`, `ClickID: 1` / Sandık `ClickID: 7`).
- **Ödüller:** **Sal (Raft — Item #48016)** ve **Robinson Crusoe Savaş Arkadaşı / Pet (`Pet ID: 12178`)**.
- **Otantik Diyalog Akışı (`Talk.dat` Kayıtları 1400..1425):**
  1. `Talk #1400`: *"Cough cough... Huh? Where am I?"* (Oyuncu kazazede uyanışı)
  2. `Talk #1401`: *"This is a deserted island. How did you end up here?"* (Robinson)
  3. `Talk #1402`: *"I was on the ship then suddenly it started violently shaking and I lost consciousness."* (Oyuncu)
  4. `Talk #1403`: *"I think you must have been shipwrecked and drifted to this island. You are still alive, which is a great blessing."* (Robinson)
  5. `Talk #1404`: *"It has been 28 years since I drifted to this island."* (Robinson)
  6. `Talk #1409`: *"What do you want to know?"* (Etkileşimli Seçim Menüsü)
     - **Seçenek 1 (Yeni Başlayanlar Rehberi — Talk #1410):** Can yenileme, ağaçlardan hindistan cevizi toplama ve sal yapımı anlatımı (`Talk #1413`..`#1416`).
     - **Seçenek 2 (Arayüz Tanıtımı — Talk #1411):** Sistem arayüzü ve kısayollar (`Talk #1417`..`#1421`).
     - **Seçenek 3 (Adadan Ayrılma — Talk #1412):** *"A while ago I built a raft. If you want to leave, just take it from the box next to me."* (`Talk #1422`..`#1425`).
  7. **Ödül & Katılım:** Sandıktan **Sal (Raft — Item #48016)** verilir ve Robinson oyuncunun ekibine (`Pet ID: 12178`) katılır.

- **`Mark.dat`**: 2,154 raw mark records grouped into Master Quests with multi-step `#01`, `#02`, `#99` progress lines.
- **`Talk.dat`**: 17,486 authentic dialogues parsed in 292-byte fixed records.

### 2. Strict First-Matching Rule Resolution
- Evaluates `EventSubEntry` conditions in client-defined order:
  - `unknownbyte1 = 1`: Player Level Check (`Level >= unknownword1`).
  - `unknownbyte1 = 2`: Item Check (`Has Item #unknownword3 x unknownword2`).
  - `unknownbyte1 = 4`: Companion Pet Check (`Has Pet #unknownword1`).
  - `unknownbyte1 = 5`: Gold Check (`Gold >= unknownword1`).
  - `unknownbyte1 = 15`: Free Inventory Slots Check.
  - `unknownword1 > 0`: Quest State / Step Check (`charquest` status `0/1/2`).
- The first fully satisfied safe branch wins and executes.

### 3. Choice Resolution (Accept / Deny / Neutral)
- **Menu Display**: Dispatches `AC 20:1 Type 6` choice menu.
- **Choice Routing**: On receiving choice packet (`AC 20:9`), matches choice index:
  - **Accept Branch**: Advances quest to Step 1, updates `charquest`, sends `AC 39` / `AC 24:1`.
  - **Deny Branch (Empty)**: Closes session without changing state, allowing quest re-offer.

### 4. StepQueue & Client Handshake
- Dialogues and scenes are enqueued sequentially via `Player.QueueData` and `Player.StepQueue`.
- Each `AC 20:6` dialogue acknowledgment triggers the next step; post-dialogue actions execute only after all speech frames conclude (`OnInteractionComplete`).

### 5. Quest Battles & Battle Context
- **Context Injection**: Passes `QuestBattleContext` (QuestID, Step, MapID, ClickID, `OnVictory`, `OnDefeat`) to `PvEBattleManager`.
- **Drop Isolation**: Suppresses generic monster loot tables; only explicit quest rewards from the victory branch apply.
- **Outcome Branching**: Victory triggers `OnVictory` (awards EXP, Gold, Items, Pet, and advances quest); defeat triggers `OnDefeat` without reverting completed steps.

### 6. Atomic Database Persistence & Double-Reward Prevention
- Single transaction commits `charquest` (`status = 2`, `current_step`, `completed_at`).
- Idempotent evaluation: Completed quests/events skip start branches to ensure rewards are never granted twice.

### 7. Map Phase & Actor Visibility Replay (`ReplayActorVisibility`)
- On map entry (`Map.Warp_In` / `AC 22:10`):
  - Hides recruited companion NPCs (e.g. Robinson on Beach 10035, Clive, Niss, Roca, Sam, Fred) if they are in the player's pet list.
  - Updates opened chests (`AC 22:1`) and despawns completed event entities.
