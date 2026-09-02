# Monkey Pet Recruitment & Dialogue Protocol

## Overview
Replicates official Wonderland Online Monkey (Şempanze / Maymun - Companion Template `10727` / NPC ClickID 1) interaction and recruitment mechanics reverse-engineered from `maymungorevibaslangici.pcapng`.

---

## Interaction & Recruitment Flow

```text
[Client]  --> AC 20 Sub 01 [01 00]                     (Click Monkey NPC #1)
[Server]  <-- AC 06 Sub 02 [01]                        (Lock Player Movement)
[Server]  <-- AC 20 Sub 01 [... 02 E0 2E 00 ...]       (Stage 1 Dialogue: TalkID 0x002EE0 / 12000)
[Client]  --> AC 20 Sub 06                             (Step 1 ACK)
[Server]  <-- AC 20 Sub 01 [... 01 03 2B 00 ...]       (Stage 2 Dialogue: TalkID 0x002B03 / 11011)
[Client]  --> AC 20 Sub 06                             (Step 2 ACK)
[Server]  <-- AC 15 Sub 01 [0F 01 52B Pet Data]        (Add Monkey Template 10727 to Battle Team)
[Server]  <-- AC 19 Sub 01 [0A 43 00 00]               (Despawn Monkey NPC from Map)
[Server]  <-- AC 22 Sub 10 [01 00 FF FF]               (Flash Particle FX on Monkey)
[Server]  <-- AC 24 Sub 05 [02 00 01]                  (Complete Quest 2)
[Server]  <-- AC 20 Sub 10                             (Audio Fanfare)
[Client]  --> AC 20 Sub 06                             (Dialogue Completion ACK)
[Server]  <-- AC 20 Sub 08 + AC 05 Sub 04              (Unlock Movement)
```

---

## Code References
- Handled in [`QuestNpc.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/QuestNpc.cs) under the Monkey recruitment branch.
