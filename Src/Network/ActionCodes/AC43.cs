using System;
using Game;
using Game.QuestRelated;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 43: Quest Navigation Compass & Objective Tracking Protocol.
    /// Handles quest guide tracking, waypoint target coordinates, and NPC location compass.
    /// </summary>
    public class AC43 : AC
    {
        public override int ID => 43;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            ushort questId = 0;
            if (p.Buffer.Length >= 8)
            {
                questId = p.Unpack16();
            }

            DebugSystem.Write($"[AC43] Quest tracker from {c.CharName}: SubCode={subCode}, QuestID={questId}");

            try
            {
                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack16(questId);
                resp.Pack16((ushort)c.CurMap.MapID); // Target Map
                resp.Pack16((ushort)c.X);             // Target X
                resp.Pack16((ushort)c.Y);             // Target Y
                c.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
