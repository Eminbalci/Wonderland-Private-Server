using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Network;
using Game;
using Game.Code;
using wlo.pserver.core.Game;

namespace Network.ActionCodes
{
    /// <summary>
    /// Handles Daily Lucky Draw / Spin Wheel minigame (AC 104).
    /// </summary>
    public class AC104 : AC
    {
        public override int ID { get { return 104; } }

        private static readonly Random _rng = new Random();

        public class LuckyDrawItem
        {
            public ushort ItemId { get; set; }
            public byte SlotIndex { get; set; }
            public string Name { get; set; }

            public LuckyDrawItem(ushort itemId, byte slotIndex, string name)
            {
                ItemId = itemId;
                SlotIndex = slotIndex;
                Name = name;
            }
        }

        // Sample authentic Lucky Draw Item Rewards & Slot mapping
        private static readonly LuckyDrawItem[] LuckyDrawRewards = new LuckyDrawItem[]
        {
            new LuckyDrawItem(34328, 8, "Tenacious Jade"),
            new LuckyDrawItem(34514, 17, "Glory Halo"),
            new LuckyDrawItem(34148, 14, "Spar"),
            new LuckyDrawItem(30002, 5, "Instant Noodles"),
            new LuckyDrawItem(28014, 11, "Coconut"),
            new LuckyDrawItem(30052, 2, "Raft Voucher")
        };

        public override void ProcessPkt(Player player, RecievePacket p)
        {
            switch (p.B)
            {
                case 1: Recv1(player, p); break; // Spin Wheel Request
                default:
                    DebugSystem.Write($"[AC104] Action Code 104,{p.B} has not been coded");
                    break;
            }
        }

        /// <summary>
        /// Handles Spin Wheel Request: C->S [104, 1]
        /// </summary>
        private void Recv1(Player player, RecievePacket p)
        {
            try
            {
                var reward = LuckyDrawRewards[_rng.Next(LuckyDrawRewards.Length)];

                // 1. Grant reward item to player's inventory (AC 23 Sub 6)
                player.Inv.AddItem(reward.ItemId, 1);

                // 2. Send wheel landing slot & spin counter: S->C [104, 1, 2, slot_index (1B), counter (1B)]
                SendPacket spinResult = Tools.FromFormat("bbbbb", 104, 1, 2, reward.SlotIndex, (byte)1);
                player.Send(spinResult);

                // 3. Send Lucky Draw global broadcast announcement: S->C [2, 15, 4, item_id (2B), amount (1B), string(player_name)]
                SendPacket broadcastPkt = new SendPacket();
                broadcastPkt.PackArray(new byte[] { 2, 15, 4 });
                broadcastPkt.Pack16(reward.ItemId);
                broadcastPkt.Pack8(1);
                broadcastPkt.PackString(player.CharName);
                player.Send(broadcastPkt);

                // 4. Update remaining daily draw credits: S->C [35, 12, credits (4B)]
                SendPacket creditsPkt = Tools.FromFormat("bbd", 35, 12, (uint)0);
                player.Send(creditsPkt);

                DebugSystem.Write($"[AC104] Player {player.CharName} completed Daily Lucky Draw! Won: {reward.Name} (ID: {reward.ItemId}) on Slot {reward.SlotIndex}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC104.Recv1] Error: {ex.Message}");
            }
        }
    }
}
