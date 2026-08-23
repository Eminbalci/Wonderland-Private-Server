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

                        // Setup Beach Authentic Dialogue Sequence (Eve.emg Map 10035 Event 8: TalkIDs 20304..20312)
                        p.StepQueue.Clear();
                        p.QueueData.Clear();

                        var beachSteps = new List<Tuple<uint, byte, byte>>
                        {
                            Tuple.Create((uint)20304, (byte)7, (byte)0), // Player: Cough cough... Huh? Where am I?
                            Tuple.Create((uint)20305, (byte)3, (byte)6), // Robinson: This is a deserted island. How did you end up here?
                            Tuple.Create((uint)20306, (byte)7, (byte)0), // Player: I was on the ship then suddenly it started violently shaking...
                            Tuple.Create((uint)20307, (byte)3, (byte)6), // Robinson: I think you must have been shipwrecked and drifted to this island...
                            Tuple.Create((uint)20308, (byte)3, (byte)6), // Robinson: It has been 28 years since I drifted to this island.
                            Tuple.Create((uint)20309, (byte)7, (byte)0), // Player: Oh, dear! 28 years ago? Didn't you look for ways to go back?
                            Tuple.Create((uint)20310, (byte)3, (byte)6), // Robinson: Yes, I did. But I've only been to the islands nearby...
                            Tuple.Create((uint)20311, (byte)7, (byte)0), // Player: My name is Player.
                            Tuple.Create((uint)20312, (byte)3, (byte)6), // Robinson: Player, you look fine. Walk around if you are free...
                        };

                        for (int i = 0; i < beachSteps.Count; i++)
                        {
                            byte stepNum = (byte)(i + 1);
                            var info = beachSteps[i];
                            SendPacket stepPkt = BuildDialoguePacket(info.Item3, info.Item1, stepNum, info.Item2);
                            if (i == 0)
                            {
                                p.Send(stepPkt);
                            }
                            else
                            {
                                p.QueueData.Enqueue(stepPkt);
                            }
                        }

                        p.OnInteractionComplete = () =>
                        {
                            p.BeachCutsceneActive = false;
                            p.Emote = 0;
                            if (p.Quests == null) p.Quests = new Dictionary<uint, Game.QuestRelated.PlayerQuest>();
                            p.Quests[12040] = new Game.QuestRelated.PlayerQuest(12040, Game.QuestRelated.QuestState.InProgress, 1);
                            Game.QuestRelated.QuestManager.SavePlayerQuest(p, 12040);
                            Game.QuestRelated.QuestManager.SendQuestUpdate(p, 12040, Game.QuestRelated.QuestState.InProgress, 1);
                            p.Send(Tools.FromFormat("bbb", 6, 2, 0)); // Unlock movement (cancels AC 6:2, 1)
                            p.Send(Tools.FromFormat("bb", 20, 8)); // Unlock screen
                            p.Send(Tools.FromFormat("bb", 5, 4));  // Unlock player movement
                            DebugSystem.Write($"[AC12] Beach rescue 9-step dialogue completed, Quest 12040 registered and player unlocked for {p.CharName}");
                        };

                        // Initial trigger packets: Screen clear, Camera focus on Robinson ClickID 6, Lock movement
                        p.Send(Tools.FromFormat("bb", 20, 8));
                        p.Send(Tools.FromFormat("bbwbb", 22, 11, (ushort)6, 0xFF, 0xFF)); // Camera focus on Robinson ClickID 6
                        p.Send(Tools.FromFormat("bbb", 6, 2, 1));                          // Movement lock
                        p.Send(Tools.FromFormat("bbbbbb", 22, 12, 2, 11, 0, 5));           // Robinson caring animation

                        DebugSystem.Write($"[AC12] Started Robinson beach rescue authentic dialogue chain for {p.CharName} on Map 10035");
                    }
                }
            }
            catch (Exception t)
            {
                DebugSystem.Write(new ExceptionData(t));
            }
        }

        private static SendPacket BuildDialoguePacket(byte speakerClickId, uint talkId, byte stepNum, byte portrait = 3)
        {
            SendPacket dPkt = new SendPacket();
            dPkt.Pack8(20);                                   // AC
            dPkt.Pack8(1);                                    // SubCode
            dPkt.Pack8(0); dPkt.Pack8(0); dPkt.Pack8(0);     // session padding
            dPkt.Pack8(stepNum);                             // step
            dPkt.Pack8(1);                                    // fixed
            dPkt.Pack8(portrait);                             // portrait (3=NPC, 7=Player)
            dPkt.Pack8(speakerClickId);                       // npc click id
            dPkt.Pack8(0);                                    // padding
            dPkt.Pack8(1); dPkt.Pack8(0); dPkt.Pack8(0); dPkt.Pack8(0); // flags
            dPkt.Pack8(0);                                    // padding
            dPkt.Pack8((byte)(talkId & 0xFF));                // TalkID LSB
            dPkt.Pack8((byte)((talkId >> 8) & 0xFF));         // TalkID MID
            dPkt.Pack8((byte)((talkId >> 16) & 0xFF));        // TalkID MSB
            return dPkt;
        }
    }
}
