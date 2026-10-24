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

                if (r.Buffer.Length != 6 || p.CurMap == null) return;
                if (p.Flags.HasFlag(PlayerFlag.Warping))
                {
                    p.Flags.Remove(PlayerFlag.Warping, PlayerFlag.Logging_into_Map);
                    if (p.CurMap?.Type != MapType.Tent && !(p.CurMap is Game.Code.Tent)) p.Flags.Remove(PlayerFlag.InTent);
                    p.Flags.Add(PlayerFlag.InMap);
                    if (p.CurMap is Game.Code.Tent tentMap)
                    {
                        byte tentType = tentMap.TentType;
                        if (tentType == 0) tentType = 1;
                        p.Send(Tools.FromFormat("bbbb", 65, 12, tentType, 0));
                        if (tentMap.Enlarged)
                        {
                            p.Send(Tools.FromFormat("bbbb", 65, 12, tentType, 1));
                        }
                        p.Send(Tools.FromFormat("bbbb", 65, 7, (byte)(tentMap.Locked ? 1 : 0), 0));
                        p.Send(Tools.FromFormat("bbw", 62, 14, tentMap.Floor1Color));
                        p.Send(Tools.FromFormat("bbw", 62, 15, tentMap.Floor1Wallpaper));
                        tentMap.EnsureDefaultItemsIfEmpty();
                        tentMap.SendTentItemsToPlayer(p);
                    }

                    // AC12:1 acknowledges native map loading. AC5:4 releases the
                    // client's scene-load gate only after that acknowledgement and tent item initialization.
                    p.Send(Tools.FromFormat("bb", 5, 4));
                    DebugSystem.Write($"[AC12] Map load completed for {p.CharName}: MapID={p.CurMap.MapID}, Warping=False");

                    // Restore the validated vehicle only after native map loading finishes.
                    Game.PlayerRelated.VehicleManager.SyncVehicleOnMapEntry(p);

                    DebugSystem.Write($"[AC12.Recv1] Map acknowledged -> MapID={p.CurMap?.MapID} PendingBeach={p.PendingBeachCutscene} BeachActive={p.BeachCutsceneActive} Quest12040={p.Quests?.ContainsKey(12040)}");

                    if ((p.PendingBeachCutscene || (p.CurMap != null && (p.CurMap.MapID == 10035 || p.CurMap.MapID == 10039) && p.Quests != null && !p.Quests.ContainsKey(12040))) && !p.BeachCutsceneActive)
                    {
                        DebugSystem.Write($"[AC12] Beach cutscene block ENTERED for {p.CharName}");
                        p.PendingBeachCutscene = false;
                        p.BeachCutsceneActive = true;
                        p.BeachCutsceneStep = 2;
                        p.ClearInteraction();

                        // Frame 2405: Player lies down on sand (Emote 9) + immobilize (AC 5:30)
                        p.Emote = 9;
                        p.Send(Tools.FromFormat("bbdb", 32, 2, p.CharID, (byte)9));
                        p.CurMap?.Broadcast(Tools.FromFormat("bbdb", 32, 2, p.CharID, (byte)9), "Ex", p.CharID);
                        p.Send(Tools.FromFormat("bbbdb", 5, 30, 1, p.CharID, (byte)0));
                        DebugSystem.Write($"[AC12] Sent Emote9 + AC5:30 immobilize to {p.CharName}");

                        // Start the rescue on the map acknowledgement: set up scene staging
                        p.Send(Tools.FromFormat("bb", 20, 8));
                        p.Send(Tools.FromFormat("bbbbbb", 22, 11, 6, 0, 0xFF, 0xFF));
                        p.Send(Tools.FromFormat("bbb", 6, 2, 1));
                        p.Send(Tools.FromFormat("bbbbbb", 22, 12, 1, 11, 0, 5)); // Actor 1 is Robinson
                        p.CurMap?.Broadcast(Tools.FromFormat("bbbbbb", 22, 12, 1, 11, 0, 5), "Ex", p.CharID);

                        // Trigger Beach Wakeup Animation (Cutscene ID 12008)
                        SendPacket animPkt = new SendPacket();
                        animPkt.PackArray(new byte[] { 20, 1, 0, 0, 0, 1, 5, 0 });
                        animPkt.Pack16(0);
                        animPkt.Pack8(1);
                        animPkt.Pack32(12008);
                        animPkt.Pack16(0);
                        animPkt.Pack8(1);
                        p.Send(animPkt);
                        DebugSystem.Write($"[AC12] Sent authentic Cutscene 12008 (Beach Wake Up) frame to {p.CharName}. Awaiting client AC 20:6.");
                    }
                    else
                    {
                        DebugSystem.Write($"[AC12] Beach cutscene block SKIPPED - Warping={p.Flags.HasFlag(PlayerFlag.Warping)} PendingBeach={p.PendingBeachCutscene} MapID={p.CurMap?.MapID} BeachActive={p.BeachCutsceneActive} Quest12040={p.Quests?.ContainsKey(12040)}");
                        // Run authored arrival scenes after loading, before region entries.
                        // Roca's father scene lives in PreEvents, not in a walk-in rectangle.
                        if (p.CurMap is GameMap storyMap && !EveEventInterpreter.TryExecuteStoryArrival(p, storyMap) &&
                            (storyMap.MapID == 12001 || storyMap.MapID == 12050 || storyMap.MapID == 12052 ||
                             storyMap.MapID == 12055 || storyMap.MapID == 60001 ||
                             storyMap.MapID == 11032 || storyMap.MapID == 60002))
                            EveEventInterpreter.TryExecuteRegion(p, storyMap);
                    }
                }
                else
                {
                    DebugSystem.Write($"[AC12.Recv1] Warping=false for {p.CharName}");
                    if (p.CurMap is Game.Code.Tent tentMap2)
                    {
                        byte tentType = tentMap2.TentType;
                        if (tentType == 0) tentType = 1;
                        p.Send(Tools.FromFormat("bbbb", 65, 12, tentType, 0));
                        if (tentMap2.Enlarged)
                        {
                            p.Send(Tools.FromFormat("bbbb", 65, 12, tentType, 1));
                        }
                        p.Send(Tools.FromFormat("bbbb", 65, 7, (byte)(tentMap2.Locked ? 1 : 0), 0));
                        p.Send(Tools.FromFormat("bbw", 62, 14, tentMap2.Floor1Color));
                        p.Send(Tools.FromFormat("bbw", 62, 15, tentMap2.Floor1Wallpaper));
                        tentMap2.EnsureDefaultItemsIfEmpty();
                        tentMap2.SendTentItemsToPlayer(p);
                        p.Send(Tools.FromFormat("bb", 5, 4));
                    }
                }
            }
            catch (Exception t)
            {
                DebugSystem.Write(new ExceptionData(t));
            }
        }
    }
}
