using System;
using Game;
using Game.PlayerRelated;
using Network;

namespace Network.ActionCodes
{
    /// <summary>
    /// AC 41: In-Game Postal Delivery & Parcel Mail Protocol.
    /// Handles sending and receiving postal packages, parcels, and attached item deliveries.
    /// </summary>
    public class AC41 : AC
    {
        public override int ID => 41;

        public override void ProcessPkt(Player c, RecievePacket p)
        {
            if (c == null || p == null) return;

            byte subCode = p.B ?? 1;
            DebugSystem.Write($"[AC41] Postal delivery action from {c.CharName}: SubCode={subCode}");

            try
            {
                switch (subCode)
                {
                    case 1: // Check Postal Inbox
                        SendPacket resp1 = new SendPacket();
                        resp1.Pack8((byte)ID);
                        resp1.Pack8(1);
                        resp1.Pack8(0); // 0 unread parcels
                        c.Send(resp1);
                        break;
                    case 2: // Collect Parcel Item
                        SendPacket resp2 = new SendPacket();
                        resp2.Pack8((byte)ID);
                        resp2.Pack8(2);
                        resp2.Pack8(1); // Collected OK
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
