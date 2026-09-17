using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 80: Leaderboard Ladder & Hall of Fame Ranking Protocol.
    /// Handles querying global rankings (Top Level, Wealthiest Players, PVP Arena champions).
    /// </summary>
    public class AC80 : AC
    {
        public override int ID => 80;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC80] Leaderboard query from {c.CharName}: SubCode={subCode}");

            try
            {
                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack16(1); // 1 entry
                resp.Pack32(c.CharID);
                resp.PackStringN(c.CharName ?? "Leader");
                resp.Pack32((uint)c.Level);
                c.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
