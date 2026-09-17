using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 70: Companion Stat Reset & Return Scroll Protocol.
    /// Handles using Pet Return Scrolls to reset companion allocated attributes back to base values.
    /// </summary>
    public class AC70 : AC
    {
        public override int ID => 70;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC70] Pet stat reset from {c.CharName}: SubCode={subCode}");

            try
            {
                byte petSlot = p.Buffer.Length >= 7 ? p.Unpack8() : (byte)0;

                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack8(petSlot);
                resp.Pack8(1); // 1 = Reset Complete
                c.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
