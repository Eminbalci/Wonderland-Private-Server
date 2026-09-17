using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 74: Casino Lucky Roulette & Carnival Wheel Protocol.
    /// Handles spinning the carnival wheel, calculating prize sectors, and granting rewards.
    /// </summary>
    public class AC74 : AC
    {
        public override int ID => 74;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC74] Casino Roulette spin from {c.CharName}: SubCode={subCode}");

            try
            {
                byte prizeSector = (byte)(new Random().Next(1, 12));

                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack8(prizeSector); // Sector index 1-12
                resp.Pack8(1);           // 1 = Spin Complete
                c.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
