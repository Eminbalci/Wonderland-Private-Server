# Redeem Voucher Exchange & Breillat Protocol

## Overview
Replicates official Wonderland Online Voucher (`Item ID 30002`) exchange interactions with Breillat (Map 10027 NPC ClickID 5) reverse-engineered from `2xreddemvoucherimvarkenbrelliatlakonustum.pcapng` and `Eve.emg` Event 4.

---

## Interaction Flow Sequence

```text
[Client]  --> AC 20 Sub 01 [05 00]                     (Click Breillat NPC #5)
[Server]  <-- AC 24 Sub 01 [6E C3 01]                  (Check / Set Voucher Flag 0xC36E)
[Server]  <-- AC 06 Sub 02 [01]                        (Lock Player Movement)
[Server]  <-- AC 20 Sub 10                             (Dialogue Stage 1 Init)
[Client]  --> AC 20 Sub 06                             (Step 1 ACK)
[Server]  <-- AC 20 Sub 01 [... 6E 77 03]              (Dialogue Window: TalkID 0x03776E - Breillat asking for vouchers)
[Client]  --> AC 32 Sub 02 [0F]                        (Advance Dialogue)
[Client]  --> AC 20 Sub 06                             (Step 2 ACK)
[Server]  <-- AC 24 Sub 01 + AC 20 Sub 10              (Flag Re-verify)
[Server]  <-- AC 20 Sub 01 [... EC 77 05]              (Dialogue Window: TalkID 0x0577EC - Breillat exchange confirmation)
[Client]  --> AC 32 Sub 02 [0F]                        (Advance Dialogue)
[Client]  --> AC 20 Sub 06                             (Step 3 ACK)
[Server]  <-- AC 23 Sub 07 [32 75 02 00 00 00]         (Consume 2x Voucher #30002)
[Server]  <-- AC 23 Sub 06 [48 7D 01 00 00...]         (Grant 1x Chocolate Ice Cream #32072)
[Server]  <-- AC 20 Sub 10                             (Fanfare sound effect)
[Client]  --> AC 20 Sub 06                             (Close Exchange Interface)
[Server]  <-- AC 20 Sub 08 + AC 05 Sub 04              (Unlock Movement)
```

---

## Code References
- Handled dynamically by [`EveEventInterpreter.cs`](file:///d:/GitHub/Wonderland-Private-Server/wlo.pserver.core/Game/Maps/Code/EveEventInterpreter.cs) under Event 4 (Sub #4 & Sub #5).
- Exchange item condition: `unkbyte1 == 2 && sub.unknownword3 == 30002` (Voucher).
- Consumption opcode: `Opcode 1` with `dialog4 == 65024` (takes 2x `#30002`) & grant opcode `dialog4 == 256` (gives 1x `#32072` / `#32071`).

