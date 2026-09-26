# Inventory synchronization repair — 2026-09-19

Scope: server only. Client executable unchanged.

## Confirmed causes
- Player.WearEQ discarded the item removed from the bag; unWearEQ discarded the item removed from equipment. Neither sent the native transfer acknowledgement.
- Moving a partial stack returned a copy containing the original full quantity, and removed the source before checking the destination.
- Recovery sent AC23:208 as a quantity update. The native client uses AC23:9 for quantity subtraction; AC23:208 changes different item metadata.
- Use-item treated Equippos=8 consumables as equipment, guessed recovery from ID ranges and consumed unsupported items. Pet targeting used database slot instead of session ClientSlot.

## Changes
- Transfer ownership between valid bag/equipment slots under the inventory lock. Native AC23:17 wear and AC23:16 unwear acknowledgements; refresh character stats.
- Validate destination, cap quantity by available stock and stack space, then move. Reject incompatible occupied destinations without changing either item.
- Use the same recovery handler for AC23:15 and AC23:96. Derive HP/SP from item data (25/26, offset 100), cap use by stock, validate session pet target, send quantity decrement and stats.
- Unsupported consumables remain in inventory instead of disappearing. Their individual special effects are outside this patch.

## Validation
- Server Compile target succeeded; existing async warning remains. No live client replay performed.
- Test-InventoryProtocol.ps1: 473 compiled protocol/state checks, including equipment swap, persistence snapshot roundtrip, partial/overflow moves, food quantity/recovery, invalid targets, pet slot identity and conservation cases.
- Test-PetNativeProtocol.ps1: 55 checks passed against inventory-bin.
- Test-CombatSpeed.ps1: 210 checks passed against inventory-bin.
- git diff --check passed.
- Repair-MizakiLostEquipment.py rehearsed on an in-memory copy: restores Kimono 21013 and Wooden Shoes 24013 to free bag cells. Existing inventory rows and all other tables unchanged; second execution creates no duplicates; SQLite integrity check passed.

## Deployment
Prepared binaries: .codex-verify/inventory-bin (server EXE/PDB and core DLL/PDB).
Runtime destination: bin/Debug. Close server before deployment/data recovery to avoid file locks and autosave overwriting repair.
Recovery source: bin/Debug/ServerDataBase.db.before-pet-restore-20260919-0152.bak, only the two confirmed missing equipment rows for character 4510001.

Client replay pending: unequip/equip Kimono and shoes; split/merge/move stacks; consume Chocolate on player/pet; reconnect and check quantities/equipment persist.

Deployed 2026-09-19 11:26:48 after user confirmed server stopped. Four runtime file hashes match staging. Binary backup: deploy-backup-20260919-112648-188-inventory. DB backup: db-backup-20260919-112648-738761-inventory. Kimono restored to bag slot 11; Wooden Shoes to slot 12, quantity 1 each; verified after reopening database. Server left stopped for user restart.

Login follow-up 2026-09-19: reproduced NullReferenceException in SetBeginnerOutfit using previous runtime binary and copied database. Removed starter-outfit fallback from both existing-character load paths (character list and direct selection). Empty equipment is valid; new-character AC09 still grants its initial outfit. New build passed 22 database-backed list/cached selection/uncached selection/starter outfit checks and 473 inventory protocol checks. Four binaries deployed after user shutdown confirmation; runtime DB SHA256 unchanged. Backup: D:\Game Private\Wonderland-Private-Server\.codex-verify\deploy-backup-20260919-113138-422-inventory-login. Client login replay pending.
