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

                    if ((p.PendingBeachCutscene || (p.CurMap != null && p.CurMap.MapID == 10035 && p.Quests != null && !p.Quests.ContainsKey(12040))) && !p.BeachCutsceneActive)
                    {
                        DebugSystem.Write($"[AC12] Beach cutscene block ENTERED for {p.CharName}");
                        p.PendingBeachCutscene = false;
                        p.BeachCutsceneActive = true;
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
                                if (p.CurMap?.MapID != 10035) return;

                                p.Send(Tools.FromFormat("bb", 20, 8));
                                p.Send(Tools.FromFormat("bbbbbb", 22, 11, 6, 0, 0xFF, 0xFF)); // AC 22:11 camera pan
                                p.Send(Tools.FromFormat("bbb", 6, 2, 1));                      // AC 6:2 cinema lock
                                p.Send(Tools.FromFormat("bb", 20, 11));
                                p.Send(Tools.FromFormat("bb", 20, 10));
                                DebugSystem.Write($"[AC12] Timeline: Sent Camera Pan + Cinema Mode to {p.CharName}");

                                // Frame 2414: Robinson approaches & bends over player (1200ms delay)
                                await Task.Delay(1200);
                                if (p.CurMap?.MapID != 10035) return;

                                p.Send(Tools.FromFormat("bbbbbb", 22, 12, 2, 11, 0, 5));       // AC 22:12 Robinson approach
                                p.CurMap?.Broadcast(Tools.FromFormat("bbbbbb", 22, 12, 2, 11, 0, 5), "Ex", p.CharID);
                                p.Send(Tools.FromFormat("bb", 20, 10));
                                DebugSystem.Write($"[AC12] Timeline: Sent Robinson approach (AC 22:12) to {p.CharName}");

                                // Frame 2436: Trigger Robinson dialogue (1500ms delay)
                                await Task.Delay(1500);
                                if (p.CurMap is GameMap gMap && p.CurMap.MapID == 10035)
                                {
                                    p.BeachCutsceneActive = false;
                                    EveEventInterpreter.TryExecute(p, gMap, 1);
                                    DebugSystem.Write($"[AC12] Timeline: Triggered Robinson dialogue for {p.CharName}");
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
