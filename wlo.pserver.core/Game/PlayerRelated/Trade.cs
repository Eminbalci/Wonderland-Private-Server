using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Network;

namespace Game.PlayerRelated
{
    public class TradeOfferItem
    {
        public byte InventorySlot { get; set; }
        public ushort ItemID { get; set; }
        public byte Count { get; set; }

        public TradeOfferItem(byte slot, ushort itemId, byte count)
        {
            InventorySlot = slot;
            ItemID = itemId;
            Count = count;
        }
    }

    public class TradeSession
    {
        public Player Player1 { get; }
        public Player Player2 { get; }

        public uint Player1Gold { get; set; }
        public uint Player2Gold { get; set; }

        public List<TradeOfferItem> Player1Items { get; } = new List<TradeOfferItem>();
        public List<TradeOfferItem> Player2Items { get; } = new List<TradeOfferItem>();

        public bool Player1Locked { get; set; }
        public bool Player2Locked { get; set; }

        public bool Player1Accepted { get; set; }
        public bool Player2Accepted { get; set; }

        public TradeSession(Player p1, Player p2)
        {
            Player1 = p1;
            Player2 = p2;
        }

        public Player GetPartner(Player p) => p == Player1 ? Player2 : Player1;
    }

    public static class TradeSystem
    {
        private static readonly Dictionary<uint, TradeSession> _activeTrades = new Dictionary<uint, TradeSession>();
        private static readonly Dictionary<uint, uint> _pendingTradeRequests = new Dictionary<uint, uint>(); // TargetID -> RequesterID
        private static readonly object _lock = new object();

        public static void RequestTrade(Player requester, Player target)
        {
            if (requester == null || target == null) return;

            if (requester.CharID == target.CharID)
            {
                SendSystemMsg(requester, "You cannot trade with yourself!");
                return;
            }

            if (requester.CurMap == null || target.CurMap == null || requester.CurMap.MapID != target.CurMap.MapID)
            {
                SendSystemMsg(requester, "You must be on the same map as the target to trade.");
                return;
            }

            double dx = requester.CurX - target.CurX;
            double dy = requester.CurY - target.CurY;
            if ((dx * dx) + (dy * dy) > (180 * 180))
            {
                SendSystemMsg(requester, $"{target.CharName} is too far away to trade.");
                return;
            }

            lock (_lock)
            {
                if (_activeTrades.ContainsKey(requester.CharID) || _activeTrades.ContainsKey(target.CharID))
                {
                    SendSystemMsg(requester, "Either you or the target is already in a trade!");
                    return;
                }

                _pendingTradeRequests[target.CharID] = requester.CharID;
            }

            // Send Trade Request Prompt to Target
            SendPacket pReq = new SendPacket();
            pReq.Pack8(25);
            pReq.Pack8(1);
            pReq.Pack32(requester.CharID);
            target.Send(pReq);

            SendSystemMsg(requester, $"Trade request sent to {target.CharName}.");
        }

        public static void AcceptTradeRequest(Player target)
        {
            if (target == null) return;
            uint requesterId = 0;

            lock (_lock)
            {
                if (!_pendingTradeRequests.TryGetValue(target.CharID, out requesterId))
                    return;
                _pendingTradeRequests.Remove(target.CharID);
            }

            Player requester = null;
            if (target.CurMap is GameMap curMap)
            {
                requester = curMap.PlayersList.FirstOrDefault(p => p.CharID == requesterId);
            }

            if (requester == null)
            {
                SendSystemMsg(target, "The trading partner is no longer available.");
                return;
            }

            double dx = target.CurX - requester.CurX;
            double dy = target.CurY - requester.CurY;
            if ((dx * dx) + (dy * dy) > (180 * 180))
            {
                SendSystemMsg(target, $"{requester.CharName} is too far away to trade.");
                return;
            }

            var session = new TradeSession(requester, target);
            lock (_lock)
            {
                _activeTrades[requester.CharID] = session;
                _activeTrades[target.CharID] = session;
            }

            // Open Trade Window for both (AC 25:1)
            SendPacket pOpen1 = new SendPacket();
            pOpen1.Pack8(25);
            pOpen1.Pack8(1);
            pOpen1.Pack32(target.CharID);
            requester.Send(pOpen1);

            SendPacket pOpen2 = new SendPacket();
            pOpen2.Pack8(25);
            pOpen2.Pack8(1);
            pOpen2.Pack32(requester.CharID);
            target.Send(pOpen2);

            DebugSystem.Write($"[TradeSystem] Trade session started between {requester.CharName} and {target.CharName}.");
        }

        public static void CancelTrade(Player player)
        {
            if (player == null) return;

            TradeSession session = null;
            lock (_lock)
            {
                _pendingTradeRequests.Remove(player.CharID);

                if (_activeTrades.TryGetValue(player.CharID, out session))
                {
                    _activeTrades.Remove(session.Player1.CharID);
                    _activeTrades.Remove(session.Player2.CharID);
                }
            }

            if (session != null)
            {
                Player partner = session.GetPartner(player);

                // Send Cancel / Close Packet (AC 25:2 [2 = cancel/closed])
                SendPacket pCancel = new SendPacket();
                pCancel.Pack8(25);
                pCancel.Pack8(2);
                pCancel.Pack8(2); // Cancelled

                player.Send(pCancel);
                partner?.Send(pCancel);

                SendSystemMsg(player, "Trade cancelled.");
                SendSystemMsg(partner, "Trade was cancelled by the other player.");
            }
        }

        public static void SetOffer(Player player, uint gold, List<TradeOfferItem> items)
        {
            if (player == null) return;

            TradeSession session = null;
            lock (_lock)
            {
                _activeTrades.TryGetValue(player.CharID, out session);
            }

            if (session == null) return;

            Player partner = session.GetPartner(player);

            if (player == session.Player1)
            {
                session.Player1Gold = Math.Min(gold, (uint)player.Gold);
                session.Player1Items.Clear();
                if (items != null) session.Player1Items.AddRange(items);
                session.Player1Locked = true;
            }
            else
            {
                session.Player2Gold = Math.Min(gold, (uint)player.Gold);
                session.Player2Items.Clear();
                if (items != null) session.Player2Items.AddRange(items);
                session.Player2Locked = true;
            }

            // Send Partner's Offer (AC 25:3)
            SendPacket pOffer = new SendPacket();
            pOffer.Pack8(25);
            pOffer.Pack8(3);
            pOffer.Pack32(gold);
            pOffer.Pack8((byte)(items?.Count ?? 0));

            if (items != null)
            {
                foreach (var it in items)
                {
                    pOffer.Pack16(it.ItemID);
                    pOffer.Pack8(it.Count);
                    pOffer.Pack8(0); // Damage
                }
            }

            partner?.Send(pOffer);
        }

        public static void SetTradeItem(Player player, byte tradeSlot, byte invSlot, byte count)
        {
            if (player == null) return;
            var inv = player.Inv;
            var it = inv?[invSlot];
            if (it != null && it.ItemID > 0)
            {
                SetOffer(player, 0, new List<TradeOfferItem> { new TradeOfferItem(invSlot, it.ItemID, Math.Min(count, it.Ammt)) });
            }
        }

        public static void SetTradeGold(Player player, uint gold)
        {
            SetOffer(player, gold, null);
        }

        public static void LockTrade(Player player)
        {
        }

        public static void ConfirmTrade(Player player)
        {
            FinalizeTrade(player);
        }

        public static void FinalizeTrade(Player player)
        {
            if (player == null) return;

            TradeSession session = null;
            lock (_lock)
            {
                _activeTrades.TryGetValue(player.CharID, out session);
            }

            if (session == null) return;

            if (player == session.Player1) session.Player1Accepted = true;
            else session.Player2Accepted = true;

            if (session.Player1Accepted && session.Player2Accepted)
            {
                ExecuteTrade(session);
            }
        }

        private static void ExecuteTrade(TradeSession session)
        {
            lock (_lock)
            {
                _activeTrades.Remove(session.Player1.CharID);
                _activeTrades.Remove(session.Player2.CharID);
            }

            Player p1 = session.Player1;
            Player p2 = session.Player2;

            if (p1 == null || p2 == null || p1.Inv == null || p2.Inv == null) return;

            // 1. Verify gold balances
            if (session.Player1Gold > 0 && p1.Gold < (int)session.Player1Gold)
            {
                SendSystemMsg(p1, "Trade failed: Not enough gold.");
                SendSystemMsg(p2, $"{p1.CharName} does not have enough gold.");
                CancelTrade(p1);
                return;
            }
            if (session.Player2Gold > 0 && p2.Gold < (int)session.Player2Gold)
            {
                SendSystemMsg(p2, "Trade failed: Not enough gold.");
                SendSystemMsg(p1, $"{p2.CharName} does not have enough gold.");
                CancelTrade(p2);
                return;
            }

            // 2. Verify offered items still exist in inventory with requested quantities
            foreach (var it in session.Player1Items)
            {
                var cur = p1.Inv[it.InventorySlot];
                if (cur == null || cur.ItemID != it.ItemID || cur.Ammt < it.Count)
                {
                    SendSystemMsg(p1, "Trade failed: Offered items have changed.");
                    SendSystemMsg(p2, $"{p1.CharName}'s offered items have changed.");
                    CancelTrade(p1);
                    return;
                }
            }
            foreach (var it in session.Player2Items)
            {
                var cur = p2.Inv[it.InventorySlot];
                if (cur == null || cur.ItemID != it.ItemID || cur.Ammt < it.Count)
                {
                    SendSystemMsg(p2, "Trade failed: Offered items have changed.");
                    SendSystemMsg(p1, $"{p2.CharName}'s offered items have changed.");
                    CancelTrade(p2);
                    return;
                }
            }

            // 3. Verify inventory space
            int p1Needed = session.Player2Items.Count;
            int p2Needed = session.Player1Items.Count;
            if (p1.Inv.FreeSpace < p1Needed)
            {
                SendSystemMsg(p1, "Trade failed: Your inventory does not have enough space.");
                SendSystemMsg(p2, $"Trade failed: {p1.CharName}'s inventory is full.");
                CancelTrade(p1);
                return;
            }
            if (p2.Inv.FreeSpace < p2Needed)
            {
                SendSystemMsg(p2, "Trade failed: Your inventory does not have enough space.");
                SendSystemMsg(p1, $"Trade failed: {p2.CharName}'s inventory is full.");
                CancelTrade(p2);
                return;
            }

            // 4. Gold Transfer
            if (session.Player1Gold > 0)
            {
                p1.TakeGold((int)session.Player1Gold);
                p2.AddGold((int)session.Player1Gold);
            }
            if (session.Player2Gold > 0)
            {
                p2.TakeGold((int)session.Player2Gold);
                p1.AddGold((int)session.Player2Gold);
            }

            // 5. Items Transfer
            foreach (var it in session.Player1Items)
            {
                p1.Inv.RemoveItem(it.InventorySlot, it.Count);
                p2.Inv.AddItem(it.ItemID, it.Count);
            }

            foreach (var it in session.Player2Items)
            {
                p2.Inv.RemoveItem(it.InventorySlot, it.Count);
                p1.Inv.AddItem(it.ItemID, it.Count);
            }

            // 6. Atomic Save
            p1.SaveCharacterData();
            p2.SaveCharacterData();

            // 7. Send Trade Completed Packet (AC 25:2 [4])
            SendPacket pDone = new SendPacket();
            pDone.Pack8(25);
            pDone.Pack8(2);
            pDone.Pack8(4); // Success

            p1.Send(pDone);
            p2.Send(pDone);

            SendSystemMsg(p1, "Trade completed successfully!");
            SendSystemMsg(p2, "Trade completed successfully!");

            DebugSystem.Write($"[TradeSystem] Trade successfully executed between {p1.CharName} and {p2.CharName}.");
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
