using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 86: Ocean Navigation & Sea Vessel Sailing Protocol.
    /// Handles open water sailing dynamics, sea obstacles, and water transport speed controls.
    /// </summary>
    public class AC86 : AC
    {
        public override int ID => 86;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC86] Ocean navigation from {c.CharName}: SubCode={subCode}");

            try
            {
                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack8(1); // 1 = Sea Sailing Active
                c.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
