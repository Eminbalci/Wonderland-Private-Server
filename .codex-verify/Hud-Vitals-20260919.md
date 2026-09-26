# HUD/inventory maximum HP mismatch - 2026-09-19

User screenshot: HUD HP187/282, inventory HP187/187; SP109/109 in both. Server level7/CON0 maximum HP187 remains unchanged.

## Native evidence
AC8:1 invokes setter0x416ebc, then207 at0x2e15f6 copies current computed player maxHP1fa8 to the separate HUD character via stat205.208 at0x2e1621 copies maxSP1fac via206. CON29 and WIS33 packets do not run those HUD-copy branches. Native207 setter0x417586 recomputes maxHP from the currently stored CON;208 at0x4175c2 recomputes maxSP from WIS. Inventory's own refresh can therefore show a newer max while the HUD retains the prior value.

Send8_1 previously sent207/208 before CON/WIS. Move both bonus/max recalculation packets after base stats and before current25/26. No formula, HP/SP value, database or client changes for this fix.

## Validation
- Read-only replay of runtime binary packet ordering reproduces HUD187/282 versus inventory187/187 from initial CON16 and final CON0.
-62 packet replay checks: reported case, CON/WIS transitions, equipment HP bonuses/unequip, wounded HP63 and SP0, no implicit recovery, only one max-HP packet.
-48 combat/map vitals checks,1110 gacha checks,2620 inventory/pet/notification checks passed on combined build.
-Compile succeeded with two existing CS1998 warnings. git diff --check passed.
-Native on-screen gameplay verification remains pending; replay models verified native cache update behavior, does not claim running-client proof.

## Pending deployment
Combined stage: .codex-verify/hud-vitals-bin. Use Deploy-HudVitals.ps1 after user closes server. Includes pending gacha and catalog correction (remove7 mistaken consumable offers, add Lucky Pack, enable Gear/Strengthen pools). Catalog migration changes only item_mall; no player data edits. Server was still running PID21376 at final review. Leave stopped after deployment for user's restart.

Deployed 2026-09-19 12:58:08 while server stopped. Four binary hashes match. Catalog migration removed7 mistaken consumable offers and added Lucky Pack; Gear Pack and Strengthen Pack retain their existing price; all non-catalog tables unchanged. Binary backup: D:\Game Private\Wonderland-Private-Server\.codex-verify\deploy-backup-20260919-125807-276-hud-vitals. DB backup recorded in migration output. Server left stopped.
