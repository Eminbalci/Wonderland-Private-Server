# Wonderland-Gonline upstream sync: what the C# server should take

Source: `https://github.com/Eminbalci/Wonderland-Gonline` `main`, 7 commits ahead of the local checkout
(`b040a46` .. `a17326f`), 51 server files changed (+2391 / -76).

## New upstream features

| Feature | Commits | Native client counterpart | C# status | Recommendation |
|---|---|---|---|---|
| Per-session AFK mode (`CommandAFK 253`, negotiated feature) | 90ccad1, 383cc1a, 00841e8 | none | missing | Do not port without a live client test; it is a Gonline protocol. |
| Native headless API and detachable spectators | cacbc48 | none | watch-only spectators exist (see docs/89) | Do not port; a headless agent API is out of scope for the native client. |
| Migrate the agent host to Vue, optional service opt-in | b040a46 | none | n/a | Not a server rule; skip. |
| Refresh compiled AFK/spectator language catalogs | 9a39087 | none | n/a | Skip; catalogs for the Gonline client. |
| Movement encounter timing correction | 90ccad1 | native behaviour | to check | Check the C# encounter timing against it on a live client. |
| Experience adjustment | a17326f | native EXP formula (docs/11) | to check | Compare the formula in `formula_data.gd` and the C# EXP table before changing it. |

## Decision needed from the user

1. Port AFK at all? It is a new protocol, so it needs a live client to confirm the negotiation. Recommended: no.
2. Movement encounter timing and the EXP adjustment: confirm the native behaviour on a live client first.

## Verified in this pass

- The upstream change set was read from `git diff HEAD origin/HEAD` and the new protocol files (`afk.go`).
- The C# server has no AFK or headless code (`grep` over `wlo.pserver.core` and `Src`).
