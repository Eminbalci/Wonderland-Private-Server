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

                    if ((p.PendingBeachCutscene || (p.CurMap != null && p.CurMap.MapID == 10035 && p.Quests != null && !p.Quests.ContainsKey(12040))) && !p.BeachCutsceneActive)
                    {
                        p.PendingBeachCutscene = false;
                        p.BeachCutsceneActive = true;

                        // Frame 2404: Set player lying down on beach (Emote 9)
                        p.Emote = 9;
                        SendPacket emotePkt = new SendPacket();
                        emotePkt.PackArray(new byte[] { 32, 2 });
                        emotePkt.Pack32(p.CharID);
                        emotePkt.Pack8(9);
                        p.Send(emotePkt);
                        p.CurMap?.Broadcast(emotePkt, "Ex", p.CharID);

                        // Setup Beach Follow-up Step Queue (Official PCAP Frames 0337 to 0352)
                        p.StepQueue.Clear();
                        p.QueueData.Clear();

                        // Step 1 (PCAP Frame 0337): Robinson caring animation (AC 22 Sub 12) + AC 20:10
                        p.StepQueue.Enqueue(() =>
                        {
                            p.Send(Tools.FromFormat("bbbbbb", 22, 12, 2, 11, 0, 5));
                            p.Send(Tools.FromFormat("bb", 20, 10));
                        });

                        // Step 2 (PCAP Frame 0339): Robinson dialogue (TalkID 0x2EE8 = 12008)
                        p.StepQueue.Enqueue(() =>
                        {
                            SendPacket s2 = new SendPacket();
                            s2.Pack8(20);
                            s2.Pack8(1);
                            s2.PackArray(new byte[] { 0, 0, 0, 1, 5, 0, 0, 0, 1, 0xE8, 0x2E, 0, 0, 0, 0, 0 });
                            p.Send(s2);
                        });

                        // Step 3 (PCAP Frame 0344): AC 24 Sub 1 (Quest 12040 Start) + AC 20:10
                        p.StepQueue.Enqueue(() =>
                        {
                            p.Send(Tools.FromFormat("bbbbb", 24, 1, 0x08, 0x2F, 1));
                            p.Send(Tools.FromFormat("bb", 20, 10));
                        });

                        // Step 4 (PCAP Frame 0346): Fanfare progression step (AC 20:10)
                        p.StepQueue.Enqueue(() =>
                        {
                            p.Send(Tools.FromFormat("bb", 20, 10));
                        });

                        // Step 5 (PCAP Frame 0348): AC 24 Sub 5 (Quest Journal Entry) + AC 20:10
                        p.StepQueue.Enqueue(() =>
                        {
                            p.Send(Tools.FromFormat("bbbbb", 24, 5, 0x61, 0x00, 0x01));
                            p.Send(Tools.FromFormat("bb", 20, 10));
                        });

                        // Step 6 (PCAP Frame 0350): Robinson walking to standing position (AC 22 Sub 12) + AC 20:10
                        p.StepQueue.Enqueue(() =>
                        {
                            p.Send(Tools.FromFormat("bbbbbb", 22, 12, 1, 1, 0, 6));
                            p.Send(Tools.FromFormat("bb", 20, 10));
                        });

                        p.OnInteractionComplete = () =>
                        {
                            p.BeachCutsceneActive = false;
                            p.Emote = 0;
                            if (p.Quests == null) p.Quests = new Dictionary<uint, Game.QuestRelated.PlayerQuest>();
                            p.Quests[12040] = new Game.QuestRelated.PlayerQuest(12040, Game.QuestRelated.QuestState.InProgress, 1);
                            Game.QuestRelated.QuestManager.SavePlayerQuest(p, 12040);
                            p.Send(Tools.FromFormat("bbb", 6, 2, 0)); // Unlock movement (cancels AC 6:2, 1)
                            p.Send(Tools.FromFormat("bb", 20, 8)); // Unlock screen
                            p.Send(Tools.FromFormat("bb", 5, 4));  // Unlock player movement
                            DebugSystem.Write($"[AC12] Beach rescue cutscene completed, Quest 12040 registered and player unlocked for {p.CharName}");
                        };

                        // Initial trigger packets (PCAP Frame 0333): Screen clear, Camera focus on Robinson ClickID 6, Lock movement
                        p.Send(Tools.FromFormat("bb", 20, 8));
                        p.Send(Tools.FromFormat("bbwbb", 22, 11, (ushort)6, 0xFF, 0xFF)); // Camera focus on Robinson ClickID 6
                        p.Send(Tools.FromFormat("bbb", 6, 2, 1));                          // Movement lock
                        p.Send(Tools.FromFormat("bb", 20, 11));
                        p.Send(Tools.FromFormat("bb", 20, 10));

                        p.ContinueInteraction();
                        DebugSystem.Write($"[AC12] Started Robinson beach rescue cutscene for {p.CharName} on Map 10035");
                    }
                }
            }
            catch (Exception t) { DebugSystem.Write(new ExceptionData(t)); }
        }
    }
}
