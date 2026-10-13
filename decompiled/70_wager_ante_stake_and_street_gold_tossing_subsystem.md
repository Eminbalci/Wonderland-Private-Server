# Document 70: Wager Ante Stake and Street Gold Tossing Subsystem

## 1. Executive Summary & Engine Architecture

Economic currency mobility in Wonderland Online extends beyond bilateral trading and NPC vendor purchases into competitive wagering and overworld currency physicalization. Dissected from client subroutines `FUN_0017efc0`, `FUN_0017f2bc`, `FUN_002774e4`, `FUN_002776b0`, and `FUN_00381c98`, this dual subsystem encompasses:
1. **Wager Ante Stake Engine (`Form_Ante`):** A modal dialog ($283 \times 173$ px) used to negotiate and lock currency stakes before launching tabletop gambling minigames (Eight-Twelve dice `Form_EightTwelve`, casino card tables, carnival wagers) and formal PvP arena challenges.
2. **Street Gold Tossing Subsystem (`Form_ThrowMoney`):** An overworld interaction dialog ($209 \times 87$ px) allowing players to withdraw physical currency from their purse and drop it onto the floor as an interactive world entity.

When gold is tossed onto the ground, the client-server pipeline converts the numerical currency balance into one of five tier-scaled sprite entities (`GdMoney1` through `GdMoney5`) rendered directly upon the isometric map layer. Any nearby character can subsequently target and pick up the coin pile via cursor interaction (`Action Code 0x14:0x09`), replicating authentic physical loot mechanics.

```
+------------------------------------------------------------------------------------+
|                         CURRENCY MOBILITY DUAL SUBSYSTEM                           |
+------------------------------------------------------------------------------------+
                                      |
         +----------------------------+----------------------------+
         | Subsystem A: Wager Ante Stakes                          | Subsystem B: Street Gold Tossing
         v                                                         v
  [Form_Ante Dialog (283x173 px)]                           [Form_ThrowMoney Dialog (209x87 px)]
  - Right-Aligned Currency Input Box                        - Numeric Edit Box + Arrow_L3 / Arrow_R3
  - Purse Limit & Min/Max Ante Validation                   - Physical Drop onto World Map Coordinates
  - Locks Gold into Mini-Game Escrow                        - Spawns Tiered Entities: GdMoney1..GdMoney5
         |                                                         |
         +----------------------------+----------------------------+
                                      |
                                      v
                       [Network Dispatcher & Protocol]
              Action Code 0x4A:0x02 (Ante) | Action Code 0x19:0x03 (Drop)
```

---

## 2. Memory Layouts & Data Structures

### 2.1 Currency Pile Tier Thresholds (`GdMoney1`..`GdMoney5`)
When currency is dropped via `Form_ThrowMoney`, the client resolves the sprite asset based on the quantitative value:

| Entity Tag | Gold Value Range | Sprite Asset | Visual Representation |
|---|---|---|---|
| `GdMoney1` | $1 \le \text{Gold} \le 99$ | `Icon_GdMoney1` | Single loose coin / small scattering |
| `GdMoney2` | $100 \le \text{Gold} \le 999$ | `Icon_GdMoney2` | Small tied velvet coin purse |
| `GdMoney3` | $1,000 \le \text{Gold} \le 9,999$ | `Icon_GdMoney3` | Large bulging coin sack |
| `GdMoney4` | $10,000 \le \text{Gold} \le 99,999$ | `Icon_GdMoney4` | Small open wooden chest overflowing with coins |
| `GdMoney5` | $\ge 100,000$ | `Icon_GdMoney5` | Large ornate gold-trimmed treasure hoard |

### 2.2 `Form_Ante` Instance Layout (`param_1`)

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x00` | `void*` | `VTable` | Delphi component virtual method table pointer |
| `+0x04` | `uint` | `FormHandle` | DirectDraw surface bitmap handle |
| `+0x4C` | `int*` | `pInputEditBox` | Currency input box control (`"     0"`) |
| `+0x4D` | `uint` | `BtnOk` | Confirm stake button (`"btn_Ok_1"`) |
| `+0x4E` | `uint` | `BtnCancel` | Dismiss button |
| `+0x4F` | `uint` | `BtnCloseS1` | Top-right small close button (`"Btn_Close_s_1"`) |
| `+0x104`| `uint` | `MaxPermittedAnte`| Maximum stake allowed by character purse and table rules |

### 2.3 `Form_ThrowMoney` Instance Layout (`param_1`)

| Offset | Type | Field Name | Description |
|---|---|---|---|
| `+0x4D` | `int*` | `pAmountInput` | Numeric drop amount edit box |
| `+0x4E` | `uint` | `BtnOk` | Drop confirmation button (`"btn_ok_1"`) |
| `+0x4F` | `uint` | `BtnCancel` | Cancel button (`"btn_Cancel_1"`) |
| `+0x50` | `uint` | `BtnArrowLeft` | Decrement amount button (`"Arrow_L3"`) |
| `+0x51` | `uint` | `BtnArrowRight`| Increment amount button (`"Arrow_R3"`) |

---

## 3. UI Form Geometry, Coordinates & Controls

### 3.1 `Form_Ante` Layout Metrics
- **Dimensions:** Width `0x11B` (283 pixels), Height `0xAD` (173 pixels).
- **Positioning:** Centered at $(X = \text{0x104}, Y = \text{0xAD})$.
- **Controls:**
  - Input Box (`param_1[0x4C]`): $X = \text{0xBC}$ (188 px), $Y = \text{0x62}$ (98 px), size $60 \times 20$ px. Initialized with `"     0"`.
  - `"btn_Ok_1"`: $X = \text{0x4B}$ (75 px), $Y = \text{0x85}$ (133 px), size $20 \times 56$ px.
  - `"Btn_Close_s_1"`: $X = \text{BaseWidth} - 50$ px, $Y = 25$ px, size $17 \times 17$ px.

### 3.2 `Form_ThrowMoney` Layout Metrics
- **Dimensions:** Width `0xD1` (209 pixels), Height `0x57` (87 pixels).
- **Positioning:** Floats near the active mouse click origin.
- **Controls:**
  - Amount Input Box (`param_1[0x4D]`): $X = \text{0x46}$ (70 px), $Y = \text{0x1D}$ (29 px), size $76 \times 24$ px.
  - `"Arrow_L3"`: $X = \text{0x22}$ (34 px), $Y = \text{0x20}$ (32 px), size $21 \times 21$ px.
  - `"Arrow_R3"`: $X = \text{0x9A}$ (154 px), $Y = \text{0x20}$ (32 px), size $21 \times 21$ px.
  - `"btn_ok_1"`: $X = \text{0x22}$ (34 px), $Y = \text{0x3B}$ (59 px), size $20 \times 56$ px.
  - `"btn_Cancel_1"`: $X = \text{0x77}$ (119 px), $Y = \text{0x3B}$ (59 px), size $20 \times 56$ px.

```
+-------------------------------------------------------+
| Form_Ante: Wager Stake Negotiation (283 x 173 px)     |
+-------------------------------------------------------+
| [Header Title: Enter Stake Ante]                  [X] |
+-------------------------------------------------------+
|                                                       |
|        Stake Amount: [      5000 ] Gold               |
|                                                       |
|        [ btn_Ok_1: Confirm ]   [ Cancel ]             |
+-------------------------------------------------------+

+-------------------------------------------------------+
| Form_ThrowMoney: Street Gold Tossing (209 x 87 px)    |
+-------------------------------------------------------+
| [Drop Amount:] [< Arrow_L3] [ 1000 ] [Arrow_R3 >]     |
| [ btn_ok_1: Drop Gold ]       [ btn_Cancel_1: Abort ] |
+-------------------------------------------------------+
```

---

## 4. Network Protocol, Wire Frames & Action Codes

### 4.1 Packet Framing
1. **Ante Wagers:** Uses Action Code `0x4A` (Tabletop Mini-Game Engine), Subcode `0x02`:
   - `C2S_AnteStakePacket`: `[0x4A, 0x02, TableId: ushort, StakeAmount: uint]`
2. **Gold Tossing / Dropping:** Uses Action Code `0x19` (Player Overworld Interaction), Subcode `0x03` (via `FUN_00381c98`):
   - `C2S_ThrowMoneyPacket`: `[0x19, 0x03, GoldAmount: uint, TargetX: ushort, TargetY: ushort]`
   - `S2C_SpawnGoldEntity`: `[0x19, 0x03, EntityGuid: uint, GoldAmount: uint, X: ushort, Y: ushort, Tier: byte]`

### 4.2 C# Packet Structure Definitions

```csharp
namespace Wonderland.Network.Packets.Economy
{
    /// <summary>
    /// Client-to-server request to toss gold coins onto the world floor.
    /// Wire Header: Action Code 0x19, Subcode 0x03.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct C2S_ThrowMoneyPacket
    {
        public byte ActionCode;       // 0x19 (25)
        public byte SubCode;          // 0x03
        public uint GoldAmount;       // Quantity of gold to discard
        public ushort TargetX;        // Target floor tile X coordinate
        public ushort TargetY;        // Target floor tile Y coordinate
    }

    /// <summary>
    /// Server-to-client broadcast spawning an interactive physical gold pile on the map.
    /// Wire Header: Action Code 0x19, Subcode 0x03.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct S2C_SpawnGoldPilePacket
    {
        public byte ActionCode;       // 0x19 (25)
        public byte SubCode;          // 0x03
        public uint EntityGuid;       // Ephemeral entity ID
        public uint GoldAmount;       // Amount contained within pile
        public ushort WorldX;         // Map X coordinate
        public ushort WorldY;         // Map Y coordinate
        public byte Tier;             // 1 = GdMoney1 .. 5 = GdMoney5
    }

    /// <summary>
    /// Client-to-server request to commit an ante stake into a minigame or duel escrow.
    /// Wire Header: Action Code 0x4A, Subcode 0x02.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct C2S_AnteStakePacket
    {
        public byte ActionCode;       // 0x4A (74)
        public byte SubCode;          // 0x02
        public ushort TableId;        // Target minigame or duel session ID
        public uint AnteAmount;       // Negotiated stake
    }
}
```

---

## 5. Security Safeguards & World Entity Lifecycle

### 5.1 Validation Safeguards
1. **Purse Ceiling Verification:** Players cannot throw or ante more gold than currently held in their active purse (`CharacterGold < Amount`).
2. **Zero Value Prevention:** Subroutine `FUN_0048243c` verifies that stake amounts exceed zero:
   `"Price can't be zero"` / `"Can't be 0"`
3. **Restricted Zone Validation:** Currency dropping is strictly blocked within certain map types:
   - Tent Interiors: `"Can't drop items at home"` / `"Max items on floor"`
   - Hot Springs: `"Don't litter In Hot Springs!"`
   - Inter-Server Zones: `"Unavailable at Interserver"`

### 5.2 World Entity Despawn & Ingestion Lifecycle
- Gold entities spawned on the ground exist for a default timeout of 180 seconds (3 minutes).
- If uncollected after 180 seconds, the entity fades out and despawns to prevent map memory bloat.
- When an entity is picked up via cursor click, the full amount is deposited directly into the collecting player's purse up to the 999,999,999 gold maximum cap.

---

## 6. Private Server Implementation Guide & C# Production Code

The following service provides full backend management for currency tossing, world entity lifecycle, and wager ante escrow.

```csharp
using System;
using System.Collections.Concurrent;
using Wonderland.Network.Packets.Economy;

namespace Wonderland.Server.Services.Economy
{
    public sealed class GroundGoldPile
    {
        public uint EntityGuid { get; init; }
        public uint MapId { get; init; }
        public ushort X { get; init; }
        public ushort Y { get; init; }
        public uint Amount { get; init; }
        public DateTime SpawnTime { get; init; } = DateTime.UtcNow;

        public byte Tier => Amount switch
        {
            < 100 => 1,
            < 1000 => 2,
            < 10000 => 3,
            < 100000 => 4,
            _ => 5
        };
    }

    public interface ICurrencyMobilityService
    {
        bool ThrowGold(PlayerCharacter player, C2S_ThrowMoneyPacket packet, out string error);
        bool PickupGold(PlayerCharacter player, uint entityGuid, out uint collectedAmount);
        bool CommitAnte(PlayerCharacter player, ushort tableId, uint anteAmount, out string error);
    }

    public sealed class CurrencyMobilityService : ICurrencyMobilityService
    {
        private readonly ConcurrentDictionary<uint, GroundGoldPile> _goldPiles = new();
        private int _nextEntityId = 500000;

        public bool ThrowGold(PlayerCharacter player, C2S_ThrowMoneyPacket packet, out string error)
        {
            error = string.Empty;

            if (player == null) throw new ArgumentNullException(nameof(player));

            if (packet.GoldAmount == 0)
            {
                error = "Can't be 0";
                return false;
            }

            if (player.Gold < packet.GoldAmount)
            {
                error = "Not enough Gold";
                return false;
            }

            if (player.IsInTent)
            {
                error = "Can't drop items at home";
                return false;
            }

            // Deduct from player purse
            player.DeductGold(packet.GoldAmount);

            // Create ground entity
            uint entityId = (uint)System.Threading.Interlocked.Increment(ref _nextEntityId);
            var pile = new GroundGoldPile
            {
                EntityGuid = entityId,
                MapId = player.MapId,
                X = packet.TargetX,
                Y = packet.TargetY,
                Amount = packet.GoldAmount
            };

            _goldPiles[entityId] = pile;

            // Broadcast spawn to nearby players
            var spawnPacket = new S2C_SpawnGoldPilePacket
            {
                ActionCode = 0x19,
                SubCode = 0x03,
                EntityGuid = entityId,
                GoldAmount = pile.Amount,
                WorldX = pile.X,
                WorldY = pile.Y,
                Tier = pile.Tier
            };

            player.CurrentMap?.BroadcastAround(player, spawnPacket);
            return true;
        }

        public bool PickupGold(PlayerCharacter player, uint entityGuid, out uint collectedAmount)
        {
            collectedAmount = 0;

            if (player == null) return false;

            if (_goldPiles.TryRemove(entityGuid, out var pile))
            {
                collectedAmount = pile.Amount;
                player.AddGold(collectedAmount);

                // Broadcast despawn notification
                player.CurrentMap?.BroadcastEntityDespawn(entityGuid);
                return true;
            }

            return false;
        }

        public bool CommitAnte(PlayerCharacter player, ushort tableId, uint anteAmount, out string error)
        {
            error = string.Empty;

            if (player == null) throw new ArgumentNullException(nameof(player));

            if (anteAmount == 0)
            {
                error = "Can't be 0";
                return false;
            }

            if (player.Gold < anteAmount)
            {
                error = "Not enough Gold";
                return false;
            }

            // Escrow deduction
            player.DeductGold(anteAmount);
            return true;
        }
    }
}
```
