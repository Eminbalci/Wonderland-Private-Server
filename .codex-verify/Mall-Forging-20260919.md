# Item Mall Forging - 2026-09-19

## User rules
- IM equipment uses Strong Scroll30101: target+N costs N scrolls,100% success, no points.
- Other eligible equipment costs3 IM Points per attempt (native price at24d881),50% independent chance. Success adds one Forge progress step, worth2 total stat points. Failure spends only points and preserves ID, durability and progress.
- Two nonnegative stats get+1 each; one eligible stat gets+2. Negative stats remain unchanged; their share goes to the other eligible stat. Max native progress200. No token consumption was added; user requested points.

## Root cause and protocol
- Live log13:23:56: AC75:3 payload0D, native one-byte bag slot13. Mizaki had11103 Sky Sword+1 there. AC75 had no case3, silently dropping the request.
- Strong Scroll families extracted from original client table4b6c3c (pointer4c9dd8),29 records of24 bytes.20 families /176 steps have server item definitions;95 item IDs from remaining chains are absent and cannot be granted/charged. Families remain exact client order (not itemID+1).
- Point eligibility extracted from original Item.dat using native24d200 checks: slots1..5, no0x200 control flag, no special field at413, at least one stat type other than0/250, special disallowed IDs excluded; IM chains take precedence.1940 configured IDs; actual definition and usable nonnegative stat also required at use time.
- S->C75:6 result6 means IM enhancement success and SEB0300 sound; result5 missing scrolls, result8 rejection/reset. Remove old item with23:9 before additive23:5 replacement at same slot.
- Point success uses75:6,1,slot,forge,0. Failure uses75:6,2 and does not modify metadata. Both have native message box handling. No GM chat.
- Inventory/storage31-byte records: forge byte23, i.e.tail[18] after slot/ID/count/damage. Native3dc9d0 parser passes it to3d7cdc +0x30. Equipped21-byte serialization: forge byte15, tail[12]; verified3fc308->438dfc stores it at equipment model+0x18.

## Implementation and validation
- Added MallForgingManager, AC75:3 handler, startup config load, Data/mall_forging.json.
- Item.Forge copied/reset with its item, persisted in existing DBforge column through bag/equipped/vault plus cached/uncached character loads. Inventory and vault deltas carry metadata; equipped login packet carries it. Equip calculations derive bonuses without modifying shared item definitions.
- Account then inventory locking matches mall checkout; all scroll costs/next data checked before mutation.3-point attempts call existing account persistence callback and character save.
- Build passed (only existing CS1998 warnings).3535 dedicated forging checks: native family/order, exact costs, both outcomes, invalid/locked/full/battle input, concurrent last scroll/points, positive/negative stat split, bag/equip/vault persistence on copiedDB.3825 gacha,493 inventory,233 current mall,2620 current inventory/storage/dialog,22 login checks passed.
- Old Test-ItemMall/Test-StorageTransfers/Test-InventoryFeedback harnesses are stale (BundleCount / GMchat expectations); use current Test-MallCategory and Test-NoGmMessages instead.
- Actual game UI/sound replay remains pending deployment.

## Staging only - NOT deployed
- mall-forging-bin and refreshed client-gacha-bin contain this change plus previously deferred24 client gacha pools, baseline combat5000ms/+750ms. client-gacha-deploy-manifest.json includes mall_forging.json and refreshed hashes.
- Deploy-ClientGacha.py rehearsal passed on a copy: only legacy Lucky offer removed,199 offers retained, other tables unchanged. Apply only when user requests deployment and closes game/server; script restores original client and backs up binaries/client/DB. No live DB/client/binary write in this turn.
- combat-handoff-bin also refreshed with Forging but retains deferred4500ms/+250ms; NOT deployed. Source still has deferred timing. Do not confuse staging folders or apply old binaries over this feature.
- Live server was already stopped at13:46; aLogin was still running. No process was stopped by this task.

## Separate base and forge display (user follow-up)
- Preserve original item definition/ID for point forging and send only the independent Forge progress byte. Do not rewrite StatusUp with a combined total.
- Verified original native client already formats base and forge separately: bag/item tooltip count at288420..288491, base string28891d..2889d5, forge suffix288a9b..288b1e, concatenation288b58..288b6e. Equipped tooltip equivalents1ac646..1ac6cf and1acd65..1ace56. Literal28a8ec /1ad6e0 is '+'. Native style includes a separating space: ATK: +10 +2, never ATK: +12.
- One positive supported stat gets2*Forge; two positive supported stats getForge each; negative stat has no suffix and does not participate in the native count. IM upgraded IDs retain native base-stat display; their Forge metadata is zero.
- No additional binary patch is needed for this requested separation. Keep pending deployment restoring original client (retired custom gacha hooks removed); do not deploy a speculative tooltip patch. Native analysis proves code formatting, not an in-game visual replay.


Deployment 2026-09-19T14:42:57: all pending changes applied from combat-handoff-bin, including4500ms/+250ms timing; eight runtime file hashes and original client verified; character tables unchanged. Backup: D:\Game Private\Wonderland-Private-Server\.codex-verify\deploy-backup-20260919-144257-032053-all-pending. Server/game left stopped for user restart.
