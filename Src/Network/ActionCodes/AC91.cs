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
    /// ActionCode 91 (0x5B Hex): Wonderland Online Item Mall Bonus & Reward Protocol.
    /// Reverse-engineered from authentic PCAP capture 'itemmallvebonuskismi.pcapng' (Frame 4767 & 4768):
    /// - AC 91 Sub 1 (C->S): Query bonus reward catalog: [0x5B, 0x01, category_id (2B LE), page (1B)]
    /// - AC 91 Sub 2 (S->C): Bonus catalog response: [0x5B, 0x02, category_id (2B LE), page (1B), entries...]
    ///   Each entry is 3 bytes: [item_id: uint16 LE, count: uint8]
    /// - AC 91 Sub 3 (C->S): Claim bonus reward item: [0x5B, 0x03, item_id (2B LE)]
    /// </summary>
    public class AC91 : AC
    {
        public override int ID { get { return 91; } } // 0x5B in Hex

        // Default authentic reward pool from live capture (Frame 4768)
        private static readonly Tuple<ushort, byte>[] AuthenticBonusItems = new Tuple<ushort, byte>[]
        {
            Tuple.Create((ushort)35135, (byte)1), // 0x893F
            Tuple.Create((ushort)35136, (byte)1), // 0x8940
            Tuple.Create((ushort)34029, (byte)1), // 0x84ED
            Tuple.Create((ushort)34181, (byte)1), // 0x8585
            Tuple.Create((ushort)33031, (byte)1), // 0x8107
            Tuple.Create((ushort)34116, (byte)1), // 0x8544
            Tuple.Create((ushort)34011, (byte)1), // 0x84DB
            Tuple.Create((ushort)34105, (byte)1), // 0x8539
            Tuple.Create((ushort)34167, (byte)1), // 0x8577
            Tuple.Create((ushort)30556, (byte)1), // 0x775C
            Tuple.Create((ushort)22884, (byte)1)  // 0x5964
        };

        public override void ProcessPkt(Player p, RecievePacket r)
        {
            if (p == null || r == null) return;

            byte sub = r.B ?? 0;
            switch (sub)
            {
                case 1:
                    Recv1(p, r);
                    break;
                case 3:
                    Recv3(p, r);
                    break;
                default:
                    DebugSystem.Write($"[AC91] Unhandled Bonus subcode: AC 91,{sub}");
                    p.Send(Tools.FromFormat("bb", 20, 8));
                    break;
            }
        }

        /// <summary>
        /// Handles client bonus catalog request (AC 91 Sub 1).
        /// Responds with authentic prize items table (AC 91 Sub 2).
        /// </summary>
        private void Recv1(Player p, RecievePacket r)
        {
            try
            {
                ushort catId = 0xDEB0;
                byte page = 1;

                int rem = r.Buffer.Count() - r.GetPtr();
                if (rem >= 2) catId = r.Unpack16();
                if (rem >= 3) page = r.Unpack8();
                if (page == 0) page = 1;

                DebugSystem.Write($"[AC91.Recv1] Player {p.CharName} queried Bonus Catalog (Cat: 0x{catId:X}, Page: {page}).");

                // Build authentic AC 91 Sub 2 response (Frame 4768)
                SendPacket resp = new SendPacket();
                resp.Pack8(91);
                resp.Pack8(2);
                resp.Pack16(catId);
                resp.Pack8(page);

                // Pack reward items (ushort ItemID + byte Count)
                foreach (var item in AuthenticBonusItems)
                {
                    resp.Pack16(item.Item1);
                    resp.Pack8(item.Item2);
                }

                p.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC91.Recv1] Error in Bonus Catalog: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles claiming a specific bonus reward item (AC 91 Sub 3).
        /// </summary>
        private void Recv3(Player p, RecievePacket r)
        {
            try
            {
                ushort itemId = 0;
                int rem = r.Buffer.Count() - r.GetPtr();
                if (rem >= 2) itemId = r.Unpack16();

                int bonusPts = ItemMallManager.GetUserBonusPoints(p);
                const int cost = 100; // 100 bonus points per claim

                if (bonusPts >= cost && itemId > 0)
                {
                    ItemMallManager.SetUserBonusPoints(p, bonusPts - cost);

                    if (p.Inv != null)
                    {
                        p.Inv.AddItem(itemId, 1);

                        // Authentic AC 23 Sub 6 acquisition popup
                        SendPacket p6 = new SendPacket();
                        p6.Pack8(23);
                        p6.Pack8(6);
                        p6.Pack16(itemId);
                        p6.Pack8(1);
                        p6.PackArray(new byte[28]);
                        p.Send(p6);

                        p.SaveCharacterData();
                    }

                    int remaining = ItemMallManager.GetUserBonusPoints(p);
                    ItemMallManager.SendPointBalance(p);

                    SendPacket sysMsg = new SendPacket();
                    sysMsg.Pack8(23);
                    sysMsg.Pack8(57);
                    sysMsg.Pack8(0);
                    sysMsg.PackString($" Claimed bonus item! Remaining Bonus Points: {remaining}.");
                    p.Send(sysMsg);

                    DebugSystem.Write($"[AC91.Recv3] {p.CharName} claimed bonus item #{itemId} for {cost} bonus points.");
                }
                else
                {
                    SendPacket sysMsg = new SendPacket();
                    sysMsg.Pack8(23);
                    sysMsg.Pack8(57);
                    sysMsg.Pack8(0);
                    sysMsg.PackString(bonusPts < cost ? "Not enough Bonus Points to claim this reward." : "Invalid item.");
                    p.Send(sysMsg);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC91.Recv3] Error claiming bonus reward: {ex.Message}");
            }
        }
    }
}
