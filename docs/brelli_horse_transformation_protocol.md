# Brelliat Swap & Character Transformation Protocol

## Overview
Replicates official Wonderland Online Brelliat (NPC ClickID 5) place-swapping dialogue choice and character shape transformation reverse-engineered from `brelliatlayerdegistirdim.pcapng`.

---

## Complete Interaction & Transformation Flow

### 1. Dialogue Stage 1 to 3
- `AC 6 Sub 2`: `06 02 01` (Lock player movement)
- `AC 20 Sub 1`: TalkID `0x0777B0` (Step 1)
- `AC 20 Sub 1`: TalkID `0x0777B1` (Step 2)
- `AC 20 Sub 1`: TalkID `0x0777B2` (Step 3: "Would you like to swap places with me?")

### 2. Dialogue Choice Prompt (`AC 20 Sub 1`)
- **Payload**: `20 01 00 00 00 04 06 03 05 00 00 00 00 00 00 01 00 07`
- Client Submits Option via **`AC 20 Sub 9`**:
  - `0x1E` (30): **YES (Evet)** &rarr; Trigger Transformation Swap!
  - `0x1F` (31): **NO (Hayır)** &rarr; Cancel / Polite refusal dialogue (TalkID `0x0A76F0`).

### 3. Transformation Sequence (When Saying "Yes"):
1. **Confirmation Dialog**: `AC 20 Sub 1` with TalkID `0x0977B4`.
2. **Quest Flag Update**: `AC 24 Sub 5` (`18 05 45 00 01`).
3. **Particle Effect Animation**: `AC 22 Sub 10` (`16 0A 05 00 FF FF`).
4. **Model Transformation (`AC 5 Sub 1`)**:
   - `05 01 [CharID (4B LE)] [ModelID (2B LE: 0x56BE / 22206)]`
   - Transforms character visual sprite into **Brelliat**!
5. **Step 5 Concluding Dialog**: `AC 20 Sub 1` with TalkID `0x0977B6`.
6. **Release Movement**: `AC 20 Sub 8` on window close.
