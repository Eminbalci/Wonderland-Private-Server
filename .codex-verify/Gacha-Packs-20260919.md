# Custom equipment gacha - 2026-09-19

User explicitly requested gacha instead of the seven consumable offers and approved configuring custom pools from existing equipment. These are local custom rewards/rates, not claimed official WLO tables.

Remove the seven previously added IM offers32075,32002,34014,34096,34097,34098,34126. Preserve inventories, balances and other existing offers. Add Lucky Pack34333 at32 IM; configure existing Gear Pack34229 (96 IM) and Strengthen Pack34199 (58 IM) without changing their price. Result:200 offers,138 IM +62 Bonus.

Each successful opening consumes one pack and grants exactly one equippable reward. Six weighted rewards per pack, weights3500/3500/1200/1300/300/200 out of10000 (tiers70%/25%/5%). Config:Data/gacha_packs.json. All18 entries validated against actual server item DAT. No EXP pills, food, or amity consumables are included as prizes.

## Gear Pack #34229

| Reward | ID | Chance |
|---|---:|---:|
| Energy Halo | 22135 | 35% |
| Jade Ears | 22159 | 35% |
| Purple Wing Robe | 21174 | 12% |
| Kung-Fu Suit | 21213 | 13% |
| Archangel Wings | 25215 | 3% |
| Energy Halo+5 | 22216 | 2% |

## Lucky Pack #34333

| Reward | ID | Chance |
|---|---:|---:|
| Sky Sword | 11101 | 35% |
| Polished Wand | 14076 | 35% |
| Sky Sword+1 | 11103 | 12% |
| Polished Wand+1 | 14079 | 13% |
| Sky Sword+5 | 11048 | 3% |
| Polished Wand+5 | 14120 | 2% |

## Strengthen Pack #34199

| Reward | ID | Chance |
|---|---:|---:|
| Sky Sword+2 | 11105 | 35% |
| Polished Wand+2 | 14081 | 35% |
| Purple Wing Robe+2 | 21209 | 12% |
| Jade Ears+2 | 22170 | 13% |
| Sky Sword+6 | 11117 | 3% |
| Polished Wand+6 | 14122 | 2% |

## Implementation and validation

- Standard AC23:96 and AC23:15 item-use paths. AC23:15 requires one pack and player target0.
- Inventory plans reward placement including the consumed pack slot, then commits decrement23:9 followed by added quantities23:5. Full bags with stacked packs retain the pack; a final single pack can use its freed slot. Locked packs and combat cannot consume.
- Invalid reward configuration hides these three gacha entries on both catalog channels and rejects stale purchases without charge.
- Removed AC55 fake reward path that destroyed arbitrary items and granted fixed23001. Native AC55 dispatch0x2ed2cb does not parse the fabricated item/count reply. Uses normal inventory deltas, consume sound and notification box instead.
-1110 gacha checks passed, including exhaustive enumeration of all10000 rolls per pool, purchase->open->equip, client inventory replay, full/locked/malformed cases, concurrent last-pack requests, combat, invalid configuration, no GM chat and no fabricated AC55 reward packet.
-233 existing mall transaction checks and2620 inventory/pet/notification checks passed.
-Build passes with the two pre-existing CS1998 warnings. git diff --check passes.
-Catalog migration rehearsed on DB copy:7 removed,1 added,200 total; repeated migration is a no-op. Every non-catalog table hash and existing retained offer unchanged; integrity_check=ok.
-Live native-client opening animation/UI replay is still pending. No new animation opcode is guessed. Server was running during final review; deployment requires shutdown.
