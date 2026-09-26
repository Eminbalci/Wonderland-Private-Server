# Native gacha open and contents preview - 2026-09-19

## Evidence and implementation
- Live Lucky Pack34333 double-click emits AC23:128 with UInt16 slot12. Gear34229/Strengthen34199 native branch36167a emits AC23:75, also UInt16 slot. Both now route to the existing atomic pack exchange; invalid/full/locked/unconfigured packs are retained.
- AC91 is pack contents, not a bonus claim protocol. Native1adfb8 queries91:1 with packId ushort + cache version byte. Native1ae0ac consumes91:2, packId, version, (rewardId ushort, quantity byte) rows. Replaced wrong hardcoded rewards and arbitrary fake claim route. Preview reads the same configured pool as opening; no currency/inventory mutation.
- Native preview eligibility3d19cc/3d3694 excludes Lucky. Gear/Strengthen use hardcoded old content in2a3cc4. Client patch adds a small executable section with three targeted hooks; only IDs34199,34229,34333 use dynamic server content. Other IDs retain original continuations. No item data, rates, or unrelated client logic changed.
- Client patch script requires exact original SHA256 and hook bytes. Staged client is .codex-verify/gacha-client/aLogin.exe; manifest records hashes/hooks.

## Validation
- Old deployed server fails native preview replay (wrong packId and hardcoded reward list). Runtime log independently confirms unhandled23:128/75.
- New server:1162 gacha purchase/open/client inventory replay checks,233 mall checks,2620 inventory/pet/notification checks passed.
- Native x86 hook harness executes staged client code for all65536 ushort IDs through all three hooks, preserving fallback stack; only intended3 IDs change. Windows PE loader accepts patched image without executing game entry point.
- Build passes with existing CS1998 warnings. git diff --check passed.
- Actual game rendering/click-through remains for user replay after restart; native hook execution and packet tests are not a live gameplay test.

## Deployment
- Server stage .codex-verify/native-gacha-bin; client stage .codex-verify/gacha-client/aLogin.exe.
- Deploy only while both runtime processes are stopped. Back up four server binaries plus client EXE; verify hashes after copying, rollback both on failure. No database or catalog edits needed.

Deployed 2026-09-19 13:10:33 with server and client stopped. Five runtime hashes match stage; database unchanged. Backup: D:\Game Private\Wonderland-Private-Server\.codex-verify\deploy-backup-20260919-131033-013-native-gacha. Server and game left stopped for user restart.
