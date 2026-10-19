# Wonderland Online: Quest Animations, Cutscenes, and Staging Engine Architecture

## 1. Executive Summary

In Wonderland Online (WLO), narrative storytelling, quest progression, and companion recruitment rely on a coordinated multi-tier cutscene and staging pipeline:
1. **In-Engine Cinematic Cutscenes (`sty\*.sty`):** DirectDraw scene staging, camera panning, scripted actor trajectories, sprite animations, visual effects, and embedded subtitle dialogue tracks.
2. **Pre-rendered Full-Motion Video (`WLOnline.avi`):** Video playback orchestrated via Action Code `186:12`.
3. **Interactive Narrative Dialogue Trees (`Talk.dat`):** Server-driven conversational sequences (`AC 20:1 Type 1`) with animated NPC facial portraits and branching choices.
4. **Direct Entity Actuators (`AC 22:11`, `AC 22:12`, `AC 32:2`):** Real-time entity postures, emote bubbles, and heading directions.

---

## 2. In-Engine Staging Scripts (`.sty`) vs CG Video (`AC 186`)

The WLO client repository contains 768 compiled `.sty` scripts in `WLRI\sty\`:

| Cutscene Range | Category | Purpose | Example |
|---|---|---|---|
| `1..29` | Shipwreck / Prologue | Ship interior actions and transitions | `1.sty`, `23.sty` |
| `123` | Ship Storm Wreck | Storm rocking deck, monkey falling overboard | `123.sty` |
| `11000..19999` | Regional / Companion Arc | Overworld companion recruitment and quest staging | `12000.sty` (S. Monkey), `12008.sty` (Robinson Wakeup) |
| `25000..25165` | Major Storylines | Holy Village, Maya, Roca, Niss, Clive, Fred, Magellan | `25010.sty` (Well Mystery) |

### 2.1 The Two Cutscene Execution Mechanisms

```
+-----------------------------------------------------------------------------------+
|                        WLO CUTSCENE EXECUTION TAXONOMY                            |
+-----------------------------------------------------------------------------------+
                                          |
         +--------------------------------+--------------------------------+
         |                                                                 |
         v                                                                 v
[In-Engine Script: .sty]                                          [Pre-rendered Video: AVI]
- Action Code: AC 20:1 Type 5                                     - Action Code: AC 186:12
- Mode 1: Timed playback -> Emits AC 20:6                         - Movie ID: 1 (WLOnline.avi)
- Mode 2: Hold renderer until map teleport                        - Handshake: AC 186:9
- Staged camera, actor sprites, emotes, SFX                       - Full-screen media renderer
```

> [!IMPORTANT]
> `AC 186:12` is exclusively reserved for video playback (e.g., `WLOnline.avi`). In-engine staging scripts (`.sty`) must **never** be dispatched with `AC 186:12`, as doing so initializes unnecessary DirectShow media player pipelines that conflict with the in-engine `.sty` renderer.

---

## 3. Wire Protocol & Packet Framing

### 3.1 In-Engine Cutscene Trigger (`AC 20:1 Type 5`)
```
Byte 0..1:   0x14, 0x01 (ActionCode 20, SubCode 1)
Byte 2..4:   0x00, 0x00, 0x00 (Session padding)
Byte 5:      Step Index (0x01)
Byte 6:      Type = 0x05 (Cinematic Cutscene)
Byte 7:      Subject = 0x00
Byte 8..9:   Actor = 0x0000 (Target Actor ID or 0 for global)
Byte 10:     Mode = 0x01 (Mode 1: Normal playback with AC 20:6 ACK; Mode 2: Hold renderer)
Byte 11..14: Value = Cutscene ID (UInt32 Little-Endian, e.g. 12000 = 0x00002EE0)
Byte 15..16: Text = 0x0000
Byte 17:     Branch = 0x01
```

### 3.2 Client Completion ACK (`AC 20:6`)
When the `.sty` timeline finishes:
```
Byte 0..3: 0xF4, 0x44, 0x02, 0x00 (Header: Len=2)
Byte 4:    0x14 (Action Code 20)
Byte 5:    0x06 (SubCode 6: Action Completed ACK)
```

---

## 4. Root Cause Analysis: S. Monkey Recruitment & Missing CG Photo (`JRole_5_3.jpg`)

### 4.1 What `12000.sty` Contains
Analysis of `WLRI\sty\12000.sty` (4,219 bytes) shows:
* **Visual Track:** Camera centers on S. Monkey (`Actor 17162`) lying unconscious on the beach (`Map 11016`). Player character walks up, kneels down, performs the CPR/revival gesture, heart emotion icon (`E10658`), revival sound (`S10061`), and monkey stirs and stands up.
* **Embedded Dialogue Track (Offsets `0xfd7`..`0x1067`):** Contains 10 sequential dialogue nodes (TalkIDs `20038` through `20047`) spaced along the cutscene timeline:
  * `TalkID 20038`: *"Oh? It's a Monkey!"*
  * `TalkID 20039`: *"Squeak~ Squeak~ Squeak~"*
  * `TalkID 20040`: *"Monkey, are you ok?"*
  * `TalkID 20041`: *"(Revive the monkey)"*
  * `TalkID 20042`: *"Squeak! Squeak! Squeak!"*
  * `TalkID 20043`: *"Ok, you have regained consciousness."*
  * `TalkID 20044`: *"Be careful next time! Monkey."*
  * `TalkID 20045`: *"... ... ... ??"*
  * `TalkID 20046`: *"Oh? Why are you following me?"*
  * `TalkID 20047`: *"I don't have anything to eat, so don't follow me! Just stay back!"*

### 4.2 The Missing Photo: Story Album CG Cutscene `11011.sty` (`JRole_5_3.jpg`)
In authentic Wonderland Online, the dialogue sequence following CPR revival does not render over the bare sand map. Instead, `eve.Emg` Map 11016 Event 1 contains a secondary Opcode 8 trigger (`d1=1 d2=0 d3=0 d4=768 dw1=43` -> Cutscene `11011`):
1. **The CG Illustration (`JRole_5_3.jpg`):**
   - Packaged in client archive `pic\jRole.JMg`.
   - Depicts the baby monkey on the sand with a forehead bandage and teary eyes looking up at the player.
   - Identified in client binary `aLogin.exe` as Story Album CG Star 29 (`JRPJRole_5_3`).
2. **What `11011.sty` (695 bytes) Does:**
   - Client activates full-screen CG backdrop mode (`[0x73efa6] = 1`).
   - Loads and displays `JRole_5_3.jpg`.
   - Embeds dialogue TalkIDs `20048`..`20053` directly inside the script timeline, rendering them over the illustration:
     * `TalkID 20048`: *"Squeak~ Squeak~ Squeak~ (crying sound)"*
     * `TalkID 20049`: *"Oh? It seems that you have mistaken me for your mom?"*
     * `TalkID 20050`: *"Squeak~ (Nods vigorously)"*
     * `TalkID 20051`: *"Do I look like Female Monkey? How annoying!"*
     * `TalkID 20052`: *"Ah! That's ok! You can accompany me! It's better to have one than none."*
     * `TalkID 20053`: *"Squeak, squeak, squeak!"*
   - Sends client ACK `AC 20:6` when the player finishes clicking through all dialogues.
3. **Defect in Prior Server Implementation:**
   - The server previously intercepted the completion of Cutscene 12000 and dispatched standalone text dialogue packets (`AC 20:1 Type 1`), bypassing Cutscene 11011.
   - This caused the client to show regular NPC chat boxes on the beach map without loading `11011.sty` or displaying the `JRole_5_3.jpg` illustration.

---

## 5. Architectural Lifecycle for Quest Cutscenes & Companion Recruitment

To achieve 100% authentic quest staging, all story cutscenes adhere to the coordinated lifecycle:

```
[Phase 1: In-Engine Cinematic Staging]
  Server -> Client: AC 6:2 [1] (Posture Lock) + AC 5:30 [1] (Movement Lock)
  Server -> Client: AC 20:1 Type 5 Mode 1 CutsceneID=12000 (12000.sty CPR Animation)
  Server -> Client: AC 186:12 [12000]
         |
         v
[Phase 2: Client Execution & ACK]
  Client executes 12000.sty timeline (CPR resuscitation + embedded TalkIDs 20038..20047)
  Client -> Server: AC 20:6 (Cutscene 12000 Concluded ACK)
         |
         v
[Phase 3: Story Album CG Cutscene & Subtitle Progression]
  Server -> Client: AC 15:20 [29, 0] (Unlock Star 29 in Story CG Album)
  Server -> Client: AC 20:1 Type 5 Mode 1 CutsceneID=11011 (11011.sty / JRole_5_3.jpg)
  Server -> Client: AC 186:12 [11011]
  Client displays full-screen JRole_5_3 illustration with TalkIDs 20048..20053
  Client -> Server: AC 20:6 (Cutscene 11011 Concluded ACK)
         |
         v
[Phase 4: Resolution & Recruitment]
  Server -> Client: AC 20:10 (Fanfare SFX)
  Server -> Client: AC 22:10 (Despawn S.Monkey from Map)
  Server -> Client: AC 23:57 (Recruit S.Monkey TID 17162)
  Server -> Client: AC 24:1 / 24:5 (Quest 12002 Completed & Quest 12003 InProgress)
  Server -> Client: AC 20:8 (End Dialog) + AC 6:2 [0] + AC 5:4 (HUD Restore)
```
