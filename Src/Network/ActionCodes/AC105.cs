using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 105: VIP & Premium Membership Privileges Protocol.
    /// Handles querying VIP tier level, claiming daily VIP supply crates, and privilege bonuses.
    /// </summary>
    public class AC105 : AC
    {
        public override int ID => 105;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC105] VIP query from {c.CharName}: SubCode={subCode}");

            try
            {
                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack8(1);  // VIP Tier 1
                resp.Pack32(100); // VIP Points
                c.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
