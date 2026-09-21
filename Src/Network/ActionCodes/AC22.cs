using System;
using System.Collections.Generic;
using System.Linq;
using Game;
using Game.Maps;
using Game.QuestRelated;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 22: Map Entity & Interactive NPC Proximity Synchronization Protocol.
    /// Handles overworld NPC spatial AI, PreEvent visibility tables, waypoint roaming,
    /// dynamic orientation, stance animations, and scene isolation concealment.
    /// </summary>
    public class AC22 : AC
    {
        public override int ID => 22;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            ushort entityId = 0;
            if (p.Buffer != null && p.Buffer.Length >= 8)
            {
                entityId = p.Unpack16();
            }

            DebugSystem.Write($"[AC22] Entity interaction query from {c.CharName}: SubCode={subCode}, EntityID={entityId}");

            try
            {
                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack16(entityId);
                resp.Pack8(1); // 1 = Active / Interactive
                c.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        /// <summary>
        /// AC 22:1 - Updates NPC facing direction.
        /// Payload: [22, 1, ClickID:w, Direction:b] (5 bytes total on wire).
        /// </summary>
        public static SendPacket BuildNpcTurnPacket(ushort clickId, byte direction)
        {
            SendPacket p = new SendPacket();
            p.Pack8(22);
            p.Pack8(1);
            p.Pack16(clickId);
            p.Pack8(direction);
            return p;
        }

        /// <summary>
        /// AC 22:2 - Dispatches dynamic NPC walking step to destination coordinates.
        /// Payload: [22, 2, ClickID:w, TargetX:w, TargetY:w, Speed:b] (9 bytes total on wire).
        /// </summary>
        public static SendPacket BuildNpcWalkPacket(ushort clickId, ushort targetX, ushort targetY, byte speed = 2)
        {
            SendPacket p = new SendPacket();
            p.Pack8(22);
            p.Pack8(2);
            p.Pack16(clickId);
            p.Pack16(targetX);
            p.Pack16(targetY);
            p.Pack8(speed);
            return p;
        }

        /// <summary>
        /// AC 22:4 - Constructs the authentic 14-byte per-record Map NPC and Prop visibility table.
        /// Record: [ClickID:w, State:w, X:w, Y:w, Direction:w, Flags:d (0)]
        /// State values: 0x00FF = Normal living NPC, 0x0000 = Intact interactive prop/chest, 0x0001 = Opened/broken prop.
        /// </summary>
        public static SendPacket BuildNpcTablePacket(IEnumerable<InteractableObjects> npcs, ushort mapId, Player player)
        {
            if (npcs == null) return null;

            SendPacket p = new SendPacket();
            p.Pack8(22);
            p.Pack8(4);

            var eveData = DataBase.GameDataBase.GlobalInstance?.EveDat?.GetMapData(mapId);

            foreach (var npc in npcs.OrderBy(n => n.CickID))
            {
                p.Pack16(npc.CickID);
                QuestNpc qn = npc as QuestNpc;
                bool isRecruited = qn != null && player != null && player.HasRecruitedCompanion(qn.Name, (ushort)qn.TemplateID);
                bool isHiddenByPreEvent = player != null && !PreEventInterpreter.ShouldNpcBeVisible(player, mapId, (ushort)npc.CickID);
                bool isDead = qn != null && qn.IsBroken && qn.RespawnTime == DateTime.MaxValue;
                bool isHidden = isRecruited || isHiddenByPreEvent || isDead;

                ushort state = 0x00FF; // Authentic default for all living NPCs (wire: FF 00)

                if (isHidden)
                {
                    state = 0xFFFF; // Authentic WLO despawned/concealed entity state
                }
                else if (qn != null && (qn.IsStaticNpc() || qn.TemplateID >= 19000))
                {
                    // Check if this prop is tied to a one-time per-player quest or chest
                    bool isQuestProp = false;
                    bool isOpened = false;

                    if (player?.Quests != null && eveData != null)
                    {
                        // 1. Check linked events via npcDef.Events in Eve.emg
                        List<Game.DataFiles.EventsinMapEntries> candidates = new List<Game.DataFiles.EventsinMapEntries>();
                        var npcDef = eveData.Npclist?.FirstOrDefault(n => n.clickId == qn.CickID);
                        if (npcDef?.Events != null && npcDef.Events.Count > 0 && eveData.Events != null)
                        {
                            foreach (var evId in npcDef.Events)
                            {
                                var linked = eveData.Events.FirstOrDefault(e => e.clickID == evId);
                                if (linked != null && !candidates.Contains(linked)) candidates.Add(linked);
                            }
                        }
                        // 2. Fallback to direct event matching clickID
                        if (candidates.Count == 0 && eveData.Events != null)
                        {
                            var direct = eveData.Events.FirstOrDefault(e => e.clickID == qn.CickID);
                            if (direct != null) candidates.Add(direct);
                        }
                        // 3. Fallback to events containing opcodes referencing this clickId
                        if (candidates.Count == 0 && eveData.Events != null)
                        {
                            foreach (var ev in eveData.Events)
                            {
                                if (ev.SubEntry != null && ev.SubEntry.Any(s => s.SubEntry != null && s.SubEntry.Any(o => o.DialogPtr == 2 && o.dialog2 == 5 && (o.dialog1 == qn.CickID || (o.dialog1 == 0 && ev.clickID == qn.CickID)))))
                                {
                                    candidates.Add(ev);
                                }
                            }
                        }

                        // Evaluate candidates for completion or chest loot state
                        foreach (var ev in candidates)
                        {
                            uint chestKey = (uint)(mapId * 1000 + ev.clickID);
                            if (player.Quests.TryGetValue(chestKey, out var pqChest) && pqChest.State == QuestState.Completed)
                            {
                                isQuestProp = true;
                                isOpened = true;
                                break;
                            }

                            if (ev.SubEntry != null)
                            {
                                foreach (var s in ev.SubEntry)
                                {
                                    if (s.unknownword1 > 0)
                                    {
                                        isQuestProp = true;
                                        if (player.Quests.TryGetValue(s.unknownword1, out var pq) && pq.State == QuestState.Completed)
                                        {
                                            isOpened = true;
                                            break;
                                        }
                                    }
                                }
                            }
                            if (isOpened) break;
                        }
                    }

                    // Also check direct chestKey using clickID
                    if (!isOpened && player?.Quests != null)
                    {
                        uint chestKey = (uint)(mapId * 1000 + qn.CickID);
                        if (player.Quests.TryGetValue(chestKey, out var pqChest) && pqChest.State == QuestState.Completed)
                        {
                            isQuestProp = true;
                            isOpened = true;
                        }
                    }

                    // Only non-quest renewable gathering nodes (ore, wood, clay) check shared qn.IsBroken
                    if (!isQuestProp)
                    {
                        isOpened = qn.IsBroken;
                    }

                    // Authentic WLO protocol (Official PCAP Seq 635 / 971):
                    // 0x0000 is the default intact animation frame for static interactive props / chests / gathering nodes
                    // 0x0001 is the opened / broken animation frame
                    // Sending 0x00FF (255) to a prop causes the client sprite engine to cycle frames 0 and 1, creating a blinking/flickering bug
                    state = isOpened ? (ushort)0x0001 : (ushort)0x0000;
                }
                else
                {
                    state = 0x00FF; // Normal living NPC actor
                }

                byte entityType = isHidden ? (byte)2 : (byte)1;
                uint duration = isHidden ? 0x03E7FC18u : 0u;

                p.Pack16(state);
                p.Pack16(npc.X);
                p.Pack16(npc.Y);
                p.Pack8(entityType);
                p.Pack32(duration);
                p.Pack8(0);
            }

            return p;
        }

        /// <summary>
        /// AC 22:5 - Instantly warps or teleports an NPC to world coordinates.
        /// Payload: [22, 5, ClickID:w, X:w, Y:w] (8 bytes total on wire).
        /// </summary>
        public static SendPacket BuildNpcWarpPacket(ushort clickId, ushort x, ushort y)
        {
            SendPacket p = new SendPacket();
            p.Pack8(22);
            p.Pack8(5);
            p.Pack16(clickId);
            p.Pack16(x);
            p.Pack16(y);
            return p;
        }

        /// <summary>
        /// AC 22:6 - Updates NPC animation or stance.
        /// Payload: [22, 6, ClickID:w, Stance:b] (5 bytes total on wire).
        /// </summary>
        public static SendPacket BuildNpcStancePacket(ushort clickId, byte stance)
        {
            SendPacket p = new SendPacket();
            p.Pack8(22);
            p.Pack8(6);
            p.Pack16(clickId);
            p.Pack8(stance);
            return p;
        }

        /// <summary>
        /// AC 22:4 - Constructs authentic entity concealment frame (0x03E7FC18 despawn duration code).
        /// Sets actor visibility field *(actor + 0x1eec) = 2 and clears map collision grid via FUN_0043d390.
        /// Payload: [22, 4, ClickID:w, 0xFFFF:w, X:w, Y:w, EntityType:b (2), Duration:d (0x03E7FC18), Stance:b (0)] (14 bytes total).
        /// </summary>
        public static SendPacket BuildNpcDespawnPacket(ushort clickId, ushort x = 0, ushort y = 0)
        {
            SendPacket p = new SendPacket();
            p.Pack8(22);
            p.Pack8(4);
            p.Pack16(clickId);
            p.Pack16(0xFFFF);
            p.Pack16(x);
            p.Pack16(y);
            p.Pack8(2); // 2 = Hidden
            p.Pack32(0x03E7FC18); // Despawn & clear collision
            p.Pack8(0);
            return p;
        }

        /// <summary>
        /// AC 22:4 - Dynamic scene isolation hide packet for staged PreEvent entities.
        /// Payload: [22, 4, ClickID:w, 0xFFFF:w, X:w, Y:w, EntityType:b (2), Duration:d (0x03E7FC18), Stance:b (0)] (14 bytes total).
        /// </summary>
        public static SendPacket BuildNpcHidePacket(ushort clickId, ushort x = 0, ushort y = 0)
        {
            return BuildNpcDespawnPacket(clickId, x, y);
        }

        /// <summary>
        /// AC 22:4 - Restores or reveals an actor on client viewport.
        /// Payload: [22, 4, ClickID:w, 0x00FF:w, X:w, Y:w, EntityType:b (1), Duration:d (0), Stance:b (0)] (14 bytes total).
        /// </summary>
        public static SendPacket BuildNpcSpawnPacket(ushort clickId, ushort x = 0, ushort y = 0)
        {
            SendPacket p = new SendPacket();
            p.Pack8(22);
            p.Pack8(4);
            p.Pack16(clickId);
            p.Pack16(0x00FF);
            p.Pack16(x);
            p.Pack16(y);
            p.Pack8(1); // 1 = Visible
            p.Pack32(0);
            p.Pack8(0);
            return p;
        }

        /// <summary>
        /// AC 22:4 - Dynamic scene isolation reveal packet for staged PreEvent entities.
        /// Payload: [22, 4, ClickID:w, 0x00FF:w, X:w, Y:w, EntityType:b (1), Duration:d (0), Stance:b (0)] (14 bytes total).
        /// </summary>
        public static SendPacket BuildNpcShowPacket(ushort clickId, ushort x = 0, ushort y = 0)
        {
            return BuildNpcSpawnPacket(clickId, x, y);
        }

        /// <summary>
        /// AC 22:4 - Restores or reveals an interactive prop/chest on client viewport.
        /// Payload: [22, 4, ClickID:w, State:w (0x0000 intact or 0x0001 opened), X:w, Y:w, EntityType:b (1), Duration:d (0), Stance:b (0)] (14 bytes total).
        /// Authentic WLO protocol mandates 0x0000 (intact) or 0x0001 (opened), never 0x00FF.
        /// </summary>
        public static SendPacket BuildPropSpawnPacket(ushort clickId, ushort x = 0, ushort y = 0, bool isOpened = false)
        {
            SendPacket p = new SendPacket();
            p.Pack8(22);
            p.Pack8(4);
            p.Pack16(clickId);
            p.Pack16(isOpened ? (ushort)0x0001 : (ushort)0x0000);
            p.Pack16(x);
            p.Pack16(y);
            p.Pack8(1); // 1 = Visible
            p.Pack32(0);
            p.Pack8(0);
            return p;
        }

        /// <summary>
        /// AC 22:12 - Configures NPC speed or patrol behavior.
        /// Payload: [22, 12, Subtype:b, ClickID:w, Speed:b] (6 bytes total on wire).
        /// </summary>
        public static SendPacket BuildNpcSpeedPacket(byte subtype, ushort clickId, byte speed)
        {
            SendPacket p = new SendPacket();
            p.Pack8(22);
            p.Pack8(12);
            p.Pack8(subtype);
            p.Pack16(clickId);
            p.Pack8(speed);
            return p;
        }
    }
}
