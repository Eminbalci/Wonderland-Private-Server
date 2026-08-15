# Client Events & UI Handling Decompiled Specifications

This document outlines the client-side slot machine mini-game animations, equipment GUI toggle frames, and seasonal holiday system broadcasts extracted from the raw decompile code of `aLogin.exe.1.c`.

---

## 1. Slot Machine Lucky Draw SFX & Animations (`SlotmachRing`)

During Item Mall Lucky Draw spins, the client updates the UI panels and triggers animation/sound loops:

- **Sound Identifier**: `"SlotmachRing"`
- **Routines**:
  - Triggers localized play functions `FUN_0047bc94` using target animation identifier `"SlotmachRing"` to signal spin sequences.
  - Updates reel overlays and maps the winning index received from the server (via Opcode `104` Sub-opcode `1`).

---

## 2. Equipment Wear & Status Handlers

The client updates the character's inventory layout and stat overlays when equipment state alterations occur:

- **`Wearing` Event Hook**:
  - Code reference: `(**(code **)(*(int *)param_1[0x4c] + 0x34))((int *)param_1[0x4c],"Wearing");`
  - Triggered whenever the player equips or changes a gear slot.
- **Weapon Shop & Icon Templates**:
  - `icon_weaponry`: Renders weaponry sub-categories inside purchase and trade slots.
  - `Btn_weapon`: Dynamic button template for toggling/fitting weapon items.

---

## 3. Seasonal Holiday Events

The server can push system alerts indicating the status of localized seasonal holiday campaigns:

- **Spring Festival event**:
  - Code reference: `FUN_004a44cc(*(undefined4 *)PTR_DAT_004c8f18,0,"Jade Rabbit Spring Festival event has ended!");`
  - Logs the event completion message to the system chat logs when received from the server.
