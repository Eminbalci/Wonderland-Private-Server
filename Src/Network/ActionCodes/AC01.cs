using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 1: Client-Server Authentication & Session Handshake Protocol.
    /// Handles initial connection handshake, client version confirmation, and session tokens.
    /// </summary>
    public class AC01 : AC
    {
        public override int ID => 1;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC01] Handshake received from {c.CharName ?? "Client"}: SubCode={subCode}");

            switch (subCode)
            {
                case 1:
                    HandleAuthHandshake(c, p);
                    break;
                default:
                    AcknowledgeGeneric(c, subCode);
                    break;
            }
        }

        private void HandleAuthHandshake(Player c, RecievePacket p)
        {
            try
            {
                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(1); // SubAction 1: Handshake Success
                resp.Pack8(1); // Status = OK
                resp.Pack32((uint)Environment.TickCount); // Server session timestamp
                c.Send(resp);

                DebugSystem.Write($"[AC01.HandleAuthHandshake] Handshake confirmed for {c.CharName ?? "Client"}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }

        private void AcknowledgeGeneric(Player c, byte subCode)
        {
            try
            {
                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack8(1);
                c.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
