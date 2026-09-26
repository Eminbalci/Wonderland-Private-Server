# Combat/map HP and SP synchronization - 2026-09-19

## Cause
Send8_1 published FullHP/FullSP as native current stats 25/26. Mizaki stored HP was 63 while the map packet advertised187. Combat initialization correctly read63. Baseline no-gm-bin reproduces this exact discrepancy.
Native AC8:1 invokes0x416ebc; stat25 routes0x4170c9 and writes current HP at1f84, capped by max HP1fa8. Stat26 routes0x417188. This is an absolute current-vitals update.

## Change
- Send actual CurHP/CurSP after base stats/equipment bonuses.
- Reading/synchronizing zero vitals no longer heals implicitly. Explicit level-up, beginner creation, FillHP/FillSP restoration remains.
- Synchronize map stats after flee, victory and defeat for both teams. Existing loser recovery is preserved.
- No live database edits or client changes.
- Stage includes pending Item Mall category validation, login-gold serialization, quiet storage and GM-chat suppression patches.

## Validation
- Compile succeeded, two existing CS1998 warnings.
- Old-build reproduction: map HP187 vs server63.
- Test-CombatVitals:48 checks: level7 187/109 maxima, wounded/full/low/zero-SP entry packets, zero-vital sync, explicit healing, item recovery, all five exit paths including PvP participants.
- NoGmMessages:2620; MallCategory:233; LoginGold:123; InventoryLogin:22 (fixture DB unchanged).
- git diff --check passed.
- Runtime client visual replay remains pending. Stage:combat-vitals-bin. Deploy only four binary/symbol files with backup and hash verification; do not start server.

Deployed 2026-09-19 12:40:56 after user shutdown confirmation. Four hashes match staging; database SHA256 unchanged. Binary backup: D:\Game Private\Wonderland-Private-Server\.codex-verify\deploy-backup-20260919-124056-326-combat-vitals. Server left stopped for user restart.
