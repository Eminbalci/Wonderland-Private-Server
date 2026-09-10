using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 79: Guild Stronghold War & Fortress Siege Protocol.
    /// Handles guild war declarations, territory claim points, and siege status.
    /// </summary>
    public class AC79 : AC
    {
        public override int ID => 79;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC79] Guild war action from {c.CharName}: SubCode={subCode}");

            try
            {
                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack8(1); // Active status
                c.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
