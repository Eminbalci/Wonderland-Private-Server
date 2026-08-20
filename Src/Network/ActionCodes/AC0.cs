using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Network;
using Game;

namespace Network.ActionCodes
{
    public class AC0:AC
    {
        public override int ID
        {
            get
            {
                return 0;
            }
        }

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            SendPacket s = new SendPacket();
            s.Pack8(1);
            s.Pack8(9);
            s.PackArray(new byte[] { 101, 0, 1 });
            s.PackStringN(cGlobal.SrvVersion);
            c.Send(s);

            // Authentic WLO Mall Category Catalog Matrix (AC 54 Sub 201) from itemmall.pcapng
            s = new SendPacket();
            s.Pack8(54);
            s.Pack8(201);
            s.PackArray(new byte[] { 0, 1, 101, 0, 3, 103, 0, 2, 104, 0, 3, 102, 0, 3 });
            c.Send(s);
        }
    }
}
