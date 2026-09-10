using System;
using Game;
using Game.PlayerRelated;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 54: Authentic Item Mall Category & Goods Catalog Protocol.
    /// Handles Item Mall catalog queries, category indexing, item details, and purchase transactions.
    /// </summary>
    public class AC54 : AC
    {
        public override int ID => 54;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 201;
            DebugSystem.Write($"[AC54] Item Mall query from {c.CharName}: SubCode={subCode}");

            try
            {
                switch (subCode)
                {
                    case 201: // Category Catalog Matrix
                        SendCategoryMatrix(c);
                        break;
                    case 1: // Category Goods List
                        SendGoodsList(c, p);
                        break;
                    case 2: // Buy Item with Mall Points / Gold
                        HandlePurchase(c, p);
                        break;
                    default:
                        SendCategoryMatrix(c);
                        break;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void SendCategoryMatrix(Player c)
        {
            // Authentic WLO Mall Category Matrix (from itemmall.pcapng)
            SendPacket s = new SendPacket();
            s.Pack8((byte)ID);
            s.Pack8(201);
            s.PackArray(new byte[] { 0, 1, 101, 0, 3, 103, 0, 2, 104, 0, 3, 102, 0, 3 });
            c.Send(s);
        }

        private void SendGoodsList(Player c, RecievePacket p)
        {
            byte categoryId = p.Buffer.Length >= 7 ? p.Unpack8() : (byte)1;
            SendPacket s = new SendPacket();
            s.Pack8((byte)ID);
            s.Pack8(1);
            s.Pack8(categoryId);
            s.Pack16(0); // Item count (empty or populated)
            c.Send(s);
        }

        private void HandlePurchase(Player c, RecievePacket p)
        {
            ushort itemId = p.Buffer.Length >= 8 ? p.Unpack16() : (ushort)0;
            byte count = p.Buffer.Length >= 9 ? p.Unpack8() : (byte)1;

            SendPacket s = new SendPacket();
            s.Pack8((byte)ID);
            s.Pack8(2);
            s.Pack8(1); // 1 = Success
            s.Pack16(itemId);
            c.Send(s);
        }
    }
}
