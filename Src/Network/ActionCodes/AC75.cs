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
    public class AC75 : AC
    {
        public override int ID { get { return 75; } }

        public override void ProcessPkt(Player r, RecievePacket p)
        {
            byte subcode = p.Unpack8();
            switch (subcode)
            {
                case 1: // Catalog request
                    Recv1(r, p);
                    break;
                case 2: // Bonus Catalog request
                    Recv1(r, p);
                    break;
                case 3: // Balance request
                    Recv3(r, p);
                    break;
                case 4: // Purchase from Bonus Mall (AC 75 Sub 4)
                case 5: // Purchase from Points Mall (AC 75 Sub 5)
                    RecvBuy(r, p, subcode);
                    break;
                default:
                    DebugSystem.Write($"[AC75] Subcode {subcode} received");
                    break;
            }
        }

        void Recv1(Player p, RecievePacket r)
        {
            try
            {
                ItemMallManager.SendCatalog(p);
                ItemMallManager.SendPointBalance(p);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC75.Recv1] Error: {ex.Message}");
            }
        }

        void Recv3(Player p, RecievePacket r)
        {
            try
            {
                ItemMallManager.SendPointBalance(p);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC75.Recv3] Error: {ex.Message}");
            }
        }

        void RecvBuy(Player p, RecievePacket r, byte subcode)
        {
            try
            {
                ushort itemId = r.Unpack16();
                byte quantity = 1;
                try
                {
                    quantity = (byte)Math.Max(1, (int)r.Unpack8());
                }
                catch { quantity = 1; }

                var catalog = ItemMallManager.GetCatalog();
                var entry = catalog.FirstOrDefault(i => i.ItemID == itemId);
                int cost = (entry != null ? entry.PointCost : 0) * quantity;

                bool success = ItemMallManager.PurchaseItem(p, itemId, quantity);
                uint remPoints = (uint)(p.UserAccount != null ? p.UserAccount.IM : 0);
                uint spentPoints = (uint)(success ? cost : 0);

                // Authentic Buy Response (aLogin.exe FUN_0025b5ec / 0x25b62f):
                // S->C AC 75 Sub [4 or 5]:
                // [AC=75, Sub=4/5, RemPoints(4B), SpentPoints(4B), ItemID(2B), Quantity(1B)]
                SendPacket resp = new SendPacket();
                resp.Pack8(75);
                resp.Pack8(subcode);
                resp.Pack32(remPoints);
                resp.Pack32(spentPoints);
                resp.Pack16(itemId);
                resp.Pack8(quantity);
                p.Send(resp);

                // Synchronize balance
                ItemMallManager.SendPointBalance(p);

                DebugSystem.Write($"[AC75.RecvBuy] Subcode {subcode} Purchase #{itemId} ({entry?.ItemName ?? "Unknown"}) x{quantity} by {p.CharName}: {(success ? "Success" : "Failed")}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC75.RecvBuy] Error: {ex.Message}");
            }
        }
    }
}
