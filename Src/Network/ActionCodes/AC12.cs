using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Network;
using Network.ActionCodes;
using Game;

namespace Wonderland_Private_Server.ActionCodes
{
    public class AC12 : AC
    {
        public override int ID { get { return 12; } }

        public override void ProcessPkt(Player r, RecievePacket p)
        {

            switch (p.B)
            {
                case 1: Recv1(r, p); break;
            }
        }
        void Recv1(Player p, RecievePacket r)
        {
            try
            {
                if (p.Flags.HasFlag(PlayerFlag.Warping))
                {
                    p.Flags.Add(PlayerFlag.InMap);

                    // Resend party info upon warp completion
                    if (p.m_teammembers != null && p.m_teammembers.Count > 0)
                    {
                        var leader = p.m_teammembers.FirstOrDefault(x => x.PartyLeader);
                        if (leader != null)
                        {
                            DebugSystem.Write($"[AC12] Resending party info to {p.CharName} after warp");
                            p.Send(leader._13_6Data);
                        }
                    }

                    // Resend active vehicle / sailing state on new map
                    if (p.ActiveVehicleID > 0)
                    {
                        SendPacket vehiclePkt = new SendPacket();
                        vehiclePkt.PackArray(new byte[] { 15, 10, 0x15 });
                        vehiclePkt.Pack32(p.CharID);
                        vehiclePkt.Pack16((ushort)p.ActiveVehicleID);
                        vehiclePkt.Pack16(0);
                        p.Send(vehiclePkt);
                        p.CurMap?.Broadcast(vehiclePkt, "Ex", p.CharID);
                    }

                    if (p.PendingBeachCutscene)
                    {
                        p.PendingBeachCutscene = false;

                        // Frame 0331: Set player lying down on beach
                        p.Emote = 9;
                        SendPacket emotePkt = new SendPacket();
                        emotePkt.PackArray(new byte[] { 32, 2 });
                        emotePkt.Pack32(p.CharID);
                        emotePkt.Pack8(9);
                        p.Send(emotePkt);
                        p.CurMap?.Broadcast(emotePkt, "Ex", p.CharID);

                        // Setup Beach Follow-up Queue (PCAP Frames 0343 - 0352)
                        p.QueueData.Clear();

                        // Step 1 response (PCAP Frame 0344): AC 24 Sub 1 (Quest 97 start)
                        SendPacket s1 = new SendPacket();
                        s1.PackArray(new byte[] { 24, 1, 8, 0x2F, 1 });
                        s1.PackArray(new byte[] { 20, 10 }); // 14 0a
                        p.QueueData.Enqueue(s1);

                        // Step 2 response (PCAP Frame 0348): AC 24 Sub 5 (Quest 97 completed: 18 05 61 00 01)
                        SendPacket s2 = new SendPacket();
                        s2.PackArray(new byte[] { 24, 5, 0x61, 0, 1 });
                        s2.PackArray(new byte[] { 20, 10 });
                        p.QueueData.Enqueue(s2);

                        // Step 3 response (PCAP Frame 0350): Player Standing Up Animation (AC 22 Sub 12: 16 0c 01 01 00 06)
                        SendPacket s3 = new SendPacket();
                        s3.PackArray(new byte[] { 22, 12, 1, 1, 0, 6 });
                        s3.PackArray(new byte[] { 20, 10 });
                        p.QueueData.Enqueue(s3);

                        p.OnInteractionComplete = () =>
                        {
                            p.Send(Tools.FromFormat("bb", 20, 8)); // 14 08
                            p.Send(Tools.FromFormat("bb", 5, 4));  // 05 04
                        };

                        // Frame 0333-0339: Trigger beach focus, Robinson dragging animation, and wake-up dialogue immediately
                        SendPacket initialBeachCutscene = new SendPacket();
                        initialBeachCutscene.PackArray(new byte[] { 20, 8 }); // 14 08
                        initialBeachCutscene.PackArray(new byte[] { 22, 11, 6, 0, 0xFF, 0xFF }); // 16 0b 06 00 ff ff (Focus)
                        initialBeachCutscene.PackArray(new byte[] { 6, 2, 1 }); // 06 02 01 (Lock)
                        initialBeachCutscene.PackArray(new byte[] { 20, 11 }); // 14 0b
                        initialBeachCutscene.PackArray(new byte[] { 20, 10 }); // 14 0a
                        initialBeachCutscene.PackArray(new byte[] { 22, 12, 2, 11, 0, 5 }); // 16 0c 02 0b 00 05 (Robinson dragging animation!)
                        initialBeachCutscene.PackArray(new byte[] { 20, 1, 0, 0, 0, 1, 5, 0, 0, 0, 1, 0xE8, 0x2E, 0, 0, 0, 0, 0 }); // 14 01 ... (Wake up dialogue!)
                        p.Send(initialBeachCutscene);
                    }
                }
            }
            catch (Exception t) { DebugSystem.Write(new ExceptionData(t)); }
        }
    }
}
