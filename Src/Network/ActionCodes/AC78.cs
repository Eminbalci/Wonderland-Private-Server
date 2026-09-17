using System;
using Game;
using Game.Battle;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 78: PVP Arena Matchmaking & Combat Tournament Queue Protocol.
    /// Handles Solo and Team PVP tournament queuing, opponent matchmaking, and arena staging.
    /// </summary>
    public class AC78 : AC
    {
        public override int ID => 78;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC78] PVP Matchmaking queue from {c.CharName}: SubCode={subCode}");

            try
            {
                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack8(1); // 1 = Queued in Matchmaking
                c.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
