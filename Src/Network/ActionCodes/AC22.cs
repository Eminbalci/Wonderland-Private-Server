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
                ushort state = 0x00FF; // Authentic default for all living NPCs (wire: FF 00)

                if (qn != null && (qn.IsStaticNpc() || qn.TemplateID >= 19000))
                {
                    // Static interactive map props / chests: 0x0001 if opened/broken, 0x0000 if intact
                    bool isOpened = qn.IsBroken;
                    if (!isOpened && player?.Quests != null && eveData != null)
                    {
                        var ev = eveData.Events?.FirstOrDefault(e => e.clickID == qn.CickID);
                        if (ev != null && ev.SubEntry != null)
                        {
                            foreach (var s in ev.SubEntry)
                            {
                                if (s.unknownword1 > 0 && player.Quests.TryGetValue(s.unknownword1, out var pq) && pq.State == QuestState.Completed)
                                {
                                    isOpened = true;
                                    break;
                                }
                            }
                        }
                    }
                    state = isOpened ? (ushort)0x0001 : (ushort)0x0000;
                }
                else
                {
                    state = 0x00FF; // Normal living NPC actor
                }

                p.Pack16(state);
                p.Pack16(npc.X);
                p.Pack16(npc.Y);
                p.Pack16((ushort)1); // Direction / mode (uint16 LE)
                p.Pack32(0);         // Flags / padding (uint32 LE)
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
        /// AC 22:10 - Temporarily conceals or despawns an actor during events.
        /// Payload: [22, 10, ClickID:w, 0xFFFF:w] (6 bytes total on wire).
        /// </summary>
        public static SendPacket BuildNpcDespawnPacket(ushort clickId)
        {
            SendPacket p = new SendPacket();
            p.Pack8(22);
            p.Pack8(10);
            p.Pack16(clickId);
            p.Pack16(0xFFFF);
            return p;
        }

        /// <summary>
        /// AC 22:11 - Dynamic scene isolation hide packet for staged PreEvent entities.
        /// Payload: [22, 11, ClickID:w, 0xFFFF:w] (6 bytes total on wire).
        /// </summary>
        public static SendPacket BuildNpcHidePacket(ushort clickId)
        {
            SendPacket p = new SendPacket();
            p.Pack8(22);
            p.Pack8(11);
            p.Pack16(clickId);
            p.Pack16(0xFFFF);
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
