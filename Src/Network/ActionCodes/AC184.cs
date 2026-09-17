using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 184: Promotional Gift Exchange & Reward Voucher Protocol.
    /// Handles redeeming special promotional event gift tokens and holiday reward vouchers.
    /// </summary>
    public class AC184 : AC
    {
        public override int ID => 184;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC184] Gift voucher exchange from {c.CharName}: SubCode={subCode}");

            try
            {
                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack8(1); // 1 = Exchange Successful
                resp.Pack16(23001); // Reward Item ID
                c.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
