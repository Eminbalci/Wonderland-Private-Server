# Join an Existing Battle

Ported from `Wonderland-Gonline` (`internal/server/combat_admission.go`, `internal/battle/admission.go`).
AC11 "Join battle" was a TODO. Request: `11, 2, 5, targetCharId(u32), 0, 0` (native `FUN_0038e1d4`).

## Rules (`PvEBattleManager.RequestJoin`)

Rejected with "This battle is unavailable." when:

- the joiner is in a battle or watching one, has 0 HP, or is a team member but not the leader;
- the target is not in a running battle on the same map, is overkilled or withdrawn, or is more than 170 map units
  away on either axis;
- the target has "Join battle" switched off (`Settings.JOINABLE`);
- the target's side has no free slot. A slot is free when no player or pet of that side (not withdrawn, not
  overkilled) stands on its row. Knocked-out fighters keep their slot.

## Requests during a round

As on the Go server, a join or watch request that arrives while a round is being processed (`IsTurnProcessing`)
is not rejected. The player is queued (`ActiveBattle.PendingAdmissions`, in arrival order), gets the movement lock
`6, 2, 1`, and counts as in battle. `AdmitPendingAdmissions` runs right before the next command prompt is sent:
each queued player is validated again and then enters. A joiner admitted this way is **not** `IsJoining`: it gets
the prompt and acts in the phase that is starting. If validation fails, no slot is free, or the battle ended, the
player is released with `11, 0, charId, 0`, `6, 2, 0`, `20, 8`. Flee, disconnect and battle end also clear the queue.

## Entry

The joiner and the active pet are placed in the first free slot of the target's side and marked `IsJoining`:
they are not counted in `ExpectedActionCount`, their actions are ignored, and monsters do not target them until
the next command phase, where the flag is cleared.

Packets:

- to the other players and spectators: `11, 5` + record (`mode` 5) for the joiner and for the pet;
- to the joiner: `11, 250, backdrop, metadata` with the joiner's own record (`mode` 5) and every other fighter
  except the joiner's pet (`mode` 1 same side, 100 other side), then `11, 10, 1`, then `11, 5` for the pet;
- presence `11, 4, 2, charId(u32), 0(u16), 5` to the joiner and the map.

The joiner is a normal participant afterwards (rewards, defeat, flee).

`BuildFighters` now builds player and pet fighters through `CreatePlayerFighter` / `CreatePetFighter`; the values
are unchanged.

## Not ported / not verified

- Not tested with a live client or with two connected players; no automated tests (needs live `Player` objects).
- The Go server also drops the joiner's party leadership; here the team is left as it is.
- `IsJoining` fighters are only excluded from the monsters' normal target list; area skills and PvP targeting were
  not checked.
