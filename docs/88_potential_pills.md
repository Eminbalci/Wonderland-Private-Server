# Potential Pills

Ported from `Wonderland-Gonline` (`internal/game/potential.go`, `internal/server/progression.go`).

## What changed

- AC68:1 used to raise a pet's potential for free, up to 100. It now changes nothing, answers "failed" and tells
  the player to use a Potential Pill, as the Go server does.
- New AC23:126 handler: `23, 126, bagSlot, target` (`target` 0 = player, 1..4 = client pet slot).
  Reply `23, 213, result, target, level` (`result` 0 rejected, 1 processed).
- A valid attempt always consumes one pill.

## Rules (`Game.PlayerRelated.PotentialRule`)

| Pill | Item | Rule |
|---|---|---|
| Potential Pill | 34269 | success chance by current level; failure loses one level |
| Golden Potential Pill | 34289 | only from level 10; failure keeps the level |
| Super Potential Pill | 34350 | always succeeds |

Maximum level 12. Success chance by current level 0..11: 100, 100, 100, 70, 60, 55, 55, 50, 35, 20, 18, 15.
The roll is a uniform 0..99 from a cryptographic RNG; success when `roll < chance`.

## Not ported / not verified

- `PotentialRule.Bonus` holds the native per-attribute bonus table (0, 1, 2, 3, 4, 6, 9, 13, 18, 24, 31, 39, 48) but
  it is **not applied** to player or pet attributes yet. Potential still changes only the stored level and the
  value the client shows.
- The Go server keeps missing HP/SP across the maximum change and forgets skills that no longer qualify after a
  failed pill; neither is ported.
- Potential values above 12 saved by the old free path are left as they are; pills are rejected for them.
- Not tested against a live client. Tests: `tests/Run-PotentialTests.ps1` (rule table and rolls only).
