using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// ActionCode 91 (0x5B Hex): Wonderland Online Bonus & Lucky Draw / Roulette System.
    /// Handles client bonus draw requests and returns authentic prize tables and win outcomes.
    /// Verified from official packet capture 'itemmallvebonuskismi.pcapng' (Frame 4767 & 4768).
    /// </summary>
    public class AC91 : AC
    {
        public override int ID { get { return 91; } } // 0x5B in Hex

        // Default authentic reward pool from capture (Frame 4768)
        private static readonly ushort[] DefaultPrizeItems = new ushort[]
        {
            35135, // 0x893F
            35136, // 0x8940
            34029, // 0x84ED
            34181, // 0x8585
            33031, // 0x8107
            34116, // 0x8544
            34011, // 0x84DB
            34105, // 0x8539
            34167, // 0x8577
            30556, // 0x775C
            22884  // 0x5964
        };

        private static readonly Random _rng = new Random();

        public override void ProcessPkt(Player p, RecievePacket r)
        {
            if (p == null || r == null) return;

            byte sub = r.B ?? 0;
            switch (sub)
            {
                case 1:
                    Recv1(p, r);
                    break;
                default:
                    DebugSystem.Write($"[AC91] Unhandled Bonus subcode: AC 91,{sub}");
                    break;
            }
        }

        /// <summary>
        /// Handles client bonus lottery/roulette draw request (AC 91 Sub 1).
        /// Responds with prize table and rewards the player (AC 91 Sub 2).
        /// </summary>
        private void Recv1(Player p, RecievePacket r)
        {
            try
            {
                ushort drawId = 0xDEB0; // 57008 default
                byte drawType = 0;

                int rem = r.Buffer.Count() - r.GetPtr();
                if (rem >= 2) drawId = r.Unpack16();
                if (rem >= 3) drawType = r.Unpack8();

                DebugSystem.Write($"[AC91.Recv1] Player {p.CharName} spun Bonus Draw #0x{drawId:X} (Type: {drawType}).");

                // Pick a winning prize from the authentic pool
                ushort wonItem = DefaultPrizeItems[_rng.Next(DefaultPrizeItems.Length)];

                // Build authentic AC 91 Sub 2 response (Frame 4768)
                SendPacket resp = new SendPacket();
                resp.PackArray(new byte[] { 91, 2 });
                resp.Pack16(drawId);
                resp.Pack8(1); // Status: 1 (Success)

                // Pack reward items (ushort ItemID + byte Count)
                foreach (var itemId in DefaultPrizeItems)
                {
                    resp.Pack16(itemId);
                    resp.Pack8(1); // count = 1
                }

                p.Send(resp);

                // Grant reward item to inventory if player has space
                if (p.Inv != null)
                {
                    p.Inv.AddItem(wonItem, 1);
                    p.SendSystemMessage($"🎁 Bonus Draw: You won Item #{wonItem} x1!");
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC91.Recv1] Error in Bonus Draw: {ex.Message}");
            }
        }
    }
}
