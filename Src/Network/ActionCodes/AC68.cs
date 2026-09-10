using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 68: Companion Potential Training & Stat Point Allocation Protocol.
    /// Handles applying potential enhancement pills and spending stat points on active pets.
    /// </summary>
    public class AC68 : AC
    {
        public override int ID => 68;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC68] Pet training action from {c.CharName}: SubCode={subCode}");

            try
            {
                byte petSlot = p.Buffer.Length >= 7 ? p.Unpack8() : (byte)0;
                byte statType = p.Buffer.Length >= 8 ? p.Unpack8() : (byte)0;

                SendPacket resp = new SendPacket();
                resp.Pack8((byte)ID);
                resp.Pack8(subCode);
                resp.Pack8(petSlot);
                resp.Pack8(statType);
                resp.Pack8(1); // Success
                c.Send(resp);
            }
            catch (Exception ex)
            {
                DebugSystem.Write(new ExceptionData(ex));
            }
        }
    }
}
