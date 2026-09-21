# Item Mall checkout fix - 2026-09-19

## Cause and protocol
AC34:1 mode 1 requests balance before checkout. Old server incorrectly bought catalog item 57205 for 32 IM and sent AC35:4 with zero balances. Item 57205 is absent from the server item DAT, so no item was delivered. Native AC35:4 at 0x2eadc6 sums its first two DWORDs and invokes checkout 0x242ae4 against that zero balance.
Actual checkout at 0x242230 / outgoing dispatch 0x2dacfb is AC75:1, row count, repeated (itemID ushort, bundleCount byte, quantity byte, orderIndex ushort). Bonus AC75:5 uses this row layout. AC75:4 at 0x2411f0 expects those six bytes plus a success byte.
AC75:3 at 0x2414ec reads IM balance, spent points, item ID, count. Bonus balance uses AC75:9 at 0x24181c, not the spent field.

## Patch
Balance requests never purchase or refresh the pending cart. Return actual checkout balance. Validate full cart, server price, bundle and row identity; plan complete delivery before changing inventory or charging. Reject malformed/invalid items/full bags without deductions. Preserve bundle quantities above 255. Send only additive AC23:5 delivery deltas, separate IM/Bonus balance packets and correct cart acknowledgments. Save successful purchases and report exact cost using system text. Catalog advertises exact charged price.

## Validation
- Previous runtime reproduced: 1000 -> 968 IM on balance request, checkout balance 0, no item delivered.
- Compile succeeded with existing CS1998 warnings.
- 181 Item Mall packet/state checks passed.
- 665 inventory protocol/state checks passed against new binaries.
- Native protocol/fixture checks do not replace live client checkout; user replay pending.

## Proposed refund, not applied
Five confirmed wrong charges of 32 IM in wlophoenixlogFile_20260919.txt at 00:10:24, 11:45:10, 11:45:22, 11:45:36, 11:45:46. Account userID 1 balance fell from 1758 to 1598. No inventory row for item 57205 remains. Repair-ItemMallPoints.py tested on a copy: restores 160 IM, verifies every other row unchanged, keeps backup/audit file. Automatic approval review requires explicit approval for this balance mutation; awaiting user's choice. Deploy binaries independently with live database unchanged.

Deployed 2026-09-19 12:02:33 while server was already stopped. Four hashes match staging; database SHA256 unchanged. Binary backup: D:\Game Private\Wonderland-Private-Server\.codex-verify\deploy-backup-20260919-120232-556-item-mall. Server left stopped for user restart.
