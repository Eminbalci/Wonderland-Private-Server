using System;
using System.Collections.Generic;
using System.Linq;
using Game;
using Game.PlayerRelated;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 56: Player Personal Market Stall System Protocol.
    /// Handles setting up a stall, vending inventory items, browsing other players' stalls, and purchasing.
    /// </summary>
    public class AC56 : AC
    {
        public override int ID => 56;

        public override void ProcessPkt(Player p, RecievePacket r)
        {
            if (p == null || r == null) return;

            byte subAction = r.B ?? 0;
            DebugSystem.Write($"[AC56] Player {p.CharName} sent Stall SubAction {subAction}");

            switch (subAction)
            {
                case 1: // Open Stall
                    HandleOpenStall(p, r);
                    break;
                case 2: // Close Stall
                    HandleCloseStall(p);
                    break;
                case 3: // View Target Stall
                    HandleViewStall(p, r);
                    break;
                case 4: // Buy Item from Stall
                    HandleBuyItem(p, r);
                    break;
                case 5: // Stall Transaction History
                    HandleStallHistory(p);
                    break;
                default:
                    DebugSystem.Write($"[AC56] Unhandled Stall SubAction: {subAction}");
                    break;
            }
        }

        private void HandleOpenStall(Player p, RecievePacket r)
        {
            try
            {
                string shopTitle = r.UnpackString();
                byte count = r.Unpack8();
                var items = new List<StallItem>();

                for (int i = 0; i < count; i++)
                {
                    byte slot = r.Unpack8();
                    ushort itemId = r.Unpack16();
                    uint price = r.Unpack32();
                    byte itCount = r.Unpack8();
                    items.Add(new StallItem(slot, itemId, price, itCount));
                }

                StallManager.OpenStall(p, shopTitle, items);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC56:1] Exception opening stall: {ex.Message}");
            }
        }

        private void HandleCloseStall(Player p)
        {
            try
            {
                StallManager.CloseStall(p);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC56:2] Exception closing stall: {ex.Message}");
            }
        }

        private void HandleViewStall(Player p, RecievePacket r)
        {
            try
            {
                uint targetCharId = r.Unpack32();
                StallManager.ViewStall(p, targetCharId);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC56:3] Exception viewing stall: {ex.Message}");
            }
        }

        private void HandleBuyItem(Player p, RecievePacket r)
        {
            try
            {
                uint targetCharId = r.Unpack32();
                byte stallSlot = r.Unpack8();
                byte count = r.Unpack8();
                StallManager.BuyItem(p, targetCharId, stallSlot, count);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC56:4] Exception buying from stall: {ex.Message}");
            }
        }

        private void HandleStallHistory(Player p)
        {
            try
            {
                SendPacket sp = new SendPacket();
                sp.Pack8((byte)ID);
                sp.Pack8(5);
                sp.Pack16(0); // 0 records
                p.Send(sp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC56:5] Exception fetching history: {ex.Message}");
            }
        }
    }
}
