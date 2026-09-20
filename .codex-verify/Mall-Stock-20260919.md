# Available Item Mall catalog - 2026-09-19

The224-row live catalog contained25 offers whose IDs do not exist in Data/itemDat.wpdat. Remove only those rows and append7 supported consumables: total206 offers (144 IM,62 Bonus).

## Added IM offers
| Item | Quantity | IM price |
|---|---:|---:|
| Chocolate |50|1|
| Rice Wine |50|2|
| Tao Rice Ball |10|90|
| Smiling Jelly |1|42|
| Full Jelly |1|54|
| Bless Jelly |1|60|
| Spring Jelly |1|24|

New prices are local defaults guided by existing recovery/amity offers; existing valid prices and quantities remain unchanged. No additional gift boxes/lucky bags were added because an item definition alone does not prove an implemented reward-opening operation. Existing199 valid-definition offers are preserved; their every gameplay effect was not audited in this change.

Both AC75:1/10 and port6416 catalog publishing filter against loaded item definitions. Purchase already rejects absent item data before any charge. Seed JSON matches the cleaned catalog; emergency fallback now contains these7 real consumables instead of unrelated IDs mislabeled as gems/pet food.

## Validation
- Build succeeded (two existing CS1998 warnings).
-613 new availability/packet/purchase/use checks: main and secondary catalog filtering, unchanged row identity and price, empty catalogs, all7 new purchases with exact balance, delivery quantity, food/amity effects, one consumption and sound, stale missing-item cart rejection.
-233 existing mall transaction checks passed.
-Migration rehearsal on a SQLite copy:25 removed,7 added,206 total; second run no-op. Every non-catalog table hash unchanged; existing valid rows unchanged; integrity_check=ok.
-git diff --check passed.
-No live client visual verification yet. Migration script backs up SQLite and requires stopped server; deployment backs up four binaries and leaves server stopped.

Deployed 2026-09-19 12:45:51 while server stopped. Four binary hashes match. Catalog migration removed25 and added7; all non-catalog tables unchanged. Binary backup: D:\Game Private\Wonderland-Private-Server\.codex-verify\deploy-backup-20260919-124550-417-mall-stock. DB backup recorded in migration output. Server left stopped.
