# Inventory input follow-up — 2026-09-19

User reports: double-click cannot unequip; drag moves 2 by default; apparently empty cells reject items; no equipment stat change notification.

Confirmed causes:
- Player.ProcessSocket invoked AC23, then EquipManager.ProcessSocket and Inventory.ProcessSocket again. AC23:10 (04 01 17) moved one item twice and emitted two acknowledgements. The same legacy path can undo an equipment swap.
- Live log double-click requests repeatedly targeted bag cell 9. Database held ID 28014 there, absent from itemDat.wpdat and invisible to client. IDs 28006/28007 also blocked apparently empty cells.
- Character stats were refreshed but no stat-delta message was sent.

Fix:
- Skip legacy inventory/equipment processing for AC23. Other opcode handlers retain their current path.
- Send actual net equipment stat differences after successful wear/unwear, e.g. DEF +1 or DEF -1.
- Reject unknown item grants in both Inventory.AddItem entry points instead of manufacturing invisible placeholders.
- User explicitly authorized approximate replacements of existing invisible items. Repair-MizakiInvisibleItems.py maps 28006 to Apple 41040, 28007 to Small Pineapple 41050, and 28014 to Coconut 41066. This is not proof of original identity. Nine Mizaki bag rows are affected; preserve counts/positions and all other data. Back up original DB before apply.

Verification:
- Old runtime reproduced one drag moving two items and sending two replies through Player.ProcessSocket.
- New compiled runtime passed 493 inventory checks through Player.ProcessSocket (not just the action handler), including stack count, single acknowledgement, wear/swap/unequip, stat messages, consume, unsupported IDs and recorded client requests.
- New runtime passed 22 database-backed character login checks on copied data.
- Repair rehearsal passed, repeated repair makes no extra changes; integrity check passes, all quantities/positions and unrelated rows/tables preserved.
- Compile succeeded with existing CS1998 warnings. git diff --check passed.
- Live client replay pending after deployment: double-click unwear, drag 1 food item, use previously blocked cells, observe DEF change messages.

Deployed 2026-09-19 11:41:56 after user shutdown confirmation. Four file hashes match staging; binary backup: D:\Game Private\Wonderland-Private-Server\.codex-verify\deploy-backup-20260919-114156-070-inventory-input. Server left stopped for user restart.
