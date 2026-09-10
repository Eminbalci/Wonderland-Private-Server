using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 22: Map Entity & Interactive NPC Proximity Synchronization Protocol.
    /// Handles queries for nearby interactive gathering nodes, signs, chest states, and roaming NPCs.
    /// </summary>
    public class AC22 : AC
    {
        public override int ID => 22;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            ushort entityId = 0;
            if (p.Buffer.Length >= 8)
            {
                entityId = p.Unpack16();
            }

            DebugSystem.Write($"[AC22] Entity interaction query from {c.CharName}: SubCode={subCode}, EntityID={entityId}");

            try
            {
                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack16(entityId);
                resp.Pack8(1); // 1 = Active / Interactive
                c.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
