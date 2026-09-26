# Mizaki: three visible pets versus four stored carried pets

Observed UI: Robinson, Xaolan, Grape Mons, one empty row.
Confirmed runtime data for charID 4510001: carried slots 1 Robinson, 2 Xaolan, 3 Grape Mons, 4 Grape Mons.
Map.Warp_In sends pets in stored-slot order. RegisterClientPet suppresses the duplicate template at slot 4. Capture capacity still counts that stored entry. This is a legacy hidden duplicate, not four usable party pets. Earlier explanation using stored count as visible count was incomplete.

Repair prepared in Repair-MizakiHiddenPet.py:
- Preserve visible slot 3 and all other pets.
- Move only inactive/unridden Grape Mons in carried slot 4 into first free Pet Hotel slot.
- Preserve all stats/equipment/EXP and total number of pet records.
- Require stopped server, create full SQLite backup, use a transaction and assert every other row unchanged.
- Abort if expected pair changed, target active, or hotel full.
- Rehearsal passed on an in-memory SQLite backup of current runtime DB: three carried pets afterward, hidden duplicate in Hotel slot 1, all other pet rows unchanged, integrity_check OK.
- Actual application pending server shutdown. No server binary change required. Existing capture/hotel-withdraw guards prevent adding another carried duplicate. Same species still cannot coexist in this native client party; another species can fill the freed fourth slot.

Applied after user-confirmed shutdown at 2026-09-19 11:03:29. Independent read confirms 3 carried pets and 1 Grape Mons in Hotel slot 1. Server left OFF. Full DB backup and changed-row audit saved by repair script.
