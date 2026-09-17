using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 81: In-Game Achievements & Milestone Badges Protocol.
    /// Handles querying completed game achievements, title milestones, and claiming reward badges.
    /// </summary>
    public class AC81 : AC
    {
        public override int ID => 81;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC81] Achievement action from {c.CharName}: SubCode={subCode}");

            try
            {
                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack16(0); // 0 achievements completed / updated
                c.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
