using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wonderland_Private_Server.Code.Objects;
using Network;
using Game;

namespace Network.ActionCodes
{
    public class AC82 : AC
    {
        public override int ID { get { return 82; } }
        public override void ProcessPkt(Player p, RecievePacket r)
        {
            switch (r.B)
            {
                case 7: Recv7(p, r); break;// ADD MESSAGE
                case 8: Recv8(p, r); break;// TAB REQUEST GUILD  
                case 9: Recv9(p, r); break;// OPen message
                case 11: Recv11(p, r); break;// write MESSAGE
                default: Console.WriteLine("AC " + r.A + "," + r.B + " has not been coded"); break;
            }
        }
        void Recv7(Player p, RecievePacket r)
        {
            try
            {
                //p.CurGuild.AddMessage(p, r);
            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv8(Player p, RecievePacket r)
        {
            try
            {
                //p.CurGuild.OpenTab(p);
            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv9(Player p, RecievePacket r)
        {
            try
            {
                //p.CurGuild.OpenMessage(p,r);
            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv11(Player p, RecievePacket r)
        {
            try
            {
                //p.CurGuild.OpenPainelWriteMessage(p);
            }
            catch (Exception t) { Console.WriteLine(t); }
        }
    }
}