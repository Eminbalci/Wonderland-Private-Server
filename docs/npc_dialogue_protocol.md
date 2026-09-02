# NPC Dialogue & Generic Interaction Protocol

## Overview
Replicates official Wonderland Online client-server dialogue exchanges captured from live traffic (`2tanenpcylekonustumikisinindegoreviyoktu.pcapng`).

## Dialogue Flow Sequence (5-Step Protocol)

```text
[Client]  --> AC 20 Sub 01 [ClickID (2B)]            (Interact Request)
[Server]  <-- AC 06 Sub 02 [01]                      (Lock Player Movement)
[Server]  <-- AC 20 Sub 01 [Dialog Frame + TalkID]   (Display Graphical NPC Dialogue Bubble)
[Client]  --> AC 32 Sub 02 [NPC Target]              (Face NPC / Advance Dialog)
[Client]  --> AC 20 Sub 06                           (Close Dialog Window)
[Server]  <-- AC 20 Sub 08 + AC 05 Sub 04            (Unlock Player Movement)
```

## Packet Formats

### 1. Client NPC Click (`AC 20 Sub 1`)
- **Hex**: `F4 44 04 00 14 01 [ClickID_LE (2B)]`

### 2. Movement Lock (`AC 06 Sub 2`)
- **Hex**: `F4 44 03 00 06 02 01`

### 3. NPC Dialogue Bubble Box (`AC 20 Sub 1`)
- **Hex**: `F4 44 12 00 14 01 00 00 00 01 01 03 [ClickID (1B)] 00 01 00 00 00 00 [TalkID (3B LE)]`
  - Portrait `0x03` = NPC Avatar
    - Mary Lou (Kelan Village Pig Girl): `0x212977` ("These are my pigs.")
    - Kelan Villagers: `0x21284C` ("Welcome to Kelan Village.") / `0x2124C2` ("This is Kelan Village...") / `0x21303F`
    - Village Guards: `0x212A75` ("There are many fierce beasts. Careful not to be injured.")
    - Guideposts / Signs: `0x212CB8` ("There is a larger village named Welling Village at the south.")
    - Ship Deck Passengers (Map 10017): `0x0175B9` (Boy) / `0x0175BA` (Girl)

### 4. Dialog Close & Unlock (`AC 20 Sub 6` -> `AC 20 Sub 8`)
- Client sends `14 06`.
- Server responds with `14 08` and `05 04`, restoring full movement to the character.
