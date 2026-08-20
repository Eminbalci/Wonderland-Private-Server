using System;
using Game;

namespace Network.ActionCodes
{
    public class AC183 : AC
    {
        public override int ID { get { return 183; } }

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            switch (p.B)
            {
                case 17:
                    Recv17(c, p);
                    break;
                default:
                    DebugSystem.Write($"[AC183] Subcode {p.B} received from {c.CharName}");
                    break;
            }
        }

        private void Recv17(Player c, RecievePacket p)
        {
            // S->C AC 183:17 [183, 17, 0]
            SendPacket s183 = new SendPacket();
            s183.Pack8(183);
            s183.Pack8(17);
            s183.Pack8(0);
            c.Send(s183);

            DebugSystem.Write($"[AC183.Recv17] Acknowledged AC 183:17 for {c.CharName}");
        }
    }
}
