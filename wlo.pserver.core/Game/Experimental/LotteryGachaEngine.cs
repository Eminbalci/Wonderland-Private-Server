using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Game.Code;
using Network;

namespace Game.Experimental
{
    public enum GachaTier : byte
    {
        Common = 1,
        Uncommon = 2,
        Rare = 3,
        Epic = 4,
        Legendary = 5
    }

    public class LotterySpinResult
    {
        public uint PlayerCharId { get; set; }
        public string PlayerName { get; set; }
        public byte SliceIndex { get; set; }
        public ushort PrizeItemId { get; set; }
        public string PrizeName { get; set; }
        public int Quantity { get; set; }
        public bool IsJackpot { get; set; }
        public DateTime SpinTime { get; set; }

        public LotterySpinResult(uint charId, string name, byte slice, ushort itemId, string itemName, int qty, bool jackpot = false)
        {
            PlayerCharId = charId;
            PlayerName = name ?? "Contestant";
            SliceIndex = slice;
            PrizeItemId = itemId;
            PrizeName = itemName;
            Quantity = qty;
            IsJackpot = jackpot;
            SpinTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Carnival Fortune Wheel Lottery, Progressive Jackpot, and Gacha Capsule Toy Machine Engine (AC 76 / Spec 85 & 98).
    /// Simulates 12-slice carnival wheel physics, progressive jackpot accumulation, and collectible capsule drops.
    /// </summary>
    public class LotteryGachaEngine
    {
        private readonly ConcurrentQueue<LotterySpinResult> m_history = new ConcurrentQueue<LotterySpinResult>();
        private readonly Random m_rnd = new Random();
        private readonly object m_lotteryLock = new object();
        public uint ProgressiveJackpotPool { get; private set; } = 250000;

        public int TotalSpinsLogged => m_history.Count;

        public IReadOnlyCollection<LotterySpinResult> GetHistory() => m_history.ToArray().ToList().AsReadOnly();

        public LotterySpinResult SpinWheel(Player player, uint wagerGold = 1000)
        {
            if (player == null) return null;

            try
            {
                lock (m_lotteryLock)
                {
                    if (player.Gold < wagerGold)
                    {
                        player.SendSystemMessage("Insufficient gold to purchase Carnival Lottery Spin.");
                        return null;
                    }

                    player.Gold -= wagerGold;
                    ProgressiveJackpotPool += (uint)(wagerGold * 0.25); // 25% pool contribution

                    int roll = m_rnd.Next(1, 101);
                    byte slice;
                    LotterySpinResult result;

                    if (roll > 98)
                    {
                        // 2% GRAND JACKPOT!
                        slice = 12;
                        uint award = ProgressiveJackpotPool;
                        ProgressiveJackpotPool = 100000; // Reset seed
                        player.Gold += award;
                        result = new LotterySpinResult(player.CharID, player.CharName, slice, 39999, $"Progressive Jackpot ({award:N0}G)", 1, true);
                    }
                    else if (roll > 80)
                    {
                        // Rare Mount Saddle / Forging Stone
                        slice = 8;
                        result = new LotterySpinResult(player.CharID, player.CharName, slice, 27501, "Carnival Golden Saddle", 1);
                        player.Inv?.AddItem(result.PrizeItemId, 1);
                    }
                    else if (roll > 55)
                    {
                        // Crafting Catalyst
                        slice = 5;
                        result = new LotterySpinResult(player.CharID, player.CharName, slice, 27100, "Refined Alchemy Jade", 2);
                        player.Inv?.AddItem(result.PrizeItemId, 2);
                    }
                    else if (roll > 30)
                    {
                        // High Potions
                        slice = 3;
                        result = new LotterySpinResult(player.CharID, player.CharName, slice, 10005, "Grand Elixir of Life", 5);
                        player.Inv?.AddItem(result.PrizeItemId, 5);
                    }
                    else
                    {
                        // Minor Gold Coin Bag
                        slice = 1;
                        result = new LotterySpinResult(player.CharID, player.CharName, slice, 26000, "Carnival Bronze Token", 10);
                        player.Inv?.AddItem(result.PrizeItemId, 10);
                    }

                    m_history.Enqueue(result);
                    while (m_history.Count > 100) m_history.TryDequeue(out _);

                    DebugSystem.Write($"[LotteryGachaEngine] {player.CharName} spun wheel -> Slice {result.SliceIndex}: {result.PrizeName} (Jackpot Pool: {ProgressiveJackpotPool:N0}G)");

                    // S->C AC 76:10 Wheel Outcome Frame
                    SendPacket p = new SendPacket();
                    p.Pack8(76);
                    p.Pack8(10);
                    p.Pack8(result.SliceIndex);
                    p.Pack16(result.PrizeItemId);
                    p.Pack8((byte)result.Quantity);
                    p.Pack8((byte)(result.IsJackpot ? 1 : 0));
                    p.Pack32(player.Gold);
                    player.Send(p);

                    return result;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[LotteryGachaEngine] SpinWheel error: {ex.Message}");
                return null;
            }
        }
    }
}
