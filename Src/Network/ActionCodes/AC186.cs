using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Network;
using Game;
using Wonderland_Private_Server.Utilities;

namespace Network.ActionCodes
{
    public class AC186 : AC
    {
        public override int ID { get { return 186; } }

        public override void ProcessPkt(Player r, RecievePacket p)
        {
            switch (p.B)
            {
                case 9: Recv9(r, p); break;
                default: Console.WriteLine($"AC {p.A},{p.B} has not been coded"); break;
            }
        }

        void Recv9(Player p, RecievePacket r)
        {
            try
            {
                // PCAP Frame 0322 -> 0325: Client sends ba 09 01 00 (Cutscene acknowledgment)
                // Server responds with Frame 0323-0326 packets to finalize storm animation
                SendPacket resp = new SendPacket();
                resp.PackArray(new byte[] { 186, 9, 1, 0, 1, 0, 0, 0, 0 }); // ba 09 01 00 01 00 00 00 00
                p.Send(resp);

                DebugSystem.Write($"[AC186.Recv9] Acknowledged storm cutscene for {p.CharName}");
            }
            catch (Exception t)
            {
                DebugSystem.Write(new ExceptionData(t));
            }
        }
    }
}
