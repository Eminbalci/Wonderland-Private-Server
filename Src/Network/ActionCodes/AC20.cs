using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Network;
using Network.ActionCodes;
using Game;
using Game.Maps;
using Game.QuestRelated;

namespace Wonderland_Private_Server.ActionCodes
{
    public class AC20 : AC
    {
        public override int ID { get { return 20; } }
        public override void ProcessPkt(Player r, RecievePacket p)
        {
            if (Game.Battle.PvEBattleManager.IsInBattle(r)) return;
            byte subCode = p.B ?? (p.Buffer != null && p.Buffer.Length > 5 ? p[5] : (byte)0);
            p.SetPtr(6);
            switch (subCode)
            {
                case 1: Recv1(r, p); break;
                case 6: Recv6(r, p); break;
                case 9: Recv9(r, p); break;
                case 8: Recv8(r, p); break;
                case 10: Recv10(r, p); break; // 0x0A Open Player Stall
                case 13: Recv13(r, p); break; // 0x0D Close Player Stall
                case 14: Recv14(r, p); break; // 0x0E Browse Player Stall
                case 17: Recv17(r, p); break; // 0x11 Purchase Item from Stall
            }
        }
        void Recv8(Player p, RecievePacket r)
        {
            if (p == null || p.CurMap == null || p.NativeEventActive) return;

            // Portal debounce and spawn proximity guard (bypassed for tent interior)
            bool isTent = p.CurMap is Game.Code.Tent || p.CurMap?.Type == MapType.Tent || p.Flags.HasFlag(PlayerFlag.InTent);

            if (!isTent && p.CurMap is GameMap regionMap && EveEventInterpreter.TryExecuteRegion(p, regionMap)) return;

            if (!isTent)
            {
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
            }

            int rem = r.Buffer.Length - r.GetPtr();
            ushort portalID = 0;
            if (rem >= 2) portalID = r.Unpack16();
            else if (rem == 1) portalID = r.Unpack8();
            if (!isTent && p.CurMap is GameMap entryMap && EveEventInterpreter.TryExecuteEntry(p, entryMap, portalID)) return;
            DebugSystem.Write($"[AC20.Recv8] Player {p.CharName} stepped on portal {portalID} on Map {p.CurMap?.MapID} (isTent={isTent}) at pos({p.CurX},{p.CurY})");

            if (p.CurMap != null)
            {
                if (isTent)
                {
                    ushort dstMap = 0, dstX = 0, dstY = 0;
                    if (p.TentReturnMap != null && p.TentReturnMap.DstMap > 0 && !(p.TentReturnMap.DstX_Axis == 460 && p.TentReturnMap.DstY_Axis == 700))
                    {
                        dstMap = p.TentReturnMap.DstMap;
                        dstX = p.TentReturnMap.DstX_Axis;
                        dstY = p.TentReturnMap.DstY_Axis;
                    }
                    else if (p.Tent != null && p.Tent.OwnerMap != null && (p.Tent.X > 0 || p.Tent.Y > 0) && !(p.Tent.X == 460 && p.Tent.Y == 700))
                    {
                        dstMap = (ushort)p.Tent.OwnerMap.MapID;
                        dstX = (ushort)p.Tent.X;
                        dstY = (ushort)p.Tent.Y;
                    }
                    else if (p.CurMap is Game.Code.Tent curTent && curTent.OwnerMap != null && (curTent.X > 0 || curTent.Y > 0) && !(curTent.X == 460 && curTent.Y == 700))
                    {
                        dstMap = (ushort)curTent.OwnerMap.MapID;
                        dstX = (ushort)curTent.X;
                        dstY = (ushort)curTent.Y;
                    }
                    else if (p.PrevMap != null && p.PrevMap.DstMap > 0 && !(p.PrevMap.DstX_Axis == 460 && p.PrevMap.DstY_Axis == 700))
                    {
                        dstMap = p.PrevMap.DstMap;
                        dstX = p.PrevMap.DstX_Axis;
                        dstY = p.PrevMap.DstY_Axis;
                    }
                    else if (p.TentReturnMap != null && p.TentReturnMap.DstMap > 0)
                    {
                        dstMap = p.TentReturnMap.DstMap;
                        dstX = p.TentReturnMap.DstX_Axis;
                        dstY = p.TentReturnMap.DstY_Axis;
                    }
                    else
                    {
                        dstMap = 12000;
                        dstX = 892;
                        dstY = 734;
                    }

                    p.Flags.Remove(PlayerFlag.InTent);
                    WarpData warp = new WarpData() { DstMap = dstMap, DstX_Axis = dstX, DstY_Axis = dstY };
                    p.CurMap.Teleport(TeleportType.CmD, p, 0, warp);
                    p.SaveCharacterData();
                    DebugSystem.Write($"[AC20.Recv8] Tent Exit: Teleported {p.CharName} out of tent to Map {dstMap} ({dstX},{dstY})");
                    return;
                }

                // Authored EVE entries above decide destinations. Do not fabricate
                // an active raft or destroy unrelated vehicles when crossing a portal.
                bool landQuestRaft = p.CurMap.MapID == 11016 && p.ActiveVehicleID == 48016;
                if (p.CurMap.Teleport(TeleportType.Regular, p, portalID))
                {
                    if (landQuestRaft) Game.PlayerRelated.VehicleManager.WreckVehicle(p, 48016);
                    p.SaveCharacterData();
                    return;
                }
            }

            p.Send(Tools.FromFormat("bb", 20, 8));
        }
        void Recv1(Player p, RecievePacket r)
        {
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

            DebugSystem.Write($"[AC20.Recv1] Incoming click: Player={p.CharName} ClickID={clickID} MapID={p.CurMap?.MapID} Storm={p.PlayingStormCutscene} Beach={p.BeachCutsceneActive} NativeEvent={p.NativeEventActive}");

            if (p.PlayingStormCutscene || p.BeachCutsceneActive || p.NativeEventActive)
            {
                DebugSystem.Write($"[AC20.Recv1] Click #{clickID} BLOCKED: Active State (Storm={p.PlayingStormCutscene}, Beach={p.BeachCutsceneActive}, NativeEvent={p.NativeEventActive}) for {p.CharName}");
                p.Send(Tools.FromFormat("bb", 20, 8));
                return;
            }
            if (p.CurMap == null)
            {
                p.Send(Tools.FromFormat("bb", 20, 8));
                return;
            }

            // Closing a native choice can queue a second click on the actor below it.
            if (p.LastNpcClick == clickID && p.LastNpcMap == p.CurMap.MapID && DateTime.UtcNow < p.NpcClickResumeAt)
            {
                double remainMs = (p.NpcClickResumeAt - DateTime.UtcNow).TotalMilliseconds;
                DebugSystem.Write($"[AC20.Recv1] Debounce BLOCKED click on NPC #{clickID} for {p.CharName}. Remaining cooldown: {remainMs:F0}ms");
                p.Send(Tools.FromFormat("bb", 20, 8));
                return;
            }
            p.LastNpcClick = clickID; p.LastNpcMap = (ushort)p.CurMap.MapID;
            p.PendingRestMap = 0; p.NpcSaleMode = null;
            var gMap = p.CurMap as GameMap;
            var clickedNpcObj = gMap?.NpcList?.FirstOrDefault(n => n.CickID == clickID) as Game.Maps.QuestNpc;
            ushort templateId = clickedNpcObj != null ? (ushort)clickedNpcObj.TemplateID : (ushort)(p.CurMap.mapData?.Npclist?.FirstOrDefault(n => n.clickId == clickID)?.npcId ?? 0);
            string npcName = clickedNpcObj != null ? clickedNpcObj.Name : (templateId > 0 ? Game.DataFiles.SceneDataManager.GetNpcName(templateId) : (p.CurMap.mapData?.Npclist?.FirstOrDefault(n => n.clickId == clickID)?.Name ?? "Unknown"));
            string mapName = Game.DataFiles.SceneDataManager.GetMapName((ushort)p.CurMap.MapID);

            DebugSystem.Write($"[AC20.Recv1] Player {p.CharName} clicked NPC #{clickID} '{npcName}' (TID: {templateId}) on Map #{p.CurMap.MapID} ({mapName})");

            // Guard: block interaction immediately if NPC is hidden / invisible
            if (p.HiddenNpcClickIDs.Contains(clickID) || !Game.QuestRelated.PreEventInterpreter.ShouldNpcBeVisible(p, (ushort)p.CurMap.MapID, clickID))
            {
                DebugSystem.Write($"[AC20.Recv1] Blocked interaction: NPC #{clickID} '{npcName}' is hidden/invisible for player {p.CharName}");
                Game.QuestRelated.PreEventInterpreter.SendActorHide(p, clickID);
                p.Send(Tools.FromFormat("bb", 20, 8));
                return;
            }

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
        }
        public static void AdvanceBeachCutscene(Player p, bool forceComplete = false)
        {
            if (!p.BeachCutsceneActive) return;

            DebugSystem.Write($"[BeachCutscene] AdvanceBeachCutscene called for {p.CharName}: CurrentStep={p.BeachCutsceneStep}, ForceComplete={forceComplete}");

            if (forceComplete)
            {
                p.BeachCutsceneStep = 6;
            }

            switch (p.BeachCutsceneStep)
            {
                case 1:
                    // Client ACK (Frame 0332: AC 20:6) confirming stage setup: Trigger Beach Wakeup Animation (Cutscene ID 12008)
                    p.BeachCutsceneStep = 2;
                    SendPacket animPkt = new SendPacket();
                    animPkt.PackArray(new byte[] { 20, 1, 0, 0, 0, 1, 5, 0 });
                    animPkt.Pack16(0);
                    animPkt.Pack8(1);
                    animPkt.Pack32(12008);
                    animPkt.Pack16(0);
                    animPkt.Pack8(1);
                    p.Send(animPkt);
                    DebugSystem.Write($"[BeachCutscene] Step 1 ACK received -> Dispatched authentic Cutscene 12008 frame to {p.CharName}");
                    break;

                case 2:
                    // Frame 2928: Cutscene 12008 animation completed!
                    p.BeachCutsceneStep = 3;

                    // Authentic PCAP Frame 2928: AC 24:1 [08, 2f, 01] + AC 20:10
                    p.Send(Tools.FromFormat("bbbbb", 24, 1, 8, 47, 1));
                    p.Send(Tools.FromFormat("bb", 20, 10));
                    DebugSystem.Write($"[BeachCutscene] Step 2 completed for {p.CharName} -> sent Quest 12040 progress + AC 20:10");
                    break;

                case 3:
                    // Frame 2971: Fanfare / Action advance
                    p.BeachCutsceneStep = 4;
                    p.Send(Tools.FromFormat("bb", 20, 10));
                    DebugSystem.Write($"[BeachCutscene] Step 3 completed for {p.CharName} -> sent AC 20:10");
                    break;

                case 4:
                    // Frame 2989: AC 24:5 [97, 0, 1] + AC 20:10
                    p.BeachCutsceneStep = 5;
                    p.Send(Tools.FromFormat("bbbbb", 24, 5, 97, 0, 1));
                    p.Send(Tools.FromFormat("bb", 20, 10));
                    DebugSystem.Write($"[BeachCutscene] Step 4 completed for {p.CharName} -> sent AC 24:5 + AC 20:10");
                    break;

                case 5:
                    // Frame 2997: AC 22:12 [1, 1, 0, 6] + AC 20:10
                    p.BeachCutsceneStep = 6;
                    p.Send(Tools.FromFormat("bbbbbb", 22, 12, 1, 1, 0, 6));
                    p.Send(Tools.FromFormat("bb", 20, 10));
                    DebugSystem.Write($"[BeachCutscene] Step 5 completed for {p.CharName} -> sent AC 22:12 (Actor 1 Robinson walk) + AC 20:10");
                    break;

                case 6:
                    // Frame 3000: Cutscene finished -> Unlock player and trigger Robinson dialogue
                    p.BeachCutsceneActive = false;
                    p.BeachCutsceneStep = 0;
                    p.Emote = 0;
                    p.Send(Tools.FromFormat("bbbdb", 5, 30, 0, p.CharID, (byte)0)); // Release AC 5:30 immobilize
                    p.Send(Tools.FromFormat("bb", 20, 8));
                    p.Send(Tools.FromFormat("bbb", 6, 2, 0));
                    p.Send(Tools.FromFormat("bb", 5, 4));
                    p.CurMap?.Broadcast(Tools.FromFormat("bbdb", 32, 2, p.CharID, (byte)0), "Ex", p.CharID);
                    p.SaveCharacterData();
                    DebugSystem.Write($"[BeachCutscene] Cutscene finished! Mobilized {p.CharName}. Awaiting player manual click on Robinson.");
                    break;

                default:
                    DebugSystem.Write($"[BeachCutscene] Unhandled BeachCutsceneStep {p.BeachCutsceneStep} for {p.CharName}");
                    break;
            }
        }

        void Recv6(Player p, RecievePacket r)
        {
            DebugSystem.Write($"[AC20.Recv6] Incoming ACK: Player={p.CharName} Storm={p.PlayingStormCutscene} Beach={p.BeachCutsceneActive} (Step {p.BeachCutsceneStep}) NativeEvent={p.NativeEventActive} Choice={p.OnDialogueChoice != null} Queue={p.QueueData?.Count ?? 0} Steps={p.StepQueue?.Count ?? 0}");

            // If the player is currently watching the ship storm cutscene, client AC 20:6 signals cutscene finished -> warp to beach
            if (p.PlayingStormCutscene)
            {
                if (!p.StormCutsceneAckReceived)
                {
                    DebugSystem.Write($"[AC20.Recv6] Ignored premature AC 20:6 before storm cutscene handshake for {p.CharName}");
                    return;
                }

                p.PlayingStormCutscene = false;
                p.StormCutsceneAckReceived = false;
                p.PendingBeachCutscene = true;
                p.ClearInteraction();
                p.Send(Tools.FromFormat("bb", 20, 8)); // Stop / clear client cutscene
                p.Send(Tools.FromFormat("bb", 20, 7)); // Frame 0328: AC 20:7 Warp Out
                var warp = new WarpData() { DstMap = 10035, DstX_Axis = 1038, DstY_Axis = 2235 };
                p.CurMap?.Teleport(TeleportType.CmD, p, 0, warp);
                DebugSystem.Write($"[AC20.Recv6] Storm Cutscene completed via client ACK for {p.CharName} -> Warping to Beach (Map 10035)");
                return;
            }

            // If beach cutscene timeline is actively running
            if (p.BeachCutsceneActive)
            {
                AdvanceBeachCutscene(p);
                return;
            }

            bool advanced = p.ContinueInteraction();
            DebugSystem.Write($"[AC20.Recv6] ContinueInteraction result={advanced} for {p.CharName}");

            if (!advanced)
            {
                // If the player is currently on an interactive choice prompt, keep the dialog open
                if (p.OnDialogueChoice != null)
                {
                    DebugSystem.Write($"[AC20.Recv6] Interactive choice prompt active for {p.CharName}, keeping dialogue open");
                    return;
                }

                // Clear any active NPC shop catalog or sale liquidation session upon dialog/modal completion
                p.NpcSaleMode = null;
                p.CurrentShopCatalogId = 0;

                DebugSystem.Write($"[AC20.Recv6] Interaction completed for {p.CharName}, sending posture/HUD unlock AC 20:8 + AC 5:4");
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
                if (r.Buffer.Length - r.GetPtr() != 1) return;
                byte choice = r.Unpack8();
                if (choice == 40)
                {
                    DebugSystem.Write($"[AC20.Recv9] Player {p.CharName} closed dialogue choice prompt (0x28 / 40)");
                    p.CancelInteraction();
                    return;
                }
                DebugSystem.Write($"[AC20.Recv9] Player {p.CharName} selected dialogue choice 0x{choice:X} ({choice})");
                if (p.OnDialogueChoice != null)
                {
                    var callback = p.OnDialogueChoice;
                    p.OnDialogueChoice = null;
                    callback.Invoke(choice);
                }
                else
                {
                    DebugSystem.Write($"[AC20.Recv9] Warning: No active OnDialogueChoice callback registered for {p.CharName}");
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC20.Recv9] Error: {ex.Message}");
            }
        }

        void Recv14(Player p, RecievePacket r)
        {
            try
            {
                if (p == null || r == null || r.Buffer.Length - r.GetPtr() < 4) return;
                uint vendorCharId = r.Unpack32();
                var stall = Game.Experimental.ExperimentalFeaturesManager.Instance.PlayerStallBazaar?.GetStall(vendorCharId);
                if (stall != null)
                {
                    var browsePkt = Game.Experimental.PlayerStallBazaarEngine.BuildStallBrowsePacket(stall);
                    if (browsePkt != null) p.Send(browsePkt);
                    var catPkt = Game.Experimental.PlayerStallBazaarEngine.BuildStallCatalogPacket(stall);
                    if (catPkt != null) p.Send(catPkt);
                    DebugSystem.Write($"[AC20.Recv14] Dispatched stall browse packets for vendor #{vendorCharId} ('{stall.StallName}') to {p.CharName}");
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC20.Recv14] Error: {ex.Message}");
            }
        }

        void Recv17(Player p, RecievePacket r)
        {
            try
            {
                if (p == null || r == null || r.Buffer.Length - r.GetPtr() < 5) return;
                uint vendorCharId = r.Unpack32();
                byte slot = r.Unpack8();
                byte qty = (r.Buffer.Length - r.GetPtr() >= 1) ? r.Unpack8() : (byte)1;
                if (qty == 0) qty = 1;

                bool success = Game.Experimental.ExperimentalFeaturesManager.Instance.PlayerStallBazaar?.PurchaseItem(p, vendorCharId, slot, qty) ?? false;
                DebugSystem.Write($"[AC20.Recv17] Purchase request from {p.CharName} for vendor #{vendorCharId} slot #{slot} x{qty} -> Result: {success}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC20.Recv17] Error: {ex.Message}");
            }
        }

        void Recv10(Player p, RecievePacket r)
        {
            try
            {
                if (p == null) return;
                string title = "Player Shop";
                if (r != null && r.Buffer.Length - r.GetPtr() > 0)
                {
                    title = r.UnpackString();
                }
                var stall = Game.Experimental.ExperimentalFeaturesManager.Instance.PlayerStallBazaar?.OpenPersonalStall(p, title);
                if (stall != null)
                {
                    p.SendSystemMessage($"[Stall] Your vending stall '{stall.StallName}' is now OPEN!");
                    DebugSystem.Write($"[AC20.Recv10] Player {p.CharName} opened stall '{stall.StallName}' via AC 20:10");
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC20.Recv10] Error: {ex.Message}");
            }
        }

        void Recv13(Player p, RecievePacket r)
        {
            try
            {
                if (p == null) return;
                Game.Experimental.ExperimentalFeaturesManager.Instance.PlayerStallBazaar?.ClosePersonalStall(p);
                p.SendSystemMessage("[Stall] Your vending stall has been CLOSED.");
                DebugSystem.Write($"[AC20.Recv13] Player {p.CharName} closed stall via AC 20:13");
            }
            catch (Exception ex)
            {
                DebugSystem.Write($"[AC20.Recv13] Error: {ex.Message}");
            }
        }
    }
}