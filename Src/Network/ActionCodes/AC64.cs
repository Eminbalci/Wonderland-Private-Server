using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wonderland_Private_Server.Code.Objects;
using wlo.pserver.core.Game.Enums;
using wlo.pserver.core;
using Network;
using Game;

namespace Network.ActionCodes
{
    public class AC64 : AC
    {
        public override int ID { get { return 64; } }
        public override void ProcessPkt(Player r, RecievePacket p)
        {
            switch (p.B)
            {
                case 1: Recv1(r, p); break;// create new objet tent
                case 2: Recv2(r, p); break;
                case 3: Recv3(r, p); break; // stop build
                default: Console.WriteLine("AC " + p.A + "," + p.B + " has not been coded"); break;
            }
        }
        void Recv1(Player r, RecievePacket p)
        {
            try
            {
                //r.Tent.Floors[1].Create_NewObject_Tent(r, p);  // TODO: Tent.Floors not implemented
            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv2(Player r, RecievePacket p)
        {
            try
            {
                //r.Tent.Floors[1].ContinueBuild(r, p.Unpack8());  // TODO: Tent.Floors not implemented
            }
            catch (Exception t) { Console.WriteLine(t); }
        }
        void Recv3(Player r, RecievePacket p)
        {
            try
            {
                //r.Tent.Floors[1].StopBuild(r, p.Unpack8());  // TODO: Tent.Floors not implemented
            }
            catch (Exception t) { Console.WriteLine(t); }
        }
    }
}
