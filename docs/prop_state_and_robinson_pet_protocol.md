# Map Prop State & Treasure Chests Protocol

## Overview
Replicates official Wonderland Online interactive lootable objects (Crates, Chests, Barrels) reverse-engineered from `digersandiklaritoplama.pcapng` and `crateatiklayipstatedegistiripcikolatakazanma.pcapng`.

---

## Supported Interactive Loot Props

| ClickID | Object Type | Map | State Effect | Reward Item | Quest Updated | Notice Banner |
|---|---|---|---|---|---|---|
| **3** | Wooden Crate | Beach (10036) | `AC 22:1` (Open State 1) | Chocolate (`32001`) x1 | Quest 70 | `TalkID 0x087D4B` |
| **5** | Fruit Crate | Beach (10036) | `AC 22:1` (Open State 1) | Coconut / Potion (`32005`) x1 | Quest 76 | `TalkID 0x0C7D49` |
| **6** | Treasure Chest | Beach (10036) | `AC 22:10` (Flash Particle) | Gold Coins (`32010`) x1 | Quest 74 | `TalkID 0x01A06A` |

---

## Interaction Sequence
1. Player clicks prop (`AC 20 Sub 1 [ClickID]`).
2. Server broadcasts prop sprite state (`AC 22 Sub 1`) or particle effect (`AC 22 Sub 10`).
3. Server plays UI sound (`AC 20 Sub 11 & 10`) and displays reward banner (`AC 23 Sub 6`).
4. On dialog close (`AC 20 Sub 6`), server updates Quest flag (`AC 24 Sub 5`), awards item to player inventory, and unlocks player movement (`AC 20 Sub 8` + `AC 5 Sub 4`).
