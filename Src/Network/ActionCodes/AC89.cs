using System;
using Game;

namespace Network.ActionCodes
{
    public class AC89 : AC
    {
        public override int ID { get { return 89; } }

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            switch (p.B)
            {
                case 0:
                    Recv0(c, p);
                    break;
                default:
                    DebugSystem.Write($"[AC89] Subcode {p.B} received from {c.CharName}");
                    break;
            }
        }

        private void Recv0(Player c, RecievePacket p)
        {
            // S->C AC 90:1 [90, 1, 0, 1, 1, 3, 2, 3] from itemmall.pcapng (packet #2888)
            SendPacket s = new SendPacket();
            s.Pack8(90);
            s.Pack8(1);
            s.PackArray(new byte[] { 0, 1, 1, 3, 2, 3 });
            c.Send(s);
            DebugSystem.Write($"[AC89.Recv0] Sent AC 90:1 to {c.CharName}");
        }
    }
}
