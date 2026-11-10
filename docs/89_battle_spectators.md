# Battle Spectators (Watch Battle)

Ported from `Wonderland-Gonline` (`internal/server/combat_admission.go`, `internal/server/combat_spectators.go`,
`internal/battle/admission.go`). AC11 "Watch battle" was a TODO.

## Request

`11, 2, mode, targetCharId(u32), 0, 0` with `mode` 4 = watch (native `FUN_0038dfe0`) and 5 = join
(`FUN_0038e1d4`). The old comments in `AC11.Recv2` had the two swapped. Join is still not implemented.

`PvEBattleManager.RequestWatch` answers "This battle is unavailable." when the watcher is already in a battle or
watching, is a team member but not the leader, the target is not in a running battle on the same map, the target
fighter is overkilled or withdrawn, or the two players are more than 170 map units apart on either axis.

## Entry

One `11, 250, backdrop, formationMetadata` packet with fighter records (the same 30-byte record the battle intro
uses): first the watcher (`mode` 4, kind 2, grid 255,255), then every fighter that is not withdrawn, overkilled or
captured (`mode` 1 on the target's side, 100 on the other side). Then `11, 10, 1`, and the presence packet
`11, 4, 2, charId(u32), 0(u16), 4` to the watcher and the map.

## While watching

`BroadcastToBattle` also reaches spectators, so they get the common round packets (actions, damage, AC53:3).
Owner-only packets (rewards, item use, AC51:1 stat sync sent per player) do not reach them.
`IsInBattle` is true for a spectator.

## Leaving

A flee request (AC11:1) from a spectator, a disconnect, or the end of the battle (victory, defeat, flee) removes
the spectator: `11, 0, charId(u32), 0(u16)`, `6, 2, 0`, `20, 8`, and the battle state off for the map.

## Not ported / not verified

- Not tested with a live client or with two connected players; there are no automated tests for this change
  (it needs live `Player` objects).
- The Go server queues a watch request that arrives while a round is processing and admits it at the next command
  phase; here the watcher enters at once.
- Spectators do not receive AC51:1 HP/SP sync, so their HP bars follow only the damage shown in round packets.
- Go's optional full-round "round chain" transport for spectators is not ported.
