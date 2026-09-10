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
                case 9:
                    Recv9(r, p);
                    break;
                default:
                    DebugSystem.Write($"[AC186] Subcode {p.B} received from {r.CharName}");
                    break;
            }
        }

        void Recv9(Player p, RecievePacket r)
        {
            try
            {
                // ActionCode 186 Subcode 9: Cutscene / CG Animation Acknowledgment
                // Client packet: Header (5B: F4 44 LenLo LenHi AC=186 Sub=9) + Payload [cutsceneId (2B)]
                ushort cutsceneId = 1;
                if (r.Buffer != null && r.Buffer.Length >= 8)
                {
                    cutsceneId = (ushort)(r[6] | (r[7] << 8));
                }

                // 1. Server responds with CG playback acknowledgment (Official PCAP Frame 1942):
                // [AC=186 (1B)][Sub=9 (1B)][cutsceneId (2B)][status=1 (1B)][reserved (4B)]
                SendPacket resp = new SendPacket();
                resp.Pack8(186);
                resp.Pack8(9);
                resp.Pack16(cutsceneId);
                resp.Pack8(1); // 1 = Active / Playing
                resp.Pack32(0); // Reserved padding
                p.Send(resp);

                // 2. Server completes cutscene animation sync & camera release (Official PCAP Frame 469, 470, 492):
                // [AC=186 (1B)][Sub=12 (1B)][cutsceneId (2B)][status/padding (3B)]
                SendPacket resp12 = new SendPacket();
                resp12.Pack8(186);
                resp12.Pack8(12);
                resp12.Pack16(cutsceneId);
                resp12.Pack8(0);
                resp12.Pack16(0);
                p.Send(resp12);

                DebugSystem.Write($"[AC186.Recv9] Acknowledged Cutscene #{cutsceneId} playback and sent camera release (186:12) for {p.CharName}");
            }
            catch (Exception t)
            {
                DebugSystem.Write(new ExceptionData(t));
            }
        }
    }
}
