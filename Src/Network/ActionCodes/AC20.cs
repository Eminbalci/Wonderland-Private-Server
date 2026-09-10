using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Network;
using Network.ActionCodes;
using Game;
using Game.Maps;

namespace Wonderland_Private_Server.ActionCodes
{
    public class AC20 : AC
    {
        public override int ID { get { return 20; } }
        public override void ProcessPkt(Player r, RecievePacket p)
        {
            switch (p.Unpack8())
            {
                case 1: Recv1(r, p); break;
                case 6: Recv6(r, p); break;
                case 9: Recv9(r, p); break;
                case 8: Recv8(r, p); break;
            }
        }
        void Recv8(Player p, RecievePacket r)
        {
            if (p == null || p.CurMap == null) return;

            // Portal debounce and spawn proximity guard
            double elapsedMs = (DateTime.UtcNow - p.LastTeleportTime).TotalMilliseconds;
            if (elapsedMs < 2500)
            {
                DebugSystem.Write($"[AC20.Recv8] Portal cooldown active ({elapsedMs:F0}ms / 2500ms) for {p.CharName}. Ignoring request.");
                p.Send(Tools.FromFormat("bb", 20, 8));
                return;
            }

            // If player just entered map and is within 120 pixels of their spawn location, ignore re-triggering return portal
            if (elapsedMs < 4000 && p.LastSpawnX > 0 && p.LastSpawnY > 0)
            {
                double spawnDist = Math.Sqrt(Math.Pow((int)p.CurX - (int)p.LastSpawnX, 2) + Math.Pow((int)p.CurY - (int)p.LastSpawnY, 2));
                if (spawnDist < 120)
                {
                    DebugSystem.Write($"[AC20.Recv8] Spawn proximity guard active for {p.CharName} (dist={spawnDist:F1}px from spawn {p.LastSpawnX},{p.LastSpawnY}). Ignoring immediate return portal.");
                    p.Send(Tools.FromFormat("bb", 20, 8));
                    return;
                }
            }

            ushort portalID = r.Unpack16();
            DebugSystem.Write($"[AC20.Recv8] Player {p.CharName} stepped on portal {portalID} on Map {p.CurMap?.MapID} at pos({p.CurX},{p.CurY})");

            if (p.CurMap != null)
            {
                // If sailing on Map 10036 (or stepping on ocean portal 11), warp through ocean exit to World Ocean Map 11016
                if (p.CurMap.MapID == 10036 && (p.ActiveVehicleID > 0 || portalID == 11))
                {
                    if (p.ActiveVehicleID == 0) p.ActiveVehicleID = 48016;
                    var oceanWarp = new Game.Maps.WarpData() { DstMap = 11016, DstX_Axis = 134, DstY_Axis = 1126 };
                    p.CurMap.Teleport(TeleportType.CmD, p, 0, oceanWarp);
                    DebugSystem.Write($"[AC20.Recv8] Player {p.CharName} sailed through ocean portal from 10036 to 11016 at (134, 1126)");
                    return;
                }

                // If sailing on Map 11016 (Open Ocean) and stepping into a land portal
                if (p.CurMap.MapID == 11016 && p.ActiveVehicleID > 0)
                {
                    Game.PlayerRelated.VehicleManager.WreckVehicle(p);
                }

                if (p.CurMap.Teleport(TeleportType.Regular, p, portalID))
                {
                    p.SaveCharacterData();
                    return;
                }
            }

            p.Send(Tools.FromFormat("bb", 20, 8));
        }
        void Recv1(Player p, RecievePacket r)
        {
            if (p.CurMap == null)
            {
                p.Send(Tools.FromFormat("bb", 20, 8));
                return;
            }

            // NPC click - unpack 16-bit or 8-bit click ID
            ushort clickID = 0;
            int rem = r.Buffer.Count() - r.GetPtr();
            if (rem >= 4)
            {
                r.Unpack8(); r.Unpack8();
                clickID = r.Unpack16();
            }
            else if (rem >= 2)
            {
                clickID = r.Unpack16();
            }
            else if (rem == 1)
            {
                clickID = r.Unpack8();
            }

            var gMap = p.CurMap as GameMap;
            var clickedNpcObj = gMap?.NpcList?.FirstOrDefault(n => n.CickID == clickID) as Game.Maps.QuestNpc;
            ushort templateId = clickedNpcObj != null ? (ushort)clickedNpcObj.TemplateID : (ushort)(p.CurMap.mapData?.Npclist?.FirstOrDefault(n => n.clickId == clickID)?.npcId ?? 0);
            string npcName = clickedNpcObj != null ? clickedNpcObj.Name : (templateId > 0 ? Game.DataFiles.SceneDataManager.GetNpcName(templateId) : (p.CurMap.mapData?.Npclist?.FirstOrDefault(n => n.clickId == clickID)?.Name ?? "Unknown"));
            string mapName = Game.DataFiles.SceneDataManager.GetMapName((ushort)p.CurMap.MapID);

            DebugSystem.Write($"[AC20.Recv1] Player {p.CharName} clicked NPC #{clickID} '{npcName}' (TID: {templateId}) on Map #{p.CurMap.MapID} ({mapName})");

            // If player is already in an active multi-step dialogue with queued steps, advance dialogue
            if ((p.QueueData != null && p.QueueData.Count > 0) || (p.StepQueue != null && p.StepQueue.Count > 0))
            {
                if (p.ContinueInteraction())
                {
                    return;
                }
            }
            if (p.OnDialogueChoice != null)
            {
                // Active choice prompt is awaiting player selection
                return;
            }

            // Prioritize NPC interaction (dialogue, quests, battles)
            if (p.CurMap.ProcessInteraction((byte)clickID, p))
            {
                // Interaction handled successfully
                return;
            }

            // Door / Portal trigger fallback ONLY if object name or type is explicitly a door
            if (p.CurMap.mapData != null && p.CurMap.mapData.Npclist != null)
            {
                var clickedNpc = p.CurMap.mapData.Npclist.FirstOrDefault(n => n.clickId == clickID);
                if (clickedNpc != null && (clickedNpc.Name ?? "").ToLower().Contains("door") && clickedNpc.unknownbytearray2 != null && clickedNpc.unknownbytearray2.Count > 0)
                {
                    ushort linkedPortal = clickedNpc.unknownbytearray2[0];
                    DebugSystem.Write($"[AC20.Recv1] Object {clickID} is an explicit door linking to portal {linkedPortal} on Map {p.CurMap.MapID}");
                    if (p.CurMap.Teleport(TeleportType.Regular, p, linkedPortal))
                    {
                        p.SaveCharacterData();
                        return;
                    }
                }
            }

            // Send default response if interaction fails
            p.Send(Tools.FromFormat("bb", 20, 8));
            p.Send(Tools.FromFormat("bb", 5, 4));
        }
        void Recv6(Player p, RecievePacket r)
        {
            // If the player is currently watching the ship storm cutscene, client AC 20:6 signals cutscene finished -> warp to beach
            if (p.PlayingStormCutscene)
            {
                p.PlayingStormCutscene = false;
                p.PendingBeachCutscene = true;
                p.Send(Tools.FromFormat("bb", 20, 7)); // Frame 2379: AC 20:7 Warp Out
                var warp = new WarpData() { DstMap = 10035, DstX_Axis = 1038, DstY_Axis = 2235 };
                p.CurMap?.Teleport(TeleportType.CmD, p, 0, warp);
                DebugSystem.Write($"[AC20.Recv6] Storm Cutscene finished for {p.CharName} -> Warping to Beach (Map 10035)");
                return;
            }

            // If beach cutscene timeline is actively running, absorb AC 20:6 so it does not interfere with the timeline
            if (p.BeachCutsceneActive)
            {
                DebugSystem.Write($"[AC20.Recv6] Absorbed AC 20:6 during BeachCutsceneActive timeline for {p.CharName}");
                return;
            }

            if (!p.ContinueInteraction())
            {
                // If the player is currently on an interactive choice prompt, keep the dialog open
                if (p.OnDialogueChoice != null)
                {
                    return;
                }

                p.Send(Tools.FromFormat("bbb", 6, 2, 0));
                p.Send(Tools.FromFormat("bb", 20, 8));
                p.Send(Tools.FromFormat("bb", 5, 4));
                p.Flags.Add(PlayerFlag.InMap);
                p.SaveCharacterData();
            }
        }
        void Recv9(Player p, RecievePacket r)
        {
            try
            {
                byte choice = r.Unpack8();
                DebugSystem.Write($"[AC20.Recv9] Player {p.CharName} selected dialogue choice 0x{choice:X} ({choice})");
                if (p.OnDialogueChoice != null)
                {
                    var callback = p.OnDialogueChoice;
                    p.OnDialogueChoice = null;
                    callback.Invoke(choice);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC20.Recv9] Error: {ex.Message}");
            }
        }
    }
}