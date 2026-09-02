using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game;
using Network;

namespace Network.ActionCodes
{
    public class AC226 : AC
    {
        public override int ID { get { return 226; } }

        public override void ProcessPkt(Player p, RecievePacket r)
        {
            switch (r.B)
            {
                case 255:
                    Recv255(p, r);
                    break;
                default:
                    DebugSystem.Write($"[AC226] Unknown SubAction: {r.B} from {p.CharName}");
                    break;
            }
        }

        private void Recv255(Player p, RecievePacket r)
        {
            try
            {
                // Authentic WLO Catalog Matrix (Packet #124, #141, #155 in itemmall.pcapng)
                SendPacket s1 = new SendPacket();
                s1.Pack8(238);
                s1.Pack8(183);
                s1.Pack8(0);
                s1.Pack8(255);
                s1.Pack16(27);
                s1.Pack8(1);
                s1.Pack16(29);
                s1.Pack8(2);
                s1.Pack16(24);
                s1.Pack8(0);
                p.Send(s1);

                // Authentic WLO Mall State / Claim Sync
                SendPacket s2 = new SendPacket();
                s2.Pack8(225);
                s2.Pack8(252);
                s2.PackArray(new byte[10]);
                p.Send(s2);

                DebugSystem.Write($"[AC226.Recv255] Dispatched Item Mall Matrix (238:183) & State (225:252) to {p.CharName}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC226.Recv255] Error: {ex.Message}");
            }
        }
    }
}
