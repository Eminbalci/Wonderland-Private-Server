using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 3: Client Scene Loaded & Map Enter Ready Confirmation (ACK).
    /// Dispatched by client when map asset rendering is complete and player is ready for world presence.
    /// </summary>
    public class AC03 : AC
    {
        public override int ID => 3;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC03] Scene Ready ACK received from {c.CharName ?? "Client"}: SubCode={subCode}");

            try
            {
                // Acknowledge scene readiness to sync map entities
                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack8(1); // Ready status
                c.Send(resp);

                // Notify map that player is fully loaded
                if (c.CurMap != null)
                {
                    SendPacket sp = new SendPacket();
                    sp.Pack8(3);
                    sp.Pack8(subCode);
                    sp.Pack32(c.CharID);
                    c.CurMap.Broadcast(sp);
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
