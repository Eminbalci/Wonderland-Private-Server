# Go-to-C# parity: status and open items

Reference: `Wonderland-Gonline` (Go). This file records what was ported in the session on branch `Develop`,
what was verified only by build and the test scripts, and what still needs a live server or client.

## Ported and committed (build 0 errors, `tests/Run-*Tests.ps1` pass)

- Overkill rule (`OverkillRule`): AC53:3 only above 150% round damage; overkilled players return to the record point.
- Item redemption cards (`ItemCardManager`, 232/236 rules load with this Item.dat).
- Potential pills (`PotentialRule`, AC23:126) and potential stat bonus on player and pet attributes.
- Battle spectators (watch), join, and the queue for requests during a round.
- Public message board (AC82) with SQLite storage and the NPC service 15 window.
- Crafting item locks (alchemy, forging, repair, tent manufacturing).
- Trade: locked items cannot be offered or traded.
- Team leadership: offer, acceptance, refusal, busy, and a 5 s timeout timer.
- Arcade: game table (8 games, 83 prizes), weighted draw, points or token payment, bag check, refund on failed insert.
- Quest disguise catalogue (13 rules) applied at op 12 before the built-in Breillat case.

## Verified by reading code (no gap)

- Posta ve depo: neither server has item-lock checks there.
- Team sub-commands 1, 2, 3, 4, 9, 10, 238 and the outgoing 5 and 6 packets exist on the C# side.

## Open, needs a live server or client

- Live client test of every item above: AC71 reply format per arcade kind, leadership timeout packet, disguise packet, spectator/join snapshot.
- Arcade payment as one database transaction: points live in `UserDataBase`, the bag in `CharacterDataBase`; a true
  atomic commit needs a coordinating transaction across both, verified on a real database.
- Leadership: Go's exact timeout reason code and the candidate-side packets are not confirmed on the native client.

## Open, code only

- Nothing blocking was left in this session that can be finished without a live environment.
