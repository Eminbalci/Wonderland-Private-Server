using System;
using System.Collections.Generic;
using Game.Battle;
using Game.Maps;
using Network;

namespace Game.PlayerRelated
{
    /// <summary>
    /// Represents a verified authentic Bamboo Dragonfly teleport destination from aLogin.exe table at 0x4b5978.
    /// </summary>
    public sealed class BambooDragonflyDestination
    {
        public byte Index { get; set; }
        public string Name { get; set; }
        public ushort MapId { get; set; }
        public ushort DstX { get; set; }
        public ushort DstY { get; set; }
    }

    /// <summary>
    /// Manages Bamboo Dragonfly (竹蜻蜓) teleportation requests received via AC 23:89 (0x17:0x59).
    /// Reverse-engineered directly from client aLogin.exe:
    /// - Opcode handler at 0x2dfba4 packing [DestIndex (1B)] [InvSlot (1B)] [Type (1B)]
    /// - Global Map button array at 0x4bbf10 and names at 0x4bbe50
    /// - Target destination coordinates table at 0x4b5978 (16 bytes per entry: [MapID at +4, X at +8, Y at +12])
    /// </summary>
    public static class BambooDragonflyManager
    {
        public const ushort StandardBambooDragonfly = 34169;
        public const ushort MemoryBambooDragonfly = 34332;

        private static readonly Dictionary<byte, BambooDragonflyDestination> _destinations = new Dictionary<byte, BambooDragonflyDestination>
        {
            { 1, new BambooDragonflyDestination { Index = 1, Name = "China", MapId = 12100, DstX = 1800, DstY = 1300 } },
            { 2, new BambooDragonflyDestination { Index = 2, Name = "Calcutta", MapId = 12300, DstX = 2062, DstY = 1735 } },
            { 3, new BambooDragonflyDestination { Index = 3, Name = "Oslya", MapId = 60006, DstX = 5180, DstY = 4675 } },
            { 4, new BambooDragonflyDestination { Index = 4, Name = "Mayan Forest", MapId = 60005, DstX = 1002, DstY = 1295 } },
            { 5, new BambooDragonflyDestination { Index = 5, Name = "Universal", MapId = 60007, DstX = 1040, DstY = 1335 } },
            { 6, new BambooDragonflyDestination { Index = 6, Name = "Revival", MapId = 60004, DstX = 5750, DstY = 1955 } },
            { 7, new BambooDragonflyDestination { Index = 7, Name = "Hawaii Isle", MapId = 60008, DstX = 1750, DstY = 3900 } },
            { 8, new BambooDragonflyDestination { Index = 8, Name = "S.Pole", MapId = 12355, DstX = 690, DstY = 1090 } },
            { 9, new BambooDragonflyDestination { Index = 9, Name = "Korea", MapId = 60013, DstX = 3045, DstY = 5235 } },
            { 10, new BambooDragonflyDestination { Index = 10, Name = "Iceberg", MapId = 12356, DstX = 1580, DstY = 1255 } },
            { 11, new BambooDragonflyDestination { Index = 11, Name = "Kyoto", MapId = 60011, DstX = 5145, DstY = 355 } },
            { 12, new BambooDragonflyDestination { Index = 12, Name = "Taiwan", MapId = 60012, DstX = 2845, DstY = 780 } },
            { 13, new BambooDragonflyDestination { Index = 13, Name = "S.Island", MapId = 60002, DstX = 2862, DstY = 4575 } },
            { 14, new BambooDragonflyDestination { Index = 14, Name = "N.Island", MapId = 60000, DstX = 2900, DstY = 3055 } },
            { 15, new BambooDragonflyDestination { Index = 15, Name = "Stoowa Isle", MapId = 60003, DstX = 2142, DstY = 5175 } },
            { 16, new BambooDragonflyDestination { Index = 16, Name = "Egypt", MapId = 12500, DstX = 3122, DstY = 1755 } },
            { 17, new BambooDragonflyDestination { Index = 17, Name = "Madagascar", MapId = 11127, DstX = 3040, DstY = 340 } },
            { 18, new BambooDragonflyDestination { Index = 18, Name = "Polo", MapId = 10069, DstX = 1840, DstY = 2075 } },
            { 19, new BambooDragonflyDestination { Index = 19, Name = "Hezrou", MapId = 10070, DstX = 3460, DstY = 1775 } },
            { 20, new BambooDragonflyDestination { Index = 20, Name = "SnowIsle", MapId = 12367, DstX = 674, DstY = 1352 } },
            { 21, new BambooDragonflyDestination { Index = 21, Name = "Inca", MapId = 60015, DstX = 4040, DstY = 355 } },
            { 22, new BambooDragonflyDestination { Index = 22, Name = "Ghostdom", MapId = 12286, DstX = 3180, DstY = 875 } },
            { 23, new BambooDragonflyDestination { Index = 23, Name = "Shayii", MapId = 12281, DstX = 340, DstY = 415 } },
            { 24, new BambooDragonflyDestination { Index = 24, Name = "Yazhou", MapId = 12105, DstX = 2000, DstY = 1555 } },
            { 25, new BambooDragonflyDestination { Index = 25, Name = "Bangkok", MapId = 12601, DstX = 342, DstY = 415 } },
            { 26, new BambooDragonflyDestination { Index = 26, Name = "LionIsle", MapId = 11145, DstX = 3362, DstY = 1795 } },
            { 27, new BambooDragonflyDestination { Index = 27, Name = "Centaur Isle", MapId = 11135, DstX = 1900, DstY = 1495 } },
            { 28, new BambooDragonflyDestination { Index = 28, Name = "Sky City", MapId = 11152, DstX = 620, DstY = 470 } },
            { 29, new BambooDragonflyDestination { Index = 29, Name = "Africa", MapId = 12700, DstX = 2500, DstY = 2110 } },
            { 30, new BambooDragonflyDestination { Index = 30, Name = "Yamatai", MapId = 12850, DstX = 2240, DstY = 1695 } },
            { 31, new BambooDragonflyDestination { Index = 31, Name = "Athens", MapId = 12800, DstX = 1182, DstY = 2575 } },
            { 32, new BambooDragonflyDestination { Index = 32, Name = "Small Hawaii", MapId = 60009, DstX = 1442, DstY = 1315 } },
            { 33, new BambooDragonflyDestination { Index = 33, Name = "Train Island", MapId = 13050, DstX = 2632, DstY = 1344 } },
            { 34, new BambooDragonflyDestination { Index = 34, Name = "Rome", MapId = 13200, DstX = 2300, DstY = 1330 } },
            { 35, new BambooDragonflyDestination { Index = 35, Name = "Persia", MapId = 13100, DstX = 930, DstY = 1870 } },
            { 36, new BambooDragonflyDestination { Index = 36, Name = "Filipino", MapId = 12952, DstX = 1780, DstY = 1295 } },
            { 37, new BambooDragonflyDestination { Index = 37, Name = "Lost Jungle", MapId = 13261, DstX = 1900, DstY = 235 } },
            { 38, new BambooDragonflyDestination { Index = 38, Name = "Fog Jungle", MapId = 13164, DstX = 1765, DstY = 390 } },
            { 39, new BambooDragonflyDestination { Index = 39, Name = "Chang'an", MapId = 12110, DstX = 1842, DstY = 1355 } },
            { 40, new BambooDragonflyDestination { Index = 40, Name = "Deep Jungle", MapId = 12905, DstX = 2680, DstY = 270 } },
        };

        public static bool IsBambooDragonfly(ushort itemId)
        {
            return itemId == StandardBambooDragonfly || itemId == MemoryBambooDragonfly;
        }

        public static BambooDragonflyDestination GetDestination(byte index)
        {
            if (_destinations.TryGetValue(index, out var dest)) return dest;
            return null;
        }

        public static void TeleportTo(Player p, byte slot, byte destId, byte itemType)
        {
            if (p == null || p.CurMap == null) return;

            if (PvEBattleManager.IsInBattle(p))
            {
                p.SendHeadBanner("Cannot teleport during battle.");
                return;
            }

            if (!_destinations.TryGetValue(destId, out var dest))
            {
                p.SendHeadBanner("Invalid teleport destination.");
                DebugSystem.Write($"[BambooDragonfly] {p.CharName} requested invalid destination ID {destId}");
                return;
            }

            if (p.CurMap.MapID == dest.MapId)
            {
                p.SendHeadBanner("You are already in this area.");
                return;
            }

            lock (p.Inv.SyncRoot)
            {
                bool itemValid = false;
                byte consumeSlot = slot;

                if (slot >= 1 && slot <= 50)
                {
                    var item = p.Inv[slot];
                    if (item != null && IsBambooDragonfly(item.ItemID))
                    {
                        itemValid = true;
                    }
                }

                // If client slot didn't point directly to the item, search inventory for any Bamboo Dragonfly
                if (!itemValid)
                {
                    for (byte i = 1; i <= 50; i++)
                    {
                        var it = p.Inv[i];
                        if (it != null && IsBambooDragonfly(it.ItemID))
                        {
                            consumeSlot = i;
                            itemValid = true;
                            break;
                        }
                    }
                }

                if (!itemValid)
                {
                    p.SendHeadBanner("A Bamboo Dragonfly is required.");
                    DebugSystem.Write($"[BambooDragonfly] {p.CharName} has no Bamboo Dragonfly in inventory.");
                    return;
                }

                // Consume 1 item quantity
                p.Inv.RemoveItem(consumeSlot, 1, true);
            }

            var warp = new WarpData
            {
                DstMap = dest.MapId,
                DstX_Axis = dest.DstX,
                DstY_Axis = dest.DstY
            };

            p.CurMap.Teleport(TeleportType.Regular, p, 0, warp);
            p.SaveCharacterData();
            DebugSystem.Write($"[BambooDragonfly] {p.CharName} teleported to destination #{dest.Index} ({dest.Name}): map {dest.MapId} ({dest.DstX},{dest.DstY})");
        }
    }
}
