using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 72: Minigame Ticket & Voucher Prize Exchange Protocol.
    /// Handles redeeming carnival game tickets, vouchers, and tokens for prize items.
    /// </summary>
    public class AC72 : AC
    {
        public override int ID => 72;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC72] Ticket voucher exchange from {c.CharName}: SubCode={subCode}");

            try
            {
                ushort prizeId = p.Buffer.Length >= 8 ? p.Unpack16() : (ushort)0;

                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack16(prizeId);
                resp.Pack8(1); // 1 = Redeemed Successfully
                c.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
