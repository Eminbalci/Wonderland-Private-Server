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
    /// AC 21: Official Native In-Game Item Mall Protocol
    /// </summary>
    public class AC21 : AC
    {
        public override int ID { get { return 21; } }

        public override void ProcessPkt(Player player, RecievePacket r)
        {
            try
            {
                byte subCode = r.Unpack8();
                switch (subCode)
                {
                    case 1: // Open / Refresh Item Mall Native GUI Window
                        SendMallWindow(player);
                        break;
                    case 2: // Buy Item from Native GUI
                        RecvBuy(player, r);
                        break;
                    case 3: // Query Item / Point Balance
                        ItemMallManager.SendPointBalance(player);
                        break;
                    default:
                        DebugSystem.Write($"[AC21] Unknown SubAction: {subCode}");
                        break;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC21.ProcessPkt] Error: {ex.Message}");
            }
        }

        public static void SendMallWindow(Player player)
        {
            try
            {
                // Send IM Point Balance first
                ItemMallManager.SendPointBalance(player);

                // S->C AC 21 Sub 1: [21, 1, 21 slot entries]
                SendPacket p = new SendPacket();
                p.Pack8(21);
                p.Pack8(1);

                // Pack 21 slots for the 7 categories
                for (byte i = 1; i <= 21; i++)
                {
                    p.Pack8(i);
                }

                player.Send(p);
                DebugSystem.Write($"[AC21] Sent Native Item Mall GUI Window to {player.CharName}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC21.SendMallWindow] Error: {ex.Message}");
            }
        }

        private void RecvBuy(Player player, RecievePacket r)
        {
            try
            {
                byte slot = r.Unpack8();
                var catalog = ItemMallManager.GetCatalog();
                if (slot >= 1 && slot <= catalog.Count)
                {
                    var item = catalog[slot - 1];
                    if (ItemMallManager.PurchaseItem(player, item.ItemID, item.Count))
                    {
                        player.SendSystemMessage($"Purchased {item.ItemName} for {item.PointCost} IM Points!");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC21.RecvBuy] Error: {ex.Message}");
            }
        }
    }
}
