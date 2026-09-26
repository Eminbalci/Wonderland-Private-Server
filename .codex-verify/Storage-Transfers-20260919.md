# Storage transfers, 2026-09-19

## Cause and correction
Captured deposit request: F4 44 03 00 1E 02 07. Previous AC30 deposited one unit then re-added the entire bag with additive 23:5, sent several speculative storage layouts, and emitted 29:5 which opens the gold bank. Previous withdrawal added the full source stack while removing only a requested/default unit.

AC30 now follows native client send paths: 30:1/2 contain selected storage/bag slot bytes (entire selected stacks, not a quantity byte); 30:3 is storage source/destination; 30:4 is storage destination/bag source; 30:5 is bag destination/storage source. Native send dispatcher 0x2d0bb1..0x2d0f91 and UI 0x1dfc10, 0x1e026c, 0x1e0640 confirm these layouts.

Plan destinations under inventory/storage locks before changing source. Automatic selection requires capacity for the whole source; targeted same-item merge only transfers available capacity. Preserve durability, item identity, locked slots, stack limit. Bag removal is 23:9; additions use only actual quantity deltas in 31-byte 23:5 records. Storage uses 30:8 clear then 30:1 snapshot, followed by 30:6/7 to release pending selections. No AC29 emitted. Save through SaveCharacterData only after a change. No character database repair or point refund included.

## Validation
Old deployed Item Mall build: reproduced both bag re-add and gold-bank popup packets.
Staged storage-bin: 2,611 protocol/state assertions passed, including the 792 previous inventory/equipment-box/pet-amity checks; simulated native additive client counts match server after each transfer. Includes selected lists, drag directions, repeated transfers, partial merge, capacity rejection, locked slots, durability, malformed requests and count conservation.
Compile passed with only the two pre-existing CS1998 warnings. git diff --check passed. Live client replay pending deployment/restart; fixtures are not a live gameplay test.

## Deployment
Combines pending equipment stat toast and item-based pet-amity fixes. Stage: .codex-verify/storage-bin. Deploy-Storage.ps1 checks server stopped, backs up four runtime files, verifies hashes and unchanged database, leaves server stopped. Await user shutdown confirmation. Do not use the older pet-amity staging for this deployment.

Follow-up: user requests gameplay feedback in notification boxes instead of GM chat. Item Mall purchase success/errors, legacy storage notices and raft-break feedback now use native AC2:16. Equipment stats, item-use failures and pet amity already use that box in this staged patch. Administrative/system chat helper stays available.
Follow-up validation: compile passed; storage/inventory/amity suite 2611 assertions passed. Item Mall suite includes box output and rejection of GM-chat feedback. Native SQLite test dependencies copied from prior verified staging into storage-bin only.

Deployed 2026-09-19 12:20:12 after user shutdown confirmation. Four hashes match staging; database SHA256 unchanged. Binary backup: D:\Game Private\Wonderland-Private-Server\.codex-verify\deploy-backup-20260919-122012-310-storage. Server left stopped for user restart.
