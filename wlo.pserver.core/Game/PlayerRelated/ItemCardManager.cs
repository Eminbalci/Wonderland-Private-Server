using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using DataFiles;
using Game.Battle;
using Network;

namespace Game.PlayerRelated
{
    public static class ItemCardManager
    {
        public class Reward
        {
            public ushort item_id { get; set; }
            public int quantity { get; set; }
        }

        public class Entry
        {
            public ushort card_id { get; set; }
            public ushort male_item { get; set; }
            public ushort female_item { get; set; }
            public Reward[] rewards { get; set; }
        }

        private struct PendingExchange
        {
            public byte Slot;
            public ushort CardId;

            public PendingExchange(byte slot, ushort cardId)
            {
                Slot = slot;
                CardId = cardId;
            }
        }

        private static Dictionary<ushort, Entry> cards = new Dictionary<ushort, Entry>();
        private static PhxItemDat items;
        private static readonly ConcurrentDictionary<uint, PendingExchange> pending = new ConcurrentDictionary<uint, PendingExchange>();

        public static void Load(PhxItemDat data, string path)
        {
            var loaded = new Dictionary<ushort, Entry>();
            try
            {
                if (File.Exists(path))
                {
                    var config = new JavaScriptSerializer().Deserialize<Entry[]>(File.ReadAllText(path));
                    foreach (var e in config ?? new Entry[0])
                    {
                        if (e == null || e.card_id == 0) continue;
                        if (e.rewards == null || e.rewards.Length == 0) continue;

                        bool missingReward = false;
                        foreach (var r in e.rewards)
                        {
                            if (r == null || r.item_id == 0 || data?.GetItemByID(r.item_id) == null)
                            {
                                DebugSystem.Write($"[ItemCard] Skipping card {e.card_id}: reward item {r?.item_id ?? 0} is not in the item data.");
                                missingReward = true;
                                break;
                            }
                            if (r.quantity < 1 || r.quantity > 255) r.quantity = 1;
                        }
                        if (missingReward) continue;

                        loaded[e.card_id] = e;
                    }
                }
                else
                {
                    DebugSystem.Write($"[ItemCard] {path} not found; item cards cannot be redeemed.");
                }
            }
            catch (Exception ex)
            {
                loaded.Clear();
                DebugSystem.Write("[ItemCard] Failed to load card table: " + ex.Message);
            }
            items = data;
            cards = loaded;
            DebugSystem.Write($"[ItemCard] Loaded {cards.Count} item redemption cards.");
        }

        public static int Count
        {
            get { return cards.Count; }
        }

        public static bool IsCard(ushort id)
        {
            return cards.ContainsKey(id);
        }

        public static bool TryGetCard(ushort id, out Entry card)
        {
            return cards.TryGetValue(id, out card);
        }

        public static bool TryRedeem(Player player, byte slot)
        {
            if (player == null || player.Inv == null || slot < 1 || slot > 50) return false;

            lock (player.Inv.SyncRoot)
            {
                var item = player.Inv[slot];
                if (item == null || !cards.TryGetValue(item.ItemID, out var card)) return false;

                if (item.isLocked || item.Ammt == 0 || PvEBattleManager.IsInBattle(player))
                    return true;

                pending[player.CharID] = new PendingExchange(slot, card.card_id);

                if (card.male_item != 0)
                {
                    ushort targetItem = (player.Eqs != null && (player.Eqs.Body == BodyStyle.Small_Female || player.Eqs.Body == BodyStyle.Big_Female))
                        ? card.female_item
                        : card.male_item;

                    byte targetIndex = 0;
                    if (card.rewards != null)
                    {
                        for (int i = 0; i < card.rewards.Length; i++)
                        {
                            if (card.rewards[i] != null && card.rewards[i].item_id == targetItem)
                            {
                                targetIndex = (byte)(i + 1);
                                break;
                            }
                        }
                    }

                    Exchange(player, slot, targetIndex, targetItem);
                    return true;
                }
                else if (card.rewards != null && card.rewards.Length == 1)
                {
                    Exchange(player, slot, 1, card.rewards[0].item_id);
                    return true;
                }
                else
                {
                    player.Send(BuildDetails(new[] { card }));

                    SendPacket promptPkt = new SendPacket();
                    promptPkt.Pack8(23);
                    promptPkt.Pack8(108);
                    promptPkt.Pack8(4);
                    promptPkt.Pack8(slot);
                    player.Send(promptPkt);

                    return true;
                }
            }
        }

        public static bool Exchange(Player player, byte slot, byte index, ushort rewardId)
        {
            if (player == null || player.Inv == null || slot < 1 || slot > 50) return false;

            lock (player.Inv.SyncRoot)
            {
                if (PvEBattleManager.IsInBattle(player)) return false;

                PendingExchange pEntry;
                if (!pending.TryGetValue(player.CharID, out pEntry))
                {
                    var slotItem = player.Inv[slot];
                    if (slotItem == null || !cards.TryGetValue(slotItem.ItemID, out var knownCard))
                    {
                        return Fail(player);
                    }
                    pEntry = new PendingExchange(slot, knownCard.card_id);
                    pending[player.CharID] = pEntry;
                }
                else if (pEntry.Slot != slot)
                {
                    return false;
                }

                ushort cardId = pEntry.CardId;
                if (!cards.TryGetValue(cardId, out var card))
                {
                    return Fail(player);
                }

                var item = player.Inv[slot];
                if (item == null || item.ItemID != cardId || item.isLocked || item.Ammt == 0)
                {
                    return Fail(player);
                }

                if (card.rewards == null || index < 1 || index > card.rewards.Length)
                {
                    return Fail(player);
                }

                var reward = card.rewards[index - 1];
                if (reward == null || reward.item_id != rewardId)
                {
                    return Fail(player);
                }

                if (card.male_item != 0)
                {
                    ushort expectedGenderItem = (player.Eqs != null && (player.Eqs.Body == BodyStyle.Small_Female || player.Eqs.Body == BodyStyle.Big_Female))
                        ? card.female_item
                        : card.male_item;
                    if (rewardId != expectedGenderItem)
                    {
                        return Fail(player);
                    }
                }

                int quantity = reward.quantity;
                if (quantity < 1 || quantity > 255) quantity = 1;

                if (!player.Inv.TryExchangeItem(slot, cardId, new Dictionary<ushort, int> { { rewardId, quantity } }))
                {
                    return Fail(player);
                }

                pending.TryRemove(player.CharID, out _);

                SendPacket sp = new SendPacket();
                sp.Pack8(23);
                sp.Pack8(108);
                sp.Pack8(3);
                sp.Pack16(cardId);
                sp.Pack8(1);
                sp.Pack16(rewardId);
                sp.Pack8((byte)quantity);
                player.Send(sp);

                player.Send(Tools.FromFormat("bb", 23, 15));
                player.SaveCharacterData();
                return true;
            }
        }

        private static bool Fail(Player player)
        {
            if (player != null)
            {
                pending.TryRemove(player.CharID, out _);
                player.Send(Tools.FromFormat("bb", 23, 15));
                player.SendHeadBanner("Cannot exchange this card. Check inventory space and ownership.");
            }
            return false;
        }

        public static byte[] BuildDetails(IEnumerable<Entry> cards)
        {
            var list = cards != null ? cards.Where(c => c != null).ToList() : new List<Entry>();
            SendPacket p = new SendPacket();
            p.Pack8(23);
            p.Pack8(108);
            p.Pack8(2);
            p.Pack16((ushort)list.Count);
            foreach (var card in list)
            {
                p.Pack16(card.card_id);
                p.Pack8(1);
                var rewards = card.rewards != null ? card.rewards.Where(r => r != null).ToList() : new List<Reward>();
                p.Pack8((byte)rewards.Count);
                foreach (var r in rewards)
                {
                    p.Pack16(r.item_id);
                    p.Pack8((byte)r.quantity);
                }
            }
            return p.Buffer;
        }

        public static byte[] BuildIndex()
        {
            var list = cards.Values.Where(c => c != null).OrderBy(c => c.card_id).ToList();
            SendPacket p = new SendPacket();
            p.Pack8(23);
            p.Pack8(108);
            p.Pack8(1);
            p.Pack16((ushort)list.Count);
            foreach (var card in list)
            {
                p.Pack16(card.card_id);
                p.Pack8(1);
            }
            return p.Buffer;
        }
    }
}
