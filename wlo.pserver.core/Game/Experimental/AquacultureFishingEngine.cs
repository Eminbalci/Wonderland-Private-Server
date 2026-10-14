using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Game.Code;
using Network;

namespace Game.Experimental
{
    public class CatchResult
    {
        public ushort ItemId { get; set; }
        public string ItemName { get; set; }
        public double WeightKg { get; set; }
        public int ExpGained { get; set; }
        public bool IsTreasureChest { get; set; }

        public CatchResult(ushort id, string name, double weight, int exp, bool treasure = false)
        {
            ItemId = id;
            ItemName = name;
            WeightKg = weight;
            ExpGained = exp;
            IsTreasureChest = treasure;
        }
    }

    /// <summary>
    /// Authentic Fishing, Aquaculture Dynamics & Catch Tables Engine (AC 90 / Spec 43).
    /// Manages casting, rod wear, bait consumption, and aquatic catch resolution.
    /// </summary>
    public class AquacultureFishingEngine
    {
        private readonly Random m_rnd = new Random();
        private readonly ConcurrentDictionary<uint, DateTime> m_activeAnglers = new ConcurrentDictionary<uint, DateTime>();

        public int ActiveAnglerCount => m_activeAnglers.Count;

        public bool StartFishing(Player player, ushort rodItemId = 26101, ushort baitItemId = 26201)
        {
            if (player == null) return false;

            m_activeAnglers[player.CharID] = DateTime.UtcNow;
            player.Fishing = true;

            DebugSystem.Write($"[AquacultureFishingEngine] Player {player.CharName} cast fishing line with Rod #{rodItemId} and Bait #{baitItemId}");

            // S->C AC 90:1 Fishing state frame
            SendPacket p = new SendPacket();
            p.Pack8(90);
            p.Pack8(1);
            p.Pack8(1); // 1 = Fishing Line In Water
            p.Pack16(rodItemId);
            player.Send(p);

            return true;
        }

        public CatchResult CatchFish(Player player)
        {
            if (player == null) return null;

            m_activeAnglers.TryRemove(player.CharID, out _);
            player.Fishing = false;

            // Determine catch based on roll
            int roll = m_rnd.Next(1, 101);
            CatchResult result;

            if (roll > 95)
            {
                // Rare Sunken Treasure Box!
                result = new CatchResult(26999, "Sunken Golden Chest", 5.2, 500, true);
            }
            else if (roll > 70)
            {
                // Premium Fish (e.g. Giant Salmon, Tuna)
                double weight = 2.0 + m_rnd.NextDouble() * 8.0;
                result = new CatchResult(26010, "Pacific Bluefin Tuna", Math.Round(weight, 2), 250);
            }
            else if (roll > 30)
            {
                // Common Fish (e.g. Carp, Trout)
                double weight = 0.5 + m_rnd.NextDouble() * 2.5;
                result = new CatchResult(26001, "Freshwater Carp", Math.Round(weight, 2), 100);
            }
            else
            {
                // Seaweed / Minor shellfish
                result = new CatchResult(26005, "Green Kelp Shellfish", 0.3, 40);
            }

            // Award item to player bag
            player.Inv?.AddItem(result.ItemId, 1);
            DebugSystem.Write($"[AquacultureFishingEngine] {player.CharName} landed a {result.ItemName} ({result.WeightKg} kg, +{result.ExpGained} EXP)!");

            // S->C AC 90:2 Fish Caught Frame
            SendPacket p = new SendPacket();
            p.Pack8(90);
            p.Pack8(2);
            p.Pack16(result.ItemId);
            p.Pack16((ushort)(result.WeightKg * 100));
            p.Pack32((uint)result.ExpGained);
            player.Send(p);

            return result;
        }

        public void StopFishing(Player player)
        {
            if (player == null) return;
            m_activeAnglers.TryRemove(player.CharID, out _);
            player.Fishing = false;

            SendPacket p = new SendPacket();
            p.Pack8(90);
            p.Pack8(3);
            p.Pack8(0); // Cancelled
            player.Send(p);
        }
    }
}
