using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game;
using Game.PlayerRelated;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 34: In-Game Shopping Cart Checkout & Points Synchronization Protocol
    /// - AC 34 Sub 1 [0]: Initial points balance query when opening cart/mall
    /// - AC 34 Sub 1 [X (X >= 1)]: Cart checkout for slot/row X
    /// Server responds with:
    ///   - S->C AC 34 Sub 1 [RemainingPoints(uint16)]
    ///   - S->C AC 75 Sub 3 [RemainingPoints(uint16)]
    ///   - S->C AC 35 Sub 4 [16 zero bytes] (authentic pcap packet #142 confirmation)
    /// </summary>
    public class AC34 : AC
    {
        public override int ID { get { return 34; } }

        public override void ProcessPkt(Player r, RecievePacket p)
        {
            byte subcode = p.Unpack8();
            switch (subcode)
            {
                case 1:
                    Recv1(r, p);
                    break;
                default:
                    DebugSystem.Write($"[AC34] Subcode {subcode} not handled");
                    break;
            }
        }

        void Recv1(Player p, RecievePacket r)
        {
            try
            {
                byte mode = 0;
                try { mode = r.Unpack8(); } catch { mode = 0; }

                ushort points = (ushort)Math.Min(65535, p.UserAccount != null ? p.UserAccount.IM : 0);

                if (mode == 0)
                {
                    // Balance query on Mall open
                    SendPacket resp = new SendPacket();
                    resp.Pack8(34);
                    resp.Pack8(1);
                    resp.Pack16(points);
                    p.Send(resp);

                    ItemMallManager.SendPointBalance(p);
                    ItemMallManager.SendCatalog(p);
                    DebugSystem.Write($"[AC34.Recv1] Synced IM points ({points} IM) and sent catalog for {p.CharName}");
                    return;
                }

                // mode >= 1: Shopping Cart Checkout (Confirm button in Form_Cart)
                var catalog = ItemMallManager.GetCatalog();
                MallItemEntry itemToBuy = null;

                int catIndex = mode - 1;
                if (catIndex >= 0 && catIndex < catalog.Count)
                {
                    itemToBuy = catalog[catIndex];
                }
                else if (catalog.Count > 0)
                {
                    itemToBuy = catalog[0];
                }

                if (itemToBuy != null)
                {
                    bool success = ItemMallManager.PurchaseItem(p, itemToBuy.ItemID, 1);
                    ushort remPoints = (ushort)Math.Min(65535, p.UserAccount != null ? p.UserAccount.IM : 0);

                    // 1. S->C AC 34 Sub 1: [RemainingPoints(2B)] -> Triggers client banner "WLO Point Remain: %04d Pts"
                    SendPacket resp = new SendPacket();
                    resp.Pack8(34);
                    resp.Pack8(1);
                    resp.Pack16(remPoints);
                    p.Send(resp);

                    // 2. S->C AC 75 Sub 3: [RemainingPoints(2B)] -> Updates GUI points counter
                    ItemMallManager.SendPointBalance(p);

                    // 3. S->C AC 35 Sub 4: [16 zero bytes] -> Authentic pcap #142 Cart Purchase confirmation
                    SendPacket pCart = new SendPacket();
                    pCart.Pack8(35);
                    pCart.Pack8(4);
                    for (int i = 0; i < 16; i++) pCart.Pack8(0);
                    p.Send(pCart);

                    // 4. Send updated catalog (AC 75 Sub 1)
                    ItemMallManager.SendCatalog(p);

                    if (success)
                    {
                        p.SendSystemMessage($"🛍️ Purchased 1x {itemToBuy.ItemName} for {itemToBuy.PointCost} IM! (Remaining: {remPoints} IM)");
                    }

                    DebugSystem.Write($"[AC34.Recv1] Cart Checkout Slot #{mode} -> {itemToBuy.ItemName} (#{itemToBuy.ItemID}) by {p.CharName}: {(success ? "Success" : "Failed")}");
                }
                else
                {
                    DebugSystem.Write($"[AC34.Recv1] No item found for Cart Slot #{mode}");
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC34.Recv1] Error: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
