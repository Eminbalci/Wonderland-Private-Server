using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Network;
using Game;
using Game.DataFiles;
using Game.Maps;
using Wonderland_Private_Server.Utilities;

namespace Network.ActionCodes
{
    public class AC186 : AC
    {
        public override int ID { get { return 186; } }

        public override void ProcessPkt(Player r, RecievePacket p)
        {
            switch (p.B)
            {
                case 9: Recv9(r, p); break;
                default: Console.WriteLine($"AC {p.A},{p.B} has not been coded"); break;
            }
        }

        void Recv9(Player p, RecievePacket r)
        {
            try
            {
                // Official PCAP (ilkgorevinanimasyonlukisimlari.pcapng Frames 1920-1941)
                // Client sends ba 09 01 00 (Cutscene acknowledgment)
                SendPacket resp = new SendPacket();
                resp.PackArray(new byte[] { 186, 9, 1, 0, 1, 0, 0, 0, 0 }); // ba 09 01 00 01 00 00 00 00
                p.Send(resp);

                // Storm rumble / sound dialogue packet (AC 20 Sub 1 Step 3)
                SendPacket stormSound = new SendPacket();
                stormSound.PackArray(new byte[] { 20, 1, 0, 0, 0, 3, 5, 0, 0, 0, 2, 0x7B, 0, 0, 0, 0, 0, 0 });
                p.Send(stormSound);

                // Teleport player directly to Map 10035 (Wrecked Ship / Robinson Beach)
                p.PendingBeachCutscene = true;
                var warp = new WarpData() { DstMap = 10035, DstX_Axis = 1038, DstY_Axis = 2235 };
                p.CurMap?.Teleport(TeleportType.CmD, p, 0, warp);

                DebugSystem.Write($"[AC186.Recv9] Acknowledged storm cutscene and teleported {p.CharName} to shipwreck beach (Map 10035)");
            }
            catch (Exception t)
            {
                DebugSystem.Write(new ExceptionData(t));
            }
        }
    }
}
