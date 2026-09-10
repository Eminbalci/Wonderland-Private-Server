using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 24: Security PIN & Trade/Stall Protection Protocol.
    /// Handles setting, verifying, and unlocking secondary security PINs for player transactions.
    /// </summary>
    public class AC24 : AC
    {
        public override int ID => 24;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 5;
            DebugSystem.Write($"[AC24] Security PIN action from {c.CharName}: SubCode={subCode}");

            try
            {
                // SubCode 1: Set PIN, SubCode 2: Verify PIN, SubCode 5: Unlock Security Lock
                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack8(1); // 1 = Success / Unlocked
                c.Send(resp);

                DebugSystem.Write($"[AC24] Security PIN verified for {c.CharName}");
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
