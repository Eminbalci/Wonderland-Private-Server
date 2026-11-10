# Item Redemption Cards (Gift Exchange Cards)

Ported from `Wonderland-Gonline` (`internal/server/item_cards.go`, `internal/protocol/item_cards.go`,
`docs/ITEM_CARDS.md`). Item.dat type-40 cards are exchanged for their rewards.

## Data

`Data/item_cards.json` is a copy of the Go server's `internal/assetsql/item_cards_defaults.json` (236 rules):
`card_id`, optional `male_item` / `female_item`, and `rewards` (`item_id`, `quantity`).
`ItemCardManager.Load` skips a card when one of its rewards is not in the loaded Item.dat. With the Item.dat in this
repository 232 cards load; 51610, 51712, 51773 and 51782 are skipped (rewards 35992, 35970, 23455, 35988 missing).

## Behaviour (`Game.PlayerRelated.ItemCardManager`)

- Double-click (AC23:96) on a known card calls `TryRedeem`.
- One reward: exchanged at once. Gender pair: the item for the player's body is chosen automatically.
- Several rewards: the server sends the card details and `23, 108, 4, slot`, which opens the client's Gift Exchange
  Card selector. Nothing is consumed until the client confirms.
- The card is removed and the reward granted in one `Inventory.TryExchangeItem` call. Locked cards, a changed slot,
  a full bag or a battle in progress keep the card.

## Packets

| Direction | Bytes | Meaning |
|---|---|---|
| C -> S | `23, 133, 1, slot, itemId(u16), x` | redeem the card in `slot` |
| C -> S | `23, 133, 2, slot, 1, index, rewardId(u16)` | confirm reward `index` (1-based) |
| C -> S | `90, 1, 1, n(u16), id(u16) * n` | card catalog request (longer than the 2-byte fishing commands) |
| S -> C | `23, 108, 1, count(u16), [id(u16), version(1)] * count` | card index, sent at login |
| S -> C | `23, 108, 2, count(u16), [id(u16), version(1), n, [item(u16), qty] * n] * count` | card details |
| S -> C | `23, 108, 3, cardId(u16), 1, rewardId(u16), qty` | redeemed |
| S -> C | `23, 108, 4, slot` | open the selector |

## Not ported / not verified

- Not tested against a live client; the tests check the data load and the index/details byte layout only
  (`tests/Run-ItemCardTests.ps1`).
- The Go server answers an unknown type-40 card with "This card's rewards are unavailable. Card retained."; here an
  unknown card falls through to the normal item-use path.
