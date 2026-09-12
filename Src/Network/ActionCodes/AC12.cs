using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Network;
using Network.ActionCodes;
using Game;
using Game.Maps;
using Wonderland_Private_Server.Utilities;

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
                DebugSystem.Write($"[AC12.Recv1] Player={p.CharName} Flags={p.Flags} MapID={p.CurMap?.MapID} PendingBeach={p.PendingBeachCutscene} BeachActive={p.BeachCutsceneActive} HasQuest12040={p.Quests?.ContainsKey(12040)}");

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

                    DebugSystem.Write($"[AC12.Recv1] Warping=true -> MapID={p.CurMap?.MapID} PendingBeach={p.PendingBeachCutscene} BeachActive={p.BeachCutsceneActive} Quest12040={p.Quests?.ContainsKey(12040)}");

                    if ((p.PendingBeachCutscene || (p.CurMap != null && (p.CurMap.MapID == 10035 || p.CurMap.MapID == 10039) && p.Quests != null && !p.Quests.ContainsKey(12040))) && !p.BeachCutsceneActive)
                    {
                        DebugSystem.Write($"[AC12] Beach cutscene block ENTERED for {p.CharName}");
                        p.PendingBeachCutscene = false;
                        p.BeachCutsceneActive = true;
                        p.BeachCutsceneStep = 1;
                        p.ClearInteraction();

                        // Frame 2405: Player lies down on sand (Emote 9) + immobilize (AC 5:30)
                        p.Emote = 9;
                        p.Send(Tools.FromFormat("bbdb", 32, 2, p.CharID, (byte)9));
                        p.CurMap?.Broadcast(Tools.FromFormat("bbdb", 32, 2, p.CharID, (byte)9), "Ex", p.CharID);
                        p.Send(Tools.FromFormat("bbbdb", 5, 30, 1, p.CharID, (byte)0));
                        DebugSystem.Write($"[AC12] Sent Emote9 + AC5:30 immobilize to {p.CharName}");

                        Task.Run(async () =>
                        {
                            try
                            {
                                // Frame 2408: Camera Pan & Cinema Mode (300ms delay)
                                await Task.Delay(300);
                                if (p.CurMap?.MapID != 10035 && p.CurMap?.MapID != 10039) return;

                                p.Send(Tools.FromFormat("bb", 20, 8));
                                p.Send(Tools.FromFormat("bbbbbb", 22, 11, 6, 0, 0xFF, 0xFF)); // AC 22:11 camera pan
                                p.Send(Tools.FromFormat("bbb", 6, 2, 1));                      // AC 6:2 cinema lock
                                p.Send(Tools.FromFormat("bb", 20, 11));
                                p.Send(Tools.FromFormat("bb", 20, 10));
                                DebugSystem.Write($"[AC12] Timeline: Sent Camera Pan + Cinema Mode to {p.CharName}");

                                // Frame 2414: Robinson approaches & bends over player (1000ms delay)
                                await Task.Delay(1000);
                                if (p.CurMap?.MapID != 10035 && p.CurMap?.MapID != 10039) return;

                                p.Send(Tools.FromFormat("bbbbbb", 22, 12, 2, 11, 0, 5));       // AC 22:12 Robinson approach
                                p.CurMap?.Broadcast(Tools.FromFormat("bbbbbb", 22, 12, 2, 11, 0, 5), "Ex", p.CharID);
                                p.Send(Tools.FromFormat("bb", 20, 10));
                                DebugSystem.Write($"[AC12] Timeline: Sent Robinson approach (AC 22:12) to {p.CharName}");

                                // Frame 2436: Trigger Beach Wakeup Animation (Cutscene ID 12008) (800ms delay)
                                await Task.Delay(800);
                                if (p.CurMap?.MapID != 10035 && p.CurMap?.MapID != 10039) return;

                                SendPacket animPkt = new SendPacket();
                                animPkt.Pack8(20);
                                animPkt.Pack8(1);
                                animPkt.Pack8(0); animPkt.Pack8(0); animPkt.Pack8(0);
                                animPkt.Pack8(1); // Step 1
                                animPkt.Pack8(5); // Type 5: Cutscene Animation
                                animPkt.Pack8(0); animPkt.Pack8(0); animPkt.Pack8(0); // Speaker 0
                                animPkt.Pack8(1); // Flag 1
                                animPkt.Pack32(12008); // Cutscene ID 12008 (Beach Wake Up)
                                animPkt.Pack8(0); animPkt.Pack8(0); animPkt.Pack8(0);
                                p.Send(animPkt);
                                p.BeachCutsceneStep = 2; // Waiting for client to finish animation (sends AC 20:6)
                                DebugSystem.Write($"[AC12] Timeline: Sent Cutscene 12008 (Beach Arrival Animation) to {p.CharName}");

                                // Safety timeout: if client does not acknowledge completion after 25s, force-complete cutscene
                                await Task.Delay(25000);
                                if (p.BeachCutsceneActive && (p.CurMap?.MapID == 10035 || p.CurMap?.MapID == 10039))
                                {
                                    DebugSystem.Write($"[AC12] Safety timeout reached for beach cutscene on {p.CharName} — force completing.");
                                    AC20.AdvanceBeachCutscene(p, forceComplete: true);
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
                        DebugSystem.Write($"[AC12] Beach cutscene block SKIPPED - Warping={p.Flags.HasFlag(PlayerFlag.Warping)} PendingBeach={p.PendingBeachCutscene} MapID={p.CurMap?.MapID} BeachActive={p.BeachCutsceneActive} Quest12040={p.Quests?.ContainsKey(12040)}");
                    }
                }
                else
                {
                    DebugSystem.Write($"[AC12.Recv1] Warping=false for {p.CharName}");
                }
            }
            catch (Exception t)
            {
                DebugSystem.Write(new ExceptionData(t));
            }
        }
    }
}
