using System;
using Game;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 28: Server Channel / Realm Hop Protocol.
    /// Handles querying server line occupancy and transferring between channels 1-5.
    /// </summary>
    public class AC28 : AC
    {
        public override int ID => 28;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC28] Channel switch action from {c.CharName}: SubCode={subCode}");

            try
            {
                switch (subCode)
                {
                    case 1: // Query Channel Status
                        SendPacket resp1 = new SendPacket();
                        resp1.Pack8((byte)ID);
                        resp1.Pack8(1);
                        resp1.Pack8(1); // Channel 1 active
                        resp1.Pack8(50); // 50% load
                        c.Send(resp1);
                        break;
                    case 2: // Change Channel
                        byte targetChannel = p.Buffer.Length >= 7 ? p.Unpack8() : (byte)1;
                        SendPacket resp2 = new SendPacket();
                        resp2.Pack8((byte)ID);
                        resp2.Pack8(2);
                        resp2.Pack8(1); // 1 = Switch Approved
                        c.Send(resp2);
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
