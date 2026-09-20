# Native mall category correction

Live log: 2026-09-19 12:23:53, 12:24:07 and 12:24:17, Mizaki sends F4 44 09 00 4B 01 01 75 DF 03 01 01 00. Native cart row is itemID=57205, category=3, quantity=1, order=1. The third row byte was previously misidentified as bundle size; this rejected the catalog entry with Count=1. Native catalog reader 0x240693 stores wire category at local d9, following order d5 and ID d7; checkout 0x2422a5 sends the corresponding record byte +4 after ID. Server now checks category exactly as serialized in catalog, and derives bundle delivery from server entry.Count. Success/failure ACK echoes category.

Read-only database row has ID57205, category3, count1, price32, order1. Live itemDat.wpdat has no item57205. This item still cannot be delivered until compatible item definitions are supplied; no placeholder or substituted item created. Cart validation now reports missing server item explicitly before balance mutation. Prior invalid-cart rejection already returned before charging, so this patch performs no point adjustment.

Built to mall-category-bin. 233 Item Mall checks passed including captured request, valid category purchase, bundle20 x15, bonus, bad category/order, no-charge failures, native ACK, category fallback and direct purchase. 2611 existing inventory/storage/amity assertions passed. Compile passed with pre-existing CS1998 warnings; git diff --check passed. Live gameplay pending deploy/restart.

Includes pending removal of three vault-open chat notices. Deploy-MallCategory.ps1 backs up and verifies four runtime binaries, leaves database untouched and server stopped. Supersedes quiet-storage-bin staging. Not deployed yet.
