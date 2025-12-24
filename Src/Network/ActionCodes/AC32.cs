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
    public class AC32 : AC
    {
        public override int ID { get { return 32; } }
        public override void ProcessPkt(Player r, RecievePacket p)
        {
            switch (p.B)
            {
                case 1: Recv1(r, p); break;
                case 2: Recv2(r, p); break;
                default: Console.WriteLine("AC " + p.A + "," + p.B + " has not been coded"); break;
            }
        }
        void Recv1(Player p, RecievePacket r)
        {
            try
            {
                p.Emote = r.Unpack8();
                SendPacket s = new SendPacket();
                s.PackArray(new byte[] { 32, 1 });
                s.Pack32(p.UserID);
                s.Pack8(r.Unpack8());
                p.CurMap.Broadcast(s, "Ex", p.ID);
            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv2(Player p, RecievePacket r)
        {
            try
            {
                p.Emote = r.Unpack8();
                SendPacket s = new SendPacket();
                s.PackArray(new byte[] { 32, 2 });
                s.Pack32(p.UserID);
                s.Pack8(r.Unpack8());
                p.CurMap.Broadcast(s, "Ex", p.ID);
            }
            catch (Exception t) { Console.WriteLine(t); }
        }
    }
}