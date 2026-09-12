using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using wlo.pserver.core.Game.Enums;
using Network;
using Game;
using Game.Code;
using wlo.pserver.core;

namespace Network.ActionCodes
{
    public class AC85 : AC
    {
        public override int ID { get { return 85; } }
        public override void ProcessPkt(Player r, RecievePacket p)
        {
            switch (p.B)
            {
                case 1: Recv1(r, p); break; // Update list instances.
                case 2: Recv2(r, p); break; // Tab list instances.
                case 3: Recv3(r, p); break; // create instance
                case 4: Recv4(r, p); break; // pre join
                case 5: Recv5(r, p); break; // join
                case 6: Recv6(r, p); break; // exit instance.
                case 10: Recv10(r, p); break;//chek members
                case 11: Recv11(r, p); break;//chek membersTabs
                case 13: Recv13(r, p); break;//demiss member
                default: Console.WriteLine("AC " + p.A + "," + p.B + " has not been coded"); break;
            }
        }
        void Recv1(Player p, RecievePacket r)
        {
            try
            {
                //cGlobal.gInstanceSystem.Send81_1(ref p, 1);  // TODO: gInstanceSystem not implemented

            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv2(Player p, RecievePacket r)
        {
            int tmp = r.Unpack8();
            try
            {
                switch (tmp)
                {

                    case 1:
                        //cGlobal.gInstanceSystem.Send81_1(ref p, tmp);  // TODO: gInstanceSystem not implemented
                        break;
                    case 2:
                        //cGlobal.gInstanceSystem.Send81_1(ref p, tmp);  // TODO: gInstanceSystem not implemented
                        break;
                }

            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv3(Player p, RecievePacket r)
        {
        }
        void Recv4(Player p, RecievePacket r)
        {
            int tmp = r.Unpack16(); // get id instance selected
            try
            {
                //cGlobal.gInstanceSystem.PreJoin(p.UserID, tmp);  // TODO: gInstanceSystem not implemented

            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv5(Player p, RecievePacket r)
        {
            int tmp = r.Unpack16(); // get id instance selected
            try
            {
                //cGlobal.gInstanceSystem.Join(ref p, tmp);  // TODO: gInstanceSystem not implemented

            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv6(Player p, RecievePacket r)
        {

            try
            {
                //cGlobal.gInstanceSystem.ExitInstancia(ref p);  // TODO: gInstanceSystem not implemented
            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv10(Player p, RecievePacket r)
        {
            try
            {
                //if (p.CurInstance != 0)  // TODO: gInstanceSystem not implemented
                //cGlobal.gInstanceSystem.CheckMembers(ref p, 1);  // TODO: gInstanceSystem not implemented

            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv11(Player p, RecievePacket r)
        {
            byte tmp = r.Unpack8();
            try
            {
                // move tab members
                if (p.CurInstance != 0)
                {
                    if ((tmp >= 1) && (tmp < 4))
                    {
                        //cGlobal.gInstanceSystem.CheckMembers(ref p, tmp);  // TODO: gInstanceSystem not implemented

                    }
                }
            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv13(Player p, RecievePacket r)
        {
            try
            {

            }
            catch (Exception t) { Console.WriteLine(t); }
        }
    }
}