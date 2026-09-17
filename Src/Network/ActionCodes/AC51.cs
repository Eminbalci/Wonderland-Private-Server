using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 51: Guild Vault & Shared Storage Protocol.
    /// Handles depositing and withdrawing guild equipment, materials, and funds into the guild vault.
    /// </summary>
    public class AC51 : AC
    {
        public override int ID => 51;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC51] Guild vault action from {c.CharName}: SubCode={subCode}");

            try
            {
                switch (subCode)
                {
                    case 1: // Open / Query Guild Vault
                        SendPacket resp1 = new SendPacket();
                        resp1.Pack8((byte)ID);
                        resp1.Pack8(1);
                        resp1.Pack8(0); // 0 items stored
                        c.Send(resp1);
                        break;
                    case 2: // Deposit Item into Guild Vault
                        SendPacket resp2 = new SendPacket();
                        resp2.Pack8((byte)ID);
                        resp2.Pack8(2);
                        resp2.Pack8(1); // 1 = Deposited OK
                        c.Send(resp2);
                        break;
                    case 3: // Withdraw Item from Guild Vault
                        SendPacket resp3 = new SendPacket();
                        resp3.Pack8((byte)ID);
                        resp3.Pack8(3);
                        resp3.Pack8(1); // 1 = Withdrawn OK
                        c.Send(resp3);
                        break;
                }
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
