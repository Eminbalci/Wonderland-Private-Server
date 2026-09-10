using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 61: Tent Staircase & Second-Floor Architecture Protocol.
    /// Handles constructing the wooden spiral staircase to unlock and transition to the second floor of the tent.
    /// </summary>
    public class AC61 : AC
    {
        public override int ID => 61;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC61] Tent second-floor upgrade from {c.CharName}: SubCode={subCode}");

            try
            {
                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack8(1); // 1 = 2nd Floor Unlocked / Transition Complete
                c.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
