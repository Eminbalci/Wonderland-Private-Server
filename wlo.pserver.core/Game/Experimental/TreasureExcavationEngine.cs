using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Game.Code;
using Network;

namespace Game.Experimental
{
    public class DigResult
    {
        public uint MinerCharId { get; set; }
        public string MinerName { get; set; }
        public ushort MapId { get; set; }
        public ushort ItemId { get; set; }
        public string ItemName { get; set; }
        public int Quantity { get; set; }
        public double DepthMeters { get; set; }
        public bool IsTreasureChest { get; set; }
        public DateTime Timestamp { get; set; }

        public DigResult(uint charId, string name, ushort mapId, ushort itemId, string itemName, int qty, double depth, bool isChest = false)
        {
            MinerCharId = charId;
            MinerName = name ?? "Excavator";
            MapId = mapId;
            ItemId = itemId;
            ItemName = itemName;
            Quantity = qty;
            DepthMeters = depth;
            IsTreasureChest = isChest;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Underground Treasure Digging, Geological Mining and Soil Excavation Engine (AC 90 / Spec 83).
    /// Manages shovel/pickaxe operations, soil hardness, geological ore yields, and buried relic excavation.
    /// </summary>
    public class TreasureExcavationEngine
    {
        private readonly ConcurrentQueue<DigResult> m_recentFinds = new ConcurrentQueue<DigResult>();
        private readonly Random m_rnd = new Random();
        private readonly object m_digLock = new object();

        public int TotalExcavationsLogged => m_recentFinds.Count;

        public IReadOnlyCollection<DigResult> GetRecentFinds() => m_recentFinds.ToArray().ToList().AsReadOnly();

        public DigResult PerformExcavation(Player player, ushort toolItemId = 26301)
        {
            if (player == null) return null;

            try
            {
                lock (m_digLock)
                {
                    int roll = m_rnd.Next(1, 101);
                    double depth = Math.Round(0.5 + m_rnd.NextDouble() * 8.5, 1);
                    DigResult result;

                    if (roll > 95)
                    {
                        // Antiquity Relic: Sunken Ironbound Chest
                        result = new DigResult(player.CharID, player.CharName, (ushort)(player.CurMap?.MapID ?? 0), 26399, "Buried Antiquity Treasure Chest", 1, depth, true);
                    }
                    else if (roll > 75)
                    {
                        // Precious Ores (Gold, Titanium, Meteorite)
                        ushort oreId = (ushort)(roll > 88 ? 26315 : 26310);
                        string oreName = roll > 88 ? "Meteoric Star Iron" : "Raw Gold Nugget";
                        result = new DigResult(player.CharID, player.CharName, (ushort)(player.CurMap?.MapID ?? 0), oreId, oreName, m_rnd.Next(1, 3), depth);
                    }
                    else if (roll > 40)
                    {
                        // Common Industrial Ores (Copper, Iron, Coal)
                        ushort oreId = (ushort)(roll > 60 ? 26302 : 26305);
                        string oreName = roll > 60 ? "High-Grade Iron Ore" : "Raw Copper Clump";
                        result = new DigResult(player.CharID, player.CharName, (ushort)(player.CurMap?.MapID ?? 0), oreId, oreName, m_rnd.Next(2, 5), depth);
                    }
                    else
                    {
                        // Soil / Clay / Fossil fragment
                        result = new DigResult(player.CharID, player.CharName, (ushort)(player.CurMap?.MapID ?? 0), 26301, "Red Clay Deposit", m_rnd.Next(1, 4), depth);
                    }

                    // Enqueue log
                    m_recentFinds.Enqueue(result);
                    while (m_recentFinds.Count > 100) m_recentFinds.TryDequeue(out _);

                    // Add to player inventory if bag loaded
                    player.Inv?.AddItem(result.ItemId, (byte)result.Quantity);

                    DebugSystem.Write($"[TreasureExcavationEngine] {player.CharName} dug at {depth}m depth and struck {result.Quantity}x {result.ItemName}!");

                    // S->C AC 90:10 Excavation Yield ACK
                    SendPacket p = new SendPacket();
                    p.Pack8(90);
                    p.Pack8(10);
                    p.Pack16(result.ItemId);
                    p.Pack8((byte)result.Quantity);
                    p.Pack16((ushort)(depth * 10));
                    p.Pack8((byte)(result.IsTreasureChest ? 1 : 0));
                    player.Send(p);

                    return result;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[TreasureExcavationEngine] PerformExcavation error: {ex.Message}");
                return null;
            }
        }
    }
}
