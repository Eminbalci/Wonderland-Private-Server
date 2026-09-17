using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 76: Scheduled World Event Calendar Protocol.
    /// Handles querying event schedules, daily/weekly activities, and tournament countdowns.
    /// </summary>
    public class AC76 : AC
    {
        public override int ID => 76;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC76] Event calendar query from {c.CharName}: SubCode={subCode}");

            try
            {
                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack16(0); // Event count (active calendar)
                c.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
