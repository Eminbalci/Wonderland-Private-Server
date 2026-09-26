# Restore native quest progression, capsule travel, and item handling

Fix blocked quest routes, stalled cutscenes, incorrect NPC visibility, invalid monster drops, and broken ground-item transfers. This PR contains local changes relative to upstream `Develop`; upstream-only features are not included in this summary.

- Restore authored EVE quest conditions, battle outcomes, rewards and companion transitions for Elin, Clive, La Tim, Truth Gem, Sealed Beads, Fred and Roca, including the Maka trigger and Ghostdom finale.
- Correct well/rope transitions, lever animations, NPC and quest-marker visibility, Holy's Record NPC, grouped NPC movement, and movie/choice/constellation acknowledgements.
- Validate vehicle ownership and inventory slots across placement, mounting, landing and map changes. Apply wear to the intended raft and accept supported capsule vehicles at travel gates, including Barnya.
- Replace guessed monster loot and level/name fallbacks with item IDs declared for each native NPC. Preserve valid configured rates/counts, keep zero-rate entries disabled, and log the actual monster/item/quantity awarded.
- Make ground drops and pickups preserve quantities and equipment metadata. Handle full bags/maps, concurrent pickups, distance checks, native respawns and map re-entry without losing or duplicating items.
- Retain the earlier local fixes for combat action ownership/timing, HP/EXP and pet synchronization, inventory/storage/mall transfers, native gacha, and persistent quest state.

Validation: the full server and dependencies build successfully for .NET Framework 4.6.2, with two existing CS1998 warnings. 11,895 checks passed across native loot/ground items, inventory, vehicles, Clive, Maka, Fred and Roca. The new loot fixture creates its own database; Fred/Roca fixtures use native NPCs and explicit client-data input. Tests verify packets, state and isolated persistence; they do not constitute live client replay of the full integrated build.

No player database, generated fixture, deployed binary or local backup is included in the new commits. The installed runtime is unchanged by this integration step.
