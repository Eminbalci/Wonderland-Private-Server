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

                // 1. Server responds with CG playback acknowledgment:
                // [AC=186 (1B)][Sub=9 (1B)][cutsceneId (2B)][status=1 (1B)][reserved (4B)]
                SendPacket resp = new SendPacket();
                resp.Pack8(186);
                resp.Pack8(9);
                resp.Pack16(cutsceneId);
                resp.Pack8(1); // 1 = Active / Playing
                resp.Pack32(0); // Reserved padding
                p.Send(resp);

                // 2. For Cutscene 1 (Prologue Shipwreck on Starter Ship), allow animation to play fully then transition to Beach
                if (cutsceneId == 1 && p.CurMap != null && (p.CurMap.MapID == 10017 || (p.CurMap.MapID >= 10024 && p.CurMap.MapID <= 10028)))
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            // Authentic CG movie playback duration (~9.5 seconds)
                            await Task.Delay(9500);

                            if (p.CurMap != null && (p.CurMap.MapID == 10017 || (p.CurMap.MapID >= 10024 && p.CurMap.MapID <= 10028)))
                            {
                                p.PendingBeachCutscene = true;
                                var warp = new WarpData() { DstMap = 10035, DstX_Axis = 1038, DstY_Axis = 2235 };
                                p.CurMap?.Teleport(TeleportType.CmD, p, 0, warp);
                                DebugSystem.Write($"[AC186.Recv9] Prologue Storm Cutscene #1 completed -> Teleported {p.CharName} to shipwreck beach (Map 10035)");
                            }
                        }
                        catch (Exception ex)
                        {
                            DebugSystem.Write(new ExceptionData(ex));
                        }
                    });
                }
                else
                {
                    DebugSystem.Write($"[AC186.Recv9] Synced CG Cutscene #{cutsceneId} playback for {p.CharName}");
                }
            }
            catch (Exception t)
            {
                DebugSystem.Write(new ExceptionData(t));
            }
        }
    }
}
