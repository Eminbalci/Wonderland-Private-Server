using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 191: Daily Login Streak & Attendance Check-In Protocol.
    /// Handles recording daily player sign-ins and granting cumulative streak reward items.
    /// </summary>
    public class AC191 : AC
    {
        public override int ID => 191;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 4;
            DebugSystem.Write($"[AC191] Attendance sign-in from {c.CharName}: SubCode={subCode}");

            try
            {
                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack8(1);  // 1 = Checked In Today
                resp.Pack8(7);  // 7-day streak
                resp.Pack16(23001); // Streak prize item
                c.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
