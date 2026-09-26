# Original client gacha - 2026-09-19

User requests original client item pools, later entries rarer, and reversion of the three invented pools/client patch. Ice Snowflake27025 confirmed as Snow Girl/Dark (Black) Snow Girl pet stat food, not HP/SP or amity food.

- Extracted24 tables /394 reward rows from original aLogin.exe SHA256 CA19EE087B601182E872235E58E6BF367F4BB21879226DFE3CC96F1C99624F61. Native preview2a3cc4 uses pointer tables of8-byte rows; first fields are itemID ushort and quantity ushort. Third client field is retained only as audit evidence, not assumed official probability.
- Gift Pack34171 has35 rows matching screenshot order: down each column then next column. 7Colors Egg34172 contains26 voucher/card items; native preview renders their pet pictures, but rewards are vouchers, not direct roster entries.
- Data/gacha_packs.json contains native IDs, quantities, order, and custom weights: geometric decline first:last about100:1, rounded to10000 total per pack. Every later row strictly rarer. Data/gacha_rates.md lists exact percentages. Repeated item IDs with different quantities remain separate outcomes.
- Strengthen and Gear use original25/21 outcomes. Lucky34333 invented pool removed, unavailable purchases rejected before charging and existing pack retained. Remove only Lucky's added shop offer; existing other prices/offers unchanged. Four placeholder-named Test pools configured as source data but not added to shop. New pools not already on sale are configured for opening; no invented prices added.
- One missing server item definition27025 appended from client Item.dat: Ice Snowflake, type20 Pet_Fruit, icon5229, no equipped stats. All4776 prior 47-byte records untouched. Existing pet-food use handlers are not expanded by this data migration.
- Native23:75/128 handlers preserved; quantity-aware atomic exchange consumes one pack and grants exact outcome quantity. Native91 preview uses same pool. No invented client patch required; deploy restores exact original EXE.

Validation:3825 gacha protocol/state checks (all pools, exhaustive10000 rolls per pool, native source row matches, exact quantity, additive client inventory replay, purchases, full/locked bags, combat, malformed slots, concurrent last pack, invalid configs).2620 inventory/pet/notification checks. Compile passed with existing warnings. Database rehearsal removes only Lucky offer; other tables unchanged. Actual in-game replay pending deployment.

Stage client-gacha-bin explicitly retains existing5000ms spell/750ms combo timing. Deferred combat-handoff-bin refreshed with new gacha code, still4500/250, remains un-deployed.

Deploy: python .codex-verify/Deploy-ClientGacha.py --apply after BOTH server and game close. Preflight hashes, backup four server binaries/client/DB, transactional catalog-only deletion, binary rollback on failure. No character-data edits; leave stopped for user's restart. Source Data files are prepared for the next start.

2026-09-19 Forging update: client-gacha-bin now also contains IM Strong Scroll and regular point Forging; see Mall-Forging-20260919.md. Manifest refreshed including Data/mall_forging.json. Baseline combat timing preserved. Still NOT deployed.


Deployment 2026-09-19T14:42:57: all pending changes applied from combat-handoff-bin, including4500ms/+250ms timing; eight runtime file hashes and original client verified; character tables unchanged. Backup: D:\Game Private\Wonderland-Private-Server\.codex-verify\deploy-backup-20260919-144257-032053-all-pending. Server/game left stopped for user restart.
