using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wonderland_Private_Server.Code.Objects;
using Wonderland_Private_Server.Utilities;
using Network;
using Game;

namespace Network.ActionCodes
{
    public class AC08 : AC
    {
        public override int ID { get { return 8; } }

        public override void ProcessPkt(Player r, RecievePacket p)
        {
            switch (p.B)
            {
                case 1: Recv_1(r, p); break;
                default: Console.WriteLine(p.A + "," + p.B + " Has not been coded"); break;
            }
        }
        void Recv_1(Player r, RecievePacket p)
        {
            //int max = p.Unpack8(3);
            //int ptr = 4;
            //for (int a = 0; a < max; a++)
            //{
            //    r.AddStat(p.Unpack8(ptr), (byte)p.Unpack32(ptr + 1)); ptr += 5;
            //}
        }
    }
}
