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
            if (p.Buffer.Count() - p.GetPtr() < 7) return;
            byte targetType = p.Unpack8();
            byte targetSlot = p.Unpack8();
            byte statId = p.Unpack8();
            uint amount = p.Unpack32();

            if (targetType == 0) // Player
            {
                if (r.SkillPoints >= amount && amount > 0)
                {
                    r.SkillPoints -= (ushort)amount;
                    switch (statId)
                    {
                        case 28: r.baseStr += (ushort)amount; break;
                        case 29: r.baseCon += (ushort)amount; break;
                        case 27: r.baseInt += (ushort)amount; break;
                        case 33: r.baseWis += (ushort)amount; break;
                        case 30: r.baseAgi += (ushort)amount; break;
                    }
                    r.Send8_1(true);
                }
            }
        }
    }
}
