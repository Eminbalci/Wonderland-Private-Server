using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Network;

namespace Game.PlayerRelated
{
    public class StallItem
    {
        public byte InventorySlot { get; set; }
        public ushort ItemID { get; set; }
        public uint Price { get; set; }
        public byte Count { get; set; }

        public StallItem(byte slot, ushort itemId, uint price, byte count)
        {
            InventorySlot = slot;
            ItemID = itemId;
            Price = price;
            Count = count;
        }
    }

    public class PlayerStall
    {
        public Player Owner { get; }
        public string StallName { get; set; }
        public List<StallItem> Items { get; } = new List<StallItem>();
        public bool IsOpen { get; set; } = true;

        public PlayerStall(Player owner, string stallName, List<StallItem> items)
        {
            Owner = owner;
            StallName = stallName;
            if (items != null) Items.AddRange(items);
        }
    }

    public static class StallManager
    {
        private static readonly Dictionary<uint, PlayerStall> _stalls = new Dictionary<uint, PlayerStall>();
        private static readonly object _lock = new object();

        public static bool OpenStall(Player player, string stallName, List<StallItem> items)
        {
            if (player == null || items == null || items.Count == 0) return false;

            var stall = new PlayerStall(player, stallName, items);
            lock (_lock)
            {
                _stalls[player.CharID] = stall;
            }

            // Broadcast Stall Sign on Map (AC 56:30)
            SendPacket pSign = new SendPacket();
            pSign.Pack8(56);
            pSign.Pack8(30);
            pSign.Pack32(player.CharID);
            pSign.PackString(stallName);
            player.CurMap?.Broadcast(pSign);

            SendSystemMsg(player, $"Your street stall '{stallName}' is now open for business!");
            DebugSystem.Write($"[StallManager] Player {player.CharName} opened stall '{stallName}' with {items.Count} items.");
            return true;
        }

        public static void CloseStall(Player player)
        {
            if (player == null) return;

            lock (_lock)
            {
                _stalls.Remove(player.CharID);
            }

            // Remove Stall Sign (AC 56:30 with empty string)
            SendPacket pSign = new SendPacket();
            pSign.Pack8(56);
            pSign.Pack8(30);
            pSign.Pack32(player.CharID);
            pSign.PackString(string.Empty);
            player.CurMap?.Broadcast(pSign);

            SendSystemMsg(player, "Your shop has been closed.");
            DebugSystem.Write($"[StallManager] Player {player.CharName} closed their stall.");
        }

        public static void ViewStall(Player buyer, uint sellerCharId)
        {
            if (buyer == null) return;

            PlayerStall stall = null;
            lock (_lock)
            {
                _stalls.TryGetValue(sellerCharId, out stall);
            }

            if (stall == null || !stall.IsOpen)
            {
                SendSystemMsg(buyer, "This stall is no longer open.");
                return;
            }

            // Send Stall Inventory List (AC 56:3)
            SendPacket p = new SendPacket();
            p.Pack8(56);
            p.Pack8(3);
            p.Pack32(stall.Owner.CharID);
            p.PackString(stall.StallName);
            p.Pack8((byte)stall.Items.Count);

            foreach (var it in stall.Items)
            {
                p.Pack8(it.InventorySlot);
                p.Pack16(it.ItemID);
                p.Pack32(it.Price);
                p.Pack8(it.Count);
            }

            buyer.Send(p);
        }

        public static bool BuyItem(Player buyer, uint sellerCharId, byte inventorySlot, byte count)
        {
            if (buyer == null) return false;

            PlayerStall stall = null;
            lock (_lock)
            {
                _stalls.TryGetValue(sellerCharId, out stall);
            }

            if (stall == null || !stall.IsOpen)
            {
                SendSystemMsg(buyer, "Stall is unavailable.");
                return false;
            }

            var stallItem = stall.Items.FirstOrDefault(i => i.InventorySlot == inventorySlot);
            if (stallItem == null || stallItem.Count < count)
            {
                SendSystemMsg(buyer, "Item is no longer available in the requested quantity.");
                return false;
            }

            uint totalCost = stallItem.Price * count;
            if (buyer.Gold < (int)totalCost)
            {
                SendSystemMsg(buyer, "You do not have enough gold!");
                return false;
            }

            // Execute purchase
            buyer.TakeGold((int)totalCost);
            stall.Owner.AddGold((int)totalCost);

            stall.Owner.Inv.RemoveItem(stallItem.InventorySlot, count);
            buyer.Inv.AddItem(stallItem.ItemID, count);

            stallItem.Count -= count;
            if (stallItem.Count == 0)
            {
                stall.Items.Remove(stallItem);
            }

            SendSystemMsg(buyer, $"Purchased {count}x item #{stallItem.ItemID} for {totalCost} gold!");
            SendSystemMsg(stall.Owner, $"{buyer.CharName} bought {count}x item #{stallItem.ItemID} from your stall for {totalCost} gold!");

            DebugSystem.Write($"[StallManager] {buyer.CharName} bought #{stallItem.ItemID} x{count} from {stall.Owner.CharName}.");
            return true;
        }

        private static void SendSystemMsg(Player p, string msg)
        {
            if (p == null || string.IsNullOrEmpty(msg)) return;
            SendPacket s = new SendPacket();
            s.Pack8(23);
            s.Pack8(57);
            s.Pack8(0);
            s.PackString(msg);
            p.Send(s);
        }
    }
}
