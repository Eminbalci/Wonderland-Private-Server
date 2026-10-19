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

                if (p.PlayingStormCutscene && cutsceneId == 1)
                {
                    p.StormCutsceneAckReceived = true;
                    DebugSystem.Write($"[AC186.Recv9] Storm cutscene #1 handshake confirmed for {p.CharName}. Awaiting client completion AC 20:6.");

                    // Failsafe watchdog (25s) in case client fails to emit AC 20:6 after 123.sty completes
                    Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(25000);
                            if (p.PlayingStormCutscene && p.CurMap != null && (p.CurMap.MapID >= 10014 && p.CurMap.MapID <= 10018))
                            {
                                p.PlayingStormCutscene = false;
                                p.StormCutsceneAckReceived = false;
                                p.PendingBeachCutscene = true;
                                p.ClearInteraction();
                                p.Send(Tools.FromFormat("bb", 20, 8));
                                p.Send(Tools.FromFormat("bb", 20, 7));
                                var warp = new WarpData() { DstMap = 10035, DstX_Axis = 1038, DstY_Axis = 2235 };
                                p.CurMap?.Teleport(TeleportType.CmD, p, 0, warp);
                                DebugSystem.Write($"[AC186.Recv9] Storm cutscene watchdog (25s) triggered -> Warped {p.CharName} to Beach (Map 10035)");
                            }
                        }
                        catch (Exception ex)
                        {
                            DebugSystem.Write(DebugItemType.Error, $"[AC186.Recv9] Error in storm cutscene watchdog: {ex.Message}");
                        }
                    });
                }
                else
                {
                    DebugSystem.Write($"[AC186.Recv9] Acknowledged Cutscene #{cutsceneId} playback for {p.CharName}");
                }
            }
            catch (Exception t)
            {
                DebugSystem.Write(new ExceptionData(t));
            }
        }
    }
}
