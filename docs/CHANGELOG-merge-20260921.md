# Local gameplay and native protocol fixes — 2026-09-21

This changelog describes only the local changes relative to `Eminbalci/Wonderland-Private-Server:Develop` at `e5ac43cf74696d389f095601d2546ac6a9d00a67`. Features already present in that upstream revision are excluded.

The changes correct client/server state mismatches that could duplicate or lose inventory items, select the wrong pet, interrupt combat, or leave quest and map state inconsistent after reconnecting.

## Combat, skills, and character stats

- Correct character-to-pet action selection and acknowledgements so submitting a character action does not reopen the entire round or assign duplicate commands to the pet.
- Resolve ordinary actions in speed order with deterministic tie-breaking, keeping combo participants adjacent without moving slower actors ahead of intervening actions.
- Encode Defend as a reaction to an incoming hit instead of a self-targeted attack animation.
- Correct capture success/failure animations, roster updates, duplicate-template checks, and battle completion without replaying a second departure animation.
- Remove duplicate escape animations and correct battle-exit packet sequencing.
- Apply combat HP changes once at impact and synchronize current/max HP and SP without prematurely applying the final damage state on the client.
- Validate learned skills, target ownership, and SP before execution; treat invalid or stale submitted actions as Defend so the client can finish the turn.
- Treat native basic-attack IDs 10001–10007 consistently across validation, SP cost, damage, and animation timing.
- Choose basic-attack damage stats from the equipped weapon: wands/staves use MATK/MDEF, while other weapons and unarmed attacks use ATK/DEF.
- Use decoded skill effect, power, flat-harm, and learned-grade data in the local damage formula; this formula is not presented as verified official balance.
- Add configurable critical-hit rules and native critical-number styling without applying critical damage twice.
- Restore character-specific stunt selection and correct character/pet skill synchronization and proficiency updates.
- Estimate per-skill pacing from supported native animation paths, retaining conservative fallbacks and a specific return/camera allowance for Coherent Strength.
- Correct level/EXP synchronization, award three stat points per level, and update derived maxima before current HP/SP.
- Make GM stat-point grants saturate at 65,535 and update POINT separately without unintentionally refilling HP/SP.

## Inventory, storage, and NPC sales

- Transfer equipment between bag and worn slots under the inventory lock, preserving the actual item and sending native equip/unequip acknowledgements.
- Validate stack destinations before mutation and move only the quantity that fits, preventing loss or duplication during partial moves and merges.
- Send only newly added quantities in additive inventory packets instead of replaying the full bag.
- Plan complete reward deliveries and item exchanges before changing inventory, including space freed by consumed materials.
- Share item-data-based HP/SP recovery across native use-item requests, validate player/pet targets, and retain unsupported consumables instead of consuming them.
- Correct selected-stack and drag-and-drop storage requests, preserve durability/forge metadata, and remove unrelated gold-bank popups.
- Correct NPC sales to use selected whole stacks and the trailing sale mode, with typed result codes and data-driven prices.
- Classify only equipment slots 1–6 as wearable items when calculating NPC sale behavior.
- Preserve legitimately empty saved equipment sets instead of recreating starter gear when an existing character logs in.

## Item Mall, gacha, and forging

- Correct Item Mall balance/cart handling, category and stock synchronization, and capacity checks before charging or delivering purchases.
- Configure 24 gacha pools from native client reward IDs, quantities, and ordering; expose the same pool through previews and pack opening.
- Use documented custom gacha weights with progressively rarer later entries, preserving duplicate item IDs with different reward quantities as separate outcomes.
- Remove the invented Lucky-pack pool, reject unavailable purchases before charging, and avoid requiring an invented client patch.
- Add the missing Ice Snowflake item definition while retaining existing item records.
- Add Strong Scroll enhancement chains and separate point-based forging with native result packets, eligibility checks, and unchanged item metadata on failed attempts.
- Persist forge progress through inventory, equipped items, storage, and character loading, while keeping base and forge stat display separate.
- Add pet-voucher redemption with item-definition, ownership, party-capacity, and inventory checks before consuming the voucher.

## Pets and companions

- Separate persistent pet keys from session client slots across roster, battle, feeding, stat allocation, rebirth, GM operations, and hotel transfers.
- Correct native pet roster, follower, battle-selection, mount, and removal packets without broadcasting private roster data to other players.
- Preserve pet skills, equipment metadata, progression, and stat bonuses through save/load and hotel/team exchanges.
- Add native pet equip/unequip transfers and synchronize absolute displayed combat stats after equipment changes.
- Apply feeding effects to the selected pet using the item definition and validated inventory consumption.
- Apply death-related amity changes to the exact pet instance and use its client slot for native amity and desertion updates.
- Reject invalid pet stat types before spending available points, and refresh rebirth through roster/progression updates rather than a recruitment packet.
- Keep temporarily absent story companions in a persistent quest reserve so scripted departures and returns retain their state.

## Native quests, dialogue, and Notebook

- Add a resumable native EVE event runtime with explicit dialogue/cinematic acknowledgements, interaction cleanup, and guarded state transitions.
- Decode supported quest, item, companion, learned-skill, and gathering conditions from native data rather than selecting permissive fallback branches.
- Re-evaluate per-player NPC visibility from PreEvents and quest state after event progress, map entry, and reconnecting.
- Preflight reward capacity and required materials before applying quest mutations, preventing partial turn-ins and repeated reward delivery.
- Keep unsupported conditions/actions and configured incomplete events disabled instead of inventing missing behavior.
- Correct story-specific companion and event flow for Niss, Xaolan, Emily, Stewart, and related supported native branches.
- Handle decoded actor-state/event actions and dialogue termination without trapping the player in an unfinished interaction.
- Persist supported water-gathering inventory changes, quest marks, and timers together; leave unverified gathering pools disabled.
- Add Notebook journal synchronization using native mark IDs and completion bits, with idempotent progress updates.
- Persist monster discoveries and synchronize the native monster book using NPC template IDs.
- Remove automatic demo constellation grants from login while retaining supported native Notebook data paths.

## Maps, persistence, and integration corrections

- Invalidate cached live character objects on save to prevent stale map IDs from being combined with new coordinates after reconnecting.
- Persist Record Point destinations before acknowledging them and restore the saved return point on login.
- Treat authored North Island maps 60000–60014 as outdoor maps; identify tents by their actual type instead of an ID threshold.
- Use the correct player's exterior return location when leaving a tent, including valid destinations above map ID 60000.
- Correct map-transition acknowledgements, entry-state cleanup, and native field encounter actor visibility/respawn handling.
- Preserve both pet `skills` and `equipment_meta` columns when integrating the current database setup code.
- Adapt trade and mail capacity checks to the local quantity-aware inventory API.
- Route the GM clear-inventory control through the existing native removal path and keep the legacy GM-chat notification helper disabled.

## Data, documentation, and verification

- Add local configuration for critical hits, gacha rates, forging, pet vouchers, NPC sale prices, and disabled quest events.
- Include targeted protocol/state regression fixtures and investigation notes for the local gameplay changes.
- Link this changelog from README; the pull-request description uses this same local-only change list.
- The pull-request branch retains the upstream sample database and excludes the local runtime database snapshot and its unpublished history.
- The merged source built successfully in `.codex-verify/upstream-merge-bin`; existing EntityFramework reference and source warnings remain.
- The isolated build uses the tracked `QuestRuntimeBuild.targets` override to skip the absent `ServerDataBase.db.temp` input.
- Regression results: 48 of 55 suites passed; seven failures were reproduced against the pre-merge local checkpoint `3d8c1dc`.
- The focused `UpstreamMerge` suite passed 29 checks; `CheatPointsHp` passed 267 checks.
- Existing failures remain in `CombatActionSelection`, `CriticalHits`, `MallForging`, `Notebook`, `NpcDialogServices`, `PetAmityFeedback`, and `QuestMinimap`; the last reports 103 failed checks out of 22,518.
- Source inspection, compiled fixtures, and build success do not establish live client gameplay, administration UI, or MySQL validation; those checks remain pending.
- The merge/build work preserved the local runtime database and server binaries; this pull request does not deploy them.
