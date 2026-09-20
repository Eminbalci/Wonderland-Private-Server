# Inventory feedback and keeper open — 2026-09-19

Scope: arbitrary server text, successful recovery sound, repeated Props Keeper opening. Existing deposit/withdraw handlers are not redesigned in this patch.

Native evidence from current aLogin.exe:
- AC2:4 at 0x2df5ac reads a sender DWORD then raw message text, invokes chat renderer. AC23:57 dispatches 0x42a918 predefined notice selector, not arbitrary text.
- AC23:15 at 0x2e5e89 builds sound\\wav0152.wav then calls audio routine 0x404fa4. Asset exists locally.
- AC23:5 at 0x3dc9d0 reads 31-byte bag records and calls additive item routine 0x3d7cdc. Resending bag contents while opening keeper adds counts again.
- AC30:1 at 0x3dfe24 reads 31-byte storage records and calls additive 0x3df03c. AC30:8 at 0x3df29c clears quantities/IDs of all 50 client storage cells. AC30:5 removes a quantity from a storage cell; it is not a snapshot.

Changes:
- SendSystemMessage sends AC2:4, sender 0, raw ASCII text, so equipment deltas and recovery failure notices can render.
- Successful recovery emits AC23:15 once after quantity/stat updates. Failed/full-target use emits no success sound.
- OpenPropsKeeper sends one storage clear, one storage list, then the existing UI open sequence. It sends no bag contents or guessed storage packet variants.

Validation:
- Compile succeeded with existing CS1998 warnings.
- 665 checks passed through compiled Player.ProcessSocket, including precise chat payload, successful/failed/player/pet consumption sound and 20 repeated keeper openings with a model of native additive quantity semantics. Empty vault clears stale cache. Existing inventory and equipment tests also pass.
- Runtime DB read confirms quantities remained normal after user's repeated keeper opens; the opening method does not mutate server inventory.
- git diff --check passed.
- Actual client rendering/audio/repeated-opening replay remains pending after deploy. Server binaries only; no database repair needed.

Deployed 2026-09-19 11:49:28 after user shutdown confirmation. Four hashes match staging; database SHA256 unchanged. Binary backup: D:\Game Private\Wonderland-Private-Server\.codex-verify\deploy-backup-20260919-114928-158-inventory-feedback. Server left stopped for user restart.
