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
    public class AC65 : AC
    {
        public override int ID { get { return 65; } }
        public override void ProcessPkt(Player r, RecievePacket p)
        {
            switch (p.B)
            {
                case 1: Recv1(r, p); break; // Enter tent
                case 2: Recv2(r, p); break; // Item canceled
                case 3: Recv3(r, p); break; // Exit tent
                default: Console.WriteLine("AC " + p.A + "," + p.B + " has not been coded"); break;
            }
        }
        void Recv1(Player r, RecievePacket p)
        {
            ((GameMap)r.CurMap).onEnterTent(p.Unpack32(), r);
        }
        void Recv2(Player r, RecievePacket p)
        {
            r.Inv.onItemCanceled(p.Unpack8());
        }
        void Recv3(Player r, RecievePacket p)
        {
            // Exit tent - warp player back to main map
            if (r.Tent != null)
            {
                r.Tent.Close();
                DebugSystem.Write(DebugItemType.Error, "[AC65] Player exiting tent");
            }
        }
    }
}
