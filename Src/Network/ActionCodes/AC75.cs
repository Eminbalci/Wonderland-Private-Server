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
                case 1: // Points Mall Catalog request
                    Recv1(r, p);
                    break;
                case 2: // Bonus Mall Catalog request
                    Recv2(r, p);
                    break;
                case 3: // Balance request
                    Recv3(r, p);
                    break;
                case 4: // Category Switch OR Points Mall Purchase
                    int rem4 = p.Buffer.Count() - p.GetPtr();
                    if (rem4 == 1)
                    {
                        byte categoryId = p.Unpack8();
                        // AC 57 Sub 1: Category ACK (Frame 4497 / 4606: 39 01 [catId] 00 00 00)
                        SendPacket ack = new SendPacket();
                        ack.PackArray(new byte[] { 57, 1, categoryId, 0, 0, 0 });
                        r.Send(ack);

                        // Sync AC 34:1 Points & AC 75:3 Points for Minigames (Slot Machine / Claw Crane)
                        uint points = (uint)ItemMallManager.GetUserPoints(r);
                        SendPacket p34 = new SendPacket();
                        p34.Pack8(34);
                        p34.Pack8(1);
                        p34.Pack32(points);
                        r.Send(p34);

                        ItemMallManager.SendPointBalance(r);

                        // If categoryId > 0, re-send Points Mall catalog; if categoryId == 0 (Exit minigame), DO NOT send catalog to prevent trap loop
                        if (categoryId > 0)
                        {
                            ItemMallManager.SendCatalog(r, isBonus: false);
                        }

                        DebugSystem.Write($"[AC75.Recv4] Client switched to Item Mall Category {categoryId}");
                    }
                    else
                    {
                        RecvBuy(r, p, subcode, isBonus: false);
                    }
                    break;
                case 5: // Bonus Mall Purchase
                    RecvBuy(r, p, subcode, isBonus: true);
                    break;
                default:
                    DebugSystem.Write($"[AC75] Subcode {subcode} received, payload: {p.Buffer.Length} bytes");
                    break;
            }
        }

        void Recv1(Player p, RecievePacket r)
        {
            try
            {
                ItemMallManager.SendCatalog(p, isBonus: false);
                ItemMallManager.SendPointBalance(p);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC75.Recv1] Error: {ex.Message}");
            }
        }

        void Recv2(Player p, RecievePacket r)
        {
            try
            {
                ItemMallManager.SendCatalog(p, isBonus: true);
                ItemMallManager.SendPointBalance(p);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC75.Recv2] Error: {ex.Message}");
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

        void RecvBuy(Player p, RecievePacket r, byte subcode, bool isBonus)
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

                var entry = ItemMallManager.GetItem(itemId, isBonus);
                int cost = (entry != null ? entry.PointCost : 0) * quantity;

                bool success = ItemMallManager.PurchaseItem(p, itemId, quantity, isBonus);
                uint remPoints = (uint)(isBonus ? ItemMallManager.GetUserBonusPoints(p) : ItemMallManager.GetUserPoints(p));
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

                DebugSystem.Write($"[AC75.RecvBuy] Subcode {subcode} ({(isBonus ? "Bonus" : "Points")}) Purchase #{itemId} ({entry?.ItemName ?? "Unknown"}) x{quantity} by {p.CharName}: {(success ? "Success" : "Failed")}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC75.RecvBuy] Error: {ex.Message}");
            }
        }
    }
}
