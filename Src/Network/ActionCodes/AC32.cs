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
                byte emote = r.Unpack8();
                if (p.Emote != emote)
                {
                    p.Emote = emote;
                    SendPacket s = new SendPacket();
                    s.PackArray(new byte[] { 32, 1 });
                    s.Pack32(p.CharID);
                    s.Pack8(emote);
                    p.CurMap?.Broadcast(s, "Ex", p.CharID);
                }
            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv2(Player p, RecievePacket r)
        {
            try
            {
                byte actionCode = (r.Buffer != null && r.Buffer.Count() > 6) ? r[6] : (byte)0;

                if (p.Emote != actionCode)
                {
                    p.Emote = actionCode;
                    SendPacket s = new SendPacket();
                    s.PackArray(new byte[] { 32, 2 });
                    s.Pack32(p.CharID);
                    s.Pack8(actionCode);
                    p.CurMap?.Broadcast(s, "Ex", p.CharID);
                }
            }
            catch (Exception t) { Console.WriteLine(t); }
        }
    }
}