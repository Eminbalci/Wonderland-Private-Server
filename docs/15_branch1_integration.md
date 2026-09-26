# Branch1 integration — 2026-09-19

Merged upstream `cheatengine-branch1` at `df26bd8e4b5467428bcc2bb40f88bc07b7d36112` into local Develop. The deployed local fixes were first saved as checkpoint `dd6ebfb`.

## Included

- Native EVE parsing through the full data file, quest condition grouping, event selection, NPC visibility, per-player quest prop state and correct replay target IDs.
- NPC database stats/cache, friend add/remove/online notifications, pet EXP column in the character editor.
- Administrative EXP rate and database configuration UI. Combat rewards apply the same EXP multiplier once to players and pets. Player rewards no longer add existing current EXP a second time.
- Upstream drop policy: effective chance `max(5%, configured percentage * 0.35)`; stop after the first successful valid loot entry. The entry's configured stack quantity remains unchanged. The existing rejection of unknown item IDs is retained. This is server balance policy, not a claim of official WLO probabilities. In particular, configured 0% also becomes 5% under this policy.

## Local behavior retained

- Selected/living battle pet rules, native client slot mapping, roster limits, pet progression and skill packets.
- Combat SPD order, action ownership, animation pacing, capture/escape outcomes, defense and critical hits.
- Atomic inventory/ground pickup, equip/unequip, consumables, storage, HP/SP, stat allocation, mall checkout, native gacha and forging.
- Dialogue feedback and suppression of legacy GM chat messages.

Upstream debug inventory/pet commands, guessed companion skill lists, raw-slot login roster replay, alternate pet growth/stat packets and scratch investigation scripts were deliberately excluded. Upstream documentation describes its original branch; this note and the retained native protocol tests describe the integration's differences.

## Additional fixes during integration

- NPCs with no EVE event return to the normal service handler, so storage/shop fallback is reachable. A regression test failed before this fix and passed afterward.
- NPC caches are per database instance, cleared when reloaded/imported/edited, and do not retain guessed fallback records.
- Static database operations respect explicit absolute database paths.
- SQLite-to-MySQL migration no longer swallows schema/table failures or reports rolled-back rows as successful. Existing target tables are permitted; failure explicitly notes that earlier tables may already be committed. Migration is not atomic across all tables.
- EXP scaling handles nonpositive rewards, nonfinite/invalid rates and native integer limits.

## Validation

- Full Debug build succeeded into `.codex-verify/branch1-bin`; existing warnings remain.
- `Run-Branch1Regression.ps1`: 21 suites passed against the final build, with default database operations redirected to isolated fixtures.
- New feature suite: 42 checks covering EXP accounting/scaling, quest state isolation, compound conditions, NPC service fallback, drop boundaries/cap, NPC cache refresh and missing migration source handling.
- Real client EVE data parsed successfully: 1,119 maps.
- Existing suites include 7,887 critical-hit, 3,825 gacha, 3,535 forging, 2,611 storage, 1,010 pet progression, 210 SPD and 55 native pet layout checks, plus inventory, login, vitals, stat allocation and capture/pacing tests.
- Older `Test-ItemMall.ps1` targets the removed BundleCount cart model; older `Test-MallStock.ps1` asserts the superseded 206-offer catalog. Current mall-category and client-gacha suites cover the active cart/catalog instead. Source-extraction tests use PowerShell 7; compiled .NET Framework tests use Windows PowerShell.
- Gameplay replay of quest/NPC presentation and real MySQL migration remain untested. Fixture/build checks do not establish visual client behavior or production MySQL schema compatibility.

## Deployment

This task merges and commits locally only. The compiled candidate is staged in `.codex-verify/branch1-bin`. No runtime server/client binary or live character database was replaced. No push was performed.
