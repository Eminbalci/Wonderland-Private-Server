using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 7: High-Precision Coordinate Waypoint Pathfinding & Step Synchronization.
    /// Handles long-distance continuous pathfinding waypoints (Map ID, Target X, Target Y).
    /// </summary>
    public class AC07 : AC
    {
        public override int ID => 7;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            try
            {
                // In decompiled client: Packs 3 uint16 values: MapID / CurrentX, TargetX, TargetY
                ushort targetX = 0;
                ushort targetY = 0;

                if (p.Buffer.Length >= 6)
                {
                    targetX = p.Unpack16();
                    targetY = p.Unpack16();
                }

                if (targetX > 0 || targetY > 0)
                {
                    c.X = targetX;
                    c.Y = targetY;
                }

                // Echo back coordinate synchronization ACK
                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(p.B ?? 1);
                resp.Pack16((ushort)c.X);
                resp.Pack16((ushort)c.Y);
                c.Send(resp);

                DebugSystem.Write($"[AC07] Waypoint sync for {c.CharName}: X={c.X}, Y={c.Y}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
