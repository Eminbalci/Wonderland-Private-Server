using System;
using Game;

namespace Network.ActionCodes
{
    public class AC92 : AC
    {
        public override int ID { get { return 92; } }

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            // AC 92 Sub 1 ACK from itemmall.pcapng (packet #2889)
            DebugSystem.Write($"[AC92] Received AC 92:{p.B} from {c.CharName}");
        }
    }
}
