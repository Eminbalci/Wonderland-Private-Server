using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using DataFiles;
using Game.Battle;
using Network;

namespace Game.PlayerRelated
{
    public static class GachaManager
    {
        public class Reward
        {
            public ushort item_id { get; set; }
            public int weight { get; set; }
            public int quantity { get; set; }
        }
        public class Pack
        {
            public ushort item_id { get; set; }
            public Reward[] rewards { get; set; }
        }

        // IDs with reward tables in the original client. Lucky Pack is retained only
        // as a recognized, unavailable legacy pack; its custom pool was withdrawn.
        private static readonly ushort[] PackIds = {
            34171, 34172, 34174, 34192, 34193, 34199, 34201, 34229,
            34248, 34296, 34297, 34346, 34349, 34365, 34366, 34367,
            34381, 34382, 34383, 34384, 34385, 34386, 34387, 34388, 34333
        };
        private static Dictionary<ushort, Pack> packs = new Dictionary<ushort, Pack>();
        private static PhxItemDat items;

        public static void Load(PhxItemDat data, string path)
        {
            var loaded = new Dictionary<ushort, Pack>();
            try
            {
                var config = new JavaScriptSerializer().Deserialize<Pack[]>(File.ReadAllText(path));
                foreach (var pack in config)
                {
                    if (pack == null || !IsGacha(pack.item_id) || pack.item_id == 34333 || data.GetItemByID(pack.item_id) == null ||
                        pack.rewards == null || pack.rewards.Length == 0 || pack.rewards.Length > 41 ||
                        pack.rewards.Any(r => r == null || r.weight <= 0 || r.weight > 10000 ||
                            r.quantity < 1 || r.quantity > 255 || data.GetItemByID(r.item_id) == null) ||
                        pack.rewards.Sum(r => (long)r.weight) != 10000 || loaded.ContainsKey(pack.item_id))
                        throw new InvalidDataException("Invalid gacha reward table");
                    loaded.Add(pack.item_id, pack);
                }
            }
            catch (Exception ex)
            {
                loaded.Clear();
                DebugSystem.Write("[Gacha] Disabled invalid reward configuration: " + ex.Message);
            }
            items = data;
            packs = loaded;
        }

        public static bool IsGacha(ushort id) { return Array.IndexOf(PackIds, id) >= 0; }
        public static bool IsAvailable(ushort id) { return !IsGacha(id) || packs.ContainsKey(id); }

        public static void SendContents(Player player, ushort packId)
        {
            // Native AC91:2: pack ID, cache version, then (item ID, quantity) rows.
            // Always send the current list; client versions are only cache hints.
            var packet = new SendPacket();
            packet.Pack8(91);
            packet.Pack8(2);
            packet.Pack16(packId);
            packet.Pack8(1);
            Pack pack;
            if (packs.TryGetValue(packId, out pack))
                foreach (var reward in pack.rewards)
                {
                    packet.Pack16(reward.item_id);
                    packet.Pack8((byte)reward.quantity);
                }
            player.Send(packet);
        }

        internal static Reward SelectReward(ushort packId, int roll)
        {
            if (roll < 0 || roll >= 10000) throw new ArgumentOutOfRangeException(nameof(roll));
            foreach (var reward in packs[packId].rewards)
            {
                if (roll < reward.weight) return reward;
                roll -= reward.weight;
            }
            throw new InvalidDataException("Invalid gacha reward weights");
        }

        public static bool TryOpen(Player player, byte slot)
        {
            if (player == null || slot < 1 || slot > 50) return false;
            lock (player.Inv.SyncRoot)
            {
                var item = player.Inv[slot];
                if (!IsGacha(item.ItemID)) return false;
                if (item.isLocked || item.Ammt == 0 || PvEBattleManager.IsInBattle(player))
                {
                    player.SendHeadBanner("This pack cannot be opened right now.");
                    return true;
                }
                if (!packs.ContainsKey(item.ItemID))
                {
                    player.SendHeadBanner("This pack's rewards are unavailable. Pack retained.");
                    return true;
                }
                // Rejection sampling gives each of the 10,000 weight slots equal probability.
                uint random;
                var bytes = new byte[4];
                using (var rng = RandomNumberGenerator.Create())
                {
                    do { rng.GetBytes(bytes); random = BitConverter.ToUInt32(bytes, 0); }
                    while (random >= uint.MaxValue - uint.MaxValue % 10000);
                }
                var reward = SelectReward(item.ItemID, (int)(random % 10000));
                ushort rewardId = reward.item_id;
                if (!player.Inv.TryExchangeItem(slot, item.ItemID, new Dictionary<ushort, int> { { rewardId, reward.quantity } }))
                {
                    player.SendHeadBanner("Please free an inventory slot. Pack retained.");
                    return true;
                }
                string name = System.Text.Encoding.ASCII.GetString(items.GetItemByID(rewardId).ItemName).Trim('\0', ' ');
                player.Send(Tools.FromFormat("bb", 23, 15));
                player.SendHeadBanner("Gacha: received [" + name + "] x" + reward.quantity + ".");
                player.SaveCharacterData();
                return true;
            }
        }
    }
}
